using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System;
using Todo.Services;

namespace Todo
{
    public sealed partial class ImportantTasksPage : Page
    {
        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> CompletedTasks { get; } = new ObservableCollection<TaskItem>();

        public ImportantTasksPage()
        {
            InitializeComponent();
            DataContext = this;

            ReminderService.Instance.DateChanged += OnDateChanged;
            Unloaded += (s, e) => ReminderService.Instance.DateChanged -= OnDateChanged;

            // 示例数据
            Tasks.Add(new TaskItem { Title = "制定计划书1", DueDate = DateTime.Now.AddDays(3), IsChecked = false });
            Tasks.Add(new TaskItem { Title = "制定计划书2", DueDate = new DateTime(2026, 3, 1), IsChecked = false });
            CompletedTasks.Add(new TaskItem { Title = "制定计划书", DueDate = new DateTime(2026, 2, 1), IsChecked = true });
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
            Tasks.Add(new TaskItem { Title = "新任务", DueDate = DateTime.Now, IsChecked = false });
        }
    }
}
