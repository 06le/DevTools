using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace SmartBookmarks.ToolWindows
{
    internal class BookmarksWindow : BaseToolWindow<BookmarksWindow>
    {
        public override string GetTitle(int toolWindowId) => "Smart Bookmarks";

        public override Type PaneType => typeof(Pane);

        public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            return Task.FromResult<FrameworkElement>(new BookmarksControl());
        }

        [Guid("8DD5FDF9-54FE-40F3-ACEF-EA84FA8F6CE9")]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.Favorite;
            }
        }
    }
}
