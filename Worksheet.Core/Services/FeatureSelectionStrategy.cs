using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Core.Models;

namespace Worksheet.Core.Services
{
    public class FeatureSelectionStrategy
    {
        private static ChannelSettings? _channelSettings;
        private static readonly object _lock = new object();

        public static void LoadChannelSettings(string jsonFilePath)
        {
            lock (_lock)
            {
                _channelSettings = new ChannelSettings();
                _channelSettings.LoadFromJsonFile(jsonFilePath);
            }
        }

        /// <summary>
        /// Returns filtered channel names (numeric wavelengths only) for spectral ribbon plots.
        /// Excludes non-numeric channels like SSC, QPD, NC, etc.
        /// </summary>
        public static IReadOnlyList<string> ChannelNames
        {
            get
            {
                lock (_lock)
                {
                    if (_channelSettings == null || _channelSettings.ConnectedChannelCount == 0)
                        return Array.Empty<string>();

                    return _channelSettings.GetAdcChannelsFiltered();
                }
            }
        }

        /// <summary>
        /// Returns all connected channel names (includes SSC, QPD, etc.) for histogram and pseudocolor plots.
        /// </summary>
        public static IReadOnlyList<string> AllChannelNames
        {
            get
            {
                lock (_lock)
                {
                    if (_channelSettings == null || _channelSettings.ConnectedChannelCount == 0)
                        return Array.Empty<string>();

                    return _channelSettings.GetAdcChannels();
                }
            }
        }

        public static IReadOnlyList<int> FilteredChannelIndices
        {
            get
            {
                lock (_lock)
                {
                    if (_channelSettings == null || _channelSettings.ConnectedChannelCount == 0)
                        return Array.Empty<int>();

                    return _channelSettings.GetAdcIndicesFiltered();
                }
            }
        }

        public static bool TryGetChannelWavelength(int channelId, out string wavelength)
        {
            lock (_lock)
            {
                if (_channelSettings == null || _channelSettings.ConnectedChannelCount == 0)
                {
                    wavelength = string.Empty;
                    return false;
                }

                wavelength = _channelSettings.GetConnectedChannelName(channelId);
                if (string.IsNullOrEmpty(wavelength))
                {
                    wavelength = string.Empty;
                    return false;
                }

                return true;
            }
        }

        public IReadOnlyList<string> GetXFeatureNames(PlotType plotType)
        {
            return plotType == PlotType.Histogram || plotType == PlotType.Pseudocolor
                ? AllChannelNames
                : Array.Empty<string>();
        }

        public IReadOnlyList<string> GetYFeatureNames(PlotType plotType)
        {
            return plotType == PlotType.Pseudocolor
                ? AllChannelNames
                : Array.Empty<string>();
        }

        public IReadOnlyList<int> GetXFeatureIndices(PlotType plotType)
        {
            if (plotType == PlotType.Histogram || plotType == PlotType.Pseudocolor)
            {
                lock (_lock)
                {
                    if (_channelSettings == null || _channelSettings.ConnectedChannelCount == 0)
                        return Array.Empty<int>();

                    return _channelSettings.GetAdcIndices();
                }
            }
            return Array.Empty<int>();
        }
    }
}
