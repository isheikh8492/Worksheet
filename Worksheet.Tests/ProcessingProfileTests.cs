using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class ProcessingProfileTests
{
    private const int ChannelCount = SignalLayout.DefaultChannelCount;
    private const int InitialEventCount = 50_000;
    private const int DeltaEventCount = 5_000;
    private const int LargeLayoutEventCount = 2_000;

    private readonly ITestOutputHelper _output;

    public ProcessingProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Profile")]
    public void ProfilePlotProcessorFullRebuildAndDeltaProcessing()
    {
        LoadChannelConfiguration();

        ProfilePlot(CreateHistogramSettings(), "Histogram", expectData: data => Assert.IsType<HistogramProcessedData>(data));
        ProfilePlot(CreatePseudocolorSettings(), "Pseudocolor", expectData: data => Assert.IsType<HeatmapProcessedData>(data));
        ProfilePlot(CreateSpectralRibbonSettings(), "SpectralRibbon", expectData: data => Assert.IsType<SpectralRibbonProcessedData>(data));
    }

    [Fact]
    [Trait("Category", "Profile")]
    public void ProfileLargeSignalLayoutAppendAndSelectedSnapshotRead()
    {
        var layout = new SignalLayout(6, 9, 60);
        int signalIndex = layout.ToIndex(2, 4, 17);
        var source = new DataSource(layout, windowCapacity: LargeLayoutEventCount);
        var batch = CreateBatch(LargeLayoutEventCount, offset: 0, signalCount: layout.SignalCount);

        batch[signalIndex][0] = 1_337;
        batch[signalIndex][LargeLayoutEventCount - 1] = 1_340;

        var append = MeasureAction(() => source.AppendBatch(batch, LargeLayoutEventCount));

        var snapshot = source.GetSnapshot(signalIndex);
        Assert.Equal(1_337, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(1_340, snapshot.Values[snapshot.PhysicalIndexForSequence(LargeLayoutEventCount - 1)]);

        WriteTiming("LFC 6x9x60 append", "batch", LargeLayoutEventCount, append);
    }

    private void ProfilePlot(
        PlotSettings settings,
        string label,
        Action<ProcessedPlotData> expectData)
    {
        var source = new DataSource(windowCapacity: InitialEventCount + DeltaEventCount);
        source.AppendBatch(CreateBatch(InitialEventCount, offset: 0), InitialEventCount);
        var processor = new PlotProcessor(new ChasmDataSource(source));

        var full = Measure(() => processor.Process(settings));
        Assert.NotNull(full.Result);
        expectData(full.Result);

        source.AppendBatch(CreateBatch(DeltaEventCount, offset: InitialEventCount), DeltaEventCount);

        var delta = Measure(() => processor.Process(settings));
        Assert.NotNull(delta.Result);
        expectData(delta.Result);

        WriteTiming(label, "full", InitialEventCount, full.Elapsed);
        WriteTiming(label, "delta", DeltaEventCount, delta.Elapsed);
    }

    private static (ProcessedPlotData? Result, TimeSpan Elapsed) Measure(Func<ProcessedPlotData?> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        var result = action();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    private static TimeSpan MeasureAction(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private void WriteTiming(string label, string phase, int eventCount, TimeSpan elapsed)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 1e-9);
        double eventsPerSecond = eventCount / seconds;
        string line = $"{label} {phase}: {elapsed.TotalMilliseconds:F2} ms, {eventsPerSecond:F0} events/sec";

        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private static PlotSettings CreateHistogramSettings() =>
        new HistogramSettings
        {
            BinCount = 256,
            XFeature = 0,
            XAxisScaleType = ScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static PlotSettings CreatePseudocolorSettings() =>
        new PseudocolorSettings
        {
            BinCount = 256,
            XFeature = 0,
            YFeature = 1,
            XAxisScaleType = ScaleType.Logarithmic,
            YAxisScaleType = ScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static PlotSettings CreateSpectralRibbonSettings() =>
        new SpectralRibbonSettings
        {
            BinCount = 256,
            YAxisScaleType = ScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static double[][] CreateBatch(int count, int offset, int signalCount = ChannelCount)
    {
        var channels = new double[signalCount][];
        for (int c = 0; c < channels.Length; c++)
        {
            var values = new double[count];
            for (int e = 0; e < values.Length; e++)
            {
                int sequence = offset + e + 1;
                values[e] = 1 + ((sequence * (c + 3) * 7919) % 100_000_000);
            }

            channels[c] = values;
        }

        return channels;
    }

    private static void LoadChannelConfiguration()
    {
        string repoRoot = FindRepoRoot();
        string channelConfigPath = Path.Combine(repoRoot, "Worksheet.App", "channels.json");

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
}
