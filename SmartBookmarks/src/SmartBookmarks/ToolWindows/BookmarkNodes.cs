using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SmartBookmarks.Models;
using SmartBookmarks.Services;

namespace SmartBookmarks.ToolWindows
{
    public abstract class BookmarkNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Raise([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class FolderNode : BookmarkNode
    {
        public FolderNode(string? id, string name)
        {
            Id = id;
            Name = name;
        }

        public string? Id { get; }

        public string Name { get; }

        public bool IsVirtual => Id == null;
    }

    public sealed class BookmarkItemNode : BookmarkNode
    {
        internal BookmarkItemNode(Bookmark bookmark, bool missing)
        {
            Id = bookmark.Id;
            Kind = bookmark.Kind;
            Number = bookmark.Number;
            FilePath = bookmark.FilePath;
            Line = bookmark.Line;
            DisplayName = string.IsNullOrWhiteSpace(bookmark.DisplayName)
                ? Path.GetFileName(bookmark.FilePath)
                : bookmark.DisplayName;
            LineText = bookmark.LineText ?? "";
            Missing = missing;
            FolderId = bookmark.FolderId;
        }

        public string Id { get; }

        public BookmarkKind Kind { get; }

        public int? Number { get; }

        public string FilePath { get; }

        public int Line { get; }

        public string DisplayName { get; }

        public string LineText { get; }

        public bool Missing { get; }

        public string? FolderId { get; }

        public string Title
        {
            get
            {
                string prefix = Number.HasValue ? $"[{Number.Value}] " : "    ";
                string missing = Missing ? " (missing)" : "";
                return $"{prefix}{DisplayName}  {Line}{missing}";
            }
        }

        public string Preview => LineText;
    }

    internal sealed class BookmarksViewModel
    {
        public ObservableCollection<BookmarkNode> Nodes { get; } = new ObservableCollection<BookmarkNode>();

        public void Rebuild()
        {
            Nodes.Clear();
            List<BookmarkFolder> folders = BookmarkService.Instance.Folders
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<Bookmark> bookmarks = BookmarkService.Instance.Bookmarks.ToList();

            AddFolder(null, "Uncategorized", bookmarks.Where(b => string.IsNullOrEmpty(b.FolderId)));
            foreach (BookmarkFolder folder in folders)
            {
                AddFolder(folder.Id, folder.Name, bookmarks.Where(b => b.FolderId == folder.Id));
            }
        }

        private void AddFolder(string? id, string name, IEnumerable<Bookmark> bookmarks)
        {
            Nodes.Add(new FolderNode(id, name));
            foreach (Bookmark bookmark in bookmarks
                .OrderBy(b => b.Kind == BookmarkKind.Numbered ? 0 : 1)
                .ThenBy(b => b.Number ?? int.MaxValue)
                .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                bool missing = string.IsNullOrWhiteSpace(bookmark.FilePath) || !File.Exists(bookmark.FilePath);
                Nodes.Add(new BookmarkItemNode(bookmark, missing));
            }
        }
    }
}
