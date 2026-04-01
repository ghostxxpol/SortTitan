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
