# P-01 / P-03 会话端口迁移：剩余消费方清单（2026-10-22 快照）

本文件保存原先记在根目录交接文里的精确残留清单，供 `BOARD.md` 与 `REGISTRY.md` 引用。
状态以本目录与 `REGISTRY.md` 的后续条目为准；这里的文件与行号是 2026-10-22 的观察，迁移前须重新核对。

## HNI-P03 transcript 消费方迁移（dispatch recovery 已落地，fork recovery 缓迁）

### 已落 master 的改动（Commit 1）

- **SHA**：`80182c4cb`（`feat(HNI-P03): route dispatch recovery off ISessionSnapshotPort onto ITranscriptPort`），ff 推至 `origin/master`。
- **改动范围**：`Interaction/Dispatch/Recovery.fs(i)` 与 `RecoverySurface.fs`。
  - `PromptRecovery.reconcile` 入参从 `snapshotOpt: ISessionSnapshotPort option` 切至 `transcriptOpt: ITranscriptPort option`。
  - 单一宿主读取由 `snapshot.GetMessages` 切换为 `transcript.Messages`；宿主读取错误通过私有 `HostError -> string` 映射（保留 transport/refusal 原文，Unsupported 输出标准格式）。
  - `RecoverySurface` 私有 stub `Snapshot` 替换为 `Transcript`（通过 `SessionSnapshotPort.projectMessages` 投影构建）。
  - 认领与恢复法则（`DispatchLaw.recover`、user 角色 + `PromptKey` 匹配、`RecoveryTailWindow = 50`）原样保留，业务决策与重试策略零变化。
- **门禁情况**：`architecture.mjs` OK；`adapter-thinness.mjs` 141 文件 / 33329 行，product-reference 588（基线 689），without-port 134（基线 149）；`package-lock.json` JSON 校验通过；pyramid 门禁在改动文件中 0 违规。

### Fork Recovery（Commit 2 / `ChildRecoveryWorkflow`）缓迁说明与剩余物盘点

依照主控「若字段缺失或扇出过大需触及禁止修改边界，精确记录遗留项而不强行开启宽泛迁移」裁决，Fork recovery 路径暂缓迁入，原因如下：

1. **严重跨层扇出与所有权边界**：
   - `ChildRecoveryWorkflow.Ports` 的注入点为 `Execution/Delegation/Fork/Host/Restart.fs`。
   - `HostForkRestart` 的 snapshot 来自 `Execution/Delegation/Fork/Host/Runtime.fs`（`HostForkRuntime`）。
   - `HostForkRuntime` 构造点由 `Change/Host/Host.fs` 及 `OpenCode/Tools/ToolRuntimeScope.fs` 共同供给。
   - `ToolRuntimeScope` 是贯穿整个宿主插件的超大型依赖中枢（跨 24 个文件、126 处调用），其下直接挂载并驱动：
     - `Execution/Delegation/Fork/OpenCode/JoinTool.fs`
     - `Execution/Delegation/Fork/OpenCode/Tool.fs`
     - `Execution/Delegation/SyncDelegate/Surface.fs`
     - `Execution/Fission/OpenCode/Tool.fs`
     - `Mission/Work/OpenCode/DeliverTool.fs`
     - `Mission/Work/OpenCode/ReviewBackendManager.fs`
   - 若在 `ChildRecoveryWorkflow` 或 `HostForkRuntime` 强行切换端口，会引发对上述 24 个适配工具文件的连锁修改；这些文件属于 Fork、Mission 及 Fission 业务线资产，超出了本泳道边界。

2. **字段结构差异与反向无损投射禁止**：
   - `ChildRecoveryWorkflow` 自身仅消费消息的 `Role`、`ParentId`、`Parts` 及 `Finish = "stop"`，这些字段在中立 `TranscriptMessage` 中皆有对应。
   - 但同源下游工具（如 `ReviewBackendManager.fs:134,143`）深度依赖 `ISessionSnapshotPort` 专有的 `SessionToolPart`、`ToolParts` 阵列及 `locateToolCall`。
   - 中立端口 `ITranscriptPort` 将工具调用与结果融合为 `TranscriptPart.ToolUse`，未暴露出 OpenCode 私有的 `SessionToolPart`。
   - HNI 架构严厉禁止为了适配而做 lossy reverse projection（将 `ITranscriptPort` 有损逆向包裹回伪 `ISessionSnapshotPort`）。因此，必须等待下游工具链完成领域化抽象后，方可对 Fork / Runtime 进行底座切换。

3. **全仓剩余 44 个引用 `ISessionSnapshotPort` 的文件分类盘点**：
   - **宿主与主干配置**：`Change/Host/Types.fs(i)`。
   - **Turn 状态协调**：`Composition/Turn/ReconcilePass.fs(i)`、`ReconcileSurface.fs`、`Scheduler.fs(i)`。
   - **前缀装配与网络层**：`Context/Prefix/OpenCode/Wire.fs(i)`、`XWireSurface.fs`。
   - **Delegation 建议席位**：`Execution/Delegation/Advisor/Seat.fs(i)`。
   - **Fork 恢复与委派**：`Execution/Delegation/ChildRecoveryWorkflow.fs(i)`、`Fork/Host/Restart.fs(i)`、`Fork/Host/Runtime.fs(i)`、`SyncDelegate/Batching.fs(i)`、`SyncDelegate/OpenCode/Batching.fs(i)`。
   - **Fission 金丝雀与验证**：`Execution/Fission/Canaries.fs`、`Verification/TemporalSurface.fs`。
   - **工具中枢与插件接线**：`Mission/Work/OpenCode/DeliverTool.fs`、`OpenCode/Host/HostSignalBootstrap.fs(i)`、`OpenCode/Host/PluginHost.fs(i)`、`OpenCode/Host/PluginHostInterop.fs(i)`、`OpenCode/Plugin/PluginHostWiring.fs(i)`、`OpenCode/Plugin/PluginRecoveryWiring.fs`、`OpenCode/Tools/ToolRegistry.fs(i)`、`OpenCode/Tools/ToolRuntimeScope.fs(i)`。
   - **适配器旧基础设施（定义源与提供者）**：`Foundation/HostSessionSnapshot.fs(i)`、`OpenCode/Host/SessionSnapshotPort.fs(i)`、`OpenCode/Host/TranscriptPort.fs(i)`。

## HNI-06 / HNI-08 宿主会话生命周期（host-lifecycle）剩余项

本段按「precisely documented as left」记录宿主生命周期线的现状：不开始大范围跨泳道改动，不虚构 DONE 状态。

### 1. 已落地成果（已合并至 `origin/master`）

1. **P-01 宿主适配器已落地**（`00ae8c156`，`feat(HNI): complete the P-01 session port in the OpenCode adapter`）：
   - `src/Wanxiangshu/OpenCode/Host/SessionSnapshotPort.fs(i)` 中的 `SdkSessionPort` 与 `HttpSessionPort` 均完整实现了中立端口 `ISessionPort`（`src/Wanxiangshu/Host/Ports/Sessions.fs(i)`）的全部 7 个动词：`Create`、`Parent`、`Children`、`Abort`、`Interrupt`、`Close`、`FindByTitle`。
   - 修复了两处 wire 缺陷：HTTP `Close` 此前误调 `POST /session/{id}/abort`，现已修正为 `DELETE /session/{id}`；SDK `Close` 发送了 SDK 所需的 `path/sessionID/headers` 载荷。
   - 适配层中所有失败均映射为中立 `HostError`（`NotFound`、`Refused`、`Transport`），消除了裸 `Result<_, string>` 通道与宿主原始载荷泄漏。
2. **P-03 转录读取消费方切片 1 已落地**（`b862832a3`）：`SessionRecoveryHost.fs(i)` 构造函数由 legacy `ISessionSnapshotPort` 迁往中立 `ITranscriptPort`，恢复判定改走 `TranscriptMessage list`。
3. **P-03 转录读取消费方切片 2 已落地**（`80182c4cb`）：`Interaction/Dispatch/Recovery.fs(i)` 与 `RecoverySurface.fs` 的 `PromptRecovery.reconcile` 迁往 `ITranscriptPort option`。

### 2. 未迁移的生命周期消费方与确切残留符号（Leftovers）

以下消费方**未开始改动**，仍严格依赖 legacy `ISessionHostPort`（`src/Wanxiangshu/Foundation/HostSessionPort.fs(i)`）与 OpenCode 专属结构：

#### A. Fork 集群（`Execution/Delegation/Fork/Host/`）

- **涉及符号与文件**：
  - `src/Wanxiangshu/Execution/Delegation/Fork/Host/Agent.fs:278`：`runtime.Sessions.CreateChildSession(runtime.ParentId, { Title = Some agentId; Agent = Some agentName; Directory = runtime.DirectoryOf agentId })`
  - `src/Wanxiangshu/Execution/Delegation/Fork/Host/Agent.fs:200` & `ChildDispatch.fs:43`：`sessions.AbortSession childId`
  - `src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs(i)`：`HostForkRuntime` 构造函数接收 `sessions: ISessionHostPort`。
- **调用方依赖与签名变更需求**：
  - `HostForkRuntime` 构造点在 `Change/Host/Host.fs:199`、`OpenCode/Tools/ToolRuntimeScope.fs:593`、`Fork/Host/HostForkPtySurface.fs:110,190,212,260`、`Fork/JoinSurface.fs:349` 共 7 处实例化（`Runtime.fsi:22` 声明 `sessions: ISessionHostPort *`）。切到 `ISessionPort` 时需提供兼容构造或在域内适配，否则将触及 `OpenCode/Tools`、`Change/Host` 等本泳道白名单外文件。
  - `Agent.fs` 的 `forkNewChild` 需从 `CreateChildSession` 切换为 `sessionPort.Create(Some runtime.ParentId, { Title = Some agentId; Role = Some (AgentRole agentName); Workspace = runtime.DirectoryOf agentId |> Option.map WorkspacePath })`，并将 `HostError` 转化为产品层期望的诊断/拒绝原因。

#### B. 同步委派集群（`Execution/Delegation/SyncDelegate/`）

- **涉及符号与文件**：
  - `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fs:337`：`sessions.ListChildren physicalOwner`
  - `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fs:356`：`sessions.CreateChildSession(owner, { Title = Some(SyncDelegatePhysicalIdentity.title scope role agentName); Agent = Some agentName; Directory = childDirectory })`
  - `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fs:407`：`sessions.InterruptAttempt delegateSession`
  - `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fs(i)`：`SyncDelegateRuntime` 构造函数接收 `sessions: ISessionHostPort`。
- **调用方依赖与签名变更需求**：
  - `SyncDelegateRuntime` 在 `Execution/Delegation/SyncDelegate/Surface.fs:912` 与 `OpenCode/Plugin/PluginSessionWiring.fs:97` 实例化（`Runtime.fsi:40` 声明 `sessions: ISessionHostPort *`）。
  - `managedChildObservation`（`Runtime.fs:313`，参数 `children: ChildSessionInfo list`（原 `OpenCodeChildInfo list`，HNI-10 slice C 就地改名）在 `:317`）需改为 `SessionInfo list`，匹配字段由 `child.Agent = Some agentName` 改为 `child.Role = Some (AgentRole agentName)`。
  - `observeChild` 需调用 `sessionPort.Children physicalOwner`，`createChild` 需调用 `sessionPort.Create`。

#### C. 顾问席位（`Execution/Delegation/Advisor/`）

- **涉及符号与文件**：
  - `src/Wanxiangshu/Execution/Delegation/Advisor/Seat.fs:23` / `.fsi:12`：`AdvisorSeatDeps` record 包含 `Sessions: ISessionHostPort`。
  - `src/Wanxiangshu/Execution/Delegation/Advisor/Seat.fs:133`：`deps.Sessions.CreateChildSession(callerId, options)`。
  - `src/Wanxiangshu/Execution/Delegation/Advisor/Seat.fs:462`：`use _subscription = deps.Sessions.SubscribeTerminal(childId, onTerminal)`。
- **调用方依赖与语义/端口缺失不匹配**：
  - `AdvisorSeatDeps` 在 `OpenCode/Tools/ToolRegistry.fs:552` 以显式 record 构造，修改 `AdvisorSeatDeps` 字段类型会打断 `ToolRegistry.fs`。
  - **语义/端口缺失**：`AdvisorSeat` 依赖 `SubscribeTerminal` 监听子会话完结。中立 `ISessionPort` 是纯无状态生命周期动词接口，**不包含**任何 `SubscribeTerminal` / `SubscribeFutureTerminal`（终端事件归属 P-07 信号源或进程总线）。若直接将 `deps.Sessions` 换成 `ISessionPort`，终端订阅将无处安放。

#### D. OpenCode 会话适配层与插件宿主（`OpenCode/Host/Sessions.fs` 与 `PluginHost.fs`）

- **涉及符号与文件**：
  - `src/Wanxiangshu/OpenCode/Host/Sessions.fs:421` 中的 `InjectedSessionPort` 仅 `interface ISessionHostPort with`，**尚未实现** `ISessionPort`。
  - `InjectedSessionPort`（`Sessions.fs:74` 构造参数含 `underlyingPort: IOpenCodePort option`、`eventPort: IEventObservationPort`、可选 `familyParent`）持有进程内家族树与终端监听表：`activeListeners`、`parentChildMap`、`childParents`、`knownRoots`（源码标记均为 `// DSL-MUTABLE: resource`），其 `CreateChildSession` 依 `findRestoredRoot` / `tryFindKnownRoot` 解析 `familyRoot`。
  - 中立 `ISessionPort` 注释明确规定：「Family-root resolution, managed-child registry and ancestry proof stay in the product (Execution/Session); the port only performs the host action.」。目前这一层分离尚未在产品层收敛，OpenCode 插件宿主（`PluginHost.fs:125`）仍直接将 `InjectedSessionPort` 作为 `ISessionHostPort` 向上注入。

### 3. `OpenCodeChildOptions` / `OpenCodeChildInfo` 存续现状（2026-10-22 HNI-10 slice C 更新）

- 声明位置：`src/Wanxiangshu/Foundation/HostSendContract.fs(i)`。
- **slice C 已把「名字」退役**（就地改名，不留别名）：`OpenCodeChildOptions` → `ChildSessionOptions`、`OpenCodeChildInfo` → `ChildSessionInfo`、`OpenCodePromptOptions` → `SessionPromptOptions`、`OpencodeModel` → `HostModel`。原别名 `type SessionPromptOptions = OpenCodePromptOptions` 已删除，`SessionPromptOptions` 现在是主名（`requirements/durable-events/tests/022.test.mjs` 的探针本来就写的 `SessionPromptOptions`，仍匹配）。
- **但「类型」本身仍不能删除**：它们仍是 Foundation 里的适配器形状类型，尚未变成中立的 `SessionSpec` / `SessionInfo` / `PromptRequest` / `ModelRoute`。
- 存续依据：`git grep` 仍有 **36 处引用 / 17 个文件**（`OpenCode/Host/SessionsSurface.fs` 7 处、`OpenCode/Host/OpenCodeContract.fs(i)` 各 3 处、`Foundation/HostSessionPort.fs(i)` 各 3 处、`Interaction/Dispatch/DispatchSurface.fs(i)` 各 2 处、`Change/Host/Surface.fs:140`、`Execution/Session/Attachment/SatelliteSurface.fs`、`Execution/Delegation/Fork/OpenCode/ToolSurface.fs`）。必须待后续消费方全部平移至 `SessionSpec` / `SessionInfo` 后方可统一退役。
- **仓主裁决选项**：
  - **选项 1（不做，等消费方平移后再删）**：等 P-01/P-02/P-08 的消费方全部切到中立端口后，把这四个 Foundation 类型直接删掉。优点是最终形态干净；缺点是宿主形状的名字在此期间继续扩散。
  - **选项 2（就地改名，保留类型）**：把四个类型就地改成中立名，**JS 侧字段名一字不改**。**本切片执行的就是选项 2**。
- **为什么字段名不能改**：Fable 生成的 JS 属性名就是 `providerID` / `modelID` / `variant`，而 `ModelRoute` 的字段是 `Provider` / `Model` / `Variant`。**类型名在 JS 里完全擦除，字段名不会**——所以改名（类型名）安全，改字段名会打破冻结的 manifest 模块和约 13 个 JS 测试的 `deepEqual`。`HostModel` 留在叶子 shard `host-boundary.host-opencode-types`（`ModelRoute` 在 `host-boundary.host-neutral-ports`，15 个消费 shard 中有 11 个不引用它），所以 `HostModel` → `ModelRoute` 这一步仍然被 JS 字段名锁和 shard 图双重阻塞。
- `IOpenCodePort`（`OpenCode/Host/OpenCodeContract.fs(i)`，12 个文件 / 37 处）在 `InjectedSessionPort` 与其余 `ISessionHostPort` 消费方切到 `ISessionPort` / `IPromptPort` 之前不能删；它的签名类型已在本切片就地改名。
- **另记**：slice B 之后 `Host/SecondHost/HostPlugin.fs:319` 还有一处重复的 `RuntimePath.stateRoot` 读取（那里按 git worktree 顶层算 `stateRoot`，而 `ports.Paths.StateRoot` 是适配器 memo 的 `RuntimePath.stateRoot workspace`；linked worktree 与主 worktree 未必相等，未证明等价，故保留原样并在此登记）。

### 4. 交付与后继建议

1. **不进行未经编译验证的强行迁移**：由于涉及多个核心 shard 的跨模块接口与构造调用点，强行推进而不进行真实构建验证将带来极高的类型不匹配与运行时崩溃风险。
2. **后续迁移路径**：
   - 引入适配层过渡适配器（如 `toSessionPort : ISessionHostPort -> ISessionPort`）允许各子系统逐个平滑切换，避免一次性破损未在白名单内的调用点（如 `ToolRegistry.fs`）。
   - 明确生命周期终端事件（`SubscribeTerminal`）在各个消费方的归属（分离生命周期管理与事件观察）。
   - 待所有调用点切至中立 `ISessionPort` 后，方可着手退役 `ISessionHostPort`、`ChildSessionOptions` 与 `ChildSessionInfo`（这三个**名字**已在 HNI-10 slice C 就地退役；此处说的是**类型**本身的退役）。
