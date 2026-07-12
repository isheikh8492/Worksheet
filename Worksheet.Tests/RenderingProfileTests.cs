using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.Support;
using Worksheet.Views.Surfaces;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class RenderingProfileTests
{
    private const int ChannelCount = SignalLayout.DefaultChannelCount;
    private const int EventCount = 50_000;
    private const int Iterations = 25;

    private readonly ITestOutputHelper _output;

    public RenderingProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Profile")]
    public void ProfilePlotViewRendering()
    {
        LoadChannelConfiguration();

        var histogram = CreateProcessedData(CreateHistogramSettings());
        var pseudocolor = CreateProcessedData(CreatePseudocolorSettings());
        var spectralRibbon = CreateProcessedData(CreateSpectralRibbonSettings());
        var oscilloscope = CreateOscilloscopeData(channelCount: 51, sampleCount: 1750);

        RunOnStaThread(() =>
        {
            ProfileRender(PlotType.Histogram, histogram, "Histogram");
            ProfileRender(PlotType.Pseudocolor, pseudocolor, "Pseudocolor");
            ProfileRender(PlotType.SpectralRibbon, spectralRibbon, "SpectralRibbon");
            ProfileRender(PlotType.Oscilloscope, oscilloscope, "Oscilloscope signal");
        });
    }

    [Fact]
    [Trait("Category", "Profile")]
    public void ProfileOscilloscopeComputeAndRenderComparison()
    {
        const int channelCount = 51;
        const int sampleCount = 1750;
        var capture = CreateAnalogCapture(channelCount, sampleCount);
        int[] selectedChannels = Enumerable.Range(0, channelCount).ToArray();

        double rawSignalComputeMs = ProfileRawSignalCompute(capture, selectedChannels);
        var rawSignals = CopySelectedSignals(capture, selectedChannels);
        var signalData = CreateOscilloscopeData(rawSignals, selectedChannels, sampleCount);

        RunOnStaThread(() =>
        {
            double viewRenderMs = ProfileRender(PlotType.Oscilloscope, signalData, "Oscilloscope signal view");
            double directScottPlotRenderMs = ProfileDirectScottPlotSignalRender(signalData);

            string summary = $"Oscilloscope signal compute: {rawSignalComputeMs:F2} ms";
            string render = $"Oscilloscope render signal-view vs direct-ScottPlot-signal: {viewRenderMs:F2} ms vs {directScottPlotRenderMs:F2} ms";
            _output.WriteLine(summary);
            _output.WriteLine(render);
            Console.WriteLine(summary);
            Console.WriteLine(render);

            Assert.True(rawSignalComputeMs < 33, $"Signal compute should fit a 30 Hz frame budget, got {rawSignalComputeMs:F2} ms.");
            Assert.True(viewRenderMs < 33, $"Signal render should fit a 30 Hz frame budget, got {viewRenderMs:F2} ms.");
        });
    }

    private double ProfileRender(PlotType plotType, ProcessedPlotData data, string label)
    {
        var factory = new PlotFactory();
        var plot = factory.CreatePlot(520, 360, plotType, out PlotView view);
        var surface = new DynamicBitmap();
        surface.SetDataRect(new Rect(0, 0, 420, 260));
        view.AttachBitmapSurface(plot, surface);

        view.Render(plot, data);

        var elapsed = Measure(() => view.Render(plot, data), Iterations);
        double averageMs = elapsed.TotalMilliseconds / Iterations;
        double rendersPerSecond = 1000.0 / Math.Max(averageMs, 1e-9);
        string line = $"{label} render: {averageMs:F2} ms avg over {Iterations} iterations, {rendersPerSecond:F0} renders/sec";

        _output.WriteLine(line);
        Console.WriteLine(line);

        Assert.True(averageMs >= 0);
        return averageMs;
    }

    private double ProfileDirectScottPlotSignalRender(OscilloscopeProcessedData data)
    {
        var plot = new ScottPlot.WPF.WpfPlot
        {
            Width = 520,
            Height = 360,
        };

        RenderSignalOscilloscope(plot, data);

        var elapsed = Measure(() => RenderSignalOscilloscope(plot, data), Iterations);
        double averageMs = elapsed.TotalMilliseconds / Iterations;
        double rendersPerSecond = 1000.0 / Math.Max(averageMs, 1e-9);
        string line = $"Oscilloscope direct ScottPlot signal render: {averageMs:F2} ms avg over {Iterations} iterations, {rendersPerSecond:F0} renders/sec";

        _output.WriteLine(line);
        Console.WriteLine(line);

        return averageMs;
    }

    private double ProfileRawSignalCompute(AnalogCapture capture, int[] selectedChannels)
    {
        CopySelectedSignals(capture, selectedChannels);

        var elapsed = Measure(() => CopySelectedSignals(capture, selectedChannels), Iterations);
        double averageMs = elapsed.TotalMilliseconds / Iterations;
        string line = $"Oscilloscope raw signal compute: {averageMs:F2} ms avg over {Iterations} iterations";

        _output.WriteLine(line);
        Console.WriteLine(line);

        return averageMs;
    }

    private static void RenderSignalOscilloscope(ScottPlot.WPF.WpfPlot plot, OscilloscopeProcessedData data)
    {
        plot.Plot.Clear();
        plot.Plot.YLabel("Voltage (V)");
        plot.Plot.XLabel("Time (samples)");

        for (int i = 0; i < data.Signals.Length; i++)
            plot.Plot.Add.Signal(data.Signals[i]);

        plot.Plot.Axes.SetLimitsX(0, Math.Max(1, data.TimestampCount - 1));
        plot.Plot.Axes.SetLimitsY(-0.18, 0.52);
        plot.Refresh();
    }

    private static TimeSpan Measure(Action action, int iterations)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            action();

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static ProcessedPlotData CreateProcessedData(PlotSettings settings)
    {
        var source = new DataSource(windowCapacity: EventCount);
        source.AppendBatch(CreateBatch(EventCount), EventCount);

        var processor = new PlotProcessor(new ChasmDataSource(source));
        var processed = processor.Process(settings);

        Assert.NotNull(processed);
        return processed;
    }

    private static PlotSettings CreateHistogramSettings() =>
        new HistogramSettings
        {
            BinCount = 256,
            XFeature = 0,
            XAxisScaleType = AxisScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static PlotSettings CreatePseudocolorSettings() =>
        new PseudocolorSettings
        {
            BinCount = 256,
            XFeature = 0,
            YFeature = 1,
            XAxisScaleType = AxisScaleType.Logarithmic,
            YAxisScaleType = AxisScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static PlotSettings CreateSpectralRibbonSettings() =>
        new SpectralRibbonSettings
        {
            BinCount = 256,
            YAxisScaleType = AxisScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static OscilloscopeProcessedData CreateOscilloscopeData(int channelCount, int sampleCount)
    {
        var capture = CreateAnalogCapture(channelCount, sampleCount);
        int[] selectedChannels = Enumerable.Range(0, channelCount).ToArray();
        var signals = CopySelectedSignals(capture, selectedChannels);

        return CreateOscilloscopeData(signals, selectedChannels, sampleCount);
    }

    private static OscilloscopeProcessedData CreateOscilloscopeData(double[][] signals, int[] selectedChannels, int sampleCount) =>
        new(
            Guid.NewGuid(),
            signals,
            selectedChannels,
            sampleCount,
            isEmpty: false);

    private static AnalogCapture CreateAnalogCapture(int channelCount, int sampleCount)
    {
        var values = new double[channelCount * sampleCount];
        for (int channel = 0; channel < channelCount; channel++)
        {
            double center = 360 + channel * 42;
            double amplitude = 0.34 - channel * 0.035;
            for (int i = 0; i < sampleCount; i++)
            {
                double baseline = Math.Sin(i * 0.014 + channel) * 0.006;
                double main = amplitude * Gaussian(i, center, 24);
                double undershoot = -0.06 * Gaussian(i, center + 68, 36);
                double tail = 0.028 * Gaussian(i, center + 165, 90);
                values[(channel * sampleCount) + i] = baseline + main + undershoot + tail;
            }
        }

        return new AnalogCapture(values, channelCount, sampleCount);
    }

    private static double[][] CopySelectedSignals(AnalogCapture capture, int[] selectedChannels)
    {
        var signals = new double[selectedChannels.Length][];
        for (int i = 0; i < selectedChannels.Length; i++)
        {
            int channelIndex = selectedChannels[i];
            var signal = new double[capture.TimestampCount];
            Array.Copy(capture.Values, channelIndex * capture.TimestampCount, signal, 0, capture.TimestampCount);
            signals[i] = signal;
        }

        return signals;
    }

    private static double Gaussian(double value, double center, double sigma)
    {
        double z = (value - center) / sigma;
        return Math.Exp(-0.5 * z * z);
    }

    private static double[][] CreateBatch(int count)
    {
        var channels = new double[ChannelCount][];
        for (int c = 0; c < channels.Length; c++)
        {
            var values = new double[count];
            for (int e = 0; e < values.Length; e++)
                values[e] = 1 + (((e + 1) * (c + 3) * 7919) % 100_000_000);

            channels[c] = values;
        }

        return channels;
    }

    private static void LoadChannelConfiguration()
    {
        string channelConfigPath = Path.Combine(FindRepoRoot(), "Worksheet.App", "channels.json");

        Assert.True(File.Exists(channelConfigPath), $"Channel config not found: {channelConfigPath}");
        FeatureSelectionStrategy.LoadChannelSettings(channelConfigPath);
    }

    private static string FindRepoRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory, Path.GetDirectoryName(sourceFilePath) })
        {
            if (string.IsNullOrWhiteSpace(startPath))
                continue;

            var directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Worksheet.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not find repo root containing Worksheet.sln.");
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception?.Throw();
    }
}
