using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Worksheet.Core.Models;

namespace Worksheet.Core.Services
{
    public class ChannelSettings
    {
        private static readonly string[] Disconnected = { "nc", "disconnected", "none", "null" };

        public List<ChannelInfo> Channels { get; private set; } = new List<ChannelInfo>();
        public List<ChannelInfo> AllChannels { get; private set; } = new List<ChannelInfo>();
        public int SourceChannelCount => AllChannels.Count;
        public int ConnectedChannelCount => Channels.Count;
        public int ChannelCount => ConnectedChannelCount;

        public bool LoadFromJsonFile(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                Console.WriteLine($"Configuration file not found: {jsonFilePath}");
                return false;
            }

            try
            {
                return LoadFromJson(jsonFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load channel configuration: {ex.Message}");
                return false;
            }
        }

        private bool LoadFromJson(string jsonFilePath)
        {
            Channels.Clear();
            AllChannels.Clear();

            var jsonText = File.ReadAllText(jsonFilePath);
            var jsonDoc = JsonDocument.Parse(jsonText);

            if (!jsonDoc.RootElement.TryGetProperty("channels", out var channelsElement))
            {
                Console.WriteLine("JSON file does not contain 'channels' property");
                return false;
            }

            // Parse source slots in file order. Connected channels get compact event-column IDs.
            int sourceSlotId = 0;
            int connectedId = 0;
            foreach (var property in channelsElement.EnumerateObject())
            {
                var key = property.Name;
                var value = property.Value.GetString();

                if (!string.IsNullOrEmpty(value))
                {
                    AllChannels.Add(new ChannelInfo(sourceSlotId, key, value, value));

                    // Only add connected channels
                    if (!Disconnected.Contains(value.ToLower()))
                    {
                        Channels.Add(new ChannelInfo(connectedId, key, value, value));
                        connectedId++;
                    }

                    sourceSlotId++;
                }
            }

            return true;
        }

        // Keep the old method for backward compatibility, redirects to JSON
        public bool LoadFromIniFile(string iniFilePath)
        {
            return LoadFromJsonFile(iniFilePath);
        }

        public List<string> GetAdcChannels()
        {
            return Channels.Select(c => c.Wavelength).ToList();
        }

        public List<int> GetAdcIndices()
        {
            return Channels.Select(c => c.Id).ToList();
        }

        public List<string> GetAdcChannelsFiltered()
        {
            return GetFilteredNumericChannelsSorted()
                .Select(c => c.Wavelength)
                .ToList();
        }

        public List<int> GetAdcIndicesFiltered()
        {
            return GetFilteredNumericChannelsSorted()
                .Select(c => c.Id)
                .ToList();
        }

        public string GetConnectedChannelName(int channelId)
        {
            var channel = Channels.FirstOrDefault(c => c.Id == channelId);
            return channel?.Wavelength ?? string.Empty;
        }

        private IEnumerable<ChannelInfo> GetFilteredNumericChannelsSorted()
        {
            return Channels
                .Where(c => IsNumericWavelength(c.Wavelength))
                .OrderBy(c => c.Wavelength, StringComparer.Ordinal);
        }

        private bool IsNumericWavelength(string wavelength)
        {
            try
            {
                // Handle wavelengths with 'nm' suffix
                var numericPart = wavelength.EndsWith("nm", StringComparison.OrdinalIgnoreCase)
                    ? wavelength.Substring(0, wavelength.Length - 2)
                    : wavelength;

                return double.TryParse(numericPart, out _);
            }
            catch
            {
                return false;
            }
        }
    }
}
