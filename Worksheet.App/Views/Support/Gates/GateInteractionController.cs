using System;
using System.Collections.Generic;
using System.Windows;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Gates;
using Worksheet.Views.PlotViews.Gates;

namespace Worksheet.Views.Support.Gates
{
    /// <summary>
    /// The gate input state machine. Translates mouse events into selection, drag-resize/move
    /// (via each gate's own <see cref="GateBase.MoveHandle"/>/<see cref="GateBase.MoveBy"/>), and
    /// gate creation. Holds no rendering — raises events for the coordinator to emit/serialize.
    /// </summary>
    internal sealed class GateInteractionController
    {
        private const double HandleHitRadiusDip = 9;
        private const double MinBoxDip = 4;
        private const double PolygonCloseThresholdDip = 16;
        private const double MinLineWidthBins = 1;

        private readonly WpfPlot _plot;
        private readonly PlotCoordinateMapper _mapper;
        private readonly GateOverlay _overlay;
        private readonly List<GateBase> _gates;
        private readonly Func<int> _getBins;
        private readonly Func<PlotType> _getPlotType;
        private readonly Func<string> _nextName;

        private enum Op { None, Move, Resize, DrawBox, DrawPolygon }
        private Op _op = Op.None;

        private int _selected = -1;
        private GateHandle _activeHandle;
        private ScottPlot.Coordinates _lastMoveCoord;
        private Point _boxStartDip;
        private bool _boxDragging;
        private GateType _drawType;
        private bool _isHistogramLine;
        private readonly List<Point> _polygonDip = new();

        public GateInteractionController(
            WpfPlot plot, PlotCoordinateMapper mapper, GateOverlay overlay, List<GateBase> gates,
            Func<int> getBins, Func<PlotType> getPlotType, Func<string> nextName)
        {
            _plot = plot;
            _mapper = mapper;
            _overlay = overlay;
            _gates = gates;
            _getBins = getBins;
            _getPlotType = getPlotType;
            _nextName = nextName;
        }

        public event Action<GateBase>? GateCreated;
        public event Action<GateBase>? GateCommitted;
        public event Action<GateBase?>? SelectionChanged;

        public bool IsDrawing => _op is Op.DrawBox or Op.DrawPolygon;
        public bool IsInteracting => _op is Op.Move or Op.Resize || (_op == Op.DrawBox && _boxDragging);
        public GateBase? SelectedGate => _selected >= 0 && _selected < _gates.Count ? _gates[_selected] : null;

        public void EnterDraw(GateType type)
        {
            Deselect();
            _drawType = type;
            _isHistogramLine = _getPlotType() == PlotType.Histogram;
            _polygonDip.Clear();
            _boxDragging = false;
            _op = type == GateType.Polygon ? Op.DrawPolygon : Op.DrawBox;
        }

        public void CancelDraw()
        {
            _op = Op.None;
            _boxDragging = false;
            _polygonDip.Clear();
            _overlay.ClearPreview();
        }

        // ---- input ----

        public void OnMouseDown(Point dip, bool leftButton)
        {
            switch (_op)
            {
                case Op.DrawPolygon:
                    HandlePolygonClick(dip, leftButton);
                    return;
                case Op.DrawBox:
                    if (leftButton) { _boxStartDip = dip; _boxDragging = true; }
                    return;
            }

            if (!leftButton)
                return;

            var coord = _mapper.ToData(dip);

            // 1) grab a handle of the already-selected gate → resize
            if (SelectedGate is GateBase sel)
            {
                foreach (var h in sel.GetHandles())
                {
                    if (Distance(_mapper.ToDip(h.Position), dip) <= HandleHitRadiusDip)
                    {
                        _op = Op.Resize;
                        _activeHandle = h;
                        return;
                    }
                }
            }

            // 2) click a gate body → select + begin move (topmost first)
            for (int i = _gates.Count - 1; i >= 0; i--)
            {
                if (_gates[i].Contains(coord))
                {
                    Select(i);
                    _op = Op.Move;
                    _lastMoveCoord = coord;
                    return;
                }
            }

            // 3) empty space → deselect
            Deselect();
        }

        public void OnMouseMove(Point dip)
        {
            switch (_op)
            {
                case Op.DrawBox when _boxDragging: PreviewBox(dip); break;
                case Op.DrawPolygon: PreviewPolygon(dip); break;
                case Op.Resize: DoResize(dip); break;
                case Op.Move: DoMove(dip); break;
            }
        }

        public void OnMouseUp(Point dip)
        {
            switch (_op)
            {
                case Op.DrawBox when _boxDragging:
                    CommitBox(dip);
                    break;
                case Op.Resize:
                case Op.Move:
                    _op = Op.None;
                    if (SelectedGate is GateBase g)
                        GateCommitted?.Invoke(g);
                    break;
            }
        }

        // ---- edit ----

        private void DoResize(Point dip)
        {
            if (SelectedGate is not GateBase gate) return;
            gate.MoveHandle(_activeHandle, ClampToBins(_mapper.ToData(dip)));
            Rerender(gate);
        }

        private void DoMove(Point dip)
        {
            if (SelectedGate is not GateBase gate) return;
            var coord = _mapper.ToData(dip);
            gate.MoveBy(coord.X - _lastMoveCoord.X, coord.Y - _lastMoveCoord.Y);
            _lastMoveCoord = coord;
            Rerender(gate);
        }

        // ---- draw: box (rectangle / ellipse / histogram line) ----

        private void PreviewBox(Point dip)
        {
            var kind = _drawType == GateType.Ellipse ? PreviewKind.Ellipse : PreviewKind.Rectangle;
            _overlay.ShowPreview(new DrawPreview(kind, new[] { _boxStartDip, dip }));
        }

        private void CommitBox(Point dip)
        {
            _boxDragging = false;
            _op = Op.None;
            _overlay.ClearPreview();

            if (Math.Abs(dip.X - _boxStartDip.X) < MinBoxDip && !_isHistogramLine)
                return;

            var c1 = _mapper.ToData(_boxStartDip);
            var c2 = _mapper.ToData(dip);
            double xMin = ClampBin(Math.Min(c1.X, c2.X));
            double xMax = ClampBin(Math.Max(c1.X, c2.X));
            double yMin = ClampBin(Math.Min(c1.Y, c2.Y));
            double yMax = ClampBin(Math.Max(c1.Y, c2.Y));

            GateBase gate;
            if (_isHistogramLine)
            {
                double top = Math.Max(1, _plot.Plot.Axes.GetLimits().Top);
                if (xMax - xMin < MinLineWidthBins) xMax = Math.Min(_getBins(), xMin + MinLineWidthBins);
                double frac = Math.Clamp((c1.Y) / Math.Max(1e-9, top), 0, 1);
                gate = new LineGate(Guid.NewGuid(), _nextName(), xMin, xMax, 0, top, frac, GateStyle.DefaultRectangle);
            }
            else
            {
                gate = GateBase.CreateFromBounds(_drawType, Guid.NewGuid(), _nextName(), xMin, xMax, yMin, yMax, GateStyle.DefaultRectangle);
            }

            AddAndSelect(gate);
        }

        // ---- draw: polygon (multi-click) ----

        private void HandlePolygonClick(Point dip, bool leftButton)
        {
            if (!leftButton)
            {
                if (_polygonDip.Count >= 3) CommitPolygon();
                else CancelDraw();
                return;
            }

            if (_polygonDip.Count >= 3 && Distance(_polygonDip[0], dip) <= PolygonCloseThresholdDip)
            {
                CommitPolygon();
                return;
            }

            _polygonDip.Add(dip);
            PreviewPolygon(dip);
        }

        private void PreviewPolygon(Point hover)
        {
            if (_polygonDip.Count == 0) return;
            var pts = new List<Point>(_polygonDip) { hover };
            _overlay.ShowPreview(new DrawPreview(PreviewKind.Polyline, pts));
        }

        private void CommitPolygon()
        {
            _op = Op.None;
            _overlay.ClearPreview();
            if (_polygonDip.Count < 3) { _polygonDip.Clear(); return; }

            var coords = new List<ScottPlot.Coordinates>(_polygonDip.Count);
            foreach (var p in _polygonDip)
            {
                var c = _mapper.ToData(p);
                coords.Add(new ScottPlot.Coordinates(ClampBin(c.X), ClampBin(c.Y)));
            }
            _polygonDip.Clear();
            AddAndSelect(new PolygonGate(Guid.NewGuid(), _nextName(), coords, GateStyle.DefaultRectangle));
        }

        // ---- helpers ----

        private void AddAndSelect(GateBase gate)
        {
            gate.RebuildPlottable(_plot);
            MoveToTop(gate);
            _plot.Refresh();
            _gates.Add(gate);
            Select(_gates.Count - 1);
            GateCreated?.Invoke(gate);
        }

        private void Rerender(GateBase gate)
        {
            gate.RebuildPlottable(_plot);
            MoveToTop(gate);
            _plot.Refresh();
            _overlay.ShowHandles(gate.GetHandles());
        }

        private void MoveToTop(GateBase gate)
        {
            if (gate.Plottable != null) _plot.Plot.MoveToTop(gate.Plottable);
            foreach (var aux in gate.AuxiliaryPlottables) _plot.Plot.MoveToTop(aux);
            if (gate.LabelPlottable != null) _plot.Plot.MoveToTop(gate.LabelPlottable);
        }

        private void Select(int index)
        {
            _selected = index;
            var gate = SelectedGate;
            if (gate != null) _overlay.ShowHandles(gate.GetHandles());
            SelectionChanged?.Invoke(gate);
        }

        private void Deselect()
        {
            _selected = -1;
            _op = Op.None;
            _overlay.HideHandles();
            SelectionChanged?.Invoke(null);
        }

        public bool RemoveSelected(out Guid gateId)
        {
            gateId = Guid.Empty;
            if (SelectedGate is not GateBase gate) return false;
            gateId = gate.GateId;
            gate.RemoveFromPlot(_plot);
            _gates.RemoveAt(_selected);
            _plot.Refresh();
            Deselect();
            return true;
        }

        private double ClampBin(double v) => Math.Clamp(v, 0, _getBins());
        private ScottPlot.Coordinates ClampToBins(ScottPlot.Coordinates c) => new(ClampBin(c.X), ClampBin(c.Y));
        private static double Distance(Point a, Point b) { double dx = a.X - b.X, dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy); }
    }
}
