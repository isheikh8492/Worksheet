using System;
using System.Collections.Generic;

namespace Worksheet.Core.Models.Data
{
    using Worksheet.Core.Models;

    public class SpectralRibbonProcessedData : ProcessedPlotData
    {
        public SpectralRibbonProcessedData(Guid plotId, double[,] data, byte[] pixelBuffer, int bins, int channelCount, int pixelWidth, int pixelHeight, IReadOnlyList<string> channelNames, bool isEmpty)
            : base(plotId, PlotType.SpectralRibbon)
        {
            Data = data;
            PixelBuffer = pixelBuffer;
            Bins = bins;
            ChannelCount = channelCount;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            ChannelNames = channelNames;
            IsEmpty = isEmpty;
        }

        public double[,] Data { get; }
        public byte[] PixelBuffer { get; }
        public int Bins { get; }
        public int ChannelCount { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }

        public IReadOnlyList<string> ChannelNames { get; }

        public bool IsEmpty { get; }
    }
}
