using System;

namespace Worksheet.Core.Models.Data
{
    using Worksheet.Core.Models;

    public class OscilloscopeProcessedData : ProcessedPlotData
    {
        public OscilloscopeProcessedData(Guid plotId, double[][] signals)
            : this(plotId, signals, Array.Empty<int>(), timestampCount: signals.Length > 0 ? signals[0].Length : 0, isEmpty: signals.Length == 0)
        {
        }

        public OscilloscopeProcessedData(Guid plotId, double[][] signals, int[] channelIndices, int timestampCount, bool isEmpty)
            : base(plotId, PlotType.Oscilloscope)
        {
            Signals = signals ?? throw new ArgumentNullException(nameof(signals));
            ChannelIndices = channelIndices ?? throw new ArgumentNullException(nameof(channelIndices));
            TimestampCount = timestampCount;
            IsEmpty = isEmpty;
        }

        public double[][] Signals { get; }

        public int[] ChannelIndices { get; }

        public int TimestampCount { get; }

        public bool IsEmpty { get; }
    }
}
