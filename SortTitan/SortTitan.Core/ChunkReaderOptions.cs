namespace SortTitan.Core;

public sealed record class ChunkReaderOptions
{
    public required string InputPath { get; init; }

    public double MemoryBudgetFraction { get; init; } = 0.25;
    public long? TotalMemoryBudgetBytesOverride { get; init; }
    public double MemoryBudgetSafetyFactor { get; init; } = 3.0;

    public int MaxInFlightChunks { get; init; } = 1;
    public int MaxEntriesPerChunk { get; init; } = int.MaxValue;

    public long GetTotalMemoryBudgetBytes()
    {
         if (TotalMemoryBudgetBytesOverride.HasValue)
         {
             return (long)TotalMemoryBudgetBytesOverride;
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
        var safety = MemoryBudgetSafetyFactor <= 0 ? 1.0 : MemoryBudgetSafetyFactor;
        return Math.Max(1024, (long)((total / (double)chunks) / safety));
    }
}
