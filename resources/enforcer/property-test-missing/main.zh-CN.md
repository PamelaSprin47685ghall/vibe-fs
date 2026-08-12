# property-test-missing — Main

先写 law，再写 generator。

一条有价值的 property test 应先有与 implementation 无关的 semantic statement：

```text
对所有 valid x，P(x)
对所有 valid x,y，R(f(x,y), x, y) 成立
对所有 permutation p，result(p(inputs)) = canonicalResult
```

然后才选择真正探索 domain 的 generator，而不是只生成容易通过的值。

Generator 要主动抵达 implementation 最容易犯错的边界：empty/singleton、duplicate、max/min、recursive depth、unusual Unicode、equivalent representation、stale/current version、conflict、permutation、以及需要时的 malformed-but-parseable edge。

不要重度 filter。如果 99% generated value 因“invalid”被丢掉，generator 多半根本不理解 domain。优先直接 construct valid case，再为 rejection law 单独生成 intentional invalid input。

Shrinking 也是 evidence 的一部分。一个“347 层随机嵌套 object”失败，不如一个 shrink 后的 minimal counterexample 能直接指出哪组条件破坏 invariant。如果 default shrink 会破坏 domain condition，就写 custom shrinker。

常见假修复：

- random value 很多，但 assertion 只有 `doesNotThrow`；
- 使用 seed，却不保存 failing seed/counterexample；
- filter 到最后只剩 happy path；
- generator 调用同一份 production normalization/constructor，而 property 正是在质疑它；
- 把所有 readable example 都删掉，只留 opaque generative suite；
- 对四个有限 enum case 用 property testing，而不是直接穷举；
- 跑几万 case，但 property 本身几乎不区分正确与错误。

Property 找到真 defect 后，minimized counterexample 若有解释价值就保留为 regression example，同时保留 property，继续覆盖邻近未知 case。

验证时故意破坏 law：某 field round-trip 丢失、normalize 不再 idempotent、merge 变 order-sensitive、transition invariant 被删。Property 应找到小而清楚的 counterexample，而不是只证明“随机流量跑了很多”。

Example 仍然要保留。它们负责 readable intent、named edge case、historical bug；property test 不是替代 example，而是把 evidence 从几个点扩成 quantified space。

完成时 general claim 有 general evidence，generator 真能到达 difficult domain，failure 能 shrink 成 explanation 而不是噪声。

> Property testing 不是 randomness，而是带搜索策略的 executable mathematics。