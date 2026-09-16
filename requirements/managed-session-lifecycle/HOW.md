# managed-session-lifecycle — HOW

## 架构机制

### 1. Handle 状态机与单一写控制器

子会话生命周期通过 `HandleProjection` 纯函数折叠与 `HandleController` 统一定义：
- 状态转移为单向不可逆：`Active → CompletedAwaitingJoin → Retired` 与 `Active | CompletedAwaitingJoin → Abandoned`。
- `HandleController` 作为唯一的写入控制器，保证完成单赋值、墓碑状态原子写入以及对隐藏句柄的视图过滤隔离。
- `HandleLinked` 只能重用 child、target、byname、role 与 ownership 完全一致的 durable binding；任一 identity 漂移返回 typed `HandleIdentityConflict`。同一 logical person 的新 work unit 可以在原物理 child 上重新进入 `Active`，`Abandoned` 不可重开。
- Executable proof 只通过注册 surface 穿越 JS 边界：`HandleSurface` 调用真实 `HandleProjection`，`HandleFoldSurface` 调用真实 `ExecutionFactFold`，`Handle/JournalSurface` 以 opaque resource 调用 canonical `EventStore → AgentJournal → HandleController`。测试不得重建 projection、fold、codec、journal、controller 或 join decision。

### 2. 运行时生命周期管理器

- **AttachedSessionRuntime**：管理 Dedicated 会话的池化与作用域生命周期，以 `(ReuseScopeId, Role)` 为键，实现跨轮次的透明复用与故障解绑。
- **SatelliteRuntime**：管理 Companion 叶子会话的单飞创建与精确恢复，实现 `Close(old) → Link(new)` 的原子替换协议。
- **HostForkRuntime**：协调 Fork 子会话的安装、关联持久化、物理执行与超时控制，保障双通道完成事件的分发。

### 3. 中断边界与排空协议

- **权限分型**：区分仅作用于子会话单次物理尝试的 `InterruptAttempt` 与执行完整资源清理的 `AbortSession`。根会话受保护，免受内部意外中断。
- **后继闭合**：内部中断必须显式挂接恢复后继（如求助处理、重试等）或直接发布 `Failed` 终态唤醒父级等待。
- **Abandon 授权闸门**：`HandleController.recordAbandon/cancelChildren` 只能由已确认的 logical parent/session 终止或 child 永久丢失恢复证明调用。`TurnAborted` 只描述当前 attempt，不拥有 child logical-cancel capability；process/plugin shutdown 也只拥有 observer detach capability。
- **双排空语义**：logical cancel 使用 `CancelAndDrain`，允许 durable `HandleAbandoned` + 物理 child teardown；process/plugin shutdown 使用 `DetachAndDrain`，只排空 callback、解绑订阅与本地 runtime/PTY 资源，绝不写 `HandleAbandoned`、绝不 `AbortSession` live agent child。重启后由 durable `HandleLinked(Active)` 恢复。
- **Execution settlement barrier**：logical cancel/delete 在切断新工作准入后，把终止授权交给 `managed-chat-execution` 的 settlement port；该 owner 从 durable projection 选择 exact keys，并以事件完成 barrier。lifecycle 只等待 owner 返回的 durable drained witness，不维护 execution 镜像，不 blind-release session，不运行 timer 或 polling。process/plugin shutdown 不调用该 port。
- **Run closure barrier**：容器复用路径在 execution settlement 与受权 child drain 完成后，调用 `interaction-authority` 持久化 exact LogicalRun closure；只有 run-matched durable closure witness 才允许 `participant-identity` 为同一 `SessionId` 安装 fresh evidence。detach、idle 与 association removal 不参与此判断。
- **有序清理**：明确会话终止时遵循严格的异步排空序列，先切断新工作准入，依次等待 execution settlement barrier、后台调和、经授权的子会话级联取消与持久化写入，再建立 exact run closure，最后发布 lifecycle terminal 并释放或复用底层容器；仅 process shutdown 时则执行无业务终态的 detach 后释放 durable substrate。

### 4. 身份替换收束与固定 DevOps 恢复机制

- **旧身份显式收束**：当探测到历史遗留的未闭合旧会话（如 Coder、Inspector、Browser）时，Lifecycle Manager 通过 `CancelAndDrain` 或显式退休流程驱动其进入 `HandleAbandoned` 或 `HandleRetired` 终态，并关闭其关联通道。历史解码仅供只读重放，不向当前权限目录透传。
- **固定 DevOps 恢复单一权威**：同一道路上的 DevOps 绑定键为 `(RoadId, Role.DevOps)`。物理故障恢复时执行 `ReplacePhysicalSession`，原子使旧物理会话退役并绑定新物理会话，保证全局单一可执行权威。恢复时锁定原 ModelTarget 与 Persona，并在道路关闭时触发 PTY `SIGTERM → SIGKILL` 级联排空。

## DEPENDS ON

- `session-ontology`
- `crash-reconciliation`
- `managed-chat-execution`
- `interaction-authority`
- `participant-identity`


## GAP

- `GAP-031`（CLOSED）：最后两个 `managed-surface.mjs` exports 用常量对象声称 SyncDelegate 复用/取消与 Host PTY 生命周期。SyncDelegate proofs 现通过 opaque runtime 执行真实 durable owner admission、prompt acceptance、completion、deleted-child staging、scope-close lookup、cancel 与 dispose；PTY proofs 通过 controlled backend 执行真实 `HostForkRuntimePty`。全部 consumers 归零后已物理删除 support 文件。
- `GAP-030`（CLOSED）：旧 `tests/support/managed-surface.mjs` 重建了 Handle projection、fact fold、JSON codec、in-memory journal、HandleController 与 join drain；因此测试可以在 production owner 错误时仍由镜像实现自证。现有 consumers 已迁到注册 production surfaces；`recordAbandon` 首胜 proof 穿过真实 canonical EventStore、AgentJournal 与 HandleController；常量 wake trace 已替换成真实 `ExecutionFactFold` replay；对应 mirror exports 全部删除。
- `GAP-029`（CLOSED）：旧实现把 plugin/process shutdown 与未被内部 successor owner 认领的 `TurnAborted` 都升级成 logical parent cancellation，最终经 `CancelAndDrain → HandleController.cancelChildren` 写入 `HandleAbandoned(ParentCancelled)` 并物理 `AbortSession(child)`。现已拆成 `DetachAndDrain` 与 `CancelAndDrain` 两种互斥权限：process/plugin shutdown 只解绑 observer 与本地资源，保留 durable `Active`；ordinary `TurnAborted` 不再拿到 `abortParent` / `CancelSessionChildren` capability；只有 SessionDeleted、显式 successor-less termination 等明确 logical termination 仍可进入 durable abandon。`shutdown-drain-contract.test.mjs`、`interrupt-boundary.test.mjs` 与真实 fork process-detach oracle 已绿；核心实现落于 `506ab7d36`。
