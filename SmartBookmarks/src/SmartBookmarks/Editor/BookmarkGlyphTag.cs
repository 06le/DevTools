using Microsoft.VisualStudio.Text.Editor;

namespace SmartBookmarks.Editor
{
    internal sealed class BookmarkGlyphTag : IGlyphTag
    {
        public BookmarkGlyphTag(int? number)
        {
            Number = number;
        }

        public int? Number { get; }
    }
}
