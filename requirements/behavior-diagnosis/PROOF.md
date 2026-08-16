# behavior-diagnosis — PROOF（测试落点表）

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 已物理移入本包 `tests/`（删除原文件，
> 单跑绿）；`REUSE` = 留在原处（多-owner 或不宜移动），记精确锚点 + cutover 拆分计划
> （`SPLIT@cutover`）；`NEW` = 本包新写。运行命令：`node --test <file>`。
> 本包无 semantic-anchors.mjs anchor id（该 catalog 只有 ROLE/TOOL/OFFICE 三组）。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| BD-001 目录即唯一规则真相（TipName=enum=RuleId） | `tests/catalog.test.mjs` `ENFORCER_170_catalog_has_exactly_120_rules` / `ENFORCER_170_tip_name_equals_rule_id_and_field` / `ENFORCER_172_field_names_match_the_rfc_spelling`；REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_01/02_and_16` | MOVE + REUSE | `node --test tests/catalog.test.mjs` |
| BD-002 装载 fail-fast、零 fallback | `tests/catalog-validation.test.mjs`（validate 拒绝族）；`tests/catalog.test.mjs`（120 规则/非空正文）；REUSE `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs`（打包资源路径） | MOVE + REUSE | `node --test tests/catalog.test.mjs` |
| BD-003 Domain 校验合同（schemaVersion/唯一/1..N/非空） | `tests/catalog-validation.test.mjs` `ENFORCER_170_validate_accepts_one_rule` … `ENFORCER_170_validate_rejects_identity_mismatch`（11 条全族） | MOVE | `node --test tests/catalog-validation.test.mjs` |
| BD-004 检测语料全量、确定性进 Blogger system | `tests/rulebook-system-composition.test.mjs` `BEHAVIOR_DIAGNOSIS_SYSTEM_001_composed_prompt_contains_every_tip_exactly_once` / `SYSTEM_002_composition_is_deterministic` / `SYSTEM_004_english_load_matches_packaged_rule_count` | NEW | `node --test tests/rulebook-system-composition.test.mjs` |
| BD-005 本地化叶子同样完整 | `tests/rulebook-system-composition.test.mjs` `SYSTEM_003_zh_cn_leaf_load_is_complete_and_nonempty` | NEW | `node --test tests/rulebook-system-composition.test.mjs` |
| BD-006 chronicle 参数合同（entry+tip 必需） | `tests/codec.test.mjs` `ENFORCER_023_missing_tip_fails` / `ENFORCER_023_empty_tip_fails` / `ENFORCER_022_text_and_evidence_are_reserved_not_tips` / `ENFORCER_022_has_valid_text_requires_nonempty_text` | MOVE | `node --test tests/codec.test.mjs` |
| BD-007 tip 精确映射，无 fuzzy | `tests/codec.test.mjs` `ENFORCER_023_unknown_tip_fails` / `ENFORCER_021_valid_field_maps_exact_rule_id` / `ENFORCER_021_tip_trims_whitespace_before_lookup` / `ENFORCER_024_fuzzy_or_misspelled_tip_is_not_mapped` | MOVE | `node --test tests/codec.test.mjs` |
| BD-008 无 score path | `tests/codec.test.mjs` `ENFORCER_024_extra_numeric_properties_are_ignored`；`tests/catalog.test.mjs` `ENFORCER_170_no_bridge_fields_on_rule`；REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_03_04_facade_surface_has_tip_not_numeric_scores` | MOVE + REUSE | `node --test tests/codec.test.mjs` |
| BD-009 provider run 恰好一次 `chronicle` | REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs` `ENFORCER_042_multi_call_first_terminal_issues_one_nudge_and_does_not_commit` / `ENFORCER_042_multi_call_same_terminal_reentry_is_idempotent` / `ENFORCER_042_second_multi_call_terminal_after_nudge_triggers_aabb`；`tests/cycle-nudge.test.mjs` `ENFORCER_042_domain_has_no_multi_call_merge_surface` | REUSE + MOVE | `node --test requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs tests/cycle-nudge.test.mjs` |
| BD-010 Cycle provider-run 身份 fail-closed | `tests/identity-fail-closed.test.mjs` `ENFORCER_043_no_provable_provider_run_fails_closed`；`tests/cycle-nudge.test.mjs` `ENFORCER_043_valid_cycle_requires_nonempty_text` | MOVE | `node --test tests/identity-fail-closed.test.mjs tests/cycle-nudge.test.mjs` |
| BD-011 fail-closed 内容硬界（512KiB/128KiB） | `tests/bounds.test.mjs` `ENFORCER_043_canonical_text_over_512KiB_fails_closed` / `ENFORCER_043_canonical_evidence_over_128KiB_fails_closed` / `ENFORCER_042_bound_constants_match_utf8_byte_thresholds` | MOVE | `node --test tests/bounds.test.mjs` |
| BD-012 BlogObservationCommitted 唯一原子事实 | `tests/observation-projection.test.mjs` `OBS_PROJ_002_zip_recent_tips_with_blog_frame_digests`；REUSE `requirements/behavior-diagnosis/tests/blogger-cycle-atomic-fact.test.mjs` `C0_no_EnforcementCycleCommitted_fact`；REUSE `requirements/context-compression/tests/enforcer-cycle-convergence.test.mjs` `ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage`（SPLIT@cutover：convergence 链归 context-compression 后，锚点随文件迁移） | MOVE + REUSE | `node --test tests/observation-projection.test.mjs` |
| BD-013 Coverage 严格推进门（出生门 + PERSIST-010 precheck） | `tests/coverage-birth-gate.test.mjs` `ENFORCER_045_mainContext_refuses_when_next_sequence_cannot_advance` / `ENFORCER_045_mainContext_refuses_unmapped_next_cursor` / `ENFORCER_045_mainContext_accepts_strict_advance`；REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-commit-branches.test.mjs` `ENFORCER_precheck_stale_ingest_abandons_then_catchup` / `ENFORCER_precheck_cutoff_mismatch_abandons` / `ENFORCER_precheck_epoch_mismatch_after_squash_abandons` | MOVE + REUSE | `node --test tests/coverage-birth-gate.test.mjs` |
| BD-014 每 cycle 恰好一个 tip；RecentTips 有界 8、oldest→newest、重放幂等 | REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_08_each_committed_cycle_records_exactly_one_tip` / `ENFORCER_TIP_09_replay_preserves_tip` / `ENFORCER_TIP_10_recent_tips_cap_at_8` / `ENFORCER_TIP_11_recent_tips_order_oldest_to_newest`（SPLIT@cutover：本断言族拆入本包） | REUSE | `node --test requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` |
| BD-015 tip↔frame 配对，禁平行流 | `tests/observation-pair.test.mjs` `RULEBOOK_OBS_001_zip_equal_length_pairs_tip_then_frame` … `RULEBOOK_OBS_008_workLogFromUnits_uses_unit_digests`；`tests/observation-projection.test.mjs` `OBS_PROJ_001/004` | MOVE | `node --test tests/observation-pair.test.mjs` |
| BD-016 历史压缩不创造新 occurrence | `tests/observation-projection.test.mjs` `OBS_PROJ_003_squash_co_moves_tips_and_frames_as_observation`；REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_12_squash_co_truncates_recent_tips`（squash 调度语义归 context-compression，本包锁 co-move 不造事件）；REUSE `requirements/behavior-diagnosis/tests/paired-history-eval.test.mjs` `A42_PAIRED_HISTORY_001..004` | MOVE + REUSE | `node --test tests/observation-projection.test.mjs` |
| BD-017 无效 cycle 有界协议修复（0/2+ chronicle、request+terminal scoped nudge/AABB、generic fallback 不抢 AABB、abandoned claim 不冒充 issued） | REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs` `ENFORCER_060_pure_prose_first_issues_interaction_nudge_not_aabb` / `ENFORCER_060_pure_prose_same_terminal_reentry_does_not_aabb` / `ENFORCER_060_same_terminal_reentry_after_aabb_is_idempotent` / `ENFORCER_061_same_empty_text_terminal_reentry_after_aabb_is_idempotent` / `ENFORCER_042_second_multi_call_terminal_after_nudge_triggers_aabb`；`requirements/behavior-diagnosis/tests/enforcer-153-rejudge.test.mjs` `ENFORCER_153_repairState_old_claim_new_terminal_is_nudge_with_claimed_run`（同 LogicalRun 不同 BloggerRequest 隔离 + Abandoned AABB 不恢复为 issued）/ `ENFORCER_153_snapshot_rejudge_recognizes_exactly_one_completed_chronicle` / `ENFORCER_153_hot_path_aabb_preserves_target_terminal_identity`；`requirements/behavior-diagnosis/tests/integration/blogger-nudge-plugin-repro.test.mjs` `REPRO_blogger_pure_prose_terminal_idle_should_nudge_without_another_transform` / `REPRO_blogger_aabb_is_sent_even_when_generic_fallback_reaches_exhaustion_on_that_failure`（通用 cursor 11→12 仍先真实发送 AABB，AABB 后新的 invalid terminal 才 fatal）/ `REPRO_blogger_second_prose_terminal_idle_spends_aabb_not_second_nudge` | REUSE + ADD | `node --test requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs requirements/behavior-diagnosis/tests/enforcer-153-rejudge.test.mjs requirements/behavior-diagnosis/tests/integration/blogger-nudge-plugin-repro.test.mjs` |

## semantic anchor id

本包不拥有任何 `scripts/checks/semantic-anchors.mjs` anchor id（catalog 只有
ROLE_SEMANTIC_ANCHORS / TOOL_DESCRIPTION_ANCHORS / OFFICE_CAPABILITY_ANCHORS，
归属 cognitive-environment / action-affordance / office-capability 等包）。

## cutover 待办（SPLIT@cutover）

- `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs`：拆出 BD-014/BD-016 断言族
  入本包；其余（ENFORCER_TIP_13/14 呈现与 anti-repeat）归 `guidance-delivery`，
  squash 部分归 `context-compression`。
- `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`、
  `enforcer-cycle-commit-branches.test.mjs`：convergence 生命周期部分随
  `context-compression` 迁移；本包锚点（BD-012/013/017）随文件迁移后更新路径。
- `requirements/behavior-diagnosis/tests/observation-pair.test.mjs` 已在本轮 MOVE（fable-library
  直连 import 已改为 support `toList`，test-boundary baseline 相应减 1 条）。
