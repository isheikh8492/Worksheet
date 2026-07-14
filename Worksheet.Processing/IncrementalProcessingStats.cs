namespace Worksheet.Processing
{
    public readonly record struct IncrementalProcessingStats(
        long DeltaAppliedCount,
        long FullRebuildCount,
        long SequenceGapCount);
}
