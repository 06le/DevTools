# Smart Bookmarks

Visual Studio 2022 书签扩展，面向 C++ / Unreal 源码阅读。

- 编号书签 `0~9`（同一跟踪位置：设置 / 覆盖 / 清除）
- 不限数量的普通命名书签
- 一级文件夹 + 未分类，可指定新建书签的默认文件夹
- 每个解决方案一份 JSON，存在 `.vs` 下
- 打开文档期间用跟踪点定位，插入行后「转到」仍落在原代码
- 编辑器左边距 glyph：编号书签显示 `0~9`，普通书签显示纯标记

第一版**不绑定**快捷键，需要自己在键盘选项里绑。

## 克隆

```bat
git clone https://github.com/06le/DevTools.git D:\DevTools
```

本仓库是工具箱。本扩展在 `SmartBookmarks\`。

## 前置条件

- Visual Studio 2022
- 工作负载 **Visual Studio 扩展开发**（VSSDK）

没有该工作负载时，MSBuild 无法产出 `.vsix`。

## 编译

```bat
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" D:\DevTools\SmartBookmarks\SmartBookmarks.sln /p:Configuration=Debug /restore
```

产出：`D:\DevTools\SmartBookmarks\src\SmartBookmarks\bin\Debug\SmartBookmarks.vsix`

命令行编译**不会**安装到日常使用的 Visual Studio。

扩展版本写在：

`D:\DevTools\SmartBookmarks\src\SmartBookmarks\source.extension.vsixmanifest`

这是源码清单。`bin\` / `obj\` 下的 `extension.vsixmanifest` 是编译产物，改那些没用。

## 安装到日常 Visual Studio（任意解决方案）

F5 只会启动实验实例（Exp）。要在 XGame / Engine / 其它 `.sln` 里用：

1. 关掉所有 Visual Studio 窗口（含 F5 拉起的 Exp 实例）。
2. 双击 `D:\DevTools\SmartBookmarks\src\SmartBookmarks\bin\Debug\SmartBookmarks.vsix`  
   或执行：

```bat
"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe" "D:\DevTools\SmartBookmarks\src\SmartBookmarks\bin\Debug\SmartBookmarks.vsix"
```

3. 确认安装到 Visual Studio 2022，然后照常打开任意解决方案。
4. 绑一次快捷键：`工具 → 选项 → 环境 → 键盘`，搜索 `SmartBookmarks`。

书签按解决方案隔离（`{SolutionDir}/.vs/SmartBookmarks/bookmarks.json`）。装一次即可覆盖所有工程；每个 `.sln` 各自一份列表。

卸载：`扩展 → 管理扩展 → 已安装 → Smart Bookmarks → 卸载`，然后重启 VS。

### 安装器提示「此扩展已安装到所有适用的产品」

这不是安装失败，是 **同一 Id + 同一版本已经装过**，安装器拒绝覆盖。不必先卸载。把版本号加一档再装，就会直接替换旧版，各解决方案的 `bookmarks.json` 会保留。

本扩展只声明 VS 2022（Community / Professional / Enterprise，`[17.0,18.0)`）。本机日常 VS 2022 和 F5 的 Exp 实例都会算作适用产品。当前磁盘上的 `.vsix` 若仍是 `0.1.0`，而这两处已经是 `0.1.0`，再双击就会得到这句提示。

一键覆盖安装（关干净 VS 后执行）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\DevTools\SmartBookmarks\Install-Overwrite.ps1
```

脚本会：检查没有 `devenv.exe` → 源码清单版本已经高于已装版本就保持该版本并强制 Rebuild（增量编译可能还打出旧 `.vsix`）→ 源码版本不够高才把补丁号 +1 → 覆盖安装。不要先卸载。强制再加一档版本可加 `-BumpVersion`。右键“使用 PowerShell 运行”时，结束（含失败）会停住，按回车才关窗口；从已有终端执行则不停。

手动处理：

1. 打开 `D:\DevTools\SmartBookmarks\src\SmartBookmarks\source.extension.vsixmanifest`，把 `Identity` 的 `Version` 改成 **高于** 已安装版本（例如已装 `0.1.0`，改成 `0.1.1`）。
2. **重新编译**，让 `bin\Debug\SmartBookmarks.vsix` 带上新版本（只改清单、不重编，双击的仍是旧包）。
3. 关掉所有 `devenv.exe` 后再跑 VSIXInstaller。
4. 新版本会替换旧版本，各解决方案下的 `bookmarks.json` 会保留。

若安装器说扩展正在使用，说明 VS 还在跑（包括 F5 的 Exp 实例）。

查看已装版本：`扩展 → 管理扩展 → 已安装 → Smart Bookmarks`。

## 更新

改完源码后，关掉 Visual Studio，再跑：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\DevTools\SmartBookmarks\Install-Overwrite.ps1
```

或手动：先把 `src\SmartBookmarks\source.extension.vsixmanifest` 的 `Version` 加一档，再按上面的编译命令重编，最后对新 `.vsix` 跑同一条 `VSIXInstaller.exe`。相同扩展 Id、更高版本会替换旧版，不必卸载。重新打开 Visual Studio 后，各解决方案已有的 JSON 会保留。

## 调试

1. 用 VS2022 打开 `D:\DevTools\SmartBookmarks\SmartBookmarks.sln`。
2. F5 → `devenv.exe /rootsuffix Exp`。
3. 在 **Exp** 实例里打开一个 C++ 解决方案。

F5 装的是 Exp hive，日常 VS 看不到。不要对默认 hive 跑 VSIXInstaller 来调试。

## 命令（工具 → Smart Bookmarks）

在 `工具 → 选项 → 环境 → 键盘` 里搜索这些命令：

| 命令 | 建议快捷键 |
|---|---|
| `SmartBookmarks.ToggleNumberedBookmark0` … `9` | `Ctrl+Shift+0` … `9` |
| `SmartBookmarks.GoToNumberedBookmark0` … `9` | `Alt+0` … `9` |
| `SmartBookmarks.ToggleNormalBookmark` | `Ctrl+Shift+B` |
| `SmartBookmarks.ShowToolWindow` | 不绑 |

`ToggleNumberedBookmarkN` 在当前光标处：

- 空槽 → 设置
- 不同行 → 覆盖（保留它原来所在的文件夹）
- 同一行 → 清除（按行判定，光标停在该行任意列都算同一处）

## 工具窗口

`工具 → Smart Bookmarks → Show Smart Bookmarks`

- 双击书签转到
- Ctrl / Shift 多选
- 右键：转到、重命名、移到文件夹、删除 / 清除
- 文件夹右键：设为默认文件夹、重命名文件夹、删除文件夹（书签回到未分类）
- 全部清除会二次确认
- 列表就地刷新，不必离开工具窗口再回来

v1 没有拖放。

## 持久化

`{SolutionDir}/.vs/SmartBookmarks/bookmarks.json`

切换解决方案时会保存旧仓库、清空窗口、再加载新仓库。

## 默认文件夹

文件夹右键 **Set as Default Folder**，之后**新建**的编号 / 普通书签都进这个文件夹，列表里该文件夹标 `(default)`。

- 对已是默认的文件夹再点一次即取消；对 `Uncategorized` 执行也是取消
- 覆盖已有编号书签（把 `[1]` 挪到别处）**不改变**它原来所在的文件夹
- 删掉默认文件夹后回到未分类
- 按解决方案记录，存在 `bookmarks.json` 的 `defaultFolderId`

## 跟踪

文档保持打开时，「转到」走 `ITrackingPoint`，书签上方插入行仍会落到原代码。窗口里的行号可能暂时过期，直到关闭文档（关闭时把新行号写回 JSON）。重启 / Git pull 之后只用存储的行号。

## 编辑器 glyph

左边距会在书签行显示蓝色标记。编号书签带数字；同一行两种书签并存时，编号优先。文件保持打开期间，glyph 跟随实时跟踪。

## 已知限制

- 没有嵌套文件夹、云同步，也不会跨 Git 修订做模糊重定位
- 缺失文件会留在列表里，直到你删除 / 清除
- 没有默认快捷键
