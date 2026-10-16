# unbounded-fanout — Lesson

## 评价

### 定义

Unbounded fan-out 的根病，是把**input cardinality 直接当 concurrency policy**。

一万个 item 表示有一万份 work，并不等于系统现在应该同时拥有一万个 socket、task、process、agent、file handle、DB request 或 remote call。总工作量与 active work 是两个不同维度；不设 bound，就是让 workload size 自己决定资源压力。

典型坏形状：

```text
await Promise.all(items.map(run))
```

如果 `items.length` 没有独立上限，那么一个完全合法的大 input 就能把“需要做很多事”翻译成“现在同时向所有有限资源索债”。这很容易变成 self-inflicted denial of service：memory 膨胀、FD/socket 耗尽、provider 429、DB pool 饱和、scheduler 抖动、retry storm。

### 支配原则

并发把 collection 变成对有限资源的 simultaneou claims。根因是把 input cardinality 当 concurrency policy。没有 bound，更大的 list 就意味着更多 socket、memory、file descriptor、process 或 remote pressure，没有独立 ceiling。Bounded concurrency 分开两个不能混淆的量——存在的 work 数量，与现在允许 active 的 work 数量。

### 何时触发

以下情形触发：

- 每个 input item 直接 spawn 一个 concurrent operation；
- batch size 可以很大，而 active task 数跟 batch size 1:1；
- agent/process/tool child 没有并发 ceiling；
- recursion/tree traversal 每个 node 都继续并行 fork，depth/width 没 bound；
- retry 自己也 fan-out，导致原 bound 被暗中乘倍；
- parent cancel 后 queued/running child 没明确 drain/cancel semantics；
- “目前数据最多几十条”是唯一 capacity 证明，但 contract 没这个限制。

### 不应触发

不要误杀 statically tiny fan-out。固定 3 个 replica、明确 4 个 reviewer、或者 compile-time 小集合，完全可以直接并行。已有 worker pool/semaphore 真正把 active work 限在 finite bound，也不是本规则。普通 CPU `map` 若没有产生 concurrent resource claim，更不是 fan-out。

### 与相邻规则区分

与 `serial-when-parallel` 正好相反：一个忽略 independence，一个只看 independence、忘了 scarcity。正确答案不是两个极端来回摆，而是**dependency graph 在 finite capacity 下执行**。

### 判定程序

真正应该问的是：哪一种有限资源首先因为 concurrency 被消耗？socket、DB connection、memory、CPU worker、provider quota、file descriptor、agent slot？Bound 应从这里来，而不是随手写 `1000`。

还要分清 queue length 与 active count。允许排队 10,000 项，不代表允许同时执行 10,000 项；好的 scheduler 能让 total work 很大，但 active work 始终有限。

### 例子

- positive：`items.map(i => fetch(i))` 没有并发 cap 就全部同时 await。
- near-miss：一个 size 8 的 worker pool 从 10,000 个 job 的 queue 拉取，parent abort 时能 cancel。
- counterexample：一个本可并行 fetch 的 pipeline 却逐个 `await`——那是 `serial-when-parallel`。

## 建议

### 现在该做什么

把 total work 与 active work 分开建模。

选择真正被保护的有限资源，再用 bounded map、worker pool、semaphore、queue consumer、task group 等机制给 active work 设置独立 ceiling。队列可以很长，正在执行的数量不能随 input cardinality 一起无限增长。

### 为什么重要

并行 work 竞争有限资源。如果每个 input 都能立刻 active，一个完全合法的大 input 就能变成资源耗尽、provider 过载或自我制造的 denial of service。Bound 让 capacity 成为刻意的 invariant，而不是 workload size 的涌现属性。

### 修复策略

Bound 不应是装饰性常数。它应能解释自己保护什么：

- DB pool 只有 N 个 connection；
- provider quota/SLO 允许 M 个并发；
- CPU worker 与 core/cache pressure 匹配；
- process/agent slot 有明确系统容量；
- memory per task × active count 在预算内；
- file/socket descriptor 留有安全余量。

Fan-out scheduler 还必须拥有 child lifetime：parent cancel 时，queued item 是否直接丢弃？running child 如何 cancel？fail-fast 时剩余 work 是否继续？collect-all 时如何防一个 failure 触发额外无界 retry？这些都不能交给 detached promises 自己决定。

### 决策分支

如果 active work 随 input size 增长，加 finite bound 和 cancellation/drain policy。如果 bound 已存在且 cancellation 已定义，保留它，不要在它旁边再 spawn 额外无界 children。

### 常见假修复

常见假修复：

- 把 `Infinity` 改成一个巨大 `10000`，没有任何 capacity 理由；
- 只在 test 用 semaphore，production 仍一项一个 task；
- spawning loop 看似有 bound，但 task 一创建就 detach，实际 active work 仍无限；
- 给 outer fan-out 限 8，但每个 child 内部又无界 fork 50 个 subtask；
- 失败后所有 queued item 一起 retry，瞬间形成 retry storm；
- 为避免 OOM 退回完全 serial，放弃明明安全可用的有限并发；
- 用 queue 掩盖问题，但 consumer 数量本身仍按 input 动态扩张。

### 验证

验证不要只用 input size 10。给远大于 bound 的 workload，持续观测 active count、memory、socket/process/agent 数，证明并发 ceiling 与 input size 解耦。然后在中途 cancel/fail parent，确认 queued/running children 按声明 policy 收敛，不留下 orphan work。

还要验证 result semantics 与 completion order 无关。Bounded concurrency 只负责资源调度；不能顺手把 scheduler arrival order 变成业务排序或 winner rule。

最后再做吞吐测量。Bound 太小会浪费 capacity，太大会放大 contention。正确数字来自实测与资源模型，可以调整；**“有一个独立于 workload size 的明确 ceiling”**才是不变的设计事实。

### 完成条件

完成时，系统即使接受越来越大的合法 workload，也只会增加 queue/backlog，而不会把 workload cardinality 直接翻译成同等规模的 simultaneous resource demand。

> Scarcity 应由 scheduler 承认，而不是由 OOM、429、FD exhaustion 在事故现场替你宣布。

### Nudge

> Input size 说明世界有多少事要做；capacity policy 才有资格说明现在同时做多少。
