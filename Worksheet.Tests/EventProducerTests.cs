using System;
using System.Threading.Tasks;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

using Worksheet.Core.Buffers;
namespace Worksheet.Tests;

public sealed class EventProducerTests
{
    [Fact]
    public void PublishReturnsZeroWhenProducerIsStopped()
    {
        var producer = new EventProducer(new SignalLayout(1, 1, 2), channelCapacityBatches: 2);

        int written = producer.Publish([EventFactory.CreateEvent(10, 20)]);

        Assert.Equal(0, written);
        Assert.False(producer.Reader.TryRead(out _));
    }

    [Fact]
    public void PublishConvertsEventsToColumnMajorBatches()
    {
        var producer = new EventProducer(
            new SignalLayout(1, 1, 2),
            channelCapacityBatches: 4,
            maxBatchSize: 2);

        producer.Start();

        int written = producer.Publish(
            [
                EventFactory.CreateEvent(10, 20),
                EventFactory.CreateEvent(11, 21),
                EventFactory.CreateEvent(12, 22),
            ]);

        Assert.Equal(2, written);
        Assert.True(producer.Reader.TryRead(out var firstBatch));
        Assert.True(producer.Reader.TryRead(out var secondBatch));

        var first = Assert.IsType<ColumnMajorEventBatch>(firstBatch);
        var second = Assert.IsType<ColumnMajorEventBatch>(secondBatch);
        Assert.Equal([10, 11, 20, 21], first.Values);
        Assert.Equal([12, 22], second.Values);
    }

    [Fact]
    public void PublishEventsIsAvailableThroughIngestionPort()
    {
        IEventIngestionPort port = new EventProducer(
            new SignalLayout(1, 1, 2),
            channelCapacityBatches: 4,
            maxBatchSize: 2);
        var producer = Assert.IsType<EventProducer>(port);

        producer.Start();

        int written = port.PublishEvents([EventFactory.CreateEvent(10, 20)]);

        Assert.Equal(1, written);
        Assert.True(producer.Reader.TryRead(out var batch));
        var columnMajor = Assert.IsType<ColumnMajorEventBatch>(batch);
        Assert.Equal([10, 20], columnMajor.Values);
    }

    [Fact]
    public void PublishColumnMajorWritesFlatBatchDirectly()
    {
        var producer = new EventProducer(new SignalLayout(1, 1, 2), channelCapacityBatches: 2);
        var values = new double[] { 10, 11, 20, 21 };

        producer.Start();

        int written = producer.PublishColumnMajor(values, eventCount: 2);

        Assert.Equal(1, written);
        Assert.True(producer.Reader.TryRead(out var batch));
        var columnMajor = Assert.IsType<ColumnMajorEventBatch>(batch);
        Assert.Same(values, columnMajor.Values);
        Assert.Equal(2, columnMajor.Count);
    }

    [Fact]
    public void PublishColumnMajorRejectsWrongFlatBufferLength()
    {
        var producer = new EventProducer(new SignalLayout(1, 1, 2), channelCapacityBatches: 2);
        producer.Start();

        Assert.Throws<ArgumentException>(() => producer.PublishColumnMajor([10, 11, 20], eventCount: 2));
    }

    [Fact]
    public void PublishEventsPublishesAnalogCapturesToSink()
    {
        var buffer = new OscilloscopeBuffer(capacity: 3);
        var producer = new EventProducer(
            new SignalLayout(1, 1, 2),
            channelCapacityBatches: 4,
            analogCaptureSink: buffer);
        var first = new Event([10, 20], EventFactory.CreateAnalogCapture(1));
        var second = new Event([11, 21], EventFactory.CreateAnalogCapture(2));

        producer.Start();

        int written = producer.PublishEvents([first, second]);

        Assert.Equal(1, written);
        Assert.Equal(2, buffer.Version);
        Assert.True(buffer.TryGetLatest(out var latest));
        Assert.Same(second.AnalogCapture, latest);
    }

    [Fact]
    public void PublishColumnMajorDoesNotPublishAnalogCaptures()
    {
        var buffer = new OscilloscopeBuffer(capacity: 3);
        var producer = new EventProducer(
            new SignalLayout(1, 1, 2),
            channelCapacityBatches: 4,
            analogCaptureSink: buffer);

        producer.Start();

        int written = producer.PublishColumnMajor([10, 11, 20, 21], eventCount: 2);

        Assert.Equal(1, written);
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.Version);
    }

    [Fact]
    public async Task ChasmCapturesPublishedEventsEndToEnd()
    {
        var layout = new SignalLayout(1, 1, 3);
        var source = new DataSource(layout, windowCapacity: 10);
        var chasmSource = new ChasmDataSource(source);
        var producer = new EventProducer(layout, channelCapacityBatches: 4, maxBatchSize: 10);
        var consumer = new ChasmConsumer(producer.Reader, chasmSource);
        using var chasm = new ChasmEngine(producer, consumer, chasmSource);

        chasm.StartStreaming();
        int written = producer.Publish(
            [
                EventFactory.CreateEvent(10, 20, 30),
                EventFactory.CreateEvent(11, 21, 31),
                EventFactory.CreateEvent(12, 22, 32),
            ]);

        await WaitUntilAsync(() => source.TotalEventsIngested == 3);
        chasm.StopStreaming();

        Assert.Equal(1, written);
        Assert.Equal(3, source.TotalEventsIngested);

        var snapshot = source.GetSnapshot(layout.ToIndex(0, 0, 1));
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(20, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(21, snapshot.Values[snapshot.PhysicalIndexForSequence(1)]);
        Assert.Equal(22, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
    }

    [Fact]
    public async Task ChasmCapturesPublishedColumnMajorEventsEndToEnd()
    {
        var layout = new SignalLayout(1, 1, 3);
        var source = new DataSource(layout, windowCapacity: 10);
        var chasmSource = new ChasmDataSource(source);
        var producer = new EventProducer(layout, channelCapacityBatches: 4, maxBatchSize: 10);
        var consumer = new ChasmConsumer(producer.Reader, chasmSource);
        using var chasm = new ChasmEngine(producer, consumer, chasmSource);

        chasm.StartStreaming();
        int written = producer.PublishColumnMajor(
            [
                10, 11, 12,
                20, 21, 22,
                30, 31, 32,
            ],
            eventCount: 3);

        await WaitUntilAsync(() => source.TotalEventsIngested == 3);
        chasm.StopStreaming();

        Assert.Equal(1, written);
        Assert.Equal(3, source.TotalEventsIngested);

        var snapshot = source.GetSnapshot(layout.ToIndex(0, 0, 1));
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(20, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(21, snapshot.Values[snapshot.PhysicalIndexForSequence(1)]);
        Assert.Equal(22, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Condition was not reached before timeout.");

            await Task.Delay(10);
        }
    }
}
