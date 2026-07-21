using System;
using System.Collections.Generic;
using Memo.NotepadEdit;
using Memo.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;
using Windows.UI.Core;

namespace Memo;

/// <summary>
/// 记事本编辑框：主题色、多步撤销、智能续行按键入口、链接标题化。
/// Text 始终是显示文本（链接以 url[标题] 呈现），StorageText 同为 url[标题] 存储格式。
/// </summary>
public sealed class NotepadEditTextBox : TextBox
{
    private readonly NotepadEditUndoStack _undo = new();
    private readonly TranslateTransform _shakeTransform = new();
    private bool _suppressUndoCapture;
    private Storyboard? _indentLimitShakeStoryboard;

    private List<(string title, string url, int displayIndex, int displayLength)> _links = new();
    private string _previousTextForLinks = "";
    private bool _suppressLinkTracking;

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
        CopyingToClipboard += OnCopyingToClipboard;
        Paste += OnPasteWithLinks;
        RightTapped += OnRightTappedLinkMenu;
        Tapped += OnTappedOpenLinkInPreview;
    }

    // ===================== 链接标题化 =====================

    /// <summary>含 url[标题] 的存储格式文本。</summary>
    public string StorageText => LinkMarkdownHelper.Reconstruct(Text ?? string.Empty, _links);

    /// <summary>当前链接映射（预览层渲染用）。</summary>
    public IReadOnlyList<(string title, string url, int displayIndex, int displayLength)> Links => _links;

    public bool HasLinks => _links.Count > 0;

    /// <summary>加载存储格式文本：链接转为纯标题显示并登记映射。</summary>
    public void SetStorageText(string? raw)
    {
        // TextBox 内部换行为 '\r'，先统一再解析，保证映射索引一致
        var normalized = (raw ?? string.Empty).Replace("\r\n", "\r").Replace('\n', '\r');
        var (display, links) = LinkMarkdownHelper.Strip(normalized);

        _suppressLinkTracking = true;
        _suppressUndoCapture = true;
        try
        {
            Text = display;
        }
        finally
        {
            _suppressUndoCapture = false;
            _suppressLinkTracking = false;
        }
        _links = links;
        _previousTextForLinks = display;
    }

    /// <summary>
    /// 文本变化时按单一变更区间平移链接映射，标题被改动的链接降级为纯文本。
    /// 在 BeforeTextChanging 中同步执行，保证 Text 更新后 StorageText 立即可靠。
    /// </summary>
    private void TrackLinksForNewText(string current)
    {
        if (_suppressLinkTracking)
        {
            _previousTextForLinks = current;
            return;
        }

        if (_links.Count > 0 && current != _previousTextForLinks)
        {
            var (start, removed, inserted) = LinkMarkdownHelper.ComputeTextDiff(_previousTextForLinks, current);
            LinkMarkdownHelper.ShiftLinksForChange(_links, start, removed, inserted);
            LinkMarkdownHelper.ResyncLinks(_links, current);
        }
        _previousTextForLinks = current;
    }

    /// <summary>程序化替换文本与链接映射（进撤销栈并通知内容同步）。</summary>
    private void ApplyLinkEdit(
        string displayText,
        List<(string title, string url, int displayIndex, int displayLength)> links,
        int caret)
    {
        _suppressLinkTracking = true;
        try
        {
            _links = links;
            _previousTextForLinks = displayText;
            ApplyProgrammaticEdit(displayText, Math.Clamp(caret, 0, displayText.Length), 0);
        }
        finally
        {
            _suppressLinkTracking = false;
        }
    }

    /// <summary>文本粘贴统一手动处理：裸 URL 转为 url[] 并把光标移入括号，其余按纯文本插入。</summary>
    private async void OnPasteWithLinks(object sender, TextControlPasteEventArgs e)
    {
        if (IsReadOnly)
            return;

        Windows.ApplicationModel.DataTransfer.DataPackageView view;
        try
        {
            view = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch
        {
            return;
        }
        if (!view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            return;

        e.Handled = true;
        string pasted;
        try
        {
            pasted = await view.GetTextAsync();
        }
        catch
        {
            return;
        }
        if (string.IsNullOrEmpty(pasted))
            return;

        var selStart = SelectionStart;
        var selLen = SelectionLength;

        if (UrlTitleResolver.IsBareUrl(pasted))
        {
            var (text, links, caret) = LinkMarkdownHelper.InsertBareUrl(
                Text ?? "", _links, selStart, selLen, pasted.Trim());
            ApplyLinkEdit(text, links, caret);
            return;
        }

        pasted = pasted.Replace("\r\n", "\r").Replace('\n', '\r');
        var (plainText, plainLinks, plainCaret) = LinkMarkdownHelper.ReplaceRange(
            Text ?? "", _links, selStart, selLen, pasted);
        ApplyLinkEdit(plainText, plainLinks, plainCaret);
    }

    private void OnCopyingToClipboard(TextBox sender, TextControlCopyingToClipboardEventArgs args)
    {
        var clip = LinkMarkdownHelper.BuildClipboardText(Text ?? "", _links, SelectionStart, SelectionLength);
        if (clip == null)
            return;
        args.Handled = true;
        SetClipboardText(clip);
    }

    /// <summary>右键点在链接标题上时弹出打开/复制菜单。</summary>
    private void OnRightTappedLinkMenu(object sender, RightTappedRoutedEventArgs e)
    {
        var link = LinkMarkdownHelper.GetLinkAtPosition(_links, SelectionStart);
        if (link == null)
            return;

        var url = link.Value.url;
        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "打开链接", Icon = new FontIcon { Glyph = "\uE71B" } };
        openItem.Click += (_, _) => OpenUrl(url);
        menu.Items.Add(openItem);
        var copyItem = new MenuFlyoutItem { Text = "复制链接", Icon = new SymbolIcon(Symbol.Copy) };
        copyItem.Click += (_, _) => SetClipboardText(url);
        menu.Items.Add(copyItem);

        menu.ShowAt(this, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>预览（只读）模式下单击链接标题直接打开。</summary>
    private void OnTappedOpenLinkInPreview(object sender, TappedRoutedEventArgs e)
    {
        if (!IsReadOnly)
            return;
        var link = LinkMarkdownHelper.GetLinkAtPosition(_links, SelectionStart);
        if (link == null)
            return;
        OpenUrl(link.Value.url);
        e.Handled = true;
    }

    private static async void OpenUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenUrl error: {ex.Message}");
        }
    }

    private static void SetClipboardText(string text)
    {
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            // 剪贴板内容统一为 \r\n，保证粘贴到外部程序时换行正常
            package.SetText(NotepadTextNewlineHelper.Normalize(text).Replace("\n", "\r\n"));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard error: {ex.Message}");
        }
    }

    /// <summary>
    /// 接管剪切（Ctrl+X 与右键菜单），让删除走智能编辑管线，触发有序列表重排；
    /// 选区含链接时剪贴板内容还原为 URL/Markdown。
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

        var clip = LinkMarkdownHelper.BuildClipboardText(raw, _links, start, length)
            ?? raw.Substring(start, length);
        SetClipboardText(clip);

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
        TrackLinksForNewText(args.NewText ?? string.Empty);

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
