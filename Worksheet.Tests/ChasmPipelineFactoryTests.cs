using System;
using System.Threading.Tasks;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

namespace Worksheet.Tests;

public sealed class ChasmPipelineFactoryTests
{
    [Fact]
    public void CreateMockBuildsMockPipelineWithoutIngressPort()
    {
        var options = ChasmOptions.Default with
        {
            SignalLayout = new SignalLayout(1, 1, 3),
            WindowCapacityEvents = 10,
        };
        var source = new DataSource(options.SignalLayout, options.WindowCapacityEvents);
        var oscilloscopeBuffer = new OscilloscopeBuffer();

        var pipeline = ChasmPipelineFactory.CreateMock(source, options, oscilloscopeBuffer);

        Assert.IsType<MockProducer>(pipeline.Producer);
        Assert.Null(pipeline.IngestionPort);
        Assert.IsType<ChasmDataSource>(pipeline.ChasmDataSource);

        pipeline.ChasmEngine.Dispose();
    }

    [Fact]
    public async Task CreateEventIngressBuildsPublishablePipeline()
    {
        var layout = new SignalLayout(1, 1, 3);
        var source = new DataSource(layout, windowCapacity: 10);
        var oscilloscopeBuffer = new OscilloscopeBuffer(capacity: 3);
        var pipeline = ChasmPipelineFactory.CreateEventIngress(source, ChasmOptions.Default with
        {
            SignalLayout = layout,
            ChannelCapacityBatches = 4,
        }, oscilloscopeBuffer);

        using var chasm = pipeline.ChasmEngine;
        var ingestion = Assert.IsAssignableFrom<IEventIngestionPort>(pipeline.IngestionPort);

        chasm.StartStreaming();
        int written = ingestion.PublishEvents(
            [
                EventFactory.CreateEvent(10, 20, 30),
                EventFactory.CreateEvent(11, 21, 31),
            ]);

        await WaitUntilAsync(() => source.TotalEventsIngested == 2);
        chasm.StopStreaming();

        Assert.Equal(1, written);
        Assert.IsType<EventProducer>(pipeline.Producer);
        Assert.Equal(2, source.TotalEventsIngested);
        Assert.Equal(2, oscilloscopeBuffer.Version);
        Assert.True(oscilloscopeBuffer.TryGetLatest(out var capture));
        Assert.NotNull(capture);
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
