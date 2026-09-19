# epistemic-reasoning — HOW

## 架构模型与执行流

`epistemic-reasoning` 实现了由程序控制的高层认知探究工作流：

```text
高层探究请求 (问题, 约束, 预算)
  ↓
初始化/恢复 EpistemicState (建立充分状态)
  ↓
程序工作流引擎 (程控调度、状态推进与终止判定)
  ↓
[程序控制循环阶段]:
  内核根据图与证书决策下一个工作项
  ↓
  确定性纯计算步骤 (Bayes / A* / MCTS / 闭包收敛)
  ↓
  需要语义调研时: 同步调度内部只读 Engineer 实例 (受预算/取消约束，无修改/执行/DevOps/Fission权能)
  ↓
  接纳合法调研结果 (按 WorkId + attempt 幂等吸收，过滤晚到结果，防重复购买)
  ↓
  判断终止条件 (收益收敛、预算耗尽或 Stop 证书生成)
  ↓
产出 CanonicalAnswer (带分列认识基底与确定性 export)
```

## 核心机制

Sphinx 的 canonical digest、事件 ID、blind token、response commit 与 SelfPrediction seal 共用 `runtime-platform/digest` 的 `HostDigest.sha256Hex`。原语 shard 只有字符串摘要 `.fs/.fsi`，无领域 ProjectReference，不把 OpenCode 消息／事件合同带入该依赖。canonical JSON、字段顺序策略、ID 前缀与截断、salt 和各调用方输入拼接保持不变；既有 replay、host-equivalence、legacy-golden、split-ballot 与 self-prediction 测试继续验证其生产后果。

### 1. 认知状态结构与生命周期 (State Structure & Lifecycle)

- **充分状态管理**：`EpistemicState` 显式维护 `Findings`、`Evidence`、`Hypotheses`、`Dependencies` 与 `CognitiveActions`，拒绝将原始文本记录作为状态本体。
- **动态契约**：`RootContract` 维持连续概率分布，可根据调查中返回的语义评估自适应调整，动态激活对应方法生成器。

### 2. 全局闭包与幂等同步 (Global Closure & Idempotence)

- 每次接收观测后，内核必须同步推导事实推论、概率更新、动作重估与等价约简，直到达到结构不动点。
- 闭包操作满足严格幂等性，纯内部计算不创造虚假证据或人为抬高后验置信。

### 3. 程序化探究与只读 Engineer 调度

- **完全程控**：预算、调度、状态推进与终止判定完全由程序控制，外部调用方通过高层入口传入问题、约束与预算直接获取结果；无 Inquiry 模型驾驶层。
- **内部只读 Engineer**：语义调研通过受限的同步 Engineer 实例完成，仅具备只读调查权，无源码修改、真实执行、DevOps 差遣、递归探究或 Fission 权能。
- **身份幂等与防重复购买**：调研结果严格绑定 `(WorkId, attempt)`，已完成且已接纳结果不重复购买，晚到结果不误接纳。
- **全链取消**：取消信号自顶层高层工具穿透至子 Engineer 会话与结果接纳层，确保安全 drain 且无孤儿会话。

### 4. 概率推断资格门禁 (Bayesian Qualification Gate)

- 严格校验证据的数值资格：必须具备有限 `[0, 1]` 区间内的似然度并覆盖全部假设空间。
- 按 `DependencyKey` 进行组内聚合，每个独立来源组仅选出一个规范代表参与似然度连乘，彻底根除同源重复陈述对后验的虚假放大。

### 5. 依赖感知的 Pareto 等价约简 (Pareto Equivalence Reduction)

- 候选动作仅在内核改写或 semantic+dependency 完全相同时归入同一等价类。
- 等价类内部执行多维收益与成本的支配比较，不可直接比较的候选保留在 Pareto 前沿，防止信息价值与执行成本的权衡被单一标量粗暴抹平。

### 6. Sphinx-GEC 组合面 (GEC Composition Surface)

- `Sphinx/Core` 只定义 ID、canonical opaque envelope、typed hypergraph、证书槽、work 事实、预算、事件与纯 reducer；认识论零硬编码，`Kind`/`Relation`/schema 只比 identity/hash。
- 同一 `NodeId` 的 `ValueCertificate` 同时持有 exact、lower/upper envelope、sample summary、ordinal constraints、latent posterior、residual 与 witness/derivation 引用；exact/bound 声明确定性 concretization inclusion，sample/latent 声明带显式 level/error/assumptions/scope 的概率 coverage。
- `replay` 把 JS 事件解码为 canonical `InquiryEvent`（`parent:"none"` 为创世，否则链式；合成 id 为 `"ev"+revision`），经 `Reducer.fold` 得到 `semanticView`；`stateHash`/`semanticHash` 是输入事件列表 canonical-JSON 的 sha256（键序无关，事件序相关）。
- `schedule` 只选依赖满足、冲突互斥、预算内批次；不可比收益保留 Pareto frontier；批组合是 canonical id 序函数复合，不做 `ΣΔ`。
- `splitBallot` 先存共同 root snapshot 再以可复现 PRNG 分配处理/标签/顺序；问法效应是带符号 difference-in-means 加 permutation null；`selfPrediction` 密封承诺加 epsilon-floor log score；`stopCertificate` 只覆盖已检验 framing 族。
- Host 与导出只翻译同一 Core 事件/证书契约：`foldHostEvents` 把 host 私有字段排除在 hash 外；`planOpenCodeDispatch` 只描述 blind child（共同根快照、无 sibling/失败泄漏、每次重试新 child、depth 恒 1）。
