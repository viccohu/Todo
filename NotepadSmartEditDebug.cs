using System;
using System.Text;
using Memo.Services;

namespace Memo;

/// <summary>
/// 记事本智能续行调试日志。写入 Debug 输出与 %LocalAppData%\Packages\...\LocalState\app.log。
/// 定位完成后将 <see cref="Enabled"/> 设为 false。
/// </summary>
public static class NotepadSmartEditDebug
{
    public static bool Enabled = false;

    public static void LogInit(string source)
    {
        if (!Enabled) return;
        AppLog.Notepad($"[{source}] smart-edit debug ON, log={AppLog.LogPath}");
    }

    public static void LogKeyDown(string source, string phase, KeyInfo info)
    {
        if (!Enabled) return;
        AppLog.Notepad(
            $"[{source}] {phase} key={info.Key} handled={info.Handled} preview={info.IsPreviewMode} " +
            $"acceptsReturn={info.AcceptsReturn} readOnly={info.IsReadOnly} " +
            $"sel={info.RawSelectionStart}..{info.RawSelectionEnd} norm={info.NormSelectionStart}..{info.NormSelectionEnd} " +
            $"line={Quote(info.CurrentLine)} textLen={info.TextLength}");
    }

    public static void LogEngine(string source, string command, EngineInfo info)
    {
        if (!Enabled) return;
        AppLog.Notepad(
            $"[{source}] engine {command} handled={info.Handled} " +
            $"inSel={info.InStart}+{info.InLength} outSel={info.OutStart}+{info.OutLength} " +
            $"inLine={Quote(info.InLine)} outLine={Quote(info.OutLine)} " +
            $"textChanged={info.TextChanged}");
        if (info.TextChanged && Enabled)
            AppLog.Notepad($"[{source}] engine textOut={Quote(info.OutTextPreview)}");
    }

    public static void LogApply(string source, ApplyInfo info)
    {
        if (!Enabled) return;
        AppLog.Notepad(
            $"[{source}] apply displaySel={info.DisplayStart}+{info.DisplayLength} " +
            $"textLen={info.DisplayTextLength} deferred={info.Deferred}");
    }

    public static void LogDeferred(string source, bool restored, int displayStart, int displayLength, string? reason = null)
    {
        if (!Enabled) return;
        AppLog.Notepad(
            $"[{source}] deferred restore={restored} sel={displayStart}+{displayLength}" +
            (reason == null ? "" : $" reason={reason}"));
    }

    public static void LogNote(string source, string message)
    {
        if (!Enabled) return;
        AppLog.Notepad($"[{source}] {message}");
    }

    public static void LogSkipped(string source, string reason)
    {
        if (!Enabled) return;
        AppLog.Notepad($"[{source}] skip {reason}");
    }

    public static string Quote(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "\"\"";

        var sb = new StringBuilder(text.Length + 8);
        sb.Append('"');
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '"' => "\\\"",
                _ => ch.ToString()
            });
        }
        sb.Append('"');
        return sb.ToString();
    }

    public static string GetCurrentLine(string normalizedText, int normIndex)
    {
        if (string.IsNullOrEmpty(normalizedText))
            return string.Empty;

        normIndex = Math.Clamp(normIndex, 0, normalizedText.Length);

        var lineStart = 0;
        for (var i = normIndex - 1; i >= 0; i--)
        {
            if (normalizedText[i] == '\n')
            {
                lineStart = i + 1;
                break;
            }
        }

        var lineEnd = normalizedText.Length;
        for (var i = normIndex; i < normalizedText.Length; i++)
        {
            if (normalizedText[i] == '\n')
            {
                lineEnd = i;
                break;
            }
        }

        return normalizedText.Substring(lineStart, lineEnd - lineStart);
    }

    public readonly struct KeyInfo
    {
        public string Key { get; init; }
        public bool Handled { get; init; }
        public bool IsPreviewMode { get; init; }
        public bool AcceptsReturn { get; init; }
        public bool IsReadOnly { get; init; }
        public int RawSelectionStart { get; init; }
        public int RawSelectionEnd { get; init; }
        public int NormSelectionStart { get; init; }
        public int NormSelectionEnd { get; init; }
        public int TextLength { get; init; }
        public string CurrentLine { get; init; }
    }

    public readonly struct EngineInfo
    {
        public bool Handled { get; init; }
        public int InStart { get; init; }
        public int InLength { get; init; }
        public int OutStart { get; init; }
        public int OutLength { get; init; }
        public string InLine { get; init; }
        public string OutLine { get; init; }
        public bool TextChanged { get; init; }
        public string OutTextPreview { get; init; }
    }

    public readonly struct ApplyInfo
    {
        public int DisplayStart { get; init; }
        public int DisplayLength { get; init; }
        public int DisplayTextLength { get; init; }
        public bool Deferred { get; init; }
    }
}
