# 任务抽屉与主窗口同步更新计划

## 问题分析

### 问题1：抽屉编辑后主窗口不同步
**原因**：
- `TaskItem.Title` 和 `TaskItem.DueDate` 是自动属性，没有实现 `INotifyPropertyChanged`
- 当在抽屉中修改这些属性时，虽然数据库更新了，但 UI 绑定的数据没有收到通知
- 需要将 `Title` 和 `DueDate` 改为带 `OnPropertyChanged` 的属性

### 问题2：任务项缺少创建时间显示
**当前状态**：
- 任务项模板只显示 `DueDateDisplay`（截止时间）
- 需要改为两排小字：
  - 上排：创建时间 (`CreatedAt`)
  - 下排：截止时间 (`DueDate`)

## 实现步骤

### 步骤 1：修改 TaskItem 模型 - Title 属性
**文件**：`TaskItem.cs`
**位置**：第 16 行

**修改内容**：
```csharp
// 原代码
public string Title { get; set; } = "";

// 修改为
private string _title = "";
public string Title
{
    get => _title;
    set { _title = value; OnPropertyChanged(); }
```

### 步骤 2：修改 TaskItem 模型 - DueDate 属性
**文件**：`TaskItem.cs`
**位置**：第 18 行

**修改内容**：
```csharp
// 原代码
public DateTime? DueDate { get; set; }

// 修改为
private DateTime? _dueDate;
public DateTime? DueDate
{
    get => _dueDate;
    set { _dueDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(DueDateDisplay)); }
```

### 步骤 3：修改任务项模板 - 未完成任务
**文件**：`MainWindow.xaml`
**位置**：第 196-206 行

**修改内容**：
- 将单列的截止时间改为两排小字
- 上排显示创建时间，下排显示截止时间
- 需要调整 Grid 布局

### 步骤 4：修改任务项模板 - 已完成任务
**文件**：`MainWindow.xaml`
**位置**：第 255-266 行

**修改内容**：
- 与未完成任务相同的布局修改

## 修改清单

| 文件 | 位置 | 修改内容 |
|------|------|----------|
| `TaskItem.cs` | 第 16 行 | Title 属性添加 OnPropertyChanged |
| `TaskItem.cs` | 第 18 行 | DueDate 属性添加 OnPropertyChanged |
| `MainWindow.xaml` | 第 196-206 行 | 未完成任务项显示创建时间和截止时间 |
| `MainWindow.xaml` | 第 255-266 行 | 已完成任务项显示创建时间和截止时间 |
