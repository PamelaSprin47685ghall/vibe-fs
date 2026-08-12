# wrong-rule-composition — Enforcer

Wrong rule composition 的病，不是“用了错误函数名”，而是**把同一种 evaluation law 套给逻辑关系不同的 rules**。

Dependent rules 与 independent rules 根本不是同一种东西。

如果 B 的意义建立在 A 成功产生的 fact 上，那么 A fail 后继续跑 B，只会制造 nonsense：email 都不存在，却继续报 “email domain 不允许”。这里应该 short-circuit，因为 premise 已经不存在。

如果 A、B、C 都是在同一完整 input 上独立判断，那么第一个 failure 后停止，只是在隐藏其余同样真实的 violation。这里 accumulation 往往更诚实。

以下情形触发：

- prerequisite fail 后 downstream validation 继续产生 cascading error；
- form/config 的独立字段 rule 永远只返回第一条 error；
- 一个 generic pipeline 全项目统一 “fail fast”，不管规则是否 independent；
- 反过来，所有 rule 都 “collect all”，连 parse/lookup fail 后没有意义的 checks 也继续跑；
- error filtering 被丢给 UI，因为 backend 先制造了一堆 premise 已失效的错误；
- rules 被 parallelize，只因为“都是 validation”，却忽略某条 rule 依赖上一条建立的 typed fact。

不要把 fail-fast 或 collect-all 当工程价值观。它们都只是 composition law，正确与否由 dependency graph 决定。

与 `missing-rule-combinator` 区分：那里还没有共享 composition vocabulary；本规则即使已经有漂亮 `andThen/all`，只要选错 operator，semantics 仍然错。与 `rule-spaghetti` 区分：那里 policy 还藏在 imperative maze；这里 rule 已清楚，问题在它们之间的逻辑关系被解释错。

判定时对每一对 rule 问：**B 需要 A 建立的新事实才能说出有意义的话吗？** 需要，就 sequence/short-circuit；不需要，就考虑独立 evaluate/accumulate。不要让 syntax similarity 替 logic 作答。

> Error behavior 本身就是 policy。失败后哪些问题仍然有意义，应该由 premise 决定，而不是由项目统一的“验证风格”决定。