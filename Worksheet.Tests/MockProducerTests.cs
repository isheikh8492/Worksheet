using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

using Worksheet.Core.Buffers;
namespace Worksheet.Tests;

public sealed class MockProducerTests
{
    [Fact]
    public async Task MaxThroughputModeEmitsColumnMajorBatches()
    {
        var options = ChasmOptions.Default with
        {
            BatchSize = 4,
            ChannelCapacityBatches = 2,
            SignalLayout = new SignalLayout(1, 1, 3),
            ThroughputMode = ProducerThroughputMode.MaxThroughput
        };

        using var producer = new MockProducer(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        producer.Start();
        IEventBatch batch = await producer.Reader.ReadAsync(timeout.Token);
        producer.Stop();

        var columnMajor = Assert.IsType<ColumnMajorEventBatch>(batch);
        Assert.Equal(options.BatchSize, columnMajor.Count);
        Assert.Equal(options.SignalLayout.SignalCount, columnMajor.SignalCount);
        Assert.Equal(options.BatchSize * options.SignalLayout.SignalCount, columnMajor.Values.Length);
    }

    [Fact]
    public async Task MockProducerPublishesAnalogCapturesWhenSinkIsAttached()
    {
        var options = ChasmOptions.Default with
        {
            AcquisitionInterval = TimeSpan.FromMilliseconds(1),
            BatchSize = 4,
            ChannelCapacityBatches = 2,
            SignalLayout = new SignalLayout(1, 1, 3)
        };
        var buffer = new OscilloscopeBuffer();

        using var producer = new MockProducer(options, buffer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        producer.Start();
        await producer.Reader.ReadAsync(timeout.Token);
        producer.Stop();

        Assert.True(buffer.Version > 0);
        Assert.True(buffer.TryGetLatest(out var capture));
        Assert.NotNull(capture);
        Assert.Equal(options.SignalLayout.ChannelCount, capture.ChannelCount);
        Assert.Equal(1750, capture.TimestampCount);
        Assert.Equal(capture.ChannelCount * capture.TimestampCount, capture.Values.Length);
    }

    [Fact]
    public async Task MockProducerPublishesAnalogCapturesForAllConfiguredChannels()
    {
        var options = ChasmOptions.Default with
        {
            AcquisitionInterval = TimeSpan.FromMilliseconds(1),
            BatchSize = 4,
            ChannelCapacityBatches = 2,
            SignalLayout = new SignalLayout(1, 1, 51)
        };
        var buffer = new OscilloscopeBuffer();

        using var producer = new MockProducer(options, buffer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        producer.Start();
        await producer.Reader.ReadAsync(timeout.Token);
        producer.Stop();

        Assert.True(buffer.TryGetLatest(out var capture));
        Assert.NotNull(capture);
        Assert.Equal(51, capture.ChannelCount);
        Assert.Equal(1750, capture.TimestampCount);
        Assert.Contains(capture.Values.Skip(50 * capture.TimestampCount).Take(capture.TimestampCount), value => value > 0.1);
    }

    [Fact]
    public async Task MockAnalogCapturesVaryWithoutPhaseScrolling()
    {
        var options = ChasmOptions.Default with
        {
            AcquisitionInterval = TimeSpan.FromMilliseconds(1),
            BatchSize = 4,
            ChannelCapacityBatches = 2,
            SignalLayout = new SignalLayout(1, 1, 3)
        };
        var sink = new CollectingAnalogCaptureSink();

        using var producer = new MockProducer(options, sink);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        producer.Start();
        while (sink.Count < 2)
        {
            await producer.Reader.ReadAsync(timeout.Token);
        }
        producer.Stop();

        var captures = sink.Captures;
        Assert.True(captures.Count >= 2);
        Assert.NotEqual(captures[0].Values, captures[1].Values);
        Assert.InRange(Math.Abs(FindPeakIndex(captures[0], channelIndex: 0) - FindPeakIndex(captures[1], channelIndex: 0)), 0, 15);
        Assert.Contains(captures[0].Values, value => value > 0.25);
        Assert.Contains(captures[0].Values.Take(80), value => Math.Abs(value) > 0.006);
    }

    private static int FindPeakIndex(AnalogCapture capture, int channelIndex)
    {
        int offset = channelIndex * capture.TimestampCount;
        int peakIndex = 0;
        double peakValue = double.NegativeInfinity;
        for (int i = 0; i < capture.TimestampCount; i++)
        {
            double value = capture.Values[offset + i];
            if (value > peakValue)
            {
                peakValue = value;
                peakIndex = i;
            }
        }

        return peakIndex;
    }

    private sealed class CollectingAnalogCaptureSink : IAnalogCaptureSink
    {
        private readonly object _lock = new();
        private readonly List<AnalogCapture> _captures = new();

        public int Count
        {
            get
            {
                lock (_lock)
                    return _captures.Count;
            }
        }

        public IReadOnlyList<AnalogCapture> Captures
        {
            get
            {
                lock (_lock)
                    return _captures.ToArray();
            }
        }

        public void Publish(AnalogCapture capture)
        {
            lock (_lock)
                _captures.Add(capture);
        }
    }
}
