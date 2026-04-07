namespace SortTitan.Core;

public sealed record class ExternalFileSorterMetrics
{
    public required TimeSpan TotalTime { get; init; }
    public required TimeSpan SplitPhaseTime { get; init; }
    public required TimeSpan MergePhaseTime { get; init; }
    public required int MergePasses { get; init; }

    public required int TempFilesCount { get; init; }

    public required long InputBytes { get; init; }
    public required long TempBytes { get; init; }
    public required long OutputBytes { get; init; }

    public long ApproxTotalBytesTouched => InputBytes + TempBytes + OutputBytes;
}
