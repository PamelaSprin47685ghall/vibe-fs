# epistemic-reasoning — WHAT

> 本页是**唯一 normative 合同**：当前世界必须同时成立的编号命题。每条命题 = 标题 +
> 规范陈述 + 含义/动机 + 边界 + 证据指针（→ HOW.md）。
> 历史断言、迁移沉积、被拒方案**不是**命题（见 HOW.md「历史与弃权」）。
> 前缀 `EPI-`。测试落点表见 `HOW.md`。

源条款：历史 what/sphinx SPHINX-001..010（本包主导全部 10 条，COVERAGE.md 单-owner
裁决）；历史 why/shape/how/proof sphinx 条款、历史 change（Sphinx）。

---

## EPI-001：认识状态是 sufficient state，不是 transcript / 搜索树

**规范陈述**：用户给出根问题后，系统维护影响未来认知决策的充分状态；transcript、问题树或
一轮自由文不是状态本体。图、frontier、posterior、MCTS 统计、等价类均是可替换计算表示。
权威状态至少显式拥有：RootContract、Findings、Evidence、Hypotheses、Dependencies、
CognitiveActions、Budget、PendingRequest；A*/MCTS/Bayes/Representation 只作为求解投影存在。

**含义/动机**：历史包含大量对未来决策无影响的表面差异；把它直接当 state 会让「多说一遍」
伪装成「多知道一点」。

**边界**：不规定状态的具体 record 布局（HOW）；只规定「sufficient state 这一事实 + 权威
状态必须显式拥有认识基底」。

**证据**：HOW.md EPI-001 行（kernel `start_yields_semantic_assessment_request`、
semantics）。

## EPI-002：Kernel 拥有 continuation、closure 与停止

**规范陈述**：continuation、方法激活、动作比较、停止与 Canonical Answer 唯一属于 Kernel。
LLM 只提供 Kernel 无法自行获得的语义观测、候选生成与调查结果；**不得自选下一步、跳过闭包、
自封 answered 或直接写权威 posterior。** Synthesis 也是 CognitiveAction，不拥有特殊终止权。
Stop 与其它动作处于同一比较空间。

**含义/动机**：若 LLM 可以自行说「我已经够了」，Closure、预算、依赖去重、Stop 都退化成
提示词建议。co-yield 是有控制器的 coroutine，不是两个平权生成器聊天。

**边界**：Kernel 的具体调度算法（当前 `Value.bestOpenAction`/`stopDominates`）是 HOW；
「谁拥有决策权」是命题。

**证据**：HOW.md EPI-002 行（kernel `resume_rejects_observation_that_does_not_match_pending_kernel_request`、
mcp-handle `full_co_yield_path_preserves_kernel_continuation`）。

## EPI-003：权威状态显式拥有认识基底

**规范陈述**：Canonical Answer 的认识基底必须显式分列 Findings / Evidence / Hypotheses；
Synthesis 只是基于已入状态 finding keys 的组织投影，不能改写认识基底。Finding 可无 Evidence，
但 Canonical Answer 必把这类 claim 标记为 uncertainty；它不能因「模型说得更完整」升级成证据。
Finding 自带的 LLM `confidence` 不具数值资格，Kernel 吸收时丢弃；对象层数值置信只来自
EPI-009 的合格概率模型。

**含义/动机**：答案必须能说清「凭什么」——基底分列使 uncertainty 可显式报告，未落地的
claim 无法伪装成证据。

**边界**：Canonical Answer 的 wire 形态（当前 `answer` 字段布局）是 HOW；「基底分列 +
ungrounded 标记」是命题。

**证据**：HOW.md EPI-003 行（semantics `ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence`、
mcp-handle `full_co_yield_path_preserves_grounded_epistemic_basis`）。

## EPI-004：Pending Request 契约

**规范陈述**：LLM 每轮只回答 Kernel 当前请求。首步必为 `SemanticAssessmentRequest`；之后
`observation` 必须与该 handle 的 pending Request 同型：

```text
SemanticAssessmentRequest ↔ SemanticAssessment
GenerateCandidatesRequest ↔ Candidates
InvestigateRequest(a)     ↔ Investigation(actionKey = a.id)
SynthesizeRequest         ↔ Synthesis
```

错型、错 actionKey 或无 pending Request → error，且状态不前进（Revision 不变）。

**含义/动机**：这是 controller-owned continuation 的机械表达——调用方无法偷偷提交未被调度
的调查结果，也无法用一句话跳过闭包。

**边界**：Request/Observation 的 wire 编码（当前 JSON 形状）是 HOW（`host-boundary`）；
「同型契约 + 错型不前进」是命题。

**证据**：HOW.md EPI-004 行（kernel `resume_rejects_observation_that_does_not_match_pending_kernel_request`）。

## EPI-005：Proposal ≠ Evidence；No Free Information

**规范陈述**：四类输入语义严格分层：

| Observation | 可改变控制状态 | 可新增 Finding | 可新增 Evidence | 可改变 posterior |
|---|---:|---:|---:|---:|
| SemanticAssessment | 是 | 否 | 否 | 否 |
| Candidates | 是 | 否 | 否 | 否 |
| Investigation | 是 | 是 | 仅显式 Evidence | 仅资格成立时 |
| Synthesis | 是 | 否 | 否 | 否 |

SemanticAssessment、候选问题、方法建议、价值估计、Synthesis 文案都是 proposal / computation，
不是世界证据。LLM 重述、递归、自我论证、重复采样不得增加 Evidence 或把相关信息伪装成独立
来源。Synthesis 前后 `Evidence.Count` 必须相同。

**含义/动机**：generation 不增加 knowledge。若生成能直接增加 evidence mass，递归十轮就能把
同一批信息「说成」更高置信度，系统奖励自我说服。

**边界**：`Evidence.Count` 是当前实现的观测面；「分槽语义」是命题。Investigation 如何产生
Evidence 的具体规则 → HOW（`Absorb.fs`）。

**证据**：HOW.md EPI-005 行（kernel `semantic_assessment_and_candidates_are_control_observations_not_world_evidence`、
semantics `synthesis_is_information_propagation_not_information_acquisition`）。

## EPI-006：Evidence 保留 source/dependency；同源重复不伪装独立支持

**规范陈述**：Evidence 的内部 identity 至少包含 normalized semantic key + dependency key：
同命题来自两个独立 dependency group 必须能同时存在；同 semantic+dependency 的重复 observation
不增加证据维度，只合并 provenance。Finding 仍按 semantic key 引用 Evidence，因此「同命题多
独立来源」不会要求 Finding 复制文本 identity。

**含义/动机**：source triangulation 的价值恰恰来自独立性；依赖感知去重是唯一不会删除独立
来源价值的判重。

**边界**：dependency key 的具体格式 → HOW；「identity = semantic + dependency」是命题。

**证据**：HOW.md EPI-006 行（bayes `same_semantic_evidence_from_independent_dependency_groups_is_preserved_twice`、
`same_dependency_group_is_not_counted_as_independent_evidence_twice`）。

## EPI-007：RootContract 保留分布，可随新语义观测更新

**规范陈述**：`QuestionForm` 不做 argmax 硬分类。Kernel 保留完整 form belief，并线性派生
AnswerContract belief；Facets 独立多标签参与方法适用度。该 belief 是 `Q_t(Form)` 而不是开局
常量：后续 Investigation 可携带 control-only `semanticAssessment`，Kernel 重算 RootContract 并
重新激活 generator；这类控制更新仍不得新增 Evidence 或改变 posterior。方法库随状态递归生长：
任何 Investigation 吸收新认识后 Kernel 都把 `NeedsGeneration` 置真，在下一次动作裁决前重新发
`GenerateCandidatesRequest`。

**含义/动机**：硬标签让 0.51/0.49 与 0.99/0.01 变成同一个控制状态；bind-once 丢掉「原来用户
真正想修复」的语义证据。

**边界**：form/facet 的具体权重与派生产物（当前 `deriveRootContract`）是 HOW；「保留分布 +
可更新 + 不增证据」是命题。

**证据**：HOW.md EPI-007 行（kernel `contract_keeps_distribution_after_semantic_assessment`、
semantics `later_semantic_assessment_updates_control_belief_without_creating_evidence`、
methodology `why_question_activates_multiple_generators_from_distribution_and_facets`）。

## EPI-008：action value 相对根问题；gateway value 进入比较

**规范陈述**：认知动作的比较量必须相对根问题。当前控制近似：

```text
ΔV(a) = dependencyDiscount × (ExpectedRootGain + 0.65 × GatewayGain) − Cost
U(stop) = − CurrentAnswerLoss
U(a)    = U(stop) + ΔV(a)
```

`ExpectedRootGain` / `GatewayGain` 是控制层估计，不是对象层置信度。gateway question 即使一步
信息增量小，只要能打开后续高价值动作，仍可被选中。Stop 与其它动作处于同一比较空间；
`U(stop) ≥ max U(a)` 或预算耗尽时停止。Synthesis 也是 CognitiveAction，不拥有特殊终止权。

**含义/动机**：核心问题永远是「如果我知道这个问题的答案，它预计会怎样改变根问题」——这
决定系统是「解惑机」而不是「无限知识展开机」。

**边界**：数值系数（0.65、`CurrentAnswerLoss` 的具体形状）是当前实现（HOW）；「root-relative
+ gateway + 同空间比较」是命题。

**证据**：HOW.md EPI-008 行（semantics `gateway_gain_can_make_low_immediate_gain_question_worth_asking`、
kernel `closure_is_idempotent_at_fixed_point`）。

## EPI-009：概率只接受合格数值证据

**规范陈述**：正式 Bayesian posterior 只有同时满足下列条件才存在：

1. 至少两个显式 Hypothesis；
2. Evidence 明示 `numericQualified = true`；
3. likelihood 覆盖全部 hypothesis key；
4. 每个 likelihood 为有限 `[0,1]` 数；
5. Evidence 有明确 `DependencyKey`。

同一 `DependencyKey` 的多个 Evidence 不得按独立因子重复相乘。Kernel 先过滤不合格 Evidence，
再在每个依赖组内选一个规范代表进入 likelihood product；不合格同源记录不得遮住合格记录。无
合格因子 → `Bayesian = None`，不得用 LLM 猜测补 posterior；Judgment/Credence 答案必须显式
携带 `numeric-credence-unqualified`。

**含义/动机**：LLM 说「我觉得 0.8」不是 likelihood model。宁可给 qualitative/uncertain
answer，也不生成伪精确数值。

**边界**：prior 选择与 normalization 的具体算法 → HOW（`Bayes.update`）；「资格门 +
dependency 组内单代表」是命题。

**证据**：HOW.md EPI-009 行（bayes `bayesian_posterior_requires_explicit_numeric_qualification`、
`qualified_independent_evidence_updates_posterior`、`unqualified_item_cannot_mask_qualified_evidence_from_same_dependency_group`）。

## EPI-010：经典算法是可验证退化求解器

**规范陈述**：Sphinx ontology 不等于 A* / Bayes / MCTS；但约束收紧时必须能得到标准算法行为。

- A*：确定图、非负 cost、固定 goal/heuristic → 按 `g+h` 展开，维护 best-g；closed 节点发现
  更低 g 时 reopen。
- Bayes：固定 hypotheses、关闭生成、只吸收合格 likelihood evidence → 标准归一化 posterior。
- MCTS：给定可展开模型与 terminal reward → selection / expansion / rollout / backup；同
  semantic node key 共享统计，即 graph-MCTS transposition。

这些 solver 不得把自身缓存、visit count、frontier 或 heuristic 冒充认识证据。

**含义/动机**：经典算法的价值在于强可验证子模型；通过退化测试能证明母模型没有被错误抽象
设计窄。

**边界**：solver 的具体实现（`Search.solveGraph`、`MonteCarlo.run`）是 HOW；「必须是真退化 +
统计不冒充证据」是命题。算法名本身不是 ontology。

**证据**：HOW.md EPI-010 行（search `graph_astar_degenerates_to_standard_g_plus_h_shortest_path`、
`graph_astar_reopens_closed_node_when_better_g_is_discovered`、`graph_astar_rejects_negative_cost_graph`；
mcts `mcts_selection_expansion_rollout_backup_prefers_high_value_branch`、
`graph_mcts_shares_transposition_statistics_by_semantic_node_key`、`uct_for_unvisited_node_is_infinite`）。

## EPI-011：等价约简 dependency-aware；wire 无判重权

**规范陈述**：动作只有两种情况允许进入同一表示等价类：

1. Kernel 的确定性 canonicalization / representation rewrite 明确写入内部 `EquivalenceKey`；
   或
2. semantic key 与 dependency key 同时相同。

LLM/wire Candidate 的 `equivalenceKey` 不具判重权（当前 codec 直接忽略）；无法由 Kernel 证明
等价时宁可多保留，也不误合并。相同 semantic+dependency 的 Candidate 是同一 Kernel identity：
重复命中时可保留控制价值更好的代表，但必须合并 provenance，不能把「另一个方法也命中」这条
来源信息抹掉。相同问题若来自不同独立 dependency group，不得判重。等价类内仅当候选在
ExpectedRootGain、GatewayGain、value、provenance 均不差且 cost 不高，并至少一维严格更优时才
支配另一候选；不可比较者保留 Pareto frontier。

**含义/动机**：文本相同不代表未来决策等价；把判重权交给语义 oracle 会删除 source
triangulation 的独立来源价值。

**边界**：等价类的具体 class key 格式（当前 `classKey`）是 HOW；「只有 Kernel-owned 或
semantic+dependency 同一才可判重」是命题。

**证据**：HOW.md EPI-011 行（represent `wire_equivalence_hint_cannot_force_kernel_merge`、
`same_kernel_identity_merges_candidate_provenance_instead_of_erasing_it`、
`same_question_from_independent_dependency_groups_is_not_false_deduplicated`、
`pareto_incomparable_equivalent_representations_both_survive`）。

## EPI-012：closure 幂等且全局；重复纯计算不增证据

**规范陈述**：每个被接受的 Observation 后执行：absorb → deterministic inference → probability
qualification/propagation → root-relative revalue → equivalence + Pareto reduction → solver
projections → repeat → fixed point。只有 `Closure(S) = S` 后才能 yield 或 answered。Closure
必须幂等：`close(close(S)) = close(S)`；重复纯计算不得凭空制造 Evidence、独立依赖组或
posterior 质量。

**含义/动机**：一次外生信息必须被全局消化后才允许下一次交互；closure 是「多知道一点」的
唯一边界，幂等保证重复计算不伪装成新信息。

**边界**：closure 的同步顺序与循环上限（当前最多 16 轮 guard）是 HOW；「closure 幂等 +
纯计算不增证据」是命题。

**证据**：HOW.md EPI-012 行（kernel `closure_is_idempotent_at_fixed_point`、semantics
`synthesis_is_information_propagation_not_information_acquisition`）。

## EPI-013：MCP affordance 面忠实翻译 Kernel continuation

**规范陈述**：每个 pending Request 类型恰好对应一个 phase tool（SemanticAssessment↔assess、
GenerateCandidates↔propose、Investigate↔investigate、Synthesize↔synthesize）；每个结果携带由
kernel-decided pending Request 翻译而来的 `nextTool`——MCP 层从不自行判断 phase、action key
或 observation 合法性（`Policy.resume` / `observationMatches` 仍是唯一裁判）。成功使用
structuredContent，失败使用 isError + typed error code
（QUESTION_REQUIRED/MISSING_HANDLE/UNKNOWN_HANDLE/INVALID_OBSERVATION/KERNEL_REJECTED/ALREADY_ANSWERED），
携带 recoverable/retryable/nextAction 以及 pre-failure revision/expectedTool。answered 与
cancelled 的 handle 终止，拒绝后续 observation；handle 是 process-local，重启后旧 handle →
UNKNOWN_HANDLE 是当前明确接受的边界（persistence 是独立的未来需求）。

**含义/动机**：affordance 面是 Kernel continuation 的机械投影，不是第二个决策者。若 MCP 层
自行判断 phase，调用方就能绕过 controller-owned continuation 提交未被调度的观测。

**边界**：wire 形状细节与 tool description 文案是 HOW；legacy `resume` 保留为兼容工具但不在
推荐面。

**证据**：HOW.md EPI-013 行（mcp-handle `mcp_server_surface_exposes_phase_tools_and_legacy_resume`、
mcp-contract、mcp-stdio）。

## EPI-014：MCP server 身份元数据与 shipped manifest 一致

**规范陈述**：initialize 的 `serverInfo.name = 'sphinx'`，`serverInfo.version` 必须等于 shipped
package.json 的 version（经 `import.meta.url` 定位包根读取，禁止 cwd 探测），server 携带
kernel-controlled 使用 instructions。

**含义/动机**：server 身份是可验证事实，不是运行时猜测。若 version 来自 cwd 探测或硬编码，
部署环境与 shipped manifest 可能不一致，使版本追踪失去意义。

**边界**：instructions 文案是 HOW。

**证据**：HOW.md EPI-014 行（mcp-stdio initialize serverInfo.version）。
