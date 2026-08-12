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

完整 A* / best-first 搜索器、通用 Bayesian Network、MCTS、跨进程 durable journal、高级方法库扩张。
