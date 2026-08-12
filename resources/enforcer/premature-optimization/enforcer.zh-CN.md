# premature-optimization — Enforcer 中文版

## 定义
Premature optimization 不是“优化太早”这种时间判断，而是：项目已经确定支付 complexity，却没有 measurement 或 hard budget 证明简单设计买不起目标性能。

Optimization 总是在交换资源：用 readability、determinism、memory model、architectural freedom 换 latency/throughput/memory。没有 scarcity evidence，这笔交易连分母都没有。

## 何时触发
- 无 profile/SLO 就加 cache/pool/batch/lock-free/denormalization；
- “以后会规模化”是唯一理由；
- cold path 因源码看起来贵被手工优化；
- 为几百请求/天引入复杂 concurrency；
- unsafe mutation/custom data structure 先于 bottleneck evidence。

## 不要误判
- 明确 SLO、memory cap、算法复杂度边界已证明简单方案不够；
- 去掉 accidental work 而不增加 machinery；
- 已知 workload 使某算法渐进复杂度实际成为约束；
- security/correctness 机制不是“性能优化”。

## 刀口
要求 optimization 出示账单：**哪个 metric 超预算？简单设计测了多少？这份复杂度具体买回多少？** 缺任一项，就是确定的 complexity 换假设的 scarcity。

## 提醒
Performance 是证据驱动 trade，不是审美。先让 constraint 说话，再让复杂度进场。
