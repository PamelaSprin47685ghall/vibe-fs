# cancellation-not-propagated — Main

把 cancellation 当 ownership protocol 修，不要只当 exception plumbing 修。

从真正能取消的 principal 出发：request、session、workflow、user command、parent task、lifecycle scope。把同一个 cancellation capability 一路传给所有仍属于它的 effect。不同 API 若使用 AbortController、CancellationToken、process signal、socket close、task group、session abort，就在边界做 adapter，不要在中途把信号丢掉。

目标不变量是：

> **Owner 一旦撤回 authority，仍然属于它的 effect 不得越过 cancellation boundary 继续运行。**

这不意味着所有 operation 都能在同一 instruction 立刻停。有的 library 只能在 safe point cancel；有的 remote service 一旦 commit 就无法撤回。关键是 semantics 必须诚实：cancel 后 effect 要么明确不会发生，要么明确已经发生，要么 outcome 明确 unknown 并由 recovery 处理。只返回一个 `Cancelled` 字符串远远不够。

如果 work 真要活得比 parent 久，必须在 detach 前完成 ownership transfer。一个合格 transfer 通常至少需要：

- 新的 durable owner identity；
- durable work record / queue item / job id；
- 独立 cancellation / retry policy；
- completion / failure 的接收者；
- 不再依赖已经消失的 parent in-memory scope。

常见假修复：

- `catch (Cancelled) { return }`，inner call 继续跑；
- 丢掉 promise/future 就假设底层 operation 停了；
- abort HTTP response，却不 abort DB/process/tool effect；
- 只 cancel direct child，grandchild 继续；
- 设 `isCancelled` flag，但 inner effect 从不读；
- ignore late result，但 late result 仍允许 mutate shared/durable state；
- 把 detached work 叫“background”，却说不出新 owner；
- 发 process signal 后不等待/确认 teardown，resource lifetime 继续模糊。

验证必须在**每个 meaningful phase** cancel，而不是只测刚启动：

- child 开始前；
- network pending 时；
- process 正在跑时；
- child 已完成但 result 尚未 commit 时；
- cleanup 中；
- 被 newer work supersede 后。

观察 physical consequence：process exit、socket close、permit return、child session 停止、later callback 不再 mutate、旧 result 不再在 cancel 之后 publish。

对于不可 recall 的 external effect，还要测 unknown-outcome 分支。Cancel 无法撤销 remote 已接受的 charge；系统不能因此假装“什么也没发生”。这种边界要和 idempotency / reconciliation 一起处理。

完成条件是 logical lifetime 与 physical lifetime 一致；唯一例外是 ownership 已正式 transfer，或协议明确承认 effect irreversible / outcome unknown。

> “我不等了”从来不是 cancellation guarantee。真正的问题是：这份 owned work 现在还被允许做什么？