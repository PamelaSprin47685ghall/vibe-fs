# PROOF — session-ontology（测试落点表）

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`（单文件可红）。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| SESSION-ONTOLOGY-001 | `tests/session-ownership-ratchet.test.mjs` `session_ownership_ratchet_documents_closed_kind_set`（closed kind 集）+ `session_ownership_attachment_tokens_require_surface`（AttachmentKind 封闭面）+ `tests/session-ontology-classification.test.mjs` `HOST_008_orthogonal_execution_class_and_ownership_are_derived_from_the_durable_link`（正交两维）+ `HOST_008_execution_class_predicates_distinguish_work_from_internal_leaf`；REUSE `tests/sync-delegate.test.mjs` `HOST_008_SessionExecutionClass_predicates_distinguish_work_from_leaf` | MOVE + NEW + REUSE | `node --test requirements/session-ontology/tests/session-ownership-ratchet.test.mjs` / `.../session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-002 | `tests/session-ownership-ratchet.test.mjs` `session_ownership_matrix_green_fixture`（8 kind 全填，含 owner 字段）+ `session_ownership_ratchet_questionnaire_requires_owner_field` + `session_ownership_matrix_empty_field_fails_closed` + `session_ownership_matrix_rejects_special_pleading`；`tests/session-ontology-classification.test.mjs` `HOST_008_attached_ownership_carries_exactly_one_owner_and_one_kind`（四格穷尽 + Attached 携带 owner/kind）+ `HOST_008_root_and_attached_helpers_agree_with_the_type_model`；REUSE `tests/sync-delegate.test.mjs` `HOST_008_SessionOwnership_attached_carries_owner_and_kind` | MOVE + NEW + REUSE | 同上 |
| SESSION-ONTOLOGY-003 | `tests/session-ontology-classification.test.mjs` `EXEC_026_dedicated_sync_inspector_and_coder_are_work_plus_attached`；REUSE `requirements/session-ontology/tests/sync-delegate.test.mjs` `HOST_008_delegateRoleToAttachment_maps_inspector_and_coder` | NEW + REUSE | `node --test requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-004 | `tests/session-ontology-classification.test.mjs` `HOST_008_strength_replica_is_universal_internal_leaf_attachment`（InternalLeaf + Attached(StrengthReplica)）+ `HOST_008_a_companion_is_internal_leaf_attached_with_owner_and_kind`（Companion = InternalLeaf+Attached） | NEW | 同上 |
| SESSION-ONTOLOGY-005 | REUSE `requirements/session-ontology/tests/session-association.test.mjs` `HOST_008_a_session_cannot_be_its_own_Companion` / `HOST_008_one_Companion_cannot_serve_two_work_sessions`（owner 唯一，自链被拒）；`HOST_008_attached_ownership_carries_exactly_one_owner_and_one_kind`（blogger owner = work session） | REUSE + NEW | `node --test requirements/session-ontology/tests/session-association.test.mjs` |
| SESSION-ONTOLOGY-006 | REUSE `requirements/session-ontology/tests/session-flattening.test.mjs` `HOST_015_child_of_child_is_physically_parented_to_family_root` / `HOST_015_family_root_resolves_through_restored_journal_parents`；REUSE `requirements/session-ontology/tests/session-association.test.mjs` `HOST_008_the_work_session_parent_is_recorded_when_supplied` / `HOST_008_relinking_without_a_parent_does_not_erase_a_known_one`；REUSE `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` `HOST_015_companion_satellite_recovery_reuses_journal_linked_child_under_flat_root` | REUSE | `node --test requirements/session-ontology/tests/session-flattening.test.mjs` / `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| SESSION-ONTOLOGY-007 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs::HOST_008_durable_link_derives_work_and_leaf_cells` | NEW | `node --test requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-008 | REUSE `requirements/session-ontology/tests/session-association.test.mjs`：`HOST_008_linking_records_both_directions_at_once`、`COMPANION_002_the_Companion_side_answers_isCompanion_immediately`、`COMPANION_002_a_Companion_is_structurally_a_leaf`、`COMPANION_002_giving_a_Companion_its_own_Companion_is_refused`、`COMPANION_002_a_second_different_Y_for_one_work_session_is_refused`、`HOST_008_one_Companion_cannot_serve_two_work_sessions`、`HOST_008_a_session_cannot_be_its_own_Companion`、`COMPANION_003_relinking_the_same_pair_is_idempotent`、`COMPANION_002_unlinking_does_not_disturb_another_pair` | REUSE | `node --test requirements/session-ontology/tests/session-association.test.mjs` |
| SESSION-ONTOLOGY-009 | REUSE `requirements/session-ontology/tests/session-association.test.mjs` `COMPANION_001_every_work_session_may_have_a_Y_regardless_of_role`（非资格白名单）+ `COMPANION_003_unlinking_frees_the_work_session_to_get_a_fresh_Y` + `COMPANION_003_unlinking_is_total_and_idempotent` | REUSE | 同上 |
| SESSION-ONTOLOGY-010 | REUSE `requirements/session-ontology/tests/session-association.test.mjs` `COMPANION_001_an_unknown_session_is_not_a_Companion`（结构事实，无 role 参数）；`tests/session-ownership-ratchet.test.mjs` `session_ownership_matrix_green_fixture`（分类不依赖 role） | REUSE + MOVE | 同上 / `node --test requirements/session-ontology/tests/session-ownership-ratchet.test.mjs` |
| SESSION-ONTOLOGY-011 | `tests/session-ontology-classification.test.mjs` `HOST_008_strength_replica_is_never_a_satellite_kind`（非 SatelliteKind）；`tests/session-ownership-ratchet.test.mjs` `session_ownership_ratchet_attachment_tokens_include_strength_replica` + `session_ownership_matrix_strength_replica_row_answers_owner`（owner 至多一个 active）+ `session_ownership_matrix_evidence_without_token_fails_closed` + `session_ownership_matrix_rejects_unexpected_kind` 兜底 | NEW + MOVE | `node --test requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-012 | REUSE `requirements/session-ontology/tests/sync-delegate.test.mjs` `HOST_008_AttachmentKind_bookkeeper_carries_transaction_id` + `HOST_008_SessionOwnership_root_and_attached_helpers`；`tests/session-ownership-ratchet.test.mjs` `session_ownership_matrix_green_fixture`（Bookkeeper 行） | REUSE + MOVE | `node --test requirements/session-ontology/tests/sync-delegate.test.mjs` |
| SESSION-ONTOLOGY-013 | `tests/session-ontology-classification.test.mjs` `HOST_008_canonical_durable_role_label_is_stable_via_catalog_not_du_name`；REUSE `tests/terminal-policy.test.mjs` `TPOL_roleName_lowercases_roles_and_handles_none` | NEW + REUSE | `node --test requirements/session-ontology/tests/session-ontology-classification.test.mjs` / `.../terminal-policy.test.mjs` |
| SESSION-ONTOLOGY-014 | `tests/satellite-kind.test.mjs` `HOST_014_SatelliteKind_is_Companion_only`；`tests/session-ownership-ratchet.test.mjs` `session_ownership_matrix_rejects_unexpected_kind`（注入 `Teacher` → `unexpected-kind`）+ `session_ownership_matrix_missing_kind_fails_closed` + `session_ownership_matrix_invalid_document_fails_closed` + `session_ownership_repo_scan_is_green` | MOVE + NEW | `node --test requirements/session-ontology/tests/session-ownership-ratchet.test.mjs` |

## 反向覆盖（OWNED clause → 本包命题）

- `HOST-008`（OWNED）→ SESSION-ONTOLOGY-001/002/003/004/005/007/010/012。
- `HOST-015`（NEEDS-SPLIT：物理扁平部分）→ SESSION-ONTOLOGY-006；restore matching 部分 →
  `managed-session-lifecycle`。
- `COMPANION-001/002`（OWNED）→ SESSION-ONTOLOGY-009/010/004/008。
- `EXEC-026`（Dedicated = Work+Attached 部分）→ SESSION-ONTOLOGY-003；runtime ownership 部分 →
  `managed-session-lifecycle`。
- `AGENT-001`（Bookkeeper InternalLeaf+Attached）→ SESSION-ONTOLOGY-004/012。
- `AGENT-024`（Dedicated Inspector/Coder = Work+Attached）→ SESSION-ONTOLOGY-003。
- `AGENT-002/020/021/022`、`HOST-014` → GARBAGE（见 WHAT 弃权节）。

## 包拥有的 gate / anchor

- `scripts/checks/session-ownership-ratchet.mjs` — 本包（+ managed-session-lifecycle 问卷部分）
  的共享 gate；KEEP（PROOF-MAP）。其 verify 测试已 MOVE 至 `tests/session-ownership-ratchet.test.mjs`。
- semantic-anchors.mjs：本包**零 anchor**（全文件为 Role Law / tool cognition 锚点，归其它包）。

## SPLIT@cutover 清单

1. `requirements/session-ontology/tests/sync-delegate.test.mjs`：4 个 `HOST_008_*` 断言归本包；4 个 `EXEC_026_*`
   （tierForOwner/agentNameFor/ReuseScopeId/DedicatedDelegateKey）归 delegation。cutover 时拆文件
   或逐断言迁移。
2. `requirements/session-ontology/tests/session-flattening.test.mjs`：直接 import `dist/fable_modules/**`（test-boundary
   铁律禁止移动）。cutover 时移除 fable import（改用 `tests/unit/support/**`）后 MOVE；
   owner = session-ontology（物理扁平）+ managed-session-lifecycle（abort 级联）。
3. `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs`：import fable List/Result → 禁止移动；
   `HOST_014_SatelliteKind_is_Companion_only` 归本包，恢复/reuse/replacement 断言归
   managed-session-lifecycle。cutover 时拆分。
4. `scripts/checks/session-ownership-matrix.json` 问卷：`owner` 等分类字段归本包；
   `reusable/cancel/retire/handle/crashReconcile` 字段归 managed-session-lifecycle。
   共享 gate 保留（一个 assertion 一个 owner，字段级划界）。
5. `tests/unit/verify/` 目录（SPLIT 混合）：本包已 MOVE `session-ownership-ratchet.test.mjs`；
   其余 verify 文件与其它包交叉，不做动作。
