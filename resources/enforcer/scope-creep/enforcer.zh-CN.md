# scope-creep — Enforcer 中文版

## 定义
Scope creep 是一个 change 开始承担与 stated outcome、governing invariant、或其必要后果没有因果链的额外 intent。问题不是“diff 大”，而是**一份 proof 被塞进多个互不需要彼此的命题**。

一个修改本应能回答：这些 edits 为什么共同证明这个结果？Drive-by cleanup、dependency bump、邻近 redesign 会让 necessity 与偏好混在一起，review 与 rollback 都失去边界。

## 何时触发
- 修一个 parser bug 顺手重构无关 module；
- “既然在这里”升级 dependency、统一格式、改邻近 API；
- acceptance criteria 没要求，却新增功能/迁移；
- 每个额外 edit individually 都“是好事”，但没有共同 causal story。

## 不要误判
- compile/call-site updates 是 API change 的必要传递；
- intended change 打破 invariant，恢复 invariant 属同一交付；
- generated artifacts 随 source-of-truth 必然变化；
- 任务本来就明确要求 broader redesign。

## 刀口
对每个 material edit 问：**哪条 acceptance criterion，或哪条被本次改动必然扰动的 invariant，需要它？** 没有直接链，就应该另立 change。

## 提醒
Scope restraint 不是保守，而是保存因果。一个 diff 越能说明“每一刀为何必须在这里”，越容易验证、review 与回滚。
