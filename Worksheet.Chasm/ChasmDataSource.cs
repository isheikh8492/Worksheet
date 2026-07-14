using System;

using Worksheet.Core.Services;
using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    // Adapter over Viewport/DataSource buffer.
    public sealed class ChasmDataSource : IChasmDataSource
    {
        private readonly DataSource _dataSource;

        public ChasmDataSource(DataSource dataSource)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        public void Append(IEventBatch batch)
        {
            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            if (batch.Count <= 0)
                return;

            switch (batch)
            {
                case EventBatch eventBatch:
                    _dataSource.AppendBatch(eventBatch.Channels, eventBatch.Count);
                    break;
                case ColumnMajorEventBatch columnMajorBatch:
                    _dataSource.AppendBatch(columnMajorBatch);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported event batch type {batch.GetType().FullName}.");
            }
        }

        public void ClearMemory() => _dataSource.ClearMemory();

        public long DataVersion => _dataSource.DataVersion;

        public bool IsStreamingEnabled => _dataSource.IsStreamingEnabled;

        public void SetStreamingEnabled(bool enabled) => _dataSource.SetStreamingEnabled(enabled);

        public int WindowCapacity => _dataSource.WindowCapacity;

        public void SetWindowCapacity(int windowCapacity) => _dataSource.ResizeWindow(windowCapacity);

        public ChannelWindowSnapshot GetSnapshot(int featureIndex) => _dataSource.GetSnapshot(featureIndex);

        public ChannelWindowSnapshot GetSnapshotCopy(int featureIndex) => _dataSource.GetSnapshotCopy(featureIndex);

        public MultiChannelWindowSnapshot GetSnapshot(params int[] featureIndices) => _dataSource.GetSnapshot(featureIndices);

        public MultiChannelWindowSnapshot GetSnapshotCopy(params int[] featureIndices) => _dataSource.GetSnapshotCopy(featureIndices);
    }
}

