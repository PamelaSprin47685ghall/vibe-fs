# crash-reconciliation

> 进程/插件中断后，只能从 durable facts 与可信物理观察重新进入普通程序；不能从临时内存、日志或
> 「上次大概做到哪」的猜测恢复。

## 一句话 WHY

崩溃丢失 process-local 状态，却不会撤销已经发生的外部事实。恢复必须建立在 durable facts +
可信物理 observation 上，fail closed 于证据不足，复用普通 workflow 入口——不发明第二状态机。
（详见 `WHY.md`）

## WHAT 概览

唯一 normative 合同在 `WHAT.md`（16 条命题，`CRASH-001`..`CRASH-016`）：
- process-local 状态（armed/permit/waiter/sensor）不是恢复权威（CRASH-001）
- 从 durable facts + 可信物理观察重建世界（CRASH-002）
- 未决 effect 先 reconcile、重入普通程序、无程序计数器（CRASH-003/004）
- 证据 ambiguous/missing fail closed；无 fresh evidence 无自动 effect（CRASH-005/006）
- TurnUnknown 是私有观测；abort 是 typed 控制面（CRASH-007/008）
- child recovery 无 Aborted 终态、结果分支穷尽、permit → join 线性序、completion 单一 owner
  （CRASH-009/010/011/012）
- combine 优先级、closure 校验与 permit 单调准入、Attached restore、Blogger 崩溃窗口
  （CRASH-013/014/015/016）

## HOW 概览

实现模型见 `HOW.md`：`Domain/SessionRecovery.fs`（纯代数）、
`Execution/Session/SessionRecoveryWorkflow.fs` / `ChildRecoveryWorkflow.fs`（编排）、
`Session/HandleController.fs`（completion 单一 owner）、`ReconcilePass.fs` / `Reconciler.fs`
（观测稳定器）、`HostForkRestart.fs`（restart 证明结构）。

## Proof 概览

`PROOF.md` 给出每条命题的测试落点：
- MOVE（7 文件，53 断言）：`session-recovery-combine`、`child-recovery-workflow`、
  `session-recovery-family`、`join-recovery-crash-matrix`、`join-recovery-trace`、
  `join-aborted-race`、`recovery-closure-permit`
- NEW：`tests/recovery-closure-permit.test.mjs`（5 断言：closure 校验 / token / permit 单调）
- REUSE：host-fork-restart、host-fork-runtime、session-quiescence-gate、signals、
  reconcile-program（structured-workflow）、p0-recovery-join gate 测试等，均含 SPLIT@cutover

## 阅读顺序

1. `WHY.md`（为什么存在、何时 RED、与 provider-attempt-recovery 的边界）
2. `WHAT.md`（16 条 normative 命题 + p0-recovery-join 反向覆盖）
3. `HOW.md`（代码怎么满足）
4. `PROOF.md`（怎么验证、红了说明什么）

## 依赖

DEPENDS ON：`durable-events`（恢复输入是已提交事实）、`effect-accounting`（unknown 不重放）、
`structured-workflow`（恢复重入普通程序形态）、`host-boundary`（snapshot 是可信物理观察）。
理由：CRASH-002/003/004/006 分别消费这四个包的 guarantee。
