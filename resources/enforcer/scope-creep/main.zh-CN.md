# scope-creep — Main 中文版

## 现在该做什么
把每个 material edit 映射回 stated outcome 或 necessary invariant restore；没有链的 work 移出当前 delivery，给它独立 intent 与 acceptance criteria。

## 为什么这很重要
Broad diff 会让因果变贵：behavior、cleanup、architecture、dependency 同时变化后，任何 regression 都拥有更大的 suspect set，reviewer 也难判断哪些 edit 是证明、哪些只是偏好。

## 常见假修复
- 因为额外改动“都是好改动”就继续捆绑。
- 用一段长说明事后给 unrelated cleanup 找理由。
- 为减少 PR 数把 dependency bump/formatting 一起塞进来。
- 反过来把真正编译/contract 必需的 transitive edits 错删，留下半成品。

## 验证
最终 diff 中每个 material area 都能用一句短链解释：requirement → necessary edit，或 intended change → disturbed invariant → required restore。

## 完成条件
交付完整但不膨胀；所有 edits 属于同一个 causal story，邻近机会另有自己的 change。
