# Phase 1: Strategic DDD Context Map

> **REF.md 宪法对应**：Phase 1 — 战略 DDD（不动运行机制）
>
> **目标**：统一通用语言，划定限界上下文边界，明确 Aggregate 与 Process Manager 清单，
> 区分领域内部事件与集成事件，标识全局 `SessionState` 为待拆分的 Read Model。
>
> **权威顺序**：实现 > 本文档。

---

## 1. 限界上下文地图

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Shared Kernel (共享内核)                          │
│  HostTools, ToolPermission, DomainError, Config, PromptFragments    │
│  ToolCatalog.Registry, CapsPrelude, MessageTransformPolicy          │
└─────────────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌───────────────────────┐
│  Review Context  │  │   Subsession    │  │  Squad Coordination  │
│  (Aggregate)     │  │  Execution      │  │  (Aggregate + PM)    │
│                  │  │  (Process Mgr)  │  │                       │
│  ReviewSession   │  │                 │  │  Dag, Task,           │
│  StateMachine    │  │  SubsessionState│  │  Scheduler,           │
│  ReviewVerdict   │  │  Decision.fs    │  │  SquadEvent,          │
│  ReviewLoopFold  │  │  Effect.fs      │  │  FfDecision           │
└────────┬─────────┘  └────────┬────────┘  └───────────┬───────────┘
         │                     │                       │
         │     ┌───────────────┴────────────────┐      │
         │     │   Fallback Context (Policy)     │      │
         │     │                                │      │
         │     │  FallbackKernel.StateMachine   │      │
         │     │  ErrorClass, FallbackPhase,    │      │
         │     │  FallbackChain, FallbackAction  │      │
         │     └────────────────────────────────┘      │
         │                     │                       │
         ▼                     ▼                       ▼
┌──────────────────────────────────────────────────────────────┐
│              Session Automation Processes                      │
│  (多个独立的 Process Manager, 同一逻辑分组)                       │
│                                                                │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────┐            │
│  │ Nudge    │  │ Continuation │  │ Compaction    │            │
│  │ Process  │  │ Process      │  │ Process       │            │
│  │ Manager  │  │ Manager      │  │ Manager       │            │
│  └──────────┘  └──────────────┘  └───────────────┘            │
└──────────────────────────────────────────────────────────────┘
         │                     │                       │
         ▼                     ▼                       ▼
┌──────────────────────────────────────────────────────────────┐
│              EventLog (Infrastructure / Read Model)            │
│                                                                │
│  SSOT: `.wanxiangshu.ndjson`                                   │
│  WanEvent { v, session, kind, at, payload }                    │
│                                                                │
│  📌 当前问题：全局 SessionState (30+ fields) 混洗所有投影        │
│  📌 Phase 6 目标：拆分为独立 Projection                         │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Aggregate 与 Process Manager 清单

### 2.1 Aggregate（标准事务边界）

| Aggregate | 模块 | 状态类型 | 命令入口 | 一致性边界 |
|-----------|------|----------|----------|-----------|
| **ReviewSession** | `Kernel/ReviewSession/` | `ReviewState` (Inactive/Active/Locked/Accepted/NeedsRevision) | `Activate, Submit, Lock, Unlock, Accept, RequestRevision` | 单次 review 循环内严格一致 |
| **SquadTask** | `Kernel/Wanxiangzhen/` | `TaskStatus` (pending/running/submitted/merged/done/cancelled) | `squad_update, submit_to_squad` | 单 task 状态转移原子性 |
| **SubsessionSession** | `Kernel/Subsession/` | `SubsessionState` (Available/Dispatching/Running/.../Poisoned) | `StartRun, DispatchAccepted, TurnErrorObserved, CancelRequested, ...` | 单子会话的严格一致性 |

### 2.2 Process Manager（跨 Aggregate 协调者）

| Process Manager | 模块 | 编排范围 | 订阅的事件 | 发送的命令 |
|----------------|------|---------|-----------|-----------|
| **SubsessionActor** | `Shell/SubsessionActor.fs` | Physical Session → Run → Turn → Abort → Poison | Host session events | `DispatchPrompt, AbortHostSession, ClosePhysicalSession` |
| **SquadScheduler** | `Kernel/Wanxiangzhen/Scheduler.fs` | DAG 任务拓扑编排 | `tasks_created, task_merged, task_done` | `startTask (经 Shell)` |
| **ContinuationManager** | `Shell/FallbackEventBridge.fs` | Fallback 续命六阶段 | `SessionError, SessionIdle, SessionBusy` | `SendContinue, RecoverWithPrompt` |

### 2.3 自动化流程（属于 Session Automation Processes 分组）

| 自动化流程 | 模块 | 触发时机 | 决策方式 | 输出 |
|-----------|------|---------|---------|------|
| **NudgeProcess** | `Kernel/Nudge/`, `Shell/NudgeRuntime*` | session.idle + `HumanTurnCompleted` | `deriveAction` 纯函数 | `session.prompt` (nudge reminder) |
| **CompactionProcess** | Host 侧 (OpenCode/Mux/OMP) | 上下文溢出时 | `isOverflow` 纯函数 | Host compaction API |
| **ContextBudgetProcess** | `Kernel/ContextBudget.fs` + `Shell/ContextBudgetStore.fs` | message transform 管线内 | `classifyPressure` 纯函数 | 同步注入 synthetic user message |

> **架构决定**：Nudge、Continuation、Compaction 不共享通用语言或一致性边界，因此**不是**一个 Bounded Context。
> 它们只是都运行在 Session 周围的独立 Process Manager，逻辑上归为「Session Automation Processes」分组。

---

## 3. 领域事件与集成事件区分

### 3.1 领域内部事件（仅被同一 Context 消费，不跨边界）

| 事件 | 所属 Context | 消费者 |
|------|-------------|--------|
| `loop_activated` | Review | ReviewLoopFold |
| `loop_cancelled` | Review | ReviewLoopFold |
| `review_verdict` | Review | ReviewLoopFold |
| `subsession_run_started` | Subsession | SubsessionFold |
| `subsession_run_settled` | Subsession | SubsessionFold |
| `subsession_turn_*` | Subsession | SubsessionDecision |
| `subsession_decision_committed` | Subsession | SubsessionEventStore |
| `squad_*` / `task_*` | Squad | SquadScheduler, SquadFold |

### 3.2 集成事件（跨 Context 或跨宿主消费）

| 事件 | 生产者 | 消费者 | 用途 |
|------|--------|--------|------|
| `human_turn_started` | 各宿主 Hook | NudgeProcess, FallbackContext, ContinuationProcess | 清空去重状态, 重置 generation |
| `user_abort_observed` | 各宿主 Hook | FallbackContext, NudgeProcess | 设置 Cancelled lifecycle |
| `assistant_completed` | 各宿主 NudgeHook | NudgeProcess (snapshot), SessionState | 更新 nudge 快照 |
| `work_backlog_committed` | todowrite tool | BacklogProjection, NudgeProcess (todos) | 更新待办清单 |
| `continuation_*` | FallbackEventBridge | OwnerEpisodeState, FallbackInjectionFold | 续命六阶段追踪 |
| `nudge_*` | NudgeRuntime | NudgeDedup, NudgeSnapshot, OwnerEpisodeState | nudge 生命周期追踪 |
| `compaction_*` | Host (OpenCode) | OwnerEpisodeState, ContextBudget | compaction 生命周期 |
| `subagent_spawned` | SubagentDispatcher | SubagentFold (父 session) | 子代理持久化投影 |
| `subagent_continued` | continue tool | SubagentFold (父 session) | 更新继续提示列表 |

### 3.3 集成事件 vs 领域事件区分原则

```
是否仅被同一模块(目录)内的代码消费？
  ├── 是 → 领域内部事件（可能后期提升为集成事件）
  └── 否 → 集成事件（生产者/消费者分属不同 Context）
```

---

## 4. 全局 SessionState Read Model 标识

### 4.1 当前问题

`Kernel/EventLog/Fold.fs` 中的 `SessionState` 类型包含 **30+** 字段，涵盖所有投影：

```fsharp
type SessionState = {
    ReviewLoop: ReviewLoopFold         // Review Context 投影
    ReviewTask: string option          // Review Context 投影
    Backlog: BacklogEntry list          // WorkBacklog 投影
    BacklogSnapshot: WorkBacklogSnapshot // WorkBacklog 投影
    NudgeDedup: NudgeDedupState        // Nudge Process 去重
    NudgeSnapshot: NudgeSnapshotState   // Nudge Process 快照
    Subagents: Map<string, SubagentState> // Subagent 投影
    FallbackInjection: FallbackInjectionState // Fallback 投影
    LatestHumanTurn: HumanTurnState option   // 轮次追踪
    SessionGeneration: int              // generation 管理
    CancelGeneration: int
    ActiveContinuationGen: int
    ActiveContinuationCancelGen: int
    FallbackLifecycle: FallbackLifecycle option // Fallback 生命周期
    FallbackPhase: FallbackPhase option         // Fallback 阶段
    SessionOwner: string option         // Episode 所有者
    PendingLease: ReplayLeaseState option       // Continuation 租约
    ContinuationOrdinal: int
    ContinuationStage: EpisodeStage
    PendingNudgeLease: ReplayNudgeLeaseState option // Nudge 租约
    NudgeOrdinal: int
    NudgeStage: EpisodeStage
    ActiveCompaction: ReplayCompactionState option // Compaction 租约
    ActiveCompactionId: string option
    CompactionOrdinal: int
    CompactionStage: EpisodeStage
    IsCompacted: bool
    CompactionGeneration: int
    HumanTurnOrdinal: int
    LastHumanTurnMessageId: string option
    EventCount: int
}
```

### 4.2 目标：Phase 6 拆分计划

| 投影名称 | 涉及字段 | 所有者 | 依赖事件 |
|----------|---------|--------|---------|
| **ReviewProjection** | `ReviewLoop, ReviewTask` | Review Context | `loop_activated, loop_cancelled, review_verdict` |
| **WorkBacklogProjection** | `Backlog, BacklogSnapshot` | WorkBacklog Context | `work_backlog_committed` |
| **NudgeDecisionProjection** | `NudgeDedup, NudgeSnapshot` | NudgeProcess | `nudge_*, assistant_completed, loop_*, work_backlog_committed` |
| **SubagentProjection** | `Subagents` | Subsession Context | `subagent_spawned, subagent_continued` |
| **FallbackInjectionProjection** | `FallbackInjection` | Fallback Context | `fallback_continue_injected, continuation_*` |
| **HumanTurnProjection** | `LatestHumanTurn, HumanTurnOrdinal, LastHumanTurnMessageId` | Shared | `human_turn_started` |
| **GenerationProjection** | `SessionGeneration, CancelGeneration, ActiveContinuationGen, ActiveContinuationCancelGen` | Shared | `human_turn_started, user_abort_observed, continuation_requested` |
| **OwnerEpisodeProjection** | `SessionOwner, PendingLease, ContinuationOrdinal, ContinuationStage, PendingNudgeLease, NudgeOrdinal, NudgeStage, ActiveCompaction, CompactionOrdinal, CompactionStage, IsCompacted, CompactionGeneration` | Shared (多 Context) | `continuation_*, nudge_*, compaction_*, human_turn_started` |

> **拆分策略**：当前 `applyEvent` 函数在一次折叠中更新所有 30+ 字段。
> Phase 6 将替换 `applyEvent` 为 `reviewProjection.Apply(event)`, `nudgeProjection.Apply(event)` 等独立投影函数。
> 组合查询层 `SessionOverview = combine ReviewProjection + NudgeProjection + ...` 保持向后兼容。

---

## 5. 聚合边界与一致性规则

| 聚合 | 一致性模型 | 命令验证规则 | 备注 |
|------|-----------|------------|------|
| ReviewSession | 强一致性（单记录） | `Activate` 仅在 `Inactive` 状态合法；`Accept` 仅在 `Locked` 或 `Active` 合法 | 已正确实现 |
| SubsessionSession | 强一致性（事件溯源） | 9 状态 × 12 命令 = 108 种匹配对，非法转移返回 `NoChange(IgnoreReason)` | `Decision.fs` 已穷举 |
| SquadTask | 强一致性（状态机） | `pending→running` (依赖全 merged), `running→submitted` (slave submit) | VALID_TRANSITIONS 表已固定 |

---

## 6. 架构纪律（Phase 1 产出）

### 6.1 模块依赖方向
```
Kernel/ReviewSession/ ──→ Kernel/EventLog/ (write events)
Kernel/Subsession/     ──→ Kernel/EventLog/ (write events)
Kernel/Wanxiangzhen/   ──→ Kernel/EventLog/ (write events)

Kernel/Nudge/          ──→ Kernel/EventLog/ (read fold), Kernel/EventLog/ReviewLoopFold
Kernel/FallbackKernel/ ──→ Kernel/EventLog/ (read fold)

EventLog/              ──→ 无上层依赖 (只被消费)
```

### 6.2 新增上下文代码位置

| 上下文 | 目录 | 说明 |
|--------|------|------|
| Review | `src/Kernel/ReviewSession/` | ✅ 已存在 |
| Subsession Execution | `src/Kernel/Subsession/` + `src/Shell/SubsessionActor*` | ✅ 已存在 |
| Fallback Policy | `src/Kernel/FallbackKernel/` + `src/Shell/FallbackRuntime*`, `FallbackEventBridge*` | ✅ 已存在 |
| Nudge Process | `src/Kernel/Nudge/` + `src/Shell/NudgeRuntime*` | ✅ 已存在 |
| WorkBacklog | `src/Kernel/WorkBacklog.fs` + `BacklogProjection*` | ✅ 已存在 |
| Squad Coordination | `src/Kernel/Wanxiangzhen/` + `src/Shell/Wanxiangzhen/` | ✅ 已存在 |
| **独立 Projections** | `src/Kernel/Projections/` | 📌 待 Phase 6 创建 |
| **Process Managers** | `src/Shell/ProcessManagers/` | 📌 待 Phase 7 创建 |
| **Flow Kernel** | `src/Kernel/Flow/` | 📌 待 Phase 3 创建 |

### 6.3 事件标记约定

```fsharp
// 领域内部事件 — 仅有 kind 字符串，不跨上下文共享
let eventKindLoopActivated = "loop_activated"      // Review 内部
let eventKindSubsessionRunStarted = "subsession_run_started"  // Subsession 内部

// 集成事件 — 跨上下文消费，添加 payload 契约
let eventKindHumanTurnStarted = "human_turn_started"  // 多 Context 消费
let eventKindAssistantCompleted = "assistant_completed"  // Nudge + Fallback 消费
```

---

## 7. 下一步（Phase 2 → Phase 8）

| 阶段 | 依赖 | 关键动作 |
|------|------|---------|
| Phase 2 (RAII) | 当前代码库 | 消除 SubsessionActor 中的手动 Timer map，引入 ResourcePlan |
| Phase 3 (Flow Kernel) | Phase 2 | 定义最小 IAsyncEnumerable 流程代数 |
| Phase 4 (Actor 拆分) | Phase 2-3 | SubsessionActor → CommandProcessor + EffectSupervisor + ResourceScope |
| Phase 5 (Reactive Edges) | Phase 4 | CommittedProgress + EphemeralTelemetry 双轨 |
| Phase 6 (Projection 拆分) | Phase 5 | 本上下文图拆分 `SessionState` 为独立 Projection |
| Phase 7 (Process Manager) | Phase 5-6 | Nudge, Continuation, Compaction 抽取为独立 PM |
| Phase 8 (清理) | Phase 6-7 | 删除全局 Fold, 旧 RuntimeScope, Actor 基础设施 |

---

*生成日期：2026-07-16*
*对应 REF.md § Phase 1 + Phase 6 + Phase 7*
