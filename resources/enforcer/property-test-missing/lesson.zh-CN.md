# property-test-missing — Lesson

## 评价

### 定义

Property test 缺失，不是说“还没用 QuickCheck/FastCheck”，而是 implementation 声称自己满足一个**覆盖大输入空间的普遍法则**，suite 却只拿几个 curated example 当全部 evidence。

Trigger 来自 quantifier，不来自代码看起来多数学。

### 支配原则

如果 correctness 天然可以写成 "for all"，少数手挑 fixture 通常只证明了几个熟悉角落。

### 何时触发

以下 law 很适合触发：

- serialization/codec round trip；
- normalization/canonicalization idempotency；
- parser/printer correspondence；
- algebraic merge/fold；
- ordering/permutation invariance；
- state-machine invariant preservation；
- generated valid structure 的 encode/decode identity；
- broad numeric/state space 上的 monotonicity/boundedness；
- 两种 representation 的 deterministic equivalence。

### 不应触发

不要因为“property testing 很强”就到处触发。One-off orchestration、固定 product copy、single migration fixture、四个 case 的 closed enum（已经 exhaustive table test）都不一定需要 generator。

Random input 没有 law，不叫 property testing。`forAll(randomBytes, x => doesNotThrow(x))` 只有在“永不 crash”本身就是 contract 时才有意义，否则只是数量很大的噪声。

Generator 质量与 assertion 同样重要。若 generator 把 difficult state filter 掉，从不产生 empty/maximal/duplicate/recursive/strange Unicode/conflict/permutation 等边界，危险区域仍然没被探索。

### 与相邻规则区分

与 `coverage-theater` 区分：property suite assertion 若空洞，一样 theater。与 `failure-path-untested` 区分：后者可能只缺一个具体 negative path；本规则缺的是 universal relation。`missing-regression-test` 保留已发现 concrete counterexample；property test 保护它周围更大的 law。

### 判定程序

决定性问题：

> Correctness 能不能诚实写成 "对所有 valid X..." 或 generated values 之间的 algebraic relation？

如果能，example 就只是 illustration，不应独自承担整个 quantifier。

好的 property 还必须会 shrink。失败时应尽量收敛到人能理解、能保留的 minimal counterexample。

### 例子

Example 证明点：

```text
f(a) = b
f(c) = d
```

Property 证明空间：

```text
对所有 valid x，decode(encode(x)) = x
对所有 x，normalize(normalize(x)) = normalize(x)
对所有允许 a,b，merge(a,b) 保持 invariant I
对所有 reachable transition，P 始终成立
```

## 建议

### 现在该做什么

先写 law，再写 generator。

一条有价值的 property test 应先有与 implementation 无关的 semantic statement：

```text
对所有 valid x，P(x)
对所有 valid x,y，R(f(x,y), x, y) 成立
对所有 permutation p，result(p(inputs)) = canonicalResult
```

然后才选择真正探索 domain 的 generator，而不是只生成容易通过的值。

### 为什么重要

Generator 要主动抵达 implementation 最容易犯错的边界：empty/singleton、duplicate、max/min、recursive depth、unusual Unicode、equivalent representation、stale/current version、conflict、permutation、以及需要时的 malformed-but-parseable edge。

不要重度 filter。如果 99% generated value 因“invalid”被丢掉，generator 多半根本不理解 domain。优先直接 construct valid case，再为 rejection law 单独生成 intentional invalid input。

Shrinking 也是 evidence 的一部分。一个“347 层随机嵌套 object”失败，不如一个 shrink 后的 minimal counterexample 能直接指出哪组条件破坏 invariant。如果 default shrink 会破坏 domain condition，就写 custom shrinker。

### 修复策略

Property 找到真 defect 后，minimized counterexample 若有解释价值就保留为 regression example，同时保留 property，继续覆盖邻近未知 case。

### 决策分支

Example 仍然要保留。它们负责 readable intent、named edge case、historical bug；property test 不是替代 example，而是把 evidence 从几个点扩成 quantified space。

### 常见假修复

常见假修复：

- random value 很多，但 assertion 只有 `doesNotThrow`；
- 使用 seed，却不保存 failing seed/counterexample；
- filter 到最后只剩 happy path；
- generator 调用同一份 production normalization/constructor，而 property 正是在质疑它；
- 把所有 readable example 都删掉，只留 opaque generative suite；
- 对四个有限 enum case 用 property testing，而不是直接穷举；
- 跑几万 case，但 property 本身几乎不区分正确与错误。

### 验证

验证时故意破坏 law：某 field round-trip 丢失、normalize 不再 idempotent、merge 变 order-sensitive、transition invariant 被删。Property 应找到小而清楚的 counterexample，而不是只证明“随机流量跑了很多”。

### 完成条件

完成时 general claim 有 general evidence，generator 真能到达 difficult domain，failure 能 shrink 成 explanation 而不是噪声。

> Property testing 不是 randomness，而是带搜索策略的 executable mathematics。

### Nudge

> 代码承诺的是 law，就测试 law。三个记得住的例子，不等于一个输入宇宙的 evidence。
