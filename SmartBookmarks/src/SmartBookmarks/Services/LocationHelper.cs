using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Community.VisualStudio.Toolkit;
using SmartBookmarks.Models;

namespace SmartBookmarks.Services
{
    internal static class LocationHelper
    {
        private static readonly Regex Identifier = new Regex(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

        internal static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            try
            {
                return Path.GetFullPath(path) ?? "";
            }
            catch (Exception)
            {
                return path;
            }
        }

        internal static bool SameFile(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool SameStoredPosition(Bookmark bookmark, CaretLocation location)
        {
            return SameFile(bookmark.FilePath, location.FilePath)
                && bookmark.Line == location.Line
                && bookmark.Column == location.Column;
        }

        /// <summary>
        /// 书签行与目标位置是否同一行。编号书签的设置 / 清除按行判定，
        /// 不比较列，光标停在行内任意位置都算同一处。
        /// </summary>
        internal static bool SameLine(string bookmarkFilePath, int bookmarkLine, CaretLocation location)
        {
            return SameFile(bookmarkFilePath, location.FilePath) && bookmarkLine == location.Line;
        }

        internal static CaretLocation? FromView(DocumentView? view)
        {
            if (view?.TextView == null || view.TextBuffer == null || string.IsNullOrWhiteSpace(view.FilePath))
            {
                return null;
            }

            SnapshotPoint caret = view.TextView.Caret.Position.BufferPosition;
            ITextSnapshotLine line = caret.GetContainingLine();
            int lineNumber = line.LineNumber + 1;
            int column = caret.Position - line.Start.Position + 1;
            string path = NormalizePath(view.FilePath);
            string lineText = PickPreview(view.TextBuffer.CurrentSnapshot, line.LineNumber);
            string displayName = BuildDisplayName(path, lineNumber, lineText, caret, line);
            return new CaretLocation(path, lineNumber, Math.Max(1, column), lineText, displayName);
        }

        internal static int ToSnapshotPosition(ITextSnapshot snapshot, int line, int column)
        {
            int lineIndex = Math.Max(0, Math.Min(line - 1, snapshot.LineCount - 1));
            ITextSnapshotLine snapshotLine = snapshot.GetLineFromLineNumber(lineIndex);
            int col = Math.Max(0, column - 1);
            return snapshotLine.Start.Position + Math.Min(col, snapshotLine.Length);
        }

        internal static void FromSnapshotPoint(SnapshotPoint point, out int line, out int column)
        {
            ITextSnapshotLine snapshotLine = point.GetContainingLine();
            line = snapshotLine.LineNumber + 1;
            column = point.Position - snapshotLine.Start.Position + 1;
            if (column < 1)
            {
                column = 1;
            }
        }

        internal static void MoveCaret(ITextView textView, ITextBuffer buffer, int line, int column)
        {
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            int position = ToSnapshotPosition(snapshot, line, column);
            var point = new SnapshotPoint(snapshot, position);
            textView.Caret.MoveTo(point);
            textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(point, 0), EnsureSpanVisibleOptions.AlwaysCenter);
            if (textView is IWpfTextView wpf)
            {
                wpf.VisualElement.Focus();
            }
        }

        private static string PickPreview(ITextSnapshot snapshot, int lineIndex)
        {
            string text = snapshot.GetLineFromLineNumber(lineIndex).GetText().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return Truncate(text);
            }

            for (int i = lineIndex + 1; i < Math.Min(snapshot.LineCount, lineIndex + 8); i++)
            {
                text = snapshot.GetLineFromLineNumber(i).GetText().Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return Truncate(text);
                }
            }

            for (int i = lineIndex - 1; i >= Math.Max(0, lineIndex - 8); i--)
            {
                text = snapshot.GetLineFromLineNumber(i).GetText().Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return Truncate(text);
                }
            }

            return "";
        }

        private static string BuildDisplayName(string filePath, int line, string lineText, SnapshotPoint caret, ITextSnapshotLine snapshotLine)
        {
            if (!string.IsNullOrEmpty(lineText))
            {
                MatchCollection matches = Identifier.Matches(lineText);
                if (matches.Count > 0)
                {
                    int caretInLine = caret.Position - snapshotLine.Start.Position;
                    foreach (Match match in matches)
                    {
                        if (caretInLine >= match.Index && caretInLine <= match.Index + match.Length)
                        {
                            return match.Value;
                        }
                    }

                    return matches[0].Value;
                }

                return Truncate(lineText, 48);
            }

            return $"{Path.GetFileName(filePath)}:{line}";
        }

        private static string Truncate(string text, int max = 80)
        {
            text = text.Replace('\t', ' ');
            if (text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max - 1) + "…";
        }
    }
}
