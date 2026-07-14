using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

using Worksheet.Core.Buffers;
namespace Worksheet.Tests;

public sealed class OscilloscopePlotProcessorTests
{
    [Fact]
    public void ProcessReturnsEmptyDataWhenBufferIsEmpty()
    {
        var processor = new OscilloscopePlotProcessor(new OscilloscopeBuffer());
        var settings = new ScopeSettings();

        OscilloscopeProcessedData data = processor.Process(settings);

        Assert.True(data.IsEmpty);
        Assert.Empty(data.Signals);
        Assert.Empty(data.ChannelIndices);
        Assert.Equal(0, data.TimestampCount);
    }

    [Fact]
    public void ProcessExtractsSelectedChannelsFromLatestCapture()
    {
        var buffer = new OscilloscopeBuffer();
        var processor = new OscilloscopePlotProcessor(buffer);
        var settings = new ScopeSettings
        {
            ChannelIndices = [1, 0],
        };
        buffer.Publish(new AnalogCapture(
            [
                1, 2, 3,
                10, 20, 30,
            ],
            channelCount: 2,
            timestampCount: 3));

        OscilloscopeProcessedData data = processor.Process(settings);

        Assert.False(data.IsEmpty);
        Assert.Equal(3, data.TimestampCount);
        Assert.Equal([1, 0], data.ChannelIndices);
        Assert.Equal([10, 20, 30], data.Signals[0]);
        Assert.Equal([1, 2, 3], data.Signals[1]);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void ProcessUsesLatestCaptureAndDropsOlderCaptures()
    {
        var buffer = new OscilloscopeBuffer(capacity: 3);
        var processor = new OscilloscopePlotProcessor(buffer);
        var settings = new ScopeSettings();

        buffer.Publish(new AnalogCapture([1, 2], channelCount: 1, timestampCount: 2));
        buffer.Publish(new AnalogCapture([3, 4], channelCount: 1, timestampCount: 2));

        OscilloscopeProcessedData data = processor.Process(settings);

        Assert.False(data.IsEmpty);
        Assert.Equal([0], data.ChannelIndices);
        Assert.Equal([3, 4], data.Signals[0]);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void ProcessIgnoresOutOfRangeChannelSelections()
    {
        var buffer = new OscilloscopeBuffer();
        var processor = new OscilloscopePlotProcessor(buffer);
        var settings = new ScopeSettings
        {
            ChannelIndices = [-1, 1, 7],
        };
        buffer.Publish(new AnalogCapture(
            [
                1, 2,
                10, 20,
            ],
            channelCount: 2,
            timestampCount: 2));

        OscilloscopeProcessedData data = processor.Process(settings);

        Assert.False(data.IsEmpty);
        Assert.Equal([1], data.ChannelIndices);
        Assert.Single(data.Signals);
        Assert.Equal([10, 20], data.Signals[0]);
    }
}
