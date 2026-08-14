# process-execution — 证明落点

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| PROC-001 终端四动词四 contract | `tests/pty-types.test.mjs`（`PTY_TYPES_pty_command_cases_carry_their_fields`——Spawn/Write/Read/Signal/Resize；`PTY_TYPES_tryParse_*`——signal 名）；`tests/pty-port.test.mjs`（`PORT_fork_*`/`PORT_send_*`/`PORT_read_*`） | MOVE | `node --test requirements/process-execution/tests/pty-types.test.mjs requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-002 command/signal 是 act、stdout 是 observation | anchors `act-vs-observation`/`signal-not-exit`/`mechanical-meaning`（devops 组，双语言命中）；`tests/pty-port.test.mjs` `PORT_send_term_kill_int_marks_abort_for_the_next_completion`、`PORT_send_plain_signal_does_not_abort_the_completion` | MOVE | `node scripts/checks/semantic-anchors.mjs`；`node --test requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-003 physical completion 只由 backend exit；kill ≠ exit | `tests/pty-port.test.mjs`（`PORT_complete_*` 组——completion 只在 complete 路径发布；`PORT_send_plain_signal_does_not_abort_the_completion`）；`tests/unit/execution/process-wait.test.mjs` `EXEC_011_A_natural_exit_before_deadline_returns_code_without_kill` | MOVE+REUSE | `node --test requirements/process-execution/tests/pty-port.test.mjs tests/unit/execution/process-wait.test.mjs` |
| PROC-004 有界执行：finite hard limit + 确定超时路径 | `tests/unit/process/process-runner.test.mjs` `EXEC_011_slow_process_is_killed_and_reports_timeout`；`tests/unit/process/process-output.test.mjs` `EXEC_011_effective_deadline_is_min_of_estimate_and_hard_limit`、`EXEC_011_default_hard_limit_is_one_hour` | REUSE（SPLIT：deadline 代数→`time-capability`） | `node --test tests/unit/process/process-runner.test.mjs tests/unit/process/process-output.test.mjs` |
| PROC-005 Process Request 类型化；无效 estimate 拒绝 | `tests/unit/process/process-runner.test.mjs`（`EXEC_011_rejects_nan_runtime_estimate`、`EXEC_011_rejects_zero_and_negative_runtime_estimate`、`EXEC_011_rejects_negative_output_estimate`）；`tests/unit/tools/executor-tool.test.mjs`（`RUN_non_positive_deadline_is_rejected`、`RUN_invalid_output_budget_is_rejected`） | REUSE | `node --test tests/unit/process/process-runner.test.mjs tests/unit/tools/executor-tool.test.mjs` |
| PROC-006 cancellation 收束资源 | `tests/unit/execution/process-wait.test.mjs` `EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit` | REUSE | `node --test tests/unit/execution/process-wait.test.mjs` |
| PROC-007 continuing process ≠ one-shot | anchor `continuing-process`（devops 组）；`tests/pty-port.test.mjs` `PORT_close_requests_terminate_but_keeps_the_session_live`、`PORT_close_all_escalates_to_kill_after_grace` | MOVE | `node --test requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-008 完成事实双通道 | `tests/unit/execution/join-v2-mailbox.test.mjs` `EXEC_018_drain_available_returns_two_completions_in_publish_order`（PTY 队列 FIFO）；`requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`（Distiller 定向等待，SPLIT：hidden handle→`managed-session-lifecycle`） | REUSE | `node --test tests/unit/execution/join-v2-mailbox.test.mjs` |
| PROC-009 物理输出捕获有界；spool 是蒸馏输入 | `tests/unit/process/process-output.test.mjs`（`EXEC_011_collector_spools_when_byte_count_crosses_threshold`、`EXEC_011_collector_spool_accumulates_later_chunks`、`EXEC_011_spool_chunk_count_rounds_up`、`EXEC_011_spool_round_trips_bytes_through_temp_file`）；`tests/pty-session.test.mjs`（session 记录形状） | MOVE+REUSE | `node --test requirements/process-execution/tests/pty-session.test.mjs tests/unit/process/process-output.test.mjs` |
| PROC-010 terminal/run 完成 = exit_code + 输出 | `tests/unit/execution/join-v2-wire.test.mjs` `EXEC_004_pty_completion_is_natural_language_plus_exit_code`；`tests/unit/tools/executor-tool.test.mjs` `RUN_completed_command_reports_exit_code_and_streams`、`RUN_nonzero_exit_is_reported_not_thrown` | REUSE | `node --test tests/unit/execution/join-v2-wire.test.mjs tests/unit/tools/executor-tool.test.mjs` |
| PROC-011 run 是 DevOps 有界执行 | `tests/unit/tools/executor-tool.test.mjs`（`RUN_spec_exposes_command_and_budget_arguments`、`RUN_missing_command_is_rejected_before_spawn`、`RUN_blank_command_is_rejected_before_spawn`、`RUN_deadline_overrun_returns_the_fixed_timeout_consequence`、`RUN_world_lock_is_accepted`） | REUSE | `node --test tests/unit/tools/executor-tool.test.mjs` |

## 移动文件清单

| 源 | 目标 | 结果 |
|----|------|------|
| `requirements/process-execution/tests/pty-types.test.mjs` | `requirements/process-execution/tests/pty-types.test.mjs` | `node --test` 绿 |
| `requirements/process-execution/tests/pty-api.test.mjs` | `requirements/process-execution/tests/pty-api.test.mjs` | `node --test` 绿 |
| `requirements/process-execution/tests/pty-backend.test.mjs` | `requirements/process-execution/tests/pty-backend.test.mjs` | `node --test` 绿 |
| `requirements/process-execution/tests/pty-port.test.mjs` | `requirements/process-execution/tests/pty-port.test.mjs` | `node --test` 绿 |
| `requirements/process-execution/tests/pty-session.test.mjs` | `requirements/process-execution/tests/pty-session.test.mjs` | `node --test` 绿 |

（5 文件合计 69 断言全绿；import 深度已适配为 `../../../tests/unit/support` + `../../../dist`。）

## SPLIT@cutover 清单（REUSE 文件拆分计划）

- `tests/unit/process/process-output.test.mjs`：collector/spool 断言归本包（PROC-009）；
  `effectiveDeadline`/`DefaultHardLimit` 纯代数断言 → `time-capability`。
- `tests/unit/process/process-runner.test.mjs`：run/spawn/kill 断言归本包；estimate 拒绝 →
  `time-capability`（PROC-005 的 deadline 面）；`large_estimate_acquires_and_releases_the_gate` →
  `output-distillation`（LargeGate 预算合同）。
- `tests/unit/process/pty-supervisor.test.mjs`：supervisor 资源监督归本包；fable_modules import
  是 test-boundary 门 grandfathered 行，移动会红 → 留原处。
- `tests/unit/process/large-gate.test.mjs`：整文件 → `output-distillation`（EXEC-013）。
- `tests/unit/execution/process-wait.test.mjs`：等待/退出断言归本包；deadline 分支 → `time-capability`。
- `tests/unit/tools/executor-tool.test.mjs`：RUN_* 语义归本包；tool spec/registry → `capability-enforcement`。
- `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`：Distiller 定向等待（AwaitAgentWithPermit）归本包
  交叉；`HostOwnedHidden` handle → `managed-session-lifecycle`；Assignment 不进工具面 →
  `participant-horizon`。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.devops`：`operational-closure`、`act-vs-observation`、`mechanical-meaning`、
`continuing-process`、`signal-not-exit`。
`TOOL_DESCRIPTION_ANCHORS.run`：`command-is-act`、`economic-commitment`。
