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
11. `BloggerRuntime.beginRequest` / `tryTakeInFlight` / `tryPeekInFlight` 已删除（DSL-003）：三个 transition 生产零调用（有定义无调用）；CurrentRequest 无独立 dictionary（注释明言「CurrentRequest IS the InFlight payload. No parallel dict」），`SetCurrentRequest`/`TryPeekCurrentRequest` 直接读写 InFlight payload——单一权威已成立；`tryTakeInFlight` 的一次性消费语义由 `onCycleCommitted`（commit 事实驱动）覆盖。
12. `DrainWindow.Open` 已携带重开它的 `AuthorityRootUserMessageId`（DSL-003）；后升级为 `DrainPermit`（模块私有构造器）：
    - 调查确认 handle lifecycle（`CompletedAwaitingJoin | Abandoned | Retired`）**永不因新 root 解除 seal**——`mainSealedForBlogger` 在 reactivation 后仍为 true，故 drain 窗口不是 durable 镜像，而是「新 root 已到达」的进程内信号；
    - `onReactivate` / `reactivateAfterNewRoot` 现在接收 root id，`PromptIngress` 的 `onAuthorityRoot` 信号携带 `promoteToAuthorityRoot physicalMessageId`；
    - 窗口记录「谁重开的」：更旧的 root 迟到不能重开更新的 seal；
    - `DrainPermit = private DrainPermit of AuthorityRootUserMessageId`：只有 reactivation 路径（新 root 到达 main）能铸造 permit，外部无法为任意 root 伪造 Open 窗口——「谁开的」由类型保证，不再依赖值断言。
13. `Companion.RecoveryArming` 已去掉 `TaskCompletionSource<unit>` 载荷，`TryConsumeRecoverySlot` 已删除（CTX-006 / DSL-003）：
    - `TryConsumeRecoverySlot` 生产零调用（有定义无调用），唯一读 TCS 的路径不存在；
    - 取消未 await 的 Promise 会产生 unhandledRejection，且 Fable 的 `TaskCompletionSource` 只有 `SetCancelled`（无 `TrySetCanceled`，§4.6 盲区）——生产 squash 启动路径一旦触发即崩；
    - 槽现在是纯一次性物理信号：`ArmRecoverySlot`（真实失败置位）/ `IsRecoveryArmed`（squash 决策查询）/ `DisarmRecoverySlot`（squash 启动清位），补了此前零覆盖的契约测试；
14. 崩溃恢复窗口 D 不再 `restoreRuntime Parked`（DSL-003 / ENFORCER-063）：
    - 窗口 D（receipt 存在 + 无 open request + 无 park waiter）置 `Parked` 且不创建 `ParkedTransform`：arming 重启后 `NotArmed` → `mayRecover` 恒 false → 无 squash 路径启动；下个 material 走 `onMaterial` 的 Offer 分支 → `SetPendingOffer` 粘槽 → `TryTakePendingOffer` 全部位于 cycle 提交路径（无人提交）→ 会话 material 永久停摆；
    - 置 `Idle` 时 material 直接 `startFrozen` 推进，cycle 提交后 drain 路径（`tryRefreshMainContextFromJournal`）重查 receipt——恢复语义不丢；
    - `WindowOutcome.RestoredParked` 改名 `ReceiptedIdle`，补防回归断言。
15. `BloggerRuntimeState.Parked` case 已删除（DSL-003）：
    - `Parked` 唯一区分性读点是 `onMaterial`（Parked → Offer vs Idle → Start），其余读点全部与 `Idle` 或 `InFlight` 合并——它是「有 park waiter」这一物理事实的 cell 镜像（权威在 `PluginRuntimeScope.parked` dictionary）；
    - `onMaterial` 增加显式 `hasParkedWaiter: bool` 参数（调用方从 `IParkedTransformHost.HasParked` 读取）：Idle + waiter → Offer、Idle + 无 waiter → Start、InFlight → Skip——Offer 语义不丢；
    - `onCycleCommitted` / `onSquashCommitted`（无 pending）→ `Idle`；`TransitionError.NotParked` 随删；
    - 历史教训（demoting Parked→Idle 曾导致 material 直接 Start 绕过 offer）由 `hasParkedWaiter` 参数承接——dictionary 是权威，不再依赖 cell 状态镜像；
    - 三态 → 二态（`InFlight`/`Idle`），unit/integration/e2e 全量轮验证通过。

## 仍存差距

无。所有已登记差距均已消除或量化为设计约束；本文件完成使命后可删除。

## 阻塞

无。

## 验收标准

- `npm run lint` 通过。
- `npm run check` 通过。
- 完成剩余差距后删除本文件。
- ENFORCER-153 前置 canary：`tests/e2e/scenarios/enforcer-repair-persist.toml`（真实 Host + mock provider）已覆盖——blog 工具空文本触发 ENFORCER-061 empty-text cycle → AABB repair 注入 synthetic `interaction-repair` 消息 → 下一次 provider 请求的消息历史携带 repair 文本（`# Protocol repair`），证明 Host 持久化了 transform 输出（unit 层的 transcript 累积模拟由此获得真实 Host 证据；`info`/`requestKey` 字段契约由 unit 层 `repairRequestKey` 锁定，provider wire 会剥离 `info`）。
