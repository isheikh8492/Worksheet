using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Core.Models.Gates;

namespace Worksheet.App.Views.PlotViews.Gates
{
    public sealed class PolygonGate : GateBase
    {
        private readonly List<ScottPlot.Coordinates> _points;

        public PolygonGate(Guid gateId, string name, IReadOnlyList<ScottPlot.Coordinates> points, GateStyle style)
            : base(gateId, name, GetBounds(points).xMin, GetBounds(points).xMax, GetBounds(points).yMin, GetBounds(points).yMax, style)
        {
            _points = points.ToList();
        }

        public IReadOnlyList<ScottPlot.Coordinates> Points => _points;

        public override GateType GateType => GateType.Polygon;

        public override GateGeometry ToGeometry(int binCount)
        {
            double inv = binCount > 0 ? 1.0 / binCount : 1.0;
            var points01 = _points
                .Select(p => new Point01(Math.Clamp(p.X * inv, 0, 1), Math.Clamp(p.Y * inv, 0, 1)))
                .ToArray();
            return GateGeometry.Polygon01(points01);
        }

        public override IReadOnlyList<GateHandle> GetHandles() =>
            _points.Select((p, i) => new GateHandle(p, i)).ToArray();

        // A polygon handle is a vertex; its Id is the vertex index. Caller clamps to range.
        public override void MoveHandle(GateHandle handle, ScottPlot.Coordinates to) =>
            SetPoint(handle.Id, to);

        public void SetPoint(int index, ScottPlot.Coordinates point)
        {
            if (index < 0 || index >= _points.Count)
                return;

            _points[index] = point;
            UpdateBoundsFromPoints();
        }

        public override bool Contains(ScottPlot.Coordinates c)
        {
            if (_points.Count < 3)
                return false;

            bool inside = false;
            int j = _points.Count - 1;
            for (int i = 0; i < _points.Count; i++)
            {
                double xi = _points[i].X;
                double yi = _points[i].Y;
                double xj = _points[j].X;
                double yj = _points[j].Y;

                bool intersect = ((yi > c.Y) != (yj > c.Y)) &&
                                 (c.X < (xj - xi) * (c.Y - yi) / (yj - yi + 1e-12) + xi);
                if (intersect)
                    inside = !inside;
                j = i;
            }

            return inside;
        }

        public override void SetBounds(double xMin, double xMax, double yMin, double yMax)
        {
            double oldXMin = XMin;
            double oldXMax = XMax;
            double oldYMin = YMin;
            double oldYMax = YMax;

            base.SetBounds(xMin, xMax, yMin, yMax);

            double oldW = Math.Max(1e-9, oldXMax - oldXMin);
            double oldH = Math.Max(1e-9, oldYMax - oldYMin);
            double newW = XMax - XMin;
            double newH = YMax - YMin;

            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                double tx = (p.X - oldXMin) / oldW;
                double ty = (p.Y - oldYMin) / oldH;
                _points[i] = new ScottPlot.Coordinates(XMin + tx * newW, YMin + ty * newH);
            }
        }

        protected override ScottPlot.Coordinates[] BuildCoordinates() => _points.ToArray();

        private void UpdateBoundsFromPoints()
        {
            if (_points.Count == 0)
                return;

            double xMin = _points.Min(p => p.X);
            double xMax = _points.Max(p => p.X);
            double yMin = _points.Min(p => p.Y);
            double yMax = _points.Max(p => p.Y);
            base.SetBounds(xMin, xMax, yMin, yMax);
        }

        private static (double xMin, double xMax, double yMin, double yMax) GetBounds(IReadOnlyList<ScottPlot.Coordinates> points)
        {
            if (points == null || points.Count == 0)
                return (0, 1, 0, 1);

            double xMin = points.Min(p => p.X);
            double xMax = points.Max(p => p.X);
            double yMin = points.Min(p => p.Y);
            double yMax = points.Max(p => p.Y);
            return (xMin, xMax, yMin, yMax);
        }
    }
}
