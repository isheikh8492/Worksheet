namespace Worksheet.Core.Models
{
    /// <summary>
    /// Base for parameter-based plots (histogram, pseudocolor, spectral ribbon) that bin
    /// event values into a fixed value range. Holds the binning/range configuration; the
    /// value-to-bin mapping itself lives in <see cref="Scale"/>.
    /// </summary>
    public abstract class ParameterPlotSettings : PlotSettings
    {
        public int BinCount { get; set; } = 256;
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100_000_000;

        public int GetBinCount()
        {
            return BinCount > 0 ? BinCount : 256;
        }
    }
}
