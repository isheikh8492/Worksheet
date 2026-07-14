using System;

using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public sealed class Event
    {
        public Event(double[] parameters, AnalogCapture analogCapture)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            AnalogCapture = analogCapture ?? throw new ArgumentNullException(nameof(analogCapture));
        }

        public double[] Parameters { get; }

        public AnalogCapture AnalogCapture { get; }

        public int SignalCount => Parameters.Length;

        public double GetSignalValue(int signalIndex)
        {
            if ((uint)signalIndex >= (uint)Parameters.Length)
                throw new ArgumentOutOfRangeException(nameof(signalIndex));

            return Parameters[signalIndex];
        }
    }
}
