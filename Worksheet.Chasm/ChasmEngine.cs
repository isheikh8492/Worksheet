using System;
using System.Threading;
using System.Threading.Tasks;

namespace Worksheet.Chasm
{
    public sealed class ChasmEngine : IDisposable
    {
        private static readonly TimeSpan StopWaitTimeout = TimeSpan.FromMilliseconds(250);

        private readonly IProducer _producer;
        private readonly IConsumer _consumer;
        private readonly IChasmDataSource _dataSource;

        private CancellationTokenSource? _consumerCts;
        private Task? _consumerTask;

        public ChasmEngine(IProducer producer, IConsumer consumer, IChasmDataSource dataSource)
        {
            _producer = producer;
            _consumer = consumer;
            _dataSource = dataSource;
        }

        public bool IsStreamingEnabled { get; private set; }

        public long DataVersion => _dataSource.DataVersion;

        public int WindowCapacity => _dataSource.WindowCapacity;

        public void StartStreaming()
        {
            if (IsStreamingEnabled)
                return;

            IsStreamingEnabled = true;
            _dataSource.SetStreamingEnabled(true);

            _consumerCts = new CancellationTokenSource();
            _consumerTask = Task.Run(() => _consumer.RunAsync(_consumerCts.Token));

            _producer.Start();
        }

        public void StopStreaming()
        {
            if (!IsStreamingEnabled)
                return;

            IsStreamingEnabled = false;
            _dataSource.SetStreamingEnabled(false);

            _producer.Stop();
            _consumerCts?.Cancel();
            StoppableTask.Observe(_consumerTask, StopWaitTimeout, "ChasmEngine.StopStreaming");

            // Drain any queued batches so restart doesn't replay stale data.
            while (_producer.Reader.TryRead(out _)) { }

            _consumerCts?.Dispose();
            _consumerCts = null;
            _consumerTask = null;
        }

        public void ClearMemory() => _dataSource.ClearMemory();

        public void SetWindowCapacity(int windowCapacity) => _dataSource.SetWindowCapacity(windowCapacity);

        public void Dispose()
        {
            StopStreaming();
            _consumerCts?.Dispose();

            if (_producer is IDisposable disposableProducer)
                disposableProducer.Dispose();
        }

    }
}

