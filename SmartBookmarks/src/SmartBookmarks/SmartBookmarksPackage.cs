using System;
using System.Runtime.InteropServices;
using System.Threading;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using SmartBookmarks.Services;
using SmartBookmarks.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace SmartBookmarks
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.SmartBookmarksString)]
    [ProvideBindingPath]
    [ProvideToolWindow(typeof(BookmarksWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
    public sealed class SmartBookmarksPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await this.RegisterCommandsAsync();
            this.RegisterToolWindows();
            SolutionLifecycle.Instance.Start();
            await SolutionLifecycle.Instance.InitializeOpenSolutionAsync();
        }
    }
}
