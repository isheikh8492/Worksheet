namespace Worksheet.Processing
{
    public readonly record struct ProcessingStatusSnapshot(
        double EventRatePerSecond,
        int BufferedEventCount,
        double HistogramAverageComputeMs,
        double PseudocolorAverageComputeMs,
        double SpectralRibbonAverageComputeMs,
        double OscilloscopeAverageComputeMs,
        double HistogramAverageRenderMs,
        double PseudocolorAverageRenderMs,
        double SpectralRibbonAverageRenderMs,
        double OscilloscopeAverageRenderMs,
        long DeltaAppliedCount,
        long FullRebuildCount,
        long SequenceGapCount);
}
