# Sphinx — 可观察行为

条款前缀：`SPHINX-`。Host 自动注入与 Inquiry 权限见 AGENT-030。

## SPHINX-001：生成式认识状态求解器

Sphinx 是 Kernel 控制、经 MCP 与 LLM co-yield 的生成式认识状态求解器。

用户给出根问题后，系统维护影响未来认知决策的充分状态，不把 transcript、问题树或一轮自由文当状态本体。图、frontier、posterior、MCTS 统计、等价类均是可替换计算表示。

```text
Kernel reaches fixed point
→ Kernel chooses cognitive action or Stop
→ pure action: Kernel executes
→ semantic/external action: yield structured Request
→ LLM/tool returns structured Observation
→ Kernel validates pending Request
→ absorb
→ GLOBAL CLOSURE
→ fixed point
```

continuation、方法激活、动作比较、停止与 Canonical Answer 唯一属于 Kernel。LLM 只提供 Kernel 无法自行获得的语义观测、候选生成与调查结果；不得自选下一步、跳过闭包、自封 answered 或直接写权威 posterior。

## SPHINX-002：handle 唯一绑定 inquiry

每次 inquiry 由不透明 `handle` 标识。Sphinx 进程内 `SessionStore` 维护 `handle → EpistemicState`。

```text
start(question)          → 新 handle
resume(handle, obs)      → 仅续作该 handle
缺失 handle              → error
未知 handle              → error
进程退出                 → handle 失效
```

禁止隐式“上一问”、用 transcript 复原权威状态、Host 再建并行会话表或跨进程伪持久化。

## SPHINX-003：MCP 只有 start / resume

服务器内工具面恰好：

```text
start(question: string)
resume(handle: string, observation: object)
```

成功体必含 `handle` 与 `status = yield | answered`；失败体为 `status = error`。`yield` 必含 Kernel 生成的 structured `request`；`answered` 必含 Kernel 组装的 `answer`。

首步必为 `SemanticAssessmentRequest`。之后 `observation` 必须与该 handle 的 pending Request 同型：

```text
SemanticAssessmentRequest ↔ SemanticAssessment
GenerateCandidatesRequest ↔ Candidates
InvestigateRequest(a)     ↔ Investigation(actionKey = a.id)
SynthesizeRequest         ↔ Synthesis
```

错型、错 actionKey 或无 pending Request → error，且状态不前进。

## SPHINX-004：EpistemicState 与全局闭包

权威状态至少显式拥有：RootContract、Findings、Evidence、Hypotheses、Dependencies、CognitiveActions、Budget、PendingRequest；A*/MCTS/Bayes/Representation 只作为求解投影存在。

每个被接受的 Observation 后执行：

```text
absorb
→ deterministic inference
→ probability qualification / propagation
→ root-relative revalue
→ equivalence + Pareto reduction
→ solver projections
→ repeat
→ fixed point
```

只有 `Closure(S) = S` 后才能 yield 或 answered。Closure 必须幂等；重复纯计算不得凭空制造 Evidence、独立依赖组或 posterior 质量。

Canonical Answer 的认识基底必须显式分列 Findings / Evidence / Hypotheses；Synthesis 只是基于已入状态 finding keys 的组织投影，不能改写认识基底。

## SPHINX-005：F# 单一实现与 Host 正交边界

Sphinx 源码唯一位于 `src/Wanxiangshu/Sphinx/*.fs`，namespace 为 `Wanxiangshu.Sphinx.*`，随 `Wanxiangshu.fsproj` 由 Fable 编译到 `dist/Sphinx/*.js`。

生产 MCP 入口唯一为：

```text
dist/Sphinx/McpServer.js
```

Sphinx 内核模块不得依赖 `Wanxiangshu.Domain`、Agent、Session、OpenCode Host 业务模块；Host 只拥有 MCP identity、launch 配置、`ToolPermission.Sphinx` 与 Inquiry 的 `sphinx_*` 权限。MCP SDK / zod 只允许停在 `Wanxiangshu.Sphinx.McpServer` wire 边界。

禁止第二套手写 `src/sphinx/*.js`、build copy、ToolRegistry / `js-*` 注册或 Host 内嵌 Closure。

## SPHINX-006：Proposal ≠ Evidence；No Free Information

四类输入语义严格分层：

| Observation | 可改变控制状态 | 可新增 Finding | 可新增 Evidence | 可改变 posterior |
|---|---:|---:|---:|---:|
| SemanticAssessment | 是 | 否 | 否 | 否 |
| Candidates | 是 | 否 | 否 | 否 |
| Investigation | 是 | 是 | 仅显式 Evidence | 仅资格成立时 |
| Synthesis | 是 | 否 | 否 | 否 |

SemanticAssessment、候选问题、方法建议、价值估计、Synthesis 文案都是 proposal / computation，不是世界证据。LLM 重述、递归、自我论证、重复采样不得增加 Evidence 或把相关信息伪装成独立来源。

Evidence 的内部 identity 至少包含 normalized semantic key + dependency key：同命题来自两个独立 dependency group 必须能同时存在；同 semantic+dependency 的重复 observation 不增加证据维度，只合并 provenance。Finding 仍按 semantic key 引用 Evidence，因此“同命题多独立来源”不会要求 Finding 复制文本 identity。

Finding 可无 Evidence，但 Canonical Answer 必把这类 claim 标记为 uncertainty；它不能因“模型说得更完整”升级成证据。Finding 自带的 LLM `confidence` 也不具数值资格，Kernel 吸收时丢弃；对象层数值置信只来自 SPHINX-008 的合格概率模型。

## SPHINX-007：RootContract 保留分布；动作价值相对根问题

`QuestionForm` 不做 argmax 硬分类。Kernel 保留完整 form belief，并线性派生 AnswerContract belief；Facets 独立多标签参与方法适用度。该 belief 是 `Q_t(Form)` 而不是开局常量：后续 Investigation 可携带 control-only `semanticAssessment`，Kernel 重算 RootContract 并重新激活 generator；这类控制更新仍不得新增 Evidence 或改变 posterior。

认知动作的比较量必须相对根问题。当前实现的控制近似：

```text
ΔV(a) = dependencyDiscount × (ExpectedRootGain + 0.65 × GatewayGain) − Cost
U(stop) = − CurrentAnswerLoss
U(a)    = U(stop) + ΔV(a)
```

`ExpectedRootGain` / `GatewayGain` 是控制层估计，不是对象层置信度。gateway question 即使一步信息增量小，只要能打开后续高价值动作，仍可被选中。

Stop 与其它动作处于同一比较空间；`U(stop) ≥ max U(a)` 或预算耗尽时停止。Synthesis 也是 CognitiveAction，不拥有特殊终止权。

## SPHINX-008：概率只接受合格数值证据

正式 Bayesian posterior 只有同时满足下列条件才存在：

1. 至少两个显式 Hypothesis；
2. Evidence 明示 `numericQualified = true`；
3. likelihood 覆盖全部 hypothesis key；
4. 每个 likelihood 为有限 `[0,1]` 数；
5. Evidence 有明确 `DependencyKey`。

同一 `DependencyKey` 的多个 Evidence 不得按独立因子重复相乘。Kernel 先过滤不合格 Evidence，再在每个依赖组内选一个规范代表进入 likelihood product；不合格同源记录不得遮住合格记录。无合格因子 → `Bayesian = None`，不得用 LLM 猜测补 posterior；Judgment/Credence 答案必须显式携带 `numeric-credence-unqualified`。

## SPHINX-009：经典算法是可验证退化求解器

Sphinx ontology 不等于 A* / Bayes / MCTS；但约束收紧时必须能得到标准算法行为。

- A*：确定图、非负 cost、固定 goal/heuristic → 按 `g+h` 展开，维护 best-g；closed 节点发现更低 g 时 reopen。
- Bayes：固定 hypotheses、关闭生成、只吸收合格 likelihood evidence → 标准归一化 posterior。
- MCTS：给定可展开模型与 terminal reward → selection / expansion / rollout / backup；同 semantic node key 共享统计，即 graph-MCTS transposition。

这些 solver 不得把自身缓存、visit count、frontier 或 heuristic 冒充认识证据。

## SPHINX-010：等价约简与方法库不偷换 ontology

动作只有两种情况允许进入同一表示等价类：

1. Kernel 的确定性 canonicalization / representation rewrite 明确写入内部 `EquivalenceKey`；或
2. semantic key 与 dependency key 同时相同。

LLM/wire Candidate 的 `equivalenceKey` 不具判重权，当前 codec 直接忽略；无法由 Kernel 证明等价时宁可多保留，也不误合并。相同 semantic+dependency 的 Candidate 是同一 Kernel identity：重复命中时可保留控制价值更好的代表，但必须合并 provenance，不能把“另一个方法也命中”这条来源信息抹掉。相同问题若来自不同独立 dependency group，不得判重。等价类内仅当候选在 ExpectedRootGain、GatewayGain、value、provenance 均不差且 cost 不高，并至少一维严格更优时才支配另一候选；不可比较者保留 Pareto frontier。

方法库是 generator library，不是流水线：Kernel 根据完整 QuestionForm belief + Facets 激活多个方法，再 yield 让语义 oracle 生成具体候选问题。任何 Investigation 吸收新认识后，Kernel 都先把 `NeedsGeneration` 置真；在下一次动作裁决前重新发 `GenerateCandidatesRequest`，Candidates 只负责填充候选并清除此标志。因此方法库会随状态递归生长，而不是开局只跑一次。核心五方法仍为 Multidisciplinary / Abduction / Analogy / Counterexample / Synthesis；扩展库包含 CausalMechanism、BaseRate、Dialectic、Falsification、BoundarySearch、SourceTriangulation、MeasurementCritique、OntologyRepair、UnknownExpansion、ScaleShift、ExperimentDesign。新增方法不得获得调度权。
