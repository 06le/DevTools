using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Community.VisualStudio.Toolkit;
using SmartBookmarks.Models;

namespace SmartBookmarks.Services
{
    internal sealed class PersistenceService
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(BookmarkStore));

        internal async System.Threading.Tasks.Task<string?> GetStorePathAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Solution? solution = await VS.Solutions.GetCurrentSolutionAsync();
            string? fullPath = solution?.FullPath;
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return null;
            }

            string? dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return null;
            }

            return Path.Combine(dir, ".vs", "SmartBookmarks", "bookmarks.json");
        }

        internal BookmarkStore Load(string path)
        {
            if (!File.Exists(path))
            {
                return new BookmarkStore();
            }

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new BookmarkStore();
            }

            BookmarkStore? store;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                store = Serializer.ReadObject(stream) as BookmarkStore;
            }
            if (store == null)
            {
                throw new InvalidDataException("Bookmark JSON deserialized to null.");
            }

            store.Folders ??= Array.Empty<BookmarkFolder>();
            store.Bookmarks ??= Array.Empty<Bookmark>();
            return store;
        }

        internal void Save(string path, BookmarkStore store)
        {
            string? dir = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(dir))
            {
                throw new InvalidOperationException("Bookmark store path has no directory.");
            }

            Directory.CreateDirectory(dir);
            string json;
            using (var stream = new MemoryStream())
            {
                Serializer.WriteObject(stream, store);
                json = Encoding.UTF8.GetString(stream.ToArray());
            }
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                File.Replace(temp, path, path + ".bak");
                TryDelete(path + ".bak");
            }
            else
            {
                File.Move(temp, path);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class BookmarkService
    {
        internal static BookmarkService Instance { get; } = new BookmarkService();

        private readonly object _gate = new object();
        private readonly List<BookmarkFolder> _folders = new List<BookmarkFolder>();
        private readonly List<Bookmark> _bookmarks = new List<Bookmark>();
        private readonly PersistenceService _persistence = new PersistenceService();

        internal event EventHandler? Changed;

        internal IReadOnlyList<BookmarkFolder> Folders
        {
            get
            {
                lock (_gate)
                {
                    return _folders.ToList();
                }
            }
        }

        internal IReadOnlyList<Bookmark> Bookmarks
        {
            get
            {
                lock (_gate)
                {
                    return _bookmarks.ToList();
                }
            }
        }

        internal Bookmark? GetNumbered(int number)
        {
            lock (_gate)
            {
                return _bookmarks.FirstOrDefault(b => b.Kind == BookmarkKind.Numbered && b.Number == number);
            }
        }

        internal Bookmark? FindNormalAt(CaretLocation location)
        {
            lock (_gate)
            {
                return _bookmarks.FirstOrDefault(b =>
                    b.Kind == BookmarkKind.Normal && LocationHelper.SameStoredPosition(b, location));
            }
        }

        internal Bookmark? FindById(string id)
        {
            lock (_gate)
            {
                return _bookmarks.FirstOrDefault(b => b.Id == id);
            }
        }

        internal BookmarkFolder? FindFolder(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            lock (_gate)
            {
                return _folders.FirstOrDefault(f => f.Id == id);
            }
        }

        internal async System.Threading.Tasks.Task LoadCurrentSolutionAsync()
        {
            string? path = await _persistence.GetStorePathAsync();
            BookmarkStore store = new BookmarkStore();
            if (path != null)
            {
                try
                {
                    store = _persistence.Load(path);
                }
                catch (Exception ex)
                {
                    await BookmarkLog.WriteAsync($"Load failed ({path}): {ex.Message}");
                    await VS.StatusBar.ShowMessageAsync("Smart Bookmarks: failed to load store (see Output pane)");
                }
            }

            lock (_gate)
            {
                _folders.Clear();
                _folders.AddRange(store.Folders ?? Array.Empty<BookmarkFolder>());
                _bookmarks.Clear();
                _bookmarks.AddRange(Sanitize(store.Bookmarks ?? Array.Empty<Bookmark>()));
            }

            RaiseChanged();
        }

        internal async System.Threading.Tasks.Task SaveCurrentSolutionAsync()
        {
            string? path = await _persistence.GetStorePathAsync();
            if (path == null)
            {
                return;
            }

            BookmarkStore store;
            lock (_gate)
            {
                store = new BookmarkStore
                {
                    Version = 1,
                    Folders = _folders.ToArray(),
                    Bookmarks = _bookmarks.ToArray()
                };
            }

            try
            {
                _persistence.Save(path, store);
            }
            catch (Exception ex)
            {
                await BookmarkLog.WriteAsync($"Save failed ({path}): {ex.Message}");
            }
        }

        internal void ClearInMemory()
        {
            lock (_gate)
            {
                _folders.Clear();
                _bookmarks.Clear();
            }

            RaiseChanged();
        }

        internal Bookmark SetNumbered(int number, CaretLocation location)
        {
            Bookmark result;
            lock (_gate)
            {
                Bookmark? existing = _bookmarks.FirstOrDefault(b => b.Kind == BookmarkKind.Numbered && b.Number == number);
                if (existing != null)
                {
                    existing.FilePath = location.FilePath;
                    existing.Line = location.Line;
                    existing.Column = location.Column;
                    existing.DisplayName = location.DisplayName;
                    existing.LineText = location.LineText;
                    result = existing;
                }
                else
                {
                    result = new Bookmark
                    {
                        Kind = BookmarkKind.Numbered,
                        Number = number,
                        FilePath = location.FilePath,
                        Line = location.Line,
                        Column = location.Column,
                        DisplayName = location.DisplayName,
                        LineText = location.LineText
                    };
                    _bookmarks.Add(result);
                }
            }

            RaiseChanged();
            return result;
        }

        internal bool ClearNumbered(int number)
        {
            bool removed;
            lock (_gate)
            {
                removed = _bookmarks.RemoveAll(b => b.Kind == BookmarkKind.Numbered && b.Number == number) > 0;
            }

            if (removed)
            {
                RaiseChanged();
            }

            return removed;
        }

        internal Bookmark AddNormal(CaretLocation location)
        {
            Bookmark bookmark;
            lock (_gate)
            {
                Bookmark? existing = _bookmarks.FirstOrDefault(b =>
                    b.Kind == BookmarkKind.Normal && LocationHelper.SameStoredPosition(b, location));
                if (existing != null)
                {
                    return existing;
                }

                bookmark = new Bookmark
                {
                    Kind = BookmarkKind.Normal,
                    FilePath = location.FilePath,
                    Line = location.Line,
                    Column = location.Column,
                    DisplayName = location.DisplayName,
                    LineText = location.LineText
                };
                _bookmarks.Add(bookmark);
            }

            RaiseChanged();
            return bookmark;
        }

        internal bool Remove(string id)
        {
            bool removed;
            lock (_gate)
            {
                removed = _bookmarks.RemoveAll(b => b.Id == id) > 0;
            }

            if (removed)
            {
                RaiseChanged();
            }

            return removed;
        }

        internal bool RenameBookmark(string id, string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            lock (_gate)
            {
                Bookmark? bookmark = _bookmarks.FirstOrDefault(b => b.Id == id);
                if (bookmark == null)
                {
                    return false;
                }

                bookmark.DisplayName = name;
            }

            RaiseChanged();
            return true;
        }

        internal BookmarkFolder AddFolder(string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = "Folder";
            }

            BookmarkFolder folder;
            lock (_gate)
            {
                folder = new BookmarkFolder { Name = UniqueFolderName(name, null) };
                _folders.Add(folder);
            }

            RaiseChanged();
            return folder;
        }

        internal bool RenameFolder(string id, string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            lock (_gate)
            {
                BookmarkFolder? folder = _folders.FirstOrDefault(f => f.Id == id);
                if (folder == null)
                {
                    return false;
                }

                folder.Name = UniqueFolderName(name, id);
            }

            RaiseChanged();
            return true;
        }

        internal bool DeleteFolder(string id)
        {
            lock (_gate)
            {
                if (_folders.RemoveAll(f => f.Id == id) == 0)
                {
                    return false;
                }

                foreach (Bookmark bookmark in _bookmarks.Where(b => b.FolderId == id))
                {
                    bookmark.FolderId = null;
                }
            }

            RaiseChanged();
            return true;
        }

        internal int MoveToFolder(IEnumerable<string> bookmarkIds, string? folderId)
        {
            HashSet<string> ids = new HashSet<string>(bookmarkIds);
            int moved = 0;
            lock (_gate)
            {
                if (folderId != null && _folders.All(f => f.Id != folderId))
                {
                    folderId = null;
                }

                foreach (Bookmark bookmark in _bookmarks.Where(b => ids.Contains(b.Id)))
                {
                    if (bookmark.FolderId == folderId)
                    {
                        continue;
                    }

                    bookmark.FolderId = folderId;
                    moved++;
                }
            }

            if (moved > 0)
            {
                RaiseChanged();
            }

            return moved;
        }

        internal void ClearAll()
        {
            lock (_gate)
            {
                _folders.Clear();
                _bookmarks.Clear();
            }

            RaiseChanged();
        }

        internal void UpdateStoredPosition(string id, int line, int column)
        {
            lock (_gate)
            {
                Bookmark? bookmark = _bookmarks.FirstOrDefault(b => b.Id == id);
                if (bookmark == null)
                {
                    return;
                }

                bookmark.Line = line;
                bookmark.Column = column;
            }
        }

        private string UniqueFolderName(string name, string? exceptId)
        {
            string candidate = name;
            int i = 2;
            while (_folders.Any(f => f.Id != exceptId && string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{name} ({i++})";
            }

            return candidate;
        }

        private static IEnumerable<Bookmark> Sanitize(IEnumerable<Bookmark> bookmarks)
        {
            var numbered = new HashSet<int>();
            foreach (Bookmark bookmark in bookmarks)
            {
                if (string.IsNullOrWhiteSpace(bookmark.Id))
                {
                    bookmark.Id = Guid.NewGuid().ToString("N");
                }

                if (bookmark.Kind == BookmarkKind.Numbered)
                {
                    if (bookmark.Number == null || bookmark.Number < 0 || bookmark.Number > 9 || !numbered.Add(bookmark.Number.Value))
                    {
                        continue;
                    }
                }
                else
                {
                    bookmark.Number = null;
                    bookmark.Kind = BookmarkKind.Normal;
                }

                if (bookmark.Line < 1)
                {
                    bookmark.Line = 1;
                }

                if (bookmark.Column < 1)
                {
                    bookmark.Column = 1;
                }

                yield return bookmark;
            }
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
