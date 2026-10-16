# premature-optimization — Lesson

## 评价

### 定义

Premature optimization 不是“优化太早”这种时间判断，而是：项目已经确定支付 complexity，却没有 measurement 或 hard budget 证明简单设计买不起目标性能。

Optimization 总是在交换资源：用 readability、determinism、memory model、architectural freedom 换 latency/throughput/memory。没有 scarcity evidence，这笔交易连分母都没有。

### 支配原则

优化用一种资源换另一种。它常常花 readability、determinism、memory model 简单性或 architectural freedom 来买 latency 或 throughput。没有证据表明被买的资源稀缺，这笔交易就没有分母、无法评估。系统为假设的性能支付确定的 complexity。

### 何时触发

- 无 profile/SLO 就加 cache/pool/batch/lock-free/denormalization；
- “以后会规模化”是唯一理由；
- cold path 因源码看起来贵被手工优化；
- 为几百请求/天引入复杂 concurrency；
- unsafe mutation/custom data structure 先于 bottleneck evidence。

### 不应触发

- 明确 SLO、memory cap、算法复杂度边界已证明简单方案不够；
- 去掉 accidental work 而不增加 machinery；
- 已知 workload 使某算法渐进复杂度实际成为约束；
- security/correctness 机制不是“性能优化”。

### 与相邻规则区分

`incidental-complexity-dominates` 是结果性的设计状态；`dependency-bloat` 引入 machinery。Tie-break：如果投机性性能权衡先于 evidence，用本规则；如果设计已经被 accidental complexity 主导，用 `incidental-complexity-dominates`；如果拉进了未用的库，用 `dependency-bloat`。

### 判定程序

要求 optimization 出示账单：哪个 metric 超预算？简单设计测了多少？这份复杂度具体买回多少？缺任一项，就是确定的 complexity 换假设的 scarcity。

### 例子

- positive：无 profile、每天几百请求的 workload 下，为“scale”加 custom lock-free cache。
- near-miss：profiling 显示 90% 延迟在某 serialization path，然后针对 stated SLO 特化那条 path。
- counterexample：简单设计已满足 budget；不引入额外 caching 或 pooling。

## 建议

### 现在该做什么

没有 measured bottleneck / explicit budget 时恢复最简单正确设计；已有约束时只优化 dominant measured cost，并把 measurement 与 complexity 绑定在同一局部边界。

### 为什么重要

Speculative optimization 会把“也许未来重要”的猜测固化成每个测试、refactor、incident 都必须理解的结构。即使 workload 从未需要它，复杂度仍然每天收租。

### 修复策略

定义 performance objective，benchmark/profile 简单路径，只优化 dominant measured cost。让 optimization 保持足够隔离，使它的证明与回滚都留在局部。

### 决策分支

- 若没有 measured bottleneck 或 stated budget，移除投机 machinery，恢复简单设计。
- 若 profiled cost 或硬 SLO 已经 binding，只优化那个 cost，并让证明留在局部。

### 常见假修复

- 用“更 scalable”代替 benchmark。
- 看到 O(n²) 就自动重写，即使 n 永远小且无预算压力；反之，真实 n 已巨大也不能用“premature”拒绝必要算法。
- 优化后不复测，只证明代码更复杂。
- 让 optimization types/assumptions 泄漏整个 domain，增加未来退出成本。

### 验证

代表性 workload 下记录 before/after，证明改进对目标 budget 有实际意义；若移除 optimization 仍达标，应优先简单设计。

### 完成条件

每一份非平凡 performance complexity 都能指向它满足的 scarcity constraint 与测量证据。

### Nudge

Performance 是证据驱动 trade，不是审美。先让 constraint 说话，再让复杂度进场。
