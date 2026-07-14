using System;
using ScottPlot.WPF;
using Worksheet.Core.Models;

namespace Worksheet.App.Views.Axes
{
    public class LogarithmicAxisItem : AxisItem
    {
        public override ScaleType ScaleType => ScaleType.Logarithmic;

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
            plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
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
            var scale = new LogScale(settings.MinValue, settings.MaxValue, settings.GetBinCount());
            var positions = new double[9];
            var labels = new string[9];

            for (int i = 0; i <= 8; i++)
            {
                var value = Math.Pow(10, i);
                positions[i] = scale.ToPosition(value);
                labels[i] = FormatLogLabel(i);
            }

            var minorPositions = new System.Collections.Generic.List<double>();
            for (int i = 0; i < 8; i++)
            {
                double decadeStart = Math.Pow(10, i);
                for (int m = 2; m <= 9; m++)
                {
                    double minorValue = decadeStart * m;
                    minorPositions.Add(scale.ToPosition(minorValue));
                }
            }

            return new FixedTickGenerator(positions, labels, minorPositions.ToArray());
        }

        internal static string FormatLogLabel(int exponent)
        {
            string[] superscripts = { "⁰", "¹", "²", "³", "⁴", "⁵", "⁶", "⁷", "⁸", "⁹" };

            if (exponent < 0)
                return $"10⁻{ConvertToSuperscript(Math.Abs(exponent), superscripts)}";

            return $"10{ConvertToSuperscript(exponent, superscripts)}";
        }

        private static string ConvertToSuperscript(int number, string[] superscripts)
        {
            string numStr = number.ToString();
            string result = "";
            foreach (char digit in numStr)
            {
                result += superscripts[digit - '0'];
            }
            return result;
        }
    }
}
