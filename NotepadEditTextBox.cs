using System;
using Memo.NotepadEdit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;
using Windows.UI.Core;

namespace Memo;

/// <summary>
/// 记事本编辑框：主题色、多步撤销、智能续行按键入口。
/// </summary>
public sealed class NotepadEditTextBox : TextBox
{
    private readonly NotepadEditUndoStack _undo = new();
    private readonly TranslateTransform _shakeTransform = new();
    private bool _suppressUndoCapture;
    private Storyboard? _indentLimitShakeStoryboard;

    public event Action<KeyRoutedEventArgs>? SmartKeyDown;
    public event Action<string>? EditStateChanged;

    public NotepadEditTextBox()
    {
        UseSystemFocusVisuals = false;
        RenderTransform = _shakeTransform;
        Loaded += (_, _) => ApplyEditorTheme();
        GotFocus += (_, _) => ApplyEditorTheme();
        RegisterPropertyChangedCallback(IsReadOnlyProperty, (_, _) => ApplyEditorTheme());
        BeforeTextChanging += OnBeforeTextChanging;
        CuttingToClipboard += OnCuttingToClipboard;
    }

    /// <summary>
    /// 接管剪切（Ctrl+X 与右键菜单），让删除走智能编辑管线，触发有序列表重排。
    /// </summary>
    private void OnCuttingToClipboard(TextBox sender, TextControlCuttingToClipboardEventArgs args)
    {
        if (IsReadOnly || SelectionLength <= 0)
            return;

        var raw = Text ?? string.Empty;
        var start = Math.Clamp(SelectionStart, 0, raw.Length);
        var length = Math.Clamp(SelectionLength, 0, raw.Length - start);
        if (length <= 0)
            return;

        args.Handled = true;

        // 剪贴板内容统一为 \r\n，保证粘贴到外部程序时换行正常
        var selected = raw.Substring(start, length);
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(NotepadTextNewlineHelper.Normalize(selected).Replace("\n", "\r\n"));
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

        var pipeline = NotepadSmartEditPipeline.ApplyKey(NotepadEditCommand.Backspace, raw, start, length);
        if (pipeline.Handled)
            ApplyProgrammaticEdit(pipeline.DisplayText, pipeline.DisplaySelectionStart, pipeline.DisplaySelectionLength);
    }

    public void ResetUndoHistory()
    {
        _undo.Clear();
    }

    public void ApplyProgrammaticEdit(string text, int selectionStart, int selectionLength)
    {
        _suppressUndoCapture = true;
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Max(0, selectionLength);
        try
        {
            _undo.Push(CreateSnapshot());
            Text = text;
            Select(start, length);
            EditStateChanged?.Invoke(text);
        }
        finally
        {
            _suppressUndoCapture = false;
        }

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
        {
            // 控件可能改写换行符表示，按规范化文本比较，避免校正被跳过
            if (NotepadTextNewlineHelper.Normalize(Text) != NotepadTextNewlineHelper.Normalize(text))
                return;

            Select(Math.Clamp(start, 0, Text.Length), length);
        });
    }

    public void ApplyEditorTheme()
    {
        var source = Application.Current.Resources["TextFillColorSecondaryBrush"] as SolidColorBrush;
        if (source == null)
            return;

        var brush = new SolidColorBrush(source.Color);
        Foreground = brush;
        Resources["TextControlForeground"] = brush;
        Resources["TextControlForegroundFocused"] = brush;
        Resources["TextControlForegroundPointerOver"] = brush;
        Resources["TextControlForegroundDisabled"] = brush;
        Resources["TextControlPlaceholderForeground"] =
            Application.Current.Resources["TextFillColorTertiaryBrush"] ?? brush;
    }

    /// <summary>反缩进已到行首时，编辑区水平抖动提示。</summary>
    public void ShowIndentLimitShake()
    {
        _indentLimitShakeStoryboard?.Stop();
        _shakeTransform.X = 0;

        var anim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(260),
            EnableDependentAnimation = true
        };
        anim.KeyFrames.Add(Key(0, 0));
        anim.KeyFrames.Add(Key(35, -5));
        anim.KeyFrames.Add(Key(70, 4));
        anim.KeyFrames.Add(Key(105, -3));
        anim.KeyFrames.Add(Key(140, 2));
        anim.KeyFrames.Add(Key(260, 0));

        var storyboard = new Storyboard();
        Storyboard.SetTarget(anim, _shakeTransform);
        Storyboard.SetTargetProperty(anim, "X");
        storyboard.Children.Add(anim);
        storyboard.Completed += OnIndentLimitShakeCompleted;
        _indentLimitShakeStoryboard = storyboard;
        storyboard.Begin();
    }

    private void OnIndentLimitShakeCompleted(object? sender, object e)
    {
        if (sender is Storyboard storyboard)
            storyboard.Completed -= OnIndentLimitShakeCompleted;
        _shakeTransform.X = 0;
        _indentLimitShakeStoryboard = null;
    }

    private static LinearDoubleKeyFrame Key(double milliseconds, double value) =>
        new() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)), Value = value };

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (TryHandleUndoRedo(e))
        {
            if (e.Handled)
                return;
        }

        if (NotepadSmartEditDebug.Enabled && IsSmartEditKey(e.Key))
            LogKeyPhase("OnKeyDown:before", e);

        SmartKeyDown?.Invoke(e);

        if (NotepadSmartEditDebug.Enabled && IsSmartEditKey(e.Key))
            LogKeyPhase("OnKeyDown:after", e);

        // 编辑模式下 Tab/Shift+Tab 用于缩进；无法再缩进时仍须消费按键，避免焦点被移出输入框。
        if (!e.Handled && !IsReadOnly && e.Key == VirtualKey.Tab)
            e.Handled = true;

        // 文档开头正常 Backspace 无内容可删时抖动（智能编辑未拦截的兜底）。
        if (!e.Handled && !IsReadOnly && e.Key == VirtualKey.Back
            && SelectionStart == 0 && SelectionLength == 0)
        {
            ShowIndentLimitShake();
            e.Handled = true;
        }

        if (e.Handled)
            return;
        base.OnKeyDown(e);
    }

    private bool TryHandleUndoRedo(KeyRoutedEventArgs e)
    {
        if (!IsCtrlPressed())
            return false;

        var shift = IsShiftPressed();

        if (e.Key == VirtualKey.Z && !shift)
        {
            if (TryUndo())
                e.Handled = true;
            return true;
        }

        if (e.Key == VirtualKey.Y || (e.Key == VirtualKey.Z && shift))
        {
            if (TryRedo())
                e.Handled = true;
            return true;
        }

        return false;
    }

    private bool TryUndo()
    {
        if (!_undo.TryUndo(CreateSnapshot(), out var target))
            return false;

        RestoreSnapshot(target);
        return true;
    }

    private bool TryRedo()
    {
        if (!_undo.TryRedo(CreateSnapshot(), out var target))
            return false;

        RestoreSnapshot(target);
        return true;
    }

    private void RestoreSnapshot(NotepadEditSnapshot snapshot)
    {
        _suppressUndoCapture = true;
        try
        {
            Text = snapshot.Text;
            SelectionStart = snapshot.SelectionStart;
            SelectionLength = snapshot.SelectionLength;
            EditStateChanged?.Invoke(snapshot.Text);
        }
        finally
        {
            _suppressUndoCapture = false;
        }
    }

    private void OnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (_suppressUndoCapture || IsReadOnly)
            return;

        _undo.Push(CreateSnapshot());
    }

    private NotepadEditSnapshot CreateSnapshot() =>
        new(Text ?? string.Empty, SelectionStart, SelectionLength);

    private void LogKeyPhase(string phase, KeyRoutedEventArgs e)
    {
        var raw = Text ?? string.Empty;
        var norm = NotepadTextNewlineHelper.Normalize(raw);
        var normStart = NotepadTextNewlineHelper.RawIndexToNormalized(raw, SelectionStart);
        NotepadSmartEditDebug.LogKeyDown(
            "TextBox",
            phase,
            new NotepadSmartEditDebug.KeyInfo
            {
                Key = e.Key.ToString(),
                Handled = e.Handled,
                AcceptsReturn = AcceptsReturn,
                IsReadOnly = IsReadOnly,
                RawSelectionStart = SelectionStart,
                RawSelectionEnd = SelectionStart + SelectionLength,
                NormSelectionStart = normStart,
                NormSelectionEnd = NotepadTextNewlineHelper.RawIndexToNormalized(raw, SelectionStart + SelectionLength),
                TextLength = raw.Length,
                CurrentLine = NotepadSmartEditDebug.GetCurrentLine(norm, normStart)
            });
    }

    private static bool IsSmartEditKey(VirtualKey key) =>
        key is VirtualKey.Enter or VirtualKey.Back or VirtualKey.Tab;

    private static bool IsCtrlPressed() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);

    private static bool IsShiftPressed() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
}
