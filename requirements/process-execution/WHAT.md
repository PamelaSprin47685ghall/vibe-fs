# process-execution — 可观察合同

本文件是 `process-execution` 包的唯一 normative 语义合同。证据指针 → `PROOF.md`。

## PROC-001：终端四动词四 contract

终端面是四个不同动词、四个不同 contract（EXEC-003，删除旧 `fork-pty`）：

```text
open-terminal(name, command)    打开
send-terminal(name, input)      写入
read-terminal(name)             读取增量
signal-terminal(name, signal)   发信号
```

不向 provider 返回 `pty_id` / `closed` / `status`。

含义/动机：打开/写入/读取/发信号是四个不同的物理 act，合并成一个动词会丢失 act 边界
（ARCH-007：same tool name ⇒ same contract）。

证据：MOVE `tests/pty-types.test.mjs`（`PtyCommand` 四 case）、`tests/pty-api.test.mjs`。

## PROC-002：command/signal 是 real-world acts；stdout/stderr 是 observation

command 与 signal 是作用在物理进程上的 act；stdout/stderr 是这些 act 产生的 observation，不是
completion，也不是下一动作的 authority（`docs/why/execution.md`、devops Role Law `act-vs-observation` /
`mechanical-meaning`）。signal 是 act 不是 exit（`signal-not-exit`）。

含义/动机：把输出当完成、把 signal 当退出，都是把 observation 升级成物理事实——两类错误都让
「物理世界状态」与「看到的文本」不可分。

证据：anchors `act-vs-observation` / `signal-not-exit` / `mechanical-meaning`（devops 组）；REUSE
`tests/unit/process/pty-port.test.mjs`（`PORT_send_term_kill_int_marks_abort_for_the_next_completion`——
signal 影响的是下一次 completion 的物化，不是立刻 exit）。

## PROC-003：physical completion 只由 backend exit 建立；kill ≠ exit

PTY completion **只**由 backend `onExit` 触发（EXEC-015）。禁止 stdout 启发式「看起来结束了」。
`Kill`（发 SIGKILL）不设置 exited：发送信号不是进程结束，把两者混同会让 waiter 在进程真正死亡前
返回（`NodeProcessHost.ChildProcess` 注释）。`Exit` 只带真实 exit code。

含义/动机：onExit 是物理完成信号；kill 是控制动作。waiter 必须等 close/error handler 报告真实退出。

证据：REUSE `tests/unit/process/pty-port.test.mjs`（`PORT_complete_*` 组：completion 经
`PtyPort.complete` 发布；`PORT_send_plain_signal_does_not_abort_the_completion`）；
`tests/unit/execution/process-wait.test.mjs`（`EXEC_011_A_natural_exit_before_deadline_returns_code_without_kill`）。

## PROC-004：有界执行——finite hard limit + 确定失败路径

任何单个进程必须有有限 hard limit（`ProcessContext.HardLimit`，不可省略）；超时走**确定**失败路径
（`ProcessError.TimeoutExceeded`），不用无限 wait（EXEC-011）。有效 deadline =
`min(provider estimate, hard limit)`，estimate 按面值应用（不 ×3 膨胀）；非有限/非正 estimate 折叠到
hard limit。

含义/动机：没有有限硬顶，「有界」不可判定；超时路径不确定，waiter 无法区分「死了」「跑了」「卡了」。

边界：deadline 的纯代数（`Deadline.ofBudget/remaining/nextWaitMs`、timer 分段）→ `time-capability`；
本包只拥有「执行必须物理有界、超时是确定失败」的义务。

证据：MOVE `tests/pty-port.test.mjs`（`PORT_complete_after_terminate_publishes_aborted`）；REUSE
`tests/unit/process/process-runner.test.mjs`（`EXEC_011_slow_process_is_killed_and_reports_timeout`）、
`tests/unit/process/process-output.test.mjs`（`EXEC_011_effective_deadline_is_min_of_estimate_and_hard_limit`）。

## PROC-005：Process Request 类型化；无效 estimate 拒绝

一次进程执行由 typed `Command`（FileName/Arguments/WorkingDirectory/Environment/Stdin/Deadline/
PtyOptions）表达（EXEC-010）。无效预算（NaN/零/负 runtime、负 output）在执行前拒绝，不进入物理世界。

含义/动机：类型化请求让「一次执行」在源码层是一个事实；无效预算在 spawn 前被边界拦截。

证据：REUSE `tests/unit/process/process-runner.test.mjs`（`EXEC_011_rejects_nan_runtime_estimate`、
`EXEC_011_rejects_zero_and_negative_runtime_estimate`、`EXEC_011_rejects_negative_output_estimate`）、
`tests/unit/tools/executor-tool.test.mjs`（`RUN_non_positive_deadline_is_rejected`、
`RUN_invalid_output_budget_is_rejected`）。

## PROC-006：cancellation 收束资源，不 hang

mid-wait cancellation：kill 进程组一次，然后拒绝等待方（`ProcessCancelled`），不得在 exit 上挂死；
已自然退出的进程照常报告真实 exit（`tests/unit/execution/process-wait.test.mjs` D 场景）。cancel
注册在 spawn 时，未退出时 kill 整个进程组（`NodeProcessHost` `ct.Register`）。

含义/动机：取消是控制面；控制面必须收敛，不能把「取消」变成新的无界等待。

证据：REUSE `tests/unit/execution/process-wait.test.mjs`（`EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit`）。

## PROC-007：continuing process ≠ one-shot execution

持续交互的 terminal（`open-terminal` 后多次 send/read/signal）与一次性执行（`run`）是两种物理形态：
前者有持续存在的交互状态（devops anchor `continuing-process`），后者一次 command 一次退出。二者
不得混为一个 contract；`run` ≠ Distiller office（EXEC-003 工具面表）。

含义/动机：caller 必须知道「这次 act 结束后进程是否还在」，否则无法决定下一次 act。

证据：anchors `continuing-process`（devops 组）；REUSE `tests/unit/process/pty-port.test.mjs`
（`PORT_close_requests_terminate_but_keeps_the_session_live`——close 请求 TERM 但 session 仍活到 exit）。

## PROC-008：完成事实双通道：agent Pulse vs PTY PublishPty

Mailbox 双通道：agent 完成路径只发 `Pulse`（结果读 Journal）；PTY 完成路径 `PublishPty`（物理结果
经通道投递）。禁止把 agent completion 塞进 PTY 通道或反之（EXEC-024、`Session/ForkRuntime.fs`）。

含义/动机：agent 完成是 durable fact（Journal 是权威），PTY 完成是物理 observation——两条事实源
不可混通道，否则「谁完成、带什么」不可分。

边界：Journal 的权威性与 fold → `durable-events`；permit 门重入 → `crash-reconciliation`。

证据：REUSE `tests/unit/execution/join-v2-mailbox.test.mjs`（`EXEC_018_drain_available_returns_two_completions_in_publish_order`——PTY 队列 FIFO）；`tests/unit/session/distiller-ownership.test.mjs`（Distiller 定向等待，SPLIT 注记见 PROOF.md）。

## PROC-009：物理输出捕获有界；spool 是蒸馏输入

stdout/stderr 采集：字节计数 + 内存积压封顶（`MemoryBufferBudget`），跨输出预算即切流式 spool
（`Spool.StreamingSpool`，chunk 204800 B）；`ProcessOutcome.Spooled(exitCode, spoolPath, totalBytes,
chunkCount)` 或 `Completed(exitCode, stdout, stderr)`。禁止无界缓冲；spool 物理文件位于工作树之外
（`tmpdir`），不污染 Clean Gate。

含义/动机：物理捕获必须自身有界，否则「观察」本身变成无界资源消耗；spool 是蒸馏管线的输入边界。

边界：spool 之后的语义压缩（`Distillation.distillSpool`）、`ToolResultBound`、`LargeGate` 输出预算
合同 → `output-distillation`；spool path 不得进 provider horizon → `participant-horizon`。

证据：REUSE `tests/unit/process/process-output.test.mjs`（`EXEC_011_collector_spools_when_byte_count_crosses_threshold`、
`EXEC_011_collector_spool_accumulates_later_chunks`、`EXEC_011_spool_chunk_count_rounds_up`）；MOVE
`tests/pty-session.test.mjs`（PTY session 记录形状）。

## PROC-010：terminal/run 完成 = exit_code + 输出（可信物理观察）

terminal 与 `run` 的完成后果 = 自然语言 + `exit_code` + 相关输出（EXEC-004 terminal 分支、EXEC-030
例外条款：`exit_code` 与非空 stdout/stderr 是语义驱动的精确观测字段）。`exit_code` 是物理 exit 的
精确投影，不是猜的 `status`。

含义/动机：`exit_code` 是 backend onExit 的可信投影；它是唯一允许穿过 horizon 的机器字段之一。

边界：join wire 的渲染（自然语言 + WorkRecord）→ `delegation`/`work-record`；DTO 禁令全法则 →
`participant-horizon`。

证据：REUSE `tests/unit/execution/join-v2-wire.test.mjs`（`EXEC_004_pty_completion_is_natural_language_plus_exit_code`）、
`tests/unit/tools/executor-tool.test.mjs`（`RUN_completed_command_reports_exit_code_and_streams`）。

## PROC-011：run 是 DevOps 有界执行，≠ Distiller office

`run` 工具：command + budget 参数（deadline/output budget）；缺失/blank command 在 spawn 前拒绝；
非正 deadline、无效输出预算拒绝；blank session 报自然执行后果；nonzero exit 是报告不是 throw
（`tests/unit/tools/executor-tool.test.mjs` RUN_* 组、AGENT-013）。run 的语义是「一次有界执行」，
不是蒸馏、不是审批、不是 Distiller 的摘要 office。

含义/动机：run 的边界 = 执行边界；把执行与蒸馏混在一个工具里会丢失「物理发生」与「语义压缩」的
分界（HANDOFF §6.6：控制进程 vs 诚实压缩是两个 WHY）。

证据：REUSE `tests/unit/tools/executor-tool.test.mjs`（`RUN_spec_exposes_command_and_budget_arguments`、
`RUN_missing_command_is_rejected_before_spawn`、`RUN_deadline_overrun_returns_the_fixed_timeout_consequence`）。
