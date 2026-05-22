using Microsoft.Data.Sqlite;
using Todo.Models;
using Windows.Storage;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

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
                    GroupId INTEGER,
                    ""Order"" INTEGER NOT NULL DEFAULT 0,
                    IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                    ListCategory INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (GroupId) REFERENCES Groups(Id)
                );

                CREATE TABLE IF NOT EXISTS Tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    DueDate TEXT,
                    IsChecked INTEGER NOT NULL DEFAULT 0,
                    IsImportant INTEGER NOT NULL DEFAULT 0,
                    IsAutoCompleted INTEGER NOT NULL DEFAULT 0,
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

                CREATE TABLE IF NOT EXISTS NotepadTabs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Content TEXT NOT NULL DEFAULT '',
                    FilePath TEXT,
                    IsModified INTEGER NOT NULL DEFAULT 0,
                    ""Order"" INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();

            MigrateDatabase(connection);
            EnsureBuiltInLists(connection);
        }

        private void MigrateDatabase(SqliteConnection connection)
        {
            MigrateRemindersTable(connection);
            MigrateRecurrencesTable(connection);
            MigrateTasksTable(connection);
            MigrateListsTable(connection);
        }

        private void MigrateRemindersTable(SqliteConnection connection)
        {
            var columns = GetTableColumns(connection, "Reminders");

            if (!columns.Contains("CustomDays"))
            {
                ExecuteAlter(connection, "ALTER TABLE Reminders ADD COLUMN CustomDays TEXT");
            }

            if (!columns.Contains("SameDayIntervalMinutes"))
            {
                ExecuteAlter(connection, "ALTER TABLE Reminders ADD COLUMN SameDayIntervalMinutes INTEGER NOT NULL DEFAULT 0");
            }

            if (!columns.Contains("EnableMultiDayReminders"))
            {
                ExecuteAlter(connection, "ALTER TABLE Reminders ADD COLUMN EnableMultiDayReminders INTEGER NOT NULL DEFAULT 0");
            }
        }

        private void MigrateRecurrencesTable(SqliteConnection connection)
        {
            var columns = GetTableColumns(connection, "Recurrences");

            if (!columns.Contains("NextDueDate"))
            {
                ExecuteAlter(connection, "ALTER TABLE Recurrences ADD COLUMN NextDueDate TEXT");
            }
        }

        private void MigrateTasksTable(SqliteConnection connection)
        {
            var columns = GetTableColumns(connection, "Tasks");

            if (!columns.Contains("IsImportant"))
            {
                ExecuteAlter(connection, "ALTER TABLE Tasks ADD COLUMN IsImportant INTEGER NOT NULL DEFAULT 0");
            }

            if (!columns.Contains("IsAutoCompleted"))
            {
                ExecuteAlter(connection, "ALTER TABLE Tasks ADD COLUMN IsAutoCompleted INTEGER NOT NULL DEFAULT 0");
            }
        }

        private void MigrateListsTable(SqliteConnection connection)
        {
            var columns = GetTableColumns(connection, "Lists");

            if (!columns.Contains("IsBuiltIn"))
            {
                ExecuteAlter(connection, "ALTER TABLE Lists ADD COLUMN IsBuiltIn INTEGER NOT NULL DEFAULT 0");
            }

            if (!columns.Contains("ListCategory"))
            {
                ExecuteAlter(connection, "ALTER TABLE Lists ADD COLUMN ListCategory INTEGER NOT NULL DEFAULT 0");
            }

            var notNullColumns = GetTableNotNullColumns(connection, "Lists");
            if (notNullColumns.Contains("GroupId"))
            {
                RecreateListsTableWithNullableGroupId(connection);
            }
        }

        private void RecreateListsTableWithNullableGroupId(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE Lists_New (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    GroupId INTEGER,
                    ""Order"" INTEGER NOT NULL DEFAULT 0,
                    IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                    ListCategory INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (GroupId) REFERENCES Groups(Id)
                );

                INSERT INTO Lists_New (Id, Name, GroupId, ""Order"", IsBuiltIn, ListCategory)
                SELECT Id, Name, GroupId, ""Order"",
                       COALESCE(IsBuiltIn, 0),
                       COALESCE(ListCategory, 0)
                FROM Lists;

                DROP TABLE Lists;

                ALTER TABLE Lists_New RENAME TO Lists;
            ";
            cmd.ExecuteNonQuery();
        }

        private List<string> GetTableColumns(SqliteConnection connection, string tableName)
        {
            var columns = new List<string>();
            var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = $"PRAGMA table_info({tableName})";
            using (var reader = pragmaCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
            }
            return columns;
        }

        private List<string> GetTableNotNullColumns(SqliteConnection connection, string tableName)
        {
            var columns = new List<string>();
            var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = $"PRAGMA table_info({tableName})";
            using (var reader = pragmaCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var notNull = reader.GetInt32(3);
                    if (notNull == 1)
                    {
                        columns.Add(reader.GetString(1));
                    }
                }
            }
            return columns;
        }

        private void ExecuteAlter(SqliteConnection connection, string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private void EnsureBuiltInLists(SqliteConnection connection)
        {
            var builtInLists = new[]
            {
                new { Name = "日常", Category = ListCategory.Daily },
                new { Name = "周常", Category = ListCategory.Weekly },
                new { Name = "月常", Category = ListCategory.Monthly },
                new { Name = "记事本", Category = ListCategory.Notepad }
            };

            foreach (var item in builtInLists)
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Lists WHERE IsBuiltIn = 1 AND ListCategory = $category";
                checkCmd.Parameters.AddWithValue("$category", (int)item.Category);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count == 0)
                {
                    var orderCmd = connection.CreateCommand();
                    orderCmd.CommandText = "SELECT COALESCE(MAX(\"Order\"), -1) + 1 FROM Lists";
                    var order = Convert.ToInt32(orderCmd.ExecuteScalar());

                    var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO Lists (Name, GroupId, \"Order\", IsBuiltIn, ListCategory) VALUES ($name, $groupId, $order, 1, $category); SELECT last_insert_rowid();";
                    insertCmd.Parameters.AddWithValue("$name", item.Name);
                    insertCmd.Parameters.AddWithValue("$groupId", DBNull.Value);
                    insertCmd.Parameters.AddWithValue("$order", order);
                    insertCmd.Parameters.AddWithValue("$category", (int)item.Category);
                    insertCmd.ExecuteScalar();
                }
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
            command.CommandText = "SELECT Id, Name, \"Order\", IsBuiltIn, ListCategory FROM Lists WHERE GroupId = $groupId ORDER BY \"Order\"";
            command.Parameters.AddWithValue("$groupId", groupId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                lists.Add(new TaskList
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Order = reader.GetInt32(2),
                    GroupId = groupId,
                    IsBuiltIn = reader.GetInt32(3) == 1,
                    ListCategory = (ListCategory)reader.GetInt32(4)
                });
            }
            return lists;
        }

        public List<TaskList> GetStandaloneLists()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var lists = new List<TaskList>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, \"Order\", IsBuiltIn, ListCategory FROM Lists WHERE GroupId IS NULL AND IsBuiltIn = 0 ORDER BY \"Order\"";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    lists.Add(new TaskList
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Order = reader.GetInt32(2),
                        GroupId = null,
                        IsBuiltIn = reader.GetInt32(3) == 1,
                        ListCategory = (ListCategory)reader.GetInt32(4)
                    });
                }
            }
            return lists;
        }

        public TaskList? GetBuiltInListByCategory(ListCategory category)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, \"Order\", IsBuiltIn, ListCategory FROM Lists WHERE IsBuiltIn = 1 AND ListCategory = $category";
            command.Parameters.AddWithValue("$category", (int)category);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new TaskList
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Order = reader.GetInt32(2),
                    GroupId = null,
                    IsBuiltIn = reader.GetInt32(3) == 1,
                    ListCategory = (ListCategory)reader.GetInt32(4)
                };
            }
            return null;
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

        public TaskList AddList(string name, int? groupId = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var orderCommand = connection.CreateCommand();
            if (groupId.HasValue)
            {
                orderCommand.CommandText = "SELECT COALESCE(MAX(\"Order\"), -1) + 1 FROM Lists WHERE GroupId = $groupId";
                orderCommand.Parameters.AddWithValue("$groupId", groupId.Value);
            }
            else
            {
                orderCommand.CommandText = "SELECT COALESCE(MAX(\"Order\"), -1) + 1 FROM Lists WHERE GroupId IS NULL";
            }
            var order = Convert.ToInt32(orderCommand.ExecuteScalar());

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Lists (Name, GroupId, \"Order\") VALUES ($name, $groupId, $order); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$groupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$order", order);

            var id = Convert.ToInt32(command.ExecuteScalar());
            return new TaskList { Id = id, Name = name, GroupId = groupId, Order = order };
        }

        public TaskList AddListStandalone(string name)
        {
            return AddList(name, null);
        }

        public void MoveListToGroup(int listId, int? groupId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Lists SET GroupId = $groupId WHERE Id = $id";
            command.Parameters.AddWithValue("$groupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$id", listId);
            command.ExecuteNonQuery();
        }

        public TaskItem AddTask(string title, DateTime? dueDate = null, int? parentTaskId = null, int? listId = null, bool isImportant = false)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Tasks (Title, DueDate, IsImportant, ParentTaskId, ListId, CreatedAt) VALUES ($title, $dueDate, $isImportant, $parentTaskId, $listId, $createdAt); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$dueDate", dueDate.HasValue ? dueDate.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("$isImportant", isImportant ? 1 : 0);
            command.Parameters.AddWithValue("$parentTaskId", parentTaskId.HasValue ? parentTaskId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$listId", listId.HasValue ? listId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("o"));

            var id = Convert.ToInt32(command.ExecuteScalar());
            return new TaskItem { Id = id, Title = title, DueDate = dueDate, IsImportant = isImportant, ParentTaskId = parentTaskId, ListId = listId, CreatedAt = DateTime.Now };
        }

        public void UpdateTaskImportant(int id, bool isImportant)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET IsImportant = $isImportant WHERE Id = $id";
            command.Parameters.AddWithValue("$isImportant", isImportant ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public List<TaskItem> GetImportantTasks()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tasks = new List<TaskItem>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE IsImportant = 1 ORDER BY CreatedAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(ReadTaskItem(reader));
            }
            return tasks;
        }

        public List<TaskItem> GetTasksByListId(int listId, bool? isChecked = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tasks = new List<TaskItem>();
            var command = connection.CreateCommand();

            if (isChecked.HasValue)
            {
                command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE ListId = $listId AND IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$listId", listId);
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE ListId = $listId ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$listId", listId);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(ReadTaskItem(reader));
            }
            return tasks;
        }

        public List<TaskItem> GetTasksByGroupLists(int groupId, bool? isChecked = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var listIds = new List<int>();
            var listCmd = connection.CreateCommand();
            listCmd.CommandText = "SELECT Id FROM Lists WHERE GroupId = $groupId";
            listCmd.Parameters.AddWithValue("$groupId", groupId);
            using (var reader = listCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    listIds.Add(reader.GetInt32(0));
                }
            }

            if (listIds.Count == 0) return new List<TaskItem>();

            var tasks = new List<TaskItem>();
            var placeholders = string.Join(",", listIds.Select((_, i) => $"$listId{i}"));
            var command = connection.CreateCommand();

            if (isChecked.HasValue)
            {
                command.CommandText = $"SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE ListId IN ({placeholders}) AND IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = $"SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE ListId IN ({placeholders}) ORDER BY CreatedAt DESC";
            }

            for (int i = 0; i < listIds.Count; i++)
            {
                command.Parameters.AddWithValue($"$listId{i}", listIds[i]);
            }

            using var reader2 = command.ExecuteReader();
            while (reader2.Read())
            {
                tasks.Add(ReadTaskItem(reader2));
            }
            return tasks;
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

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE Lists SET GroupId = NULL WHERE GroupId = $id";
            updateCmd.Parameters.AddWithValue("$id", id);
            updateCmd.ExecuteNonQuery();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Groups WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteList(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var deleteTasksCmd = connection.CreateCommand();
            deleteTasksCmd.CommandText = @"
                DELETE FROM SubTasks WHERE ParentTaskId IN (SELECT Id FROM Tasks WHERE ListId = $listId);
                DELETE FROM Reminders WHERE TaskId IN (SELECT Id FROM Tasks WHERE ListId = $listId);
                DELETE FROM Recurrences WHERE TaskId IN (SELECT Id FROM Tasks WHERE ListId = $listId);
                DELETE FROM Tasks WHERE ListId = $listId";
            deleteTasksCmd.Parameters.AddWithValue("$listId", id);
            deleteTasksCmd.ExecuteNonQuery();

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
                command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks ORDER BY CreatedAt DESC";
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(ReadTaskItem(reader));
            }
            return tasks;
        }

        public void UpdateTaskChecked(int id, bool isChecked)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET IsChecked = $isChecked, CompletedAt = $completedAt, IsAutoCompleted = 0 WHERE Id = $id";
            command.Parameters.AddWithValue("$isChecked", isChecked ? 1 : 0);
            command.Parameters.AddWithValue("$completedAt", isChecked ? DateTime.Now.ToString("o") : (object)DBNull.Value);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateTaskAutoCompleted(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET IsChecked = 1, IsAutoCompleted = 1, CompletedAt = $completedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$completedAt", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public List<TaskItem> GetOverdueUncheckedTasks()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tasks = new List<TaskItem>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE IsChecked = 0 AND DueDate IS NOT NULL AND DueDate < $now ORDER BY CreatedAt";
            command.Parameters.AddWithValue("$now", DateTime.Now.ToString("o"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(ReadTaskItem(reader));
            }
            return tasks;
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

        public void UpdateSubTaskTitle(int id, string title)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE SubTasks SET Title = $title WHERE Id = $id";
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public List<SubTask> GetSubTasksForTask(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var subTasks = new List<SubTask>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, ParentTaskId, Title, IsChecked, CreatedAt FROM SubTasks WHERE ParentTaskId = $taskId ORDER BY CreatedAt";
            command.Parameters.AddWithValue("$taskId", taskId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                subTasks.Add(new SubTask
                {
                    Id = reader.GetInt32(0),
                    ParentTaskId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    IsChecked = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                });
            }
            return subTasks;
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
                                       DueDate = $dueDate,
                                       IsImportant = $isImportant
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$title", task.Title);
            command.Parameters.AddWithValue("$description", task.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$dueDate", task.DueDate?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$isImportant", task.IsImportant ? 1 : 0);
            command.Parameters.AddWithValue("$id", task.Id);
            command.ExecuteNonQuery();
        }

        public TaskItem? GetTaskById(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted FROM Tasks WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadTaskItem(reader);
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

        public List<Reminder> GetDueReminders(DateTime currentTime, TimeSpan? lookbackWindow = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var reminders = new List<Reminder>();
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT r.Id, r.TaskId, r.ReminderType, r.ReminderDateTime, r.IsRecurring,
                                   r.RecurringInterval, r.CustomDays, r.SameDayIntervalMinutes, r.EnableMultiDayReminders
                                   FROM Reminders r
                                   INNER JOIN Tasks t ON r.TaskId = t.Id
                                   WHERE r.ReminderDateTime IS NOT NULL
                                   AND t.IsChecked = 0";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime? reminderDateTime = reader.IsDBNull(3)
                    ? null
                    : DateTime.Parse(reader.GetString(3));
                if (!reminderDateTime.HasValue || reminderDateTime.Value > currentTime)
                {
                    continue;
                }

                if (lookbackWindow.HasValue && reminderDateTime.Value < currentTime.Subtract(lookbackWindow.Value))
                {
                    continue;
                }

                reminders.Add(new Reminder
                {
                    Id = reader.GetInt32(0),
                    TaskId = reader.GetInt32(1),
                    ReminderType = (ReminderType)reader.GetInt32(2),
                    ReminderDateTime = reminderDateTime,
                    IsRecurring = reader.GetInt32(4) == 1,
                    RecurringInterval = (RecurringInterval)reader.GetInt32(5),
                    CustomDays = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SameDayIntervalMinutes = reader.GetInt32(7),
                    EnableMultiDayReminders = reader.GetInt32(8) == 1
                });
            }
            return reminders;
        }

        private TaskItem ReadTaskItem(SqliteDataReader reader)
        {
            return new TaskItem
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                DueDate = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                IsChecked = reader.GetInt32(4) == 1,
                IsImportant = reader.GetInt32(5) == 1,
                ParentTaskId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ListId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                CreatedAt = DateTime.Parse(reader.GetString(8)),
                CompletedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
                IsAutoCompleted = reader.IsDBNull(10) ? false : reader.GetInt32(10) == 1
            };
        }

        public List<NotepadTab> GetNotepadTabs()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tabs = new List<NotepadTab>();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Content, FilePath, IsModified, \"Order\", CreatedAt, UpdatedAt FROM NotepadTabs ORDER BY \"Order\"";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tabs.Add(new NotepadTab
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Content = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    FilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    IsModified = reader.GetInt32(4) == 1,
                    Order = reader.GetInt32(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6)),
                    UpdatedAt = DateTime.Parse(reader.GetString(7))
                });
            }
            return tabs;
        }

        public NotepadTab AddNotepadTab(string title)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COALESCE(MAX(\"Order\"), -1) + 1 FROM NotepadTabs";
            var order = Convert.ToInt32(orderCommand.ExecuteScalar());

            var now = DateTime.Now.ToString("o");
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO NotepadTabs (Title, Content, FilePath, IsModified, \"Order\", CreatedAt, UpdatedAt) VALUES ($title, $content, $filePath, $isModified, $order, $createdAt, $updatedAt); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$content", "");
            command.Parameters.AddWithValue("$filePath", DBNull.Value);
            command.Parameters.AddWithValue("$isModified", 0);
            command.Parameters.AddWithValue("$order", order);
            command.Parameters.AddWithValue("$createdAt", now);
            command.Parameters.AddWithValue("$updatedAt", now);

            var id = Convert.ToInt32(command.ExecuteScalar());
            return new NotepadTab
            {
                Id = id,
                Title = title,
                Content = "",
                FilePath = null,
                IsModified = false,
                Order = order,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        public void UpdateNotepadTabContent(int id, string content)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE NotepadTabs SET Content = $content, UpdatedAt = $updatedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$content", content);
            command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateNotepadTabTitle(int id, string title)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE NotepadTabs SET Title = $title, UpdatedAt = $updatedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateNotepadTabFilePath(int id, string? filePath)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE NotepadTabs SET FilePath = $filePath, UpdatedAt = $updatedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$filePath", filePath != null ? (object)filePath : DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteNotepadTab(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM NotepadTabs WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }
}
