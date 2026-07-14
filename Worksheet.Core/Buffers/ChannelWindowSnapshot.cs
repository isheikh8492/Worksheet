using System;

namespace Worksheet.Core.Buffers
{
    /// <summary>
    /// Live view over one retained signal column in <see cref="DataSource"/>.
    /// Metadata is captured atomically, but <see cref="Values"/> references the backing ring buffer.
    /// </summary>
    public readonly record struct ChannelWindowSnapshot(
        double[] Values,
        int StartIndex,
        int Count,
        int Capacity,
        long Version,
        long StartSequence,
        long EndSequence)
    {
        public bool IsContiguous => Count == 0 || StartIndex + Count <= Capacity;

        public int PhysicalIndexAt(int logicalIndex)
        {
            if ((uint)logicalIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(logicalIndex), logicalIndex, $"Index must be in [0, {Count - 1}].");

            return (StartIndex + logicalIndex) % Capacity;
        }

        public int PhysicalIndexForSequence(long sequence)
        {
            if (sequence < StartSequence || sequence >= EndSequence)
                throw new ArgumentOutOfRangeException(nameof(sequence), sequence, $"Sequence must be in [{StartSequence}, {EndSequence - 1}].");

            return PhysicalIndexAt((int)(sequence - StartSequence));
        }
    }
}
