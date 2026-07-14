namespace Worksheet.Chasm
{
    public interface IEventBatch
    {
        int Count { get; }
        int SignalCount { get; }
    }
}
