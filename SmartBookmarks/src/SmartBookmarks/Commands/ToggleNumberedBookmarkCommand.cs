using System;
using System.IO;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using SmartBookmarks.Models;
using SmartBookmarks.Services;
using Task = System.Threading.Tasks.Task;

namespace SmartBookmarks.Commands
{
    internal abstract class ToggleNumberedBookmarkCommandBase<T> : BaseCommand<T>
        where T : class, new()
    {
        protected abstract int Number { get; }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView? view = await VS.Documents.GetActiveDocumentViewAsync();
            CaretLocation? location = LocationHelper.FromView(view);
            if (location == null)
            {
                await VS.StatusBar.ShowMessageAsync("Smart Bookmarks: no active document");
                return;
            }

            // 先让当前文档进入跟踪，已有编号书签的行号才能按实时位置判定。
            DocumentTrackingService.Instance.AttachView(view);

            Bookmark? existing = BookmarkService.Instance.GetNumbered(Number);
            if (existing != null)
            {
                DocumentTrackingService.Instance.TryGetTrackedPosition(existing, out int line, out _);
                // 按行判定，不比较列：光标停在该行任意位置都算同一处，再按一次即清除。
                if (LocationHelper.SameLine(existing.FilePath, line, location.Value))
                {
                    BookmarkService.Instance.ClearNumbered(Number);
                    await BookmarkService.Instance.SaveCurrentSolutionAsync();
                    await VS.StatusBar.ShowMessageAsync($"Smart Bookmarks: cleared [{Number}]");
                    return;
                }
            }

            Bookmark bookmark = BookmarkService.Instance.SetNumbered(Number, location.Value);
            DocumentTrackingService.Instance.AttachView(view);
            await BookmarkService.Instance.SaveCurrentSolutionAsync();
            await VS.StatusBar.ShowMessageAsync(
                $"Smart Bookmarks: [{Number}] {Path.GetFileName(bookmark.FilePath)}:{bookmark.Line}");
        }
    }

    [Command(PackageIds.ToggleNumberedBookmark0)]
    internal sealed class ToggleNumberedBookmark0 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark0>
    {
        protected override int Number => 0;
    }

    [Command(PackageIds.ToggleNumberedBookmark1)]
    internal sealed class ToggleNumberedBookmark1 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark1>
    {
        protected override int Number => 1;
    }

    [Command(PackageIds.ToggleNumberedBookmark2)]
    internal sealed class ToggleNumberedBookmark2 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark2>
    {
        protected override int Number => 2;
    }

    [Command(PackageIds.ToggleNumberedBookmark3)]
    internal sealed class ToggleNumberedBookmark3 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark3>
    {
        protected override int Number => 3;
    }

    [Command(PackageIds.ToggleNumberedBookmark4)]
    internal sealed class ToggleNumberedBookmark4 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark4>
    {
        protected override int Number => 4;
    }

    [Command(PackageIds.ToggleNumberedBookmark5)]
    internal sealed class ToggleNumberedBookmark5 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark5>
    {
        protected override int Number => 5;
    }

    [Command(PackageIds.ToggleNumberedBookmark6)]
    internal sealed class ToggleNumberedBookmark6 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark6>
    {
        protected override int Number => 6;
    }

    [Command(PackageIds.ToggleNumberedBookmark7)]
    internal sealed class ToggleNumberedBookmark7 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark7>
    {
        protected override int Number => 7;
    }

    [Command(PackageIds.ToggleNumberedBookmark8)]
    internal sealed class ToggleNumberedBookmark8 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark8>
    {
        protected override int Number => 8;
    }

    [Command(PackageIds.ToggleNumberedBookmark9)]
    internal sealed class ToggleNumberedBookmark9 : ToggleNumberedBookmarkCommandBase<ToggleNumberedBookmark9>
    {
        protected override int Number => 9;
    }
}
