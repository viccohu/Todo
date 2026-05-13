using System.Collections.Generic;

namespace Todo.Models
{
    public class TaskList
    {
        public int Id { get; set; }
        public string Name { get; set; } = "新建列表";
        public int GroupId { get; set; }
        public int Order { get; set; }
        public List<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
