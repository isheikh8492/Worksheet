using System;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

namespace Worksheet.Tests;

public sealed class PlotPipelineRegistryTests
{
    [Fact]
    public void GetRequiredReturnsRegisteredPipeline()
    {
        var registry = new PlotPipelineRegistry();
        var pipeline = new TestPipeline(TimeSpan.FromMilliseconds(50));

        registry.Register(PlotType.Histogram, pipeline);

        Assert.Same(pipeline, registry.GetRequired(PlotType.Histogram));
    }

    [Fact]
    public void RegisterRejectsDuplicatePlotType()
    {
        var registry = new PlotPipelineRegistry();
        registry.Register(PlotType.Histogram, new TestPipeline(TimeSpan.FromMilliseconds(50)));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(PlotType.Histogram, new TestPipeline(TimeSpan.FromMilliseconds(10))));
    }

    [Fact]
    public void GetRequiredRejectsUnregisteredPlotType()
    {
        var registry = new PlotPipelineRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.GetRequired(PlotType.Histogram));
    }

    [Fact]
    public void FastestCadenceReturnsMinimumRegisteredCadence()
    {
        var registry = new PlotPipelineRegistry();
        registry.Register(PlotType.Histogram, new TestPipeline(TimeSpan.FromMilliseconds(250)));
        registry.Register(PlotType.Oscilloscope, new TestPipeline(TimeSpan.FromMilliseconds(33)));

        Assert.Equal(TimeSpan.FromMilliseconds(33), registry.FastestCadence);
    }

    [Fact]
    public void ResetStatesDeduplicatesSharedPipeline()
    {
        var registry = new PlotPipelineRegistry();
        var pipeline = new TestPipeline(TimeSpan.FromMilliseconds(250));
        registry.Register(PlotType.Histogram, pipeline);
        registry.Register(PlotType.Pseudocolor, pipeline);

        registry.ResetStates();

        Assert.Equal(1, pipeline.ResetCount);
    }

    private sealed class TestPipeline : IPlotPipeline
    {
        public TestPipeline(TimeSpan cadence)
        {
            Cadence = cadence;
        }

        public int ResetCount { get; private set; }
        public TimeSpan Cadence { get; }
        public long Version => 0;

        public ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize)
        {
            return null;
        }

        public int GetSettingsHash(PlotSettings settings, RenderTargetSize targetSize)
        {
            return 0;
        }

        public void ResetState()
        {
            ResetCount++;
        }

        public (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats()
        {
            return (0, 0, 0);
        }
    }
}
