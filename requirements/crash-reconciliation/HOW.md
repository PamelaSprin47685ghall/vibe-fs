# crash-reconciliation — HOW

## 架构与核心机制

### 纯恢复代数与决策

- **Family Recovery**：通过 `validateClosurePure` 验证闭包无环，结合各节点观察聚合恢复状态（Blocked > Waiting > Recovered），生成私有的 `FamilyRecoveryPermit`。
- **Child 恢复决策**：基于 durable 记录与 Host 快照，按照严格优先级依次判定为 RecoveredAbandoned、RecoveredTerminal、RecoveryIncomplete 或 RecoveryBlocked。
- **Completion 单一拥有者**：HandleController 负责原子记录完成态与 Pulse 唤醒，通过 retire 标记确保幂等性。

### 显式续传机制（`/continue`）

1. **Command 注册与捕获**：注册 `/continue` 命令，在 `command.execute.before` 阶段查询 durable 关联与 physical snapshot，生成 disclosure briefing。
2. **Materialize 与 Binding**：在真实的 `chat.message` 中将 briefing 注入为带有专用 marker 的 visible user part，并绑定到精确的 `(SessionId, PhysicalUserMessageId)`。
3. **Disclosure-Only 抑制**：命中显式续传 binding 的请求跳过普通的 business routing、Persona 强制与自动 continuation，仅执行基础的 wire sanitization，确保业务效果完全由 LLM 后续显式 tool call 驱动。

### 外部 effect reconciliation registry

`scripts/checks/external-effect-contracts.json` 是闭合的 12 项高价值 effect census；每行同时记录 owner/WHAT、四阶段 typed contract、物理 identity、有限歧义与安全重试律、普通 CE 重入点及分级 proof portfolio。`scripts/checks/external-effect-reconciliation.mjs` 复用 canonical requirement-trace parser 解析 WHAT 与 executable test title，精确核对 source symbol/proof anchor，并对重入源扫描隐藏 recovery program counter。Admission 仅可为 process-local 或明确不适用；registry 不保存 capability，也不读取 feature history 来发明恢复位置。

## 依赖关系

DEPENDS ON:
- `durable-events`
- `effect-accounting`
- `structured-workflow`
- `host-boundary`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CRASH-001 | `requirements/crash-reconciliation/tests/quiescence-surface.test.mjs::WHAT[CRASH-001] Q07_restart_gate_holds_no_permit` |
| CRASH-002 | `requirements/crash-reconciliation/tests/child-recovery-workflow.test.mjs::WHAT[CRASH-002] VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses` |
| CRASH-003 | `requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs::WHAT[CRASH-003] unknown_effect_without_quiescence_is_not_replayed` |
| CRASH-004 | `requirements/crash-reconciliation/tests/session-recovery-family.test.mjs::WHAT[CRASH-004] RECOVERY_FAMILY_dsl_module_and_private_permit_exist` |
| CRASH-005 | `requirements/crash-reconciliation/tests/child-recovery-workflow.test.mjs::WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable` |
| CRASH-006 | `requirements/crash-reconciliation/tests/session-recovery-family.test.mjs::WHAT[CRASH-006] RECOVERY_FAMILY_authorize_ready_issues_private_permit` |
| CRASH-007 | `requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs::WHAT[CRASH-007] turn_unknown_is_snapshot_observation_not_turn_outcome` |
| CRASH-008 | `requirements/crash-reconciliation/tests/quiescence-surface.test.mjs::WHAT[CRASH-008] ESC_P0_2_operator_abort_revokes_unconsumed_idle_permit` |
| CRASH-009 | `requirements/crash-reconciliation/tests/join-clean-break-recovery.test.mjs::WHAT[CRASH-009] P0_CLEAN_BREAK_delayed_recovery_before_ready_no_aborted_join_then_true_terminal` |
| CRASH-010 | `requirements/crash-reconciliation/tests/child-recovery-workflow.test.mjs::WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live` |
| CRASH-011 | `requirements/crash-reconciliation/tests/host-fork-runtime-permit.test.mjs::WHAT[CRASH-011] HFRT_join_with_valid_permit_passes_validation` |
| CRASH-012 | `requirements/crash-reconciliation/tests/child-recovery-workflow.test.mjs::WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner` |
| CRASH-013 | `requirements/crash-reconciliation/tests/session-recovery-combine.test.mjs::WHAT[CRASH-013] RECOVERY_COMBINE_blocked_dominates` |
| CRASH-014 | `requirements/crash-reconciliation/tests/recovery-closure-permit.test.mjs::WHAT[CRASH-014] CRASH_CLOSURE_validate_accepts_unique_sessions_and_keeps_order` |
| CRASH-015 | `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs::WHAT[CRASH-015] HFR_restart_multiple_children_recovered_in_link_order` |
| CRASH-016 | `requirements/crash-reconciliation/tests/blogger-crash-recovery.test.mjs::WHAT[CRASH-016] C5_classify_open_request_window_A_unsent` |
| CRASH-017 | `requirements/crash-reconciliation/tests/blogger-crash-recovery.test.mjs::WHAT[CRASH-017] C5_crash_recovery_library_is_not_wired_into_ordinary_plugin_lifecycle` |
| CRASH-018 | `requirements/crash-reconciliation/tests/explicit-continue.test.mjs::WHAT[CRASH-018] CRASH_018_real_command_material_materializes_briefing_and_stays_disclosure_only` |
| CRASH-019 | `requirements/crash-reconciliation/tests/external-effect-reconciliation.test.mjs::external_effect_registry_accepts_the_closed_12_row_contract` |
