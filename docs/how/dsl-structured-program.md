# DSL 结构化程序规则 — 目标实现

行为见 `what/dsl-structured-program.md`；边界见 `shape/dsl-structured-program.md`。

## 问题陈述

当前仓库存在以下偏离结构化程序 DSL 的实现形态：

1. `Process/NodeProcessWait.fs` 用四个 `let mutable` 布尔表达程序阶段。
2. `Session/Companion.fs` 用 `slotArmed` 可变布尔作为控制流状态。
3. `Session/BloggerRuntimeState.fs` 的 `BloggerRuntimeCell` 把 `InFlight/Parked/Sealed` 等阶段编码为字段。
4. `Session/BloggerRuntimeState.fs` 的 `BloggerToolRecovery` 隐藏 runtime 状态。
5. `Domain/SessionRecovery.fs` 的 `RecoveryTrace` 被解释为第二证明系统。
6. `Application/Reconciliation/ReconciledTurn.fs` 与 `Domain/ReconcileProgram.fs` 重复定义 `TurnOutcome`。
7. `Kernel/Roles.fs` 与 `Session/AgentRoleIdentity.fs` 重复定义十态角色类型。
8. `Journal/AgentFact` 是跨多个 bounded context 的 41-case 大总和类型。
9. `dsl-ownership.mjs` 仅按名称黑名单检查，无法识别语义状态机。

## 迁移目标

### 1. `NodeProcessWait`

拆分为：

```fsharp
awaitExitOrDeadline : Child -> int -> CancellationToken -> Task<ExitOrDeadline>
killAndAwaitAcknowledgement : Child -> CancellationToken -> Task<KillResult>
waitForProcess : Ports -> Child -> int -> CancellationToken -> Task<ProcessWaitResult>
```

删除 `timedOut`、`cancelled`、`killSent`、`killAckExpired`。

### 2. `Companion` 恢复槽

用一次性 `TaskCompletionSource<ProviderSemanticProjection>` 表示等待下一份材料的物理 waiter。

删除：

- `slotArmed`
- `ArmRecoverySlot`
- `DisarmRecoverySlot`
- `IsRecoveryArmed`

保留：

- `StartRecoveryOpportunity`
- `OfferMainMaterial`
- `CancelRecoveryOpportunity`

### 3. `BloggerToolRecovery`

由 `BloggerCrashRecovery.repairEvidence` 从 Host transcript / rawMessages 推导，删除 runtime cell 中的 `Recovery` 字段。

### 4. `BloggerRuntimeCell`

逐步移除：

1. `Recovery`
2. `Drain` 改为私有 `DrainPermit`
3. `PendingOffer` 交给 parked waiter
4. `Sealed` 改为 durable projection 查询
5. `InFlight` 改为单 Task ownership
6. 最终删除 `BloggerRuntimeState` 状态 DU 及 transition module

目标入口：

```fsharp
onMainMaterial : Ports -> ProviderSemanticProjection -> Task<DecisionEffect>
```

### 5. `RecoveryTrace`

仅保留为测试侧日志；生产正确性由 `FamilyRecoveryPermit` 不可伪造参数保证。

### 6. `TurnOutcome`

选定 `Domain/ReconcileProgram.TurnOutcome` 为 canonical type，删除 `Application/Reconciliation/ReconciledTurn.fs` 的重复定义与 `domainOutcome` 转换。

### 7. `Role` / `AgentRole`

选定 `Kernel.Role` 为唯一类型，删除 `Session/AgentRoleIdentity.fs` 的重复 DU。短期可用 `type AgentRole = Role` 兼容。

### 8. `AgentFact`

阶段 A：保留 wire 扁平事实，新增 `PromptFact` / `ReviewFact` / `ExecutionFact` / `OrchestratorFact` / `CompanionFact` 内部 family view，通过 `tryOfAgentFact` 转换。

阶段 B：schema version 升级后写入嵌套 family fact；codec 支持旧→新、新→新。

### 9. `dsl-ownership.mjs`

增强：

- 将 `src/Wanxiangshu/Process/` 纳入扫描。
- mutable 豁免精确到声明或函数，需固定注解。
- 新增组合状态检测：`State + Pending/Offer + Recovery/Repair + Drain/Reactivated + Stage/Phase + Next`。
- 新增多布尔循环检测：`let mutable a = false; let mutable b = false; while ...`。
- 新增大 DU 分类审查：超过约定 case 数时要求标注 `Vocabulary | DurableFact | Evidence | Decision | ExternalSignal | ControlState`。
- 新增重复 case 集检测。
- 新增改名 canary：`InFlight`、`Parked`、`Sealed`、`Armed` 等必须触发门禁。

## 迁移顺序

```text
PR 2 NodeProcessWait
PR 3 TurnOutcome + Role
PR 4 RecoveryTrace 删除解释器
PR 5 BloggerToolRecovery 删除隐藏状态
PR 6 Companion slotArmed → 一次性 CE
PR 7 BloggerRuntimeCell 状态乘积拆除
PR 8 AgentFact 分治
PR 9 dsl-ownership 升级
```

每次 PR 保持测试绿，不得一次重写多个生命周期。
