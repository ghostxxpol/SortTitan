namespace SortTitan.Core;

public sealed record class ExternalSortMergePhaseMetrics
{
    public required int Passes { get; init; }
    public required int IntermediateFilesCreated { get; init; }
    public required long IntermediateBytesWritten { get; init; }
}
