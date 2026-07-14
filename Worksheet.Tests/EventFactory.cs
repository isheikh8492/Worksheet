using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;

using Worksheet.Core.Buffers;
namespace Worksheet.Tests;

internal static class EventFactory
{
    public static Event CreateEvent(params double[] parameters)
    {
        return new Event(parameters, CreateAnalogCapture(parameters.Length));
    }

    public static AnalogCapture CreateAnalogCapture(int seed = 0)
    {
        double start = seed * 10;
        return new AnalogCapture(
            [
                start + 1,
                start + 2,
                start + 3,
                start + 4,
            ],
            channelCount: 2,
            timestampCount: 2);
    }
}
