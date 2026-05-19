using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Todo.Models
{
    public enum ReminderType
    {
        Deadline,
        Custom,
        Recurring
    }

    public enum RecurringInterval
    {
        None,
        HalfDay,
        Day
    }

    public class Reminder : INotifyPropertyChanged
    {
        private int _id;
        private int _taskId;
        private ReminderType _reminderType;
        private DateTime? _reminderDateTime;
        private bool _isRecurring;
        private RecurringInterval _recurringInterval;
        private string? _customDays;
        private int _sameDayIntervalMinutes;
        private bool _enableMultiDayReminders;
        
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }
        
        public int TaskId
        {
            get => _taskId;
            set { _taskId = value; OnPropertyChanged(); }
        }
        
        public ReminderType ReminderType
        {
            get => _reminderType;
            set { _reminderType = value; OnPropertyChanged(); }
        }
        
        public DateTime? ReminderDateTime
        {
            get => _reminderDateTime;
            set { _reminderDateTime = value; OnPropertyChanged(); }
        }
        
        public bool IsRecurring
        {
            get => _isRecurring;
            set { _isRecurring = value; OnPropertyChanged(); }
        }
        
        public RecurringInterval RecurringInterval
        {
            get => _recurringInterval;
            set { _recurringInterval = value; OnPropertyChanged(); }
        }
        
        public string? CustomDays
        {
            get => _customDays;
            set { _customDays = value; OnPropertyChanged(); }
        }
        
        public int SameDayIntervalMinutes
        {
            get => _sameDayIntervalMinutes;
            set { _sameDayIntervalMinutes = value; OnPropertyChanged(); }
        }
        
        public bool EnableMultiDayReminders
        {
            get => _enableMultiDayReminders;
            set { _enableMultiDayReminders = value; OnPropertyChanged(); }
        }
        
        public string DisplayText
        {
            get
            {
                if (ReminderDateTime.HasValue)
                {
                    return ReminderDateTime.Value.ToString("M月d日 HH:mm");
                }
                return "未设置";
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
