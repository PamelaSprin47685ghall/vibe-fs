# change-integration — HOW

## 架构机制

### Relay + deterministic artifact admission 发布循环

1. **等待 Relay outcome**：`OrchestratorProgram` 只消费 `ManagerLoopSignal.Candidate` / `ManagerLoopSignal.Continue` / `ManagerLoopSignal.ExceptionalTerminal`。`observeRelayProgram` 的 signal 时间线记为 `await:Candidate` / `await:Continue` / `await:ExceptionalTerminal`。无有效证书的 retirement 即 `Continue` 信号，只以无参数 `ContinueLoop` 继续沿同一 `ManagerJob` 循环。
2. **确定性 artifact admission**：有效证书先与当前 `WorkspaceSnapshotId` 对齐，再检查 unmerged entries、candidate 与 target head。任何 binding change 都先 `InvalidateCertificate`。
3. **rebase / conflict 都回同一循环的下一 loop continuation**：rebase 成功记录 `RebasedCandidateReady` 后以无参数 `ContinueLoop` 继续，`continuations` 按调用顺序记为确定性 `surface-loop-N`（首个 continuation 即 `surface-loop-1`），时间线记为 `continue:surface-loop-N`；冲突记录 `ConflictDetected` 后同样以无参数 `ContinueLoop` 继续。原因（`InitialRebaseRequired` / `TargetAdvanced` / `PublishCasMissed` / `WorkspaceChangedAfterAssessment` / `ArtifactAdmissionUnmerged`）只保留在 `invalidations` 侧的 durable 事实中，不作为 continuation 参数。没有 ResumeManager/Reviewer 分支。
4. **短门禁 CAS 发布**：只有已经在当前 target head 上有新证书的 rebased candidate 才进入 `IntegrationGate`。门内重读 target、写 `PublishClaimed` 并 ff-only；CAS miss 释放门禁后按 `invalidate:PublishCasMissed` → `git:rebase` → `continue:surface-loop-N` 顺序继续。

### 工作树演化与并行协调

1. **DevOps 自修快照演化**：DevOps 在验证期间执行非架构级修复推进工作树快照时，当前证书绑定失效（`WorkspaceChangedAfterAssessment`），发布管线拒绝直接发布并触发下一轮独立评估。
2. **任务边界与固定快照**：Manager 调度并行 Engineer 与 DevOps 时，通过任务边界避免重叠写入，或使运行针对固定快照；严禁依赖文件前后哈希相同推断运行期未变，严禁使用自动架构分类器。
3. **汇聚后验证**：Orchestrator 汇聚多道路成果时，在共享分支推进前必须重新运行汇聚后验证并保留证书失效合同。

### Host 装配与异步调用边界

`ToolRuntimeScope.OrchestratorHostFor` 在既有 composition 层静态构造 `OrchestratorHostDeps` 与 `OrchestratorHost`，由 Fable 保留 `ContinueManagerLoop` 等高阶参数的调用约定；`CaptureWorktreeSnapshot` 全链保持 `WorkspaceSnapshotId`。系统严禁使用 `createObj + box` 和动态加载替代声明依赖，以保证双参数回调始终遵循 Promise 调用约定，防止 publication 过程因计算未决而失败。会话取消和 scope 卸载分别直调 `CancelAndDrain`、`DetachAndDrain`，不以缺模块时的默认成功绕过资源结算。包含 typed 装配的 scope shard 必须通过真实 focused Fable 编译（闭包 942 个 source）以及 Host 验收。

端到端验证由 `requirements/verification-system/tests/e2e/014.test.mjs` 承载，完整覆盖 `Continue → IncumbencyOpened → Accepted → ConflictDetected → RebasedCandidate → Published` 链路，事件与 outstanding 判定严格按规范执行。

### RuntimePath 摘要依赖

`change-fact` 分片中的 `RuntimePath` 仅通过 `HostDigest.sha256Hex` 为非 Git 工作区计算状态目录，直接引用既有零领域依赖的 `runtime-platform/digest`；GitSubject、Identity、Change facts 与 durable fact 引用保留。源码和签名不变，Git common-dir 优先、失败后的 XDG／home 路径选择及缓存语义均未修改。

在 `bd99d71e7` 上，该分片声明递归闭包从 29 项目／158 个 `.fs/.fsi` 输入降至 27／148；独立 Fable 编译通过 186 parsed sources（`7711a827c1d3`）。以三个既有签名为输入保守选择反向消费者，单次 focused Fable 并集通过 1432 parsed sources／1394 items（`6f04afa1af0a`），并非签名发生了修改。新隔离产物 smoke 验证 Unicode 工作区的独立 Node SHA-256、已配置／未配置 XDG 的 fallback 与真实 Git common-dir 路径；该 smoke 专注于路径与摘要推导，不证明锁竞争、journal 恢复或 linked worktree 全分支。

### 门禁与工作树资源管理

- **IntegrationGate 互斥**：基于文件锁实现的轻量互斥机制，仅覆盖目标分支指针更新窗口，不侵占 Relay assessment、Manager work、rebase 或冲突处理。
- **WorktreeResource 生命周期**：为每个任务分配由 `ManagerJobId` 绑定的独立工作树，完成任务后原子清理，崩溃恢复时按持久化事实精准收拢或复用。projection 不 fold 成唯一「最新 case」，structured-workflow-003 vs structured-workflow-009 消歧保证恢复重入直接由事实与当前外部 head 判定。
