namespace SortTitan.Core;

public sealed record class ExternalSortMergePhaseOptions
{
    public required IReadOnlyList<string> InputRunFiles { get; init; }
    public required string OutputPath { get; init; }
}
