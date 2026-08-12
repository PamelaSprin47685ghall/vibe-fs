# missing-rule-combinator — Enforcer

Missing rule combinator 的问题，不是“还没有函数式抽象”，而是多个 named rules 已经拥有**相同 semantic shape**，callers 却在不同地方反复手写同一套 sequencing、short-circuit、accumulation、mapping law。

当几条 rule 都像：

```text
A -> Result<A, E>
```

那么“成功继续、失败停止”本身已经是一条可命名 composition law。若每个 caller 都重新写 `match/if err return`，重复的不只是 syntax，而是“这些 failure 应如何组合”的 policy。

以下情形触发：

- 三处以上手写同一 validation short-circuit；
- 一组 independent rules 到处自己 accumulate error；
- rule pipeline 每个 caller 都维护自己的 loop/result plumbing；
- 同一 rule set 在不同入口因手写 composition 漂出不同 failure behavior；
- composition mechanics 比每条具体 rule 还长。

不要看到两个相似函数就抽 combinator。只有一两处 isolated rule、signature 看似相同但 failure semantics 不同、或者 domain shape 仍在变化时，抽象可能只是 `premature-unification`。

也不要为了“有 algebra”造 rules engine。需要的通常只是少量 named operators：`andThen`、`all`、`map`、`collectIndependent`。Combinator 应比手写 control flow 更少知道 domain，而不是变成新的 god framework。

与 `rule-spaghetti` 区分：那里 policy 自己还没被清楚命名；本规则假设 rules 已经干净，只缺稳定 composition vocabulary。与 `wrong-rule-composition` 区分：combinator 存在但选错 logical law，用后者。

判定问题：**如果修改“rule failure 如何组合”的语义，需要同步改多少 caller？** 若多处都要改，composition 已经成为独立知识，却还没有 owner。

> 当 rules 已经共享形状，组合方式本身就成了知识。把它命名一次，不要让每个 caller 重写一遍小型解释器。