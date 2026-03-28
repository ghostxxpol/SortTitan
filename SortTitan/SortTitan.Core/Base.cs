namespace SortTitan.Core;

public readonly record struct LineEntry
{
    public long Number { get; }
    public string Text { get; }

    public LineEntry(long number, string text)
    {
        Number = number;
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }
}

public sealed class LineEntryComparer : IComparer<LineEntry>
{
    private static readonly StringComparer TextComparer = StringComparer.Ordinal;

    public int Compare(LineEntry x, LineEntry y)
    {
        var textComparison = TextComparer.Compare(x.Text, y.Text);
        if (textComparison != 0)
        {
            return textComparison;
        }

        return x.Number.CompareTo(y.Number);
    }
}
