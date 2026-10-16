# scope-creep — Main 中文版

## 现在该做什么

把每个 material edit 映射回 stated outcome 或 necessary invariant restore；没有链的 work 移出当前 delivery，给它独立 intent 与 acceptance criteria。

## 为什么重要

Broad diff 会让因果变贵：behavior、cleanup、architecture、dependency 同时变化后，任何 regression 都拥有更大的 suspect set，reviewer 也难判断哪些 edit 是证明、哪些只是偏好。

## 修复策略

把每个改动的区域映射到一条 requirement。保留必要的 transitive edits，但把独立改进推迟为带各自 acceptance criteria 的独立工作。命名每个保留的相邻文件的必要性，让边界保持可检查。

## 决策分支

- 若 edit 没有通向 acceptance criteria 或被扰动 invariant 的链，把它移出本 delivery。
- 若 edit 是 intended change 之后编译或 invariant restore 所必需，保留它并命名那个必要性。
- 若工作已变成对相邻 subsystem 的 rewrite，拆出一个新 change，而不是扩大本 change。

## 常见假修复

- 因为额外改动“都是好改动”就继续捆绑。
- 用一段长说明事后给 unrelated cleanup 找理由。
- 为减少 PR 数把 dependency bump/formatting 一起塞进来。
- 反过来把真正编译/contract 必需的 transitive edits 错删，留下半成品。

## 验证

最终 diff 中每个 material area 都能用一句短链解释：requirement → necessary edit，或 intended change → disturbed invariant → required restore。

## 完成条件

交付完整但不膨胀；所有 edits 属于同一个 causal story，邻近机会另有自己的 change。