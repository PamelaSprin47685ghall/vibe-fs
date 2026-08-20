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
- **有序清理**：会话关闭时遵循严格的异步排空序列，先切断新工作准入，依次排空后台调和、子会话级联取消与持久化写入，最后释放底层资源。

## DEPENDS ON

- `session-ontology`
- `crash-reconciliation`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| MANAGED-SESSION-001 | `requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` |
| MANAGED-SESSION-002 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-003 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-004 | `requirements/managed-session-lifecycle/tests/host-fork-agent.test.mjs` |
| MANAGED-SESSION-005 | `requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` |
| MANAGED-SESSION-006 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-007 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-008 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-009 | `requirements/managed-session-lifecycle/tests/handle-abandoned.test.mjs` |
| MANAGED-SESSION-010 | `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| MANAGED-SESSION-011 | `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-012 | `requirements/managed-session-lifecycle/tests/child-run-projection.test.mjs` |
| MANAGED-SESSION-013 | `requirements/managed-session-lifecycle/tests/host-fork-restart-lifecycle.test.mjs` |
| MANAGED-SESSION-014 | `requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs` |
| MANAGED-SESSION-015 | `requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-016 | `requirements/managed-session-lifecycle/tests/interrupt-boundary.test.mjs` |
| MANAGED-SESSION-017 | `requirements/managed-session-lifecycle/tests/interrupt-successor.test.mjs` |
