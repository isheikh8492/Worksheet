using System;
using System.Collections.Generic;
using ScottPlot.WPF;
using Worksheet.Core.Models.Gates;

namespace Worksheet.App.Views.PlotViews.Gates
{
    public sealed class LineGate : GateBase
    {
        private const double LineThicknessXBins = 0.35;

        public LineGate(
            Guid gateId,
            string name,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            double yFraction,
            GateStyle style)
            : base(gateId, name, xMin, xMax, yMin, yMax, style)
        {
            YFraction = Math.Clamp(yFraction, 0, 1);
        }

        public double YFraction { get; private set; }

        // A histogram line serializes as a full-height rectangle (matches legacy EmitGateUpsert behavior).
        public override GateType GateType => GateType.Rectangle;

        public override GateGeometry ToGeometry(int binCount) =>
            GateGeometry.FromBinRectangle(XMin, XMax, 0, binCount, binCount);

        // Left edge (0), right edge (1), center (2) — all at the current y-line.
        public override IReadOnlyList<GateHandle> GetHandles()
        {
            double yLine = YMin + (YMax - YMin) * YFraction;
            return new[]
            {
                new GateHandle(new ScottPlot.Coordinates(XMin, yLine), 0),
                new GateHandle(new ScottPlot.Coordinates(XMax, yLine), 1),
                new GateHandle(new ScottPlot.Coordinates((XMin + XMax) / 2.0, yLine), 2),
            };
        }

        public override void MoveHandle(GateHandle handle, ScottPlot.Coordinates to)
        {
            switch (handle.Id)
            {
                case 0: // left edge
                    SetBounds(Math.Min(to.X, XMax), XMax, YMin, YMax);
                    break;
                case 1: // right edge
                    SetBounds(XMin, Math.Max(to.X, XMin), YMin, YMax);
                    break;
                default: // center: translate horizontally + set the y-fraction from the drop point
                    double dx = to.X - (XMin + XMax) / 2.0;
                    SetBounds(XMin + dx, XMax + dx, YMin, YMax);
                    double span = Math.Max(1e-9, YMax - YMin);
                    SetYFraction((to.Y - YMin) / span);
                    break;
            }
        }

        public void SetYFraction(double value) => YFraction = Math.Clamp(value, 0, 1);

        public override bool Contains(ScottPlot.Coordinates c)
        {
            double yLine = YMin + (YMax - YMin) * YFraction;
            double xTol = Math.Max(0.5, LineThicknessXBins * 1.5);
            double yTol = Math.Max(1e-6, (YMax - YMin) * 0.02);

            bool nearLeft = Math.Abs(c.X - XMin) <= xTol;
            bool nearRight = Math.Abs(c.X - XMax) <= xTol;
            bool onMid = c.X >= XMin - xTol && c.X <= XMax + xTol && Math.Abs(c.Y - yLine) <= yTol;
            return nearLeft || nearRight || onMid;
        }

        public override void RebuildPlottable(WpfPlot plot)
        {
            RemovePlottables(plot);

            double ySpan = Math.Max(1e-6, YMax - YMin);
            double yLine = YMin + ySpan * YFraction;
            double halfX = LineThicknessXBins / 2.0;
            double halfY = ComputeHalfYFromVerticalPixelWidth(plot, yLine, halfX);

            var lineColor = ScottPlot.Colors.Black;
            var fillColor = ScottPlot.Colors.Black;

            var left = AddPolygon(plot, new[]
            {
                new ScottPlot.Coordinates(XMin - halfX, YMin),
                new ScottPlot.Coordinates(XMin + halfX, YMin),
                new ScottPlot.Coordinates(XMin + halfX, YMax),
                new ScottPlot.Coordinates(XMin - halfX, YMax),
            }, lineColor, fillColor, 1);

            var right = AddPolygon(plot, new[]
            {
                new ScottPlot.Coordinates(XMax - halfX, YMin),
                new ScottPlot.Coordinates(XMax + halfX, YMin),
                new ScottPlot.Coordinates(XMax + halfX, YMax),
                new ScottPlot.Coordinates(XMax - halfX, YMax),
            }, lineColor, fillColor, 1);

            var mid = AddPolygon(plot, new[]
            {
                new ScottPlot.Coordinates(XMin, yLine - halfY),
                new ScottPlot.Coordinates(XMax, yLine - halfY),
                new ScottPlot.Coordinates(XMax, yLine + halfY),
                new ScottPlot.Coordinates(XMin, yLine + halfY),
            }, lineColor, fillColor, 1);

            Plottable = mid;
            RegisterAuxiliaryPlottable(left);
            RegisterAuxiliaryPlottable(right);
            LabelPlottable = AddDefaultCenteredLabel(plot, Name, (XMin + XMax) / 2.0, yLine + (ySpan * 0.04));
        }

        private double ComputeHalfYFromVerticalPixelWidth(WpfPlot plot, double yLine, double halfX)
        {
            try
            {
                var axes = plot.Plot.Axes;
                double xCenter = (XMin + XMax) / 2.0;
                var pxLeft = plot.Plot.GetPixel(new ScottPlot.Coordinates(xCenter - halfX, yLine), axes.Bottom, axes.Left);
                var pxRight = plot.Plot.GetPixel(new ScottPlot.Coordinates(xCenter + halfX, yLine), axes.Bottom, axes.Left);
                double widthPx = Math.Abs(pxRight.X - pxLeft.X);
                if (widthPx < 1)
                    widthPx = 1;

                var c1 = plot.Plot.GetCoordinates((float)pxLeft.X, (float)pxLeft.Y, axes.Bottom, axes.Left);
                var c2 = plot.Plot.GetCoordinates((float)pxLeft.X, (float)(pxLeft.Y + widthPx), axes.Bottom, axes.Left);
                double thicknessY = Math.Abs(c2.Y - c1.Y);
                return Math.Max(1e-6, thicknessY / 2.0);
            }
            catch
            {
                return Math.Max(1e-6, (YMax - YMin) * 0.004);
            }
        }

        protected override ScottPlot.Coordinates[] BuildCoordinates() =>
            new[]
            {
                new ScottPlot.Coordinates(XMin, YMin),
                new ScottPlot.Coordinates(XMax, YMin),
                new ScottPlot.Coordinates(XMax, YMax),
                new ScottPlot.Coordinates(XMin, YMax),
            };
    }
}
