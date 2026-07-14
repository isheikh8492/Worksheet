using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Worksheet.Chasm
{
    public sealed class ChasmConsumer : IConsumer
    {
        private readonly ChannelReader<IEventBatch> _reader;
        private readonly IChasmDataSource _dataSource;

        public ChasmConsumer(ChannelReader<IEventBatch> reader, IChasmDataSource dataSource)
        {
            _reader = reader;
            _dataSource = dataSource;
        }

        public async Task RunAsync(CancellationToken token)
        {
            await foreach (var batch in _reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                _dataSource.Append(batch);
            }
        }
    }
}

