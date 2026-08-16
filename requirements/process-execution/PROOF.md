# process-execution — 证明落点

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| PROC-001 终端四动词四 contract | `tests/pty-types.test.mjs`（`PTY_TYPES_pty_command_cases_carry_their_fields`——Spawn/Write/Read/Signal/Resize；`PTY_TYPES_tryParse_*`——signal 名）；`tests/pty-port.test.mjs`（`PORT_fork_*`/`PORT_send_*`/`PORT_read_*`） | MOVE | `node --test requirements/process-execution/tests/pty-types.test.mjs requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-002 command/signal 是 act、stdout 是 observation | anchors `act-vs-observation`/`signal-not-exit`/`mechanical-meaning`（devops 组，双语言命中）；`tests/pty-port.test.mjs` `PORT_send_term_kill_int_marks_abort_for_the_next_completion`、`PORT_send_plain_signal_does_not_abort_the_completion`；`tests/pty-supervisor.test.mjs` `SUPERVISOR_applyLive_signal_kills_the_real_process_group_or_process`、`SUPERVISOR_applyLive_signal_unknown_pid_becomes_error` | MOVE | `node scripts/checks/semantic-anchors.mjs`；`node --test requirements/process-execution/tests/pty-port.test.mjs requirements/process-execution/tests/pty-supervisor.test.mjs` |
| PROC-003 physical completion 只由 backend exit；kill ≠ exit | `tests/pty-port.test.mjs`（`PORT_complete_*` 组——completion 只在 complete 路径发布；`PORT_send_plain_signal_does_not_abort_the_completion`）；`tests/pty-backend.test.mjs`（`BACKEND_fork_without_bun_pty_fails_spawn_and_publishes_failed`、`BACKEND_concurrent_failed_forks_each_publish_one_completion`——失败也走 completion 路径）；`tests/pty-supervisor.test.mjs`（`SUPERVISOR_attach_onExit_completes_exit_publishes_closed_and_drops_session`、`SUPERVISOR_attach_onExit_publishes_residual_output`、`SUPERVISOR_attach_onExit_fails_pending_writes_and_parked_read`）；`requirements/process-execution/tests/process-wait.test.mjs` `EXEC_011_A_natural_exit_before_deadline_returns_code_without_kill` | MOVE+REUSE | `node --test requirements/process-execution/tests/pty-port.test.mjs requirements/process-execution/tests/pty-backend.test.mjs requirements/process-execution/tests/pty-supervisor.test.mjs requirements/process-execution/tests/process-wait.test.mjs` |
| PROC-004 有界执行：finite hard limit + 确定超时路径 | `requirements/process-execution/tests/process-runner.test.mjs` `EXEC_011_slow_process_is_killed_and_reports_timeout`；`requirements/process-execution/tests/process-wait.test.mjs` `EXEC_011_B_deadline_kills_once_then_real_exit_is_timed_out`、`EXEC_011_C_kill_never_acked_ends_with_minus_one_timed_out`；`requirements/process-execution/tests/handle-process.test.mjs` `EXEC_oneshot_completion_wait_is_bounded_by_management_deadline` | REUSE（SPLIT：deadline 代数→`time-capability`） | `node --test requirements/process-execution/tests/process-runner.test.mjs requirements/process-execution/tests/process-wait.test.mjs requirements/process-execution/tests/handle-process.test.mjs` |
| PROC-005 Process Request 类型化；无效 estimate 拒绝 | `requirements/process-execution/tests/executor-tool.test.mjs`（`RUN_non_positive_deadline_is_rejected`、`RUN_invalid_output_budget_is_rejected`）；`requirements/process-execution/tests/handle-process.test.mjs` `EXEC_010_process_request_carries_all_fields` | REUSE | `node --test requirements/process-execution/tests/executor-tool.test.mjs requirements/process-execution/tests/handle-process.test.mjs` |
| PROC-006 cancellation 收束资源 | `requirements/process-execution/tests/process-wait.test.mjs` `EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit`；`requirements/process-execution/tests/pty-api.test.mjs`（`PTY_API_abort_parent_*`/`PTY_API_unregister_*`/`PTY_API_tokens_are_monotonic_across_parents`/`PTY_API_throwing_abort_callback_does_not_block_the_rest`——parent-abort registry 收束）；`requirements/process-execution/tests/handle-process.test.mjs` `EXEC_011_kill_ack_grace_is_finite_not_MaxTimerWaitMs` | REUSE | `node --test requirements/process-execution/tests/process-wait.test.mjs requirements/process-execution/tests/pty-api.test.mjs requirements/process-execution/tests/handle-process.test.mjs` |
| PROC-007 continuing process ≠ one-shot | anchor `continuing-process`（devops 组）；`tests/pty-port.test.mjs` `PORT_close_requests_terminate_but_keeps_the_session_live`、`PORT_close_all_escalates_to_kill_after_grace`；`tests/pty-supervisor.test.mjs`（session 注册表/`takePending`/`drop`/`attach` 组——持续交互状态） | MOVE | `node --test requirements/process-execution/tests/pty-port.test.mjs requirements/process-execution/tests/pty-supervisor.test.mjs` |
| PROC-008 完成事实双通道 | `requirements/process-execution/tests/join-v2-mailbox-drain.test.mjs` `EXEC_018_drain_available_returns_two_completions_in_publish_order`（PTY 队列 FIFO）；`requirements/delegation/tests/join-v2-mailbox.test.mjs` 同锚点（REUSE 原件）；`requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`（Distiller 定向等待，SPLIT：hidden handle→`managed-session-lifecycle`） | REUSE | `node --test requirements/process-execution/tests/join-v2-mailbox-drain.test.mjs` |
| PROC-009 物理输出捕获有界；spool 是蒸馏输入 | `requirements/process-execution/tests/process-output.test.mjs`（`EXEC_011_collector_spools_when_byte_count_crosses_threshold`、`EXEC_011_collector_spool_accumulates_later_chunks`、`EXEC_011_collector_spooled_buffers_are_cleared`、`EXEC_011_spool_chunk_count_rounds_up`、`EXEC_011_spool_chunk_bytes_splits_at_chunk_size`、`EXEC_011_spool_round_trips_bytes_through_temp_file`、`EXEC_011_spool_append_tracks_bytes_written`）；`tests/pty-session.test.mjs`（session 记录形状） | MOVE+REUSE | `node --test requirements/process-execution/tests/pty-session.test.mjs requirements/process-execution/tests/process-output.test.mjs` |
| PROC-010 terminal/run 完成 = exit_code + 输出 | `requirements/process-execution/tests/join-v2-wire-pty.test.mjs` `EXEC_004_pty_completion_is_natural_language_plus_exit_code`；`requirements/delegation/tests/join-v2-wire.test.mjs` 同锚点（REUSE 原件）；`requirements/process-execution/tests/executor-tool.test.mjs` `RUN_completed_command_reports_exit_code_and_streams`、`RUN_nonzero_exit_is_reported_not_thrown` | REUSE | `node --test requirements/process-execution/tests/join-v2-wire-pty.test.mjs requirements/process-execution/tests/executor-tool.test.mjs` |
| PROC-011 run 是 DevOps 有界执行 | `requirements/process-execution/tests/executor-tool.test.mjs`（`RUN_spec_exposes_command_and_budget_arguments`、`RUN_missing_command_is_rejected_before_spawn`、`RUN_blank_command_is_rejected_before_spawn`、`RUN_blank_session_surfaces_natural_execution_consequence_before_spawn`、`RUN_deadline_overrun_returns_the_fixed_timeout_consequence`、`RUN_world_lock_is_accepted`、`RUN_spooled_request_without_authority_fails_before_execution_without_identity_leak`、`RUN_spooled_output_family_blocked_surfaces_recovery_consequence`） | REUSE | `node --test requirements/process-execution/tests/executor-tool.test.mjs` |

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

- `requirements/process-execution/tests/process-output.test.mjs`：collector/spool 断言归本包（PROC-009）；
  `effectiveDeadline`/`DefaultHardLimit` 纯代数断言 → `time-capability`。
- `requirements/process-execution/tests/process-runner.test.mjs`：run/spawn/kill 断言归本包；estimate 拒绝 →
  `time-capability`（PROC-005 的 deadline 面）；`large_estimate_acquires_and_releases_the_gate` →
  `output-distillation`（LargeGate 预算合同）。
- `requirements/process-execution/tests/pty-supervisor.test.mjs`：supervisor 资源监督归本包；fable_modules import
  是 test-boundary 门 grandfathered 行，移动会红 → 留原处。
- `requirements/output-distillation/tests/large-gate.test.mjs`：整文件 → `output-distillation`（EXEC-013）。
- `requirements/process-execution/tests/process-wait.test.mjs`：等待/退出断言归本包；deadline 分支 → `time-capability`。
- `requirements/process-execution/tests/executor-tool.test.mjs`：RUN_* 语义归本包；tool spec/registry → `capability-enforcement`。
- `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`：Distiller 定向等待（AwaitAgentWithPermit）归本包
  交叉；`HostOwnedHidden` handle → `managed-session-lifecycle`；Assignment 不进工具面 →
  `participant-horizon`。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.devops`：`operational-closure`、`act-vs-observation`、`mechanical-meaning`、
`continuing-process`、`signal-not-exit`。
`TOOL_DESCRIPTION_ANCHORS.run`：`command-is-act`、`economic-commitment`。
