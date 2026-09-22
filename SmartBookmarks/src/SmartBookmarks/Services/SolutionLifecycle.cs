using System;
using Community.VisualStudio.Toolkit;

namespace SmartBookmarks.Services
{
    internal sealed class SolutionLifecycle
    {
        internal static SolutionLifecycle Instance { get; } = new SolutionLifecycle();

        private bool _subscribed;

        internal void Start()
        {
            if (_subscribed)
            {
                return;
            }

            VS.Events.SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
            VS.Events.SolutionEvents.OnBeforeCloseSolution += OnBeforeCloseSolution;
            _subscribed = true;
        }

        internal async System.Threading.Tasks.Task InitializeOpenSolutionAsync()
        {
            Solution? solution = await VS.Solutions.GetCurrentSolutionAsync();
            if (solution != null)
            {
                await BookmarkService.Instance.LoadCurrentSolutionAsync();
                DocumentTrackingService.Instance.Start();
                DocumentView? view = await VS.Documents.GetActiveDocumentViewAsync();
                DocumentTrackingService.Instance.AttachView(view);
            }
        }

        private void OnAfterOpenSolution(Solution? solution)
        {
            _ = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await BookmarkService.Instance.LoadCurrentSolutionAsync();
                    DocumentTrackingService.Instance.Start();
                    DocumentView? view = await VS.Documents.GetActiveDocumentViewAsync();
                    DocumentTrackingService.Instance.AttachView(view);
                }
                catch (Exception ex)
                {
                    await BookmarkLog.WriteAsync($"Solution open handling failed: {ex.Message}");
                }
            });
        }

        private void OnBeforeCloseSolution()
        {
            _ = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    DocumentTrackingService.Instance.FlushAllToStore();
                    await BookmarkService.Instance.SaveCurrentSolutionAsync();
                    DocumentTrackingService.Instance.StopAndClear();
                    BookmarkService.Instance.ClearInMemory();
                }
                catch (Exception ex)
                {
                    await BookmarkLog.WriteAsync($"Solution close handling failed: {ex.Message}");
                }
            });
        }
    }
}
