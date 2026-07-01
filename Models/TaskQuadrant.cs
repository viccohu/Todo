namespace Memo.Models
{
    public enum TaskQuadrant
    {
        Q1_ImportantUrgent,
        Q2_ImportantNotUrgent,
        Q3_UrgentNotImportant,
        Q4_NotImportantNotUrgent
    }

    public static class TaskQuadrantExtensions
    {
        public static string GetTitle(this TaskQuadrant quadrant) => quadrant switch
        {
            TaskQuadrant.Q1_ImportantUrgent => "重要且紧急",
            TaskQuadrant.Q2_ImportantNotUrgent => "重要不紧急",
            TaskQuadrant.Q3_UrgentNotImportant => "紧急不重要",
            TaskQuadrant.Q4_NotImportantNotUrgent => "可暂缓",
            _ => ""
        };

        public static string GetHint(this TaskQuadrant quadrant) => quadrant switch
        {
            TaskQuadrant.Q1_ImportantUrgent => "立即处理",
            TaskQuadrant.Q2_ImportantNotUrgent => "计划安排",
            TaskQuadrant.Q3_UrgentNotImportant => "快速处理",
            TaskQuadrant.Q4_NotImportantNotUrgent => "可暂缓",
            _ => ""
        };

        public static string GetShortLabel(this TaskQuadrant quadrant) => quadrant switch
        {
            TaskQuadrant.Q1_ImportantUrgent => "Q1",
            TaskQuadrant.Q2_ImportantNotUrgent => "Q2",
            TaskQuadrant.Q3_UrgentNotImportant => "Q3",
            TaskQuadrant.Q4_NotImportantNotUrgent => "Q4",
            _ => ""
        };
    }
}
