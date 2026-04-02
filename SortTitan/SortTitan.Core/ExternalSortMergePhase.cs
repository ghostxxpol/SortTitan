using System.Text;

namespace SortTitan.Core;

public sealed class ExternalSortMergePhase
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly IComparer<LineEntry> _comparer;

    public ExternalSortMergePhase() : this(new LineEntryComparer())
    {
    }

    public ExternalSortMergePhase(IComparer<LineEntry> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    public async Task ExecuteAsync(ExternalSortMergePhaseOptions options, CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.InputRunFiles is null || options.InputRunFiles.Count == 0)
        {
            throw new ArgumentException("InputRunFiles are required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("OutputPath is required.", nameof(options));
        }

        var outputDir = System.IO.Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var cursors = new List<RunCursor>(options.InputRunFiles.Count);
        var heap = new PriorityQueue<RunCursor, LineEntry>(_comparer);

        try
        {
            foreach (var runPath in options.InputRunFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cursor = new RunCursor(runPath);
                cursors.Add(cursor);

                if (await cursor.MoveNextAsync(cancellationToken))
                {
                    heap.Enqueue(cursor, cursor.Current);
                }
                else
                {
                    await cursor.DisposeAsync();
                }
            }

            await using var outputStream = new FileStream(
                options.OutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                useAsync: true);

            await using var writer = new StreamWriter(outputStream, Utf8NoBom);
            writer.NewLine = "\n";

            while (heap.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cursor = heap.Dequeue();
                var entry = cursor.Current;

                var line = $"{entry.Number}. {entry.Text}";
                await writer.WriteAsync(line.AsMemory(), cancellationToken);
                await writer.WriteAsync(writer.NewLine.AsMemory(), cancellationToken);

                if (await cursor.MoveNextAsync(cancellationToken))
                {
                    heap.Enqueue(cursor, cursor.Current);
                }
                else
                {
                    await cursor.DisposeAsync();
                }
            }

            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            foreach (var cursor in cursors)
            {
                await cursor.DisposeAsync();
            }
        }
    }
}
