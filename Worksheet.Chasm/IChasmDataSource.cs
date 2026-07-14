using Worksheet.Core.Services;
using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public interface IChasmDataSource : IChannelDataBuffer
    {
        void Append(IEventBatch batch);
        void ClearMemory();
        long DataVersion { get; }

        // Optional passthroughs (handy for UI)
        bool IsStreamingEnabled { get; }
        void SetStreamingEnabled(bool enabled);
        int WindowCapacity { get; }
        void SetWindowCapacity(int windowCapacity);
    }
}

