# 实现真实数据的接入和储存计划

## 问题分析

当前代码存在以下问题：

1. **DatabaseService.cs** 每次初始化时会删除旧数据库（第19-23行），导致数据无法持久化
2. **MainWindow.xaml.cs** 的 `InitializeData()` 方法使用硬编码的示例数据
3. **TasksPage.xaml.cs** 的 `AddTask_Click` 方法创建临时任务对象，不保存到数据库

## 实现步骤

### 1. 修复 DatabaseService.cs
- 移除删除旧数据库的代码，确保数据持久化
- 添加从数据库加载任务的方法 `GetTasks()`
- 添加更新任务状态的方法 `UpdateTaskChecked()`
- 添加删除任务的方法 `DeleteTask()`

### 2. 修改 MainWindow.xaml.cs
- 修改 `InitializeData()` 方法，从数据库加载真实数据，移除示例任务代码
- 添加从数据库加载已完成任务的逻辑
- 更新删除任务的逻辑，同步到数据库

### 3. 修改 TasksPage.xaml.cs
- 修改 `AddTask_Click` 方法，使用数据库服务添加任务

## 文件修改清单

| 文件 | 修改内容 |
|------|----------|
| Services/DatabaseService.cs | 移除删除数据库代码，添加任务查询、更新、删除方法 |
| MainWindow.xaml.cs | 移除示例数据，实现从数据库加载任务 |
| TasksPage.xaml.cs | 使用数据库服务添加任务 |

## 风险处理

- 数据库结构变更需要考虑数据迁移，当前版本首次使用无需迁移
- 确保数据库连接正确关闭，使用 `using` 语句
- 添加适当的错误处理

## 预期结果

- 应用启动时自动从数据库加载之前保存的任务
- 用户创建的任务会持久化到数据库
- 任务状态变更会同步到数据库
- 删除任务会从数据库中移除