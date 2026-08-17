# degeneration-guard — 测试落点

运行命令：单文件 `node --test requirements/degeneration-guard/tests/<file>.test.mjs`；整包被
`node requirements/verification-system/tests/run.mjs` 自动发现。落点类型：MOVE = 从旧 `tests/unit`
物理移入本包；REUSE = 留在原处；NEW = 本包新写。

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| DG-001 低多样性 loop vs 正常多样输出 | `tests/loop-detector.test.mjs`：`LOOP_003_single_token_repetition_converges_to_theoretical_loop`、`LOOP_003_diverse_programmatic_text_stays_normal`、Markdown table / ASCII graph 两个 normal fixture | MOVE+NEW | `node --test requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-002 传感器只吃 text delta、不写业务事实 | `tests/loop-sensor.test.mjs`：`LOOP_002_sensor_observes_text_delta_only`、`LOOP_007_reasoning_deltas_are_ignored`；`tests/loop-detector.test.mjs`：`LOOP_009_text_delta_decodes_fail_closed` | MOVE | 各文件 `node --test` |
| DG-003 token weighted-distinct 指标 | `tests/loop-detector.test.mjs`：fresh prior、o200k exact step/reference score、whitespace/punctuation 也是 token、single-token loop、diverse programmatic、Markdown table、ASCII graph；`tests/loop-detector-memory.test.mjs`：单次越阈无 latch / 无 consecutive-hit 要求 | MOVE+NEW | 各文件 `node --test` |
| DG-004 固定参数 + 全仓滴定 | `tests/loop-calibration.test.mjs`：Git tracked+unignored strict UTF-8 全文；当前非空行 token p99=57 → half-life=64；正常端=全文最低 weighted-distinct；异常端=理论 1；threshold=中线。当前统计基线随语料派生，feel free to modify；`tests/loop-detector.test.mjs` 校验 production 常量关系 | NEW | `node --test requirements/degeneration-guard/tests/loop-calibration.test.mjs requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-005 O(1) 更新与 vocabulary-bounded 内存 | `tests/loop-detector-memory.test.mjs`：`LOOP_005_detector_memory_is_bounded_by_tokenizer_vocabulary_not_stream_length`；`tests/loop-detector.test.mjs`：reference recurrence exactness | NEW | 各文件 `node --test` |
| DG-006 生命周期绑定单次 ProviderRun | `tests/loop-sensor.test.mjs`：`LOOP_006_reset_detector_preserves_loop_kill_armed`；`tests/loop-detector-memory.test.mjs`：`LOOP_005_two_detectors_are_independent_attempts` | MOVE+NEW | 各文件 `node --test` |
| DG-007 命中只停止当前物理 attempt、恰好一次 | `tests/loop-sensor.test.mjs`：`LOOP_006_owned_low_diversity_stream_aborts_exactly_once`、`LOOP_006_unowned_session_never_aborts`、`LOOP_006_clear_armed_allows_next_attempt_to_arm_again` | MOVE | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-008 LoopKillArmed 进程内局部 | `tests/loop-sensor.test.mjs`：`LOOP_001_kill_arm_is_process_local_not_persisted` | MOVE | 同上 |
| DG-009 强杀桥接标准 recovery | `tests/loop-sensor.test.mjs`：`LOOP_006_armed_abort_bridges_to_fallback_advance_once`、`LOOP_008_budget_exhaustion_is_final_and_writes_the_exhausted_fact` | MOVE | 同上 |
| DG-009 桥接静态形状 | `tests/p0-recovery-join-bridge-shape.test.mjs`：`P0_RECOVERY_JOIN_GATE_*` | REUSE | `node --test requirements/degeneration-guard/tests/p0-recovery-join-bridge-shape.test.mjs` |
| DG-010 作用域与豁免 | `tests/loop-sensor.test.mjs`：`LOOP_007_unowned_and_armed_deltas_are_ignored`（非 Owned session / 已武装同 attempt 忽略）、`LOOP_006_unowned_session_never_aborts` | MOVE | 同上 sensor |
| DG-011 continuation 独立叶子 | `tests/loop-sensor.test.mjs`：`LOOP_006_continuation_text_is_the_english_loop_nudge` | MOVE | 同上 sensor |
| DG-012 detector 不是业务 truth / retry controller | `tests/loop-sensor.test.mjs`：`LOOP_008_loop_kill_advances_cursor_only_via_fallback_controller`（FallbackController 唯一推进路径、不直接改 Offset）；`requirements/context-compression/tests/ctx014.test.mjs`：loop-kill 只允许 `weighted_distinct_token_count` 等诊断字段 | MOVE+REUSE | 各文件 `node --test` |

## 包拥有的 semantic anchor id

`scripts/checks/semantic-anchors.mjs` 无本包语义 ID；本包为空清单。

## 独立变化边界

未来可替换 detector 算法与滴定常量，但 attempt-local、bounded、非权威、一次越阈、复用标准 recovery
五条边界（DG-005/006/007/009/012）不得削弱。算法变化必须同步更新 WHAT/HOW 与永久 calibration proof。
