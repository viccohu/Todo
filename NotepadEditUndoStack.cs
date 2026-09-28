using System;
using System.Collections.Generic;

namespace Memo;

public readonly record struct NotepadEditSnapshot(string Text, int SelectionStart, int SelectionLength);

public sealed class NotepadEditUndoStack
{
    private readonly Stack<NotepadEditSnapshot> _undo = new();
    private readonly Stack<NotepadEditSnapshot> _redo = new();
    private const int MaxDepth = 200;
    private string? _typingText;
    private int _typingCaret;
    private DateTimeOffset _typingTime;

    public void PushTyping(NotepadEditSnapshot snapshot, string newText, DateTimeOffset now)
    {
        var added = newText.Length - snapshot.Text.Length;
        var start = snapshot.SelectionStart;
        var insertion = snapshot.SelectionLength == 0 && added > 0
            && start >= 0 && start <= snapshot.Text.Length
            && newText.StartsWith(snapshot.Text[..start], StringComparison.Ordinal)
            && newText.EndsWith(snapshot.Text[start..], StringComparison.Ordinal);
        var plain = insertion && newText.Substring(start, added).IndexOfAny(new[] { '\r', '\n', '\t' }) < 0;
        var merge = plain && _typingText == snapshot.Text && _typingCaret == start
            && now - _typingTime <= TimeSpan.FromSeconds(1) && now >= _typingTime;
        if (!merge)
            Push(snapshot);
        else
            _redo.Clear();
        _typingText = plain ? newText : null;
        _typingCaret = start + added;
        _typingTime = now;
    }

    public void Push(NotepadEditSnapshot snapshot)
    {
        _typingText = null;
        if (_undo.Count > 0 && _undo.Peek() == snapshot)
            return;

        _undo.Push(snapshot);
        Trim(_undo);
        _redo.Clear();
    }

    public bool TryUndo(NotepadEditSnapshot current, out NotepadEditSnapshot target)
    {
        _typingText = null;
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
        _typingText = null;
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
        _typingText = null;
        _undo.Clear();
        _redo.Clear();
    }

    private static void Trim(Stack<NotepadEditSnapshot> stack)
    {
        if (stack.Count <= MaxDepth)
            return;

        var items = new List<NotepadEditSnapshot>(stack);
        stack.Clear();
        for (var i = MaxDepth - 1; i >= 0; i--)
            stack.Push(items[i]);
    }
}
