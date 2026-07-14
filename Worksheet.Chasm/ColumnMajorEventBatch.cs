using System;
using Worksheet.Core.Models;

namespace Worksheet.Chasm
{
    public sealed class ColumnMajorEventBatch : IEventBatch
    {
        public int Count { get; }
        public double[] Values { get; }
        public int SignalCount { get; }

        public ColumnMajorEventBatch(int count, double[] values)
            : this(count, values, SignalLayout.Default)
        {
        }

        public ColumnMajorEventBatch(int count, double[] values, SignalLayout signalLayout)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            int expectedLength = checked(signalLayout.SignalCount * count);
            if (values.Length != expectedLength)
                throw new ArgumentException($"Values must have length {expectedLength}.", nameof(values));

            Count = count;
            Values = values;
            SignalCount = signalLayout.SignalCount;
        }

        public ReadOnlySpan<double> GetSignalValues(int signalIndex)
        {
            if ((uint)signalIndex >= (uint)SignalCount)
                throw new ArgumentOutOfRangeException(nameof(signalIndex));

            return Values.AsSpan(signalIndex * Count, Count);
        }
    }
}
