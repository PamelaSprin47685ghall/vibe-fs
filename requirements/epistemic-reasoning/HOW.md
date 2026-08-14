# epistemic-reasoning — HOW

> 非 normative。描述当前实现模型与约束，以及「历史与弃权」裁决。
> 当前实现名（Sphinx、A*/Bayes/MCTS、MCP start/resume、handle、F# 文件布局）全部是 HOW，
> 不是 WHAT。若未来换实现，WHAT.md 不变。源：`archive/docs/how/sphinx.md`、`archive/docs/shape/sphinx.md`、
> `archive/changes/completed/Sphinx.md`、`src/Wanxiangshu/Sphinx/*.fs`。

## 1. 模块地图（当前实现）

```text
src/Wanxiangshu/Sphinx/
  Types.fs               QuestionForm / AnswerContract / ActionKind / EvidenceKind /
                         SemanticAssessment / RootContract / EvidenceSource / Evidence /
                         Finding / Hypothesis / CandidateProposal / CognitiveAction（领域语义类型）
  RuntimeTypes.fs        Request / Observation / EpistemicState / solver 运行态类型
  State.fs               normalizeDistribution / deriveRootContract（不取 argmax）/ create /
                         withYield / clearPending
  Methodology.fs         library（核心五方法 + 扩展库）/ phase0Names / utility（form/facet 加权）
  Bayes.fs               update：资格门 → 过滤 → groupBy DependencyKey → 组内单代表 → posterior
  Search.fs              solveGraph：strict graph A*（g+h、bestG、reopen、负边拒绝）+ epistemic frontier
  MonteCarlo.fs          run / uct / puct：selection / expansion / rollout / backup；semantic transposition
  Representation.fs      classKey（Kernel EquivalenceKey 或 semantic|dependency）/ dominates /
                         paretoFrontier / optimize
  Value.fs               currentAnswerLoss / stopUtility / dependencyDiscount / actionDelta（root-relative）
  Absorb.fs               typed Observation → EpistemicState（Candidates→Action、Investigation→
                         Finding/Evidence、wire confidence 丢弃）
  Closure.fs             ensureSynthesisAction → synchronize → fixed point；close(close(S))=close(S)
  Policy.fs              observationMatches（PendingRequest 门）/ canonicalAnswer / decide
  DecodePrimitives.fs    raw JS shape primitives（wire 边界唯一 duck type 点）
  ObservationCodec.fs    raw observation → strong Observation
  WireEncode.fs          Request / Canonical Answer → MCP wire object
  Codec.fs               public wire façade（只委托，不实现业务）
  Session.fs             SessionStore：handle → EpistemicState（进程内；UUID；唯一 handle 索引）
  McpServer.fs           MCP SDK / zod / stdio 唯一 owner；注册 start / resume
```

Fable 编译 `Wanxiangshu.fsproj` 的 `Sphinx/*.fs` → `dist/Sphinx/*.js`。生产 MCP 入口唯一为
`dist/Sphinx/McpServer.js`；`scripts/build.mjs` 只验证该产物存在，不 copy 第二套源码。

## 2. 主循环

```text
start(question)
  → create immutable EpistemicState
  → Policy.decide → YIELD SemanticAssessmentRequest → SessionStore alloc opaque handle

resume(handle, rawObservation)
  → SessionStore lookup（缺失/未知 handle → error，不建隐式会话）
  → Codec.decodeObservation
  → Policy 校验 PendingRequest ↔ Observation（错型/错 actionKey → error，状态不前进）
  → Closure.absorbAndClose → fixed point
  → Policy.decide → YIELD next Request | ANSWER CanonicalAnswer
```

## 3. RootContract 与方法生成

- `State.deriveRootContract` 不取 argmax：每个 QuestionForm 概率质量映射到对应 AnswerContract
  后求和再归一（Why→Explanation、How→Plan、What/Who/Where/When→Direct、Which→Ranking、
  Polar→Judgment、Other→Credence）。Why=.55, How=.35, Polar=.10 同时保留三个 contract 质量。
- Investigation 可返回可选 `semanticAssessment`：Absorb 只更新控制层 RootContract 并把
  `NeedsGeneration` 置真；Evidence / posterior 不因此增加。
- `Methodology.utility` = 0.58 × form 适用度 + 0.42 × facet 适用度 + synthesis readiness
  − saturation penalty − 0.08 × base cost；阈值只决定哪些 generator 激活。Kernel 随后发
  `GenerateCandidatesRequest(methods, root)`；LLM 只生成 CandidateProposal
  （question / semanticKey / 可选 dependency / ExpectedRootGain / GatewayGain / Cost /
  provenance）。wire 不接受可支配判重的 equivalence assertion。
- CandidateProposal 进入 `Actions`，不进入 Findings/Evidence/Hypotheses。任何 Investigation
  后 `NeedsGeneration=true` → 下一次裁决前重跑 generator（方法库递归生长，不是开局一次）。

## 4. absorb 规则（每类 Observation）

- **SemanticAssessment**：只写 RootContract；不写 Evidence。
- **Candidates**：每个 proposal 转 `ActionKind.Investigate`，Id = normalized SemanticKey + "|"
  + DependencyKey-or-independent，Value=0、Status=Open。同 id 重复保留控制价值更高的代表但
  `Provenance = distinct(old @ new)`；仍不写 Evidence/Dependencies。
- **Investigation**：① 所选 action 标 Resolved 并扣实际 cost；② 可选 semanticAssessment 只重算
  RootContract；③ Findings 以 semantic key 合并 supports/refutes/evidenceKeys/provenance，
  wire `confidence` 一律丢弃；④ Evidence 必须携 Source + DependencyKey（storage key =
  normalized semantic key + dependency key；同 semantic+dependency 重复只合并 provenance，
  同 semantic 不同 dependency 并存）；⑤ Hypotheses 以 semantic key 去重；⑥ 新
  CandidateProposal 递归回同一 action pool，`NeedsGeneration=true`；⑦ 已有 Synthesis 被任何
  新 Investigation 失效，Closure 可重新生成。
- **Synthesis**：只保存 text + 已存在 finding keys + uncertainties；未知 finding key 被过滤；
  不写 Evidence、不写 posterior。

## 5. Global Closure

每次 absorb 后：`ensure Synthesis action → Bayes.update → Value.revalueActions →
Representation.optimize → Value.revalueActions → Search.syncEpistemicFrontier（BestFirst 模式）
→ MonteCarlo.syncEpistemicNodes（MonteCarlo 模式）→ repeat until structural equality`。
最多 16 轮是实现 guard；`close(close(S)) = close(S)` 是 proof 契约（EPI-012）。

## 6. Bayesian qualification（当前算法）

`Bayes.update`：`hypotheses < 2 → None`；qualified evidence 要求 `NumericQualified`、
likelihood 数 = hypothesis 数、keys 恰好覆盖 hypothesis set、每值有限且 0≤p≤1。先过滤
qualified → groupBy DependencyKey → 每组按 SemanticKey 取一个规范代表。无合格组 → None。
prior = 全部显式 prior 完整时用之，否则 uniform；`posterior ∝ prior × ∏group likelihood`；
risk = 1 − max posterior；entropy = −Σ p log2 p。同 DependencyKey 两种重述不重复相乘（保守
独立性边界；更复杂依赖模型必须显式升级表示）。

## 7. Root-relative value 与 Stop（当前系数）

```text
CurrentAnswerLoss: 无 RootContract → 1；非概率根 → grounded finding coverage × synthesis factor；
  Judgment/Credence → Bayesian risk；无合格 posterior 时用高残余 loss
ΔV(Investigate) = dependencyDiscount × (ExpectedRootGain + 0.65 × GatewayGain) − Cost
ΔV(Synthesize)  = synthesis readiness − Cost
U(stop) = −CurrentAnswerLoss；U(action) = U(stop) + ΔV(action)
```

`Value.bestOpenAction` 与 `stopDominates` 统一决定继续/停止；Budget 用 yields + cost 双界限，
耗尽直接 answered。DependencyKey 已有真实事实时，相同依赖动作折损；候选本身不触发折损。

## 8. Representation / A* / MCTS 退化面（当前实现）

- **class key**：Kernel-owned EquivalenceKey 存在 → `eq:<key>`；否则 →
  `semantic:<SemanticKey>|dependency:<DependencyKey-or-independent>`。`CandidateProposal` 没有
  EquivalenceKey 字段；wire 即使发送 `equivalenceKey` 也被 codec 忽略。等价类内逐维 dominance
  （ExpectedRootGain、GatewayGain、Value、provenance ≥，Cost ≤，至少一项严格）；不可比较 → Pareto。
- **Strict A***：`Search.solveGraph` 接受固定 Start/Goal/Edges/Heuristic；负边拒绝；OPEN 按
  (g+h, g, node) 升序；bestG 保存已知最低路径成本；发现更低 g → 更新 parent + 从 CLOSED
  移除 → reopen。Generic Sphinx 的 `epistemicPriority = action.Value / cost` 与 A* 的 `g+h`
  明确分开，禁止互相借名。
- **Graph-MCTS**：`MonteCarlo.run`：selection 用 PUCT（未访问 child = +∞）、expansion 取首个
  未访问 child、rollout 沿 prior 选到 terminal、backup 共享 semantic node stats。Node map 以
  semantic key 为 identity；多 parent 同 key → 同一 visit/valueSum（transposition）。统计不进
  Evidence。

## 9. Canonical Answer

Kernel 输出：question / contract / EpistemicBasis（findings / evidence / hypotheses）/
synthesis?（presentation projection）/ bayesian?（仅 qualified）/ uncertainties / stopReason /
revision。Judgment/Credence 且无合格 posterior → `numeric-credence-unqualified`。Canonical
Answer 不复制 transcript，也不把 candidate/value estimate/method score 当认识事实。

## 10. 依赖（DEPENDS ON，逐条理由）

来自 `requirements-design/INDEX.md` 依赖骨架（不增删 edge）：

- `participant-horizon`：只有会改变合法行动的最小事实应穿过 horizon——investigation 得到的新
  世界事实经 evidence-acquisition contract 注入为 observation。具体是 repository、external 还是
  其它来源，不构成 epistemic core 的 hard dependency（HANDOFF §8：删除了对 acquisition
  package 的假依赖）。

## 11. 历史与弃权（考古记录，非 normative）

- **算法/组件降为 HOW**：Sphinx 名、A*/Bayes/MCTS 实现、MCP `start`/`resume` wire protocol、
  handle/SessionStore、F# 文件布局、value 系数（0.65、synthesis factor 0.72 等）、闭包 16 轮
  guard、方法库权重（0.58/0.42）——全部当前实现，不进 WHAT（边界卡片 DOES NOT OWN 与
  HANDOFF §6.7 同类裁决：`sphinx` 组件名与算法降为 HOW/proof）。
- **`Sphinx-wiki.html`（archive/changes/proposed/）**：算法资料，HOW 参考，非 ontology（CHANGES-AUDIT）。
- **旧完成声明的语义漂移**：`evidenceMass` 伪置信度、primary argmax、bind-once、wire
  equivalenceKey、LLM 自报 confidence、开局一次性生成候选，已在 corrective round
  （archive/changes/completed/Sphinx.md「Corrective outcome — 2026-08-12」）逐条修正；被拒方向归档于
  WHY.md §3。
- **MCP/wire 身份 → `host-boundary`**：MCP server identity、launch config、`sphinx_*` 权限键、
  wire 编码归属 Host 边界；本包只拥有认识语义（EPI-004 的「同型契约」是语义侧）。
- **Inquiry office authority → `office-capability`/`capability-enforcement`**：谁能发起 inquiry、
  谁 allow `sphinx_*` 不是认识论命题。
- **durable journal 弃权**：V1 不做 durable EpistemicState journal——handle 进程内失效即丢弃；
  认识内核是否 durable 是独立实现选择，不由本包命题强制（边界卡片 DOES NOT OWN 末条）。
- **SPHINX-005（F# 单一实现）→ HOW**：编译边界/模块依赖方向是当前实现约束，不是认识论合同；
  其「内核不依赖 Host/Agent/Journal」的信息已吸收进 HOW §1。
