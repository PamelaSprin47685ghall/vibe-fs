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

允许用户面同名（如 `executor` 角色与工具）若语境清楚；实现必须用类型命名空间区分。  
禁止为消歧引入 Translator / Governor / Broker 等无价值中间层。

## ARCH-007：工具同名条件

仅当 schema、权限、生命周期、结果语义**完全相同**时才共享工具名。  
`join` 可在 Manager 与 Orchestrator 共享（语义同：消费当前 owner 可用 completion）。

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
通用“状态先于表示”原则由 ARCH-011 拥有；Session ownership 由 HOST-008 拥有；SyncInspector/SyncCoder 的
Returned→Completion 调用协议由 EXEC-026/028 拥有。Host 本体仍遵守 ARCH-003。
