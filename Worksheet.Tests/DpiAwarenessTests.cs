using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Services;
using Worksheet.Views.Support;
using Worksheet.Views.Surfaces;
using Xunit;

namespace Worksheet.Tests;

public sealed class DpiAwarenessTests
{
    [Theory]
    [InlineData(1.0, 1.0, 400, 300, 400, 300)]
    [InlineData(1.25, 1.25, 400, 300, 500, 375)]
    [InlineData(1.5, 1.5, 400, 300, 600, 450)]
    [InlineData(2.0, 2.0, 400, 300, 800, 600)]
    public void DpiContextConvertsDipsToPhysicalPixels(
        double scaleX,
        double scaleY,
        double widthDip,
        double heightDip,
        int expectedPixelWidth,
        int expectedPixelHeight)
    {
        var dpi = new DpiContext(scaleX, scaleY);

        Assert.Equal(expectedPixelWidth, dpi.DipWidthToPixels(widthDip));
        Assert.Equal(expectedPixelHeight, dpi.DipHeightToPixels(heightDip));
    }

    [Fact]
    public void DpiContextConvertsScottPlotPixelsToWpfDips()
    {
        var dpi = new DpiContext(1.5, 2.0);

        var rect = dpi.PixelsToDips(new Rect(30, 40, 600, 400));

        Assert.Equal(new Rect(20, 20, 400, 200), rect);
    }

    [Fact]
    public void DynamicBitmapPublishesTargetPixelSizeFromDataRectDips()
    {
        RunOnStaThread(() =>
        {
            var surface = new DynamicBitmap();

            surface.SetDataRect(new Rect(10, 20, 400, 300), new DpiContext(1.5, 1.25));

            Assert.Equal(new Rect(10, 20, 400, 300), surface.DataRect);
            Assert.Equal(600, surface.TargetWidth);
            Assert.Equal(375, surface.TargetHeight);
        });
    }

    [Fact]
    public void PseudocolorProcessorUsesRequestedRenderTargetSize()
    {
        var settings = CreatePseudocolorSettings();
        var source = CreateSource(settings);
        var processor = new PlotProcessor(new ChasmDataSource(source));

        var processed = Assert.IsType<HeatmapProcessedData>(
            processor.Process(settings, new RenderTargetSize(640, 360)));

        Assert.Equal(640, processed.PixelWidth);
        Assert.Equal(360, processed.PixelHeight);
        Assert.Equal(640 * 360 * 4, processed.PixelBuffer.Length);
    }

    [Fact]
    public void SpectralRibbonProcessorUsesRequestedRenderTargetSize()
    {
        string channelConfigPath = Path.Combine(Path.GetTempPath(), $"worksheet-channels-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(channelConfigPath, """
            {
              "channels": {
                "daq0.0": "405nm",
                "daq0.1": "488nm",
                "daq0.2": "640nm"
              }
            }
            """);
            FeatureSelectionStrategy.LoadChannelSettings(channelConfigPath);

            var settings = CreateSpectralRibbonSettings();
            var source = CreateSource(settings);
            var processor = new PlotProcessor(new ChasmDataSource(source));

            var processed = Assert.IsType<SpectralRibbonProcessedData>(
                processor.Process(settings, new RenderTargetSize(320, 240)));

            Assert.Equal(320, processed.PixelWidth);
            Assert.Equal(240, processed.PixelHeight);
            Assert.Equal(320 * 240 * 4, processed.PixelBuffer.Length);
        }
        finally
        {
            if (File.Exists(channelConfigPath))
                File.Delete(channelConfigPath);
        }
    }

    private static DataSource CreateSource(PlotSettings settings)
    {
        var source = new DataSource(windowCapacity: 32);
        var batch = new double[SignalLayout.Default.SignalCount][];
        for (int c = 0; c < batch.Length; c++)
        {
            batch[c] = new double[32];
            for (int e = 0; e < batch[c].Length; e++)
                batch[c][e] = 1 + ((e + 1) * (c + 1));
        }

        source.AppendBatch(batch, count: 32);
        return source;
    }

    private static PlotSettings CreatePseudocolorSettings() =>
        new PseudocolorSettings
        {
            BinCount = 64,
            XFeature = 0,
            YFeature = 1,
            XAxisScaleType = ScaleType.Linear,
            YAxisScaleType = ScaleType.Linear,
            MinValue = 1,
            MaxValue = 100,
        };

    private static PlotSettings CreateSpectralRibbonSettings() =>
        new SpectralRibbonSettings
        {
            BinCount = 64,
            YAxisScaleType = ScaleType.Linear,
            MinValue = 1,
            MaxValue = 100,
        };

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
