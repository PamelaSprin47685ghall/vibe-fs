# delegation — HOW

## 架构机制

### 委托接口分流与权能门禁

DELEG-020 约束：委托语义不依赖当前工具名字面值（`fork`、`commission`、`inspect`、`establish-behavior`、`repair-behavior`），改名不动 WHAT 语义定义。

系统定义三类委托途径，由角色权能门禁严格限制：

1. **异步见证委托（`fork`）**：由 Manager 在使命内部调用，创建具有独立 Byname 的子执行者，支持附加历史背景（attachment）与建议性工具调用估算。
2. **独立道路委托（`commission`）**：由 Orchestrator 调用，负责开启或续做独立集成道路，支持多道路并行推进。
3. **同步委托（`inspect` / `establish-behavior` / `repair-behavior`）**：由业务角色在单轮内发起阻塞式子任务，经由 `SyncDelegate` 管道调度，完成取证或局部修复。

### 载荷渲染与方向不对称

- **父 → 子（初始提示词注入）**：父会话向子会话传递上下文时，`ForkChildPayload` 将任务正文渲染为 `instructions`，将 `commissioner_record` 与 `attached_work_record` 作为 TOML 数据字段嵌入 body，杜绝将背景解析为指令或混入注释。
- **子 → 父（完成项回传）**：子会话结束并回传结果时，`JoinResultRenderer` 仅将物化的 WorkRecord 以 entry-local 注释形式注入 wire，严禁包裹为字段式 DTO。

### 同步委托批次与串行化

- **批次聚拢**：宿主在处理同单次运行中指向同一角色的多个同步委托时，按工具调用顺序合并为一个语义 batch，拼接 charge 后单次调度。
- **单栈执行与结果分发**：串行化键为直接调用方的 `ReuseScope`。仅第一位 canonical 调用方获得完整 WorkRecord，其余 sibling 调用方获得引用句柄。
- **普通完成收口**：被委托方普通 Assistant 结束即触发返回，无独立 return 协议通道。

### Reusable work unit

- 业务控制流只存在于 F# CE：`prepareHandoff → dispatch → await own completion → checkpointCompletedHandoff`。禁止 `Stage/Phase/ActiveWorkUnit`、显式 transition API 等第二运行时或 durable program counter。
- durable truth 只记录已经发生的事实：某个 logical route 的一次已完成 handoff 确实让 callee 看到了 parent XTrace 截止到哪个 cursor。projection 仅把这些 completion facts 积分成 `latestDeliveredThrough(route)`；它不拥有执行位置。
- 新调用从 `latestDeliveredThrough(route)` 到当前 parent XTrace head 物化 delta；route 首次调用取完整 parent LWR。logical route = fork Byname 或 caller scope 下的 dedicated SyncDelegate role，绝不以 physical child `SessionId` 作为连续性身份。
- invocation-local 的 child start cursor、expected Authority Root、waiter/subscription 属于物理 correlation resource，可跨 callback 保存；它们不得 durable 化为 workflow stage。
- Host sticky terminal 可以继续服务 late observer/recovery；delegation CE 只接受与本次 dispatch 的 causal identity 匹配的 completion/failure。run-scoped `Completed/Failed/Aborted` 都保留 Authority Root；不能把“订阅之后”当作身份。
- 首 prompt 的 Host acceptance 若为 unknown，`PromptAuthority` 的 durable Pending claim 是唯一恢复所有者：fork run 保持 Active、terminal observer 保持绑定、不得合成 `HandleCompleted`、不得自动重发。调用面返回明确的“可能已接受”后果，阻止调用方用第二个 child 猜测性补偿。
- fork 新 participant 是异步 assignment：返回只由本次 dispatch 成败决定；same-road continuation 与 SyncDelegate 是同步 CE：等待本调用 completion、物化 bounded callee LWR、再 checkpoint completed handoff。
- 新 charge 遇到仍在运行的同 route 调用直接拒绝。Busy nudge 只服务同一 LogicalRun 的内部 continuation，彻底退出 assignment 工具路径。

### Join 消费与中断

Join 机制从所有者的完成信箱中按稳定排序逐项 CAS 消费可用结果，单次消费上限受 `MaxJoinBatch` 约束。外部打断信号与超时仅产生 `Interrupted` 结果，确保子会话的执行与既有权能不受破坏。

### Contract/Runtime 编译边界

- `Delegation.Contract` 汇集稳定 command/result、fact、payload、route 与 completion evidence；`Delegation.Fold` 只消费 contract 计算投影；`Delegation.Ledger` 在显式 Composition locality 中连接 `AgentJournal`。
- Sync/Fork/Recovery CE 分居三个 Runtime locality；Host 与 PTY 的物理调用分居两个 Adapter locality。Runtime/Adapter 只能依赖 contract/fold，普通 consumer 不能引用它们。
- `scripts/checks/owner-projects.mjs` 固定 locality kind、方向与 closure budget；`scripts/compile-owner.mjs` 只编译目标 ProjectReference closure 的单一 flat projection。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DELEG-001 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-001] manager_role_law_entrusts_by_consequence_not_persona` |
| DELEG-002 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-002] calling_names_differ_in_persona_depth_not_authority` |
| DELEG-003 | `requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-003] FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname` |
| DELEG-004 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-004] commission_and_fork_are_distinct_contracts_not_witness` |
| DELEG-005 | `requirements/delegation/tests/join-v2-wire.test.mjs::WHAT[DELEG-005] JOIN_V2_rendered_wire_is_parseable_without_legacy_fields` |
| DELEG-006 | `requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-006] FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier` |
| DELEG-007 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-007] sync_delegate_edges_are_the_allowed_dag_only` |
| DELEG-008 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-008] SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order` |
| DELEG-009 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-009] SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent` |
| DELEG-010 | `requirements/delegation/tests/sync-delegate.test.mjs::WHAT[DELEG-010] EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder` |
| DELEG-011 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-011] SYNC_RUNTIME_ordinary_completion_settles_batch_without_return_channel` |
| DELEG-012 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-012] SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference` |
| DELEG-013 | `requirements/delegation/tests/join-completion.test.mjs::WHAT[DELEG-013] JOIN_COMPLETION_completed_is_rendered_as_entry_local_work_record` |
| DELEG-014 | `requirements/delegation/tests/join-completion.test.mjs::WHAT[DELEG-014] JOIN_COMPLETION_batch_preserves_order_and_bounded_work_records` |
| DELEG-015 | `requirements/delegation/tests/join-completion.test.mjs::WHAT[DELEG-015] JOIN_COMPLETION_interrupted_is_not_fork_error` |
| DELEG-016 | `requirements/delegation/tests/join-v2-wire.test.mjs::WHAT[DELEG-016] JOIN_V2_empty_batch_is_plain_empty_wire` |
| DELEG-017 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-017] SYNC_RUNTIME_work_record_is_evidence_and_does_not_transfer_authority` |
| DELEG-019 | `requirements/delegation/tests/fork-child-payload.test.mjs::WHAT[DELEG-019] FORK_CHILD_PAYLOAD_full_shape_puts_all_instructions_before_reference_data` |
| DELEG-020 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-020] delegation_semantics_do_not_depend_on_current_tool_names` |
| DELEG-021 | `requirements/delegation/tests/fork-attachment.test.mjs::WHAT[DELEG-021] DELEG_021_attachment_is_background_between_commissioner_and_requirements`；`requirements/delegation/tests/fork-attachment.test.mjs::WHAT[DELEG-021] DELEG_021_attachment_text_cannot_replace_the_assignment` |
| DELEG-022 | `requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs::WHAT[DELEG-022] DELEG_022_replace_sets_exact_remaining_and_clears_prior_counted_calls`；`requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs::WHAT[DELEG-022] DELEG_022_each_distinct_real_tool_call_decrements_once_and_saturates_at_zero`；`requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs::WHAT[DELEG-022] DELEG_022_projection_is_incremental_not_a_transcript_or_xtrace_scan` |
| DELEG-023 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-023] SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted` |
| DELEG-024 | `requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join`；`requirements/delegation/tests/reusable-handoff.test.mjs::WHAT[DELEG-024] reusable handoff advances one durable parent delta window at a time`；`requirements/delegation/tests/reusable-handoff.test.mjs::WHAT[DELEG-024] reusable prompt carries the new charge and parent delta as data`；`requirements/delegation/tests/reusable-handoff.test.mjs::WHAT[DELEG-024] bounded child result never widens to an earlier invocation` |
| DELEG-025 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-025] SYNC_RUNTIME_late_failure_from_previous_authority_root_cannot_fail_reused_call`；`requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-025] reusable fork terminal failure is guarded by the accepted authority root`；`requirements/host-boundary/tests/events-port.test.mjs::WHAT[DELEG-025] EVT_run_scoped_failure_preserves_authority_root_across_host_event_port` |
| DELEG-026 | `requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-026] reusable delegation has no durable program-counter/state-machine vocabulary`；`requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-026] fork admission bookkeeping that can fail happens before dispatch` |
| DELEG-027 | `requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-027] active fork assignment never becomes BusyAgentNudge` |
| DELEG-028 | `requirements/delegation/tests/delegation-compile-boundary.test.mjs::WHAT[DELEG-028] Delegation contract excludes workflow Host PTY and recovery sources`；`requirements/delegation/tests/delegation-compile-boundary.test.mjs::WHAT[DELEG-028] Delegation focused localities stay within compile budgets` |

## GAP

- `GAP-027`（CLOSED）：旧 reusable handoff 以 physical child `SessionId` 持有 cursor，并在 prompt 已 dispatch 后追加可失败 bookkeeping；旧 sticky terminal 还能跨 invocation 重放，fork idle reuse 又会立即返回旧/全生命周期结果，active new charge 还会混入 `BusyAgentNudge`。现已收口为 direct F# CE：logical-route frontier 只由 completed-handoff fact 推导；same-road fork/SyncDelegate 都执行 `prepare delta → dispatch → await own causal completion → bounded callee LWR → checkpoint`；fresh-only terminal observation 与 Authority Root 共同阻断上一轮 Completed/Failed；active assignment 明确拒绝；HostForkRuntime 的 bounded WorkRecord projector 为必需 capability，不能再构造“可完成但无 invocation delta”的 runtime。真实 fork tool 与 inspector/coder reuse 回归均已覆盖，authoritative runner 3405/3405 green。
