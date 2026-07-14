namespace Worksheet.Core.Buffers
{
    public interface IOscilloscopeBuffer
    {
        int Count { get; }
        long Version { get; }
        bool TryGetLatest(out AnalogCapture? capture);
    }
}
