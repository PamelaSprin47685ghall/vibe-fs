# wholesale-rewrite — Enforcer 中文版

## 定义

Wholesale rewrite 不是“大改动即罪”。真正的问题是：required semantic delta 明明只推翻少量 invariant，却选择删除/重建一大片已有、已验证结构，让所有旧知识一起重新进入待证明状态。

现有代码承载的不只设计美丑，还沉积 bug fixes、edge cases、operational constraints 与 compatibility facts。Rewrite 会一次性丢掉这些隐性证据，再要求新 tests 重新发现。

## 支配原则

现有代码包含比可见设计更多的信息：bug fixes、operational constraints、edge cases 与经历史积累的 compatibility facts。为更小的语义 delta 丢弃已被证明的结构，就是 wholesale 地扔掉这些信息，并要求 tests 一次重新发现每个相关约束。Precision 因此是认知策略：保留已被证明的东西，只使新要求真正变 false 的最小假设失效。

## 何时触发

- 一字段/一行为改动触发整个 service 重写“顺便变干净”；
- generated/new package 复制所有行为，再慢慢补 parity；
- 接受条件很窄，blast radius 却覆盖大量 unaffected owners；
- preserved tests 被一起重写，旧行为不再有独立 witness；
- rewrite 理由主要是“旧代码看起来不好”，不是旧结构本身违反目标 invariant。

## 不应触发

- task 明确授权 greenfield replacement；
- ADR 已证明旧 structure 本身就是 defect，incremental preservation 会继续保留错误 invariant；
- required change 推翻整个 module core invariants，此时 local module rewrite 可能正是最小正确变换；
- regenerated artifact 来自 canonical source，不是丢弃手工知识。

## 与相邻规则区分

`scope-creep` 扩大了一个 change 的意图；`half-finished-refactor` 未能完成必需迁移。Tie-break：如果策略是更小语义 delta 上不必要的 blast radius，用本规则；如果同一 change 被附加了无关 intent，用 `scope-creep`。

## 判定程序

列出新要求真正使哪些旧 assumptions 变 false。为什么其它已证明 assumptions 也必须一起被删除？若答不出，rewrite surface 超过 semantic delta。

## 例子

- positive：一字段 validation 改动“顺便”重写整个 service。
- near-miss：ADR 授权替换一个被证明不安全的 persistence model，且该 module 被重写。
- counterexample：ticket 膨胀到包含无关 cleanup 与 features——那是 `scope-creep`。

## Nudge

Known-good structure 是证据，不是包袱。只有当结构本身就是问题时才重写；否则应尽量保留已经赢得的证明。