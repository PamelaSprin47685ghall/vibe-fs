# canary-skipped — Lesson

## 评价

### 定义

Canary skipped 的核心不是“还没跑 staging”，而是 correctness 依赖一个**不由本仓库拥有、也无法从本仓库内部推导出来的 empirical premise**，验证却停在 mock、comment 或旧记忆。

Mock 只能证明“我们让替身怎么表现”；它不能证明真实 Host/provider/runtime 今天仍以同样 framing、ordering、identity、lifecycle、timing 工作。只要某条关键 assumption 属于外部系统，最后一份证据就必须跨过真实 boundary。

### 支配原则

未文档化的外部行为是一个 empirical premise。任何数量的内部证明都推不出它，因为该命题不归被测代码所有。当 correctness 依赖另一个系统提供的 ordering、framing、identity、timing 或 lifecycle 时，最后的证明义务必须跨过那个系统的真实 boundary。

### 何时触发

以下情形触发：

- Host hook order/shape 没正式 contract，却只按 mock 测；
- provider 的 streaming/tool/error 行为来自历史经验，没有 live falsification；
- deployment/runtime 的 filesystem/process/network 特性影响 correctness，但 local double 抹掉了差异；
- 一次外部 version upgrade 后仍引用旧 canary 结果；
- broad E2E 跑了，但根本没断言那条具体 empirical premise。

### 不应触发

不要把 canary 变成宗教。如果外部 behavior 已有稳定 versioned contract，并且 real parser/consumer 的 contract test 足以在 incompatibility 时失败，就不需要为同一事实再付 live cost。Change 根本不到那个 boundary，也不需要 canary。

### 与相邻规则区分

与 `contract-test-missing` 区分：contract test 证明双方**声明的 agreement**；canary 证明未完全被声明、只能问真实系统的 fact。与 `release-ladder-skipped` 区分：后者是整条证据梯子乱序，这条只盯 irreducible real-world premise。

### 判定程序

判定方法：把 assumption 写成一句可以被真实系统打脸的话，例如“Host 在 tool result append 后才触发 transform X”。如果 mock 只能复述这句话，而不能独立证明它，真实 canary 才是 authority。

### 例子

- positive：基于 mock Host 回复与“上季度手工检查过”的 comment 发布一个 framing change。
- near-miss：Host 行为是 versioned contract，已有会在不兼容时失败的 contract test 证明。
- counterexample：一条针对 Host 的窄真实 canary，empirical assumption 为假时它失败。

## 建议

### 现在该做什么

把不可由内部证明的 external premise 写成一条最小 falsifiable canary。

先明确谁拥有事实：Host、provider、deployment runtime、OS、external service。然后设计最小真实 interaction，只观察那条 assumption 本身，避免用巨大 E2E 把十种风险混在一起。

### 为什么重要

Mock 证明的是我们让它做的事。Canary 证明的是另一个系统真正做的事。如果行为未文档化，这两句话之间的差距就是全部风险。内部测试可以全绿，却仍建立在关于 framing、ordering、identity、lifecycle 的错误 premise 上。

### 修复策略

好的 canary 应回答：

- 发了什么真实 stimulus；
- 观察哪个 exact behavior；
- 什么结果会明确证明 assumption false；
- 依赖哪个 external version/environment；
- 是否安全、可重复、成本可控。

### 决策分支

- 如果 premise 是未文档化的 Host 行为，release 前加一条窄真实 canary。
- 如果稳定 declared contract 已有等价的可失败测试，不要为同一 declared fact 重复 live canary。
- 如果 change 永远到不了 Host boundary，跳过 canary；empirical premise 不在场。

### 常见假修复

常见假修复：

- 把 mock 写得更像自己希望的真实系统；
- 引用“上个月手工看过一次”；
- broad staging suite green，却没验证关键 ordering/framing/identity；
- live canary 只断言“request 成功”，对 assumption 没辨别力；
- canary 太大、依赖太多 unrelated setup，失败后无法知道哪条 premise 被打破；
- external upgrade 后沿用旧 observation，不重新跑 relevant premise。

### 验证

Canary 不替代 local proof。Pure logic、codec、contract、replay 应先在更窄层证明；live check 只承担**下面各层无法拥有的那一点 empirical uncertainty**。

验证 canary 自己也要 mutation thinking：如果真实 Host 把关键 behavior 改成相反值，这条 canary 会不会可靠变红？若只是仍然得到 200/“completed”，它不够具体。

如果 external side 终于发布稳定 contract，且本地 contract test 能 faithful exercise，可以降低 live canary 频率或移除；证据策略应跟风险所有权走，不是永久 ritual。

### 完成条件

完成时 release 不再依赖 “Host 应该这样吧”，而有一条真实 observation 能让这个 premise 随时被否证。

> Canary 的价值不在“接触了真实环境”，而在真实环境终于有机会对我们的假设说不。

### Nudge

> 外部世界拥有的事实，最终必须向外部世界取证。Mock 是模型，不是证人。
