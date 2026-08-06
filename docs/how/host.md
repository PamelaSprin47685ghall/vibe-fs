# Host — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
OpenCode Host 提供了非阻断式 Hook 与 SDK 事件流，但在多实例、并发 Hook 与流式中断场景下存在数据竞争、身份缺失与取消信号混淆问题。Host 子模块必须在零修改 OpenCode 本体（ARCH-003）的硬性约束下，将 Host 的弱类型、流式、不确定性事件转化为强类型、原子化的领域事实（`HostSignal` 与 `ReconciledTurn`），并提供多 Worktree 插件实例间的严格并发隔离与共享数据同步机制。

### 2. 输入输出与规则边界
- **输入**：OpenCode 原始 SDK 消息事件、transform hook 输入、tool execute 阶段回调、会话生命周期事件。
- **输出**：强类型 `HostSignal` 领域信号、`ReconciledTurn` 终态转折对象、`ProviderInputSealed` 身份封印事实。
- **核心边界与不变量**：
  1. 不得改动 Host 源码或引入专用魔改接口（ARCH-003）。
  2. 传输层取消信号（`TurnAborted`）必须在 Reconciler 边界通过 `LoopKillArmed` 检查精确化分流：命中 LoopKill 转为 `TurnFailed` 推进 Fallback，未命中转为 `TurnAbandoned`（EXEC-020）。
  3. 跨 Worktree 实例共享单例表必须具备无锁不可变 Atomic Swap 并发保障（HOST-012）。

---

## HOST-004：Reconciler

- Single-flight：同一 session 同时最多一次 reconcile。  
- Dirty：idle 到达设 dirty。  
- Unknown：一次 idle 建 Dirty latch；最多 3 次因果重读；仍 Unknown 则保持 Dirty 等下一信号。

### 终态对齐（EXEC-020）

```fsharp
type TurnOutcome =
    | Completed
    | Failed of reason: string
    | Abandoned of reason: TurnAbandonReason   // 显式对应 EXEC-020 的 Agent 终态 (Completed | Failed | Abandoned)

type ReconciledTurn =
    { SessionId: SessionId
      UserMessageId: MessageId
      AssistantMessageId: MessageId
      AgentRole: AgentRole option
      Directory: string
      Parts: ProviderVisiblePart array
      Outcome: TurnOutcome }
```

**终态映射规则**：Host 事件级别的取消/中断信号（`TurnAborted` / `MessageAbortedError` / `finish=aborted`）**严禁**直接暴露给 Agent 领域终态。Reconciler 在消费 Host 事件时，必须按以下两阶段规则进行重构与映射：

```text
Host 事件到达 (TurnAborted / MessageAbortedError / finish=aborted)
  │
  ├─► 检查该 sessionId 的进程内局部标识 LoopKillArmed
  │     │
  │     ├─► 若 LoopKillArmed 命中：
  │     │     1. 清除该 Session 的 LoopKillArmed 标识
  │     │     2. 将 outcome 重新分类为 TurnFailed("LoopDetectedKill")
  │     │     3. 显式调用 FallbackController.recordConfirmedFailure 推进 Fallback 恢复槽
  │     │
  │     └─► 若 LoopKillArmed 未命中：
  │           1. 映射为 TurnAbandoned(UserOrSystemCancelled)
  │           2. 不触发 FallbackController 自动恢复
```

通过此映射，彻底解决 `TurnAborted` 导致 Fallback 不推进的冲突，同时保证非强杀的中止信号严格遵循 `EXEC-020` 的 `Completed | Failed | Abandoned` 终态代数。

---

## HOST-009：生命周期

```text
plugin start
→ create runtime services
→ register static tools/transforms
→ lazily create association/companion on first projection
→ dispose: cancel Tasks, kill PTY/process, dispose sessions
```

---

## 多实例状态初始化与并发隔离机制（HOST-012）

Host 按照 Working Directory 实例化插件；在多 worktree 环境中会触发第二插件实例。为了防止两个实例发生状态踩踏、重复创建 Companion 或丢失 Verdict 关联，必须按以下并发契约进行初始化与隔离：

### 1. 跨实例共享清单与并发同步契约（模块级单例）
下述表在进程内作为模块级全局单例注册，跨 worktree 实例共享：
- `SessionParents`：记录 fork 会话的父子关联树。
- `VerdictSessions`：记录 Review verdict 与验证会话绑定。
- `SessionDirectories`：记录会话与工作目录的绝对映射。

**并发与同步契约 (C2 并发安全)**：
- **存储模式**：共享表在 Node.js / Fable 环境中统一采用**不可变 Swap（Thread-safe Immutable Swap）** 机制。任何写操作（如新增 SessionParent 或更新 VerdictSession）均通过产生新的不可变 Map/Set 实例，并使用 CAS / 原子性指针替换完成。
- **读取模式**：读操作获取的是特定时刻的不可变 Snapshot 引用，保证单次 Reconcile/Projection 视角内的读一致性，无锁、无脏读风险。
- **写冲突处理**：并发写入采用 Single-Writer 序列化或 CAS 重试，严格禁止就地（in-place）修改可变字典，彻底杜绝丢失更新（Lost Update）。

### 2. 每实例独立隔离清单（PluginRuntimeScope）
每次插件实例化时创建独立的 `PluginRuntimeScope`，管理该 worktree 专属的状态：
- `AgentJournal`：每个实例独占其私有 Path `wanxiangshu-next/runtimes` 下的 Journal 句柄。
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
任一条不满足（经 SDK 读到 0 或 ≥2 未完成 assistant；或突变窗口读到旧快照）→ 不写 seal，
Review 只见 PendingIdentity/Rejected（REVIEW-010）。开裂侧安全：宁缺 seal 不赌同一身。

---

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
