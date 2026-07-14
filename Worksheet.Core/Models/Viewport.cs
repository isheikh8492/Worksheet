using System;
using System.Collections.Generic;

namespace Worksheet.Core.Models
{
    public class Viewport
    {
        public Guid Id { get; } = Guid.NewGuid();
        public List<Guid> PlotIds { get; } = new List<Guid>();
    }
}
