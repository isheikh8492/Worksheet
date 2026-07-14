using System;
using System.Collections.Generic;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;

using Worksheet.Core.Buffers;
namespace Worksheet.Processing
{
    public sealed class OscilloscopePlotProcessor
    {
        private static readonly int[] DefaultChannelSelection = [0];
        private readonly IOscilloscopeBuffer _buffer;

        public OscilloscopePlotProcessor(IOscilloscopeBuffer buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public OscilloscopeProcessedData Process(PlotSettings settings)
        {
            return Process(settings, RenderTargetSize.Empty);
        }

        public OscilloscopeProcessedData Process(PlotSettings settings, RenderTargetSize targetSize)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (settings is not ScopeSettings scopeSettings)
                throw new ArgumentException($"Expected {nameof(ScopeSettings)} but received {settings.GetType().Name}.", nameof(settings));

            if (!_buffer.TryGetLatest(out var capture) || capture == null)
                return Empty(scopeSettings.Id);

            var requestedChannels = scopeSettings.ChannelIndices;
            if (requestedChannels == null || requestedChannels.Length == 0)
                requestedChannels = DefaultChannelSelection;

            var validChannels = new List<int>(requestedChannels.Length);
            foreach (int channelIndex in requestedChannels)
            {
                if ((uint)channelIndex < (uint)capture.ChannelCount)
                    validChannels.Add(channelIndex);
            }

            if (validChannels.Count == 0)
                return Empty(scopeSettings.Id);

            var signals = new double[validChannels.Count][];
            for (int i = 0; i < validChannels.Count; i++)
            {
                int channelIndex = validChannels[i];
                var signal = new double[capture.TimestampCount];
                Array.Copy(
                    capture.Values,
                    channelIndex * capture.TimestampCount,
                    signal,
                    0,
                    capture.TimestampCount);
                signals[i] = signal;
            }

            return new OscilloscopeProcessedData(
                scopeSettings.Id,
                signals,
                validChannels.ToArray(),
                capture.TimestampCount,
                isEmpty: false);
        }

        private static OscilloscopeProcessedData Empty(Guid plotId)
        {
            return new OscilloscopeProcessedData(
                plotId,
                Array.Empty<double[]>(),
                Array.Empty<int>(),
                timestampCount: 0,
                isEmpty: true);
        }
    }
}
