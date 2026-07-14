using System;

namespace Worksheet.Core.Models.Gates
{
    public readonly record struct GatePlotRef(Guid PlotId, PlotType? PlotType = null);
}

