# cancellation-not-propagated — Lesson

## 评价

### 定义

Cancellation 真正坏掉的样子，是外层已经说“这件事结束了”，但仍然属于它的 child work 继续在世界里运行。

Request timeout、user abort、superseding command、session shutdown、cancelled task 都意味着某个 principal 撤回了对其所拥有工作的授权。如果 child process、network call、agent run、stream、database operation、timer、callback 在此之后仍继续，而且没有发生明确 ownership transfer，那么 physical lifetime 已经逃出 logical lifetime。

### 支配原则

这不只是 resource leak，而是 ownership 在说谎。

系统于是出现 orphan work：已经没有合法 principal 仍然想要它，它却还带着 effect capability 在跑。

### 何时触发

当 cancel/abort 在一层发生，而一个仍由它拥有的 child effect 没有 causal path 收到该信号时，触发本规则。

### 不应触发

不要误杀真正 detached work。Durable outbox、queue job、scheduler task、独立 workflow 可以合法活得比 initiating request 久，但前提是 ownership 在 parent 退出**之前**已经正式转移。真正 transfer 必须回答：现在谁拥有它？ownership 在哪 durable？谁能 cancel？完成结果交给谁？

也不要把“ignore result”误当“cancel work”。你不再 await future/promise，只代表你不听结果；underlying effect 是否停止完全是另一件事。

### 与相邻规则区分

邻近规则：

- `resource-not-scoped`：acquire/release lifetime 本身没有结构保证；
- `permit-leak`：有限 concurrency capacity 没归还；
- `race-first-wins-semantics`：loser/stale work 可能继续，但中心问题是 timing 决定 truth；
- `partial-write-assumption`：cancel 可能打断 write，那条规则管“以为没有 partial effect”。

### 判定程序

最好的诊断方法是画 ownership tree。从 cancelled operation 开始，枚举它造成的所有 child effect，对每一个问：

1. cancel 时 child 还属于这个 parent 吗？
2. 如果是，cancel signal 怎么到它？
3. 什么证明 physical effect 已停止或到达定义好的 cancellation boundary？
4. cleanup 有什么保证？
5. 如果已经不属于 parent，ownership 在哪里正式转移？

只要某一项答案是“我们不 await 了”，那就不是 cancellation，而是把仍然带武器的工作遗弃了。

### 例子

很多最诡异的事故都从这里来：

- UI 显示 Cancelled，三十秒后旧 request 仍然写入 state；
- tool timeout 已返回，但 process 继续占 file/port；
- 新 computation 已 supersede 旧 computation，旧结果却晚到并 publish；
- client disconnect 后 response 不再处理，downstream billing/API effect 仍继续；
- parent agent abort，child session 继续耗 token，最后回到一个已经不期待它的世界；
- outer cleanup 已结束，但 detached callback 还握着能 mutate 的 capability。

## 建议

### 现在该做什么

把 cancellation 当 ownership protocol 修，不要只当 exception plumbing 修。

从真正能取消的 principal 出发：request、session、workflow、user command、parent task、lifecycle scope。把同一个 cancellation capability 一路传给所有仍属于它的 effect。不同 API 若使用 AbortController、CancellationToken、process signal、socket close、task group、session abort，就在边界做 adapter，不要在中途把信号丢掉。

### 为什么重要

目标不变量是：

> **Owner 一旦撤回 authority，仍然属于它的 effect 不得越过 cancellation boundary 继续运行。**

### 修复策略

如果 work 真要活得比 parent 久，必须在 detach 前完成 ownership transfer。一个合格 transfer 通常至少需要：

- 新的 durable owner identity；
- durable work record / queue item / job id；
- 独立 cancellation / retry policy；
- completion / failure 的接收者；
- 不再依赖已经消失的 parent in-memory scope。

### 决策分支

这不意味着所有 operation 都能在同一 instruction 立刻停。有的 library 只能在 safe point cancel；有的 remote service 一旦 commit 就无法撤回。关键是 semantics 必须诚实：cancel 后 effect 要么明确不会发生，要么明确已经发生，要么 outcome 明确 unknown 并由 recovery 处理。只返回一个 `Cancelled` 字符串远远不够。

### 常见假修复

常见假修复：

- `catch (Cancelled) { return }`，inner call 继续跑；
- 丢掉 promise/future 就假设底层 operation 停了；
- abort HTTP response，却不 abort DB/process/tool effect；
- 只 cancel direct child，grandchild 继续；
- 设 `isCancelled` flag，但 inner effect 从不读；
- ignore late result，但 late result 仍允许 mutate shared/durable state；
- 把 detached work 叫“background”，却说不出新 owner；
- 发 process signal 后不等待/确认 teardown，resource lifetime 继续模糊。

### 验证

验证必须在**每个 meaningful phase** cancel，而不是只测刚启动：

- child 开始前；
- network pending 时；
- process 正在跑时；
- child 已完成但 result 尚未 commit 时；
- cleanup 中；
- 被 newer work supersede 后。

观察 physical consequence：process exit、socket close、permit return、child session 停止、later callback 不再 mutate、旧 result 不再在 cancel 之后 publish。

对于不可 recall 的 external effect，还要测 unknown-outcome 分支。Cancel 无法撤销 remote 已接受的 charge；系统不能因此假装“什么也没发生”。这种边界要和 idempotency / reconciliation 一起处理。

### 完成条件

完成条件是 logical lifetime 与 physical lifetime 一致；唯一例外是 ownership 已正式 transfer，或协议明确承认 effect irreversible / outcome unknown。

> “我不等了”从来不是 cancellation guarantee。真正的问题是：这份 owned work 现在还被允许做什么？

### Nudge

> Cancellation 不是 early return；它是 authority withdrawal，必须沿 ownership graph 传播。
