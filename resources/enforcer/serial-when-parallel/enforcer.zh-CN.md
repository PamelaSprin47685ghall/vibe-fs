# serial-when-parallel — Enforcer

Serial-when-parallel 不是“没把代码写成 Promise.all”这种表面问题，而是**本来独立的 work 被实现习惯虚构出因果关系**。

如果 A 与 B 之间没有 data dependency、没有共同 mutable owner、没有 protocol ordering，那么让 B 必须等 A 完成，就是让 elapsed time 变成 `A + B`，而真实 dependency graph 明明只要求 `max(A, B)`。

以下情形触发：

- 两个 independent HTTP/read/validation/tool call 逐个 await；
- report 的多个 source 可独立 fetch，却按列表一个个请求；
- 多个独立 check 都必须完成，但没有理由让 later check 等 earlier check；
- map over items 明明可有限并行，却因为 loop 写法全部串行；
- workflow 中“先做 A 再做 B”只来自代码排列，没有 domain/protocol explanation。

不要误杀真正 dependency。B 需要 A 的 ID、shared state 只有一个 owner、external protocol 要求先 commit A 才能 B、或者 capacity 已经是 1，这些都是真 serial edge。RuleBook 不奖励“并行看起来高级”。

也不要从一个极端跳另一个极端。`items.map(async ...)` 直接把一万个 input 同时启动，会落入 `unbounded-fanout`。正确目标是：**dependency graph + finite capacity**。

与 `serial-investigation` 区分：后者专门审 evidence gathering，还考虑 first-evidence anchoring；本规则是通用 runtime/tool work scheduling。与 `blocking-event-loop` 区分：那里一个 task 霸占 shared progress engine；这里即便 executor 仍可工作，也把 independent operations 人工排队。与 `race-first-wins-semantics` 区分：并行后不能让谁先完成就决定 truth。

判定时给 operations 画 graph：edge 只能来自 data、ownership、protocol order。没有 path 的节点就是 concurrent candidate。然后另画一条 capacity ceiling，告诉系统同时允许多少 active work。

如果你无法解释一条 serial edge 除了“代码就是这样写的”，那条 edge 多半不是 requirement，而是 latency debt。

> Independence 应该在 schedule 中可见；capacity 也应该在 schedule 中可见。正确并发既不制造虚假等待，也不假装资源无限。