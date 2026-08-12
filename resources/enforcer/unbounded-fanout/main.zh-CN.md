# unbounded-fanout — Main

把 total work 与 active work 分开建模。

选择真正被保护的有限资源，再用 bounded map、worker pool、semaphore、queue consumer、task group 等机制给 active work 设置独立 ceiling。队列可以很长，正在执行的数量不能随 input cardinality 一起无限增长。

Bound 不应是装饰性常数。它应能解释自己保护什么：

- DB pool 只有 N 个 connection；
- provider quota/SLO 允许 M 个并发；
- CPU worker 与 core/cache pressure 匹配；
- process/agent slot 有明确系统容量；
- memory per task × active count 在预算内；
- file/socket descriptor 留有安全余量。

Fan-out scheduler 还必须拥有 child lifetime：parent cancel 时，queued item 是否直接丢弃？running child 如何 cancel？fail-fast 时剩余 work 是否继续？collect-all 时如何防一个 failure 触发额外无界 retry？这些都不能交给 detached promises 自己决定。

常见假修复：

- 把 `Infinity` 改成一个巨大 `10000`，没有任何 capacity 理由；
- 只在 test 用 semaphore，production 仍一项一个 task；
- spawning loop 看似有 bound，但 task 一创建就 detach，实际 active work 仍无限；
- 给 outer fan-out 限 8，但每个 child 内部又无界 fork 50 个 subtask；
- 失败后所有 queued item 一起 retry，瞬间形成 retry storm；
- 为避免 OOM 退回完全 serial，放弃明明安全可用的有限并发；
- 用 queue 掩盖问题，但 consumer 数量本身仍按 input 动态扩张。

验证不要只用 input size 10。给远大于 bound 的 workload，持续观测 active count、memory、socket/process/agent 数，证明并发 ceiling 与 input size 解耦。然后在中途 cancel/fail parent，确认 queued/running children 按声明 policy 收敛，不留下 orphan work。

还要验证 result semantics 与 completion order 无关。Bounded concurrency 只负责资源调度；不能顺手把 scheduler arrival order 变成业务排序或 winner rule。

最后再做吞吐测量。Bound 太小会浪费 capacity，太大会放大 contention。正确数字来自实测与资源模型，可以调整；**“有一个独立于 workload size 的明确 ceiling”**才是不变的设计事实。

完成时，系统即使接受越来越大的合法 workload，也只会增加 queue/backlog，而不会把 workload cardinality 直接翻译成同等规模的 simultaneous resource demand。

> Scarcity 应由 scheduler 承认，而不是由 OOM、429、FD exhaustion 在事故现场替你宣布。