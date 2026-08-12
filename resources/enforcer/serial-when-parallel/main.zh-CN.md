# serial-when-parallel — Main

把 schedule 改成真实 dependency graph，而不是代码书写顺序。

先把 operation 分成几类：

- 必须等 predecessor data 的；
- 共享同一 mutation owner、必须 serial 的；
- external protocol 明确要求 ordering 的；
- 彼此 independent、只受 finite capacity 限制的。

只有最后一类应该 overlap。给它们一个明确 bounded map / worker pool / semaphore / task group，然后用 deterministic join 聚合结果，让 completion order 只影响 latency，不影响 semantics。

不要把并行仅理解成 `Promise.all`。真正修复还要处理：

- cancellation：parent 失败/取消时 running 与 queued children 怎么结束；
- error policy：fail-fast、collect-all、best-effort 哪一种才符合 domain；
- result association：结果必须按 stable identity 对回原 operation，而不是按 arrival order；
- capacity：bound 来自 socket/provider/CPU/worker 等真实资源；
- side effects：independent read 可以并发，共享 mutable write 不一定可以；
- retry：每个 child 的 retry 不应把 active concurrency 悄悄乘上去。

常见假修复：

- sequential loop 直接换成 unbounded `all`；
- 同时 mutate 同一 object，只因为 operations “看起来不同”；
- completion 谁先回就把谁当 canonical result；
- 为了 readability 保留逐个 await，却让用户一直付 latency；
- bound 写一个巨大魔法常数，只为看起来“有限”；
- parallelize 后忘了对 cancellation/failure 做整体 ownership。

验证要证明两件事同时成立：

1. independent work 真正在 overlap，elapsed time 不再等于所有 waits 之和；
2. 任意合法 completion permutation 都给出同一个 logical result，并且 active count 永不超过 declared bound。

然后故意找一条真实 dependency：让 B 需要 A 的 output。System 必须仍然保留这条 serial edge，证明优化没有把 causality 一起删掉。

正确完成状态不是“并发度越高越好”，而是 schedule 能准确读出系统的两类事实：

> 有 dependency 的工作会等待；没有 dependency 的工作会 overlap；finite resource 有明确 ceiling。

这三件事同时成立，才叫 concurrency design，而不是 async syntax。