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

## Host signal / transform 合成边界

| 关注点 | 拥有者 | 边界 |
|------|------|------|
| Host signal 订阅与路由 | `HostSignalBootstrap` | 只回答怎样订阅、哪个 signal 交给哪个 owner；`WiredSignals` 合成根 |
| Turn 观测政策 | `HostTurnObserver` | Strength promotion、FamilyRecovery、Blogger recovery opportunity、`TurnWorkflow.observe` |
| Compaction 观测 | `HostCompactionObserver` | HOST-006 startup gate + reanchor |
| SessionDeleted teardown | `HostSessionDeletion` | LoopSensor / Strength cancel / SyncDelegate finalize / Quiescence Drop / Dispose；不拥有 Scheduler |
| Reconcile coalesce / drain | `Reconciler.Scheduler` | 同 session 信号合并、generation 隔离、最多一个 drain；外部 API 不变 |
| Reconcile causal pass | `ReconcilePass` | snapshot → evidence → reread/publish；不拥有锁与 generation dictionary |
| Provider transform 顺序 | `PluginTransforms` | 只保留 hook 顺序；禁止吸收各阶段算法 |
| Strength replay / traced | `StrengthReplay` | Promoted replay before XTrace；Traced close after capture |

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
    | StrengthReplica

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

Companion / Bookkeeper / StrengthReplica
  = InternalLeaf + Attached(Companion|Bookkeeper _|StrengthReplica)

StrengthReplica
  = decision-local InternalLeaf；无 Companion/SyncDelegate/嵌套 Attached，完成即 retire（STRENGTH-004/014）

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
StrengthReplica 每个 owner 最多一个 active attachment；不得进入 SatelliteKind，且不跨 Strength decision 复用 transcript
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

`Ordinal` 严格递增；`CallId` 在该 transcript 内唯一；`CallGap` / `ResultGap` 是 provider-independent occurrence 的两个 transcript gap anchor。记录一经追加不可修改、删除或换位。普通 provider 把每条记录渲染为一条 completed `auto-injected` Host tool part；OpenCode 再展开为 tool-call 与 tool-result。

**Tool.Def 边界**：`auto-injected` 的可执行 entity 由 `AutoInjectedTool` 拥有（空参数，execute 恒返回 `OK`），经 `ToolRegistry` 进入 `hooks.tool`。`PairProgrammingThoughtTransform` 只渲染已 completed 的历史 pair，不执行该工具。二者同名、分属 entity 与 renderer，禁止把 execute 写进 transform。Blogger / Distiller 的 Host permission 保持 deny。

Cursor 不创建 synthetic message/part，只在 `ResultGap` 紧跟真实 terminal tool result 时，把该真实 result 的 provider-visible 终态文本投影为 `original + NUL + BOM + MarkerText`。所有投影共享 canonical `MarkerText`；ordinary synthetic identity 继续由 `source = "pair-programming-auto-injected"` + stable id 标识，Cursor suffix identity 只来自 durable occurrence 与 anchor，不按正文识别。

`TranscriptMessageAddress` 是 Host transcript message address（raw message 的 `info.id` / `id`，与 Session snapshot 以 message `Id` 寻址一致）的窄类型 codec；禁止偷换成 `PhysicalUserMessageId`、`AuthorityRootUserMessageId`、`ProviderRunIdentity` 或 `ToolCallId`，除非该值在具体位置上确实就是 transcript message address。

**放置边界**：历史 synthetic 的位置只由它自己的 durable gap anchor 决定，replay 按 `Start → 逐条真实消息（Before 组 → 消息 → After 组）` 注入，组内 ordinal 升序。当前 transcript 长什么样不得改变历史 pair 的位置。新 pair 的 gap 只由当前真实消息末端结构决定（tool batch 时 identity 仍是 call 批末 / result 批末，ordinary 只在 ResultGap 渲染一条 completed 行；无 tool 时二者同 gap 相邻；空历史用 `Start`）。同一 placement identity（SessionId + CallGap + ResultGap）最多一个 pair；重复 transform 只 replay。

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

## SessionProviderLanguage 绑定写（HOST-026）

行为：`what/host.md` HOST-026；类型与可译边界：`PROMPT-017`。  
本节只定 **谁写 / 谁读**。

| 关注点 | 唯一 writer | 读者 / 禁止 |
|------|------|------|
| 全局语言偏好 → `SessionProviderLanguage` | **session 创建瞬间**唯一绑定写（Host session 装配路径） | 创建后不可变；Fallback / Strength / restart / reanchor / BlindPlan T1 **不得**改写 |
| child / attached / InternalLeaf（Companion、SyncDelegate、Bookkeeper、StrengthReplica） | **继承** owner 或 commissioner 的已绑语言 | 禁止各自再读全局偏好 |
| HOST-013 marker 正文语言 | 只读 `SessionProviderLanguage` 选 EN/ZH guideline + Nudge | 禁止 transform 现场读全局；历史 marker 永不因语言偏好变更重算 |
| Opening / Office Library / tool description / argument prose / tool consequence / WorkRecord headings | 各文本 owner 按已绑语言取 localized representation（PROMPT-016/017/020） | Host 不拥有文案 SSOT；只保证绑定字节连续；**禁** system 与 tool contract 混语 |
| protocol identifiers | — | tool 名 / argument / wire field / enum / path / command / `exit_code` **永不翻译**（ARCH-016 Gate C） |

```text
global preference
    ↓ 仅 session create
SessionProviderLanguage (immutable)
    ↓
provider-facing localizable prose
```

用户事后改全局偏好 → 只影响**此后新建** session。已开 Life 的世界语保持前缀连续。

与 Persona：`SessionPersona` 绑定属 AGENT-028（创建一次）；`SessionProviderLanguage` 绑定属本条；二者同为 session 创建冻结，Host 不得在运行中重写任一。

## Magic Todo V1 membrane 所有权边界（归属 HOST-017..025）

可观察总行为与 canary 合同见 `what/host.md` HOST-017（及 HOST-018..025）；义务协议见 `TODO-*`。本节只定实现层所有权与挂载边界，不重复定义条款。

### 分层所有权

```text
OpenCode Host
  = transport / builtin todowrite executor / TodoTable UI sink
  = V1 hook 挂载面（definition / before / after）

Wanxiangshu Domain + Journal
  = Magic Todo protocol owner（TODO-001..015）
  = CurrentObligations、checkpoint、BlindPlan T1、review obligation、settlement、rebase evidence
```

禁止 Host core 修改；禁止 plugin 同名 tool 覆盖 builtin `todowrite`（会夺取 executor 与 store 契约）。Membrane 只叠加钩子，把原 executor **降级**为 compatibility sink（TODO-007）。

### Hook 面与身份半边（扩展 HOST-011）

| 面 | 可见身份 | 可变合同 |
|----|----------|----------|
| `tool.definition` | tool 名 / schema | 同时写 `parameters`+`jsonSchema`+`description`；provider-visible **obligations** schema 唯一广告点（TODO-002） |
| `tool.execute.before` | `sessionID`+`callID` only | 原地 mutation `args` 字段；async 可等待 ConsumableReview（TODO-006） |
| `tool.execute.after` | `sessionID`+`callID` only | 改写模型可见 `output`；ensure Accepted（HOST-022）；T1 revelation 字节属 TODO-015 |
| executor `ToolContext` | message id + call id | 跑原 V1 decoder；只见 compatibility 投影 |

before/after **不得**猜配 messageID。定位 ToolPart/assistant/run/ordinal/XTrace range 必须经完整 SDK snapshot 唯一成立（HOST-025）；否则 fail closed，不得上线。

### deferred materialization barrier + 非别名边界（HOST-019）

```text
before live args → non-enumerable `todos` V1 view → executor
                  JSON persistence 仍只见 `obligations`
        ↘ captured canonical
           deferred prepare:
           snapshot {} → wait/reread same physical carrier
           materialized ToolPart.input = captured canonical
           → digest / admission

after → await deferred prepare → Accepted
```

`{}` 只表示 Host 尚未完成 tool-call input materialization，不能进入 admission。ProviderInputDigest 归 materialized ToolPart.input；carrier 变化或 canonical 不等立即 fail closed。before 不等待磁盘/轮询；after 不得绕过 deferred prepare。compatibility mutation 不得污染 historical input。

### Ephemeral bridge 边界（HOST-021 载体）

```js
// 形状示意，非第二真相源
Symbol("wanxiangshu.magic-todo.bridge")
Map<`${sessionID}:${callID}`, carrier>
```

| 允许 | 禁止 |
|------|------|
| process-local before→after 短传：settledOld / proposal / preview / previousReview | 表示 Prepared/Accepted/checkpoint/review obligation/settlement |
| after 成功或 tool/turn failure cleanup 删除 entry | 跨进程、写 Journal、crash recovery 读取 |
| carrier 上 non-enumerable Symbol 字段 | 依赖 before/after hook output 偶然同对象身份 |

Durable 只在 AgentJournal（TODO-012）。bridge 丢失 ⇒ 从 Prepared + physical evidence 重建，不从 Host TodoTable 反推。

### Compatibility sink 边界（HOST-023）

```text
Host TodoTable     = optimistic compatibility projection（无 stable id）
MagicTodoProjection = canonical obligations truth（TODO-007）
```

- sink 字段策略不得把 `kind`/`id`/`status`/`priority`/`reviewing` 回写成 provider 冷状态（TODO-002/003）。
- REVISE 消费后 CurrentObligations 未升格 ⇒ 幂等 reconcile sink 到 settled current；**不**产生 checkpoint/review（TODO-005/007）。
- sink 永不拥有 recovery 权；后续新 Life 不得把同 session TodoTable 再当 canonical seed（TODO-011）。

### V2 runner 边界（HOST-024）

```text
MagicTodo Manager Attempt
  → 仅允许已证明 definition+before+after 的 path
  → V2 local settle 无 hook parity ⇒ construction fail closed（TODO-004）
```

不得静默裸 `SessionTodo.update`。解除限制前必须重跑 HOST-019..025 全套 canary。

### 与 HOST-013 / BlindPlan 的交界

HOST-013 pair-programming 仍是 **Work session 通用** marker，不专属 Manager；正文语言读 `SessionProviderLanguage`（HOST-026）。  
Manager-only 持续 todowrite / BlindPlan 文案是 TODO-013/015 表面片段，在 role=Manager 且 `todowrite` provider-visible 时叠加；禁止并入 `host/pair-programming-guideline`。  
Host membrane **不**拥有 OpeningPolicy / OpeningMaterial；T1 关闭 Opening 属 TODO-015 / COMPANION-014。

## NEEDHELP Host shape（HOST-027）

`NeedHelpEventCodec` 只负责解码 Host raw stream identity：先由 `message.part.updated` 的 `part.type = reasoning` 登记 `(SessionId, ProviderRun, PartId)`，再把同一 PartId 的 `message.part.delta(field = text)` 适配成 `{ SessionId; ProviderRun; PartId; Field; Delta }` reasoning delta；legacy direct reasoning-field shape 仅保留 codec compatibility，不作为现行 OpenCode Host 假设。`NeedHelpSensor` 保存每个 live attempt 的有限 rolling suffix、reasoning PartId 集与 armed identity。Attempt identity 必须来自当前 provider run，不能退化成 session-wide boolean。Sensor 只请求 `AbortSession`；实际 fast/deep/consultation 决策在 `ReconciledTurnContext` 上完成，且请求者 binding 从同一 Host snapshot 中 `Id = ProviderRunIdentity` 的 assistant `SessionMessage.Agent` 精确读取，禁止从 fallback cursor / SelectedAgent 猜。deep assistance 在 AbortWake 只 claim owner attempt；physical consultation child 只能在同一 aborted turn 的 fresh `IdleRevisit` 后创建，防止落入 OpenCode 尚未结束的 parent-abort descendant sweep。该 `QuiescencePermit` 只作为 transport fence，不消费 HOST-004 已 revoke 的 idle-derived continuation capability。

LoopDetector/LoopSensor 状态、LoopKillArmed 与 NeedHelpArmed 分离。若物理 abort settle 前两者都已 arm，显式 NEEDHELP assistance 在 reconcile 有确定性优先级，并立即清除 LoopKillArmed；一个 abort 只消费一个 typed cause，竞争 cause 不得泄漏到下一 attempt。StrengthReplica、Companion/InternalLeaf 不进入 NEEDHELP raw sensor admission。consultation child 的完成 turn 被 assistance 特殊路由抢先消费，故最后一条助手文本须已在 child XTrace parts（Recent work）中；`captureTerminal` 只写私有完成标记，不构成 LWR 段。随后从同一 XTrace 物化 `LifecycleWorkRecord(includeOpening=false)`；禁止返回只含旧 Chronicle、缺本轮陈述的 stale child record。
