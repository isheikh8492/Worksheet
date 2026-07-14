using System.Collections.Generic;

namespace Worksheet.Chasm
{
    public interface IEventIngestionPort
    {
        int PublishEvents(IReadOnlyList<Event> events);

        // No-copy publish path. The caller transfers buffer ownership to CHASM.
        int PublishColumnMajor(double[] values, int eventCount);
    }
}
