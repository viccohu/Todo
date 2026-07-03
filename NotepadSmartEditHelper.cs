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
        var normStart = NotepadNewlineHelper.RawIndexToNormalized(raw, textBox.SelectionStart);
        var currentLine = NotepadSmartEditDebug.GetCurrentLine(
            NotepadNewlineHelper.Normalize(raw),
            normStart);

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
                NormSelectionEnd = NotepadNewlineHelper.RawIndexToNormalized(
                    raw,
                    textBox.SelectionStart + textBox.SelectionLength),
                TextLength = raw.Length,
                CurrentLine = currentLine
            });

        var pipeline = NotepadSmartEditPipeline.ApplyKey(
            command.Value,
            raw,
            textBox.SelectionStart,
            textBox.SelectionLength);

        var outLine = NotepadSmartEditDebug.GetCurrentLine(
            NotepadNewlineHelper.Normalize(pipeline.DisplayText),
            NotepadNewlineHelper.RawIndexToNormalized(
                pipeline.DisplayText,
                pipeline.DisplaySelectionStart));

        NotepadSmartEditDebug.LogEngine(
            debugSource,
            command.Value.ToString(),
            new NotepadSmartEditDebug.EngineInfo
            {
                Handled = pipeline.Handled,
                InStart = normStart,
                InLength = textBox.SelectionLength,
                OutStart = NotepadNewlineHelper.RawIndexToNormalized(
                    pipeline.DisplayText,
                    pipeline.DisplaySelectionStart),
                OutLength = pipeline.DisplaySelectionLength,
                InLine = currentLine,
                OutLine = outLine,
                TextChanged = pipeline.Handled && pipeline.DisplayText != raw,
                OutTextPreview = PreviewText(pipeline.DisplayText)
            });

        if (!pipeline.Handled)
        {
            if (ShouldShakeAtEdge(command.Value, normStart, textBox.SelectionLength)
                && textBox is NotepadEditTextBox edgeEditor)
                edgeEditor.ShowIndentLimitShake();

            if (command == NotepadEditCommand.Backspace && normStart == 0 && textBox.SelectionLength == 0)
                return true;

            NotepadSmartEditDebug.LogSkipped(debugSource, $"engine unhandled command={command}");
            return false;
        }

        if (pipeline.IndentLimitReached && textBox is NotepadEditTextBox shakeEditor)
            shakeEditor.ShowIndentLimitShake();

        if (pipeline.IndentLimitReached
            && pipeline.DisplayText == raw
            && pipeline.DisplaySelectionStart == textBox.SelectionStart
            && pipeline.DisplaySelectionLength == textBox.SelectionLength)
            return true;

        beginApply?.Invoke();
        try
        {
            if (textBox is NotepadEditTextBox editor)
            {
                editor.ApplyProgrammaticEdit(
                    pipeline.DisplayText,
                    pipeline.DisplaySelectionStart,
                    pipeline.DisplaySelectionLength);
                syncContent?.Invoke(pipeline.DisplayText);
            }
            else
            {
                syncContent?.Invoke(pipeline.DisplayText);
                textBox.Text = pipeline.DisplayText;
                textBox.SelectionStart = pipeline.DisplaySelectionStart;
                textBox.SelectionLength = pipeline.DisplaySelectionLength;
            }

            NotepadSmartEditDebug.LogApply(
                debugSource,
                new NotepadSmartEditDebug.ApplyInfo
                {
                    DisplayStart = pipeline.DisplaySelectionStart,
                    DisplayLength = pipeline.DisplaySelectionLength,
                    DisplayTextLength = pipeline.DisplayText.Length,
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
