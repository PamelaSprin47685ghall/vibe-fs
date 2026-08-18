# crash-reconciliation — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Execution/Session/Recovery/Model.fs` | recovery 纯代数：RecoveryNode/RecoveryClosure/validateClosurePure、SessionRecovery.combine、authorizeFamilyResume、FamilyRecoveryPermit（私有构造 + missingFrom） | CRASH-005/010/011/013/014 |
| `src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs` | family 恢复编排：SessionRecoveryPorts（全强制）、recoverFamilyDirect（child-first recoverNodes）、authorize | CRASH-002/005/006/010/011 |
| `src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs` | child 恢复纯决策：DurableHandleEvidence / ChildSnapshotEvidence / HostObservation → resolveChild；JoinableCompletion（fromDecoded / tryFromProvenTerminal）；JoinRecoveryTrace | CRASH-005/009/010/011/012 |
| `src/Wanxiangshu/Execution/Delegation/ChildRecoveryWorkflow.fs` | resolveAndCommit：读 durable + snapshot → resolve → recordCompletion/recordAbandon → Pulse | CRASH-002/009/012 |
| `src/Wanxiangshu/Execution/Delegation/Handle/Controller.fs` | completion 单一 owner（recordCompletion/recordAbandon/retire/consume） | CRASH-009/012 |
| `src/Wanxiangshu/Composition/Turn/ReconcilePass.fs` / `TurnReconcile.fs` | snapshot 观测 → wake evidence → publish；TurnUnknown 私有观测 | CRASH-003/007/008 |
| `src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs` / `BloggerCrashSurface.fs` | Blogger 崩溃窗口分类与恢复探针 | CRASH-002/016 |
| `src/Wanxiangshu/Interaction/Dispatch/Recovery.fs` | detached Prompt claim 物理证据核对（Proven / StillPending / Unreadable） | CRASH-005；普通 lifecycle 不接线 |
| `src/Wanxiangshu/Execution/Delegation/Fork/Host/Restart.fs` / `Host/RunLifecycle.fs` / `Fork/Recovery.fs` | restart 恢复 walk：restoreLinkedChildren、HostForkRestart 的证明结构（p0-recovery-join 正向模式） | CRASH-002/009/012 |
| `src/Wanxiangshu/Execution/Session/RecoveryClosureProjection.fs` | 从 durable 关联发现 closure（child-first 序） | CRASH-002/014 |
| `src/Wanxiangshu/Execution/Session/Recovery/Coordinator.fs` | 物理 single-flight runOnce（Session 层，非 Application） | CRASH-006 |
| `src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs` / `Execution/Delegation/Handle/JoinDrain.fs` / `Execution/Delegation/Join.fs` | agent Pulse vs PTY Publish 双通道；join 消费 v2 terminal | CRASH-011/012 |
| `src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs` / `PluginRecoveryScope.fs` | RequireFamilyRecovery 端口接线 | CRASH-006 |

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
- **显式 `/continue`（CRASH-018）**：config 注册 command；`command.execute.before` 只在 command=`continue` 时读取 parent 的 durable handles，逐 child 用 `ISessionSnapshotPort.GetMessages` 判 physical 可访问；可访问 child 只调用 process-local adopt（Restore + BindChildSession + parent map），不 append fact、不 send prompt。动态 restart/broken-tool disclosure + surviving/unavailable child 清单先进入 process-local one-shot pending handoff，同时仍写 command hook output 兼容会转发 parts 的 Host。真实 `chat.message` 到达后，若当前 Host user material 尚无 marker，则从 pending handoff materialize 一份带 `wanxiangshu_explicit_resume=true` metadata 的 visible text part；若 Host 已转发 marker，只消费 pending、不重复插入。此刻才获得 exact `(SessionId, PhysicalUserMessageId)` 并登记 suppression。静态 command template 只要求读取同一 user material 中的 briefing，绝不声称存在一个不可见 attachment。

  exact resume material 的 `chat.message` 只做 physical admission / reconcile binding，不进入 PromptIngress、AuthorityRoot、PromptKey continuation 或 managed business routing。provider messages transform 先检查 trailing user material marker；若 Host projection 丢失自定义 part metadata，则用 trailing `PhysicalUserMessageId` 查询 `chat.message` 已登记的 exact suppression binding。两条路径任一命中都只做 Host wire sanitization，跳过 Manager narrative、Companion/XTrace、Strength、Pair、Blogger 等普通业务 transform。该 binding 跟随 exact physical material，因此同一 `/continue` 中 tool result 后的下一次 provider step 仍走 disclosure-only；下一条新 ordinary PhysicalUserMessageId 才自然恢复普通路径。`HostTurnObserver` 对同一 exact material 继续 suppress idle/reconcile 自动 effect。真正 reopen handle/发送 charge 只来自 LLM 看见 briefing 后显式选择的正常 tool call，因此 resume discovery 与业务 effect 分离。
- **各 domain 的恢复规则**（ORCH-007、magic-todo settle、managed-session replacement、publish reconcile）：归各 domain owner，本包只引用为本地应用示例。Attached replacement 的共享恢复纪律是：proven old physical loss 后 create fresh，再由 domain 的 `Close(old)` / `Link(new)` 显式迁移 durable association；不得把 Link 当覆盖赋值。Companion 的动态证明见 managed-session-lifecycle `satellite-runtime.test.mjs`。
- **ORCH-007 的领域语义**（`requirements/change-integration/tests/job.test.mjs`）：归
  `change-integration`；本包 REUSE 其「从最后事实决定唯一动作」作为 CRASH-002 的域内实例。

## 依赖

DEPENDS ON：`durable-events`（恢复输入是已提交事实）、`effect-accounting`（unknown 不重放）、
`structured-workflow`（恢复重入普通程序形态）、`host-boundary`（snapshot 是可信物理观察）。
理由：CRASH-002/003/004/006 分别消费这四个包的 guarantee。

## 验证与测试落点

运行命令：单文件 `node --test requirements/crash-reconciliation/tests/<file>.test.mjs`；整包被
`node requirements/verification-system/tests/run.mjs` 自动发现。落点类型：MOVE = 从旧 `tests/unit` 物理移入本包；REUSE =
留在原处（多 owner 或共享 checker），记锚点与 cutover 拆分；NEW = 本包新写。跨包引用统一写
`requirements/<pkg>/tests/<file>`。

> CRASH-002..016 中保留的 `ChildRecovery` / `SessionRecoveryWorkflow` / `HostForkRestart` 测试现在证明的是**可显式调用的恢复算法库**，不表示它们仍接在 plugin startup、普通 turn/tool 或 teardown 上。生产普通生命周期的禁接线由 CRASH-017 证明；用户可见的唯一 restart resume 入口由 CRASH-018 `/continue` 证明。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| CRASH-001 process-local 状态不是恢复权威 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs`：`LOOP_001_kill_arm_is_process_local_not_persisted`、`LOOP_006_reset_detector_preserves_loop_kill_armed`；`requirements/context-compression/tests/recovery-slot.test.mjs`：`FALLBACK_012_arming_is_lost_across_a_restart_and_the_safe_side_is_unarmed`；`requirements/crash-reconciliation/tests/quiescence-surface.test.mjs`：`Q07_restart_gate_holds_no_permit`、`Q08_restart_or_unknown_idle_cannot_mint_new_send_authority`（没有本进程 BeginProviderAttempt 的历史 idle 不得造 permit） | MOVE（跨包）+ REUSE | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs`；`node --test requirements/crash-reconciliation/tests/quiescence-surface.test.mjs` |
| CRASH-002 从 durable + 物理观察重建世界 | `tests/child-recovery-workflow.test.mjs`：`VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses`；`tests/join-recovery-crash-matrix.test.mjs`：`P0_RECOVERY_JOIN_001_crash_after_completed_before_consume_is_awaiting_join`；`tests/session-recovery-extra.test.mjs`：`MISC_recovery_receipt_accessors_and_nonempty_helpers`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_positive_session_ports_shapes_present` | MOVE | 各文件 `node --test` |
| CRASH-002（Attached restart 域内实例） | `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs`：`HFR_restart_empty_journal_yields_no_linked_handles`、`HFR_restart_completed_terminal_re_enlists_child_into_runtime`、`HFR_restart_active_with_terminal_snapshot_recovered_terminal` | REUSE | `node --test requirements/crash-reconciliation/tests/host-fork-restart.test.mjs`（SPLIT@cutover：handle 生命周期归 managed-session-lifecycle） |
| CRASH-002（ORCH-007 域内实例） | `requirements/change-integration/tests/job.test.mjs`：`ORCH-007 exactly one recovery action per job` 组 | REUSE | `node --test requirements/change-integration/tests/job.test.mjs`（owner：change-integration） |
| CRASH-003 未决 effect 先 reconcile | `requirements/structured-workflow/tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_003: decideStep bounds causal rereads and stops on exhaustion`、`RECONCILE_PROGRAM_004: publishDecision gates already-published terminal and provisional`；`requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs`：`unknown_effect_without_quiescence_is_not_replayed`、`reconcile_decision_has_no_business_repair_vocabulary` | REUSE + NEW | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs`；`node --test requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs`（owner：structured-workflow；本包消费 observation vocabulary） |
| CRASH-004 恢复重入普通程序、无程序计数器 | `requirements/structured-workflow/tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_006: Domain surface has no Command/Reply/Trace AST exports`；`tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_dsl_module_and_private_permit_exist`（无 fromTask/Flow.lift）；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_negative_ensure-recovery-unit_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_host-fork-runtime-recovery-task_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_host-fork-runtime-await-recovery-call_goes_red` | REUSE + MOVE | 同上；`node --test requirements/crash-reconciliation/tests/session-recovery-family.test.mjs` |
| CRASH-005 证据不足 fail closed | `tests/child-recovery-workflow.test.mjs`：`VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable`、`VERIFY_008_child_recovery_workflow_blocks_retired_handle`、`VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank`；`tests/join-aborted-race.test.mjs`：`P0_RECOVERY_JOIN_001_case_C_parent_cancelled_is_abandon`、`P0_RECOVERY_JOIN_001_case_C_parent_cancelled_after_aborts_still_abandon`、`P0_RECOVERY_JOIN_001_case_D_unreadable_snapshot_is_incomplete_not_blocked`；`tests/host-fork-restart.test.mjs`：`HFR_restart_active_with_unreadable_snapshot_waits_for_terminal_evidence`；`tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_authorize_blocks_on_child_block`、`RECOVERY_FAMILY_authorize_waiting_is_family_waiting_not_blocked`、`RECOVERY_FAMILY_handle_family_waiting_maps_to_waiting_not_blocked`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_negative_executor-tool-empty-session-fail-closed_goes_red` | MOVE | 各文件 `node --test` |
| CRASH-006 无 fresh evidence 无自动 effect | `tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_ready_before_business_is_type_enforced`、`RECOVERY_FAMILY_authorize_ready_issues_private_permit`、`RECOVERY_FAMILY_constructor_does_not_start_fork_restore`；`requirements/crash-reconciliation/tests/quiescence-surface.test.mjs`：`Q01_normal_stable_idle_yields_one_consumable_permit`、`Q02_new_provider_attempt_invalidates_the_old_permit`、`Q03_repeated_idle_does_not_repeat_send`、`Q04_new_attempt_own_idle_can_send_again`、`Q05_new_physical_user_material_revokes_the_previous_idle_before_transform`（physical admission 在 transform 前关闭旧 idle window；同一 physical replay 幂等）、`Q10_session_deleted_drops_every_permit`、`P4_SURFACE_exports_exact_capability_names`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_join_tool_missing_recovery_goes_red`、`P0_RECOVERY_JOIN_GATE_join_tool_with_dsl_stays_green_for_positive`、`P0_RECOVERY_JOIN_GATE_negative_missing-ports-family-ready_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_join-tool-no-bare-runtime-join_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_tools-no-bare-runtime-join_executor-tool_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_tools-no-bare-runtime-join_distillation-runtime_goes_red` | MOVE + REUSE | 各文件 `node --test` |
| CRASH-007 TurnUnknown 是私有观测 | `requirements/structured-workflow/tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_005: TurnUnknown never crosses the stable business-turn boundary`、`RECONCILE_PROGRAM_007: TurnUnknown is SnapshotObservation, not TurnOutcome`；`requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs`：`turn_unknown_is_snapshot_observation_not_turn_outcome`、`publish_boundary_carries_turn_outcome_not_snapshot_observation` | REUSE + NEW | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs`；`node --test requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs` |
| CRASH-008 abort 是 typed 控制面 | `requirements/host-boundary/tests/signals.test.mjs`：`R3_abort_error_adapts_to_attempt_aborted_not_dropped`；`requirements/crash-reconciliation/tests/quiescence-surface.test.mjs`：`ESC_P0_2_operator_abort_revokes_unconsumed_idle_permit`、`ESC_P0_3_aborted_attempt_cannot_be_reminted_by_delayed_idle` | REUSE | `node --test requirements/host-boundary/tests/signals.test.mjs`；`node --test requirements/crash-reconciliation/tests/quiescence-surface.test.mjs` |
| CRASH-009 child recovery 无 Aborted 终态 | `tests/join-aborted-race.test.mjs`：`P0_RECOVERY_JOIN_001_case_A_aborted_path_leaves_handle_active`、`P0_RECOVERY_JOIN_001_case_A_tryFromProvenTerminal_then_joinable_once`、`P0_RECOVERY_JOIN_001_case_B_aborted_then_terminal_snapshot_is_joinable`、`P0_RECOVERY_JOIN_001_case_E_aborted_times_n_projection_unchanged`；`tests/join-recovery-crash-matrix.test.mjs`：`P0_RECOVERY_JOIN_001_crash_after_aborted_observed_stays_active`、`P0_RECOVERY_JOIN_001_crash_matrix_no_aborted_durable_fact`；`tests/join-clean-break-recovery.test.mjs`：`P0_CLEAN_BREAK_delayed_recovery_before_ready_no_aborted_join_then_true_terminal`；`tests/join-recovery-trace.test.mjs`：全部 `P0_RECOVERY_JOIN_001_trace_*`（8 条）；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_exports_recovery_rule_ids`、`P0_RECOVERY_JOIN_GATE_negative_lifecycle-aborted-setresult_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_fork-recovery-synthetic-restored_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_fork-recovery-synthetic-restored_paren-form_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_fork-recovery-interrupted-finality_goes_red`、`P0_RECOVERY_JOIN_GATE_positive_child_recovery_shapes_present`（joinable-from-decoded / child-recovery-result-five-cases） | MOVE + REUSE | 各文件 `node --test`（gate 测试 SPLIT@cutover：effect-accounting 的 aborted≠terminal 规则不归本包） |
| CRASH-010 结果分支穷尽，Waiting ≠ Blocked | `tests/child-recovery-workflow.test.mjs`：全部 9 条（RecoveredTerminal / RecoveredActive / Incomplete / Blocked / blank-body，含拆分的 `_is_incomplete_not_blocked`、`_is_blocked_branch`、`_is_incomplete_branch`）；`tests/join-clean-break-recovery.test.mjs`：`P0_CLEAN_BREAK_aborted_only_observation_is_incomplete_not_blocked`；`tests/session-recovery-extra.test.mjs`：`MISC_recovery_of_handle_family_all_branches`、`MISC_recovery_of_job_family_all_branches`；`tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_handle_family_types_and_permit_rules`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_negative_awaiting-evidence-case_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_restore-handles-none-no-recovery_goes_red`、`P0_RECOVERY_JOIN_GATE_negative_recover-job-none-no-recovery_goes_red` | MOVE | `node --test requirements/crash-reconciliation/tests/child-recovery-workflow.test.mjs`；`node --test requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs` |
| CRASH-011 线性序 permit → join，permit 携带 members | `tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_authorize_ready_issues_private_permit`；`tests/recovery-closure-permit.test.mjs`：`CRASH_CLOSURE_permit_refuses_loss_and_admits_growth`；`tests/host-fork-runtime-permit.test.mjs`：`HFRT_join_with_permit_root_mismatch_is_not_found`、`HFRT_join_with_permit_stale_journal_sequence_is_not_found`、`EXEC_023_permit_whose_recovered_member_is_gone_is_not_found`、`EXEC_023_permit_survives_family_growth_after_recovery_closed`、`HFRT_join_with_valid_permit_passes_validation`、`HFRT_await_agent_with_permit_validation_error_maps_to_not_found`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_join_tool_bare_runtime_join_goes_red`、`P0_RECOVERY_JOIN_GATE_bare_join_allowlist_host_fork_stays_green`、`P0_RECOVERY_JOIN_GATE_executor_permit_path_stays_green`、`P0_RECOVERY_JOIN_GATE_positive_join_program_requires_permit_shape_present` | MOVE + NEW + REUSE | 各文件 `node --test` |
| CRASH-012 completion 单一 owner | `tests/child-recovery-workflow.test.mjs`：`VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner`；`tests/join-aborted-race.test.mjs`：`P0_RECOVERY_JOIN_001_case_A_proven_terminal_completes_once_then_retire_once`；`tests/join-recovery-crash-matrix.test.mjs`：`P0_RECOVERY_JOIN_001_crash_after_retired_is_idempotent`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_host_fork_restart_missing_proof_goes_red`、`P0_RECOVERY_JOIN_GATE_host_fork_restart_with_terminal_structure_stays_green`、`P0_RECOVERY_JOIN_GATE_production_sources_are_green`、`P0_RECOVERY_JOIN_GATE_positive_mailbox_pulse_shape_present` | MOVE + REUSE | 各文件 `node --test` |
| CRASH-013 combine 优先级 | `tests/session-recovery-combine.test.mjs`：`RECOVERY_COMBINE_blocked_dominates`、`RECOVERY_COMBINE_waiting_dominates_ready`、`RECOVERY_COMBINE_recovered_over_ready`、`RECOVERY_COMBINE_empty_is_no_recovery_required`、`RECOVERY_COMBINE_order_independent_for_tier`；`tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_combine_and_coordinator_ownership_moved`；`tests/session-recovery-extra.test.mjs`：`MISC_recovery_authorize_aggregates_blocks_waits_ready` | MOVE | `node --test requirements/crash-reconciliation/tests/session-recovery-combine.test.mjs`；`node --test requirements/crash-reconciliation/tests/session-recovery-family.test.mjs` |
| CRASH-014 closure 校验与 permit 单调 | `tests/recovery-closure-permit.test.mjs`：`CRASH_CLOSURE_validate_accepts_unique_sessions_and_keeps_order`、`CRASH_CLOSURE_duplicate_session_is_a_cycle_block`、`CRASH_CLOSURE_member_tokens_are_stable_identities`、`CRASH_CLOSURE_members_set_matches_tokens`；`tests/session-recovery-extra.test.mjs`：`MISC_recovery_validate_closure_pure` | NEW + MOVE | `node --test requirements/crash-reconciliation/tests/recovery-closure-permit.test.mjs`；`node --test requirements/crash-reconciliation/tests/session-recovery-extra.test.mjs` |
| CRASH-015 Attached restore 复用/替换/fail-closed | `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs`：`HFR_restart_multiple_children_recovered_in_link_order`、`HFR_restart_legacy_false_abort_waits_with_rejection_fact`、`HFR_restart_retired_legacy_false_abort_migrates_replacement_once`、`HFR_restart_invalid_completion_blob_waits`；`requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs`：`HOST_015_companion_satellite_recovery_closes_old_durable_link_before_linking_replacement`、`HOST_015_companion_replacement_transitions_real_durable_link_without_semantic_cut`（Companion replacement 必须 Close→Link，不得 direct repoint）；`requirements/session-ontology/tests/session-flattening.test.mjs`：`HOST_015_family_root_resolves_through_restored_journal_parents` | REUSE | `node --test requirements/crash-reconciliation/tests/host-fork-restart.test.mjs requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs requirements/session-ontology/tests/session-flattening.test.mjs` |
| CRASH-016 Blogger 崩溃窗口 | `requirements/crash-reconciliation/tests/blogger-crash-recovery.test.mjs`：`C5_crash_recovery_module_exists_with_window_outcomes`、`C5_classify_open_request_window_A_unsent`、`C5_classify_open_request_window_B_inflight`、`C5_classify_open_request_window_C_tool_present`、`C5_snapshot_tool_evidence_uses_latest_assistant_and_exactly_one_chronicle`、`C5_crash_recovery_reads_HasFlight_not_cell_State`、`C5_window_D_never_forces_parked_without_a_waiter` | REUSE | `node --test requirements/crash-reconciliation/tests/blogger-crash-recovery.test.mjs`（SPLIT@cutover：ENFORCER-153 锚点归 behavior-diagnosis） |
| CRASH-017 broken tool stays broken / no implicit ownership | `requirements/host-boundary/tests/plugin-load-purity.test.mjs`（init graph 无 recovery / Host re-entry；ordinary join 只认 current-process ownership）；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs`（pending transaction 不 undo）；`tests/explicit-continue.test.mjs`：`CRASH_017_new_process_runtime_dispose_does_not_claim_or_abort_old_active_handle`；`tests/blogger-crash-recovery.test.mjs`：`C5_crash_recovery_library_is_not_wired_into_ordinary_plugin_lifecycle`；`tests/session-recovery-family.test.mjs`：`RECOVERY_FAMILY_library_is_detached_from_ordinary_plugin_and_join_uses_current_process_permit`；`requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：`P0_RECOVERY_JOIN_GATE_negative_spike-restore-handles-none_goes_red` | NEW + REUSE | `node --test requirements/host-boundary/tests/plugin-load-purity.test.mjs requirements/repository-programming/tests/js-tools-transaction-store.test.mjs requirements/crash-reconciliation/tests/explicit-continue.test.mjs` |
| CRASH-018 explicit `/continue` | `tests/explicit-continue.test.mjs`：`CRASH_018_continue_registers_a_visible_command`、`CRASH_018_non_continue_command_is_a_noop`、`CRASH_018_continue_discloses_restart_without_minting_completion`、`CRASH_018_missing_session_is_visible_and_does_not_resume`、`CRASH_018_resume_briefing_keeps_unverified_children_visible`、`CRASH_018_real_command_material_materializes_briefing_and_stays_disclosure_only`、`CRASH_018_transform_uses_exact_physical_binding_when_host_drops_part_metadata`、`CRASH_018_abandoned_command_handoff_cannot_mark_a_later_ordinary_material`（真实 plugin hooks：command output 不由测试手工转发；Host physical message 只有 command template → `chat.message` 生产 materialize dynamic briefing；messages transform 在 marker 存在或 exact physical binding 命中时都保持 disclosure-only，不能因 Host 丢 custom metadata 进入普通 Companion/Strength 路径；idle 不发 repair；被放弃的 command handoff 不得污染下一条 ordinary material） | NEW | `node --test requirements/crash-reconciliation/tests/explicit-continue.test.mjs` |
| CRASH-001..012 静态 gate（p0-recovery-join 本包部分） | `requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：全部 `P0_RECOVERY_JOIN_GATE_*`（recovery 规则 negative/positive + production green；每 test 已按命题标 `WHAT[CRASH-*]`，见上文各行） | REUSE | `node --test requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`（SPLIT@cutover：effect-accounting 侧规则拆出） |

### 包拥有的 semantic anchor id

`scripts/checks/semantic-anchors.mjs` 无本包语义 ID（该 catalog 只装 Role Law / office / tool
cognition anchors）；本包为空清单。p0-recovery-join 的 recovery 规则 id 清单见 WHAT.md 反向覆盖节。

### GAP

- `GAP-017` —— **CLOSED**：CRASH-018 已把 command→physical material 的 seam 变成 production-owned one-shot handoff；真实 `chat.message` 只在 witness 对得上的 `/continue` material 上 materialize dynamic briefing，Host 已转发 marker 时不重复，被放弃的 handoff 在下一条 ordinary material 上 fail closed。exact material 的 messages transform 以 trailing marker 或 `chat.message` 登记的 exact physical binding 识别 disclosure-only，不依赖 Host 保留 custom metadata；tool-result 后续 provider step 同样只做 wire sanitization。证据由 `tests/explicit-continue.test.mjs` 持续覆盖。

### cutover 待办（SPLIT@cutover）

1. `requirements/crash-reconciliation/tests/p0-recovery-join-gate.test.mjs`：按规则 id 拆分——本包取 recovery 侧
   （`host-fork-restart-proof-structure`、`record-completion-single-owner`、
   `session-ports-*`、`child-recovery-result-five-cases`、`joinable-from-decoded`、
   `join-with-permit-closure-digest`、`join-tool-family-*`、`executor-tool-require-permit`、
   `distillation-*-join-with-permit`、`mailbox-pulse-agent-handle`、
   `false-completion-rejected-fact`、`parent-join-correction-fact`、`fork-recovery-*`、
   `ensure-recovery-unit`、`missing-ports-family-ready`、`lifecycle-aborted-record/setresult`、
   `awaiting-evidence-case` 等）；`agent-aborted-*` 等 aborted≠terminal 规则归
   `effect-accounting`。
2. `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`：effect-accounting owner
   （aborted≠terminal），本包只交叉引用。
3. `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs`、`requirements/managed-session-lifecycle/tests/host-fork-runtime.test.mjs`：
   handle 生命周期锚点归 `managed-session-lifecycle`；恢复锚点归本包，cutover 时按断言拆分。
4. `requirements/crash-reconciliation/tests/blogger-crash-recovery.test.mjs`：C5 恢复锚点迁本包，ENFORCER-153 锚点留
   `behavior-diagnosis`。
5. `requirements/change-integration/tests/job.test.mjs`：ORCH-007 归 `change-integration`，本包只 REUSE。
6. e2e/integration（`tests/e2e/cases/temporal-ownership-unhappy-path.test.mjs`、
   `orchestrator-restart-publish`）由 lead 在 cutover 阶段归位。
