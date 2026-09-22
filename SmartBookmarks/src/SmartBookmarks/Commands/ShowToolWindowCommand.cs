using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using SmartBookmarks.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace SmartBookmarks.Commands
{
    [Command(PackageIds.ShowToolWindow)]
    internal sealed class ShowToolWindowCommand : BaseCommand<ShowToolWindowCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await BookmarksWindow.ShowAsync();
        }
    }
}
