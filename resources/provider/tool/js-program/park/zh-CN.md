park(payload) —— 在这个 await 点挂起程序，把控制权交还编排层。工具调用返回顶级 parked 载荷 `{ handle, payload }`；调用方以 `{ unpark: { handle, value } }` 恢复，或以 `{ abandon: { handle } }` 放弃。

程序视角里 park 只是一次对外部输入的普通 await：恢复值就是调用方经 unpark 注入的东西。现场在宿主中保持存活，直到被恢复或放弃；挂起的程序不算结束，已落盘的写保持原样（as-is）。

`unpark.value` 是任意 JSON：含义与消费方式由 park 点定义。注入错了由下游自然失败；宿主不做预校验。`where` 的歧义分支走同一通道（载荷是 `{ needle, candidates, nearest }`）。

parked 载荷到达时，按从便宜到昂贵的顺序决策：先读载荷。`where` 的歧义携带 `candidates`/`nearest`——诊断指向正确位置就 unpark 注入该 span，程序从原 await 处继续；这是日常结局，代价只是一次调用。abandon 是重武器：它拆掉存活现场、整个程序得重新发起——只在程序逻辑本身必须重写时才用它，那种情况下程序自己的不变量也会拒绝恢复。
