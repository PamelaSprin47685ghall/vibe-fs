# JS Tools — 可观察语义

Clause 前缀 `JS-`。本页只冻结 observable semantics，不规定内部模块名。

## JS-001 Capability-projected tool surface

对每次 provider request，从唯一权威 `AttemptExecutionProfile.ToolCapabilitySet` 生成与当前 Agent、当前 RequestKind 实际能力**完全同构**的 `js-ROLE` 主工具。无第二份 role→JS permission matrix。stale/forged call fail closed。

## JS-002 Primary js-ROLE generation

生成结果同时包含：工具名称、工具 schema、工具描述、`JsProgram` 基类、允许出现的成员函数、canonical examples、runtime capability bindings、BuiltinToolDescriptionHook 注入文案。确定性生成：同一 profile 必须得到同一 surface（fast/deep 相同）。

## JS-003 Builtin coexistence + description hook

内置文件系统工具（`read` / `edit` / `write` / `glob` / `grep` / `patch`，凡存在者）保留原 schema、原实现、原可执行性。不把它们变成 `js-*` alias，不改顶层 RPC schema。当 `js-ROLE` 可见时，BuiltinToolDescriptionHook 在内置工具 description 中标记 `DEPRECATED` + `Prefer js-ROLE` + 鼓吹复杂 JS program / 并行调用。钩子不形成 security scope，不改变 builtin 可执行性；钩子文案提到的 `js-ROLE` 必须同时 provider-visible。

## JS-004 Generated base-class exactness

`JsProgram` 基类只包含当前真正可执行的成员。能力缺失的方法不出现于基类、description、canonical examples 任何一层。即使模型伪造底层调用，runtime gate 仍 fail closed。

## JS-005 file() / FileView

`file(path)` 返回不可变 FileView。FileView 持有读取时的内容快照，后续任何并发修改不影响已取得的视图。strict UTF-8：非法 UTF-8 拒绝为 `FILE_NOT_UTF8`，不以替换字符静默清洗。

## JS-006 Ordered string/RegExp anchors

`find()` / `replace()` 按**声明顺序**匹配：同一文本中多个匹配依序消歧（第 N 个匹配）。支持精确字符串锚点与 RegExp 锚点；`^` / `$` 按文本绝对位置（文件首/尾），非行首/行尾。零宽 RegExp 位置有效。锚点声明 5 类拒绝：不唯一且未指定序、不匹配、正则非法、跨文件混用、空锚点（见 how）。

## JS-007 glob()

有界确定性路径枚举：返回匹配路径的稳定排序；结果受当前 capability 边界约束（不可见的路径不出现）。不跟随符号链接逃逸 capability 根。

## JS-008 rewrite()

改写已存在文件。与 `write()`（创建缺失文件）语义分离：目标不存在时 `rewrite()` 失败为 `FILE_MISSING`，目标已存在时 `write()` 失败为 `FILE_EXISTS`。

## JS-009 write()

创建缺失文件。同一次 program 内同一路径只允许一个 mutation 意图（same path only once；冲突 fail closed）。

## JS-010 JSON-compatible return

`run()` 的返回值必须是 JSON-compatible 结构化结果；query 可以零 mutation。成功结果形状稳定（见 proof 的 golden）。

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

工具结果经 Synthetic TOML 渲染（`ARCH-010` 唯一渲染 owner），受 ToolResultBound 约束。

## JS-017 Builtin tools remain

`read` / `edit` / `write` / `glob` / `grep` / `patch` 继续作为独立内置工具存在，原 schema / 原实现；`js-ROLE` 与它们共存。provider 同名 spec 无重复（builtin 与 `js-*` 名字本就不同）。

## JS-018 Parallel calls

同一消息内并行工具调用由 Host 按确定性顺序串行执行；同文件并行编辑无 lost update；异文件并行编辑各自 all-or-nothing；并行读取不影响编辑。模型侧合同：并行调用绝对安全，强烈鼓励复杂 program。

## JS-019 Failure algebra

失败以稳定失败码表达（如 `FILE_MISSING` / `FILE_EXISTS` / `FILE_CHANGED` / `FILE_NOT_UTF8` / `ANCHOR_*` / `CAPABILITY_DENIED` / `SANDBOX_*`），LLM-visible errors 可读且稳定；不把程序可预见失败伪装成异常。
