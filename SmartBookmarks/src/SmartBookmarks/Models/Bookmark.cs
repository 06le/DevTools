using System;
using System.Runtime.Serialization;

namespace SmartBookmarks.Models
{
    public enum BookmarkKind
    {
        Numbered,
        Normal
    }

    [DataContract]
    internal sealed class Bookmark
    {
        [DataMember(Name = "id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [DataMember(Name = "kind")]
        public BookmarkKind Kind { get; set; }

        [DataMember(Name = "number")]
        public int? Number { get; set; }

        [DataMember(Name = "filePath")]
        public string FilePath { get; set; } = "";

        [DataMember(Name = "line")]
        public int Line { get; set; } = 1;

        [DataMember(Name = "column")]
        public int Column { get; set; } = 1;

        [DataMember(Name = "folderId")]
        public string? FolderId { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; } = "";

        [DataMember(Name = "lineText")]
        public string LineText { get; set; } = "";
    }

    [DataContract]
    internal sealed class BookmarkFolder
    {
        [DataMember(Name = "id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [DataMember(Name = "name")]
        public string Name { get; set; } = "";
    }

    [DataContract]
    internal sealed class BookmarkStore
    {
        [DataMember(Name = "version")]
        public int Version { get; set; } = 1;

        [DataMember(Name = "folders")]
        public BookmarkFolder[] Folders { get; set; } = Array.Empty<BookmarkFolder>();

        [DataMember(Name = "bookmarks")]
        public Bookmark[] Bookmarks { get; set; } = Array.Empty<Bookmark>();
    }

    internal readonly struct CaretLocation
    {
        public CaretLocation(string filePath, int line, int column, string lineText, string displayName)
        {
            FilePath = filePath;
            Line = line;
            Column = column;
            LineText = lineText;
            DisplayName = displayName;
        }

        public string FilePath { get; }

        public int Line { get; }

        public int Column { get; }

        public string LineText { get; }

        public string DisplayName { get; }
    }
}
