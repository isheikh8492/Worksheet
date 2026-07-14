using System;

namespace Worksheet.Core.Models.Gates
{
    public sealed class GateSettings
    {
        public Guid GateId { get; init; } = Guid.NewGuid();
        public GatePlotRef Plot { get; init; }
        public GateType GateType { get; init; }
        public GateGeometry Geometry { get; init; } = GateGeometry.Rectangle01(0, 0, 0, 0);
        public string Name { get; init; } = "Gate";
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; init; } = DateTime.UtcNow;
    }
}

