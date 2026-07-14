using System;
using Worksheet.Core.Models;

namespace Worksheet.Chasm
{
    public sealed class EventBatch : IEventBatch
    {
        public int Count { get; }
        public double[][] Channels { get; }
        public int SignalCount { get; }

        public EventBatch(int count, double[][] channels)
            : this(count, channels, SignalLayout.Default)
        {
        }

        public EventBatch(int count, double[][] channels, SignalLayout signalLayout)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (channels == null)
                throw new ArgumentNullException(nameof(channels));
            if (channels.Length != signalLayout.SignalCount)
                throw new ArgumentException($"Channels must have length {signalLayout.SignalCount}.", nameof(channels));

            for (int c = 0; c < channels.Length; c++)
            {
                if (channels[c] == null)
                    throw new ArgumentException($"Channels[{c}] is null.", nameof(channels));
                if (channels[c].Length != count)
                    throw new ArgumentException($"Channels[{c}] length must equal count.", nameof(channels));
            }

            Count = count;
            Channels = channels;
            SignalCount = signalLayout.SignalCount;
        }
    }
}
