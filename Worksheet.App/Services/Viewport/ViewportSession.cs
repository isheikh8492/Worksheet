using System;
using System.Windows.Threading;
using ScottPlot.WPF;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Core.Models.Gates;

using Worksheet.Chasm;
using Worksheet.Processing.Gates;
namespace Worksheet.App.Services.Viewport
{
    public class ViewportSession : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly DataStore _dataStore;
        private readonly DataSource _dataSource;
        private readonly OscilloscopeBuffer _oscilloscopeBuffer;
        private readonly ChasmDataSource _chasmDataSource;
        private readonly ChasmEngine _chasm;
        private readonly PlotProcessor _plotProcessor;
        private readonly OscilloscopePlotProcessor _oscilloscopePlotProcessor;
        private readonly GateProcessor _gateProcessor;
        private readonly ProcessingEngine _processingEngine;
        private readonly RenderingEngine _renderingEngine;
        private readonly FeatureSelectionStrategy _featureSelection;
        private readonly ChasmOptions _chasmOptions;
        private readonly object _eventRateLock = new();
        private long _lastRateEventCount;
        private DateTime _lastRateSampleUtc = DateTime.UtcNow;
        private double _lastEventRatePerSecond;

        public ViewportSession(
            Dispatcher dispatcher,
            TimeSpan processingInterval,
            TimeSpan renderingInterval,
            ChasmOptions? chasmOptions = null,
            TimeSpan? oscilloscopeProcessingInterval = null)
        {
            _dispatcher = dispatcher;
            _dataStore = new DataStore();
            _chasmOptions = chasmOptions ?? ChasmOptions.Default;
            _dataSource = new DataSource(_chasmOptions.SignalLayout, _chasmOptions.WindowCapacityEvents);
            _oscilloscopeBuffer = new OscilloscopeBuffer();
            var pipeline = ChasmPipelineFactory.CreateMock(_dataSource, _chasmOptions, _oscilloscopeBuffer);
            _chasmDataSource = pipeline.ChasmDataSource;
            _chasm = pipeline.ChasmEngine;

            _plotProcessor = new PlotProcessor(_chasmDataSource);
            _oscilloscopePlotProcessor = new OscilloscopePlotProcessor(_oscilloscopeBuffer);
            _gateProcessor = new GateProcessor(_chasmDataSource);
            var scopeInterval = oscilloscopeProcessingInterval ?? TimeSpan.FromMilliseconds(33);
            var pipelines = CreatePlotPipelines(processingInterval, scopeInterval);
            _processingEngine = new ProcessingEngine(
                _dataStore,
                pipelines,
                _gateProcessor,
                () => _chasm.DataVersion,
                pipelines.FastestCadence,
                parameterPlotInterval: processingInterval);
            _renderingEngine = new RenderingEngine(_dataStore, dispatcher, renderingInterval);
            _featureSelection = new FeatureSelectionStrategy();
        }

        public event EventHandler? MemoryCleared;

        public DataStore DataStore => _dataStore;
        public FeatureSelectionStrategy FeatureSelection => _featureSelection;
        public bool IsStreamingEnabled => _chasm.IsStreamingEnabled;
        public int WindowCapacity => _chasm.WindowCapacity;

        private PlotPipelineRegistry CreatePlotPipelines(TimeSpan parameterCadence, TimeSpan oscilloscopeCadence)
        {
            var registry = new PlotPipelineRegistry();
            var parameterPipeline = new ParameterPlotPipeline(_plotProcessor, () => _chasm.DataVersion, parameterCadence);

            registry.Register(PlotType.Histogram, parameterPipeline);
            registry.Register(PlotType.Pseudocolor, parameterPipeline);
            registry.Register(PlotType.SpectralRibbon, parameterPipeline);
            registry.Register(PlotType.Oscilloscope, new OscilloscopePlotPipeline(_oscilloscopePlotProcessor, () => _oscilloscopeBuffer.Version, oscilloscopeCadence));

            return registry;
        }

        public void Start()
        {
            _processingEngine.Start();
            _renderingEngine.Start();
        }

        public void Stop()
        {
            _processingEngine.Stop();
            _renderingEngine.Stop();
        }

        public void Dispose()
        {
            Stop();
            _chasm.Dispose();
            _processingEngine.Dispose();
            _renderingEngine.Dispose();
        }

        public void RegisterPlot(PlotSettings settings)
        {
            _dataStore.UpsertSettings(settings);
        }

        public void UnregisterPlot(Guid plotId)
        {
            _dataStore.RemovePlot(plotId);
            _renderingEngine.Unregister(plotId);
        }

        public void RegisterRenderTarget(WpfPlot plot, Views.PlotViews.PlotView plotView, PlotSettings settings)
        {
            _renderingEngine.Register(plot, plotView, settings);
        }

        public void SetStreamingEnabled(bool enabled)
        {
            if (enabled)
                _chasm.StartStreaming();
            else
                _chasm.StopStreaming();
        }

        public void SetWindowCapacity(int windowCapacity)
        {
            _chasm.SetWindowCapacity(windowCapacity);
        }

        public void ClearMemory()
        {
            _chasm.ClearMemory();
            _processingEngine.OnDataCleared();

            // Clear visuals immediately on the UI thread without relying on a new render cycle.
            if (_dispatcher.CheckAccess())
                MemoryCleared?.Invoke(this, EventArgs.Empty);
            else
                _dispatcher.BeginInvoke(() => MemoryCleared?.Invoke(this, EventArgs.Empty));
        }

        public void ResetProcessingMetrics()
        {
            _processingEngine.ResetMetrics();
        }

        public void ResetRenderMetrics()
        {
            _renderingEngine.ResetMetrics();
        }

        public void UpsertGate(GateSettings gate)
        {
            _dataStore.UpsertGate(gate);
        }

        public void RemoveGate(Guid gateId)
        {
            _dataStore.RemoveGate(gateId);
        }

        public ProcessingStatusSnapshot GetProcessingStatusSnapshot()
        {
            var compute = _processingEngine.GetAverageComputeTimes();
            var render = _renderingEngine.GetAverageRenderTimes();
            var incremental = _processingEngine.GetIncrementalStats();

            double eventRate;
            long totalEvents = _dataSource.TotalEventsIngested;
            var now = DateTime.UtcNow;
            lock (_eventRateLock)
            {
                double seconds = (now - _lastRateSampleUtc).TotalSeconds;
                if (seconds >= 0.2)
                {
                    long deltaEvents = totalEvents - _lastRateEventCount;
                    _lastEventRatePerSecond = seconds > 0 ? Math.Max(0, deltaEvents / seconds) : 0;
                    _lastRateEventCount = totalEvents;
                    _lastRateSampleUtc = now;
                }

                if (!IsStreamingEnabled)
                    _lastEventRatePerSecond = 0;

                eventRate = _lastEventRatePerSecond;
            }

            return new ProcessingStatusSnapshot(
                EventRatePerSecond: eventRate,
                BufferedEventCount: _dataSource.BufferedEventCount,
                HistogramAverageComputeMs: compute.HistogramAverageMs,
                PseudocolorAverageComputeMs: compute.PseudocolorAverageMs,
                SpectralRibbonAverageComputeMs: compute.SpectralRibbonAverageMs,
                OscilloscopeAverageComputeMs: compute.OscilloscopeAverageMs,
                HistogramAverageRenderMs: render.HistogramAverageMs,
                PseudocolorAverageRenderMs: render.PseudocolorAverageMs,
                SpectralRibbonAverageRenderMs: render.SpectralRibbonAverageMs,
                OscilloscopeAverageRenderMs: render.OscilloscopeAverageMs,
                DeltaAppliedCount: incremental.DeltaAppliedCount,
                FullRebuildCount: incremental.FullRebuildCount,
                SequenceGapCount: incremental.SequenceGapCount);
        }
    }
}
