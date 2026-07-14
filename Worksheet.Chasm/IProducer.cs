using System.Threading.Channels;

namespace Worksheet.Chasm
{
    public interface IProducer
    {
        ChannelReader<IEventBatch> Reader { get; }
        void Start();
        void Stop();
    }
}

