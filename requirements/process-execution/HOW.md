# process-execution — HOW

## 架构机制

### 1. PTY 会话与进程状态机

- **动作分型**：通过 `PtySession` 与底层 `PtyBackend` 暴露 `open`、`send`、`read` 与 `signal` 四个独立动词。
- **完成确认**：底层适配器绑定子进程的退出事件（`onExit`/`close`），作为完成事实的唯一写入入口。发送终止信号（`kill`）仅触发向进程组发送信号，不直接修改完成单元。
- **资源清理**：`PtySupervisor` 在清理时执行两阶段优雅终止（先 `SIGTERM`，超时后升级为 `SIGKILL`），保证进程资源彻底释放。短于 1 秒的物理 exit race 保持 Node timer 引用，确保清理 Promise 必然结算；长生产 grace timer 才可 `unref`，避免空闲进程被预算计时器单独挽留。

### 2. 双通道完成分发

系统采用隔离的双通道信箱：
- **Agent 完成**：发送 `PulseAgentHandle` 轻量信号，结果由持久化日志承载。
- **PTY 完成**：发送 `PublishPtyCompletion` 信号，携带实际退出码与输出的物理结果。

### 3. 输出缓冲、流式临时存储与零 Distiller 留尾截断

`OutputCollector` 实时追踪累积字节数：
- **内存缓冲与 Spool 切换**：在内存缓冲限额内（`MemoryBufferBudget`），输出在内存中暂存；超过限额时自动启动 `StreamingSpool`，将历史与后续数据流式转储至外部临时文件，防止大输出耗尽内存。
- **零 Distiller 预算截尾**：当 spool 或输出超过 `output_budget_bytes` 时，直接调用有界尾部读取函数从 spool 尾部读取预算内字节，严禁拉起 Distiller 模型子会话。
- **UTF-8 边界对齐**：截断时从尾部预算起点向后探测首个有效 UTF-8 字符起始字节，避免切碎多字节序列产生乱码。
- **事实与截断格式化**：为真实退出码、signal/超时/取消状态、显式截断声明和 TOML/注释封装预留空间，保证最终输出结果在预算内。

### 4. Large Gate 大输出互斥与 ToolResultBound 留尾

- **Large Gate 互斥**：对于估算大输出的命令，在执行前通过 `LargeGateSurface` 获取单持有者门禁，支持 FIFO 队列与取消 token。
- **ToolResultBound 留尾截断**：自定义工具回传长文本时，通过 `ToolResultBound` 执行确定性行边界留尾截断并注入显式截断标记。

## 条款与测试映射

| 条款编号 | 规范主题 | 核心断言 | 测试文件 |
| --- | --- | --- | --- |
| PROC-001 | 终端四动词四 Contract | 独立动词、UTF-8 编码、唯一 ID | `pty-api.test.mjs`, `pty-types.test.mjs`, `pty-backend.test.mjs` |
| PROC-002 | Command 与 Signal 为物理 Act | 命令发送与增量观察分离 | `pty-backend.test.mjs` |
| PROC-003 | 物理完成仅由 Backend Exit 确立 | Kill 不等于 Exit，等待退出事件 | `process-wait.test.mjs`, `pty-backend.test.mjs` |
| PROC-004 | 有界执行之 Hard Limit 与超时 | 超时判定与确定性失败 | `deadline-surface.test.mjs`, `process-runner.test.mjs` |
| PROC-005 | Process Request 类型化与预算拒绝 | 非法时限与负预算前置拒绝 | `executor-tool.test.mjs`, `handle-process.test.mjs` |
| PROC-006 | Cancellation 彻底收束进程组 | 进程组信号发送与非阻塞返回 | `handle-process.test.mjs`, `pty-api.test.mjs`, `process-runner.test.mjs` |
| PROC-007 | 持续终端与一次性执行严格分型 | PTY 与 run 互斥形态及 exit race | `pty-timing.test.mjs` |
| PROC-008 | 完成事实双通道 | Agent Pulse 与 PTY completion 隔离 | `join-v2-mailbox-drain.test.mjs` |
| PROC-009 | 物理输出捕获有界与 Spool 机制 | 缓冲预算切换 spool 流式转储 | `process-output.test.mjs`, `pty-session.test.mjs` |
| PROC-010 | Terminal 与 Run 完成投影 | 真实退出码与输出投影 | `join-v2-wire-pty.test.mjs`, `process-runner.test.mjs`, `executor-tool.test.mjs` |
| PROC-011 | Run/Query-Shell 参数能力与非蒸馏 | 参数对等校验与无蒸馏 | `executor-tool.test.mjs` |
| PROC-012 | process 与 PTY contract 窄能力 | 纯词汇与适配器隔离 | `m6-slice-boundary.test.mjs` |
| PROC-013 | 大输出零 Distiller 与原始留尾 | 任意输出零 Distiller、尾部原始 | `output-truncation-contract.test.mjs` |
| PROC-014 | 真实程序事实与显式截断声明 | 事实不从日志推断、截断说明明确 | `output-truncation-contract.test.mjs` |
| PROC-015 | 显式字节预算与 UTF-8 边界安全 | 字节预算计量、UTF-8 边界对齐 | `output-truncation-contract.test.mjs` |
| PROC-016 | Large Gate 单持有者互斥门禁 | FIFO 排队、取消、互斥释放 | `large-gate.test.mjs`, `large-gate-runner.test.mjs` |
| PROC-017 | 自定义工具留尾截断 | 留尾截断、标记注入、行边界完整 | `tool-result-bound.test.mjs` |

## DEPENDS ON

- `time-capability`
- `host-boundary`
- `participant-horizon`
