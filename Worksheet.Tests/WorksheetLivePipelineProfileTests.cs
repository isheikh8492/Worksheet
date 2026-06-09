using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.Support;
using Worksheet.Views.Surfaces;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class WorksheetLivePipelineProfileTests
{
    private static readonly TimeSpan ProcessingInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RenderingInterval = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan OscilloscopeInterval = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan WarmupDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SampleDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    private readonly ITestOutputHelper _output;

    public WorksheetLivePipelineProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Profile")]
    public void ProfileLiveWorksheetPipelineStatusMetrics()
    {
        LoadChannelConfiguration();

        RunOnStaThread(() =>
        {
            using var session = new ViewportSession(
                Dispatcher.CurrentDispatcher,
                ProcessingInterval,
                RenderingInterval,
                ChasmOptions.Balanced50k,
                OscilloscopeInterval);

            session.Start();
            RegisterRepresentativePlots(session);
            session.SetStreamingEnabled(true);

            try
            {
                PumpDispatcherFor(WarmupDuration);

                var samples = new List<ProcessingStatusSnapshot>();
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < SampleDuration)
                {
                    PumpDispatcherFor(SampleInterval);
                    samples.Add(session.GetProcessingStatusSnapshot());
                }

                var final = samples.Count > 0
                    ? samples[^1]
                    : session.GetProcessingStatusSnapshot();

                WriteLiveSummary(final, samples);

                Assert.True(final.EventRatePerSecond > 0, "Live worksheet profile should ingest events while streaming.");
                Assert.True(final.BufferedEventCount > 0, "Live worksheet profile should retain events in DataSource.");
                Assert.True(final.HistogramAverageComputeMs > 0, "Histogram should process at least once.");
                Assert.True(final.PseudocolorAverageComputeMs > 0, "Pseudocolor should process at least once.");
                Assert.True(final.SpectralRibbonAverageComputeMs > 0, "Spectral ribbon should process at least once.");
                Assert.True(final.OscilloscopeAverageComputeMs > 0, "Oscilloscope should process at least once.");
                Assert.True(final.HistogramAverageRenderMs > 0, "Histogram should render at least once.");
                Assert.True(final.PseudocolorAverageRenderMs > 0, "Pseudocolor should render at least once.");
                Assert.True(final.SpectralRibbonAverageRenderMs > 0, "Spectral ribbon should render at least once.");
                Assert.True(final.OscilloscopeAverageRenderMs > 0, "Oscilloscope should render at least once.");
            }
            finally
            {
                session.SetStreamingEnabled(false);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(100));
            }
        });
    }

    private static void RegisterRepresentativePlots(ViewportSession session)
    {
        var factory = new PlotFactory();

        RegisterPlot(session, factory, PlotType.Histogram, dataWidth: 210, dataHeight: 150);
        RegisterPlot(session, factory, PlotType.Pseudocolor, dataWidth: 220, dataHeight: 220);
        RegisterPlot(session, factory, PlotType.SpectralRibbon, dataWidth: 890, dataHeight: 210);
        RegisterPlot(session, factory, PlotType.Oscilloscope, dataWidth: 470, dataHeight: 210, settings =>
        {
            settings.OscilloscopeChannelIndices = new[] { 0, 1, 2, 3 };
        });
    }

    private static void RegisterPlot(
        ViewportSession session,
        PlotFactory factory,
        PlotType plotType,
        int dataWidth,
        int dataHeight,
        Action<PlotSettings>? configure = null)
    {
        var plot = factory.CreatePlot(plotType, out PlotView plotView);
        var settings = plotView.Settings;
        configure?.Invoke(settings);

        var surface = new DynamicBitmap();
        surface.SetDataRect(new Rect(0, 0, dataWidth, dataHeight));
        plotView.AttachBitmapSurface(plot, surface, new Border { Visibility = Visibility.Collapsed });

        session.RegisterPlot(settings);
        session.DataStore.SetRenderTargetSize(settings.Id, new RenderTargetSize(dataWidth, dataHeight));
        session.RegisterRenderTarget(plot, plotView, settings);
    }

    private void WriteLiveSummary(ProcessingStatusSnapshot final, IReadOnlyList<ProcessingStatusSnapshot> samples)
    {
        double averageEventRate = samples.Count == 0 ? final.EventRatePerSecond : Average(samples, s => s.EventRatePerSecond);
        string line = "Live worksheet profile status: " +
            $"event rate final {final.EventRatePerSecond:F0} ev/s, " +
            $"event rate avg {averageEventRate:F0} ev/s, " +
            $"buffered {final.BufferedEventCount:N0}, " +
            $"compute ms hist {final.HistogramAverageComputeMs:F2}, " +
            $"pc {final.PseudocolorAverageComputeMs:F2}, " +
            $"sr {final.SpectralRibbonAverageComputeMs:F2}, " +
            $"scope {final.OscilloscopeAverageComputeMs:F2}, " +
            $"render ms hist {final.HistogramAverageRenderMs:F2}, " +
            $"pc {final.PseudocolorAverageRenderMs:F2}, " +
            $"sr {final.SpectralRibbonAverageRenderMs:F2}, " +
            $"scope {final.OscilloscopeAverageRenderMs:F2}, " +
            $"delta {final.DeltaAppliedCount}, " +
            $"full {final.FullRebuildCount}, " +
            $"gaps {final.SequenceGapCount}";

        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private static double Average(IReadOnlyList<ProcessingStatusSnapshot> samples, Func<ProcessingStatusSnapshot, double> selector)
    {
        double sum = 0;
        for (int i = 0; i < samples.Count; i++)
            sum += selector(samples[i]);

        return sum / Math.Max(1, samples.Count);
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration,
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };

        timer.Start();
        Dispatcher.PushFrame(frame);
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
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception?.Throw();
    }
}
