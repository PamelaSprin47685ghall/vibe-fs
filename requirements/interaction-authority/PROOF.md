# PROOF —— 测试落点表（interaction-authority）

## 运行方式

```bash
node --test requirements/interaction-authority/tests/authority-root.test.mjs
node --test requirements/interaction-authority/tests/continuation-origin.test.mjs
# 全量：node tests/unit/run.mjs（自动包含 requirements/**/tests/*.test.mjs）
```

## 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| R1 | INTERACTION-AUTHORITY-001/002 | `requirements/interaction-authority/tests/authority-root.test.mjs::IA_001_authority_root_id_is_reachable_only_by_promoting_a_physical_message` + `IA_001_002_no_function_from_transport_receipt_to_authority_root` | NEW | `node --test requirements/interaction-authority/tests/authority-root.test.mjs` |
| R2 | INTERACTION-AUTHORITY-003 | `authority-root.test.mjs::IA_003_root_fixes_profile_and_derives_peer_role_tier_from_selected_agent_alone` + `IA_003_new_root_replaces_the_profile_and_clears_everything_run_scoped` + `IA_003_root_becomes_the_continuation_source_for_later_defaults` | NEW | 同上 |
| R3 | INTERACTION-AUTHORITY-004 | `continuation-origin.test.mjs::IA_004_a_continuation_never_replaces_the_authority_root` | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R4 | INTERACTION-AUTHORITY-016 | `continuation-origin.test.mjs::IA_016_accepting_an_authority_root_claim_does_not_enter_the_continuation_map` + `authority-root.test.mjs::IA_003_agent_owner_root_claim_has_no_run_until_physical_acceptance` | NEW | 两文件各自命令 |
| R5 | INTERACTION-AUTHORITY-005 | `continuation-origin.test.mjs::IA_005_every_continuation_kind_is_representable_and_none_is_a_root` + `authority-root.test.mjs::IA_005_needhelp_kinds_are_continuations_not_roots` | NEW | 同上 |
| R6 | INTERACTION-AUTHORITY-006 | `authority-root.test.mjs::IA_006_bare_legacy_names_are_refused_with_typed_rejection` + `IA_006_agent_owner_root_claims_reject_bare_legacy_names_too` | NEW | `node --test requirements/interaction-authority/tests/authority-root.test.mjs` |
| R7 | INTERACTION-AUTHORITY-007/008/017 | `continuation-origin.test.mjs::IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root` + `IA_017_unclaimed_continuation_without_active_run_stays_unknown` | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R8 | INTERACTION-AUTHORITY-009 | `continuation-origin.test.mjs::IA_009_a_human_root_is_never_inferred_by_a_pure_function` + `IA_009_ingress_does_not_promote_UnknownOrigin_to_HumanRoot_while_run_active` | NEW | 同上 |
| R9 | INTERACTION-AUTHORITY-010 | `authority-root.test.mjs::IA_010_one_terminal_provider_run_earns_exactly_one_repair` | NEW | `node --test requirements/interaction-authority/tests/authority-root.test.mjs` |
| R10 | INTERACTION-AUTHORITY-012 | `tests/unit/host/assistance-host.test.mjs::AGENT_031_fast_needhelp_continues_same_session_as_deep_peer_without_moving_fallback`（sends[0].agent = deep peer；fallback offset/failures 不变；AcceptedContinuationIds 含 NeedHelpEscalation 不含 ProviderRetryAttempt）+ `authority-root.test.mjs::IA_005_needhelp_kinds_are_continuations_not_roots` | REUSE + NEW | `node --test tests/unit/host/assistance-host.test.mjs` |
| R11 | INTERACTION-AUTHORITY-013 | `tests/unit/host/assistance-host.test.mjs::AGENT_031_snapshot_agent_binding_turns_fast_escalation_into_deep_consultation_even_while_fallback_stays_fast`（fast→deep 同 Session 续推；consultation 部分归 delegation） | REUSE | `node --test tests/unit/host/assistance-host.test.mjs` |
| R12 | INTERACTION-AUTHORITY-014 | `tests/unit/execution/join-guard.test.mjs::EXEC_016_join_guard_continuation_kind_is_parseable` + `tests/unit/host/join-guard.test.mjs::JNGD_nudge_sends_join_guard_continuation_and_claims`（claim 持久化半边归 dispatch-protocol） | REUSE | `node --test tests/unit/execution/join-guard.test.mjs` / `node --test tests/unit/host/join-guard.test.mjs` |
| R13 | INTERACTION-AUTHORITY-015 | `tests/unit/prompt/authority.test.mjs::PROMPT_004_ingress_does_not_promote_UnknownOrigin_to_HumanRoot_while_run_active` + `tests/unit/execution/join-v2-mailbox.test.mjs::EXEC_017_*`（wake 机制归 delegation；ingress 不给 authority 归本包） | REUSE | `node --test tests/unit/execution/join-v2-mailbox.test.mjs` |
| R14 | INTERACTION-AUTHORITY-011 | `tests/unit/prompt/authority.test.mjs::PROMPT_002_root_derives_peer_role_and_tier_from_the_selected_agent_alone`（profile 字段由 SelectedAgent 唯一派生 = 原子 authority 子记录；「无 model 字段」断言归 dispatch-protocol 的 DP-010） | REUSE | `node --test tests/unit/prompt/authority.test.mjs` |
| R15 | INTERACTION-AUTHORITY-012（idle 续推 occasion 一次） | `tests/unit/host/idle-continuation-authority.test.mjs::HOST_004_idle_manager_continuation_consumes_one_permit_and_claims_once`（permit 语义归 causal-wait；同 occasion 只发一次归本包） | REUSE | `node --test tests/unit/host/idle-continuation-authority.test.mjs` |
| R16 | INTERACTION-AUTHORITY-004（repair=continuation 判定） | `tests/unit/reconciliation/completed-turn-classifier.test.mjs::RECON_needs_interactionRepair_role_by_outcome_table`（TurnOutcome 分类归 host-boundary；「repair 是 continuation 而非 fallback/新 root」归本包） | REUSE | `node --test tests/unit/reconciliation/completed-turn-classifier.test.mjs` |
| R17 | INTERACTION-AUTHORITY-005（来源解析 family） | `tests/unit/prompt/authority.test.mjs::PROMPT_009_resolution_order_is_accepted_then_claimed_then_compaction_then_root` + `PROMPT_004_009_an_accepted_id_outranks_host_compaction` + `PROMPT_004_a_human_root_is_never_inferred_by_a_pure_function` | REUSE | `node --test tests/unit/prompt/authority.test.mjs` |

统计：17 行落点；NEW 2 文件 18 断言全绿；REUSE 7 个既有文件（SPLIT@cutover 前留在原处）。

## `tests/unit/prompt/authority.test.mjs` 断言级 SPLIT 计划（PROOF-MAP mandatory split #1）

双 owner 文件：**REUSE + SPLIT@cutover**。cutover 时按下列清单物理拆成两文件后删除原文件；
拆出的 interaction-authority 部分与 `requirements/interaction-authority/tests/` 现有 NEW 文件合并去重。

| authority.test.mjs 测试 | 归属 |
|---|---|
| `PROMPT_001_authority_root_id_is_reachable_only_by_promoting_a_physical_message` | interaction-authority |
| `PROMPT_001_a_transport_receipt_can_never_become_an_authority_root` | 拆分：`isAdmissionShaped` 形态断言 → dispatch-protocol；crossing 缺席断言 → interaction-authority |
| `PROMPT_002_authority_root_profile_cannot_express_a_model` | dispatch-protocol（Model=None 无字段） |
| `PROMPT_002_root_derives_peer_role_and_tier_from_the_selected_agent_alone` | interaction-authority |
| `AGENT_004_005_bare_legacy_agent_names_are_refused` | interaction-authority（typed 拒绝）；精确 legacy 名单级断言 → 迁移 ratchet（HOW），cutover 时收敛为「显式 managed agent 才可成 root」 |
| `PROMPT_002_a_new_root_replaces_the_profile_and_clears_everything_run_scoped` | interaction-authority |
| `PROMPT_003_a_continuation_never_replaces_the_authority_root` | interaction-authority |
| `PROMPT_003_every_continuation_kind_is_representable_and_none_is_a_root` | interaction-authority |
| `PROMPT_005_submit_records_the_receipt_without_resolving_the_claim` | dispatch-protocol |
| `PROMPT_005_abandon_removes_the_claim_and_leaves_the_active_run_alone` | dispatch-protocol（claim 生命周期）；「active run 不变」断言 → interaction-authority |
| `PROMPT_011_stable_logical_run_id_is_a_function_of_runtime_session_and_root` | interaction-authority（root 建立 run 的身份派生） |
| `PROMPT_011_claim_scope_names_exactly_session_run_origin_and_payload` | dispatch-protocol |
| `PROMPT_011_claim_sequence_advances_on_registration_not_on_resolution` | dispatch-protocol |
| `PROMPT_011_prompt_key_is_deterministic_and_moves_with_every_component` | dispatch-protocol |
| `PROMPT_011_recovery_budget_is_folded_from_plugin_starts_not_written` | dispatch-protocol（budget 派生；精确常数 = HOW） |
| `FALLBACK_008_one_terminal_provider_run_earns_exactly_one_repair` | interaction-authority（repair 预算 = root 可重置的 authority 资源；「不计入 A/B 失败推进」半边归 provider-attempt-recovery） |
| `PROMPT_009_resolution_order_is_accepted_then_claimed_then_compaction_then_root` | interaction-authority |
| `PROMPT_004_009_an_accepted_id_outranks_host_compaction` | interaction-authority |
| `PROMPT_004_a_human_root_is_never_inferred_by_a_pure_function` | interaction-authority |
| `PROMPT_004_ingress_does_not_promote_UnknownOrigin_to_HumanRoot_while_run_active` | interaction-authority |
| `PROMPT_009_accepting_an_authority_root_claim_does_not_enter_the_continuation_map` | interaction-authority |
| `PROMPT_002_agent_owner_root_claims_reject_bare_legacy_names_too` | interaction-authority |

## semantic anchors

`scripts/checks/semantic-anchors.mjs` 当前只含 Role cognition / tool description / office capability
三类 catalog（owner = `cognitive-environment` / `action-affordance` / `office-capability`）。
**interaction-authority 拥有 0 个 semantic anchor id**：authority 判定由类型（`PromptOrigin`、
`AuthorityExecutionProfile`）与行为测试承载，不经 prompt 正则。

## GAP 与 cutover 待办

- 无 GAP：17 条命题全部有落点（NEW 或 REUSE 锚点）。
- SPLIT@cutover：`authority.test.mjs` 按上表物理拆分并删除原文件；`assistance-host.test.mjs`、
  `join-guard.test.mjs`（host+execution）、`idle-continuation-authority.test.mjs`、
  `completed-turn-classifier.test.mjs`、`join-v2-mailbox.test.mjs` 的跨 owner 断言在文件级拆分时
  收敛到各自 owner（见各 REUSE 行的边界注记）。
- 迁移 ratchet 退休：`student-teacher-absence.mjs`、legacy 名单级断言（AGENT-004）在新世界基线
  稳定后删除（PROOF-MAP DELETE 清单）。
