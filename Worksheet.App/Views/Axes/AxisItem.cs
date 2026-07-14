using System;
using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Axes
{
    public abstract class AxisItem
    {
        public abstract ScaleType ScaleType { get; }
        public abstract void Apply(WpfPlot plot, ParameterPlotSettings settings, AxisOrientation orientation);

        internal static string FormatSIPrefix(double value)
        {
            if (value == 0) return "0";

            double absValue = Math.Abs(value);
            string sign = value < 0 ? "-" : "";

            if (absValue >= 1_000_000_000)
                return $"{sign}{absValue / 1_000_000_000:0.##}G";
            else if (absValue >= 1_000_000)
                return $"{sign}{absValue / 1_000_000:0.##}M";
            else if (absValue >= 1_000)
                return $"{sign}{absValue / 1_000:0.##}k";
            else
                return $"{sign}{absValue:0.##}";
        }
    }
}
