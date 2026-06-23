# Methodology Selection Probe PRD

## 背景

`vibe-fs` 已经把历史记录视为唯一真相，消息变换只是从真相投影出的临时上下文。新功能继续遵守这条边界：不 fork session，不 revert session，不发隐藏的第二次 LLM 请求，不把临时探针写入 UI 或数据库。

目标是在每次普通主对话正式回答前，让模型先自选本轮需要使用的思维方法，并把选择结果作为同一轮工具调用事实保留在历史里。工具调用成功时，模型继续回答原问题；工具未调用时，不阻塞正常回答。

## 用户价值

- 让模型在复杂任务前显式选择推理框架，降低随手回答和隐式漂移。
- 把方法选择变成可审计工具事实，而不是不可见内存状态。
- 避免预请求、fork、revert 带来的历史污染、并发风险和副作用补偿。
- 保持现有长会话压缩、review、todo、knowledge graph 投影机制的语义一致性。

## 核心设计

实现只包含两件事：投影函数 + 工具。

### 1. 投影函数

在普通主对话的 `experimental.chat.messages.transform` 中运行。

职责：

- 排除 compaction、title、browser、investigator、executor、bookkeeper 等非主回答上下文。
- 检查当前历史尾部最近一轮 assistant 工具结果。
- 如果最近一轮工具结果已经包含 `select_methodology`，不做任何事。
- 否则在投影末尾追加一条虚拟 user 消息。
- 虚拟 user 消息只存在于本次内存投影，不写入宿主历史、UI、SDK `session.messages`、导出内容。

LLM 可见探针文本：

```text
<system>Before the task, please decide which methodologies are useful for this turn. Now you MUST Call the select_methodology tool with one or more methods. </system>
```

这条消息不是事实，只是本次 provider turn 的临时投影输入。真正事实是后续模型是否调用 `select_methodology` 以及工具参数。

### 2. `select_methodology` 工具

工具名：

```text
select_methodology
```

Schema：

```ts
{
  methods: array<enum>,
  reason: string
}
```

工具描述必须用英文，因为这是 LLM 可见内容。

工具 description 必须完整包含方法 catalog，禁止只给 enum 名称让模型望文生义。

建议 description：

```text
Select the reasoning methodologies that should guide the next work step. Use this before continuing when the task benefits from explicit structure, search-space control, proof discipline, design reasoning, decomposition, verification, or risk control. Choose all useful methodologies by their definitions, not by keyword vibes, and write a concise reason.

Methodology catalog:
Common methods can be selected with their short definitions. Uncommon methods include trigger conditions; do not avoid them merely because they are less familiar.

- first_principles: reduce the problem to irreducible facts and rebuild from them.
- deduction: derive necessary conclusions from accepted premises.
- induction: infer a general rule from repeated cases or patterns.
- analogy: transfer a known solution structure from a genuinely similar problem.
- specialization: inspect simple, concrete, boundary, or extreme cases before generalizing.
- generalization: widen the problem to expose the underlying structure.
- working_backwards: start from the desired end state and derive prerequisites.
- decomposition_recombination: split the object into parts and reconnect them in a better structure.
- constructive_method: build the required object, algorithm, or witness directly.
- reductio_ad_absurdum: assume the negation and derive a contradiction.
- search_space_exploration: model candidates as a space or graph and choose a traversal strategy.
- branch_and_bound: prune impossible or dominated branches using bounds.
- dynamic_programming: exploit overlapping subproblems and memoized state transitions.
- systems_thinking: model feedback loops, dependencies, delays, and emergent behavior.
- root_cause_analysis: trace symptoms back to the causal fault, not the visible failure.
- state_machine_reasoning: enumerate legal states, transitions, and impossible states.
- type_driven_design: encode domain boundaries and illegal states in types.
- event_sourcing: separate commands from facts and derive current state from event history.
- bayesian_update: update belief strength as evidence arrives.
- simplification: remove accidental complexity until the essential problem remains.
- tradeoff_analysis: compare options across explicit constraints and costs.
- risk_analysis: identify failure modes, blast radius, and irreversible decisions.
- test_driven_reasoning: make expected behavior executable before or during implementation.
- debugging_trace: reproduce, isolate, instrument, and verify the fault chain.
- security_review: reason adversarially about trust boundaries and abuse paths.
- performance_analysis: locate bottlenecks, asymptotics, and resource constraints.
- user_intent_clarification: resolve ambiguous goals before optimizing the wrong target.

- axiomatization: use when hidden assumptions or drifting definitions make reasoning unstable. State primitive terms, allowed operations, invariants, forbidden states, and derivation rules explicitly; then solve only inside that declared system.
- abduction: use when debugging, diagnosing, investigating, or explaining surprising evidence. Generate the best causal hypothesis: if X were true, observed Y would be expected; then seek discriminating tests rather than treating the hypothesis as proven.
- analysis_synthesis: use when a goal is clear but the path is not. First analyze backward from the desired result until reaching known facts or constructible conditions; then synthesize forward into the actual proof, algorithm, or implementation plan.
- auxiliary_construction: use when known facts and unknown target have no direct bridge. Introduce a helper line, variable, function, data structure, invariant, lemma, adapter, or intermediate representation that exposes a hidden relation.
- equivalent_transformation: use when the current representation is noisy. Convert the problem into an equivalent form, such as algebra to geometry, state updates to events, recursion to graph search, or protocol text to typed algebra.
- model_problem_transfer: use when the problem resembles a known canonical template. Identify the shared unknowns, constraints, and topology, then transfer the solution skeleton while checking which assumptions fail.
- invariance: use when a system undergoes operations, rewrites, moves, or state transitions. Find what cannot change, such as parity, ordering, conservation, type identity, ownership, permission boundary, or event prefix integrity.
- symmetry_analysis: use when many cases appear distinct but may be equivalent. Exploit symmetry to collapse cases; inspect symmetry breaking when bugs or edge cases arise from one side no longer matching the other.
- dimensional_reduction: use when the full state space is too large. Project to a lower-dimensional view, summary statistic, slice, trace, minimal reproduction, or quotient; reason there, then lift the conclusion back cautiously.
- perturbation_continuity: use when a hard case is near an easy case. Change one variable slightly, vary load, remove one constraint, or move along a continuous path to see which properties survive and where the phase change occurs.
- pigeonhole_principle: use when exact placement is unknown but counts, capacities, or partitions are known. Prove that collision, overflow, repetition, or coverage must occur because there are more items than distinguishable slots.
- duality: use when the direct problem is hard but a mirrored constraint or optimization problem is easier. Solve the shadow problem: producer/consumer, read/write, syntax/semantics, primal/dual, local/global, command/event.
- quotient_space: use when too many objects differ only in irrelevant detail. Define an equivalence relation, collapse each class to one representative, solve on classes, then map results back to concrete cases.
- category_mapping: use when structure matters more than object internals. Preserve relationships and transformations while moving the problem into a domain with stronger tools, such as graphs, types, events, algebra, or state machines.
- relaxation: use when hard constraints make direct search impossible. Temporarily weaken integer, ordering, permission, timing, or exactness constraints; solve the easier superset; then project, round, or validate back under real constraints.
- monte_carlo_sampling: use when the space is too large for exhaustive reasoning and approximate confidence is acceptable. Sample many plausible paths or cases, look for stable signals, then verify critical findings deterministically.
- simulated_annealing: use when greedy search gets trapped in local optima. Early exploration may accept worse intermediate states to escape traps; later narrow the search to refine the best candidate.
- swarm_optimization: use when multiple candidate directions can search in parallel. Let independent hypotheses, agents, examples, or solution drafts explore, share best findings, and converge without central overcommitment too early.
- operationalism: use when terms are vague or metaphysical. Define each concept by the observable operation that detects, measures, or changes it; discard distinctions that create no behavioral difference.
- falsification: use when claims risk becoming unfalsifiable stories. Formulate a hypothesis with clear failure conditions, actively search for counterexamples, and keep it only while it survives severe tests.
- thought_experiment: use when real execution is costly, dangerous, or impossible. Push an idealized or extreme scenario through the rules to test concept boundaries, contradictions, and hidden assumptions.
- transcendental_argument: use when an accepted fact is undeniable but its preconditions are hidden. Ask what structures must already exist for this fact, behavior, or experience to be possible at all.
- conceptual_analysis: use when confusion may come from language, not facts. Clarify meanings, category boundaries, and scope; remove category mistakes such as treating a process, relation, or aggregate as a separate concrete object.
- dialectical_analysis: use when opposing forces both shape the outcome. Identify thesis, antithesis, tension, dependency, and resolution path instead of flattening the system into one-sided causality.
- hermeneutic_circle: use when local meaning depends on global context and global meaning depends on local details. Iterate between part and whole until both stabilize, especially for codebase understanding and document interpretation.
- deconstruction: use when a design or text relies on hidden binaries or authority. Inspect what the framing excludes, which hierarchy it assumes, and where internal tensions make the apparent center unstable.
- renormalization: use when micro-detail overwhelms macro behavior. Coarse-grain details, preserve only scale-relevant variables, and identify the universal structure that remains stable across levels.
```

`methods` enum values：

```text
first_principles
axiomatization
deduction
induction
abduction
analogy
specialization
generalization
working_backwards
analysis_synthesis
auxiliary_construction
equivalent_transformation
decomposition_recombination
model_problem_transfer
constructive_method
reductio_ad_absurdum
invariance
symmetry_analysis
dimensional_reduction
perturbation_continuity
pigeonhole_principle
duality
quotient_space
category_mapping
relaxation
search_space_exploration
branch_and_bound
dynamic_programming
monte_carlo_sampling
simulated_annealing
swarm_optimization
systems_thinking
root_cause_analysis
state_machine_reasoning
type_driven_design
event_sourcing
operationalism
bayesian_update
falsification
thought_experiment
transcendental_argument
conceptual_analysis
dialectical_analysis
hermeneutic_circle
deconstruction
renormalization
simplification
tradeoff_analysis
risk_analysis
test_driven_reasoning
debugging_trace
security_review
performance_analysis
user_intent_clarification
```

工具返回固定英文文本：

```text
Continue using the selected methodologies.
```

工具不执行外部副作用，不读写文件，不发网络，不改 session，不触发子代理。它只把模型选择从自然语言变成强结构工具事实。

## 状态机

按当前投影历史判断，不依赖进程内 store。

```text
普通主对话投影
  ↓
最近 assistant 工具结果包含 select_methodology ?
  ├─ yes → 不追加探针 → 模型继续回答
  └─ no  → 追加虚拟 user 探针
          ↓
        模型可选择调用 select_methodology
          ├─ called     → 工具结果写入历史 → 下一 step 不再追加探针
          └─ not called → 正常回答，不阻塞
```

关键点：不需要删除虚拟 user。它从未入库，下一 step 会从历史重新投影，自然不存在。只需要根据真实 tool result 抑制重复追加。

## 历史语义

历史中允许出现：

- assistant tool call：`select_methodology({ methods, reason })`
- tool result：`Continue using the selected methodologies.`
- assistant 后续正式回答

历史中不应出现：

- 虚拟 user 探针文本
- 单独的预请求 user message
- fork session
- revert marker
- preflight-only assistant answer

因此历史仍是唯一真相：模型选择了什么方法是事实；触发模型选择的方法探针只是投影规则。

## Compaction 排除

当前仓库已有排除 compaction 的方法。本功能必须复用同一类判断，不能让压缩上下文收到 `select_methodology` 探针。

验收条件：

- compaction agent 的投影不追加 methodology probe。
- compaction 历史不生成 `select_methodology` tool call。
- 普通主 agent 的投影仍正常追加 probe。

## 非目标

- 不强制模型一定调用工具。
- 不改 OpenCode core 来设置 `toolChoice: required`。
- 不创建隐藏 session。
- 不通过 revert、delete message、cleanup 做补偿事务。
- 不把 methodology reason 写入独立存储。
- 不影响 title、summary、bookkeeper、reviewer、subagent 的专用上下文。

## 失败策略

模型不调用工具：正常回答。

模型调用失败：宿主按普通工具失败处理，主流程不应额外中断。

模型重复调用：投影函数只保证下一 step 不重复追加探针；同一 step 内模型自己重复调用属于模型行为，不由插件阻断。

未知宿主消息形态：保守不追加 probe，避免污染非主上下文。

## 验收标准

- 普通主对话第一次投影会追加一条虚拟 user probe。
- 虚拟 user probe 不出现在 session history、UI、导出、SDK messages 中。
- 模型调用 `select_methodology` 后，下一次投影不会再追加 probe。
- `select_methodology` 参数包含 `methods: string[]` 和 `reason: string`。
- `methods` 只允许枚举值。
- 工具返回固定文本 `Continue using the selected methodologies.`。
- compaction/title/subagent/bookkeeper 等上下文不收到 probe。
- 不需要 fork/revert/delete message 即可完成一次 methodology selection flow。

## 实现落点

优先在本仓库实现。

- Kernel：定义 methodology enum、probe 文本、工具返回文本、历史检测纯函数。
- Opencode：在 `src/Opencode/MessageTransform.fs` 接入投影追加与 compaction 排除；注册 `select_methodology` 工具。
- Mux：如宿主支持同等 messages transform 与工具注册，再做同语义接线。
- Tests：覆盖普通投影追加、已存在 tool result 时不追加、compaction 排除、工具 schema 与固定返回。

## 最小测试矩阵

1. 空普通会话：追加 probe。
2. 最后一轮 assistant 含完成的 `select_methodology` tool part：不追加 probe。
3. 最后一轮 assistant 含其他 tool part：仍追加 probe。
4. compaction agent：不追加 probe。
5. title agent：不追加 probe。
6. `select_methodology` 执行：返回固定英文文本。
7. schema：`methods` 是 enum array，`reason` 是 string。
