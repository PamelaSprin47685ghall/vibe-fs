# process-execution

## 一句话 WHY

participant 控制真实进程/PTY 时，command、signal、output、exit 与 cancellation 必须对应物理世界；
不能靠 stdout 文本或 Host DTO 猜完成。

## WHAT 概览

- 终端四动词四 contract；不返回 `pty_id`/`closed`/`status`（PROC-001）。
- command/signal 是 real-world acts；stdout/stderr 是 observation，不是 completion（PROC-002）。
- physical completion 只由 backend exit 建立；kill ≠ exit（PROC-003）。
- 有界执行：finite hard limit、超时确定失败路径（PROC-004/005）。
- cancellation 收束资源，不 hang（PROC-006）；continuing process ≠ one-shot execution（PROC-007）。
- 完成事实双通道：agent Pulse vs PTY PublishPty（PROC-008）。
- 物理输出捕获有界（spool 阈值），为蒸馏提供输入（PROC-009）。
- terminal/run 完成 = exit_code + 输出，是可信物理观察（PROC-010）；run = DevOps 有界执行（PROC-011）。

## HOW 概览

实现模型：`Process/{Pty,PtySession,PtyTypes,PtyBackend,PtySupervisor,ProcessRunner,NodeProcessHost,NodeProcessWait,ProcessRequest,ProcessOutput,Spool}.fs`。
物理完成入口 = backend onExit（`NodeProcessHost` 的 close/error handler 唯一设置 `Exited`；`Kill` 不设置）。
有界执行 = `ProcessEstimate.effectiveDeadline = min(estimate, HardLimit)`。详见 HOW.md。

## PROOF 概览

- 包内（MOVE）：`tests/pty-types.test.mjs`、`tests/pty-api.test.mjs`、`tests/pty-backend.test.mjs`、
  `tests/pty-port.test.mjs`、`tests/pty-session.test.mjs`。
- REUSE（SPLIT@cutover）：`tests/unit/process/{process-output,process-runner,pty-supervisor,large-gate}.test.mjs`、
  `requirements/process-execution/tests/process-wait.test.mjs`、`requirements/process-execution/tests/executor-tool.test.mjs`。
- Semantic anchors（`scripts/checks/semantic-anchors.mjs`）拥有：devops 组 `operational-closure` /
  `act-vs-observation` / `mechanical-meaning` / `continuing-process` / `signal-not-exit`；
  run 工具组 `command-is-act` / `economic-commitment`。

## 阅读顺序

1. `WHY.md` — 为什么必须独立存在、RED 是什么、历史失败模式。
2. `WHAT.md` — normative 合同。
3. `HOW.md` — 实现模型（非 normative；含「历史与弃权」）。
4. `PROOF.md` — 命题落点表 + SPLIT@cutover。

## 边界（DOES NOT OWN）

output 如何被蒸馏（`output-distillation`）；Coder/DevOps office authority（`office-capability`）；
generic time capability / deadline 代数（`time-capability`）；当前 PTY/backend 实现（HOW）；
provider-visible DTO 布局（`provider-projection`）。
