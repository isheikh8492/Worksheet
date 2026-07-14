using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class IngestionProfileTests
{
    private const int BatchCount = 20;
    private const int BatchSize = 1_000;
    private readonly ITestOutputHelper _output;

    public IngestionProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileIngestionStorageBandwidth(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var batch = CreateBatch(layout.SignalCount, BatchSize, offset: 0);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                source.AppendBatch(batch, BatchSize);
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Append prebuilt {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileFlatColumnMajorStorageBandwidth(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var batch = CreateFlatColumnMajorBatch(layout.SignalCount, BatchSize, offset: 0);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                source.AppendBatchColumnMajor(batch, BatchSize);
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Append prebuilt flat column-major {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileIngestionGenerateAndStoreThroughput(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                var batch = CreateBatch(layout.SignalCount, BatchSize, offset: i * BatchSize);
                source.AppendBatch(batch, BatchSize);
            }
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Generate+append {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileBatchAllocationOnly(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        double[][]? last = null;

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                last = AllocateBatch(layout.SignalCount, BatchSize);
        });

        Assert.NotNull(last);
        WriteThroughput($"Allocate batch arrays {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileFlatColumnMajorAllocationOnly(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        double[]? last = null;

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                last = AllocateFlatColumnMajorBatch(layout.SignalCount, BatchSize);
        });

        Assert.NotNull(last);
        WriteThroughput($"Allocate flat column-major {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileFillReusedBatchOnly(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var batch = AllocateBatch(layout.SignalCount, BatchSize);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                FillBatch(batch, offset: i * BatchSize);
        });

        WriteThroughput($"Fill reused batch {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileFillReusedFlatColumnMajorOnly(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var batch = AllocateFlatColumnMajorBatch(layout.SignalCount, BatchSize);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                FillFlatColumnMajorBatch(batch, layout.SignalCount, BatchSize, offset: i * BatchSize);
        });

        WriteThroughput($"Fill reused flat column-major {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileFillReusedBatchAndAppend(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var batch = AllocateBatch(layout.SignalCount, BatchSize);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                FillBatch(batch, offset: i * BatchSize);
                source.AppendBatch(batch, BatchSize);
            }
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Fill reused+append {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileFillReusedFlatColumnMajorAndAppend(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var batch = AllocateFlatColumnMajorBatch(layout.SignalCount, BatchSize);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                FillFlatColumnMajorBatch(batch, layout.SignalCount, BatchSize, offset: i * BatchSize);
                source.AppendBatchColumnMajor(batch, BatchSize);
            }
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Fill reused flat column-major+append {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 51)]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileEventConversionToColumnMajor(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var converter = new EventBatchConverter(layout, maxBatchSize: BatchSize);
        var events = CreateEvents(layout.SignalCount, BatchSize, offset: 0);
        IReadOnlyList<ColumnMajorEventBatch> batches = Array.Empty<ColumnMajorEventBatch>();

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                batches = converter.Convert(events);
        });

        var batch = Assert.Single(batches);
        Assert.Equal(BatchSize, batch.Count);
        WriteThroughput($"Event convert to flat column-major {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 51)]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileEventConversionAndAppend(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var converter = new EventBatchConverter(layout, maxBatchSize: BatchSize);
        var events = CreateEvents(layout.SignalCount, BatchSize, offset: 0);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                var batch = Assert.Single(converter.Convert(events));
                source.AppendBatch(batch);
            }
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Event convert+append {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 51)]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileEventProducerPublish(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var producer = new EventProducer(
            layout,
            channelCapacityBatches: BatchCount,
            maxBatchSize: BatchSize);
        var events = CreateEvents(layout.SignalCount, BatchSize, offset: 0);

        producer.Start();

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                producer.Publish(events);
        });

        producer.Stop();

        WriteThroughput($"EventProducer publish {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 51)]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileEventProducerPublishColumnMajor(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var producer = new EventProducer(
            layout,
            channelCapacityBatches: BatchCount,
            maxBatchSize: BatchSize);
        var values = CreateFlatColumnMajorBatch(layout.SignalCount, BatchSize, offset: 0);

        producer.Start();

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                producer.PublishColumnMajor(values, BatchSize);
        });

        producer.Stop();

        WriteThroughput($"EventProducer publish flat column-major {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData("histogram", 1, 1, 51, 1)]
    [InlineData("pseudocolor", 1, 1, 51, 2)]
    [InlineData("spectral", 1, 1, 51, 42)]
    [InlineData("large-pseudocolor", 6, 9, 60, 2)]
    [InlineData("large-spectral", 6, 9, 60, 42)]
    public void ProfileSnapshotCopyCost(string label, int lasers, int features, int channels, int selectedSignals)
    {
        var layout = new SignalLayout(lasers, features, channels);
        int eventCount = BatchCount * BatchSize;
        var source = new DataSource(layout, windowCapacity: eventCount);
        var batch = CreateFlatColumnMajorBatch(layout.SignalCount, BatchSize, offset: 0);
        for (int i = 0; i < BatchCount; i++)
            source.AppendBatchColumnMajor(batch, BatchSize);

        int[] selected = Enumerable.Range(0, Math.Min(selectedSignals, layout.SignalCount)).ToArray();

        var liveElapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                if (selected.Length == 1)
                    _ = source.GetSnapshot(selected[0]);
                else
                    _ = source.GetSnapshot(selected);
            }
        });

        var copyElapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                if (selected.Length == 1)
                    _ = source.GetSnapshotCopy(selected[0]);
                else
                    _ = source.GetSnapshotCopy(selected);
            }
        });

        WriteSnapshotTiming($"Snapshot {label} live {lasers}x{features}x{channels} selected={selected.Length}", layout, selected.Length, eventCount, liveElapsed);
        WriteSnapshotTiming($"Snapshot {label} copy {lasers}x{features}x{channels} selected={selected.Length}", layout, selected.Length, eventCount, copyElapsed);
    }

    private static TimeSpan Measure(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private void WriteThroughput(string label, SignalLayout layout, int eventCount, TimeSpan elapsed)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 1e-9);
        double eventsPerSecond = eventCount / seconds;
        double bytesPerSecond = eventsPerSecond * layout.SignalCount * sizeof(double);
        double mebibytesPerSecond = bytesPerSecond / (1024 * 1024);
        double rawPayloadMiB = (double)eventCount * layout.SignalCount * sizeof(double) / (1024 * 1024);

        string line = $"{label}: {elapsed.TotalMilliseconds:F2} ms, {eventsPerSecond:F0} events/sec, {mebibytesPerSecond:F1} MiB/sec raw, {rawPayloadMiB:F1} MiB payload";
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private void WriteSnapshotTiming(string label, SignalLayout layout, int selectedSignals, int eventCount, TimeSpan elapsed)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 1e-9);
        double snapshotsPerSecond = BatchCount / seconds;
        double copiedMiB = (double)selectedSignals * eventCount * sizeof(double) / (1024 * 1024);
        string line = $"{label}: {elapsed.TotalMilliseconds:F2} ms for {BatchCount} snapshots, {snapshotsPerSecond:F0} snapshots/sec, {copiedMiB:F1} MiB logical payload";
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private static double[][] CreateBatch(int signalCount, int count, int offset)
    {
        var signals = AllocateBatch(signalCount, count);
        FillBatch(signals, offset);
        return signals;
    }

    private static double[] CreateFlatColumnMajorBatch(int signalCount, int count, int offset)
    {
        var values = AllocateFlatColumnMajorBatch(signalCount, count);
        FillFlatColumnMajorBatch(values, signalCount, count, offset);
        return values;
    }

    private static Event[] CreateEvents(int signalCount, int count, int offset)
    {
        var events = new Event[count];
        for (int e = 0; e < count; e++)
        {
            var values = new double[signalCount];
            for (int s = 0; s < signalCount; s++)
                values[s] = 1 + (((offset + e + 1) * (s + 3) * 7919) % 100_000_000);

            events[e] = new Event(values, EventFactory.CreateAnalogCapture(e));
        }

        return events;
    }

    private static double[][] AllocateBatch(int signalCount, int count)
    {
        var signals = new double[signalCount][];
        for (int s = 0; s < signals.Length; s++)
            signals[s] = new double[count];

        return signals;
    }

    private static double[] AllocateFlatColumnMajorBatch(int signalCount, int count)
    {
        return new double[signalCount * count];
    }

    private static void FillBatch(double[][] signals, int offset)
    {
        for (int s = 0; s < signals.Length; s++)
        {
            var values = signals[s];
            for (int e = 0; e < values.Length; e++)
                values[e] = 1 + (((offset + e + 1) * (s + 3) * 7919) % 100_000_000);
        }
    }

    private static void FillFlatColumnMajorBatch(double[] values, int signalCount, int count, int offset)
    {
        for (int s = 0; s < signalCount; s++)
        {
            int signalOffset = s * count;
            for (int e = 0; e < count; e++)
                values[signalOffset + e] = 1 + (((offset + e + 1) * (s + 3) * 7919) % 100_000_000);
        }
    }
}
