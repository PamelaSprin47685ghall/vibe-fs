# big-batch-intent — Lesson

## 评价

### 定义

Big-batch intent 的问题，不是“一个 PR 改了很多文件”，而是**多个本可独立判断、独立回滚、独立验证的 semantic change 被绑成一笔 all-or-nothing intention**。

### 支配原则

大 batch 最大的风险不是 diff review 累，而是因果归因消失。结果变好/变坏时，你很难知道是哪一项改变造成；rollback 只能整包撤；validation 失败也不知道是哪一个 obligation 未满足。Batch 越大，`guess-based-fix` 越容易伪装成“整体方案终于 green”。

### 何时触发

以下情形触发：

- 同一 change 同时重构、换 dependency、改 protocol、改 behavior、清 legacy，却没有真实 atomic reason；
- 多个 speculative fix 一起落，谁真正必要无法区分；
- 需要“一次性做完，否则不好测”只是因为 test/architecture 没有可组合 seam；
- review 只能按文件看，无法按 independent obligation 判断；
- release/rollback 必须接受一堆 unrelated risk 一起进退。

### 不应触发

不要误杀真正 atomic migration。Schema + all callers、provider clean-break、cryptographic format cutover、rename crossing generated surfaces，有时必须一笔完成才能保持 invariant。关键是所有 edits 是否由**同一个不可分割 obligation**约束，而不是“顺便一起做更省事”。

### 与相邻规则区分

与 `wholesale-rewrite` 区分：wholesale rewrite 是以重写整个现有系统为策略；big batch 可以由很多小 edit 组成，只是 semantic intents 被不必要绑在一起。与 `scope-creep` 区分：batch 内工作可能都在 scope，只是本来可以分开证明。

### 判定程序

判定问题：删掉其中一项 change，其他项是否仍有独立正确含义、能单独验证和交付？如果 yes，batch 很可能在购买不必要的耦合风险。

### 例子

- positive：一次工具调用同时搜索文档、改三个无关模块、跑一次 migration，却只报一个“done”。
- near-miss：跨文件 rename 必须一起落地，因为部分 rename 无法编译。
- counterexample：每个独立 outcome 都是自己的 task，有自己的验收与失败面。

## 建议

### 现在该做什么

按 semantic obligation 拆 batch，不按文件数量拆。

### 为什么重要

不要把拆分当 project-management 美学。真正目标是降低 simultaneous uncertainty，让 failure 能定位、review 能按 obligation 判断、rollback 不必牺牲无关正确工作。

### 修复策略

把 change 列成几条独立 claim：behavior A、migration B、refactor C、dependency D。对每条问：它是否有自己的 owner、proof、rollback boundary？若有，就尽量让它成为独立可验证单元；只有共享一个真正 atomic invariant 的 edits 才绑在一起。

常见拆法：

- 先加兼容新 path + tests，再迁 callers，再删旧 path；
- 先建立新 contract/boundary，再做内部 refactor；
- behavior fix 与 unrelated cleanup 分开；
- dependency upgrade 与 semantic feature 分开，除非 feature 必须依赖该 upgrade；
- speculative diagnosis changes 一次只保留 causal proven 的一项。

### 决策分支

- 如果各 outcome 能独立成功或失败，就在执行前拆成独立单元。
- 如果真实 invariant 要求 all-or-nothing 完成，就把这些步骤留在一起，并命名这个 atomic promise。
- 如果拆分后各单元相互独立，只在有明确 join 与有界 fan-out 时才并发运行。

### 常见假修复

常见假修复：

- 为“每个 commit 小”把一个 atomic semantic change 切成无法独立 green 的碎片；
- 反过来，几十个 unrelated edits 放一起，只因“反正都在这个模块”；
- 用 feature flag 把 batch 表面拆开，实际两套 path 长期共存；
- 把 proof 推迟到 batch 末尾，前面的每一项都没有独立 evidence；
- 大范围 mechanical formatting 混进 behavioral diff，掩盖真正 change。

### 验证

验证每个 unit：能否单独说明 user-visible/internal invariant、单独让相关 tests green、单独 rollback 而不破坏其他已交付 obligations。若不能，说明它可能确实属于同一 atomic batch；这不是失败。

### 完成条件

完成时一个 batch 可以很大，但里面的 edits 都被同一不可分割语义约束；或者 batch 很小，因为独立假设已经被拆到各自 proof boundary。

> 好 batch 不是“小”，而是每次只要求世界同时相信尽可能少的新东西。

### Nudge

> Batch size 真正昂贵的不是行数，而是同时变化的独立假设数量。
