using System;

namespace Worksheet.App.Views.Support
{
    /// <summary>Snaps a coordinate to the nearest half-grid increment.</summary>
    internal static class GridSnap
    {
        public static double Snap(double value, double gridSize)
        {
            double increment = gridSize / 2.0;
            if (increment <= 0)
                return value;

            return Math.Round(value / increment) * increment;
        }
    }
}
