namespace SortTitan.Core;

public sealed record class ChunkReaderOptions
{
    public required string InputPath { get; init; }

    public double MemoryBudgetFraction { get; init; } = 0.25;
    public long? TotalMemoryBudgetBytesOverride { get; init; }

    public int MaxInFlightChunks { get; init; } = 1;
    public int MaxEntriesPerChunk { get; init; } = int.MaxValue;

    public InvalidLineHandling InvalidLineHandling { get; init; } = InvalidLineHandling.Throw;

    public long GetTotalMemoryBudgetBytes()
    {
        if (TotalMemoryBudgetBytesOverride is { } value)
        {
            return value;
        }

        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available <= 0)
        {
            available = 8L * 1024 * 1024 * 1024;
        }

        return (long)(available * MemoryBudgetFraction);
    }

    public long GetPerChunkMemoryBudgetBytes()
    {
        var total = GetTotalMemoryBudgetBytes();
        var chunks = Math.Max(1, MaxInFlightChunks);
        return Math.Max(1, total / chunks);
    }
}
