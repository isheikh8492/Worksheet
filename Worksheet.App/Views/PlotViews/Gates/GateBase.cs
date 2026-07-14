using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using Worksheet.Models.Gates;

namespace Worksheet.Views.PlotViews.Gates
{
    /// <summary>A draggable edit-handle on a gate. <see cref="Id"/> is meaningful only to the gate that produced it.</summary>
    public readonly record struct GateHandle(ScottPlot.Coordinates Position, int Id);

    public abstract class GateBase
    {
        private readonly GateStyle _style;
        private readonly List<ScottPlot.IPlottable> _auxPlottables = new();

        protected GateBase(Guid gateId, string name, double xMin, double xMax, double yMin, double yMax, GateStyle style)
        {
            GateId = gateId;
            Name = name;
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            _style = style;
        }

        public Guid GateId { get; }
        public string Name { get; }
        public double XMin { get; private set; }
        public double XMax { get; private set; }
        public double YMin { get; private set; }
        public double YMax { get; private set; }

        public ScottPlot.IPlottable? Plottable { get; protected set; }
        public ScottPlot.IPlottable? LabelPlottable { get; protected set; }
        public IReadOnlyList<ScottPlot.IPlottable> AuxiliaryPlottables => _auxPlottables;

        /// <summary>The gate-type this shape serializes as.</summary>
        public abstract GateType GateType { get; }

        /// <summary>Serializes this gate's shape to a normalized <see cref="GateGeometry"/> for the given bin count.</summary>
        public abstract GateGeometry ToGeometry(int binCount);

        /// <summary>The draggable edit-handles this gate exposes when selected (in data coordinates).</summary>
        public abstract IReadOnlyList<GateHandle> GetHandles();

        /// <summary>Builds a box-drawn gate (rectangle or ellipse) from drag bounds. The single, explicit construction switch.</summary>
        public static GateBase CreateFromBounds(GateType type, Guid id, string name,
            double xMin, double xMax, double yMin, double yMax, GateStyle style) => type switch
        {
            GateType.Ellipse => new EllipseGate(id, name, xMin, xMax, yMin, yMax, style),
            GateType.Rectangle => new RectangleGate(id, name, xMin, xMax, yMin, yMax, style),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "CreateFromBounds supports rectangle/ellipse only."),
        };

        /// <summary>Translates the whole gate by a data-space delta.</summary>
        public void MoveBy(double dx, double dy) =>
            SetBounds(XMin + dx, XMax + dx, YMin + dy, YMax + dy);

        /// <summary>
        /// Reshapes the gate by dragging one of its handles to a new data coordinate. Default is
        /// corner-resize (matches the legacy <c>ApplyResize</c>); range-clamping is the caller's job.
        /// Handle Ids: 0=BL, 1=BR, 2=TL, 3=TR.
        /// </summary>
        public virtual void MoveHandle(GateHandle handle, ScottPlot.Coordinates to)
        {
            double xMin = XMin, xMax = XMax, yMin = YMin, yMax = YMax;
            switch (handle.Id)
            {
                case 0: xMin = to.X; yMin = to.Y; break; // bottom-left
                case 1: xMax = to.X; yMin = to.Y; break; // bottom-right
                case 2: xMin = to.X; yMax = to.Y; break; // top-left
                case 3: xMax = to.X; yMax = to.Y; break; // top-right
            }
            if (xMin > xMax) (xMin, xMax) = (xMax, xMin);
            if (yMin > yMax) (yMin, yMax) = (yMax, yMin);
            SetBounds(xMin, xMax, yMin, yMax);
        }

        public virtual bool Contains(ScottPlot.Coordinates c) =>
            c.X >= XMin && c.X <= XMax && c.Y >= YMin && c.Y <= YMax;

        public virtual void SetBounds(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        public virtual void RebuildPlottable(WpfPlot plot)
        {
            RemovePlottables(plot);
            var gate = AddPolygon(plot, BuildCoordinates(), _style.LineColor, _style.FillColor, _style.LineWidth);
            Plottable = gate;
            LabelPlottable = AddDefaultCenteredLabel(plot, Name, (XMin + XMax) / 2, (YMin + YMax) / 2);
        }

        /// <summary>Removes all of this gate's plottables from the plot.</summary>
        public void RemoveFromPlot(WpfPlot plot) => RemovePlottables(plot);

        protected void RemovePlottables(WpfPlot plot)
        {
            if (Plottable != null)
            {
                try { plot.Plot.Remove(Plottable); } catch { }
                Plottable = null;
            }

            if (LabelPlottable != null)
            {
                try { plot.Plot.Remove(LabelPlottable); } catch { }
                LabelPlottable = null;
            }

            if (_auxPlottables.Count > 0)
            {
                foreach (var pl in _auxPlottables)
                {
                    try { plot.Plot.Remove(pl); } catch { }
                }
                _auxPlottables.Clear();
            }
        }

        protected ScottPlot.IPlottable AddPolygon(
            WpfPlot plot,
            IReadOnlyList<ScottPlot.Coordinates> coords,
            ScottPlot.Color lineColor,
            ScottPlot.Color fillColor,
            float lineWidth)
        {
            ScottPlot.Plottables.Polygon gate;
            try
            {
                gate = plot.Plot.Add.Polygon(coords.ToArray());
            }
            catch
            {
                gate = new ScottPlot.Plottables.Polygon(coords.ToArray());
                plot.Plot.PlottableList.Add(gate);
            }

            gate.LineWidth = lineWidth;
            gate.LineColor = lineColor;
            gate.FillColor = fillColor;
            return gate;
        }

        protected ScottPlot.IPlottable? AddDefaultCenteredLabel(WpfPlot plot, string text, double x, double y)
        {
            try
            {
                var label = plot.Plot.Add.Text(text, x, y);
                label.Alignment = ScottPlot.Alignment.MiddleCenter;
                label.LabelFontColor = ScottPlot.Colors.Black;
                label.LabelStyle.FontSize += 2;
                label.LabelStyle.Bold = true;
                return label;
            }
            catch
            {
                return null;
            }
        }

        protected void RegisterAuxiliaryPlottable(ScottPlot.IPlottable plottable)
        {
            _auxPlottables.Add(plottable);
        }

        protected abstract ScottPlot.Coordinates[] BuildCoordinates();
    }

    public readonly record struct GateStyle(
        ScottPlot.Color LineColor,
        ScottPlot.Color FillColor,
        float LineWidth)
    {
        public static GateStyle DefaultRectangle =>
            new(ScottPlot.Colors.Black, ScottPlot.Colors.Transparent, 2);
    }
}
