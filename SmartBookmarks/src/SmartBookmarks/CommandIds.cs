using System;

namespace SmartBookmarks
{
    internal sealed partial class PackageGuids
    {
        public const string SmartBookmarksString = "541535E1-A0E3-4A81-BCBC-B4377982196F";
        public static readonly Guid SmartBookmarks = new Guid(SmartBookmarksString);
    }

    internal sealed partial class PackageIds
    {
        public const int MenuGroup = 0x0001;
        public const int ToggleNumberedGroup = 0x0002;
        public const int GoToNumberedGroup = 0x0003;
        public const int MiscGroup = 0x0004;

        public const int ToggleNumberedBookmark0 = 0x0100;
        public const int ToggleNumberedBookmark1 = 0x0101;
        public const int ToggleNumberedBookmark2 = 0x0102;
        public const int ToggleNumberedBookmark3 = 0x0103;
        public const int ToggleNumberedBookmark4 = 0x0104;
        public const int ToggleNumberedBookmark5 = 0x0105;
        public const int ToggleNumberedBookmark6 = 0x0106;
        public const int ToggleNumberedBookmark7 = 0x0107;
        public const int ToggleNumberedBookmark8 = 0x0108;
        public const int ToggleNumberedBookmark9 = 0x0109;

        public const int GoToNumberedBookmark0 = 0x0200;
        public const int GoToNumberedBookmark1 = 0x0201;
        public const int GoToNumberedBookmark2 = 0x0202;
        public const int GoToNumberedBookmark3 = 0x0203;
        public const int GoToNumberedBookmark4 = 0x0204;
        public const int GoToNumberedBookmark5 = 0x0205;
        public const int GoToNumberedBookmark6 = 0x0206;
        public const int GoToNumberedBookmark7 = 0x0207;
        public const int GoToNumberedBookmark8 = 0x0208;
        public const int GoToNumberedBookmark9 = 0x0209;

        public const int ToggleNormalBookmark = 0x0300;
        public const int ShowToolWindow = 0x0400;
    }
}
