namespace SortTitan.Core;

using System.Collections.Concurrent;
using System.Threading.Channels;

public sealed class ExternalSortSplitPhase
{
    private readonly LineEntryChunkReader _chunkReader;
    private readonly LineEntryChunkSpiller _spiller;

    public ExternalSortSplitPhase() : this(new LineEntryChunkReader(), new LineEntryChunkSpiller())
    {
    }

    public ExternalSortSplitPhase(LineEntryChunkReader chunkReader, LineEntryChunkSpiller spiller)
    {
        _chunkReader = chunkReader ?? throw new ArgumentNullException(nameof(chunkReader));
        _spiller = spiller ?? throw new ArgumentNullException(nameof(spiller));
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        ExternalSortSplitPhaseOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.ChunkReader is null)
        {
            throw new ArgumentException("ChunkReader options are required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            throw new ArgumentException("TempDirectory is required.", nameof(options));
        }

        var runFileProvider = new TempRunFileProvider(options.TempDirectory, options.RunFilePrefix);
        var runFiles = new List<string>();

        var degreeOfParallelism = Math.Max(1, options.ChunkReader.MaxInFlightChunks);
        if (degreeOfParallelism == 1)
        {
            var runIndex = 0;
            await foreach (var chunk in _chunkReader.ReadChunksAsync(options.ChunkReader, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                runIndex++;
                var tempFilePath = runFileProvider.GetRunFilePath(runIndex);

                await _spiller.SpillSortedAsync(chunk, tempFilePath, cancellationToken);
                runFiles.Add(tempFilePath);
            }

            return runFiles;
        }

        var channel = Channel.CreateBounded<LineEntryChunk>(new BoundedChannelOptions(degreeOfParallelism)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var maxConcurrentSpills = Math.Max(1, options.MaxConcurrentSpills);
        var spillGate = new SemaphoreSlim(maxConcurrentSpills, maxConcurrentSpills);
        var runIndexParallel = 0;
        var comparer = new LineEntryComparer();
        var runFilesQueue = new ConcurrentQueue<string>();

        var workers = new List<Task>(degreeOfParallelism);
        for (var i = 0; i < degreeOfParallelism; i++)
        {
            workers.Add(Task.Run(async () =>
            {
                await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    chunk.Entries.Sort(comparer);
                    var runIndex = Interlocked.Increment(ref runIndexParallel);
                    var tempFilePath = runFileProvider.GetRunFilePath(runIndex);

                    await spillGate.WaitAsync(cancellationToken);
                    try
                    {
                        await _spiller.SpillAlreadySortedAsync(chunk, tempFilePath, cancellationToken);
                    }
                    finally
                    {
                        spillGate.Release();
                    }

                    runFilesQueue.Enqueue(tempFilePath);
                }
            }, cancellationToken));
        }

        try
        {
            await foreach (var chunk in _chunkReader.ReadChunksAsync(options.ChunkReader, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await channel.Writer.WriteAsync(chunk, cancellationToken);
            }
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await Task.WhenAll(workers);
        runFiles.AddRange(runFilesQueue);
        return runFiles;
    }
}
