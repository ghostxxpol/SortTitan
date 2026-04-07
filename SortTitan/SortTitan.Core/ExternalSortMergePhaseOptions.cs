namespace SortTitan.Core;

public sealed record class ExternalSortMergePhaseOptions
{
    public required IReadOnlyList<string> InputRunFiles { get; init; }
    public required string OutputPath { get; init; }
    public required string TempDirectory { get; init; }
    public string RunFilePrefix { get; init; } = "run";
    public int FanIn { get; init; } = 128;
    public bool KeepTempFilesOnError { get; init; } = true;
}
