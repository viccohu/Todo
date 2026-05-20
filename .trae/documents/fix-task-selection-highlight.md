# 优化任务选中高亮 UI - 实施计划

## 问题描述
点击不同任务项时，多条任务同时高亮，视觉没有立即更新。之前选中的项只有在鼠标 hover 过后才会变回正常非高亮状态。

## 根因分析
当前实现使用 `FindVisualChildren<Border>` 遍历可视化树来手动更新背景色，这种方式存在以下问题：

1. **可视化树遍历不可靠**：`FindVisualChildren<Border>` 会找到所有 Border（包括 CheckBox 模板内部的 Border），虽然通过 `DataContext is TaskItem` 过滤，但内部 Border 也继承了 TaskItem 的 DataContext，导致干扰
2. **事件时序冲突**：`PointerEntered`/`PointerExited` 手动设置背景与 `UpdateBordersBackground` 遍历设置背景互相冲突
3. **缺少即时响应**：`IsSelected` 属性已更新，但没有机制将属性变化自动反映到 UI 上

## 解决方案
用 **PropertyChanged 事件订阅** 替代可视化树遍历。每个 Border 在 Loaded 时订阅其 TaskItem 的 PropertyChanged 事件，当 IsSelected 变化时立即更新背景色。

### 优势
- 不依赖可视化树遍历，更可靠
- IsSelected 变化时 UI 立即响应
- 代码更简洁，移除 `UpdateBordersBackground`、`FindVisualChildren`、`FindVisualChild` 等方法

## 实施步骤

### 步骤 1：在 MainWindow.xaml 中为任务项 Border 添加 Loaded/Unloaded 事件
**文件**：`MainWindow.xaml`

在 `TasksList` 和 `CompletedTasksList` 的 DataTemplate 中，为外层 Border 添加 `Loaded` 和 `Unloaded` 事件：

```xml
<Border ... Loaded="TaskBorder_Loaded" Unloaded="TaskBorder_Unloaded" ...>
```

需要修改两处：
- 第 179 行附近（TasksList 的 Border）
- 第 245 行附近（CompletedTasksList 的 Border）

### 步骤 2：添加事件订阅跟踪字典和核心方法
**文件**：`MainWindow.xaml.cs`

1. 添加字段：
```csharp
private readonly Dictionary<TaskItem, (Border border, PropertyChangedEventHandler handler)> _borderSubscriptions = new();
```

2. 添加 `TaskBorder_Loaded` 方法：Border 加载时订阅 TaskItem.PropertyChanged，当 IsSelected 变化时立即更新背景

3. 添加 `TaskBorder_Unloaded` 方法：Border 卸载时取消订阅，防止内存泄漏

4. 添加 `UpdateBorderBackground` 辅助方法：根据 IsSelected 状态设置正确的背景色
   - 选中：`#2a2a2a`
   - 未选中：`#1e1e1e`

### 步骤 3：修改 PointerEntered/PointerExited 事件处理
**文件**：`MainWindow.xaml.cs`

- `TaskItem_PointerEntered`：改为检查 `task.IsSelected`（而非 `_selectedTask == task`），未选中时设置 hover 背景 `#252525`
- `TaskItem_PointerExited`：改为调用 `UpdateBorderBackground(border, task)` 恢复正确状态

### 步骤 4：简化 UpdateTaskItemSelection 方法
**文件**：`MainWindow.xaml.cs`

移除 `UpdateBordersBackground` 调用，只保留 `IsSelected` 属性更新。PropertyChanged 订阅会自动处理 UI 更新：

```csharp
private void UpdateTaskItemSelection(TaskItem? selectedTask)
{
    foreach (var item in Tasks) item.IsSelected = (item == selectedTask);
    foreach (var item in CompletedTasks) item.IsSelected = (item == selectedTask);
}
```

### 步骤 5：修改 CloseDrawer 方法
**文件**：`MainWindow.xaml.cs`

在 `CloseDrawer` 中，将 `UpdateBordersBackground` 调用替换为直接设置 `_selectedTask.IsSelected = false`（在动画回调之前执行，确保视觉立即更新）：

```csharp
private void CloseDrawer()
{
    if (_selectedTask != null) _selectedTask.IsSelected = false;
    AnimateCollapse(DetailDrawer, () =>
    {
        _isDrawerOpen = false;
        _selectedTask = null;
    });
}
```

### 步骤 6：修改其他使用 UpdateBordersBackground 的地方
**文件**：`MainWindow.xaml.cs`

- `CompleteTaskInUi`：移除 `UpdateBordersBackground` 调用，改用 `IsSelected = false`
- `TaskCheckBox_Click`：移除 `UpdateBordersBackground` 调用，改用 `IsSelected = false`

### 步骤 7：清理不再需要的代码
**文件**：`MainWindow.xaml.cs`

移除以下方法：
- `UpdateBordersBackground`
- `FindVisualChild<T>`
- `FindVisualChildren<T>`

### 步骤 8：验证
- 编译项目确保无错误
- 运行应用测试选中行为：点击不同任务项，确认只有当前选中项高亮
- 测试 hover 效果：鼠标悬停未选中项显示 hover 色，移开后恢复正常
- 测试关闭抽屉：选中项高亮正确移除
- 测试完成任务：选中项高亮正确移除
