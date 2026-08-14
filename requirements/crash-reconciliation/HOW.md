# crash-reconciliation — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Domain/SessionRecovery.fs` | recovery 纯代数：RecoveryNode/RecoveryClosure/validateClosurePure、SessionRecovery.combine、authorizeFamilyResume、FamilyRecoveryPermit（私有构造 + missingFrom） | CRASH-005/010/011/013/014 |
| `src/Wanxiangshu/Application/Reconciliation/SessionRecoveryWorkflow.fs` | family 恢复编排：SessionRecoveryPorts（全强制）、recoverFamilyDirect（child-first recoverNodes）、authorize | CRASH-002/005/006/010/011 |
| `src/Wanxiangshu/Domain/ChildRecovery.fs` | child 恢复纯决策：DurableHandleEvidence / ChildSnapshotEvidence / HostObservation → resolveChild；JoinableCompletion（fromDecoded / tryFromProvenTerminal）；JoinRecoveryTrace | CRASH-005/009/010/011/012 |
| `src/Wanxiangshu/Application/Reconciliation/ChildRecoveryWorkflow.fs` | resolveAndCommit：读 durable + snapshot → resolve → recordCompletion/recordAbandon → Pulse | CRASH-002/009/012 |
| `src/Wanxiangshu/Session/HandleController.fs` | completion 单一 owner（recordCompletion/recordAbandon/retire/consume） | CRASH-009/012 |
| `src/Wanxiangshu/Application/Reconciliation/ReconcilePass.fs` / `Reconciler.fs` / `ReconciledTurn.fs` | snapshot 观测 → wake evidence → publish；TurnUnknown 私有观测 | CRASH-003/007/008 |
| `src/Wanxiangshu/Application/Reconciliation/BloggerCrashRecovery.fs` / `BloggerRecoveryProbe.fs` | Blogger 崩溃窗口分类与恢复探针 | CRASH-002/016 |
| `src/Wanxiangshu/Application/Reconciliation/PromptRecovery.fs` | Prompt claim 恢复（Proven / StillPending / GaveUp） | CRASH-005 |
| `src/Wanxiangshu/Session/HostForkRestart.fs` / `HostForkRunLifecycle.fs` / `ForkRecovery.fs` | restart 恢复 walk：restoreLinkedChildren、HostForkRestart 的证明结构（p0-recovery-join 正向模式） | CRASH-002/009/012 |
| `src/Wanxiangshu/Journal/RecoveryClosureProjection.fs` | 从 durable 关联发现 closure（child-first 序） | CRASH-002/014 |
| `src/Wanxiangshu/Session/FamilyRecoveryCoordinator.fs` | 物理 single-flight runOnce（Session 层，非 Application） | CRASH-006 |
| `src/Wanxiangshu/Session/CompletionMailbox.fs` / `JoinDrain.fs` / `Application/Reconciliation/Join.fs` | agent Pulse vs PTY Publish 双通道；join 消费 v2 terminal | CRASH-011/012 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs` / `PluginRecoveryScope.fs` | RequireFamilyRecovery 端口接线 | CRASH-006 |

## 一次 family 恢复的主路径（代码时序）

```text
plugin start / session idle 触发
→ RecoveryClosureProjection.discover(parentSession, projections, sequence)   // durable 关联
→ validateClosurePure（重复 session → RecoveryCycle block）
→ recoverNodes（child-first，按节点类型跑各端口）：
     RecoverPromptClaims（PromptRecovery）
     RecoverBlogger（BloggerCrashRecovery）
     RestoreHandles（HandleFamilyRecovery）
     RecoverJobs（JobFamilyRecovery）
   → 每端口 SessionRecovery → combine（Blocked > Waiting > Recovered）
→ authorizeFamilyResume：
     any Blocked → FamilyBlocked
     else any Waiting → FamilyWaiting
     else FamilyReady(FamilyRecoveryPermit(root, sequence, members))
→ 消费方（join / AwaitAgentWithPermit）持 permit 入场；join 边界校验
   missingFrom(current, permit) == []（丢失成员拒绝，增长合法）
```

## child 恢复的 resolve 顺序（纯决策）

```text
durable Abandoned                 → RecoveredAbandoned
durable CompletedAwaitingJoin     → RecoveredTerminal（fromDecoded）
snapshot legal terminal           → RecoveredTerminal（tryFromProvenTerminal）
snapshot Unreadable               → RecoveryIncomplete（等待，不发 permit，非硬 block）
session active                    → RecoveredActive（恢复工作完成，child 继续）
restore in flight / abort-only / unknown → RecoveryIncomplete（不得发 permit）
ParentCancelled / DeadlineExceeded / HostSessionGone → RecoveredAbandoned
conflict / retired                → RecoveryBlocked
```

## 反向覆盖：p0-recovery-join gate 的本包部分

gate `scripts/checks/p0-recovery-join.mjs` 扫生产源码，禁止 reintroduce false finality 与
裸 join。本包侧（recovery 部分）关键正向模式：

```text
HostForkRestart：match! ChildRecoveryWorkflow.resolveAndCommit ports
                → Ok (Joinable proof) → JoinableCompletion.fromDecoded → recordCompletion → Pulse
JoinTool：RequireFamilyRecovery root → FamilyReady permit → joinAvailable（带 permit）
ExecutorTool：requirePermit → Distillation.asDistillationRuntime runtime requirePermit
```

## 历史与弃权

以下事实来自 `archive/docs/why/*`、`archive/docs/how/host.md` 与 gate 考古，均为决策记录，不是现行命题：

- **恢复哲学（ARCH-005 / FLOW-005 / DSL-004）**：恢复重入普通程序，不恢复协程；「执行到第几步」
  不是可恢复对象。曾有一个 `EnsureRecoveryDone: Task<unit>`（collapsed FamilyRecovery → unit）
  的 fail-open 形态，被 gate 禁止——family recovery 必须带 permit 闭合，不能返回 unit。
- **ABORTED 终态化**：EXEC-020 曾把 abort 洗成 agent 终态，恢复/fallback 走错分支；
  clean-break 后 `ChildFinality`/`AgentCompletionOutcome` 无 Aborted，`LegacyFalseAbort` 永不
  RunCompletion。
- **digest 校验 vs closure members（EXEC-023 race）**：permit 一度只带 closure digest；child 在
  join 窗口 fork grandchild 使 digest 变化而恢复未失效（`temporal-ownership-unhappy-path` 的
  `closureDigest mismatch` 失败）。改携 members 集合后，增长合法、仅丢失拒绝。
- **RecoveryStage / AwaitingEvidence**：被 `RecoveryIncomplete | RecoveryBlocked` 取代；
  `AwaitingEvidence` case 被 gate 禁止（EXEC-023）。
- **orchestrator-e2e-timeout（考古）**：`orchestrator-restart-publish` 曾因 companion blogger
  flights per-plugin-instance 而挂起（Finality 等 journal-work-log）。根因修复属于
  `change-integration`/`verification-system`；本包吸收的教训是：restart canary 证明「恢复后从
  Journal 事实重入普通程序」，而 blogger 恢复机会（HostTurnObserver 观察）是恢复路径的入口之一。
- **Reconciler 事件驱动（reconciler-event-driven-de-polling）**：未裁决候选，归
  `causal-wait`/`host-boundary`；本包只要求 reconcile 是单 flight、有界因果重读、wake evidence
  类型化。

## GARBAGE / 弃权裁决

- **恢复的具体编排时机**（startup sweep vs lazy on-demand）：HOW。boundary card
  INDEPENDENT CHANGE 明确「把 startup sweep 改成 lazy on-demand reconciliation 而 durable/
  domain contracts 不变」是本包可独立变化点。
- **各 domain 的恢复规则**（ORCH-007、magic-todo settle、managed-session replacement、
  publish reconcile）：归各 domain owner，本包只引用为本地应用示例。
- **recoveryAction 的领域语义**（`requirements/change-integration/tests/job.test.mjs`）：归
  `change-integration`；本包 REUSE 其「从最后事实决定唯一动作」作为 CRASH-002 的域内实例。
