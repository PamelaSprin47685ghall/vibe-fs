# epistemic-reasoning — HOW

## 架构模型与执行流

`epistemic-reasoning` 实现了带控制器的认知协同循环（Co-yield Coroutine）：

```text
start(question)
  ↓
初始化 EpistemicState (建立充分状态)
  ↓
Policy.decide → 生成挂起请求 PendingRequest (首步固定为 SemanticAssessmentRequest)
  ↓
MCP 层返回 structuredContent 携带 nextTool 提示
  ↓
[循环交互阶段]:
  阶段工具 (assess / propose / investigate / synthesize) 提交 Observation
  ↓
  校验 Observation 与当前 PendingRequest 是否严格同型
  ↓
  Absorb 阶段: 吸收观测 (提案写入控制层，仅调查产生事实与证据)
  ↓
  Global Closure: 触发闭包同步循环直至不动点
    [Bayes.update → Value.revalue → Representation.optimize → Solver 同步]
  ↓
  Policy.decide:
    - 收益收敛或预算耗尽 → 产出 CanonicalAnswer (带分列认识基底)
    - 探究继续 → 产出下一个 PendingRequest 及 nextTool 引导
```

## 核心机制

### 1. 认知状态结构与生命周期 (State Structure & Lifecycle)

- **充分状态管理**：`EpistemicState` 显式维护 `Findings`、`Evidence`、`Hypotheses`、`Dependencies` 与 `CognitiveActions`，拒绝将原始文本记录作为状态本体。
- **动态契约**：`RootContract` 维持连续概率分布，可根据调查中返回的语义评估自适应调整，动态激活对应方法生成器。

### 2. 全局闭包与幂等同步 (Global Closure & Idempotence)

- 每次接收观测后，内核必须同步推导事实推论、概率更新、动作重估与等价约简，直到达到结构不动点。
- 闭包操作满足严格幂等性，纯内部计算不创造虚假证据或人为抬高后验置信。

### 3. 概率推断资格门禁 (Bayesian Qualification Gate)

- 严格校验证据的数值资格：必须具备有限 `[0, 1]` 区间内的似然度并覆盖全部假设空间。
- 按 `DependencyKey` 进行组内聚合，每个独立来源组仅选出一个规范代表参与似然度连乘，彻底根除同源重复陈述对后验的虚假放大。

### 4. 依赖感知的 Pareto 等价约简 (Pareto Equivalence Reduction)

- 候选动作仅在内核改写或 semantic+dependency 完全相同时归入同一等价类。
- 等价类内部执行多维收益与成本的支配比较，不可直接比较的候选保留在 Pareto 前沿，防止信息价值与执行成本的权衡被单一标量粗暴抹平。

### 5. MCP 交互映射 (MCP Affordance Translation)

- MCP 服务端将内核的挂起请求严格映射为对应的阶段工具，并输出 `nextTool` 引导字段。
- 服务端身份由 `PackageMetadata` 从 `package.json` 读取，杜绝基于当前目录探测带来的环境漂移。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EPI-001 | `requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-002 | `requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-003 | `requirements/epistemic-reasoning/tests/semantics.test.mjs` |
| EPI-004 | `requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-005 | `requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-006 | `requirements/epistemic-reasoning/tests/bayes.test.mjs` |
| EPI-007 | `requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-008 | `requirements/epistemic-reasoning/tests/semantics.test.mjs` |
| EPI-009 | `requirements/epistemic-reasoning/tests/bayes.test.mjs` |
| EPI-010 | `requirements/epistemic-reasoning/tests/search.test.mjs` |
| EPI-011 | `requirements/epistemic-reasoning/tests/represent.test.mjs` |
| EPI-012 | `requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-013 | `requirements/epistemic-reasoning/tests/mcp-handle.test.mjs` |
| EPI-014 | `requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs` |
