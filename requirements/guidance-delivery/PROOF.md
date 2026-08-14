# guidance-delivery — PROOF（测试落点表）

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 已物理移入本包 `tests/`（删除原文件，
> 单跑绿）；`REUSE` = 留在原处（多-owner 或不宜移动），记精确锚点 + cutover 拆分计划
> （`SPLIT@cutover`）；`NEW` = 本包新写。运行命令：`node --test <file>`。
> 本包无 semantic-anchors.mjs anchor id（该 catalog 只有 ROLE/TOOL/OFFICE 三组）。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| GD-001 交付前沿 ≠ 语义覆盖（两轴分离） | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_002_second_resolve_same_tip_is_identity_only` / `ENFORCER_TIP_DELIVERY_006_context_reanchor_clears_full_so_next_is_full_again`；`tests/tip-delivery-projection.test.mjs` `TDP_001/002/004/005` | MOVE + NEW | `node --test tests/tip-delivery-projection.test.mjs` |
| GD-002 首次交付 = Full main.md | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_001_first_resolve_is_full_main_md` / `ENFORCER_PROMPT_017_full_tip_guidance_uses_owner_session_zh_cn_rulebook` | MOVE | `node --test tests/tip-guidance-delivery.test.mjs` |
| GD-003 重复交付 = IdentityOnly，不重复全文 | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_002`（`tip: <name>`、不含 main.md、Full 长于 Identity）；`tests/tip-delivery-projection.test.mjs` `TDP_002/003` | MOVE + NEW | `node --test tests/tip-guidance-delivery.test.mjs` |
| GD-004 交付决策只 fold durable facts，restart-safe | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_003_latestTipGuidance_matches_resolve_text`；`tests/latest-tip-nudge.test.mjs` `ENFORCER_TIP_NUDGE_001_latest_tip_first_delivery_is_full_main_md`（二次调用即 Identity，证明判定已 durable 化）；`tests/tip-delivery-projection.test.mjs` `TDP_001/002` | MOVE + NEW | `node --test tests/latest-tip-nudge.test.mjs` |
| GD-005 reanchor：语义恢复 ≠ 新 occurrence | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_006`（reanchor 后必须再 Full）；`tests/tip-delivery-projection.test.mjs` `TDP_004_reanchor_voids_full_history_so_next_resolve_refulls` / `TDP_005_reanchor_does_not_advance_occurrence_frontier` | MOVE + NEW | `node --test tests/tip-delivery-projection.test.mjs` |
| GD-006 owner 解析与 None 语义 | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_004_blogger_session_id_resolves_owner_main` / `ENFORCER_TIP_DELIVERY_005_missing_tip_returns_none`；`tests/latest-tip-nudge.test.mjs` `ENFORCER_TIP_NUDGE_002_missing_recent_tip_returns_none` / `ENFORCER_TIP_NUDGE_003_missing_owner_returns_none` | MOVE | `node --test tests/tip-guidance-delivery.test.mjs` |
| GD-007 `latestTipGuidance`/`latestTipNudge` 同义 | `tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_003`（viaLatest === viaAlias）；`tests/latest-tip-nudge.test.mjs` `ENFORCER_TIP_NUDGE_001` | MOVE | `node --test tests/latest-tip-nudge.test.mjs` |
| GD-008 detection/remediation audience 分离 | `tests/audience-separation.test.mjs` `AUDIENCE_001_main_md_sections_never_enter_blogger_system_prompt` / `AUDIENCE_002_corpus_level_detection_and_remediation_do_not_leak` / `AUDIENCE_003_previous_tip_history_is_not_main_authority`；REUSE `requirements/guidance-delivery/tests/tip-v2-delivery.test.mjs` `ENFORCER_TIP_13_work_record_contains_previous_enforcer_tip_blocks`（SPLIT@cutover：随文件拆分更新路径） | NEW + REUSE | `node --test tests/audience-separation.test.mjs` |
| GD-009 交付不创建 interaction authority | `tests/latest-tip-nudge.test.mjs` `CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text` / `CTX_002_GUIDELINE_002_marker_with_nudge_uses_double_newline`（auto-injected tool pair 机制，非 user message）；`tests/tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_001`（交付形状 = tip header + main.md）；`tests/audience-separation.test.mjs` `AUDIENCE_003` | MOVE + NEW | `node --test tests/latest-tip-nudge.test.mjs` |
| GD-010 检测语料跨 family 冲突门 | REUSE `requirements/guidance-delivery/tests/enforcer-cross-family-collision.test.mjs` `enforcer_collision_documents_scoring_formula` / `enforcer_collision_scorePair_fail_warn_note_clean` / `enforcer_collision_temp_dir_fail_closed_on_extreme_non_siblings`（gate KEEP，本体在 `scripts/checks/enforcer-cross-family-collision.mjs`） | REUSE | `node --test requirements/guidance-delivery/tests/enforcer-cross-family-collision.test.mjs` |
| GD-011 已投递 auto-injected 字节按原文冻结 | `tests/guideline-projection.test.mjs` `GP_002_apply_records_pair_and_restores_marker_bytes` / `GP_003_non_sequential_ordinal_is_rejected` / `GP_004_duplicate_call_id_is_rejected` / `GP_005_duplicate_placement_is_rejected` / `GP_006_replay_restores_pairs_oldest_first`；`tests/latest-tip-nudge.test.mjs` `CTX_002_GUIDELINE_001/002`（marker 正文透传） | NEW + MOVE | `node --test tests/guideline-projection.test.mjs` |

## semantic anchor id

本包不拥有任何 `scripts/checks/semantic-anchors.mjs` anchor id。

## cutover 待办（SPLIT@cutover）

- `requirements/guidance-delivery/tests/tip-v2-delivery.test.mjs`：`ENFORCER_TIP_13/14`（previous
  tip 呈现与 anti-repeat 提示）拆入本包；其余归 `behavior-diagnosis` /
  `context-compression`。
- `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`：nudge/repair 族
  （ENFORCER-060..068）跨 diagnosis/delivery——本包只引用与交付相关的断言
  （见 `behavior-diagnosis/PROOF.md` BD-017 的 SPLIT 记录），cutover 时按
  convergence 文件归属统一迁移。
- 交付路径的「字节级 horizon 探测」（Coverage 精确测量）是未来增强，当前以
  `TipDeliveryProjection.applyReanchor` 近似（见 `HOW.md` §1 诚实性注），
  不改变两轴分离合同。
