using System;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public sealed class ParameterPlotPipeline : IPlotPipeline
    {
        private readonly PlotProcessor _processor;
        private readonly Func<long> _getVersion;

        public ParameterPlotPipeline(PlotProcessor processor, Func<long> getVersion, TimeSpan cadence)
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
            var hash = new HashCode();
            if (settings is ParameterPlotSettings parameter)
            {
                hash.Add(parameter.GetBinCount());
                hash.Add(parameter.MinValue);
                hash.Add(parameter.MaxValue);
            }

            switch (settings)
            {
                case HistogramSettings histogram:
                    hash.Add(histogram.XFeature);
                    hash.Add(histogram.XAxisScaleType);
                    break;
                case PseudocolorSettings pseudocolor:
                    hash.Add(pseudocolor.XFeature);
                    hash.Add(pseudocolor.YFeature);
                    hash.Add(pseudocolor.XAxisScaleType);
                    hash.Add(pseudocolor.YAxisScaleType);
                    break;
                case SpectralRibbonSettings spectral:
                    hash.Add(spectral.YAxisScaleType);
                    break;
            }

            hash.Add(targetSize.PixelWidth);
            hash.Add(targetSize.PixelHeight);
            return hash.ToHashCode();
        }

        public void ResetState()
        {
            _processor.ResetIncrementalState();
        }

        public (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats()
        {
            return _processor.GetDeltaStats();
        }
    }
}
