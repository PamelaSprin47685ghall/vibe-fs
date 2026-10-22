# CUT-35：顶层组装与逐域恢复

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-35) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（2026-10-21，eng-cut28d-goose 起刀，eng-cut35b-goose 接手收尾；施工记录见 §9）
- **所属泳道 / 波次**：宿主 / W5
- **切换前置**：`CUT-13`、`CUT-14`、`CUT-16`、`CUT-17`、`CUT-19`、`CUT-22`、`CUT-23`、`CUT-27`、`CUT-28`、`CUT-29`、`CUT-30`、`CUT-31`、`CUT-34`
- **关联护栏**：遵守 `structured-workflow`（原生控制流、不建第二业务运行时、composition root 只管构造与固定次序）与 `crash-reconciliation`（process-local 缓存归零、按需从 durable 投影解析、逐域恢复失败隔离）。

## 2. 主仓顶层组装与恢复源码区域盘点

| 组装与恢复子域 | 主仓真实路径（含 `.fsi`） | 所属编译分片（`.fsproj`） | 核心职责与演进方向 |
|---|---|---|---|
| **插件生命周期与组装根** | `OpenCode/Plugin/{Plugin,SpikePlugin,PluginBoot,PluginHostWiring,PluginSessionWiring,PluginRecoveryWiring}.fs`<br>`OpenCode/Host/{PluginRuntimeScope,PluginSessionScope,PluginRecoveryScope,SharedState}.fs` | `plugin-composition.fsproj`、`opencode-host-pluginruntimescope.fsproj`、`opencode-host-sharedstate.fsproj` | **顶层组装者（C）**：严守 `PluginBoot`（Load Phase 零业务写入）→ `DurabilityActivation`（首次业务消费激活）→ `DisposeAsync`（先关外部订阅与内部准入、再排空 reconciler/ownedWork/runtimeOwner、最后释放 Journal）的固定次序 |
| **持久化聚合桥与领域端口适配器** | `Composition/Durable/{Fact,Fold,Projection,ProjectionState,ProjectionUpdate,HostFactFold,JournalIntegration,DomainFamilyBridge,DelegationProjectionBridge,CompanionFact,ChatExecutionFact,ProviderFailureFact}.fs`<br>`Composition/Durable/{AgentJournalPortAdapter,PromptJournalAdapter,DelegationJournalAdapter,ChatExecutionJournal,SessionStartedAtJournalAdapter}.fs`（建板时列出的 `AttentionConcernJournalAdapter`、`RequirementGroundingJournalAdapter` 已在 CUT-22/26 退出） | `composition-durable-*.fsproj`、`durable-adapter-*.fsproj`、`journal-integration-rule.fsproj` | **待逐族拆薄的持久桥（C）**：每当一个领域卡（如 CUT-01 Work、CUT-03 Trace、CUT-04 Ownership、CUT-11/12/16/22）完成独立状态机切换后，删除 `AgentProjectionSet` 与 `AgentFact` 中对应的旧共享切片，改为组合者直接向各领域 owner 查询 |
| **回合调和与业务工作流扇出** | `Composition/Turn/{Scheduler,ReconcilePass,Program,TurnReconcile,Observation,TurnObservationPort,Binding,Workflow,OrdinaryTurnWorkflow,ReconcileSurface}.fs` | `composition-turn-scheduler.fsproj`、`composition-turn-recovery-workflow.fsproj`、`composition-turn-workflow.fsproj`、`composition-turn-ordinaryturnworkflow.fsproj` | **回合调和与路由（C）**：单飞快照读取，稳定观测按 `SyncDelegate → ManagerWorkflow → OrdinaryTurnWorkflow` 固定顺序扇出 |
| **逐域崩溃恢复接线** | `OpenCode/Plugin/PluginRecoveryWiring.fs`<br>`OpenCode/Host/{SessionRecoveryHost,ChildWorkRecovery,SessionBindingRecovery,LoadRecoverySurface}.fs`<br>`Execution/Session/Recovery/{Model,Coordinator,Surface}.fs`、`RecoveryClosureProjection.fs` | `plugin-composition.fsproj`、`managed-chat-execution.runtime.fsproj`、`execution-session-recovery-model.fsproj`、`execution-session-recoveryclosureprojection.fsproj` | **逐域恢复组合（C）**：在 `AttachDurabilityActivation` 后依次执行遗留子工作作废（`ChildWorkRecovery`）、陈旧 Blogger 请求废弃与同源未启动执行结算、挂起 Plan 交付调和（`reconcilePendingDeliveries`）、`ChatExecution` 恢复信号与固定 DevOps 模型目标重播 |

## 3. 目标

- **用户/维护者当前损失**：`Composition/Durable/Projection.fs` 的 `SessionAgentProjection`（14 个可选字段）与 `AgentProjectionSet`（11 个顶层集合）目前仍是跨领域的聚合投影大结构体；一处改动容易牵动整条编译链。
- **这次完成后可少维护什么**：
  1. 主仓只保留少量显式组合者 C，按调用链选择独立对象、安排“预留提交 → 外部效果 → 结果观察 → 终态结算”的顺序；
  2. 随着各领域个体独立，`Composition/Durable/` 中已被替代的 `AgentFact` 分支、`SessionAgentProjection` 字段与无消费者的 `*JournalAdapter` 逐批退出，不再存在全局业务状态副本或通用消息总线。
- **明确不做**：
  - 不在各领域卡尚未完成前提前重写整个 `PluginTransforms.fs` 或 `Composition/Durable/`；
  - 不构造业务 AST 解释器、Command/Reply 消息总线或恢复程序计数器（`RecoveryStage` / `ResumeAt`，`structured-workflow-002/003`）；
  - 不在恢复阶段自动重放中断的工具或未决物理命令（`crash-reconciliation-017/018/020`）。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/crash-reconciliation/WHAT.md` `[001]`—`[021]`（process-local 状态非恢复权威、从 durable 事实 + 可信物理观察重建、未决外部 effect 先 reconcile、恢复复用普通入口、加载归位不重放命令、外部 effect 逐项闭合、固定 DevOps 恢复唯一权威、进程本地表仅为缓存）。
  - `requirements/structured-workflow/WHAT.md` `[001]`—`[019]`（原生控制流、不建第二业务运行时、存储事实不存执行位置、决策与效果分离、composition root 只负责构造/拓扑/固定次序/排空释放）。
  - `requirements/managed-session-lifecycle/WHAT.md` `[018]`—`[021]`、`[026]`、`[027]`（插件卸载仅 detach 不写 `HandleAbandoned`、`ClearSession` 与 `DisposeSession` 失败隔离且保留首个异常重抛）。
- **对应测试套件**：
  - `requirements/crash-reconciliation/tests/001.test.mjs` 至 `021.test.mjs`
  - `requirements/structured-workflow/tests/001.test.mjs` 至 `019.test.mjs`
  - `requirements/managed-session-lifecycle/tests/018.test.mjs`、`019.test.mjs`、`026.test.mjs`、`027.test.mjs`
  - `requirements/work/tests/048.test.mjs`

## 5. 自治个体与顶层组合（A / B / C）

- **A & B（各独立领域个体与物理适配个体）**：
  - `Work` / `DurableWork`（CUT-01）、`ProcessSession`（CUT-02）、`Trace`（CUT-03）、`Ownership`（CUT-04）、`Resources`（CUT-05）、`WorkRecord`（CUT-06）、`Identity`（CUT-07）、`Capability`（CUT-08）、`Authority`（CUT-09）、`ModelRouting`（CUT-10）、`Dispatch`（CUT-11）、`ChatExecution`（CUT-12）、`Delegation`（CUT-13）、`FailurePolicy`（CUT-14）、`Prefix`（CUT-15）、`Compression`（CUT-16）、`Projection`（CUT-17）、`EventStore`（CUT-18/19）及 OpenCode 宿主适配层（CUT-34）。
  - 它们互不直接持有对方的可变状态或内部句柄。
- **C（顶层组合者：`SpikePlugin`、`PluginBoot`、`PluginRecoveryWiring`、`PluginRuntimeScope`、`TurnWorkflow`）**：
  - **启动与激活次序**：`PluginBoot.create` 完成静态资源与配置装载 → 注册 `AttachDurabilityActivation` 回调 → 首次业务持久化请求触发 `ActivateDurability()` → 后台启动 `PluginRecoveryWiring` 归位流水线。
  - **逐域恢复隔离次序**（`PluginRecoveryWiring.attach`）：
   1. `ChildWorkRecovery.settleOrphanedChildRuns`：上一进程遗留、本进程无法继续持有的活跃子 run 在加载期**作废**（crash-reconciliation-017/018/020：已有 scoped 工作追加 `ExecutionFactCases.ChildWorkVoided`，合法无 scope 历史才走 `ChildRunVoided`，不得由裸 Root 补造准入）。作废不是完成，但它结清父方的未解除义务：join 以 `voided` 报告一次后该工作退役，重启不重复报告。行为侧已接线该事实（回归结果由行为泳道回填，本卡不记绿）；仍为 Open 的 Fission 组记 `FissionFailed(process_interrupted)` 并清本进程 owner 缓存。
    2. `inspectRecoveryWorks`（provider-attempt-recovery-024）：从 `work.sqlite` 读出待核对的 work 及其陈旧请求，单个 work 损坏只记诊断，不阻断其它 work（work-048）；（建板时写的 Blogger 步骤已随 Blogger 退役不复存在）
    3. `reconcilePendingDeliveries`：逐个 work 独立 try/catch 核对挂起的 Plan 交付（已绑定查首条 user prompt 摘要与 `RetirementBlockersFor`，未绑定查精确 title root 会话）；
    4. `SignalChatRecovery(PluginRuntimeReloaded)` 与 `CapacityProjectionReplayed`；
    5. `seedBoundDevOpsModelTargets`：从 `work.sqlite` 的 `DevOpsTarget` 与 `Handles` 投影恢复固定 DevOps 模型绑定；
    6. `MarkRestartGuidancePending()`：为下一次真实用户指令准备一次性重启状态说明。
  - **关闭与排空次序**（`PluginRuntimeScope.DisposeAsync`）：
    1. 停止外部事件订阅（`subscription.Dispose()`），关闭内部准入；
    2. 并发排空 `reconcileShutdown` 与 `StopOwnedWorkAndDrain`；
    3. 释放 `toolRuntime.DisposeAsync()`（仅 detach 进程观察者，不把插件卸载写成 `HandleAbandoned`）；
    4. 释放 `sessions.Dispose()`、`syncDelegateRuntime.Dispose()`；（`scopeDisposers`/`sessionCleanups` 两个挂钩从未有调用方注册，已在 CUT-35 删除；Blogger 已退役）
    5. 最后释放共享 `SharedAgentJournal.releaseAsync` 与 `SharedTerminalBus`，若过程中有异常则重抛首个真实异常。

## 6. 必须能红的最小反例

1. **反例 1（单个损坏历史或恢复异常阻断其它独立工作）**：在 `work.sqlite` 或会话恢复列表中植入一个损坏的 `a-corrupt-work` 和一个合法的 `z-valid-work`，激活恢复后，`a-corrupt-work` 被记录诊断并拒绝正常读取，但 `z-valid-work` 必须顺利完成恢复结算（对应 `work-048` / `crash-reconciliation-005`）。
2. **反例 2（插件卸载把活跃子句柄误写为 `HandleAbandoned` 或提前关闭 Journal）**：当子会话仍在运行或等待 join 时调用 `PluginRuntimeScope.DisposeAsync()`，只允许 detach 本地观察者并在所有后台回调排空后才释放 Journal，严禁写入 `HandleAbandoned` 破坏重启后的可恢复性（对应 `managed-session-lifecycle-018`）。
3. **反例 3（重启后靠进程本地空表拒绝已有子会话）**：重启后不经过任何“全量预热扫描”，直接对持久化中已存在的 `devops` 或 `engineer` 句柄执行 `resume` / `TryChildSession`，必须按需从 durable 投影解析并成功续做，不得返回 `Unknown agent id`（对应 `crash-reconciliation-018/021`）。
4. **反例 4（`ClearSession` / `DisposeSession` 某一步抛错导致后续清理被跳过）**：当某一步释放发生所有权冲突抛错时，后续 linked InternalLeaf 的结算、容量归还及本会话的传感器/注册表清理仍必须全部执行完毕，最后才将首个异常重抛（对应 `managed-session-lifecycle-027`）。

## 7. 精确写集合与删除清单

- **候选写集合**：
  - `src/Wanxiangshu/Composition/Durable/*.fs(.fsi)`
  - `src/Wanxiangshu/Composition/Turn/*.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Plugin/{SpikePlugin,PluginBoot,PluginRecoveryWiring,PluginSessionWiring}.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Host/{PluginRuntimeScope,PluginSessionScope,PluginRecoveryScope,SessionRecoveryHost,SharedState}.fs(.fsi)`
- **旧桥删除条件**：
  - 每当某张上游领域卡（如 CUT-03、06、09、11、12、13、16、22）完成独立状态机切换且不再消费 `AgentProjectionSet` 中的旧字段时，在对应集成批次中同步删除 `Composition/Durable/` 下的旧桥接模块与 `*JournalAdapter`。

## 8. 验证命令（交由 `devops` 执行）

```sh
node scripts/build.mjs
node --test requirements/crash-reconciliation/tests/*.test.mjs requirements/structured-workflow/tests/*.test.mjs
node --test requirements/managed-session-lifecycle/tests/018.test.mjs requirements/managed-session-lifecycle/tests/026.test.mjs requirements/managed-session-lifecycle/tests/027.test.mjs
node scripts/check.mjs
```

## 9. 施工记录与收尾（2026-10-21）

**做法**：按卡 §3“只留少量显式 C、旧桥逐批退出、不先重写 PluginTransforms”的顺序，逐个核对 `Composition/`、插件组装根、插件作用域和逐域恢复接线里每个公开名字的真实调用者。没有消费者的删除，只有本文件使用的不再导出。没有新增运行时、消息总线或恢复程序计数器。

| 提交（master） | 内容 |
|---|---|
| `6f2e8b8f2` | `Composition.Durable.Fold` 里 envelope 与 fact 两份折叠合成一份，删除无消费者的 `isOk`/`unwrap` |
| `1e4f43d2e` | 删除无消费者的 `ReconcileSurface.isAbortControlPlaneWake`；`Turn/Binding`、`Turn/Program`、`PluginRecoveryScope` 中只有本文件调用的三个 helper 不再导出 |
| `429c1fc0a` | 认领 |
| `38892ddb2` | 删除无消费者的 `FamilyRecoveryCoordinator` 单飞装饰器（`Execution/Session/Recovery/Coordinator`），同步改 shard 与 `compile-order.txt` |
| `36c3f3b3f` | 删除孤儿 `OpenCode/Host/ToolSchemaJson`（418 行）。它唯一的两个调用方（PluginBoot 装载期初始化、只读委派 schema）在 `6eef89d1e` 已离开 |
| `9b8564b9d` | 去掉运行时依赖 `effect`：`ToolSchemaJson` 删除后没有任何代码导入它（锁文件里它只作为 `@opencode-ai/plugin` 的 dev 依赖存在） |
| `144e7d020` | 注释更正：`ProjectionUpdate` 不再服务 Cognition 分支（fold 侧是 no-op 墓碑） |
| `cc59d5de5` | `PluginRuntimeScope`：`AttachSessionCleanup`/`AttachScopeDispose` 从未有调用方，清理与释放循环一直在空表上跑，删除；`AttachmentRegistry` 与 `CompanionLeases` 插槽只写不读，连同 `PluginHostWiring` 里的构造一起删除。`RecordPendingAttemptPlan`/`RecordAttemptPlan`/`TryPeekAttemptPlan` 只能经三个没有测试导入的 Surface 导出到达，删除。另删 `PluginSessionScope.CompanionGate`、`Scheduler.RootBindings`，`PluginTransforms` 三个只给 `create` 用的函数不再导出 |
| `22f6e8ac5` | `ChildWorkRecovery` 的 Fission 结算三件（原因常量、`orphanedFissionGroups`、`fissionSettlementFact`）与 `FissionRuntime.bindHandleAffinity` 只在本模块内使用，不再导出 |

**验证**（README §9，base 与候选同输入同环境）：每一轮都由落地循环在最新 master 上全量构建（heavy.lock 只包编译），然后跑卡 §8 套件加相邻包，共 66 个文件（`wt/logs-cut35/files.txt`：crash-reconciliation 全部、structured-workflow 全部、managed-session-lifecycle 006–027、provider-attempt-recovery、delegation-028、host-boundary-028、work-048 等）。当轮的 base/候选对比结果（base 失败名单与候选失败名单相同、无新增）见 `wt/logs-cut35/base.fail`；该结果早于当前作废改动，**未复跑**，不作为当前结论。`node scripts/checks/architecture.mjs` 每轮推送前通过。`node scripts/check.mjs` 的唯一红灯是 `fsharp-control-pyramid`，红在 master 本身（base 117 条，主要来自卡外提交 `6f39cd156` 改的 `RunLifecycle.fs`），候选没有新增条目。

**卡 §6 反例的现状**（正控，不伪造红）：①坏 work 不阻断其它 work 由 work-048、crash-reconciliation-005 覆盖；②卸载不写 `HandleAbandoned`、Journal 最后释放由 managed-session-lifecycle-018 覆盖；③重启后按需从 durable 投影解析由 crash-reconciliation-018/021 覆盖；④`ClearSession`/`DisposeSession` 失败隔离并重抛首个异常由 managed-session-lifecycle-027 覆盖。四者在 base 与候选上结果相同（失败名单里的既有项见 base.fail，不是本卡引入）。

**退出条件核对**（CARDS CUT-35）：每份共享状态都有真实主人。插件作用域现在只剩被读的插槽；聚合投影 `AgentProjectionSet` 仍在，但 9 个 `SessionAgentProjection` 字段都有活读者（见 `wt/logs-cut09k/cut35-prep.md`），没有可删的切片。组合者清单：`SpikePlugin`/`PluginBoot`（装载）、`PluginHostWiring`/`PluginSessionWiring`（接线）、`PluginRecoveryWiring`（逐域恢复）、`PluginRuntimeScope`（关闭次序）、`Composition/Turn`（回合调和与扇出）。

### 先前各卡移交本卡复核的 RETAINED 项：裁决

| 来源 | 内容 | 裁决与理由 |
|---|---|---|
| CUT-27 ④ | Casebook 的 Bookkeeper（LLM 参与者）、Settlement/fatal（knowledge-reuse WHAT 013）、Host 工具接线 | **RETAINED，明示参与者**。Bookkeeper 是一个要调模型的参与者，Settlement 是宿主侧的致命结算，工具接线是宿主适配。三者都是 C 或 A，纯规则已在 casebook/casebook-maintenance/casebook-access 三个库里。再抽只会造出带宿主端口的“库”，不是净改进 |
| CUT-28 | `Execution/Fission/Admission`（整组准入的物理效果排序：建 lane、提交 `LanesCreated`、启动、静默打断 owner、失败回收）与 `Execution/Fission/Runtime`（进程内单飞与缓存） | **RETAINED，明示参与者**。Admission 正是 README 所说的 C：对宿主端口按固定次序做效果，规则部分已归 wanxiangli/fission-group。Runtime 是 crash-reconciliation 意义上的进程本地缓存，不是恢复权威，重启后由 durable 投影重建。本卡只收窄了它的导出 |
| CUT-31 | Sphinx v2 的 Core/Runtime/Persistence/Composition/Wire/Hosts 与 Questionnaire/Probes/Inquiry/Provider 插件 | **RETAINED**。WHAT 009 已经是单 reducer、单 driver、单持久状态，跨 JS 边界拆分没有收益。Hosts 留到 CUT-36 出现第二宿主时再议。其中零消费者的导出由 CUT-38 统一清理 |

这些 RETAINED 是明确保留，不计入“抽干”。CUT-38 按 W6 逐项列出。

**已知不足**：
- `CompanionLeaseRuntime` 在生产中已没有持有者（插件作用域插槽只写不读，本卡已删），现在只被测试面 `SatelliteSurface` 构造。生产的 companion 生命周期走 `PluginSessionScope.Companions`。两条路是否该合并，交给 CUT-38 登记的后继项，本卡不改行为。
- 本卡没有跑全量 `verify.mjs` 和集成套件，那是 CUT-38 的终局验证。

**回退**：每个提交都只删除或收窄，没有改持久格式，也没有新数据。单独 `git revert` 即可。
