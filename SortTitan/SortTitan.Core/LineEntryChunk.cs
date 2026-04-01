namespace SortTitan.Core;

public sealed class LineEntryChunk
{
    public required List<LineEntry> Entries { get; init; }
    public required long EstimatedMemoryBytes { get; init; }
    public required long FirstLineNumber { get; init; }
    public required long LastLineNumber { get; init; }
}
