# scope-creep — Lesson

## 评价

### 定义

Scope creep 是一个 change 开始承担与 stated outcome、governing invariant、或其必要后果没有因果链的额外 intent。问题不是“diff 大”，而是**一份 proof 被塞进多个互不需要彼此的命题**。

一个修改本应能回答：这些 edits 为什么共同证明这个结果？Drive-by cleanup、dependency bump、邻近 redesign 会让 necessity 与偏好混在一起，review 与 rollback 都失去边界。

### 支配原则

一个 change 是一个有边界的 proof：它声明有限的一组 edits 确立有限的结果。无关的 cleanup 同时扩大命题与搜索空间，使 review 更难区分必要后果与偏好。因此 restraint 不是保守主义；它保存从意图到修改的因果链。

### 何时触发

- 修一个 parser bug 顺手重构无关 module；
- “既然在这里”升级 dependency、统一格式、改邻近 API；
- acceptance criteria 没要求，却新增功能/迁移；
- 每个额外 edit individually 都“是好事”，但没有共同 causal story。

### 不应触发

- compile/call-site updates 是 API change 的必要传递；
- intended change 打破 invariant，恢复 invariant 属同一交付；
- generated artifacts 随 source-of-truth 必然变化；
- 任务本来就明确要求 broader redesign。

### 与相邻规则区分

`wholesale-rewrite` 为同一意图选择了不必要地宽的替换策略；`half-finished-refactor` 中途停下必需的迁移；`big-batch-intent` 在执行前把独立成功条件熔进一条指令。本规则管的是工作本身的意图已经膨胀到超出正当理由。Tie-break：如果额外的无关 intent 被附加到这个 change，这个案子归本规则。

### 判定程序

对每个 material edit 问：哪条 acceptance criterion，或哪条被本次改动必然扰动的 invariant，需要它？没有直接链，就应该另立 change。

### 例子

- positive：修一个 parser 的 bug，同时顺手 reformat 无关 module 并“既然在这里”升级 dependency。
- near-miss：rename 一个 function 需要同一 change 里更新每个 call site，让 tree 仍能编译。
- counterexample：diff 里每个 material file 都映射到 stated acceptance criteria 或必要的 invariant restore。

## 建议

### 现在该做什么

把每个 material edit 映射回 stated outcome 或 necessary invariant restore；没有链的 work 移出当前 delivery，给它独立 intent 与 acceptance criteria。

### 为什么重要

Broad diff 会让因果变贵：behavior、cleanup、architecture、dependency 同时变化后，任何 regression 都拥有更大的 suspect set，reviewer 也难判断哪些 edit 是证明、哪些只是偏好。

### 修复策略

把每个改动的区域映射到一条 requirement。保留必要的 transitive edits，但把独立改进推迟为带各自 acceptance criteria 的独立工作。命名每个保留的相邻文件的必要性，让边界保持可检查。

### 决策分支

- 若 edit 没有通向 acceptance criteria 或被扰动 invariant 的链，把它移出本 delivery。
- 若 edit 是 intended change 之后编译或 invariant restore 所必需，保留它并命名那个必要性。
- 若工作已变成对相邻 subsystem 的 rewrite，拆出一个新 change，而不是扩大本 change。

### 常见假修复

- 因为额外改动“都是好改动”就继续捆绑。
- 用一段长说明事后给 unrelated cleanup 找理由。
- 为减少 PR 数把 dependency bump/formatting 一起塞进来。
- 反过来把真正编译/contract 必需的 transitive edits 错删，留下半成品。

### 验证

最终 diff 中每个 material area 都能用一句短链解释：requirement → necessary edit，或 intended change → disturbed invariant → required restore。

### 完成条件

交付完整但不膨胀；所有 edits 属于同一个 causal story，邻近机会另有自己的 change。

### Nudge

Scope restraint 不是保守，而是保存因果。一个 diff 越能说明“每一刀为何必须在这里”，越容易验证、review 与回滚。
