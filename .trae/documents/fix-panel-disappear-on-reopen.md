# 修复第二次打开面板时界面丢失的问题

## 问题描述
已完成列表、提醒我、截止日期等面板，打开→关闭→再打开时，界面内容消失。

## 根因分析
`AnimateCollapse` 方法使用了 `FadeOutThemeAnimation`，这个动画会将元素的 **Opacity 渐变到 0**。动画结束后，元素的 Opacity 保持在 0，同时 Visibility 被设为 Collapsed。

问题在于：当再次设置 `Visibility = Visible` 时，Opacity 仍然是 0，导致元素虽然可见但完全透明，看起来就像"界面丢失"了。

```
第一次打开: Visibility=Visible, Opacity=1 → 正常显示
关闭: FadeOutThemeAnimation → Opacity=0, Visibility=Collapsed
第二次打开: Visibility=Visible, Opacity=0 → 透明不可见！
```

## 受影响的位置
`AnimateCollapse` 被以下场景调用：
1. **已完成列表** (`CompletedTasksList`) - `ToggleCompleted_Click` 关闭时
2. **截止日期日历** (`DetailCalendarView`) - `ShowDueDatePicker_Click` 和 `DetailCalendarView_SelectedDatesChanged` 关闭时
3. **提醒设置面板** (`ReminderSettingsPanel`) - `CancelReminderSettings_Click` 和 `ConfirmReminderSettings_Click` 关闭时

## 修复方案
两步修复，确保健壮性：

### 步骤 1：修复 `AnimateCollapse` 方法
在动画完成回调中，元素已被设为 Collapsed 后，将 Opacity 重置为 1。这样下次显示时 Opacity 就是正确的。

**文件**：`MainWindow.xaml.cs` 第 769-794 行

```csharp
private void AnimateCollapse(FrameworkElement element, Action? onComplete = null)
{
    var storyboard = new Storyboard();

    var fadeOut = new FadeOutThemeAnimation();
    Storyboard.SetTarget(fadeOut, element);
    storyboard.Children.Add(fadeOut);

    var visibilityAnimation = new ObjectAnimationUsingKeyFrames();
    var keyFrame = new DiscreteObjectKeyFrame
    {
        Value = Visibility.Collapsed,
        KeyTime = TimeSpan.FromSeconds(0.3)
    };
    visibilityAnimation.KeyFrames.Add(keyFrame);
    Storyboard.SetTarget(visibilityAnimation, element);
    Storyboard.SetTargetProperty(visibilityAnimation, "Visibility");
    storyboard.Children.Add(visibilityAnimation);

    storyboard.Completed += (s, e) =>
    {
        element.Opacity = 1;  // 重置透明度，确保下次显示时可见
        onComplete?.Invoke();
    };

    storyboard.Begin();
}
```

### 步骤 2：在显示元素时也重置 Opacity（防御性修复）
防止动画被中断（如用户快速点击）导致 Completed 未触发、Opacity 未重置的情况。

**文件**：`MainWindow.xaml.cs`

需要修改 3 处：

1. `ToggleCompleted_Click`（第 758-760 行）- 显示已完成列表时：
```csharp
CompletedTasksList.Opacity = 1;
CompletedTasksList.Visibility = Visibility.Visible;
```

2. `ShowDueDatePicker_Click`（第 959 行）- 显示截止日期日历时：
```csharp
DetailCalendarView.Opacity = 1;
DetailCalendarView.Visibility = Visibility.Visible;
```

3. `ShowReminderDialog_Click`（第 1014 行）- 显示提醒设置面板时：
```csharp
ReminderSettingsPanel.Opacity = 1;
ReminderSettingsPanel.Visibility = Visibility.Visible;
```

### 步骤 3：编译验证
- 编译项目确保无错误
