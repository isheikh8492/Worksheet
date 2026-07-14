using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Worksheet.Core.Services;
using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public sealed class MockProducer : IProducer, IDisposable
    {
        private const int PopulationCount = 4;
        private const double MaxValue = 100_000_000d;
        private const int MockAnalogTimestampCount = 1750;
        private const int MaxThroughputBurstBatches = 16;
        private static readonly TimeSpan StopWaitTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan MaxThroughputRestInterval = TimeSpan.FromMilliseconds(1);

        private readonly ChasmOptions _options;
        private readonly Channel<IEventBatch> _channel;
        private readonly IAnalogCaptureSink? _analogCaptureSink;
        private readonly int _signalCount;
        private readonly int _analogChannelCount;

        private CancellationTokenSource? _cts;
        private Task? _task;
        private volatile bool _running;

        private readonly Random _rng;
        private readonly double[,] _logMeans;
        private readonly double[,] _logSigmas;
        private readonly double[] _populationWeights = new double[PopulationCount];
        private int _analogCaptureSequence;

        public MockProducer(ChasmOptions? options = null, IAnalogCaptureSink? analogCaptureSink = null)
        {
            _options = options ?? ChasmOptions.Default;
            _analogCaptureSink = analogCaptureSink;
            _signalCount = _options.SignalLayout.SignalCount;
            _analogChannelCount = _options.SignalLayout.ChannelCount;
            _logMeans = new double[_signalCount, PopulationCount];
            _logSigmas = new double[_signalCount, PopulationCount];

            var bounded = new BoundedChannelOptions(_options.ChannelCapacityBatches)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            };
            _channel = Channel.CreateBounded<IEventBatch>(bounded);

            _rng = new Random(_options.Seed);
            InitializePopulationModel();
        }

        public ChannelReader<IEventBatch> Reader => _channel.Reader;

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _cts = new CancellationTokenSource();
            _task = Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _cts?.Cancel();

            // Prevent stale batches from being consumed after restart.
            while (_channel.Reader.TryRead(out _)) { }

            StoppableTask.Observe(_task, StopWaitTimeout, "MockProducer.Stop");
            _cts?.Dispose();
            _cts = null;
            _task = null;
        }

        public void Dispose() => Stop();

        private async Task RunAsync(CancellationToken token)
        {
            if (_options.ThroughputMode == ProducerThroughputMode.MaxThroughput)
                await RunMaxThroughputAsync(token).ConfigureAwait(false);
            else
                await RunFixedRateAsync(token).ConfigureAwait(false);
        }

        private async Task RunFixedRateAsync(CancellationToken token)
        {
            try
            {
                using var timer = new PeriodicTimer(_options.AcquisitionInterval);
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    if (!_running)
                        continue;

                    var batch = GenerateBatch(_options.BatchSize);
                    _channel.Writer.TryWrite(batch);
                    PublishMockAnalogCapture();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "MockProducer.RunAsync");
            }
        }

        private async Task RunMaxThroughputAsync(CancellationToken token)
        {
            try
            {
                int batchesSinceRest = 0;
                while (!token.IsCancellationRequested)
                {
                    if (!_running)
                    {
                        await Task.Delay(1, token).ConfigureAwait(false);
                        continue;
                    }

                    var batch = GenerateColumnMajorBatch(_options.BatchSize);
                    _channel.Writer.TryWrite(batch);
                    PublishMockAnalogCapture();

                    batchesSinceRest++;
                    if (batchesSinceRest >= MaxThroughputBurstBatches)
                    {
                        batchesSinceRest = 0;
                        await Task.Delay(MaxThroughputRestInterval, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "MockProducer.RunMaxThroughputAsync");
            }
        }

        private EventBatch GenerateBatch(int count)
        {
            var channels = new double[_signalCount][];
            for (int c = 0; c < _signalCount; c++)
                channels[c] = new double[count];

            for (int e = 0; e < count; e++)
            {
                int pop = SamplePopulation();
                for (int c = 0; c < _signalCount; c++)
                {
                    double logMean = _logMeans[c, pop];
                    double logSigma = _logSigmas[c, pop];

                    double z = NextStandardNormal();
                    double log10Value = logMean + logSigma * z;
                    double value = Math.Pow(10, log10Value);

                    if (double.IsNaN(value) || double.IsInfinity(value))
                        value = MaxValue;
                    else if (value < 0)
                        value = 0;
                    else if (value > MaxValue)
                        value = MaxValue;

                    channels[c][e] = value;
                }
            }

            return new EventBatch(count, channels, _options.SignalLayout);
        }

        private ColumnMajorEventBatch GenerateColumnMajorBatch(int count)
        {
            var values = new double[_signalCount * count];

            for (int e = 0; e < count; e++)
            {
                int pop = SamplePopulation();
                for (int c = 0; c < _signalCount; c++)
                {
                    double logMean = _logMeans[c, pop];
                    double logSigma = _logSigmas[c, pop];

                    double z = NextStandardNormal();
                    double log10Value = logMean + logSigma * z;
                    double value = Math.Pow(10, log10Value);

                    if (double.IsNaN(value) || double.IsInfinity(value))
                        value = MaxValue;
                    else if (value < 0)
                        value = 0;
                    else if (value > MaxValue)
                        value = MaxValue;

                    values[(c * count) + e] = value;
                }
            }

            return new ColumnMajorEventBatch(count, values, _options.SignalLayout);
        }

        private void PublishMockAnalogCapture()
        {
            if (_analogCaptureSink == null)
                return;

            try
            {
                _analogCaptureSink.Publish(GenerateAnalogCapture());
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "MockProducer.PublishMockAnalogCapture");
            }
        }

        private AnalogCapture GenerateAnalogCapture()
        {
            var values = new double[_analogChannelCount * MockAnalogTimestampCount];
            int sequence = _analogCaptureSequence++;

            for (int channel = 0; channel < _analogChannelCount; channel++)
            {
                int lane = channel % 8;
                int bank = channel / 8;
                double baseAmplitude = 0.22 + 0.18 * ((lane % 4) / 3.0);
                double amplitude = baseAmplitude * (1.0 + 0.14 * StableNoise(channel + 17, sequence));
                double center = 260 + lane * 170 + (bank % 3) * 36 + 6.0 * StableNoise(channel + 29, sequence);
                double width = (20 + (lane % 5) * 5) * (1.0 + 0.12 * StableNoise(channel + 41, sequence));
                double undershootCenter = center + 62;
                double recoveryCenter = center + 150;
                double baselineOffset = 0.006 * StableNoise(channel + 53, sequence);
                double baselinePhase = 2 * Math.PI * StableNoise(channel + 67, sequence);
                int offset = channel * MockAnalogTimestampCount;

                for (int t = 0; t < MockAnalogTimestampCount; t++)
                {
                    double baseline = baselineOffset + 0.012 * Math.Sin((2 * Math.PI * t / 430.0) + baselinePhase);
                    double pulse = amplitude * Gaussian(t, center, width);
                    double undershoot = -amplitude * 0.24 * Gaussian(t, undershootCenter, width * 1.7);
                    double recovery = amplitude * 0.09 * Gaussian(t, recoveryCenter, width * 3.0);
                    double noise = 0.014 * StableNoise(channel + sequence * 31, t);
                    values[offset + t] = baseline + pulse + undershoot + recovery + noise;
                }
            }

            return new AnalogCapture(values, _analogChannelCount, MockAnalogTimestampCount);
        }

        private static double Gaussian(double x, double center, double sigma)
        {
            double z = (x - center) / sigma;
            return Math.Exp(-0.5 * z * z);
        }

        private static double StableNoise(int channel, int timestamp)
        {
            unchecked
            {
                uint x = (uint)((channel + 1) * 0x9E3779B9) ^ (uint)(timestamp * 0x85EBCA6B);
                x ^= x >> 16;
                x *= 0x7FEB352D;
                x ^= x >> 15;
                x *= 0x846CA68B;
                x ^= x >> 16;
                return (x / (double)uint.MaxValue * 2.0) - 1.0;
            }
        }

        private void InitializePopulationModel()
        {
            double sum = 0;
            for (int p = 0; p < PopulationCount; p++)
            {
                _populationWeights[p] = 0.15 + _rng.NextDouble() * 0.35;
                sum += _populationWeights[p];
            }
            for (int p = 0; p < PopulationCount; p++)
                _populationWeights[p] /= sum;

            var distinctPeakCounts = new int[_signalCount];
            for (int i = 0; i < _signalCount; i++)
                distinctPeakCounts[i] = 1 + (i % 4);
            Shuffle(distinctPeakCounts);

            for (int c = 0; c < _signalCount; c++)
            {
                int peakCount = distinctPeakCounts[c];
                var (distinctMeans, distinctSigmas) = CreateDistinctPeaks(peakCount);

                int[] popToPeak = peakCount switch
                {
                    1 => new[] { 0, 0, 0, 0 },
                    2 => new[] { 0, 1, 0, 1 },
                    3 => new[] { 0, 1, 2, 0 },
                    _ => new[] { 0, 1, 2, 3 },
                };

                if (peakCount >= 2)
                {
                    int[] peakIds = new int[peakCount];
                    for (int i = 0; i < peakCount; i++) peakIds[i] = i;
                    Shuffle(peakIds);
                    for (int p = 0; p < PopulationCount; p++)
                        popToPeak[p] = peakIds[popToPeak[p]];
                }

                for (int p = 0; p < PopulationCount; p++)
                {
                    int peakIndex = popToPeak[p];
                    _logMeans[c, p] = distinctMeans[peakIndex];
                    _logSigmas[c, p] = distinctSigmas[peakIndex];
                }
            }
        }

        private (double[] means, double[] sigmas) CreateDistinctPeaks(int peakCount)
        {
            const double minLog = 0.3;
            const double maxLog = 7.7;

            var means = new double[peakCount];
            var sigmas = new double[peakCount];

            if (peakCount == 1)
            {
                means[0] = 1.5 + _rng.NextDouble() * 5.0;
                sigmas[0] = 0.12 + _rng.NextDouble() * 0.10;
                return (means, sigmas);
            }

            double step = (maxLog - minLog) / peakCount;
            for (int i = 0; i < peakCount; i++)
            {
                double center = minLog + (i + 0.5) * step;
                double jitter = (_rng.NextDouble() * 2.0 - 1.0) * (step * 0.15);
                means[i] = Math.Clamp(center + jitter, minLog, maxLog);
                sigmas[i] = 0.08 + _rng.NextDouble() * 0.16;
            }

            Shuffle(means, sigmas);
            return (means, sigmas);
        }

        private int SamplePopulation()
        {
            double r = _rng.NextDouble();
            double cdf = 0;
            for (int p = 0; p < PopulationCount; p++)
            {
                cdf += _populationWeights[p];
                if (r <= cdf)
                    return p;
            }
            return PopulationCount - 1;
        }

        private double NextStandardNormal()
        {
            double u1 = _rng.NextDouble();
            if (u1 < double.Epsilon) u1 = double.Epsilon;
            double u2 = _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private void Shuffle(int[] values)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private void Shuffle(double[] means, double[] sigmas)
        {
            for (int i = means.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (means[i], means[j]) = (means[j], means[i]);
                (sigmas[i], sigmas[j]) = (sigmas[j], sigmas[i]);
            }
        }
    }
}

