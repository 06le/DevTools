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
        private string? _defaultFolderId;

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

        /// <summary>新建书签默认进入的文件夹 Id；null 表示未分类。</summary>
        internal string? DefaultFolderId
        {
            get
            {
                lock (_gate)
                {
                    return _defaultFolderId;
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
                _defaultFolderId = NormalizeFolderId(store.DefaultFolderId);
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
                    Bookmarks = _bookmarks.ToArray(),
                    DefaultFolderId = _defaultFolderId
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
                _defaultFolderId = null;
            }

            RaiseChanged();
        }

        internal Bookmark SetNumbered(int number, CaretLocation location)
        {
            Bookmark result;
            lock (_gate)
            {
                // 一行只保留最后设置的编号，避免左侧 glyph 与 ToolWindow 各显示一个。
                // 行号取实时跟踪位置；未跟踪或跟踪点过期时该方法仍把 line 设为存储行。
                _bookmarks.RemoveAll(b =>
                {
                    if (b.Kind != BookmarkKind.Numbered || b.Number == number)
                    {
                        return false;
                    }

                    DocumentTrackingService.Instance.TryGetTrackedPosition(b, out int line, out _);
                    return LocationHelper.SameLine(b.FilePath, line, location);
                });

                Bookmark? existing = _bookmarks.FirstOrDefault(b => b.Kind == BookmarkKind.Numbered && b.Number == number);
                if (existing != null)
                {
                    existing.FilePath = location.FilePath;
                    existing.Line = location.Line;
                    existing.Column = location.Column;
                    existing.DisplayName = location.DisplayName;
                    existing.LineText = location.LineText;
                    // 位置被显式改写，跟踪点必须按新位置重建，否则 glyph 留在旧行。
                    existing.PositionRevision++;
                    // 覆盖已有编号书签不改变它所在的文件夹。
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
                        LineText = location.LineText,
                        FolderId = _defaultFolderId
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
                    LineText = location.LineText,
                    FolderId = _defaultFolderId
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

                if (_defaultFolderId == id)
                {
                    _defaultFolderId = null;
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
                _defaultFolderId = null;
            }

            RaiseChanged();
        }

        /// <summary>
        /// 设置新建书签的默认文件夹。传 null 或未知 Id 表示回到未分类；
        /// 重复设置同一文件夹视为取消，便于右键菜单来回切换。
        /// </summary>
        internal bool SetDefaultFolder(string? folderId)
        {
            lock (_gate)
            {
                string? target = NormalizeFolderId(folderId);
                if (target != null && _defaultFolderId == target)
                {
                    target = null;
                }

                if (_defaultFolderId == target)
                {
                    return false;
                }

                _defaultFolderId = target;
            }

            RaiseChanged();
            return true;
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

        /// <summary>把空串和已不存在的文件夹 Id 归一为 null（未分类）。调用方须持有 _gate。</summary>
        private string? NormalizeFolderId(string? folderId)
        {
            if (string.IsNullOrEmpty(folderId))
            {
                return null;
            }

            return _folders.Any(f => f.Id == folderId) ? folderId : null;
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
