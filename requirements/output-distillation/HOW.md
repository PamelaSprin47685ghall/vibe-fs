# output-distillation — HOW

## 架构机制

### 蒸馏管线与失败降级

1. **单窗口截断**：消费流式落盘的 `spool` 文件时只保留最近一个 `Spool.ChunkSizeBytes`（200 KiB）窗口。前面的窗口读过即丢，不进入数组，不创建 map task。若读到第二个窗口，记录 `truncated=true`。
2. **单 Distiller**：非空 spool 只启动一个私有 fast Distiller，payload 仅为 bounded tail。成功且 `truncated=true` 时，在摘要外层加入明确的“更早输出已截断”声明。不存在 merge prompt、reduce fan-in 或层级 Distiller。
3. **失败降级策略**：唯一 Distiller 失败或超时时，按 owned agent id 幂等执行一次 `CancelAgent`，返回 `condensation-failed` + 同一 bounded raw tail，拒绝虚构成功。
4. **叶子运行时生命周期**：Distiller 子会话被标记为 `HostOwnedHidden`，生命周期由宿主管控；Companion eligibility 对 Distiller 返回 false，因此该子会话不会再派生 Blogger。

### 大输出门禁与确定性留尾截断

- **Large Gate 互斥**：对于估算产出大体积日志的进程，执行前必须获取单持有者的 `LargeGate`。未获取到门禁的执行请求在 FIFO 队列中排队，确保全系统同一时刻仅有一个大输出流占用内存与分析资源。
- **留尾截断（ToolResultBound）**：插件工具回传长文本时，在达到宿主全局限制前完成留尾截断。注入固定的截断提示标记并优先保留最新的完整尾部行，消除宿主默认头部截断导致最新日志丢失的不确定性。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DISTILL-001 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs::WHAT[DISTILL-001] truncation_produces_nonempty_bounded_observation` |
| DISTILL-002 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs::WHAT[DISTILL-002] bounded_tail_keeps_recent_judgment_changing_marker` |
| DISTILL-003 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs::WHAT[DISTILL-003] truncated_tail_is_explicitly_not_the_whole_run` |
| DISTILL-004 | `requirements/output-distillation/tests/executor-summarize.test.mjs::WHAT[DISTILL-004] EXEC_distill_spool_never_fans_out_or_reduces_when_spool_grows` |
| DISTILL-005 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs::WHAT[DISTILL-005] unseen_reader_gets_locator_plus_visible_truncation_boundary` |
| DISTILL-006 | `requirements/output-distillation/tests/executor-summarize.test.mjs::WHAT[DISTILL-006] EXEC_distill_spool_failure_returns_bounded_raw_tail_and_cancels_once`；`requirements/output-distillation/tests/reconcile-supervisor-distill.test.mjs::WHAT[DISTILL-006] EXEC_distillation_cancel_single_owned_distiller_once_on_failure` |
| DISTILL-007 | `requirements/output-distillation/tests/executor-summarize.test.mjs::WHAT[DISTILL-007] EXEC_distill_spool_keeps_only_latest_200kib_payload` |
| DISTILL-008 | `requirements/output-distillation/tests/executor-summarize.test.mjs::WHAT[DISTILL-008] EXEC_distill_spool_waiting_rechecks_same_exact_agent_after_readiness` |
| DISTILL-009 | `requirements/output-distillation/tests/distiller-role-contract.test.mjs::WHAT[DISTILL-009] distiller_is_private_leaf_runtime_without_public_target_or_blogger_companion` |
| DISTILL-010 | `requirements/output-distillation/tests/distiller-role-contract.test.mjs::WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface` |
| DISTILL-011 | `requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_first_acquire_succeeds_immediately`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_second_acquire_waits_until_release`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_release_without_holder_is_noop`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_waiters_are_served_fifo`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_cancelled_waiter_is_skipped`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_precancelled_token_is_rejected_immediately`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_cancellation_observed_by_gate`；`requirements/output-distillation/tests/large-gate.test.mjs::WHAT[DISTILL-011] VERIFY_009_large_gate_acquire_after_release_reenters_cleanly` |
| DISTILL-012 | `requirements/output-distillation/tests/tool-host-codec-full.test.mjs::WHAT[DISTILL-012] CODEC_register_applies_tool_with_uncurried_execute_and_bounds_result` |
| DISTILL-013 | `requirements/output-distillation/tests/executor-summarize.test.mjs::WHAT[DISTILL-013] DISTILLATION_prompt_has_no_chunk_or_reduce_instrumentation` |
