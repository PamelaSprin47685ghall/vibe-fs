# JS Tools — 可观察语义

Clause 前缀 `JS-`。本页只冻结 observable semantics，不规定内部模块名。

## JS-001 Capability-projected tool surface

对每次 provider request，从唯一权威 `AttemptExecutionProfile.ToolCapabilitySet` 生成与当前 Agent、当前 RequestKind 实际能力**完全同构**的 `js-ROLE` 主工具。无第二份 role→JS permission matrix。stale/forged call fail closed。

## JS-002 Primary js-ROLE generation

生成结果同时包含：工具名称、工具 schema、工具描述、`JsProgram` 基类、允许出现的成员函数、canonical examples、runtime capability bindings、BuiltinToolDescriptionHook 注入文案。确定性生成：同一 profile 必须得到同一 surface（fast/deep 相同）。

工具 schema 恰好一个必填字段 `program: string`：其值为恰好一个 `class Js extends JsProgram` 且实现 `async run()` 的源码。provider-visible 工具描述必须内嵌当前 Attempt 的**公开** `JsProgram` 基类全文（header + 公开基类 + 仅存在的方法规则 + 仅存在的 examples + footer）。公开基类不含 Host 内部 `_api` / binding key；runtime 代理类只用于沙箱执行，不得充当模型文档。

## JS-003 Builtin coexistence + description hook

内置文件系统工具（`read` / `edit` / `write` / `glob` / `grep` / `patch`，凡存在者）保留原 schema、原实现、原可执行性。不把它们变成 `js-*` alias，不改顶层 RPC schema。当 `js-ROLE` 可见时，BuiltinToolDescriptionHook 在内置工具 description 中标记 `DEPRECATED` + `Prefer js-ROLE` + 鼓吹复杂 JS program / 并行调用。钩子不形成 security scope，不改变 builtin 可执行性；钩子文案提到的 `js-ROLE` 必须同时 provider-visible。

## JS-004 Generated base-class exactness

`JsProgram` 基类只包含当前真正可执行的成员。能力缺失的方法不出现于公开基类、description、canonical examples 任何一层。即使模型伪造底层调用，runtime gate 仍 fail closed。模型只对描述里的公开基类编程：看得到的方法可调用，看不到的方法不存在。

## JS-005 file() / FileView

`file(path, matches = [])` 读取本事务 immutable UTF-8 快照，按声明顺序解析 begin/end anchors，返回不可变 FileView。`text(from, to)`（默认 `^`/`$`）切出原文 substring。后续 rewrite 不影响已取得的视图。strict UTF-8：非法 UTF-8 拒绝，不以替换字符静默清洗。

## JS-006 Ordered string/RegExp anchors

`matches` 为 `Array<[begin, end, pattern]>`，按**声明顺序**从 cursor 向前匹配；匹配后 `cursor = match.end`。pattern 为非空字符串或 RegExp（忽略调用方 g/y/`lastIndex`）。`^` / `$` 为文件绝对首/尾，禁止自定义同名。零宽 RegExp 合法。拒绝：空名字、保留名、重复名、begin==end、空字符串 pattern、按序找不到。

## JS-007 glob()

gitignore / wildmatch 风格的有界确定性路径枚举。`pattern` 相对 capability 根：

- `*` 不跨 `/`；`**` 匹配零段或多段目录；`?` 匹配一个非 `/` 字符；`[abc]` / `[a-z]` 为字符类；`{a,b}` 先展开为交替再匹配。
- pattern 不含 `/` 时匹配任意深度（`*.md` 命中全树 `.md`）；含 `/` 或前导 `/` 时相对根锚定。
- 永不进入 `.git`；应用根与子目录 `.gitignore` 以及 `.git/info/exclude`（若存在）；不跟随符号链接。
- 有界打在**匹配条数**，不打在 DFS 枚举前缀。返回 `{ paths, truncated }`：`paths` 为稳定排序的 canonical 相对路径；截断时 `truncated = true`，不得把截断伪装成空匹配。
- 不授予 Read。capability 边界外的路径不出现。

## JS-020 grep()

`ToolPermission.Grep` 投影为 `grep(needle, pattern = "**/*")`。needle 为非空字符串（字面量）或 RegExp（忽略调用方 g/y/`lastIndex`）。`pattern` 与 JS-007 同一套 gitignore 选文件。Host 在选中的严格 UTF-8 文件上搜索；不可读或非法 UTF-8 的文件跳过，不使整次调用失败。返回 `{ matches: [{ path, line, column, text }], truncated }`：`line` / `column` 为 1-based；`text` 为匹配子串；有界打在匹配条数；截断可见。不授予 `file()`。builtin `grep` RPC 仍独立存在（JS-003/017）。Read+Glob 而无 Grep 时，仍可用 `glob()+file()+RegExp` 组合，但那不是 Grep capability 的投影。

## JS-008 rewrite()

改写已存在文件。与 `write()`（创建缺失文件）语义分离：目标不存在时 `rewrite()` 失败为 `FILE_NOT_FOUND`，目标已存在时 `write()` 失败为 `FILE_ALREADY_EXISTS`。

## JS-009 write()

创建缺失文件。同一次 program 内同一路径只允许一个 mutation 意图（same path only once；冲突 fail closed）。

## JS-010 JSON-compatible return

`run()` 的返回值必须是 JSON-compatible 结构化结果；query 可以零 mutation。允许：`null`、boolean、finite number、string、array、plain object。下列在 **commit 前** 失败为 `INVALID_RETURN_VALUE`：`undefined` / BigInt / NaN / Infinity / function / symbol / cyclic；数组（任意深度）含 `null`；同一数组混有对象与非对象。对象字段 `null` 合法，渲染时省略该键。顶层 `null` 合法。成功 LLM-visible 形状见 JS-016。

## JS-011 Sandbox capability boundary

模型 JavaScript 在无 ambient Host authority 的沙箱中执行：fs / network / process / env 不可直接获得；只获得显式注入的数据与 runtime bindings。deadline 可 kill，memory / output bounded。`new Function` 只是 invocation mechanism，不是权限授予。

## JS-012 Transaction staging

所有 mutation 先进入 ephemeral staging，不触碰真实文件系统。durable prepare facts 只经统一 EventStore（`JobRequested` 语义或等价权威事件），禁止 `js-transaction.db` / feature-owned store / manifest 目录权威。

## JS-013 Multi-file all-or-nothing commit

一个 program 的所有 mutation 在单个事务内提交：任一文件失败 → 全部零提交（含已成功的文件）。commit 成功后才暴露 success result。

## JS-014 Conflict detection

preflight 基于事务读取快照：任一目标文件在快照之后被外部修改 → `FILE_CHANGED`，整个事务 fail closed。失败不隐式 retry。

## JS-015 Rollback / recovery

normal 失败路径回滚全部 staged 效果。crash 恢复只从 EventStore facts/payloads 重建：未 commit 的 prepare 不产生磁盘效果；已 commit 的 facts 重放后与磁盘一致。

## JS-016 Synthetic TOML result

工具结果经 Synthetic TOML 渲染（`ARCH-010` 唯一渲染 owner），受 ToolResultBound 约束。沙箱 `JSON.stringify` 只是 VM 出口，不得再包进 TOML 字符串字段。

两份文档，无 `status` discriminator，无 `result` / `written` / `created`：

- **失败**：instruction 恰好 `# failed`；根级 `code` / `reason`（JS-019）。无 `[data]`，无 `[fs]`。
- **成功**：instruction 恰好 `# ok`；程序值进入 `data`（对象 → `[data]`；原始值/原始值数组 → `data = …`；对象数组 → `[[data]]`）；有磁盘效果时文末 `[fs]`，`rewritten` / `created` 为非空路径数组。空提交不出现 `[fs]`；`[fs]` 不含空数组或空字符串。顶层 `null` 无 data 体。

程序键活在 `[data]` 内，与 Host `[fs]`、失败根级 `code` 不合体。值编码（省略对象 `null` 字段、整数/浮点、quoted key、裸字段先于表）由 `SyntheticToml` 决定，见 `how/synthetic-toml.md`。

## JS-017 Builtin tools remain

`read` / `edit` / `write` / `glob` / `grep` / `patch` 继续作为独立内置工具存在，原 schema / 原实现；`js-ROLE` 与它们共存。provider 同名 spec 无重复（builtin 与 `js-*` 名字本就不同）。

## JS-018 Parallel calls

同一消息内并行工具调用由 Host 按确定性顺序串行执行；同文件并行编辑无 lost update；异文件并行编辑各自 all-or-nothing；并行读取不影响编辑。模型侧合同：并行调用绝对安全，强烈鼓励复杂 program。

## JS-019 Failure algebra

失败以稳定失败码表达（proposal §77.1：`INVALID_PROGRAM` / `PROGRAM_FAILED` / `PROGRAM_TIMEOUT` / `PERMISSION_DENIED` / `PATH_DENIED` / `FILE_NOT_FOUND` / `FILE_ALREADY_EXISTS` / `INVALID_UTF8` / `ANCHOR_NOT_FOUND` / `ANCHOR_NOT_UNIQUE` / `DUPLICATE_MUTATION_TARGET` / `RESULT_TOO_LARGE` / `INVALID_RETURN_VALUE` / `FILE_CHANGED` / `TRANSACTION_*` / `UNKNOWN_MEMBER`），LLM-visible errors 可读且稳定；不把程序可预见失败伪装成异常，不从 exception message 反推业务错误种类。LLM-visible 失败文档形状见 JS-016；失败码集合不因渲染改动而增减。
