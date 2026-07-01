using System;
using Memo.NotepadEdit;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Memo;

public static class NotepadSmartEditHelper
{
    public static bool TryApplyKeyDown(
        TextBox textBox,
        KeyRoutedEventArgs e,
        Action<string>? syncContent = null,
        Action? beginApply = null,
        Action? endApply = null,
        string debugSource = "Notepad")
    {
        var command = MapKey(e);
        if (command == null)
            return false;

        var raw = textBox.Text ?? string.Empty;
        var normText = NotepadTextNewlineHelper.Normalize(raw);
        var normStart = NotepadTextNewlineHelper.RawIndexToNormalized(raw, textBox.SelectionStart);
        var normEnd = NotepadTextNewlineHelper.RawIndexToNormalized(raw, textBox.SelectionStart + textBox.SelectionLength);
        var normLength = Math.Max(0, normEnd - normStart);
        var currentLine = NotepadSmartEditDebug.GetCurrentLine(normText, normStart);

        NotepadSmartEditDebug.LogKeyDown(
            debugSource,
            "TryApply:input",
            new NotepadSmartEditDebug.KeyInfo
            {
                Key = e.Key.ToString(),
                Handled = e.Handled,
                RawSelectionStart = textBox.SelectionStart,
                RawSelectionEnd = textBox.SelectionStart + textBox.SelectionLength,
                NormSelectionStart = normStart,
                NormSelectionEnd = normEnd,
                TextLength = raw.Length,
                CurrentLine = currentLine
            });

        var result = NotepadContinuationEngine.Apply(
            command.Value,
            new NotepadEditState(normText, normStart, normLength));

        var outLine = NotepadSmartEditDebug.GetCurrentLine(result.Text, result.SelectionStart);

        NotepadSmartEditDebug.LogEngine(
            debugSource,
            command.Value.ToString(),
            new NotepadSmartEditDebug.EngineInfo
            {
                Handled = result.Handled,
                InStart = normStart,
                InLength = normLength,
                OutStart = result.SelectionStart,
                OutLength = result.SelectionLength,
                InLine = currentLine,
                OutLine = outLine,
                TextChanged = result.Handled,
                OutTextPreview = PreviewText(result.Text)
            });

        if (!result.Handled)
        {
            if (ShouldShakeAtEdge(command.Value, normStart, normLength)
                && textBox is NotepadEditTextBox edgeEditor)
                edgeEditor.ShowIndentLimitShake();

            if (command == NotepadEditCommand.Backspace && normStart == 0 && normLength == 0)
                return true;

            NotepadSmartEditDebug.LogSkipped(debugSource, $"engine unhandled command={command}");
            return false;
        }

        if (result.IndentLimitReached && textBox is NotepadEditTextBox shakeEditor)
            shakeEditor.ShowIndentLimitShake();

        if (result.IndentLimitReached
            && result.Text == normText
            && result.SelectionStart == normStart
            && result.SelectionLength == normLength)
            return true;

        var displayText = NotepadTextNewlineHelper.Denormalize(result.Text);
        var displayStart = NotepadTextNewlineHelper.NormalizedIndexToRaw(result.Text, result.SelectionStart);
        var displayEnd = NotepadTextNewlineHelper.NormalizedIndexToRaw(
            result.Text,
            result.SelectionStart + result.SelectionLength);
        displayStart = Math.Clamp(displayStart, 0, displayText.Length);
        displayEnd = Math.Clamp(displayEnd, displayStart, displayText.Length);
        var displayLength = displayEnd - displayStart;

        beginApply?.Invoke();
        try
        {
            if (textBox is NotepadEditTextBox editor)
            {
                editor.ApplyProgrammaticEdit(displayText, displayStart, Math.Max(0, displayLength));
                syncContent?.Invoke(displayText);
            }
            else
            {
                syncContent?.Invoke(displayText);
                textBox.Text = displayText;
                textBox.SelectionStart = displayStart;
                textBox.SelectionLength = Math.Max(0, displayLength);
            }

            NotepadSmartEditDebug.LogApply(
                debugSource,
                new NotepadSmartEditDebug.ApplyInfo
                {
                    DisplayStart = displayStart,
                    DisplayLength = Math.Max(0, displayLength),
                    DisplayTextLength = displayText.Length,
                    Deferred = textBox is NotepadEditTextBox
                });
        }
        finally
        {
            endApply?.Invoke();
        }

        return true;
    }

    private static string PreviewText(string text, int maxLen = 120)
    {
        if (text.Length <= maxLen)
            return text;
        return text[..maxLen] + $"…(+{text.Length - maxLen})";
    }

    private static bool ShouldShakeAtEdge(NotepadEditCommand command, int selectionStart, int selectionLength) =>
        selectionLength == 0
        && command is NotepadEditCommand.Backspace or NotepadEditCommand.ShiftTab
        && (command == NotepadEditCommand.ShiftTab || selectionStart == 0);

    private static NotepadEditCommand? MapKey(KeyRoutedEventArgs e)
    {
        if (IsCtrlPressed())
            return null;

        if (e.Key == VirtualKey.Enter)
            return NotepadEditCommand.Enter;

        if (e.Key == VirtualKey.Back)
            return NotepadEditCommand.Backspace;

        if (e.Key == VirtualKey.Tab)
            return IsShiftPressed() ? NotepadEditCommand.ShiftTab : NotepadEditCommand.Tab;

        return null;
    }

    private static bool IsCtrlPressed() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);

    private static bool IsShiftPressed() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
}
