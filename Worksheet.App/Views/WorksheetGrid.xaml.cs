using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.Support;
using Worksheet.Models.Gates;

namespace Worksheet.Views
{
    public partial class WorksheetGrid : UserControl
    {
        private const double LayoutMargin = 10;
        private const ChasmPreset CurrentChasmPreset = ChasmPreset.Balanced50k;
        private readonly SelectionManager<IWorksheetItem> _selectionManager;
        private readonly PlotFactory _plotFactory;
        private readonly PlotContainerFactory _containerFactory;
        private readonly ThumbManager _thumbManager;
        private readonly DragHandler _dragHandler;
        private readonly ViewportSession _viewportSession;
        private readonly List<PlotItem> _plotItems = new();

        private double _snapSize = 0;
        private int _nextZIndex = 1;

        /// <summary>
        /// Grid snap size in pixels. Set to 0 to disable snapping and hide grid lines.
        /// </summary>
        public double SnapSize
        {
            get => _snapSize;
            set
            {
                _snapSize = value;
                UpdateGridBackground();
            }
        }

        public bool IsStreamingEnabled => _viewportSession.IsStreamingEnabled;
        public int WindowCapacity => _viewportSession.WindowCapacity;

        public WorksheetGrid() : this(
            new SelectionManager<IWorksheetItem>(),
            new PlotFactory(),
            new PlotContainerFactory(),
            new ThumbManager(),
            new DragHandler(),
            null)
        {
        }

        public WorksheetGrid(
            SelectionManager<IWorksheetItem> selectionManager,
            PlotFactory plotFactory,
            PlotContainerFactory containerFactory,
            ThumbManager thumbManager,
            DragHandler dragHandler,
            ViewportSession? viewportSession)
        {
            _selectionManager = selectionManager;
            _plotFactory = plotFactory;
            _containerFactory = containerFactory;
            _thumbManager = thumbManager;
            _dragHandler = dragHandler;
            _viewportSession = viewportSession ?? new ViewportSession(
                Dispatcher,
                System.TimeSpan.FromMilliseconds(250),
                System.TimeSpan.FromMilliseconds(33),
                ChasmOptions.FromPreset(CurrentChasmPreset),
                System.TimeSpan.FromMilliseconds(33));
            _viewportSession.Start();

            InitializeComponent();
            UpdateGridBackground();

            _viewportSession.MemoryCleared += (_, _) => ClearAllPlotVisuals();
            _selectionManager.SelectionChanged += item =>
            {
                if (item != null)
                    Panel.SetZIndex(item.Container, _nextZIndex++);
            };

            // Click on empty space deselects all items
            WorksheetGridContainer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == WorksheetGridContainer)
                {
                    _selectionManager.Deselect();
                }
            };
        }

        private void UpdateGridBackground()
        {
            if (_snapSize <= 0)
            {
                // No snapping - solid background
                WorksheetGridContainer.Background = new SolidColorBrush(Colors.WhiteSmoke);
                return;
            }

            // Create grid pattern matching snap size
            var geometryGroup = new GeometryGroup();
            geometryGroup.Children.Add(new RectangleGeometry(new Rect(0, 0, _snapSize, _snapSize)));
            geometryGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(_snapSize, 0)));
            geometryGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, _snapSize)));

            var drawing = new GeometryDrawing
            {
                Geometry = geometryGroup,
                Brush = new SolidColorBrush(Colors.WhiteSmoke),
                Pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), 0.5)
            };

            var brush = new DrawingBrush
            {
                Drawing = drawing,
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, _snapSize, _snapSize),
                ViewportUnits = BrushMappingMode.Absolute
            };

            WorksheetGridContainer.Background = brush;
        }

        public void AddPlot()
        {
            // Create the plot (with fixed padding that aligns with grid)
            var plot = _plotFactory.CreatePlot(200, 200);

            AddPlotToWorksheet(plot, null, null);
        }

        public void AddPlot(PlotType plotType)
        {
            try
            {
                // Create the plot using PlotFactory defaults
                var plot = _plotFactory.CreatePlot(plotType, out var plotView);
                AddPlotToWorksheet(plot, plotView, plotView?.Settings);
            }
            catch (Exception ex)
            {
                Worksheet.Services.AppLog.Exception(ex, $"WorksheetGrid.AddPlot plotType={plotType}");
            }
        }

        public void AddPlot(PlotType plotType, ScaleType axisScale)
        {
            try
            {
                // Create the plot using PlotFactory defaults
                var plot = _plotFactory.CreatePlot(plotType, axisScale, out var plotView);
                AddPlotToWorksheet(plot, plotView, plotView?.Settings);
            }
            catch (Exception ex)
            {
                Worksheet.Services.AppLog.Exception(ex, $"WorksheetGrid.AddPlot plotType={plotType} axisScale={axisScale}");
            }
        }

        public void AddHistogramPlotsForAllChannels()
        {
            var indices = _viewportSession.FeatureSelection.GetXFeatureIndices(PlotType.Histogram);

            if (indices.Count == 0)
            {
                for (int i = 0; i < SignalLayout.DefaultChannelCount; i++)
                    AddHistogramPlotForChannel(i);
                return;
            }

            for (int i = 0; i < indices.Count; i++)
                AddHistogramPlotForChannel(indices[i]);
        }

        private void AddHistogramPlotForChannel(int featureIndex)
        {
            var plot = _plotFactory.CreatePlot(PlotType.Histogram, out var plotView);
            if (plotView?.Settings is HistogramSettings histogramSettings)
                histogramSettings.XFeature = featureIndex;

            AddPlotToWorksheet(plot, plotView, plotView?.Settings);
        }

        public void LoadConfig()
        {
            double worksheetWidth = WorksheetScrollViewer.ViewportWidth > 0
                ? WorksheetScrollViewer.ViewportWidth
                : 800;

            double startY = GetNextPlacementY(LayoutMargin);
            double x = LayoutMargin;

            // Row 1: 2 pseudocolors + 1 spectral ribbon
            AddConfiguredPseudocolor(x, startY, "QPD H", "QPD V");
            x += 280 + LayoutMargin;

            AddConfiguredPseudocolor(x, startY, "372nm", "293nm");
            x += 280 + LayoutMargin;

            AddConfiguredSpectralRibbon(x, startY);

            double rowHeight = 280; // pseudocolor default height
            double histStartY = startY + rowHeight + LayoutMargin;

            // Row 2+: histograms for all channels
            AddHistogramGrid(histStartY, worksheetWidth);
            WorksheetScrollViewer.ScrollToVerticalOffset(Math.Max(0, startY - LayoutMargin));
            WorksheetScrollViewer.ScrollToHorizontalOffset(0);
        }

        private void AddConfiguredPseudocolor(double x, double y, string xName, string yName)
        {
            var plot = _plotFactory.CreatePlot(PlotType.Pseudocolor, out var plotView);
            if (plotView?.Settings is not PseudocolorSettings pseudocolorSettings)
                return;

            var channelMap = GetChannelNameToIdMap();
            if (!TryResolveChannelId(channelMap, xName, out int xId, out string resolvedX))
                xId = pseudocolorSettings.XFeature;
            if (!TryResolveChannelId(channelMap, yName, out int yId, out string resolvedY, excludeId: xId))
                yId = pseudocolorSettings.YFeature;

            pseudocolorSettings.XFeature = xId;
            pseudocolorSettings.YFeature = yId;

            AddPlotToWorksheetAt(plot, plotView, pseudocolorSettings, x, y);
        }

        private void AddConfiguredSpectralRibbon(double x, double y)
        {
            var plot = _plotFactory.CreatePlot(PlotType.SpectralRibbon, out var plotView);
            if (plotView?.Settings == null)
                return;

            AddPlotToWorksheetAt(plot, plotView, plotView.Settings, x, y);
        }

        private void AddHistogramGrid(double startY, double worksheetWidth)
        {
            var indices = _viewportSession.FeatureSelection.GetXFeatureIndices(PlotType.Histogram);
            List<int> ids = indices.Count == 0
                ? Enumerable.Range(0, SignalLayout.DefaultChannelCount).ToList()
                : indices.ToList();

            const double plotWidth = 280;
            const double plotHeight = 200;
            int plotsPerRow = Math.Max(1, (int)((worksheetWidth - LayoutMargin) / (plotWidth + LayoutMargin)));

            for (int i = 0; i < ids.Count; i++)
            {
                int col = i % plotsPerRow;
                int row = i / plotsPerRow;
                double x = LayoutMargin + col * (plotWidth + LayoutMargin);
                double y = startY + row * (plotHeight + LayoutMargin);

                var plot = _plotFactory.CreatePlot(PlotType.Histogram, out var plotView);
                if (plotView?.Settings is HistogramSettings histogramSettings)
                    histogramSettings.XFeature = ids[i];

                AddPlotToWorksheetAt(plot, plotView, plotView?.Settings, x, y);
            }
        }

        private double GetNextPlacementY(double margin)
        {
            double maxBottom = margin;
            foreach (UIElement child in WorksheetGridContainer.Children)
            {
                if (child is FrameworkElement fe)
                {
                    double top = Canvas.GetTop(fe);
                    if (double.IsNaN(top)) top = 0;
                    double bottom = top + fe.Height;
                    if (bottom > maxBottom) maxBottom = bottom;
                }
            }
            return maxBottom + margin;
        }

        private Dictionary<string, int> GetChannelNameToIdMap()
        {
            var names = _viewportSession.FeatureSelection.GetXFeatureNames(PlotType.Histogram);
            var ids = _viewportSession.FeatureSelection.GetXFeatureIndices(PlotType.Histogram);
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int count = Math.Min(names.Count, ids.Count);
            for (int i = 0; i < count; i++)
                map[names[i]] = ids[i];

            return map;
        }

        private static bool TryResolveChannelId(
            Dictionary<string, int> channelMap,
            string requestedName,
            out int id,
            out string resolvedName,
            int? excludeId = null)
        {
            if (channelMap.TryGetValue(requestedName, out id) && (!excludeId.HasValue || id != excludeId.Value))
            {
                resolvedName = requestedName;
                return true;
            }

            // If requested is a wavelength, pick the nearest available wavelength (avoid picking excludeId).
            if (TryParseWavelength(requestedName, out double requestedNm))
            {
                double bestDist = double.MaxValue;
                int bestId = -1;
                string bestName = string.Empty;

                foreach (var kvp in channelMap)
                {
                    if (excludeId.HasValue && kvp.Value == excludeId.Value)
                        continue;

                    if (!TryParseWavelength(kvp.Key, out double nm))
                        continue;

                    double dist = Math.Abs(nm - requestedNm);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestId = kvp.Value;
                        bestName = kvp.Key;
                    }
                }

                if (bestId >= 0)
                {
                    id = bestId;
                    resolvedName = bestName;
                    return true;
                }
            }

            id = -1;
            resolvedName = string.Empty;
            return false;
        }

        private static bool TryParseWavelength(string name, out double nm)
        {
            nm = 0;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var s = name.Trim();
            if (s.EndsWith("nm", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 2);

            return double.TryParse(s, out nm);
        }

        private void AddPlotToWorksheetAt(ScottPlot.WPF.WpfPlot plot, PlotView? plotView, PlotSettings? settings, double x, double y)
        {
            try
            {
            var worksheetWidth = WorksheetScrollViewer.ViewportWidth > 0
                ? WorksheetScrollViewer.ViewportWidth
                : 800;

            var container = _containerFactory.CreateContainer(plot, WorksheetGridContainer.Children.Count, worksheetWidth);
            Canvas.SetLeft(container.Container, x);
            Canvas.SetTop(container.Container, y);

            var thumbs = _thumbManager.CreateThumbs(container.Overlay);
            _thumbManager.AttachPositioning(plot, thumbs);
            _thumbManager.AttachResize(thumbs, container, plot, () => SnapSize);

            var plotItem = new PlotItem(plot, container, thumbs)
            {
                PlotView = plotView,
                OnCloseRequested = (item) =>
                {
                    if (settings != null)
                    {
                        _viewportSession.UnregisterPlot(settings.Id);
                    }

                    _selectionManager.Unregister(item);
                    _plotItems.Remove(item);
                    WorksheetGridContainer.Children.Remove(item.Container);
                }
            };

            _dragHandler.AttachDrag(container.DragLayer, plotItem,
                                    WorksheetGridContainer, _selectionManager, () => SnapSize);

            if (plotView is PseudocolorPlotView pcView)
            {
                pcView.GateSettingsSink = gate => _viewportSession.UpsertGate(gate);
                pcView.GateRemovedSink = gateId => _viewportSession.RemoveGate(gateId);
            }
            else if (plotView is HistogramPlotView histView)
            {
                histView.GateSettingsSink = gate => _viewportSession.UpsertGate(gate);
                histView.GateRemovedSink = gateId => _viewportSession.RemoveGate(gateId);
            }

            plotView?.AttachOverlay(container.Overlay);
            plotView?.AttachBitmapSurface(plot, container.DynamicSurface, container.DataRectBacking);
            plotView?.AttachContextMenu(plotItem);
            _selectionManager.Register(plotItem, plotItem.OnSelect, plotItem.OnDeselect);

            if (plotView != null && settings != null)
            {
                _viewportSession.RegisterPlot(settings);
                _viewportSession.RegisterRenderTarget(plot, plotView, settings);
            }

            WorksheetGridContainer.Children.Add(container.Container);
            Panel.SetZIndex(container.Container, _nextZIndex++);
            _plotItems.Add(plotItem);
            EnsureWorksheetBoundsIfExceeded(container.Container);
            _selectionManager.Select(plotItem);
            }
            catch (Exception ex)
            {
                Worksheet.Services.AppLog.Exception(ex, $"WorksheetGrid.AddPlotToWorksheetAt plotType={settings?.PlotType} plotId={settings?.Id}");
            }
        }

        private void AddPlotToWorksheet(ScottPlot.WPF.WpfPlot plot, PlotView? plotView, PlotSettings? settings)
        {
            try
            {
            // Create the container structure (use ActualWidth for grid layout)
            var worksheetWidth = WorksheetScrollViewer.ViewportWidth > 0
                ? WorksheetScrollViewer.ViewportWidth
                : 800; // fallback if not yet rendered
            var container = _containerFactory.CreateContainer(plot, WorksheetGridContainer.Children.Count, worksheetWidth);

            // Create and setup thumbs
            var thumbs = _thumbManager.CreateThumbs(container.Overlay);
            _thumbManager.AttachPositioning(plot, thumbs);
            _thumbManager.AttachResize(thumbs, container, plot, () => SnapSize);

            // Create the worksheet item with metadata
            var plotItem = new PlotItem(plot, container, thumbs)
            {
                PlotView = plotView,
                OnCloseRequested = (item) =>
                {
                    if (settings != null)
                    {
                        _viewportSession.UnregisterPlot(settings.Id);
                    }

                    _selectionManager.Unregister(item);
                    _plotItems.Remove(item);
                    WorksheetGridContainer.Children.Remove(item.Container);
                }
            };

            // Setup drag behavior with snapping
            _dragHandler.AttachDrag(container.DragLayer, plotItem,
                                    WorksheetGridContainer, _selectionManager, () => SnapSize);

            // Attach plot-specific context menu
            if (plotView is PseudocolorPlotView pcView)
            {
                pcView.GateSettingsSink = gate => _viewportSession.UpsertGate(gate);
                pcView.GateRemovedSink = gateId => _viewportSession.RemoveGate(gateId);
            }
            else if (plotView is HistogramPlotView histView)
            {
                histView.GateSettingsSink = gate => _viewportSession.UpsertGate(gate);
                histView.GateRemovedSink = gateId => _viewportSession.RemoveGate(gateId);
            }

            plotView?.AttachOverlay(container.Overlay);
            plotView?.AttachBitmapSurface(plot, container.DynamicSurface, container.DataRectBacking);
            plotView?.AttachContextMenu(plotItem);

            // Register with selection manager
            _selectionManager.Register(plotItem, plotItem.OnSelect, plotItem.OnDeselect);

            // Register with ViewportSession for processing/rendering.
            if (plotView != null && settings != null)
            {
                _viewportSession.RegisterPlot(settings);
                _viewportSession.RegisterRenderTarget(plot, plotView, settings);
            }

            // Add to worksheet and select
            WorksheetGridContainer.Children.Add(container.Container);
            Panel.SetZIndex(container.Container, _nextZIndex++);
            _plotItems.Add(plotItem);
            EnsureWorksheetBoundsIfExceeded(container.Container);
            _selectionManager.Select(plotItem);
            }
            catch (Exception ex)
            {
                Worksheet.Services.AppLog.Exception(ex, $"WorksheetGrid.AddPlotToWorksheet plotType={settings?.PlotType} plotId={settings?.Id}");
            }
        }

        public void SetStreamingEnabled(bool enabled)
        {
            _viewportSession.SetStreamingEnabled(enabled);
        }

        private void EnsureWorksheetBoundsIfExceeded(FrameworkElement item)
        {
            double left = Canvas.GetLeft(item);
            double top = Canvas.GetTop(item);

            if (double.IsNaN(left))
                left = 0;
            if (double.IsNaN(top))
                top = 0;

            double itemWidth = item.Width > 0 ? item.Width : item.ActualWidth;
            double itemHeight = item.Height > 0 ? item.Height : item.ActualHeight;
            double requiredWidth = left + itemWidth + LayoutMargin;
            double requiredHeight = top + itemHeight + LayoutMargin;
            double currentWidth = double.IsNaN(WorksheetGridContainer.Width) ? 0 : WorksheetGridContainer.Width;
            double currentHeight = double.IsNaN(WorksheetGridContainer.Height) ? 0 : WorksheetGridContainer.Height;

            if (requiredWidth <= currentWidth && requiredHeight <= currentHeight)
                return;

            if (requiredWidth > currentWidth)
                WorksheetGridContainer.Width = requiredWidth;
            if (requiredHeight > currentHeight)
                WorksheetGridContainer.Height = requiredHeight;
        }

        public void ToggleStreaming()
        {
            _viewportSession.SetStreamingEnabled(!_viewportSession.IsStreamingEnabled);
        }

        public void ClearMemory()
        {
            _viewportSession.ClearMemory();
        }

        public void SetWindowCapacity(int windowCapacity)
        {
            _viewportSession.SetWindowCapacity(windowCapacity);
        }

        public void ResetProcessingMetrics()
        {
            _viewportSession.ResetProcessingMetrics();
        }

        public void ResetRenderMetrics()
        {
            _viewportSession.ResetRenderMetrics();
        }

        public IReadOnlyList<GateStatsDisplayRow> GetGateStatsRows()
        {
            try
            {
                var gates = _viewportSession.DataStore.GetAllGates();
                var rows = new List<GateStatsDisplayRow>(gates.Count);

                foreach (var gate in gates)
                {
                    var dict = _viewportSession.DataStore.TryGetGateResult(gate.GateId, out var result)
                        ? result.Stats.ToDisplayDictionary()
                        : new Dictionary<string, string>
                        {
                            ["Num"] = "0.0 %",
                            ["Total"] = "0",
                            ["CV"] = "0.0",
                            ["Mean"] = "0.0",
                            ["STD"] = "0.0",
                            ["Var"] = "0.0",
                        };

                    rows.Add(new GateStatsDisplayRow
                    {
                        GateId = gate.GateId,
                        GateName = gate.Name,
                        Num = dict.GetValueOrDefault("Num", ""),
                        Total = dict.GetValueOrDefault("Total", ""),
                        CV = dict.GetValueOrDefault("CV", ""),
                        Mean = dict.GetValueOrDefault("Mean", ""),
                        STD = dict.GetValueOrDefault("STD", ""),
                        Var = dict.GetValueOrDefault("Var", ""),
                    });
                }

                return rows;
            }
            catch
            {
                return Array.Empty<GateStatsDisplayRow>();
            }
        }

        public ProcessingStatusSnapshot GetProcessingStatusSnapshot()
        {
            try
            {
                return _viewportSession.GetProcessingStatusSnapshot();
            }
            catch
            {
                return new ProcessingStatusSnapshot();
            }
        }

        private void ClearAllPlotVisuals()
        {
            // Clear each plot view directly. This does not depend on the processing/rendering pipeline.
            foreach (var item in _plotItems.ToList())
            {
                try
                {
                    item.PlotView?.Clear(item.Plot);
                }
                catch
                {
                    // Clearing should not crash the UI.
                }
            }
        }
    }
}
