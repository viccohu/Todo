# 任务抽屉编辑框按钮修复计划

## 问题分析

### 当前状态
1. **主任务编辑框**：已有自定义 TextBox.Style 模板，但用户反馈仍有清除按钮
2. **子任务编辑框**：没有自定义样式，WinUI3 默认会显示清除按钮
3. **子任务删除按钮**：按钮已可见，但点击可能崩溃

### 崩溃原因分析
`DeleteSubTask_Click` 方法（第778-787行）：
```csharp
if (sender is Button btn && btn.DataContext is SubTask subTask && _selectedTask != null)
```
- 如果 DataContext 绑定失败，条件不满足，不会执行删除
- 可能是按钮在 DataTemplate 中的 DataContext 绑定问题

### WinUI3 TextBox 清除按钮问题
WinUI3 的 TextBox 默认有内置清除按钮，需要通过设置 `IsEnabled="False"` 或自定义模板来禁用。

## 实现步骤

### 步骤 1：修复主任务编辑框
**文件**：`MainWindow.xaml`
**位置**：第 373-412 行

**修改方案**：
- 确保自定义 TextBox 模板完全移除清除按钮
- 或使用更简单的方法：添加 `ClearButtonEnabled="False"` 属性（如果 WinUI3 支持）

### 步骤 2：修复子任务编辑框
**文件**：`MainWindow.xaml`
**位置**：第 432-451 行

**修改方案**：
- 为子任务的 TextBox 添加自定义样式，移除清除按钮
- 与主任务编辑框使用相同的处理方式

### 步骤 3：验证子任务删除按钮
**文件**：`MainWindow.xaml.cs`
**位置**：第 778-787 行

**检查项**：
- 确保 DataContext 绑定正确
- 添加空值检查防止崩溃

## 修改清单

| 文件 | 位置 | 修改内容 |
|------|------|----------|
| `MainWindow.xaml` | 第 373-412 行 | 确保主任务 TextBox 无清除按钮 |
| `MainWindow.xaml` | 第 432-451 行 | 子任务 TextBox 添加样式移除清除按钮 |
| `MainWindow.xaml.cs` | 第 778-787 行 | 添加异常处理防止崩溃 |
