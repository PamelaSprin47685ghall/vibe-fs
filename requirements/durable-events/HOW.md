# durable-events — HOW

## 架构机制与持久化执行链

`durable-events` 提供全局唯一的事件存储与积分管线：

1. **单写者日志与本地提交**：
   - 进程启动时获取唯一的 `WriterId`，独占写入 `.git/wanxiang/events/<WriterId>.ndjson`。
   - `EventStore.append` 获取跨进程门禁锁后，将规范化编码的 `EventEnvelope` 以单行 `JSON + LF` 形式追加到末尾。
   - 大对象内容优先落盘至 `.git/wanxiang/payloads/<PayloadRef>`，确保在事件追加前满足 Payload 完整闭包约束。

2. **Canonical Integrator 与状态折叠**：
   - 系统仅由唯一的 `CanonicalIntegrator` 解释事件历史并派生业务 `Current`；feature 不得自行读取、合并或折叠历史，也不得拥有 NDJSON、SQLite 或其他私有持久化 substrate。
   - `IntegrationRule` 是单事件 oracle：每次只消费一个 `EventEnvelope`，不接收历史 reader 或事件集合。`ReloadLocal` 与 `PrepareLive` 必须复用同一积分程序；Structural、Journal、Strength、Casebook、JsTransaction 的合法事实经 live append 得到的 production Current，必须与从同一 durable history 重启得到的 production Current 逐项相同。禁止维护 boot/live 两套 reducer。
   - 启动或恢复时：先按统一 24h writer-retention predicate 仅枚举活跃本地写者文件，再通过 `EventKWayMerge` 按确定性顺序输入 Integrator 计算当前 `Current` 投影；过期 writer 不读取、不解码。
   - 精确列名的物理收敛/reader（`ProcessEventLog`、`EventKWayMerge`、`WriterStreamSync`、`RetentionSurface`、`MergeSurface`）和 `Verification/TemporalSurface` proof probe 可以观察或排序流，但不得解释业务事件或派生 feature `Current`；该权限不扩展到同目录的其他文件。`IEventStore.TryEvent`、`TryHeads`、`AllHeads` 与 EventStore `Surface.read`/`heads` 均属于受控 reader，只有上述精确 observer、`CanonicalIntegrator` 及精确的 canonical owner 实现（`Persistence/EventStore/Store.fs`、`Persistence/EventStore/Surface.fs`、`OpenCode/Host/WorkspaceEventStore.fs`、`Persistence/Journal/EventStoreJournalWriter.fs`、`Verification/EventStoreWriterSurface.fs`）可以定义或调用；`replayEvents` 与 load/read/scan/fold/merge history 变体仍只允许 `CanonicalIntegrator`。物理 substrate token 与写入 capability（包括 NDJSON/JSONL/数据库、custom store、`File.AppendAllText`/`WriteAllText` 等文件写入、`FileStream`/`StreamWriter`）的额外精确 owner 仅为 `RetentionSurface.fs`、`OpenCode/Host/WorkspaceEventStore.fs`、`Persistence/Journal/EventStoreJournalWriter.fs` 与 `Verification/EventStoreWriterSurface.fs`；同名 feature 文件不继承权限。
   - 运行时追加时：新事件经结构校验后由 Integrator 计算候选 `Current` 与提交闭包；完整 canonical 行物理追加成功后才执行闭包。物理追加失败时丢弃候选，事件索引、Structural heads 与业务 `Current` 均不推进。若业务规则判定语义不合法，则紧随写入 `ProjectionCutTail` 并在返回前触发进程安全退出。

3. **独立 Git Hook 同步**：
   - 运行时完全不进行 Git 树或对象的读写。
   - 当用户触发 Git 远程操作时，安装的 `reference-transaction` 或 `pre-push` Hook 进程拉起同步脚本。
   - Hook 进程读取本地与远端写者流，在同一截止时刻先整体淘汰过期 writer，再完成 retained k-way merge；每个保留的本地完整写者文件封装为单个 Git blob，并发布带 writer activity manifest 的远端快照。

4. **Contract/Runtime 编译边界**：
   - Attention 工具经 `AttentionJournalPort` 读取本域投影、追加本域事实；`AgentJournalPortAdapter.forAttention` 在既有 durable composition 分片内独占 `AgentFact.Attention` 包装、stream／provider-run 接线及预期错误转换。`ToolRegistry` 只选择并注入这个适配器，不再承担该领域的 outer routing。工具闭包与源码归属反例见 structured-workflow 的 `attention-compile-boundary.test.mjs`；工具的实际调用、幂等与错误路径证明见 attention-regulation/HOW，不以记录型 port 宣称物理重启证明。
   - `EventStore.Model.Contract`、`EventStore.Port.Contract` 与 `EventStore.EventVocabulary.Contract` 是业务可见的最小 contract；`Strength.EventVocabulary.Contract` 单独发布四个 Strength event type，不拖入 predictor、replica 或 Host runtime。
   - Git object/ref/physical port 位于独立 Git contract，Git 实现、local process log、codec、Integrator、Journal 与 Host acquisition 位于 Runtime/Adapter 分片。Runtime 依赖 contract；contract 的 direct/transitive ProjectReference closure 不得出现 Runtime/Adapter。
   - 单字段事实族的装配归 composition：`Change`(Orchestrator)、`Fission`、`Concern`、`Attention`(+learning resurface)、`InstitutionalLearning` 的域 fold 只认识自己的切片状态（`OrchestratorProjection`、`FissionProjectionState`、`ConcernProjectionState`、`AttentionProjectionState`、`InstitutionalLearningProjectionState`），切片写回与拒绝渲染由 `Composition/Durable/ProjectionUpdate.apply*` 与 `Fold.foldAgentFact` 的 `Orchestrator` 分支承担（delegation-029 / durable-events-023 的「唯一装配点」）。`Change/Fold` 采用切片签名并具有闭合 `OrchestratorFoldRejection`（`fact` 渲染 `PublishClaimed`）；`m6-slice-boundary.test.mjs` 断言不存在额外的包装层、`change-fold` 不得声明 `composition-durable-projection` 且 fold 源不得出现 `AgentProjectionSet`/`FoldRejection`。
   - 多切片事实族同样交出聚合写入：`PromptFactFold`（authority）、`ProviderFailureFactFold`（provider）与 `CompanionFactFold`（context）的 fold 只接收窄查询函数与切片值（`authorityOf`/`runtimeStartCount`、`providerFailuresOf`、`associations`/`companionOf`/`xTraceOf`），返回闭合变更列表（`PromptAuthorityProjectionChange`、`ProviderFailureProjectionChange`、`CompanionProjectionChange`）与闭合拒绝（`…FoldRejection` + `fact`/`message`）。`Composition/Durable/DomainFamilyBridge.{fs,fsi}` 是唯一把变更写回聚合、把域拒绝渲染成 spine `FoldRejection` 的位置，`Fold.foldAgentFact` 只做分派。`m6-slice-boundary.test.mjs` 断言这三个 fold 源不得出现聚合／写入代数／spine 拒绝词汇，且 `Composition/` 之外不得调用 `ProjectionUpdate.updateSession|updateAuthority|updateCompanion`。
   - 域 fold `ContextFactFold`（`Context/Companion/Blogger`，写入 `BloggerCycles`／`Enforcement`／`Blog`／`PrefixEpoch` 切片并在成功时注销辅助可见性）只接收 `bloggerCyclesOf`／`enforcementOf`／`blogOf`／`prefixEpochOf` 窄查询，返回 `ContextProjectionChange` 与闭合 `ContextFoldRejection`。前缀观测的吸收策略（`StalePrefixEpoch`／`CandidateNotNew`／`CompactionAlreadyReanchored` 吸收为零写入）归 `PrefixEpochProjection.describe`，`ProjectionUpdate.prefixOutcome` 与 context fold 共用同一份判定。`ContextProjectionBridge` 负责桥接。`m6-slice-boundary.test.mjs` 的域 fold 断言集合包含 `Context/Companion/Blogger/ContextFactFold.fs`，并把 `retireAuxiliaryInjectionVisibility` 列入「`Composition/` 之外不得调用」的写入辅助。
- `event-store-compile-boundary.test.mjs` 直接消费既有 compile-shard 与 subsystem inventory，按显式分片与 subsystem 验证闭包排除及预算；`scripts/compile-owner.mjs` 对每个目标分片生成 aggregate-order、零 ProjectReference 的 flat project，并以一次 Fable invocation 编译。

## GAP

- `durable-events-011` / `durable-events-025`（CLOSED）：Git blob 仅在 remote sync 边界存在（单文件对应单 blob）与 persistence cut stores 禁 optional fatal hook（唯一 fatal 归 composition）已闭合，落点 `tests/011.test.mjs` 与 `tests/025.test.mjs`。

