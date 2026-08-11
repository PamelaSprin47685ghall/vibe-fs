# Host — 目标实现

## Implements

行为合同见 `what/host.md`；本文件只描述 hook 收敛、snapshot reconcile 和共享运行时算法。

## Ownership

Host 适配、信号和共享状态边界见 `shape/host.md`。

---

## HOST-004：Reconciler

- Single-flight：同一 session 同时最多一次 reconcile。  
- Dirty：idle 到达设 dirty。  
- 有界因果重读：每次 idle 至多 3 次因果重读（`rereadsRemaining = maxCausalRereads + 1`）。  
- `decideStep`（GLORY-070 / HOST-004 rev.3 / rabbit §7）：因果重读耗尽后 `Provisional` → `Publish`；只有 `SnapshotError` / `NoTurn` 保持 `StopPass`（无对象可作用，等下一粗粒度信号重新入队）。`Unknown` 不再由重读耗尽直接推导业务 repair：带 `IdleWake`（fresh `QuiescencePermit`）evidence 时 `Publish` 稳定观测给业务（TurnWorkflow / InteractionRepair 决定是否 missing-final-report）；`Retry` / `Failure` wake 下的 `Unknown` 不交接（`StopPass`）。`ReconcileDecision` 只有 observation vocabulary（`Reread` / `Publish` / `StopPass`），不含业务 repair 名字。
- 观测稳定性 ≠ 静止资格：重复 snapshot 相同只证明观测稳定，不证明发送瞬间仍 idle。idle-derived continuation 必须同时满足：
  1. snapshot / 业务决策认为 continuation 有用；
  2. 起源的 `QuiescencePermit` 在 side-effect 时刻仍 fresh（发送边界再次 `TryConsume`）。
- 三层分离：snapshot 观测（`ObservedTurn` + reconciliation 私有 `SnapshotObservation`）→ wake evidence（`ReconcileWake = IdleWake of QuiescencePermit | RetryWake | FailureWake | AbortWake`）→ physical continuation admission（`ContinuationAdmission = Ordinary | RequiresQuiescence of QuiescencePermit`）。`materializeActive` 全程携带 wake 直到 publish；`onTurn` 收 `ReconciledTurnContext { Turn; Quiescence: QuiescencePermit option }`，仅 `IdleWake` 有 `Some permit`。`AbortWake` 下 `TurnUnknown` / `Provisional` 只能 StopPass，禁止构造 missing-final-report、interaction-repair 或裸 `#`。

### 接线

- `BeginProviderAttempt(sessionId)`：在 `experimental.chat.messages.transform` 最早同步位置（sessionId 解析后、任何 `let!` 之前）调用，使旧 idle permit 在新 provider request 开始构建时立即失效。
- `SessionIdle`：`LoopSensor.ResetDetector` 后 `ObserveIdle` 得 permit，随 `SignalIdle(sessionId, permit)` 进入 Reconciler。
- `AttemptAborted`：先 `RevokeCurrentAttempt(sessionId)`，再原样 signal Reconciler 的 `AbortWake`；禁止改写成 `ProviderFailure`。
- `SessionDeleted`：`DropSession`，旧 permit 永久失效。
- 发送边界：`trySendIdleContinuation` / `trySendIdleInteractionRepair` 唯一封装——`TryConsume` 失败 → `Superseded`（不写 claim、不发消息）；成功 → 同一同步调用链直接进入 dispatcher，中间禁止 await。`TryConsume` 不得复制到多个 caller。

### 终态对齐（EXEC-020）

这里的 `TurnOutcome` 是可 publish / 稳定边界上的 provider-turn 分类，不是 EXEC-020 的 `AgentCompletionOutcome`。Clean Break：`TurnUnknown` 不得作为 `TurnOutcome` 成员；它是 reconciliation 私有的 `SnapshotObservation`（finish=None 的稳定 snapshot），不得穿过稳定业务 turn 边界。

```fsharp
/// Reconciliation-private. Not a publishable TurnOutcome case.
type SnapshotObservation =
    | TurnUnknown

type TurnOutcome =
    | TurnInProgress
    | TurnNeedsContinuation of reason: string
    | TurnCompleted
    | TurnAborted of reason: string
    | TurnFailed of error: string

type ReconciledTurn =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      ProviderRun: ProviderRunIdentity
      AgentRole: AgentRole option
      Directory: string option
      Parts: MessagePart array
      Finish: string option
      ErrorName: string option
      Model: OpencodeModel option
      Outcome: TurnOutcome }
```

行为不变（语义分层后）：`IdleWake` 下因果重读耗尽仍为 `TurnUnknown` → `Publish` 稳定观测交接（禁止静默 StopPass）；业务侧 TurnWorkflow / InteractionRepair 在有 quiescence 时才发 missing-final-report。无 idle 权限的 `Retry` / `Failure` / `Abort` wake 只 StopPass，等待下一真实信号。`publishDecision` 不得接收 `TurnUnknown` 作为 Outcome（类型上已不可达）；Unknown 交接用 placeholder Outcome 做 provisional seal / dedupe。

Host 的 `MessageAbortedError` / `finish=aborted` 先被 Reconciler 分类为 `TurnAborted`。`TurnCompletionProgram` 再消费这个控制面结局：

```text
ReconciledTurn.Outcome = TurnAborted
  │
  ├─► 检查该 sessionId 的进程内局部事实 LoopKillArmed
  │     │
  │     ├─► 若 LoopKillArmed 命中：
  │     │     1. 清除该 Session 的 LoopKillArmed 标识
  │     │     2. 走与 provider failure 等价的 FallbackController 路径
  │     │
  │     └─► 若 LoopKillArmed 未命中：
  │           1. 终止/清理该 provider turn
  │           2. 不构造 RunCompletion，不推进 Fallback
```

`TurnAborted` 因而可以存在于 provider-turn 分类中，但绝不成为 Agent `RunCompletion`。把两种代数合并为同一个 DU 会同时破坏 LOOP-006 与 EXEC-020。

---

## HOST-009：生命周期

```text
plugin start
→ create runtime services
→ register static tools/transforms
→ lazily create association/companion on first projection
→ dispose: cancel Tasks, kill PTY/process, dispose sessions
```

Attached 创建统一走（HOST-015 扁平拓扑；HOST-008 ExecutionClass × Ownership）：

```text
query family root children（owner ≠ root 时并查 owner children，兼容扁平前的物理位置）
→ 有 journal 关联（RestoredSessionId）且恰好 1 个 id+agent+title 匹配：复用
→ journal 关联的 id 不存在：Replacement（新建，物理挂 root）
→ 无 journal 关联：不复用任何候选，直接新建（Created）
→ id 匹配但 agent/title 冲突、多个 id 匹配或查询失败：fail closed
```

登记顺序：先写入 `SessionAssociation`（`ExecutionClass` + `Ownership`），再发送首个 prompt。

```text
Companion / Bookkeeper
  → InternalLeaf + Attached；Transform 见 InternalLeaf 则跳过 Companion 创建

SyncInspector / SyncCoder
  → Work + Attached；MAY 再创建自己的 Companion（Work 能力路径）
  → 复用 Teacher CE 代数（Returned → Completion），不登记为 InternalLeaf

G2 过渡 Teacher
  → 仍可作为 transitional InternalLeaf 创建/恢复（legacy 关联，非长期 AttachmentKind）；不得当作已删除
```

`AttachedSessionRuntime` 是 Attached 的唯一创建、恢复、级联 abort/retire owner；owner 删除/取消时
级联清理，不进入公开 Handle/list/join。

---

## 多实例状态初始化与并发隔离机制（HOST-012）

Host 按照 Working Directory 实例化插件；在多 worktree 环境中会触发第二插件实例。为了防止两个实例发生状态踩踏、重复创建 Companion 或丢失 Verdict 关联，必须按以下并发契约进行初始化与隔离：

### 1. 跨实例共享清单与并发同步契约（模块级单例）
下述表在进程内作为模块级全局单例注册，跨 worktree 实例共享：
- `SessionParents`：记录 fork 会话的父子关联树。
- `VerdictSessions`：记录 Review verdict 与验证会话绑定。
- `SessionDirectories`：记录会话与工作目录的绝对映射。

**并发与同步契约 (C2 并发安全)**：这些表只由同一 Node.js event loop 访问。每次查询、登记、删除或快照复制必须在一个不跨 `await` 的同步片段内完成；禁止读取后跨 `await` 再按旧值回写。若未来引入 Worker/共享内存，必须先为 HOST-012 增加明确的消息所有者或原子同步协议，不能把“CAS”当作未指定原语的要求。

### 2. 每实例独立隔离清单（PluginRuntimeScope）
每次插件实例化时创建独立的 `PluginRuntimeScope`，管理该 worktree 专属的状态：
- `AgentJournal`：每个实例独占 `RuntimePath` 给出的私有 Journal 句柄。
- `Companions/Blogger` 缓存：每个实例只持有属于自己 worktree 的 Companion/Blogger 运行实例。
- `OwnedSessions`：仅记录当前 worktree 实例发起的 Managed Sessions。
- `UserMessageBindings`：仅维护当前实例内部的 Prompt/UserMessage 绑定。
- `Hook 订阅`：每个实例独立向 OpenCode 注册 hook 回调。

### 3. 多实例检测与 Dispose 顺序
- **二次实例化检测**：启动时检测目标 directory 是否已有活跃的 `PluginRuntimeScope`。若检测到重叠，抛出 `HostContractUnsupported` 并进入安全终止。
- **Dispose 顺序**：插件被卸载或销毁时，必须按以下顺序严格清理资源：
  `取消 Hook 订阅 -> 撤销待处理 Task -> 杀掉该实例管理的 PTY/进程组 -> 清理实例局部 Companion/Blogger 内存缓存 -> 释放 Journal 文件锁`

---

## Compaction 程序（归属 HOST-006）

### 预防

关闭 automatic / overflow（共键）/ autocontinue / prune。  
静态读配置不够：首个 managed session 第一轮请求后，compaction pseudo-run 必须为零。  
设置不可用优先于 pseudo-run 报错（根因在设置）。

### 收容

识别：`agent="compaction"` ∨ `mode="compaction"` ∨ `summary=true`。

```text
观察 pseudo-run
→ ActivePrefixEpoch 退役（Snapshot→None，EpochId+=1）
→ PrefixCoverage 归零
→ RecordCoverage.IngestedThrough 与 Frames 保留
→ 写 ContextReanchored（PERSIST-010，同一 ObservedCompactionMessageId 幂等）
```

入口：`HostCompactionGate` + 启动探测。关掉配置单独不算已证明预防。

---

## HOST-010：Transform → ProviderRunIdentity

transform input 为空对象。绑定靠因果读：在 transform 中从 SDK 找**唯一**未完成 assistant：

```text
role = assistant
time.completed 未设
parentID = transform 输出最后一条 user 的 id
id 为 session 内 assistant 最大者
```

```text
命中 0 或 ≥2 → 不写 seal
compaction / summary 路径 → 不写 seal
```

无 seal 时第二次 PERFECT 只能 PendingIdentity/Rejected（REVIEW-010）。

Canary 用 journal 代理等式，不要求共时观测 transform 内存 id ≡ ToolContext.messageID：

```text
Reviewer: ReviewVerdictRecorded.ProviderRun == ProviderInputSealed.ProviderRun
X: PrefixRebaseCommitted.SolvingProviderRun 唯一非空
```

### 形式化保证（OpenCode 源码锚定）

对照 `../opencode`（commit e024e2ef）逐条验证，因果读在 SDK 行为下唯一成立：

**引理 1：每次 transform 恰对应一条新持久化 assistant。** 主循环每轮创建新 assistant
（`session/prompt.ts:1187-1194`，`id=MessageID.ascending()`、`parentID=lastUser.id`、
`time={created}` 无 completed）并**先** `sessions.updateMessage` 持久化（`:1197`），**后**
才 `plugin.trigger("experimental.chat.messages.transform")`（`:1255`）。每轮恰好一次 transform。

**引理 2：transform 输入不含飞行中 assistant，绑定必须走 SDK 重读，不能看输入。**
transform 收到的 `{ messages }` 是**循环顶部**快照（`:1094`，在新 assistant 创建之前）。
in-flight `msg` 已落盘但不在输入里；绑定时经 SDK 读会话（会含 `:1197` 已持久化的它）
才能命中。

**引理 3：重试复用同一 assistant，不产生第二条未完成者。** provider 重试在
`handle.process` 内部经 `Effect.retry(SessionRetry.policy)`（`session/processor.ts:660`）
进行，复用同一 `ctx.assistantMessage`，不新建消息、不重触发 transform。`process` 的 onExit
统一 `time.completed = Date.now()` 并 `updateMessage`（`processor.ts:594-597`），
成功/出错/compaction 全路径皆达；中断由 `finalizeInterruptedAssistant`（`prompt.ts:1203-1211`）finalize。
`continue` 型路径（subtask `:1150`、compaction `:1162`、overflow `:1166`）均不创建 assistant。
故 transform 触发时，`parentID=lastUser.id` 的**唯一未完成 assistant** 恰为刚创建的 `msg`。

**引理 4：`id` 单调 → 「session 内 assistant 最大」唯一选中它。** `MessageID.ascending()`
经 `generateID(prefix,"ascending")` 带 lastTimestamp 单调护栏（`core/src/id/id.ts:16-18,54-55`），
每进程全局严格递增；新创建的即最大，且在 `handle.process` 完成前无人再建更大者。

**并发前提与 fail-closed。** 唯一性要求单 actor 写 assistant（managed Session 单 flight，HOST-004）。

---

## Student / Teacher source canary（OpenCode v1.18.14）

生产依赖 `opencode-ai=1.18.14`，证据读取 `../opencode` 的同名 tag：

- `session/prompt.ts:createUserMessage`：`plugin.trigger("chat.message")` 位于 message/part 持久化前。
- `session/prompt.ts:PromptInput`：`tools` 进入 Session permission；`session/llm/request.ts:resolveTools`
  以 Agent + Session permission 裁剪每个 provider request。
- `session/processor.ts`：普通 tool result 后返回 `continue`；`session/prompt.ts:runLoop` 在后续无 tool-call
  的 Assistant finish 才退出。
- `client.session.abort` 只停止当前 processing；Session 记录保留，可接受下一次 `prompt_async`。
- `client.session.children` 返回 `id/parentID/agent/title`，可证明 Teacher 复用、永久丢失或歧义。

这些锚点必须有安装版本 gate 与真实 Host e2e；仅源码存在不替代 provider-visible schema、return 路由和
同 Session 三轮复用 canary。
任一条不满足（经 SDK 读到 0 或 ≥2 未完成 assistant；或突变窗口读到旧快照）→ 不写 seal，
Review 只见 PendingIdentity/Rejected（REVIEW-010）。开裂侧安全：宁缺 seal 不赌同一身。

---

## Marker 程序（归属 HOST-013）

链序（seal 之前）：

```text
StrengthReplay → XTraceCapture → Companion → XWire → EnforcerHost
→ StrengthSpeculate → PairProgrammingThoughtTransform
→ HostMessageProjection.sanitizeMessages (HOST-016) → ReviewSeal
```

- 适用判定：仅 `SessionExecutionClass.Work` 进入本程序。`journal` 存在时以 SessionAssociation 为准：
  `Ownership = Attached(_, Companion)`（或 `isCompanion`）→ 跳过 `PairProgrammingThoughtTransform`，不读 tip、
  不 append durable pair、不改 `messages`。InternalLeaf Bookkeeper 同跳过。无 journal / 无 association 时按
  非 Companion Work 处理（保持既有测试与未知 session 行为）。
- 每次 transform 的 commit 顺序：读 durable anchored pair 序列 → strip raw 中已有 HOST-013 synthetic 消息（仅在 durable anchor 足够完整时）→ 校验（真实消息地址唯一）→ 过滤 placeable pairs（CallGap 与 ResultGap 的 anchor 均在当前真实消息中）→ 内存中 replay 可放置历史 → 决定本轮新 pair 的 placement（仅当该 placement 尚不存在）→ 内存构造候选 fact → 内存渲染完整 wire → 校验全部不变量 → append durable fact（失败 fail closed，禁止忽略后照发或降级为不注入）→ 返回已校验消息。
- gap replay（禁止再出现 `historyBlock`）：输入真实消息 + durable synthetic entries，输出：

```text
Start 组（ordinal 升序）
逐条真实消息：
    Before(id) 组（ordinal 升序）→ 消息 → After(id) 组（ordinal 升序）
```

组内排序唯一合法：`Ordinal` 升序，同 ordinal 时 call 先于 result。历史 synthetic 位置只由它自己 durable 的 gap anchor 决定；当前 transcript 长什么样不得改变历史 pair 的位置。

- 本轮新 pair 的 placement 决策（只读当前**真实**消息，不含 synthetic；trailing user = 最后一条消息是 user）：
  - 末端存在同轮 tool batch（`Req1 Req2 Resp1 Resp2`，或紧跟 trailing user 之前）：`CallGap = After(Req2)`、`ResultGap = After(Resp2)`；
  - 无 tool batch 且最后一条是 user：`CallGap = Before(trailingUser)`、`ResultGap = Before(trailingUser)`；
  - 空 transcript：`Start` / `Start`；
  - 无 trailing user（含末尾为 assistant 文本的 continuation transcript）：`After(lastReal)` / `After(lastReal)`。
  新 pair 的 gap 必须落在本次追加区；旧「pair 总在最后一条 user 任意位置之前」在 continuation transcript 上会中途插入新 pair 破坏 prefix，已废弃。
  查同一 placement identity（SessionId + CallGap + ResultGap）是否已存在：存在 → 只 replay 既有 pair，不 append 新 fact；不存在 → 走上述 commit 顺序。
- 一个 pair 的 wire：assistant `tool-call`（工具名 `auto-injected`、输入 `{}`）与对应 `tool-result`（同一 `callID`、`status = completed`、输出 `markerText`）。有同批 tool 时 call / result 分别挂 call 批末 / result 批末；无同批 tool 时二者同 gap 相邻。
- `markerText` 只对本次新 pair 读取当时的 prior tip；历史 pair 保留其原始正文。有 prior tip 时为英文 Nudge、空行、中文正文；无 prior tip 时仅为中文正文。中文正文由 `ProjectionConstants.PairProgrammingGuidelineText` 定义。prior tip 由 owner 的 RecentTips 解析（主 session），不是 Blogger 自身 tip 注入。
- pair 的 synthetic side-channel 标识为 `source = "pair-programming-auto-injected"`；两侧均按 source 排除于 XTrace 等非 provider 投影，禁止按正文识别或过滤。
- `CallId = digest(transcript identity + source + Ordinal)`；禁止随机、时间、anchor 或 tip 文本参与身份。正文与 source 单点定义。
- 不变量校验至少：全部历史 anchor 已解析、无重复 placement、call/result 同 callID、synthetic 字节确定（同输入同输出）、当前 placement 与决策算法一致。
- Strength frames 必须在本轮 pair placement 决策前已进入 raw view（STRENGTH-009）；因此新 tool-result anchor 仍被 PairProgrammingThought 覆盖。StrengthReplay 只重建 Promoted 历史且发生在 XTraceCapture 前；StrengthSpeculate 只为当前 target request 注入 Candidate 且发生在 XTraceCapture 后（STRENGTH-006..009）。
- ReviewSeal 覆盖恢复后的全部历史 pair、本次新 pair与所有 Strength provider-visible bytes；历史 pair 原位不变，以保持 Prefix Cache。Reviewer 路径 Strength 恒 K0。Blogger 跳过注入时 ReviewSeal 只覆盖无 auto-injected 的消息视图。
- tip nudge 查找：`latestTipNudge` 仅在非 Companion 路径调用；不得以当前 session 是 Blogger 为由把 tip 写进 Blogger transcript。
- 实现点：`SpikePlugin` transform 在 `PairProgrammingThoughtTransform.tryInject` 之前用 association 门禁短路。
- 注入旁路：`WANXIANGSHU_SKIP_AUTO_INJECTED=1` 或 transcript provider 为 `cursor` 时，`tryInject` 仍 strip + replay 历史 pair，但跳过「本轮 placement 尚不存在 → append 新 fact」分支（`PairProgrammingThoughtTransform.skipAutoInjectedRequested`；provider 由 `providerIdFromMessages` 读取）。

## 空 Content 预防（归属 HOST-016）

在 `PairProgrammingThoughtTransform` 之后、`ReviewSeal` 之前，遍历 `messages` 进行合法性保障：
1. 任何 `assistant` 消息：若无 tool part（`type: tool / tool-call / tool_call / tool-result / tool_result` 或 top-level `tool_calls`），且无非空 text part / content：
   - 若存在 reasoning / thinking part，提取其非空文本填充为 text part（`{ type: "text", text: r }`）；
   - 若不存在 reasoning 文本，补充最小非空占位 text part（`{ type: "text", text: "..." }`）。
2. 任何 `user` 消息：若无非空 text part / content，补充非空 text part（`{ type: "text", text: "#" }`）。
3. 经 `HostMessageProjection.sanitizeMessages` 处理后的消息就地写回 `outObj.messages`，确保 ReviewSeal 与上游 Provider 获得相同且合法的数据。
