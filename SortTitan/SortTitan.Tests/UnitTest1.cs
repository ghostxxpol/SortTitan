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
