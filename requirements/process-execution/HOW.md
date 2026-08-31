# process-execution — HOW

## 架构机制

### 1. PTY 会话与进程状态机

- **动作分型**：通过 `PtySession` 与底层 `PtyBackend` 暴露 `open`、`send`、`read` 与 `signal` 四个独立动词。
- **完成确认**：底层适配器绑定子进程的退出事件（`onExit`/`close`），作为完成事实的唯一写入入口。发送终止信号（`kill`）仅触发向进程组发送信号，不直接修改完成单元。
- **资源清理**：`PtySupervisor` 在清理时执行两阶段优雅终止（先 `SIGTERM`，超时后升级为 `SIGKILL`），保证进程资源彻底释放。短于 1 秒的物理 exit race 保持 Node timer 引用，确保清理 Promise 必然结算；长生产 grace timer 才可 `unref`，避免空闲进程被预算计时器单独挽留。

### 2. 有界执行与请求验证

- **请求类型化**：所有进程调用统一经过 `ProcessRequest` 结构表达。在调用物理 `spawn` 之前，前置校验并拒绝非法预估与预算参数。
- **硬性超时控制**：通过 `ProcessContext.HardLimit` 施加系统级硬顶，有效截止时间取用户预估与系统硬顶的最小值。超时触发确定性错误。

### 3. 双通道完成分发

系统采用隔离的双通道信箱：
- **Agent 完成**：发送 `PulseAgentHandle` 轻量信号，结果由持久化日志承载。
- **PTY 完成**：发送 `PublishPtyCompletion` 信号，携带实际退出码与输出的物理结果。

### 4. 输出缓冲与流式临时存储

`OutputCollector` 实时追踪累积字节数：
- 在内存缓冲限额内（`MemoryBufferBudget`），输出在内存中暂存。
- 超过限额时自动启动 `StreamingSpool`，将历史与后续数据流式转储至外部临时文件，防止大输出耗尽内存。

## DEPENDS ON

- `time-capability`
- `host-boundary`
- `participant-horizon`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PROC-001 | `requirements/process-execution/tests/pty-types.test.mjs::WHAT[PROC-001] PTY_TYPES_tryParse_accepts_every_supported_signal_name` |
| PROC-002 | `requirements/process-execution/tests/pty-port.test.mjs::WHAT[PROC-002] PORT_send_term_kill_int_marks_abort_for_the_next_completion` |
| PROC-003 | `requirements/process-execution/tests/pty-port.test.mjs::WHAT[PROC-003] PORT_complete_default_publishes_pty_exited_closed` |
| PROC-004 | `requirements/process-execution/tests/process-runner.test.mjs::WHAT[PROC-004] EXEC_011_slow_process_is_killed_and_reports_timeout` |
| PROC-005 | `requirements/process-execution/tests/executor-tool.test.mjs::WHAT[PROC-005] RUN_non_positive_deadline_is_rejected`；`requirements/process-execution/tests/executor-tool.test.mjs::WHAT[PROC-005] RUN_invalid_output_budget_is_rejected` |
| PROC-006 | `requirements/process-execution/tests/process-wait.test.mjs::WHAT[PROC-006] EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit` |
| PROC-007 | `requirements/process-execution/tests/pty-port.test.mjs::WHAT[PROC-007] PORT_list_reports_active_handles`；`requirements/process-execution/tests/pty-timing.test.mjs::WHAT[PROC-007] short PTY exit race owns enough physical lifetime to settle` |
| PROC-008 | `requirements/process-execution/tests/join-v2-mailbox-drain.test.mjs::WHAT[PROC-008] EXEC_018_drain_available_returns_two_completions_in_publish_order` |
| PROC-009 | `requirements/process-execution/tests/process-output.test.mjs::WHAT[PROC-009] EXEC_011_collector_spools_when_byte_count_crosses_threshold`；`requirements/process-execution/tests/process-output.test.mjs::WHAT[PROC-009] EXEC_011_collector_spooled_buffers_are_cleared` |
| PROC-010 | `requirements/process-execution/tests/join-v2-wire-pty.test.mjs::WHAT[PROC-010] EXEC_004_pty_completion_is_natural_language_plus_exit_code` |
| PROC-011 | `requirements/process-execution/tests/executor-tool.test.mjs::WHAT[PROC-011] RUN_surface_names_the_provider_execution_verb`；`requirements/process-execution/tests/executor-tool.test.mjs::WHAT[PROC-011] RUN_deadline_overrun_returns_the_fixed_timeout_consequence` |
