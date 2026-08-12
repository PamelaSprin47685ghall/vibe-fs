# Sphinx — 算法与协议

实现 SPHINX-001..010；Host 装配见 AGENT-030。

## 1. 主循环

```text
start(question)
  → create immutable EpistemicState
  → Policy.decide
  → YIELD SemanticAssessmentRequest
  → SessionStore alloc opaque handle

resume(handle, rawObservation)
  → SessionStore lookup
  → Codec.decodeObservation
  → Policy checks PendingRequest ↔ Observation
  → Closure.absorbAndClose
  → fixed point
  → Policy.decide
  → YIELD next Request | ANSWER CanonicalAnswer
```

错误发生在 handle lookup、wire decode、pending-request matching 任一层 → `status=error`，旧 state 原封不动保留。

## 2. RootContract

SemanticAssessment 只改变控制语义：

```text
Forms: Map<QuestionForm,float>
Facets: Map<string,float>
Targets / Intents
```

`State.normalizeDistribution` 先去负值/非法数再归一。`deriveRootContract` 不取 argmax：每个 QuestionForm 的概率质量映射到对应 AnswerContract 后求和再归一。

```text
Why   → Explanation
How   → Plan
What/Who/Where/When → Direct
Which → Ranking
Polar → Judgment
Other → Credence
```

因此 `Why=.55, How=.35, Polar=.10` 同时保留 Explanation / Plan / Judgment 质量；后续方法选择读取完整分布与 Facets。

## 3. 方法生成

`Methodology.library` 保存方法定义：form weights、facet weights、base cost、是否为 combining operator。

```text
MethodUtility
= .58 × weighted QuestionForm applicability
+ .42 × weighted Facet applicability
+ synthesis readiness
− saturation penalty
− .08 × base cost
```

阈值只决定哪些 generator 激活。Kernel 随后发 `GenerateCandidatesRequest(methods, root)`；LLM 只生成具体 CandidateProposal：question、semantic key、可选 equivalence/dependency、ExpectedRootGain、GatewayGain、Cost、provenance。

CandidateProposal 进入 `Actions`，不进入 Findings/Evidence/Hypotheses。

## 4. Pending Request 契约

```text
SemanticAssessmentRequest → SemanticAssessmentObservation
GenerateCandidatesRequest → CandidatesObservation
InvestigateRequest(a)     → InvestigationObservation(ActionKey=a.Id)
SynthesizeRequest         → SynthesisObservation
```

错型直接拒绝。Investigation 的 `ActionKey` 必须等于 Kernel 选中的 action id，防止调用方偷偷提交未被调度的调查结果。

## 5. absorb

### SemanticAssessment

只写 RootContract；不写 Evidence。

### Candidates

每个 proposal 转成 `ActionKind.Investigate`：

```text
Id = normalized SemanticKey + "|" + DependencyKey-or-independent
Value 初始为 0
Status = Open
```

同 id 重复时仅保留控制价值更高的候选；仍不写 Evidence / Dependencies。

### Investigation

1. 将 selected action 标为 Resolved，并扣实际 action cost。
2. Findings 以 semantic key 合并 supports/refutes/evidenceKeys/provenance；wire `confidence` 一律丢弃，不把 LLM 自报数值写入对象层 belief。
3. Evidence 必须携 Source + DependencyKey；同 semantic key 重复不重复插入。
4. Hypotheses 以 semantic key 去重。
5. 新 CandidateProposal 递归回到同一 action pool。
6. 若已有 Synthesis，任何新 Investigation 都使它失效；Closure 可重新生成 Synthesis action。

### Synthesis

只保存 text + 已存在 finding keys + uncertainties；未知 finding key 被过滤。Synthesis 不写 Evidence、不写 posterior。

## 6. Global Closure

每次 absorb 后：

```text
ensure Synthesis action when applicable
→ Bayes.update
→ Value.revalueActions
→ Representation.optimize
→ Value.revalueActions
→ Search.syncEpistemicFrontier when BestFirst mode
→ MonteCarlo.syncEpistemicNodes when MonteCarlo mode
→ repeat until structural equality
```

最多 16 轮是实现 guard；正确实现通常 1–2 轮稳定。`close(close(S)) = close(S)` 是 proof 契约。

## 7. No Free Information

Dependencies 只由真正吸收的 Evidence 建立。Candidate 声称“未来要查 source:X”不会预先建立 source:X 事实，也不会折损自身价值。

Finding 与 Evidence 分槽：

```text
Finding without referenced Evidence
→ 可留作 claim
→ Canonical Answer uncertainty = ungrounded-finding:<key>
→ 不参与 Bayesian likelihood product
```

Synthesis 是 propagation/computation：前后 `Evidence.Count` 必须相同。

## 8. Bayesian qualification

`Bayes.update`：

```text
if hypotheses < 2 → None
qualified evidence requires:
  NumericQualified
  likelihood count = hypothesis count
  keys exactly cover hypothesis set
  each likelihood finite ∧ 0≤p≤1
先过滤 qualified evidence
→ 再 groupBy DependencyKey
→ 每组按 SemanticKey 取一个规范代表
if no qualified group → None
prior = all explicit priors when complete; otherwise uniform
posterior ∝ prior × ∏group likelihood
risk = 1 − max posterior
entropy = −Σ p log2 p
```

同 DependencyKey 的两种重述不重复相乘。这里是保守独立性边界，不是宣称同源多条观测永远没有额外信息；更复杂依赖模型必须显式升级表示，不能偷偷乘起来。

## 9. Root-relative value 与 Stop

当前控制近似：

```text
CurrentAnswerLoss:
  无 RootContract → 1
  非概率根 → grounded finding coverage × synthesis factor
  Judgment/Credence → Bayesian risk；无 qualified posterior 时用高残余 loss

ΔV(Investigate)
= dependencyDiscount × (ExpectedRootGain + .65 × GatewayGain) − Cost

ΔV(Synthesize)
= synthesis readiness − Cost

U(stop) = −CurrentAnswerLoss
U(action) = U(stop) + ΔV(action)
```

DependencyKey 已有真实事实时，相同依赖动作折损；候选本身不会触发折损。`GatewayGain` 让门户问题可跨过一步 EIG 的短视。

`Value.bestOpenAction` 与 `stopDominates` 统一决定继续/停止。Budget 用 yields + cost 双界限；耗尽直接 answered。

## 10. Representation optimizer

class key：

```text
EquivalenceKey present
→ eq:<key>
else
→ semantic:<SemanticKey>|dependency:<DependencyKey-or-independent>
```

因此跨独立 dependency 的同文本问题不合并。

等价类内逐维 dominance：ExpectedRootGain、GatewayGain、Value、provenance strength 均 ≥，Cost ≤，且至少一项严格。不可比较候选进入 Pareto frontier；RepresentationState 记录 classes / frontiers / representative / estimated future cost。

## 11. Strict A* embedding

`Search.solveGraph` 接受固定 `Start/Goal/Edges/Heuristic`：

```text
negative edge → reject
OPEN 按 (g+h, g, node) 升序
bestG 保存已知最低路径成本
发现更低 g → 更新 parent + 从 CLOSED 移除 → reopen
Goal pop 出 OPEN → reconstruct path
```

这是标准 graph A* 退化面。Generic Sphinx 的 `epistemicPriority = action.Value / cost` 与 A* 的 `g+h` 明确分开，禁止互相借名。

## 12. Graph-MCTS embedding

`MonteCarlo.run`：

```text
selection: PUCT，未访问 child = +∞
expansion: 首个未访问 child
rollout: 沿 prior 选择到 terminal reward
backup: path 上共享 semantic node stats
```

Node map 以 semantic key 为 identity；多个 parent 指向同 key → 同一 visit/valueSum，即 transposition graph-MCTS。该统计不进入 Evidence。

## 13. Canonical Answer

Kernel 输出：

```text
question
contract
EpistemicBasis:
  findings
  evidence
  hypotheses
synthesis?       // presentation projection
bayesian?        // only qualified
uncertainties
stopReason
revision
```

Judgment/Credence 且无合格 posterior → `numeric-credence-unqualified`。Canonical Answer 不复制 transcript，也不把 candidate/value estimate/method score 当认识事实。

## 14. MCP / Fable 边界

`Wanxiangshu.fsproj` 编译 `src/Wanxiangshu/Sphinx/*.fs` → `dist/Sphinx/*.js`。`scripts/build.mjs` 只验证 `dist/Sphinx/McpServer.js` 存在，不 copy 第二套源码。

`McpServer.fs` 是 MCP SDK / zod 唯一 owner。raw JS shape 只存在于 `DecodePrimitives.fs` / `ObservationCodec.fs` wire 边界；`WireEncode.fs` 只负责强类型 Request / Canonical Answer 的输出投影，`Codec.fs` 只是公共 façade。内核其它文件不做 runtime duck typing。
