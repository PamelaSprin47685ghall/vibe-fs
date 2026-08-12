# mixed-side-effect-boundaries — Main 中文版

## 现在该做什么
先把 business decision 抽成可独立运行的 policy，再按 effect contract 划 adapter：storage、network、process、filesystem、Git 等各自拥有自己的 representation / failure / lifetime 翻译。最后用一个薄 workflow 组合 typed outcomes。

## 为什么这很重要
DB transaction、HTTP timeout、process exit、filesystem durability、Git conflict 的“失败”不是同一个概念。把它们都变成 `try/catch + bool`，只是在语法层抹平差异，真实语义仍然存在，并会在异常路径重新报复系统。

边界混合还会扩大 test fixture：为了验证一个 domain branch，被迫 mock 五种基础设施；这不是测试工具的问题，而是 policy 已经知道太多外部世界。

## 修复策略
- 每个 effect adapter 负责把外部协议转换为内部 typed outcome；
- core/policy 只基于这些 outcome 决策；
- orchestration 显式展示 effect ordering；
- retry/cancellation/cleanup 放回实际拥有其语义的 boundary；
- transaction-like cross-effect workflow 若无法原子化，要明确 compensation / unknown outcome，而不是 catch-all。

## 常见假修复
- 建一个 `InfrastructureService` 把所有 effect 收进去；只是换了桶。
- 建十层 interface，每层只转发同样的方法；参见 `translator-layer-bloat`。
- 把所有 exception 统一成 `OperationFailed`，丢掉 retryability/commit uncertainty。
- 只移动代码文件，不改变谁拥有 failure policy。
- 为了“纯 core”把真正 application ordering 隐藏到 framework hooks 中。

## 验证
core policy 应能在没有 DB/network/process/filesystem runtime 的情况下被直接测试；每个 adapter 能独立 contract-test；workflow 可以从代码顺序直接读出 effect 的 causal order。

改变一个 provider 的 error shape，不应迫使无关 domain policy 重写。

## 完成条件
不同外部世界的 failure/lifetime law 各有 owner；policy 决定“应该发生什么”，adapter 决定“这个世界如何表达发生/失败”，workflow 只承担显式编排。
