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
        private AppWindow? _appWindow;
        private bool _showCompleted = false;
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
            Tasks.Add(new TaskItem { Id = 1, Title = "制定计划书1", DueDate = "下周三", IsChecked = false });
            Tasks.Add(new TaskItem { Id = 2, Title = "制定计划书2", DueDate = "2026年3月1日 周三", IsChecked = false });
            CompletedTasks.Add(new TaskItem { Id = 3, Title = "制定计划书", DueDate = "2026年2月1日 周三", IsChecked = true });
            
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
                _selectedTask.DueDate = args.NewDate.Value.DateTime.ToString("yyyy年M月d日");
            }
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TaskItem task)
            {
                if (task.IsChecked)
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
                var newId = Tasks.Count + CompletedTasks.Count + 1;
                Tasks.Add(new TaskItem 
                { 
                    Id = newId, 
                    Title = AddTaskTextBox.Text.Trim(), 
                    DueDate = "今天", 
                    IsChecked = false 
                });
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

        private void ToggleDesktopMode_Click(object sender, RoutedEventArgs e)
        {
            _isDesktopMode = !_isDesktopMode;
            if (_isDesktopMode)
            {
                this.SetDesktopWallpaperStyle();
            }
            else
            {
                this.SetNormalStyle();
                InitializeCustomTitleBar();
            }
        }
    }
}
