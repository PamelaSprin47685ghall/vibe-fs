# premature-optimization — Main 中文版

## 现在该做什么
没有 measured bottleneck / explicit budget 时恢复最简单正确设计；已有约束时只优化 dominant measured cost，并把 measurement 与 complexity 绑定在同一局部边界。

## 为什么这很重要
Speculative optimization 会把“也许未来重要”的猜测固化成每个测试、refactor、incident 都必须理解的结构。即使 workload 从未需要它，复杂度仍然每天收租。

## 常见假修复
- 用“更 scalable”代替 benchmark。
- 看到 O(n²) 就自动重写，即使 n 永远小且无预算压力；反之，真实 n 已巨大也不能用“premature”拒绝必要算法。
- 优化后不复测，只证明代码更复杂。
- 让 optimization types/assumptions 泄漏整个 domain，增加未来退出成本。

## 验证
代表性 workload 下记录 before/after，证明改进对目标 budget 有实际意义；若移除 optimization 仍达标，应优先简单设计。

## 完成条件
每一份非平凡 performance complexity 都能指向它满足的 scarcity constraint 与测量证据。
