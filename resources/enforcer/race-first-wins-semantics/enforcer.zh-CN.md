# race-first-wins-semantics — Enforcer

Race-first-wins 的本质，是 scheduler、network 或 runtime 偶然替 domain 回答了一个本该由业务事实回答的问题。

最锋利的判定方式只有一句：

> 保持 logical inputs 完全相同，只改变并发结果谁先到，business outcome 就变了。

如果规范说不出“为什么先到本身有业务意义”，系统就把 semantics 外包给 latency 了。

它经常藏在这些现代写法后面：`Promise.race`、`Task.WhenAny`、hedged request、speculative execution、多个 callback、并行 worker、“first successful result wins”。这些机制本身没有罪。问题出在**physical completion order 被拿来替代真正的 selection rule**。

典型 accidental semantics：

- 两个 replica 返回不同 version，谁先回谁就成 truth；
- 两个 candidate fix 并行生成，先结束的 patch 直接 publish，没有比较 correctness；
- 多个 caller 竞争初始化 shared state，先完成者悄悄定义 canonical value；
- primary 与 fallback 赛跑，谁先完成谁赢，但 domain 明明偏好其中一种来源；
- cache fill 与 fresher source 并发，“first visible” 被当成 “authoritative”。

不要误杀真正 timing-based protocol。Leader election、lease acquisition、auction close、明确 first-writer-wins register、lowest-latency replica、hedged read 都可能合法依赖时序，前提是 protocol 真正定义了 identity、freshness、quorum、loser cancellation、tie/conflict behavior。那时 timing 是显式输入，不是 accidental scheduler authority。

也不要因为“并发完成顺序不固定”就触发。并发完全可以乱序结束，最后由 deterministic join/merge 决定真实 outcome。Concurrency 没问题；**未声明的 scheduler sovereignty** 才有问题。

邻近规则：

- `lost-update`：stale write 抹掉 accepted update；
- `shared-mutable-concurrency`：多个 actor 共享 mutation authority；
- `serial-when-parallel`：独立工作被无谓串行；
- `flaky-test-tolerated`：不稳定 verdict 被组织正常化。

只有当这一句最准确时才用本规则：**arrival order 正在替 domain fact 选择答案。**

判定实验要控制 schedule，而不是靠运气跑并发。固定 logical inputs，先压住 A 让 B 先到，记录 outcome；再反过来。如果 outcome 改变，要求 specification 在不引用 runtime timing 的情况下解释为什么这个差异是合法的。解释不了，race 就已经偷偷成为 policy。

修复只有两条正路：

1. 把 arrival order 从 decision 中拿掉，用 required information 上的 deterministic merge / selection law；
2. 承认 first-wins 就是 protocol，并把 stable identity、freshness、loser handling、tie/conflict、durable winner 全部写成正式规则。

不要用微小 sleep、task priority、“primary 通常更快”、或者 retry 去调概率。这些只是让未声明 policy 更少暴露。

> Scheduler 可以决定事实**什么时候到**；除非 domain 明确授权，它不该决定这些事实**是什么意思**。