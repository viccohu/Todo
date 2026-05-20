# 记事本页面实现计划

## 概述
在现有 Todo 应用中实现记事本页面，包含 TabView 多标签、轻量 Markdown 编辑、工具栏和自动保存功能。

## 设计决策（基于用户描述的合理推断）
- **编辑模式**：纯文本 Markdown 编辑 + 工具栏语法插入（最轻量、最可靠）
- **关闭行为**：关闭标签时永久删除内容（除非已另存为文件）
- **自动编号**：工具栏插入 Markdown 有序列表语法（1. 2. 3.）
- **标签持久化**：应用重启后恢复未关闭的标签（自动保存的体现）

## 页面布局

```
┌───────────────────────────────────────────────────┐
│  [标签1 ×] [标签2 ×] [+]                         │  TabView 标签栏
│ ────────────────────────────────────────────────── │
│  [打开][另存为]  |  [B] [I] [U] [S] [1.]         │  工具栏
│ ────────────────────────────────────────────────── │
│                                                   │
│              Markdown 文本编辑区                   │  内容区
│                                                   │
│                                                   │
└───────────────────────────────────────────────────┘
```

## 实现步骤

### 步骤 1：创建数据模型 `NotepadTab`
**文件**: `Models/NotepadTab.cs`

```csharp
public class NotepadTab : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Title { get; set; }           // 标签标题
    public string Content { get; set; }         // Markdown 内容
    public string? FilePath { get; set; }       // 关联的文件路径（如果是从文件打开的）
    public bool IsModified { get; set; }        // 是否有未保存的修改
    public int Order { get; set; }              // 标签顺序
    public DateTime CreatedAt { get; set; }     // 创建时间
    public DateTime UpdatedAt { get; set; }     // 最后更新时间
}
```

### 步骤 2：扩展数据库服务
**文件**: `Services/DatabaseService.cs`

新增 `NotepadTabs` 表：
```sql
CREATE TABLE IF NOT EXISTS NotepadTabs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    Content TEXT NOT NULL DEFAULT '',
    FilePath TEXT,
    IsModified INTEGER NOT NULL DEFAULT 0,
    "Order" INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
```

新增方法：
- `GetNotepadTabs()` - 获取所有标签（按 Order 排序）
- `AddNotepadTab(title)` - 新增标签
- `UpdateNotepadTabContent(id, content)` - 更新标签内容
- `UpdateNotepadTabTitle(id, title)` - 更新标签标题
- `UpdateNotepadTabFilePath(id, filePath)` - 更新关联文件路径
- `DeleteNotepadTab(id)` - 删除标签（关闭标签时调用）
- `ReorderNotepadTabs()` - 重排标签顺序

### 步骤 3：替换 MainWindow 中的记事本占位区域
**文件**: `MainWindow.xaml`

将现有的 `NotepadPlaceholder`（Grid，Visibility=Collapsed）替换为完整的记事本界面：

```xml
<!-- 记事本页面 -->
<Grid x:Name="NotepadContent" Grid.Row="1" Visibility="Collapsed">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>   <!-- TabView -->
        <RowDefinition Height="Auto"/>   <!-- 工具栏 -->
        <RowDefinition Height="*"/>      <!-- 编辑区 -->
    </Grid.RowDefinitions>

    <!-- TabView 标签栏 -->
    <TabView x:Name="NotepadTabView" Grid.Row="0"
             AddTabButtonClick="NotepadTabView_AddTabClick"
             TabCloseRequested="NotepadTabView_TabCloseRequested"
             TabItemsChanged="NotepadTabView_TabItemsChanged"
             SelectedIndex="0"
             Background="#1c1c1c">
    </TabView>

    <!-- 工具栏 -->
    <Grid x:Name="NotepadToolbar" Grid.Row="1" Background="#1e1e1e"
          Padding="8,4" BorderBrush="#333" BorderThickness="0,0,0,1">
        <!-- 左侧：文件操作 -->
        <!-- 右侧：格式化工具 -->
    </Grid>

    <!-- 编辑区 -->
    <TextBox x:Name="NotepadEditor" Grid.Row="2"
             AcceptsReturn="True"
             TextWrapping="Wrap"
             VerticalAlignment="Stretch"
             HorizontalAlignment="Stretch"
             TextChanged="NotepadEditor_TextChanged"/>
</Grid>
```

### 步骤 4：实现 TabView 标签管理逻辑
**文件**: `MainWindow.xaml.cs`

核心逻辑：
- **新增标签**：点击 `+` 按钮创建新标签，标题默认 "未命名"，自动选中
- **关闭标签**：点击 `×` 关闭标签，删除数据库记录，如果有关联文件则提示
- **切换标签**：切换时保存当前标签内容，加载目标标签内容
- **标签标题**：双击可编辑，或根据内容首行自动命名

数据结构：
```csharp
private ObservableCollection<NotepadTab> _notepadTabs = new();
private DispatcherTimer? _notepadSaveTimer;
```

### 步骤 5：实现工具栏
**文件**: `MainWindow.xaml` + `MainWindow.xaml.cs`

工具栏布局（左到右）：

**文件操作区**：
| 按钮 | 图标 | 功能 |
|------|------|------|
| 打开 | &#xE8E5; | FileOpenPicker 打开 .md/.txt 文件 |
| 另存为 | &#xE74E; | FileSavePicker 另存为 .md/.txt 文件 |

**分隔符**

**格式化工具区**：
| 按钮 | 图标 | 功能 | 插入语法 |
|------|------|------|----------|
| 粗体 | &#xE8DD; | 加粗 | `**文本**` |
| 斜体 | &#xE8DB; | 斜体 | `*文本*` |
| 下划线 | &#xE8DC; | 下划线 | `<u>文本</u>` |
| 删除线 | &#xE8DE; | 删除线 | `~~文本~~` |
| 自动编号 | &#xE8FD; | 有序列表 | `1. ` |

工具栏按钮样式：统一使用 WinUI3 ToggleButton/Button + FontIcon，深色主题适配。

### 步骤 6：实现自动保存
**文件**: `MainWindow.xaml.cs`

自动保存机制：
- 使用 `DispatcherTimer`，内容变更后 500ms 触发保存
- 保存时更新数据库中的 Content 和 UpdatedAt
- 保存时根据内容首行自动更新标签标题（如果用户未手动修改标题）

```csharp
private void NotepadEditor_TextChanged(object sender, TextChangedEventArgs e)
{
    _notepadSaveTimer?.Stop();
    _notepadSaveTimer = new DispatcherTimer();
    _notepadSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
    _notepadSaveTimer.Tick += (s, args) =>
    {
        _notepadSaveTimer?.Stop();
        SaveCurrentNotepadTab();
    };
    _notepadSaveTimer.Start();
}
```

### 步骤 7：实现文件打开/另存为
**文件**: `MainWindow.xaml.cs`

使用 WinUI3 的 `FileOpenPicker` 和 `FileSavePicker`：

**打开文件**：
1. 弹出 FileOpenPicker，过滤 .md 和 .txt 文件
2. 读取文件内容
3. 创建新标签，标题为文件名，内容为文件内容
4. 记录 FilePath 关联

**另存为**：
1. 弹出 FileSavePicker，默认 .md 格式
2. 将当前标签内容写入文件
3. 更新标签的 FilePath

### 步骤 8：实现格式化工具插入逻辑
**文件**: `MainWindow.xaml.cs`

工具栏按钮的插入逻辑：
- 获取当前选中文本
- 如果有选中文本：在选中文本前后包裹 Markdown 语法
- 如果没有选中文本：插入语法占位符并将光标放在中间

```csharp
private void InsertMarkdownSyntax(string prefix, string suffix)
{
    var textBox = NotepadEditor;
    var selectedText = textBox.SelectedText;
    var start = textBox.SelectionStart;

    if (!string.IsNullOrEmpty(selectedText))
    {
        var newText = prefix + selectedText + suffix;
        textBox.Text = textBox.Text.Remove(start, selectedText.Length).Insert(start, newText);
        textBox.SelectionStart = start + prefix.Length;
        textBox.SelectionLength = selectedText.Length;
    }
    else
    {
        var placeholder = prefix + "文本" + suffix;
        textBox.Text = textBox.Text.Insert(start, placeholder);
        textBox.SelectionStart = start + prefix.Length;
        textBox.SelectionLength = 2; // 选中"文本"
    }
}
```

### 步骤 9：修改导航逻辑
**文件**: `MainWindow.xaml.cs`

修改 `ShowNotepadContent()` 方法：
- 隐藏任务列表相关 UI（TaskListScrollViewer、AddTaskBar、DetailDrawer）
- 显示 NotepadContent
- 首次加载时从数据库恢复标签

修改 `ShowTaskListContent()` 方法：
- 隐藏 NotepadContent
- 保存当前记事本标签内容

### 步骤 10：编辑器样式适配
**文件**: `MainWindow.xaml`

为 TextBox 编辑器定制深色主题样式：
- 背景色：#161616（与页面一致）
- 文字色：#e0e0e0
- 选中文本色：#0078d4
- 字体：等宽字体（Cascadia Code 或 Consolas）
- 字号：14px
- 行距：1.5 倍
- 无边框
- 垂直滚动

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Models/NotepadTab.cs` | 新建 | 记事本标签数据模型 |
| `Services/DatabaseService.cs` | 修改 | 新增 NotepadTabs 表和 CRUD 方法 |
| `MainWindow.xaml` | 修改 | 替换占位区域为完整记事本界面 |
| `MainWindow.xaml.cs` | 修改 | 新增记事本相关事件处理和逻辑 |

## 注意事项
- 所有 UI 使用 WinUI3 原生控件和 ThemeResource
- 数据库操作不放在 UI 线程（虽然 SQLite 操作很快，但保持良好实践）
- 文件操作使用 WinUI3 的 Picker API（需注意 WinUI3 中 Picker 需要设置窗口句柄）
- 工具栏按钮使用 Segoe MDL2 Assets 图标字体
- 关闭标签时直接删除，不做确认提示（遵循用户描述）
- 编辑器使用等宽字体以适配 Markdown 编辑场景
