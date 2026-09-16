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

Sphinx 的 canonical digest、事件 ID、blind token、response commit 与 SelfPrediction seal 共用 `runtime-platform/digest` 的 `HostDigest.sha256Hex`；不再在 CoreHash 复制 Node crypto 适配器或保留同名转发入口。原语 shard 只有字符串摘要 `.fs/.fsi`，无领域 ProjectReference，不把 OpenCode 消息／事件合同带入该依赖。canonical JSON、字段顺序策略、ID 前缀与截断、salt 和各调用方输入拼接保持不变；既有 replay、host-equivalence、legacy-golden、split-ballot 与 self-prediction 测试继续验证其生产后果。

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
- 服务端身份（`serverName`/`serverVersion`）由 `McpServer` 自身从包根 `package.json` 读取（`import.meta.url` 固定上溯），杜绝基于当前目录探测带来的环境漂移；Sphinx 树不依赖 distribution 子系统。

### 6. Sphinx-GEC 组合面 (GEC Composition Surface)

- `Sphinx/Core` 只定义 ID、canonical opaque envelope、typed hypergraph、证书槽、work 事实、预算、事件与纯 reducer；认识论零硬编码，`Kind`/`Relation`/schema 只比 identity/hash。
- 同一 `NodeId` 的 `ValueCertificate` 同时持有 exact、lower/upper envelope、sample summary、ordinal constraints、latent posterior、residual 与 witness/derivation 引用；exact/bound 声明确定性 concretization inclusion，sample/latent 声明带显式 level/error/assumptions/scope 的概率 coverage。
- `replay` 把 JS 事件解码为 canonical `InquiryEvent`（`parent:"none"` 为创世，否则链式；合成 id 为 `"ev"+revision），经 `Reducer.fold` 得到 `semanticView`；`stateHash`/`semanticHash` 是输入事件列表 canonical-JSON 的 sha256（键序无关，事件序相关）。
- `schedule` 只选依赖满足、冲突互斥、预算内批次；不可比收益保留 Pareto frontier；批组合是 canonical id 序函数复合，不做 `ΣΔ`。
- `splitBallot` 先存共同 root snapshot 再以可复现 PRNG 分配处理/标签/顺序；问法效应是带符号 difference-in-means 加 permutation null；`selfPrediction` 密封承诺加 epsilon-floor log score；`stopCertificate` 只覆盖已检验 framing 族。
- Host 与导出只翻译同一 Core 事件/证书契约：`foldHostEvents` 把 host 私有字段排除在 hash 外；`planOpenCodeDispatch` 只描述 blind child（共同根快照、无 sibling/失败泄漏、每次重试新 child、depth 恒 1）。
