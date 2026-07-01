using System;

namespace Memo;

internal static class NotepadTextNewlineHelper
{
    public static string Normalize(string text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

    public static string Denormalize(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\n", "\r\n");

    public static int RawIndexToNormalized(string raw, int rawIndex)
    {
        rawIndex = Math.Clamp(rawIndex, 0, raw.Length);
        var norm = 0;
        for (var i = 0; i < rawIndex; i++)
        {
            if (raw[i] == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n')
                i++;
            norm++;
        }
        return norm;
    }

    public static int NormalizedIndexToRaw(string normalized, int normIndex)
    {
        normIndex = Math.Clamp(normIndex, 0, normalized.Length);
        var raw = 0;
        for (var i = 0; i < normIndex; i++)
            raw += normalized[i] == '\n' ? 2 : 1;
        return raw;
    }
}
