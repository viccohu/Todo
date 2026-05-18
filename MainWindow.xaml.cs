using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Windows.Graphics;
using Todo.Models;
using Todo.Services;
using System.Collections.Generic;

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
            DetailTitle.Text = task.Title;
            DetailDescription.Text = task.Description;
            DetailDrawer.Visibility = Visibility.Visible;
            _isDrawerOpen = true;
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
            }
        }

        private void DetailDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _selectedTask.Description = DetailDescription.Text;
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
            DetailDatePicker.Visibility = DetailDatePicker.Visibility == Visibility.Visible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        private void ClearDueDate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _selectedTask.DueDate = null;
                DueDateText.Text = "截止日期";
                ClearDueDateButton.Visibility = Visibility.Collapsed;
                DetailDatePicker.Date = null;
            }
        }

        private void ShowReminderMenu_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutItem { Text = "截止日提醒", Icon = new SymbolIcon(Symbol.Clock) });
            flyout.Items.Add(new MenuFlyoutItem { Text = "自定义时间", Icon = new SymbolIcon(Symbol.Calendar) });
            flyout.Items.Add(new MenuFlyoutItem { Text = "反复提醒", Icon = new SymbolIcon(Symbol.RepeatAll) });
            flyout.ShowAt(ReminderButton);
        }

        private void ToggleRecurringReminder_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ClearReminder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                foreach (var reminder in _selectedTask.Reminders)
                {
                    _dbService.DeleteReminder(reminder.Id);
                }
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
                var recurrence = _dbService.AddRecurrence(_selectedTask.Id, type, baseDate);
                _selectedTask.Recurrence = recurrence;
                
                RecurrenceText.Text = type switch
                {
                    RecurrenceType.Daily => "每天",
                    RecurrenceType.Weekly => "每周",
                    RecurrenceType.Monthly => "每月",
                    RecurrenceType.Yearly => "每年",
                    _ => "重复"
                };
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
            CloseDrawer();

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

            ExpandButton.Visibility = Visibility.Collapsed;
            CompactScrollViewer.Visibility = Visibility.Visible;
            CompactMinimizeButton.Visibility = Visibility.Visible;
            _isCompactMinimized = false;

            PinIcon.Glyph = "\uE196";
        }

        private void ExitPinnedMode()
        {
            this.SetNormalStyle();

            NormalContent.Visibility = Visibility.Visible;
            CompactContent.Visibility = Visibility.Collapsed;

            AppTitleBar.Visibility = Visibility.Visible;
            SetTitleBar(AppTitleBar);
            RootGrid.RowDefinitions[0].Height = new GridLength(32);

            PinIcon.Glyph = "\uE718";
        }

        private void CompactToggleCompleted_Click(object sender, RoutedEventArgs e)
        {
            _compactShowCompleted = !_compactShowCompleted;
            CompactCompletedTasksList.Visibility = _compactShowCompleted ? Visibility.Visible : Visibility.Collapsed;
            CompactCompletedArrow.Glyph = _compactShowCompleted ? "\uE70E" : "\uE70D";
        }

        private void CompactMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCompactMinimized)
            {
                CompactScrollViewer.Visibility = Visibility.Collapsed;
                CompactMinimizeButton.Visibility = Visibility.Collapsed;
                ExpandButton.Visibility = Visibility.Visible;
                this.ResizePinned(400, 100);
                _isCompactMinimized = true;
            }
            else
            {
                ExpandCompactWindow(this, new RoutedEventArgs());
            }
        }

        private void CompactTitle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isCompactMinimized)
            {
                ExpandCompactWindow(this, new RoutedEventArgs());
            }
        }

        private void ExpandCompactWindow(object sender, RoutedEventArgs e)
        {
            ExpandButton.Visibility = Visibility.Collapsed;
            CompactScrollViewer.Visibility = Visibility.Visible;
            CompactMinimizeButton.Visibility = Visibility.Visible;
            this.ResizePinned(400, 500);
            _isCompactMinimized = false;
        }
    }
}
