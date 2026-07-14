using System;

namespace Worksheet.Core.Buffers
{
    public sealed class AnalogCapture
    {
        public AnalogCapture(double[] values, int channelCount, int timestampCount)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (channelCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(channelCount));
            if (timestampCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(timestampCount));

            int expectedLength = checked(channelCount * timestampCount);
            if (values.Length != expectedLength)
                throw new ArgumentException($"Values must have length {expectedLength}.", nameof(values));

            Values = values;
            ChannelCount = channelCount;
            TimestampCount = timestampCount;
        }

        public double[] Values { get; }

        public int ChannelCount { get; }

        public int TimestampCount { get; }

        public double GetValue(int channelIndex, int timestampIndex)
        {
            if ((uint)channelIndex >= (uint)ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            if ((uint)timestampIndex >= (uint)TimestampCount)
                throw new ArgumentOutOfRangeException(nameof(timestampIndex));

            return Values[(channelIndex * TimestampCount) + timestampIndex];
        }
    }
}
