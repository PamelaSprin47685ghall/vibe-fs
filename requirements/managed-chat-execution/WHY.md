# managed-chat-execution — WHY

## 不可替代的存在理由

一个可复用 `SessionId` 会连续承载多个物理用户消息。若模型准入、provider 启动、失败裁决、取消与恢复分别凭 session 状态或进程内租约判断，同一物理消息会在崩溃边界获得互相矛盾的执行结论：重复调用 provider、永久占用容量，或把前一轮的终态误投影到后一轮。

`managed-chat-execution` 因而拥有每个 `(SessionId, PhysicalUserMessageId)` 的 durable managed chat execution：从物理消息被接受，到 provider 确实开始，再到唯一终态。`SessionId` 只是可复用物理容器，不能代替消息级执行身份。

## 核心不变量

- exact key 是 `(SessionId, PhysicalUserMessageId)`；不存在 session-scoped current execution 真相。
- durable acceptance 先于容量获取及任何 provider effect。
- `Accepted`、`ProviderStarted` 与 terminal disposition 是可版本升级、可重放的事实，不是进程回调状态。
- terminal 单赋值；重复事件重放幂等；冲突终态 fail closed。
- 崩溃恢复只由 durable projection 与显式容量、Host 或失败事件推进，不读取墙钟，不轮询猜测。
- lease、waiter、callback、queue node 等 process-local artifact 可重建但不可持久化。

## 违反边界的后果（RED）

- 新物理消息复用旧消息的 binding、容量或终态。
- provider 已启动但 durable 历史仍停在无法区分“从未启动”的状态。
- cancel/delete 通过 SessionId 粗放释放另一个执行的容量。
- 重启通过超时补写终态、重试 provider，或恢复已失效的进程内 waiter。
- 测试复制 terminal、release 或 dispatch 公式，再 mutation 该副本；production 破坏后伪 oracle 仍保持全绿。

## DEPENDS ON

- `durable-events`
- `interaction-authority`
- `participant-identity`
- `execution-model-routing`
- `execution-failure-policy`
- `host-boundary`
