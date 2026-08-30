# execution-model-routing — HOW

## 架构与核心机制

`execution-model-routing` 通过单向流水线将 MJS 策略与 Host 消息拦截打通：

```text
~/.config/opencode/wanxiangshu.mjs (唯一策略权威)
       │ (route: role, running, previous -> target | null)
       ▼
ModelRoutingRuntime (进程单例，管理 Lease multiset 与 Capacity Token)
       │
       ├──► chat.message hook (物理准入: (SessionId, PhysicalUserMessageId) -> ModelTarget)
       └──► messages.transform hook (容量仲裁: Lineage Token 借用与 Step Fence 拦截)
```

1. **Bootstrap 与 MJS 策略加载**：
   - 进程启动时探测 `~/.config/opencode/wanxiangshu.mjs`，若缺失则以原子方式写出内置推荐模板文件并加载。
   - 加载后保持函数引用，不维护多级 runtime 兜底策略。

2. **物理准入与租约管理**：
   - 调度请求仅在 Host `chat.message` 阶段触发，根据 `(SessionId, PhysicalUserMessageId)` 绑定目标模型并修改 Host message。
   - 新物理消息到达时原子取代并取消旧 pending demand；`null` 返回值进入等待队列并在租约归还时事件驱动重试。

3. **Lineage 令牌借用与召回**：
   - 真实 Token Ledger 记录全局占用；Borrowing Decorator 维护 session 派生树。
   - 子节点在 step 级别借用祖先等待中的 token，并在祖先恢复或 step 终结时按序归还。
   - `ToolRegistry` 在 managed tool invocation 的最外层先调用 `SessionExecutionBinding.endProviderStepAtToolBoundary`。后者只从当前 provider-attempt binding 取得冻结的 `PhysicalUserMessageId`，并结合 Host tool context 的 exact `ProviderRunIdentity` 调用 `ModelRouting.endProviderStep`；随后才进入 Strength/Role/Capability gate 与实际 tool body。这样同步 delegate、output distillation 等“工具内等待子模型”的路径不会把调用者 capacity 一起锁住。
   - 该 handoff 纯因果、时间无关；测试只推进显式状态边界，不使用 sleep、deadline 或真实 timeout。真实 `InFlight` provider step 仍不可被 borrower 越权并发使用。

4. **Physical terminal 证据收敛**：
   - `HostEventCodec` 把 assistant completion 分成 provider-step terminal 与 physical-execution terminal 两层。
   - physical terminal 只接受明确最终 `finish`：`stop | length | content-filter`；`tool-calls | unknown | error` 与显式 assistant error 仅结束 step。OpenCode 的 upstream stream failure 可落盘为 `completed + finish="unknown"` 且无 `error`，因此禁止用“非 tool-calls”反推 physical completion。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EMR-001 | `requirements/execution-model-routing/tests/scheduler-module-config.test.mjs::WHAT[EMR-001] EMR_001_missing_scheduler_is_created_once_then_loaded_from_disk`；`requirements/execution-model-routing/tests/scheduler-module-config.test.mjs::WHAT[EMR-001] EMR_001_existing_scheduler_is_never_overwritten`；`requirements/execution-model-routing/tests/scheduler-module-config.test.mjs::WHAT[EMR-001] EMR_001_concurrent_bootstrap_keeps_one_atomic_winner_without_merge` |
| EMR-002 | `requirements/execution-model-routing/tests/scheduler-module-config.test.mjs::WHAT[EMR-002] EMR_002_scheduler_preserves_running_duplicates_null_and_previous`；`requirements/execution-model-routing/tests/scheduler-module-config.test.mjs::WHAT[EMR-002] EMR_002_scheduler_program_errors_fail_closed` |
| EMR-003 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-003] EMR_003_each_active_physical_execution_contributes_one_running_occurrence` |
| EMR-004 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-004] EMR_004_required_null_waits_for_an_occupancy_event_then_retries`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-004] EMR_004_newer_physical_message_cancels_superseded_pending_demand`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-004] EMR_004_an_earlier_null_waiter_does_not_head_of_line_block_another_role`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-004] EMR_004_optional_null_is_k0_not_a_pending_demand`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-004] EMR_004_strength_reservation_is_adopted_by_chat_message_without_double_counting` |
| EMR-005 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs::WHAT[EMR-005] EMR_005_runtime_contains_no_product_lane_or_max_sessions_policy` |
| EMR-006 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-006] EMR_006_same_physical_message_retry_reuses_target_without_scheduler_rerun`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-006] EMR_006_new_physical_message_supersedes_old_A_B_occupancy_without_idle`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_effective_agent`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-006] EMR_006_lease_is_stable_only_for_one_physical_user_material`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-006] EMR_006_continuation_passes_previous_target_but_new_session_passes_null` |
| EMR-007 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-007] EMR_007_execution_release_is_idempotent_and_wakes_waiters_once`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-007] EMR_007_late_terminal_for_superseded_physical_execution_cannot_release_current_lease` |
| EMR-008 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs::WHAT[EMR-008] EMR_008_host_inventory_no_longer_exposes_model_binding_authority`；`requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs::WHAT[EMR-008] SPEC_INV_fast_and_deep_physical_model_equality_is_not_an_eligibility_gate` |
| EMR-009 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs::WHAT[EMR-009] EMR_009_chat_message_is_the_single_managed_execution_admission_owner` |
| EMR-010 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_lineage_credit_is_free_only_to_descendants_not_global_waiters`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_provider_step_handoff_makes_the_same_credit_available_to_a_waiting_descendant`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_ancestor_recall_waits_for_descendant_step_end_without_overbooking`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_late_old_terminal_cannot_release_a_new_provider_step`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_confirmed_pre_dispatch_suppression_returns_the_step_token`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_recalled_child_may_take_new_ordinary_capacity_for_exact_target`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_owner_priority_beats_multiple_children_and_nested_borrowers`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_credit_never_crosses_provider_boundary`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_multi_provider_credit_requires_one_token_attribution`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_blogger_borrows_the_lender_blogger_when_main_is_borrowed`；`requirements/execution-model-routing/tests/model-routing-runtime.test.mjs::WHAT[EMR-010] EMR_010_blogger_gets_no_companion_credit_when_main_did_not_borrow`；`requirements/execution-model-routing/tests/tool-provider-step-boundary.test.mjs::WHAT[EMR-010] EMR_010_managed_tool_execution_ends_the_current_provider_step_before_tool_body` |
