# cancellation-not-propagated — Enforcer

Cancellation 真正坏掉的样子，是外层已经说“这件事结束了”，但仍然属于它的 child work 继续在世界里运行。

这不只是 resource leak，而是 ownership 在说谎。

Request timeout、user abort、superseding command、session shutdown、cancelled task 都意味着某个 principal 撤回了对其所拥有工作的授权。如果 child process、network call、agent run、stream、database operation、timer、callback 在此之后仍继续，而且没有发生明确 ownership transfer，那么 physical lifetime 已经逃出 logical lifetime。

系统于是出现 orphan work：已经没有合法 principal 仍然想要它，它却还带着 effect capability 在跑。

很多最诡异的事故都从这里来：

- UI 显示 Cancelled，三十秒后旧 request 仍然写入 state；
- tool timeout 已返回，但 process 继续占 file/port；
- 新 computation 已 supersede 旧 computation，旧结果却晚到并 publish；
- client disconnect 后 response 不再处理，downstream billing/API effect 仍继续；
- parent agent abort，child session 继续耗 token，最后回到一个已经不期待它的世界；
- outer cleanup 已结束，但 detached callback 还握着能 mutate 的 capability。

当 cancel/abort 在一层发生，而一个仍由它拥有的 child effect 没有 causal path 收到该信号时，触发本规则。

不要误杀真正 detached work。Durable outbox、queue job、scheduler task、独立 workflow 可以合法活得比 initiating request 久，但前提是 ownership 在 parent 退出**之前**已经正式转移。真正 transfer 必须回答：现在谁拥有它？ownership 在哪 durable？谁能 cancel？完成结果交给谁？

也不要把“ignore result”误当“cancel work”。你不再 await future/promise，只代表你不听结果；underlying effect 是否停止完全是另一件事。

邻近规则：

- `resource-not-scoped`：acquire/release lifetime 本身没有结构保证；
- `permit-leak`：有限 concurrency capacity 没归还；
- `race-first-wins-semantics`：loser/stale work 可能继续，但中心问题是 timing 决定 truth；
- `partial-write-assumption`：cancel 可能打断 write，那条规则管“以为没有 partial effect”。

最好的诊断方法是画 ownership tree。从 cancelled operation 开始，枚举它造成的所有 child effect，对每一个问：

1. cancel 时 child 还属于这个 parent 吗？
2. 如果是，cancel signal 怎么到它？
3. 什么证明 physical effect 已停止或到达定义好的 cancellation boundary？
4. cleanup 有什么保证？
5. 如果已经不属于 parent，ownership 在哪里正式转移？

只要某一项答案是“我们不 await 了”，那就不是 cancellation，而是把仍然带武器的工作遗弃了。

> Cancellation 不是 early return；它是 authority withdrawal，必须沿 ownership graph 传播。