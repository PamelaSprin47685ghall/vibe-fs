# Host — 目标实现

## HOST-004：Reconciler

- Single-flight：同一 session 同时最多一次 reconcile。  
- Dirty：idle 到达设 dirty。  
- Unknown：一次 idle 建 Dirty latch；最多 3 次因果重读；仍 Unknown 则保持 Dirty 等下一信号。

```fsharp
type ReconciledTurn =
    { SessionId; UserMessageId; AssistantMessageId
      AgentRole: AgentRole option; Directory: string
      Parts: ProviderVisiblePart array
      Outcome: TurnOutcome }  // Completed | Failed | Aborted
```

## HOST-009：生命周期

```text
plugin start
→ create runtime services
→ register static tools/transforms
→ lazily create association/companion on first projection
→ dispose: cancel Tasks, kill PTY/process, dispose sessions
```

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
任一条不满足（经 SDK 读到 0 或 ≥2 未完成 assistant；或突变窗口读到旧快照）→ 不写 seal，
Review 只见 PendingIdentity/Rejected（REVIEW-010）。开裂侧安全：宁缺 seal 不赌同一身。

## Marker 程序（归属 HOST-013）

链序（seal 之前）：

```text
XTraceCapture → Companion → XWire → EnforcerHost
→ PairProgrammingThoughtTransform → ReviewSeal
```

- 锚点：每个 user 或已完成 tool-result；`anchorIndex+1` 插入；从后向前处理。  
- 全锚点重放（Host 不持久化 synthetic）。  
- `id = digest(sessionId + anchorMessageId + source)`，禁止随机/时间。  
- 幂等键 = 锚点 identity + source；同锚点只插一次。  
- 排除路径按 `source` 过滤，禁止只按中文正文过滤。  
- 文本与 source 单点定义。
