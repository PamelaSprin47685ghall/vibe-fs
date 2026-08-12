# Sphinx — 目标算法与协议

实现 SPHINX-001..005；Host 装配见 AGENT-030。

## Inquiry 主环

```text
start(question)
  → 分配 handle
  → 初始化 EpistemicState（Root Question = question）
  → Closure（空观测基线）
  → 首步必选：YIELD SemanticAssessmentRequest
  → 返回 { handle, status=yield, request }

resume(handle, observation)
  → 查 Map；缺失 → { status=error }
  → absorb observation
  → GLOBAL CLOSURE 至 fixed point
  → Policy：比较 V(a) 与 V_stop
  → 若 Stop 最优 → Kernel 写 Canonical Answer → { status=answered, answer }
  → 若需外生语义 → YIELD structured request → { status=yield, request }
  → 若纯计算动作 → Kernel 执行后再次 Closure，不经 LLM
```

LLM 不持有 continuation。每次 resume 必须回传同一 `handle`。

## Global Closure（Core Reduction only）

每次外生信息后固定：

```text
absorb
→ infer
→ reduce          // semantic-key novelty/dedup + simple dominance
→ revalue         // root-relative V(a), V_stop
→ repeat
→ fixed point
```

达到 `S* = Φ(S*)` 才允许下一认知动作或 answered。

Phase 0 不做 Representation Optimization（pivot / e-graph / Blackwell / SCC rewrite）。

## EpistemicState

最小表示：

```text
S = (R, B, E, D, A, C)
```

`R` 由 SemanticAssessment 等观测经 Kernel 派生为 RootContract belief，不是硬标签。  
QuestionForm / Facets 是控制层语义不确定性；对象层命题概率分列，不得混槽。

## GenerativeRule 与方法库 V1

统一接口：

```text
G_i(S) → { Candidate Cognitive Actions }
```

方法库：

```text
Multidisciplinary | Abduction | Analogy | Counterexample | Synthesis
```

前四个扩展认知动作空间；Synthesis 组织已有结构为答案结构。规则无调度权；方法选择由 Kernel 计分，可同帧触发多个高分方法。

## Root-relative value 与 Stop

```text
V(a | S, R) = expected improvement of root answer − cost
```

Phase 0 允许粗糙启发式。比较权属 Kernel。

Stop 是普通动作：`V_stop(S) ≥ max_a V(a)` → 停止最优 → 写 Canonical Answer。

## MCP JSON 形

`start` / `resume` 成功体必须含 `handle` 与 `status`。  
`status=yield` 必含 `request`；`status=answered` 必含 `answer`；`status=error` 不得带伪 answer。

observation 在边界收敛为强类型 / 结构化对象后再入 Kernel；禁止下游靠正则猜自由文。

## Phase 0 明确不做

完整 A* / 通用 Bayesian Network、MCTS、跨进程 durable journal、高级方法库扩张。

## Phase 1 — 搜索增强

Kernel `search.js`：

```text
PriorityQueue           → graph best-first 队列
syncSearchFrontier      → semantic-key 判重 + bestG + closed + reopenCount
reopenOnBeliefShift     → evidenceMass 变化 > ε 时清空 closed（incremental reopen）
orderActionsByFrontier  → EstimateValueRequest 按 f 降序
ExpandFrontierRequest   → exploreSteps < maxExploreSteps 时定向展开 frontier head
anytimeAnswer           → yield 附带当前 bestAnswer 投影
graphAstarExpandOrder   → g+h 排序；可构造严格 graph A* 退化 case
```

`f(action) = V(a) − 0.1 × cost`；`rootInformationGain` 来自 `value.js` 启发式。

Phase 1 验收：`tests/unit/sphinx/search.test.mjs`。

## Phase 2 — 概率增强

Kernel `bayes.js`：

```text
uniformPrior / updatePosteriors → explicit hypotheses + prior/likelihood/posterior
syncBayesianBelief              → Closure 内折叠 factor representation
bayesRisk / expectedValueOfInformation → 进入 actionValue
Evidence observation            → supports/refutes 触发贝叶斯更新
frozenBayesianInference         → 冻结生成后的纯 Bayesian 退化 case
```

Phase 2 验收：`tests/unit/sphinx/bayes.test.mjs`。

## Phase 3 — Monte Carlo 增强

Kernel `mcts.js`：

```text
uctScore / puctScore      → UCT/PUCT 选择
backupMctsValue           → visit count + value backup
syncMcts                  → transposition node + sampled rollout value
degenerateMctsSelection   → 可构造 MCTS 退化 case
```

Phase 3 验收：`tests/unit/sphinx/mcts.test.mjs`。

## Phase 4 — Representation Optimizer

Kernel `represent.js`：

```text
groupEquivalenceClasses / paretoRepresentative → equivalence class + Pareto 代表元
optimizeRepresentation                         → epistemic pivot + factor ordering
contractRepresentation                         → cost-based extraction 入口
```

Closure 顺序：`reduce → optimizeRepresentation → syncBayesianBelief → revalue → search → mcts`。

Phase 4 验收：`tests/unit/sphinx/represent.test.mjs`。

## Phase 5 — Methodology Library

`rules.js` 在 V1 五方法之外扩展：

```text
CausalMechanism | BaseRate | Dialectic | Falsification | BoundarySearch
```

`allMethods()` / `activateMethods()` / `generateFromRules()` 默认覆盖 V1+V2；`METHODS` 常量仍锁 Phase 0 五方法。

Phase 5 验收：`tests/unit/sphinx/methodology.test.mjs`。
