using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace Todo
{
    public sealed partial class TasksPage : Page
    {
        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();

        public TasksPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void SetPageInfo(string title, string iconGlyph)
        {
            PageTitle.Text = title;
            PageIcon.Glyph = iconGlyph;
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new TaskItem { Title = "新任务", DueDate = "今天", IsChecked = false });
        }
    }
}
