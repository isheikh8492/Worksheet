using System;
using System.Collections.Generic;
using ScottPlot;

namespace Worksheet.Views.PlotViews.Axes
{
    public class FixedTickGenerator : ITickGenerator
    {
        private readonly Tick[] _ticks;

        public FixedTickGenerator(double[] majorPositions, string[] majorLabels, double[] minorPositions)
        {
            if (majorPositions == null) throw new ArgumentNullException(nameof(majorPositions));
            if (majorLabels == null) throw new ArgumentNullException(nameof(majorLabels));
            if (minorPositions == null) throw new ArgumentNullException(nameof(minorPositions));
            if (majorPositions.Length != majorLabels.Length)
                throw new ArgumentException("Major positions and labels must have the same length.");

            var ticks = new List<Tick>(majorPositions.Length + minorPositions.Length);

            for (int i = 0; i < majorPositions.Length; i++)
                ticks.Add(new Tick(majorPositions[i], majorLabels[i], true));

            for (int i = 0; i < minorPositions.Length; i++)
                ticks.Add(new Tick(minorPositions[i], string.Empty, false));

            ticks.Sort((a, b) => a.Position.CompareTo(b.Position));
            _ticks = ticks.ToArray();

            Ticks = _ticks;
            MaxTickCount = _ticks.Length;
        }

        public Tick[] Ticks { get; private set; }

        public int MaxTickCount { get; set; }

        public void Regenerate(CoordinateRange range, Edge edge, PixelLength length, Paint paint, LabelStyle labelStyle)
        {
            Ticks = _ticks;
        }
    }
}
