using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SmartBookmarks.Models;
using SmartBookmarks.Services;

namespace SmartBookmarks.Editor
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(BookmarkGlyphTag))]
    internal sealed class BookmarkTaggerProvider : ITaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactory = null!;

        public ITagger<T>? CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
            {
                return null;
            }

            BookmarkTagger tagger = buffer.Properties.GetOrCreateSingletonProperty(
                () => new BookmarkTagger(buffer, TextDocumentFactory));
            return tagger as ITagger<T>;
        }
    }

    internal sealed class BookmarkTagger : ITagger<BookmarkGlyphTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextDocumentFactoryService _documentFactory;
        private ITextDocument? _document;
        private string _filePath = "";
        private bool _detached;

        internal BookmarkTagger(ITextBuffer buffer, ITextDocumentFactoryService documentFactory)
        {
            _buffer = buffer;
            _documentFactory = documentFactory;
            if (documentFactory.TryGetTextDocument(buffer, out ITextDocument document))
            {
                _document = document;
                _filePath = LocationHelper.NormalizePath(document.FilePath);
            }

            BookmarkService.Instance.Changed += OnBookmarksChanged;
            _buffer.Changed += OnBufferChanged;
            _documentFactory.TextDocumentDisposed += OnDocumentDisposed;
        }

        public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

        public IEnumerable<ITagSpan<BookmarkGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            EnsureDocument();
            if (_detached || spans.Count == 0 || string.IsNullOrEmpty(_filePath))
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            var byLine = new Dictionary<int, BookmarkGlyphTag>();
            foreach (Bookmark bookmark in BookmarkService.Instance.Bookmarks)
            {
                if (!LocationHelper.SameFile(bookmark.FilePath, _filePath))
                {
                    continue;
                }

                DocumentTrackingService.Instance.TryGetTrackedPosition(bookmark, out int line, out _);
                int lineIndex = line - 1;
                if (lineIndex < 0 || lineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                if (bookmark.Kind == BookmarkKind.Numbered && bookmark.Number.HasValue)
                {
                    byLine[lineIndex] = new BookmarkGlyphTag(bookmark.Number);
                }
                else if (!byLine.ContainsKey(lineIndex))
                {
                    byLine[lineIndex] = new BookmarkGlyphTag(null);
                }
            }

            foreach (SnapshotSpan span in spans)
            {
                foreach (KeyValuePair<int, BookmarkGlyphTag> pair in byLine)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(pair.Key);
                    var lineSpan = new SnapshotSpan(snapshot, line.Start, line.LengthIncludingLineBreak);
                    if (span.IntersectsWith(lineSpan))
                    {
                        yield return new TagSpan<BookmarkGlyphTag>(lineSpan, pair.Value);
                    }
                }
            }
        }

        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            RaiseAll();
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            RaiseAll();
        }

        private void OnDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (e.TextDocument == _document)
            {
                Detach();
            }
        }

        /// <summary>
        /// tagger 可能在文档与缓冲区关联之前就已创建，此时构造函数拿不到路径。
        /// 每次取 tag 时补一次，避免该缓冲区始终不显示 glyph。
        /// </summary>
        private void EnsureDocument()
        {
            if (_detached || !string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            if (_documentFactory.TryGetTextDocument(_buffer, out ITextDocument document))
            {
                _document = document;
                _filePath = LocationHelper.NormalizePath(document.FilePath);
            }
        }

        private void RaiseAll()
        {
            if (_detached)
            {
                return;
            }

            // 书签变更可能来自后台线程，编辑器要求在 UI 线程上通知。
            if (Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Context.IsOnMainThread)
            {
                RaiseAllCore();
                return;
            }

            _ = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RaiseAllCore();
            });
        }

        private void RaiseAllCore()
        {
            if (_detached)
            {
                return;
            }

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        private void Detach()
        {
            if (_detached)
            {
                return;
            }

            _detached = true;
            BookmarkService.Instance.Changed -= OnBookmarksChanged;
            _buffer.Changed -= OnBufferChanged;
            _documentFactory.TextDocumentDisposed -= OnDocumentDisposed;
        }
    }
}
