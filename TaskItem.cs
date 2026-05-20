using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System;
using Todo.Models;

namespace Todo
{
    public class TaskItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isSelected;
        private bool _isImportant;
        private string _title = "";
        private string _description = "";
        private DateTime? _dueDate;
        
        public int Id { get; set; }
        
        public string Title
        {
            get => _title;
            set
            {
                if (_title == value) return;
                _title = value;
                OnPropertyChanged();
            }
        }
        
        public string Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
            }
        }
        
        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                if (_dueDate == value) return;
                _dueDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DueDateDisplay));
                OnPropertyChanged(nameof(DueDateShortDisplay));
            }
        }
        
        public int? ParentTaskId { get; set; }
        public int? ListId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsImportant
        {
            get => _isImportant;
            set
            {
                if (_isImportant == value) return;
                _isImportant = value;
                OnPropertyChanged();
            }
        }

        public string DueDateDisplay => DueDate?.ToString("yyyy年M月d日") ?? "今天";
        
        public string CreatedAtDisplay => CreatedAt.ToString("MM/dd");
        
        public string DueDateShortDisplay => DueDate?.ToString("MM/dd") ?? "无期限";
        
        public ObservableCollection<SubTask> SubTasks { get; set; } = new ObservableCollection<SubTask>();
        public ObservableCollection<Reminder> Reminders { get; set; } = new ObservableCollection<Reminder>();
        public Recurrence? Recurrence { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
