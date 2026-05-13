using System.Collections.Generic;

namespace Todo.Models
{
    public class TaskGroup
    {
        public int Id { get; set; }
        public string Name { get; set; } = "新建分组";
        public int Order { get; set; }
        public bool IsExpanded { get; set; } = true;
        public List<TaskList> Lists { get; set; } = new List<TaskList>();
    }
}
