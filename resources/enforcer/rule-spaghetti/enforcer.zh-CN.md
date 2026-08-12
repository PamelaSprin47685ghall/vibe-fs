# rule-spaghetti — Enforcer

Rule spaghetti 的病，不是 `if` 太多，而是业务 policy 只存在于 nested branch、temporary flag、early return、mutation 的执行轨迹里；读者必须**在脑内运行程序**，才能恢复“规则到底是什么”。

Policy 本质上是 facts 与 conclusion 的关系。Control flow 只是它的一种解释器。当 interpreter 自己变成唯一 specification，改一个业务 clause 就等于改 execution topology；reviewer 也无法逐条对照 domain sentence，只能跟着 branch 猜。

以下情形触发：

- eligibility/permission/routing 要追四五层 `if/else` 才能说清；
- 三个 temporary booleans 在不同 branch 被修改，最后共同决定 verdict；
- 同一个 predicate 在不同 path 以稍不同方式重复；
- error/reason 来自最后走到哪条 return，而不是命名 rule；
- domain expert 看代码无法直接找到“哪一行表达这条业务 clause”；
- 新规则只能继续往 maze 里塞 another branch。

不要误杀真正 sequential prerequisite。先 parse、再根据 parse result 校验；先 load account、再检查其 license——这些依赖本来就有方向。只要每个 proposition 被命名，control flow 能一眼映射到 domain law，branch 本身完全没问题。

也不要把所有 rule 压成一个巨大 boolean expression。`a && (b || !c) && ...` 可能比 nested if 更难读。目标是**让 proposition 有名字，让 composition 显示逻辑关系**，不是追求 declarative syntax 外观。

与 `missing-rule-combinator` 区分：那里 rule 已经清楚，只是相同 composition law 到处手写；本规则更早——rule 本身还没从 imperative maze 中浮现。与 `wrong-rule-composition` 区分：那里 named rules 已有，但 independent/dependent 的组合方式错。

判定方法：先不用看代码，把业务规则写成几句完整 domain sentence。若无法把每句话直接映射到一个 named predicate/case/composition，而必须说“它大概散在这几个 branch 里”，policy 已经 spaghetti 化。

> 代码应该让人读到规则，而不是要求人模拟执行之后才猜出规则。