using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using SmartBookmarks.Models;
using SmartBookmarks.Services;
using Task = System.Threading.Tasks.Task;

namespace SmartBookmarks.Commands
{
    internal abstract class GoToNumberedBookmarkCommandBase<T> : BaseCommand<T>
        where T : class, new()
    {
        protected abstract int Number { get; }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            Bookmark? bookmark = BookmarkService.Instance.GetNumbered(Number);
            if (bookmark == null)
            {
                await VS.StatusBar.ShowMessageAsync($"Smart Bookmarks: [{Number}] is empty");
                return;
            }

            await NavigationService.Instance.GoToAsync(bookmark);
        }
    }

    [Command(PackageIds.GoToNumberedBookmark0)]
    internal sealed class GoToNumberedBookmark0 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark0>
    {
        protected override int Number => 0;
    }

    [Command(PackageIds.GoToNumberedBookmark1)]
    internal sealed class GoToNumberedBookmark1 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark1>
    {
        protected override int Number => 1;
    }

    [Command(PackageIds.GoToNumberedBookmark2)]
    internal sealed class GoToNumberedBookmark2 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark2>
    {
        protected override int Number => 2;
    }

    [Command(PackageIds.GoToNumberedBookmark3)]
    internal sealed class GoToNumberedBookmark3 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark3>
    {
        protected override int Number => 3;
    }

    [Command(PackageIds.GoToNumberedBookmark4)]
    internal sealed class GoToNumberedBookmark4 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark4>
    {
        protected override int Number => 4;
    }

    [Command(PackageIds.GoToNumberedBookmark5)]
    internal sealed class GoToNumberedBookmark5 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark5>
    {
        protected override int Number => 5;
    }

    [Command(PackageIds.GoToNumberedBookmark6)]
    internal sealed class GoToNumberedBookmark6 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark6>
    {
        protected override int Number => 6;
    }

    [Command(PackageIds.GoToNumberedBookmark7)]
    internal sealed class GoToNumberedBookmark7 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark7>
    {
        protected override int Number => 7;
    }

    [Command(PackageIds.GoToNumberedBookmark8)]
    internal sealed class GoToNumberedBookmark8 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark8>
    {
        protected override int Number => 8;
    }

    [Command(PackageIds.GoToNumberedBookmark9)]
    internal sealed class GoToNumberedBookmark9 : GoToNumberedBookmarkCommandBase<GoToNumberedBookmark9>
    {
        protected override int Number => 9;
    }
}
