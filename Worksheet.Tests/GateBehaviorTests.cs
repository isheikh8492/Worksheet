using System;
using System.Linq;
using Worksheet.Core.Models.Gates;
using Worksheet.App.Views.PlotViews.Gates;
using Xunit;

namespace Worksheet.Tests;

// Foundation of the gate redesign: each gate owns GateType, ToGeometry, GetHandles, MoveBy.
public sealed class GateBehaviorTests
{
    private static readonly GateStyle Style = GateStyle.DefaultRectangle;

    // ---- ToGeometry (must match the legacy EmitGateUpsert switch) ----

    [Fact]
    public void RectangleGate_Serializes()
    {
        var g = new RectangleGate(Guid.NewGuid(), "A", 2, 6, 2, 6, Style).ToGeometry(10);
        Assert.Equal(GateType.Rectangle, g.Type);
        Assert.Equal(0.2, g.XMin01, 6);
        Assert.Equal(0.6, g.XMax01, 6);
    }

    [Fact]
    public void EllipseGate_Serializes()
    {
        var g = new EllipseGate(Guid.NewGuid(), "A", 2, 6, 2, 6, Style).ToGeometry(10);
        Assert.Equal(GateType.Ellipse, g.Type);
        Assert.Equal(0.4, g.CenterX01, 6);
        Assert.Equal(0.2, g.RadiusX01, 6);
    }

    [Fact]
    public void LineGate_SerializesAsFullHeightRectangle()
    {
        var g = new LineGate(Guid.NewGuid(), "A", 3, 7, 0, 100, 0.5, Style).ToGeometry(10);
        Assert.Equal(GateType.Rectangle, g.Type);
        Assert.Equal(0.0, g.YMin01, 6);
        Assert.Equal(1.0, g.YMax01, 6);
    }

    // ---- GetHandles ----

    [Fact]
    public void RectangleGate_HasFourCornerHandles()
    {
        var handles = new RectangleGate(Guid.NewGuid(), "A", 0, 10, 0, 20, Style).GetHandles();
        Assert.Equal(4, handles.Count);
        Assert.Contains(handles, h => h.Position.X == 0 && h.Position.Y == 0);
        Assert.Contains(handles, h => h.Position.X == 10 && h.Position.Y == 20);
        Assert.Equal(new[] { 0, 1, 2, 3 }, handles.Select(h => h.Id).ToArray());
    }

    [Fact]
    public void LineGate_HasLeftRightCenterHandlesAtYLine()
    {
        var handles = new LineGate(Guid.NewGuid(), "A", 2, 8, 0, 100, 0.25, Style).GetHandles();
        Assert.Equal(3, handles.Count);
        Assert.Equal(2, handles[0].Position.X, 6);   // left
        Assert.Equal(8, handles[1].Position.X, 6);   // right
        Assert.Equal(5, handles[2].Position.X, 6);   // center = (2+8)/2
        Assert.All(handles, h => Assert.Equal(25, h.Position.Y, 6)); // y-line = 0 + 100*0.25
    }

    // ---- MoveHandle ----

    [Fact]
    public void MoveHandle_CornerResizesRectangle()
    {
        var gate = new RectangleGate(Guid.NewGuid(), "A", 2, 6, 2, 6, Style);
        var tr = gate.GetHandles().Single(h => h.Id == 3);      // top-right = (XMax, YMax)
        gate.MoveHandle(tr, new ScottPlot.Coordinates(9, 8));
        Assert.Equal(2, gate.XMin, 6);
        Assert.Equal(9, gate.XMax, 6);
        Assert.Equal(2, gate.YMin, 6);
        Assert.Equal(8, gate.YMax, 6);
    }

    [Fact]
    public void MoveHandle_NormalizesWhenDraggedPastOppositeCorner()
    {
        var gate = new RectangleGate(Guid.NewGuid(), "A", 2, 6, 2, 6, Style);
        var br = gate.GetHandles().Single(h => h.Id == 1);      // bottom-right = (XMax, YMin)
        gate.MoveHandle(br, new ScottPlot.Coordinates(0, 9));   // dragged left of XMin and above YMax
        Assert.Equal(0, gate.XMin, 6);
        Assert.Equal(2, gate.XMax, 6);
        Assert.Equal(6, gate.YMin, 6);
        Assert.Equal(9, gate.YMax, 6);
    }

    [Fact]
    public void MoveHandle_MovesPolygonVertex()
    {
        var pts = new[] { new ScottPlot.Coordinates(0, 0), new ScottPlot.Coordinates(4, 0), new ScottPlot.Coordinates(0, 4) };
        var gate = new PolygonGate(Guid.NewGuid(), "A", pts, Style);
        gate.MoveHandle(new GateHandle(default, 1), new ScottPlot.Coordinates(7, 3));
        Assert.Equal(7, gate.Points[1].X, 6);
        Assert.Equal(3, gate.Points[1].Y, 6);
    }

    [Fact]
    public void MoveHandle_LineCenterTranslatesAndSetsYFraction()
    {
        var gate = new LineGate(Guid.NewGuid(), "A", 2, 8, 0, 100, 0.5, Style);
        var center = gate.GetHandles().Single(h => h.Id == 2);
        gate.MoveHandle(center, new ScottPlot.Coordinates(20, 75));
        Assert.Equal(17, gate.XMin, 6);          // center 5 -> 20 shifts by +15
        Assert.Equal(23, gate.XMax, 6);
        Assert.Equal(0.75, gate.YFraction, 6);   // 75 / (100-0)
    }

    [Fact]
    public void PolygonGate_HasOneHandlePerVertexIndexedInOrder()
    {
        var pts = new[]
        {
            new ScottPlot.Coordinates(1, 1),
            new ScottPlot.Coordinates(9, 1),
            new ScottPlot.Coordinates(5, 8),
        };
        var handles = new PolygonGate(Guid.NewGuid(), "A", pts, Style).GetHandles();
        Assert.Equal(3, handles.Count);
        Assert.Equal(new[] { 0, 1, 2 }, handles.Select(h => h.Id).ToArray());
        Assert.Equal(9, handles[1].Position.X, 6);
    }

    // ---- MoveBy ----

    [Fact]
    public void MoveBy_TranslatesBounds()
    {
        var gate = new RectangleGate(Guid.NewGuid(), "A", 2, 6, 3, 7, Style);
        gate.MoveBy(10, -1);
        Assert.Equal(12, gate.XMin, 6);
        Assert.Equal(16, gate.XMax, 6);
        Assert.Equal(2, gate.YMin, 6);
        Assert.Equal(6, gate.YMax, 6);
    }

    [Fact]
    public void MoveBy_TranslatesPolygonVertices()
    {
        var pts = new[] { new ScottPlot.Coordinates(0, 0), new ScottPlot.Coordinates(4, 0), new ScottPlot.Coordinates(0, 4) };
        var gate = new PolygonGate(Guid.NewGuid(), "A", pts, Style);
        gate.MoveBy(5, 5);
        Assert.Contains(gate.Points, p => Math.Abs(p.X - 5) < 1e-6 && Math.Abs(p.Y - 5) < 1e-6);
        Assert.Contains(gate.Points, p => Math.Abs(p.X - 9) < 1e-6 && Math.Abs(p.Y - 5) < 1e-6);
    }
}
