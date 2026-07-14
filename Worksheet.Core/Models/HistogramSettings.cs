namespace Worksheet.Models
{
    /// <summary>
    /// Settings for a 1D histogram plot: a single feature binned along the X (value) axis.
    /// </summary>
    public sealed class HistogramSettings : ParameterPlotSettings
    {
        public override PlotType PlotType => PlotType.Histogram;

        public int XFeature { get; set; }
        public ScaleType XAxisScaleType { get; set; } = ScaleType.Linear;
    }
}
