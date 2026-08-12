# catch-all-swallows-future — Main

让 closed vocabulary 的新增 case 重新变成显式工作。

Closed union/enum/policy 用 exhaustive matching；每个 case 要么命名处理，要么被一个**明确、已审过的等价 group**覆盖。不要用 generic fallback 把未来 case 自动塞进去。

若 boundary 本来 open-world，就把 unknown semantics 正式命名：`Unknown raw -> preserve`, `Unknown extension -> reject`, `Unknown field -> ignore`。让人一眼看出这不是偷懒 default，而是 protocol law。

常见假修复：

- `_ -> fail` 就自称安全；未来 case 仍然未经 review 自动获得 failure semantics；
- 在 catch-all 里加 log/warning；
- test 当前 enum count，却新增 case 后 policy 仍不用改；
- 把所有未来 case 映成 `Other`，但 `Other` 没有真正 open-world contract；
- 关闭 compiler exhaustiveness warning；
- 用反射/string switch 绕过 closed type，未来 case 更难被发现。

验证方式是 ontology mutation：加一个假 case。Build 或 focused test 必须暴露所有需要 semantic decision 的地方；做完决定后才重新 green。

对 truly open protocol 再做 opposite test：喂一个从未见过的 extension，系统应严格执行 documented unknown law，而不是 crash 或误当已知 case。

完成时，新 ontology 无法悄悄借用旧 fallback。Closed world 会迫使 review；open world 则有一条明确、稳定的 unknown-case contract。

> Exhaustiveness 的价值不是多写 branch，而是让世界新增一种可能时，旧政策必须重新表态。