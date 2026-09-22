using System;
using System.IO;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using SmartBookmarks.Models;
using Task = System.Threading.Tasks.Task;

namespace SmartBookmarks.Services
{
    internal sealed class NavigationService
    {
        internal static NavigationService Instance { get; } = new NavigationService();

        internal async System.Threading.Tasks.Task<bool> GoToAsync(Bookmark bookmark)
        {
            if (bookmark == null)
            {
                return false;
            }

            string path = LocationHelper.NormalizePath(bookmark.FilePath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                await BookmarkLog.WriteAsync($"Missing file: {path}");
                await VS.StatusBar.ShowMessageAsync("Smart Bookmarks: file missing");
                return false;
            }

            int line = bookmark.Line;
            int column = bookmark.Column;
            DocumentTrackingService.Instance.TryGetTrackedPosition(bookmark, out line, out column);

            try
            {
                DocumentView? opened = await VS.Documents.OpenAsync(path);
                if (!Move(opened, line, column))
                {
                    opened = await OpenWithDteAsync(path);
                    if (!Move(opened, line, column))
                    {
                        await BookmarkLog.WriteAsync($"Navigation opened but no text view: {path}");
                        return false;
                    }
                }

                DocumentTrackingService.Instance.AttachView(opened);
                return true;
            }
            catch (Exception ex)
            {
                await BookmarkLog.WriteAsync($"OpenAsync failed ({path}): {ex.Message}");
                try
                {
                    DocumentView? opened = await OpenWithDteAsync(path);
                    if (Move(opened, line, column))
                    {
                        DocumentTrackingService.Instance.AttachView(opened);
                        return true;
                    }
                }
                catch (Exception dteEx)
                {
                    await BookmarkLog.WriteAsync($"DTE open failed ({path}): {dteEx.Message}");
                }

                await VS.StatusBar.ShowMessageAsync("Smart Bookmarks: navigation failed");
                return false;
            }
        }

        private static bool Move(DocumentView? opened, int line, int column)
        {
            if (opened?.TextView == null || opened.TextBuffer == null)
            {
                return false;
            }

            LocationHelper.MoveCaret(opened.TextView, opened.TextBuffer, line, column);
            return true;
        }

        private static async System.Threading.Tasks.Task<DocumentView?> OpenWithDteAsync(string path)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            EnvDTE.DTE? dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE.DTE>();
            if (dte == null)
            {
                return null;
            }

            dte.ItemOperations.OpenFile(path, EnvDTE.Constants.vsViewKindTextView);
            return await VS.Documents.GetActiveDocumentViewAsync();
        }
    }
}
