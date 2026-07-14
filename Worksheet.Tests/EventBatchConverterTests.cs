using System.Threading.Channels;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

using Worksheet.Core.Buffers;
namespace Worksheet.Tests;

public sealed class EventBatchConverterTests
{
    [Fact]
    public void ConvertSkipsEmptyEventBatches()
    {
        var converter = CreateConverter(signalCount: 3);

        var batches = converter.Convert([]);

        Assert.Empty(batches);
    }

    [Fact]
    public void ConvertStoresEventObjectsInColumnMajorOrder()
    {
        var converter = CreateConverter(signalCount: 3);
        var events = new[]
        {
            EventFactory.CreateEvent(10, 20, 30),
            EventFactory.CreateEvent(11, 21, 31),
            EventFactory.CreateEvent(12, 22, 32),
        };

        var batch = Assert.Single(converter.Convert(events));

        Assert.Equal(3, batch.Count);
        Assert.Equal(3, batch.SignalCount);
        Assert.Equal(
            [
                10, 11, 12,
                20, 21, 22,
                30, 31, 32,
            ],
            batch.Values);
    }

    [Fact]
    public void ConvertSplitsOversizedEventBatches()
    {
        var converter = CreateConverter(signalCount: 2, maxBatchSize: 2);
        var events = new[]
        {
            EventFactory.CreateEvent(10, 20),
            EventFactory.CreateEvent(11, 21),
            EventFactory.CreateEvent(12, 22),
        };

        var batches = converter.Convert(events);

        Assert.Equal(2, batches.Count);
        Assert.Equal(2, batches[0].Count);
        Assert.Equal([10, 11, 20, 21], batches[0].Values);
        Assert.Equal(1, batches[1].Count);
        Assert.Equal([12, 22], batches[1].Values);
    }

    [Fact]
    public void ConvertStoresEventObjectsInColumnMajorOrderWhenParallelPathIsUsed()
    {
        var converter = CreateConverter(signalCount: 3, parallelCellThreshold: 1);
        var events = new[]
        {
            EventFactory.CreateEvent(10, 20, 30),
            EventFactory.CreateEvent(11, 21, 31),
            EventFactory.CreateEvent(12, 22, 32),
        };

        var batch = Assert.Single(converter.Convert(events));

        Assert.Equal(
            [
                10, 11, 12,
                20, 21, 22,
                30, 31, 32,
            ],
            batch.Values);
    }

    [Fact]
    public void ConvertUsesParametersAndIgnoresAnalogCapture()
    {
        var converter = CreateConverter(signalCount: 2);
        var events = new[]
        {
            new Event([10, 20], new AnalogCapture([100, 101, 102, 103], channelCount: 2, timestampCount: 2)),
            new Event([11, 21], new AnalogCapture([200, 201, 202, 203], channelCount: 2, timestampCount: 2)),
        };

        var batch = Assert.Single(converter.Convert(events));

        Assert.Equal([10, 11, 20, 21], batch.Values);
    }

    [Fact]
    public void ConverterExposesParallelThreshold()
    {
        var converter = CreateConverter(signalCount: 3, parallelCellThreshold: 123);

        Assert.Equal(123, converter.ParallelCellThreshold);
    }

    [Fact]
    public void ConvertRejectsEventsThatDoNotMatchLayout()
    {
        var layout = new SignalLayout(1, 1, 3);
        var converter = new EventBatchConverter(layout);

        var ex = Assert.Throws<ArgumentException>(() => converter.Convert([EventFactory.CreateEvent(10, 20)]));

        Assert.Contains("expected 3", ex.Message);
    }

    [Fact]
    public void TryWriteToWritesConvertedBatchesToChasmChannel()
    {
        var converter = CreateConverter(signalCount: 2, maxBatchSize: 2);
        var channel = Channel.CreateUnbounded<IEventBatch>();
        var events = new[]
        {
            EventFactory.CreateEvent(10, 20),
            EventFactory.CreateEvent(11, 21),
            EventFactory.CreateEvent(12, 22),
        };

        int written = converter.TryWriteTo(channel.Writer, events);

        Assert.Equal(2, written);
        Assert.True(channel.Reader.TryRead(out var firstBatch));
        Assert.True(channel.Reader.TryRead(out var secondBatch));

        var first = Assert.IsType<ColumnMajorEventBatch>(firstBatch);
        var second = Assert.IsType<ColumnMajorEventBatch>(secondBatch);
        Assert.Equal([10, 11, 20, 21], first.Values);
        Assert.Equal([12, 22], second.Values);
    }

    private static EventBatchConverter CreateConverter(
        int signalCount,
        int maxBatchSize = 1000,
        int parallelCellThreshold = EventBatchConverter.DefaultParallelCellThreshold)
    {
        var layout = new SignalLayout(1, 1, signalCount);
        return new EventBatchConverter(
            layout,
            maxBatchSize,
            parallelCellThreshold);
    }
}
