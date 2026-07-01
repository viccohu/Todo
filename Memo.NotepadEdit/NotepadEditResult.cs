namespace Memo.NotepadEdit;

public readonly record struct NotepadEditResult(
    string Text,
    int SelectionStart,
    int SelectionLength,
    bool Handled,
    bool IndentLimitReached = false);
