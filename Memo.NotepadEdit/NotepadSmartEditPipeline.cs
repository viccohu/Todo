namespace Memo.NotepadEdit;

/// <summary>供 UI 与单测共用的智能编辑管线（规范化 → 引擎 → 反规范化）。</summary>
public static class NotepadSmartEditPipeline
{
    public readonly record struct PipelineResult(
        string DisplayText,
        int DisplaySelectionStart,
        int DisplaySelectionLength,
        bool Handled,
        bool IndentLimitReached);

    public static PipelineResult ApplyKey(
        NotepadEditCommand command,
        string rawText,
        int rawSelectionStart,
        int rawSelectionLength)
    {
        var normText = NotepadNewlineHelper.Normalize(rawText);
        var normStart = NotepadNewlineHelper.RawIndexToNormalized(rawText, rawSelectionStart);
        var normEnd = NotepadNewlineHelper.RawIndexToNormalized(rawText, rawSelectionStart + rawSelectionLength);
        var normLength = Math.Max(0, normEnd - normStart);

        var result = NotepadContinuationEngine.Apply(
            command,
            new NotepadEditState(normText, normStart, normLength));

        if (!result.Handled)
        {
            return new PipelineResult(
                rawText,
                rawSelectionStart,
                rawSelectionLength,
                Handled: false,
                result.IndentLimitReached);
        }

        if (result.IndentLimitReached
            && result.Text == normText
            && result.SelectionStart == normStart
            && result.SelectionLength == normLength)
        {
            return new PipelineResult(
                rawText,
                rawSelectionStart,
                rawSelectionLength,
                Handled: true,
                IndentLimitReached: true);
        }

        var displayText = NotepadNewlineHelper.Denormalize(result.Text);
        var displayStart = NotepadNewlineHelper.NormalizedIndexToRaw(result.Text, result.SelectionStart);
        var displayEnd = NotepadNewlineHelper.NormalizedIndexToRaw(
            result.Text,
            result.SelectionStart + result.SelectionLength);
        displayStart = Math.Clamp(displayStart, 0, displayText.Length);
        displayEnd = Math.Clamp(displayEnd, displayStart, displayText.Length);

        return new PipelineResult(
            displayText,
            displayStart,
            displayEnd - displayStart,
            Handled: true,
            result.IndentLimitReached);
    }
}
