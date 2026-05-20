using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Todo.Models
{
    public class NotepadTab : INotifyPropertyChanged
    {
        private string _title = "";
        private string _content = "";
        private string? _filePath;
        private bool _isModified;

        public int Id { get; set; }

        public string Title
        {
            get => _title;
            set
            {
                if (_title == value) return;
                _title = value;
                OnPropertyChanged();
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                if (_content == value) return;
                _content = value;
                OnPropertyChanged();
            }
        }

        public string? FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath == value) return;
                _filePath = value;
                OnPropertyChanged();
            }
        }

        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (_isModified == value) return;
                _isModified = value;
                OnPropertyChanged();
            }
        }

        public int Order { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
