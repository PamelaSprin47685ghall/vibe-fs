# race-first-wins-semantics — Lesson

## 评价

### 定义

Race-first-wins 的本质，是 scheduler、network 或 runtime 偶然替 domain 回答了一个本该由业务事实回答的问题。

最锋利的判定方式只有一句：

> 保持 logical inputs 完全相同，只改变并发结果谁先到，business outcome 就变了。

如果规范说不出“为什么先到本身有业务意义”，系统就把 semantics 外包给 latency 了。

### 支配原则

它经常藏在这些现代写法后面：`Promise.race`、`Task.WhenAny`、hedged request、speculative execution、多个 callback、并行 worker、“first successful result wins”。这些机制本身没有罪。问题出在**physical completion order 被拿来替代真正的 selection rule**。

### 何时触发

典型 accidental semantics：

- 两个 replica 返回不同 version，谁先回谁就成 truth；
- 两个 candidate fix 并行生成，先结束的 patch 直接 publish，没有比较 correctness；
- 多个 caller 竞争初始化 shared state，先完成者悄悄定义 canonical value；
- primary 与 fallback 赛跑，谁先完成谁赢，但 domain 明明偏好其中一种来源；
- cache fill 与 fresher source 并发，“first visible” 被当成 “authoritative”。

### 不应触发

不要误杀真正 timing-based protocol。Leader election、lease acquisition、auction close、明确 first-writer-wins register、lowest-latency replica、hedged read 都可能合法依赖时序，前提是 protocol 真正定义了 identity、freshness、quorum、loser cancellation、tie/conflict behavior。那时 timing 是显式输入，不是 accidental scheduler authority。

也不要因为“并发完成顺序不固定”就触发。并发完全可以乱序结束，最后由 deterministic join/merge 决定真实 outcome。Concurrency 没问题；**未声明的 scheduler sovereignty** 才有问题。

### 与相邻规则区分

邻近规则：

- `lost-update`：stale write 抹掉 accepted update；
- `shared-mutable-concurrency`：多个 actor 共享 mutation authority；
- `serial-when-parallel`：独立工作被无谓串行；
- `flaky-test-tolerated`：不稳定 verdict 被组织正常化。

### 判定程序

只有当这一句最准确时才用本规则：**arrival order 正在替 domain fact 选择答案。**

判定实验要控制 schedule，而不是靠运气跑并发。固定 logical inputs，先压住 A 让 B 先到，记录 outcome；再反过来。如果 outcome 改变，要求 specification 在不引用 runtime timing 的情况下解释为什么这个差异是合法的。解释不了，race 就已经偷偷成为 policy。

修复只有两条正路：

1. 把 arrival order 从 decision 中拿掉，用 required information 上的 deterministic merge / selection law；
2. 承认 first-wins 就是 protocol，并把 stable identity、freshness、loser handling、tie/conflict、durable winner 全部写成正式规则。

不要用微小 sleep、task priority、“primary 通常更快”、或者 retry 去调概率。这些只是让未声明 policy 更少暴露。

### 例子

两个 replica 返回不同 version，谁先回谁就成 truth。两个 candidate fix 并行生成，先结束的 patch 直接 publish，没有比较 correctness。primary 与 fallback 赛跑，谁先完成谁赢，但 domain 明明偏好其中一种来源。

## 建议

### 现在该做什么

先回答一个根本问题：timing 到底是不是产品规则的一部分。

如果不是，就把 scheduler order 从 outcome 中移走。并发仍然可以负责更快拿到信息，但真正 decision 应该依赖 stable domain fact：version、priority、quorum、freshness、score、explicit precedence，或者对所有合法 arrival permutation 都给出同一结果的 merge law。

### 为什么重要

如果 first-wins 真的是 protocol，就别再把它当 implementation shortcut。把它正式写清：

- 竞争者的 stable identity 是什么；
- 哪些 candidate 有资格参与；
- stale candidate 能不能赢；
- success/failure race 是否等价；
- loser 如何 cancel / ignore；
- simultaneous/tie 怎么定义；
- replay 是否必须恢复原 winner；
- winner 如何 durable 记录，避免 restart 后重新赛一次、换出不同答案。

### 修复策略

真正 owner 是拥有这个 business choice 的 layer，不是 `race`、`WhenAny`、callback order 或 completion queue 这些 scheduler API。

如果 decision 需要全部相关信息，优先 deterministic join。Parallelism 仍然可以买 latency：fetch、compute、inspect 都可以并行。关键只是**完成顺序只影响什么时候能做决定，不影响什么决定才正确**。

如果只需要 subset，也要把“subset 为什么足够”写成规则。比如 “3 个 replica 中任意 2 个相同构成 quorum” 是 protocol；“最先回来的两个就算” 只有在 quorum law 能证明所有 eligible subset 对结果等价时才安全。

### 决策分支

若 winner 会持久化，还要测 restart/replay：winner 一旦成为 durable fact，recovery 必须恢复它，而不是重新跑一个不可复现 timing 的 race。

### 常见假修复

常见假修复：

- `sleep(10)` 让想要的 branch 更容易先结束；
- 提高 thread/task priority；
- primary 提前几毫秒启动，靠 head start 保胜率；
- “wrong branch” 赢了就 retry，直到抽到喜欢的 schedule；
- first result 还没验证 admissibility 就先 cancel losers；
- 事后按 completion timestamp 排序就说 deterministic——如果 timestamp 只是 scheduler 产物，仍然没有 domain authority；
- 用 facade 把 winner selection 藏起来，表面 API 看起来很干净。

### 验证

验证时要人为 permutation schedule。对每一组 logical inputs，要么证明所有 arrival order outcome 一样；要么证明不同结果完全符合已声明 first-wins protocol。

### 完成条件

最终 reviewer 应能回答“为什么 X 赢了？”而不需要说“因为它 future 先 resolve”，除非**这句话本身就是正式 domain law**。

> Performance 可以选择最快到达事实的路，但不能把 latency 偷换成 judgment。

### Nudge

> Scheduler 可以决定事实**什么时候到**；除非 domain 明确授权，它不该决定这些事实**是什么意思**。
