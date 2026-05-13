using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Todo
{
    public class TaskItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isSelected;
        
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string DueDate { get; set; } = "";
        public string Description { get; set; } = "";
        
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
