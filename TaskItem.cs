using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
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
                OnPropertyChanged(nameof(DueDateLabel));
                OnPropertyChanged(nameof(DueUrgencyBackground));
            }
        }
        
        public int? ParentTaskId { get; set; }
        public int? ListId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        private bool _isAutoCompleted;

        public DateTime? CompletedAt { get; set; }
        public bool IsAutoCompleted
        {
            get => _isAutoCompleted;
            set
            {
                if (_isAutoCompleted == value) return;
                _isAutoCompleted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CompletionIndicatorBrush));
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CompletionIndicatorBrush));
                OnPropertyChanged(nameof(DueUrgencyBackground));
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

        public string DueDateDisplay => GetFriendlyDateText(DueDate, "今天");
        
        public string CreatedAtDisplay => CreatedAt.ToString("MM/dd");
        
        public string DueDateShortDisplay => GetFriendlyDateText(DueDate, "无期限");

        public string CreatedAtLabel => "创建日期：" + GetFriendlyDateText(CreatedAt);

        public string DueDateLabel
        {
            get
            {
                if (!DueDate.HasValue) return "";
                return "截止日期：" + GetFriendlyDateText(DueDate);
            }
        }

        public SolidColorBrush? DueUrgencyBackground
        {
            get
            {
                if (IsChecked) return null;
                if (!DueDate.HasValue) return null;
                var d = DueDate.Value.Date;
                var today = DateTime.Today;
                if (d == today)
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 0x61, 0x8C, 0x24));
                if (d == today.AddDays(1))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 0x70, 0x8A, 0x4D));
                return null;
            }
        }

        public SolidColorBrush? CompletionIndicatorBrush => IsChecked
            ? (IsAutoCompleted
                ? new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0x8C, 0x00))  // 橙色 = 系统自动完成
                : new SolidColorBrush(ColorHelper.FromArgb(255, 0x00, 0x78, 0xD4))) // 蓝色 = 手动点击完成
            : null;
        
        public ObservableCollection<SubTask> SubTasks { get; set; } = new ObservableCollection<SubTask>();
        public ObservableCollection<Reminder> Reminders { get; set; } = new ObservableCollection<Reminder>();
        public Recurrence? Recurrence { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void RefreshDateDisplay()
        {
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(DueDateShortDisplay));
            OnPropertyChanged(nameof(DueDateLabel));
            OnPropertyChanged(nameof(DueUrgencyBackground));
            OnPropertyChanged(nameof(CreatedAtLabel));
            OnPropertyChanged(nameof(CreatedAtDisplay));
        }

        private static string GetChineseDayOfWeek(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "周一",
            DayOfWeek.Tuesday => "周二",
            DayOfWeek.Wednesday => "周三",
            DayOfWeek.Thursday => "周四",
            DayOfWeek.Friday => "周五",
            DayOfWeek.Saturday => "周六",
            DayOfWeek.Sunday => "周天",
            _ => ""
        };

        public static string GetFriendlyDateText(DateTime? date, string defaultText = "")
        {
            if (!date.HasValue) return defaultText;
            var d = date.Value.Date;
            var today = DateTime.Today;
            if (d == today) return "今天";
            if (d == today.AddDays(1)) return "明天";
            if (d == today.AddDays(2)) return "后天";
            if (d == today.AddDays(-1)) return "昨天";
            if (d == today.AddDays(-2)) return "前天";

            var todayDow = (int)today.DayOfWeek;
            var mondayOffset = todayDow == 0 ? -6 : 1 - todayDow;
            var thisMonday = today.AddDays(mondayOffset);
            var thisSunday = thisMonday.AddDays(6);
            var lastMonday = thisMonday.AddDays(-7);
            var lastSunday = thisMonday.AddDays(-1);
            var nextMonday = thisSunday.AddDays(1);
            var nextSunday = nextMonday.AddDays(6);

            if (d >= thisMonday && d <= thisSunday)
                return GetChineseDayOfWeek(d.DayOfWeek);
            if (d >= lastMonday && d <= lastSunday)
                return "上周" + GetChineseDayOfWeek(d.DayOfWeek);
            if (d >= nextMonday && d <= nextSunday)
                return "下周" + GetChineseDayOfWeek(d.DayOfWeek);

            return d.ToString("yyyy年M月d日");
        }
    }
}
