using System;
using System.Collections.Generic;
using System.Linq;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;
using SmartBookmarks.Models;

namespace SmartBookmarks.Services
{
    internal sealed class DocumentTrackingService
    {
        internal static DocumentTrackingService Instance { get; } = new DocumentTrackingService();

        private readonly Dictionary<string, TrackedFile> _files =
            new Dictionary<string, TrackedFile>(StringComparer.OrdinalIgnoreCase);

        private bool _subscribed;

        private sealed class TrackedPoint
        {
            public TrackedPoint(ITrackingPoint point, int revision)
            {
                Point = point;
                Revision = revision;
            }

            public ITrackingPoint Point { get; }

            /// <summary>建立此跟踪点时书签的 PositionRevision，用于判断位置是否已被改写。</summary>
            public int Revision { get; }
        }

        private sealed class TrackedFile
        {
            public ITextBuffer Buffer { get; set; } = null!;

            public Dictionary<string, TrackedPoint> Points { get; } = new Dictionary<string, TrackedPoint>();
        }

        internal void Start()
        {
            if (_subscribed)
            {
                return;
            }

            VS.Events.DocumentEvents.Opened += OnOpened;
            VS.Events.DocumentEvents.Closed += OnClosed;
            BookmarkService.Instance.Changed += OnBookmarksChanged;
            _subscribed = true;
        }

        internal void StopAndClear()
        {
            if (_subscribed)
            {
                VS.Events.DocumentEvents.Opened -= OnOpened;
                VS.Events.DocumentEvents.Closed -= OnClosed;
                BookmarkService.Instance.Changed -= OnBookmarksChanged;
                _subscribed = false;
            }

            FlushAllToStore();
            _files.Clear();
        }

        internal bool TryGetTrackedPosition(Bookmark bookmark, out int line, out int column)
        {
            line = bookmark.Line;
            column = bookmark.Column;
            string path = LocationHelper.NormalizePath(bookmark.FilePath);
            if (!_files.TryGetValue(path, out TrackedFile file))
            {
                return false;
            }

            if (!file.Points.TryGetValue(bookmark.Id, out TrackedPoint tracked))
            {
                return false;
            }

            // 位置刚被显式改写、跟踪点还没重建时，用存储位置，避免读到旧行。
            if (tracked.Revision != bookmark.PositionRevision)
            {
                return false;
            }

            try
            {
                SnapshotPoint snapshotPoint = tracked.Point.GetPoint(file.Buffer.CurrentSnapshot);
                LocationHelper.FromSnapshotPoint(snapshotPoint, out line, out column);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal void AttachView(DocumentView? view)
        {
            if (view?.TextBuffer == null || string.IsNullOrWhiteSpace(view.FilePath))
            {
                return;
            }

            string path = LocationHelper.NormalizePath(view.FilePath!);
            SyncFile(path, view.TextBuffer);
        }

        internal void DetachPath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string path = LocationHelper.NormalizePath(filePath!);
            if (!_files.TryGetValue(path, out TrackedFile file))
            {
                return;
            }

            foreach (KeyValuePair<string, TrackedPoint> pair in file.Points)
            {
                try
                {
                    // 位置已被显式改写而跟踪点尚未重建时不得回写，否则旧行会覆盖新位置。
                    Bookmark? bookmark = BookmarkService.Instance.FindById(pair.Key);
                    if (bookmark == null || bookmark.PositionRevision != pair.Value.Revision)
                    {
                        continue;
                    }

                    SnapshotPoint snapshotPoint = pair.Value.Point.GetPoint(file.Buffer.CurrentSnapshot);
                    LocationHelper.FromSnapshotPoint(snapshotPoint, out int line, out int column);
                    BookmarkService.Instance.UpdateStoredPosition(pair.Key, line, column);
                }
                catch
                {
                }
            }

            _files.Remove(path);
        }

        internal void FlushAllToStore()
        {
            foreach (string path in _files.Keys.ToList())
            {
                DetachPath(path);
            }
        }

        private void SyncFile(string path, ITextBuffer buffer)
        {
            if (!_files.TryGetValue(path, out TrackedFile file))
            {
                file = new TrackedFile { Buffer = buffer };
                _files[path] = file;
            }
            else
            {
                file.Buffer = buffer;
            }

            HashSet<string> live = new HashSet<string>(
                BookmarkService.Instance.Bookmarks
                    .Where(b => LocationHelper.SameFile(b.FilePath, path))
                    .Select(b => b.Id));

            foreach (string id in file.Points.Keys.Where(id => !live.Contains(id)).ToList())
            {
                file.Points.Remove(id);
            }

            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            foreach (Bookmark bookmark in BookmarkService.Instance.Bookmarks.Where(b => live.Contains(b.Id)))
            {
                // 已有跟踪点且位置未被改写时保留，继续承担编辑跟随。
                if (file.Points.TryGetValue(bookmark.Id, out TrackedPoint existing)
                    && existing.Revision == bookmark.PositionRevision)
                {
                    continue;
                }

                int position = LocationHelper.ToSnapshotPosition(snapshot, bookmark.Line, bookmark.Column);
                file.Points[bookmark.Id] = new TrackedPoint(
                    snapshot.CreateTrackingPoint(position, PointTrackingMode.Positive),
                    bookmark.PositionRevision);
            }
        }

        private void OnOpened(string filePath)
        {
            _ = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    DocumentView? view = await VS.Documents.GetDocumentViewAsync(filePath);
                    AttachView(view);
                }
                catch (Exception ex)
                {
                    await BookmarkLog.WriteAsync($"Tracking attach failed: {ex.Message}");
                }
            });
        }

        private void OnClosed(string filePath)
        {
            DetachPath(filePath);
        }

        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            foreach (KeyValuePair<string, TrackedFile> pair in _files.ToList())
            {
                SyncFile(pair.Key, pair.Value.Buffer);
            }
        }
    }
}
