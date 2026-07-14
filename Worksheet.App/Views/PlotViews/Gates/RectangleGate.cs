using System;
using System.Collections.Generic;
using Worksheet.Core.Models.Gates;

namespace Worksheet.App.Views.PlotViews.Gates
{
    public sealed class RectangleGate : GateBase
    {
        public RectangleGate(Guid gateId, string name, double xMin, double xMax, double yMin, double yMax, GateStyle style)
            : base(gateId, name, xMin, xMax, yMin, yMax, style)
        {
        }

        public override GateType GateType => GateType.Rectangle;

        public override GateGeometry ToGeometry(int binCount) =>
            GateGeometry.FromBinRectangle(XMin, XMax, YMin, YMax, binCount);

        public override IReadOnlyList<GateHandle> GetHandles() => new[]
        {
            new GateHandle(new ScottPlot.Coordinates(XMin, YMin), 0),
            new GateHandle(new ScottPlot.Coordinates(XMax, YMin), 1),
            new GateHandle(new ScottPlot.Coordinates(XMin, YMax), 2),
            new GateHandle(new ScottPlot.Coordinates(XMax, YMax), 3),
        };

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
