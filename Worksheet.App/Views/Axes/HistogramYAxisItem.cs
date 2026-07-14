using System;
using System.Collections.Generic;
using System.Globalization;
using ScottPlot.WPF;

namespace Worksheet.App.Views.Axes
{
    internal sealed class HistogramYAxisItem
    {
        private const int MinorTicksPerMajorInterval = 4;
        private static readonly double[] NormalizedTickPositions = [0, 0.2, 0.4, 0.6, 0.8, 1.0];
        private static readonly double[] NormalizedMinorTickPositions = BuildNormalizedMinorTickPositions();

        public void Apply(WpfPlot plot, double upperBound)
        {
            var labels = new string[NormalizedTickPositions.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = FormatTickLabel(NormalizedTickPositions[i] * upperBound);

            plot.Plot.Axes.Left.TickGenerator = new FixedTickGenerator(
                NormalizedTickPositions,
                labels,
                NormalizedMinorTickPositions);
            plot.Plot.Axes.SetLimitsY(0, 1);
        }

        public double GetSnappedUpperBound(double maxCount)
        {
            if (maxCount <= 0)
                return 1000;

            double padded = Math.Max(1000, maxCount * 1.05);
            double exponent = Math.Floor(Math.Log10(padded));
            double magnitude = Math.Pow(10, exponent);
            double normalized = padded / magnitude;

            double snappedNormalized = normalized switch
            {
                <= 1 => 1,
                <= 2 => 2,
                <= 5 => 5,
                _ => 10
            };

            return snappedNormalized * magnitude;
        }

        private static double[] BuildNormalizedMinorTickPositions()
        {
            var positions = new List<double>((NormalizedTickPositions.Length - 1) * MinorTicksPerMajorInterval);
            for (int i = 0; i < NormalizedTickPositions.Length - 1; i++)
            {
                double start = NormalizedTickPositions[i];
                double end = NormalizedTickPositions[i + 1];
                double step = (end - start) / (MinorTicksPerMajorInterval + 1);
                for (int minorIndex = 1; minorIndex <= MinorTicksPerMajorInterval; minorIndex++)
                    positions.Add(start + (step * minorIndex));
            }

            return positions.ToArray();
        }

        private static string FormatTickLabel(double value)
        {
            if (value >= 1_000_000)
                return $"{value / 1_000_000:0.#}M";
            if (value >= 1_000)
                return $"{value / 1_000:0.#}k";
            return value.ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
