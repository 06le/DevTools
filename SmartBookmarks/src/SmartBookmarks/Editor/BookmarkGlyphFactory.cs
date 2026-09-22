using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace SmartBookmarks.Editor
{
    [Export(typeof(IGlyphFactoryProvider))]
    [Name("SmartBookmarksGlyph")]
    [Order(After = "VsTextMarker")]
    [ContentType("text")]
    [TagType(typeof(BookmarkGlyphTag))]
    internal sealed class BookmarkGlyphFactoryProvider : IGlyphFactoryProvider
    {
        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin)
        {
            return new BookmarkGlyphFactory();
        }
    }

    internal sealed class BookmarkGlyphFactory : IGlyphFactory
    {
        private static readonly Brush Fill = new SolidColorBrush(Color.FromRgb(0x2B, 0x79, 0xC2));
        private static readonly Brush Stroke = new SolidColorBrush(Color.FromRgb(0x7E, 0xB6, 0xE6));

        public UIElement? GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            if (!(tag is BookmarkGlyphTag bookmarkTag))
            {
                return null;
            }

            var badge = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(2),
                Background = Fill,
                BorderBrush = Stroke,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                ToolTip = bookmarkTag.Number.HasValue
                    ? $"Smart Bookmark [{bookmarkTag.Number.Value}]"
                    : "Smart Bookmark"
            };

            if (bookmarkTag.Number.HasValue)
            {
                badge.Child = new TextBlock
                {
                    Text = bookmarkTag.Number.Value.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, -1, 0, 0)
                };
            }
            else
            {
                badge.Child = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            return badge;
        }
    }
}
