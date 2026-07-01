using System.Text.RegularExpressions;

namespace Memo.NotepadEdit;

internal enum LineMarkerKind
{
    None,
    OrderedWestern,
    OrderedChinesePunct,
    OrderedChineseChar,
    Bullet,
    Quote
}

internal readonly record struct LinePrefix(
    string LeadingWhitespace,
    LineMarkerKind MarkerKind,
    string MarkerText,
    string Content)
{
    public bool HasMarker => MarkerKind != LineMarkerKind.None;
    public bool IsContentEmpty => string.IsNullOrWhiteSpace(Content);
}

internal static class LinePrefixParser
{
    private static readonly Regex OrderedWestern = new(@"^(\d+)([\.\)])\s", RegexOptions.Compiled);
    private static readonly Regex OrderedChinesePunct = new(@"^(\d+)([、）])\s?", RegexOptions.Compiled);
    private static readonly Regex OrderedChineseWrapped = new(@"^（([一二三四五六七八九十]+)）\s?", RegexOptions.Compiled);
    private static readonly Regex OrderedChineseChar = new(@"^([一二三四五六七八九十]+)([、）])\s?", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^([-+*·•●○])\s", RegexOptions.Compiled);
    private static readonly Regex Quote = new(@"^(>)\s?", RegexOptions.Compiled);

    public static LinePrefix Parse(string line)
    {
        line = line.TrimEnd('\r');
        var leading = TakeLeadingWhitespace(line);
        var rest = line[leading.Length..];

        if (TryMatchOrderedWestern(rest, out var western))
            return new LinePrefix(leading, LineMarkerKind.OrderedWestern, western.Marker, western.Content);

        if (TryMatchOrderedChinesePunct(rest, out var cnPunct))
            return new LinePrefix(leading, LineMarkerKind.OrderedChinesePunct, cnPunct.Marker, cnPunct.Content);

        if (TryMatchOrderedChineseWrapped(rest, out var wrapped))
            return new LinePrefix(leading, LineMarkerKind.OrderedChineseChar, wrapped.Marker, wrapped.Content);

        if (TryMatchOrderedChineseChar(rest, out var cnChar))
            return new LinePrefix(leading, LineMarkerKind.OrderedChineseChar, cnChar.Marker, cnChar.Content);

        if (Bullet.Match(rest) is { Success: true } bullet)
            return new LinePrefix(leading, LineMarkerKind.Bullet, bullet.Groups[1].Value + " ", rest[bullet.Length..]);

        if (Quote.Match(rest) is { Success: true } quote)
            return new LinePrefix(leading, LineMarkerKind.Quote, quote.Groups[1].Value + " ", rest[quote.Length..]);

        return new LinePrefix(leading, LineMarkerKind.None, string.Empty, rest);
    }

    private static string TakeLeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;
        return line[..i];
    }

    private static bool TryMatchOrderedWestern(string rest, out (string Marker, string Content) result)
    {
        result = default;
        var m = OrderedWestern.Match(rest);
        if (!m.Success) return false;
        result = ($"{m.Groups[1].Value}{m.Groups[2].Value} ", m.Groups[0].Length < rest.Length ? rest[m.Groups[0].Length..] : string.Empty);
        return true;
    }

    private static bool TryMatchOrderedChinesePunct(string rest, out (string Marker, string Content) result)
    {
        result = default;
        var m = OrderedChinesePunct.Match(rest);
        if (!m.Success) return false;
        var marker = $"{m.Groups[1].Value}{m.Groups[2].Value} ";
        result = (marker, rest[m.Groups[0].Length..]);
        return true;
    }

    private static bool TryMatchOrderedChineseWrapped(string rest, out (string Marker, string Content) result)
    {
        result = default;
        var m = OrderedChineseWrapped.Match(rest);
        if (!m.Success) return false;
        var marker = $"（{m.Groups[1].Value}） ";
        result = (marker, rest[m.Groups[0].Length..]);
        return true;
    }

    private static bool TryMatchOrderedChineseChar(string rest, out (string Marker, string Content) result)
    {
        result = default;
        var m = OrderedChineseChar.Match(rest);
        if (!m.Success) return false;
        var marker = $"{m.Groups[1].Value}{m.Groups[2].Value} ";
        result = (marker, rest[m.Groups[0].Length..]);
        return true;
    }

    public static string NextMarker(LinePrefix prefix)
    {
        return prefix.MarkerKind switch
        {
            LineMarkerKind.OrderedWestern => NextWesternMarker(prefix.MarkerText),
            LineMarkerKind.OrderedChinesePunct => NextChinesePunctMarker(prefix.MarkerText),
            LineMarkerKind.OrderedChineseChar => NextChineseCharMarker(prefix.MarkerText),
            LineMarkerKind.Bullet => prefix.MarkerText,
            LineMarkerKind.Quote => "> ",
            _ => string.Empty
        };
    }

    private static string NextWesternMarker(string marker)
    {
        var m = Regex.Match(marker.Trim(), @"^(\d+)([\.\)])\s*$");
        if (!m.Success) return marker;
        var n = int.Parse(m.Groups[1].Value) + 1;
        return $"{n}{m.Groups[2].Value} ";
    }

    private static string NextChinesePunctMarker(string marker)
    {
        var m = OrderedChinesePunct.Match(marker);
        if (!m.Success) return marker;
        var n = int.Parse(m.Groups[1].Value) + 1;
        return $"{n}{m.Groups[2].Value} ";
    }

    private static string NextChineseCharMarker(string marker)
    {
        var wrapped = OrderedChineseWrapped.Match(marker);
        if (wrapped.Success)
        {
            var next = ChineseNumeralHelper.Increment(wrapped.Groups[1].Value);
            return $"（{next}） ";
        }

        var plain = OrderedChineseChar.Match(marker);
        if (plain.Success)
        {
            var next = ChineseNumeralHelper.Increment(plain.Groups[1].Value);
            return $"{next}{plain.Groups[2].Value} ";
        }

        return marker;
    }
}

internal static class ChineseNumeralHelper
{
    private static readonly string[] Digits = ["", "一", "二", "三", "四", "五", "六", "七", "八", "九"];
    private static readonly Dictionary<string, int> Map = BuildMap();

    private static Dictionary<string, int> BuildMap()
    {
        var map = new Dictionary<string, int>();
        for (var i = 1; i <= 9; i++)
            map[Digits[i]] = i;
        map["十"] = 10;
        return map;
    }

    public static string Increment(string value)
    {
        if (Map.TryGetValue(value, out var n))
        {
            var next = n + 1;
            if (next < 10)
                return Digits[next];
            if (next == 10)
                return "十";
        }

        return value;
    }
}
