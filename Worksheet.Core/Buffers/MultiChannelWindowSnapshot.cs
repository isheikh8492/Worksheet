using System;

namespace Worksheet.Core.Buffers
{
    /// <summary>
    /// Live view over multiple retained signal columns in <see cref="DataSource"/>.
    /// Metadata is captured atomically, but <see cref="ChannelValues"/> references backing ring buffers.
    /// </summary>
    public readonly record struct MultiChannelWindowSnapshot(
        double[][] ChannelValues,
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

        public double ValueAt(int channelIndex, long sequence)
        {
            if ((uint)channelIndex >= (uint)ChannelValues.Length)
                throw new ArgumentOutOfRangeException(nameof(channelIndex), channelIndex, $"Channel index must be in [0, {ChannelValues.Length - 1}].");

            return ChannelValues[channelIndex][PhysicalIndexForSequence(sequence)];
        }

        public ChannelWindowSnapshot Channel(int channelIndex)
        {
            if ((uint)channelIndex >= (uint)ChannelValues.Length)
                throw new ArgumentOutOfRangeException(nameof(channelIndex), channelIndex, $"Channel index must be in [0, {ChannelValues.Length - 1}].");

            return new ChannelWindowSnapshot(
                Values: ChannelValues[channelIndex],
                StartIndex: StartIndex,
                Count: Count,
                Capacity: Capacity,
                Version: Version,
                StartSequence: StartSequence,
                EndSequence: EndSequence);
        }

        public static MultiChannelWindowSnapshot Empty { get; } = new(
            ChannelValues: Array.Empty<double[]>(),
            StartIndex: 0,
            Count: 0,
            Capacity: 1,
            Version: 0,
            StartSequence: 0,
            EndSequence: 0);
    }
}
