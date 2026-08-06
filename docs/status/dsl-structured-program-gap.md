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
5. `src/Wanxiangshu/Kernel/Fact.fs` 的扁平跨域 `AgentFact`（54 case）已按 bounded context 拆分为 7 个 family：`PromptFactCases` / `FallbackFactCases` / `ReviewFactCases` / `ExecutionFactCases` / `OrchestratorFactCases` / `CompanionFactCases` / `ContextFactCases`。`AgentFact` 变为 7-case 分派联合；同名 family 模块（`PromptFact` 等）提供唯一的构造面（`PromptFact.PluginPromptClaimed payload` 形式），fold 按 family 分派。wire 形状逐字节不变（Thoth 只编码 case 名与 payload，不编码声明类型），无需 journal 迁移。
6. `src/Wanxiangshu/Session/Companion.fs` 的 `slotArmed` 裸布尔已升级为一次性 `RecoveryArming`（`Armed of TaskCompletionSource<unit>`）：物理 waiter 而非控制流布尔，重启后 `NotArmed` 语义保持。

## 仍存差距

1. `src/Wanxiangshu/Session/BloggerRuntimeState.fs` 仍含 `BloggerRuntimeCell` 状态乘积（`State`, `PendingOffer`, `Recovery`, `Drain`）。
   - `BloggerToolRecovery` 已改为从 Host transcript 纯推导（`BloggerCrashRecovery.repairState`），但 `cell.Recovery` 字段尚未删除。
   - 后续需拆除 `InFlight/Idle/Parked/Sealed/Disposed` 业务 State，迁移为 single-flight Task ownership。
2. `src/Wanxiangshu/Session/Companion.fs` 仍暴露 `ArmRecoverySlot/DisarmRecoverySlot/IsRecoveryArmed` 查询/设置式 API；底层虽已是 TCS waiter，接口形态仍未迁移为「失败启动一次结构化恢复机会、材料经由 `TryConsumeRecoverySlot` 消费」的直接 CE 流程。

## 阻塞

 BloggerRuntimeCell 拆除与 Companion slotArmed 改造涉及并发生命周期接口与多个 Host 调用点，应作为后续独立 PR；当前 `npm run lint` 与 `npm run check` 全绿，不阻塞主线验证。

## 验收标准

- `npm run lint` 通过。
- `npm run check` 通过。
- 完成剩余差距后删除本文件。
