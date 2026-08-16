# crash-reconciliation — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Domain/SessionRecovery.fs` | recovery 纯代数：RecoveryNode/RecoveryClosure/validateClosurePure、SessionRecovery.combine、authorizeFamilyResume、FamilyRecoveryPermit（私有构造 + missingFrom） | CRASH-005/010/011/013/014 |
| `src/Wanxiangshu/Execution/Session/SessionRecoveryWorkflow.fs` | family 恢复编排：SessionRecoveryPorts（全强制）、recoverFamilyDirect（child-first recoverNodes）、authorize | CRASH-002/005/006/010/011 |
| `src/Wanxiangshu/Domain/ChildRecovery.fs` | child 恢复纯决策：DurableHandleEvidence / ChildSnapshotEvidence / HostObservation → resolveChild；JoinableCompletion（fromDecoded / tryFromProvenTerminal）；JoinRecoveryTrace | CRASH-005/009/010/011/012 |
| `src/Wanxiangshu/Execution/Delegation/ChildRecoveryWorkflow.fs` | resolveAndCommit：读 durable + snapshot → resolve → recordCompletion/recordAbandon → Pulse | CRASH-002/009/012 |
| `src/Wanxiangshu/Session/HandleController.fs` | completion 单一 owner（recordCompletion/recordAbandon/retire/consume） | CRASH-009/012 |
| `src/Wanxiangshu/Composition/Turn/ReconcilePass.fs` / `Reconciler.fs` / `ReconciledTurn.fs` | snapshot 观测 → wake evidence → publish；TurnUnknown 私有观测 | CRASH-003/007/008 |
| `src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs` / `BloggerRecoveryProbe.fs` | Blogger 崩溃窗口分类与恢复探针 | CRASH-002/016 |
| `src/Wanxiangshu/Interaction/Dispatch/Recovery.fs` | detached Prompt claim 物理证据核对（Proven / StillPending / Unreadable） | CRASH-005；普通 lifecycle 不接线 |
| `src/Wanxiangshu/Session/HostForkRestart.fs` / `HostForkRunLifecycle.fs` / `ForkRecovery.fs` | restart 恢复 walk：restoreLinkedChildren、HostForkRestart 的证明结构（p0-recovery-join 正向模式） | CRASH-002/009/012 |
| `src/Wanxiangshu/Execution/Session/RecoveryClosureProjection.fs` | 从 durable 关联发现 closure（child-first 序） | CRASH-002/014 |
| `src/Wanxiangshu/Session/FamilyRecoveryCoordinator.fs` | 物理 single-flight runOnce（Session 层，非 Application） | CRASH-006 |
| `src/Wanxiangshu/Session/CompletionMailbox.fs` / `JoinDrain.fs` / `Execution/Delegation/Join.fs` | agent Pulse vs PTY Publish 双通道；join 消费 v2 terminal | CRASH-011/012 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs` / `PluginRecoveryScope.fs` | RequireFamilyRecovery 端口接线 | CRASH-006 |

## 当前进程内 family 校验路径

```text
当前进程创建的 family 在 join / await 前
→ RecoveryClosureProjection.discover(parentSession, projections, sequence)
→ validateClosurePure（重复 session → RecoveryCycle block）
→ 对当前进程仍可观察的 child 做证据校验
→ authorizeFamilyResume → permit → join
```

该路径不是跨进程 tool recovery。进程重启后，不自动 restore 上一进程的 tool/family，不扫描旧未完成 handle 去补完成。未来若要恢复旧 session，只能由显式 `/continue` 建立新的、可见的 resume workflow；旧坏 tool 仍保留在 transcript。

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

以下事实来自历史五层 docs（why/*、how/host）与 gate 考古，均为决策记录，不是现行命题：

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

- **startup sweep / lazy tool recovery**：均已裁决为非法。CRASH-017 不允许把自动恢复从 startup 挪到普通 tool/hook；旧 tool crash 保持失败。
- **显式 `/continue`（CRASH-018）**：config 注册 command；`command.execute.before` 只在 command=`continue` 时读取 parent 的 durable handles，逐 child 用 `ISessionSnapshotPort.GetMessages` 判 physical 可访问；可访问 child 只调用 process-local adopt（Restore + BindChildSession + parent map），不 append fact、不 send prompt。hook 把 restart/broken-tool disclosure + surviving/unavailable child 清单作为带 `wanxiangshu_explicit_resume=true` metadata 的 visible text part 交给 LLM。messages transform 只检查 trailing user material 上这个 marker；不维护 SessionId suppression latch，因此同 material retry 仍 disclosure-only，而下一条普通 user material 不依赖 idle/abort/delete 就恢复正常。真正 reopen handle/发送 charge 留给后续普通 fork reuse，因此 resume discovery 与业务 effect 分离。
- **各 domain 的恢复规则**（ORCH-007、magic-todo settle、managed-session replacement、publish reconcile）：归各 domain owner，本包只引用为本地应用示例。Attached replacement 的共享恢复纪律是：proven old physical loss 后 create fresh，再由 domain 的 `Close(old)` / `Link(new)` 显式迁移 durable association；不得把 Link 当覆盖赋值。Companion 的动态证明见 managed-session-lifecycle `satellite-runtime.test.mjs`。
- **recoveryAction 的领域语义**（`requirements/change-integration/tests/job.test.mjs`）：归
  `change-integration`；本包 REUSE 其「从最后事实决定唯一动作」作为 CRASH-002 的域内实例。
