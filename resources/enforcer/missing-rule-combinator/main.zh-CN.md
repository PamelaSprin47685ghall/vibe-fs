# missing-rule-combinator — Main

先写 shared rule signature，再只抽真正稳定的 composition law。

如果 rules 共享 `Input -> Result<Output, Error>`，明确区分你实际需要哪几种组合：

- prerequisite chain：成功后才把新 fact 交给下一 rule；
- independent validation：在同一 input 上积累多个 violation；
- mapping：改变 success value，不改变 failure law；
- conjunction/disjunction：只有 domain 真的这样定义时才提供。

不要一个 `compose()` 包打天下。Combinator 名字应该告诉读者 failure/ordering semantics，而不是逼读者进去看实现。

常见假修复：

- 引入通用 rules engine/DSL，只为省几段 `match`；
- 把 semantics 不同但 signature 相似的 rules 强塞进一个 operator；
- combinator 接大量 flags：`stopOnError`, `collect`, `parallel`, `skipMissing`，最后把 control-flow maze 搬进 abstraction；
- generic 到每个新 rule 都需要 escape hatch；
- caller 仍有一半手写 composition，新的 owner 并不唯一；
- combinator 自己偷偷决定 domain priority/error text。

验证重点是 law，而不是 line coverage。相同 rule set 从任何 caller 组合都应得到相同 semantics；dependent chain 的前提失败后，下游不能执行；independent accumulation 的结果应与合法 evaluation order 无关。

如果 rule 数量很少、变化很快，先保持普通函数调用。抽象应该在 repetition 已证明存在之后出现，而不是提前预测未来会有一个漂亮 algebra。

对新的 combinator 做 readability test 也很重要：domain reviewer 应能从 `a |> andThen b` 或 `all [a;b;c]` 直接理解 relation，而不需要学习一个 mini framework。

完成时 composition semantics 有一个小而稳定的词汇表；caller 只声明“这些 rules 如何相关”，不再重复实现 error plumbing。

> 好 combinator 抽的是已经重复出现的逻辑法则，不是把几段 if 藏进一个更抽象的名字。