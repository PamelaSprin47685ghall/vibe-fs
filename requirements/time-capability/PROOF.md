# PROOF — time-capability（测试落点表）

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（已物理移入本包 `tests/`）/ `REUSE`（留在原处，记录 cutover 拆分）/ `NEW`（本包新写）。
> 运行命令：`node --test <file>` 单跑；`node requirements/verification-system/tests/run.mjs` 全单元（自动包含 `requirements/**/tests/*.test.mjs`）；`node scripts/check.mjs` 全部静态门。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| TIME-001 时钟/timer 显式注入 capability | `tests/timer-port.test.mjs` — `WHAT[TIME-003] VERIFY_004_virtual_timer_*`（Delay/Cancel/Dispose 契约）+ `tests/clock-port-virtual.test.mjs` — `WHAT[TIME-001] TIME_001_virtual_clocks_are_independent_not_ambient`（两个虚拟时钟互不影响） | MOVE + NEW | `node --test requirements/time-capability/tests/timer-port.test.mjs requirements/time-capability/tests/clock-port-virtual.test.mjs` |
| TIME-002 deadline/elapsed typed 表达 | `tests/deadline-typed.test.mjs` — `WHAT[TIME-002] TIME_002_deadline_of_budget_and_remaining_are_pure_clock_functions` / `WHAT[TIME-002] TIME_002_of_budget_clamps_to_datetime_max_no_overflow` / `WHAT[TIME-002] TIME_002_next_wait_ms_caps_at_js_timer_ceiling`（ofBudget/remaining/isExpired/nextWaitMs 全部经注入时钟纯函数消费；`MaxTimerWaitMs` 封顶）+ `tests/devops-join-timeout.test.mjs` — `WHAT[TIME-002] EXEC_025_join_deadline_expired_renders_waiting_ended_natural_language`（EXEC-025 deadline 机制面）+ `tests/process-output-deadline.test.mjs` — `WHAT[TIME-002] EXEC_011_*`（effectiveDeadline=min(estimate, HardLimit)、非有限/非正坍缩、DefaultHardLimit=1h）+ `tests/process-runner-estimate.test.mjs` — `WHAT[TIME-002] EXEC_011_rejects_*_estimate`（无效预算 spawn 前拒绝） | NEW | `node --test requirements/time-capability/tests/deadline-typed.test.mjs requirements/time-capability/tests/devops-join-timeout.test.mjs requirements/time-capability/tests/process-output-deadline.test.mjs requirements/time-capability/tests/process-runner-estimate.test.mjs` |
| TIME-003 虚拟化；测试替换物理时钟 | `tests/timer-port.test.mjs`（`WHAT[TIME-003] VERIFY_004_virtual_timer_*` 虚拟 timer 精确触发/cancel/dispose）+ `tests/clock-port-virtual.test.mjs`（`WHAT[TIME-003] TIME_003_virtual_clock_starts_at_fixed_epoch` / `WHAT[TIME-003] TIME_003_virtual_clock_advance_and_set_are_deterministic` 虚拟时钟推进/设定） | MOVE + NEW | 同上两文件 |
| TIME-004 业务层禁 ambient 时间 | REUSE：`requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs` — `G4R_CE_S0_raw_time_scanner_RED_on_synthetic_tokens`（合成 token 必红）+ `G4R_CE_S14_production_is_clean_in_hard_phase`（生产三层无 raw time）。机制归 structured-workflow，本包消费其 guarantee + NEW：`tests/ambient-time-forbidden.test.mjs` — `WHAT[TIME-004] domain_application_session_contain_no_raw_time_tokens`（本包侧产品事实：三层扫描命中零）+ `WHAT[TIME-004] business_layer_scan_is_not_vacuous_across_a_clean_tree`（消费非盲目：合成业务层文件必红；扫描层=Domain/Application/Session） | REUSE + NEW | `node --test requirements/time-capability/tests/ambient-time-forbidden.test.mjs`；`node --test requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs` |
| TIME-005 时间值不是 authority | `tests/deadline-typed.test.mjs` — `WHAT[TIME-005] TIME_005_verdict_follows_injected_clock_not_value`（同一 deadline 两个注入时钟给出不同判定；`Deadline` 无公开时刻访问器）+ `tests/clock-port-virtual.test.mjs` — `WHAT[TIME-005] TIME_005_deadline_verdict_uses_injected_clock_view` + `tests/temporal-virtual-clock.test.mjs` — `WHAT[TIME-005] TEMPORAL_virtual_clock_*`（harness 虚拟时钟：time is input, never authority） + REUSE：`requirements/verification-system/tests/support/temporal-harness.mjs`（`One World / Pure Time`：Time is input, never authority，供 temporal 定理） | NEW + REUSE | `node --test requirements/time-capability/tests/deadline-typed.test.mjs requirements/time-capability/tests/clock-port-virtual.test.mjs requirements/time-capability/tests/temporal-virtual-clock.test.mjs` |
| TIME-006 deadline 是 causal-wait 的可选 escape | `tests/until-signal-or-deadline.test.mjs` — `WHAT[TIME-006] THEOREM_untilSignalOrDeadline_deadline_without_material_is_WaitTimedOut`（IDeadlineHandle 作为等待 escape；SPLIT@cutover：CausalAwait 词汇归 causal-wait，deadline 语义归本包） | REUSE（SPLIT@cutover） | `node --test requirements/time-capability/tests/until-signal-or-deadline.test.mjs` |
| TIME-007 首次 prompt bind-once SessionStartedAt；新 occurrence fresh elapsed | `tests/pair-session-elapsed.test.mjs` — `WHAT[TIME-007] TIME_007_*` + `WHAT[TIME-007] GD_012_*`（pure + durable bind once / bounded projection no-scan-no-mutable / clamp / 双语 human-readable / occurrence fresh + old MarkerText immutable / composition order） | NEW（FROZEN 2026-08-14） | `node --test requirements/time-capability/tests/pair-session-elapsed.test.mjs` |

## 关联 REUSE 落点（边界消费方，不重复拥有）

| 场景 | 落点 | owner |
|---|---|---|
| EXEC-025 DevOps 10s → `DeadlineExpired` 自然语言 | `requirements/participant-horizon/tests/devops-join-timeout.test.mjs`（`devops_join_deadline_renders_natural_language_not_timed_out_dto`） | 本包（deadline 机制面）+ `delegation`（join 中断面）SPLIT@cutover |
| EXEC-011 process deadline 有界、超时确定失败 | `requirements/process-execution/tests/process-runner.test.mjs`（`EXEC_011_*`）、`requirements/process-execution/tests/process-output.test.mjs`（`effectiveDeadline`） | `process-execution`（本体）+ 本包（deadline 输入） |
| join 分段等待（注入 IClockPort/ITimerPort + nextWaitMs） | `requirements/process-execution/tests/process-wait.test.mjs`、`tests/unit/session/host-fork-*.test.mjs` | `delegation` / `process-execution`（消费） |
| G4R temporal 定理（虚拟时间证明） | `requirements/time-capability/tests/temporal-virtual-clock.test.mjs` 等（harness 虚拟端口） | 各业务 owner；本包只提供虚拟时间能力 |

## 运行与红/绿判读

- 单跑：`node --test requirements/time-capability/tests/<file>`。任一断言失败 → 该命题的当前世界 RED。
- 全单元：`node requirements/verification-system/tests/run.mjs`（自动包含 `requirements/time-capability/tests/**`）。
- 静态门：`node scripts/check.mjs`（含 `causal-wait-boundary.mjs`、`test-boundary.mjs`、`g4r-ce-vocabulary.mjs` 等）。

## Semantic anchor ids

本包在 `scripts/checks/semantic-anchors.mjs` 中**不拥有**任何 semantic ID（该 catalog 的 owner 为 cognitive-environment / office-capability / action-affordance / epistemic-reasoning / review-judgement）。本包的 anchor 证据是静态 gate 扫描（g4r-ce-vocabulary）与行为测试，不是 prompt 散文锚点。
