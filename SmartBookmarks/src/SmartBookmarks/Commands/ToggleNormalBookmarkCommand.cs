using System.IO;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using SmartBookmarks.Models;
using SmartBookmarks.Services;
using Task = System.Threading.Tasks.Task;

namespace SmartBookmarks.Commands
{
    [Command(PackageIds.ToggleNormalBookmark)]
    internal sealed class ToggleNormalBookmarkCommand : BaseCommand<ToggleNormalBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView? view = await VS.Documents.GetActiveDocumentViewAsync();
            CaretLocation? location = LocationHelper.FromView(view);
            if (location == null)
            {
                await VS.StatusBar.ShowMessageAsync("Smart Bookmarks: no active document");
                return;
            }

            Bookmark? existing = BookmarkService.Instance.FindNormalAt(location.Value);
            if (existing != null)
            {
                BookmarkService.Instance.Remove(existing.Id);
                await BookmarkService.Instance.SaveCurrentSolutionAsync();
                await VS.StatusBar.ShowMessageAsync("Smart Bookmarks: removed normal bookmark");
                return;
            }

            Bookmark added = BookmarkService.Instance.AddNormal(location.Value);
            DocumentTrackingService.Instance.AttachView(view);
            await BookmarkService.Instance.SaveCurrentSolutionAsync();
            await VS.StatusBar.ShowMessageAsync(
                $"Smart Bookmarks: {Path.GetFileName(added.FilePath)}:{added.Line}");
        }
    }
}
