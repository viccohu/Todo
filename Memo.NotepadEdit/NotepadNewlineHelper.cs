namespace Memo.NotepadEdit;

public static class NotepadNewlineHelper
{
    public static string Normalize(string text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

    // WinUI TextBox 内部换行统一为单个 '\r'（赋入 "\r\n" 会被控件压缩成 "\r"）。
    // 输出必须与控件的内部表示一致，否则 SelectionStart 会按行数累积偏移。
    public static string Denormalize(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\n", "\r");

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

    // 换行为单字符 '\r'，与规范化文本一一对应。
    public static int NormalizedIndexToRaw(string normalized, int normIndex) =>
        Math.Clamp(normIndex, 0, normalized.Length);
}
