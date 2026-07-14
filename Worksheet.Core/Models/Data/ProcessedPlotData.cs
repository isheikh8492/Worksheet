using System;

namespace Worksheet.Core.Models.Data
{
    using Worksheet.Core.Models;

    public abstract class ProcessedPlotData
    {
        protected ProcessedPlotData(Guid plotId, PlotType plotType)
        {
            PlotId = plotId;
            PlotType = plotType;
        }

        public Guid PlotId { get; }
        public PlotType PlotType { get; }
    }
}
