# WinUI3 动画效果实现计划

## 官方推荐实践

根据 [Microsoft Learn 官方文档](https://learn.microsoft.com/zh-cn/windows/apps/design/motion/xaml-animation)，WinUI3 推荐使用 **ThemeTransition（主题过渡）** 动画，这是动画库提供的内置动画，优势：
- 符合 Windows 动画指南
- 快速流畅的过渡
- 声明式 XAML，无需手动编写 Storyboard

### 关键 API 映射

| 场景 | 官方推荐 API | 说明 |
|------|-------------|------|
| 列表项添加/删除 | `AddDeleteThemeTransition` | 添加时先腾空间再插入，删除时移除后重排 |
| 列表项重排 | `RepositionThemeTransition` | 项目位置变化时平滑移动 |
| 内容首次出现 | `EntranceThemeTransition` | 子元素依次滑入视图 |
| 边缘面板滑入/滑出 | `PaneThemeTransition` | 较大边缘 UI（如抽屉面板） |
| 小边缘 UI | `EdgeUIThemeTransition` | 较小边缘 UI |
| 内容切换 | `ContentThemeTransition` | 内容改变时的过渡 |
| 淡入淡出 | `FadeInThemeAnimation` / `FadeOutThemeAnimation` | 显示/隐藏时的渐变 |

## 实现步骤

### 步骤1：任务列表 - 添加/删除/完成动画

**文件**：`MainWindow.xaml`

**1.1 未完成任务列表 `TasksList`（ItemsControl）**
在 `TasksList` 的 `ItemsControl.ItemContainerTransitions` 中添加：
```xml
<ItemsControl.ItemContainerTransitions>
    <TransitionCollection>
        <AddDeleteThemeTransition/>
        <RepositionThemeTransition/>
        <EntranceThemeTransition/>
    </TransitionCollection>
</ItemsControl.ItemContainerTransitions>
```

**1.2 已完成任务列表 `CompletedTasksList`（ItemsControl）**
同样添加 `AddDeleteThemeTransition` 和 `RepositionThemeTransition`

**1.3 任务完成时的动画**
在 `TaskCheckBox_Click` 中，当任务从未完成移到已完成时，动画由 `AddDeleteThemeTransition` 自动处理：
- 从 TasksList 删除 → 播放删除动画
- 添加到 CompletedTasksList → 播放添加动画

### 步骤2：已完成列表的展开/折叠动画

**文件**：`MainWindow.xaml` + `MainWindow.xaml.cs`

**2.1 XAML 修改**
将 `CompletedTasksList` 的 `Visibility="Collapsed"` 改为始终 Visible，用 `Height` + `ClipToBounds` 或 `Opacity` 动画控制展开/折叠效果。

更简单的方案：为 `CompletedTasksList` 添加 `Transitions`，并在其外层包一个容器，通过 `Visibility` 切换时自动触发 `EntranceThemeTransition`。

**2.2 代码修改**
在 `ToggleCompleted_Click` 中：
- 展开：设置 `Visibility=Visible`，由 `EntranceThemeTransition` 自动播放滑入动画
- 折叠：使用 `FadeOutThemeAnimation` Storyboard 先播放淡出动画，完成后设置 `Visibility=Collapsed`

### 步骤3：任务抽屉的展开/关闭动画

**文件**：`MainWindow.xaml` + `MainWindow.xaml.cs`

**3.1 XAML 修改**
为 `DetailDrawer` 添加 `PaneThemeTransition`（官方推荐用于较大边缘 UI 面板）：
```xml
<Grid x:Name="DetailDrawer" ...>
    <Grid.Transitions>
        <TransitionCollection>
            <PaneThemeTransition Edge="Right"/>
        </TransitionCollection>
    </Grid.Transitions>
</Grid>
```

**3.2 代码修改**
- `ShowDrawer()`：设置 `DetailDrawer.Visibility = Visible`，`PaneThemeTransition` 自动播放从右侧滑入动画
- `CloseDrawer()`：使用 Storyboard 播放 `FadeOutThemeAnimation`，完成后设置 `Collapsed`

### 步骤4：子任务的添加/删除动画

**文件**：`MainWindow.xaml`

**4.1 XAML 修改**
为 `SubTasksList` 的 `ItemsControl.ItemContainerTransitions` 添加：
```xml
<ItemsControl.ItemContainerTransitions>
    <TransitionCollection>
        <AddDeleteThemeTransition/>
        <RepositionThemeTransition/>
    </TransitionCollection>
</ItemsControl.ItemContainerTransitions>
```

**4.2 代码修改**
`DeleteSubTask_Click` 中移除 `SubTasksList.ItemsSource = null; SubTasksList.ItemsSource = ...` 的重置方式，改为直接操作 `ObservableCollection`，这样 `AddDeleteThemeTransition` 才能正确触发动画。

### 步骤5：提醒设置/截止日期面板的展开关闭动画

**文件**：`MainWindow.xaml` + `MainWindow.xaml.cs`

**5.1 截止日期 CalendarView 展开/关闭**
为 `DetailCalendarView` 添加 `ContentThemeTransition`：
```xml
<CalendarView x:Name="DetailCalendarView" ...>
    <CalendarView.Transitions>
        <TransitionCollection>
            <ContentThemeTransition/>
        </TransitionCollection>
    </CalendarView.Transitions>
</CalendarView>
```

在 `ShowDueDatePicker_Click` 中切换 Visibility 时自动播放动画。

**5.2 提醒设置面板展开/关闭**
为 `ReminderSettingsPanel` 添加 `ContentThemeTransition`：
```xml
<Border x:Name="ReminderSettingsPanel" ...>
    <Border.Transitions>
        <TransitionCollection>
            <ContentThemeTransition/>
        </TransitionCollection>
    </Border.Transitions>
</Border>
```

**5.3 关闭动画实现**
对于关闭（Collapsed）场景，ThemeTransition 不直接支持。需要使用 Storyboard：
- 创建辅助方法 `AnimateCollapse(FrameworkElement element, Action onComplete)`
- 使用 `FadeOutThemeAnimation` + `ObjectAnimationUsingKeyFrames`（Visibility→Collapsed）
- 动画完成后执行回调设置 `Visibility=Collapsed`

### 步骤6：辅助动画方法

**文件**：`MainWindow.xaml.cs`

添加通用动画辅助方法：
```csharp
private void AnimateCollapse(FrameworkElement element)
{
    var storyboard = new Storyboard();
    
    var fadeOut = new FadeOutThemeAnimation();
    Storyboard.SetTarget(fadeOut, element);
    storyboard.Children.Add(fadeOut);
    
    var visibilityAnimation = new ObjectAnimationUsingKeyFrames();
    var keyFrame = new DiscreteObjectKeyFrame { Value = Visibility.Collapsed, KeyTime = TimeSpan.FromSeconds(0.3) };
    visibilityAnimation.KeyFrames.Add(keyFrame);
    Storyboard.SetTarget(visibilityAnimation, element);
    Storyboard.SetTargetProperty(visibilityAnimation, "Visibility");
    storyboard.Children.Add(visibilityAnimation);
    
    storyboard.Begin();
}

private void AnimateShow(FrameworkElement element)
{
    element.Visibility = Visibility.Visible;
    // EntranceThemeTransition 会自动播放
}
```

## 修改文件清单

1. **MainWindow.xaml** - 添加所有 TransitionCollection 声明
2. **MainWindow.xaml.cs** - 修改展开/折叠逻辑，添加动画辅助方法
