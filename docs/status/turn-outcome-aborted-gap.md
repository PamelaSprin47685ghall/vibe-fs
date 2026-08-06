# Agent 终态 Aborted 泄漏缺口与 LoopKill 映射（EXEC-020 / HOST-004 / LOOP-006）

目标：
- 对齐 `what/execution.md`（EXEC-020：Agent 终态代数 `Completed | Failed | Abandoned`）、`how/host.md`（ReconciledTurn 终态对齐映射）与 `what/loop.md`（LOOP-006）

当前：
- 传输层可识别 Host 取消信号 `TurnAborted`，规范层已在 `how/host.md` 与 `what/loop.md` 中完备形式化二阶段映射：

```text
Host 事件 (TurnAborted / MessageAbortedError / finish=aborted)
  │
  ├─► 检查 LoopKillArmed
  │     ├─► 命中 Armed  ──► 清除 Armed ──► 映射为 TurnFailed("LoopDetectedKill") ──► 推进 Fallback
  │     └─► 未命中 Armed ──► 映射为 TurnAbandoned(UserOrSystemCancelled) ──► 终结 turn
```

缺口消除要求：
- 在 `ReconciledTurn.fs` 与 `ReconcileProgram.fs` 的代码实现中，删除 `TurnOutcome` DU 的 `TurnAborted` Case，彻底阻断传输层取消事件泄漏进入领域 `TurnOutcome`。
- 确保所有的 `TurnAborted` 事件进入 Reconciler 时必须经过 `LoopKillArmed` 判决并映射为 `TurnFailed` 或 `TurnAbandoned`。

阻塞：
- 无。代码级 `TurnOutcome` DU 调整与 Reconciler 分支对齐。
