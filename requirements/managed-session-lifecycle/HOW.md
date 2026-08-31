# managed-session-lifecycle — HOW

## 架构机制

### 1. Handle 状态机与单一写控制器

子会话生命周期通过 `HandleProjection` 纯函数折叠与 `HandleController` 统一定义：
- 状态转移为单向不可逆：`Active → CompletedAwaitingJoin → Retired` 与 `Active | CompletedAwaitingJoin → Abandoned`。
- `HandleController` 作为唯一的写入控制器，保证完成单赋值、墓碑状态原子写入以及对隐藏句柄的视图过滤隔离。
- `HandleLinked` 只能重用 child、target、byname、role 与 ownership 完全一致的 durable binding；任一 identity 漂移返回 typed `HandleIdentityConflict`。同一 logical person 的新 work unit 可以在原物理 child 上重新进入 `Active`，`Abandoned` 不可重开。
- Executable proof 只通过注册 surface 穿越 JS 边界：`HandleSurface` 调用真实 `HandleProjection`，`HandleFoldSurface` 调用真实 `ExecutionFactFold`，`Handle/JournalSurface` 以 opaque resource 调用 canonical `EventStore → AgentJournal → HandleController`。测试不得重建 projection、fold、codec、journal、controller 或 join decision。

### 2. 运行时生命周期管理器

- **AttachedSessionRuntime**：管理 Dedicated 会话的池化与作用域生命周期，以 `(ReuseScopeId, Role)` 为键，实现跨轮次的透明复用与故障解绑。
- **SatelliteRuntime**：管理 Companion 叶子会话的单飞创建与精确恢复，实现 `Close(old) → Link(new)` 的原子替换协议。
- **HostForkRuntime**：协调 Fork 子会话的安装、关联持久化、物理执行与超时控制，保障双通道完成事件的分发。

### 3. 中断边界与排空协议

- **权限分型**：区分仅作用于子会话单次物理尝试的 `InterruptAttempt` 与执行完整资源清理的 `AbortSession`。根会话受保护，免受内部意外中断。
- **后继闭合**：内部中断必须显式挂接恢复后继（如求助处理、重试等）或直接发布 `Failed` 终态唤醒父级等待。
- **Abandon 授权闸门**：`HandleController.recordAbandon/cancelChildren` 只能由已确认的 logical parent/session 终止或 child 永久丢失恢复证明调用。`TurnAborted` 只描述当前 attempt，不拥有 child logical-cancel capability；process/plugin shutdown 也只拥有 observer detach capability。
- **双排空语义**：logical cancel 使用 `CancelAndDrain`，允许 durable `HandleAbandoned` + 物理 child teardown；process/plugin shutdown 使用 `DetachAndDrain`，只排空 callback、解绑订阅与本地 runtime/PTY 资源，绝不写 `HandleAbandoned`、绝不 `AbortSession` live agent child。重启后由 durable `HandleLinked(Active)` 恢复。
- **Execution settlement barrier**：logical cancel/delete 在切断新工作准入后，把终止授权交给 `managed-chat-execution` 的 settlement port；该 owner 从 durable projection 选择 exact keys，并以事件完成 barrier。lifecycle 只等待 owner 返回的 durable drained witness，不维护 execution 镜像，不 blind-release session，不运行 timer 或 polling。process/plugin shutdown 不调用该 port。
- **Run closure barrier**：容器复用路径在 execution settlement 与受权 child drain 完成后，调用 `interaction-authority` 持久化 exact LogicalRun closure；只有 run-matched durable closure witness 才允许 `participant-identity` 为同一 `SessionId` 安装 fresh evidence。detach、idle 与 association removal 不参与此判断。
- **有序清理**：明确会话终止时遵循严格的异步排空序列，先切断新工作准入，依次等待 execution settlement barrier、后台调和、经授权的子会话级联取消与持久化写入，再建立 exact run closure，最后发布 lifecycle terminal 并释放或复用底层容器；仅 process shutdown 时则执行无业务终态的 detach 后释放 durable substrate。

## DEPENDS ON

- `session-ontology`
- `crash-reconciliation`
- `managed-chat-execution`
- `interaction-authority`
- `participant-identity`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| MANAGED-SESSION-001 | `requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs::WHAT[MANAGED-SESSION-001] EXEC_026_get_or_create_creates_and_binds_a_work_child_once` |
| MANAGED-SESSION-002 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs::WHAT[MANAGED-SESSION-002] HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child` |
| MANAGED-SESSION-003 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs::WHAT[MANAGED-SESSION-003] HOST_015_companion_reuses_exact_journal_linked_physical_child`；`requirements/managed-session-lifecycle/tests/session-recovery.test.mjs::WHAT[MANAGED-SESSION-003] session_recovery_contract_conflict_fails_closed_without_guessing` |
| MANAGED-SESSION-004 | `requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs::WHAT[MANAGED-SESSION-004] EXEC_026_sync_delegate_reuses_session_after_full_completion` |
| MANAGED-SESSION-005 | `requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs::WHAT[MANAGED-SESSION-005] EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent`；`requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs::WHAT[MANAGED-SESSION-005] EXEC_026_unusable_binding_is_treated_as_absent_and_recreated` |
| MANAGED-SESSION-006 | `requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-006] EXEC_009_agent_pty_and_manager_job_handles_are_separate_identities`；`requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-006] EXEC_005_the_views_partition_the_lifecycle_and_never_show_retired`；`requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_handle_answers_retired_forever` |
| MANAGED-SESSION-007 | `requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-007] EXEC_004_the_first_completion_wins_and_later_ones_are_refused`；`requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-007] EXEC_004_each_completion_kind_survives_into_the_state` |
| MANAGED-SESSION-008 | `requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-008] EXEC_004_join_may_only_retire_a_handle_that_actually_completed`；`requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-008] EXEC_009_a_replayed_completion_or_retirement_is_absorbed` |
| MANAGED-SESSION-009 | `requirements/managed-session-lifecycle/tests/handle-abandoned.test.mjs::WHAT[MANAGED-SESSION-009] EXEC_009_Active_to_Abandoned_fold_and_projection`；`requirements/managed-session-lifecycle/tests/handle-abandoned.test.mjs::WHAT[MANAGED-SESSION-009] EXEC_009_recordAbandon_CAS_first_wins` |
| MANAGED-SESSION-010 | `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs::WHAT[MANAGED-SESSION-010] EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible` |
| MANAGED-SESSION-011 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs::WHAT[MANAGED-SESSION-011] HOST_015_missing_restored_child_closes_then_links_replacement`；`requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs::WHAT[MANAGED-SESSION-011] HOST_014_children_query_failure_does_not_guess_or_create` |
| MANAGED-SESSION-012 | `requirements/managed-session-lifecycle/tests/child-run-projection.test.mjs::WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_completion_cell_is_single_assignment`；`requirements/managed-session-lifecycle/tests/child-run-projection.test.mjs::WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_cancel_or_runtime_cancel` |
| MANAGED-SESSION-013 | `requirements/managed-session-lifecycle/tests/host-fork-restart-lifecycle.test.mjs::WHAT[MANAGED-SESSION-013] HFR_restart_active_handle_recovers_active`；`requirements/managed-session-lifecycle/tests/session-recovery.test.mjs::WHAT[MANAGED-SESSION-013] session_recovery_contract_reenlist_filters_hidden_handles` |
| MANAGED-SESSION-014 | `requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs::WHAT[MANAGED-SESSION-014] G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close` |
| MANAGED-SESSION-015 | `requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-015] EXEC_009_only_an_agent_handle_answers_the_agent_question`；`requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-015] EXEC_009_a_linked_handle_records_the_child_session_it_drives`；`requirements/managed-session-lifecycle/tests/handle.test.mjs::WHAT[MANAGED-SESSION-015] EXEC_009_one_durable_handle_cannot_be_rebound_to_another_child` |
| MANAGED-SESSION-016 | `requirements/managed-session-lifecycle/tests/interrupt-boundary.test.mjs::WHAT[MANAGED-SESSION-016] Sessions adapter rejects root attempt interrupt and physically aborts a managed child exactly once`；`requirements/managed-session-lifecycle/tests/interrupt-boundary.test.mjs::WHAT[MANAGED-SESSION-016] automatic sensors cannot interrupt user-facing root and use attempt-only port`；`requirements/managed-session-lifecycle/tests/interrupt-boundary.test.mjs::WHAT[MANAGED-SESSION-016] Turn orchestration consumes typed outcome without cross-callback aborted registry PC` |
| MANAGED-SESSION-017 | `requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs::WHAT[MANAGED-SESSION-017] fail-closed interrupt becomes Failed terminal so fork completion wakes parent`；`requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs::WHAT[MANAGED-SESSION-017] invariant and tool fail-closed paths cannot use orphan InterruptAttempt`；`requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs::WHAT[MANAGED-SESSION-017] raw InterruptAttempt callers are restricted to workflows with an explicit successor owner`；`requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs::WHAT[MANAGED-SESSION-017] failed Host abort rolls back Loop one-shot causes`；`requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs::WHAT[MANAGED-SESSION-017] fatal termination never stores cross-callback cause state` |
| MANAGED-SESSION-018 | `requirements/managed-session-lifecycle/tests/shutdown-drain-contract.test.mjs::WHAT[MANAGED-SESSION-018] shutdown detaches session runtimes before journal release without logical cancel`；`requirements/managed-session-lifecycle/tests/shutdown-drain-contract.test.mjs::WHAT[MANAGED-SESSION-018] fork terminal callbacks drain before either detach or authorized parent cancel`；`requirements/managed-session-lifecycle/tests/shutdown-drain-contract.test.mjs::WHAT[MANAGED-SESSION-018] TurnAborted publishes attempt terminal without child cascade`；`requirements/delegation/tests/fork-tool.test.mjs::WHAT[MANAGED-SESSION-018] FORK_TOOL_process_detach_preserves_durable_active_child_for_restart` |
| MANAGED-SESSION-019 | `requirements/managed-session-lifecycle/tests/exact-execution-settlement.test.mjs::WHAT[MANAGED-SESSION-019] cancel and delete lifecycle signals settle exact terminal resources through the execution owner` |
| MANAGED-SESSION-020 | `requirements/managed-session-lifecycle/tests/reused-session-run-closure.test.mjs::WHAT[MANAGED-SESSION-020] fresh identity waits for exact durable prior-run closure on the public plugin canary` |

## GAP

- `GAP-031`（CLOSED）：最后两个 `managed-surface.mjs` exports 用常量对象声称 SyncDelegate 复用/取消与 Host PTY 生命周期。SyncDelegate proofs 现通过 opaque runtime 执行真实 durable owner admission、prompt acceptance、completion、deleted-child staging、scope-close lookup、cancel 与 dispose；PTY proofs 通过 controlled backend 执行真实 `HostForkRuntimePty`。全部 consumers 归零后已物理删除 support 文件。
- `GAP-030`（CLOSED）：旧 `tests/support/managed-surface.mjs` 重建了 Handle projection、fact fold、JSON codec、in-memory journal、HandleController 与 join drain；因此测试可以在 production owner 错误时仍由镜像实现自证。现有 consumers 已迁到注册 production surfaces；`recordAbandon` 首胜 proof 穿过真实 canonical EventStore、AgentJournal 与 HandleController；常量 wake trace 已替换成真实 `ExecutionFactFold` replay；对应 mirror exports 全部删除。
- `GAP-029`（CLOSED）：旧实现把 plugin/process shutdown 与未被内部 successor owner 认领的 `TurnAborted` 都升级成 logical parent cancellation，最终经 `CancelAndDrain → HandleController.cancelChildren` 写入 `HandleAbandoned(ParentCancelled)` 并物理 `AbortSession(child)`。现已拆成 `DetachAndDrain` 与 `CancelAndDrain` 两种互斥权限：process/plugin shutdown 只解绑 observer 与本地资源，保留 durable `Active`；ordinary `TurnAborted` 不再拿到 `abortParent` / `CancelSessionChildren` capability；只有 SessionDeleted、显式 successor-less termination 等明确 logical termination 仍可进入 durable abandon。`shutdown-drain-contract.test.mjs`、`interrupt-boundary.test.mjs` 与真实 fork process-detach oracle 已绿；核心实现落于 `506ab7d36`。
