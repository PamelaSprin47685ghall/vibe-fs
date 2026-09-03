# epistemic-reasoning — WHAT

## EPI-001: 认识状态为充分状态而非历史记录

系统维护历史关于未来认知决策的充分统计量，完整 transcript、问题展开树或单轮自由文本不作为状态本体。Sphinx Core 的权威状态由 event head、typed hypergraph、ValueCertificate、WorkItem、PluginLock、ResourceBudget 与 InquiryStatus 构成。默认 Legacy plugin 可在 opaque graph payload 中维护 RootContract、Findings、Evidence、Hypotheses、Dependencies、CognitiveActions 与 PendingRequest；搜索缓存、后验数值和采样统计均为可替换证书或投影，不得成为第二权威状态。

## EPI-002: Runtime 拥有 Continuation 提交权，插件拥有认识论判断

Runtime 唯一执行 work dependency、闭包传播、资源扣账、事件提交与 terminal transition；语言模型只能提交当前 WorkItem 要求的 typed observation，严禁自行改写证书、自封完成或绕过闭包。方法激活、价值比较、stop certificate 与 answer rendering 由锁定的插件提供，Core 不得复制其公式。默认 Legacy plugin 继续把 Synthesis 视为普通 CognitiveAction，并保持原有 continuation 与停止行为。

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

## EPI-008: Legacy 根相对 Action Value 与 Gateway 价值

默认 Legacy value plugin 的认知动作评估必须严格相对于根问题的解答效益。低即时信息增益但能解锁后续高价值调查的 Gateway 问题通过期望根收益与 Gateway 增益进入 Legacy 动作比较；Legacy stop certificate 基于当前答案损失。该标量公式只属于 Legacy plugin，不得成为 Core 或跨插件 scheduler 的通用收益尺度。

## EPI-009: Bayes exact refiner 仅接受合格因子

形式化 posterior 仅在有限离散假设至少两个、先验可归一、观测因子在给定假设后条件独立、每个 likelihood 完整覆盖假设键且为有限 `[0,1]` 数、DependencyKey 明确、partition function 严格为正时建立。同一依赖组只取一个 canonical factor，不合格记录不得遮蔽合格记录。乘积必须在 log-space 计算并以 log-sum-exp 归一；零先验保持零。条件不满足时 exact 槽为空并输出 typed qualitative uncertainty，严禁伪造数值后验。

## EPI-010: 三种经典精化的可验证退化

在各自假设成立时，插件必须退化为对应标准算法：
1. **Graph A***：有限、确定、非负成本图与非负 admissible heuristic 下按最小 `g+h` 展开；发现更优 `g` 时重开。证书下界是 open frontier 的全局最小 `f`，上界是全局 incumbent，不把单节点 `g+h` 误报为全局下界；
2. **Bayes**：满足 EPI-009 时 exact sum-product 输出与 brute-force normalized product 一致；
3. **Graph-MCTS**：有限动作、有限 horizon 或折扣回报、bounded reward 与 generative kernel 下执行选择、扩展、模拟、回溯并按 semantic node 共享统计。采样证书只能声明带 `δ` 的概率覆盖与收敛性质，不得声明确定性 singleton。
缓存、visit、heuristic 与模型自述分数严禁伪装为外部世界证据。Bellman 是决策固定点面，不得作为第四 refiner 或互斥 `SolverMode`。

## EPI-011: 依赖感知的等价约简与 Wire 判重限制

动作仅在由 Kernel 确定性改写写入内部 `EquivalenceKey` 或 semantic key 与 dependency key 同时相同时方可进入同一等价类。外部输入的 `equivalenceKey` 不具备判重权威；来自不同独立依赖组的相同问题严禁去重。等价类内部仅在候选在各项收益指标均不劣且至少一维更优时方可发生支配，不可比较者保留在 Pareto 前沿。

## EPI-012: 拓扑精化闭包幂等

每次接受 Observation 后，Runtime 按 plugin dependency DAG 传播 PluginDelta，直至没有可应用的确定性精化。确定性 closure 必须满足 `close(close(S)) = close(S)`，不得新增 witness、重复扣账或改变 certificate。归一化、incumbent 与 posterior 更新按 canonical event 顺序组合；只有声明独立支持且 conflict keys 不交叠的 delta 才要求交换。默认 Legacy plugin 必须保持原有吸收、Bayes、value、Pareto 与 projection 的可观察闭包结果。

## EPI-013: MCP Host 忠实翻译通用 Work 与 Legacy Continuation

MCP Host 必须暴露 `sphinx_inquiry_start`、`sphinx_work_submit`、`sphinx_inquiry_status`、`sphinx_inquiry_export`、`sphinx_inquiry_cancel`；`submit` 一次接受一个或多个结果并直接返回下一批 ready work。旧 `start`、`assess`、`propose`、`investigate`、`synthesize`、`status`、`cancel`、`resume` 全部保留为 Legacy Adapter，其中四种 PendingRequest 仍各有唯一阶段工具与 `nextTool`。Host 只做 schema/表示转换，不裁决 observation 合法性、refiner、停止或答案。

Protocol-boundary exemption（遵循 STRUCTURED-WORKFLOW-017）：`nextTool` 与 generic work envelope 都是外部协议语义；默认 Legacy profile 下，Legacy Kernel 唯一拥有 continuation、closure 与停止判定，external caller 只提供 typed observation，不决定下一步执行语义；旧四阶段的 yield/observe 循环是协议语义而非领域程序计数器；通用 WorkEnvelope 路径由 Runtime 独占事件提交与 continuation，caller 只提交 typed observation。

## EPI-014: MCP Server 身份、版本与能力协商

MCP initialize 的 `serverInfo.name` 固定为 `sphinx`，`serverInfo.version` 严格等于 package manifest version，并通过 module path 定位包根而非 cwd。`2024-11-05` 客户端必须继续使用 structuredContent/Legacy tools；更新协议可发现 generic tools。Tasks 只有双方协商能力后才可启用，direct-provider 不依赖已弃用的 MCP Sampling。

## EPI-015: Core 认识论零硬编码

`Sphinx/Core` 只能定义 ID、canonical opaque envelope、typed hypergraph、certificate slots、work facts、budget、event 与 reducer。Core 源码不得出现 Legacy 方法名、Finding/Evidence/Hypothesis 本体、Bayes/A*/MCTS/Borda/BTL、ranking、stop threshold 或 answer renderer；Core 对 `Kind`、`Relation`、schema ID 与 payload 只比较 identity/hash，不解释语义。

## EPI-016: 单一证书空间与分型精化保证

同一 NodeId 的 ValueCertificate 可同时持有 exact、lower/upper envelope、sample summary、ordinal constraints、latent posterior、residual 与 witness/derivation event references。系统不得存在互斥 SolverMode 或 parallel Bayesian/Search/MonteCarlo state。价值偏序 `≼V` 与信息精化预序 `⊑I` 必须分开：exact/bound plugin 可声明确定性 concretization inclusion；sample plugin 必须声明 coverage level、assumptions 与随机误差，不能用确定性 inclusion；witness 增长不参与反对称性判断。

## EPI-017: 探究协议是可回放实验

每项 accepted observation 必须绑定 root snapshot hash、BranchId、WorkId、attempt、plugin lock、schema content hash、prompt/question ID、wording/polarity、candidate/label/order permutation、treatment assignment、blind token、random seed、model/provider、sampling parameters与 usage。seed 只固定 Sphinx 自己的随机化，不承诺 provider 输出确定。重放消费已接受 observation，不重新调用 provider，并产生相同 canonical Core state hash。

## EPI-018: 两宿主语义等价

给定相同 initial envelope、plugin lock、canonical accepted-event 序列与资源事实，MCP Host 与 OpenCode Host 必须经同一 reducer 得到相同 graph、certificate、work、budget、status、answer 与 semantic hash。Host 私有 session ID、transport receipt、arrival timing 和日志不得进入 semantic hash；并发 provider 到达顺序不同不构成“相同事件序列”。

## EPI-019: Inquiry 以 canonical EventStore 为唯一 durable truth

Sphinx 业务事实只能追加到 DURABLE-EVENTS 的 canonical EventStore；禁止 SQLite、feature-private NDJSON、第二 history fold 或 durable snapshot。append 成功后才可推进 Current/响应成功。进程重启后 inquiry 由 Integrator Current 恢复；丢弃任意 process-local projection cache 不改变结果。`expectedRevision` 不匹配必须在写入前返回 typed conflict；`workId + attempt` 的同一 canonical observation 幂等，冲突 payload 必须拒绝。

## EPI-020: Plugin manifest、依赖与 schema lock 不可漂移

InquiryCreated 时锁定每个 plugin 的 ID、release、ABI hash、capability、dependency 与 immutable schema content hash。缺依赖、同 ID 多 release、ABI 不匹配、schema hash 不匹配或运行中换 plugin 必须在接受 observation 前 fail closed。EventEnvelope 本身遵循 additive event vocabulary，不携带 storage/schema version；schema revision 属于唯一 schema ID 的一部分。

## EPI-021: 通用 WorkItem 拒绝非法生命周期

WorkItem 用封闭状态表达 Planned、Ready、Leased、Executing、InputRequired、Succeeded、Failed、Cancelled 与 Superseded；需要 fence/session/attempt 的状态在对应 case 内携带证据，不以 bool+option 拼装。依赖满足前不得 Ready；同一 attempt 至多接受一个 observation；terminal 不可回到 executing。lease 释放、重试、取消与 crash recovery 只由 durable transition 或 typed Host terminal 驱动，禁止 `leaseExpiresAt`、heartbeat timeout 或 wall clock 推断。

## EPI-022: Scheduler 选择相容计算而非统一认识论总分

Plugin 提交 refinement target、依赖、conflict keys、资源请求、预期 certificate effect 与可选的同尺度 decision-loss estimate。Runtime 只选择依赖已满足、隔离不泄漏、冲突键互斥且总资源不超预算的 batch。收益尺度不可比较时保留 Pareto frontier；只有 plugin 显式声明共同 currency 才可相加。批更新以 canonical 顺序函数组合表示，不以 `ΣΔ` 假设非交换后验可加。

## EPI-023: Split-ballot 随机化与问法效应估计可审计

Questionnaire plugin 必须先保存共同 root snapshot，再以可复现 PRNG 完成处理、candidate label 与 order 分配，执行 open-before-closed、balanced incomplete block、reverse wording、共同 anchor 与 commit–reveal–revise。blind branch 不接收 sibling answer、当前 ranking 或 aggregate tendency。问法效应使用有方向的 difference-in-means 或声明的模型系数，并报告 uncertainty/permutation null；ATE 解释必须声明 SUTVA/no-interference、positivity、同前缀、无 differential attrition 与 estimand，非负距离不得冒充无偏 ATE。

## EPI-024: Borda 与 Bradley–Terry 的适用域显式

Borda 只作为 complete/equal-exposure ranking 的 baseline；存在 tie、abstention、top-k 或不平衡 exposure 时必须使用声明的 fractional/appearance-normalized extension。Borda 只保证 ballot-order invariance 与 candidate-label equivariance，不声称 clone independence、IIA 或是 BTL 的严格退化。BTL 必须固定 location gauge、检查 comparison graph/design rank、用稳定 sigmoid 与 regularization 处理 separation，并返回 finite estimate、fit diagnostics 与 uncertainty；无法识别时返回 typed error，不输出伪 posterior。

## EPI-025: Proper self-prediction 密封且数值安全

Future-self prediction 必须在 reflection stimulus 前 commit 并绑定 WorkId；target observation 不得受未密封预测内容影响。Log score 对概率使用声明的正 epsilon floor，Brier score验证 simplex 后计算。Raw score 不替代最终答案；calibration 与 sharpness 分开报告，held-out target 才可更新 branch/protocol calibration certificate。

## EPI-026: 固定点存在性与异步收敛不得过度宣称

Plugin 必须声明其 closure 成立域：finite DAG，或 complete lattice/DCPO 上 monotone/Scott-continuous operator，或有明确 contraction modulus 的 metric space。未满足时 Runtime 只能报告 bounded iteration/residual，不得宣称唯一 fixed point。异步 Exact/Bound/Sample/Ordinal 收敛仍是 conjecture，除非额外证明 finite decision set、strict optimality gap、vanishing uncertainty、correct specification、公平调度与 order-aware composition。

## EPI-027: OpenCode Host 复用现有受管执行语义

OpenCode Host 暴露与 MCP 同义的 start/status/cancel/explain/export，并把 ready WorkItem 交给现有 managed session、delegation/fission、capacity 与 failure-policy owner。每个 blind work 从共同 message snapshot 建立独立 child；失败重试使用同一 WorkId、新 attempt、新 child 和原始快照，不携带失败输出。fan-out/fan-in、abort、shutdown drain、provider-step boundary 与 exact capacity fence 不得在 Sphinx 内复制；默认 subagent depth 为 1，worker 不可递归派发。

## EPI-028: Research export 区分可识别对象与外部真值

Export bundle 至少包含 canonical event trace、event head/semantic hash、plugin/schema/model manifest、branch tree、randomization matrix、resource ledger、certificate、ranking/framing/calibration diagnostics、initial/reflective disposition、stable minority modes 与 answer。Renderer 必须区分 `model-belief`、`reflective-model-belief`、`cross-branch-consensus`、`protocol-stable-judgment`、`externally-grounded-claim`；无外部 source 时最后一类为空。全新进程重放 bundle 必须得到相同 semantic hash 与 answer hash。

## EPI-029: Stop certificate 只覆盖已检验的决策域

Stop plugin 可基于 decision-equivalence posterior、tested framing family 内的 reversal bound、anytime-valid sequential evidence、major-mode coverage、budget 与 conservative upper-confidence VOC 生成证书。不得把未测试问法的稳定性、misspecified posterior 或 plugin 自报点估值当作普适保证；重复检查必须控制 sequential error。存在稳定少数模式时答案输出 decision class/distribution，不强压成单一 winner。

## EPI-030: Legacy Adapter 黄金轨迹保持可观察兼容

默认 Legacy profile 必须经 `旧 MCP → Legacy Adapter → Sphinx Core events → Legacy renderer` 重放冻结的 programming-quality transcript，并保持 request/nextTool 顺序、每次 accepted observation 的 revision、最终 epistemic basis、answer 与 `stop-dominates`。唯一明确翻转的旧行为是 process restart：handle 现在恢复为同一 durable inquiry；旧 `restart invalidates handles` 不再是合法特征。
