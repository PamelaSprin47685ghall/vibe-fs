# wholesale-rewrite — Main 中文版

## 现在该做什么
把 required semantic delta 映射到被它真正 invalidated 的 owners/invariants，只重做这些结构；其它 known-good paths 与 behavioral witnesses 尽量保留。若 structure 本身确实是 defect，明确记录这个事实，再把 rewrite 边界限定在那里。

## 为什么这很重要
Rewrite 会让 verification burden 从“证明这个新行为”膨胀成“重新证明整个 subsystem”。最危险的是 tests 也跟着重写，于是新实现与新 examiner 可以一起忘掉旧的 edge-case knowledge。

## 常见假修复
- 用“新代码更少”证明 rewrite 更简单。
- 新旧 package 并存，形成 `half-finished-refactor`。
- 从零重写 tests，让 preserved behavior 没有 independent memory。
- 因为反对 rewrite 而死守一个已被证明确实错误的 core structure；precision 不是保守主义。

## 验证
Diff 应能解释为“只变更被新 contract 推翻的 assumptions”。Unchanged behavior 继续由既有 tests/evidence 保护，不需要在新世界里从零重新猜。

## 完成条件
改动的 blast radius 与 semantic invalidation 匹配；既有可靠知识尽量保留，只有真正被要求否定的结构才被重建。
