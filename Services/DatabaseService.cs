using Microsoft.Data.Sqlite;
using Memo.Models;
using Windows.Storage;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace Memo.Services
{
    public class DatabaseService
    {
        private const string TaskSelectColumns =
            "Id, Title, Description, DueDate, IsChecked, IsImportant, ParentTaskId, ListId, CreatedAt, CompletedAt, IsAutoCompleted, LinkedNotepadTabId, IsUrgent, IsUrgencyManual, SortOrder";

        private readonly string _dbPath;

        public DatabaseService()
        {
            // WinUI 3 recommended: ApplicationData.Current.LocalFolder
            _dbPath = Path.Combine(
                ApplicationData.Current.LocalFolder.Path, "todo.db");

            // Migrate from old fixed path if it exists and new path doesn't
            var legacyDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Memo");
            var legacyDb = Path.Combine(legacyDir, "todo.db");
            if (!File.Exists(_dbPath) && File.Exists(legacyDb))
            {
                File.Copy(legacyDb, _dbPath);
            }

            // Migrate from old signed MSIX package LocalState if it exists and new path doesn't
            if (!File.Exists(_dbPath))
            {
                var signedPackageLocalState = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages", "Vicco.Memo_406x52sfa7nkt", "LocalState", "todo.db");
                if (File.Exists(signedPackageLocalState))
                {
                    try
                    {
                        File.Copy(signedPackageLocalState, _dbPath);
                        System.Diagnostics.Debug.WriteLine($"[DB] Migrated from signed package: {signedPackageLocalState}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] Migration failed: {ex.Message}");
                    }
                }
            }

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
                    IsUrgent INTEGER NOT NULL DEFAULT 0,
                    IsUrgencyManual INTEGER NOT NULL DEFAULT 0,
                    IsAutoCompleted INTEGER NOT NULL DEFAULT 0,
                    ParentTaskId INTEGER,
                    ListId INTEGER,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
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

            if (!columns.Contains("LinkedNotepadTabId"))
            {
                ExecuteAlter(connection, "ALTER TABLE Tasks ADD COLUMN LinkedNotepadTabId INTEGER");
            }

            if (!columns.Contains("IsUrgent"))
            {
                ExecuteAlter(connection, "ALTER TABLE Tasks ADD COLUMN IsUrgent INTEGER NOT NULL DEFAULT 0");
            }

            if (!columns.Contains("IsUrgencyManual"))
            {
                ExecuteAlter(connection, "ALTER TABLE Tasks ADD COLUMN IsUrgencyManual INTEGER NOT NULL DEFAULT 0");
            }

            if (!columns.Contains("SortOrder"))
            {
                ExecuteAlter(connection, "ALTER TABLE Tasks ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0");
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

        public void UpdateTaskUrgency(int id, bool isUrgent, bool isUrgencyManual)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Tasks
                                   SET IsUrgent = $isUrgent,
                                       IsUrgencyManual = $isUrgencyManual
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$isUrgent", isUrgent ? 1 : 0);
            command.Parameters.AddWithValue("$isUrgencyManual", isUrgencyManual ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateTaskPriority(int id, bool isImportant, bool isUrgent, bool isUrgencyManual)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Tasks
                                   SET IsImportant = $isImportant,
                                       IsUrgent = $isUrgent,
                                       IsUrgencyManual = $isUrgencyManual
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$isImportant", isImportant ? 1 : 0);
            command.Parameters.AddWithValue("$isUrgent", isUrgent ? 1 : 0);
            command.Parameters.AddWithValue("$isUrgencyManual", isUrgencyManual ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public List<TaskItem> GetActiveTasksForMatrix()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tasks = new List<TaskItem>();
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE IsChecked = 0 ORDER BY CreatedAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(ReadTaskItem(reader));
            }
            return tasks;
        }

        public void UpdateTaskSortOrders(IEnumerable<(int taskId, int sortOrder)> orders)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET SortOrder = $sortOrder WHERE Id = $id";
            var sortParam = command.CreateParameter();
            sortParam.ParameterName = "$sortOrder";
            command.Parameters.Add(sortParam);
            var idParam = command.CreateParameter();
            idParam.ParameterName = "$id";
            command.Parameters.Add(idParam);

            foreach (var (taskId, sortOrder) in orders)
            {
                sortParam.Value = sortOrder;
                idParam.Value = taskId;
                command.ExecuteNonQuery();
            }
        }

        public List<TaskItem> GetImportantTasks()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var tasks = new List<TaskItem>();
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE IsImportant = 1 ORDER BY CreatedAt DESC";

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
                command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE ListId = $listId AND IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$listId", listId);
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE ListId = $listId ORDER BY CreatedAt DESC";
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
                command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE ListId IN ({placeholders}) AND IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE ListId IN ({placeholders}) ORDER BY CreatedAt DESC";
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
                command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE IsChecked = $isChecked ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("$isChecked", isChecked.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks ORDER BY CreatedAt DESC";
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
            var today = DateTime.Today;
            command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE IsChecked = 0 AND DueDate IS NOT NULL ORDER BY CreatedAt";
            System.Diagnostics.Debug.WriteLine($"GetOverdueUncheckedTasks: today={today:yyyy-MM-dd}");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var rawDueDate = reader.GetString(3);
                var dueDate = DateTime.Parse(rawDueDate);
                var isOverdue = dueDate.Date <= today;
                System.Diagnostics.Debug.WriteLine($"  Task {reader.GetInt32(0)} '{reader.GetString(1)}': DueDate='{rawDueDate}', overdue={isOverdue}");
                if (isOverdue)
                {
                    tasks.Add(ReadTaskItem(reader));
                }
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

        public void DeleteRemindersByType(int taskId, ReminderType reminderType)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Reminders WHERE TaskId = $taskId AND ReminderType = $type";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.Parameters.AddWithValue("$type", (int)reminderType);
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
                                       IsImportant = $isImportant,
                                       LinkedNotepadTabId = $linkedTabId
                                   WHERE Id = $id";
            command.Parameters.AddWithValue("$title", task.Title);
            command.Parameters.AddWithValue("$description", task.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$dueDate", task.DueDate?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$isImportant", task.IsImportant ? 1 : 0);
            command.Parameters.AddWithValue("$linkedTabId", task.LinkedNotepadTabId.HasValue ? (object)task.LinkedNotepadTabId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$id", task.Id);
            command.ExecuteNonQuery();
        }

        public void LinkNotepadTab(int taskId, int notepadTabId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET LinkedNotepadTabId = $tabId WHERE Id = $taskId";
            command.Parameters.AddWithValue("$tabId", notepadTabId);
            command.Parameters.AddWithValue("$taskId", taskId);
            command.ExecuteNonQuery();
        }

        public void UnlinkNotepadTab(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tasks SET LinkedNotepadTabId = NULL WHERE Id = $taskId";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.ExecuteNonQuery();
        }

        public NotepadTab? GetLinkedNotepadTab(int taskId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT nt.Id, nt.Title, nt.Content, nt.FilePath, nt.IsModified, nt.""Order"", nt.CreatedAt, nt.UpdatedAt
                                   FROM NotepadTabs nt
                                   INNER JOIN Tasks t ON nt.Id = t.LinkedNotepadTabId
                                   WHERE t.Id = $taskId";
            command.Parameters.AddWithValue("$taskId", taskId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new NotepadTab
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Content = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    FilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    IsModified = reader.GetInt32(4) == 1,
                    Order = reader.GetInt32(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6)),
                    UpdatedAt = DateTime.Parse(reader.GetString(7))
                };
            }
            return null;
        }

        public TaskItem? GetTaskById(int id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {TaskSelectColumns} FROM Tasks WHERE Id = $id";
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
                reminders.Add(ReadReminder(reader));
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

        public List<Reminder> GetTodayReminders()
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
                                   AND t.IsChecked = 0
                                   AND date(r.ReminderDateTime) = date($today)";
            command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                reminders.Add(ReadReminder(reader));
            }
            return reminders;
        }

        public List<Reminder> GetRepeatEnabledReminders()
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
                                   AND t.IsChecked = 0
                                   AND r.EnableMultiDayReminders = 1
                                   AND r.SameDayIntervalMinutes > 0";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                reminders.Add(ReadReminder(reader));
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
                IsAutoCompleted = reader.IsDBNull(10) ? false : reader.GetInt32(10) == 1,
                LinkedNotepadTabId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                IsUrgent = reader.FieldCount > 12 && !reader.IsDBNull(12) && reader.GetInt32(12) == 1,
                IsUrgencyManual = reader.FieldCount > 13 && !reader.IsDBNull(13) && reader.GetInt32(13) == 1,
                SortOrder = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetInt32(14) : 0
            };
        }

        private Reminder ReadReminder(SqliteDataReader reader)
        {
            return new Reminder
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

        public void UpdateNotepadTabOrders(List<NotepadTab> tabs)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            for (int i = 0; i < tabs.Count; i++)
            {
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE NotepadTabs SET \"Order\" = $order WHERE Id = $id";
                command.Parameters.AddWithValue("$order", i);
                command.Parameters.AddWithValue("$id", tabs[i].Id);
                command.ExecuteNonQuery();
            }
        }
        /// <summary>Export the database to a chosen path.</summary>
        public void ExportDatabase(string targetPath)
        {
            // Flush WAL to main database file
            using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();
            File.Copy(_dbPath, targetPath, overwrite: true);
        }

        /// <summary>
        /// Import a database file. If overwrite, replaces the current database.
        /// If append, copies new tasks/notes/tabs from the source.
        /// The caller must reload data from the database after calling this.
        /// </summary>
        public void ImportDatabase(string sourcePath, bool overwrite)
        {
            SqliteConnection.ClearAllPools();

            if (overwrite)
            {
                File.Copy(sourcePath, _dbPath, overwrite: true);
                // 覆盖后重新初始化数据库（运行迁移，补充新列）
                InitializeDatabase();
                return;
            }

            // Append: attach source and copy missing/new records
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ATTACH DATABASE '{sourcePath.Replace("'", "''")}' AS src";
            cmd.ExecuteNonQuery();

            // Debug: check source row counts
            foreach (var tbl in new[] { "Tasks", "SubTasks", "NotepadTabs" })
            {
                try
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM src.{tbl}";
                    var count = (long)cmd.ExecuteScalar()!;
                    System.Diagnostics.Debug.WriteLine($"[ImportDB] Source {tbl}: {count} rows");
                }
                catch { }
            }

            // Copy tasks (skip same Title)
            try
            {
                cmd.CommandText = @"
                    INSERT INTO Tasks (Title, Description, DueDate, IsChecked, IsImportant, IsUrgent, IsUrgencyManual, LinkedNotepadTabId, CreatedAt)
                    SELECT s.Title, s.Description, s.DueDate, s.IsChecked, s.IsImportant,
                           COALESCE(s.IsUrgent, 0), COALESCE(s.IsUrgencyManual, 0),
                           s.LinkedNotepadTabId, s.CreatedAt
                    FROM src.Tasks s
                    WHERE NOT EXISTS (SELECT 1 FROM Tasks t WHERE t.Title = s.Title)";
                int n = cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[ImportDB] Tasks imported: {n}");
            }
            catch (Exception ex)
            {
                // 如果源数据库没有 LinkedNotepadTabId 列，降级为不带该列的导入
                System.Diagnostics.Debug.WriteLine($"[ImportDB] Tasks error (fallback): {ex.Message}");
                try
                {
                    cmd.CommandText = @"
                        INSERT INTO Tasks (Title, Description, DueDate, IsChecked, IsImportant, CreatedAt)
                        SELECT s.Title, s.Description, s.DueDate, s.IsChecked, s.IsImportant, s.CreatedAt
                        FROM src.Tasks s
                        WHERE NOT EXISTS (SELECT 1 FROM Tasks t WHERE t.Title = s.Title)";
                    int n = cmd.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"[ImportDB] Tasks imported (fallback): {n}");
                }
                catch { }
            }

            // Copy subtasks for tasks that were imported (matched by Title)
            try
            {
                cmd.CommandText = @"
                    INSERT INTO SubTasks (ParentTaskId, Title, IsChecked, CreatedAt)
                    SELECT t2.Id, s.Title, s.IsChecked, s.CreatedAt
                    FROM src.SubTasks s
                    JOIN src.Tasks t1 ON s.ParentTaskId = t1.Id
                    JOIN Tasks t2 ON t1.Title = t2.Title
                    WHERE NOT EXISTS (
                        SELECT 1 FROM SubTasks x
                        WHERE x.ParentTaskId = t2.Id AND x.Title = s.Title
                    )";
                int n = cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[ImportDB] SubTasks imported: {n}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImportDB] SubTasks error: {ex.Message}");
            }

            // Copy notepad tabs (skip same Title)
            try
            {
                cmd.CommandText = @"
                    INSERT INTO NotepadTabs (Title, Content, FilePath, IsModified, ""Order"", CreatedAt, UpdatedAt)
                    SELECT s.Title, s.Content, s.FilePath, s.IsModified, s.""Order"", s.CreatedAt, s.UpdatedAt
                    FROM src.NotepadTabs s
                    WHERE NOT EXISTS (SELECT 1 FROM NotepadTabs t WHERE t.Title = s.Title)";
                int n = cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[ImportDB] NotepadTabs imported: {n}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImportDB] NotepadTabs error: {ex.Message}");
            }

            cmd.CommandText = "DETACH DATABASE src";
            cmd.ExecuteNonQuery();
        }

        public string DatabasePath => _dbPath;
    }
}
