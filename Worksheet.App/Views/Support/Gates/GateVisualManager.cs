using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Gates;
using Worksheet.Services;
using Worksheet.Views.PlotViews.Gates;

namespace Worksheet.Views.Support.Gates
{
    /// <summary>
    /// Coordinates a plot's gates: owns the gate collection, hosts the overlay + interaction
    /// controller, and serializes gates to the settings sink. All per-type behavior lives on the
    /// gates (<see cref="GateBase"/>); drawing/editing lives in <see cref="GateInteractionController"/>;
    /// the visuals live in <see cref="GateOverlay"/>. This class only wires them together.
    /// </summary>
    public sealed class GateVisualManager
    {
        private readonly List<GateBase> _gates = new();

        private Func<int> _getBinCount = static () => 256;
        private Func<Guid> _getPlotId = static () => Guid.Empty;
        private Func<PlotType> _getPlotType = static () => PlotType.Pseudocolor;
        private Action<GateSettings>? _gateSettingsSink;
        private Action<Guid>? _gateRemovedSink;

        private bool _attached;
        private Canvas? _overlayCanvas;
        private GateOverlay? _overlay;
        private GateInteractionController? _controller;

        public void Attach(
            PlotItem plotItem,
            Func<int> getBinCount,
            Func<Guid>? getPlotId = null,
            Func<PlotType>? getPlotType = null,
            Action<GateSettings>? gateSettingsSink = null,
            Action<Guid>? gateRemovedSink = null)
        {
            if (_attached)
                return;

            var container = plotItem?.PlotContainer;
            if (plotItem?.Plot == null || container?.DragLayer == null || container.Overlay == null || container.Host == null)
                return;

            _attached = true;
            _getBinCount = getBinCount ?? _getBinCount;
            _getPlotId = getPlotId ?? _getPlotId;
            _getPlotType = getPlotType ?? _getPlotType;
            _gateSettingsSink = gateSettingsSink;
            _gateRemovedSink = gateRemovedSink;

            var host = container.Host;
            _overlayCanvas = new Canvas
            {
                Width = host.ActualWidth > 0 ? host.ActualWidth : host.Width,
                Height = host.ActualHeight > 0 ? host.ActualHeight : host.Height,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
            };
            Panel.SetZIndex(_overlayCanvas, 5);
            container.Overlay.Children.Add(_overlayCanvas);
            host.SizeChanged += (_, __) =>
            {
                if (_overlayCanvas == null) return;
                _overlayCanvas.Width = host.ActualWidth;
                _overlayCanvas.Height = host.ActualHeight;
            };

            var mapper = new PlotCoordinateMapper(plotItem.Plot);
            _overlay = new GateOverlay(_overlayCanvas, mapper);
            _controller = new GateInteractionController(
                plotItem.Plot, mapper, _overlay, _gates,
                () => Math.Max(1, _getBinCount()), () => _getPlotType(), NextGateName);
            _controller.GateCreated += Emit;
            _controller.GateCommitted += Emit;

            var drag = container.DragLayer;
            drag.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _controller.OnMouseDown(e.GetPosition(plotItem.Plot), leftButton: true);
                if (_controller.IsInteracting)
                {
                    drag.CaptureMouse();
                    e.Handled = true;
                }
            };
            drag.PreviewMouseMove += (_, e) => _controller.OnMouseMove(e.GetPosition(plotItem.Plot));
            drag.PreviewMouseLeftButtonUp += (_, e) =>
            {
                _controller.OnMouseUp(e.GetPosition(plotItem.Plot));
                if (drag.IsMouseCaptured) drag.ReleaseMouseCapture();
            };
            drag.PreviewMouseRightButtonDown += (_, e) =>
            {
                if (_controller.IsDrawing)
                {
                    _controller.OnMouseDown(e.GetPosition(plotItem.Plot), leftButton: false);
                    e.Handled = true; // consume the right-click so the context menu stays closed mid-draw
                }
            };

            // Reposition the selected gate's handles as the plot re-renders (pan / axis rescale).
            plotItem.Plot.Plot.RenderManager.RenderFinished += (_, __) =>
            {
                try
                {
                    if (_controller?.SelectedGate is GateBase g)
                        _overlay?.ShowHandles(g.GetHandles());
                }
                catch
                {
                }
            };
        }

        public bool HasSelectedGate => _controller?.SelectedGate != null;

        public bool RemoveSelectedGate(PlotItem plotItem)
        {
            if (_controller == null || !_controller.RemoveSelected(out var gateId))
                return false;

            try
            {
                _gateRemovedSink?.Invoke(gateId);
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.RemoveSelectedGate");
            }

            return true;
        }

        public void BeginAddRectangleGate(PlotItem plotItem) => _controller?.EnterDraw(GateType.Rectangle);
        public void BeginAddEllipseGate(PlotItem plotItem) => _controller?.EnterDraw(GateType.Ellipse);
        public void BeginAddPolygonGate(PlotItem plotItem) => _controller?.EnterDraw(GateType.Polygon);
        public void BeginAddLineGate(PlotItem plotItem) => _controller?.EnterDraw(GateType.Rectangle); // histogram context draws a line

        private void Emit(GateBase gate)
        {
            var sink = _gateSettingsSink;
            if (sink == null)
                return;

            try
            {
                int bins = Math.Max(1, _getBinCount());
                sink(new GateSettings
                {
                    GateId = gate.GateId,
                    Name = gate.Name,
                    Plot = new GatePlotRef(_getPlotId(), _getPlotType()),
                    GateType = gate.GateType,
                    Geometry = gate.ToGeometry(bins),
                    UpdatedUtc = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.Emit");
            }
        }

        // Next Excel-style label (A, B, ... Z, AA, ...) beyond the highest existing gate name.
        private string NextGateName()
        {
            int maxIndex = -1;
            foreach (var name in _gates.Select(g => g.Name))
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var letters = new string(name.Where(char.IsLetter).ToArray()).ToUpperInvariant();
                if (letters.Length == 0)
                    continue;

                int num = 0;
                bool valid = true;
                foreach (char c in letters)
                {
                    if (c < 'A' || c > 'Z') { valid = false; break; }
                    num = num * 26 + (c - 'A' + 1);
                }
                if (valid)
                    maxIndex = Math.Max(maxIndex, num - 1);
            }

            int n = maxIndex + 2; // 1-based label for (maxIndex + 1)
            string label = "";
            while (n > 0)
            {
                n--;
                label = (char)('A' + n % 26) + label;
                n /= 26;
            }
            return label;
        }
    }
}
