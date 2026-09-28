using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Memo.Models;
using Memo.Services;

namespace Memo
{
    public sealed partial class CompactWindow : Window
    {
        private DatabaseService _dbService;
        private ObservableCollection<TaskItem> _tasks;
        private ObservableCollection<TaskItem> _completedTasks;
        private bool _isMinimized;
        private bool _dragging;
        private bool _resizing;
        private CursorPoint _lastCursor;
        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint { public int X; public int Y; }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out CursorPoint point);

        private int _expandedHeight = 560;
        private string _matrixVersion = "";
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _refreshTimer;
        public event Action? ExitRequested;
        public event Action? TasksChanged;

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

            RefreshMatrix();
            _refreshTimer = DispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += (_, _) => RefreshMatrix();
            _refreshTimer.Start();

            Title = "Memo";
            try { this.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "16logo.ico")); } catch { }

            // 固定到桌面右下角
            this.SetupPinnedWindow(yOffset);
            this.Closed += (s, e) =>
            {
                _refreshTimer.Stop();
                SaveBounds();
                this.StopPinnedWindowGuard();
            };
        }

        private void SetupPinnedWindow(int yOffset = 40)
        {
            this.ApplyCompactWindowStyle();
            var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            _isMinimized = values.TryGetValue("Compact_TaskMinimized", out var minimized) && minimized is true;
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var width = values.TryGetValue("Compact_MatrixWidth", out var w) && w is int savedW ? savedW : 720;
            _expandedHeight = values.TryGetValue("Compact_MatrixHeight", out var h) && h is int savedH ? savedH : 560;
            width = Math.Clamp(width, Math.Min(480, area.Width), area.Width);
            _expandedHeight = Math.Clamp(_expandedHeight, Math.Min(320, area.Height), area.Height);
            var height = _isMinimized ? 40 : _expandedHeight;
            var x = values.TryGetValue("Compact_MatrixX", out var sx) && sx is int px ? px : area.X + area.Width - width - 16;
            var y = values.TryGetValue("Compact_MatrixY", out var sy) && sy is int py ? py : area.Y + area.Height - height - 16;
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                Math.Clamp(x, area.X, area.X + area.Width - width),
                Math.Clamp(y, area.Y, area.Y + area.Height - height), width, height));
            UpdateCollapsedState();
            this.UpdatePinnedWindowGuard();
        }

        private void RefreshMatrix()
        {
            try
            {
                var tasks = _dbService.GetActiveTasksForMatrix()
                    .OrderBy(t => t.DueDate?.Date ?? DateTime.MaxValue).ThenByDescending(t => t.CreatedAt).ToList();
                var version = string.Join("|", tasks.Select(t => $"{t.Id}:{t.Title}:{t.Quadrant}:{t.DueDate}:{t.DueDateShortDisplay}"));
                if (version == _matrixVersion && Q1List.ItemsSource != null) return;
                _matrixVersion = version;
                var lists = new[] { Q1List, Q2List, Q3List, Q4List };
                var counts = new[] { Q1Count, Q2Count, Q3Count, Q4Count };
                var empty = new[] { Q1Empty, Q2Empty, Q3Empty, Q4Empty };
                for (var i = 0; i < lists.Length; i++)
                {
                    var items = tasks.Where(t => (int)t.Quadrant == i).ToList();
                    lists[i].ItemsSource = items;
                    counts[i].Text = $"{items.Count} 项";
                    empty[i].Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex) { AppLog.Error($"Compact matrix refresh: {ex}"); }
        }

        private void SaveBounds()
        {
            var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            values["Compact_MatrixWidth"] = AppWindow.Size.Width;
            values["Compact_MatrixHeight"] = _expandedHeight;
            values["Compact_MatrixX"] = AppWindow.Position.X;
            values["Compact_MatrixY"] = AppWindow.Position.Y;
        }

        private void UpdateCollapsedState()
        {
            MatrixPanel.Visibility = _isMinimized ? Visibility.Collapsed : Visibility.Visible;
            ResizeGrip.Visibility = _isMinimized ? Visibility.Collapsed : Visibility.Visible;
            RootGrid.RowDefinitions[2].Height = new GridLength(_isMinimized ? 0 : 16);
            CompactToggleIcon.Glyph = _isMinimized ? "\uE70D" : "\uE70E";
        }

        private void Bounds_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed || !GetCursorPos(out _lastCursor)) return;
            _resizing = ReferenceEquals(sender, ResizeGrip);
            _dragging = ((UIElement)sender).CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Bounds_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging || !GetCursorPos(out var cursor)) return;
            ChangeBounds(cursor.X - _lastCursor.X, cursor.Y - _lastCursor.Y, _resizing);
            _lastCursor = cursor;
            e.Handled = true;
        }

        private void Bounds_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
            SaveBounds();
            e.Handled = true;
        }

        private void Bounds_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            SaveBounds();
        }

        private void ChangeBounds(double dx, double dy, bool resize)
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            const double scale = 1; // GetCursorPos 与 AppWindow 都使用物理像素。
            var x = AppWindow.Position.X;
            var y = AppWindow.Position.Y;
            var width = AppWindow.Size.Width;
            var height = AppWindow.Size.Height;
            if (resize)
            {
                width = Math.Clamp(width + (int)Math.Round(dx * scale), Math.Min(480, area.Width), area.Width);
                height = Math.Clamp(height + (int)Math.Round(dy * scale), Math.Min(320, area.Height), area.Height);
                _expandedHeight = height;
            }
            else
            {
                x += (int)Math.Round(dx * scale);
                y += (int)Math.Round(dy * scale);
            }
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                Math.Clamp(x, area.X, area.X + area.Width - width),
                Math.Clamp(y, area.Y, area.Y + area.Height - height), width, height));
            this.UpdatePinnedWindowGuard();
            HeightChanged?.Invoke(height);
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

        private void TitleBar_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ToggleExpand_Click(this, new RoutedEventArgs());
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TaskItem task)
            {
                // 四象限使用全量查询对象，同步当前主页集合中的同 ID 对象。
                var sharedTask = _tasks.FirstOrDefault(t => t.Id == task.Id);
                if (sharedTask != null) sharedTask.IsChecked = cb.IsChecked ?? false;
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

                    if (sharedTask != null)
                    {
                        _tasks.Remove(sharedTask);
                        _completedTasks.Add(sharedTask);
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
                RefreshMatrix();
                TasksChanged?.Invoke();
            }
        }

        private void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMinimized) _expandedHeight = AppWindow.Size.Height;
            _isMinimized = !_isMinimized;
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var height = _isMinimized ? 40 : Math.Min(_expandedHeight, area.Height);
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(AppWindow.Position.X,
                Math.Clamp(AppWindow.Position.Y, area.Y, area.Y + area.Height - height), AppWindow.Size.Width, height));
            UpdateCollapsedState();
            this.UpdatePinnedWindowGuard();
            SaveMinimizedState();
            SaveBounds();
            HeightChanged?.Invoke(height);
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
