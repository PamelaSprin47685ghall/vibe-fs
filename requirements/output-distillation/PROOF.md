# output-distillation — 证明落点

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| DISTILL-001 大输出诚实压缩、不静默截断 | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-001] fragment_humility_compression_is_lossy_but_honest_not_a_silent_empty_success`（失败 → 承认不完整而非报成功）；`tests/executor-summarize.test.mjs` `WHAT[DISTILL-001] DISTILLATION_distill_fragment_prompt_is_plain_intent` | NEW+MOVE | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-002 保留改变 judgment 的事实 | anchor `distinguishing`（双语言命中）；`tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-002] fragment_humility_keeps_the_judgment_changing_distinguishing_marker`（marker 保留）；`requirements/output-distillation/tests/large-gate.test.mjs`（预算合同交叉） | REUSE+NEW | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-003 fragment 谦逊 ≠ 整体成功 | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-003] fragment_humility_admits_fragment_boundary_and_never_fabricates_failed_summary`（summary 命中 `/Condensation incomplete\|Most recent raw output/`、不含 `summary-for-<failedId>`）；anchor `fragment-humility` | NEW | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-004 合并不发明因果/成功率 | anchors `merge-conflicts` / `no-invented-causality`（双语言命中）；`tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-004] fragment_humility_failed_fragment_is_not_outvoted_by_quiet_chunks`（失败者被诚实保留而非投票消除）；`tests/executor-summarize.test.mjs` `WHAT[DISTILL-004] DISTILLATION_merge_distillations_prompt_is_plain_intent` | NEW+REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-005 对未见原文 reader 可用 | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-005] fragment_humility_raw_tail_keeps_the_locator_for_an_unseen_reader`（verbatim 含 marker）；anchor `locatable-to-unseen-reader` | NEW | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-006 失败路径 = partial + raw tail | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-006] fragment_humility_failed_second_chunk_keeps_raw_tail_and_admits_incompleteness`；`tests/executor-summarize.test.mjs`（`WHAT[DISTILL-006] EXEC_distill_spool_await_not_found_hard_fail_collects_failure`、`WHAT[DISTILL-006] EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`） | NEW+MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-007 chunked map + online reduce + cancelOwned | `tests/executor-summarize.test.mjs`（`WHAT[DISTILL-007] EXEC_distill_spool_targeted_await_one_call_per_agent_no_stash`、`WHAT[DISTILL-007] EXEC_distill_spool_targeted_await_out_of_order_returns_own_agent`、`WHAT[DISTILL-007] EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await`）；`tests/reconcile-supervisor-distill.test.mjs` `WHAT[DISTILL-007] EXEC_distillation_cancel_owned_on_failure` | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/reconcile-supervisor-distill.test.mjs` |
| DISTILL-008 每 chunk 定向 await 一次；permit 分型 | `tests/executor-summarize.test.mjs`（`WHAT[DISTILL-008] EXEC_distill_spool_family_waiting_waits_for_readiness_before_one_fresh_permit_check`——call order `[permit, readiness, permit]`；`WHAT[DISTILL-006] EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`） | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-009 私有 runtime | `tests/distiller-role-contract.test.mjs` `WHAT[DISTILL-009] distiller_is_private_internal_runtime_not_a_public_fork_or_horizon_target`（`DistillationSurface` 返回私有角色、固定 fast-distiller identity）；`requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` `EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible` | NEW+REUSE（surface→本包；hidden handle→managed-session-lifecycle） | `node --test requirements/output-distillation/tests/distiller-role-contract.test.mjs requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| DISTILL-010 Distiller 不执行/不裁决 | anchor `no-execution`（distiller 组双语命中）；`tests/distiller-role-contract.test.mjs` `WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface`（`DistillationSurface` 零权限、run 为唯一执行面）；`requirements/process-execution/tests/executor-tool.test.mjs`（RUN_* 与 distill 边界） | NEW+REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-011 Large Gate 预算合同 | `requirements/output-distillation/tests/large-gate.test.mjs`（`WHAT[DISTILL-011] VERIFY_009_large_gate_first_acquire_succeeds_immediately`、`WHAT[DISTILL-011] VERIFY_009_large_gate_second_acquire_waits_until_release`、cancelable waiters 组）；`requirements/output-distillation/tests/large-gate-runner.test.mjs` `WHAT[DISTILL-011] EXEC_011_large_estimate_acquires_and_releases_the_gate`；`requirements/process-execution/tests/process-runner.test.mjs` `EXEC_011_large_estimate_acquires_and_releases_the_gate` | REUSE | `node --test requirements/output-distillation/tests/large-gate.test.mjs requirements/output-distillation/tests/large-gate-runner.test.mjs requirements/process-execution/tests/process-runner.test.mjs` |
| DISTILL-012 确定性留尾截断 | `requirements/output-distillation/tests/tool-host-codec-full.test.mjs`（ToolResultBound 面）`WHAT[DISTILL-012] CODEC_register_applies_tool_with_uncurried_execute_and_bounds_result` | REUSE（SPLIT：wire 渲染→`provider-projection`） | `node --test requirements/output-distillation/tests/tool-host-codec-full.test.mjs` |
| DISTILL-013 不返回 chunk 统计仪表盘 | `tests/executor-summarize.test.mjs` `WHAT[DISTILL-013] DISTILLATION_prompts_carry_no_chunk_index_or_level`；`tests/executor-tool.test.mjs` `WHAT[DISTILL-013] RUN_spooled_output_runs_distillation_without_chunk_statistics`（wire 无 chunk_count/total_bytes/spool_path） | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/executor-tool.test.mjs` |

## 移动/新写文件清单

| 源 | 目标 | 类型 | 结果 |
|----|------|------|------|
| — | `src/Wanxiangshu/OpenCode/Tools/DistillationSurface.fs` | NEW semantic owner surface | `DistillationSurface` exposes only JS-native role/privacy/permission/execution observations; role test imports it directly |
| — | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` | NEW（Oracle 2，SPLIT：DISTILL-001..006 六 test） | `node --test` 绿（6 断言） |
| — | `requirements/output-distillation/tests/distiller-role-contract.test.mjs` | NEW（DISTILL-009/010 contract test） | `node --test` 绿（2 断言） |

## SPLIT@cutover 清单（REUSE 文件拆分计划）

- `requirements/output-distillation/tests/large-gate.test.mjs`：整文件归本包（DISTILL-011）；fable_modules import 是
  test-boundary 门 grandfathered 行，移动会红 → 留原处。
- `requirements/process-execution/tests/process-runner.test.mjs`：`EXEC_011_large_estimate_acquires_and_releases_the_gate`
  归本包；run/spawn/kill 断言 → `process-execution`；estimate 拒绝 → `time-capability`。
- `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`：Distiller 私有 runtime 语义归本包（DISTILL-009）；
  `HostOwnedHidden` handle → `managed-session-lifecycle`；Assignment 过滤 → `participant-horizon`。
- `requirements/output-distillation/tests/tool-host-codec-full.test.mjs`：ToolResultBound 截断断言归本包；Semantic/Wire
  codec 断言 → `provider-projection`。
- `requirements/process-execution/tests/executor-tool.test.mjs`：distill 面（`ExecutorTool` 中 distill 工具）归本包；
  RUN_* → `process-execution`；tool registry → `capability-enforcement`。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.distiller`（全部 5 个）：`distinguishing`、`fragment-humility`、`merge-conflicts`、
`locatable-to-unseen-reader`、`no-invented-causality`。
