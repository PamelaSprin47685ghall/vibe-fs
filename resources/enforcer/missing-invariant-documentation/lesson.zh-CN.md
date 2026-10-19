# missing-invariant-documentation — Lesson

## 评价

### 定义

Invariant 未记录，不是“文档不够多”，而是 correctness 依赖一条非显然、可证伪的关系，却没有在 semantic owner 处留下 durable statement。未来修改者只能从 defensive code、事故或口头传统重新发现它。

### 支配原则

Invariant 是超越单个 code path 存活的知识：许多实现可以保持它，许多未来改动可以违反它。如果规则没有在 owner 处被命名，维护者必须从症状重新发现它，或从 defensive code 反向工程。隐藏 invariant 因此是延迟的 defect——correctness 依赖系统尚未使其可传递的知识。

### 何时触发

- ordering、uniqueness、ownership、durability、state relation 很关键却无 canonical statement；
- rule 散在几个 comments，没人知道哪句 authoritative；
- 类型无法表达的约束也没有 behavioral/property test；
- 新人必须问“这里为什么绝对不能这样改”才能知道规则。

### 不应触发

- 强类型已让 invariant 一眼可见且 illegal state 无法构造，不必重复写长文；
- 缺的是为什么选择某方案，属于 `unrecorded-decision`；
- purely local implementation detail 无跨路径义务；
- tutorial/example 缺失不是 invariant debt。

### 与相邻规则区分

`missing-architecture-gate` 缺少对已知结构规则的 enforcement；`unrecorded-decision` 缺少对某个选择的 rationale。Tie-break：如果 correctness property 本身没有 durable statement，用本规则；如果规则已知但未 enforcement，用 `missing-architecture-gate`；如果只是缺“为什么这样选”，用 `unrecorded-decision`。

### 判定程序

把规则写成一句能被证明为 false 的话。若连这句话都没有 canonical owner，correctness 只存在于人的记忆里。

### 例子

- positive：active leases 的唯一性很关键，却只存在于某个 worker 的 comment 里，不在 lease contract 上。
- near-miss：closed sum type 已让 forbidden combination 无法表示，额外 prose 只是复述类型。
- counterexample：owning module 文档化了 ordering rule，且 property test 在它被破坏时失败。

## 建议

### 现在该做什么

在 semantic owner 处记录一条精确、可证伪的 invariant；能由 type/construction/test/gate 表达的部分继续机械化，但不要复制成多份 prose。

### 为什么重要

隐藏 invariant 会让每代维护者重新交学费。代码可能暂时一直遵守它，却没人知道哪些 shape 是必要、哪些只是偶然；一次看似合理的 cleanup 就能删掉真正的保护机制。

### 修复策略

陈述什么必须恒真、哪个 boundary 拥有这条规则、什么 evidence 会证伪它。保持 prose 简短，然后把可执行部分移入 construction、exhaustive matching、architecture checks 或 behavioral tests。

### 决策分支

- 若 property 能被 type 或 construction rule 变得不可表示，在那里编码它，并保留一句 owner statement。
- 若它无法被 typing 表达，在 owner 处记录可证伪句子，并添加能检测违反的最廉价 test 或 gate。

### 常见假修复

- 到处 comment 同一句 rule。
- 写“注意顺序”“保持一致”这种无法 falsify 的提醒。
- 新建大文档却不放在概念 owner 附近。
- 类型已经完整表达后继续复制长 prose，制造第二 truth source。

### 验证

一个没参与原讨论的人，从 owning concept 能找到 rule，能说出什么 observation 会证明它被破坏，也能找到已有 mechanical protection。

### 完成条件

关键 correctness 不依赖口头传统；rule 一处定义、owner 清楚、可机械的部分由机制承担。

### Nudge

Documentation 的价值不是 prose 数量，而是让重要 correctness knowledge 能离开原作者继续存活。
