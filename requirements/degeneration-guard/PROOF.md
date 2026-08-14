# degeneration-guard — 测试落点

运行命令：单文件 `node --test requirements/degeneration-guard/tests/<file>.test.mjs`；整包被
`node requirements/verification-system/tests/run.mjs` 自动发现。落点类型：MOVE = 从旧 `tests/unit` 物理移入本包；REUSE =
留在原处（多 owner 或共享 checker），记锚点与 cutover 拆分；NEW = 本包新写。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| DG-001 问题与非目标（低多样性循环 vs 高多样性正常） | `tests/loop-detector.test.mjs`：`LOOP_003_single_character_long_run_is_loop`、`LOOP_003_diverse_alphabet_stays_normal` | MOVE | `node --test requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-002 传感器只吃 text delta、不写业务事实 | `tests/loop-sensor.test.mjs`：`LOOP_002_sensor_observes_text_delta_only`、`LOOP_007_unowned_and_reasoning_deltas_are_ignored`；`tests/loop-detector.test.mjs`：`LOOP_009_text_delta_decodes_fail_closed`（非 text 字段 fail-closed） | MOVE | 各文件 `node --test` |
| DG-003 判定指标（先验 / 短流 / 空白忽略 / 单字符循环 / 高多样性 / 单次越阈无迟滞） | `tests/loop-detector.test.mjs`：`LOOP_003_fresh_detector_is_innocent_normal_code_prior`、`LOOP_003_fewer_than_four_characters_keeps_prior`、`LOOP_003_whitespace_and_minus_are_ignored_and_do_not_advance`、`LOOP_003_single_character_long_run_is_loop`、`LOOP_003_diverse_alphabet_stays_normal`；`tests/loop-detector-memory.test.mjs`：`LOOP_003_threshold_crossing_is_a_single_event_with_no_latch`、`LOOP_003_judgement_does_not_require_consecutive_hits` | MOVE + NEW | 各文件 `node --test` |
| DG-004 固定参数 | `tests/loop-detector.test.mjs`：`LOOP_004_constants_match_the_clause`（N=4/桶=4096/K=3/先验=256/垃圾=24/阈值=140/HHI=1/140/中点） | MOVE | `node --test requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-005 O(1) 递推与固定内存 | `tests/loop-detector.test.mjs`：`LOOP_005_streaming_matches_batch_push`、`LOOP_005_ignored_chars_do_not_form_grams_or_dilute_prior`；`tests/loop-detector-memory.test.mjs`：`LOOP_005_detector_memory_is_bounded_by_fixed_buckets_not_stream_length`（40 万字符后 Value=4096/Cross=3/Total=3/LastStep=4096/Prefix=4 不变） | MOVE + NEW | 各文件 `node --test` |
| DG-006 生命周期绑定单次 ProviderRun | `tests/loop-sensor.test.mjs`：`LOOP_006_reset_detector_preserves_loop_kill_armed`（attempt 边界重置 detector，不丢 armed 标记）；`tests/loop-detector-memory.test.mjs`：`LOOP_005_two_detectors_are_independent_attempts` | MOVE + NEW | 各文件 `node --test` |
| DG-007 命中只停止当前物理 attempt、恰好一次 | `tests/loop-sensor.test.mjs`：`LOOP_006_owned_low_diversity_stream_aborts_exactly_once`、`LOOP_006_unowned_session_never_aborts`、`LOOP_006_clear_armed_allows_next_attempt_to_arm_again` | MOVE | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-008 LoopKillArmed 进程内局部 | `tests/loop-sensor.test.mjs`：`LOOP_001_kill_arm_is_process_local_not_persisted` | MOVE | 同上 |
| DG-009 强杀桥接标准 recovery | `tests/loop-sensor.test.mjs`：`LOOP_006_armed_abort_bridges_to_fallback_advance_once`（armed abort → recordConfirmedFailure 一次、同 run 去重）、`LOOP_008_loop_kill_advances_cursor_only_via_fallback_controller`、`LOOP_008_budget_exhaustion_is_final_and_writes_the_exhausted_fact` | MOVE | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-009（桥接的静态形状） | `requirements/degeneration-guard/tests/p0-recovery-join-bridge-shape.test.mjs`：`P0_RECOVERY_JOIN_GATE_*`（lifecycle-aborted-record / record-completion-single-owner 正负模式） | REUSE | `node --test requirements/degeneration-guard/tests/p0-recovery-join-bridge-shape.test.mjs`（SPLIT@cutover：aborted≠terminal 规则归 effect-accounting） |
| DG-010 作用域与豁免 | `tests/loop-sensor.test.mjs`：`LOOP_007_unowned_and_reasoning_deltas_are_ignored`、`LOOP_006_unowned_session_never_aborts` | MOVE | 同上 sensor |
| DG-011 continuation 独立叶子 | `tests/loop-sensor.test.mjs`：`LOOP_006_continuation_text_is_the_english_loop_nudge`（loop-continue ≠ provider-retry 正文） | MOVE | 同上 sensor |
| DG-012 detector 不是业务 truth / retry controller | `tests/loop-sensor.test.mjs`：`LOOP_008_loop_kill_advances_cursor_only_via_fallback_controller`（sensor 无 Journal 句柄、不直接改 Offset） | MOVE | 同上 sensor |

## 包拥有的 semantic anchor id

`scripts/checks/semantic-anchors.mjs` 无本包语义 ID（该 catalog 只装 Role Law / office / tool
cognition anchors）；本包为空清单。

## cutover 待办（SPLIT@cutover）

1. `requirements/degeneration-guard/tests/p0-recovery-join-bridge-shape.test.mjs`：本包只 REUSE（LOOP-006 桥接的静态形状）；
   gate 本体的 aborted≠terminal 规则归 `effect-accounting`，recovery 规则归
   `crash-reconciliation`。
2. e2e（`tests/e2e/cases/fallback-aabb-trace.test.mjs` 中 loop-kill 路径）由 lead 在 cutover
   阶段归位。
3. 未来换 detector 算法时：DG-004/DG-005 的常数断言随 HOW 更新（independence change 允许），
   但 attempt-local、bounded、非权威、复用标准 recovery 四条（DG-006/005/012/009）不得削弱。
