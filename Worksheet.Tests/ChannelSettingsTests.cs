using System;
using System.IO;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

namespace Worksheet.Tests;

public sealed class ChannelSettingsTests
{
    [Fact]
    public void ConnectedChannelIdsAreCompactWhilePreservingSourceChannelNames()
    {
        string path = WriteChannelConfig("""
        {
          "channels": {
            "daq0.0": "372nm",
            "daq0.1": "NC",
            "daq0.2": "392nm",
            "mb.0": "SSC",
            "mb.1": "NC",
            "mb.2": "QPD.BR"
          }
        }
        """);

        try
        {
            var settings = new ChannelSettings();
            Assert.True(settings.LoadFromJsonFile(path));

            Assert.Equal(6, settings.SourceChannelCount);
            Assert.Equal(4, settings.ConnectedChannelCount);
            Assert.Equal(settings.ConnectedChannelCount, settings.ChannelCount);

            Assert.Collection(
                settings.Channels,
                c =>
                {
                    Assert.Equal(0, c.Id);
                    Assert.Equal("daq0.0", c.DaqChannel);
                    Assert.Equal("372nm", c.Wavelength);
                },
                c =>
                {
                    Assert.Equal(1, c.Id);
                    Assert.Equal("daq0.2", c.DaqChannel);
                    Assert.Equal("392nm", c.Wavelength);
                },
                c =>
                {
                    Assert.Equal(2, c.Id);
                    Assert.Equal("mb.0", c.DaqChannel);
                    Assert.Equal("SSC", c.Wavelength);
                },
                c =>
                {
                    Assert.Equal(3, c.Id);
                    Assert.Equal("mb.2", c.DaqChannel);
                    Assert.Equal("QPD.BR", c.Wavelength);
                });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FilteredWavelengthChannelsSortLexicographicallyWithMatchingCompactIds()
    {
        string path = WriteChannelConfig("""
        {
          "channels": {
            "daq0.0": "500nm",
            "daq0.1": "NC",
            "daq0.2": "372nm",
            "mb.0": "SSC",
            "daq0.3": "403nm"
          }
        }
        """);

        try
        {
            var settings = new ChannelSettings();
            Assert.True(settings.LoadFromJsonFile(path));

            Assert.Equal(new[] { "372nm", "403nm", "500nm" }, settings.GetAdcChannelsFiltered());
            Assert.Equal(new[] { 1, 3, 0 }, settings.GetAdcIndicesFiltered());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteChannelConfig(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"worksheet-channels-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
