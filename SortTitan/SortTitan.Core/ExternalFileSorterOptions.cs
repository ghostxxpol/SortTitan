namespace SortTitan.Core;

public sealed record class ExternalFileSorterOptions
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required string TempDirectory { get; init; }

    public string RunFilePrefix { get; init; } = "run";
    public int MergeFanIn { get; init; } = 128;
    public int MaxConcurrentSpills { get; init; } = 1;

    public double MemoryBudgetFraction { get; init; } = 0.25;
    public long? TotalMemoryBudgetBytesOverride { get; init; }
    public double MemoryBudgetSafetyFactor { get; init; } = 3.0;
    public int MaxInFlightChunks { get; init; } = 1;
    public int MaxEntriesPerChunk { get; init; } = int.MaxValue;

    public bool KeepTempFilesOnError { get; init; } = true;
}
