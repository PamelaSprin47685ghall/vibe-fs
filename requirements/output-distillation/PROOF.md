# output-distillation — 证明落点

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| DISTILL-001 大输出诚实压缩、不静默截断 | `tests/distiller-fragment-humility.test.mjs`（失败 → 承认不完整而非报成功）；`tests/executor-summarize.test.mjs` `DISTILLATION_distill_fragment_prompt_is_plain_intent` | NEW+MOVE | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-002 保留改变 judgment 的事实 | anchor `distinguishing`（双语言命中）；`tests/unit/process/large-gate.test.mjs`（预算合同交叉） | REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-003 fragment 谦逊 ≠ 整体成功 | `tests/distiller-fragment-humility.test.mjs`（summary 命中 `/Condensation incomplete\|Most recent raw output/`、不含 `summary-for-<failedId>`、`cancelled.length≥1`）；anchor `fragment-humility` | NEW | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-004 合并不发明因果/成功率 | anchors `merge-conflicts` / `no-invented-causality`（双语言命中）；`tests/distiller-fragment-humility.test.mjs`（失败者被诚实保留） | NEW+REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-005 对未见原文 reader 可用 | `tests/distiller-fragment-humility.test.mjs`（verbatim 含 marker）；anchor `locatable-to-unseen-reader` | NEW | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-006 失败路径 = partial + raw tail | `tests/distiller-fragment-humility.test.mjs`；`tests/executor-summarize.test.mjs`（`EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await`、`EXEC_distill_spool_await_not_found_hard_fail_collects_failure`、`EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`） | NEW+MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-007 chunked map + online reduce + cancelOwned | `tests/executor-summarize.test.mjs`（`EXEC_distill_spool_targeted_await_one_call_per_agent_no_stash`、`EXEC_distill_spool_targeted_await_out_of_order_returns_own_agent`、`EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await`） | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-008 每 chunk 定向 await 一次；permit 分型 | `tests/executor-summarize.test.mjs`（`EXEC_distill_spool_family_waiting_waits_for_readiness_before_one_fresh_permit_check`——call order `[permit, readiness, permit]`；`EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`） | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-009 Distiller 私有 runtime | `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` `EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible` | REUSE（SPLIT：hidden handle→`managed-session-lifecycle`） | `node --test requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| DISTILL-010 Distiller 不执行/不裁决 | anchor `no-execution`（distiller 组双语命中）；`tests/unit/tools/executor-tool.test.mjs`（RUN_* 与 distill 边界，见 process-execution PROOF.md） | REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-011 Large Gate 预算合同 | `tests/unit/process/large-gate.test.mjs`（`VERIFY_009_large_gate_first_acquire_succeeds_immediately`、`VERIFY_009_large_gate_second_acquire_waits_until_release`、cancelable waiters）；`tests/unit/process/process-runner.test.mjs` `EXEC_011_large_estimate_acquires_and_releases_the_gate` | REUSE | `node --test tests/unit/process/large-gate.test.mjs tests/unit/process/process-runner.test.mjs` |
| DISTILL-012 确定性留尾截断 | `tests/unit/plugin/tool-host-codec-full.test.mjs`（ToolResultBound 面） | REUSE（SPLIT：wire 渲染→`provider-projection`） | `node --test tests/unit/plugin/tool-host-codec-full.test.mjs` |
| DISTILL-013 不返回 chunk 统计仪表盘 | `tests/executor-summarize.test.mjs` `DISTILLATION_prompts_carry_no_chunk_index_or_level` | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs` |

## 移动/新写文件清单

| 源 | 目标 | 类型 | 结果 |
|----|------|------|------|
| `requirements/output-distillation/tests/executor-summarize.test.mjs` | `requirements/output-distillation/tests/executor-summarize.test.mjs` | MOVE | `node --test` 绿（9 断言） |
| — | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` | NEW（Oracle 2） | `node --test` 绿（1 断言） |

## SPLIT@cutover 清单（REUSE 文件拆分计划）

- `tests/unit/process/large-gate.test.mjs`：整文件归本包（DISTILL-011）；fable_modules import 是
  test-boundary 门 grandfathered 行，移动会红 → 留原处。
- `tests/unit/process/process-runner.test.mjs`：`EXEC_011_large_estimate_acquires_and_releases_the_gate`
  归本包；run/spawn/kill 断言 → `process-execution`；estimate 拒绝 → `time-capability`。
- `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`：Distiller 私有 runtime 语义归本包（DISTILL-009）；
  `HostOwnedHidden` handle → `managed-session-lifecycle`；Assignment 过滤 → `participant-horizon`。
- `tests/unit/plugin/tool-host-codec-full.test.mjs`：ToolResultBound 截断断言归本包；Semantic/Wire
  codec 断言 → `provider-projection`。
- `tests/unit/tools/executor-tool.test.mjs`：distill 面（`ExecutorTool` 中 distill 工具）归本包；
  RUN_* → `process-execution`；tool registry → `capability-enforcement`。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.distiller`（全部 5 个）：`distinguishing`、`fragment-humility`、`merge-conflicts`、
`locatable-to-unseen-reader`、`no-invented-causality`。
