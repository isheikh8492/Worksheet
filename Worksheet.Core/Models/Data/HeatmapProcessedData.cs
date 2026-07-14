using System;

namespace Worksheet.Core.Models.Data
{
    using Worksheet.Core.Models;

    public class HeatmapProcessedData : ProcessedPlotData
    {
        public HeatmapProcessedData(Guid plotId, double[,] data, byte[] pixelBuffer, int bins, int pixelWidth, int pixelHeight, bool isEmpty)
            : base(plotId, PlotType.Pseudocolor)
        {
            Data = data;
            PixelBuffer = pixelBuffer;
            Bins = bins;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            IsEmpty = isEmpty;
        }

        public double[,] Data { get; }
        public byte[] PixelBuffer { get; }
        public int Bins { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public bool IsEmpty { get; }
    }
}
