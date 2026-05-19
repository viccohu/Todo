using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Todo.Models
{
    public enum RecurrenceType
    {
        None,
        Daily,
        Weekly,
        Monthly,
        Yearly
    }

    public class Recurrence : INotifyPropertyChanged
    {
        private int _id;
        private int _taskId;
        private RecurrenceType _recurrenceType;
        private DateTime _baseDate;
        private DateTime _nextDueDate;
        
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
        
        public RecurrenceType RecurrenceType
        {
            get => _recurrenceType;
            set { _recurrenceType = value; OnPropertyChanged(); }
        }
        
        public DateTime BaseDate
        {
            get => _baseDate;
            set { _baseDate = value; OnPropertyChanged(); }
        }
        
        public DateTime NextDueDate
        {
            get => _nextDueDate;
            set { _nextDueDate = value; OnPropertyChanged(); }
        }
        
        public string DisplayText
        {
            get
            {
                return RecurrenceType switch
                {
                    RecurrenceType.Daily => "每天",
                    RecurrenceType.Weekly => "每周",
                    RecurrenceType.Monthly => "每月",
                    RecurrenceType.Yearly => "每年",
                    _ => "不重复"
                };
            }
        }
        
        public DateTime CalculateNextDueDate()
        {
            return RecurrenceType switch
            {
                RecurrenceType.Daily => NextDueDate.AddDays(1),
                RecurrenceType.Weekly => NextDueDate.AddDays(7),
                RecurrenceType.Monthly => NextDueDate.AddMonths(1),
                RecurrenceType.Yearly => NextDueDate.AddYears(1),
                _ => NextDueDate
            };
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
