using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using ScottPlot.WPF;
using Worksheet.Core.Models;
using Worksheet.App.Views.PlotViews;

using Worksheet.Processing;
using Worksheet.Core.Services;
namespace Worksheet.App.Services.Viewport
{
    public class RenderingEngine : PollingEngine
    {
        private readonly DataStore _dataStore;
        private readonly Dispatcher _dispatcher;
        private readonly object _targetsLock = new();
        private readonly List<RenderTarget> _targets = new();
        private readonly object _pendingLock = new();
        private readonly Dictionary<Guid, PendingRender> _pendingRenders = new();
        private int _renderPassScheduled;

        private static readonly int PlotTypeCount = Enum.GetValues<PlotType>().Length;
        private readonly object _metricsLock = new();
        private readonly double[] _renderTotalMs = new double[PlotTypeCount];
        private readonly long[] _renderCount = new long[PlotTypeCount];

        public RenderingEngine(DataStore dataStore, Dispatcher dispatcher, TimeSpan interval)
            : base(interval)
        {
            _dataStore = dataStore;
            _dispatcher = dispatcher;
        }

        public void Register(WpfPlot plot, PlotView plotView, PlotSettings settings)
        {
            lock (_targetsLock)
            {
                Action<Guid, RenderTargetSize> targetSizeHandler = (plotId, size) => _dataStore.SetRenderTargetSize(plotId, size);
                plotView.TargetSizeChanged += targetSizeHandler;
                _targets.Add(new RenderTarget(plot, plotView, settings.Id, settings.PlotType, targetSizeHandler));
            }
        }

        public void Unregister(Guid plotId)
        {
            lock (_targetsLock)
            {
                foreach (var target in _targets.Where(t => t.PlotId == plotId))
                    target.PlotView.TargetSizeChanged -= target.TargetSizeHandler;

                _targets.RemoveAll(t => t.PlotId == plotId);
            }

            lock (_pendingLock)
            {
                _pendingRenders.Remove(plotId);
            }
        }

        protected override void Tick()
        {
            List<RenderTarget> snapshot;
            lock (_targetsLock)
            {
                snapshot = new List<RenderTarget>(_targets);
            }

            bool enqueued = false;
            foreach (var target in snapshot)
            {
                if (!_dataStore.TryGetProcessedData(target.PlotId, out var data))
                    continue;

                if (ReferenceEquals(data, target.LastRenderedData))
                    continue;

                lock (_pendingLock)
                {
                    _pendingRenders[target.PlotId] = new PendingRender(target, data);
                }

                enqueued = true;
            }

            if (enqueued)
                ScheduleRenderPass();
        }

        public PlotTimingSnapshot GetAverageRenderTimes()
        {
            lock (_metricsLock)
            {
                return new PlotTimingSnapshot(
                    HistogramAverageMs: ComputeAverage(PlotType.Histogram),
                    PseudocolorAverageMs: ComputeAverage(PlotType.Pseudocolor),
                    SpectralRibbonAverageMs: ComputeAverage(PlotType.SpectralRibbon),
                    OscilloscopeAverageMs: ComputeAverage(PlotType.Oscilloscope));
            }
        }

        public void ResetMetrics()
        {
            lock (_metricsLock)
            {
                Array.Clear(_renderTotalMs);
                Array.Clear(_renderCount);
            }
        }

        private void ScheduleRenderPass()
        {
            if (Interlocked.Exchange(ref _renderPassScheduled, 1) == 1)
                return;

            _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderPendingOnUiThread));
        }

        private void RenderPendingOnUiThread()
        {
            try
            {
                List<PendingRender> pending;
                lock (_pendingLock)
                {
                    pending = new List<PendingRender>(_pendingRenders.Values);
                    _pendingRenders.Clear();
                }

                foreach (var item in pending)
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        item.Target.PlotView.Render(item.Target.Plot, item.Data);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Exception(ex, $"RenderingEngine.Render plotId={item.Target.PlotId} view={item.Target.PlotView.GetType().Name}");
                    }
                    finally
                    {
                        stopwatch.Stop();
                        RecordRenderTime(item.Target.PlotType, stopwatch.Elapsed.TotalMilliseconds);
                        item.Target.LastRenderedData = item.Data;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref _renderPassScheduled, 0);

                bool hasPending;
                lock (_pendingLock)
                {
                    hasPending = _pendingRenders.Count > 0;
                }

                if (hasPending)
                    ScheduleRenderPass();
            }
        }

        private void RecordRenderTime(PlotType plotType, double elapsedMs)
        {
            lock (_metricsLock)
            {
                _renderTotalMs[(int)plotType] += elapsedMs;
                _renderCount[(int)plotType]++;
            }
        }

        private double ComputeAverage(PlotType plotType)
        {
            long count = _renderCount[(int)plotType];
            return count > 0 ? _renderTotalMs[(int)plotType] / count : 0;
        }

        private sealed class RenderTarget
        {
            public RenderTarget(WpfPlot plot, PlotView plotView, Guid plotId, PlotType plotType, Action<Guid, RenderTargetSize> targetSizeHandler)
            {
                Plot = plot;
                PlotView = plotView;
                PlotId = plotId;
                PlotType = plotType;
                TargetSizeHandler = targetSizeHandler;
            }

            public WpfPlot Plot { get; }
            public PlotView PlotView { get; }
            public Guid PlotId { get; }
            public PlotType PlotType { get; }
            public Action<Guid, RenderTargetSize> TargetSizeHandler { get; }
            public object? LastRenderedData { get; set; }
        }

        private readonly record struct PendingRender(RenderTarget Target, Worksheet.Core.Models.Data.ProcessedPlotData Data);
    }
}
