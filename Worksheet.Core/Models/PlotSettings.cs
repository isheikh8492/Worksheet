using System;

namespace Worksheet.Core.Models
{
    /// <summary>
    /// Base configuration shared by every worksheet plot. Concrete plot types derive
    /// from this (via <see cref="ParameterPlotSettings"/> or directly) and expose only
    /// the fields they actually use.
    /// </summary>
    public abstract class PlotSettings
    {
        public Guid Id { get; } = Guid.NewGuid();

        public abstract PlotType PlotType { get; }
    }
}
