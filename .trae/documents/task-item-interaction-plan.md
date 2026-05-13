# 任务项交互优化计划

## 目标
优化任务项交互：只有左键切换抽屉，右键不响应抽屉，添加 hover 和选中视觉效果。

## 当前状态分析
- `PointerPressed` 事件响应所有鼠标按键，包括右键
- 任务项 Border 没有 hover 效果
- 任务项没有选中状态的视觉区分
- 右键点击也会触发抽屉切换

## 目标行为
1. 只有左键点击切换抽屉
2. 右键点击只显示菜单，不切换抽屉
3. 鼠标悬停时显示视觉反馈（背景色变化）
4. 选中状态显示视觉反馈（边框或背景色变化）

## 实现方案

### 1. 修改 PointerPressed 只响应左键
在 `TaskItem_PointerPressed` 中检查 `e.Pointer.PointerDeviceType` 和 `e.GetCurrentPoint().Properties.IsLeftButtonPressed`

### 2. 添加 hover 效果
使用 `PointerEntered` 和 `PointerExited` 事件，或使用 VisualStateManager

### 3. 添加选中效果
- 为 Border 添加 `x:Name` 或使用数据绑定
- 在选中时更改边框颜色或背景色

### XAML 修改
```xml
<Border BorderBrush="#333" 
        BorderThickness="1" 
        CornerRadius="8" 
        Height="60" 
        Margin="0,0,0,10" 
        Background="#1e1e1e"
        PointerPressed="TaskItem_PointerPressed"
        PointerEntered="TaskItem_PointerEntered"
        PointerExited="TaskItem_PointerExited"
        RightTapped="TaskItem_RightTapped">
    <Border.Resources>
        <SolidColorBrush x:Key="TaskItemHoverBrush" Color="#252525"/>
        <SolidColorBrush x:Key="TaskItemSelectedBrush" Color="#2a2a2a"/>
    </Border.Resources>
</Border>
```

### 代码后台修改
- `TaskItem_PointerPressed`: 检查是否为左键
- `TaskItem_PointerEntered`: 设置 hover 背景
- `TaskItem_PointerExited`: 恢复默认背景
- 更新选中状态时更改边框颜色

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `MainWindow.xaml` | 修改 | 添加 hover/选中效果事件 |
| `MainWindow.xaml.cs` | 修改 | 实现交互逻辑 |

## 验证步骤
1. 构建项目无错误
2. 左键点击切换抽屉
3. 右键点击只显示菜单，不切换抽屉
4. 鼠标悬停显示视觉反馈
5. 选中的任务项有视觉区分
