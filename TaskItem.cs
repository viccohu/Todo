using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using Todo.Models;

namespace Todo
{
    public class TaskItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isSelected;
        
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public int? ParentTaskId { get; set; }
        public int? ListId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        
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

        public string DueDateDisplay => DueDate?.ToString("yyyy年M月d日") ?? "今天";
        
        public ObservableCollection<SubTask> SubTasks { get; set; } = new ObservableCollection<SubTask>();
        public List<Reminder> Reminders { get; set; } = new List<Reminder>();
        public Recurrence? Recurrence { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
