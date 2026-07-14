using System;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

using Worksheet.Processing.Gates;
using Worksheet.Core.Buffers;
namespace Worksheet.Tests;

public sealed class ProcessingEngineOscilloscopeTests
{
    [Fact]
    public void TickProcessesOscilloscopeWhenWaveformBufferVersionChanges()
    {
        var dataStore = new DataStore();
        var dataSource = new DataSource();
        var chasmDataSource = new ChasmDataSource(dataSource);
        var oscilloscopeBuffer = new OscilloscopeBuffer();
        var engine = new TestProcessingEngine(
            dataStore,
            new PlotProcessor(chasmDataSource),
            new GateProcessor(chasmDataSource),
            oscilloscopeBuffer);
        var settings = new ScopeSettings
        {
            ChannelIndices = [0],
        };
        dataStore.UpsertSettings(settings);

        engine.RunTick();
        Assert.True(dataStore.TryGetProcessedData(settings.Id, out var first));
        var empty = Assert.IsType<OscilloscopeProcessedData>(first);
        Assert.True(empty.IsEmpty);

        oscilloscopeBuffer.Publish(new AnalogCapture([5, 6, 7], channelCount: 1, timestampCount: 3));

        engine.RunTick();

        Assert.True(dataStore.TryGetProcessedData(settings.Id, out var second));
        var waveform = Assert.IsType<OscilloscopeProcessedData>(second);
        Assert.False(waveform.IsEmpty);
        Assert.Equal([5, 6, 7], waveform.Signals[0]);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void TickThrottlesParameterPlotsWithoutBlockingOscilloscopePlots()
    {
        var dataStore = new DataStore();
        var dataSource = new DataSource();
        var chasmDataSource = new ChasmDataSource(dataSource);
        var oscilloscopeBuffer = new OscilloscopeBuffer();
        var engine = new TestProcessingEngine(
            dataStore,
            new PlotProcessor(chasmDataSource),
            new GateProcessor(chasmDataSource),
            oscilloscopeBuffer);
        var histogramSettings = new HistogramSettings
        {
            XFeature = 0,
        };
        var scopeSettings = new ScopeSettings
        {
            ChannelIndices = [0],
        };
        dataStore.UpsertSettings(histogramSettings);
        dataStore.UpsertSettings(scopeSettings);

        engine.RunTick();
        oscilloscopeBuffer.Publish(new AnalogCapture([1, 2, 3], channelCount: 1, timestampCount: 3));
        engine.RunTick();

        Assert.True(dataStore.TryGetProcessedData(histogramSettings.Id, out _));
        Assert.True(dataStore.TryGetProcessedData(scopeSettings.Id, out var scopeData));
        var waveform = Assert.IsType<OscilloscopeProcessedData>(scopeData);
        Assert.False(waveform.IsEmpty);
    }

    private sealed class TestProcessingEngine : ProcessingEngine
    {
        public TestProcessingEngine(
            DataStore dataStore,
            PlotProcessor plotProcessor,
            GateProcessor gateProcessor,
            OscilloscopeBuffer oscilloscopeBuffer)
            : base(
                dataStore,
                CreatePipelines(plotProcessor, oscilloscopeBuffer),
                gateProcessor,
                getDataVersion: () => 0,
                interval: TimeSpan.FromMilliseconds(1),
                parameterPlotInterval: TimeSpan.FromSeconds(10))
        {
        }

        public void RunTick()
        {
            Tick();
        }

        private static PlotPipelineRegistry CreatePipelines(PlotProcessor plotProcessor, OscilloscopeBuffer oscilloscopeBuffer)
        {
            var registry = new PlotPipelineRegistry();
            var parameterPipeline = new ParameterPlotPipeline(plotProcessor, () => 0, TimeSpan.FromSeconds(10));

            registry.Register(PlotType.Histogram, parameterPipeline);
            registry.Register(PlotType.Pseudocolor, parameterPipeline);
            registry.Register(PlotType.SpectralRibbon, parameterPipeline);
            registry.Register(
                PlotType.Oscilloscope,
                new OscilloscopePlotPipeline(
                    new OscilloscopePlotProcessor(oscilloscopeBuffer),
                    () => oscilloscopeBuffer.Version,
                    TimeSpan.Zero));

            return registry;
        }
    }
}
