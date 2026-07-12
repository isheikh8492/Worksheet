using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Axes
{
    public abstract class AxisItem
    {
        public abstract AxisScaleType ScaleType { get; }
        public abstract void Apply(WpfPlot plot, ParameterPlotSettings settings, AxisOrientation orientation);
    }
}
