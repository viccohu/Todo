# 导航与页面逻辑开发计划

## 当前项目状态分析

- 所有UI逻辑集中在 `MainWindow.xaml` 和 `MainWindow.xaml.cs` 中，未使用 Frame 页面导航
- 导航项切换仅改变页面标题和图标，不切换数据源
- `ImportantTasksPage` 和 `TasksPage` 存在但未实际使用（含硬编码示例数据）
- 自定义区的列表必须属于某个分组（GroupId NOT NULL），不支持独立列表
- 任务模型缺少 `IsImportant` 字段
- 数据库 Lists 表缺少 `IsBuiltIn` 标记

---

## 实施步骤

### 第一步：数据库模型变更

#### 1.1 修改 `TaskItem` 模型 (`TaskItem.cs`)
- 添加 `IsImportant` 属性（bool，默认 false），实现 INotifyPropertyChanged

#### 1.2 修改 `TaskList` 模型 (`Models/TaskList.cs`)
- `GroupId` 改为可空类型 `int?`（支持独立列表，不属于任何分组）
- 添加 `IsBuiltIn` 属性（bool，默认 false）
- 添加 `ListCategory` 枚举属性（None/Daily/Weekly/Monthly/Notepad），标识内置列表类型

#### 1.3 修改 `DatabaseService` (`Services/DatabaseService.cs`)
- **数据库迁移**：
  - Tasks 表添加 `IsImportant INTEGER NOT NULL DEFAULT 0` 列
  - Lists 表 `GroupId` 改为可空（需重建表，因为 SQLite 不支持直接 ALTER COLUMN）
  - Lists 表添加 `IsBuiltIn INTEGER NOT NULL DEFAULT 0` 列
  - Lists 表添加 `ListCategory INTEGER NOT NULL DEFAULT 0` 列
- **新增方法**：
  - `GetImportantTasks()` — 获取 IsImportant=true 的任务
  - `UpdateTaskImportant(int id, bool isImportant)` — 更新任务重要性
  - `AddListStandalone(string name)` — 创建独立列表（GroupId=null）
  - `MoveListToGroup(int listId, int? groupId)` — 移动列表到分组/移出分组
  - `GetStandaloneLists()` — 获取不属于任何分组的列表
  - `GetTasksByListId(int listId)` — 根据列表ID获取任务
  - `GetBuiltInListByCategory(ListCategory category)` — 获取内置列表
- **初始化内置列表**：在 `InitializeDatabase` 中确保 Daily/Weekly/Monthly/Notepad 四个内置列表存在（IsBuiltIn=1）

### 第二步：导航结构变更 (`MainWindow.xaml`)

#### 2.1 将"日历视图"改为"记事本"
- `Content` 从 "日历视图" 改为 "记事本"
- `Tag` 从 "Calendar" 改为 "Notepad"
- 图标改为记事本图标（Glyph `&#xE70F;` 或 `&#xE8BD;`）

#### 2.2 移除"自定义"标题
- 删除 `<NavigationViewItemHeader Content="自定义"/>`

#### 2.3 添加自定义区空状态图标
- 在分隔线后添加一个 `Grid`，内含一个浅色 `FontIcon`（如列表图标 `&#xE8FD;`，Opacity=0.15）
- 通过 `x:Name="CustomAreaEmptyIcon"` 命名，在代码中控制可见性
- 有自定义项时隐藏，无自定义项时显示

#### 2.4 为自定义导航项添加右键菜单
- 自定义列表项：右键菜单包含"重命名"和"删除"
- 自定义分组项：右键菜单包含"重命名"和"删除"
- 内置导航项（记事本、重要任务、常驻任务子项）不提供右键删除

### 第三步：导航逻辑变更 (`MainWindow.xaml.cs`)

#### 3.1 添加当前导航状态跟踪
- 新增 `_currentNavTag` 字段，记录当前选中的导航项 Tag
- 新增 `_currentListId` 字段，记录当前选中的列表ID（用于自定义列表和内置列表）

#### 3.2 重构 `NavView_SelectionChanged`
- 选中"记事本"(Notepad)：显示记事本占位页面（暂显示空内容+提示文字）
- 选中"重要任务"(Important)：加载 IsImportant=true 的任务
- 选中"日常"(Daily)/"周常"(Weekly)/"月常"(Monthly)：加载对应内置列表的任务
- 选中自定义列表(List_{id})：加载该列表的任务
- 选中自定义分组(Group_{id})：加载该分组下所有列表的任务汇总

#### 3.3 重构 `InitializeData`
- 不再加载全部任务，改为根据默认选中项加载
- 默认选中"重要任务"

#### 3.4 重构 `AddTaskFromInput`
- 根据当前导航状态决定新任务的属性：
  - "重要任务"页：新任务 IsImportant=true
  - "日常"/"周常"/"月常"页：新任务 ListId=对应内置列表ID
  - 自定义列表页：新任务 ListId=当前列表ID
  - 其他页面：新任务无特殊属性

#### 3.5 任务详情抽屉增加"重要"标记
- 在抽屉设置区添加"标记为重要"开关
- 切换时更新 IsImportant 并保存到数据库

### 第四步：自定义区功能完善

#### 4.1 重构 `RefreshCustomNavigation`
- 先清除所有自定义导航项（Tag 以 "Group_" 或 "List_" 或 "StandaloneList_" 开头）
- 加载独立列表（GroupId=null），作为顶级导航项添加（Tag="StandaloneList_{id}"）
- 加载分组及其下属列表，作为可展开导航项添加
- 更新空状态图标可见性：无独立列表且无分组时显示

#### 4.2 修改 `NewList_Click`
- 创建独立列表（GroupId=null），不再自动创建分组
- 刷新导航，选中新创建的列表

#### 4.3 修改 `NewGroup_Click`
- 创建分组
- 刷新导航

#### 4.4 实现拖拽功能
- 为自定义区的 NavigationViewItem 启用拖拽（`CanDrag="True"`、`AllowDrop="True"`）
- 处理 `DragItemsStarting`/`DragOver`/`Drop` 事件
- 拖拽列表到分组上：更新列表的 GroupId
- 拖拽列表出分组（拖到自定义区空白处）：将 GroupId 设为 null
- 拖拽完成后更新数据库和导航

#### 4.5 实现右键菜单
- 在 `RefreshCustomNavigation` 中为自定义项动态添加 `ContextFlyout`
- 列表右键菜单：重命名（弹出 ContentDialog 输入新名称）、删除
- 分组右键菜单：重命名、删除（删除分组时将下属列表变为独立列表，不级联删除任务）
- 内置项不添加右键菜单

#### 4.6 删除确认
- 删除列表：ContentDialog 确认，删除列表及其所有任务
- 删除分组：ContentDialog 确认，分组内列表变为独立列表

### 第五步：记事本占位页面

#### 5.1 记事本页面内容
- 选中"记事本"时，主内容区显示占位UI：
  - 居中显示一个记事本图标（浅色，Opacity=0.3）
  - 下方显示"记事本功能即将推出"提示文字
  - 隐藏底部的"添加任务"栏

### 第六步：常驻任务列表初始化

#### 6.1 内置列表初始化
- 在数据库初始化时，检查并创建四个内置列表：
  - "日常"（ListCategory=Daily）
  - "周常"（ListCategory=Weekly）
  - "月常"（ListCategory=Monthly）
  - "记事本"（ListCategory=Notepad）
- 这些列表的 IsBuiltIn=1，不可删除

#### 6.2 常驻任务导航项绑定
- "日常"导航项 Tag="Daily"，绑定到 ListCategory=Daily 的内置列表
- "周常"导航项 Tag="Weekly"，绑定到 ListCategory=Weekly 的内置列表
- "月常"导航项 Tag="Monthly"，绑定到 ListCategory=Monthly 的内置列表

---

## 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `TaskItem.cs` | 修改 | 添加 IsImportant 属性 |
| `Models/TaskList.cs` | 修改 | GroupId 可空，添加 IsBuiltIn、ListCategory |
| `Services/DatabaseService.cs` | 修改 | 数据库迁移、新增方法、内置列表初始化 |
| `MainWindow.xaml` | 修改 | 导航结构调整、空状态图标、右键菜单 |
| `MainWindow.xaml.cs` | 修改 | 导航逻辑重构、数据源切换、拖拽、右键菜单处理 |
| `Models/ListCategory.cs` | 新建 | ListCategory 枚举定义 |

---

## 实施顺序

1. **模型层** → TaskItem、TaskList、ListCategory 枚举
2. **数据层** → DatabaseService 迁移和新方法
3. **导航结构** → MainWindow.xaml 导航项调整
4. **导航逻辑** → MainWindow.xaml.cs 数据源切换
5. **自定义区** → 空状态、独立列表、右键菜单
6. **拖拽功能** → 列表拖入/拖出分组
7. **记事本占位** → 占位UI
8. **测试验证** → 编译运行，验证各功能
