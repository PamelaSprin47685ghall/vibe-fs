# serial-when-parallel — Lesson

## 评价

### 定义

Serial-when-parallel 不是“没把代码写成 Promise.all”这种表面问题，而是**本来独立的 work 被实现习惯虚构出因果关系**。

如果 A 与 B 之间没有 data dependency、没有共同 mutable owner、没有 protocol ordering，那么让 B 必须等 A 完成，就是让 elapsed time 变成 `A + B`，而真实 dependency graph 明明只要求 `max(A, B)`。

### 支配原则

并发应该跟随独立性。如果 A 与 B 不共享 premise 或 mutable owner，让 B 等 A 就发明了问题里没有的 causality。反方向的错误是 unbounded fan-out；正确模型是用 finite capacity 执行的 dependency graph。

### 何时触发

以下情形触发：

- 两个 independent HTTP/read/validation/tool call 逐个 await；
- report 的多个 source 可独立 fetch，却按列表一个个请求；
- 多个独立 check 都必须完成，但没有理由让 later check 等 earlier check；
- map over items 明明可有限并行，却因为 loop 写法全部串行；
- workflow 中“先做 A 再做 B”只来自代码排列，没有 domain/protocol explanation。

### 不应触发

不要误杀真正 dependency。B 需要 A 的 ID、shared state 只有一个 owner、external protocol 要求先 commit A 才能 B、或者 capacity 已经是 1，这些都是真 serial edge。RuleBook 不奖励“并行看起来高级”。

也不要从一个极端跳另一个极端。`items.map(async ...)` 直接把一万个 input 同时启动，会落入 `unbounded-fanout`。正确目标是：**dependency graph + finite capacity**。

### 与相邻规则区分

与 `serial-investigation` 区分：后者专门审 evidence gathering，还考虑 first-evidence anchoring；本规则是通用 runtime/tool work scheduling。与 `blocking-event-loop` 区分：那里一个 task 霸占 shared progress engine；这里即便 executor 仍可工作，也把 independent operations 人工排队。与 `race-first-wins-semantics` 区分：并行后不能让谁先完成就决定 truth。

### 判定程序

判定时给 operations 画 graph：edge 只能来自 data、ownership、protocol order。没有 path 的节点就是 concurrent candidate。然后另画一条 capacity ceiling，告诉系统同时允许多少 active work。

如果你无法解释一条 serial edge 除了“代码就是这样写的”，那条 edge 多半不是 requirement，而是 latency debt。

### 例子

- positive：两个用于构建同一 report 的 independent HTTP GET 没有 protocol 理由却逐个 await。
- near-miss：step B 需要 step A 的 parsed id；serialization 是 dependency，不是 smell。
- counterexample：independent fetches 在 bounded pool 下运行，结果被 deterministic 地 join。

## 建议

### 现在该做什么

把 schedule 改成真实 dependency graph，而不是代码书写顺序。

先把 operation 分成几类：

- 必须等 predecessor data 的；
- 共享同一 mutation owner、必须 serial 的；
- external protocol 明确要求 ordering 的；
- 彼此 independent、只受 finite capacity 限制的。

只有最后一类应该 overlap。给它们一个明确 bounded map / worker pool / semaphore / task group，然后用 deterministic join 聚合结果，让 completion order 只影响 latency，不影响 semantics。

### 为什么重要

Artificial serialization 拉长 critical path，同时传达一个不存在的 dependency。正确并发是架构压缩：让 elapsed time 反映最长的真实 dependency chain，而不是无关 waits 的总和。

### 修复策略

不要把并行仅理解成 `Promise.all`。真正修复还要处理：

- cancellation：parent 失败/取消时 running 与 queued children 怎么结束；
- error policy：fail-fast、collect-all、best-effort 哪一种才符合 domain；
- result association：结果必须按 stable identity 对回原 operation，而不是按 arrival order；
- capacity：bound 来自 socket/provider/CPU/worker 等真实资源；
- side effects：independent read 可以并发，共享 mutable write 不一定可以；
- retry：每个 child 的 retry 不应把 active concurrency 悄悄乘上去。

### 决策分支

- 如果 operations 不共享 data 或 owner，在 finite bound 下 overlap，并 deterministic join。
- 如果 later work 需要 earlier results 或 protocol order，保留那条 serial edge。
- 如果当前代码已经无界 overlap，那是 `unbounded-fanout`——加 capacity，不要把所有东西重新 serial。

### 常见假修复

常见假修复：

- sequential loop 直接换成 unbounded `all`；
- 同时 mutate 同一 object，只因为 operations “看起来不同”；
- completion 谁先回就把谁当 canonical result；
- 为了 readability 保留逐个 await，却让用户一直付 latency；
- bound 写一个巨大魔法常数，只为看起来“有限”；
- parallelize 后忘了对 cancellation/failure 做整体 ownership。

### 验证

验证要证明两件事同时成立：

1. independent work 真正在 overlap，elapsed time 不再等于所有 waits 之和；
2. 任意合法 completion permutation 都给出同一个 logical result，并且 active count 永不超过 declared bound。

然后故意找一条真实 dependency：让 B 需要 A 的 output。System 必须仍然保留这条 serial edge，证明优化没有把 causality 一起删掉。

### 完成条件

正确完成状态不是“并发度越高越好”，而是 schedule 能准确读出系统的两类事实：

> 有 dependency 的工作会等待；没有 dependency 的工作会 overlap；finite resource 有明确 ceiling。

这三件事同时成立，才叫 concurrency design，而不是 async syntax。

### Nudge

> Independence 应该在 schedule 中可见；capacity 也应该在 schedule 中可见。正确并发既不制造虚假等待，也不假装资源无限。
