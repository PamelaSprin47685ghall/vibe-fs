# 架构 — 可观察行为与不变量

控制结构与并发契约见 `shape/architecture.md`。  
合成 TOML 可执行细则见 `how/synthetic-toml.md`。理由见 `why/architecture.md`。

## ARCH-002：事件是信号，不是数据

碎片事件不得进入业务层。唯一允许的业务信号：

```text
session.status = idle
session.status = retry
session.deleted
```

业务事实只从 SDK 完整 snapshot 读取。

禁止：

- 处理 `message.updated` / `part.delta` / `session.updated` / `session.diff` 作为业务输入  
- 从 idle payload 推断 terminal / 完成 / 失败  
- 依赖事件先后顺序推导因果  

（LOOP 传感器是 ARCH-002 的定点例外，见 LOOP-002。）

## ARCH-003：不修改 OpenCode 本体

仅使用现有 Hook / SDK：

```text
chat.message
experimental.chat.messages.transform
tool.definition / tool.execute.before / tool.execute.after
experimental.session.compacting / experimental.compaction.autocontinue
event
client.session.* / prompt_async / session.messages
```

禁止：要求新 Hook；改 Host 源码；依赖未公开 API。

## ARCH-004：前缀缓存保护

平常回合：Y frames 可增长，X active prefix **字节不变**。  
PrefixEpoch 只在下列**已发生事实**提交时切换（冷边界；单一 `ActivePrefixEpoch` SSOT，COMPANION-009）：

1. X prefix probe 提升（CTX-012）— `PrefixRebaseCommitted`，`EvidenceKind=Probe`  
2. Host compaction 后重锚（HOST-006）— `ContextReanchored`  
3. TodoCheckpoint lag-1 rebase（TODO-009 / CTX-015）— 同一 `PrefixRebaseCommitted` 合同，`EvidenceKind=TodoCheckpoint`

第 3 项是既有冷边界的合法 **evidence**，不是新的 epoch 状态机或平行 SSOT（TODO-009、TODO-012）：

```text
desired cutoff     ← 仅 Accepted Todo 链推导；Accepted 本身不 commit epoch
commit 时机        ← 下一 provider attempt seal / 绑定之前原子 append
todowrite after    ← 不 commit；不强制等 Y materialize
provider 结局      ← Failed/Aborted 不回滚已 seal epoch；成功也不是 commit 条件
Y 材料             ← 仅 PrefixCoverage 可证明的 complete-turn prefix（禁止 RawGap）
```

历史 `StudentLearn → StudentCompile`（AGENT-020 / PROMPT-012）：**G3 已删除（absent）**，
不得再作 PrefixEpoch 例外。后继 SyncDelegate 不引入 owner-prefix 冷切换（EXEC-026/028）。

Y BlogSquash 只推进 `FrameEpoch`（COMPANION-006），不得改 `PrefixEpoch`。

禁止：按 token / 窗口 / 占比主动切换 epoch；把 Y frames 塞进 X active prefix；用 runtimeId/timestamp 做 canonical equality；以 provider 成功当作 epoch commit 条件；先发 provider 再补 `PrefixRebaseCommitted`；seal 后因失败删除/回滚已提交 epoch。

## ARCH-005：恢复哲学

```text
对话事实源 = OpenCode Session transcript
代码事实源 = Git commit / tree / worktree
跨进程领域事实 = per-runtime NDJSON
崩溃后 = Boot Fold 领域事实 → 普通程序决定下一步
```

不是「恢复暂停的协程」。

## ARCH-006：命名

人是名词（Role / Persona / office）；工具是动词。  
实现以类型命名空间消歧；禁止 Translator / Governor / Broker 等无价值中间层。  
禁止以「用户面同名方便」让 Role 与 Tool 共用一名承载不同语义（例如已删的 Executor 角色名与 `executor` 工具名）；不同硬语义必须不同名（`commission` ≠ `fork`）。

## ARCH-007：工具名引用完整性

> **A tool name names one contract everywhere.**

```text
same tool name
⇒ same semantic act
   same argument schema
   same meaning of every argument
   same lifecycle consequence
   same return semantics
   same important failure semantics
```

仅 schema 相同不足。role visibility / 永不同时出现不削弱此不变量。  
`join` 可在 Manager 与 Orchestrator 共享，当且仅当语义合同完全同一（消费当前 owner 可用 completion）。

## ARCH-008：禁止词

下列词不得作程序计数器或伪领域状态（真实资源世代名如 CancellationEpoch 除外）：

```text
Stage, Phase, Lease, Owner, Generation
```

## ARCH-010：合成文本用 TOML Instruction/Data（主命题）

同时满足下列四条的文本 payload 必须用 TOML 表达：

1. 最终由 LLM 按文本阅读  
2. 不是原生 system / developer prompt  
3. 不是未经包装的人类原文  
4. 由运行时 / Host / 插件 / 工具 / projection 构造或重投影  

原则：instruction = 顶层 comment 且在前；data = field/table/value。  
该表示只供 LLM 阅读，永不反向解析为 authority / origin / 控制流（ARCH-011）。  
细则、字符串、containment、迁移见 `how/synthetic-toml.md`。

## ARCH-011：状态先于表示

```text
typed fact / state / intent → renderer → string
string ↛ origin / authority / phase / next action
```

程序状态用 DU、字段、事件、Journal、typed metadata 表达。  
禁止用空非空、零宽、前缀、regex、error prose 等反推「我是谁、下一步做什么」。  
测试断言 typed behavior；仅当外部协议规定字节合同时才钉字节。

## ARCH-012：自定义 Tool 文本结果有界

插件返回给 Host 的自定义 tool 文本结果必须在 Host 默认 head truncation 之前完成确定性留尾截断：

- 不超过 2000 行且 UTF-8 不超过 51200 字节时逐字返回。
- 超限时输出固定 marker + 确定性尾部：优先保留最新完整行；若最后一行自身超限，按 UTF-8 scalar 安全保留其后缀。最终结果同时满足两项上限。
- 计量与截断只认 UTF-8 字节和换行，不按字符数、token 或 provider 容量估算。

该边界只限制 tool 返回 wire，不改变内部完整结果的事实来源。

## ARCH-013：（空缺）Student / Teacher 知识控制模型 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 Student/Teacher runtime、QA store、`teacher` 工具、Student request kind、
Teacher Satellite 与全部兼容恢复路径。不得以 alias、deprecated type、隐藏 storage 或 SyncDelegate fallthrough 复活。
通用“状态先于表示”原则由 ARCH-011 拥有；Session ownership 由 HOST-008 拥有；SyncDelegate 调用协议由 EXEC-026/028 拥有。Host 本体仍遵守 ARCH-003。

## ARCH-014：Provider Horizon

> **The Horizon Has No State Machine. Nor does it have UUIDs.**

每个 provider-visible field 须过 decision filter：

```text
Did the participant already know this?        → omit
Did they just supply this themselves?          → omit
Is it implied by successful completion?        → omit
Is it useful only for correlation/debug?       → keep internal
Would different values change next action?     → if no → omit
Does the participant need the value itself
  rather than merely its consequence?          → if no → render consequence
                                                → if yes → preserve minimal observation
```

显式小法则（不得只当「Horizon 蕴含」而省略）：

```text
State belongs to the machine. Change belongs to experience.
Do not tell a participant what state the world is in when you can tell them what has happened.
An echo is not an observation.          // tool success 已证事实，result 不重述
Do not make the model decode your discriminated unions.
A description must not secretly be an instruction.
Give the participant the measurement, not the Host's judgment of it.
Never show a path to something that no longer exists.
People are nouns. Tools are verbs.
Failure is a fact in the world, not an `error` object handed to a person.
Errors belong to machinery. Consequences belong to experience.
Idempotency should replay experience, not expose deduplication.
The machine may know everything required to keep the world coherent.
A person should be told only what belongs in their horizon.
The machine guards the boundary. The participant chooses what is worth spending within it.
```

禁止把 `status` / `code` / `error` DTO、SessionId / AgentId / RunId、cursor / offset、已删 spool 的 `spool_path` 等机器态塞进 provider surface。

## ARCH-015：Closing report = prose，不是 schema

> **Closing Report is prose, not a schema.**

约束内容的诚实，不约束文章的骨架。  
Closing report 如实陈述什么重要；**无** universal 固定字段义务（如 result / files / tests / risks / blockers）。  
角色可在自然需要时提及这些事实——提及 ≠ 格式义务。  
machine-semantic 结构只留在协议真需处（如 `exit_code`、`verdict`、`root_requirement`）。  
禁止再造 per-role fixed report DTO（`### Summary` / `### Files Changed` / …）。

## ARCH-016：静态架构 Gates A–D

可观察门禁锚点（实现与 proof 须能失败）：

| Gate | 不变量 |
|------|--------|
| A Tool Referential Integrity | same tool name → 唯一 schema owner + 唯一 semantic contract owner（ARCH-007） |
| B Provider Leak | provider 输出不得含 SessionId / AgentId / ManagerJobId / PtyId / FissionGroupId / lane_index / worktree / fallback offset / `fast-`·`deep-` binding / spool path |
| C Language Parity | 每个 provider semantic resource：EN 与 zh-CN 皆存在（HOST-026） |
| D Prompt Stability | 同 session：fallback / T1 / review / reanchor / Strength → system prompt 字节相同（AGENT-029、FALLBACK-014） |
