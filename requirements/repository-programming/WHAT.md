# WHAT — repository-programming 的唯一 normative 合同

> 命题 = 当前世界必须同时成立的事实。每条命题有测试落点（见 `HOW.md`）。
> 边界（DOES NOT OWN）写在各条「边界」；更完整的弃权记录在 `HOW.md` §历史与弃权。

## REPOSITORY-PROGRAMMING-001 — capability-projected surface，无第二权限矩阵

**规范陈述**：对每次 provider request，`js-ROLE` 主工具必须从唯一权威 `AttemptExecutionProfile.ToolCapabilitySet` 机械投影生成；不得存在第二份 role→JS permission matrix（`JsToolGenerator` 不得接收 role 后自行重算权限）。文件系统 primitive capability 集为空时不得生成任何 `js-*` 工具。

**含义/动机**：surface 与权限脱钩的唯一方式就是让 surface 是权限的纯投影。第二份矩阵必然与权威漂移；「文档说你能但实际不能」的认知负担会让模型误调。

**边界**：不裁决「capability 从哪来」（→ `office-capability`）；不拥有同构律本身（→ `capability-enforcement`，本包应用它）。`js-` 前缀名与 `Read/Write/Edit/Glob/Grep` 枚举名是当前 HOW，非永久合同。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-001 行。

## REPOSITORY-PROGRAMMING-002 — 四层同构应用到编程面

**规范陈述**：对当前 Attempt 的每个 JS filesystem capability，以下各层必须完全一致：① 生成的 `JsProgram` 基类中出现对应方法；② `js-*` 工具 description 中出现该方法；③ canonical examples 中出现该方法；④ 即使模型伪造底层调用，runtime gate 仍 fail closed。能力缺失时四层同时缺失。

**含义/动机**：模型对 `js-*` 的认知负担为零——不需要记「文档里有但你不能用」。四层中任何一层漂移都制造「看起来能调」的假 surface。**同构律本身归 `capability-enforcement`**；本命题只要求编程面应用该律。

**边界**：内置工具（`read`/`edit`/`write`/`glob`/`grep`/`patch`）不是 JS capability projection 的第五层，也不是 alias（其可见性由既有 ToolPermission 决定 → `capability-enforcement`/`office-capability` 交叉）。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-002 行。

## REPOSITORY-PROGRAMMING-003 — 确定性生成

**规范陈述**：同一 Attempt profile（同一 capability set + 同一 role）必须生成字节相同的 surface（工具名、schema、description、base class、examples、runtime bindings）。Tier（fast/deep）永远不进入工具名：`fast-coder` 与 `deep-coder` 都用 `js-coder` 且得到 byte-equivalent surface。

**含义/动机**：非确定性生成 = 每次调用模型看到不同合同；tier 进工具名会把执行绑定泄漏进工具面。生成是纯函数：同输入同输出。

**边界**：`js-<roleName>` 的命名规则是 HOW（`JsToolGenerator.toolNameFor`），可随语言替换。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-003 行。

## REPOSITORY-PROGRAMMING-004 — generated-name gate

**规范陈述**：工具执行入口（ToolRegistry/绑定层）只接受当前 Attempt 的生成 surface 所拥有的 `js-*` 主工具名。伪造名字（如 Reviewer 调 `js-coder`）、旧 Attempt 名字必须 fail closed；名字合法不能成为执行理由。

**含义/动机**：生成层负责 UX 与声明，执行层负责 enforcement。名字是 surface 身份；没有 gate，模型只要猜中一个合法名字就能绕过 capability 投影。

**边界**：内置工具名（`read` 等）的可见性不归本包裁决（→ `capability-enforcement`/`office-capability`）。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-004 行。

## REPOSITORY-PROGRAMMING-005 — 编程面诚实：无不存在的方法、无不可见推荐

**规范陈述**：`JsProgram` 基类只包含当前真正可执行的成员；能力缺失的方法不得出现在公开基类、description、canonical examples 任何一层（`If a method is present, the capability exists. If a method is absent, it does not.`）。任何对 builtin 工具的推荐文案（description hook）不得推荐 provider 不可见的工具——推荐不可见工具 = 说谎钩子，fail closed。公开基类不得包含 Host 内部 `_api` / binding key；runtime 代理类只用于沙箱执行，不得充当模型文档。

**含义/动机**：没有方法就是最清楚的说明，比「You cannot write」更诚实；hook 推荐一个 provider 看不到的工具等于教模型调一个必定失败的名字。

**边界**：builtin 工具是否长期 coexist 由产品决定（→ HOW/历史与弃权），本包不拥有该决定；本命题只约束编程面与推荐文案的诚实性。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-005 行。

## REPOSITORY-PROGRAMMING-006 — sandbox 无 ambient OS authority

**规范陈述**：模型 JavaScript 在无 ambient Host authority 的沙箱中执行：fs / network / process / env 不可直接获得；runner 只得到显式注入的数据（路径字符串、FileView 内容、glob 结果），不获得文件句柄。每个 program 有明确 deadline，超时 kill + reap；memory / output / program source 有界。`new Function`（或等价 invocation mechanism）只是程序调用机制，**不是权限授予**，不单独构成安全证明。stdout/stderr 不是编辑结果：结果只来自 `run()` 的 framed return。

**含义/动机**：任意 JavaScript 直接拿到 fs/network/process/env 等于把 Host 权限交给 prompt 注入。安全边界永远是外层隔离（独立进程、可杀、有界），不是 in-process JS context。

**边界**：`new Function` 的具体实现、具体 bound 数值（deadline ms、memory bytes）是 HOW 常数；「有界、可杀、无 ambient authority」才是合同。进程/PTY 的真实 execution 语义 → `process-execution`。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-006 行。

## REPOSITORY-PROGRAMMING-007 — file()/FileView：immutable 快照与 anchor 代数

**规范陈述**：`file(path, matches = [])` 读取本事务的 immutable UTF-8 快照（strict UTF-8：非法字节拒绝为 `INVALID_UTF8`，禁止 replacement-character 修复、跳过坏字节、猜 encoding），按声明顺序解析 begin/end anchors，返回不可变 FileView；`text(from, to)` 切原文 substring（默认 `^`/`$` 文件绝对首/尾）。锚点可用于只读切片。`from`/`to` 可为已声明名、`^`、`$`、或临时位移 `name±N`：N 是已解码 JS 字符串上的下标增量（与 `String.length`/`source.slice` 同一单位，UTF-16 code unit），**不是行号，也不是 UTF-8 字节数**；caret clip 到闭区间 `[0, file_len]`。`matches` 为 `Array<[begin, end, pattern]>`，按声明顺序从 cursor 向前匹配，匹配后 `cursor = match.end`；pattern 为非空字符串或 RegExp（忽略调用方 g/y/`lastIndex`）。拒绝：空名、保留名、重复名、begin==end、空 pattern、按序找不到（`ANCHOR_NOT_FOUND`，reason 含 1-based 声明序号、path、cursor、pattern 预览）。同一 program 内后续 rewrite 不影响已取得的视图。

**含义/动机**：锚点是「在一个 window 内钉住可编辑区域」的代数，不是行号猜测。行号位移与字符串位移混用会把 `h1+200` 读成「往下 200 行」。不可变快照保证读到的与改写的是同一事务内的同一世界。

**边界**：正则引擎的具体语义（JS RegExp）是 HOW；「有序、可消歧、位移是字符串下标」才是合同。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-007 行。

## REPOSITORY-PROGRAMMING-008 — glob()：gitignore/wildmatch 确定性枚举

**规范陈述**：`glob(pattern)` 是 gitignore / wildmatch 风格的确定性路径枚举：`*` 不跨 `/`，`**` 匹配零段或多段，`?` 匹配单字符，`[abc]`/`[a-z]` 字符类，`{a,b}` 交替；pattern 不含 `/` 时匹配任意深度，含 `/` 或前导 `/` 时相对 capability 根锚定。永不进入 `.git`；应用根与子目录 `.gitignore` 与 `.git/info/exclude`；不跟随符号链接。枚举无内部上界，返回全量 `{ paths }`；超限由最终 tool result 留尾机制一次性收敛（见 host-boundary 的 Host bound）。capability 边界外的路径不出现。不授予 Read。

**含义/动机**：walk-then-filter 的 DFS 会先进入 `.git`；工具内部无截断即无第二套上界语义，超大结果由 Host 最终留尾统一收敛，模型所见即工具所得。

**边界**：具体 maxEntries 数值、glob 实现（`Infrastructure/JsGlobFs.fs`）是 HOW。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-008 行。

## REPOSITORY-PROGRAMMING-009 — grep()：Grep capability 投影为 Host member

**规范陈述**：`ToolPermission.Grep` 投影为 `grep(needle, pattern = "**/*")` member：needle 为非空字符串（字面量）或 RegExp（忽略调用方 g/y/`lastIndex`）；pattern 用与 `glob()` 同一套 gitignore 选文件。Host 在选中的严格 UTF-8 文件上搜索；不可读或非法 UTF-8 的文件**跳过**，不使整次调用失败。返回全量 `{ matches: [{ path, line, column, text }] }`：`line`/`column` 1-based，`text` 为匹配子串；无内部上界，超限由最终 tool result 留尾机制收敛。不授予 `file()`。Read+Glob 而无 Grep 时仍可 `glob()+file()+RegExp` 组合，但那不是 Grep capability 的投影。

**含义/动机**：grep「可表达」≠「不需要 primitive」——glob 假阴性让组合零命中，沙箱内逐文件 `file()` 被 timeout/`RESULT_TOO_LARGE`/二进制放大。Grep 是独立的 primitive capability 投影。

**边界**：builtin `grep` RPC 独立存在（其 schema/权限 → `capability-enforcement` 交叉）；具体 maxMatches 数值是 HOW。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-009 行。

## REPOSITORY-PROGRAMMING-010 — rewrite()/write() 分离（Edit ≠ Write）

**规范陈述**：`rewrite(path, newText)` 只改写**已存在**文件（目标缺失 → `FILE_NOT_FOUND`）；`write(path, text)` 只创建**缺失**文件（目标已存在 → `FILE_ALREADY_EXISTS`）。同一次 program 内同一路径只允许一个 mutation 意图（same path only once；冲突 → `DUPLICATE_MUTATION_TARGET` fail closed）。

**含义/动机**：Edit 与 Write 是可分别授予的 capability；若 `write()` 能覆盖旧文件，「没有 Edit 的 Agent 也能改文件」就成了权限旁路。同路径双意图无法确定语义，fail closed。

**边界**：`mv`/`rm` 的 POSIX 语义在 REPOSITORY-PROGRAMMING-020；「Edit/Write 是两个 capability」才是本命题。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-010 行。

## REPOSITORY-PROGRAMMING-011 — JSON-compatible return，commit 前校验

**规范陈述**：`run()` 的返回值必须是 JSON-compatible 结构化结果。允许：`null`、boolean、finite number、string、array、plain object。下列在 **commit 前** 失败为 `INVALID_RETURN_VALUE`：`undefined`/BigInt/NaN/Infinity/function/symbol/cyclic；数组（任意深度）含 `null`；同一数组混有对象与非对象。对象字段 `null` 合法（渲染时省略该键）；顶层 `null` 合法。

**含义/动机**：结果面（Synthetic TOML）不能诚实表示数组 `null` 与异构数组；校验必须发生在 durable prepare / 磁盘 commit 之前，非法返回值不得留下半提交。

**边界**：值编码规则（TOML 如何表达每种值）→ `provider-projection`；本命题只要求「commit 前校验 + 合法集合」。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-011 行。

## REPOSITORY-PROGRAMMING-012 — transaction staging；durable prepare 只经统一 EventStore

**规范陈述**：一次 `js-*` tool call 对应恰好一个 `JsTransaction`。所有 mutation 先进入 **ephemeral staging**，真实文件系统在 `run()` 成功结束前不得被修改（`file()`/`glob()` 只产生 observations，`rewrite()`/`write()` 只产生 staged mutations）。durable prepare facts 只经统一 EventStore（`JsTransactionPrepared` 事件 + owned payloads）；禁止 `js-transaction.db`、`transaction-v2.json`、special feature ref、manifest 目录权威、任何 feature-owned durable store。

**含义/动机**：先写盘再执行会让崩溃后的磁盘与 EventStore 事实分歧；durable prepare 是 crash-recovery 的唯一 substrate。feature store 无法共享 Persist 的 merge/CAS/恢复。

**边界**：EventStore 本身 → `durable-events`；Requested/Prepared/Committed 效果分型 → `effect-accounting`；本命题是「staging 先行 + 单一 durable substrate」的编程面合同。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-012 行。

## REPOSITORY-PROGRAMMING-013 — multi-file all-or-nothing commit

**规范陈述**：一个 program 的所有 mutation 在**单个事务**内提交：任一文件失败 → 全部零提交（含已成功的文件）。commit 成功后才暴露 success result。正常路径：preflight 全过 → durable prepare → apply mutations（canonical path 顺序）→ append `JsTransactionCommitted` → 只在这时才暴露成功工具结果。write/rewrite 每一步有 compare-before-effect 保护。

**含义/动机**：multi-file 编辑的半途落盘留下不一致世界。all-or-nothing 不是「所有路径在同一 CPU instant 变化」（普通文件系统没有跨路径瞬时原子点），而是「正常成功 → 全部新状态；正常失败 → 全部旧状态；崩溃 → recovery 收敛到可证明完整终态」。

**边界**：外部不服从 transaction ownership 的进程在底层多次文件替换之间观察到短暂 mixed view，不属于本合同隐藏的事实（历史 change（js-capability-projected-tools）§63）。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-013 行。

## REPOSITORY-PROGRAMMING-014 — conflict detection：FILE_CHANGED fail closed

**规范陈述**：preflight 基于事务读取快照：任一目标文件（及事务读取过的具体文件）在快照之后被外部修改 → `FILE_CHANGED`，整个事务 fail closed。**失败不隐式 retry**：不得自动重新读取、不得自动重新 resolve anchors、不得自动重新执行 model program（那会把一次 tool call 变成隐式 retry，改变模型原本定位的对象）。第一版 glob 结果视为一次路径观察。

**含义/动机**：静默用旧快照覆盖外部修改 = lost update。失败就是失败——返回 `FILE_CHANGED` 让调用方决定下一步。

**边界**：完整数据库级 phantom serializability 不承诺；「所有实际读取文件和 mutation preimages 提交前重新验证」才是合同。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-014 行。

## REPOSITORY-PROGRAMMING-015 — normal rollback；crash 后不自动恢复工具

**规范陈述**：同一进程内可观察到的 normal 失败路径回滚本次已写效果：Rewrite 恢复 `Replacement → Original`，Create 恢复 `Replacement → Missing`；rollback 必须 CAS-style，当前文件已不是本事务 replacement 时不得覆盖第三方内容。进程 crash 是另一条边界：若 crash 发生在 durable `Prepared` 之后、`Committed` 之前，下一进程**不得自动 undo、redo、rollback、重跑 model program 或补 commit**。`Prepared` 与磁盘现状只作为“上一 js-* tool 已中断”的审计证据；该 tool 保持失败/不完整。禁止 plugin startup、EventStore open 或下一次普通 js-* 调用偷偷替它善后。

**含义/动机**：跨进程自动 rollback 看似修复，实际是在没有用户/LLM 新意图时修改 workspace，并把坏 tool 伪装成从未发生。KISS 边界是：进程内事务负责正常失败原子性；进程死亡打断工具，断点必须可见。

**边界**：未来若 session `/continue` 需要处理中断 transaction，只能把 `Prepared` + 当前文件事实公开给 LLM/显式恢复 workflow，由新意图决定修复；仍禁止 feature store、隐式 replay 与 constructor recovery。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-015 行。

## REPOSITORY-PROGRAMMING-016 — Synthetic TOML 结果面：两份文档，无 status discriminator

**规范陈述**：工具结果经 Synthetic TOML 渲染（ARCH-010 唯一渲染 owner），受 ToolResultBound 约束。沙箱 `JSON.stringify` 只是 VM 出口，不得再包进 TOML 字符串字段。两份文档，无 `status` discriminator，无 `result`/`written`/`created` 兼容别名：

- **失败**：instruction 恰好 `# failed`；根级 `code`/`reason`（REPOSITORY-PROGRAMMING-018 稳定码）；无 `[data]`、无 `[fs]`。程序 throw、`INVALID_RETURN_VALUE`、`FILE_CHANGED`、事务失败全走这一份（commit 未发生）。
- **成功**：instruction 恰好 `# ok`；程序值进入 `data`（对象 → `[data]`；原始值/原始值数组 → `data = …`；对象数组 → `[[data]]`）；有磁盘效果时文末 `[fs]`，`rewritten`/`created` 为非空路径数组。空提交不出现 `[fs]`。顶层 `null` 无 data 体。

程序键活在 `[data]` 内，与 Host `[fs]`、失败根级 `code` 不合体（TOML 同名表合并因此不可能发生）。

**含义/动机**：`status = "ok"` 会与程序自己的 `status` 键竞争；`result = "{...}"` 是两套语法叠信封；逗号拼接路径在路径含逗号时不可消歧。成败不靠 `# ok`/`# failed` 注释行成立——截断时先丢 instruction，失败靠根级 `code`/`reason`，成功靠有 `data` 和/或 `[fs]`。

**边界**：值编码细节（引号、裸字段先于表、integer/float 规则）→ `provider-projection`（`SyntheticToml`）；本命题只锁定文档形状与无 discriminator。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-016 行。

## REPOSITORY-PROGRAMMING-017 — 并行调用绝对安全

**规范陈述**：模型可以在一次 assistant 消息中并行发出任意多个 `js-*` 与/或内置工具调用（同文件、异文件均可）。Host 对同一消息内的工具调用按**确定性顺序逐个执行**：每个调用是独立 transaction，后一个基于前一个 commit 后的 committed state 重新 snapshot。因此同文件并行编辑 = 顺序叠加（无 lost update）；异文件并行编辑 = 各自独立 all-or-nothing；并行读取 = 各自独立只读。模型不需要自己节流或串行化；模型侧合同：并行调用绝对安全，强烈鼓励复杂 program。派生类没有 `commit()`/`rollback()`/`transaction()`/`snapshot()`——事务生命周期完全由 Host 持有，编辑意图只通过 `rewrite()`/`write()` 表达。

**含义/动机**：「并行」是模型侧的请求形态；执行侧是确定性的串行提交。这让模型用一次复杂 program 完成批量工作而不担心文件冲突。

**边界**：Host 的具体串行执行实现位于 plugin 边界（`tests/integration/plugin/`，REUSE）；本包证明编程面合同（模型被教导可并行 + 事务 re-snapshot 语义）。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-017 行。

## REPOSITORY-PROGRAMMING-018 — failure algebra：稳定失败码，不伪装异常

**规范陈述**：失败以稳定失败码表达：`INVALID_PROGRAM` / `PROGRAM_FAILED` / `PROGRAM_TIMEOUT` / `PROGRAM_RESOURCE_LIMIT` / `PERMISSION_DENIED` / `PATH_DENIED` / `FILE_NOT_FOUND` / `FILE_ALREADY_EXISTS` / `FILE_READ_FAILED` / `INVALID_UTF8` / `ANCHOR_*` / `DUPLICATE_MUTATION_TARGET` / `RESULT_TOO_LARGE` / `INVALID_RETURN_VALUE` / `FILE_CHANGED` / `TRANSACTION_*` / `UNKNOWN_MEMBER`。程序可预见失败（找不到文件、找不到锚点）**不得**压成 `PROGRAM_FAILED`；LLM-visible errors 小而稳定（`code` + 可读 `reason`），不回显完整 source/program/secret path/sandbox internals。生成面把 Host `{ ok: false, code, reason }` 与锚点声明失败经结构化 sentinel（`__jsFailure`）交给沙箱；未带 sentinel 的普通 throw 才是 `PROGRAM_FAILED`（reason 含 message）。不从不带 sentinel 的 exception message 反推业务错误种类。失败码集合不因渲染改动而增减。

**含义/动机**：可预见失败是业务分支，不是程序事故。`PROGRAM_FAILED` 一把抓会让模型无法区分「目标不存在」与「程序写错了」；从 exception 文本嗅探错误种类是脆弱解析。

**边界**：异常只留给程序无法继续的事故（进程崩溃、Host 故障）。失败文档形状 → REPOSITORY-PROGRAMMING-016。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-018 行。

## REPOSITORY-PROGRAMMING-019 — program return 在 commit 前诚实编码；return 与 commit 耦合

**规范陈述**：有 mutation 时：`run()` 返回 value → value 校验通过（REPOSITORY-PROGRAMMING-011）→ transaction commit → 暴露 value。commit 失败时不得把 `run()` 的业务 return 当作成功结果交给 LLM。纯查询（WriteSet 空）→ 无 commit 必要 → 暴露已校验的 return；同一个 JS primitive 自然同时支持 query 和 mutation。staged replacement 与 preimage 字节相同 → 该文件不执行无意义 write/rename，整体仍算成功（`status = ok, changed = false`）。

**含义/动机**：结果验证必须在 commit 前，否则「先写盘再发现结果不可用」留下已提交的错误世界。成功 return 与 commit 耦合 = 模型看到的成功结果永远对应一个已提交的世界。

**边界**：结果渲染（TOML 形状）→ REPOSITORY-PROGRAMMING-016。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-019 行。

## REPOSITORY-PROGRAMMING-020 — mv/rm 文件变换的 POSIX 语义

**规范陈述**：文件变换编程面（当前：builtin `mv`/`rm` 工具）必须保持 POSIX 语义：`mv` 移动/重命名文件与目录（含目录内容、覆盖语义、跨文件系统按 POSIX 行为，source/destination 必填，缺失 source 返回错误）；`rm` 删除文件与**空**目录，**拒绝删除非空目录**，缺失 path 返回错误。工具 spec 带名字、description、参数 schema 与本地化文案；错误面返回稳定可读消息（含 OS message 保留）。

**含义/动机**：文件变换是 repository mutation 的一部分；POSIX 语义是模型训练中极强的既有约定。`rm` 拒删非空目录是安全边界：一次误删整个目录树不可逆。

**边界**：这些工具只进 Coder 矩阵等角色门禁 → `office-capability`/`capability-enforcement` 交叉（AGENT-016）；「POSIX 变换语义」才是本命题。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-020 行。

## REPOSITORY-PROGRAMMING-021 — 静态门禁：禁手写 per-role js-* 变体

**规范陈述**：生产源码中禁止手写 per-role `js-*` 工具变体。唯一合法的 `js-*` 工具名是 `JsToolGenerator` 在运行时构造的名字（`js-` + roleName）；任何字面量 per-role `js-*` 名出现在生产源码（除权限矩阵的合法枚举：`src/Wanxiangshu/Tools/StaticTools.fs`）意味着引入了手写变体 → fail closed。

**含义/动机**：手写变体是「生成器不存在的第五份实现」——它不经过 capability 投影，必然与唯一权威漂移。静态门禁让「新增手写 js-* 工具」在编译/门禁期就红。

**边界**：门禁机制本身是共享 checker（`scripts/checks/js-surface-gate.mjs`，MECHANISM）；本命题拥有的是「生成面唯一 + 无手写变体」的语义。同构律本身 → `capability-enforcement`（其 `capability-isomorphism-gate.mjs` 拥有 schema/runtime gate 同源）。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-021 行。

## REPOSITORY-PROGRAMMING-022 — 工具选择也属于正确性：description 必须让失败经验现身说法

**规范陈述**：生成的 `js-*` description 不得只罗列 API 或给轻飘飘的「prefer X / avoid Y」建议；它必须把**正确行为包装成高显著性、低逃逸率的行为引导**。开头先做醒目的风险中断（例如「警告：你正准备酿成大错」），抢在模型沿熟悉低层路径自动补全之前重新分配注意力；随后用短而具体的失败经验解释为什么应该先使用当前 surface 已拥有的高层 primitive。至少覆盖：① `file(matches)` / ordered anchors / `text()` 已拥有结构定位时，不得把手算 `indexOf`/`substring`/大范围 `replace` 当默认重构路径；② grep 是候选发现工具，不承担文件切片/重组语义；③ mutation 应先在不可变 snapshot 上构造目标结果，再对每个 path 一次提交，禁止把「第一轮留下的残骸」当成合理的第二、第三轮工作流；④ program 返回前应检查廉价且明显的不变量（例如规模、关键结构、预期锚点），结果离谱时应 fail 当前 program，让 staged mutation 零提交，而不是看到文件开头「看起来正常」就继续猜；⑤ description 必须给出可执行的 trigger→action 规则（例如「如果你正准备手算边界，就停下并先声明 anchor」）和明确 stop rule，使异常证据能中断自动驾驶；⑥ 允许并鼓励使用**真实权威、数字锚定、损失厌恶、二选一 framing、重复、首因/近因、承诺一致性、反自我辩护提示**等心理手段强化记忆，但这些手段只能强化真实合同，禁止虚构权威、虚构数字、虚构因果或把非穷尽集合伪装成逻辑穷尽；⑦ 当工程 policy 本身定义了穷尽选择时，应故意把选择压成强二分，例如「高层 primitive 已拥有边界 → 用它；否则先明确证明它表达不了，再下降一层」，不保留「我熟悉低层 API 所以顺手重写一遍」这种心理逃生门。该教育必须保留「我/我们已经为这种错误付过代价」的现身说法质感：惊醒 → 事故 → 损失 → 根因 → 下一次动作；单纯禁令、口号或 API 摘要不满足本命题。

**含义/动机**：对 LLM 来说，工具说明就是执行习惯塑形层。一个拥有 snapshot/anchor/transaction 的高级 primitive 如果被描述成「可选便利方法」，模型会沿训练语料中最熟悉的字符串手工活自动补全，并重新制造 Host 已经替它消灭的 offset、边界、残骸和多轮修补问题。仅靠理性摘要往往来不及截断这条自动路径；先用高显著性警告制造 prediction break，再用具体损失形成鲜活记忆，用因果解释避免迷信，用 trigger→action 把记忆绑定到下一次相同情境，用 stop rule 让异常证据真正打断执行。目标不是吓唬，而是让正确工具选择在需要它的瞬间被想起来。

**边界**：具体事故数字（例如约 8k → 31k）、比喻、第一人称措辞、文案长度与双语表达属于 HOW，可替换；永久合同是「description 以具体失败记忆教授高层 primitive 的适用边界，并给出可执行的下一步」，且不得因此推荐当前 capability surface 中不存在的方法（仍受 002/005 约束）。

**证据**：→ `HOW.md` REPOSITORY-PROGRAMMING-022 行。
