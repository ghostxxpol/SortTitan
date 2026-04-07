namespace SortTitan.Core;

public sealed record class ExternalSortSplitPhaseOptions
{
    public required ChunkReaderOptions ChunkReader { get; init; }
    public required string TempDirectory { get; init; }
    public string RunFilePrefix { get; init; } = "run";
    public int MaxConcurrentSpills { get; init; } = 1;
}
