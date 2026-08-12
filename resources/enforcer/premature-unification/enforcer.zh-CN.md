# premature-unification — Enforcer 中文版

## 定义
Premature unification 是把“长得像”误当成“属于同一知识”。两个 concept 当前字段/流程一致，并不证明它们必须一起演化；抽成一个 abstraction 等于新增一条强 contract：未来一边改变，另一边也应跟着改变。

DRY 应消除 knowledge duplication，不是 visual resemblance。

## 何时触发
- 两个独立 domain types 因字段一样被合成一个 mega type；
- abstraction 很快出现 mode flags/optional hooks/context enum；
- 一边新增字段，另一边被迫携带 `None/not applicable`；
- “以后可能统一”成为继续共享 owner 的理由；
- change reasons 不同，却必须共同修改 generic layer。

## 不要误判
- 真正 shared invariant 已稳定出现；
- duplicated-control-flow 确实是同一 protocol，应统一；
- tiny primitive/value object 的语义在各处完全相同；
- 两个 context 可以暂时复制几行相似代码，不是设计失败。

## 刀口
问：**A 因自己的 domain reason 改变时，B 是否必然应该一起改？** 若不是，当前相似不能授权共同 abstraction。

## 提醒
相似是观察，不是 ownership。宁可允许独立概念暂时长得一样，也不要为了 DRY 让它们从此被迫一起生活。
