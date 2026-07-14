namespace Worksheet.Models
{
    /// <summary>
    /// Settings for a spectral ribbon plot: all configured channels are laid out along the X
    /// axis while values are binned on the Y (value) axis.
    /// </summary>
    public sealed class SpectralRibbonSettings : ParameterPlotSettings
    {
        public override PlotType PlotType => PlotType.SpectralRibbon;

        public ScaleType YAxisScaleType { get; set; } = ScaleType.Linear;
    }
}
