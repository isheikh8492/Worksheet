using System;

namespace Worksheet.Core.Models.Gates
{
    public sealed class GateStatsDisplayRow
    {
        public Guid GateId { get; init; }
        public string GateName { get; init; } = "";
        public string Num { get; init; } = "";
        public string Total { get; init; } = "";
        public string CV { get; init; } = "";
        public string Mean { get; init; } = "";
        public string STD { get; init; } = "";
        public string Var { get; init; } = "";
    }
}
