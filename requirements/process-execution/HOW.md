# process-execution — HOW

## 架构机制

### 1. PTY 会话与进程状态机

- **动作分型**：通过 `PtySession` 与底层 `PtyBackend` 暴露 `open`、`send`、`read` 与 `signal` 四个独立动词。
- **完成确认**：底层适配器绑定子进程的退出事件（`onExit`/`close`），作为完成事实的唯一写入入口。发送终止信号（`kill`）仅触发向进程组发送信号，不直接修改完成单元。
- **资源清理**：`PtySupervisor` 在清理时执行两阶段优雅终止（先 `SIGTERM`，超时后升级为 `SIGKILL`），保证进程资源彻底释放。

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
| PROC-001 | `requirements/process-execution/tests/pty-types.test.mjs` |
| PROC-002 | `requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-003 | `requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-004 | `requirements/process-execution/tests/process-runner.test.mjs` |
| PROC-005 | `requirements/process-execution/tests/executor-tool.test.mjs` |
| PROC-006 | `requirements/process-execution/tests/process-wait.test.mjs` |
| PROC-007 | `requirements/process-execution/tests/pty-port.test.mjs` |
| PROC-008 | `requirements/process-execution/tests/join-v2-mailbox-drain.test.mjs` |
| PROC-009 | `requirements/process-execution/tests/process-output.test.mjs` |
| PROC-010 | `requirements/process-execution/tests/join-v2-wire-pty.test.mjs` |
| PROC-011 | `requirements/process-execution/tests/executor-tool.test.mjs` |
