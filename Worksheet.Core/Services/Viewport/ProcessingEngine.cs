using System;
using System.Collections.Generic;
using System.Diagnostics;
using Worksheet.Models;
using Worksheet.Models.Gates;
using Worksheet.Services.Viewport.Gates;

namespace Worksheet.Services
{
    public class ProcessingEngine : PollingEngine
    {
        private readonly DataStore _dataStore;
        private readonly PlotPipelineRegistry _pipelines;
        private readonly GateProcessor _gateProcessor;
        private readonly Func<long> _getDataVersion;
        private readonly TimeSpan _parameterPlotInterval;
        private readonly object _processingLock = new();
        private readonly Dictionary<Guid, SettingsFingerprint> _lastProcessedSettings = new();
        private readonly Dictionary<Guid, DateTime> _lastPlotProcessUtc = new();
        private readonly Dictionary<Guid, GateFingerprint> _lastProcessedGates = new();
        private readonly HashSet<Guid> _activePlotIds = new();
        private readonly HashSet<Guid> _activeGateIds = new();
        private readonly List<Guid> _staleIds = new();
        private readonly object _metricsLock = new();
        private DateTime _lastGateProcessUtc = DateTime.MinValue;
        private double _histComputeTotalMs;
        private long _histComputeCount;
        private double _pcComputeTotalMs;
        private long _pcComputeCount;
        private double _srComputeTotalMs;
        private long _srComputeCount;
        private double _scopeComputeTotalMs;
        private long _scopeComputeCount;

        public ProcessingEngine(
            DataStore dataStore,
            PlotProcessor plotProcessor,
            GateProcessor gateProcessor,
            Func<long> getDataVersion,
            TimeSpan interval,
            OscilloscopePlotProcessor? oscilloscopePlotProcessor = null,
            Func<long>? getOscilloscopeVersion = null,
            TimeSpan? parameterPlotInterval = null,
            TimeSpan? oscilloscopePlotInterval = null)
            : this(
                dataStore,
                CreateDefaultRegistry(
                    plotProcessor,
                    getDataVersion,
                    parameterPlotInterval ?? interval,
                    oscilloscopePlotProcessor,
                    getOscilloscopeVersion,
                    oscilloscopePlotInterval ?? interval),
                gateProcessor,
                getDataVersion,
                interval,
                parameterPlotInterval)
        {
        }

        public ProcessingEngine(
            DataStore dataStore,
            PlotPipelineRegistry pipelines,
            GateProcessor gateProcessor,
            Func<long> getDataVersion,
            TimeSpan interval,
            TimeSpan? parameterPlotInterval = null)
            : base(interval)
        {
            _dataStore = dataStore;
            _pipelines = pipelines;
            _gateProcessor = gateProcessor;
            _getDataVersion = getDataVersion;
            _parameterPlotInterval = parameterPlotInterval ?? interval;
        }

        private static PlotPipelineRegistry CreateDefaultRegistry(
            PlotProcessor plotProcessor,
            Func<long> getDataVersion,
            TimeSpan parameterPlotInterval,
            OscilloscopePlotProcessor? oscilloscopePlotProcessor,
            Func<long>? getOscilloscopeVersion,
            TimeSpan oscilloscopePlotInterval)
        {
            var registry = new PlotPipelineRegistry();
            var parameterPipeline = new ParameterPlotPipeline(plotProcessor, getDataVersion, parameterPlotInterval);

            registry.Register(PlotType.Histogram, parameterPipeline);
            registry.Register(PlotType.Pseudocolor, parameterPipeline);
            registry.Register(PlotType.SpectralRibbon, parameterPipeline);

            if (oscilloscopePlotProcessor != null && getOscilloscopeVersion != null)
            {
                registry.Register(
                    PlotType.Oscilloscope,
                    new OscilloscopePlotPipeline(oscilloscopePlotProcessor, getOscilloscopeVersion, oscilloscopePlotInterval));
            }

            return registry;
        }

        protected override void Tick()
        {
            lock (_processingLock)
            {
                long dataVersion = _getDataVersion();
                var now = DateTime.UtcNow;

                var settings = _dataStore.GetAllSettings();
                _activePlotIds.Clear();

                foreach (var plotSettings in settings)
                {
                    _activePlotIds.Add(plotSettings.Id);

                    var targetSize = _dataStore.GetRenderTargetSize(plotSettings.Id);
                    var pipeline = _pipelines.GetRequired(plotSettings.PlotType);
                    long plotDataVersion = pipeline.Version;
                    var fingerprint = SettingsFingerprint.From(plotSettings, pipeline.GetSettingsHash(plotSettings, targetSize), plotDataVersion);
                    bool hadPrevious = _lastProcessedSettings.TryGetValue(plotSettings.Id, out var previous);

                    if (hadPrevious && previous.Equals(fingerprint))
                    {
                        _lastPlotProcessUtc[plotSettings.Id] = now;
                        continue;
                    }

                    if (hadPrevious && previous.HasSameSettings(fingerprint) && !IsPlotDue(plotSettings.Id, pipeline.Cadence, now))
                        continue;

                    var stopwatch = Stopwatch.StartNew();
                    var processed = pipeline.Process(plotSettings, targetSize);
                    stopwatch.Stop();
                    RecordComputeTime(plotSettings.PlotType, stopwatch.Elapsed.TotalMilliseconds);
                    _lastPlotProcessUtc[plotSettings.Id] = now;

                    if (processed != null)
                    {
                        _dataStore.SetProcessedData(processed);
                        _lastProcessedSettings[plotSettings.Id] = fingerprint;
                    }
                }

                RemoveStalePlotState();

                if (AreGatesDue(now))
                    ProcessGates(dataVersion);
            }
        }

        private bool IsPlotDue(Guid plotId, TimeSpan cadence, DateTime now)
        {
            if (!_lastPlotProcessUtc.TryGetValue(plotId, out var lastProcessedUtc))
                return true;

            return now - lastProcessedUtc >= cadence;
        }

        private void RemoveStalePlotState()
        {
            _staleIds.Clear();
            foreach (var plotId in _lastProcessedSettings.Keys)
            {
                if (!_activePlotIds.Contains(plotId))
                    _staleIds.Add(plotId);
            }

            foreach (var staleId in _staleIds)
            {
                _lastProcessedSettings.Remove(staleId);
                _lastPlotProcessUtc.Remove(staleId);
            }
        }

        private bool AreGatesDue(DateTime now)
        {
            if (_lastGateProcessUtc == DateTime.MinValue || now - _lastGateProcessUtc >= _parameterPlotInterval)
            {
                _lastGateProcessUtc = now;
                return true;
            }

            return false;
        }

        private void ProcessGates(long dataVersion)
        {
            var gates = _dataStore.GetAllGates();
            _activeGateIds.Clear();

            foreach (var gate in gates)
            {
                _activeGateIds.Add(gate.GateId);

                if (!_dataStore.TryGetSettings(gate.Plot.PlotId, out var plotSettings))
                    continue;

                var fingerprint = GateFingerprint.From(gate, plotSettings, dataVersion);
                if (_lastProcessedGates.TryGetValue(gate.GateId, out var prev) && prev.Equals(fingerprint))
                    continue;

                var result = _gateProcessor.Process(gate, plotSettings, dataVersion);
                _dataStore.SetGateResult(result);
                _lastProcessedGates[gate.GateId] = fingerprint;
            }

            RemoveStaleGateState();
            _gateProcessor.RemoveInactiveStates(_activeGateIds);
        }

        private void RemoveStaleGateState()
        {
            _staleIds.Clear();
            foreach (var gateId in _lastProcessedGates.Keys)
            {
                if (!_activeGateIds.Contains(gateId))
                    _staleIds.Add(gateId);
            }

            foreach (var staleId in _staleIds)
                _lastProcessedGates.Remove(staleId);
        }

        public PlotTimingSnapshot GetAverageComputeTimes()
        {
            lock (_metricsLock)
            {
                return new PlotTimingSnapshot(
                    HistogramAverageMs: ComputeAverage(_histComputeTotalMs, _histComputeCount),
                    PseudocolorAverageMs: ComputeAverage(_pcComputeTotalMs, _pcComputeCount),
                    SpectralRibbonAverageMs: ComputeAverage(_srComputeTotalMs, _srComputeCount),
                    OscilloscopeAverageMs: ComputeAverage(_scopeComputeTotalMs, _scopeComputeCount));
            }
        }

        public IncrementalProcessingStats GetIncrementalStats()
        {
            var (deltaAppliedCount, fullRebuildCount, sequenceGapCount) = _pipelines.GetDeltaStats();
            return new IncrementalProcessingStats(deltaAppliedCount, fullRebuildCount, sequenceGapCount);
        }

        public void ResetMetrics()
        {
            lock (_metricsLock)
            {
                _histComputeTotalMs = 0;
                _histComputeCount = 0;
                _pcComputeTotalMs = 0;
                _pcComputeCount = 0;
                _srComputeTotalMs = 0;
                _srComputeCount = 0;
                _scopeComputeTotalMs = 0;
                _scopeComputeCount = 0;
            }

            _pipelines.ResetStates();
            _gateProcessor.ResetIncrementalState();
        }

        public void OnDataCleared()
        {
            lock (_processingLock)
            {
                _lastProcessedSettings.Clear();
                _lastPlotProcessUtc.Clear();
                _lastProcessedGates.Clear();
                _lastGateProcessUtc = DateTime.MinValue;
                _pipelines.ResetStates();
                _gateProcessor.ResetIncrementalState();
            }
        }

        private void RecordComputeTime(PlotType plotType, double elapsedMs)
        {
            lock (_metricsLock)
            {
                switch (plotType)
                {
                    case PlotType.Histogram:
                        _histComputeTotalMs += elapsedMs;
                        _histComputeCount++;
                        break;
                    case PlotType.Pseudocolor:
                        _pcComputeTotalMs += elapsedMs;
                        _pcComputeCount++;
                        break;
                    case PlotType.SpectralRibbon:
                        _srComputeTotalMs += elapsedMs;
                        _srComputeCount++;
                        break;
                    case PlotType.Oscilloscope:
                        _scopeComputeTotalMs += elapsedMs;
                        _scopeComputeCount++;
                        break;
                }
            }
        }

        private static double ComputeAverage(double totalMs, long count)
        {
            return count > 0 ? totalMs / count : 0;
        }

        private readonly record struct SettingsFingerprint(
            PlotType PlotType,
            int SettingsHash,
            long DataVersion)
        {
            public static SettingsFingerprint From(PlotSettings settings, int settingsHash, long dataVersion) =>
                new(
                    settings.PlotType,
                    settingsHash,
                    dataVersion);

            public bool HasSameSettings(SettingsFingerprint other)
            {
                return PlotType == other.PlotType
                    && SettingsHash == other.SettingsHash;
            }
        }

        private readonly record struct GateFingerprint(
            Guid PlotId,
            GateType GateType,
            int GeometryHash,
            PlotType PlotType,
            int BinCount,
            int XFeature,
            int YFeature,
            AxisScaleType XAxisScaleType,
            AxisScaleType YAxisScaleType,
            double MinValue,
            double MaxValue,
            long DataVersion)
        {
            public static GateFingerprint From(GateSettings gate, PlotSettings settings, long dataVersion)
            {
                // Gates only attach to parameter plots (histogram, pseudocolor).
                int binCount = 0, xFeature = 0, yFeature = 0;
                AxisScaleType xScale = AxisScaleType.Linear, yScale = AxisScaleType.Linear;
                double minValue = 0, maxValue = 0;

                if (settings is ParameterPlotSettings parameter)
                {
                    binCount = parameter.GetBinCount();
                    minValue = parameter.MinValue;
                    maxValue = parameter.MaxValue;
                }

                switch (settings)
                {
                    case HistogramSettings histogram:
                        xFeature = histogram.XFeature;
                        xScale = histogram.XAxisScaleType;
                        break;
                    case PseudocolorSettings pseudocolor:
                        xFeature = pseudocolor.XFeature;
                        yFeature = pseudocolor.YFeature;
                        xScale = pseudocolor.XAxisScaleType;
                        yScale = pseudocolor.YAxisScaleType;
                        break;
                }

                return new GateFingerprint(
                    gate.Plot.PlotId,
                    gate.GateType,
                    gate.Geometry.GetGeometryHash(),
                    settings.PlotType,
                    binCount,
                    xFeature,
                    yFeature,
                    xScale,
                    yScale,
                    minValue,
                    maxValue,
                    dataVersion);
            }
        }
    }
}
