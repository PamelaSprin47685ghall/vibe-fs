park(payload) — 在这个 await 点挂起当前程序，把控制权交还给编排者。本次工具调用返回顶级 parked 载荷 `{ handle, payload }`；调用方用 `{ unpark: { handle, value } }` 恢复，或用 `{ abandon: { handle } }` 放弃。

程序视角里 park 只是普通地 await 一个外部输入：恢复时 await 的值就是调用方通过 unpark 注入的值。现场在宿主中保持存活，直到被恢复或放弃；挂起的程序不算结束，它已经落盘的写入仍留在盘上（as-is）。

`unpark.value` 是任意 JSON：注入值是什么意思、如何消费，由 park 点定义。注入错了就让下游自然失败，宿主不做预校验。`where` 的歧义分支走同一条通道（载荷为 `{ needle, candidates, nearest }`）。
