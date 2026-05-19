using Microsoft.Toolkit.Uwp.Notifications;
using Todo.Models;
using Todo.Services;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Todo.Services
{
    public class ReminderService
    {
        private static ReminderService? _instance;
        public static ReminderService Instance => _instance ??= new ReminderService();
        
        private Timer? _checkTimer;
        private DatabaseService _dbService;
        private HashSet<string> _notifiedReminders = new HashSet<string>();
        
        private ReminderService()
        {
            _dbService = new DatabaseService();
        }
        
        public void Initialize()
        {
            StartReminderCheck();
        }
        
        public void StartReminderCheck()
        {
            _checkTimer = new Timer(CheckReminders, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }
        
        public void StopReminderCheck()
        {
            _checkTimer?.Dispose();
            _checkTimer = null;
        }
        
        private void CheckReminders(object? state)
        {
            try
            {
                var now = DateTime.Now;
                var dueReminders = _dbService.GetDueReminders(now);
                
                foreach (var reminder in dueReminders)
                {
                    var reminderKey = $"{reminder.TaskId}_{reminder.ReminderDateTime:yyyyMMddHHmm}";
                    
                    if (!_notifiedReminders.Contains(reminderKey))
                    {
                        ShowSystemNotification(reminder);
                        _notifiedReminders.Add(reminderKey);
                        
                        if (_notifiedReminders.Count > 1000)
                        {
                            _notifiedReminders.Clear();
                        }
                    }
                    
                    if (reminder.EnableMultiDayReminders && reminder.SameDayIntervalMinutes > 0)
                    {
                        ScheduleSameDayReminders(reminder);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reminder check error: {ex.Message}");
            }
        }
        
        private void ScheduleSameDayReminders(Reminder reminder)
        {
            if (!reminder.ReminderDateTime.HasValue || reminder.SameDayIntervalMinutes <= 0)
                return;
            
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
                var reminderKey = $"{reminder.TaskId}_{nextReminderTime:yyyyMMddHHmm}";
                
                if (!_notifiedReminders.Contains(reminderKey))
                {
                    var task = _dbService.GetTaskById(reminder.TaskId);
                    if (task != null && !task.IsChecked)
                    {
                        ShowSameDayNotification(task, nextReminderTime);
                        _notifiedReminders.Add(reminderKey);
                    }
                }
            }
        }
        
        private void ShowSystemNotification(Reminder reminder)
        {
            try
            {
                var task = _dbService.GetTaskById(reminder.TaskId);
                if (task == null || task.IsChecked)
                    return;
                
                var timeText = reminder.ReminderDateTime.Value.ToString("HH:mm");
                
                new ToastContentBuilder()
                    .AddText("📋 任务提醒")
                    .AddText(task.Title)
                    .AddText($"提醒时间: {timeText}")
                    .Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show notification error: {ex.Message}");
            }
        }
        
        private void ShowSameDayNotification(TaskItem task, DateTime reminderTime)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText("🔔 再次提醒")
                    .AddText(task.Title)
                    .AddText($"提醒时间: {reminderTime:HH:mm}")
                    .Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show same day notification error: {ex.Message}");
            }
        }
    }
}
