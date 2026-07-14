namespace Worksheet.Core.Buffers
{
    public interface IChannelDataBuffer
    {
        ChannelWindowSnapshot GetSnapshot(int featureIndex);
        ChannelWindowSnapshot GetSnapshotCopy(int featureIndex);
        MultiChannelWindowSnapshot GetSnapshot(params int[] featureIndices);
        MultiChannelWindowSnapshot GetSnapshotCopy(params int[] featureIndices);
    }
}

