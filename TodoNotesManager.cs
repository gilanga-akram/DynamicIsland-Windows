// Language: C#
// Filename: TodoNotesManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows)
// Targets: Todo List & Scratchpad Notes Manager inspired by Poni

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DynamicIsland
{
    public class TodoItem : INotifyPropertyChanged
    {
        private string _title = "";
        private bool _isDone;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public bool IsDone
        {
            get => _isDone;
            set { _isDone = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class TodoNotesManager : INotifyPropertyChanged
    {
        private static TodoNotesManager? _instance;
        public static TodoNotesManager Instance => _instance ??= new TodoNotesManager();

        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPro");

        private static readonly string TodosFile = Path.Combine(StorageDir, "todos.json");
        private static readonly string NotesFile = Path.Combine(StorageDir, "notes.txt");

        private string _quickNoteText = "";
        private string _activeTaskSummary = "📝 Belum ada tugas";

        public ObservableCollection<TodoItem> Items { get; } = new();

        public string QuickNoteText
        {
            get => _quickNoteText;
            set
            {
                if (_quickNoteText != value)
                {
                    _quickNoteText = value;
                    OnPropertyChanged();
                    SaveNotes();
                }
            }
        }

        public string ActiveTaskSummary
        {
            get => _activeTaskSummary;
            private set { _activeTaskSummary = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public TodoNotesManager()
        {
            Directory.CreateDirectory(StorageDir);
            LoadTodos();
            LoadNotes();
            UpdateSummary();
        }

        public void AddTodo(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            var item = new TodoItem { Title = title.Trim(), IsDone = false };
            item.PropertyChanged += (s, e) => { SaveTodos(); UpdateSummary(); };
            Items.Insert(0, item);
            SaveTodos();
            UpdateSummary();
        }

        public void RemoveTodo(TodoItem item)
        {
            if (Items.Remove(item))
            {
                SaveTodos();
                UpdateSummary();
            }
        }

        public void ToggleTodo(TodoItem item)
        {
            item.IsDone = !item.IsDone;
            SaveTodos();
            UpdateSummary();
        }

        public void UpdateSummary()
        {
            int total = Items.Count;
            if (total == 0)
            {
                ActiveTaskSummary = "📝 Tidak ada tugas aktif";
                return;
            }

            int done = 0;
            TodoItem? firstPending = null;
            foreach (var item in Items)
            {
                if (item.IsDone) done++;
                else firstPending ??= item;
            }

            if (firstPending != null)
            {
                ActiveTaskSummary = $"📝 [{done}/{total}] {firstPending.Title}";
            }
            else
            {
                ActiveTaskSummary = $"🎉 Semua {total} tugas selesai!";
            }
        }

        private void LoadTodos()
        {
            try
            {
                if (File.Exists(TodosFile))
                {
                    string json = File.ReadAllText(TodosFile);
                    var list = JsonSerializer.Deserialize<ObservableCollection<TodoItem>>(json);
                    if (list != null)
                    {
                        Items.Clear();
                        foreach (var it in list)
                        {
                            it.PropertyChanged += (s, e) => { SaveTodos(); UpdateSummary(); };
                            Items.Add(it);
                        }
                    }
                }
                else
                {
                    // Default friendly initial tasks
                    AddTodo("Tekan Alt+N untuk buka/tutup Dynamic Island");
                    AddTodo("Coba klik kanan karakter untuk ganti Minion / Spongebob");
                }
            }
            catch { }
        }

        private void SaveTodos()
        {
            try
            {
                string json = JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TodosFile, json);
            }
            catch { }
        }

        private void LoadNotes()
        {
            try
            {
                if (File.Exists(NotesFile))
                {
                    _quickNoteText = File.ReadAllText(NotesFile);
                    OnPropertyChanged(nameof(QuickNoteText));
                }
                else
                {
                    QuickNoteText = "Catatan cepat: kamu bisa mengetik apa saja di sini, otomatis tersimpan.";
                }
            }
            catch { }
        }

        private void SaveNotes()
        {
            try
            {
                File.WriteAllText(NotesFile, _quickNoteText);
            }
            catch { }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
