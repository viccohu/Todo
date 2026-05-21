using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.UI.Dispatching;
using Todo.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Todo.Services
{
    public class ReminderService
    {
        private static ReminderService? _instance;
        public static ReminderService Instance => _instance ??= new ReminderService();
        
        private Timer? _checkTimer;
        private DatabaseService _dbService;
        private HashSet<string> _notifiedReminders = new HashSet<string>();
        private readonly object _notifiedLock = new object();
        private readonly HashSet<string> _mutedTodayTasks = new HashSet<string>();
        private DispatcherQueue? _dispatcherQueue;
        private bool _isInitialized;
        private bool _hasCompletedInitialReminderCheck;
        private const string ReminderNotificationGroup = "Todo.Reminders";
        private const string SnoozeInputId = "snoozeMinutes";
        private static readonly TimeSpan ReminderCatchUpWindow = TimeSpan.FromMinutes(2);

        public event EventHandler<TaskCompletedFromNotificationEventArgs>? TaskCompletedFromNotification;
        
        private ReminderService()
        {
            _dbService = new DatabaseService();
        }
        
        public void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                AppNotificationManager.Default.Register();
                ScheduleAllPendingReminderNotifications();
                StartReminderCheck();
                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"ReminderService initialized successfully, DispatcherQueue={_dispatcherQueue != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReminderService initialization failed: {ex.Message}");
            }
        }
        
        public void StartReminderCheck()
        {
            _checkTimer?.Dispose();
            _checkTimer = new Timer(CheckReminders, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
            System.Diagnostics.Debug.WriteLine("Reminder check timer started");
        }
        
        public void StopReminderCheck()
        {
            _checkTimer?.Dispose();
            _checkTimer = null;
        }

        public void Shutdown()
        {
            StopReminderCheck();

            try
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.Unregister();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReminderService shutdown failed: {ex.Message}");
            }

            _isInitialized = false;
        }

        public void ResetNotifiedReminders()
        {
            lock (_notifiedLock)
            {
                _notifiedReminders.Clear();
            }
        }
        
        public void TestNotification()
        {
            System.Diagnostics.Debug.WriteLine("TestNotification called");
            var builder = CreateReminderNotificationBuilder(0, 0, "🧪 测试通知", "提醒功能测试", "如果你看到这条通知，说明通知功能正常工作！", false);
            ShowNotificationOnUIThread(builder.BuildNotification());
        }

        public void ScheduleReminderNotificationsForTask(int taskId)
        {
            try
            {
                RemoveScheduledReminderNotifications(taskId);

                var task = _dbService.GetTaskById(taskId);
                if (task == null || task.IsChecked)
                {
                    return;
                }

                foreach (var reminder in _dbService.GetRemindersForTask(taskId))
                {
                    ScheduleReminderNotification(task, reminder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleReminderNotificationsForTask error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void RemoveScheduledReminderNotifications(int taskId)
        {
            try
            {
                var notifier = ToastNotificationManager.CreateToastNotifier();
                var tagPrefix = GetReminderTagPrefix(taskId);
                foreach (var notification in notifier.GetScheduledToastNotifications())
                {
                    if (notification.Group == ReminderNotificationGroup &&
                        notification.Tag.StartsWith(tagPrefix, StringComparison.Ordinal))
                    {
                        notifier.RemoveFromSchedule(notification);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveScheduledReminderNotifications error: {ex.Message}");
            }
        }
        
        private void CheckReminders(object? state)
        {
            try
            {
                var now = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"Checking reminders at {now:HH:mm:ss}");

                // 自动完成截止日期已过的任务
                AutoCompleteOverdueTasks();

                var lookbackWindow = _hasCompletedInitialReminderCheck ? ReminderCatchUpWindow : TimeSpan.Zero;
                var dueReminders = _dbService.GetDueReminders(now, lookbackWindow);
                _hasCompletedInitialReminderCheck = true;
                System.Diagnostics.Debug.WriteLine($"Found {dueReminders.Count} due reminders");
                
                foreach (var reminder in dueReminders)
                {
                    if (IsMutedForToday(reminder.TaskId))
                    {
                        continue;
                    }

                    var reminderKey = $"{reminder.TaskId}_{reminder.ReminderDateTime:yyyyMMddHHmm}";
                    
                    System.Diagnostics.Debug.WriteLine($"Processing reminder: TaskId={reminder.TaskId}, ReminderTime={reminder.ReminderDateTime}, Key={reminderKey}");
                    
                    if (ShouldShowReminder(reminderKey))
                    {
                        System.Diagnostics.Debug.WriteLine($"Showing notification for reminder {reminderKey}");
                        ShowSystemNotification(reminder);
                        MarkReminderShown(reminderKey);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Reminder {reminderKey} already notified");
                    }
                    
                    if (reminder.EnableMultiDayReminders && reminder.SameDayIntervalMinutes > 0)
                    {
                        ScheduleSameDayReminders(reminder);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reminder check error: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void AutoCompleteOverdueTasks()
        {
            try
            {
                var overdueTasks = _dbService.GetOverdueUncheckedTasks();
                foreach (var task in overdueTasks)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-completing overdue task: {task.Id} - {task.Title}, DueDate={task.DueDate}");
                    _dbService.UpdateTaskAutoCompleted(task.Id);
                    RemoveScheduledReminderNotifications(task.Id);

                    // 处理重复任务的下一期
                    var recurrence = _dbService.GetRecurrenceForTask(task.Id);
                    if (recurrence != null && recurrence.RecurrenceType != RecurrenceType.None)
                    {
                        var sourceDueDate = task.DueDate ?? recurrence.BaseDate;
                        var newDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, sourceDueDate);
                        var newTask = _dbService.AddTask(task.Title, newDueDate, null, task.ListId);
                        newTask.Description = task.Description;
                        _dbService.UpdateTask(newTask);

                        recurrence.NextDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, newDueDate);
                        _dbService.UpdateRecurrence(recurrence);

                        var oldReminders = _dbService.GetRemindersForTask(task.Id);
                        foreach (var oldReminder in oldReminders)
                        {
                            if (!oldReminder.ReminderDateTime.HasValue) continue;
                            var dayOffset = (oldReminder.ReminderDateTime.Value.Date - sourceDueDate.Date).Days;
                            var newReminderDate = newDueDate.Date.AddDays(dayOffset).Add(oldReminder.ReminderDateTime.Value.TimeOfDay);
                            if (newReminderDate <= DateTime.Now) continue;

                            var newReminder = new Reminder
                            {
                                TaskId = newTask.Id,
                                ReminderType = oldReminder.ReminderType,
                                ReminderDateTime = newReminderDate,
                                EnableMultiDayReminders = oldReminder.EnableMultiDayReminders,
                                SameDayIntervalMinutes = oldReminder.SameDayIntervalMinutes,
                                CustomDays = oldReminder.CustomDays,
                                IsRecurring = oldReminder.IsRecurring,
                                RecurringInterval = oldReminder.RecurringInterval
                            };
                            _dbService.AddReminderWithDetails(newReminder);
                        }

                        var newRecurrence = _dbService.AddRecurrence(newTask.Id, recurrence.RecurrenceType, newDueDate);
                        newRecurrence.NextDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, newDueDate);
                        _dbService.UpdateRecurrence(newRecurrence);
                        ScheduleReminderNotificationsForTask(newTask.Id);

                        TaskCompletedFromNotification?.Invoke(this, new TaskCompletedFromNotificationEventArgs(task.Id, newTask.Id));
                    }
                    else
                    {
                        TaskCompletedFromNotification?.Invoke(this, new TaskCompletedFromNotificationEventArgs(task.Id, null));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoCompleteOverdueTasks error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ScheduleSameDayReminders(Reminder reminder)
        {
            if (!reminder.ReminderDateTime.HasValue || reminder.SameDayIntervalMinutes <= 0)
                return;
            
            try
            {
                var now = DateTime.Now;
                var reminderTime = reminder.ReminderDateTime.Value;
                
                if (reminderTime.Date != now.Date)
                    return;
                
                var minutesSinceReminder = (int)(now - reminderTime).TotalMinutes;
                if (minutesSinceReminder < 0)
                    return;
                
                var intervalsPassed = minutesSinceReminder / reminder.SameDayIntervalMinutes;
                var currentIntervalMinutes = (intervalsPassed + 1) * reminder.SameDayIntervalMinutes;
                
                var nextReminderTime = reminderTime.AddMinutes(currentIntervalMinutes);
                
                if (nextReminderTime.Date == now.Date && nextReminderTime <= now.AddMinutes(1))
                {
                    if (IsMutedForToday(reminder.TaskId))
                    {
                        return;
                    }

                    var reminderKey = $"{reminder.TaskId}_{nextReminderTime:yyyyMMddHHmm}";
                    
                    if (ShouldShowReminder(reminderKey))
                    {
                        var task = _dbService.GetTaskById(reminder.TaskId);
                        if (task != null && !task.IsChecked)
                        {
                            var builder = CreateReminderNotificationBuilder(task.Id, reminder.Id, "🔔 再次提醒", task.Title, $"提醒时间: {nextReminderTime:HH:mm}", true);
                            ShowNotificationOnUIThread(builder.BuildNotification());
                            MarkReminderShown(reminderKey);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleSameDayReminders error: {ex.Message}");
            }
        }

        private void ScheduleAllPendingReminderNotifications()
        {
            foreach (var task in _dbService.GetTasks(false))
            {
                ScheduleReminderNotificationsForTask(task.Id);
            }
        }

        private void ScheduleReminderNotification(TaskItem task, Reminder reminder)
        {
            if (!reminder.ReminderDateTime.HasValue || reminder.ReminderDateTime.Value <= DateTime.Now)
            {
                return;
            }

            var payload = CreateReminderNotificationBuilder(
                    task.Id,
                    reminder.Id,
                    "📋 任务提醒",
                    task.Title,
                    $"提醒时间: {reminder.ReminderDateTime.Value:HH:mm}",
                    reminder.EnableMultiDayReminders)
                .BuildNotification()
                .Payload;

            var doc = new XmlDocument();
            doc.LoadXml(payload);

            var scheduledNotification = new ScheduledToastNotification(
                doc,
                new DateTimeOffset(reminder.ReminderDateTime.Value));
            scheduledNotification.Tag = GetReminderTag(task.Id, reminder.Id, reminder.ReminderDateTime.Value);
            scheduledNotification.Group = ReminderNotificationGroup;

            ToastNotificationManager.CreateToastNotifier().AddToSchedule(scheduledNotification);
            System.Diagnostics.Debug.WriteLine($"Scheduled reminder notification: TaskId={task.Id}, ReminderId={reminder.Id}, Time={reminder.ReminderDateTime:yyyy-MM-dd HH:mm}");
        }

        private static string GetReminderTagPrefix(int taskId)
        {
            return $"T{taskId}_";
        }

        private static string GetReminderTag(int taskId, int reminderId, DateTime reminderTime)
        {
            return $"{GetReminderTagPrefix(taskId)}R{reminderId}_{reminderTime:MMddHHmm}";
        }

        private static string GetTemporaryReminderTag(int taskId, DateTime reminderTime)
        {
            return $"{GetReminderTagPrefix(taskId)}S{reminderTime:MMddHHmmss}";
        }

        private static string GetMutedTodayKey(int taskId)
        {
            return $"{taskId}_{DateTime.Today:yyyyMMdd}";
        }

        private bool ShouldShowReminder(string reminderKey)
        {
            lock (_notifiedLock)
            {
                return !_notifiedReminders.Contains(reminderKey);
            }
        }

        private void MarkReminderShown(string reminderKey)
        {
            lock (_notifiedLock)
            {
                _notifiedReminders.Add(reminderKey);

                if (_notifiedReminders.Count > 1000)
                {
                    _notifiedReminders.Clear();
                }
            }
        }

        private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            try
            {
                var action = GetArgument(args, "action");
                var taskIdText = GetArgument(args, "taskId");
                var reminderIdText = GetArgument(args, "reminderId");

                System.Diagnostics.Debug.WriteLine($"Notification invoked: {args.Argument}");

                if (!int.TryParse(taskIdText, out var taskId))
                {
                    return;
                }

                switch (action)
                {
                    case "complete":
                        CompleteTaskFromNotification(taskId);
                        break;
                    case "muteToday":
                        MuteTaskForToday(taskId);
                        break;
                    case "snooze":
                        var minutes = GetSnoozeMinutes(args);
                        ScheduleTemporarySnoozeNotification(taskId, reminderIdText, minutes);
                        break;
                    case "ack":
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNotificationInvoked error: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void ShowSystemNotification(Reminder reminder)
        {
            try
            {
                var task = _dbService.GetTaskById(reminder.TaskId);
                if (task == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Task not found for reminder: {reminder.TaskId}");
                    return;
                }
                
                if (task.IsChecked)
                {
                    System.Diagnostics.Debug.WriteLine($"Task already completed, skipping notification: {task.Title}");
                    return;
                }
                
                var timeText = reminder.ReminderDateTime.Value.ToString("HH:mm");
                System.Diagnostics.Debug.WriteLine($"Showing notification: {task.Title} at {timeText}");
                
                var builder = CreateReminderNotificationBuilder(
                    task.Id,
                    reminder.Id,
                    "📋 任务提醒",
                    task.Title,
                    $"提醒时间: {timeText}",
                    reminder.EnableMultiDayReminders);
                ShowNotificationOnUIThread(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show notification error: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private AppNotificationBuilder CreateReminderNotificationBuilder(
            int taskId,
            int reminderId,
            string title,
            string line1,
            string line2,
            bool isRepeatingReminder)
        {
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddArgument("taskId", taskId.ToString())
                .AddArgument("reminderId", reminderId.ToString())
                .AddText(title)
                .AddText(line1)
                .AddText(line2)
                .SetAudioEvent(AppNotificationSoundEvent.Reminder)
                .AddButton(new AppNotificationButton("已完成")
                    .AddArgument("action", "complete")
                    .AddArgument("taskId", taskId.ToString())
                    .AddArgument("reminderId", reminderId.ToString())
                    .SetButtonStyle(AppNotificationButtonStyle.Success))
                .AddButton(new AppNotificationButton("知道了")
                    .AddArgument("action", "ack")
                    .AddArgument("taskId", taskId.ToString())
                    .AddArgument("reminderId", reminderId.ToString()));

            if (isRepeatingReminder)
            {
                builder.AddButton(new AppNotificationButton("不再提醒")
                    .AddArgument("action", "muteToday")
                    .AddArgument("taskId", taskId.ToString())
                    .AddArgument("reminderId", reminderId.ToString()));
            }
            else
            {
                var snoozeButton = new AppNotificationButton("稍后提醒")
                    .AddArgument("action", "snooze")
                    .AddArgument("taskId", taskId.ToString())
                    .AddArgument("reminderId", reminderId.ToString());
                snoozeButton.InputId = SnoozeInputId;

                builder
                    .AddComboBox(new AppNotificationComboBox(SnoozeInputId)
                        .SetTitle("推迟时间")
                        .AddItem("5", "5分钟")
                        .AddItem("10", "10分钟")
                        .AddItem("15", "15分钟")
                        .AddItem("30", "30分钟")
                        .AddItem("60", "1小时")
                        .SetSelectedItem("10"))
                    .AddButton(snoozeButton);
            }

            return builder;
        }

        private void CompleteTaskFromNotification(int taskId)
        {
            var task = _dbService.GetTaskById(taskId);
            if (task == null || task.IsChecked)
            {
                return;
            }

            _dbService.UpdateTaskChecked(taskId, true);
            RemoveScheduledReminderNotifications(taskId);

            var recurrence = _dbService.GetRecurrenceForTask(taskId);
            if (recurrence == null || recurrence.RecurrenceType == RecurrenceType.None)
            {
                TaskCompletedFromNotification?.Invoke(this, new TaskCompletedFromNotificationEventArgs(taskId, null));
                return;
            }

            var sourceDueDate = task.DueDate ?? recurrence.BaseDate;
            var newDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, sourceDueDate);
            var newTask = _dbService.AddTask(task.Title, newDueDate, null, task.ListId);
            newTask.Description = task.Description;
            _dbService.UpdateTask(newTask);

            recurrence.NextDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, newDueDate);
            _dbService.UpdateRecurrence(recurrence);

            foreach (var oldReminder in _dbService.GetRemindersForTask(taskId))
            {
                if (!oldReminder.ReminderDateTime.HasValue)
                {
                    continue;
                }

                var dayOffset = (oldReminder.ReminderDateTime.Value.Date - sourceDueDate.Date).Days;
                var newReminderDate = newDueDate.Date.AddDays(dayOffset).Add(oldReminder.ReminderDateTime.Value.TimeOfDay);
                if (newReminderDate <= DateTime.Now)
                {
                    continue;
                }

                var newReminder = new Reminder
                {
                    TaskId = newTask.Id,
                    ReminderType = oldReminder.ReminderType,
                    ReminderDateTime = newReminderDate,
                    EnableMultiDayReminders = oldReminder.EnableMultiDayReminders,
                    SameDayIntervalMinutes = oldReminder.SameDayIntervalMinutes,
                    CustomDays = oldReminder.CustomDays,
                    IsRecurring = oldReminder.IsRecurring,
                    RecurringInterval = oldReminder.RecurringInterval
                };
                _dbService.AddReminderWithDetails(newReminder);
            }

            var newRecurrence = _dbService.AddRecurrence(newTask.Id, recurrence.RecurrenceType, newDueDate);
            newRecurrence.NextDueDate = CalculateNextRecurringDueDate(newRecurrence.RecurrenceType, newDueDate);
            _dbService.UpdateRecurrence(newRecurrence);
            ScheduleReminderNotificationsForTask(newTask.Id);
            TaskCompletedFromNotification?.Invoke(this, new TaskCompletedFromNotificationEventArgs(taskId, newTask.Id));
        }

        private static DateTime CalculateNextRecurringDueDate(RecurrenceType recurrenceType, DateTime currentDueDate)
        {
            return recurrenceType switch
            {
                RecurrenceType.Daily => currentDueDate.AddDays(1),
                RecurrenceType.Weekly => currentDueDate.AddDays(7),
                RecurrenceType.Monthly => currentDueDate.AddMonths(1),
                RecurrenceType.Yearly => currentDueDate.AddYears(1),
                _ => currentDueDate
            };
        }

        private void MuteTaskForToday(int taskId)
        {
            lock (_notifiedLock)
            {
                _mutedTodayTasks.Add(GetMutedTodayKey(taskId));
            }
        }

        private bool IsMutedForToday(int taskId)
        {
            lock (_notifiedLock)
            {
                return _mutedTodayTasks.Contains(GetMutedTodayKey(taskId));
            }
        }

        private int GetSnoozeMinutes(AppNotificationActivatedEventArgs args)
        {
            try
            {
                if (args.UserInput.TryGetValue(SnoozeInputId, out var value) &&
                    int.TryParse(value, out var minutes) &&
                    minutes > 0)
                {
                    return minutes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSnoozeMinutes error: {ex.Message}");
            }

            return 10;
        }

        private string? GetArgument(AppNotificationActivatedEventArgs args, string key)
        {
            return args.Arguments.TryGetValue(key, out var value) ? value : null;
        }

        private void ScheduleTemporarySnoozeNotification(int taskId, string? reminderIdText, int minutes)
        {
            var task = _dbService.GetTaskById(taskId);
            if (task == null || task.IsChecked)
            {
                return;
            }

            _ = int.TryParse(reminderIdText, out var reminderId);
            var reminderTime = DateTime.Now.AddMinutes(minutes);
            var builder = CreateReminderNotificationBuilder(
                task.Id,
                reminderId,
                "📋 任务提醒",
                task.Title,
                $"推迟到: {reminderTime:HH:mm}",
                false);

            var doc = new XmlDocument();
            doc.LoadXml(builder.BuildNotification().Payload);

            var scheduledNotification = new ScheduledToastNotification(doc, new DateTimeOffset(reminderTime));
            scheduledNotification.Tag = GetTemporaryReminderTag(task.Id, reminderTime);
            scheduledNotification.Group = ReminderNotificationGroup;
            ToastNotificationManager.CreateToastNotifier().AddToSchedule(scheduledNotification);
        }

        private void ShowNotificationOnUIThread(AppNotification appNotification)
        {
            try
            {
                if (_dispatcherQueue != null)
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            AppNotificationManager.Default.Show(appNotification);
                            System.Diagnostics.Debug.WriteLine($"AppNotification shown on UI thread successfully");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Show on UI thread error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    AppNotificationManager.Default.Show(appNotification);
                    System.Diagnostics.Debug.WriteLine($"AppNotification shown (no dispatcher) successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowNotificationOnUIThread error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public sealed class TaskCompletedFromNotificationEventArgs : EventArgs
    {
        public TaskCompletedFromNotificationEventArgs(int completedTaskId, int? newTaskId)
        {
            CompletedTaskId = completedTaskId;
            NewTaskId = newTaskId;
        }

        public int CompletedTaskId { get; }
        public int? NewTaskId { get; }
    }
}
