using System;
using ScottPlot.WPF;
using Worksheet.Models;
using System.Collections.Generic;

namespace Worksheet.Views.PlotViews.Axes
{
    public class LinearAxisItem : AxisItem
    {
        public override ScaleType ScaleType => ScaleType.Linear;

        public override void Apply(WpfPlot plot, ParameterPlotSettings settings, AxisOrientation orientation)
        {
            if (orientation == AxisOrientation.Bottom)
            {
                ApplyBottom(plot, settings);
            }
            else if (orientation == AxisOrientation.Left)
            {
                ApplyLeft(plot);
            }
        }

        private static void ApplyBottom(WpfPlot plot, ParameterPlotSettings settings)
        {
            plot.Plot.Axes.Bottom.TickGenerator = CreateDataTickGenerator(settings);

            // Configure minor grid styling
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;

            plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
        }

        private static void ApplyLeft(WpfPlot plot)
        {
            plot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic()
            {
                LabelFormatter = (double x) => FormatSIPrefix(x)
            };
        }

        internal static FixedTickGenerator CreateDataTickGenerator(ParameterPlotSettings settings)
        {
            var scale = new LinearScale(settings.MinValue, settings.MaxValue, settings.GetBinCount());
            var majorValues = new double[] { 0, 20_000_000, 40_000_000, 60_000_000, 80_000_000, 100_000_000 };
            var majorPositions = new double[majorValues.Length];
            var majorLabels = new string[majorValues.Length];

            for (int i = 0; i < majorValues.Length; i++)
            {
                majorPositions[i] = scale.ToPosition(majorValues[i]);
                majorLabels[i] = FormatSIPrefix(majorValues[i]);
            }

            var minorPositions = new List<double>();

            for (int i = 0; i < majorValues.Length - 1; i++)
            {
                double start = majorValues[i];
                double step = (majorValues[i + 1] - start) / 5;
                for (int j = 1; j <= 4; j++)
                {
                    double minorValue = start + step * j;
                    minorPositions.Add(scale.ToPosition(minorValue));
                }
            }

            return new FixedTickGenerator(majorPositions, majorLabels, minorPositions.ToArray());
        }
    }
}
