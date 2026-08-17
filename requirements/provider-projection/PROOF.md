# PROOF — provider-projection

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 物理移入本包；`REUSE` = 留在原处（多-owner，
> cutover 时按 `SPLIT@cutover` 拆分）；`NEW` = 本包新写。
> 运行命令统一为 `node --test <file>`（在仓库根执行）。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PROVIDER-PROJECTION-001（投影是代数；禁直接改 Message list） | REUSE `requirements/provider-projection/tests/projection-algebra.test.mjs` `WHAT[PROVIDER-PROJECTION-001] PROJ_004_renderer_maps_intents_to_writeback_instructions`（intent → writeback 指令，无直改路径） | REUSE | `node --test requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-002（不可变 ProjectionSnapshot） | REUSE `requirements/provider-projection/tests/projection-algebra.test.mjs` `WHAT[PROVIDER-PROJECTION-002] PROJ_002_the_snapshot_is_the_attempt_local_input_contract` + `WHAT[PROVIDER-PROJECTION-002] PROJ_002_the_committed_prefix_in_the_snapshot_drives_the_prefix_decision` | REUSE | 同上 |
| PROVIDER-PROJECTION-003（Semantic ≠ Wire，禁隐式互转） | REUSE 同上 `WHAT[PROVIDER-PROJECTION-003] PROJ_004_the_wire_view_and_the_written_back_bytes_decode_to_the_same_digest_input` + `WHAT[PROVIDER-PROJECTION-003] PROJ_004_the_written_back_bytes_are_the_frozen_prefix_shape`（wire 视图↔写回字节同 digest 输入）；codec 面（Semantic/Wire 分层的行为面）：`requirements/provider-projection/tests/projection.test.mjs` `WHAT[PROVIDER-PROJECTION-003] MISC_projection_*` 全族、`requirements/provider-projection/tests/misc-codecs-host-wire.test.mjs` `WHAT[PROVIDER-PROJECTION-003] MISC_*` 全族、`requirements/provider-projection/tests/tool-host-codec-full.test.mjs` `WHAT[PROVIDER-PROJECTION-003] CODEC_looks_like_handle_id_shape` / `CODEC_digest_is_true_fnv1a_32bit` | REUSE | 各文件 `node --test` |
| PROVIDER-PROJECTION-004（三层结构） | REUSE 同上 `WHAT[PROVIDER-PROJECTION-004] PROJ_004_physical_prefix_renders_the_messages_unchanged` / `PROJ_004_synthetic_prefix_prepends_the_memory_and_drops_the_physical_cutoff` / `PROJ_004_a_cutoff_beyond_the_message_view_fails_closed` / `PROJ_004_writeback_preserves_the_tail_objects_verbatim`（renderer 只 fold intent，Host 适配写回原语）；`requirements/provider-projection/tests/projection.test.mjs` `WHAT[PROVIDER-PROJECTION-004] MISC_projection_prepend_companion_memory` / `MISC_projection_apply_rendered_prefix_both_shapes` | REUSE | 各文件 `node --test` |
| PROVIDER-PROJECTION-005（只声明 intent；固定集合） | REUSE 同上 `WHAT[PROVIDER-PROJECTION-005] PROJ_008_step3a_InsertBlogFrames_smoke_inserts_assistant_frame_bodies` / `PROJ_008_step3a_InsertRepair_smoke_appends_user_repair_instruction` / `PROJ_008_step3a_SuppressTransportOnly_smoke_drops_transport_message_ids` / `PROJ_008_step3a_AppendReviewChallenge_smoke_appends_challenge_text` / `PROJ_008_step3a_ReanchorAfterCompaction_smoke_is_wire_noop` / `PROJ_008_step3a_empty_BlogFrames_is_render_noop`（九 intent Domain 代数 + canonical rank）；`requirements/provider-projection/tests/strength-projection-algebra.test.mjs` `WHAT[PROVIDER-PROJECTION-005] STRENGTH_009_016_projection_exposes_strength_intent_constructors`；`requirements/provider-projection/tests/tool-host-codec-full.test.mjs` `WHAT[PROVIDER-PROJECTION-005] CODEC_*` 全族（工具参数/schema DSL 不直改消息） | REUSE | 各文件 `node --test` |
| PROVIDER-PROJECTION-006（canonical order + 显式冲突；禁注册顺序） | REUSE 同上 `WHAT[PROVIDER-PROJECTION-006] PROJ_006_prefix_intents_are_mutually_exclusive_at_the_same_anchor` / `PROJ_006_two_activations_of_the_same_anchor_are_a_conflict` / `PROJ_008_step3a_plan_is_permutation_independent` / `PROJ_008_step3a_canonical_order_is_rank_sorted_regardless_of_input_order` / `PROJ_008_step3a_*_fail_closed`（各冲突族）；`requirements/provider-projection/tests/strength-projection-algebra.test.mjs` `WHAT[PROVIDER-PROJECTION-006] STRENGTH_009_mirror_conflicts_with_normal_work_base_selection` / `STRENGTH_008_009_multiple_promoted_absolute_anchors_are_registration_order_independent`（Strength 专属冲突律的注册顺序无关半边） | REUSE | 各文件 `node --test` |
| PROVIDER-PROJECTION-007（DSL 不负责生命周期） | REUSE `requirements/provider-projection/tests/projection-algebra.test.mjs` `WHAT[PROVIDER-PROJECTION-007] PROJ_007_projection_pipeline_owns_no_lifecycle_verbs`（NEW contract test：四投影模块 public 面无生命周期动词，结构性守卫）；结构性证明见历史 how/projection 条款（Coordinator 外置副作用） | REUSE + NEW | 同上 |
| PROVIDER-PROJECTION-008（SyntheticToml 唯一 owner；无 parser） | `requirements/provider-projection/tests/synthetic-toml.test.mjs`（MOVE）`WHAT[PROVIDER-PROJECTION-008] ARCH_010_*` 全族（字符串规则/值树/文档布局/round-trip parseability/byteCount）；`requirements/provider-projection/tests/synthetic-toml-surface.test.mjs` `WHAT[PROVIDER-PROJECTION-008] P6_TOML_SURFACE_*`（registered surface 上的字符串规则/布局）；`requirements/provider-projection/tests/blogger-toml.test.mjs` `WHAT[PROVIDER-PROJECTION-008] ARCH_010_a_payload_shaped_like_TOML_stays_inside_an_item_value`；`requirements/provider-projection/tests/tool-host-codec-full.test.mjs` `WHAT[PROVIDER-PROJECTION-008] CODEC_toml_object_renders_scalar_fields` / `CODEC_toml_object_renders_nested_table` / `CODEC_toml_table_renders_array_of_tables` | MOVE + REUSE | 各文件 `node --test` |
| PROVIDER-PROJECTION-009（instruction/data plane 由消费语义决定） | REUSE `requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs` `WHAT[PROVIDER-PROJECTION-009] MISC_join_render_batch_empty_work_record_no_comment` / `MISC_join_render_batch_child_to_parent_lwr_stays_entry_local_comment`；跨包 REUSE `requirements/delegation/tests/join-v2-wire.test.mjs` `WHAT[DELEG-013] EXEC_004_child_to_parent_lwr_is_hashed_comment_not_toml_field` / `EXEC_004_work_record_lines_are_hash_prefixed_including_malicious`（子→父 `# LWR`）；跨包 REUSE `requirements/delegation/tests/fork-child-payload.test.mjs` `WHAT[DELEG-019] FORK_CHILD_PAYLOAD_commissioner_lwr_is_toml_field_not_hashed_instructions`（父→子 data field）；`requirements/provider-projection/tests/blogger-toml.test.mjs` `WHAT[PROVIDER-PROJECTION-009] CTX_013_*`（data-only delta 无 comment + 显式 instruction header）+ `requirements/provider-projection/tests/blogger-toml-surface.test.mjs` `WHAT[PROVIDER-PROJECTION-009] P6_BLOGGER_SURFACE_*` 全族 + `requirements/provider-projection/tests/tool-host-codec-full.test.mjs` `WHAT[PROVIDER-PROJECTION-009] CODEC_toml_object_with_instructions_prepends_them` | REUSE | 各文件 `node --test` |
| PROVIDER-PROJECTION-010（representation 不反向创造 authority/state） | `requirements/provider-projection/tests/synthetic-toml.test.mjs` `WHAT[PROVIDER-PROJECTION-010] ARCH_011_renderer_exposes_no_parser` + `requirements/provider-projection/tests/synthetic-toml-surface.test.mjs` `WHAT[PROVIDER-PROJECTION-010] P6_TOML_SURFACE_renderer_exposes_no_parser`（NEW：结构上无读回通道）；`requirements/provider-projection/tests/pair-thought-transform.test.mjs` `WHAT[PROVIDER-PROJECTION-010] C_PH_cursor_*` 全族（ordinary wire = synthetic `skill({ name: "" })` + `<skill_content name="">`；Cursor 不造 synthetic 冒充消息，把同一 skill-content payload 以 NUL+BOM 骑真实 tool result）；跨包 REUSE `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` `WHAT[PREFIX-STABILITY-010] H13_05_missing_anchor_pair_is_omitted_not_relocated`（synthetic pair 不冒充真实消息） | NEW + REUSE | 见各文件行 |
| PROVIDER-PROJECTION-011（semantic equality ≠ wire equality；digest 从 Semantic 算） | REUSE 同上 `WHAT[PROVIDER-PROJECTION-011] PROJ_003_semantic_equality_ignores_wire_ids_but_wire_bytes_differ`（NEW contract test：同语义跨 callID 同 semantic 投影、wire bytes 不同）；跨包 REUSE `requirements/context-compression/tests/companion-projection.test.mjs`（SPLIT@cutover 后归本包）`WHAT[CONTEXT-COMPRESSION-012] COMPANION_007_canonical_digest_uses_semantic_projection_not_toml` | REUSE + NEW | `node --test requirements/context-compression/tests/companion-projection.test.mjs`（当前物理位置） |
| PROVIDER-PROJECTION-012（确定性 renderer：同输入同 bytes） | `requirements/provider-projection/tests/synthetic-toml.test.mjs`（MOVE）`WHAT[PROVIDER-PROJECTION-012] ARCH_010_identical_input_renders_byte_identical_output` / `ARCH_010_CRLF_and_lone_CR_normalise_to_LF` / `ARCH_010_byteCount_measures_UTF8_not_characters` / `ARCH_010_byteCount_agrees_with_the_platform_encoder`；`requirements/provider-projection/tests/synthetic-toml-surface.test.mjs` `WHAT[PROVIDER-PROJECTION-012] P6_TOML_SURFACE_byte_count_measures_utf8_not_characters`；`requirements/provider-projection/tests/blogger-toml.test.mjs` `WHAT[PROVIDER-PROJECTION-012] CTX_013_identical_input_renders_byte_identical_output` / `CTX_013_document_ends_with_exactly_one_LF` / `CTX_013_an_empty_document_is_empty_not_a_bare_newline` / `CTX_013_no_timestamps_or_host_ids_are_emitted` | MOVE + REUSE | 各文件 `node --test` |

## MOVE 记录

| 源 → 目标 | 适配 | 验证 |
|---|---|---|
| `requirements/provider-projection/tests/synthetic-toml.test.mjs` → `requirements/provider-projection/tests/synthetic-toml.test.mjs` | 直接引入所有者模块；追加 `ARCH_011_renderer_exposes_no_parser` | `node --test` 25/25 绿 |

## SPLIT@cutover（REUSE 项拆 owner 计划）

- `requirements/provider-projection/tests/projection-algebra.test.mjs`：
  - provider-projection 拿走：`PROJ_004/005/006/008` 全部代数断言（order/merge/conflict/
    permutation/deterministic render）。
  - 留给其它 owner：`CTX_011_step5_*`（→ `prefix-stability`）、feature 生产字节合同
    （Repair → interaction-authority、Challenge → review-assurance、BlogFrames →
    context-compression、Strength → speculative-investigation）按断言拆。
- `requirements/context-compression/tests/companion-projection.test.mjs`（当前物理在
  context-compression，teammate 已移动）：
  - `COMPANION_007_canonical_digest_uses_semantic_projection_not_toml` 断言归属
    provider-projection（PROOF-MAP 第 90 行「projection-algebra.test.mjs 保留 algebra
    oracle；COMPANION-007 canonical digest → provider-projection」）——cutover 时物理移入
    本包 tests/。
- `requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs`：join renderer 的 work-record 语义 →
  `work-record`；「entry-local comment 分类」→ provider-projection（009）。
- `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs`：prefix 字节/anchor replay → 
  `prefix-stability`；「synthetic pair 不冒充真实消息」的 authority 边界 → provider-projection（010）。

## 本包拥有的 semantic anchor id

**0 个。** provider-projection 不拥有 `ROLE_SEMANTIC_ANCHORS` / `TOOL_DESCRIPTION_ANCHORS`
/ `OFFICE_CAPABILITY_ANCHORS` 中的语义 id；它的 proof 是 algebra/renderer/digest 行为
断言（projection-algebra、synthetic-toml、companion-projection COMPANION_007）。