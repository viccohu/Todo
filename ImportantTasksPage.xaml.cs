using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

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
            
            // 示例数据
            Tasks.Add(new TaskItem { Title = "制定计划书1", DueDate = "下周三", IsChecked = false });
            Tasks.Add(new TaskItem { Title = "制定计划书2", DueDate = "2026年3月1日 周三", IsChecked = false });
            CompletedTasks.Add(new TaskItem { Title = "制定计划书", DueDate = "2026年2月1日 周三", IsChecked = true });
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
    }
}
