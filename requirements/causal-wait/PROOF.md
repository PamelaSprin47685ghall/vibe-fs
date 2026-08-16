# PROOF — causal-wait（测试落点表）

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（已物理移入本包 `tests/`）/ `REUSE`（留在原处，记录 cutover 拆分）/ `NEW`（本包新写）。
> 运行命令：`node --test <file>` 单跑；`node requirements/verification-system/tests/run.mjs` 全单元（自动包含 `requirements/**/tests/*.test.mjs`）；`node scripts/check.mjs` 全部静态门。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点，均带 `WHAT[<ID>]` 前缀） | 类型 | 运行命令 |
|---|---|---|---|
| CAUSAL-001 观测非权威 | `tests/causal-wait.test.mjs` — `RED_8_application_observer_enter_only_snapshot_via_reader`（observer/reader 分面）+ `tests/reconcile-idle-observation-non-authoritative.test.mjs` — `EXEC_reconcile_idle_before_transcript_materializes_within_causal_rereads`（idle 观测不构成 turn，非权威消费面）+ REUSE：`scripts/checks/causal-wait-boundary.mjs`（六条静态扫描；经 check.mjs 运行，可红） | MOVE + NEW + REUSE | `node --test requirements/causal-wait/tests/causal-wait.test.mjs requirements/causal-wait/tests/reconcile-idle-observation-non-authoritative.test.mjs`；`node scripts/check.mjs` |
| CAUSAL-002 跨 owner 等待可诊断 | `tests/causal-wait.test.mjs` — `RED_1_active_wait_visible_after_enter`（owner/producer/kind 可见）+ `tests/wait-lifecycle.test.mjs` — `descriptor_carries_typed_owner_producer_subject` | MOVE + NEW | `node --test requirements/causal-wait/tests/causal-wait.test.mjs requirements/causal-wait/tests/wait-lifecycle.test.mjs` |
| CAUSAL-003 观测不进 Journal/决策 | `tests/boundary-observation.test.mjs` — `Journal codec surfaces stay free of the causal-wait vocabulary` / `Fact codec surface stays free of the causal-wait vocabulary` / `diagnostics snapshot stays out of decision and prompt paths`（本包 NEW，pin gate 第 3/4 条同一事实的最小可执行子集）+ REUSE：`scripts/checks/causal-wait-boundary.mjs` 第 3/4/5 条（Fact/Journal codec 干净、诊断不进 PromptDispatcher/TurnCompletionProgram、关键迁移点无裸 TCS.Task await） | NEW + REUSE（gate 不可移动） | `node --test requirements/causal-wait/tests/boundary-observation.test.mjs`；`node scripts/check.mjs` |
| CAUSAL-004 Reader/Writer 类型隔离 | `tests/wait-lifecycle.test.mjs` — `observer_surface_has_no_snapshot`（`IWaitObserver` 无 Snapshot 成员）+ `tests/causal-wait.test.mjs` — `RED_8_application_observer_enter_only_snapshot_via_reader`（reader 经 CausalWaitHub 读）+ REUSE：gate 第 1/2 条（Domain/Application 边界） | MOVE + NEW + REUSE | `node --test requirements/causal-wait/tests/wait-lifecycle.test.mjs requirements/causal-wait/tests/causal-wait.test.mjs`；`node scripts/check.mjs` |
| CAUSAL-005 event-driven 优先 polling | `tests/until-signal-or-deadline.test.mjs` — `THEOREM_untilSignalOrDeadline_returns_immediately_when_tryRead_ready` + `THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline` + `THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline`（无 slice timer/轮询间隔，真实信号 re-arm；SPLIT@cutover：CausalAwait 词汇归本包，deadline 能力归 time-capability） | REUSE（SPLIT@cutover） | `node --test requirements/causal-wait/tests/until-signal-or-deadline.test.mjs` |
| CAUSAL-006 取消/完成后观测终止 | `tests/causal-wait.test.mjs` — `RED_2_resolve_clears_active_and_records_resolved` / `RED_3_fail_clears_active_and_records_failed` / `RED_4_cancel_clears_active_and_records_cancelled` / `RED_4_cancel_message_also_classifies_as_cancelled` / `history_capacity_bounds_ring_buffer` + `tests/wait-lifecycle.test.mjs` — `dispose_defaults_to_wait_disposed` / `mark_exit_then_dispose_preserves_exit` / `repeated_mark_exit_last_one_wins` / `dispose_is_idempotent_single_leave` / `reenter_is_fresh_observation_not_revival` / `history_default_capacity_is_256` + `tests/escape-taxonomy.test.mjs` — `wait_escape_has_five_typed_cases` / `escapes_render_distinctly_in_diagnostics` / `deadline_escape_carries_typed_instant` | MOVE + NEW | `node --test requirements/causal-wait/tests/causal-wait.test.mjs requirements/causal-wait/tests/wait-lifecycle.test.mjs requirements/causal-wait/tests/escape-taxonomy.test.mjs` |
| CAUSAL-007 frontier 纯诊断解释 | `tests/causal-frontier.test.mjs` — `RED_5_nested_graph_walks_to_external_frontier` / `RED_6_missing_producer_reports_broken_causal_edge` / `RED_7_cycle_reports_without_hanging` / `empty_snapshot_yields_empty_frontier` | MOVE | `node --test requirements/causal-wait/tests/causal-frontier.test.mjs` |
| CAUSAL-008 process-local、重启安全消失 | `tests/wait-lifecycle.test.mjs` — `fresh_registry_starts_empty_no_durable_state`（新 registry 无任何状态）+ `tests/causal-wait-bridge.test.mjs` — `CAUSAL_BRIDGE_writeSnapshot_overwrites_workspace_json`（诊断文件 git-excluded、非 Journal）/ `CAUSAL_BRIDGE_hub_refreshes_file_on_enter` + REUSE：`requirements/verification-system/tests/causal-diagnostics.test.mjs` — `gather reads causal waits file`（诊断文件 git-excluded、非 Journal） | MOVE + NEW + REUSE | `node --test requirements/causal-wait/tests/wait-lifecycle.test.mjs requirements/causal-wait/tests/causal-wait-bridge.test.mjs`；`node --test requirements/verification-system/tests/causal-diagnostics.test.mjs` |

## 关联 REUSE 落点（边界消费方，不重复拥有）

| 场景 | 落点 | owner |
|---|---|---|
| Escape 显式终止路径（CCE-005 渲染） | `tests/escape-taxonomy.test.mjs`（本包 NEW）— WaitEscape 五 case 全区分、bridge JSON tag 全区分 | 本包 |
| Scheme B 桥 + E2E 诊断首屏 | `requirements/verification-system/tests/causal-diagnostics.test.mjs`（`CAUSAL_DIAG_format_puts_frontier_before_e2e_events` 等） | 本包（bridge 面）+ `verification-system`（format/watchdog harness）SPLIT@cutover |
| E2E watchdog 因果续期（`renewOn`） | `requirements/verification-system/tests/integration/harness/timeout-cases.mjs`、`requirements/verification-system/tests/e2e/support/scenario-schema.js`（waitFactRenewOnProblems 校验） | `verification-system`（消费 CAUSAL-001） |
| QuiescencePermit 观测非权威 | `requirements/host-boundary/tests/reconcile-idle-early.test.mjs`、`tests/unit/host/**`（host-boundary 的 machinery） | `host-boundary` + 本包（非权威面） |

## 运行与红/绿判读

- 单跑：`node --test requirements/causal-wait/tests/<file>`。任一断言失败 → 该命题的当前世界 RED。
- 全单元：`node requirements/verification-system/tests/run.mjs`（自动包含 `requirements/causal-wait/tests/**`）。
- 静态门：`node scripts/check.mjs`（`causal-wait-boundary.mjs` 是本包语义的静态 enforcement）。

## SPLIT@cutover 清单（本轮 REUSE，cutover 时拆分）

1. `requirements/causal-wait/tests/until-signal-or-deadline.test.mjs` → CausalAwait 词汇断言迁本包；deadline 能力断言迁 `time-capability`。
2. `requirements/verification-system/tests/causal-diagnostics.test.mjs` → bridge 文件/registry 断言迁本包；`formatDiagnostics`/`formatCausalSection`/watchdog onTimeout 断言迁 `verification-system`。
3. `scripts/checks/causal-wait-boundary.mjs` → cutover 后作为本包静态 gate 保留（文件位置是否移动由 requirement-system 布局裁决）。

## Semantic anchor ids

本包在 `scripts/checks/semantic-anchors.mjs` 中**不拥有**任何 semantic ID（该 catalog 的 owner 为 cognitive-environment / office-capability / action-affordance / epistemic-reasoning / review-judgement）。本包的语义 proof 是行为测试 + `causal-wait-boundary.mjs` 静态门，不是 prompt 散文锚点。
