namespace SortTitan.Tests;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(". Apple")]
    [InlineData("415.Apple")]
    [InlineData("415 Apple")]
    [InlineData("415. ")]
    [InlineData("abc. Apple")]
    [InlineData("-1. Apple")]
    [InlineData("9223372036854775808. Overflow")]
    public void TryParse_InvalidLines_ReturnsFalse(string? line)
    {
        var parser = new LineEntryParser();

        var isParsed = parser.TryParse(line!, out var entry);

        Assert.False(isParsed);
        Assert.Equal(default, entry);
    }

    [Fact]
    public void Parse_InvalidLine_ThrowsFormatException()
    {
        var parser = new LineEntryParser();

        Assert.Throws<FormatException>(() => parser.Parse("invalid"));
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
