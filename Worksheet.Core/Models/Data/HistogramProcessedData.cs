using System;

namespace Worksheet.Core.Models.Data
{
    using Worksheet.Core.Models;

    public class HistogramProcessedData : ProcessedPlotData
    {
        public HistogramProcessedData(Guid plotId, double[] positions, double[] counts, int binCount, ScaleType scaleType)
            : base(plotId, PlotType.Histogram)
        {
            Positions = positions;
            Counts = counts;
            BinCount = binCount;
            ScaleType = scaleType;
        }

        public double[] Positions { get; }
        public double[] Counts { get; }
        public int BinCount { get; }
        public ScaleType ScaleType { get; }
    }
}
