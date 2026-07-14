using System;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

namespace Worksheet.Tests;

public sealed class OscilloscopeBufferTests
{
    [Fact]
    public void PublishRejectsNullCapture()
    {
        var buffer = new OscilloscopeBuffer();

        Assert.Throws<ArgumentNullException>(() => buffer.Publish(null!));
    }

    [Fact]
    public void BufferRejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OscilloscopeBuffer(capacity: 0));
    }

    [Fact]
    public void TryGetLatestReturnsFalseWhenEmpty()
    {
        var buffer = new OscilloscopeBuffer();

        Assert.False(buffer.TryGetLatest(out var capture));
        Assert.Null(capture);
    }

    [Fact]
    public void BufferDropsOldCapturesWhenCapacityIsExceeded()
    {
        var buffer = new OscilloscopeBuffer(capacity: 2);
        var first = EventFactory.CreateAnalogCapture(1);
        var second = EventFactory.CreateAnalogCapture(2);
        var third = EventFactory.CreateAnalogCapture(3);

        buffer.Publish(first);
        buffer.Publish(second);
        buffer.Publish(third);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(3, buffer.Version);
        Assert.True(buffer.TryGetLatest(out var latest));
        Assert.Same(third, latest);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void TryGetLatestDrainsOlderCaptures()
    {
        var buffer = new OscilloscopeBuffer(capacity: 3);
        var first = EventFactory.CreateAnalogCapture(1);
        var second = EventFactory.CreateAnalogCapture(2);

        buffer.Publish(first);
        buffer.Publish(second);

        Assert.True(buffer.TryGetLatest(out var latest));

        Assert.Same(second, latest);
        Assert.Equal(0, buffer.Count);
        Assert.False(buffer.TryGetLatest(out _));
    }
}
