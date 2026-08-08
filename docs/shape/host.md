# Host — 所有权与边界

## HOST-003：Transport ≠ Domain

Transport 可以是 plugin event 或 global SSE。  
Domain 合同永远是 typed `HostSignal`。业务层不得观察原始 payload。

```fsharp
type HostSignal =
    | SessionIdle of sessionId: SessionId
    | ProviderRetry of {| SessionId; Attempt; UserMessageId: MessageId option |}
    | SessionDeleted of sessionId: SessionId
```

`ProviderRetry.Attempt` 只用于诊断与唤醒，不是 Fallback 领域计数（FALLBACK-010）。

## HOST-008：Session 关联所有权

```fsharp
type ManagedSessionKind =
    | WorkSession
    | SatelliteSession of ownerSessionId: SessionId * kind: SatelliteKind

type SatelliteKind =
    | Companion
    | Teacher

type SessionAssociation =
    { SessionId: SessionId
      Kind: ManagedSessionKind
      BloggerSessionId: SessionId option
      TeacherSessionId: SessionId option
      ParentSessionId: SessionId option }
```

不变量：

```text
每个 WorkSession 恰好一个 CompanionSession
Student WorkSession 在学习任务期间恰好一个 Teacher Satellite
每个 SatelliteSession 恰好属于一个 WorkSession
SatelliteSession.BloggerSessionId = None
SatelliteSession.TeacherSessionId = None   // Satellite 不递归
Companion 与 Teacher SessionId 均不等于 owner，也彼此不同
```

关联由 **Session 种类** 决定，不由 Role / Tier / 工具面 / Logical Run / Authority / Fallback 临时决定。
`SatelliteRuntime` 是两类 Satellite 的唯一创建、恢复、注册、级联取消与 retire owner；Companion 与 Teacher
只能提供各自的 payload/terminal 策略，不得复制 Session 所有权框架。

优先存宿主 metadata 并以 Journal 关联做 durable keyed lookup。重启时：Host 证明原 Satellite 存在则复用；
Host 证明永久丢失则按该 kind 的恢复合同 Replacement；Host 查询失败、重复候选或归属冲突则 fail closed。

物理拓扑恒扁平（HOST-015）：每个 Managed child 的 Host 物理 parent 是 family root，不是逻辑 owner；
`ParentSessionId` 只承载 journal 证明的逻辑归属，不参与 Host 树的恢复匹配。

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

Student/Teacher run、QA writer 与 Satellite flight 是每插件实例状态；durable association 与 Host child
metadata 用于重建，不得把自然语言问题、回答或 QA 正文放进共享表。

### 并发与同步契约 (C2 并发安全)
- 共享表的并发所有者是单一 Node.js event loop，不假定不存在的跨线程 CAS。
- 单次查改与枚举必须同步完成，不跨 `await`；需要跨异步边界使用的数据先复制成不可变快照。
- 禁止“读取 → await → 按旧值回写”的 read-modify-write；若引入 Worker/共享内存，须先新增明确的消息所有者或原子同步端口。

## 永久 guideline pair 所有权

HOST-013 的唯一持久状态是按 provider transcript 分区的 append-only pair 序列：

```fsharp
type PairProgrammingGuideline =
    { Ordinal: int64
      CallId: ToolCallId
      MarkerText: string }
```

`Ordinal` 严格递增；`CallId` 在该 transcript 内唯一。记录一经追加不可修改、删除或换位。每条记录只渲染为相邻的 assistant `guideline` tool-call 与对应 completed tool-result，两侧共享 `CallId`，均标记 `source = "pair-programming-guideline"` 与 `synthetic = true`。

Coordinator 是追加与恢复的唯一 writer；Projection 只读取完整序列并确定性渲染。XTrace、Companion、Blogger、work record 与 compaction 不拥有也不复制正文。pair 自含调用与结果，不依赖任何外部消息作为 anchor。

## 空 Content 预防边界

| 角色 | 触发条件 | 补救动作 |
|------|----------|----------|
| `assistant` | 无 `tool_calls` / tool part，且 text part / content 为空 | 提取 reasoning/thinking 文本填充为 text part；无 reasoning 则填充 `"..."` |
| `user` | text part / content 为空 | 填充 `"#"` text part |
