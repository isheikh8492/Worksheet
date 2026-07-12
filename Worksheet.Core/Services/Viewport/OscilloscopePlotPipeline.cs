using System;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public sealed class OscilloscopePlotPipeline : IPlotPipeline
    {
        private readonly OscilloscopePlotProcessor _processor;
        private readonly Func<long> _getVersion;

        public OscilloscopePlotPipeline(OscilloscopePlotProcessor processor, Func<long> getVersion, TimeSpan cadence)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _getVersion = getVersion ?? throw new ArgumentNullException(nameof(getVersion));
            Cadence = cadence;
        }

        public TimeSpan Cadence { get; }
        public long Version => _getVersion();

        public ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize)
        {
            return _processor.Process(settings, targetSize);
        }

        public int GetSettingsHash(PlotSettings settings, RenderTargetSize targetSize)
        {
            var channelIndices = (settings as ScopeSettings)?.ChannelIndices;
            if (channelIndices == null || channelIndices.Length == 0)
                return 0;

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < channelIndices.Length; i++)
                    hash = (hash * 31) + channelIndices[i];
                return hash;
            }
        }

        public void ResetState()
        {
        }

        public (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats()
        {
            return (0, 0, 0);
        }
    }
}
