using System;
using Community.VisualStudio.Toolkit;

namespace SmartBookmarks.Services
{
    internal static class BookmarkLog
    {
        internal const string PaneName = "Smart Bookmarks";

        internal static async System.Threading.Tasks.Task WriteAsync(string message)
        {
            try
            {
                OutputWindowPane pane = await VS.Windows.CreateOutputWindowPaneAsync(PaneName);
                await pane.WriteLineAsync($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
            catch
            {
            }
        }
    }
}
