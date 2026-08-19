# HOW — managed-session-lifecycle（实现模型与约束；非 normative）

## 实现模型

### Handle 状态机（`src/Wanxiangshu/Execution/Delegation/LinkageProjection.fs`）

```fsharp
type HandleLifecycle =
    | Active
    | CompletedAwaitingJoin of HandleCompletion     // join 可消费；list 显示 CompletedAwaitingJoin
    | Abandoned of HandleAbandonReason              // durable terminal；不可 join
    | Retired                                       // tombstone；不可回退

type HandleRecord =
    { Handle: HandleId            // = agent id（HandleController.agentHandle）
      ChildSessionId: SessionId   // 只由 Host 签发
      TargetAgent: string
      Byname: string
      CanonicalRole: Role
      Ownership: Fact.HandleOwnership               // DurableParentHandle | HostOwnedHidden
      Lifecycle: HandleLifecycle
      CreationOrder: int
      LastCompletion: HandleCompletion option }
```

`HandleProjection`（纯 fold）：`linkNamed`（重链 live handle 重绑不重复）、`complete`（单赋值，
后到者 `AlreadyCompleted`）、`abandon`（单赋值）、`retire`、`rejectFalseCompletion`（仅
CompletedAwaitingJoin 且 ref+digest 精确匹配才回 Active）、视图（`listable` / `joinable` /
`reportableAbandoned` / `activeHandles`，全部经 `parentVisible` 过滤 HostOwnedHidden）、
`tryFindByByname`（Retired 仍可搜，防名字回收）、`linkedChildren`、`lifecycleSealsBlogger`。

`HandleController`（`src/Wanxiangshu/Session/HandleController.fs`）是四个 fact 的唯一 writer：
`linkNamed / recordCompletion / recordAbandon / retire / consume / cancelChildren`。
`consume` 先读投影再 append `HandleRetired`；CommitUnknown 不交 payload。

### runtimes

- **AttachedSessionRuntime**（`Session/AttachedSessionRuntime.fs`）：`Dictionary<(scope, role),
  (childId, agent)>`，`GetOrCreate` 先查后建；`isUsable` 回调把已删 child 视为 absent（安全侧
  重新创建）；`Remove` / `RemoveByDelegateSession` 是唯一解绑。
- **SatelliteRuntime**（`Execution/Session/Attachment/SatelliteRuntime.fs`）：Companion leaf 的 `Ensure(owner, spec)` → `start`：查 root children（+ owner children，兼容扁平前）→ 按 `RestoredSessionId` 精确匹配 → `Reused | Replacement | Created`。Created/Reused 在返回前 `Link`；Replacement 则先创建 fresh child，再 `Close(owner)` 旧 durable association，最后 `Link(owner,new)`，任一步失败只 abort fresh lease。`Retire` → Abort + Close + Invalidate；`Ensure` single-flight（per-kind flight cache），上层 ensure 失败必须 Invalidate 该 failed flight 后才允许下一次重试。
- **HostForkRuntime / ForkRuntime**（`Session/{HostForkRuntime,ForkRuntime}.fs`）：fork child 的
  install → HandleLinked（失败则 abort 新 child）→ SendPrompt（失败则 fail pending run）；
  reuse 不 spawn、沿用已绑 agent；`ForkRuntime` 维护 in-process ChildRun 注册 + 双通道 mailbox。
- **interrupt 权限分型**：`ISessionHostPort.InterruptAttempt` 只允许有 physical parent 的 managed
  sub-session，且只调用 Host physical abort；`AbortSession` 才拥有 detach + descendant cascade。
  `InterruptAttempt` 仅供已经拥有 successor 的 Loop/NeedHelp/Fission/Reviewer/Finality control stop；
  tool/invariant fail-closed 走 `ManagedSessionTermination.terminate`：调用栈内证明 managed child，先 durable
  cancel descendants，再 logical/physical `AbortSession`，随后同步发布 `Failed` terminal。root/user-facing
  通过 `ISessionHostPort.IsManagedChild` fail-closed，任何 child cancel / abort effect 之前即被拒绝；该 predicate
  与 `InterruptAttempt` / `AbortSession` 共享同一 parent proof（live child map、restored parent、
  `SessionExecutionBinding.tryParent`），禁止不同 stop path 各自猜 root。
- **NeedHelp abort→idle handoff**：`TryObserveAssistanceClaim` 只把 exact attempt 翻译成 typed claim，
  不删除 sensor arm；`TurnAborted` 无 fresh idle 时只 claim ownership。`IdleRevisit` 再次取得同一 typed
  claim，`withFreshAssistanceQuiescence` 在 permit 后调用 `TryConsumeAssistanceClaim`，成功才发送 escalation /
  创建 consultation。这样 abort observation 不会提前吃掉唯一 successor capability。
- **abort rollback**：Loop/NeedHelp 遵守 reserve→Host abort→commit；Host abort Error/throw 立即撤销本次
  sensor reserve。fatal/invariant termination 不 reserve 跨 callback cause，而是在当前调用栈内完成终态，
  因而没有 stale reason 可以泄漏到下一 provider attempt。
- **TurnAborted cancel**：`OrdinaryTurnWorkflow.handleAborted` 先调用
  `PluginRuntimeScope.CancelSessionChildren` → `ToolRuntimeScope.CancelSessionChildren` →
  `HostForkRuntime.CancelAndDrain`，使 active durable handles 先写 `HandleAbandoned(ParentCancelled)`；
  再 `AbortChildren` 收束 Companion 等非 fork attachment，最后发布 parent terminal。
- **HostForkRestart**（`Session/HostForkRestart.fs`）：`restoreLinkedChildren` 按 durable handle
  投影 re-enlist；`restoreLinkedChildrenWithoutRuntime` 是 journal-only walk。

### 复用判据（restart，HOST-009/015）

```text
query family root children（owner ≠ root 时并查 owner children）
→ journal 关联（RestoredSessionId）且 id+agent+title 恰 1 匹配 → 复用
→ journal 关联的 id 不存在 → Replacement（新建，物理挂 root；Close old durable link → Link new）
→ 无 journal 关联 → 新建（不收养同 agent/title sibling）
→ id 匹配但 agent/title 冲突 / 多候选 / 查询失败 → fail closed
```

## 历史与弃权

- **ReuseScope 概念升级**（universal.md §11）：dedicated key 从 `(owner SessionId, role)` 升级为
  `(OwnerReuseScopeId, role)`；「owner Session 最终 dispose」≠ ReuseScope 终结；只有 scope 被证明
  关闭才 freeze draft → synthesize → publish → retire/release。
- **同 caller ReuseScope 串行**（universal.md §12）：serialization key = immediate caller
  ReuseScope（非 family root，防 DevOps→Coder→Inspector 死锁）——该不变量实现于
  SyncDelegateRuntime（batch mailbox），归 `delegation` 消费，本包只拥有 binding 生命周期。
- **P0-RECOVERY-JOIN-001**：`recordCompletion` 只接受 `JoinableCompletion`，raw Aborted 不能占
  cell；parent cancel 走 durable `HandleAbandoned(ParentCancelled)` 而非 aborted cell
  （`ForkRuntime` 注释）。
- **Student/Teacher**：G3 clean-break 删除（`universal.md`、`ce-student-teacher-collapse.md`）；
  无 Student/Teacher lifecycle 残留（GARBAGE）。
- **cache.md §17（QuiescenceGate）**：idle-derived continuation 资格归 `causal-wait`（process-local
  permit）；本包只消费其「已消费 completion 才 retire」的投递纪律，不拥有 quiescence 语义。
- **explicit 不写 Host 的 session API**：Host 具体 session 接口（ListChildren/CreateChildSession/
  AbortSession/FamilyRootOf）是 `host-boundary` 提供的物理 port；本包通过 `ISessionHostPort` 消费，
  不拥有其 shape。

## DEPENDS ON

- `session-ontology`（执行类 × 归属分类；本包消费其 existence/ownership 事实）。
- `crash-reconciliation`（generic 恢复协议；本包只定义 session-specific 合法恢复结果）。

## 验证与测试落点

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| MANAGED-SESSION-001 | `tests/attached-session-runtime.test.mjs` `EXEC_026_get_or_create_creates_and_binds_a_work_child_once`（runtime 是绑定唯一 owner）+ `EXEC_026_remove_and_remove_by_delegate_session_are_the_only_unbind_paths` | NEW | `node --test requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` |
| MANAGED-SESSION-002 | `tests/satellite-runtime.test.mjs` `HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child`（link 先于 prompt 的 create 路径）；`tests/host-fork-agent.test.mjs` `HFA_fork_linkage_failure_aborts_the_new_child`（关联未持久则不留孤儿 child） | MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` / `.../host-fork-agent.test.mjs` |
| MANAGED-SESSION-003 | `tests/satellite-runtime.test.mjs` `HOST_015_companion_satellite_recovery_reuses_journal_linked_child_under_flat_root` / `HOST_015_companion_satellite_recovery_closes_old_durable_link_before_linking_replacement` / `HOST_015_companion_replacement_transitions_real_durable_link_without_semantic_cut` / `HOST_015_companion_satellite_recovery_fails_closed_when_journal_linked_child_conflicts` / `HOST_015_companion_satellite_recovery_never_adopts_same_agent_sibling_without_journal_link`；`tests/session-flattening.test.mjs` `HOST_015_abort_children_cascade_stays_keyed_on_family_root`（恢复匹配以 family root 为键；物理扁平断言归 session-ontology） | MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` / `.../session-flattening.test.mjs` |
| MANAGED-SESSION-004 | `tests/host-fork-agent.test.mjs` `HFA_reuse_after_join_sends_prompt_on_same_child`（completion 后复用不 spawn）；`tests/sync-delegate-lifecycle.test.mjs` `EXEC_026_sync_delegate_reuses_session_after_full_completion` / `EXEC_027_dispose_fails_unsettled_sync_delegate_call_scope` / `EXEC_027_cancel_before_completion_fails_pending_invoke` | MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/host-fork-agent.test.mjs` / `.../sync-delegate-lifecycle.test.mjs` |
| MANAGED-SESSION-005 | `tests/attached-session-runtime.test.mjs` `EXEC_026_reuse_scope_is_the_serialization_key_across_sessions` + `EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent` + `EXEC_026_unusable_binding_is_treated_as_absent_and_recreated`；`tests/host-fork-agent.test.mjs` `HFA_existing_fork_keeps_deep_agent_when_caller_passes_fast` / `HFA_reuse_keeps_deep_agent`；`tests/host-fork-busy-nudge.test.mjs`（BUSY_NUDGE_* 全组：fallback cursor 推进后仍保持 managed agent） | NEW + MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` / `.../host-fork-agent.test.mjs` / `.../host-fork-busy-nudge.test.mjs` |
| MANAGED-SESSION-006 | `tests/handle.test.mjs` `EXEC_009_a_retired_handle_answers_retired_forever` / `EXEC_009_a_retired_id_is_distinguishable_from_one_that_never_existed` / `EXEC_009_a_retired_child_session_is_still_recognised_as_a_child` + `EXEC_009_agent_pty_and_manager_job_handles_are_separate_identities` / `EXEC_005_the_views_partition_the_lifecycle_and_never_show_retired` / `EXEC_009_linked_children_lists_every_child_ever_linked`（按 creationOrder 而非 handle key 返回） / `EXEC_009_the_three_facts_replay_into_the_terminal_state` / `EXEC_001_fork_creates_a_child_run` / `EXEC_007_nudge_is_fire_and_forget`；`tests/terminal-policy.test.mjs` `TPOL_outstandingBackground_*` / `TPOL_mainSealedForBlogger_*` / `TPOL_sessionDead_*`（listable / retired 判定）；`tests/join-guard-outstanding-handles.test.mjs`；`tests/join-guard-wakeup.test.mjs` THEOREM_join_blocked / THEOREM_WorkActivated / THEOREM_projection_steps | MOVE + MOVE + MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `.../terminal-policy.test.mjs` / `.../join-guard-outstanding-handles.test.mjs` / `.../join-guard-wakeup.test.mjs` |
| MANAGED-SESSION-007 | `tests/handle.test.mjs` `EXEC_004_the_first_completion_wins_and_later_ones_are_refused` / `EXEC_004_each_completion_kind_survives_into_the_state` + `EXEC_004_completing_an_unknown_handle_is_refused_by_name` / `EXEC_009_completed_awaiting_join_carries_blob_refs` / `EXEC_009_cancelled_completion_has_no_blob` / `EXEC_009_fold_replays_completion_blob_refs` / `EXEC_009_codec_migrates_0_5_1_handle_completed_missing_blob_fields`；`tests/host-fork-agent.test.mjs` `HFA_fork_send_failure_fails_the_pending_run_without_blocking_fork_return`；`tests/host-fork-runtime.test.mjs` `HFRT_fail_run_writes_durable_failure_and_settles_source` / `HFRT_fail_run_cancelled_code_is_CANCELLED`；`tests/join-completion-property.test.mjs`；`tests/join-guard-wakeup.test.mjs` THEOREM_handle_completed / THEOREM_join_wake_path | MOVE + MOVE + MOVE + MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `.../host-fork-agent.test.mjs` / `.../host-fork-runtime.test.mjs` / `.../join-completion-property.test.mjs` / `.../join-guard-wakeup.test.mjs` |
| MANAGED-SESSION-008 | `tests/handle.test.mjs` `EXEC_004_join_may_only_retire_a_handle_that_actually_completed` / `EXEC_009_a_replayed_completion_or_retirement_is_absorbed` / `EXEC_004_a_retirement_without_a_completion_stops_the_replay`；`tests/join-v2-abandoned-order-lifecycle.test.mjs` `EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired`；`tests/join-guard-wakeup.test.mjs` THEOREM_blocked_to_awakened_fold_trails_confluent_after_retire | MOVE + MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `.../join-v2-abandoned-order-lifecycle.test.mjs` / `.../join-guard-wakeup.test.mjs` |
| MANAGED-SESSION-009 | `tests/handle.test.mjs` `EXEC_009_parent_abort_needs_the_handles_themselves_not_a_count`；`tests/handle-abandoned.test.mjs`（EXEC_009_* 全组：Abandoned 单赋值/不可 join/不可回退）；`tests/host-fork-agent.test.mjs` `HFA_fork_abandoned_handle_is_refused_before_spawn` + `HFA_reuse_abandoned_handle_is_retired_error`（abandon 不可复活）；`tests/host-fork-runtime.test.mjs` `HFRT_cancel_agent_fails_pending_run_and_aborts_child` / `HFRT_cancel_agent_after_run_settled_skips_fail_run_but_aborts_child` / `MANAGED_SESSION_009_shutdown_cancel_drains_durable_abandon_before_return`；`tests/shutdown-drain-contract.test.mjs` `shutdown ownership drains session runtimes before journal release` / `fork terminal callbacks are runtime-owned and drained before parent cancel` / `TurnAborted awaits child cascade before publishing parent terminal`（reconcile/background/per-session/fork/one-shot XTrace callback、Finality reviewer abort 与 parent `AbortChildren` 都留在 owner drain tree；禁止 remove→detached cancel / ignored AbortSession/AbortChildren；poisoned journal 同步关闭 reconcile admission）；`tests/join-v2-abandoned-order-lifecycle.test.mjs` `EXEC_009_abandoned_retire_clears_reportable_single_report`；`tests/sync-delegate-lifecycle.test.mjs` `G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child`（cascade cancel）；交叉 `requirements/verification-system/tests/temporal-harness.test.mjs` 的 scheduler/plugin-scope/finality reviewer real Task drain + poisoned-admission race | MOVE + MOVE + MOVE + NEW + MOVE + MOVE + CROSS | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `.../handle-abandoned.test.mjs` / `.../host-fork-agent.test.mjs` / `.../host-fork-runtime.test.mjs` / `.../shutdown-drain-contract.test.mjs` / `.../join-v2-abandoned-order-lifecycle.test.mjs` / `.../sync-delegate-lifecycle.test.mjs` |
| MANAGED-SESSION-010 | `tests/distiller-ownership.test.mjs` `EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible`（HostOwnedHidden 不进 listable） | MOVE | `node --test requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| MANAGED-SESSION-011 | `tests/satellite-runtime.test.mjs` `HOST_015_cache_invalidation_rereads_the_live_durable_companion_link` / `HOST_015_cache_invalidation_then_physical_loss_uses_close_then_replacement`（send/cache failure 后每次 ensure 重读 durable association，不会忘掉 old link 后直接 repoint）/ `HOST_015_companion_satellite_recovery_closes_old_durable_link_before_linking_replacement` / `HOST_015_companion_replacement_transitions_real_durable_link_without_semantic_cut` / `HOST_015_direct_companion_repoint_trips_process_fatal_on_semantic_cut` / conflict/no-adopt/query-failure/retry cases | MOVE | `node --test requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-012 | `tests/child-run-projection.test.mjs`（VERIFY_009 全组：`child_run_starts_active` / `child_run_completion_cell_is_single_assignment` / `projection_status_*` / `projection_to_record_*`）；`tests/host-fork-runtime.test.mjs`（HFRT_install_run / HFRT_mark_ready / HFRT_fork_runtime_* 全组：Child Run 生命周期）；`tests/host-fork-pty.test.mjs`（HFP_* 全组：PTY child run 生命周期）；`tests/host-fork-agent.test.mjs` `HFA_fork_create_session_failure_surfaces_host_error` / `HFA_fork_cancelled_runtime_is_not_found_and_fails_run` | MOVE + MOVE + MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/child-run-projection.test.mjs` / `.../host-fork-runtime.test.mjs` / `.../host-fork-pty.test.mjs` / `.../host-fork-agent.test.mjs` |
| MANAGED-SESSION-013 | `tests/host-fork-restart-lifecycle.test.mjs` `HFR_restart_abandoned_handle_recovered_abandoned` / `HFR_restart_retired_handle_recovered_retired` / `HFR_restart_host_owned_hidden_handle_is_filtered_out` / `HFR_restart_active_handle_recovers_active` / `HFR_restart_recovery_commit_failure_blocks`（handle 投影恢复；恢复工作流/legacy false abort 归 crash-reconciliation） | MOVE | `node --test requirements/managed-session-lifecycle/tests/host-fork-restart-lifecycle.test.mjs` |
| MANAGED-SESSION-014 | `tests/sync-delegate-lifecycle.test.mjs` `G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close`（deleted child retire live binding 但为 owner scope close 保留） | MOVE | `node --test requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs` |
| MANAGED-SESSION-015 | `tests/handle.test.mjs` `EXEC_009_a_linked_handle_records_the_child_session_it_drives` / `EXEC_009_only_an_agent_handle_answers_the_agent_question` + `EXEC_009_relinking_a_live_handle_rebinds_it_rather_than_duplicating` / `EXEC_009_a_completion_for_a_handle_that_was_never_linked_stops_the_replay`；`tests/terminal-policy.test.mjs` `TPOL_tryLinkedChild_finds_child_handle_and_keeps_target_agent` / `TPOL_tryLinkedChild_without_journal_returns_none`；`tests/host-fork-agent.test.mjs` `HFA_reuse_unknown_agent_id_is_error`；`tests/join-v2-abandoned-order-lifecycle.test.mjs` `EXEC_018_creation_order_follows_HandleLinked_fold_sequence`（handle id → child session 记录） | MOVE + MOVE + MOVE + MOVE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `.../terminal-policy.test.mjs` / `.../host-fork-agent.test.mjs` / `.../join-v2-abandoned-order-lifecycle.test.mjs` |
| MANAGED-SESSION-016 | `tests/interrupt-boundary.test.mjs`（attempt-only port root fail-closed + 无 child cascade；Loop/NeedHelp root gate；TurnAborted durable cancel → physical cascade → terminal 顺序）；`tests/handle-abandoned.test.mjs` `EXEC_009_Active_to_Abandoned_fold_and_projection`（Abandoned 立即退出 listable/horizon） | NEW + MOVE | `node --test requirements/managed-session-lifecycle/tests/interrupt-boundary.test.mjs` / `.../handle-abandoned.test.mjs` |
| MANAGED-SESSION-017 | `tests/interrupt-successor.test.mjs`（NeedHelp claim survives abort→idle；fatal stop uses synchronous `ManagedSessionTermination`；internal InterruptAttempt caller allowlist；sensor abort failure rollback；no cross-callback termination registry）；`requirements/interaction-authority/tests/assistance-abort-fence.test.mjs`（INTERACTION-AUTHORITY-012 fresh-idle consumption shape） | NEW + CROSS | `node --test requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs requirements/interaction-authority/tests/assistance-abort-fence.test.mjs` |

### 反向覆盖（OWNED / NEEDS-SPLIT clause → 本包命题）

- `HOST-009`（OWNED）→ MANAGED-SESSION-001/002/003/011。
- `HOST-015`（restore matching 部分）→ MANAGED-SESSION-003/013。
- `EXEC-006`（OWNED）→ MANAGED-SESSION-012。
- `EXEC-009`（OWNED）→ MANAGED-SESSION-006/007/008/009/015。
- `EXEC-014`（hidden handle 部分）→ MANAGED-SESSION-010。
- `EXEC-017`（cascade cancel 部分）→ MANAGED-SESSION-009。
- `EXEC-017`（attempt interrupt 与 logical cancel 权限分型）→ MANAGED-SESSION-016。
- `EXEC-017`（internal interrupt successor / parent wake closure）→ MANAGED-SESSION-017。
- `EXEC-026`（runtime ownership 部分）→ MANAGED-SESSION-001/005/014。
- `EXEC-028`（lifecycle 部分）→ MANAGED-SESSION-004/005。
- `REVIEW-010/019`（fail-closed 消费）→ MANAGED-SESSION-003/011（交叉引用，不复制）。
- `REVIEW-015`（dedicated create/retire ≠ Dispose）→ MANAGED-SESSION-014。

### 包拥有的 gate / anchor

- `scripts/checks/session-ownership-ratchet.mjs` 问卷的
  `reusable / cancel / retire / handle / crashReconcile` 字段 → 本包（与 session-ontology 共享
  同一 gate；一个 assertion 一个 owner，字段级划界）。verify 测试已随 session-ontology MOVE
  （`requirements/session-ontology/tests/session-ownership-ratchet.test.mjs`），本包 REUSE。
- semantic-anchors.mjs：本包**零 anchor**。

### SPLIT@cutover 清单

1. `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs`（import fable → 禁止移动）：恢复/reuse/
   replacement 断言归本包；`HOST_014_SatelliteKind_is_Companion_only` 归 session-ontology。
   cutover 时拆文件并移除 fable import。
2. `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs`：handle 投影恢复断言归本包；
   `HFR_restart_legacy_false_abort_*` 归 effect-accounting（EXEC-021/022）。cutover 时拆分。
3. `requirements/managed-session-lifecycle/tests/host-fork-runtime.test.mjs`：InstallRun/FailRun/CancelAgent/ForkRuntime
   面归本包；`JoinWithPermit/AwaitAgentWithPermit/validatePermit` 归 crash-reconciliation
   （EXEC-023）与 causal-wait；join/await 调用代数归 delegation。cutover 时拆分。
4. `requirements/delegation/tests/sync-delegate-runtime.test.mjs`：reuse/retire/scope-close 断言归本包；
   batch/canonical/WorkRecord 断言归 delegation / work-record。cutover 时拆分。
5. `requirements/managed-session-lifecycle/tests/terminal-policy.test.mjs`：`tryLinkedChild` / `mainSealedForBlogger` /
   `outstandingBackground`（durable 部分）归本包；`isTopLevelManager` 归 interaction-authority
   （AuthorityRoot 事实）；`roleName` 归 session-ontology/host-boundary。cutover 时拆分。
6. `requirements/session-ontology/tests/session-flattening.test.mjs`（import fable）：`abort_children_cascade`
   归本包；物理扁平断言归 session-ontology。cutover 时移除 fable import 后 MOVE。
7. `requirements/managed-session-lifecycle/tests/handle.test.mjs` 等 execution/ 家族文件：本包 REUSE 其 handle 断言；
   join/fork/PTY 断言归 delegation/process-execution；cutover 时按断言拆分（该目录 owner 多元，
   本包不 MOVE）。
