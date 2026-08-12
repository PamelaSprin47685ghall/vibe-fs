# race-first-wins-semantics — Main

先回答一个根本问题：timing 到底是不是产品规则的一部分。

如果不是，就把 scheduler order 从 outcome 中移走。并发仍然可以负责更快拿到信息，但真正 decision 应该依赖 stable domain fact：version、priority、quorum、freshness、score、explicit precedence，或者对所有合法 arrival permutation 都给出同一结果的 merge law。

如果 first-wins 真的是 protocol，就别再把它当 implementation shortcut。把它正式写清：

- 竞争者的 stable identity 是什么；
- 哪些 candidate 有资格参与；
- stale candidate 能不能赢；
- success/failure race 是否等价；
- loser 如何 cancel / ignore；
- simultaneous/tie 怎么定义；
- replay 是否必须恢复原 winner；
- winner 如何 durable 记录，避免 restart 后重新赛一次、换出不同答案。

真正 owner 是拥有这个 business choice 的 layer，不是 `race`、`WhenAny`、callback order 或 completion queue 这些 scheduler API。

如果 decision 需要全部相关信息，优先 deterministic join。Parallelism 仍然可以买 latency：fetch、compute、inspect 都可以并行。关键只是**完成顺序只影响什么时候能做决定，不影响什么决定才正确**。

如果只需要 subset，也要把“subset 为什么足够”写成规则。比如 “3 个 replica 中任意 2 个相同构成 quorum” 是 protocol；“最先回来的两个就算” 只有在 quorum law 能证明所有 eligible subset 对结果等价时才安全。

常见假修复：

- `sleep(10)` 让想要的 branch 更容易先结束；
- 提高 thread/task priority；
- primary 提前几毫秒启动，靠 head start 保胜率；
- “wrong branch” 赢了就 retry，直到抽到喜欢的 schedule；
- first result 还没验证 admissibility 就先 cancel losers；
- 事后按 completion timestamp 排序就说 deterministic——如果 timestamp 只是 scheduler 产物，仍然没有 domain authority；
- 用 facade 把 winner selection 藏起来，表面 API 看起来很干净。

验证时要人为 permutation schedule。对每一组 logical inputs，要么证明所有 arrival order outcome 一样；要么证明不同结果完全符合已声明 first-wins protocol。

若 winner 会持久化，还要测 restart/replay：winner 一旦成为 durable fact，recovery 必须恢复它，而不是重新跑一个不可复现 timing 的 race。

最终 reviewer 应能回答“为什么 X 赢了？”而不需要说“因为它 future 先 resolve”，除非**这句话本身就是正式 domain law**。

> Performance 可以选择最快到达事实的路，但不能把 latency 偷换成 judgment。