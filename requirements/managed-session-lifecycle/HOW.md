# managed-session-lifecycle — HOW

## 架构机制

### 1. Handle 状态机与单一写控制器

子会话生命周期通过 `HandleProjection` 纯函数折叠与 `HandleController` 统一定义：
- 状态转移为单向不可逆：`Active → CompletedAwaitingJoin → Retired` 与 `Active | CompletedAwaitingJoin → Abandoned`。
- `HandleController` 作为唯一的写入控制器，保证完成单赋值、墓碑状态原子写入以及对隐藏句柄的视图过滤隔离。

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

## DEPENDS ON

- `session-ontology`
- `crash-reconciliation`
- `managed-chat-execution`
- `interaction-authority`
- `participant-identity`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| MANAGED-SESSION-001 | `requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` |
| MANAGED-SESSION-002 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-003 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` + `requirements/managed-session-lifecycle/tests/session-recovery.test.mjs` |
| MANAGED-SESSION-004 | `requirements/managed-session-lifecycle/tests/host-fork-agent.test.mjs` |
| MANAGED-SESSION-005 | `requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` |
| MANAGED-SESSION-006 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-007 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-008 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-009 | `requirements/managed-session-lifecycle/tests/handle-abandoned.test.mjs` |
| MANAGED-SESSION-010 | `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| MANAGED-SESSION-011 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-012 | `requirements/managed-session-lifecycle/tests/child-run-projection.test.mjs` |
| MANAGED-SESSION-013 | `requirements/managed-session-lifecycle/tests/host-fork-restart-lifecycle.test.mjs` + `requirements/managed-session-lifecycle/tests/session-recovery.test.mjs` |
| MANAGED-SESSION-014 | `requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs` |
| MANAGED-SESSION-015 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-016 | `requirements/managed-session-lifecycle/tests/interrupt-boundary.test.mjs` |
| MANAGED-SESSION-017 | `requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs` |
| MANAGED-SESSION-018 | `requirements/managed-session-lifecycle/tests/shutdown-drain-contract.test.mjs` + `requirements/delegation/tests/fork-tool.test.mjs` |
| MANAGED-SESSION-019 | `requirements/managed-session-lifecycle/tests/exact-execution-settlement.test.mjs` |
| MANAGED-SESSION-020 | `requirements/managed-session-lifecycle/tests/reused-session-run-closure.test.mjs` |

## GAP

- `GAP-029`（CLOSED）：旧实现把 plugin/process shutdown 与未被内部 successor owner 认领的 `TurnAborted` 都升级成 logical parent cancellation，最终经 `CancelAndDrain → HandleController.cancelChildren` 写入 `HandleAbandoned(ParentCancelled)` 并物理 `AbortSession(child)`。现已拆成 `DetachAndDrain` 与 `CancelAndDrain` 两种互斥权限：process/plugin shutdown 只解绑 observer 与本地资源，保留 durable `Active`；ordinary `TurnAborted` 不再拿到 `abortParent` / `CancelSessionChildren` capability；只有 SessionDeleted、显式 successor-less termination 等明确 logical termination 仍可进入 durable abandon。`shutdown-drain-contract.test.mjs`、`interrupt-boundary.test.mjs` 与真实 fork process-detach oracle 已绿；核心实现落于 `506ab7d36`。
