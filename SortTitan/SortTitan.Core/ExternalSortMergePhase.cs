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

    public async Task<ExternalSortMergePhaseMetrics> ExecuteAsync(
        ExternalSortMergePhaseOptions options,
        CancellationToken cancellationToken = default)
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

        if (string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            throw new ArgumentException("TempDirectory is required.", nameof(options));
        }

        var fanIn = Math.Max(2, options.FanIn);

        var outputDir = System.IO.Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        Directory.CreateDirectory(options.TempDirectory);

        if (options.InputRunFiles.Count == 1)
        {
            await CopyFileAsync(options.InputRunFiles[0], options.OutputPath, cancellationToken);
            return new ExternalSortMergePhaseMetrics
            {
                Passes = 0,
                IntermediateFilesCreated = 0,
                IntermediateBytesWritten = 0,
            };
        }

        var passes = 0;
        var intermediateFilesCreated = 0;
        long intermediateBytesWritten = 0;

        var current = options.InputRunFiles.ToList();

        try
        {
            while (current.Count > fanIn)
            {
                cancellationToken.ThrowIfCancellationRequested();

                passes++;
                var next = new List<string>();
                var groupIndex = 0;

                for (var i = 0; i < current.Count; i += fanIn)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    groupIndex++;
                    var group = current.Skip(i).Take(fanIn).ToList();
                    var tempPath = System.IO.Path.Combine(options.TempDirectory, $"{options.RunFilePrefix}_m{passes:D2}_{groupIndex:D6}.txt");

                    await MergeKWayAsync(group, tempPath, cancellationToken);
                    next.Add(tempPath);
                    intermediateFilesCreated++;
                    intermediateBytesWritten += GetFileSize(tempPath);
                }

                if (!options.KeepTempFilesOnError)
                {
                    TryDeleteFiles(current);
                }

                current = next;
            }

            await MergeKWayAsync(current, options.OutputPath, cancellationToken);
        }
        finally
        {
            if (!options.KeepTempFilesOnError)
            {
                TryDeleteFiles(current);
            }
        }

        return new ExternalSortMergePhaseMetrics
        {
            Passes = passes,
            IntermediateFilesCreated = intermediateFilesCreated,
            IntermediateBytesWritten = intermediateBytesWritten,
        };
    }

    private async Task MergeKWayAsync(IReadOnlyList<string> inputRunFiles, string outputPath, CancellationToken cancellationToken)
    {
        var cursors = new List<RunCursor>(inputRunFiles.Count);
        var heap = new PriorityQueue<RunCursor, LineEntry>(_comparer);

        try
        {
            foreach (var runPath in inputRunFiles)
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
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                useAsync: true);

            await using var writer = new StreamWriter(outputStream, Utf8NoBom);

            while (heap.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cursor = heap.Dequeue();
                var entry = cursor.Current;

                writer.Write(entry.Number);
                writer.Write(". ");
                writer.Write(entry.Text);
                writer.Write(writer.NewLine);

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

    private static async Task CopyFileAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        await using var output = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        await input.CopyToAsync(output, 1024 * 1024, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static void TryDeleteFiles(IReadOnlyList<string> files)
    {
        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
