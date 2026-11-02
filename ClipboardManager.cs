// Language: C#
// Filename: ClipboardManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Atoll Clipboard History Manager

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;

namespace DynamicIsland
{
    public class ClipboardItem : INotifyPropertyChanged
    {
        private bool _isPinned;
        private string _text = string.Empty;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Preview));
                OnPropertyChanged(nameof(CharCountText));
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                _isPinned = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PinIcon));
            }
        }

        public string PinIcon => _isPinned ? "📌" : "📍";

        public string Preview
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_text)) return "(empty)";
                string singleLine = _text.Replace("\r\n", " ").Replace("\n", " ").Trim();
                return singleLine.Length > 120 ? singleLine.Substring(0, 117) + "..." : singleLine;
            }
        }

        public string CharCountText => $"{_text.Length} chars";

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalSeconds < 60) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return Timestamp.ToString("MMM dd");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ClipboardManager : INotifyPropertyChanged
    {
        private static readonly string StoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPro", "clipboard_history.json");

        private readonly ObservableCollection<ClipboardItem> _items = new();
        private ICollectionView _filteredView;
        private string _searchText = string.Empty;
        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private string _lastCopiedText = string.Empty;
        private bool _isSelfCopying = false;

        public ObservableCollection<ClipboardItem> Items => _items;
        public ICollectionView FilteredItems => _filteredView;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    _filteredView.Refresh();
                }
            }
        }

        public ClipboardManager()
        {
            _filteredView = CollectionViewSource.GetDefaultView(_items);
            _filteredView.Filter = FilterPredicate;
            LoadHistory();
        }

        public void Initialize(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                _hwnd = helper.Handle;
                _hwndSource = HwndSource.FromHwnd(_hwnd);
                _hwndSource?.AddHook(WndProc);

                NativeMethods.AddClipboardFormatListener(_hwnd);
            }
            catch { }
        }

        public void Cleanup()
        {
            try
            {
                if (_hwnd != IntPtr.Zero)
                {
                    NativeMethods.RemoveClipboardFormatListener(_hwnd);
                }
                _hwndSource?.RemoveHook(WndProc);
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            if (_isSelfCopying) return;

            try
            {
                // Must access clipboard on UI thread
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!Clipboard.ContainsText()) return;
                        string text = Clipboard.GetText();
                        if (string.IsNullOrWhiteSpace(text) || text == _lastCopiedText) return;

                        _lastCopiedText = text;

                        // Check if already in list, move to top
                        var existing = _items.FirstOrDefault(x => x.Text == text);
                        if (existing != null)
                        {
                            existing.Timestamp = DateTime.Now;
                            _items.Remove(existing);
                            _items.Insert(0, existing);
                        }
                        else
                        {
                            var newItem = new ClipboardItem
                            {
                                Text = text,
                                Timestamp = DateTime.Now
                            };
                            _items.Insert(0, newItem);

                            // Cap size at 50 unpinned items
                            while (_items.Count(x => !x.IsPinned) > 50)
                            {
                                var oldestUnpinned = _items.LastOrDefault(x => !x.IsPinned);
                                if (oldestUnpinned != null) _items.Remove(oldestUnpinned);
                            }
                        }

                        SaveHistory();
                    }
                    catch { }
                }));
            }
            catch { }
        }

        public void CopyToClipboard(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Text)) return;
            try
            {
                _isSelfCopying = true;
                Clipboard.SetText(item.Text);
                _lastCopiedText = item.Text;
                SoundManager.Play(AtollSound.Feedback);
            }
            catch { }
            finally
            {
                _isSelfCopying = false;
            }
        }

        public void DeleteItem(ClipboardItem item)
        {
            if (item == null) return;
            _items.Remove(item);
            SaveHistory();
        }

        public void TogglePin(ClipboardItem item)
        {
            if (item == null) return;
            item.IsPinned = !item.IsPinned;
            SaveHistory();
        }

        public void ClearAllUnpinned()
        {
            var unpinned = _items.Where(x => !x.IsPinned).ToList();
            foreach (var item in unpinned)
            {
                _items.Remove(item);
            }
            SaveHistory();
            SoundManager.Play(AtollSound.Feedback);
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not ClipboardItem item) return false;
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            return item.Text.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        }

        private void SaveHistory()
        {
            try
            {
                string? dir = Path.GetDirectoryName(StoragePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var list = _items.Take(50).Select(x => new ClipboardItemDto
                {
                    Id = x.Id,
                    Text = x.Text,
                    Timestamp = x.Timestamp,
                    IsPinned = x.IsPinned
                }).ToList();

                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(StoragePath, json);
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    string json = File.ReadAllText(StoragePath);
                    var list = JsonSerializer.Deserialize<ClipboardItemDto[]>(json);
                    if (list != null)
                    {
                        foreach (var dto in list)
                        {
                            _items.Add(new ClipboardItem
                            {
                                Id = dto.Id,
                                Text = dto.Text ?? "",
                                Timestamp = dto.Timestamp,
                                IsPinned = dto.IsPinned
                            });
                        }
                    }
                }
            }
            catch { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private class ClipboardItemDto
        {
            public string Id { get; set; } = string.Empty;
            public string? Text { get; set; }
            public DateTime Timestamp { get; set; }
            public bool IsPinned { get; set; }
        }
    }
}
