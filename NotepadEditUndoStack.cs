using System.Collections.Generic;

namespace Memo;

public readonly record struct NotepadEditSnapshot(string Text, int SelectionStart, int SelectionLength);

public sealed class NotepadEditUndoStack
{
    private readonly Stack<NotepadEditSnapshot> _undo = new();
    private readonly Stack<NotepadEditSnapshot> _redo = new();
    private const int MaxDepth = 200;

    public void Push(NotepadEditSnapshot snapshot)
    {
        if (_undo.Count > 0 && _undo.Peek() == snapshot)
            return;

        _undo.Push(snapshot);
        Trim(_undo);
        _redo.Clear();
    }

    public bool TryUndo(NotepadEditSnapshot current, out NotepadEditSnapshot target)
    {
        if (_undo.Count == 0)
        {
            target = default;
            return false;
        }

        _redo.Push(current);
        target = _undo.Pop();
        return true;
    }

    public bool TryRedo(NotepadEditSnapshot current, out NotepadEditSnapshot target)
    {
        if (_redo.Count == 0)
        {
            target = default;
            return false;
        }

        _undo.Push(current);
        target = _redo.Pop();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private static void Trim(Stack<NotepadEditSnapshot> stack)
    {
        if (stack.Count <= MaxDepth)
            return;

        var items = new List<NotepadEditSnapshot>(stack);
        stack.Clear();
        for (var i = items.Count - MaxDepth; i < items.Count; i++)
            stack.Push(items[i]);
    }
}
