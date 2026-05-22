namespace Todo.EditorCore;

public readonly record struct MarkdownEditResult(
    string Text,
    int SelectionStart,
    int SelectionLength);
