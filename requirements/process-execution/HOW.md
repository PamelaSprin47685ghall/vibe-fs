# process-execution — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型与历史裁决。

## 实现模型

### 类型面（`Process/`）

| 文件 | 职责 |
|------|------|
| `ProcessRequest.fs` | typed `Command`、`ProcessEstimate`、`ProcessContext.HardLimit`、`ProcessOutcome`、`ProcessError`、`effectiveDeadline = min(estimate, HardLimit)` |
| `PtyTypes.fs` | `PtySignal`（TERM/KILL/INT/HUP/QUIT/USR1/USR2）、`PtyCommand`（Spawn/Write/Read/Signal/Resize）、`PtyHandle`、`PtyRead`、`ReadPlan` |
| `Pty.fs` | PTY 会话状态机（open/send/read/signal/exit） |
| `PtySession.fs` | `create`：id + backend；exit completion cell 未决直至 onExit |
| `PtyBackend.fs` | backend port：`PtyId -> PtyCommand -> Task<Result<unit, string>>` |
| `PtySupervisor.fs` | 资源监督（owner-initiated cleanup：TERM → grace(5000ms) → KILL → await exit；**不是**业务 deadline） |
| `ProcessRunner.fs` | one-shot 执行编排（estimate 校验、spawn、wait、gate 获取） |
| `NodeProcessHost.fs` | Node/Bun child_process adapter：spawn、kill 进程组、spool 文件 I/O、chunked read |
| `NodeProcessWait.fs` | 等待逻辑：自然 exit / deadline kill / cancel 三分支 |
| `ProcessOutput.fs` | `OutputCollector`：stdout/stderr 字节聚合、字节计数、spool 阈值切换（`MemoryBufferBudget = 204800`） |
| `Spool.fs` | `StreamingSpool`：chunk 204800 B、`chunkCount`、`readChunksSync/Async` |
| `Deadline.fs` | 纯 deadline 代数（**归属 `time-capability`**） |
| `LargeGate.fs` | 单持有者大进程门（**归属 `output-distillation` 输出预算合同**） |

### 物理完成链（EXEC-015 / PROC-003）

```text
spawn → child.on('close', code) / child.on('error', err)
     → 唯一设置 exitedRef + trySetResult exitTcs（真实 exit code / -1 on error）
     → notifyExitedList（PTY completion 写入口）
Kill 只发 SIGKILL 到进程组，绝不设置 exited
ct.Register：未退出时 killProcessGroup（PROC-006）
```

### 双通道（EXEC-024 / PROC-008）

`Session/ForkRuntime.fs`：agent 完成 → `mailbox.PulseAgentHandle`（wake-only，无 payload，结果读
Journal）；PTY 完成 → `mailbox.PublishPtyCompletion`（携带 `PtyJoinItem` 物理结果）。

### 有界执行（EXEC-010/011 / PROC-004）

`ProcessEstimate.effectiveDeadline(RuntimeSeconds s, HardLimit) = min(TimeSpan.FromSeconds s, HardLimit)`
；`DefaultHardLimit = 1h`（真实有限值）。无效 estimate 在 runner 入口拒绝（PROC-005）。

### 输出捕获与 spool（PROC-009）

`OutputCollector.addChunk`：`BytesObserved` 累加；跨 `min(OutputLimit, MemoryBufferBudget)` 即
`Spool.startStreamingSpool`，历史 combined 块先灌入再清空内存缓冲。`buildResult`：有 spool →
`ProcessOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount)`；否则 `Completed(exitCode,
stdoutText, stderrText)`。spool 文件在 `os.tmpdir()`，位于工作树之外。

### run 工具（`Infrastructure/OpenCode/Tools/ExecutorTool.fs`）

`RUN_*` 面：command + `deadline_seconds` / `output_budget_bytes` / `world_lock` 参数；spawn 前拒绝
缺失/blank command、非正 deadline、无效预算（PROC-011）。

## 物理落点（CURRENT EVIDENCE）

- 类型：`Process/{Pty,PtySession,PtyTypes,PtyBackend,PtySupervisor,ProcessRunner,NodeProcessHost,NodeProcessWait}.fs`。
- 失败面：onExit-only completion（`NodeProcessHost.ChildProcess.Exited`）、`Process/Deadline.fs`。
- Tests：包内 5 个 pty 测试（MOVE）；REUSE 清单见 PROOF.md。

## 边界与弃权（非 normative）

- **GARBAGE——`fork-pty` 工具**：终端面已删 `fork-pty`，四动词四分合同（EXEC-003）；不得复活。
- **GARBAGE——`executor` 工具/角色**：Meditator/Executor 已删（GrandRewrite clean-break）；`run` 是
  DevOps 工具，不叫 executor，与 Distiller office 无关。
- **GARBAGE——timeout-on-process flag**：`Exit: int * bool`（bool=超时）已删，理由见 WHY.md。
- **HOW——机制常数**：`MemoryBufferBudget = 204800`、`Spool.ChunkSizeBytes = 204800`、
  `termToKillGraceMs = 5000`、`DefaultHardLimit = 1h`、`MaxTimerWaitMs = 2147483647`：有界性才是 WHAT。
- **HOW——backend 选择**：当前 Node `child_process` + `bun-pty` 形状可整体更换（独立变化测试）；
  工具名 `open-terminal` 等可改（PROC-001 语义不变）。
- **归属他包**：deadline 代数（`Deadline.fs`、`PtyTiming` timer port）→ `time-capability`；输出预算
  合同与蒸馏（`LargeGate.fs`、`ToolResultBound.fs`、`Distillation.fs`）→ `output-distillation`；
  Git 执行动词（`Infrastructure/Git/GitOperations.fs` 的 git spawn）→ `change-integration`；
  JS 沙箱（`Process/JsSandbox.fs`）→ `repository-programming`。
