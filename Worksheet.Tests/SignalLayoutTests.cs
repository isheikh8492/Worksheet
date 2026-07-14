using System;
using Worksheet.Core.Models;
using Xunit;

namespace Worksheet.Tests;

public sealed class SignalLayoutTests
{
    [Fact]
    public void DefaultLayoutMatchesConnectedChannelEventShape()
    {
        var layout = SignalLayout.Default;

        Assert.Equal(1, layout.LaserCount);
        Assert.Equal(1, layout.FeatureCount);
        Assert.Equal(51, layout.ChannelCount);
        Assert.Equal(51, layout.SignalCount);
    }

    [Fact]
    public void SixLaserNineFeatureSixtyChannelLayoutHasExpectedSignalCount()
    {
        var layout = new SignalLayout(6, 9, 60);

        Assert.Equal(3_240, layout.SignalCount);
    }

    [Fact]
    public void ToIndexMapsLaserFeatureChannelToFlatColumnIndex()
    {
        var layout = new SignalLayout(6, 9, 60);

        Assert.Equal(1_337, layout.ToIndex(2, 4, 17));
        Assert.Equal(1_337, layout.ToIndex(new SignalKey(Laser: 2, Feature: 4, Channel: 17)));
    }

    [Fact]
    public void ToIndexRejectsOutOfRangeCoordinates()
    {
        var layout = new SignalLayout(6, 9, 60);

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.ToIndex(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.ToIndex(6, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.ToIndex(0, 9, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.ToIndex(0, 0, 60));
    }
}
