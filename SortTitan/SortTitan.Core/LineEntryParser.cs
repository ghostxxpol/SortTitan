using System.Globalization;

namespace SortTitan.Core;

public sealed class LineEntryParser
{
    public LineEntry Parse(string line)
    {
        if (TryParse(line, out var entry))
        {
            return entry;
        }

        throw new FormatException("Line must match format: <Number>. <String>.");
    }

    public bool TryParse(string line, out LineEntry entry)
    {
        entry = default;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var span = line.AsSpan().Trim();
        var dotIndex = span.IndexOf('.');

        if (dotIndex <= 0)
        {
            return false;
        }

        var numberSpan = span[..dotIndex];
        if (!long.TryParse(numberSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        var suffixSpan = span[(dotIndex + 1)..];
        if (suffixSpan.IsEmpty || suffixSpan[0] != ' ')
        {
            return false;
        }

        var textSpan = suffixSpan.TrimStart();
        if (textSpan.IsEmpty)
        {
            return false;
        }

        entry = new LineEntry(number, textSpan.ToString());
        return true;
    }
}
