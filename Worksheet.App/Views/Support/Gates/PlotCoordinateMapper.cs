using System.Windows;
using ScottPlot.WPF;

namespace Worksheet.App.Views.Support.Gates
{
    /// <summary>
    /// Converts between mouse DIP points and plot data coordinates for a single plot,
    /// accounting for DPI. Concrete (no interface) — there is exactly one implementation.
    /// </summary>
    internal sealed class PlotCoordinateMapper
    {
        private readonly WpfPlot _plot;

        public PlotCoordinateMapper(WpfPlot plot) => _plot = plot;

        /// <summary>Mouse DIP point → plot data coordinate.</summary>
        public ScottPlot.Coordinates ToData(Point dip)
        {
            var dpi = DpiContext.From(_plot);
            var axes = _plot.Plot.Axes;
            return _plot.Plot.GetCoordinates(
                (float)(dip.X * dpi.ScaleX),
                (float)(dip.Y * dpi.ScaleY),
                axes.Bottom,
                axes.Left);
        }

        /// <summary>Plot data coordinate → DIP point.</summary>
        public Point ToDip(ScottPlot.Coordinates data)
        {
            var dpi = DpiContext.From(_plot);
            var axes = _plot.Plot.Axes;
            var px = _plot.Plot.GetPixel(data, axes.Bottom, axes.Left);
            return new Point(px.X / dpi.ScaleX, px.Y / dpi.ScaleY);
        }
    }
}
