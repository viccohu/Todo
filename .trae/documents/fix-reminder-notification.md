# 提醒通知不工作 - 完整修复计划

## 问题排查结果

经过全面排查，发现以下 **5个关键问题**：

### 问题1：Timer 回调在后台线程，AppNotificationManager.Default.Show() 可能需要 UI 线程
- **位置**：`ReminderService.cs` 第54行 `CheckReminders` 方法
- **原因**：`System.Threading.Timer` 回调在后台线程池线程执行，而 `AppNotificationManager.Default.Show()` 在某些情况下可能需要 UI 线程上下文
- **修复**：保存 `DispatcherQueue` 引用，通过 `TryEnqueue` 在 UI 线程上执行 Show

### 问题2：ConfirmReminderSettings_Click 中跳过了过去时间的提醒
- **位置**：`MainWindow.xaml.cs` 第982-985行
- **原因**：`if (reminderDateTime < DateTime.Now) { continue; }` 会跳过所有已过时间的提醒。但用户设置1-2分钟后的提醒时，由于日历选择的日期是今天，时间选择后可能刚好在当前时间的边缘，导致被跳过
- **修复**：改为只跳过超过5分钟前的提醒（给用户一些缓冲时间），或者改为不跳过，让 GetDueReminders 查询来决定

### 问题3：ConfirmReminderSettings_Click 中跳过了超过截止日期的提醒
- **位置**：`MainWindow.xaml.cs` 第987-990行
- **原因**：`if (_selectedTask.DueDate.HasValue && reminderDateTime > _selectedTask.DueDate.Value) { continue; }` 如果任务没有截止日期但设置了提醒，这不会触发。但如果截止日期是今天，提醒时间在截止时间之后就会被跳过
- **修复**：移除此限制，或改为只比较日期不比较时间

### 问题4：ReminderService 没有在 UI 线程初始化 DispatcherQueue
- **位置**：`ReminderService.cs` 第25-39行
- **原因**：当前 `Initialize()` 方法没有保存 `DispatcherQueue`，导致无法在 UI 线程上执行通知
- **修复**：在 `Initialize()` 中获取并保存 `DispatcherQueue`

### 问题5：WindowsAppSDK 版本可能不支持 AppNotificationBuilder
- **位置**：`Todo.csproj` 第42行
- **原因**：`Microsoft.WindowsAppSDK` 版本 `2.0.1` 应该支持 `AppNotificationBuilder`（1.2+引入），但需要确认 API 是否可用
- **修复**：确认版本兼容性，如需要则升级版本

## 修复步骤

### 步骤1：修复 ReminderService - 添加 DispatcherQueue 支持
- 在 `Initialize()` 中保存 `DispatcherQueue.GetForCurrentThread()`
- 在 `ShowSystemNotification` 和 `ShowSameDayNotification` 中使用 `DispatcherQueue.TryEnqueue` 调用 `AppNotificationManager.Default.Show()`

### 步骤2：修复 ConfirmReminderSettings_Click - 移除过度限制
- 移除 `reminderDateTime < DateTime.Now` 的跳过逻辑（或改为超过30分钟才跳过）
- 移除 `reminderDateTime > _selectedTask.DueDate` 的跳过逻辑（提醒可以独立于截止日期存在）

### 步骤3：添加调试日志到 ConfirmReminderSettings_Click
- 在保存提醒后输出日志，确认提醒确实被保存到数据库
- 输出保存的提醒时间和任务ID

### 步骤4：验证数据库查询
- 在 `GetDueReminders` 中添加日志，确认查询能返回结果
- 检查 datetime 比较格式是否正确

### 步骤5：添加即时测试方法
- 在 ReminderService 中添加 `TestNotification()` 公共方法
- 在 UI 中添加测试按钮，点击后立即发送一条测试通知
- 这样可以快速验证通知 API 是否工作
