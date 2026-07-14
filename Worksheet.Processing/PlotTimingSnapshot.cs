namespace Worksheet.Processing
{
    public readonly record struct PlotTimingSnapshot(
        double HistogramAverageMs,
        double PseudocolorAverageMs,
        double SpectralRibbonAverageMs,
        double OscilloscopeAverageMs);
}
