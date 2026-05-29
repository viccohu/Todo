using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Windows.Graphics;
using Windows.Foundation;
using Todo.Models;
using Todo.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using System.Drawing.Printing;

namespace Todo
{
    public sealed partial class MainWindow : Window
    {
        private bool _isDesktopMode = false;
        private AppWindow? _appWindow;
        private bool _showCompleted = false;
        private bool _isDrawerOpen = false;
        private DatabaseService _dbService = new DatabaseService();
        private TaskItem? _selectedTask;
        private readonly Dictionary<TaskItem, (Border border, PropertyChangedEventHandler handler)> _borderSubscriptions = new();
        
        private DispatcherTimer? _saveTitleTimer;
        private DispatcherTimer? _saveDescriptionTimer;
        private bool _isUpdatingDescriptionText;
        // 链接追踪: 显示文本中标题的位置 → URL
        private List<(string title, string url, int displayIndex, int displayLength)> _descriptionLinks = new();
        private string _previousDescriptionText = "";
        
        private System.ComponentModel.PropertyChangedEventHandler? TaskItemPropertyChanged;
        
        private bool _isAnimating = false;
        private Storyboard? _drawerStoryboard;

        private string _currentNavTag = "Important";
        private int? _currentListId = null;
        private DateTimeOffset? _pendingDueDate = null;
        private RecurrenceType _pendingRecurrence = RecurrenceType.None;
        private bool _isDatePickerOpen = false;
        private bool _isRecurrenceMenuOpen = false;
        private List<TaskList> _standaloneLists = new List<TaskList>();

        private SystemTrayService? _trayService;
        private bool _isExiting = false;

        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> CompletedTasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskGroup> CustomGroups { get; } = new ObservableCollection<TaskGroup>();

        public MainWindow()
        {
            this.InitializeComponent();
            AppLog.Info("App started");
            InitializeCustomTitleBar();
            InitializeCalendarBounds();

            // 注册主窗口全局热键 (Alt+1/Alt+2 切换固定窗口, Alt+` 唤起主窗口)
            WindowHelper.RegisterMainWindow(this.GetWindowHandle());

            LoadCustomGroups();

            // 全局快捷键：Ctrl+Shift+P 强制退出固定模式（兜底恢复手段）
            RootGrid.KeyDown += (sender, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.P &&
                    IsCtrlPressed() &&
                    IsShiftPressed() &&
                    _isDesktopMode)
                {
                    ToggleDesktopMode_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }

                // 全局 Enter：记事本→编辑模式，任务页→提交新任务
                if (e.Key == Windows.System.VirtualKey.Enter &&
                    !IsCtrlPressed() && !IsShiftPressed() && !IsAltPressed())
                {
                    if (_currentNavTag == "Notepad")
                    {
                        if (_isPreviewMode && _currentNotepadTab != null)
                        {
                            SwitchToEditMode();
                            e.Handled = true;
                        }
                    }
                    else if (_currentNavTag != "Notepad")
                    {
                        if (AddTaskInputArea.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(AddTaskTextBox.Text))
                        {
                            AddTaskFromInput();
                            e.Handled = true;
                        }
                    }
                }
            };

            _trayService = new SystemTrayService(this, RootGrid, _dbService);
            _trayService.ExitRequested += () =>
            {
                _isExiting = true;
                _trayService.Dispose();
                Close();
            };
            _trayService.DatabaseImported += () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_currentNavTag == "Notepad")
                    {
                        _notepadTabs.Clear();
                        var tabs = _dbService.GetNotepadTabs();
                        foreach (var t in tabs) _notepadTabs.Add(t);
                        NotepadTabView.TabItems.Clear();
                        foreach (var tab in _notepadTabs)
                            NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                        NotepadTabView.SelectedIndex = 0;
                    }
                    else
                    {
                        LoadTasksForCurrentNav();
                    }
                });
            };

            if (_appWindow != null)
            {
                _appWindow.Closing += (sender, args) =>
                {
                    if (!_isExiting)
                    {
                        args.Cancel = true;
                        _trayService?.HideToTray();
                    }
                };
            }

            ReminderService.Instance.TaskCompletedFromNotification += ReminderService_TaskCompletedFromNotification;
            ReminderService.Instance.DateChanged += OnDateChanged;

            if (AddTaskDueDateButton.Flyout is Flyout dateFlyout)
            {
                dateFlyout.Opening += (s, e) => _isDatePickerOpen = true;
                dateFlyout.Closed += (s, e) => _isDatePickerOpen = false;
            }

            LoadTasksForCurrentNav();

            // 恢复上次的固定模式状态（必须在 NavView.SelectedItem 之前，避免被覆盖）
            RestoreCompactState();

            NavView.SelectedItem = NavView.MenuItems[1];

            UpdatePinButtonState();

            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"[Notepad] MainWindow_Closed: _currentNavTag={_currentNavTag}, _currentNotepadTab={_currentNotepadTab?.Id}");
            if (_currentNavTag == "Notepad")
            {
                SaveCurrentNotepadTab();
            }

            // 清理桌面固定模式资源
            SaveCompactState();
            _taskCompactWindow?.Close();
            _notepadCompactWindow?.Close();
            WindowHelper.ShutdownDesktopPin();
        }

        private void LoadTasksForCurrentNav()
        {
            // 先自动完成截止日期已过的任务
            AutoCompleteOverdueTasks();

            Tasks.Clear();
            CompletedTasks.Clear();
            _borderSubscriptions.Clear();

            List<TaskItem> allTasks;

            switch (_currentNavTag)
            {
                case "Important":
                    allTasks = _dbService.GetImportantTasks();
                    break;
                case "Daily":
                case "Weekly":
                case "Monthly":
                    {
                        var category = _currentNavTag switch
                        {
                            "Daily" => ListCategory.Daily,
                            "Weekly" => ListCategory.Weekly,
                            "Monthly" => ListCategory.Monthly,
                            _ => ListCategory.None
                        };
                        var builtInList = _dbService.GetBuiltInListByCategory(category);
                        if (builtInList != null)
                        {
                            _currentListId = builtInList.Id;
                            allTasks = _dbService.GetTasksByListId(builtInList.Id);
                        }
                        else
                        {
                            allTasks = new List<TaskItem>();
                        }
                        break;
                    }
                case "StandaloneList":
                case "GroupList":
                    if (_currentListId.HasValue)
                    {
                        allTasks = _dbService.GetTasksByListId(_currentListId.Value);
                    }
                    else
                    {
                        allTasks = new List<TaskItem>();
                    }
                    break;
                case "Group":
                    if (_currentListId.HasValue)
                    {
                        allTasks = _dbService.GetTasksByGroupLists(_currentListId.Value);
                    }
                    else
                    {
                        allTasks = new List<TaskItem>();
                    }
                    break;
                default:
                    allTasks = new List<TaskItem>();
                    break;
            }

            foreach (var task in allTasks)
            {
                var subTasks = _dbService.GetSubTasksForTask(task.Id);
                foreach (var subTask in subTasks)
                {
                    task.SubTasks.Add(subTask);
                }

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

        private void InitializeCalendarBounds()
        {
            var today = new DateTimeOffset(DateTime.Today);
            DetailCalendarView.MinDate = today;
            DetailCalendarView.SetDisplayDate(today);
            ReminderCalendarView.MinDate = today;
            ReminderCalendarView.SetDisplayDate(today);
        }

        private void LoadCustomGroups()
        {
            var groups = _dbService.GetGroups();
            CustomGroups.Clear();
            foreach (var group in groups)
            {
                CustomGroups.Add(group);
            }
            _standaloneLists = _dbService.GetStandaloneLists();
            RefreshCustomNavigation();
            RefreshRecurringSubItems();
        }

        private void RefreshRecurringSubItems()
        {
            RecurringNavItem.MenuItems.Clear();

            var builtInCategories = new[] { ListCategory.Daily, ListCategory.Weekly, ListCategory.Monthly };
            foreach (var category in builtInCategories)
            {
                var list = _dbService.GetBuiltInListByCategory(category);
                if (list == null) continue;

                var tag = category switch
                {
                    ListCategory.Daily => "Daily",
                    ListCategory.Weekly => "Weekly",
                    ListCategory.Monthly => "Monthly",
                    _ => ""
                };

                var iconPath = category switch
                {
                    ListCategory.Daily => AppIcons.Daily,
                    ListCategory.Weekly => AppIcons.Weekly,
                    ListCategory.Monthly => AppIcons.Monthly,
                    _ => ""
                };

                var item = new NavigationViewItem
                {
                    Content = list.Name,
                    Tag = tag,
                    Icon = AppIcons.Create(iconPath, 16)
                };
                item.ContextFlyout = CreateBuiltInListContextFlyout(list);
                RecurringNavItem.MenuItems.Add(item);
            }
        }

        private MenuFlyout CreateBuiltInListContextFlyout(TaskList list)
        {
            var flyout = new MenuFlyout();
            var renameItem = new MenuFlyoutItem { Text = "重命名", Icon = new SymbolIcon(Symbol.Rename) };
            renameItem.Click += (s, e) => ShowRenameListDialog(list);
            flyout.Items.Add(renameItem);
            return flyout;
        }

        private void AutoCompleteOverdueTasks()
        {
            try
            {
                var now = DateTime.Now;
                var overdueTasks = _dbService.GetOverdueUncheckedTasks();
                foreach (var task in overdueTasks)
                {
                    if (!task.DueDate.HasValue) continue;

                    // 今天截止必须等到 17:00，过去截止立即完成
                    if (task.DueDate.Value.Date == now.Date && now.Hour < 17)
                    {
                        AppLog.AutoComplete($"SKIP 今日{now.Hour}:{now.Minute:00}未到17点 | Task#{task.Id} '{task.Title}' Due={task.DueDate:yyyy-MM-dd}");
                        continue;
                    }

                    AppLog.AutoComplete($"DONE Task#{task.Id} '{task.Title}' Due={task.DueDate:yyyy-MM-dd} IsPast={task.DueDate.Value.Date < now.Date}");
                    _dbService.UpdateTaskAutoCompleted(task.Id);
                    ReminderService.Instance.RemoveScheduledReminderNotifications(task.Id);

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
                        ReminderService.Instance.ScheduleReminderNotificationsForTask(newTask.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoCompleteOverdueTasks error: {ex.Message}");
            }
        }

        private void RefreshCustomNavigation()
        {
            var itemsToRemove = new List<object>();
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    var tag = navItem.Tag?.ToString();
                    if (tag?.StartsWith("Group_") == true || tag?.StartsWith("StandaloneList_") == true || tag == "CustomEmptyIcon")
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }
            foreach (var item in itemsToRemove)
            {
                NavView.MenuItems.Remove(item);
            }

            var hasCustomItems = _standaloneLists.Count > 0 || CustomGroups.Count > 0;

            if (!hasCustomItems)
            {
                var emptyItem = new NavigationViewItem
                {
                    Tag = "CustomEmptyIcon",
                    IsEnabled = false,
                    SelectsOnInvoked = false
                };
                var emptyPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.12
                };
                var emptyIcon = new FontIcon { Glyph = "\uE8FD", FontSize = 28, Foreground = new SolidColorBrush(Colors.White), Margin = new Thickness(0, 20, 0, 20), HorizontalAlignment = HorizontalAlignment.Center };
                
                emptyPanel.Children.Add(emptyIcon);
                
                emptyItem.Content = emptyPanel;
                NavView.MenuItems.Add(emptyItem);
            }

            foreach (var list in _standaloneLists)
            {
                var listItem = new NavigationViewItem
                {
                    Content = list.Name,
                    Tag = $"StandaloneList_{list.Id}"
                };
                listItem.Icon = AppIcons.Create(AppIcons.TaskList, 16);
                listItem.ContextFlyout = CreateListContextFlyout(list);
                NavView.MenuItems.Add(listItem);
            }

            foreach (var group in CustomGroups)
            {
                var groupItem = new NavigationViewItem
                {
                    Content = group.Name,
                    Tag = $"Group_{group.Id}",
                    IsExpanded = group.IsExpanded
                };
                groupItem.Icon = AppIcons.Create(AppIcons.Group, 16);
                groupItem.ContextFlyout = CreateGroupContextFlyout(group);

                foreach (var list in group.Lists)
                {
                    var listItem = new NavigationViewItem
                    {
                        Content = list.Name,
                        Tag = $"List_{list.Id}"
                    };
                    listItem.Icon = AppIcons.Create(AppIcons.TaskList, 16);
                    listItem.ContextFlyout = CreateListContextFlyout(list);
                    groupItem.MenuItems.Add(listItem);
                }

                NavView.MenuItems.Add(groupItem);
            }
        }

        private MenuFlyout CreateListContextFlyout(TaskList list)
        {
            var flyout = new MenuFlyout();

            var renameItem = new MenuFlyoutItem { Text = "重命名", Icon = new SymbolIcon(Symbol.Rename) };
            renameItem.Click += (s, e) => ShowRenameListDialog(list);
            flyout.Items.Add(renameItem);

            var moveToItem = new MenuFlyoutSubItem { Text = "移至分组" };
            moveToItem.Icon = new FontIcon { Glyph = "\uE8C6" };

            var noGroupItem = new ToggleMenuFlyoutItem { Text = "（独立列表）", IsChecked = list.GroupId == null };
            noGroupItem.Click += (s, e) => MoveListToGroup(list, null);
            moveToItem.Items.Add(noGroupItem);

            var groups = _dbService.GetGroups();
            foreach (var group in groups)
            {
                var groupItem = new ToggleMenuFlyoutItem { Text = group.Name, IsChecked = list.GroupId == group.Id };
                groupItem.Click += (s, e) => MoveListToGroup(list, group.Id);
                moveToItem.Items.Add(groupItem);
            }

            flyout.Items.Add(moveToItem);

            var deleteItem = new MenuFlyoutItem { Text = "删除", Icon = new SymbolIcon(Symbol.Delete) };
            deleteItem.Click += (s, e) => ShowDeleteListDialog(list);
            flyout.Items.Add(deleteItem);

            return flyout;
        }

        private void MoveListToGroup(TaskList list, int? groupId)
        {
            _dbService.MoveListToGroup(list.Id, groupId);
            LoadCustomGroups();
        }

        private MenuFlyout CreateGroupContextFlyout(TaskGroup group)
        {
            var flyout = new MenuFlyout();

            var renameItem = new MenuFlyoutItem { Text = "重命名", Icon = new SymbolIcon(Symbol.Rename) };
            renameItem.Click += (s, e) => ShowRenameGroupDialog(group);
            flyout.Items.Add(renameItem);

            var deleteItem = new MenuFlyoutItem { Text = "删除", Icon = new SymbolIcon(Symbol.Delete) };
            deleteItem.Click += (s, e) => ShowDeleteGroupDialog(group);
            flyout.Items.Add(deleteItem);

            return flyout;
        }

        private async void ShowRenameListDialog(TaskList list)
        {
            var textBox = new TextBox
            {
                Text = list.Name,
                PlaceholderText = "输入列表名称",
                SelectionStart = 0,
                SelectionLength = list.Name.Length
            };

            var dialog = new ContentDialog
            {
                Title = "重命名列表",
                Content = textBox,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                _dbService.UpdateListName(list.Id, textBox.Text.Trim());
                list.Name = textBox.Text.Trim();
                LoadCustomGroups();
            }
        }

        private async void ShowDeleteListDialog(TaskList list)
        {
            var dialog = new ContentDialog
            {
                Title = "删除列表",
                Content = $"确定要删除列表\"{list.Name}\"及其所有任务吗？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _dbService.DeleteList(list.Id);
                LoadCustomGroups();
                if (_currentListId == list.Id)
                {
                    _currentNavTag = "Important";
                    _currentListId = null;
                    NavView.SelectedItem = NavView.MenuItems[1];
                    LoadTasksForCurrentNav();
                    UpdatePageHeader();
                }
            }
        }

        private async void ShowRenameGroupDialog(TaskGroup group)
        {
            var textBox = new TextBox
            {
                Text = group.Name,
                PlaceholderText = "输入分组名称",
                SelectionStart = 0,
                SelectionLength = group.Name.Length
            };

            var dialog = new ContentDialog
            {
                Title = "重命名分组",
                Content = textBox,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                _dbService.UpdateGroupName(group.Id, textBox.Text.Trim());
                group.Name = textBox.Text.Trim();
                LoadCustomGroups();
            }
        }

        private async void ShowDeleteGroupDialog(TaskGroup group)
        {
            var dialog = new ContentDialog
            {
                Title = "删除分组",
                Content = $"确定要删除分组\"{group.Name}\"吗？分组内的列表将变为独立列表。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _dbService.DeleteGroup(group.Id);
                LoadCustomGroups();
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

        private void NavView_PaneOpened(NavigationView sender, object args)
        {
            PaneFooterGrid.Opacity = 1;
            PaneFooterGrid.IsHitTestVisible = true;
        }

        private void NavView_PaneClosed(NavigationView sender, object args)
        {
            PaneFooterGrid.Opacity = 0;
            PaneFooterGrid.IsHitTestVisible = false;
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            EnsureNotepadPreviewMode();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            EnsureNotepadPreviewMode();

            if (_isDrawerOpen)
            {
                CloseDrawer(false);
            }

            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                
                if (tag?.StartsWith("StandaloneList_") == true)
                {
                    var idStr = tag.Substring("StandaloneList_".Length);
                    if (int.TryParse(idStr, out int listId))
                    {
                        _currentNavTag = "StandaloneList";
                        _currentListId = listId;
                    }
                    PageTitle.Text = item.Content?.ToString() ?? "列表";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.TaskList));
                    ShowTaskListContent();
                    LoadTasksForCurrentNav();
                    return;
                }

                if (tag?.StartsWith("List_") == true)
                {
                    var idStr = tag.Substring("List_".Length);
                    if (int.TryParse(idStr, out int listId))
                    {
                        _currentNavTag = "GroupList";
                        _currentListId = listId;
                    }
                    PageTitle.Text = item.Content?.ToString() ?? "列表";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.TaskList));
                    ShowTaskListContent();
                    LoadTasksForCurrentNav();
                    return;
                }
                
                if (tag?.StartsWith("Group_") == true)
                {
                    var idStr = tag.Substring("Group_".Length);
                    if (int.TryParse(idStr, out int groupId))
                    {
                        _currentNavTag = "Group";
                        _currentListId = groupId;
                    }
                    PageTitle.Text = item.Content?.ToString() ?? "分组";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Group));
                    ShowTaskListContent();
                    LoadTasksForCurrentNav();
                    return;
                }
                
                _currentListId = null;

                switch (tag)
                {
                    case "Notepad":
                        _currentNavTag = "Notepad";
                        PageTitle.Text = "记事本";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Notepad));
                        ShowNotepadContent();
                        break;
                    case "Important":
                        _currentNavTag = "Important";
                        PageTitle.Text = "重要任务";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Important));
                        ShowTaskListContent();
                        LoadTasksForCurrentNav();
                        break;
                    case "Daily":
                        _currentNavTag = "Daily";
                        PageTitle.Text = _dbService.GetBuiltInListByCategory(ListCategory.Daily)?.Name ?? "日常";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Daily));
                        ShowTaskListContent();
                        LoadTasksForCurrentNav();
                        break;
                    case "Weekly":
                        _currentNavTag = "Weekly";
                        PageTitle.Text = _dbService.GetBuiltInListByCategory(ListCategory.Weekly)?.Name ?? "周常";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Weekly));
                        ShowTaskListContent();
                        LoadTasksForCurrentNav();
                        break;
                    case "Monthly":
                        _currentNavTag = "Monthly";
                        PageTitle.Text = _dbService.GetBuiltInListByCategory(ListCategory.Monthly)?.Name ?? "月常";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Monthly));
                        ShowTaskListContent();
                        LoadTasksForCurrentNav();
                        break;
                }
            }

            UpdatePinButtonState();
        }

        private void UpdatePageHeader()
        {
            switch (_currentNavTag)
            {
                case "Notepad":
                    PageTitle.Text = "记事本";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Notepad));
                    break;
                case "Important":
                    PageTitle.Text = "重要任务";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Important));
                    break;
                case "Daily":
                    PageTitle.Text = _dbService.GetBuiltInListByCategory(ListCategory.Daily)?.Name ?? "日常";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Daily));
                    break;
                case "Weekly":
                    PageTitle.Text = _dbService.GetBuiltInListByCategory(ListCategory.Weekly)?.Name ?? "周常";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Weekly));
                    break;
                case "Monthly":
                    PageTitle.Text = _dbService.GetBuiltInListByCategory(ListCategory.Monthly)?.Name ?? "月常";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Monthly));
                    break;
                case "StandaloneList":
                case "GroupList":
                    PageTitle.Text = "列表";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.TaskList));
                    break;
                case "Group":
                    PageTitle.Text = "分组";
                    PageIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(AppIcons.Group));
                    break;
            }
        }

        private void TaskBorder_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                if (_borderSubscriptions.ContainsKey(task))
                {
                    _borderSubscriptions.Remove(task);
                }

                PropertyChangedEventHandler handler = (s, args) =>
                {
                    if (args.PropertyName == nameof(TaskItem.IsSelected))
                    {
                        UpdateBorderBackground(border, task);
                    }
                };
                task.PropertyChanged += handler;
                _borderSubscriptions[task] = (border, handler);
                UpdateBorderBackground(border, task);
            }
        }

        private void TaskBorder_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                if (_borderSubscriptions.TryGetValue(task, out var entry))
                {
                    task.PropertyChanged -= entry.handler;
                    _borderSubscriptions.Remove(task);
                }
            }
        }

        private void UpdateBorderBackground(Border border, TaskItem task)
        {
            border.Background = task.IsSelected
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x2a, 0x2a, 0x2a))
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x1e, 0x1e, 0x1e));
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
                    UpdateTaskItemSelection(task);
                }
                e.Handled = true;
            }
        }

        private void TaskItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                if (task.IsSelected)
                {
                    return;
                }
                
                border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 0x25, 0x25, 0x25));
            }
        }

        private void TaskItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                UpdateBorderBackground(border, task);
            }
        }

        private void UpdateTaskItemSelection(TaskItem? selectedTask)
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
                ReminderService.Instance.RemoveScheduledReminderNotifications(task.Id);
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
            if (_drawerStoryboard != null)
            {
                _drawerStoryboard.Stop();
                DrawerTranslateTransform.X = 0;
                DetailDrawer.Opacity = 1;
            }

            _selectedTask = task;
            DetailTitle.Text = task.Title;
            // 将描述中的 [title](url) 转为纯标题显示
            var (displayText, links) = StripLinksForDisplay(task.Description ?? "");
            _isUpdatingDescriptionText = true;
            DetailDescription.Text = displayText;
            _descriptionLinks = links;
            _previousDescriptionText = displayText;
            _isUpdatingDescriptionText = false;
            
            var subTasks = _dbService.GetSubTasksForTask(task.Id);
            _selectedTask.SubTasks.Clear();
            foreach (var subTask in subTasks)
            {
                _selectedTask.SubTasks.Add(subTask);
            }
            SubTasksList.ItemsSource = task.SubTasks;
            
            RefreshSelectedTaskDueDateControls();
            
            var reminders = _dbService.GetRemindersForTask(task.Id);
            task.Reminders.Clear();
            foreach (var reminder in reminders)
            {
                task.Reminders.Add(reminder);
            }
            RefreshSelectedTaskReminderControls();
            
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

            DetailCalendarView.Visibility = Visibility.Collapsed;
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            ImportantToggle.IsOn = task.IsImportant;

            if (_isDrawerOpen)
            {
                DrawerScrollViewer.ChangeView(null, 0, null);
                return;
            }

            DetailDrawer.Visibility = Visibility.Visible;
            DrawerTranslateTransform.X = 380;
            DetailDrawer.Opacity = 0;

            _drawerStoryboard = new Storyboard();

            var slideIn = new DoubleAnimation
            {
                From = 380,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideIn, DrawerTranslateTransform);
            Storyboard.SetTargetProperty(slideIn, "X");
            _drawerStoryboard.Children.Add(slideIn);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, DetailDrawer);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            _drawerStoryboard.Children.Add(fadeIn);

            _drawerStoryboard.Begin();

            _isDrawerOpen = true;
        }
        
        private void NotifyTaskItemChanged(TaskItem task)
        {
            TaskItemPropertyChanged?.Invoke(task, new System.ComponentModel.PropertyChangedEventArgs("SubTasks"));
        }

        private void RefreshSelectedTaskDueDateControls()
        {
            if (_selectedTask == null) return;

            DueDateText.Text = _selectedTask.DueDate.HasValue
                ? _selectedTask.DueDate.Value.ToString("M月d日")
                : "截止日期";
            ClearDueDateButton.Visibility = _selectedTask.DueDate.HasValue
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshSelectedTaskReminderControls()
        {
            if (_selectedTask == null) return;

            // 只显示用户自定的 Custom 提醒，不显示 Deadline 默认提醒
            var customReminders = _selectedTask.Reminders
                .Where(r => r.ReminderType != ReminderType.Deadline && r.ReminderDateTime.HasValue)
                .OrderBy(r => r.ReminderDateTime)
                .ToList();

            var firstReminder = customReminders.FirstOrDefault();

            ReminderText.Text = firstReminder?.ReminderDateTime?.ToString("M月d日 HH:mm") ?? "提醒我";
            ClearReminderButton.Visibility = firstReminder != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            // 默认截止日提醒开关：有截止日才显示
            if (_selectedTask.DueDate.HasValue)
            {
                DeadlineReminderToggle.Visibility = Visibility.Visible;
                DeadlineReminderToggle.IsChecked = ReminderService.Instance.HasDeadlineReminders(_selectedTask.Id);
                UpdateDeadlineReminderToggleAppearance();
            }
            else
            {
                DeadlineReminderToggle.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshReminderSelectedDatesText()
        {
            if (ReminderSelectedDatesText == null || ReminderCalendarView == null)
            {
                return;
            }

            var selectedDates = ReminderCalendarView.SelectedDates
                .Select(date => date.DateTime.Date)
                .OrderBy(date => date)
                .ToList();

            ReminderSelectedDatesText.Text = selectedDates.Count == 0
                ? "未选择日期"
                : $"已选择：{string.Join("、", selectedDates.Select(date => date.ToString("M月d日")))}";
        }

        private void CloseDrawer(bool animate = true)
        {
            if (_selectedTask != null) _selectedTask.IsSelected = false;

            if (_drawerStoryboard != null)
            {
                _drawerStoryboard.Stop();
                DrawerTranslateTransform.X = 0;
                DetailDrawer.Opacity = 1;
            }

            if (!animate)
            {
                DetailDrawer.Visibility = Visibility.Collapsed;
                DrawerTranslateTransform.X = 380;
                DetailDrawer.Opacity = 0;
                _isDrawerOpen = false;
                _selectedTask = null;
                return;
            }

            _drawerStoryboard = new Storyboard();

            var slideOut = new DoubleAnimation
            {
                From = 0,
                To = 380,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideOut, DrawerTranslateTransform);
            Storyboard.SetTargetProperty(slideOut, "X");
            _drawerStoryboard.Children.Add(slideOut);

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, DetailDrawer);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            _drawerStoryboard.Children.Add(fadeOut);

            _drawerStoryboard.Completed += (s, e) =>
            {
                DetailDrawer.Visibility = Visibility.Collapsed;
                DrawerTranslateTransform.X = 380;
                DetailDrawer.Opacity = 0;
                _isDrawerOpen = false;
                _selectedTask = null;
            };
            _drawerStoryboard.Begin();
        }

        private void CloseDrawer_Click(object sender, RoutedEventArgs e)
        {
            CloseDrawer();
        }

        private void ContentArea_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isDrawerOpen)
            {
                CloseDrawer();
            }
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
            if (_isUpdatingDescriptionText) return;
            if (_selectedTask == null) return;

            // 检测是否有人工粘贴了 [title](url) 格式，自动转为纯标题
            var (displayText, links) = StripLinksForDisplay(DetailDescription.Text);
            if (displayText != DetailDescription.Text)
            {
                _isUpdatingDescriptionText = true;
                DetailDescription.Text = displayText;
                DetailDescription.SelectionStart = displayText.Length;
                _isUpdatingDescriptionText = false;
            }
            _descriptionLinks = links;

            // 根据文本变更调整链接位置
            if (!string.IsNullOrEmpty(_previousDescriptionText))
            {
                var delta = displayText.Length - _previousDescriptionText.Length;
                var changePos = DetailDescription.SelectionStart;
                if (delta != 0)
                {
                    for (int i = 0; i < _descriptionLinks.Count; i++)
                    {
                        var l = _descriptionLinks[i];
                        if (l.displayIndex >= changePos)
                        {
                            _descriptionLinks[i] = (l.title, l.url, l.displayIndex + delta, l.displayLength);
                        }
                    }
                }
            }
            _previousDescriptionText = displayText;

            // 保存时用带链接的完整文本
            var rawText = ReconstructLinksForStorage(displayText, links);
            _selectedTask.Description = rawText;

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


        private void DetailDescription_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Ctrl+Enter: 打开光标处的链接
            if (e.Key == Windows.System.VirtualKey.Enter && IsCtrlPressed())
            {
                var link = GetLinkAtPosition(DetailDescription.SelectionStart);
                if (link != null)
                {
                    OpenUrl(link.Value.url);
                    e.Handled = true;
                }
            }
        }

        private void DetailDescription_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var link = GetLinkAtPosition(DetailDescription.SelectionStart);
            if (link == null) return;

            var menu = new MenuFlyout();

            var openItem = new MenuFlyoutItem
            {
                Text = "打开链接",
                Icon = new FontIcon { Glyph = "" }
            };
            var url = link.Value.url;
            openItem.Click += (s, args) => OpenUrl(url);
            menu.Items.Add(openItem);

            var copyItem = new MenuFlyoutItem
            {
                Text = "复制链接",
                Icon = new SymbolIcon(Symbol.Copy)
            };
            copyItem.Click += (s, args) =>
            {
                try
                {
                    var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dp.SetText(url);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Copy link error: {ex.Message}");
                }
            };
            menu.Items.Add(copyItem);

            menu.ShowAt(DetailDescription, e.GetPosition(DetailDescription));
            e.Handled = true;
        }

        /// <summary>
        /// 获取光标位置所在的链接。返回 (title, url) 或 null。
        /// </summary>
        private (string title, string url)? GetLinkAtPosition(int caretIndex)
        {
            foreach (var link in _descriptionLinks)
            {
                if (caretIndex > link.displayIndex && caretIndex <= link.displayIndex + link.displayLength)
                {
                    return (link.title, link.url);
                }
            }
            return null;
        }

        private static async void OpenUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenUrl error: {ex.Message}");
            }
        }

        /// <summary>
        /// 从存储格式 (含 [title](url)) 转为显示格式 (纯标题)，
        /// 同时返回链接映射列表。
        /// </summary>
        private static (string displayText, List<(string title, string url, int displayIndex, int displayLength)> links)
            StripLinksForDisplay(string rawText)
        {
            var links = new List<(string title, string url, int displayIndex, int displayLength)>();
            if (string.IsNullOrEmpty(rawText))
                return ("", links);

            var displayText = "";
            var regex = new System.Text.RegularExpressions.Regex(@"\[(.+?)\]\((.+?)\)");
            var lastIndex = 0;

            foreach (System.Text.RegularExpressions.Match match in regex.Matches(rawText))
            {
                displayText += rawText.Substring(lastIndex, match.Index - lastIndex);
                var title = match.Groups[1].Value;
                var url = match.Groups[2].Value;
                links.Add((title, url, displayText.Length, title.Length));
                displayText += title;
                lastIndex = match.Index + match.Length;
            }
            displayText += rawText.Substring(lastIndex);

            return (displayText, links);
        }

        /// <summary>
        /// 从显示文本 + 链接映射重建存储格式 (含 [title](url))。
        /// </summary>
        private static string ReconstructLinksForStorage(string displayText,
            List<(string title, string url, int displayIndex, int displayLength)> links)
        {
            if (links.Count == 0) return displayText;

            // 按位置排序 (从后往前插入，避免坐标偏移)
            var sorted = links.OrderBy(l => l.displayIndex).ToList();
            var result = "";
            var cursor = 0;

            foreach (var link in sorted)
            {
                if (link.displayIndex < cursor) continue; // 跳过重叠/无效链接
                result += displayText.Substring(cursor, link.displayIndex - cursor);
                result += $"[{link.title}]({link.url})";
                cursor = link.displayIndex + link.displayLength;
            }
            result += displayText.Substring(cursor);
            return result;
        }

        private void ScrollToElement(FrameworkElement element)
        {
            if (DrawerScrollViewer != null && element != null)
            {
                DrawerScrollViewer.UpdateLayout();
                var transform = element.TransformToVisual(DrawerScrollViewer);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                DrawerScrollViewer.ChangeView(null, position.Y - 50, null, false);
            }
        }

        private void DetailDatePicker_DateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (_selectedTask != null && args.NewDate != null)
            {
                _selectedTask.DueDate = args.NewDate.Value.DateTime;
            }
        }

        private void ReminderService_TaskCompletedFromNotification(object? sender, TaskCompletedFromNotificationEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() => CompleteTaskInUi(args.CompletedTaskId, args.NewTaskId));
        }

        private void OnDateChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var task in Tasks)
                    task.RefreshDateDisplay();
                foreach (var task in CompletedTasks)
                    task.RefreshDateDisplay();
            });
        }

        private void CompleteTaskInUi(int taskId, int? newTaskId)
        {
            var task = Tasks.FirstOrDefault(item => item.Id == taskId);
            if (task == null)
            {
                task = CompletedTasks.FirstOrDefault(item => item.Id == taskId);
            }

            if (task != null)
            {
                task.IsChecked = true;
                task.CompletedAt = DateTime.Now;
                // 从数据库中刷新 IsAutoCompleted 状态
                var dbTask = _dbService.GetTaskById(taskId);
                if (dbTask != null)
                {
                    task.IsAutoCompleted = dbTask.IsAutoCompleted;
                }
                Tasks.Remove(task);
                if (!CompletedTasks.Contains(task))
                {
                    CompletedTasks.Add(task);
                }
            }

            if (newTaskId.HasValue &&
                Tasks.All(item => item.Id != newTaskId.Value) &&
                CompletedTasks.All(item => item.Id != newTaskId.Value))
            {
                var newTask = _dbService.GetTaskById(newTaskId.Value);
                if (newTask != null && !newTask.IsChecked)
                {
                    foreach (var subTask in _dbService.GetSubTasksForTask(newTask.Id))
                    {
                        newTask.SubTasks.Add(subTask);
                    }

                    Tasks.Add(newTask);
                }
            }

            if (_selectedTask?.Id == taskId)
            {
                CloseDrawer();
            }

            UpdateTaskItemSelection(null);
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

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TaskItem task)
            {
                _dbService.UpdateTaskChecked(task.Id, cb.IsChecked ?? false);
                
                if (cb.IsChecked ?? false)
                {
                    ReminderService.Instance.RemoveScheduledReminderNotifications(task.Id);
                    ReminderService.Instance.ShowTaskCompletedNotification(task.Title);

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
                        
                        Tasks.Add(newTask);
                        ReminderService.Instance.ScheduleReminderNotificationsForTask(newTask.Id);
                    }
                    
                    if (Tasks.Contains(task))
                    {
                        Tasks.Remove(task);
                        CompletedTasks.Add(task);
                    }
                    
                    _selectedTask = null;
                    UpdateTaskItemSelection(null);
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
            ResetPendingDueDate();
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

        private void AddTaskTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 延迟判断：如果焦点仍在 AddTaskInputArea 内（如点选日期），不收起
            DispatcherQueue.TryEnqueue(() =>
            {
                var focused = FocusManager.GetFocusedElement(AddTaskTextBox.XamlRoot) as DependencyObject;
                if (focused != null && IsElementInsideAddTaskInputArea(focused))
                    return;

                if (string.IsNullOrWhiteSpace(AddTaskTextBox.Text) && _pendingDueDate == null
                    && !_isDatePickerOpen && !_isRecurrenceMenuOpen)
                {
                    HideAddTaskInput();
                }
            });
        }

        private bool IsElementInsideAddTaskInputArea(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current == AddTaskInputArea) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void AddTaskFromInput()
        {
            if (!string.IsNullOrWhiteSpace(AddTaskTextBox.Text))
            {
                int? listId = null;
                bool isImportant = false;

                switch (_currentNavTag)
                {
                    case "Important":
                        isImportant = true;
                        break;
                    case "Daily":
                    case "Weekly":
                    case "Monthly":
                    case "StandaloneList":
                    case "GroupList":
                        listId = _currentListId;
                        break;
                }

                var newTask = _dbService.AddTask(AddTaskTextBox.Text.Trim(), _pendingDueDate?.DateTime.Date, null, listId, isImportant);
                var subTasks = _dbService.GetSubTasksForTask(newTask.Id);
                foreach (var subTask in subTasks)
                {
                    newTask.SubTasks.Add(subTask);
                }
                Tasks.Add(newTask);

                // 自动生成截止日默认提醒
                if (_pendingDueDate.HasValue)
                {
                    ReminderService.Instance.EnsureDeadlineReminders(newTask.Id, _pendingDueDate.Value.DateTime.Date);
                }

                // 设置了重复
                if (_pendingRecurrence != RecurrenceType.None)
                {
                    var baseDate = _pendingDueDate?.DateTime.Date ?? DateTime.Today;
                    var recurrence = _dbService.AddRecurrence(newTask.Id, _pendingRecurrence, baseDate);
                    recurrence.NextDueDate = _dbService.CalculateNextRecurrenceDate(_pendingRecurrence, baseDate);
                    _dbService.UpdateRecurrence(recurrence);
                }
            }
            HideAddTaskInput();
        }

        private void HideAddTaskInput()
        {
            AddTaskInputArea.Visibility = Visibility.Collapsed;
            AddTaskButton.Visibility = Visibility.Visible;
            AddTaskTextBox.Text = "";
            ResetPendingDueDate();
        }

        private void ResetPendingDueDate()
        {
            _pendingDueDate = null;
            // SVG icon, color is baked in
            AddTaskDueDateText.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(AddTaskDueDateButton, "设置截止日期");
        }

        private void AddTaskDueDate_Click(object sender, RoutedEventArgs e)
        {
            // Flyout 会自动打开，无需额外处理
        }

        private void AddTaskCalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (sender.SelectedDates.Count > 0)
            {
                _pendingDueDate = sender.SelectedDates[0];
                // SVG icon, color is baked in
                AddTaskDueDateText.Text = _pendingDueDate.Value.ToString("MM/dd");
                AddTaskDueDateText.Visibility = Visibility.Visible;
                ToolTipService.SetToolTip(AddTaskDueDateButton, $"截止日期: {_pendingDueDate.Value:yyyy/MM/dd}");
                AddTaskDueDateButton.Flyout.Hide();
            }
        }

        private void AddTaskClearDate_Click(object sender, RoutedEventArgs e)
        {
            ResetPendingDueDate();
            AddTaskCalendarView.SelectedDates.Clear();
        }

        private void ToggleCompleted_Click(object sender, RoutedEventArgs e)
        {
            _showCompleted = !_showCompleted;
            if (_showCompleted)
            {
                AnimateExpand(CompletedTasksList, CompletedListTransform);
            }
            else
            {
                AnimateSlideCollapse(CompletedTasksList, CompletedListTransform);
            }
            CompletedArrow.Glyph = _showCompleted ? "\uE70E" : "\uE70D";
        }

        private void AnimateExpand(FrameworkElement element, TranslateTransform transform)
        {
            element.Visibility = Visibility.Visible;
            element.Opacity = 0;
            transform.Y = -12;

            var storyboard = new Storyboard();

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnim, element);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);

            var slideAnim = new DoubleAnimation
            {
                From = -12,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideAnim, transform);
            Storyboard.SetTargetProperty(slideAnim, "Y");
            storyboard.Children.Add(slideAnim);

            storyboard.Begin();
        }

        private void AnimateSlideCollapse(FrameworkElement element, TranslateTransform transform)
        {
            var storyboard = new Storyboard();

            var opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnim, element);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);

            var slideAnim = new DoubleAnimation
            {
                From = 0,
                To = -12,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideAnim, transform);
            Storyboard.SetTargetProperty(slideAnim, "Y");
            storyboard.Children.Add(slideAnim);

            storyboard.Completed += (s, e) =>
            {
                element.Visibility = Visibility.Collapsed;
                element.Opacity = 0;
                transform.Y = 0;
            };

            storyboard.Begin();
        }

        private void NewGroup_Click(object sender, RoutedEventArgs e)
        {
            var newGroup = _dbService.AddGroup("新建分组");
            CustomGroups.Add(newGroup);
            RefreshCustomNavigation();
        }

        private void NewList_Click(object sender, RoutedEventArgs e)
        {
            var newList = _dbService.AddListStandalone("新建列表");
            LoadCustomGroups();
            
            var newItem = FindNavItemByTag($"StandaloneList_{newList.Id}");
            if (newItem != null)
            {
                NavView.SelectedItem = newItem;
            }
        }

        private NavigationViewItem? FindNavItemByTag(string tag)
        {
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == tag)
                {
                    return navItem;
                }
                if (item is NavigationViewItem parentItem && parentItem.MenuItems != null)
                {
                    foreach (var child in parentItem.MenuItems)
                    {
                        if (child is NavigationViewItem childItem && childItem.Tag?.ToString() == tag)
                        {
                            return childItem;
                        }
                    }
                }
            }
            return null;
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

        private Timer? _subTaskTitleTimer;

        private void SubTaskTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            _subTaskTitleTimer?.Dispose();
            _subTaskTitleTimer = new Timer(state =>
            {
                if (sender is TextBox tb && tb.DataContext is SubTask subTask)
                {
                    _dbService.UpdateSubTaskTitle(subTask.Id, tb.Text);
                }
            }, null, 300, Timeout.Infinite);
        }

        private void SubTaskTitle_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SubTask subTask)
            {
                _dbService.UpdateSubTaskTitle(subTask.Id, tb.Text);
            }
        }

        private void SubTask_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var subTask = element.DataContext as SubTask;
                if (subTask == null) return;
                
                var menu = new MenuFlyout();
                var deleteItem = new MenuFlyoutItem() { Text = "删除", Icon = new SymbolIcon(Symbol.Delete) };
                deleteItem.Click += (s, args) =>
                {
                    if (_selectedTask != null)
                    {
                        _dbService.DeleteSubTask(subTask.Id);
                        _selectedTask.SubTasks.Remove(subTask);
                        SubTasksList.ItemsSource = null;
                        SubTasksList.ItemsSource = _selectedTask.SubTasks;
                    }
                };
                menu.Items.Add(deleteItem);
                menu.ShowAt(element, e.GetPosition(element));
                e.Handled = true;
            }
        }

        private void DeleteSubTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is SubTask subTask && _selectedTask != null)
                {
                    _dbService.DeleteSubTask(subTask.Id);
                    _selectedTask.SubTasks.Remove(subTask);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除子任务失败: {ex.Message}");
            }
        }

        private void ShowDueDatePicker_Click(object sender, RoutedEventArgs e)
        {
            if (DetailCalendarView.Visibility == Visibility.Visible)
            {
                DetailCalendarView.Visibility = Visibility.Collapsed;
                ScrollToElement(DueDateButton);
            }
            else
            {
                DetailCalendarView.Visibility = Visibility.Visible;
                var today = new DateTimeOffset(DateTime.Today);
                DetailCalendarView.MinDate = today;
                DetailCalendarView.SelectedDates.Clear();
                
                if (_selectedTask?.DueDate.HasValue == true && _selectedTask.DueDate.Value.Date >= DateTime.Today)
                {
                    DetailCalendarView.SetDisplayDate(_selectedTask.DueDate.Value);
                }
                else
                {
                    DetailCalendarView.SetDisplayDate(today);
                }
                
                ScrollToElement(DetailCalendarView);
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
                
                _selectedTask.DueDate = selectedDate.Date;
                RefreshSelectedTaskDueDateControls();

                _dbService.UpdateTask(_selectedTask);

                // 截止日期变更：重建默认提醒
                ReminderService.Instance.EnsureDeadlineReminders(_selectedTask.Id, selectedDate.Date);

                // 刷新提醒列表和 toggle 状态
                _selectedTask.Reminders.Clear();
                foreach (var r in _dbService.GetRemindersForTask(_selectedTask.Id))
                    _selectedTask.Reminders.Add(r);
                RefreshSelectedTaskReminderControls();

                DetailCalendarView.Visibility = Visibility.Collapsed;
                ScrollToElement(DueDateButton);
            }
        }
        
        private void ClearDueDate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _selectedTask.DueDate = null;
                RefreshSelectedTaskDueDateControls();
                
                _dbService.UpdateTask(_selectedTask);
            }
        }
        
        private void ShowReminderDialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReminderSettingsPanel.Visibility = ReminderSettingsPanel.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
                
                if (ReminderSettingsPanel.Visibility == Visibility.Collapsed)
                {
                    ScrollToElement(ReminderButton);
                    return;
                }
                
                if (ReminderCalendarView != null)
                {
                    var today = new DateTimeOffset(DateTime.Today);
                    ReminderCalendarView.MinDate = today;
                    ReminderCalendarView.SetDisplayDate(today);
                    ReminderCalendarView.SelectedDates.Clear();
                    RefreshReminderSelectedDatesText();
                }

                if (_selectedTask != null)
                {
                    // 提醒日历范围：今天 → 任务截止日
                    if (ReminderCalendarView != null)
                    {
                        if (_selectedTask.DueDate.HasValue && _selectedTask.DueDate.Value.Date >= DateTime.Today)
                        {
                            ReminderCalendarView.MaxDate = _selectedTask.DueDate.Value;
                            ReminderCalendarView.SetDisplayDate(_selectedTask.DueDate.Value);
                        }
                        else
                        {
                            ReminderCalendarView.MaxDate = DateTimeOffset.MaxValue; // 无截止日则不限制上限
                        }
                    }

                    var reminders = _dbService.GetRemindersForTask(_selectedTask.Id)
                        .Where(r => r.ReminderType != ReminderType.Deadline)
                        .ToList();
                    if (reminders.Count > 0 && ReminderCalendarView != null)
                    {
                        foreach (var reminder in reminders)
                        {
                            if (reminder.ReminderDateTime.HasValue)
                            {
                                if (reminder.ReminderDateTime.Value.Date >= DateTime.Today)
                                {
                                    ReminderCalendarView.SelectedDates.Add(reminder.ReminderDateTime.Value);
                                }
                                if (ReminderTimePicker != null)
                                {
                                    ReminderTimePicker.SelectedTime = reminder.ReminderDateTime.Value.TimeOfDay;
                                }
                            }
                        }
                        var firstReminder = reminders[0];
                        if (EnableSameDayReminderToggle != null)
                        {
                            EnableSameDayReminderToggle.IsOn = firstReminder.EnableMultiDayReminders;
                            SameDayIntervalComboBox.IsEnabled = firstReminder.EnableMultiDayReminders;
                        }
                        if (firstReminder.SameDayIntervalMinutes > 0 && SameDayIntervalComboBox != null)
                        {
                            for (int i = 0; i < SameDayIntervalComboBox.Items.Count; i++)
                            {
                                if (SameDayIntervalComboBox.Items[i] is ComboBoxItem item && 
                                    int.TryParse(item.Tag?.ToString(), out int minutes) && 
                                    minutes == firstReminder.SameDayIntervalMinutes)
                                {
                                    SameDayIntervalComboBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (ReminderTimePicker != null)
                        {
                            ReminderTimePicker.SelectedTime = TimeSpan.FromHours(9);
                        }
                        if (EnableSameDayReminderToggle != null)
                        {
                            EnableSameDayReminderToggle.IsOn = false;
                            SameDayIntervalComboBox.IsEnabled = false;
                        }
                        if (SameDayIntervalComboBox != null)
                        {
                            SameDayIntervalComboBox.SelectedIndex = 0;
                        }
                    }
                }

                RefreshReminderSelectedDatesText();
                
                if (ReminderSettingsPanel.Visibility == Visibility.Visible)
                {
                    ScrollToElement(ReminderSettingsPanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowReminderDialog error: {ex.Message}");
            }
        }

        private void ReminderCalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            RefreshReminderSelectedDatesText();
        }
        
        private void EnableSameDayReminderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (SameDayIntervalComboBox != null && EnableSameDayReminderToggle != null)
            {
                SameDayIntervalComboBox.IsEnabled = EnableSameDayReminderToggle.IsOn;
                if (!EnableSameDayReminderToggle.IsOn)
                {
                    SameDayIntervalComboBox.SelectedIndex = 0;
                }
            }
        }
        
        private void SameDayIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
        
        private void CancelReminderSettings_Click(object sender, RoutedEventArgs e)
        {
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            ScrollToElement(ReminderButton);
        }
        
        private void ConfirmReminderSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask != null)
                {
                    var selectedDates = ReminderCalendarView?.SelectedDates;
                    var reminderTime = ReminderTimePicker?.SelectedTime ?? TimeSpan.FromHours(9);
                    
                    System.Diagnostics.Debug.WriteLine($"ConfirmReminder: TaskId={_selectedTask.Id}, SelectedDates={selectedDates?.Count ?? 0}, Time={reminderTime}");
                    
                    // 只删除用户手动设置的 Custom 提醒，保留 Deadline 提醒
                    _dbService.DeleteRemindersByType(_selectedTask.Id, ReminderType.Custom);
                    _selectedTask.Reminders.Clear();
                    
                    if (selectedDates != null && selectedDates.Count > 0)
                    {
                        var savedReminderTimes = new List<DateTime>();
                        foreach (var date in selectedDates)
                        {
                            var reminderDateTime = new DateTime(
                                date.Year, date.Month, date.Day,
                                (int)reminderTime.TotalHours,
                                (int)(reminderTime.TotalMinutes % 60),
                                0);
                            
                            System.Diagnostics.Debug.WriteLine($"ConfirmReminder: Processing date={date:yyyy-MM-dd}, reminderDateTime={reminderDateTime:yyyy-MM-dd HH:mm}, now={DateTime.Now:yyyy-MM-dd HH:mm}");
                            
                            var reminder = new Reminder
                            {
                                TaskId = _selectedTask.Id,
                                ReminderType = ReminderType.Custom,
                                ReminderDateTime = reminderDateTime,
                                EnableMultiDayReminders = EnableSameDayReminderToggle?.IsOn ?? false,
                                SameDayIntervalMinutes = (EnableSameDayReminderToggle?.IsOn ?? false) && SameDayIntervalComboBox?.SelectedItem is ComboBoxItem item
                                    ? int.Parse(item.Tag?.ToString() ?? "0")
                                    : 0
                            };
                            
                            _dbService.AddReminderWithDetails(reminder);
                            _selectedTask.Reminders.Add(reminder);
                            savedReminderTimes.Add(reminderDateTime);
                            System.Diagnostics.Debug.WriteLine($"ConfirmReminder: Saved reminder Id={reminder.Id}, DateTime={reminderDateTime:yyyy-MM-dd HH:mm}");
                        }
                        
                        if (savedReminderTimes.Count > 0)
                        {
                            var firstReminderTime = savedReminderTimes.OrderBy(time => time).First();
                            if (ReminderText != null)
                            {
                                ReminderText.Text = savedReminderTimes.Count == 1
                                    ? firstReminderTime.ToString("M月d日 HH:mm")
                                    : $"已设置 {savedReminderTimes.Count} 个提醒";
                            }
                            if (ClearReminderButton != null)
                            {
                                ClearReminderButton.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            if (ReminderText != null)
                            {
                                ReminderText.Text = "提醒我";
                            }
                            if (ClearReminderButton != null)
                            {
                                ClearReminderButton.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                    else
                    {
                        if (ReminderText != null)
                        {
                            ReminderText.Text = "提醒我";
                        }
                        if (ClearReminderButton != null)
                        {
                            ClearReminderButton.Visibility = Visibility.Collapsed;
                        }
                    }
                    
                    if (ReminderSettingsPanel != null)
                    {
                        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
                        ScrollToElement(ReminderButton);
                    }

                    ReminderService.Instance.ResetNotifiedReminders();
                    ReminderService.Instance.ScheduleReminderNotificationsForTask(_selectedTask.Id);

                    // 用户自定了截止日当天的提醒 → 关闭默认截止日提醒
                    if (selectedDates != null && selectedDates.Count > 0 && _selectedTask.DueDate.HasValue)
                    {
                        var dueDate = _selectedTask.DueDate.Value.Date;
                        var hasReminderOnDueDate = selectedDates.Any(d => d.Date == dueDate);
                        if (hasReminderOnDueDate)
                        {
                            ReminderService.Instance.RemoveDeadlineReminders(_selectedTask.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfirmReminderSettings error: {ex.Message}");
            }
        }
        
        private void ToggleRecurringReminder_Click(object sender, RoutedEventArgs e)
        {
        }
        
        private void ClearReminder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                _dbService.DeleteRemindersByType(_selectedTask.Id, ReminderType.Custom);
                _selectedTask.Reminders.Clear();
                // 重新加载（Deadline 提醒还在）
                var reminders = _dbService.GetRemindersForTask(_selectedTask.Id);
                foreach (var r in reminders)
                    _selectedTask.Reminders.Add(r);
                RefreshSelectedTaskReminderControls();
                RecurringReminderButton.Visibility = Visibility.Collapsed;
                ReminderService.Instance.ResetNotifiedReminders();
                ReminderService.Instance.RemoveScheduledReminderNotifications(_selectedTask.Id);
                ReminderService.Instance.ScheduleReminderNotificationsForTask(_selectedTask.Id);
            }
        }

        private void DeadlineReminderToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask?.DueDate == null) return;

            if (DeadlineReminderToggle.IsChecked == true)
            {
                ReminderService.Instance.EnsureDeadlineReminders(_selectedTask.Id, _selectedTask.DueDate.Value, force: true);
            }
            else
            {
                ReminderService.Instance.RemoveDeadlineReminders(_selectedTask.Id);
            }

            UpdateDeadlineReminderToggleAppearance();

            var reminders = _dbService.GetRemindersForTask(_selectedTask.Id);
            _selectedTask.Reminders.Clear();
            foreach (var r in reminders)
                _selectedTask.Reminders.Add(r);
            RefreshSelectedTaskReminderControls();
        }

        private void UpdateDeadlineReminderToggleAppearance()
        {
            if (DeadlineReminderToggle.Content is FontIcon icon)
            {
                icon.Foreground = DeadlineReminderToggle.IsChecked == true
                    ? new SolidColorBrush(Microsoft.UI.Colors.Black)
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x88, 0x88, 0x88));
            }
        }

        private void AddTaskRecurrence_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            _isRecurrenceMenuOpen = true;
            flyout.Closed += (s, args) => _isRecurrenceMenuOpen = false;

            var noneItem = new MenuFlyoutItem { Text = "不重复", Icon = AppIcons.Create(AppIcons.RecurNone, 16) };
            noneItem.Click += (s, args) =>
            {
                _pendingRecurrence = RecurrenceType.None;
                ResetPendingRecurrence();
            };
            flyout.Items.Add(noneItem);

            var dailyItem = new MenuFlyoutItem { Text = "每天", Icon = AppIcons.Create(AppIcons.RecurDaily, 16) };
            dailyItem.Click += (s, args) => SetPendingRecurrence(RecurrenceType.Daily, "每天");

            var weeklyItem = new MenuFlyoutItem { Text = "每周", Icon = AppIcons.Create(AppIcons.RecurWeekly, 16) };
            weeklyItem.Click += (s, args) => SetPendingRecurrence(RecurrenceType.Weekly, "每周");

            var monthlyItem = new MenuFlyoutItem { Text = "每月", Icon = AppIcons.Create(AppIcons.RecurMonthly, 16) };
            monthlyItem.Click += (s, args) => SetPendingRecurrence(RecurrenceType.Monthly, "每月");

            var yearlyItem = new MenuFlyoutItem { Text = "每年", Icon = AppIcons.Create(AppIcons.RecurYearly, 16) };
            yearlyItem.Click += (s, args) => SetPendingRecurrence(RecurrenceType.Yearly, "每年");

            flyout.Items.Add(dailyItem);
            flyout.Items.Add(weeklyItem);
            flyout.Items.Add(monthlyItem);
            flyout.Items.Add(yearlyItem);

            flyout.ShowAt(AddTaskRecurrenceButton);
        }

        private void SetPendingRecurrence(RecurrenceType type, string label)
        {
            _pendingRecurrence = type;
            AddTaskRecurrenceIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
            AddTaskRecurrenceText.Text = label;
            AddTaskRecurrenceText.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(AddTaskRecurrenceButton, $"重复: {label}");
        }

        private void ResetPendingRecurrence()
        {
            _pendingRecurrence = RecurrenceType.None;
            AddTaskRecurrenceIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 85, 85, 85));
            AddTaskRecurrenceText.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(AddTaskRecurrenceButton, "设置重复");
        }


        private void ShowRecurrenceMenu_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            
            var dailyItem = new MenuFlyoutItem { Text = "每天", Icon = AppIcons.Create(AppIcons.RecurDaily, 16) };
            dailyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Daily);
            
            var weeklyItem = new MenuFlyoutItem { Text = "每周", Icon = AppIcons.Create(AppIcons.RecurWeekly, 16) };
            weeklyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Weekly);
            
            var monthlyItem = new MenuFlyoutItem { Text = "每月", Icon = AppIcons.Create(AppIcons.RecurMonthly, 16) };
            monthlyItem.Click += (s, args) => SetRecurrence(RecurrenceType.Monthly);
            
            var yearlyItem = new MenuFlyoutItem { Text = "每年", Icon = AppIcons.Create(AppIcons.RecurYearly, 16) };
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

        private void ImportantToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null && sender is ToggleSwitch toggle)
            {
                _selectedTask.IsImportant = toggle.IsOn;
                _dbService.UpdateTaskImportant(_selectedTask.Id, toggle.IsOn);
            }
        }

        private CompactWindow? _taskCompactWindow;
        private NotepadCompactWindow? _notepadCompactWindow;
        private string? _taskCompactPageTag;

        private bool IsCurrentPageCompactSupported =>
            _currentNavTag is "Important" or "Daily" or "Weekly" or "Monthly"
                or "StandaloneList" or "GroupList" or "Group" or "Notepad";

        private bool IsCurrentPageCompactOpen =>
            _currentNavTag == "Notepad" ? _notepadCompactWindow != null : _taskCompactWindow != null && _taskCompactPageTag == _currentNavTag;

        private void ToggleDesktopMode_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentPageCompactSupported) return;

            if (IsCurrentPageCompactOpen)
            {
                ExitPinnedMode();
            }
            else
            {
                EnterPinnedMode();
            }
        }

        private void EnterPinnedMode()
        {
            if (IsCurrentPageCompactOpen) return;

            // 已有任务紧凑窗口但属于其他页面 → 重新归属到当前页面
            if (_currentNavTag != "Notepad" && _taskCompactWindow != null)
            {
                _taskCompactPageTag = _currentNavTag;
                UpdatePinButtonState();
                SaveCompactState();
                return;
            }

            var yOffset = 40;
            if (_taskCompactWindow != null && _currentNavTag == "Notepad")
                yOffset = 40 + 480 + 12;
            else if (_notepadCompactWindow != null && _currentNavTag != "Notepad")
                yOffset = 40 + 480 + 12;

            if (_currentNavTag == "Notepad")
            {
                _notepadCompactWindow = new NotepadCompactWindow(_dbService, _notepadTabs, yOffset);
                _notepadCompactWindow.HeightChanged += OnCompactWindowHeightChanged;
                _notepadCompactWindow.ExitRequested += () =>
                {
                    _notepadCompactWindow?.Close();
                    _notepadCompactWindow = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                    SaveCompactState();
                };
                _notepadCompactWindow.Closed += (s, e) =>
                {
                    _notepadCompactWindow = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                };
                _notepadCompactWindow.Activate();
            }
            else
            {
                _taskCompactWindow = new CompactWindow(Tasks, CompletedTasks, _dbService, yOffset);
                _taskCompactPageTag = _currentNavTag;
                _taskCompactWindow.HeightChanged += OnCompactWindowHeightChanged;
                _taskCompactWindow.ExitRequested += () =>
                {
                    _taskCompactWindow?.Close();
                    _taskCompactWindow = null;
                    _taskCompactPageTag = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                    SaveCompactState();
                };
                _taskCompactWindow.Closed += (s, e) =>
                {
                    _taskCompactWindow = null;
                    _taskCompactPageTag = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                };
                _taskCompactWindow.Activate();
            }

            UpdatePinButtonState();
            SaveCompactState();
        }

        private void OnCompactWindowHeightChanged(int newHeight)
        {
            RepositionCompactWindows();
        }

        private void RepositionCompactWindows()
        {
            const int x = 1500;
            const int topY = 40;
            const int gap = 12;
            var appWindow = this.AppWindow;

            // 确定哪一个是上方窗口（先创建的在上面）
            if (_taskCompactWindow != null && _notepadCompactWindow != null)
            {
                // 任务窗口在上，记事本在下
                var taskAppWindow = _taskCompactWindow.AppWindow;
                var notepadAppWindow = _notepadCompactWindow.AppWindow;

                if (taskAppWindow != null)
                {
                    taskAppWindow.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = x, Y = topY,
                        Width = taskAppWindow.Size.Width,
                        Height = taskAppWindow.Size.Height
                    });
                    _taskCompactWindow.UpdatePinnedWindowGuard();
                }

                if (notepadAppWindow != null)
                {
                    var taskHeight = taskAppWindow?.Size.Height ?? 480;
                    notepadAppWindow.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = x, Y = topY + taskHeight + gap,
                        Width = notepadAppWindow.Size.Width,
                        Height = notepadAppWindow.Size.Height
                    });
                    _notepadCompactWindow.UpdatePinnedWindowGuard();
                }
            }
            else if (_taskCompactWindow != null)
            {
                var w = _taskCompactWindow.AppWindow;
                if (w != null)
                    w.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = x, Y = topY,
                        Width = w.Size.Width,
                        Height = w.Size.Height
                    });
                _taskCompactWindow.UpdatePinnedWindowGuard();
            }
            else if (_notepadCompactWindow != null)
            {
                var w = _notepadCompactWindow.AppWindow;
                if (w != null)
                    w.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = x, Y = topY,
                        Width = w.Size.Width,
                        Height = w.Size.Height
                    });
                _notepadCompactWindow.UpdatePinnedWindowGuard();
            }
        }

        private void ExitPinnedMode()
        {
            if (_currentNavTag == "Notepad")
            {
                _notepadCompactWindow?.Close();
                _notepadCompactWindow = null;
            }
            else
            {
                _taskCompactWindow?.Close();
                _taskCompactWindow = null;
                _taskCompactPageTag = null;
            }

            UpdatePinButtonState();
            SaveCompactState();
        }

        private void UpdatePinButtonState()
        {
            var supported = IsCurrentPageCompactSupported;
            var isOpen = IsCurrentPageCompactOpen;
            PinButton.IsEnabled = supported;
            PinButton.Opacity = supported ? 1 : 0.4;
            PinIcon.Glyph = isOpen ? "" : "";
            System.Diagnostics.Debug.WriteLine($"[PinButton] page={_currentNavTag}, supported={supported}, taskWin={_taskCompactWindow != null}, taskTag={_taskCompactPageTag}, notepadWin={_notepadCompactWindow != null}, isOpen={isOpen}, icon={(isOpen ? "active" : "inactive")}");
        }

        private void SaveCompactState()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["Compact_Task"] = _taskCompactWindow != null;
            settings.Values["Compact_Notepad"] = _notepadCompactWindow != null;
        }

        private void RestoreCompactState()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            AppLog.Info($"RestoreCompactState: Task={settings.Values.TryGetValue("Compact_Task", out var tv) && tv is true}, Notepad={settings.Values.TryGetValue("Compact_Notepad", out var nv) && nv is true}");

            if (settings.Values.TryGetValue("Compact_Task", out var taskVal) && taskVal is true)
            {
                _taskCompactWindow = new CompactWindow(Tasks, CompletedTasks, _dbService);
                _taskCompactPageTag = _currentNavTag;
                _taskCompactWindow.HeightChanged += OnCompactWindowHeightChanged;
                _taskCompactWindow.ExitRequested += () =>
                {
                    _taskCompactWindow?.Close();
                    _taskCompactWindow = null;
                    _taskCompactPageTag = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                    SaveCompactState();
                };
                _taskCompactWindow.Closed += (s, e) =>
                {
                    _taskCompactWindow = null;
                    _taskCompactPageTag = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                };
                _taskCompactWindow.Activate();
            }

            if (settings.Values.TryGetValue("Compact_Notepad", out var npVal) && npVal is true)
            {
                // 主界面可能还没初始化记事本，先预加载标签数据
                if (_notepadTabs.Count == 0)
                {
                    foreach (var tab in _dbService.GetNotepadTabs())
                        _notepadTabs.Add(tab);
                }

                _notepadCompactWindow = new NotepadCompactWindow(_dbService, _notepadTabs);
                _notepadCompactWindow.HeightChanged += OnCompactWindowHeightChanged;
                _notepadCompactWindow.ExitRequested += () =>
                {
                    _notepadCompactWindow?.Close();
                    _notepadCompactWindow = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                    SaveCompactState();
                };
                _notepadCompactWindow.Closed += (s, e) =>
                {
                    _notepadCompactWindow = null;
                    RepositionCompactWindows();
                    UpdatePinButtonState();
                };
                _notepadCompactWindow.Activate();
            }

            RepositionCompactWindows();
            UpdatePinButtonState();
            SaveCompactState();
        }


        #region Keyboard Helper

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static bool IsCtrlPressed() => (GetKeyState(0x11) & 0x8000) != 0;
        private static bool IsShiftPressed() => (GetKeyState(0x10) & 0x8000) != 0;
        private static bool IsAltPressed() => (GetKeyState(0x12) & 0x8000) != 0;

        #endregion

    }
}
