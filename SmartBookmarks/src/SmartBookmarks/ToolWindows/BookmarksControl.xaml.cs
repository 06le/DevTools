using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SmartBookmarks.Models;
using SmartBookmarks.Services;

namespace SmartBookmarks.ToolWindows
{
    public partial class BookmarksControl : UserControl
    {
        private readonly BookmarksViewModel _viewModel = new BookmarksViewModel();
        private BookmarkNode? _contextNode;
        private bool _rebuilding;

        public BookmarksControl()
        {
            InitializeComponent();
            DataContext = _viewModel;
            IsVisibleChanged += OnIsVisibleChanged;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachChanged();
            RefreshList();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                AttachChanged();
                RefreshList();
            }
        }

        private void AttachChanged()
        {
            BookmarkService.Instance.Changed -= OnBookmarksChanged;
            BookmarkService.Instance.Changed += OnBookmarksChanged;
        }

        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                RefreshList();
                return;
            }

            _ = Dispatcher.BeginInvoke(new Action(RefreshList));
        }

        private void RefreshList()
        {
            _rebuilding = true;
            try
            {
                _viewModel.Rebuild();
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_rebuilding)
            {
                return;
            }

            foreach (FolderNode folder in e.AddedItems.OfType<FolderNode>().ToList())
            {
                NodeList.SelectedItems.Remove(folder);
            }
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (NodeList.SelectedItem is BookmarkItemNode item)
            {
                GoTo(item);
            }
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ListViewItem? item = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            _contextNode = item?.DataContext as BookmarkNode;
        }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            bool hasBookmarks = SelectedBookmarks().Any();
            bool oneFolder = ContextFolder() != null && !hasBookmarks;

            SetItem(menu, "Go To", hasBookmarks);
            SetItem(menu, "Rename", SelectedBookmarks().Count == 1);
            SetItem(menu, "Delete / Clear", hasBookmarks);
            SetItem(menu, "Set as Default Folder", ContextFolderOrUncategorized() != null && !hasBookmarks);
            SetItem(menu, "Rename Folder", oneFolder);
            SetItem(menu, "Delete Folder", oneFolder);

            var move = menu.Items.OfType<MenuItem>().First(i => i.Name == "MoveMenu");
            move.Items.Clear();
            move.IsEnabled = hasBookmarks;
            if (hasBookmarks)
            {
                var uncategorized = new MenuItem { Header = "Uncategorized", Tag = (string?)null };
                uncategorized.Click += OnMoveToFolder;
                move.Items.Add(uncategorized);
                foreach (BookmarkFolder folder in BookmarkService.Instance.Folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var item = new MenuItem { Header = folder.Name, Tag = folder.Id };
                    item.Click += OnMoveToFolder;
                    move.Items.Add(item);
                }
            }
        }

        private static void SetItem(ContextMenu menu, string header, bool enabled)
        {
            MenuItem? item = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (i.Header as string) == header);
            if (item != null)
            {
                item.IsEnabled = enabled;
            }
        }

        private List<BookmarkItemNode> SelectedBookmarks()
        {
            List<BookmarkItemNode> selected = NodeList.SelectedItems.OfType<BookmarkItemNode>().ToList();
            if (selected.Count == 0 && _contextNode is BookmarkItemNode one)
            {
                selected.Add(one);
            }

            return selected;
        }

        private FolderNode? ContextFolder()
        {
            if (_contextNode is FolderNode folder && !folder.IsVirtual)
            {
                return folder;
            }

            return null;
        }

        /// <summary>右键命中的文件夹节点，含 Uncategorized，用于把默认文件夹设回未分类。</summary>
        private FolderNode? ContextFolderOrUncategorized()
        {
            return _contextNode as FolderNode;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void OnGoTo(object sender, RoutedEventArgs e)
        {
            BookmarkItemNode? item = SelectedBookmarks().FirstOrDefault();
            if (item != null)
            {
                GoTo(item);
            }
        }

        private void GoTo(BookmarkItemNode item)
        {
            Bookmark? bookmark = BookmarkService.Instance.FindById(item.Id);
            if (bookmark == null)
            {
                return;
            }

            _ = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NavigationService.Instance.GoToAsync(bookmark);
            });
        }

        private void OnRename(object sender, RoutedEventArgs e)
        {
            BookmarkItemNode? item = SelectedBookmarks().FirstOrDefault();
            if (item == null)
            {
                return;
            }

            string? name = Prompt("Rename bookmark", item.DisplayName);
            if (name == null)
            {
                return;
            }

            BookmarkService.Instance.RenameBookmark(item.Id, name);
            RefreshList();
            Persist();
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            foreach (BookmarkItemNode item in SelectedBookmarks())
            {
                BookmarkService.Instance.Remove(item.Id);
            }

            RefreshList();
            Persist();
        }

        private void OnMoveToFolder(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;
            string? folderId = menuItem.Tag as string;
            int moved = BookmarkService.Instance.MoveToFolder(SelectedBookmarks().Select(b => b.Id), folderId);
            StatusText.Text = moved > 0 ? $"Moved {moved} bookmark(s)" : "No change";
            RefreshList();
            Persist();
        }

        private void OnNewFolder(object sender, RoutedEventArgs e)
        {
            string? name = Prompt("New folder", "Folder");
            if (name == null)
            {
                return;
            }

            BookmarkService.Instance.AddFolder(name);
            RefreshList();
            Persist();
        }

        private void OnSetDefaultFolder(object sender, RoutedEventArgs e)
        {
            FolderNode? folder = ContextFolderOrUncategorized();
            if (folder == null)
            {
                return;
            }

            // Uncategorized 传 null 清除默认；对已是默认的文件夹再点一次同样清除。
            BookmarkService.Instance.SetDefaultFolder(folder.Id);
            StatusText.Text = BookmarkService.Instance.DefaultFolderId == null
                ? "New bookmarks go to Uncategorized"
                : $"New bookmarks go to \"{folder.Name}\"";
            RefreshList();
            Persist();
        }

        private void OnRenameFolder(object sender, RoutedEventArgs e)
        {
            FolderNode? folder = ContextFolder();
            if (folder?.Id == null)
            {
                return;
            }

            string? name = Prompt("Rename folder", folder.Name);
            if (name == null)
            {
                return;
            }

            BookmarkService.Instance.RenameFolder(folder.Id, name);
            RefreshList();
            Persist();
        }

        private void OnDeleteFolder(object sender, RoutedEventArgs e)
        {
            FolderNode? folder = ContextFolder();
            if (folder?.Id == null)
            {
                return;
            }

            BookmarkService.Instance.DeleteFolder(folder.Id);
            RefreshList();
            Persist();
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Delete all numbered bookmarks, normal bookmarks, and folders?",
                "Smart Bookmarks",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
            {
                return;
            }

            BookmarkService.Instance.ClearAll();
            RefreshList();
            Persist();
        }

        private static string? Prompt(string title, string value)
        {
            bool ok = TextInputDialog.Show(title, "Name", value, out string name);
            if (!ok)
            {
                return null;
            }

            name = (name ?? "").Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private static void Persist()
        {
            _ = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await BookmarkService.Instance.SaveCurrentSolutionAsync();
            });
        }
    }

    internal static class TextInputDialog
    {
        internal static bool Show(string title, string label, string value, out string result)
        {
            result = value;
            var window = new Window
            {
                Title = title,
                Width = 360,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var text = new TextBox { Text = value, Margin = new Thickness(0, 6, 0, 12) };
            var ok = new Button { Content = "OK", Width = 72, IsDefault = true, Margin = new Thickness(4, 0, 0, 0) };
            var cancel = new Button { Content = "Cancel", Width = 72, IsCancel = true, Margin = new Thickness(4, 0, 0, 0) };
            bool accepted = false;
            ok.Click += (_, __) => { accepted = true; window.DialogResult = true; window.Close(); };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new StackPanel { Margin = new Thickness(12) };
            root.Children.Add(new TextBlock { Text = label });
            root.Children.Add(text);
            root.Children.Add(buttons);
            window.Content = root;
            text.SelectAll();
            text.Focus();
            window.ShowDialog();
            if (!accepted)
            {
                return false;
            }

            result = text.Text;
            return true;
        }
    }
}
