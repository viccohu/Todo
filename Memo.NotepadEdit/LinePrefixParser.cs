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

    public static string NextMarker(LinePrefix prefix) =>
        prefix.MarkerKind switch
        {
            LineMarkerKind.OrderedWestern or LineMarkerKind.OrderedChinesePunct or LineMarkerKind.OrderedChineseChar
                => FormatOrderedMarker(prefix.MarkerKind, GetOrderedIndex(prefix) + 1, prefix.MarkerText),
            LineMarkerKind.Bullet => prefix.MarkerText,
            LineMarkerKind.Quote => "> ",
            _ => string.Empty
        };

    public static bool IsOrderedKind(LineMarkerKind kind) =>
        kind is LineMarkerKind.OrderedWestern
            or LineMarkerKind.OrderedChinesePunct
            or LineMarkerKind.OrderedChineseChar;

    public static int GetOrderedIndex(LinePrefix prefix)
    {
        if (!IsOrderedKind(prefix.MarkerKind))
            return 0;

        return prefix.MarkerKind switch
        {
            LineMarkerKind.OrderedWestern => ParseWesternIndex(prefix.MarkerText),
            LineMarkerKind.OrderedChinesePunct => ParseChinesePunctIndex(prefix.MarkerText),
            LineMarkerKind.OrderedChineseChar => ParseChineseCharIndex(prefix.MarkerText),
            _ => 0
        };
    }

    public static string FormatOrderedMarker(LineMarkerKind kind, int number, string templateMarker)
    {
        number = Math.Max(1, number);
        return kind switch
        {
            LineMarkerKind.OrderedWestern => FormatWesternMarker(templateMarker, number),
            LineMarkerKind.OrderedChinesePunct => FormatChinesePunctMarker(templateMarker, number),
            LineMarkerKind.OrderedChineseChar => FormatChineseCharMarker(templateMarker, number),
            _ => templateMarker
        };
    }

    public static LinePrefix ReplaceOrderedIndex(LinePrefix prefix, int number)
    {
        if (!IsOrderedKind(prefix.MarkerKind))
            return prefix;

        var marker = FormatOrderedMarker(prefix.MarkerKind, number, prefix.MarkerText);
        return prefix with { MarkerText = marker };
    }

    public static string ApplyPrefixToContent(LinePrefix prefix) =>
        prefix.LeadingWhitespace + prefix.MarkerText + prefix.Content;

    public static string RenumberLine(string line, int number)
    {
        line = line.TrimEnd('\r');
        var parsed = Parse(line);
        if (!IsOrderedKind(parsed.MarkerKind))
            return line;

        var leading = parsed.LeadingWhitespace;
        var rest = line[leading.Length..];
        var matchLen = GetMarkerMatchLength(rest, parsed.MarkerKind);
        if (matchLen <= 0)
            return line;

        var newMarker = FormatOrderedMarker(parsed.MarkerKind, number, parsed.MarkerText);
        if (matchLen < newMarker.Length && rest.Length > matchLen && rest[matchLen] != ' ')
            newMarker = newMarker.TrimEnd();

        return leading + newMarker + rest[matchLen..];
    }

    public static int GetMarkerEndIndex(string line)
    {
        line = line.TrimEnd('\r');
        var prefix = Parse(line);
        if (!prefix.HasMarker)
            return prefix.LeadingWhitespace.Length;

        var rest = line[prefix.LeadingWhitespace.Length..];
        return prefix.LeadingWhitespace.Length + GetMarkerMatchLength(rest, prefix.MarkerKind);
    }

    private static int GetMarkerMatchLength(string rest, LineMarkerKind kind) =>
        kind switch
        {
            LineMarkerKind.OrderedWestern => OrderedWestern.Match(rest) is { Success: true } m ? m.Length : 0,
            LineMarkerKind.OrderedChinesePunct => OrderedChinesePunct.Match(rest) is { Success: true } m ? m.Length : 0,
            LineMarkerKind.OrderedChineseChar => OrderedChineseWrapped.Match(rest) is { Success: true } w ? w.Length
                : OrderedChineseChar.Match(rest) is { Success: true } p ? p.Length : 0,
            _ => 0
        };

    private static int ParseWesternIndex(string marker)
    {
        var m = Regex.Match(marker.Trim(), @"^(\d+)([\.\)])\s*$");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }

    private static int ParseChinesePunctIndex(string marker)
    {
        var m = OrderedChinesePunct.Match(marker);
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }

    private static int ParseChineseCharIndex(string marker)
    {
        var wrapped = OrderedChineseWrapped.Match(marker);
        if (wrapped.Success)
            return ChineseNumeralHelper.Parse(wrapped.Groups[1].Value);

        var plain = OrderedChineseChar.Match(marker);
        if (plain.Success)
            return ChineseNumeralHelper.Parse(plain.Groups[1].Value);

        return 1;
    }

    private static string FormatWesternMarker(string templateMarker, int number)
    {
        var m = Regex.Match(templateMarker.Trim(), @"^(\d+)([\.\)])\s*$");
        if (!m.Success)
            return templateMarker;
        return $"{number}{m.Groups[2].Value} ";
    }

    private static string FormatChinesePunctMarker(string templateMarker, int number)
    {
        var m = OrderedChinesePunct.Match(templateMarker);
        if (!m.Success)
            return templateMarker;
        return $"{number}{m.Groups[2].Value} ";
    }

    private static string FormatChineseCharMarker(string templateMarker, int number)
    {
        var wrapped = OrderedChineseWrapped.Match(templateMarker);
        if (wrapped.Success)
            return $"（{ChineseNumeralHelper.Format(number)}） ";

        var plain = OrderedChineseChar.Match(templateMarker);
        if (plain.Success)
            return $"{ChineseNumeralHelper.Format(number)}{plain.Groups[2].Value} ";

        return templateMarker;
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
        for (var tens = 2; tens <= 9; tens++)
        {
            map[$"{Digits[tens]}十"] = tens * 10;
            for (var ones = 1; ones <= 9; ones++)
                map[$"{Digits[tens]}十{Digits[ones]}"] = tens * 10 + ones;
        }
        for (var ones = 1; ones <= 9; ones++)
            map[$"十{Digits[ones]}"] = 10 + ones;
        return map;
    }

    public static string Increment(string value) => Format(Parse(value) + 1);

    public static int Parse(string value) => Map.TryGetValue(value, out var n) ? n : 1;

    public static string Format(int number)
    {
        number = Math.Max(1, number);
        if (number <= 9)
            return Digits[number];
        if (number == 10)
            return "十";
        if (number < 20)
            return "十" + Digits[number - 10];
        if (number % 10 == 0)
            return Digits[number / 10] + "十";
        if (number < 100)
            return Digits[number / 10] + "十" + Digits[number % 10];
        return number.ToString();
    }
}
