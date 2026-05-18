using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System;
using Todo.Services;

namespace Todo
{
    public sealed partial class TasksPage : Page
    {
        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        private DatabaseService _dbService = new DatabaseService();

        public TasksPage()
        {
            InitializeComponent();
            DataContext = this;
            LoadTasks();
        }

        private void LoadTasks()
        {
            var tasks = _dbService.GetTasks(false);
            foreach (var task in tasks)
            {
                Tasks.Add(task);
            }
        }

        public void SetPageInfo(string title, string iconGlyph)
        {
            PageTitle.Text = title;
            PageIcon.Glyph = iconGlyph;
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            var newTask = _dbService.AddTask("新任务", DateTime.Now);
            Tasks.Add(newTask);
        }
    }
}
