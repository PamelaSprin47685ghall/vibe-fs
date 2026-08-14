# Optional optimization / Epistemics

## `speculative-investigation`

WHY: 系统可以用更便宜的执行先猜 primary 可能需要哪些只读调查，但 speculation 只有在未被 primary 消费前对 authoritative world 零影响时才安全。

OWNS:
- eligible speculation 只限低风险、可丢弃、无外部副作用的调查机会。
- speculative execution 与 owner participant 保持同 Role/Persona/language；只允许改变 execution binding 与更窄 request capability。
- Candidate prepared ≠ history；只有 primary 的真实 consumption proof 才能 Promotion。
- 未 promoted candidate 不进入 canonical trace/context/companion history。
- promotion 后 candidate 成为真实 causal history，restart 后必须可 replay。
- predictor/control policy 不能用自己干预产生的数据冒充“无干预时 primary 会怎么做”的 label。
- 条件无法证明时 K0/no speculation；optimization 不能成为 correctness dependency。

DOES NOT OWN:
- repository fact acquisition contract。
- participant identity canonical semantics。
- provider projection generic law。
- fallback/retry policy。
- current Strength name、same-role-fast 模型选择、具体 budget/predictor algorithm。

DEPENDS ON: `repository-investigation`, `participant-identity`, `participant-horizon`, `provider-projection`, `semantic-trace`。

PROVIDES: 可安全丢弃、可因消费晋升为真实历史的 speculative investigation guarantee。

FAILURE MEANING: RED = 未被 primary 使用的 speculative intervention 能污染 authoritative history/authority，或优化关闭后产品 correctness 改变。

INDEPENDENT CHANGE: 把 predictor 从当前模型/统计策略换成 deterministic heuristic 或 learned policy，而 Candidate/Promotion/no-impact semantics 不变。

CURRENT EVIDENCE: `docs/why/strength.md`；STRENGTH-001..012；type `Domain/{StrengthBudget,StrengthCostModel,StrengthEvents,StrengthFrame,StrengthPolicy,StrengthPredictor,StrengthPromotion,StrengthProjection,StrengthRollout,StrengthCommit,StrengthBatchCollector}.fs`；wiring `Application/Strength/**`、`Session/StrengthRuntime.fs`；fact `Infrastructure/Persist/{StrengthDurability,StrengthStore}.fs`；tests `tests/unit/strength/**`。

---

## `epistemic-reasoning`

WHY: 重复生成文本不能自动增加知识。一个 reasoning system 必须显式区分 proposal 与 evidence、来源依赖与不确定性，并让下一信息动作/停止由认识状态而不是 transcript eloquence 决定。

OWNS:
- epistemic state 是当前问题的 sufficient state，而非 transcript/search tree 本身。
- Proposal/Candidate/Synthesis 与 Evidence/Finding 物理分槽；generation 不增加 evidence mass。
- evidence 保留 source/dependency；同源重复不伪装独立支持。
- root question/answer contract 可在新语义观察后更新，而非开局 bind-once 单标签。
- action value 相对 root problem，同时考虑信息增益、future gateway value、cost/risk。
- numeric posterior 只有在显式 hypotheses/likelihood/资格条件成立时才允许；否则保持 qualitative uncertainty。
- semantic equivalence/dominance 不能删掉独立来源价值；依赖感知去重。
- continuation/closure 由 epistemic controller 决定；生成模型不能靠一句“我完成了”跳过 pending request contract。
- 实现算法必须可由经典子模型/退化性质验证，但 A*/Bayes/MCTS 名称本身不是 ontology。

DOES NOT OWN:
- repository evidence acquisition。
- external web/browser acquisition。
- Sphinx MCP/handle/F# 文件布局、当前 start/resume wire protocol。
- Inquiry office authority。
- durable host/session lifecycle；epistemic kernel 是否 durable 是独立实现选择，除非未来需求改变。

DEPENDS ON: `participant-horizon`。新的世界事实通过 evidence-acquisition contracts 注入为 observation；具体是 repository、external 还是其它来源，不构成 epistemic core 的 hard dependency。

PROVIDES: no-free-information、dependency-aware uncertainty 与 controlled information-seeking/closure semantics。

FAILURE MEANING: RED = 模型可以通过重复思考提高“证据”，把同源材料当独立 likelihood，或绕过 controller 自己宣布认识闭包。

INDEPENDENT CHANGE: 用完全不同的 planning/search/inference algorithm 重写 Sphinx core，而 Proposal≠Evidence、dependency、qualified posterior、controller-owned closure 等 WHAT 不变。

CURRENT EVIDENCE: `docs/{why,what}/sphinx.md`；SPHINX-001..010；type `Sphinx/{Types,State,Search,Bayes,MonteCarlo,Value,Policy,Closure,Methodology,Representation,Absorb}.fs`；host `Sphinx/{McpServer,Codec,WireEncode,DecodePrimitives}.fs`；tests `tests/unit/sphinx/**`。
