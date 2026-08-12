# unbounded-fanout — Enforcer

Unbounded fan-out 的根病，是把**input cardinality 直接当 concurrency policy**。

一万个 item 表示有一万份 work，并不等于系统现在应该同时拥有一万个 socket、task、process、agent、file handle、DB request 或 remote call。总工作量与 active work 是两个不同维度；不设 bound，就是让 workload size 自己决定资源压力。

典型坏形状：

```text
await Promise.all(items.map(run))
```

如果 `items.length` 没有独立上限，那么一个完全合法的大 input 就能把“需要做很多事”翻译成“现在同时向所有有限资源索债”。这很容易变成 self-inflicted denial of service：memory 膨胀、FD/socket 耗尽、provider 429、DB pool 饱和、scheduler 抖动、retry storm。

以下情形触发：

- 每个 input item 直接 spawn 一个 concurrent operation；
- batch size 可以很大，而 active task 数跟 batch size 1:1；
- agent/process/tool child 没有并发 ceiling；
- recursion/tree traversal 每个 node 都继续并行 fork，depth/width 没 bound；
- retry 自己也 fan-out，导致原 bound 被暗中乘倍；
- parent cancel 后 queued/running child 没明确 drain/cancel semantics；
- “目前数据最多几十条”是唯一 capacity 证明，但 contract 没这个限制。

不要误杀 statically tiny fan-out。固定 3 个 replica、明确 4 个 reviewer、或者 compile-time 小集合，完全可以直接并行。已有 worker pool/semaphore 真正把 active work 限在 finite bound，也不是本规则。普通 CPU `map` 若没有产生 concurrent resource claim，更不是 fan-out。

与 `serial-when-parallel` 正好相反：一个忽略 independence，一个只看 independence、忘了 scarcity。正确答案不是两个极端来回摆，而是**dependency graph 在 finite capacity 下执行**。

真正应该问的是：哪一种有限资源首先因为 concurrency 被消耗？socket、DB connection、memory、CPU worker、provider quota、file descriptor、agent slot？Bound 应从这里来，而不是随手写 `1000`。

还要分清 queue length 与 active count。允许排队 10,000 项，不代表允许同时执行 10,000 项；好的 scheduler 能让 total work 很大，但 active work 始终有限。

> Input size 说明世界有多少事要做；capacity policy 才有资格说明现在同时做多少。