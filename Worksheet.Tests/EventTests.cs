using System;
using Worksheet.Models;
using Worksheet.Services;
using Xunit;

namespace Worksheet.Tests;

public sealed class EventTests
{
    [Fact]
    public void PlotSettingsDefaultOscilloscopeChannelCountMatchesConnectedEventShape()
    {
        var settings = new ScopeSettings();

        Assert.Equal(51, settings.ChannelCount);
    }

    [Fact]
    public void EventSignalCountComesFromParameters()
    {
        var analog = new AnalogCapture([1, 2, 3, 4, 5, 6], channelCount: 2, timestampCount: 3);
        var ev = new Event([10, 20, 30], analog);

        Assert.Equal(3, ev.SignalCount);
        Assert.Same(analog, ev.AnalogCapture);
        Assert.Equal(20, ev.GetSignalValue(1));
    }

    [Fact]
    public void EventRequiresAnalogCapture()
    {
        Assert.Throws<ArgumentNullException>(() => new Event([10, 20], analogCapture: null!));
    }

    [Fact]
    public void AnalogCaptureReadsChannelTimestampValues()
    {
        var capture = new AnalogCapture(
            [
                10, 11, 12,
                20, 21, 22,
            ],
            channelCount: 2,
            timestampCount: 3);

        Assert.Equal(10, capture.GetValue(0, 0));
        Assert.Equal(12, capture.GetValue(0, 2));
        Assert.Equal(20, capture.GetValue(1, 0));
        Assert.Equal(22, capture.GetValue(1, 2));
    }

    [Fact]
    public void AnalogCaptureRejectsWrongDimensions()
    {
        Assert.Throws<ArgumentException>(() => new AnalogCapture([1, 2, 3], channelCount: 2, timestampCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalogCapture([1, 2], channelCount: 0, timestampCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalogCapture([1, 2], channelCount: 2, timestampCount: 0));
    }

    [Fact]
    public void AnalogCaptureRejectsOutOfRangeReads()
    {
        var capture = new AnalogCapture([1, 2, 3, 4], channelCount: 2, timestampCount: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => capture.GetValue(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.GetValue(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.GetValue(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.GetValue(0, 2));
    }
}
