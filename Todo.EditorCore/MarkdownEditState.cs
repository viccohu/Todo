namespace Todo.EditorCore;

public readonly record struct MarkdownEditState(
    string Text,
    int SelectionStart,
    int SelectionLength);
