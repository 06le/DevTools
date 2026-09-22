# Smart Bookmarks

Visual Studio 2022 bookmarks for C++ / Unreal source reading.

- Numbered bookmarks `0~9` (set / overwrite / clear at same tracked position)
- Unlimited named normal bookmarks
- One-level folders + Uncategorized
- Per-solution JSON under `.vs`
- Open-document tracking so Go To still hits the original line after inserts
- Glyph in the editor left margin: numbered bookmarks show `0~9`, normal bookmarks show a plain mark

First version does **not** bind keyboard shortcuts. Bind them yourself.

## Clone

```bat
git clone https://github.com/06le/DevTools.git D:\DevTools
```

This repo is a toolbox. This extension lives in `SmartBookmarks\`.

## Prerequisites

- Visual Studio 2022
- Workload **Visual Studio extension development** (VSSDK)

Without that workload, MSBuild cannot produce a `.vsix`.

## Build

```bat
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" D:\DevTools\SmartBookmarks\SmartBookmarks.sln /p:Configuration=Debug /restore
```

Output: `D:\DevTools\SmartBookmarks\src\SmartBookmarks\bin\Debug\SmartBookmarks.vsix`

Command-line build does **not** install into daily Visual Studio.

## Install into daily Visual Studio (any solution)

F5 only starts an Experimental Instance. To use the extension in XGame / Engine / any other `.sln`:

1. Close all Visual Studio windows.
2. Double-click `D:\DevTools\SmartBookmarks\src\SmartBookmarks\bin\Debug\SmartBookmarks.vsix`  
   or run:

```bat
"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe" "D:\DevTools\SmartBookmarks\src\SmartBookmarks\bin\Debug\SmartBookmarks.vsix"
```

3. Confirm install for Visual Studio 2022, then open any solution as usual.
4. Bind keys once: `Tools → Options → Environment → Keyboard`, search `SmartBookmarks`.

Bookmarks are per solution (`{SolutionDir}/.vs/SmartBookmarks/bookmarks.json`). Installing once covers every project; each `.sln` keeps its own list.

Uninstall: `Extensions → Manage Extensions → Installed → Smart Bookmarks → Uninstall`, then restart VS.

## Update

After changing source:

1. Close Visual Studio (installer cannot overwrite a loaded extension).
2. Rebuild with the Build command above.
3. Run the same `VSIXInstaller.exe` command on the new `.vsix`. Same extension id replaces the old version.
4. Reopen Visual Studio. Existing per-solution JSON is kept.

If the installer says the extension is in use, VS is still running (including Exp instances from F5).

## Debug

1. Open `D:\DevTools\SmartBookmarks\SmartBookmarks.sln` in VS2022.
2. F5 → `devenv.exe /rootsuffix Exp`.
3. In the **Exp** instance, open a C++ solution.

Do not run VSIXInstaller against the default hive.

## Commands (Tools → Smart Bookmarks)

Search these in `Tools → Options → Environment → Keyboard`:

| Command | Recommended shortcut |
|---|---|
| `SmartBookmarks.ToggleNumberedBookmark0` … `9` | `Ctrl+Shift+0` … `9` |
| `SmartBookmarks.GoToNumberedBookmark0` … `9` | `Alt+0` … `9` |
| `SmartBookmarks.ToggleNormalBookmark` | `Ctrl+Shift+B` |
| `SmartBookmarks.ShowToolWindow` | none |

`ToggleNumberedBookmarkN` at the current caret:

- empty slot → set
- different location → overwrite
- same tracked location → clear

## Tool Window

`Tools → Smart Bookmarks → Show Smart Bookmarks`

- Double-click a bookmark to Go To
- Ctrl / Shift multi-select
- Right-click: Go To, Rename, Move to Folder, Delete / Clear
- Folder right-click: Rename Folder, Delete Folder (bookmarks move to Uncategorized)
- Clear All asks for confirmation

No drag-and-drop in v1.

## Persistence

`{SolutionDir}/.vs/SmartBookmarks/bookmarks.json`

Switching solutions saves the old store, clears the window, and loads the new one.

## Tracking

While a document stays open, Go To uses `ITrackingPoint`, so inserts above the bookmark still land on the original code. The window line number may stay stale until the document is closed (close writes the new line back to JSON). After restart / Git pull, only the stored line is used.

## Editor glyphs

The left glyph margin shows a blue mark on bookmarked lines. Numbered bookmarks include the digit; a numbered bookmark wins if both kinds sit on the same line. Glyphs follow live tracking while the file stays open.

## Known limitations

- No nested folders, cloud sync, or fuzzy relocation across Git revisions
- Missing files stay in the list until you Delete / Clear them
- No default keybindings
