namespace Memo.NotepadEdit;

public readonly record struct NotepadEditState(
    string Text,
    int SelectionStart,
    int SelectionLength);
