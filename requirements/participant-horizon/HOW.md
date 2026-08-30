# participant-horizon — HOW

## 架构与核心机制

`participant-horizon` 通过静态反向扫描门禁与运行时投影过滤相结合，筑牢信息准入边界：

```text
内部物理事件 / DTO 状态
       │
       ▼
JoinResultRenderer / HorizonTool (正向过滤: 转换为自然语言后果与 WorkRecord)
       │
       ▼
Provider-Visible Surface
       ▲
       │ (反向门禁拦截: Gate B 扫描禁止 token 与 DTO 模式)
provider-leak-gate.mjs
```

1. **正向准入与自然语言转换**：
   - `JoinResultRenderer` 负责将内部任务完成、中断、错误或超时统一转化为面向自然语言的后果说明，剥离所有底层状态机码。
   - `HorizonTool` 以只读拉取方式返回当前在场子参与者的最新工作记录摘要（Byname 索引），不暴露物理 SessionId。
   - Horizon roster 与终结门禁的 `listable/outstanding` 视图严格分离：父级可见 `Active`、`CompletedAwaitingJoin`、`Abandoned` 都进入 roster，只有 `Retired` 才退出。`Abandoned` 由 Horizon 转译成“未返回”，直到 Join 消费该后果。

2. **Gate B 反向防泄露门禁**：
   - 静态检查器 `provider-leak-gate.mjs` 扫描所有面向模型组装提示词与工具描述的代码，禁止 `SessionId`、`AgentId`、`ManagerJobId`、`PtyId`、`status`、`code` 等标记出现在输出流中。
   - 对隐藏角色（如 Reviewer、Blogger）的调用在解析层统一按通用不存在处理，避免错误信息泄露内部拓扑。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PARTICIPANT-HORIZON-001 | `requirements/participant-horizon/tests/admission-law.test.mjs::WHAT[PARTICIPANT-HORIZON-001] PH_exec_005_horizon_description_declares_pull_only_and_hides_machinery` |
| PARTICIPANT-HORIZON-002 | `requirements/participant-horizon/tests/provider-leak-gate.test.mjs::WHAT[PARTICIPANT-HORIZON-002] gate_b_documents_forbidden_machine_tokens`；`requirements/participant-horizon/tests/provider-leak-gate.test.mjs::WHAT[PARTICIPANT-HORIZON-002] gate_b_leaky_renderer_fixture_is_red_for_machine_tokens`；`requirements/participant-horizon/tests/provider-leak-gate.test.mjs::WHAT[PARTICIPANT-HORIZON-002] gate_b_scan_entries_aggregates`；`requirements/participant-horizon/tests/provider-leak-gate.test.mjs::WHAT[PARTICIPANT-HORIZON-002] gate_b_repo_scan_without_baseline_is_zero` |
| PARTICIPANT-HORIZON-003 | `requirements/participant-horizon/tests/join-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-003] JOIN_SURFACE_interrupt_and_fork_error_are_natural_language_only` |
| PARTICIPANT-HORIZON-004 | `requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_completed_natural_language_and_work_record`；`requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_failed_natural_language_consequence`；`requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_abandoned_natural_language`；`requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_completed_managed_agent_name_and_raw_resolve` |
| PARTICIPANT-HORIZON-005 | `requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_batch_pty_exit_code_observation`；`requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_batch_pty_failure_output_observation`；`requirements/participant-horizon/tests/join-result-renderer.test.mjs::WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_completed_pty_exit_observation` |
| PARTICIPANT-HORIZON-006 | `requirements/participant-horizon/tests/admission-law.test.mjs::WHAT[PARTICIPANT-HORIZON-006] PH_exec_030_internal_machine_state_renders_as_consequence_not_dto` |
| PARTICIPANT-HORIZON-007 | `requirements/participant-horizon/tests/admission-law.test.mjs::WHAT[PARTICIPANT-HORIZON-007] PH_agent_008_internal_participants_absent_from_provider_visible_surfaces` |
| PARTICIPANT-HORIZON-008 | `requirements/participant-horizon/tests/admission-law.test.mjs::WHAT[PARTICIPANT-HORIZON-008] PH_glory_002_030_manager_surface_hides_review_orchestration` |
| PARTICIPANT-HORIZON-009 | `requirements/participant-horizon/tests/fork-tool.test.mjs::WHAT[PARTICIPANT-HORIZON-009] FORK_manager-unavailable_is_denied_generically` |
| PARTICIPANT-HORIZON-010 | `requirements/participant-horizon/tests/admission-law.test.mjs::WHAT[PARTICIPANT-HORIZON-010] PH_agent_009_fork_visible_set_is_exactly_the_five_forkable_offices` |
| PARTICIPANT-HORIZON-011 | `requirements/participant-horizon/tests/horizon-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_shows_only_each_visible_subagent_latest_work_record`；`requirements/participant-horizon/tests/horizon-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_says_when_visible_subagent_has_no_work_record`；`requirements/participant-horizon/tests/horizon-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_does_not_fall_back_when_latest_work_record_is_unreadable`；`requirements/participant-horizon/tests/horizon-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_has_no_polling_or_background_wait_primitive`；`requirements/participant-horizon/tests/horizon-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-011] HORIZON_abandoned_child_remains_visible_until_join_retires_it` |
| PARTICIPANT-HORIZON-012 | `requirements/participant-horizon/tests/warm-start-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-012] warm_start_keywords_entry_restricted_to_repository_evidence_roles` |
| PARTICIPANT-HORIZON-013 | `requirements/participant-horizon/tests/warm-start-surface.test.mjs::WHAT[PARTICIPANT-HORIZON-013] warm_start_material_is_labelled_orientation_data_not_instruction` |
| PARTICIPANT-HORIZON-014 | `requirements/participant-horizon/tests/fork-tool.test.mjs::WHAT[PARTICIPANT-HORIZON-014] FORK_unknown_calling_does_not_expose_machine_binding_affordance` |

## GAP

- `GAP-028`（CLOSED）：Horizon 已改用独立 `HandleProjection.horizonVisible`，父级可见 `Abandoned` 在 Join 消费并 `Retired` 前持续留在 roster；fork 首 prompt 的 `AcceptanceUnknown` 由 durable PromptAuthority `Pending` claim 接管恢复，保留 terminal observer 与单次物理发送，不再合成 `HandleCompleted` 或返回“未放置”。`horizon-surface.test.mjs`、`host-fork-restart-lifecycle.test.mjs` 与真实 `fork-tool.test.mjs` 回归均已绿；核心实现落于 `2953a0978`。
