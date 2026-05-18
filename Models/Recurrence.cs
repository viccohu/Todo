using System;

namespace Todo.Models
{
    public enum RecurrenceType
    {
        Daily,
        Weekly,
        Monthly,
        Yearly
    }

    public class Recurrence
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public RecurrenceType RecurrenceType { get; set; }
        public DateTime BaseDate { get; set; }
    }
}
