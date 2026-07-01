using System;
using System.IO;
using Windows.Storage;

namespace Memo.Services
{
    public static class AppLog
    {
        private static readonly string _logPath;
        private static readonly object _lock = new();

        static AppLog()
        {
            _logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "app.log");
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void AutoComplete(string message)
        {
            Write("AUTO", message);
        }

        public static void Notepad(string message)
        {
            Write("NOTEPAD", message);
        }

        private static void Write(string level, string message)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            System.Diagnostics.Debug.WriteLine(line);
            lock (_lock)
            {
                try { File.AppendAllText(_logPath, line + Environment.NewLine); }
                catch { /* 静默失败，不影响主流程 */ }
            }
        }

        public static string LogPath => _logPath;
    }
}
