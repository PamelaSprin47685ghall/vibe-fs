本次工具调用返回 parked 载荷，因为程序在 `await js.park(payload)` 处挂起了。

- `handle` 标识挂起现场。它只消费一次：用 `{ unpark: { handle, value } }` 恢复，或用 `{ abandon: { handle } }` 放弃。
- `payload` 是程序传给 park 的值，是程序自己的数据，不是宿主的裁决。
- 挂起现场是进程内活物：随进程死亡。只要它还没被表态，交付就会被泄漏兜底门禁拦截。

恢复时把 `value` 注入为 park 的 await 结果，让程序继续执行。放弃时以 ABANDONED 标记拒绝程序，让原生 `try/finally` 清理执行；结局记 Abandoned，无终值。
