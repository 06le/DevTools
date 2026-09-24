# Perforce 跨版本文件历史查看：本机调研与方案评估任务

> 用途：交给 Codex / Claude / 其他本机 AI Agent 执行。  
> 目标：先调查本机 Perforce / P4V / Visual Studio 能力，再决定是否需要开发小工具。  
> 原则：**优先复用现成 P4/P4V/P4VS 功能；能配置解决就不开发，能用薄封装解决就不做重型插件。**

---

## 1. 背景与当前问题

项目使用 Perforce。

项目组进入新的大版本阶段时，会建立新的分支/仓库路径并把整个项目迁移过去，例如：

```text
OB1 / 旧版本
    ↓ 分支合并 / integrate / branch / copy（具体方式待本机验证）
OB2 / dev
    ↓
后续版本
```

因此同一个文件在新版本路径中通常会重新从 `#1` 开始，例如：

```text
旧版本：
    //.../OB1/.../SomeFile.cpp#1 ~ #80

当前版本：
    //Project_Function/dev/ProjectX/.../SomeFile.cpp#1 ~ #15
```

实际开发中的痛点是：

> 在 OB2 / 当前仓库中查看一个文件的提交历史时，只能很方便地看到当前路径的直接历史。  
> 想追溯 OB1 甚至更早版本的修改，需要切到旧仓库/旧路径继续查询，人工成本很高。

期望最终达到：

> 在当前 Visual Studio / P4 工作环境中，对一个文件执行一次操作，就能较方便地看到它跨 OB1、OB2、后续版本的连续历史。

不要求 revision number 连续；重点是能看到：

- changelist
- 日期
- 提交人
- changelist 描述
- 当时的 depot path / revision
- 当前版本与旧版本之间的 branch / merge / integration 关系
- 最好可以继续打开 changelist、diff revision、Revision Graph

---

## 2. 当前已有证据

已观察到一个典型文件：

```text
//Project_Function/dev/ProjectX/Content/Logic/Environment/Building/BP_BuildingSmartCore.uasset
```

其 Revision Graph 中可以看到：

- `dev`
- `main`
- `release`
- `release_china`
- 其他历史版本路径

之间存在 integration 连线。

另外发现 changelist：

```text
CL 292290
```

具有以下特征：

- 描述中包含“分支合并”“添加功能仓库文件”等信息；
- 一次提交约 35372 个文件；
- 大量文件在 `//Project_Function/dev/ProjectX/...` 下为 `#1`；
- Revision Graph 中能看到与其他路径的关系。

### 当前判断

**较大概率：新版本虽然重新从 `#1` 开始，但 Perforce integration lineage 并没有完全丢失。**

这只是判断，不要直接当成事实。

必须通过 `p4 filelog -i`、`p4 integrated` 等命令对实际文件验证。

---

# 3. 任务目标

请先完成本机调查，不要立即开发插件。

最终回答以下问题：

1. 当前机器已经安装了哪些 Perforce 客户端、Visual Studio 插件？
2. 当前使用的是官方 P4VS 还是其他 VS 插件？
3. 当前 P4V / P4 CLI / P4VC / P4VS 的版本分别是什么？
4. 项目使用 classic depot/branch 还是 Streams？Workspace 如何配置？
5. 当前版本文件是否保留了来自 OB1/更早版本的 integration history？
6. P4V / P4VS 现成功能能否直接满足“跨版本连续查看历史”？
7. 如果不能直接满足：
   - 能否只通过 P4 CLI + P4VC + Visual Studio External Tool/命令实现？
   - 是否真的有必要做 Visual Studio 插件？
8. 给出**最小成本、最少维护、最符合日常使用习惯**的方案。

---

# 4. 安全约束

本阶段只做调查。

## 允许

可以执行只读操作，例如：

```text
p4 -V
p4 info
p4 set
p4 client -o
p4 where
p4 fstat
p4 filelog
p4 integrated
p4 changes
p4vc -V
p4vc -h
p4vc help ...
```

可以：

- 查看 Visual Studio 已安装扩展
- 查看 P4V / P4VS 配置
- 阅读本机配置文件
- 阅读项目脚本
- 创建临时分析脚本（如果必要）
- 查询官方文档

## [高风险] 禁止未经确认执行

不要执行任何可能修改 workspace 或 depot 状态的命令，包括但不限于：

```text
p4 edit
p4 add
p4 delete
p4 move
p4 integrate
p4 copy
p4 merge
p4 resolve
p4 submit
p4 revert
p4 sync
p4 clean
p4 reconcile（非预览模式）
```

不要：

- 修改 P4 server
- 修改 branch/stream spec
- 修改 workspace 配置
- 安装/卸载 Visual Studio 插件
- 修改 Visual Studio 设置
- 修改项目文件
- 重写历史

如确实需要上述操作，只在报告中提出，不实际执行。

---

# 5. 第一阶段：调查本机环境

## 5.1 检查 P4 工具

至少确认：

```text
where p4
where p4v
where p4vc

p4 -V
p4 info
p4 set
p4vc -V
```

记录：

- p4.exe 路径与版本
- P4V 路径与版本
- P4VC 是否存在
- P4PORT
- P4USER
- P4CLIENT
- P4CONFIG
- P4 Server 版本
- 当前 workspace root

敏感凭据不要写入报告。

---

## 5.2 判断 Workspace / Branch 类型

通过只读配置判断：

- 当前 workspace 是否绑定 Stream；
- 如果不是 Stream，当前 branch/depot 大致组织形式是什么；
- `dev / main / release / OB1 / OB2` 之间是通过什么机制形成关系。

不要仅根据目录名字推断。

---

## 5.3 检查 Visual Studio

确认：

- Visual Studio 版本；
- 已安装 Perforce 相关扩展；
- 是否安装官方 **P4VS - P4 for Visual Studio**；
- P4VS 版本；
- 是否还有第三方 P4 插件；
- 是否已经有右键 File History / Revision Graph / Time-Lapse 等入口。

### 已知可重点检查的现成方案

#### A. 官方 P4VS

Perforce 官方的 Visual Studio 插件。

官方文档表明 P4VS 支持：

- File History
- File History 中查看 Integrations
- Revision Graph
- Time-Lapse View
- changelist / diff 等操作

其中 Revision Graph 实际复用 P4V 组件。

**优先确认官方 P4VS 是否已经足够解决问题。**

不要因为想做工具而忽略现成功能。

#### B. P4V / P4VC

P4V 自带 `p4vc`，可以从命令行调用 P4V 的部分 UI，例如：

```text
p4vc history <file>
p4vc revgraph <file>
p4vc timelapse <file>
p4vc change <CL>
p4vc diff ...
```

如果 Visual Studio 中只缺一个“快捷入口”，优先考虑：

```text
Visual Studio External Tool / 简单命令
        ↓
p4vc
        ↓
复用 P4V UI
```

而不是重新实现完整历史 UI。

#### C. 第三方 Visual Studio 插件

如果本机没有官方 P4VS，或者官方插件在当前项目配置下不好用，可以调查 Visual Studio Marketplace 中的现有 Perforce 插件。

已知存在类似：

```text
P4 Lightweight Plugin for Visual Studio
```

其功能重点之一是从 Visual Studio 调用：

- P4V History
- Revision Graph
- Time-Lapse
- Diff
- Pending Changelists

但第三方插件不是默认推荐。

只有在以下情况下再考虑：

- 官方 P4VS 无法满足需求；
- 第三方插件功能刚好满足；
- 插件维护状态、VS版本兼容性、公司环境允许安装均可接受。

---

# 6. 第二阶段：验证跨版本历史是否真的存在

选取当前真实 workspace 中的一个典型文件。

优先使用：

```text
BP_BuildingSmartCore.uasset
```

先通过本地路径获取真实 depot path，例如：

```bash
p4 where <local-file>
p4 fstat <local-file>
```

然后执行只读验证。

---

## 6.1 `p4 filelog -i`

核心验证：

```bash
p4 filelog -i -l <depot-file>
```

必要时也测试：

```bash
p4 filelog -h -i -l <depot-file>
```

重点判断：

- 是否能从当前 `dev/OB2` 路径一路追到旧版本路径；
- 是否能看到旧版本的 changelist；
- branch point 在哪里；
- 是否存在历史断点。

如果输出过大，可以限制数量，但不要因为限制输出而误判“历史不存在”。

---

## 6.2 `p4 integrated`

执行：

```bash
p4 integrated <depot-file>
```

确认：

- 当前文件是从哪个 path branch/copy/merge 来的；
- 是否存在多条 source/target integration；
- CL 292290 附近是否对应建立当前版本文件的 integration。

---

## 6.3 `p4 changes -i`

评估：

```bash
p4 changes -i -l <depot-file>
```

是否可以直接得到接近日常需要的：

> “影响当前文件及其 integration 来源的 changelist 列表”

重点评估它是否适合作为“连续历史”数据源。

检查：

- 顺序是否合理；
- 是否包含无关 merge changelist；
- 是否重复；
- 是否能识别真正 originating change；
- 是否能满足开发者快速定位“谁在什么时候为什么改了这个文件”。

---

# 7. 第三阶段：测试现有 GUI 是否已经够用

对同一文件分别测试：

## P4V

- File History
- Integrations
- Revision Graph
- Time-Lapse View
- Time-Lapse 的 Branch History
- Time-Lapse 的 Originating Changelist

注意：

- `.cpp/.h` 等文本文件适合 Time-Lapse；
- `.uasset` 是二进制资产，不能假设文本 Time-Lapse 能解决问题；
- `.uasset` 重点测试 File History、Integrations、Revision Graph。

---

## Visual Studio + P4VS

检查是否可以：

```text
Solution Explorer
    ↓ 右键文件
Revisions / Show History
```

并观察：

- File History 是否能看到 integration；
- 是否需要额外切 Integrations 标签；
- 是否能直接打开 Revision Graph；
- 是否能快速看到 ancestor branch 的 changelist；
- 是否仍然需要人工“找到旧路径 → 再打开旧路径历史”。

---

# 8. 需要明确区分的三个概念

调查时不要混为一谈。

## 8.1 Direct History

当前 depot path 自身的 revision：

```text
dev/File.cpp#1
dev/File.cpp#2
dev/File.cpp#3
```

---

## 8.2 Integration / Branch History

例如：

```text
OB1/File.cpp#80
       ↓ branch
dev/File.cpp#1
```

表示文件 lineage。

---

## 8.3 用户真正想要的 Continuous History

理想展示：

```text
CL     Date        User       Path/Rev              Description
-------------------------------------------------------------------
350000 2026-09...  A          dev/File.cpp#12       ...
340000 2026-08...  B          dev/File.cpp#11       ...
292290 2025-11...  C          dev/File.cpp#1        branch from ...
291000 2025-11...  D          OB1/File.cpp#80       ...
280000 2025-10...  E          OB1/File.cpp#79       ...
...
```

即：

> 使用者不关心 depot path 是否换过，希望看到逻辑上“这个文件”的完整生命周期。

P4V Revision Graph 能表达 lineage，不代表日常 File History 已经能以这种方式展示。

需要实际测试后判断。

---

# 9. 方案优先级

完成调查后，请按以下优先级选择方案。

---

## 方案 A：直接使用 P4VS / P4V 现成功能

### 条件

如果官方 P4VS 已经能够比较顺畅地：

```text
当前文件
→ History
→ integration/ancestor history
→ changelist/diff
```

则不开发任何工具。

只需要输出：

- 推荐的操作流程；
- 需要开启的 P4V/P4VS 设置；
- 建议快捷键；
- 必要时写一份团队使用说明。

### 优先级

**最高。**

---

## 方案 B：Visual Studio 快捷入口 + P4VC

如果问题只是：

> P4V 功能够用，但从 VS 进去太麻烦。

则优先做非常薄的一层：

```text
Visual Studio 当前文件
        ↓
External Tool / Command
        ↓
p4vc history / revgraph / timelapse
        ↓
P4V
```

这种方案：

- 几乎没有业务逻辑；
- 不需要重新解析 P4 历史；
- 不需要维护复杂 VS UI；
- P4V 升级后能力可以直接复用。

如果 Visual Studio External Tools 无法方便获得当前文件参数，再考虑极小 VS 扩展。

---

## 方案 C：CLI 连续历史脚本

如果：

- P4 lineage 完整；
- `p4 filelog -i` / `p4 changes -i` 能拿到足够数据；
- 但 P4V/P4VS 展示不符合日常使用习惯；

则做一个独立的小脚本/CLI：

```text
p4-continuous-history <local-file>
```

它只负责：

1. local path → depot path；
2. 查询 inherited/integration history；
3. 整理为时间线；
4. 输出 changelist / date / author / path / rev / description；
5. 可选调用 `p4vc change` / `p4vc revgraph`。

### [设计决策]

优先让 P4 CLI 作为**唯一数据源**。

不要自己扫描：

```text
OB1
OB2
OB3
release
main
```

猜测同名文件。

只要 integration lineage 存在，就应该沿 P4 记录的关系追踪。

---

## 方案 D：Visual Studio 小插件

只有在 C 已经验证有效，并且确实需要更好的交互时再做。

建议插件保持很薄：

```text
当前编辑文件
     ↓
右键：
P4 Continuous History
     ↓
调用现有查询模块
     ↓
Tool Window 展示
```

建议最小 UI：

| CL | 时间 | 用户 | Path / Revision | Action | Description |
|---|---|---|---|---|---|

可选右键操作：

```text
Open Changelist
Open Revision Graph
Open P4V History
Diff Previous
Copy Depot Path
```

这些动作尽量调用：

```text
p4vc
p4
```

不要自己重新实现：

- P4 登录
- diff viewer
- Revision Graph
- changelist viewer

### [Codex自行决定]

具体使用：

- VSIX
- AsyncPackage
- ToolWindow
- command registration
- process invocation
- JSON/typed parsing

由实现 Agent 根据当前 VS 版本和已有项目结构决定。

---

# 10. [高风险] History lineage 断裂时的处理

如果验证发现某些版本之间是：

```text
Windows Copy
→ p4 add
```

而不是 Perforce branch/integrate/copy，导致 integration lineage 丢失：

**不要为了实现工具去修改现有 depot 历史。**

先报告：

- 哪个版本点开始断裂；
- 哪些文件/路径受影响；
- 是否存在稳定的 OB1 → OB2 路径映射；
- 是否只能通过命名约定进行 fallback。

可以考虑工具层 fallback：

```text
优先：P4 integration lineage
       ↓ 不存在
可选：配置式 path mapping
```

例如：

```text
//Project_Function/dev/ProjectX/...
    ←→
//Project_Function/OB1/ProjectX/...
```

但这种映射必须显式配置，不能通过“文件名相同”自动认定同一个逻辑文件。

---

# 11. 性能注意事项

项目体量很大，一次版本迁移可能涉及 3 万+ 文件。

因此工具如果需要开发：

- 只查询用户当前选择的单文件；
- 不默认扫描整个 depot；
- 不递归跑全项目 filelog；
- 不预构建全项目历史数据库；
- 对 `filelog -i` 等查询设置合理 UI 超时/取消机制；
- 如有必要，对单文件结果做短期缓存。

不要为了一个文件历史功能引入大型索引系统。

---

# 12. 最终输出要求

完成调查后，不要直接交代码。

先输出一份简短的“人类审核层”。

格式：

```markdown
# 结论

推荐方案：A / B / C / D

一句话说明原因。

# 本机现状

- Visual Studio：
- P4VS：
- P4V：
- p4：
- p4vc：
- Workspace 类型：
- Server：
- Integration lineage：

# 关键验证结果

1. `p4 filelog -i`：
2. `p4 integrated`：
3. `p4 changes -i`：
4. P4V：
5. P4VS：

# [设计决策]

推荐最终工作流：

Visual Studio
→ ...
→ ...

# [风险 / 限制]

...

# 是否需要开发

不需要 / 需要一个薄脚本 / 需要一个小 VS 插件

预计涉及模块：
...

# 下一步

只列 1~3 个下一步。
```

然后再附详细调查记录。

---

# 13. 判断标准

方案选择时按以下顺序排序：

1. **日常使用方便**
2. **不需要维护自研代码**
3. **复用官方 P4/P4V/P4VS 能力**
4. **不修改公司现有 Perforce 工作流**
5. **查询准确**
6. **性能可接受**
7. **实现成本低**

不要为了“技术上更完整”而过度设计。

---

# 14. 当前已知的官方能力（供核实，不要盲信）

根据当前 Perforce 官方文档：

### P4VS

官方 P4 for Visual Studio 支持：

- File History；
- File History 中查看 Integrations；
- Revision Graph；
- Time-Lapse View。

Revision Graph 会复用 P4V 组件。

### P4 CLI

重点命令：

```text
p4 filelog -i
```

用于包含 inherited / integration 相关文件历史。

```text
p4 integrated
```

用于查看已经提交的 integration 关系。

```text
p4 changes -i
```

可以包含影响通过 integration 关联文件的 changelist，值得验证是否可以直接作为“连续历史”的数据源。

### P4VC

P4V 附带 P4VC 命令行入口，可直接打开：

```text
history
revisiongraph / revgraph
timelapse
change
diff
```

这意味着即使最终需要一点自研，也应优先考虑：

> 自研“入口/编排”，而不是自研 P4 历史查看器。

---

# 15. 参考资料关键词

需要在线核实时，优先使用 Perforce 官方文档，搜索：

```text
P4VS Display revision history
P4VS Revision Graph
P4V Revision Graph
P4V Time-Lapse Branch History
P4V Originating Changelist
p4 filelog -i
p4 integrated
p4 changes -i
P4VC command line client for P4V components
```

Visual Studio 插件优先调查：

```text
P4VS - P4 for Visual Studio
```

其次才考虑第三方：

```text
P4 Lightweight Plugin for Visual Studio
```

---

## 最终原则

**先证明现有工具不够用，再开发。**

理想结果并不是“做出一个插件”，而是：

> 从 Visual Studio 当前文件出发，用最少的操作可靠地追溯该文件跨版本、跨 depot path 的真实 Perforce 历史。
