using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;

namespace Todo.Models
{
    public class SubTask : INotifyPropertyChanged
    {
        private bool _isChecked;
        
        public int Id { get; set; }
        public int ParentTaskId { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }

        public string CreatedAtDisplay => CreatedAt.ToString("MM/dd HH:mm");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
