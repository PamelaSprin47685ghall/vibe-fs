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
    | CompanionSession of mainSessionId: SessionId

type SessionAssociation =
    { SessionId: SessionId
      Kind: ManagedSessionKind
      BloggerSessionId: SessionId option
      ParentSessionId: SessionId option
      Role: AgentRole }
```

不变量：

```text
每个 WorkSession 恰好一个 CompanionSession
每个 CompanionSession 恰好属于一个 WorkSession
CompanionSession.BloggerSessionId = None   // Y 不递归
SessionId ≠ BloggerSessionId
```

关联由 **Session 种类** 决定，不由 Role / Tier / 工具面 / Logical Run / Authority / Fallback 决定。  
优先存宿主 metadata；重启复用同一 Blogger，不向空白 Y 发历史 delta。

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

### 并发与同步契约 (C2 并发安全)
- 共享表的并发所有者是单一 Node.js event loop，不假定不存在的跨线程 CAS。
- 单次查改与枚举必须同步完成，不跨 `await`；需要跨异步边界使用的数据先复制成不可变快照。
- 禁止“读取 → await → 按旧值回写”的 read-modify-write；若引入 Worker/共享内存，须先新增明确的消息所有者或原子同步端口。
