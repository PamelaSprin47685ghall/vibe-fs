# premature-optimization — Enforcer 中文版

## 定义

Premature optimization 不是“优化太早”这种时间判断，而是：项目已经确定支付 complexity，却没有 measurement 或 hard budget 证明简单设计买不起目标性能。

Optimization 总是在交换资源：用 readability、determinism、memory model、architectural freedom 换 latency/throughput/memory。没有 scarcity evidence，这笔交易连分母都没有。

## 支配原则

优化用一种资源换另一种。它常常花 readability、determinism、memory model 简单性或 architectural freedom 来买 latency 或 throughput。没有证据表明被买的资源稀缺，这笔交易就没有分母、无法评估。系统为假设的性能支付确定的 complexity。

## 何时触发

- 无 profile/SLO 就加 cache/pool/batch/lock-free/denormalization；
- “以后会规模化”是唯一理由；
- cold path 因源码看起来贵被手工优化；
- 为几百请求/天引入复杂 concurrency；
- unsafe mutation/custom data structure 先于 bottleneck evidence。

## 不应触发

- 明确 SLO、memory cap、算法复杂度边界已证明简单方案不够；
- 去掉 accidental work 而不增加 machinery；
- 已知 workload 使某算法渐进复杂度实际成为约束；
- security/correctness 机制不是“性能优化”。

## 与相邻规则区分

`incidental-complexity-dominates` 是结果性的设计状态；`dependency-bloat` 引入 machinery。Tie-break：如果投机性性能权衡先于 evidence，用本规则；如果设计已经被 accidental complexity 主导，用 `incidental-complexity-dominates`；如果拉进了未用的库，用 `dependency-bloat`。

## 判定程序

要求 optimization 出示账单：哪个 metric 超预算？简单设计测了多少？这份复杂度具体买回多少？缺任一项，就是确定的 complexity 换假设的 scarcity。

## 例子

- positive：无 profile、每天几百请求的 workload 下，为“scale”加 custom lock-free cache。
- near-miss：profiling 显示 90% 延迟在某 serialization path，然后针对 stated SLO 特化那条 path。
- counterexample：简单设计已满足 budget；不引入额外 caching 或 pooling。

## Nudge

Performance 是证据驱动 trade，不是审美。先让 constraint 说话，再让复杂度进场。