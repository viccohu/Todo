using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Windows.Graphics;
using Todo.Models;
using Todo.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Todo
{
    public sealed partial class MainWindow : Window
    {
        private bool _isDesktopMode = false;
        private bool _isCompactMinimized = false;
        private AppWindow? _appWindow;
        private bool _showCompleted = false;
        private bool _compactShowCompleted = false;
        private bool _isDrawerOpen = false;
        private DatabaseService _dbService = new DatabaseService();
        private TaskItem? _selectedTask;
        
        private DispatcherTimer? _saveTitleTimer;
        private DispatcherTimer? _saveDescriptionTimer;
        
        private System.ComponentModel.PropertyChangedEventHandler? TaskItemPropertyChanged;
        
        // 动画相关字段
        private bool _isAnimating = false;

        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> CompletedTasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskGroup> CustomGroups { get; } = new ObservableCollection<TaskGroup>();

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeCustomTitleBar();
            InitializeData();
            LoadCustomGroups();
            
            NavView.SelectedItem = NavView.MenuItems[1];
        }

        private void InitializeData()
        {
            var allTasks = _dbService.GetTasks();
            foreach (var task in allTasks)
            {
                if (task.IsChecked)
                {
                    CompletedTasks.Add(task);
                }
                else
                {
                    Tasks.Add(task);
                }
            }
            
            TasksList.ItemsSource = Tasks;
            CompletedTasksList.ItemsSource = CompletedTasks;
        }

        private void LoadCustomGroups()
        {
            var groups = _dbService.GetGroups();
            CustomGroups.Clear();
            foreach (var group in groups)
            {
                CustomGroups.Add(group);
            }
            RefreshCustomNavigation();
        }

        private void RefreshCustomNavigation()
        {
            var itemsToRemove = new List<object>();
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString()?.StartsWith("Group_") == true)
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach (var item in itemsToRemove)
            {
                NavView.MenuItems.Remove(item);
            }

            foreach (var group in CustomGroups)
            {
                var groupItem = new NavigationViewItem
                {
                    Content = group.Name,
                    Tag = $"Group_{group.Id}",
                    IsExpanded = group.IsExpanded
                };
                groupItem.Icon = new FontIcon { Glyph = "\uE8B7" };

                foreach (var list in group.Lists)
                {
                    var listItem = new NavigationViewItem
                    {
                        Content = list.Name,
                        Tag = $"List_{list.Id}"
                    };
                    listItem.Icon = new FontIcon { Glyph = "\uE8FD" };
                    groupItem.MenuItems.Add(listItem);
                }

                NavView.MenuItems.Add(groupItem);
            }
        }

        private void InitializeCustomTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            
            _appWindow = this.AppWindow;
            if (_appWindow != null)
            {
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonForegroundColor = Colors.White;
            }
            
            AppTitleBar.Loaded += AppTitleBar_Loaded;
            AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            SetDragRectangles();
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetDragRectangles();
        }

        private void SetDragRectangles()
        {
            if (ExtendsContentIntoTitleBar)
            {
                var scaleFactor = Content.XamlRoot.RasterizationScale;
                
                if (_appWindow != null)
                {
                    var leftInset = _appWindow.TitleBar.LeftInset;
                    var rightInset = _appWindow.TitleBar.RightInset;
                    
                    LeftPaddingColumn.Width = new GridLength(leftInset);
                    RightPaddingColumn.Width = new GridLength(rightInset);
                }
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                
                if (tag?.StartsWith("List_") == true)
                {
                    PageTitle.Text = item.Content?.ToString() ?? "列表";
                    PageIcon.Glyph = "\uE8FD";
                    return;
                }
                
                if (tag?.StartsWith("Group_") == true)
                {
                    PageTitle.Text = item.Content?.ToString() ?? "分组";
                    PageIcon.Glyph = "\uE8B7";
                    return;
                }
                
                switch (tag)
                {
                    case "Calendar":
                        PageTitle.Text = "日历视图";
                        PageIcon.Glyph = "\uE787";
                        break;
                    case "Important":
                        PageTitle.Text = "重要任务";
                        PageIcon.Glyph = "\uE8C8";
                        break;
                    case "Daily":
                        PageTitle.Text = "日常";
                        PageIcon.Glyph = "\uE823";
                        break;
                    case "Weekly":
                        PageTitle.Text = "周常";
                        PageIcon.Glyph = "\uE817";
                        break;
                    case "Monthly":
                        PageTitle.Text = "月常";
                        PageIcon.Glyph = "\uE817";
                        break;
                }
            }
        }

        private void TaskItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if (!pointerPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }
            
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                if (_selectedTask == task)
                {
                    CloseDrawer();
                }
                else
                {
                    _selectedTask = task;
                    ShowDrawer(task);
                    UpdateTaskItemSelection(task, border);
                }
            }
        }

        private void TaskItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 0x25, 0x25, 0x25));
            }
        }

        private void TaskItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                if (_selectedTask == task)
                {
                    border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(255, 0x2a, 0x2a, 0x2a));
                }
                else
                {
                    border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(255, 0x1e, 0x1e, 0x1e));
                }
            }
        }

        private void UpdateTaskItemSelection(TaskItem? selectedTask, Border? selectedBorder)
        {
            foreach (var item in Tasks)
            {
                item.IsSelected = (item == selectedTask);
            }
            foreach (var item in CompletedTasks)
            {
                item.IsSelected = (item == selectedTask);
            }
        }

        private void TaskItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // 右键菜单由 ContextFlyout 自动处理，不切换抽屉
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is TaskItem task)
            {
                _dbService.DeleteTask(task.Id);
                
                if (Tasks.Contains(task))
                {
                    Tasks.Remove(task);
                }
                else if (CompletedTasks.Contains(task))
                {
                    CompletedTasks.Remove(task);
                }
                
                if (_selectedTask == task)
                {
                    CloseDrawer();
                }
            }
        }

        private void ShowDrawer(TaskItem task)
        {
            _selectedTask = task;
            DetailTitle.Text = task.Title;
            DetailDescription.Text = task.Description;
            
            SubTasksList.ItemsSource = task.SubTasks;
            
            DueDateText.Text = task.DueDate.HasValue ? task.DueDate.Value.ToString("M月d日") : "截止日期";
            ClearDueDateButton.Visibility = task.DueDate.HasValue ? Visibility.Visible : Visibility.Collapsed;
            
            var reminders = _dbService.GetRemindersForTask(task.Id);
            if (reminders.Count > 0)
            {
                var reminder = reminders[0];
                if (reminder.ReminderDateTime.HasValue)
                {
                    ReminderText.Text = reminder.ReminderDateTime.Value.ToString("M月d日 HH:mm");
                }
                ClearReminderButton.Visibility = Visibility.Visible;
            }
            else
            {
                ReminderText.Text = "提醒我";
                ClearReminderButton.Visibility = Visibility.Collapsed;
            }
            
            var recurrence = _dbService.GetRecurrenceForTask(task.Id);
            if (recurrence != null && recurrence.RecurrenceType != RecurrenceType.None)
            {
                RecurrenceText.Text = recurrence.DisplayText;
                ClearRecurrenceButton.Visibility = Visibility.Visible;
            }
            else
            {
                RecurrenceText.Text = "重复";
                ClearRecurrenceButton.Visibility = Visibility.Collapsed;
            }
            
            DetailDrawer.Visibility = Visibility.Visible;
            _isDrawerOpen = true;
        }
        
        private void NotifyTaskItemChanged(TaskItem task)
        {
            TaskItemPropertyChanged?.Invoke(task, new System.ComponentModel.PropertyChangedEventArgs("SubTasks"));
        }

        private void CloseDrawer()
        {
            DetailDrawer.Visibility = Visibility.Collapsed;
            _isDrawerOpen = false;
            _selectedTask = null;
        }

        private void CloseDrawer_Click(object sender, RoutedEventArgs e)
        {
            CloseDrawer();
        }

        private void DetailTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _selectedTask.Title = DetailTitle.Text;
                
                _saveTitleTimer?.Stop();
                _saveTitleTimer = new DispatcherTimer();
                _saveTitleTimer.Interval = TimeSpan.FromMilliseconds(300);
                _saveTitleTimer.Tick += (s, args) =>
                {
                    _saveTitleTimer?.Stop();
                    if (_selectedTask != null)
                    {
                        _dbService.UpdateTask(_selectedTask);
                    }
                };
                _saveTitleTimer.Start();
            }
        }
        
        private void DetailDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _selectedTask.Description = DetailDescription.Text;
                
                _saveDescriptionTimer?.Stop();
                _saveDescriptionTimer = new DispatcherTimer();
                _saveDescriptionTimer.Interval = TimeSpan.FromMilliseconds(300);
                _saveDescriptionTimer.Tick += (s, args) =>
                {
                    _saveDescriptionTimer?.Stop();
                    if (_selectedTask != null)
                    {
                        _dbService.UpdateTask(_selectedTask);
                    }
                };
                _saveDescriptionTimer.Start();
            }
        }

        private void DetailDatePicker_DateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (_selectedTask != null && args.NewDate != null)
            {
                _selectedTask.DueDate = args.NewDate.Value.DateTime;
            }
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TaskItem task)
            {
                _dbService.UpdateTaskChecked(task.Id, cb.IsChecked ?? false);
                
                if (cb.IsChecked ?? false)
                {
                    var recurrence = _dbService.GetRecurrenceForTask(task.Id);
                    if (recurrence != null && recurrence.RecurrenceType != RecurrenceType.None)
                    {
                        var newDueDate = recurrence.CalculateNextDueDate();
                        
                        var newTask = _dbService.AddTask(task.Title, newDueDate, null, task.ListId);
                        newTask.Description = task.Description;
                        _dbService.UpdateTask(newTask);
                        
                        recurrence.NextDueDate = newDueDate;
                        _dbService.UpdateRecurrence(recurrence);
                        
                        var oldReminders = _dbService.GetRemindersForTask(task.Id);
                        foreach (var oldReminder in oldReminders)
                        {
                            if (oldReminder.ReminderDateTime.HasValue && task.DueDate.HasValue)
                            {
                                var dayOffset = (oldReminder.ReminderDateTime.Value.Date - task.DueDate.Value.Date).Days;
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
                        
                        Tasks.Add(newTask);
                    }
                    
                    if (Tasks.Contains(task))
                    {
                        Tasks.Remove(task);
                        CompletedTasks.Add(task);
                    }
                }
                else
                {
                    if (CompletedTasks.Contains(task))
                    {
                        CompletedTasks.Remove(task);
                        Tasks.Add(task);
                    }
                }
            }
        }

        private void ShowAddTaskInput_Click(object sender, RoutedEventArgs e)
        {
            AddTaskButton.Visibility = Visibility.Collapsed;
            AddTaskInputArea.Visibility = Visibility.Visible;
            AddTaskTextBox.Text = "";
            AddTaskTextBox.Focus(FocusState.Programmatic);
        }

        private void AddTaskInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddTaskFromInput();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HideAddTaskInput();
                e.Handled = true;
            }
        }

        private void AddTaskInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(AddTaskTextBox.Text))
            {
                AddTaskFromInput();
            }
            else
            {
                HideAddTaskInput();
            }
        }

        private void AddTaskFromInput()
        {
            if (!string.IsNullOrWhiteSpace(AddTaskTextBox.Text))
            {
                var newTask = _dbService.AddTask(AddTaskTextBox.Text.Trim(), DateTime.Now);
                Tasks.Add(newTask);
            }
            HideAddTaskInput();
        }

        private void HideAddTaskInput()
        {
            AddTaskInputArea.Visibility = Visibility.Collapsed;
            AddTaskButton.Visibility = Visibility.Visible;
            AddTaskTextBox.Text = "";
        }

        private void ToggleCompleted_Click(object sender, RoutedEventArgs e)
        {
            _showCompleted = !_showCompleted;
            CompletedTasksList.Visibility = _showCompleted ? Visibility.Visible : Visibility.Collapsed;
            CompletedArrow.Glyph = _showCompleted ? "\uE70E" : "\uE70D";
        }

        private void NewGroup_Click(object sender, RoutedEventArgs e)
        {
            var newGroup = _dbService.AddGroup("新建分组");
            CustomGroups.Add(newGroup);
            RefreshCustomNavigation();
        }

        private void NewList_Click(object sender, RoutedEventArgs e)
        {
            if (CustomGroups.Count == 0)
            {
                NewGroup_Click(sender, e);
            }
            
            if (CustomGroups.Count > 0)
            {
                var targetGroup = CustomGroups[0];
                var newList = _dbService.AddList("新建列表", targetGroup.Id);
                targetGroup.Lists.Add(newList);
                RefreshCustomNavigation();
            }
        }

        private void DetailCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null && sender is CheckBox cb)
            {
                _selectedTask.IsChecked = cb.IsChecked ?? false;
            }
        }

        private void ShowAddSubTaskInput_Click(object sender, RoutedEventArgs e)
        {
            AddSubTaskButton.Visibility = Visibility.Collapsed;
            AddSubTaskTextBox.Visibility = Visibility.Visible;
            AddSubTaskTextBox.Text = "";
            AddSubTaskTextBox.Focus(FocusState.Programmatic);
        }

        private void AddSubTaskInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddSubTaskFromInput();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HideAddSubTaskInput();
                e.Handled = true;
            }
        }

        private void AddSubTaskInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(AddSubTaskTextBox.Text))
            {
                AddSubTaskFromInput();
            }
            else
            {
                HideAddSubTaskInput();
            }
        }

        private void AddSubTaskFromInput()
        {
            if (_selectedTask != null && !string.IsNullOrWhiteSpace(AddSubTaskTextBox.Text))
            {
                var subTask = _dbService.AddSubTask(_selectedTask.Id, AddSubTaskTextBox.Text.Trim());
                _selectedTask.SubTasks.Add(subTask);
                SubTasksList.ItemsSource = null;
                SubTasksList.ItemsSource = _selectedTask.SubTasks;
                
                NotifyTaskItemChanged(_selectedTask);
            }
            HideAddSubTaskInput();
        }

        private void HideAddSubTaskInput()
        {
            AddSubTaskTextBox.Visibility = Visibility.Collapsed;
            AddSubTaskButton.Visibility = Visibility.Visible;
            AddSubTaskTextBox.Text = "";
        }

        private void SubTaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is SubTask subTask)
            {
                _dbService.UpdateSubTaskChecked(subTask.Id, subTask.IsChecked);
            }
        }

        private void SubTask_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }

        private void DeleteSubTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SubTask subTask && _selectedTask != null)
            {
                _dbService.DeleteSubTask(subTask.Id);
                _selectedTask.SubTasks.Remove(subTask);
                SubTasksList.ItemsSource = null;
                SubTasksList.ItemsSource = _selectedTask.SubTasks;
            }
        }

        private void ShowDueDatePicker_Click(object sender, RoutedEventArgs e)
        {
            DetailCalendarView.Visibility = DetailCalendarView.Visibility == Visibility.Visible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
            
            if (_selectedTask?.DueDate != null)
            {
                DetailCalendarView.SelectedDates.Clear();
                DetailCalendarView.SelectedDates.Add(_selectedTask.DueDate.Value);
            }
        }
        
        private void DetailCalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (_selectedTask != null && args.AddedDates.Count > 0)
            {
                var selectedDate = args.AddedDates[0].DateTime;
                
                if (selectedDate.Date < DateTime.Today)
                {
                    return;
                }
                
                _selectedTask.DueDate = selectedDate;
                DueDateText.Text = selectedDate.ToString("M月d日");
                ClearDueDateButton.Visibility = Visibility.Visible;
                
                _dbService.UpdateTask(_selectedTask);
                
                TaskItemPropertyChanged?.Invoke(_selectedTask, new PropertyChangedEventArgs(nameof(_selectedTask.DueDateDisplay)));
                
                DetailCalendarView.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ClearDueDate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _selectedTask.DueDate = null;
                DueDateText.Text = "截止日期";
                ClearDueDateButton.Visibility = Visibility.Collapsed;
                
                _dbService.UpdateTask(_selectedTask);
            }
        }
        
        private void ShowReminderDialog_Click(object sender, RoutedEventArgs e)
        {
            ReminderSettingsPanel.Visibility = ReminderSettingsPanel.Visibility == Visibility.Visible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
            
            if (_selectedTask != null)
            {
                var reminders = _dbService.GetRemindersForTask(_selectedTask.Id);
                if (reminders.Count > 0)
                {
                    var reminder = reminders[0];
                    if (reminder.ReminderDateTime.HasValue)
                    {
                        ReminderTimePicker.SelectedTime = reminder.ReminderDateTime.Value.TimeOfDay;
                    }
                    EnableSameDayReminderToggle.IsOn = reminder.EnableMultiDayReminders;
                    SameDayIntervalComboBox.IsEnabled = reminder.EnableMultiDayReminders;
                    if (reminder.SameDayIntervalMinutes > 0)
                    {
                        for (int i = 0; i < SameDayIntervalComboBox.Items.Count; i++)
                        {
                            if (SameDayIntervalComboBox.Items[i] is ComboBoxItem item && 
                                int.TryParse(item.Tag?.ToString(), out int minutes) && 
                                minutes == reminder.SameDayIntervalMinutes)
                            {
                                SameDayIntervalComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ReminderTimePicker.SelectedTime = TimeSpan.FromHours(9);
                    EnableSameDayReminderToggle.IsOn = false;
                    SameDayIntervalComboBox.IsEnabled = false;
                    SameDayIntervalComboBox.SelectedIndex = -1;
                }
            }
        }
        
        private void EnableSameDayReminderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SameDayIntervalComboBox.IsEnabled = EnableSameDayReminderToggle.IsOn;
            if (!EnableSameDayReminderToggle.IsOn)
            {
                SameDayIntervalComboBox.SelectedIndex = -1;
            }
        }
        
        private void SameDayIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
        
        private void CancelReminderSettings_Click(object sender, RoutedEventArgs e)
        {
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        }
        
        private void ConfirmReminderSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                var selectedDates = ReminderCalendarView.SelectedDates;
                var reminderTime = ReminderTimePicker.SelectedTime ?? TimeSpan.FromHours(9);
                
                _dbService.DeleteRemindersForTask(_selectedTask.Id);
                
                if (selectedDates.Count > 0)
                {
                    foreach (var date in selectedDates)
                    {
                        var reminderDateTime = new DateTime(
                            date.Year, date.Month, date.Day,
                            (int)reminderTime.TotalHours,
                            (int)(reminderTime.TotalMinutes % 60),
                            0);
                        
                        if (reminderDateTime < DateTime.Now)
                        {
                            continue;
                        }
                        
                        if (_selectedTask.DueDate.HasValue && reminderDateTime > _selectedTask.DueDate.Value)
                        {
                            continue;
                        }
                        
                        var reminder = new Reminder
                        {
                            TaskId = _selectedTask.Id,
                            ReminderType = ReminderType.Custom,
                            ReminderDateTime = reminderDateTime,
                            EnableMultiDayReminders = EnableSameDayReminderToggle.IsOn,
                            SameDayIntervalMinutes = EnableSameDayReminderToggle.IsOn && SameDayIntervalComboBox.SelectedItem is ComboBoxItem item
                                ? int.Parse(item.Tag.ToString())
                                : 0
                        };
                        
                        _dbService.AddReminderWithDetails(reminder);
                    }
                    
                    ReminderText.Text = selectedDates.Count == 1
                        ? $"{selectedDates[0].ToString("M月d日")} {reminderTime.Hours:D2}:{reminderTime.Minutes:D2}"
                        : $"已设置 {selectedDates.Count} 个提醒";
                    ClearReminderButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ReminderText.Text = "提醒我";
                    ClearReminderButton.Visibility = Visibility.Collapsed;
                }
                
                ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ToggleRecurringReminder_Click(object sender, RoutedEventArgs e)
        {
        }
        
        private void ClearReminder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _dbService.DeleteRemindersForTask(_selectedTask.Id);
                _selectedTask.Reminders.Clear();
                ReminderText.Text = "提醒我";
                ClearReminderButton.Visibility = Visibility.Collapsed;
                RecurringReminderButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowRecurrenceMenu_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            
            var dailyItem = new MenuFlyoutItem { Text = "每天", Icon = new SymbolIcon(Symbol.CalendarDay) };
            dailyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Daily);
            
            var weeklyItem = new MenuFlyoutItem { Text = "每周", Icon = new SymbolIcon(Symbol.CalendarWeek) };
            weeklyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Weekly);
            
            var monthlyItem = new MenuFlyoutItem { Text = "每月", Icon = new FontIcon { Glyph = "\uE817" } };
            monthlyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Monthly);
            
            var yearlyItem = new MenuFlyoutItem { Text = "每年", Icon = new FontIcon { Glyph = "\uE817" } };
            yearlyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Yearly);
            
            flyout.Items.Add(dailyItem);
            flyout.Items.Add(weeklyItem);
            flyout.Items.Add(monthlyItem);
            flyout.Items.Add(yearlyItem);
            
            flyout.ShowAt(RecurrenceButton);
        }

        private void SetRecurrence(RecurrenceType type)
        {
            if (_selectedTask != null)
            {
                var baseDate = _selectedTask.DueDate ?? DateTime.Now;
                
                var existingRecurrence = _dbService.GetRecurrenceForTask(_selectedTask.Id);
                if (existingRecurrence != null)
                {
                    _dbService.DeleteRecurrence(_selectedTask.Id);
                }
                
                var recurrence = _dbService.AddRecurrence(_selectedTask.Id, type, baseDate);
                recurrence.NextDueDate = _dbService.CalculateNextRecurrenceDate(type, baseDate);
                _dbService.UpdateRecurrence(recurrence);
                
                _selectedTask.Recurrence = recurrence;
                
                RecurrenceText.Text = recurrence.DisplayText;
                ClearRecurrenceButton.Visibility = Visibility.Visible;
            }
        }

        private void ClearRecurrence_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null && _selectedTask.Recurrence != null)
            {
                _dbService.DeleteRecurrence(_selectedTask.Id);
                _selectedTask.Recurrence = null;
                RecurrenceText.Text = "重复";
                ClearRecurrenceButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ToggleDesktopMode_Click(object sender, RoutedEventArgs e)
        {
            _isDesktopMode = !_isDesktopMode;
            if (_isDesktopMode)
            {
                EnterPinnedMode();
            }
            else
            {
                ExitPinnedMode();
            }
        }

        private void EnterPinnedMode()
        {
            if (_isAnimating) return;
            _isAnimating = true;
            CloseDrawer();

            // 设置固定模式样式
            this.SetPinnedStyle(400, 500);

            NormalContent.Visibility = Visibility.Collapsed;
            CompactContent.Visibility = Visibility.Visible;

            CompactTasksList.ItemsSource = null;
            CompactTasksList.ItemsSource = Tasks;
            CompactCompletedTasksList.ItemsSource = null;
            CompactCompletedTasksList.ItemsSource = CompletedTasks;

            AppTitleBar.Visibility = Visibility.Collapsed;
            SetTitleBar(DummyTitleBar);
            RootGrid.RowDefinitions[0].Height = new GridLength(0);

            CompactScrollViewer.Visibility = Visibility.Visible;
            CompactToggleButton.Visibility = Visibility.Visible;
            CompactToggleIcon.Glyph = "\uE96E";
            _isCompactMinimized = false;

            PinIcon.Glyph = "\uE196";
            _isAnimating = false;
        }

        private void ExitPinnedMode()
        {
            if (_isAnimating) return;
            _isAnimating = true;

            // 直接恢复正常模式样式（不使用动画）
            this.SetNormalStyle();

            NormalContent.Visibility = Visibility.Visible;
            CompactContent.Visibility = Visibility.Collapsed;

            AppTitleBar.Visibility = Visibility.Visible;
            SetTitleBar(AppTitleBar);
            RootGrid.RowDefinitions[0].Height = new GridLength(32);

            PinIcon.Glyph = "\uE718";

            _isAnimating = false;
        }

        private void CompactToggleCompleted_Click(object sender, RoutedEventArgs e)
        {
            _compactShowCompleted = !_compactShowCompleted;
            CompactCompletedTasksList.Visibility = _compactShowCompleted ? Visibility.Visible : Visibility.Collapsed;
            CompactCompletedArrow.Glyph = _compactShowCompleted ? "\uE70E" : "\uE70D";
        }

        private async void CompactToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            if (!_isCompactMinimized)
            {
                _isAnimating = true;
                // 先播放收起动画
                await AnimateWindowSize(400, 500, 400, 90, 200);
                // 动画结束后再隐藏内容，修改图标
                CompactScrollViewer.Visibility = Visibility.Collapsed;
                CompactToggleIcon.Glyph = "\uE96D"; // 向下箭头
                _isCompactMinimized = true;
                _isAnimating = false;
            }
            else
            {
                _isAnimating = true;
                // 先显示内容，修改图标
                CompactScrollViewer.Visibility = Visibility.Visible;
                CompactToggleIcon.Glyph = "\uE96E"; // 向上箭头
                // 再播放展开动画
                await AnimateWindowSize(400, 90, 400, 500, 200);
                _isCompactMinimized = false;
                _isAnimating = false;
            }
        }

        private void CompactTitle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isCompactMinimized)
            {
                CompactToggle_Click(this, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// 平滑动画调整窗口尺寸
        /// </summary>
        private async Task AnimateWindowSize(int fromWidth, int fromHeight, int toWidth, int toHeight, int durationMs)
        {
            const int frameDurationMs = 16; // 60fps
            int totalFrames = (int)Math.Ceiling((double)durationMs / frameDurationMs);

            this.BeginAnimation();

            for (int i = 1; i <= totalFrames; i++)
            {
                // 使用缓动函数 (Ease Out)
                double t = (double)i / totalFrames;
                double easeT = 1 - Math.Pow(1 - t, 3); // Cubic ease out

                int currentWidth = fromWidth + (int)Math.Round((toWidth - fromWidth) * easeT);
                int currentHeight = fromHeight + (int)Math.Round((toHeight - fromHeight) * easeT);

                if (_isDesktopMode)
                {
                    this.AnimateResizePinned(currentWidth, currentHeight);
                }

                await Task.Delay(frameDurationMs);
            }

            this.EndAnimation(toWidth, toHeight);
        }
    }
}
