namespace SortTitan.Tests;

using System.Text;
using SortTitan.Core;

public class LineEntryParserTests
{
    [Fact]
    public void Parse_ValidLine_ReturnsEntry()
    {
        var parser = new LineEntryParser();

        var entry = parser.Parse("415. Apple");

        Assert.Equal(415, entry.Number);
        Assert.Equal("Apple", entry.Text);
    }

    [Theory]
    [InlineData("2. Banana is yellow", 2, "Banana is yellow")]
    [InlineData("1. A", 1, "A")]
    [InlineData("9223372036854775807. Max", 9223372036854775807, "Max")]
    [InlineData("  42. Trimmed input  ", 42, "Trimmed input")]
    [InlineData("7.   Multiple spaces", 7, "Multiple spaces")]
    public void TryParse_ValidLines_ReturnsTrueAndEntry(string line, long expectedNumber, string expectedText)
    {
        var parser = new LineEntryParser();

        var isParsed = parser.TryParse(line, out var entry);

        Assert.True(isParsed);
        Assert.Equal(expectedNumber, entry.Number);
        Assert.Equal(expectedText, entry.Text);
    }
}

public class LineEntryComparerTests
{
    [Fact]
    public void Compare_DifferentText_SortsByText()
    {
        var comparer = new LineEntryComparer();

        var x = new LineEntry(10, "Apple");
        var y = new LineEntry(1, "Banana");

        Assert.True(comparer.Compare(x, y) < 0);
        Assert.True(comparer.Compare(y, x) > 0);
    }

    [Fact]
    public void Compare_SameText_SortsByNumberAscending()
    {
        var comparer = new LineEntryComparer();

        var x = new LineEntry(2, "Apple");
        var y = new LineEntry(10, "Apple");

        Assert.True(comparer.Compare(x, y) < 0);
        Assert.True(comparer.Compare(y, x) > 0);
    }

    [Fact]
    public void Sort_MixedEntries_SortsByTextThenNumber()
    {
        var comparer = new LineEntryComparer();

        var items = new List<LineEntry>
        {
            new(2, "Banana"),
            new(10, "Apple"),
            new(2, "Apple"),
            new(1, "Banana"),
        };

        items.Sort(comparer);

        Assert.Equal(new LineEntry(2, "Apple"), items[0]);
        Assert.Equal(new LineEntry(10, "Apple"), items[1]);
        Assert.Equal(new LineEntry(1, "Banana"), items[2]);
        Assert.Equal(new LineEntry(2, "Banana"), items[3]);
    }
}

public sealed class TestFileGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_CreatesNonEmptyFile_WithParsableLines_RepeatedTexts_AndApproxSize()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SortTitan_Gen_{Guid.NewGuid():N}.txt");
        try
        {
            const long targetBytes = 20_000;
            var options = new GeneratorOptions
            {
                OutputPath = path,
                TargetSizeBytes = targetBytes,
                Seed = 123,
                TextPoolSize = 20,
                RepeatProbability = 1.0,
                NewLine = "\n",
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };

            var generator = new TestFileGenerator();
            await generator.GenerateAsync(options);

            Assert.True(File.Exists(path));

            var fileInfo = new FileInfo(path);
            Assert.True(fileInfo.Length > 0);
            Assert.True(fileInfo.Length >= targetBytes);

            const long allowedOvershoot = 2048;
            Assert.True(fileInfo.Length - targetBytes <= allowedOvershoot);

            var parser = new LineEntryParser();
            var texts = new List<string>();

            foreach (var line in File.ReadLines(path, options.Encoding))
            {
                Assert.True(parser.TryParse(line, out var entry));
                texts.Add(entry.Text);
            }

            Assert.NotEmpty(texts);
            Assert.Contains(texts.GroupBy(t => t), g => g.Count() > 1);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public sealed class ExternalFileSorterIntegrationTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public async Task SortAsync_EmptyFile_CreatesEmptyOutput()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            WriteLines(inputPath, []);

            await SortAsync(inputPath, outputPath, tempDir);

            Assert.True(File.Exists(outputPath));
            Assert.Empty(File.ReadAllLines(outputPath, Utf8NoBom));
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_SingleLine_WritesSameLine()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            WriteLines(inputPath, ["5. Apple"]);

            await SortAsync(inputPath, outputPath, tempDir);

            AssertFileSorted(outputPath);
            Assert.Equal(File.ReadAllLines(inputPath, Utf8NoBom), File.ReadAllLines(outputPath, Utf8NoBom));
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_SmallFile_SortsCorrectly()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            var inputLines = new[]
            {
                "415. Apple",
                "30432. Something something something",
                "1. Apple",
                "32. Cherry is the best",
                "2. Banana is yellow",
            };

            WriteLines(inputPath, inputLines);

            await SortAsync(inputPath, outputPath, tempDir, maxEntriesPerChunk: 2);

            var expected = SortLines(inputLines);
            var actual = File.ReadAllLines(outputPath, Utf8NoBom);

            AssertFileSorted(outputPath);
            Assert.Equal(expected, actual);
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_SmallFile_StrongCorrectness_MatchesInMemorySort()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            var inputLines = new[]
            {
                "9. Apple",
                "1. Apple",
                "2. Banana",
                "1. Banana",
                "10. Cherry",
                "5. Apple",
                "3. Banana",
                "7. Cherry",
            };

            WriteLines(inputPath, inputLines);

            await SortAsync(inputPath, outputPath, tempDir, maxEntriesPerChunk: 3);

            var expected = SortLines(File.ReadAllLines(inputPath, Utf8NoBom));
            var actual = File.ReadAllLines(outputPath, Utf8NoBom);

            Assert.Equal(expected, actual);
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_SameTextDifferentNumber_SortsByNumberAscending()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            var inputLines = new[] { "2. Apple", "10. Apple", "1. Apple" };
            WriteLines(inputPath, inputLines);

            await SortAsync(inputPath, outputPath, tempDir, maxEntriesPerChunk: 2);

            var actual = File.ReadAllLines(outputPath, Utf8NoBom);
            Assert.Equal(new[] { "1. Apple", "2. Apple", "10. Apple" }, actual);
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_AlreadySorted_KeepsOrder()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            var inputLines = new[]
            {
                "1. Apple",
                "2. Apple",
                "1. Banana",
                "2. Banana",
            };
            WriteLines(inputPath, inputLines);

            await SortAsync(inputPath, outputPath, tempDir, maxEntriesPerChunk: 2);

            AssertFileSorted(outputPath);
            Assert.Equal(inputLines, File.ReadAllLines(outputPath, Utf8NoBom));
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_ReverseOrder_SortsCorrectly()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            var sortedLines = new[]
            {
                "1. Apple",
                "2. Apple",
                "1. Banana",
                "2. Banana",
            };

            var reversed = sortedLines.Reverse().ToArray();
            WriteLines(inputPath, reversed);

            await SortAsync(inputPath, outputPath, tempDir, maxEntriesPerChunk: 2);

            AssertFileSorted(outputPath);
            Assert.Equal(sortedLines, File.ReadAllLines(outputPath, Utf8NoBom));
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SortAsync_InvalidLine_Throws()
    {
        var testDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(testDir, "input.txt");
            var outputPath = Path.Combine(testDir, "output.txt");
            var tempDir = Path.Combine(testDir, "temp");

            var inputLines = new[] { "1. Apple", "invalid line", "2. Banana" };
            WriteLines(inputPath, inputLines);

            var sorter = new ExternalFileSorter();
            var options = CreateSorterOptions(inputPath, outputPath, tempDir, maxEntriesPerChunk: 2) with
            {
                KeepTempFilesOnError = false
            };

            await Assert.ThrowsAsync<InvalidDataException>(() => sorter.SortAsync(options));
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            TryDeleteDirectory(testDir);
        }
    }

    private static async Task SortAsync(string inputPath, string outputPath, string tempDir, int maxEntriesPerChunk = int.MaxValue)
    {
        var sorter = new ExternalFileSorter();
        var options = CreateSorterOptions(inputPath, outputPath, tempDir, maxEntriesPerChunk);
        await sorter.SortAsync(options);
    }

    private static ExternalFileSorterOptions CreateSorterOptions(string inputPath, string outputPath, string tempDir, int maxEntriesPerChunk)
    {
        return new ExternalFileSorterOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            TempDirectory = tempDir,
            TotalMemoryBudgetBytesOverride = 256 * 1024,
            MaxInFlightChunks = 1,
            MaxEntriesPerChunk = maxEntriesPerChunk,
        };
    }

    private static void AssertFileSorted(string outputPath)
    {
        var entries = ReadEntries(outputPath);
        AssertSorted(entries);
    }

    private static List<LineEntry> ReadEntries(string path)
    {
        var parser = new LineEntryParser();
        var entries = new List<LineEntry>();

        foreach (var line in File.ReadLines(path, Utf8NoBom))
        {
            Assert.True(parser.TryParse(line, out var entry));
            entries.Add(entry);
        }

        return entries;
    }

    private static void AssertSorted(IReadOnlyList<LineEntry> entries)
    {
        var comparer = new LineEntryComparer();
        for (var i = 1; i < entries.Count; i++)
        {
            Assert.True(comparer.Compare(entries[i - 1], entries[i]) <= 0);
        }
    }

    private static string[] SortLines(IEnumerable<string> lines)
    {
        var parser = new LineEntryParser();
        var entries = new List<LineEntry>();

        foreach (var line in lines)
        {
            Assert.True(parser.TryParse(line, out var entry));
            entries.Add(entry);
        }

        entries.Sort(new LineEntryComparer());
        return entries.Select(e => $"{e.Number}. {e.Text}").ToArray();
    }

    private static void WriteLines(string path, IEnumerable<string> lines)
    {
        var content = string.Join("\n", lines);
        if (!string.IsNullOrEmpty(content))
        {
            content += "\n";
        }

        File.WriteAllText(path, content, Utf8NoBom);
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"SortTitan_SorterIT_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
