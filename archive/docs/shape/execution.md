# 执行 — 所有权与边界

## EXEC-009：Handle 生命周期

Handle 四态：Active / CompletedAwaitingJoin / Abandoned / Retired。  
tombstone 与 abandon **均不可回退**。  
持久化身份 `HandleId`；消费路径唯一，禁止第二处「假装完成」。

## EXEC-014：Distiller 私有 Runtime

Distiller 映射子会话是私有 runtime，不暴露为公开 fork / `horizon` 目标（配合 AGENT-008）。  
map/reduce、chunk、session id 属机器 Assignment，不进 provider 工具面。

Distiller child 的 durable handle = `HandleOwnership.HostOwnedHidden`（GLORY-002 / SURFACE-006）：对父 session 的 `list` / `join` / `horizon` / EXEC-016 background guard / 父恢复（RestoreHandles）全部不可见；记录仍持久，仅供 Host-owned workflow 审计与自身恢复。

`run` 工具同步掌控 Distiller 生命周期：fork → permit-gated await → 摘要 → 返回。调用方不 join、不承担生命周期，退出（suicide）不被 distiller handle 阻塞。

## 工具面所有权（EXEC-001..005 / 029）

| 面 | owner 角色 | 边界 |
|----|------------|------|
| `fork` / `join` / `horizon` | Manager | 使命内 witness；非独立路 |
| `commission` / `join` / `horizon` | Orchestrator | 独立集成之路；与 `fork` 不同名（EXEC-029） |
| 终端四动词 + `run` + `join` / `horizon` | DevOps | 删除 `fork-pty`；`run` ≠ Distiller office |
| `inspect` / `establish-behavior` / `repair-behavior` | SyncDelegate callers | 见 EXEC-026/031；无独立 `return` |

`horizon` 只读在场名册（Byname / TerminalName）+ 每个 parent-visible child session 最新 durable 工作记录（来源：最新 `BlogFrame` 正文）；handle 与 child `BlogProjection` 必须来自同一个 journal snapshot。它不拥有 watcher、timer、subscription 或 refresh loop；latest blob 缺失/digest 无效时 fail closed，不回退旧 frame。禁止 id / status DTO 穿过 provider（EXEC-005/030）。

## JoinAttempt 中断所有权

`JoinAttemptRegistry` 只持有当前 active attempt 的物理 lease；零 active attempt 时没有可写的 future wake。external-user ingress 只 resolve lease，不拥有 child runtime。Esc 同时拥有两层效果：lease 产生当前 join 的 operator-abort 自然语言后果；父 provider 的 `TurnAborted` cleanup 调用 `AbortChildren`，取消全部仍在运行的 sub-session。session delete、parent teardown 与 runtime dispose 保持同一 child cancellation owner；用户消息路径不得借用它。

## EXEC-023：恢复所有权与线性序

Session/Child 恢复：端口全强制；结果分支穷尽（RecoveredActive / Terminal / Abandoned / RecoveryIncomplete / RecoveryBlocked）。  
线性序：permit → join；禁止跳步恢复。  
Distiller 定向等待（AwaitAgentWithPermit）同样受 permit 门：每次定向 await 前重新 requirePermit，校验通过才可读目标 agent 的 Journal 权威 completion；TCS/Pulse 仅作唤醒，不构成第二份 RunCompletion 真理源。

## EXEC-024：Mailbox 双通道

```text
agent 路径：仅 Pulse（结果读 Journal）
PTY 路径：PublishPty
```

禁止把 agent completion 塞进 PTY 通道或反之。

## EXEC-026：SatelliteRuntime 与 SyncDelegate 所有权

`SatelliteRuntime` 统一拥有 Companion 的 child create、Host children reconcile、Session kind
登记、abort、retire 与 owner 级联；不得复制 child Session map。`SatelliteKind` **仅** `Companion`。

### Student / Teacher — G3 已删除（absent）

`StudentRun` / `teacherCalls` / `StudentQaStore` / Learn·Compile / `StudentCompile` idle nudge /
`InvokeTeacher` / SKILL mutation evidence：**G3 已删除（absent）**（EXEC-027 / AGENT-020…022 空缺）。
不得再列 `runs` / `teacherCalls` / `skillMutations` 为现行物理 owner，也不得用 registry presence
充当业务阶段 PC。后继见下节 SyncDelegate（及 `SyncDelegateIdleNudge`，PROMPT-003）。

### SyncDelegate 所有权

通用 `SyncDelegate` 所有权（Dedicated Inspector / Dedicated Coder；入口工具
`inspect` / `establish-behavior` / `repair-behavior`）。
`SyncDelegateRuntime` 拥有 dedicated synchronous callee 的 create/reuse、Host children reconcile、abort、
retire 与 OwnerReuseScope 级联；不得复制 child Session map，也不得把 SyncDelegate 伪装成 fork/handle/join。

物理 owner：

- sync batch mailbox：key = `(immediate caller ReuseScope, SyncDelegateRole)`；pending batch 身份 = 当前 assistant `ProviderRunIdentity`，expected members = Host message 中同 role 的完整 `ToolCallId` 顺序。禁止 microtask drain window。
- active batch lease：同 key 至多一个 active batch；不同 ProviderRun 在前一 completion 前到达 fail closed，不排队、不叠发。
- `attachedSessions`：`(OwnerReuseScopeId, SyncDelegateRole)` → at most one live dedicated Session
- 进行中的 sync batch：普通 Assistant completion 为结束信号；Host 物化
  `InvocationStartCursor..InvocationEndCursor` 的 bounded WorkRecord（`includeOpening=false`）；仅 canonical invocation 投影正文，其余 siblings 引用 canonical result（EXEC-028/031）

单一 CE 调用栈（业务 caller 不可见）：

```text
Admit(current ToolCallId against ProviderRun expected members)
→ when batch complete: reserve (ReuseScope, role)
→ GetOrCreate(OwnerReuseScopeId, role)
→ prepare every member in provider order
→ concat → one Send
→ await ordinary Completion
→ materialize one bounded WorkRecord
→ canonical caller gets WorkRecord; siblings reference canonical caller
```

**删除**独立 `return` 工具、`Returned` await、`pendingCompletionTexts` /
`SyncDelegateReturnCompletion` magic literal、TextComplete 改写路径。Host 边界不为 return 武装
pending text；无 flight / 错误 owner / 物化失败均 fail closed。

不变量：

1. **Semantic batch / serialization**：同一 assistant `ProviderRunIdentity`、同一 `SyncDelegateRole` 的 sibling calls 必须按 Host tool-call 顺序合并成一个 active batch；同 key 同时最多一个 active batch。不得靠 scheduler 到达顺序决定成员；不得把另一个 ProviderRun 排队进 active dedicated Session。嵌套合法且不得死锁：`DevOps → Coder → Inspector`（各层 ownership 绑定各自 immediate caller ReuseScope）。禁止按 family root 串行。
2. **Reuse key**：`(OwnerReuseScopeId, SyncDelegateRole)`；同 scope 兼容续问复用同一 Session，
   completion 后不 retire / 不 dispose。
3. **Tier**：owner effective tier → deterministic delegate tier（`fast→fast`，`deep→deep`）；
   模型不可每轮选择 target Agent。
4. **单次完成**：callee 普通 completion 即结束整个 sync batch；exactly one canonical invocation（provider 顺序第一项）返回 bounded WorkRecord（最后一条助手文本在 Recent work），siblings 只引用 canonical result。无第二 await、无 N 份正文复制、无 `answer` 字段（EXEC-031）。
5. **Prompt split**：每个 batch member 各自拥有 `{ Charge; PrepareProviderPrompt }`；batch admission 完整后按 provider 顺序 prepare，再分别 concat 成单个 `SyncDelegatePromptRequest = { Charge; ProviderPrompt }`。generic workflow 只把 `ProviderPrompt` 交给 SendPrompt，把合并后的 `Charge` 交给 Opening/Casebook prompt hook（EXEC-032）。
6. **Lifetime**：Dedicated Session lifetime = OwnerReuseScope lifetime；graceful ReuseScope close 才
   retire/release（Casebook synthesis 若启用见 Casebook 合同，不属本条所有权）。

Dedicated Inspector/Coder = Work + Attached（可有 Companion），**不是**历史 Teacher-style InternalLeaf /
no-Companion Satellite。Student/Teacher 路径已删除；通用 SyncDelegate 不继承该拓扑。

## 单一写入口（完成）

PTY completion 写入口 = backend onExit（EXEC-015）。  
Agent completion 经 Journal 事实 + join / SyncDelegate WorkRecord 消费，不由碎片事件拼。
