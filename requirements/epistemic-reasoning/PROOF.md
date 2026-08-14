# epistemic-reasoning — PROOF

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包 `tests/`，删原文件）、
> `REUSE`（留在原处，记精确锚点 + SPLIT@cutover 计划）、`NEW`（本包新写）。
> 单跑命令：`node --test <file>`。全量：`node tests/unit/run.mjs`（自动发现
> `requirements/<package>/tests/**/*.test.mjs`）。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EPI-001 认识状态是 sufficient state | `tests/kernel.test.mjs`（`start_yields_semantic_assessment_and_contract_keeps_distribution`）+ `tests/semantics.test.mjs`（`later_semantic_assessment_updates_control_belief_without_creating_evidence`） | MOVE | `node --test requirements/epistemic-reasoning/tests/kernel.test.mjs` |
| EPI-002 Kernel 拥有 continuation/closure/停止 | `tests/kernel.test.mjs`（`resume_rejects_observation_that_does_not_match_pending_kernel_request`）+ `tests/mcp-handle.test.mjs`（`full_co_yield_path_preserves_kernel_continuation_and_grounded_basis`） | MOVE | 对应文件 `node --test` |
| EPI-003 权威状态显式拥有认识基底 | `tests/semantics.test.mjs`（`ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence`）+ `tests/mcp-handle.test.mjs`（`full_co_yield_path_preserves_kernel_continuation_and_grounded_basis`） | MOVE | 对应文件 `node --test` |
| EPI-004 Pending Request 契约 | `tests/kernel.test.mjs`（`resume_rejects_observation_that_does_not_match_pending_kernel_request`）+ `tests/mcp-handle.test.mjs`（`mcp_server_surface_is_exactly_start_and_resume`） | MOVE | 对应文件 `node --test` |
| EPI-005 Proposal ≠ Evidence（No Free Information） | `tests/kernel.test.mjs`（`semantic_assessment_and_candidates_are_control_observations_not_world_evidence`、`candidate_question_must_be_investigated_before_it_can_affect_answer`）+ `tests/semantics.test.mjs`（`synthesis_is_information_propagation_not_information_acquisition`） | MOVE | `node --test requirements/epistemic-reasoning/tests/kernel.test.mjs`；`node --test requirements/epistemic-reasoning/tests/semantics.test.mjs` |
| EPI-006 Evidence 保留 source/dependency | `tests/bayes.test.mjs`（`same_semantic_evidence_from_independent_dependency_groups_is_preserved_twice`、`same_dependency_group_is_not_counted_as_independent_evidence_twice`） | MOVE | `node --test requirements/epistemic-reasoning/tests/bayes.test.mjs` |
| EPI-007 RootContract 保留分布 | `tests/kernel.test.mjs`（`start_yields_semantic_assessment_and_contract_keeps_distribution`）+ `tests/semantics.test.mjs`（`later_semantic_assessment_updates_control_belief_without_creating_evidence`）+ `tests/methodology.test.mjs`（`why_question_activates_multiple_generators_from_distribution_and_facets`、`predictive_polar_question_activates_base_rate_and_falsification`） | MOVE | 对应文件 `node --test` |
| EPI-008 action value 相对根问题 | `tests/semantics.test.mjs`（`gateway_gain_can_make_low_immediate_gain_question_worth_asking`）+ `tests/kernel.test.mjs`（`closure_is_idempotent_at_fixed_point`） | MOVE | 对应文件 `node --test` |
| EPI-009 概率只接受合格数值证据 | `tests/bayes.test.mjs`（`bayesian_posterior_requires_explicit_numeric_qualification`、`qualified_independent_evidence_updates_posterior`、`unqualified_item_cannot_mask_qualified_evidence_from_same_dependency_group`） | MOVE | `node --test requirements/epistemic-reasoning/tests/bayes.test.mjs` |
| EPI-010 经典算法是可验证退化 | `tests/search.test.mjs`（`graph_astar_degenerates_to_standard_g_plus_h_shortest_path`、`graph_astar_reopens_closed_node_when_better_g_is_discovered`、`graph_astar_rejects_negative_cost_graph`）+ `tests/mcts.test.mjs`（`mcts_selection_expansion_rollout_backup_prefers_high_value_branch`、`graph_mcts_shares_transposition_statistics_by_semantic_node_key`、`uct_for_unvisited_node_is_infinite`） | MOVE | 对应文件 `node --test` |
| EPI-011 等价约简 dependency-aware | `tests/represent.test.mjs`（`wire_equivalence_hint_cannot_force_kernel_merge`、`same_kernel_identity_merges_candidate_provenance_instead_of_erasing_it`、`same_question_from_independent_dependency_groups_is_not_false_deduplicated`、`kernel_owned_equivalence_class_removes_only_truly_dominated_representation`、`pareto_incomparable_equivalent_representations_both_survive`） | MOVE | `node --test requirements/epistemic-reasoning/tests/represent.test.mjs` |
| EPI-012 closure 幂等且全局 | `tests/kernel.test.mjs`（`closure_is_idempotent_at_fixed_point`）+ `tests/semantics.test.mjs`（`synthesis_is_information_propagation_not_information_acquisition`） | MOVE | 对应文件 `node --test` |

> 表中 `tests/` 前缀省略为 `requirements/epistemic-reasoning/tests/`（MOVE 落点全部在本包）。

## REUSE 落点与 SPLIT@cutover

| 覆盖 | 落点 | 说明 / cutover 计划 |
|---|---|---|
| MCP 身份 / Host 注入 / `sphinx_*` 权限 | `tests/unit/agent/sphinx-mcp.test.mjs`（`AGENT_030_kernel_identity_and_commands`、`AGENT_030_launch_disabled_fixture_test_local`、`AGENT_030_apply_preserves_other_mcp_servers`、`AGENT_030_inquiry_only_wildcard_permission`） | SPLIT 家族（PROOF-MAP `agent/`）：kernel identity/commands 断言 → 本包；launch/injection → `host-boundary`；Inquiry-only 权限 → `capability-enforcement`。cutover 时按断言拆分。 |
| MCP fixture | `tests/unit/support/sphinx-mcp-fixture.js` | 共享 fixture，`tests/unit/support/**` 不动（契约 §4 规则 4）。 |
| Host canary / e2e dry-run | `tests/e2e/entry.test.mjs` long-stroke `strength-canary-*` | `verification-system` MECHANISM（与本包无直接落点，供追踪）。 |

## Semantic anchor ids（本包拥有）

`scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.inquiry`（PROOF-MAP §9.2 声明
逐 ID 归包，本包 = epistemic-reasoning）：

```text
kernel-owns-state     control-not-evidence   generation-not-control
no-free-information   closure-not-collapse   root-relative
synthesis-boundary
```

## SPLIT@cutover 待办

1. `tests/unit/agent/sphinx-mcp.test.mjs`：按断言拆三份——本包（kernel identity/commands 的
   `sphinx_*`/`dist/Sphinx/McpServer.js` 事实）、`host-boundary`（launch/env/apply）、
   `capability-enforcement`（Inquiry-only wildcard permission）。
2. 若未来 `semantic-anchors.mjs` 为 speculation 增加 anchor，speculative-investigation 应声明
   独立组；本包 inquiry 组不变。

## 验证状态

- 8 个 MOVE 文件 + helper 单跑绿：`kernel` 6、`semantics` 4、`mcp-handle` 3、`bayes` 5、
  `search` 3、`mcts` 3、`represent` 5、`methodology` 3（共 32 pass，2026-08-14）。
- `support.mjs` 为 helper（非 `*.test.mjs`），runner 不误发现；test-boundary 门不扫描。
