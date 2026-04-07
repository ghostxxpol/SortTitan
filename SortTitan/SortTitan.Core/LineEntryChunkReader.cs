using System.Text;

namespace SortTitan.Core;

public sealed class LineEntryChunkReader
{
    private readonly LineEntryParser _parser = new();

    public async IAsyncEnumerable<LineEntryChunk> ReadChunksAsync(
        ChunkReaderOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new ArgumentException("InputPath is required.", nameof(options));
        }

        var perChunkBudgetBytes = options.GetPerChunkMemoryBudgetBytes();
        var maxEntries = Math.Max(1, options.MaxEntriesPerChunk);

        await using var fileStream = new FileStream(
            options.InputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        using var reader = new StreamReader(
            fileStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024 * 1024,
            leaveOpen: false);

        var initialCapacity = EstimateInitialCapacity(perChunkBudgetBytes, maxEntries);
        var entries = new List<LineEntry>(capacity: initialCapacity);
        long estimatedBytes = 0;

        long lineNumber = 0;
        long chunkStartLine = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lineNumber++;

            if (!_parser.TryParse(line, out var entry))
            {
                throw new InvalidDataException($"Invalid line at {lineNumber}.");
            }

            entries.Add(entry);
            estimatedBytes += EstimateEntryBytes(entry);

            if (entries.Count >= maxEntries || estimatedBytes >= perChunkBudgetBytes)
            {
                yield return new LineEntryChunk
                {
                    Entries = entries,
                    EstimatedMemoryBytes = estimatedBytes,
                    FirstLineNumber = chunkStartLine,
                    LastLineNumber = lineNumber,
                };

                entries = new List<LineEntry>(capacity: initialCapacity);
                estimatedBytes = 0;
                chunkStartLine = lineNumber + 1;
            }
        }

        if (entries.Count > 0)
        {
            yield return new LineEntryChunk
            {
                Entries = entries,
                EstimatedMemoryBytes = estimatedBytes,
                FirstLineNumber = chunkStartLine,
                LastLineNumber = lineNumber,
            };
        }
    }

    private static long EstimateEntryBytes(LineEntry entry)
    {
        return 64 + (entry.Text.Length * sizeof(char));
    }

    private static int EstimateInitialCapacity(long perChunkBudgetBytes, int maxEntriesPerChunk)
    {
        const int minCapacity = 4 * 1024;
        const long estimatedBytesPerEntry = 384;

        if (perChunkBudgetBytes <= 0)
        {
            return minCapacity;
        }

        var byBudget = perChunkBudgetBytes / estimatedBytesPerEntry;
        if (byBudget <= minCapacity)
        {
            return minCapacity;
        }

        var byBudgetInt = byBudget >= int.MaxValue ? int.MaxValue : (int)byBudget;
        return Math.Min(byBudgetInt, maxEntriesPerChunk);
    }
}
