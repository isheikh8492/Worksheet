namespace Worksheet.Models
{
    /// <summary>
    /// Settings for an oscilloscope ("scope") plot: a set of analog channels drawn as
    /// time-domain signals. Unlike parameter plots, a scope does not bin values into a range.
    /// </summary>
    public sealed class ScopeSettings : PlotSettings
    {
        public const int DefaultChannelCount = 51;
        public const int DefaultInitialSampleCount = 1750;

        public override PlotType PlotType => PlotType.Oscilloscope;

        /// <summary>Total number of channels available for selection.</summary>
        public int ChannelCount { get; set; } = DefaultChannelCount;

        /// <summary>Indices of the channels currently displayed.</summary>
        public int[] ChannelIndices { get; set; } = [0];

        /// <summary>Initial X-axis sample width used before the first capture arrives.</summary>
        public int InitialSampleCount { get; set; } = DefaultInitialSampleCount;
    }
}
