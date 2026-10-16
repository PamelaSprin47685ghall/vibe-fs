# premature-unification — Enforcer 中文版

## 定义

Premature unification 是把“长得像”误当成“属于同一知识”。两个 concept 当前字段/流程一致，并不证明它们必须一起演化；抽成一个 abstraction 等于新增一条强 contract：未来一边改变，另一边也应跟着改变。

DRY 应消除 knowledge duplication，不是 visual resemblance。

## 支配原则

DRY 适用于知识，不是外观。两个结构今天可能逐字相同，因为独立 domain 恰好共享一个 shape。统一它们制造了一个新声明：它们必须一起演化。如果这个声明为假，每一次未来的分叉都会以 flags、optional fields、hooks 或 special cases 的形式对抗 abstraction，直到“复用”比它消除的重复更贵。

## 何时触发

- 两个独立 domain types 因字段一样被合成一个 mega type；
- abstraction 很快出现 mode flags/optional hooks/context enum；
- 一边新增字段，另一边被迫携带 `None/not applicable`；
- “以后可能统一”成为继续共享 owner 的理由；
- change reasons 不同，却必须共同修改 generic layer。

## 不应触发

- 真正 shared invariant 已稳定出现；
- duplicated-control-flow 确实是同一 protocol，应统一；
- tiny primitive/value object 的语义在各处完全相同；
- 两个 context 可以暂时复制几行相似代码，不是设计失败。

## 与相邻规则区分

`duplicated-control-flow` 适用于重复代码真的是同一 protocol；`context-model-leak` 跨意义复用同一个 model。Tie-break：如果在 sameness of knowledge 确立前就统一了，用本规则；如果重复的 flow 真是一个 protocol，用 `duplicated-control-flow`；如果一个 model 被拉去覆盖多个 contexts，用 `context-model-leak`。

## 判定程序

问：A 因自己的 domain reason 改变时，B 是否必然应该一起改？若不是，当前相似不能授权共同 abstraction。

## 例子

- positive：`UserAddress` 与 `ShippingAddress` 被强合成一个 type，然后 billing-only fields 以 optionals 出现。
- near-miss：两个 call sites 共享一条必须保持一致的 validation law；抽取它是真 DRY。
- counterexample：每个 concept 有自己的 type；相似的 helpers 保持重复，直到 shared invariant 出现。

## Nudge

相似是观察，不是 ownership。宁可允许独立概念暂时长得一样，也不要为了 DRY 让它们从此被迫一起生活。