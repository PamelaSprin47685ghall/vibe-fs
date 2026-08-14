# PROOF — provider-projection

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 物理移入本包；`REUSE` = 留在原处（多-owner，
> cutover 时按 `SPLIT@cutover` 拆分）；`NEW` = 本包新写。
> 运行命令统一为 `node --test <file>`（在仓库根执行）。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PROVIDER-PROJECTION-001（投影是代数；禁直接改 Message list） | REUSE `requirements/provider-projection/tests/projection-algebra.test.mjs` `PROJ_004_renderer_maps_intents_to_writeback_instructions`（intent → writeback 指令，无直改路径） | REUSE | `node --test requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-002（不可变 ProjectionSnapshot） | REUSE 同上 `PROJ_002_the_snapshot_is_the_attempt_local_input_contract` + `PROJ_002_the_committed_prefix_in_the_snapshot_drives_the_prefix_decision` | REUSE | 同上 |
| PROVIDER-PROJECTION-003（Semantic ≠ Wire，禁隐式互转） | REUSE 同上 `PROJ_004_the_wire_view_and_the_written_back_bytes_decode_to_the_same_digest_input` + `PROJ_004_the_written_back_bytes_are_the_frozen_prefix_shape`（wire 视图↔写回字节同 digest 输入） | REUSE | 同上 |
| PROVIDER-PROJECTION-004（三层结构） | REUSE 同上 `PROJ_004_*` 族（renderer 只 fold intent）+ `PROJ_006_prefix_intents_are_mutually_exclusive_at_the_same_anchor`（planner 纯函数判冲突） | REUSE | 同上 |
| PROVIDER-PROJECTION-005（只声明 intent；固定集合） | REUSE 同上 `PROJ_008_step3a_*` 族（九 intent Domain 代数 + canonical rank）+ `PROJ_008_step3b_InsertBlogFrames_digest_equiv_to_CompanionProjectionBuilder`（renderer 单形状源） | REUSE | 同上 |
| PROVIDER-PROJECTION-006（canonical order + 显式冲突；禁注册顺序） | REUSE 同上 `PROJ_006_prefix_intents_are_mutually_exclusive_at_the_same_anchor` / `PROJ_006_two_activations_of_the_same_anchor_are_a_conflict` / `PROJ_008_step3a_plan_is_permutation_independent` / `PROJ_008_step3a_canonical_order_is_rank_sorted_regardless_of_input_order` / `PROJ_008_step3a_*_fail_closed`（各冲突族） | REUSE | 同上 |
| PROVIDER-PROJECTION-007（DSL 不负责生命周期） | REUSE 同上 `PROJ_002_the_snapshot_is_the_attempt_local_input_contract`（纯快照进→投影出，无生命周期输入）；结构性证明见 `archive/docs/how/projection.md`（Coordinator 外置副作用） | REUSE | 同上 |
| PROVIDER-PROJECTION-008（SyntheticToml 唯一 owner；无 parser） | `requirements/provider-projection/tests/synthetic-toml.test.mjs`（MOVE）`ARCH_010_*` 全族（字符串规则/值树/文档布局/round-trip parseability/byteCount）+ `ARCH_011_renderer_exposes_no_parser`（NEW 追加） | MOVE + NEW | `node --test requirements/provider-projection/tests/synthetic-toml.test.mjs` |
| PROVIDER-PROJECTION-009（instruction/data plane 由消费语义决定） | REUSE `requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs` `MISC_join_render_batch_agent_completed_natural_language_and_work_record`（work record → entry-local comment）/ `MISC_join_render_batch_empty_work_record_no_comment`；REUSE `requirements/provider-projection/tests/blogger-toml.test.mjs` `CTX_013_*`（data-only delta） | REUSE | `node --test requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs` / `node --test requirements/provider-projection/tests/blogger-toml.test.mjs` |
| PROVIDER-PROJECTION-010（representation 不反向创造 authority/state） | `requirements/provider-projection/tests/synthetic-toml.test.mjs` `ARCH_011_renderer_exposes_no_parser`（NEW：结构上无读回通道）；REUSE `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` `H13_05_missing_anchor_pair_is_omitted_not_relocated`（synthetic pair 不冒充真实消息） | NEW + REUSE | 见各文件行 |
| PROVIDER-PROJECTION-011（semantic equality ≠ wire equality；digest 从 Semantic 算） | REUSE `requirements/context-compression/tests/companion-projection.test.mjs`（SPLIT@cutover 后归本包）`COMPANION_007_canonical_digest_uses_semantic_projection_not_toml`；REUSE `requirements/provider-projection/tests/projection-algebra.test.mjs` `PROJ_004_the_wire_view_and_the_written_back_bytes_decode_to_the_same_digest_input` | REUSE | `node --test requirements/context-compression/tests/companion-projection.test.mjs`（当前物理位置） |
| PROVIDER-PROJECTION-012（确定性 renderer：同输入同 bytes） | `requirements/provider-projection/tests/synthetic-toml.test.mjs`（MOVE）`ARCH_010_identical_input_renders_byte_identical_output` / `ARCH_010_CRLF_and_lone_CR_normalise_to_LF` / `ARCH_010_byteCount_measures_UTF8_not_characters`；REUSE 同上 `PROJ_008_step3a_plan_is_permutation_independent` | MOVE + REUSE | 见各文件行 |

## MOVE 记录

| 源 → 目标 | 适配 | 验证 |
|---|---|---|
| `requirements/provider-projection/tests/synthetic-toml.test.mjs` → `requirements/provider-projection/tests/synthetic-toml.test.mjs` | `../support/domain.mjs` → `../../../requirements/verification-system/tests/support/domain.mjs`；追加 `ARCH_011_renderer_exposes_no_parser` | `node --test` 25/25 绿 |

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
