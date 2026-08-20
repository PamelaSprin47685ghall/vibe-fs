# epistemic-reasoning — WHAT

## EPI-001: 认识状态为充分状态而非历史记录

系统维护历史关于未来认知决策的充分统计量，完整的对话记录、问题展开树或单轮自由文本不作为状态本体。权威状态显式由 RootContract、Findings、Evidence、Hypotheses、Dependencies、CognitiveActions、Budget 与 PendingRequest 构成；图搜索前沿、后验概率分布、MCTS 统计与等价类仅作为可替换的求解计算投影。

## EPI-002: Kernel 拥有 Continuation、Closure 与停止权

状态延续、方法激活、动作价值比较、全局闭包同步、停止判定与 Canonical Answer 生成权唯一属于 Kernel。语言模型仅提供内核请求的语义观测、候选生成与调查事实，严禁自行选择下一步动作、跳过闭包检查、自封已回答或直接写入权威后验。Synthesis 仅作为一种普通的 CognitiveAction，不享有特殊终止特权；Stop 动作与其他认知动作处于同一价值空间进行统一比较。

## EPI-003: 权威状态显式拥有认识基底

Canonical Answer 的认识基底必须显式分列 Findings、Evidence 与 Hypotheses。Synthesis 仅作为基于已知 finding keys 的组织投影，不得改写基底。无 Evidence 支撑的 Finding 必须被显式标记为 uncertainty，不得因模型措辞详尽而升级为证据；模型返回的自由文本置信度在吸收时一律丢弃，对象层数值置信仅来源于严格合格的概率推断。

## EPI-004: Pending Request 与 Observation 契约

调用方每轮仅允许回答 Kernel 当前挂起的特定请求。首步固定为 `SemanticAssessmentRequest`，后续输入的 Observation 类型必须与挂起的 Request 严格同型：
- `SemanticAssessmentRequest` ↔ `SemanticAssessment`
- `GenerateCandidatesRequest` ↔ `Candidates`
- `InvestigateRequest(a)` ↔ `Investigation(actionKey = a.id)`
- `SynthesizeRequest` ↔ `Synthesis`
输入错型、错误的 actionKey 或在无挂起请求时提交数据均直接报错，且内核状态不发生改变（Revision 保持不变）。

## EPI-005: Proposal 与 Evidence 严格分层

系统对输入语义实施严格分层管控：
1. `SemanticAssessment`、`Candidates`、`Synthesis` 仅能改变控制状态或组织视图，严禁新增 Finding 或 Evidence，严禁直接改变后验分布；
2. 仅有 `Investigation` 能够新增 Finding 与显式 Evidence；
3. Synthesis 前后 Evidence 数量必须严格保持不变。
模型的重复阐述、递归推演或重新采样不得凭空增加证据维度。

## EPI-006: Evidence 保留 Source 与 Dependency

Evidence 的内部标识至少由规范化的 semantic key 与 dependency key 联合构成。来自两个独立依赖组的同一命题必须同时并存；相同 semantic 与相同 dependency 的重复观测不增加证据维度，仅合并溯源信息 (provenance)。Finding 统一通过 semantic key 引用关联的 Evidence。

## EPI-007: RootContract 保留分布与动态更新

`QuestionForm` 严禁采取 argmax 硬分类，Kernel 必须保留完整的问题形态信念分布并线性派生答案契约信念。后续 Investigation 可返回仅作用于控制层的 `semanticAssessment`，触发 Kernel 重新评估 RootContract 并刷新方法激活状态；此类控制更新不得新增世界证据或改变后验分布。每次吸收新认识后，方法库随状态递归重新触发候选生成。

## EPI-008: 根相对 Action Value 与 Gateway 价值

认知动作的价值评估必须严格相对于根问题的解答效益。具有较低即时信息增益但能解锁后续高价值调查动作的 Gateway 问题通过期望根收益与 Gateway 增益的联合近似进入动作选择。Stop 动作基于当前答案损失进行评估，当停止效用超过所有候选动作或预算耗尽时终止探究。

## EPI-009: 概率推断仅接受合格数值证据

形式化 Bayesian 后验仅在满足以下条件时被允许建立：至少存在两个显式假设；Evidence 明确声明 `numericQualified = true`；似然度完整覆盖全部假设键且取值位于 `[0, 1]` 有限区间；具备明确的 `DependencyKey`。同一依赖组内的多个证据仅取单一规范代表进入似然度乘积，严禁虚假相乘；不合格的证据不得遮蔽合格记录，无合格证据时后验置空并显式标记定性不确定性。

## EPI-010: 经典算法的可验证退化

在约束条件收紧时，内核算法必须在对应子问题上严格退化为经典标准算法：
1. **Graph A***：在确定图、非负成本与固定启发式下按 `g+h` 展开，维护最优路径并在发现更优 g 时重新开放节点；
2. **Bayes**：在固定假设与合格证据下严格按归一化乘积更新后验；
3. **Graph-MCTS**：给定展开模型与终末奖励时执行选择、扩展、模拟与回溯，同语义节点共享统计量。
求解器的内部缓存、访问计数与启发值严禁伪装为认知证据。

## EPI-011: 依赖感知的等价约简与 Wire 判重限制

动作仅在由 Kernel 确定性改写写入内部 `EquivalenceKey` 或 semantic key 与 dependency key 同时相同时方可进入同一等价类。外部输入的 `equivalenceKey` 不具备判重权威；来自不同独立依赖组的相同问题严禁去重。等价类内部仅在候选在各项收益指标均不劣且至少一维更优时方可发生支配，不可比较者保留在 Pareto 前沿。

## EPI-012: Global Closure 幂等性

每次吸收 Observation 后，内核必须依次执行证据吸收、确定性推断、概率资格校验与传播、动作价值重估、等价与 Pareto 约简以及求解器投影同步，循环直至达到不动点（`Closure(S) = S`）。Closure 必须保持严格幂等性（`close(close(S)) = close(S)`），重复计算不得增加证据或改变后验质量。

## EPI-013: MCP Affordance 面忠实翻译 Kernel Continuation

MCP 接口层为每个挂起的 Request 类型提供恰好一个阶段工具（`assess`, `propose`, `investigate`, `synthesize`），并在返回结果中携带由 Kernel 决定的 `nextTool` 提示。MCP 层严禁自行裁决阶段合法性，统一由内核校验；成功调用使用 structuredContent，失败使用带类型错误码的 isError 响应。

Protocol-boundary exemption（遵循 STRUCTURED-WORKFLOW-017）：MCP nextTool 提示属于外部协议边界豁免，满足三项条件：(1) Kernel 唯一拥有 continuation、closure 与停止权；(2) external caller 只提供 typed observation，不决定下一步执行语义；(3) yield/observe 循环是协议语义而非领域程序计数器。

## EPI-014: MCP Server 身份元数据与 Package Manifest 一致

MCP Server 的 initialize 握手响应中，`serverInfo.name` 必须固定为 `sphinx`，`serverInfo.version` 必须严格等于已发布 `package.json` 中的 version 字段（通过模块路径向上解析定位包根读取，禁止基于 cwd 探测）。
