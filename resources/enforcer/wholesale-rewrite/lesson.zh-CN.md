# wholesale-rewrite — Lesson

## 评价

### 定义

Wholesale rewrite 不是“大改动即罪”。真正的问题是：required semantic delta 明明只推翻少量 invariant，却选择删除/重建一大片已有、已验证结构，让所有旧知识一起重新进入待证明状态。

现有代码承载的不只设计美丑，还沉积 bug fixes、edge cases、operational constraints 与 compatibility facts。Rewrite 会一次性丢掉这些隐性证据，再要求新 tests 重新发现。

### 支配原则

现有代码包含比可见设计更多的信息：bug fixes、operational constraints、edge cases 与经历史积累的 compatibility facts。为更小的语义 delta 丢弃已被证明的结构，就是 wholesale 地扔掉这些信息，并要求 tests 一次重新发现每个相关约束。Precision 因此是认知策略：保留已被证明的东西，只使新要求真正变 false 的最小假设失效。

### 何时触发

- 一字段/一行为改动触发整个 service 重写“顺便变干净”；
- generated/new package 复制所有行为，再慢慢补 parity；
- 接受条件很窄，blast radius 却覆盖大量 unaffected owners；
- preserved tests 被一起重写，旧行为不再有独立 witness；
- rewrite 理由主要是“旧代码看起来不好”，不是旧结构本身违反目标 invariant。

### 不应触发

- task 明确授权 greenfield replacement；
- ADR 已证明旧 structure 本身就是 defect，incremental preservation 会继续保留错误 invariant；
- required change 推翻整个 module core invariants，此时 local module rewrite 可能正是最小正确变换；
- regenerated artifact 来自 canonical source，不是丢弃手工知识。

### 与相邻规则区分

`scope-creep` 扩大了一个 change 的意图；`half-finished-refactor` 未能完成必需迁移。Tie-break：如果策略是更小语义 delta 上不必要的 blast radius，用本规则；如果同一 change 被附加了无关 intent，用 `scope-creep`。

### 判定程序

列出新要求真正使哪些旧 assumptions 变 false。为什么其它已证明 assumptions 也必须一起被删除？若答不出，rewrite surface 超过 semantic delta。

### 例子

- positive：一字段 validation 改动“顺便”重写整个 service。
- near-miss：ADR 授权替换一个被证明不安全的 persistence model，且该 module 被重写。
- counterexample：ticket 膨胀到包含无关 cleanup 与 features——那是 `scope-creep`。

## 建议

### 现在该做什么

把 required semantic delta 映射到被它真正 invalidated 的 owners/invariants，只重做这些结构；其它 known-good paths 与 behavioral witnesses 尽量保留。若 structure 本身确实是 defect，明确记录这个事实，再把 rewrite 边界限定在那里。

### 为什么重要

Rewrite 会让 verification burden 从“证明这个新行为”膨胀成“重新证明整个 subsystem”。最危险的是 tests 也跟着重写，于是新实现与新 examiner 可以一起忘掉旧的 edge-case knowledge。

### 修复策略

映射所需语义 delta，保留未受影响的 owners 与 paths，只迁移 invariants 真正改变的结构，并在变换期间用 tests 保护被保留的行为。

### 决策分支

- 若 requirement 只使少量 invariants 失效，保留其余，变换那一组。
- 若记录在案的决定说 structure 本身是 defect，重写那个 structure，仍保留无关的 proven code。

### 常见假修复

- 用“新代码更少”证明 rewrite 更简单。
- 新旧 package 并存，形成 `half-finished-refactor`。
- 从零重写 tests，让 preserved behavior 没有 independent memory。
- 因为反对 rewrite 而死守一个已被证明确实错误的 core structure；precision 不是保守主义。

### 验证

Diff 应能解释为“只变更被新 contract 推翻的 assumptions”。Unchanged behavior 继续由既有 tests/evidence 保护，不需要在新世界里从零重新猜。

### 完成条件

改动的 blast radius 与 semantic invalidation 匹配；既有可靠知识尽量保留，只有真正被要求否定的结构才被重建。

### Nudge

Known-good structure 是证据，不是包袱。只有当结构本身就是问题时才重写；否则应尽量保留已经赢得的证明。
