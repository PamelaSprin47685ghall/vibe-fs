# epistemic-reasoning — HOW

> 非 normative。描述当前实现模型与约束，以及「历史与弃权」裁决。
> 当前实现名（Sphinx、A*/Bayes/MCTS、MCP start/resume、handle、F# 文件布局）全部是 HOW，
> 不是 WHAT。若未来换实现，WHAT.md 不变。源：历史 how/shape sphinx 条款、
> 历史 change（Sphinx）、`src/Wanxiangshu/Sphinx/*.fs`。

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
  Session.fs             SessionStore：handle → SessionEntry（Active|Answered lifecycle；进程内；
                         UUID；唯一 handle 索引）；Status/Cancel typed members
  McpContract.fs         nextTool 翻译（Request→tool）/ success DTO（yield/answered/status/cancel
                         payload）/ typed error view（ErrorView + errorObject）/ human summary；
                         纯 F# + plain JS objects，不依赖 MCP SDK
  McpServer.fs           MCP SDK / zod / stdio 唯一 owner；注册 start / assess / propose /
                         investigate / synthesize / status / cancel + legacy resume；
                         structuredContent（成功）/ isError + _meta（失败）；server instructions；
                         version 来自 PackageMetadata
```

Fable 编译 `Wanxiangshu.fsproj` 的 `Sphinx/*.fs` → `dist/Sphinx/*.js`。生产 MCP 入口唯一为
`dist/Sphinx/McpServer.js`；`scripts/build.mjs` 只验证该产物存在，不 copy 第二套源码。

`src/Wanxiangshu/Resources/PackageMetadata.fs`：`packageRoot()` 经 `import.meta.url` 定位包根
（`dist/<Area>/<Module>.js` 上溯两级），`version()` 读取该处 `package.json` 的 `version` 字段；
不 cwd 探测、不 candidate search、不 fallback。`McpServer.create` 用 `PackageMetadata.version()`
填充 `serverInfo.version`。

## 2. 主循环

```text
start(question)
  → create immutable EpistemicState
  → Policy.decide → YIELD SemanticAssessmentRequest → SessionStore alloc opaque handle
  → McpContract.nextTool(SemanticAssessmentRequest) = "assess"

phase tool（assess / propose / investigate / synthesize）(handle, typed observation)
  → SessionStore.ResumeObservation（所有 phase tool 汇聚于此）
  → Codec.decodeTyped（每 tool 各自的 decoder）
  → Policy 校验 PendingRequest ↔ Observation（错型/错 actionKey → KERNEL_REJECTED，状态不前进）
  → Closure.absorbAndClose → fixed point
  → Policy.decide → YIELD next Request | ANSWER CanonicalAnswer
  → McpContract.nextTool(next Request) → structuredContent.nextTool

status(handle) → active（nextTool + pending request）| answered（answer, nextTool=null）
cancel(handle) → cancelled；handle 立即失效
resume(handle, observation)  ← legacy 兼容工具（raw observation with explicit type field）
```

每个 phase tool 的 handler 调用各自的 `ObservationCodec.decode*`，然后统一进入
`SessionStore.ResumeObservation`——MCP 层不判断 phase 或 observation 合法性，`Policy.resume` /
`observationMatches` 是唯一裁判。成功返回 `{content, structuredContent}`，失败返回
`{content, isError:true, _meta:{tool, error:{code, ...}}}`（无 structuredContent）。

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

来自 `requirements/INDEX.md` 依赖骨架（不增删 edge）：

- `participant-horizon`：只有会改变合法行动的最小事实应穿过 horizon——investigation 得到的新
  世界事实经 evidence-acquisition contract 注入为 observation。具体是 repository、external 还是
  其它来源，不构成 epistemic core 的 hard dependency（HANDOFF §8：删除了对 acquisition
  package 的假依赖）。

## 11. 历史与弃权（考古记录，非 normative）

- **算法/组件降为 HOW**：Sphinx 名、A*/Bayes/MCTS 实现、MCP `start`/`resume` wire protocol、
  handle/SessionStore、F# 文件布局、value 系数（0.65、synthesis factor 0.72 等）、闭包 16 轮
  guard、方法库权重（0.58/0.42）——全部当前实现，不进 WHAT（边界卡片 DOES NOT OWN 与
  HANDOFF §6.7 同类裁决：`sphinx` 组件名与算法降为 HOW/proof）。
- **`Sphinx-wiki.html`（proposals/）**：算法资料，HOW 参考，非 ontology（CHANGES-AUDIT）。
- **旧完成声明的语义漂移**：`evidenceMass` 伪置信度、primary argmax、bind-once、wire
  equivalenceKey、LLM 自报 confidence、开局一次性生成候选，已在 corrective round
  （历史 change（Sphinx）「Corrective outcome — 2026-08-12」）逐条修正；被拒方向归档于
  WHY.md §3。
- **MCP/wire 身份 → `host-boundary`**：MCP server identity、launch config、`sphinx_*` 权限键、
  wire 编码归属 Host 边界；本包只拥有认识语义（EPI-004 的「同型契约」是语义侧）。
- **v2 affordance surface（2026-08-17）**：旧 `start`/`resume`-only wire（observation 以 JSON
  塞入 content text）被 v2 affordance 面取代——phase tools（assess/propose/investigate/synthesize）
  + status/cancel + structuredContent/isError 双信封 + `nextTool` 翻译。`mcp_server_surface_is_exactly_start_and_resume`
  测试被 `mcp_server_surface_exposes_phase_tools_and_legacy_resume` 取代（tool count 不是认识论
  定理，是 affordance 面事实，归 EPI-013）。legacy `resume` 保留为兼容工具但不在推荐面。
- **Inquiry office authority → `office-capability`/`capability-enforcement`**：谁能发起 inquiry、
  谁 allow `sphinx_*` 不是认识论命题。
- **durable journal 弃权**：V1 不做 durable EpistemicState journal——handle 进程内失效即丢弃；
  认识内核是否 durable 是独立实现选择，不由本包命题强制（边界卡片 DOES NOT OWN 末条）。
- **SPHINX-005（F# 单一实现）→ HOW**：编译边界/模块依赖方向是当前实现约束，不是认识论合同；
  其「内核不依赖 Host/Agent/Journal」的信息已吸收进 HOW §1。
- **SW-017 protocol-boundary exemption 裁决（2026-08-19）**：Sphinx `PendingRequest → nextTool`
  经裁决为领域 protocol automaton 的 protocol-level affordance translation，不是普通 CE workflow
  的 child action opcode。豁免以 EPI-013 书面记录。裁决依据：EPI-002（Kernel 拥有 continuation）、
  EPI-004（Pending Request 契约）、EPI-013（MCP affordance 面忠实翻译）。豁免不改变 `nextTool`
  的实现形态，只正式记录其语义边界。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包 `tests/`，删原文件）、
> `REUSE`（留在原处，记精确锚点 + SPLIT@cutover 计划）、`NEW`（本包新写）。
> 单跑命令：`node --test <file>`。全量：`node requirements/verification-system/tests/run.mjs`（自动发现
> `requirements/<package>/tests/**/*.test.mjs`）。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EPI-001 认识状态是 sufficient state | `tests/kernel.test.mjs`（`start_yields_semantic_assessment_request`）+ `tests/mcp-wire-characterization.test.mjs`（`start_yield_returns_structured_content_with_next_tool`） | MOVE | 对应文件 `node --test` |
| EPI-002 Kernel 拥有 continuation/closure/停止 | `tests/kernel.test.mjs`（`fsharp_kernel_has_no_agent_host_domain_dependency_and_sdk_stays_at_mcp_edge`）+ `tests/mcp-handle.test.mjs`（`handle_is_opaque_process_local_session_key`、`full_co_yield_path_preserves_kernel_continuation`）+ `tests/mcp-wire-characterization.test.mjs`（`answered_returns_structured_answer_and_null_next_tool`）+ `tests/mcp-contract.test.mjs`（`terminal_answered_rejects_further_observations`、`cancel_releases_handle_and_makes_it_unknown`）+ `tests/mcp-stdio.test.mjs`（`interleaved_inquiries_stay_independent`、`cancel_over_wire_then_status_unknown`） | MOVE | 对应文件 `node --test` |
| EPI-003 权威状态显式拥有认识基底 | `tests/semantics.test.mjs`（`ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence`）+ `tests/mcp-handle.test.mjs`（`full_co_yield_path_preserves_grounded_epistemic_basis`） | MOVE | 对应文件 `node --test` |
| EPI-004 Pending Request 契约 | `tests/kernel.test.mjs`（`resume_rejects_observation_that_does_not_match_pending_kernel_request`）+ `tests/sphinx-mcp-kernel.test.mjs`（`AGENT_030_kernel_identity_and_commands`）+ `tests/decoder-parity.test.mjs`（`decode and decodeSemanticAssessmentObservation produce same result for SemanticAssessment raw`、`decode and decodeCandidatesObservation produce same result for Candidates raw`、`decode and decodeInvestigationObservation produce same result for Investigation raw`、`decode and decodeSynthesisObservation produce same result for Synthesis raw`、`decode rejects unknown observation type`）+ `tests/mcp-wire-characterization.test.mjs`（`wrong_phase_returns_typed_error_without_structured_content`、`kernel_reject_does_not_advance_revision`）+ `tests/mcp-contract.test.mjs`（`wrong_phase_returns_kernel_rejected_without_advancing`、`wrong_action_key_returns_kernel_rejected_revision_unchanged`）+ `tests/mcp-stdio.test.mjs`（`wrong_phase_over_wire_returns_kernel_rejected`） | MOVE | 对应文件 `node --test` |
| EPI-005 Proposal ≠ Evidence（No Free Information） | `tests/kernel.test.mjs`（`semantic_assessment_and_candidates_are_control_observations_not_world_evidence`、`candidate_question_must_be_investigated_before_it_can_affect_answer`）+ `tests/semantics.test.mjs`（`synthesis_is_information_propagation_not_information_acquisition`） | MOVE | `node --test requirements/epistemic-reasoning/tests/kernel.test.mjs`；`node --test requirements/epistemic-reasoning/tests/semantics.test.mjs` |
| EPI-006 Evidence 保留 source/dependency | `tests/bayes.test.mjs`（`same_semantic_evidence_from_independent_dependency_groups_is_preserved_twice`、`same_dependency_group_is_not_counted_as_independent_evidence_twice`） | MOVE | `node --test requirements/epistemic-reasoning/tests/bayes.test.mjs` |
| EPI-007 RootContract 保留分布 | `tests/kernel.test.mjs`（`contract_keeps_distribution_after_semantic_assessment`）+ `tests/semantics.test.mjs`（`later_semantic_assessment_updates_control_belief_without_creating_evidence`）+ `tests/methodology.test.mjs`（`method_library_preserves_phase0_kernel_and_extends_without_pipeline_semantics`、`why_question_activates_multiple_generators_from_distribution_and_facets`、`predictive_polar_question_activates_base_rate_and_falsification`） | MOVE | 对应文件 `node --test` |
| EPI-008 action value 相对根问题 | `tests/semantics.test.mjs`（`gateway_gain_can_make_low_immediate_gain_question_worth_asking`） | MOVE | `node --test requirements/epistemic-reasoning/tests/semantics.test.mjs` |
| EPI-009 概率只接受合格数值证据 | `tests/bayes.test.mjs`（`bayesian_posterior_requires_explicit_numeric_qualification`、`qualified_independent_evidence_updates_posterior`、`unqualified_item_cannot_mask_qualified_evidence_from_same_dependency_group`） | MOVE | `node --test requirements/epistemic-reasoning/tests/bayes.test.mjs` |
| EPI-010 经典算法是可验证退化 | `tests/search.test.mjs`（`graph_astar_degenerates_to_standard_g_plus_h_shortest_path`、`graph_astar_reopens_closed_node_when_better_g_is_discovered`、`graph_astar_rejects_negative_cost_graph`）+ `tests/mcts.test.mjs`（`mcts_selection_expansion_rollout_backup_prefers_high_value_branch`、`graph_mcts_shares_transposition_statistics_by_semantic_node_key`、`uct_for_unvisited_node_is_infinite`） | MOVE | 对应文件 `node --test` |
| EPI-011 等价约简 dependency-aware | `tests/represent.test.mjs`（`wire_equivalence_hint_cannot_force_kernel_merge`、`same_kernel_identity_merges_candidate_provenance_instead_of_erasing_it`、`same_question_from_independent_dependency_groups_is_not_false_deduplicated`、`kernel_owned_equivalence_class_removes_only_truly_dominated_representation`、`pareto_incomparable_equivalent_representations_both_survive`） | MOVE | `node --test requirements/epistemic-reasoning/tests/represent.test.mjs` |
| EPI-012 closure 幂等且全局 | `tests/kernel.test.mjs`（`closure_is_idempotent_at_fixed_point`） | MOVE | `node --test requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-013 MCP affordance 面忠实翻译 Kernel continuation | `tests/mcp-handle.test.mjs`（`mcp_server_surface_exposes_phase_tools_and_legacy_resume`）+ `tests/mcp-contract.test.mjs`（`full_next_tool_chain_via_phase_tools`、`legacy_resume_advances_via_generic_decode_with_same_envelope`、`invalid_observation_when_forms_missing`、`missing_handle_question_required_unknown_handle_codes`、`surface_status_and_cancel_functions_match_handler_envelopes`、`kernel_rejected_error_content_is_human_readable`）+ `tests/mcp-stdio.test.mjs`（`tools_list_returns_eight_tools_with_schemas`、`full_flow_to_answered_driven_by_next_tool`、`unknown_handle_and_malformed_payload_are_typed_errors`、`answered_then_submit_returns_already_answered`、`stdout_lines_are_pure_jsonrpc`、`restart_invalidates_handles`） | NEW | 对应文件 `node --test` |
| EPI-014 MCP server 身份元数据与 shipped manifest 一致 | `tests/mcp-stdio.test.mjs`（`initialize_returns_server_identity_and_instructions`） | NEW | `node --test requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs` |

> 表中 `tests/` 前缀省略为 `requirements/epistemic-reasoning/tests/`（MOVE 落点全部在本包）。

### REUSE 落点与 SPLIT@cutover

| 覆盖 | 落点 | 说明 / cutover 计划 |
|---|---|---|
| MCP 身份 / Host 注入 / `sphinx_*` 权限 | `requirements/epistemic-reasoning/tests/sphinx-mcp-kernel.test.mjs`（`AGENT_030_kernel_identity_and_commands`、`AGENT_030_launch_disabled_fixture_test_local`、`AGENT_030_apply_preserves_other_mcp_servers`、`AGENT_030_inquiry_only_wildcard_permission`） | SPLIT 家族（PROOF-MAP `agent/`）：kernel identity/commands 断言 → 本包；launch/injection → `host-boundary`；Inquiry-only 权限 → `capability-enforcement`。cutover 时按断言拆分。 |
| MCP fixture | `requirements/verification-system/tests/support/sphinx-mcp-fixture.js` | 共享 fixture（harness 已迁 verification-system/tests/support/）。 |
| Host canary / e2e dry-run | `requirements/verification-system/tests/e2e/entry.test.mjs` long-stroke `strength-canary-*` | `verification-system` MECHANISM（与本包无直接落点，供追踪）。 |

### Semantic anchor ids（本包拥有）

`scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.inquiry`（PROOF-MAP §9.2 声明
逐 ID 归包，本包 = epistemic-reasoning）：

```text
kernel-owns-state     control-not-evidence   generation-not-control
no-free-information   closure-not-collapse   root-relative
synthesis-boundary
```

### SPLIT@cutover 待办

1. `requirements/epistemic-reasoning/tests/sphinx-mcp-kernel.test.mjs`：按断言拆三份——本包（kernel identity/commands 的
   `sphinx_*`/`dist/Sphinx/McpServer.js` 事实）、`host-boundary`（launch/env/apply）、
   `capability-enforcement`（Inquiry-only wildcard permission）。
2. 若未来 `semantic-anchors.mjs` 为 speculation 增加 anchor，speculative-investigation 应声明
   独立组；本包 inquiry 组不变。

### 验证状态

- v2 affordance surface landed 2026-08-17：phase tools（assess/propose/investigate/synthesize）+
  status/cancel + structuredContent/isError 双信封 + `nextTool` 翻译 + server instructions +
  `PackageMetadata.version()` 身份元数据。`McpContract.fs`、`Resources/PackageMetadata.fs` 新增；
  `McpServer.fs` 重写（8 tools）；`Session.fs` 增加 Status/Cancel typed members。
- 44 existing tests green（2026-08-17，`node --test requirements/epistemic-reasoning/tests/*.test.mjs`）。
- 新增 `mcp-contract.test.mjs`（10 tests）、`mcp-stdio.test.mjs`（10 tests，真实 spawn
  `dist/Sphinx/McpServer.js` 走 stdio JSON-RPC）；均 green（2026-08-17）。
- `support.mjs` 为 helper（非 `*.test.mjs`），runner 不误发现；test-boundary 门不扫描。
