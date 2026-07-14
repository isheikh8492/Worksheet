namespace Worksheet.Models
{
    /// <summary>
    /// Settings for a 2D pseudocolor (density) plot: two features binned on the X and Y
    /// value axes.
    /// </summary>
    public sealed class PseudocolorSettings : ParameterPlotSettings
    {
        public override PlotType PlotType => PlotType.Pseudocolor;

        public int XFeature { get; set; }
        public int YFeature { get; set; }
        public ScaleType XAxisScaleType { get; set; } = ScaleType.Linear;
        public ScaleType YAxisScaleType { get; set; } = ScaleType.Linear;
    }
}
