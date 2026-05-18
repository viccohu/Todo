using System;

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

    public class Reminder
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public ReminderType ReminderType { get; set; }
        public DateTime? ReminderDateTime { get; set; }
        public bool IsRecurring { get; set; }
        public RecurringInterval RecurringInterval { get; set; }
        public string? CustomDays { get; set; }
    }
}
