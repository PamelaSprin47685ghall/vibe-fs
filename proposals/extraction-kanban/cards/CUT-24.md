# CUT-24：文件读取、锚点与原子编辑

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-24) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`READY`（由 `CUT-00` 完成准备核对后转入，待认领）
- **所属泳道 / 波次**：仓库 / W1 独立卡
- **切换前置**：`CUT-00`（已完成，见 [CUT-00](CUT-00.md)）
- **关联护栏（D-05 & 与 CUT-25 的边界）**：
  - 按 [CARDS.md](../CARDS.md#cut-24) 与 [COVERAGE.md](../COVERAGE.md)：**本卡（`CUT-24`）只承接真实文件观察、不可变 UTF-8 快照、有序锚点解析、Glob/Grep 枚举以及原子编辑/事务计划（`Anchor`、`AnchorFs`、`Utf8Fs`、`GlobFs`、`MutationFs`、`Transaction`）**，不含角色权限决策、不含 JS 沙箱子进程（`Process/JsSandbox.fs`）、不含生成式 `JsProgram` 基类模板与 OpenCode 工具宿主协议（以上均归 `CUT-25`）；
  - 新库必须直接操作有界文件系统与纯事务模型，**严禁通过回调旧 `ToolHost` 来实现所谓的“独立文件操作”**。
- **零阻塞结论**：底层文件读取（`Utf8Fs`）、gitignore/wildmatch 枚举（`GlobMatch` + `GlobFs`）、锚点与正则搜索（`Anchor` + `AnchorFs`）以及事务预检与原子写/回滚（`Transaction` + `MutationFs`）在主仓中已与 OpenCode 工具层解耦，无会话或 LLM 依赖，可零阻塞独立抽出。

## 2. 草稿概念名/路径与主仓真实模块/`.fsproj` 映射

委托说明及 `CARDS.md` 中提及 `src/Wanxiangshu/Repository/Programming/` 下的文件快照、锚点与原子编辑。在主仓当前基线（`b322c180b`）中，其真实源码与 `.fsproj` 映射如下：

| 职责分片 | 主仓真实源码路径（含 `.fsi`） | 所属编译单元（`.fsproj`） | 去向判定 |
|---|---|---|---|
| Wildmatch / Gitignore 路径匹配内核 | `src/Wanxiangshu/Foundation/GlobMatch.fs(.fsi)` | `Wanxiangshu.Owner.runtime-platform.foundation-globmatch.fsproj` | **抽出核心（A1）**：`* / ** / ? / [...] / {a,b}` 展开与 gitignore 规则匹配（供 `CUT-24` 与 `CUT-26` 共用） |
| 统一错误码与锚点规格契约 | `src/Wanxiangshu/Repository/Programming/Js/Failure.fs(.fsi)`<br>`src/Wanxiangshu/Repository/Programming/Js/Anchor.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj` | **抽出核心（A2）**：`JsFailure` 稳定机器码、`AnchorSpec`（`Exact | Regex`）、`AnchorDeclaration` 校验 |
| 纯事务模型、单意图校验与预检计划 | `src/Wanxiangshu/Repository/Programming/Js/Transaction.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj` | **抽出核心（A3）**：`JsStagedMutation`、`JsReadSnapshot`、`validateSingleIntent`、`validateTargets`、`validateFreshness`、`preflight`、`commitPlan`、`rollbackPlan` |
| 严格 UTF-8 文件读取与二进制拒绝 | `src/Wanxiangshu/Repository/Programming/Js/Utf8Fs.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj` | **抽出物理文件原语（B1）**：`readUtf8` / `readUtf8Classified`（fatal UTF-8 解码 + `0x00` NUL 字节拒绝为 `InvalidUtf8`） |
| 有界工作树 Glob 枚举与 Grep / Anchor 定位 | `src/Wanxiangshu/Repository/Programming/Js/GlobFs.fs(.fsi)`<br>`src/Wanxiangshu/Repository/Programming/Js/AnchorFs.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj` | **抽出物理文件原语（B2）**：跳过 `.git`、不跟随符号链接、遵守分级 `.gitignore` 的枚举与带读取快照的 `grep` / `findAnchor` / `requireUnique` |
| 路径逃逸拦截、原子写盘与回滚执行 | `src/Wanxiangshu/Repository/Programming/Js/MutationFs.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj` | **抽出物理文件原语（B3）**：`resolveToolPath`（含符号链接 realpath 边界检查）、临时文件 + rename 原子落盘、失败按预期当前内容安全回滚 |
| 文件与事务公开测试 Surface | `src/Wanxiangshu/Repository/Programming/Js/FilesystemSurface.fs(.fsi)`<br>`src/Wanxiangshu/Repository/Programming/Js/TransactionSurface.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.runtime.fsproj` | **主仓适配表面**：桥接新库，维持现有测试调用入口 |
| 受限 JS 沙箱、能力生成与宿主工具流 | `src/Wanxiangshu/Process/JsSandbox.fs(.fsi)`<br>`src/Wanxiangshu/Repository/Programming/Js/{Capability,Description,Surface,TransactionStore,IntegrationRules,ToolsBindings,GeneratorSurface,RuntimeSurface,WorkflowSurface}.fs(.fsi)`<br>`src/Wanxiangshu/Repository/Programming/Js/OpenCode/{ToolWorkflow,ToolHost,ToolHostSurface}.fs(.fsi)` | `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj`<br>`Wanxiangshu.Owner.repository-programming.repository-programming-js-toolbindings.fsproj`<br>`Wanxiangshu.Owner.repository-programming.runtime.fsproj` | **暂留主仓，移交 `CUT-25`** |

## 3. 目标

- **用户/维护者当前损失**：当前文件读取、锚点解析、原子写盘与 `JsSandbox`、角色权限矩阵（`Capability.fs`）打包在同一个 `.fsproj`（`Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj`）中；文件操作算法无法在不引入 Office 权限与 EventStore 的情况下被独立复用和验证。
- **这次完成后可少维护什么**：把纯文件快照、锚点定位、Glob/Grep 与原子修改/回滚原语独立为自治个体（同时将 `Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj` 拆分为独立的文件事务单元与 JS 能力/沙箱单元），后续 `CUT-25` 只需将角色权限与生成式 JS 基类组合到该文件能力之上。
- **明确不做**：
  - 不把角色权限矩阵（`OfficeCapability` / `PlanReviewRequired`）、工具文档生成（`Description.fs`）或 `JsSandbox` 子进程求值塞进本卡的文件库（归 `CUT-25`）；
  - 不在 `edit()` 中做任何模糊/近似自动修改（近似候选仅用于生成 `EDIT_NOT_FOUND` 诊断文本，绝不自动应用）；
  - 不允许跨越工作区根目录或通过符号链接跳出工作区读写文件。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/repository-programming/WHAT.md`：
    - `[007]` 锚点解析与 `FileView.text(from, to)` 有序前向搜索、内置 `^`/`$` 与 `name±N` 偏移裁剪；
    - `[008]` `glob` 与 `grep` 遵循 gitignore/wildmatch、省略 `.git`、不跟随符号链接、跳过非 UTF-8 文件；
    - `[009]` 单目标单变更意图（同一 canonical path 二次修改拒绝为 `DUPLICATE_MUTATION_TARGET`）与全有或全无事务提交；
    - `[010]` 统一稳定错误码（`INVALID_ANCHOR_PATTERN`、`ANCHOR_NOT_FOUND`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS`、`EDIT_OVERLAP`、`FILE_CHANGED` 等）；
    - `[011]` 路径边界（拒绝 `..` 逃逸与指向工作区外的符号链接 `PATH_DENIED`）与 UTF-8 完整性（拒绝坏 UTF-8 与 `0x00` 字节 `INVALID_UTF8`）；
    - `[016]` 读取快照与写前新鲜度校验（`FILE_CHANGED`）及崩溃恢复安全回滚；
    - `[025]`—`[027]` `edit(path, changes)` 精确匹配、多重性 `all: true`、非重叠规划、CRLF 保持与只读零暂存诊断。

## 5. 自治个体（A / B / C）

- **A（纯锚点、编辑规划与事务预检内核：`Anchor` + `EditPlanner` + `JsTransaction`，抽入新库）**：
  - 负责：解析与校验 `AnchorSpec` / 有序锚点表、计算 `text(from, to)` 与 `name±N` 切片、在不可变文件快照上规划 `edit(path, changes)`（检测零命中 `EDIT_NOT_FOUND`、多命中未设 `all: true` 的 `EDIT_AMBIGUOUS`、原始区间重叠 `EDIT_OVERLAP`、一致 CRLF 行尾恢复）、执行 `JsTransaction.preflight`（单路径单意图校验、目标存在性校验、全部已读快照与待改文件的当前内容新鲜度比对）并生成确定性的 `commitPlan` 与 `rollbackPlan`。
  - 拥有状态：纯不可变数据结构与纯函数。
- **B（有界工作区物理文件执行器：`Utf8Fs` + `GlobFs` + `AnchorFs` + `MutationFs`，抽入新库）**：
  - 负责：在显式给定的 `workspaceRoot` 下执行路径规范化与符号链接边界检查（`resolveToolPath`）、严格 UTF-8 读取（`readUtf8Classified`）、gitignore 感知枚举（`glob` / `grep`）、临时文件写入与原子 `rename` 提交（`commitPlan`）以及按 `expectedCurrent` 比对的安全回滚（`rollbackPlan` / `undoIfMatches`）。
  - 与 A 不共享可变值：B 不隐式持有会话状态，每次操作均显式接收 `root` 与 A 生成的纯计划。
- **C（主仓 `ToolWorkflow` / `ToolHost` / `TransactionStore` 组合层，由 `CUT-25` 深化）**：
  - 负责：在执行前向 EventStore 写入 `JsTransactionPrepared`，调用 B 执行 `commitPlan`，成功后写入 `JsTransactionCommitted`；若进程在 `Prepared` 后崩溃，恢复期仅当磁盘当前文本仍等于 `NewText` 时才用 `OriginalText` 还原，绝不盲目覆盖用户或其他进程后来写入的新内容（`repository-programming-016`）。

## 6. 公开消息与最小完整契约

- **输入字段 / 字节 / 范围 / 身份**：
  - `root : string`（工作区绝对根路径）、`relativePath : string`（工作区内相对路径）；
  - `matches : (string * string * AnchorSpec) list`（有序锚点声明 `[beginName, endName, pattern]`）；
  - `changes : EditChange list`（规范形态 `{ find: string | RegExp, put: string, all?: bool }`）；
  - `mutations : JsStagedMutation list`（`Rewrite(path, originalText, newText) | Create(path, text)`）。
- **成功结果及其精确保证**：
  - `readUtf8Classified` 返回完整 UTF-8 字符串，保留原始换行符与末尾是否有 LF 的精确字节形态；
  - `edit` 在同一原始快照上完成全部非重叠替换，若原文件一致使用 CRLF 而 `find`/`put` 使用 LF，自动在结果中保持 CRLF；当 `put` 与原区间字节完全相同时返回 `changed = false` 且零暂存；
  - `commitPlan` 仅在 `preflight` 全部通过后以同目录临时文件 + 原子 `rename` 落盘。
- **拒绝结果及零副作用保证**：
  - 路径越界或符号链接指向根外 → `Error (PathDenied path)`；
  - 非法 UTF-8 序列或含 `0x00` 字节 → `Error (InvalidUtf8 path)`；
  - 锚点重名、使用保留名 `^`/`$`、`begin = end`、空 pattern、前向未命中或反向切片 `start > end` → `Error AnchorEmptyContent / AnchorInvalidPattern / AnchorNotFound`；
  - 编辑零命中、多命中未设 `all: true`、原始 span 重叠、奇异字段 → `Error (EditNotFound / EditAmbiguous / EditOverlap / InvalidEdit)`，**零暂存、零写盘**；
  - 同一路径两次 mutation → `Error (DuplicateMutationTarget path)`；
  - 预检时发现任一已读快照或目标文件在磁盘上已被外部修改 → `Error (FileChanged path)`，**零写盘**；
  - 提交中途某文件写盘失败 → 立即按 `rollbackPlan` 回滚本次已写文件，并返回 `Error TransactionCommitFailed`。

## 7. 主仓调用方清单与对应测试套件

### 7.1 主仓调用方清单

1. **JS 编程工具与沙箱桥接**：
   - `src/Wanxiangshu/Process/JsSandbox.fs`（子进程沙箱内 `file` / `edit` / `rewrite` / `write` 实现，将在 `CUT-25` 中与本卡保持同源）
   - `src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolWorkflow.fs`（宿主持久化事务准备、预检、提交与回滚主链）
   - `src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolHost.fs`、`ToolHostSurface.fs`
   - `src/Wanxiangshu/Repository/Programming/Js/{FilesystemSurface,TransactionSurface,WorkflowSurface}.fs`
2. **跨模块 `GlobMatch` 消费者**：
   - `src/Wanxiangshu/Requirement/Grounding/Catalog.fs`（`CUT-26` 消费 `GlobMatch.isMatch` 解析 `APPLIES-TO` 规则）

### 7.2 对应测试套件

- `requirements/repository-programming/tests/007.test.mjs`（有序锚点与 `FileView.text` 切片）
- `requirements/repository-programming/tests/008.test.mjs`（`glob` 与 `grep` 枚举及 gitignore/symlink 边界）
- `requirements/repository-programming/tests/009.test.mjs`（单目标单变更与事务原子性）
- `requirements/repository-programming/tests/010.test.mjs`（错误码契约）
- `requirements/repository-programming/tests/011.test.mjs`（路径逃逸、符号链接与 UTF-8/NUL 门禁）
- `requirements/repository-programming/tests/016.test.mjs`（读取快照新鲜度 `FILE_CHANGED` 与崩溃恢复回滚）
- `requirements/repository-programming/tests/025.test.mjs`、`026.test.mjs`、`027.test.mjs`（`edit()` 精确替换、非重叠规划、CRLF 保持与只读诊断）

## 8. 必须能红的最小反例与拆除测试设计

1. **反例 1（同名段落误改与非唯一替换）**：在含两处相同文本的文件上调用未设置 `all: true` 的 `edit`，必须返回 `EDIT_AMBIGUOUS` 且文件分毫不改；调用有序锚点 `file(path, [["a1","a1e","foo"], ["a2","a2e","foo"]])` 必须按游标顺序分别命中第 1 处和第 2 处，而非重复命中第 1 处（对应 `repository-programming-007/025/026`）。
2. **反例 2（重叠编辑或同路径二次变更）**：在同一 `edit` 数组中传入两个原始匹配区间重叠的 change，必须拒绝为 `EDIT_OVERLAP`；对同一路径先后暂存两次 `edit` / `rewrite`，必须拒绝为 `DUPLICATE_MUTATION_TARGET`（对应 `repository-programming-009/026`）。
3. **反例 3（CRLF / 末尾无 LF 被破坏，或含 NUL 字节二进制文件被当作文本改写）**：对一致使用 CRLF 且末尾无换行符的文件执行 LF 字面量 `edit`，结果必须保留 CRLF 与末尾无换行形态；对含 `0x00` 字节或非法 UTF-8 字节的文件读取/编辑，必须拒绝为 `INVALID_UTF8`（对应 `repository-programming-011/026`）。
4. **反例 4（路径 `..` 逃逸、符号链接指向工作区外、并发修改覆盖与中途失败残留）**：
   - 传入 `../outside.txt` 或工作区内指向 `/tmp/outside` 的符号链接，必须拒绝为 `PATH_DENIED`（对应 `repository-programming-011`）；
   - 快照读取后、事务 `preflight` 前外部修改了目标文件，必须拒绝为 `FILE_CHANGED` 且不覆盖外部修改（对应 `repository-programming-016`）；
   - 双文件事务中第 2 个文件写入失败，第 1 个文件必须回滚至原始字节，不得留下半提交状态（对应 `repository-programming-009/016`）。
5. **独立拆除测试**：在独立临时目录上仅加载本卡文件与事务模块（不含 `JsSandbox`、不含 `OfficeCapability`、不含 OpenCode `ToolHost`），运行上述全部 4 组反例与正控测试全绿。

## 9. 精确写集合与暂留/相邻卡边界

- **库仓（`wanxiangli`）写集合**：
  - 新增文件快照、锚点、Glob/Grep 与原子事务子包（或按 `CUT-00` 规划落入对应子目录）及工程导出声明。
- **主仓（`wanxiangshu`）切换期写集合**：
  - `src/Wanxiangshu/Foundation/GlobMatch.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/Failure.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/Anchor.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/Transaction.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/Utf8Fs.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/GlobFs.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/AnchorFs.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/MutationFs.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/FilesystemSurface.fs(.fsi)`
  - `src/Wanxiangshu/Repository/Programming/Js/TransactionSurface.fs(.fsi)`
  - `src/Wanxiangshu/Wanxiangshu.Owner.runtime-platform.foundation-globmatch.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.repository-programming.repository-programming-js-capability.fsproj`（拆离文件原语与 JS 沙箱/权限）
  - `src/Wanxiangshu/Wanxiangshu.Owner.repository-programming.runtime.fsproj`
- **暂留主仓与相邻卡边界**：
  - `src/Wanxiangshu/Process/JsSandbox.fs(.fsi)`、`src/Wanxiangshu/Repository/Programming/Js/{Capability,Description,Surface,TransactionStore,IntegrationRules,ToolsBindings,GeneratorSurface,RuntimeSurface,WorkflowSurface}.fs(.fsi)` 及 `OpenCode/{ToolWorkflow,ToolHost,ToolHostSurface}.fs(.fsi)` 留待 `CUT-25`（受限 JS 编程面与执行组合）处理。

## 10. 施工记录（2026-10-15，eng-cut24，源码层面完成，未运行验证）

- **库仓 `wanxiangli` 新增 `src/fs/` 子包**（入口 `wanxiangli/fs`，导出工厂 `Filesystem({ root })`）：
  - `Native.fs`（node:fs/node:path 原语与 `{ ok, value, code, reason }` 结果形状）、`GlobMatch.fs`（wildmatch 内核）、`Anchor.fs`（有序锚点/行列纯逻辑）、`Transaction.fs`（纯事务决策与 JS 形状转换）、`Utf8Fs.fs`（严格 UTF-8 + NUL 拒绝）、`GlobFs.fs`（gitignore 感知枚举）、`AnchorFs.fs`（grep，含被扫描文件完整快照）、`MutationFs.fs`（路径解析、`resolveBounded` realpath 边界拦截、原子写/回滚）、`Filesystem.fs`（公开消息接口聚合）；
  - `Wanxiangli.Fs.fsproj`、`index.d.ts`、`AGENTS.md`、`tests/{001..005}.test.mjs`（对应 §8 四组反例 + GlobMatch 共用内核/结果形状）；
  - `package.json`：`exports` 增加 `./fs`，`files` 增加 `src/fs/index.d.ts`，`test` 脚本纳入 `src/fs/tests/*.test.mjs`。
- **主仓 `wanxiangshu` 切换**（六处算法删除，改为薄桥接，签名不变）：
  - `Foundation/GlobMatch.fs`：wildmatch 算法删除，桥接 `Filesystem` 的 `compilePattern/matchesPathPattern/testCompiled`；
  - `Repository/Programming/Js/Utf8Fs.fs`：桥接 `readUtf8/readUtf8Classified`，经 `JsFailure.ofWire` 还原 typed 失败；
  - `GlobFs.fs`、`AnchorFs.fs`：枚举/grep/锚点算法删除，桥接 `glob/grep/findAnchor/requireUnique`；`JsGrepListing.ReadSnapshots` 保持「被扫描文件完整快照」语义；
  - `MutationFs.fs`：写盘/回滚算法删除，桥接 `resolveToolPath/existsPath/applyCommit/applyRollback/undoIfMatches`（commit/rollback 计划映射到库的 staged 形状）；
  - `Transaction.fs`：纯决策（`validateSingleIntent/validateTargets/validateFreshness/preflight/commitPlan/rollbackPlan`）改为桥接（函数参数在边界先物化为 existing 数组/current 映射再传库）；durable facts（`JsDurableMutation/Prepared/Committed/Projection/Facts`）保留主仓；
  - `Failure.fs`、`Anchor.fs` 保留：`JsFailure` DU 与 `AnchorSpec` 是冻结的共享声明（D-05），库侧用同码表的 JS 数据形状，两侧经 `ofWire`/`specToJs` 转换。
- **fsproj 拆分**：新建 `Wanxiangshu.Owner.repository-programming.repository-programming-js-filesystem.fsproj`（Failure/Anchor/Transaction/Utf8Fs/GlobFs/AnchorFs/MutationFs）；原 js-capability shard 保留 Capability/Description/Surface/JsSandbox/TransactionStore 并 ProjectReference 新 shard；`compile-order.txt` 未改（无源文件增删）。
- **未运行验证（DevOps 待办）**：
  - 库仓：`npm --prefix /home/kunweiz/Desktop/vibe/wanxiangshu-lifecycle-cutover/wanxiangli run build -- fs` 与 `node --test src/fs/tests/*.test.mjs`（需先构建 fs 产物）；
  - 主仓：`node scripts/build.mjs`（或 `npm run format-build-test`）+ `node --test requirements/repository-programming/tests/`——**前置条件**：主仓 `node_modules/wanxiangli` 需含 `./fs` 入口的新版本 tarball（当前锁定 0.5.2 不含）；tarball/版本/lock 更新归 `CUT-33` 串行处理，接线作者不另造根锁文件；
  - 结构门禁：`node scripts/checks/subsystems.mjs`（新 shard 声明与 DAG 校验）。
- **遗留边界**：
  - 主仓 `FilesystemSurface/TransactionSurface/ToolsBindings/ToolWorkflow/Casebook{Capture,Replay}` 调用方未改动（桥接模块签名不变，属 CUT-25/27 范围的组合层）；
  - 库侧 `resolveBounded`（PATH_DENIED 拦截）为新增能力，主仓既有 `resolveToolPath` 纯解析语义保持不变，权限判定与文件能力组合归 CUT-25；
  - `readUtf8Classified` 现在把 NUL 字节归入 `INVALID_UTF8`（卡 B1 契约），比主仓原实现更严；主仓 011 测试若依赖旧行为需在验证时核对。
