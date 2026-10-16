# missing-invariant-documentation — Enforcer 中文版

## 定义

Invariant 未记录，不是“文档不够多”，而是 correctness 依赖一条非显然、可证伪的关系，却没有在 semantic owner 处留下 durable statement。未来修改者只能从 defensive code、事故或口头传统重新发现它。

## 支配原则

Invariant 是超越单个 code path 存活的知识：许多实现可以保持它，许多未来改动可以违反它。如果规则没有在 owner 处被命名，维护者必须从症状重新发现它，或从 defensive code 反向工程。隐藏 invariant 因此是延迟的 defect——correctness 依赖系统尚未使其可传递的知识。

## 何时触发

- ordering、uniqueness、ownership、durability、state relation 很关键却无 canonical statement；
- rule 散在几个 comments，没人知道哪句 authoritative；
- 类型无法表达的约束也没有 behavioral/property test；
- 新人必须问“这里为什么绝对不能这样改”才能知道规则。

## 不应触发

- 强类型已让 invariant 一眼可见且 illegal state 无法构造，不必重复写长文；
- 缺的是为什么选择某方案，属于 `unrecorded-decision`；
- purely local implementation detail 无跨路径义务；
- tutorial/example 缺失不是 invariant debt。

## 与相邻规则区分

`missing-architecture-gate` 缺少对已知结构规则的 enforcement；`unrecorded-decision` 缺少对某个选择的 rationale。Tie-break：如果 correctness property 本身没有 durable statement，用本规则；如果规则已知但未 enforcement，用 `missing-architecture-gate`；如果只是缺“为什么这样选”，用 `unrecorded-decision`。

## 判定程序

把规则写成一句能被证明为 false 的话。若连这句话都没有 canonical owner，correctness 只存在于人的记忆里。

## 例子

- positive：active leases 的唯一性很关键，却只存在于某个 worker 的 comment 里，不在 lease contract 上。
- near-miss：closed sum type 已让 forbidden combination 无法表示，额外 prose 只是复述类型。
- counterexample：owning module 文档化了 ordering rule，且 property test 在它被破坏时失败。

## Nudge

Documentation 的价值不是 prose 数量，而是让重要 correctness knowledge 能离开原作者继续存活。