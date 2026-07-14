using System;

namespace Worksheet.Core.Models.Gates
{
    public sealed class GateResult
    {
        public Guid GateId { get; init; }
        public Guid PlotId { get; init; }
        public long DataVersion { get; init; }
        public GateStatistics Stats { get; init; } = new();
        public int PassedCount { get; init; }
        public int TotalCount { get; init; }
        public int[]? EventIndices { get; init; }
    }
}

