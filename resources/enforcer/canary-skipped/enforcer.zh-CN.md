# canary-skipped — Enforcer

Canary skipped 的核心不是“还没跑 staging”，而是 correctness 依赖一个**不由本仓库拥有、也无法从本仓库内部推导出来的 empirical premise**，验证却停在 mock、comment 或旧记忆。

Mock 只能证明“我们让替身怎么表现”；它不能证明真实 Host/provider/runtime 今天仍以同样 framing、ordering、identity、lifecycle、timing 工作。只要某条关键 assumption 属于外部系统，最后一份证据就必须跨过真实 boundary。

以下情形触发：

- Host hook order/shape 没正式 contract，却只按 mock 测；
- provider 的 streaming/tool/error 行为来自历史经验，没有 live falsification；
- deployment/runtime 的 filesystem/process/network 特性影响 correctness，但 local double 抹掉了差异；
- 一次外部 version upgrade 后仍引用旧 canary 结果；
- broad E2E 跑了，但根本没断言那条具体 empirical premise。

不要把 canary 变成宗教。如果外部 behavior 已有稳定 versioned contract，并且 real parser/consumer 的 contract test 足以在 incompatibility 时失败，就不需要为同一事实再付 live cost。Change 根本不到那个 boundary，也不需要 canary。

与 `contract-test-missing` 区分：contract test 证明双方**声明的 agreement**；canary 证明未完全被声明、只能问真实系统的 fact。与 `release-ladder-skipped` 区分：后者是整条证据梯子乱序，这条只盯 irreducible real-world premise。

判定方法：把 assumption 写成一句可以被真实系统打脸的话，例如“Host 在 tool result append 后才触发 transform X”。如果 mock 只能复述这句话，而不能独立证明它，真实 canary 才是 authority。

> 外部世界拥有的事实，最终必须向外部世界取证。Mock 是模型，不是证人。