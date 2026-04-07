using System.Text;

namespace SortTitan.Core;

public sealed class LineEntryChunkSpiller
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly IComparer<LineEntry> _comparer;

    public LineEntryChunkSpiller() : this(new LineEntryComparer())
    {
    }

    public LineEntryChunkSpiller(IComparer<LineEntry> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    public async Task SpillSortedAsync(LineEntryChunk chunk, string tempFilePath, CancellationToken cancellationToken = default)
    {
        if (chunk is null)
        {
            throw new ArgumentNullException(nameof(chunk));
        }

        if (chunk.Entries is null)
        {
            throw new ArgumentException("Chunk entries are required.", nameof(chunk));
        }

        if (string.IsNullOrWhiteSpace(tempFilePath))
        {
            throw new ArgumentException("Temp file path is required.", nameof(tempFilePath));
        }

        chunk.Entries.Sort(_comparer);
        await SpillAlreadySortedAsync(chunk, tempFilePath, cancellationToken);
    }

    public async Task SpillAlreadySortedAsync(LineEntryChunk chunk, string tempFilePath, CancellationToken cancellationToken = default)
    {
        if (chunk is null)
        {
            throw new ArgumentNullException(nameof(chunk));
        }

        if (chunk.Entries is null)
        {
            throw new ArgumentException("Chunk entries are required.", nameof(chunk));
        }

        if (string.IsNullOrWhiteSpace(tempFilePath))
        {
            throw new ArgumentException("Temp file path is required.", nameof(tempFilePath));
        }

        var directory = Path.GetDirectoryName(tempFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            tempFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        await using var writer = new StreamWriter(fileStream, Utf8NoBom);

        foreach (var entry in chunk.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.Write(entry.Number);
            writer.Write(". ");
            writer.Write(entry.Text);
            writer.Write(writer.NewLine);
        }

        await writer.FlushAsync(cancellationToken);
    }
}
