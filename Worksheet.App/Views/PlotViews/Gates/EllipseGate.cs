using System;
using System.Collections.Generic;
using Worksheet.Models.Gates;

namespace Worksheet.Views.PlotViews.Gates
{
    public sealed class EllipseGate : GateBase
    {
        private const int SegmentCount = 64;

        public EllipseGate(Guid gateId, string name, double xMin, double xMax, double yMin, double yMax, GateStyle style)
            : base(gateId, name, xMin, xMax, yMin, yMax, style)
        {
        }

        public override GateType GateType => GateType.Ellipse;

        public override GateGeometry ToGeometry(int binCount)
        {
            double inv = binCount > 0 ? 1.0 / binCount : 1.0;
            double cx = ((XMin + XMax) / 2.0) * inv;
            double cy = ((YMin + YMax) / 2.0) * inv;
            double rx = ((XMax - XMin) / 2.0) * inv;
            double ry = ((YMax - YMin) / 2.0) * inv;
            return GateGeometry.Ellipse01(cx, cy, rx, ry, angleDeg: 0);
        }

        public override IReadOnlyList<GateHandle> GetHandles() => new[]
        {
            new GateHandle(new ScottPlot.Coordinates(XMin, YMin), 0),
            new GateHandle(new ScottPlot.Coordinates(XMax, YMin), 1),
            new GateHandle(new ScottPlot.Coordinates(XMin, YMax), 2),
            new GateHandle(new ScottPlot.Coordinates(XMax, YMax), 3),
        };

        public override bool Contains(ScottPlot.Coordinates c)
        {
            double cx = (XMin + XMax) / 2.0;
            double cy = (YMin + YMax) / 2.0;
            double rx = (XMax - XMin) / 2.0;
            double ry = (YMax - YMin) / 2.0;

            if (rx <= 0 || ry <= 0)
                return false;

            double dx = (c.X - cx) / rx;
            double dy = (c.Y - cy) / ry;
            return dx * dx + dy * dy <= 1.0;
        }

        protected override ScottPlot.Coordinates[] BuildCoordinates()
        {
            double cx = (XMin + XMax) / 2.0;
            double cy = (YMin + YMax) / 2.0;
            double rx = (XMax - XMin) / 2.0;
            double ry = (YMax - YMin) / 2.0;

            var coords = new ScottPlot.Coordinates[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                double t = (2.0 * Math.PI * i) / SegmentCount;
                coords[i] = new ScottPlot.Coordinates(
                    cx + rx * Math.Cos(t),
                    cy + ry * Math.Sin(t));
            }

            return coords;
        }
    }
}
