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
7. `src/Wanxiangshu/Session/BloggerRuntimeState.fs` 的 `BloggerRuntimeCell.Recovery` 字段与 `BloggerRuntime.markInteractionNudgeIssued` / `markAabbRepairConsumed` 转换器已删除（ENFORCER-153 / DSL-003）：
   - 推导逻辑移入 `src/Wanxiangshu/Application/Reconciliation/BloggerRecoveryProbe.fs`（`repairState` / `rejudgeToolRecovery` / `rejudgeFromEvidence`）；
   - 热路径（`EnforcerHost.handleContinuation`）通过注入的 `RecoveryStageProbe` 端口读取推导结果，`BloggerRuntimeCell` 不再携带 Recovery 镜像；
   - `repairState` 两段式 claim 检查：同一 terminal 重入 → `InteractionNudgeIssued(terminal)`；任一旧 claim 存在且出现新 pure-prose terminal → `InteractionNudgeIssued(claimedRun)`（AABB 语义失败）；transcript 含注入的 repair 消息（同 requestKey）→ `AabbRepairConsumed`；
   - AABB 消耗的可见证据是注入的 `interaction-repair` synthetic 消息：Host transform 输入是完整 snapshot，后续回合可见该消息（测试 harness 模拟输出累积）。

8. `BloggerRuntimeCell.PendingOffer` 字段与 `BloggerRuntime.tryTakePending` 已删除（ENFORCER-050 / DSL-003）：该字段在所有构造点恒为 `None`（有读取无数据），唯一 staging 权威是 `PluginRuntimeScope` 的独立 `pendingOffer` dictionary；`TransitionError.PendingWhileInFlight` 随之删除。
9. `BloggerRuntimeState.Sealed` case 与全部 Sealed 镜像读写已删除（DSL-003 / ENFORCER-047）：
   - `onSeal` / `isSealed` 生产零调用（死代码）删除；
   - handle seal 唯一真相源是 durable `AgentProjection.mainSealedForBlogger`——所有 `forceSeal` 调用点（EnforcerHost / Coordinator 10+ 处）此前都已在 `mainSealedForBlogger` / `blocksNew`（durable 判断）之后执行，镜像只能重复并漂移（`onMainMaterial` 的 stale 处理即漂移证据）；
   - `forceSeal` 变为只关内存 drain 窗口；`onReactivate` 只重开 drain 窗口（不再 Sealed→Idle 转换）；`blocksNewRequest` 由 `durableHandleSealed` 参数驱动；
   - `CancelParked` / `SetCurrentRequest` 的 Sealed 保持/拒绝分支删除（durable 检查在每次入口先行）；
   - `TransitionError.Sealed` 与 Coordinator / EnforcerHost 的错误分支随之删除。

10. `BloggerRuntimeState.Disposed` case 与 `onDispose` 已删除（DSL-003）：唯一写入点 `PluginRuntimeScope.DeleteSession` 在 lock 内**写后立即 Remove**（registry 删除），`GetBloggerRuntime` 对缺失项返回 `BloggerRuntime.empty`（Idle）——生产中没有任何读点能观察到 Disposed cell（有写入无读取）；owner lifetime 的物理事实是 registry 项的存在性，不是状态标签；`DecisionEffect.Disposed` 与 `TransitionError.Disposed` 随之删除。
11. `DrainWindow.Open` 已携带重开它的 `AuthorityRootUserMessageId`（DSL-003）：
    - 调查确认 handle lifecycle（`CompletedAwaitingJoin | Abandoned | Retired`）**永不因新 root 解除 seal**——`mainSealedForBlogger` 在 reactivation 后仍为 true，故 drain 窗口不是 durable 镜像，而是「新 root 已到达」的进程内信号；
    - `onReactivate` / `reactivateAfterNewRoot` 现在接收 root id，`PromptIngress` 的 `onAuthorityRoot` 信号携带 `promoteToAuthorityRoot physicalMessageId`；
    - 窗口记录「谁重开的」：更旧的 root 迟到不能重开更新的 seal（为后续私有 `DrainPermit` 权限化铺路）。

## 仍存差距

1. `src/Wanxiangshu/Session/BloggerRuntimeState.fs` 仍含 `BloggerRuntimeCell` 状态乘积（`State`, `Drain`）。
   - 后续需拆除 `InFlight/Idle/Parked` 业务 State，迁移为 single-flight Task ownership；
   - `Drain` 应替换为模块外不可构造的私有 `DrainPermit`（仅「新 Authority Root 重开 drain」路径可得；窗口已记录 root 身份，权限化只需把 `Open of root` 改为不可伪造的 permit 传递）。
2. `src/Wanxiangshu/Session/Companion.fs` 仍暴露 `ArmRecoverySlot/DisarmRecoverySlot/IsRecoveryArmed` 查询/设置式 API；底层虽已是 TCS waiter，接口形态仍未迁移为「失败启动一次结构化恢复机会、材料经由 `TryConsumeRecoverySlot` 消费」的直接 CE 流程。

## 阻塞

 BloggerRuntimeCell 拆除与 Companion slotArmed 改造涉及并发生命周期接口与多个 Host 调用点，应作为后续独立 PR；当前 `npm run lint` 与 `npm run check` 全绿，不阻塞主线验证。

## 验收标准

- `npm run lint` 通过。
- `npm run check` 通过。
- 完成剩余差距后删除本文件。
- ENFORCER-153 前置 canary：需在 e2e 层证明注入的 `interaction-repair` synthetic 消息在 Host 下一次 transform 输入的完整 snapshot 中仍然存在（当前由 unit 层 harness 的 transcript 累积模拟，真实 Host 持久化行为尚未有 canary 覆盖）。
