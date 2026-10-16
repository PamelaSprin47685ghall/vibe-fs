# mixed-side-effect-boundaries — Lesson

## 评价

### 定义

一个 unit 同时碰 DB、HTTP、Git、process、filesystem 并不自动有罪。真正的病灶是：**这些拥有不同 failure / lifetime / retry / authority law 的外部世界，被同一个 imperative policy owner 混成一团**。

当“该做什么”的业务判断与“每个外部世界怎样失败”的适配细节纠缠，测试就必须一次模拟整个宇宙，任何 effect 的变化都会迫使无关 policy 跟着改。

### 支配原则

Effect 之间的差别不只是 API，而是失败模型、生命周期、retry 语义与 authority。把它们混在一起，等于把这些不同的契约压进同一张 control-flow 表面，于是测试必须模拟整个世界，policy 与编排事故再也分不开。隔离恢复了一个关键的不对称：policy 可以决定“应该发生什么”，而不必知道每个外部世界怎样执行它。

### 何时触发

- 一个 service method 一边决定业务规则，一边直接 transaction、shell、HTTP、filesystem；
- retry policy、rollback、auth、domain branching 与多个 adapter call 交错；
- 某个 effect 的 timeout/error shape 泄漏进另一个 effect 的 decision；
- 单测一个 policy 需要构造 DB、Git、network、process 四套 fixture。

### 不应触发

- composition root / application workflow 只顺序执行已经决定好的 commands，可以同时认识多个 adapter；
- 同一 store contract 下的多个 query 不算“多个外部世界”；
- generated adapter 做机械协议翻译而不拥有 policy，不必为了 purity 再拆十层；
- 跨多个 effect 的 atomic workflow 如果确实是 application concern，可以有一个 orchestrator，但各 effect law 仍应由自己的 adapter 拥有。

### 与相邻规则区分

`impure-core` 关注 policy 主动观察/执行外界；`mixed-side-effect-boundaries` 更强调多个不同 effect contract 被一个 owner 一起承担。

`god-module` 范围更广；这里即使函数不大，只要 effect laws 互相污染，也成立。

### 判定程序

问：**如果只改变其中一个外部系统的 failure model，为什么业务 policy 也必须被打开修改？**

若答案只是“因为它们都写在这个 function 里”，边界已经混了。

### 例子

- 正例：一个 `deploy()` 同时判断是否可发布、开 Git worktree、写 DB、启动 process、POST webhook，并在一串 catch 中决定补偿。
- 近邻：workflow 接收 `DeploymentPlan`，依次调用 Git/Store/Process ports；每个 adapter 各自翻译 failure，workflow 只处理 typed outcome。
- 反例：为了“分层”给每个一行调用套一个 forwarding service，但 failure/policy 仍然混在顶层。

## 建议

### 现在该做什么

先把 business decision 抽成可独立运行的 policy，再按 effect contract 划 adapter：storage、network、process、filesystem、Git 等各自拥有自己的 representation / failure / lifetime 翻译。最后用一个薄 workflow 组合 typed outcomes。

### 为什么重要

DB transaction、HTTP timeout、process exit、filesystem durability、Git conflict 的“失败”不是同一个概念。把它们都变成 `try/catch + bool`，只是在语法层抹平差异，真实语义仍然存在，并会在异常路径重新报复系统。

边界混合还会扩大 test fixture：为了验证一个 domain branch，被迫 mock 五种基础设施；这不是测试工具的问题，而是 policy 已经知道太多外部世界。

### 修复策略

- 每个 effect adapter 负责把外部协议转换为内部 typed outcome；
- core/policy 只基于这些 outcome 决策；
- orchestration 显式展示 effect ordering；
- retry/cancellation/cleanup 放回实际拥有其语义的 boundary；
- transaction-like cross-effect workflow 若无法原子化，要明确 compensation / unknown outcome，而不是 catch-all。

### 决策分支

- 若这个 unit 既决定 policy 又驱动互不相关的 effects，把决策抽到 pure core，给每个 effect 自己的 port；
- 若它只是顺序执行已经决定好的 commands，保持薄 shell，不要发明额外层次。

### 常见假修复

- 建一个 `InfrastructureService` 把所有 effect 收进去；只是换了桶。
- 建十层 interface，每层只转发同样的方法；参见 `translator-layer-bloat`。
- 把所有 exception 统一成 `OperationFailed`，丢掉 retryability/commit uncertainty。
- 只移动代码文件，不改变谁拥有 failure policy。
- 为了“纯 core”把真正 application ordering 隐藏到 framework hooks 中。

### 验证

core policy 应能在没有 DB/network/process/filesystem runtime 的情况下被直接测试；每个 adapter 能独立 contract-test；workflow 可以从代码顺序直接读出 effect 的 causal order。

改变一个 provider 的 error shape，不应迫使无关 domain policy 重写。

### 完成条件

不同外部世界的 failure/lifetime law 各有 owner；policy 决定“应该发生什么”，adapter 决定“这个世界如何表达发生/失败”，workflow 只承担显式编排。

### Nudge

边界不是按 API 数量切，而是按**不同世界拥有不同失败定律**来切。
