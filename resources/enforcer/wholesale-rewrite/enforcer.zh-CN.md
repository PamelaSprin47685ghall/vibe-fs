# wholesale-rewrite — Enforcer 中文版

## 定义
Wholesale rewrite 不是“大改动即罪”。真正的问题是：required semantic delta 明明只推翻少量 invariant，却选择删除/重建一大片已有、已验证结构，让所有旧知识一起重新进入待证明状态。

现有代码承载的不只设计美丑，还沉积 bug fixes、edge cases、operational constraints 与 compatibility facts。Rewrite 会一次性丢掉这些隐性证据，再要求新 tests 重新发现。

## 何时触发
- 一字段/一行为改动触发整个 service 重写“顺便变干净”；
- generated/new package 复制所有行为，再慢慢补 parity；
- 接受条件很窄，blast radius 却覆盖大量 unaffected owners；
- preserved tests 被一起重写，旧行为不再有独立 witness；
- rewrite 理由主要是“旧代码看起来不好”，不是旧结构本身违反目标 invariant。

## 不要误判
- task 明确授权 greenfield replacement；
- ADR 已证明旧 structure 本身就是 defect，incremental preservation 会继续保留错误 invariant；
- required change 推翻整个 module core invariants，此时 local module rewrite 可能正是最小正确变换；
- regenerated artifact 来自 canonical source，不是丢弃手工知识。

## 刀口
列出新要求真正使哪些旧 assumptions 变 false。**为什么其它已证明 assumptions 也必须一起被删除？** 若答不出，rewrite surface 超过 semantic delta。

## 提醒
Known-good structure 是证据，不是包袱。只有当结构本身就是问题时才重写；否则应尽量保留已经赢得的证明。
