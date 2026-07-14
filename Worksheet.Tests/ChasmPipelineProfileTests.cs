using System;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class ChasmPipelineProfileTests
{
    private const int BatchCount = 40;
    private const int BatchSize = 500;
    private const int ProductionChannelCapacityBatches = 8;

    private readonly ITestOutputHelper _output;

    public ChasmPipelineProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public async Task ProfileChasmPipelineNoDropPrebuiltBatches(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var result = await RunPipelineAsync(
            layout,
            channelCapacityBatches: BatchCount,
            generateEachBatch: false);

        Assert.Equal(result.ProducedEvents, result.CapturedEvents);
        WriteResult($"CHASM no-drop prebuilt {lasers}x{features}x{channels}", layout, result);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public async Task ProfileChasmPipelineNoDropFlatColumnMajorPrebuiltBatches(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var result = await RunPipelineAsync(
            layout,
            channelCapacityBatches: BatchCount,
            generateEachBatch: false,
            batchLayout: ProfileBatchLayout.FlatColumnMajor);

        Assert.Equal(result.ProducedEvents, result.CapturedEvents);
        WriteResult($"CHASM no-drop flat prebuilt {lasers}x{features}x{channels}", layout, result);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public async Task ProfileChasmPipelineNoDropGenerateAndCapture(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var result = await RunPipelineAsync(
            layout,
            channelCapacityBatches: BatchCount,
            generateEachBatch: true);

        Assert.Equal(result.ProducedEvents, result.CapturedEvents);
        WriteResult($"CHASM no-drop generate+capture {lasers}x{features}x{channels}", layout, result);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public async Task ProfileChasmPipelineNoDropFlatColumnMajorGenerateAndCapture(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var result = await RunPipelineAsync(
            layout,
            channelCapacityBatches: BatchCount,
            generateEachBatch: true,
            batchLayout: ProfileBatchLayout.FlatColumnMajor);

        Assert.Equal(result.ProducedEvents, result.CapturedEvents);
        WriteResult($"CHASM no-drop flat generate+capture {lasers}x{features}x{channels}", layout, result);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public async Task ProfileChasmPipelineProductionCapacityFlood(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var result = await RunPipelineAsync(
            layout,
            channelCapacityBatches: ProductionChannelCapacityBatches,
            generateEachBatch: false,
            batchLayout: ProfileBatchLayout.Jagged);

        Assert.InRange(result.CapturedEvents, 0, result.ProducedEvents);
        WriteResult($"CHASM cap8 flood prebuilt {lasers}x{features}x{channels}", layout, result);
    }

    private static async Task<PipelineProfileResult> RunPipelineAsync(
        SignalLayout layout,
        int channelCapacityBatches,
        bool generateEachBatch,
        ProfileBatchLayout batchLayout = ProfileBatchLayout.Jagged)
    {
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var chasmSource = new ChasmDataSource(source);
        var channel = Channel.CreateBounded<IEventBatch>(new BoundedChannelOptions(channelCapacityBatches)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        var consumer = new ChasmConsumer(channel.Reader, chasmSource);
        var consumerTask = consumer.RunAsync(default);
        var prebuilt = generateEachBatch ? null : CreateEventBatch(layout, offset: 0, batchLayout);

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < BatchCount; i++)
        {
            var batch = generateEachBatch
                ? CreateEventBatch(layout, offset: i * BatchSize, batchLayout)
                : prebuilt!;

            channel.Writer.TryWrite(batch);
        }

        channel.Writer.Complete();
        await consumerTask.ConfigureAwait(false);
        stopwatch.Stop();

        int producedEvents = BatchCount * BatchSize;
        long capturedEvents = source.TotalEventsIngested;
        return new PipelineProfileResult(producedEvents, capturedEvents, stopwatch.Elapsed);
    }

    private void WriteResult(string label, SignalLayout layout, PipelineProfileResult result)
    {
        double seconds = Math.Max(result.Elapsed.TotalSeconds, 1e-9);
        double capturedEventsPerSecond = result.CapturedEvents / seconds;
        double producedEventsPerSecond = result.ProducedEvents / seconds;
        double rawMiBPerSecond = capturedEventsPerSecond * layout.SignalCount * sizeof(double) / (1024 * 1024);
        double capturedPayloadMiB = result.CapturedEvents * layout.SignalCount * sizeof(double) / (1024.0 * 1024.0);
        long droppedEvents = result.ProducedEvents - result.CapturedEvents;

        string line = $"{label}: {result.Elapsed.TotalMilliseconds:F2} ms, produced {producedEventsPerSecond:F0} ev/s, captured {capturedEventsPerSecond:F0} ev/s, {rawMiBPerSecond:F1} MiB/sec captured raw, dropped {droppedEvents}, captured payload {capturedPayloadMiB:F1} MiB";
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private static IEventBatch CreateEventBatch(SignalLayout layout, int offset, ProfileBatchLayout batchLayout)
    {
        return batchLayout switch
        {
            ProfileBatchLayout.Jagged => CreateJaggedEventBatch(layout, offset),
            ProfileBatchLayout.FlatColumnMajor => CreateFlatColumnMajorEventBatch(layout, offset),
            _ => throw new ArgumentOutOfRangeException(nameof(batchLayout), batchLayout, null),
        };
    }

    private static EventBatch CreateJaggedEventBatch(SignalLayout layout, int offset)
    {
        var signals = new double[layout.SignalCount][];
        for (int s = 0; s < signals.Length; s++)
        {
            var values = new double[BatchSize];
            for (int e = 0; e < values.Length; e++)
                values[e] = 1 + (((offset + e + 1) * (s + 3) * 7919) % 100_000_000);

            signals[s] = values;
        }

        return new EventBatch(BatchSize, signals, layout);
    }

    private static ColumnMajorEventBatch CreateFlatColumnMajorEventBatch(SignalLayout layout, int offset)
    {
        var values = new double[layout.SignalCount * BatchSize];
        for (int s = 0; s < layout.SignalCount; s++)
        {
            int signalOffset = s * BatchSize;
            for (int e = 0; e < BatchSize; e++)
                values[signalOffset + e] = 1 + (((offset + e + 1) * (s + 3) * 7919) % 100_000_000);
        }

        return new ColumnMajorEventBatch(BatchSize, values, layout);
    }

    private enum ProfileBatchLayout
    {
        Jagged,
        FlatColumnMajor,
    }

    private readonly record struct PipelineProfileResult(
        int ProducedEvents,
        long CapturedEvents,
        TimeSpan Elapsed);
}
