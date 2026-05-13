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
                    DueDate TEXT,
                    IsChecked INTEGER NOT NULL DEFAULT 0,
                    ListId INTEGER,
                    FOREIGN KEY (ListId) REFERENCES Lists(Id)
                );
            ";
            command.ExecuteNonQuery();
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
    }
}
