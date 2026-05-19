using Microsoft.Data.Sqlite;
using Todo.Models;
using Windows.Storage;
using System.Collections.Generic;
using System.IO;
using System;

namespace Todo.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService()
        {
            var localFolder = ApplicationData.Current.LocalFolder.Path;
            _dbPath = Path.Combine(localFolder, "todo.db");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Groups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ""Order"" INTEGER NOT NULL DEFAULT 0,
                    IsExpanded INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Lists (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    GroupId INTEGER NOT NULL,
                    ""Order"" INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (GroupId) REFERENCES Groups(Id)
                );

                CREATE TABLE IF NOT EXISTS Tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    DueDate TEXT,
                    IsChecked INTEGER NOT NULL DEFAULT 0,
                    ParentTaskId INTEGER,
                    ListId INTEGER,
                    CreatedAt TEXT NOT NULL,
                    CompletedAt TEXT,
                    FOREIGN KEY (ParentTaskId) REFERENCES Tasks(Id),
                    FOREIGN KEY (ListId) REFERENCES Lists(Id)
                );

                CREATE TABLE IF NOT EXISTS SubTasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ParentTaskId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    IsChecked INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (ParentTaskId) REFERENCES Tasks(Id)
                );

                CREATE TABLE IF NOT EXISTS Reminders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TaskId INTEGER NOT NULL,
                    ReminderType INTEGER NOT NULL,
                    ReminderDateTime TEXT,
                    IsRecurring INTEGER NOT NULL DEFAULT 0,
                    RecurringInterval INTEGER NOT NULL DEFAULT 0,
                    CustomDays TEXT,
                    SameDayIntervalMinutes INTEGER NOT NULL DEFAULT 0,
                    EnableMultiDayReminders INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (TaskId) REFERENCES Tasks(Id)
                );

                CREATE TABLE IF NOT EXISTS Recurrences (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TaskId INTEGER NOT NULL,
                    RecurrenceType INTEGER NOT NULL,
                    BaseDate TEXT NOT NULL,
                    NextDueDate TEXT,
                    FOREIGN KEY (TaskId) REFERENCES Tasks(Id)
                );
            ";
            command.ExecuteNonQuery();
            
            MigrateDatabase(connection);
        }
        
        private void MigrateDatabase(SqliteConnection connection)
        {
            var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA table_info(Reminders)";
            var columns = new List<string>();
            using (var reader = pragmaCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
            }
            
            if (!columns.Contains("CustomDays"))
            {
                var alterCommand1 = connection.CreateCommand();
                alterCommand1.CommandText = "ALTER TABLE Reminders ADD COLUMN CustomDays TEXT";
                alterCommand1.ExecuteNonQuery();
            }
            
            if (!columns.Contains("SameDayIntervalMinutes"))
            {
                var alterCommand2 = connection.CreateCommand();
                alterCommand2.CommandText = "ALTER TABLE Reminders ADD COLUMN SameDayIntervalMinutes INTEGER NOT NULL DEFAULT 0";
                alterCommand2.ExecuteNonQuery();
            }
            
            if (!columns.Contains("EnableMultiDayReminders"))
            {
                var alterCommand3 = connection.CreateCommand();
                alterCommand3.CommandText = "ALTER TABLE Reminders ADD COLUMN EnableMultiDayReminders INTEGER NOT NULL DEFAULT 0";
                alterCommand3.ExecuteNonQuery();
            }
            
            var recurrencePragma = connection.CreateCommand();
            recurrencePragma.CommandText = "PRAGMA table_info(Recurrences)";
            var recurrenceColumns = new List<string>();
            using (var reader = recurrencePragma.ExecuteReader())
            {
                while (reader.Read())
                {
                    recurrenceColumns.Add(reader.GetString(1));
                }
            }
            
            if (!recurrenceColumns.Contains("NextDueDate"))
            {
                var alterRecurrence = connection.CreateCommand();
                alterRecurrence.CommandText = "ALTER TABLE Recurrences ADD COLUMN NextDueDate TEXT";
                alterRecurrence.ExecuteNonQuery();
            }
        }

        public List<TaskGroup> GetGroups()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var groups = new List<TaskGroup>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, \"Order\", IsExpanded FROM Groups ORDER BY \"Order\"";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var group = new TaskGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Order = reader.GetInt32(2),
                    IsExpanded = reader.GetInt32(3) == 1,
                    Lists = GetListsForGroup(connection, reader.GetInt32(0))
                };
                groups.Add(group);
            }
            return groups;
        }

        private List<TaskList> GetListsForGroup(SqliteConnection connection, int groupId)
        {
            var lists = new List<TaskList>();
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT Id, Name, \"Order\" FROM Lists WHERE GroupId = {groupId} ORDER BY \"Order\"";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                lists.Add(new TaskList
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Order = reader.GetInt32(2),
                    GroupId = groupId
                });
            }
            return lists;
        }

        public TaskGroup AddGroup(string name)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COALESCE(MAX(\"Order\"), -1) + 1 FROM Groups";
            var order = Convert.ToInt32(orderCommand.ExecuteScalar());

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Groups (Name, \"Order\") VALUES ($name, $order); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$order", order);
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            return new TaskGroup { Id = id, Name = name, Order = order };
        }

        public TaskList AddList(string name, int groupId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = $"SELECT COALESCE(MAX(\"Order\"), -1) + 1 FROM Lists WHERE GroupId = {groupId}";
            var order = Convert.ToInt32(orderCommand.ExecuteScalar());

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Lists (Name, GroupId, \"Order\") VALUES ($name, $groupId, $order); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$order", order);
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            return new TaskList { Id = id, Name = name, GroupId = groupId, Order = order };
        }

        public TaskItem AddTask(string title, DateTime? dueDate = null, int? parentTaskId = null, int? listId = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Tasks (Title, DueDate, ParentTaskId, ListId, CreatedAt) VALUES ($title, $dueDate, $parentTaskId, $listId, $createdAt); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$dueDate", dueDate?.ToString("o"));
            command.Parameters.AddWithValue("$parentTaskId", parentTaskId.HasValue ? parentTaskId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$listId", listId.HasValue ? listId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("o"));
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            return new TaskItem { Id = id, Title = title, DueDate = dueDate, ParentTaskId = parentTaskId, ListId = listId, CreatedAt = DateTime.Now };
        }

        public void UpdateGroupName(int id, string name)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Groups SET Name = $name WHERE Id = $id";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateListName(int id, string name)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Lists SET Name = $name WHERE Id = $id";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteGroup(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Lists WHERE GroupId = $id; DELETE FROM Groups WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteList(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Lists WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public List<TaskItem> GetTasks(bool? isChecked = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tasks = new List<TaskItem>();
            var command = connection.CreateCommand();
            
            if (isChecked.HasValue)
            {
                command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, ParentTaskId, ListId, CreatedAt, CompletedAt FROM Tasks WHERE IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, ParentTaskId, ListId, CreatedAt, CompletedAt FROM Tasks ORDER BY CreatedAt DESC";
            }
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DueDate = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    IsChecked = reader.GetInt32(4) == 1,
                    ParentTaskId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    ListId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    CompletedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8))
                });
            }
            return tasks;
        }

        public void UpdateTaskChecked(int id, bool isChecked)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET IsChecked = $isChecked, CompletedAt = $completedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$isChecked", isChecked ? 1 : 0);
            command.Parameters.AddWithValue("$completedAt", isChecked ? DateTime.Now.ToString("o") : (object)DBNull.Value);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteTask(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SubTasks WHERE ParentTaskId = $id; DELETE FROM Reminders WHERE TaskId = $id; DELETE FROM Recurrences WHERE TaskId = $id; DELETE FROM Tasks WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public SubTask AddSubTask(int parentTaskId, string title)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO SubTasks (ParentTaskId, Title, CreatedAt) VALUES ($parentId, $title, $createdAt); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$parentId", parentTaskId);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("o"));
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            return new SubTask { Id = id, ParentTaskId = parentTaskId, Title = title, CreatedAt = DateTime.Now };
        }

        public void DeleteSubTask(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SubTasks WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateSubTaskChecked(int id, bool isChecked)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE SubTasks SET IsChecked = $isChecked WHERE Id = $id";
            command.Parameters.AddWithValue("$isChecked", isChecked ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public Reminder AddReminder(int taskId, ReminderType type, DateTime? dateTime = null, bool isRecurring = false, RecurringInterval interval = RecurringInterval.None)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Reminders (TaskId, ReminderType, ReminderDateTime, IsRecurring, RecurringInterval) VALUES ($taskId, $type, $dateTime, $isRecurring, $interval); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.Parameters.AddWithValue("$type", (int)type);
            command.Parameters.AddWithValue("$dateTime", dateTime?.ToString("o"));
            command.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
            command.Parameters.AddWithValue("$interval", (int)interval);
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            return new Reminder { Id = id, TaskId = taskId, ReminderType = type, ReminderDateTime = dateTime, IsRecurring = isRecurring, RecurringInterval = interval };
        }

        public void DeleteReminder(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Reminders WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        
        public void DeleteRemindersForTask(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Reminders WHERE TaskId = $taskId";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.ExecuteNonQuery();
        }
        
        public void AddReminderWithDetails(Reminder reminder)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO Reminders 
                                  (TaskId, ReminderType, ReminderDateTime, IsRecurring, RecurringInterval, CustomDays, SameDayIntervalMinutes, EnableMultiDayReminders) 
                                  VALUES ($taskId, $type, $dateTime, $isRecurring, $interval, $customDays, $intervalMinutes, $enableMultiDay); 
                                  SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$taskId", reminder.TaskId);
            command.Parameters.AddWithValue("$type", (int)reminder.ReminderType);
            command.Parameters.AddWithValue("$dateTime", reminder.ReminderDateTime?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$isRecurring", reminder.IsRecurring ? 1 : 0);
            command.Parameters.AddWithValue("$interval", (int)reminder.RecurringInterval);
            command.Parameters.AddWithValue("$customDays", reminder.CustomDays ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$intervalMinutes", reminder.SameDayIntervalMinutes);
            command.Parameters.AddWithValue("$enableMultiDay", reminder.EnableMultiDayReminders ? 1 : 0);
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            reminder.Id = id;
        }

        public Recurrence AddRecurrence(int taskId, RecurrenceType type, DateTime baseDate)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Recurrences (TaskId, RecurrenceType, BaseDate, NextDueDate) VALUES ($taskId, $type, $baseDate, $nextDueDate); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.Parameters.AddWithValue("$type", (int)type);
            command.Parameters.AddWithValue("$baseDate", baseDate.ToString("o"));
            command.Parameters.AddWithValue("$nextDueDate", CalculateNextRecurrenceDate(type, baseDate).ToString("o"));
            
            var id = Convert.ToInt32(command.ExecuteScalar());
            var recurrence = new Recurrence 
            { 
                Id = id, 
                TaskId = taskId, 
                RecurrenceType = type, 
                BaseDate = baseDate,
                NextDueDate = CalculateNextRecurrenceDate(type, baseDate)
            };
            return recurrence;
        }

        public void DeleteRecurrence(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Recurrences WHERE TaskId = $taskId";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.ExecuteNonQuery();
        }

        public DateTime CalculateNextRecurrenceDate(RecurrenceType type, DateTime baseDate)
        {
            return type switch
            {
                RecurrenceType.Daily => baseDate.AddDays(1),
                RecurrenceType.Weekly => baseDate.AddDays(7),
                RecurrenceType.Monthly => baseDate.AddMonths(1),
                RecurrenceType.Yearly => baseDate.AddYears(1),
                _ => baseDate
            };
        }
        
        public void UpdateTask(TaskItem task)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Tasks 
                                   SET Title = $title, 
                                       Description = $description,
                                       DueDate = $dueDate
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$title", task.Title);
            command.Parameters.AddWithValue("$description", task.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$dueDate", task.DueDate?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$id", task.Id);
            command.ExecuteNonQuery();
        }
        
        public TaskItem? GetTaskById(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, ParentTaskId, ListId, CreatedAt, CompletedAt FROM Tasks WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DueDate = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    IsChecked = reader.GetInt32(4) == 1,
                    ParentTaskId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    ListId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    CompletedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8))
                };
            }
            return null;
        }
        
        public List<Reminder> GetRemindersForTask(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var reminders = new List<Reminder>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, TaskId, ReminderType, ReminderDateTime, IsRecurring, RecurringInterval, CustomDays, SameDayIntervalMinutes, EnableMultiDayReminders FROM Reminders WHERE TaskId = $taskId";
            command.Parameters.AddWithValue("$taskId", taskId);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                reminders.Add(new Reminder
                {
                    Id = reader.GetInt32(0),
                    TaskId = reader.GetInt32(1),
                    ReminderType = (ReminderType)reader.GetInt32(2),
                    ReminderDateTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    IsRecurring = reader.GetInt32(4) == 1,
                    RecurringInterval = (RecurringInterval)reader.GetInt32(5),
                    CustomDays = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SameDayIntervalMinutes = reader.GetInt32(7),
                    EnableMultiDayReminders = reader.GetInt32(8) == 1
                });
            }
            return reminders;
        }
        
        public void UpdateReminder(Reminder reminder)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Reminders 
                                   SET ReminderType = $type, 
                                       ReminderDateTime = $dateTime,
                                       IsRecurring = $isRecurring,
                                       RecurringInterval = $interval,
                                       CustomDays = $customDays,
                                       SameDayIntervalMinutes = $intervalMinutes,
                                       EnableMultiDayReminders = $enableMultiDay
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$type", (int)reminder.ReminderType);
            command.Parameters.AddWithValue("$dateTime", reminder.ReminderDateTime?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$isRecurring", reminder.IsRecurring ? 1 : 0);
            command.Parameters.AddWithValue("$interval", (int)reminder.RecurringInterval);
            command.Parameters.AddWithValue("$customDays", reminder.CustomDays ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$intervalMinutes", reminder.SameDayIntervalMinutes);
            command.Parameters.AddWithValue("$enableMultiDay", reminder.EnableMultiDayReminders ? 1 : 0);
            command.Parameters.AddWithValue("$id", reminder.Id);
            command.ExecuteNonQuery();
        }
        
        public Recurrence? GetRecurrenceForTask(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, TaskId, RecurrenceType, BaseDate, NextDueDate FROM Recurrences WHERE TaskId = $taskId";
            command.Parameters.AddWithValue("$taskId", taskId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Recurrence
                {
                    Id = reader.GetInt32(0),
                    TaskId = reader.GetInt32(1),
                    RecurrenceType = (RecurrenceType)reader.GetInt32(2),
                    BaseDate = DateTime.Parse(reader.GetString(3)),
                    NextDueDate = reader.IsDBNull(4) ? DateTime.Parse(reader.GetString(3)) : DateTime.Parse(reader.GetString(4))
                };
            }
            return null;
        }
        
        public void UpdateRecurrence(Recurrence recurrence)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Recurrences 
                                   SET RecurrenceType = $type, 
                                       BaseDate = $baseDate,
                                       NextDueDate = $nextDueDate
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$type", (int)recurrence.RecurrenceType);
            command.Parameters.AddWithValue("$baseDate", recurrence.BaseDate.ToString("o"));
            command.Parameters.AddWithValue("$nextDueDate", recurrence.NextDueDate.ToString("o"));
            command.Parameters.AddWithValue("$id", recurrence.Id);
            command.ExecuteNonQuery();
        }
        
        public List<Reminder> GetDueReminders(DateTime currentTime)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var reminders = new List<Reminder>();
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT r.Id, r.TaskId, r.ReminderType, r.ReminderDateTime, r.IsRecurring, 
                                   r.RecurringInterval, r.CustomDays, r.SameDayIntervalMinutes, r.EnableMultiDayReminders,
                                   t.Title 
                                   FROM Reminders r 
                                   INNER JOIN Tasks t ON r.TaskId = t.Id 
                                   WHERE r.ReminderDateTime IS NOT NULL 
                                   AND datetime(r.ReminderDateTime) <= datetime($currentTime)
                                   AND t.IsChecked = 0";
            command.Parameters.AddWithValue("$currentTime", currentTime.ToString("o"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                reminders.Add(new Reminder
                {
                    Id = reader.GetInt32(0),
                    TaskId = reader.GetInt32(1),
                    ReminderType = (ReminderType)reader.GetInt32(2),
                    ReminderDateTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    IsRecurring = reader.GetInt32(4) == 1,
                    RecurringInterval = (RecurringInterval)reader.GetInt32(5),
                    CustomDays = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SameDayIntervalMinutes = reader.GetInt32(7),
                    EnableMultiDayReminders = reader.GetInt32(8) == 1
                });
            }
            return reminders;
        }
    }
}
