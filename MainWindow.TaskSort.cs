using System.Collections.Generic;
using System.Linq;

namespace Memo
{
    public sealed partial class MainWindow
    {
        private void SortActiveTasks(List<TaskItem> tasks)
        {
            tasks.Sort(CompareByDueDateThenCreated);
        }

        private static int CompareByDueDateThenCreated(TaskItem a, TaskItem b)
        {
            var aDue = a.DueDate?.Date;
            var bDue = b.DueDate?.Date;
            if (aDue.HasValue != bDue.HasValue)
                return aDue.HasValue ? -1 : 1;
            if (aDue.HasValue && bDue.HasValue)
            {
                var dueCompare = aDue.Value.CompareTo(bDue.Value);
                if (dueCompare != 0) return dueCompare;
            }
            return b.CreatedAt.CompareTo(a.CreatedAt);
        }

        private void ResortActiveTasksIfNeeded()
        {
            if (_currentNavTag is "Matrix" or "Notepad") return;

            var sorted = Tasks.ToList();
            SortActiveTasks(sorted);
            if (sorted.Select(t => t.Id).SequenceEqual(Tasks.Select(t => t.Id)))
                return;

            Tasks.Clear();
            foreach (var task in sorted)
                Tasks.Add(task);
        }
    }
}
