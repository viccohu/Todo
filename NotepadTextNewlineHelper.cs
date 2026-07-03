using Memo.NotepadEdit;

namespace Memo;

internal static class NotepadTextNewlineHelper
{
    public static string Normalize(string text) => NotepadNewlineHelper.Normalize(text);

    public static string Denormalize(string text) => NotepadNewlineHelper.Denormalize(text);

    public static int RawIndexToNormalized(string raw, int rawIndex) =>
        NotepadNewlineHelper.RawIndexToNormalized(raw, rawIndex);

    public static int NormalizedIndexToRaw(string normalized, int normIndex) =>
        NotepadNewlineHelper.NormalizedIndexToRaw(normalized, normIndex);
}
