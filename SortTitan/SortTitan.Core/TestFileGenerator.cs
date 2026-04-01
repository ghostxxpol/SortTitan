namespace SortTitan.Core;

using System.Text;

public sealed class TestFileGenerator : ITestFileGenerator
{
    private static readonly string[] Words =
    [
        "Apple",
        "Banana",
        "Cherry",
        "Yellow",
        "Green",
        "Something",
        "Titan",
        "Sort",
        "File",
        "Stream",
        "Large",
        "Data",
        "Value",
        "Random",
        "Text",
    ];

    public async Task GenerateAsync(GeneratorOptions options, CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Validate(options);

        var random = options.Seed.HasValue ? new Random((int)options.Seed) : new Random();
        var textPool = CreateTextPool(random, options.TextPoolSize);

        var newLine = options.NewLine;
        var encoding = options.Encoding;
        var newLineByteCount = encoding.GetByteCount(newLine);

        await using var fileStream = new FileStream(
            options.OutputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        await using var writer = new StreamWriter(fileStream, encoding);
        writer.NewLine = newLine;

        long bytesWritten = 0;

        while (bytesWritten < options.TargetSizeBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var number = NextNumber(random, options.NumberMin, options.NumberMax);
            var text = NextText(random, textPool, options.RepeatProbability);

            var line = $"{number}. {text}";
            var lineByteCount = encoding.GetByteCount(line) + newLineByteCount;

            await writer.WriteAsync(line.AsMemory(), cancellationToken);
            await writer.WriteAsync(newLine.AsMemory(), cancellationToken);
            bytesWritten += lineByteCount;

            if (bytesWritten >= options.TargetSizeBytes)
            {
                break;
            }
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static void Validate(GeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("OutputPath is required.", nameof(options));
        }

        if (options.TargetSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TargetSizeBytes, "TargetSizeBytes must be > 0.");
        }

        if (options.Encoding is null)
        {
            throw new ArgumentException("Encoding is required.", nameof(options));
        }

        if (options.NewLine is null)
        {
            throw new ArgumentException("NewLine is required.", nameof(options));
        }

        if (options.NumberMin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.NumberMin, "NumberMin must be >= 0.");
        }

        if (options.NumberMax < options.NumberMin)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.NumberMax, "NumberMax must be >= NumberMin.");
        }

        if (options.TextPoolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TextPoolSize, "TextPoolSize must be > 0.");
        }

        if (options.RepeatProbability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.RepeatProbability, "RepeatProbability must be in [0..1].");
        }
    }

    private static List<string> CreateTextPool(Random random, int textPoolSize)
    {
        var pool = new List<string>(textPoolSize);

        for (var i = 0; i < textPoolSize; i++)
        {
            pool.Add(GenerateText(random));
        }

        return pool;
    }

    private static long NextNumber(Random random, long minInclusive, long maxInclusive)
    {
        if (minInclusive == maxInclusive)
        {
            return minInclusive;
        }

        if (maxInclusive == long.MaxValue)
        {
            return random.NextInt64(minInclusive, long.MaxValue);
        }

        return random.NextInt64(minInclusive, maxInclusive + 1);
    }

    private static string NextText(Random random, IReadOnlyList<string> textPool, double repeatProbability)
    {
        if (random.NextDouble() < repeatProbability)
        {
            return textPool[random.Next(textPool.Count)];
        }

        return GenerateText(random);
    }

    private static string GenerateText(Random random)
    {
        var wordsCount = random.Next(1, 8);

        var builder = new StringBuilder(capacity: wordsCount * 8);
        for (var i = 0; i < wordsCount; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Words[random.Next(Words.Length)]);
        }

        return builder.ToString();
    }
}
