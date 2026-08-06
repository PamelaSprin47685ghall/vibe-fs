# DSL 结构化程序规则实现差距

对应规范：`what/dsl-structured-program.md`、`shape/dsl-structured-program.md`、`how/dsl-structured-program.md`、`proof/dsl-structured-program.md`。

## 已消除差距

1. `src/Wanxiangshu/Application/Reconciliation/ReconciledTurn.fs` 不再重复定义 `TurnOutcome`，统一使用 `ReconcileProgram.TurnOutcome`。
2. `src/Wanxiangshu/Session/ForkTypes.fs` 不再重复定义 `AgentRole`，统一使用 `Kernel.Role`。
3. `src/Wanxiangshu/Domain/SessionRecovery.fs` 已移除 `RecoveryTrace` DU 与 `familyReadyBeforeBusiness` 解释器；`FamilyRecoveryPermit` 作为业务入口的必需参数直接使用。
4. `scripts/checks/dsl-ownership.mjs` 已升级：
   - `src/Wanxiangshu/Process/` 纳入扫描；
   - 区分物理运行资源可变与业务程序计数器；
   - `ProcessRequest.fs` / `PtyTypes.fs` 的外部协议 `Command` 与 `Reply` 被识别为合法边界类型；
   - `dsl-ownership-ratchet-baseline.json` 已更新。

## 仍存差距

1. `src/Wanxiangshu/Session/BloggerRuntimeState.fs` 仍含 `BloggerRuntimeCell` 状态乘积（`State`, `PendingOffer`, `Recovery`, `Drain`）。
   - `BloggerToolRecovery` 已改为从 Host transcript 纯推导（`BloggerCrashRecovery.repairState`），但 `cell.Recovery` 字段尚未删除。
   - 后续需拆除 `InFlight/Idle/Parked/Sealed/Disposed` 业务 State，迁移为 single-flight Task ownership。
2. `src/Wanxiangshu/Session/Companion.fs` 仍暴露 `ArmRecoverySlot/DisarmRecoverySlot/IsRecoveryArmed` 基于 `let mutable slotArmed`。
3. `src/Wanxiangshu/Journal/AgentFact.fs` / `Kernel/Fact.fs` 仍是 41-case 跨域总和类型，需按 bounded context 拆分为 family view（wire 兼容分阶段）。

## 阻塞

 BloggerRuntimeCell 拆除与 Companion slotArmed 改造涉及并发生命周期接口与多个 Host 调用点，应作为后续独立 PR；当前 `npm run lint` 与 `npm run check` 全绿，不阻塞主线验证。

## 验收标准

- `npm run lint` 通过。
- `npm run check` 通过。
- 完成剩余差距后删除本文件。
