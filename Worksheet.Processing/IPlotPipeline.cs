using System;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;

namespace Worksheet.Processing
{
    public interface IPlotPipeline
    {
        TimeSpan Cadence { get; }
        long Version { get; }
        ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize);
        int GetSettingsHash(PlotSettings settings, RenderTargetSize targetSize);
        void ResetState();
        (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats();
    }
}
