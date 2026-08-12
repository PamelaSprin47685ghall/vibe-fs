# callback-pyramid — Main 中文版

## 现在该做什么
在 adapter edge 适配 callback API，然后把 operation 改成 structured async flow：一个 top-level scope 拥有 sequence、resource lifetime、cancellation 与 error propagation；parallel work 在显式 join point 汇合。

## 为什么这很重要
Nested callbacks 把 operation 的一个时间故事切成许多局部 closure。每个 closure 都只知道自己的下一步，于是没人拥有完整 lifecycle。维护者最容易在这里漏 cleanup、吞 error、丢 cancellation，或者让 inner work 在 outer scope 已结束后继续跑。

## 修复策略
- foreign callbacks 边界化；
- sequence 用 async/task/structured concurrency 表达；
- resources 用 lexical scope；
- cancellation 从 top-level 向 owned children 传播；
- parallel branches 使用明确 combinator/join；
- error mapping 在 effect boundary 做一次，不在每层 closure 重复 catch。

## 常见假修复
- 只把 inner callbacks 抽成命名函数。
- 从 callbacks 改成十层 `.then()`，仍是 continuation tree。
- flatten syntax 后忘记 thread cancellation。
- 为简化 error flow 在 inner callback 吞错。
- 把所有 callback 变成 global event handlers，进一步隐式化 control flow。

## 验证
从 top-level operation 分别走 success、failure、cancellation、early exit。每条路径都应有明确 cleanup owner；无需跳到另一个 lexical closure 才能解释“谁负责释放/停止”。

## 完成条件
operation 的 causal order 可以从上到下阅读；failure、cancellation 与 resources 共用同一结构化 lifetime，而不是散落在 indentation 中。
