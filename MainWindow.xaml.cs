using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        private DatabaseService _dbService = new DatabaseService();

        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> CompletedTasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskGroup> CustomGroups { get; } = new ObservableCollection<TaskGroup>();

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeCustomTitleBar();
            InitializeData();
            LoadCustomGroups();
            
            NavView.SelectedItem = NavView.MenuItems[1]; // 默认选中重要任务
        }

        private void InitializeData()
        {
            Tasks.Add(new TaskItem { Title = "制定计划书1", DueDate = "下周三", IsChecked = false });
            Tasks.Add(new TaskItem { Title = "制定计划书2", DueDate = "2026年3月1日 周三", IsChecked = false });
            CompletedTasks.Add(new TaskItem { Title = "制定计划书", DueDate = "2026年2月1日 周三", IsChecked = true });
            
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
            // 清除旧的动态导航项
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

            // 添加新的分组
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

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new TaskItem { Title = "新任务", DueDate = "今天", IsChecked = false });
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
