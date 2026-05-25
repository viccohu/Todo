using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;
using Todo.Models;
using Todo.Services;

namespace Todo
{
    public sealed partial class CompactWindow : Window
    {
        private DatabaseService _dbService;
        private ObservableCollection<TaskItem> _tasks;
        private ObservableCollection<TaskItem> _completedTasks;
        private bool _showCompleted;
        private bool _isAnimating;
        private bool _isMinimized;

        public event Action? ExitRequested;

        public event Action<int>? HeightChanged;

        public CompactWindow(
            ObservableCollection<TaskItem> tasks,
            ObservableCollection<TaskItem> completedTasks,
            DatabaseService dbService,
            int yOffset = 40)
        {
            this.InitializeComponent();

            _dbService = dbService;
            _tasks = tasks;
            _completedTasks = completedTasks;

            CompactTasksList.ItemsSource = _tasks;
            CompactCompletedTasksList.ItemsSource = _completedTasks;

            // 固定到桌面右下角
            this.SetupPinnedWindow(yOffset);
        }

        private void SetupPinnedWindow(int yOffset = 40)
        {
            this.ApplyCompactWindowStyle();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var wasMinimized = settings.Values.TryGetValue("Compact_TaskMinimized", out var val) && val is true;
            _isMinimized = wasMinimized;

            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = 1500,
                    Y = yOffset,
                    Width = 400,
                    Height = wasMinimized ? 32 : 480
                });
            }

            if (wasMinimized)
            {
                CompactScrollViewer.Visibility = Visibility.Collapsed;
                CompactToggleIcon.Glyph = "";
            }
        }

        private void SaveMinimizedState()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["Compact_TaskMinimized"] = _isMinimized;
        }


        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke();
        }

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isMinimized)
            {
                ToggleExpand_Click(this, new RoutedEventArgs());
            }
        }

        private void TitleBar_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ToggleExpand_Click(this, new RoutedEventArgs());
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TaskItem task)
            {
                _dbService.UpdateTaskChecked(task.Id, cb.IsChecked ?? false);

                if (cb.IsChecked ?? false)
                {
                    ReminderService.Instance.RemoveScheduledReminderNotifications(task.Id);

                    var recurrence = _dbService.GetRecurrenceForTask(task.Id);
                    if (recurrence != null && recurrence.RecurrenceType != RecurrenceType.None)
                    {
                        var sourceDueDate = task.DueDate ?? recurrence.BaseDate;
                        var newDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, sourceDueDate).Date;

                        var newTask = _dbService.AddTask(task.Title, newDueDate, null, task.ListId);
                        newTask.Description = task.Description;
                        _dbService.UpdateTask(newTask);

                        recurrence.NextDueDate = CalculateNextRecurringDueDate(recurrence.RecurrenceType, newDueDate);
                        _dbService.UpdateRecurrence(recurrence);

                        var oldReminders = _dbService.GetRemindersForTask(task.Id);
                        foreach (var oldReminder in oldReminders)
                        {
                            if (oldReminder.ReminderDateTime.HasValue)
                            {
                                var dayOffset = (oldReminder.ReminderDateTime.Value.Date - sourceDueDate.Date).Days;
                                var newReminderDate = newDueDate.Date.AddDays(dayOffset).Add(oldReminder.ReminderDateTime.Value.TimeOfDay);
                                if (newReminderDate > DateTime.Now)
                                {
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
                            }
                        }

                        var newRecurrence = _dbService.AddRecurrence(newTask.Id, recurrence.RecurrenceType, newDueDate);
                        newRecurrence.NextDueDate = _dbService.CalculateNextRecurrenceDate(recurrence.RecurrenceType, newDueDate);
                        _dbService.UpdateRecurrence(newRecurrence);
                        newTask.Recurrence = newRecurrence;

                        _tasks.Add(newTask);
                        ReminderService.Instance.ScheduleReminderNotificationsForTask(newTask.Id);
                    }

                    if (_tasks.Contains(task))
                    {
                        _tasks.Remove(task);
                        _completedTasks.Add(task);
                    }
                }
                else
                {
                    if (_completedTasks.Contains(task))
                    {
                        _completedTasks.Remove(task);
                        _tasks.Add(task);
                    }
                }
            }
        }

        private void ToggleCompleted_Click(object sender, RoutedEventArgs e)
        {
            _showCompleted = !_showCompleted;
            CompactCompletedTasksList.Visibility = _showCompleted ? Visibility.Visible : Visibility.Collapsed;
            CompactCompletedArrow.Glyph = _showCompleted ? "" : "";
        }

        private async void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            var appWindow = this.AppWindow;
            if (appWindow == null) return;

            if (!_isMinimized)
            {
                _isAnimating = true;
                await AnimateWindowSize(480, 40, 200);
                CompactScrollViewer.Visibility = Visibility.Collapsed;
                CompactToggleIcon.Glyph = "";
                _isMinimized = true;
                _isAnimating = false;
                SaveMinimizedState();
            }
            else
            {
                _isAnimating = true;
                CompactScrollViewer.Visibility = Visibility.Visible;
                CompactToggleIcon.Glyph = "";
                await AnimateWindowSize(40, 480, 200);
                _isMinimized = false;
                _isAnimating = false;
                SaveMinimizedState();
            }
        }

        private async Task AnimateWindowSize(int fromHeight, int toHeight, int durationMs)
        {
            var appWindow = this.AppWindow;
            if (appWindow == null) return;

            const int frameDurationMs = 16;
            int totalFrames = (int)Math.Ceiling((double)durationMs / frameDurationMs);

            var pos = appWindow.Position;
            var width = appWindow.Size.Width;

            for (int i = 1; i <= totalFrames; i++)
            {
                double t = (double)i / totalFrames;
                double easeT = 1 - Math.Pow(1 - t, 3);
                int currentHeight = fromHeight + (int)Math.Round((toHeight - fromHeight) * easeT);
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = pos.X,
                    Y = pos.Y,
                    Width = width,
                    Height = currentHeight
                });
                HeightChanged?.Invoke(currentHeight);
                await Task.Delay(frameDurationMs);
            }

            appWindow.MoveAndResize(new Windows.Graphics.RectInt32
            {
                X = pos.X,
                Y = pos.Y,
                Width = width,
                Height = toHeight
            });
            HeightChanged?.Invoke(toHeight);
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
    }
}
