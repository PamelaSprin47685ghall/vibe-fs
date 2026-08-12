# property-test-missing — Enforcer

Property test 缺失，不是说“还没用 QuickCheck/FastCheck”，而是 implementation 声称自己满足一个**覆盖大输入空间的普遍法则**，suite 却只拿几个 curated example 当全部 evidence。

Trigger 来自 quantifier，不来自代码看起来多数学。

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

如果 correctness 天然可以写成 “for all”，少数手挑 fixture 通常只证明了几个熟悉角落。

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

不要因为“property testing 很强”就到处触发。One-off orchestration、固定 product copy、single migration fixture、四个 case 的 closed enum（已经 exhaustive table test）都不一定需要 generator。

Random input 没有 law，不叫 property testing。`forAll(randomBytes, x => doesNotThrow(x))` 只有在“永不 crash”本身就是 contract 时才有意义，否则只是数量很大的噪声。

Generator 质量与 assertion 同样重要。若 generator 把 difficult state filter 掉，从不产生 empty/maximal/duplicate/recursive/strange Unicode/conflict/permutation 等边界，危险区域仍然没被探索。

与 `coverage-theater` 区分：property suite assertion 若空洞，一样 theater。与 `failure-path-untested` 区分：后者可能只缺一个具体 negative path；本规则缺的是 universal relation。`missing-regression-test` 保留已发现 concrete counterexample；property test 保护它周围更大的 law。

决定性问题：

> Correctness 能不能诚实写成 “对所有 valid X...” 或 generated values 之间的 algebraic relation？

如果能，example 就只是 illustration，不应独自承担整个 quantifier。

好的 property 还必须会 shrink。失败时应尽量收敛到人能理解、能保留的 minimal counterexample。

> 代码承诺的是 law，就测试 law。三个记得住的例子，不等于一个输入宇宙的 evidence。