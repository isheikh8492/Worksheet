using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

namespace Worksheet.Tests;

public sealed class ChasmLifecycleTests
{
    [Fact]
    public async Task StopStreamingStopsProducerAndCancelsConsumer()
    {
        var producer = new RecordingProducer();
        var consumer = new RecordingConsumer();
        var dataSource = new ChasmDataSource(new DataSource());
        using var chasm = new ChasmEngine(producer, consumer, dataSource);

        chasm.StartStreaming();
        await consumer.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        chasm.StopStreaming();
        await consumer.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(producer.StartCalled);
        Assert.True(producer.StopCalled);
        Assert.False(chasm.IsStreamingEnabled);
    }

    private sealed class RecordingProducer : IProducer
    {
        private readonly Channel<IEventBatch> _channel = Channel.CreateUnbounded<IEventBatch>();

        public ChannelReader<IEventBatch> Reader => _channel.Reader;

        public bool StartCalled { get; private set; }

        public bool StopCalled { get; private set; }

        public void Start()
        {
            StartCalled = true;
        }

        public void Stop()
        {
            StopCalled = true;
        }
    }

    private sealed class RecordingConsumer : IConsumer
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RunAsync(CancellationToken token)
        {
            Started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult();
            }
        }
    }
}
