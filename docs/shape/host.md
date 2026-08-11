# Host — 所有权与边界

## HOST-003：Transport ≠ Domain

Transport 为 in-process plugin event（hooks.event）。  
Domain 合同永远是 typed `HostSignal`。业务层不得观察原始 payload。

```fsharp
type HostSignal =
    | SessionIdle of sessionId: SessionId
    | ProviderRetry of {| SessionId; Attempt; UserMessageId: MessageId option |}
    | SessionDeleted of sessionId: SessionId
```

`ProviderRetry.Attempt` 只用于诊断与唤醒，不是 Fallback 领域计数（FALLBACK-010）。

## HOST-008：Session 关联所有权

长期所有权由两个正交维度决定，**不再**以 `SatelliteKind` 单轴（历史曾含 Teacher）为唯一模型：

```fsharp
type SessionExecutionClass =
    | Work
    | InternalLeaf

type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId

type SessionOwnership =
    | Root
    | Attached of ownerSessionId: SessionId * attachment: AttachmentKind
```

Durable `SessionAssociation`（FactCodec）仍以 `ManagedSessionKind` 记录 Work↔Companion；正交
ExecutionClass × Ownership 是派生视图（`SessionOwnershipClassification`）。`SatelliteKind` **仅**
`Companion`。历史 G2 过渡字段 `TeacherSessionId` / `StudentTeacherLinked`：**G3 已删除（gone）**，
不在 durable association、也不在长期 `AttachmentKind`。

组合规则：

```text
Dedicated SyncInspector / SyncCoder
  = Work + Attached(SyncInspector|SyncCoder)
  MAY 拥有自己的 Companion（Work 能力路径）

Companion / Bookkeeper
  = InternalLeaf + Attached(Companion|Bookkeeper _)

Work + Root
  = 普通主会话；恰好一个 Companion（InternalLeaf + Attached Companion）
```

SyncDelegate（SyncInspector / SyncCoder）使用 Returned→Completion 调用代数
（send prompt → await Returned → await Completion），**不是** InternalLeaf / no-Companion Satellite。
禁止把 dedicated Sync* 实现成历史 Teacher-style InternalLeaf。

G3 clean-break：`Role.Student` / `Role.Teacher`、Student↔Teacher 绑定、HOST-014 canary 与
`TeacherSessionId` 投影 **absent**（AGENT-020 空缺）。不得写成 pending / 仍存在的过渡路径。

不变量：

```text
关联由 ExecutionClass × Ownership 决定，不由 Role / Tier / 工具面 / Logical Run / Authority / Fallback 临时决定
每个 Attached 恰好属于一个 ownerSessionId；Attached SessionId ≠ owner
InternalLeaf 不持有 Companion（BloggerSessionId / Companion 侧 = None），也不再挂其它 Attached
Work + Root：恰好一个 Companion/BloggerSessionId，且 ≠ owner
Work + Attached Sync*：Companion 可选；若有则 ≠ owner、≠ 该 Sync* 自身
Bookkeeper 绑定具体 transactionId；不得与 Companion / Sync* 身份混用
```

`AttachedSessionRuntime` 是 Attached 会话的唯一创建、恢复、
注册、级联取消与 retire owner；各 AttachmentKind 只提供 payload/terminal 策略，不得复制所有权框架。

优先存宿主 metadata 并以 Journal 关联做 durable keyed lookup。重启时：Host 证明原 Attached 存在则复用；
Host 证明永久丢失则按该 AttachmentKind 的恢复合同 Replacement；Host 查询失败、重复候选或归属冲突则
fail closed（与 HOST-015 相同安全侧）。

物理拓扑恒扁平（HOST-015）：每个 Managed child 的 Host 物理 parent 是 family root，不是逻辑 owner；
`ParentSessionId` 只承载 journal 证明的逻辑归属，不参与 Host 树的恢复匹配。逻辑上允许
Work+Attached 再挂 InternalLeaf Companion / Bookkeeper；物理上全部重挂 family root，Host 树深度仍为 2。

## HOST-011：Tool 身份的两个半边

| 边界 | message id | call id |
|------|------------|---------|
| `ToolContext`（execute） | 有 | 有 |
| `tool.execute.before/after` | 无 | 有 |

`ProviderRunIdentity` + `ToolCallId` 只能同时从 `ToolContext` 取得；任一缺失 → fail closed。  
禁止用 after 的 callID 与别处 messageID 猜测配对。  
禁止使用 SDK/Host 不存在的字段（如 `userMessageID`）冒充物理用户消息身份。

## HOST-012：多实例共享与并发边界

Host 按 directory 实例化插件；worktree 触发第二实例。跨实例因果链（fork → verdict）上：

```text
必须共享（模块级单例）：
  SessionParents, VerdictSessions, SessionDirectories

必须每实例独有：
  AgentJournal, Companions/Blogger 缓存, OwnedSessions,
  UserMessageBindings, hook 订阅
```

新增跨实例状态必须同时登记共享清单与 `PluginRuntimeScope` 初始化，否则第二实例静默失配。

SyncDelegate / Attached flight（含 Sync* / Companion / Bookkeeper）是每插件实例状态；durable association
与 Host child metadata 用于重建。历史 Student/Teacher run、QA writer：**G3 absent**，不得当作现行共享表行。

### 并发与同步契约 (C2 并发安全)
- 共享表的并发所有者是单一 Node.js event loop，不假定不存在的跨线程 CAS。
- 单次查改与枚举必须同步完成，不跨 `await`；需要跨异步边界使用的数据先复制成不可变快照。
- 禁止“读取 → await → 按旧值回写”的 read-modify-write；若引入 Worker/共享内存，须先新增明确的消息所有者或原子同步端口。

## 永久 auto-injected pair 所有权

HOST-013 的唯一持久状态是按 provider transcript 分区的 append-only anchored pair 序列：

```fsharp
type TranscriptMessageAddress = private TranscriptMessageAddress of string

type TranscriptGap =
    | Start
    | Before of TranscriptMessageAddress
    | After of TranscriptMessageAddress

type PairProgrammingGuideline =
    { Ordinal: int64
      CallId: ToolCallId
      MarkerText: string
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }
```

`Ordinal` 严格递增；`CallId` 在该 transcript 内唯一；`CallGap` / `ResultGap` 是 pair 两个 half 各自的 transcript gap anchor。记录一经追加不可修改、删除或换位。每条记录渲染为 assistant `auto-injected` tool-call 与对应 completed tool-result，两侧共享 `CallId`，均标记 `source = "pair-programming-auto-injected"` 与 `synthetic = true`。

`TranscriptMessageAddress` 是 Host transcript message address（raw message 的 `info.id` / `id`，与 Session snapshot 以 message `Id` 寻址一致）的窄类型 codec；禁止偷换成 `PhysicalUserMessageId`、`AuthorityRootUserMessageId`、`ProviderRunIdentity` 或 `ToolCallId`，除非该值在具体位置上确实就是 transcript message address。

**放置边界**：历史 synthetic half 的位置只由它自己的 durable gap anchor 决定，replay 按 `Start → 逐条真实消息（Before 组 → 消息 → After 组）` 注入，组内 ordinal 升序、同 ordinal call 先于 result。当前 transcript 长什么样不得改变历史 pair 的位置。新 pair 的 gap 只由当前真实消息末端结构决定（tool batch 时 call 挂 call 批末、result 挂 result 批末；无 tool 时二者同 gap 相邻；空历史用 `Start`）。同一 placement identity（SessionId + CallGap + ResultGap）最多一个 pair；重复 transform 只 replay。

Coordinator 是追加与恢复的唯一 writer；Projection 只按 anchor 确定性渲染，不再决定历史位置。XTrace、Companion、Blogger、work record 与 compaction 不拥有也不复制正文。anchor 引用的真实消息在当前真实 view 中缺失时，该 pair 不参与本次 wire 渲染（禁止重定位；禁止因此 AbortSession）；durable fact 保留，完整 transcript 回来后再 replay。legacy 无 anchor fact 使该 session fail closed，不做启发式迁移。

**所有权边界**：`AttachmentKind.Companion`（Blogger）与 `Bookkeeper` 等 InternalLeaf transcript 不进入
HOST-013 writer 路径——不为这些 session 创建 `Guidelines` 投影、不 append `PairProgrammingGuidelineAnchored`、
不在其 provider wire 上渲染 pair。`SessionExecutionClass.Work`（含 Root 与 Attached SyncInspector/SyncCoder）
按 HOST-013 永久追加。历史 Teacher InternalLeaf / HOST-014：**G3 已删除**，不得借本边界写成仍存在。

## Idle-derived continuation 发送资格（HOST-004）

`SessionQuiescenceGate` 是每插件实例 process-local 的 side-effect admission capability，只回答一个问题：一个以 idle 为前提的副作用现在是否仍有资格发送。它不是领域状态机：不写 Journal、不参与 crash recovery、不表达业务 stage。重启后 gate 清空（没有 fresh idle → 没有 permit → 不自动发送 idle-derived continuation），安全侧失败。

```fsharp
type QuiescencePermit =
    private { SessionId: SessionId; AttemptSerial: int64 }

type private Activity =
    | Unknown
    | Running of attemptSerial: int64
    | Idle of attemptSerial: int64
    | IdleConsumed of attemptSerial: int64
    | Revoked of attemptSerial: int64
```

唯一状态转换：

```text
BeginProviderAttempt(session)   serial+1 → Running(serial)；任何旧 permit 立即失效
ObserveIdle(session)             Running(serial) → Idle(serial)，返回 Permit(session, serial)
TryConsume(permit)               state == Idle(permit.AttemptSerial) → IdleConsumed(serial) → true；否则 false
RevokeCurrentAttempt(session)   当前 serial → Revoked(serial)；全部现存 permit 永久失效
DropSession(session)             清空该 session 状态，旧 permit 永久失效
```

`AttemptSerial` 只是进程内同步 token，**禁止写入 Journal**（HOST-007）。

接线边界：

- `BeginProviderAttempt` 必须在每次 provider request 构建前的最早同步位置调用（`experimental.chat.messages.transform` 入口，任何 `let!` 之前），不得等 request 已运行。
- `ObserveIdle` 在收到 `SessionIdle` 时调用；permit 从 idle 观察携带到 side-effect 边界（`ReconcileWake = IdleWake of QuiescencePermit`），禁止用 scheduler dispatch generation 冒充 provider attempt serial。
- `AttemptAborted` 同步调用 `RevokeCurrentAttempt`，再发 `AbortWake`；即使延迟 `SessionIdle` 随后到达，Revoked 也不得退回 Idle。只有下一次真实 `BeginProviderAttempt` 才建立新 serial。
- 最终物理发送前必须再次 `TryConsume`；`TryConsume` 与 dispatcher send 之间禁止 await（防 TOCTOU）。permit 失效 = `Superseded`，不是错误、不写 `PluginPromptClaimed`。

idle-derived continuation（missing-final-report、interaction-repair、ManagerIdleEncouragement、SyncDelegateIdleNudge）必须同时满足：业务决策认为值得继续 + fresh `QuiescencePermit`。`ProviderRetryAttempt`、`BusyAgentNudge`、显式用户 continuation、`FinalityRejected` 不由 idle 前提产生，不走 gate。

**所有权**：gate 由 `PluginRuntimeScope` 持有（与 NudgeSent / AbortedSessions / LoopSensor 同层），不放 SharedState / Journal projection / PromptAuthority。若 session 因插件实例变化发生 owner 转移，新实例没有旧 permit，安全侧失败。

## 空 Content 预防边界

| 角色 | 触发条件 | 补救动作 |
|------|----------|----------|
| `assistant` | 无 `tool_calls` / tool part，且 text part / content 为空 | 提取 reasoning/thinking 文本填充为 text part；无 reasoning 则填充 `"..."` |
| `user` | text part / content 为空 | 填充 `"#"` text part |
