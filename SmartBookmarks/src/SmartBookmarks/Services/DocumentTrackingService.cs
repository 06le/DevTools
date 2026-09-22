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

        private sealed class TrackedFile
        {
            public ITextBuffer Buffer { get; set; } = null!;

            public Dictionary<string, ITrackingPoint> Points { get; } = new Dictionary<string, ITrackingPoint>();
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

            if (!file.Points.TryGetValue(bookmark.Id, out ITrackingPoint point))
            {
                return false;
            }

            try
            {
                SnapshotPoint snapshotPoint = point.GetPoint(file.Buffer.CurrentSnapshot);
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

            foreach (KeyValuePair<string, ITrackingPoint> pair in file.Points)
            {
                try
                {
                    SnapshotPoint snapshotPoint = pair.Value.GetPoint(file.Buffer.CurrentSnapshot);
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
                if (file.Points.ContainsKey(bookmark.Id))
                {
                    continue;
                }

                int position = LocationHelper.ToSnapshotPosition(snapshot, bookmark.Line, bookmark.Column);
                file.Points[bookmark.Id] = snapshot.CreateTrackingPoint(position, PointTrackingMode.Positive);
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
