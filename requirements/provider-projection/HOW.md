# provider-projection — HOW

## 架构与实现机制

1. **两意图通用投影代数**：
   - `ProjectionSnapshot` 只携带本次 attempt 的 `CurrentProjection`；provider-projection 不保存 prefix、blog、repair、transport 或 lifecycle feature state。
   - `ProjectionIntent` 只有 `ReplaceMessageBase` 与 `InsertMessageRows`。前者用带 Host metadata 的 rows 确定性替换 base；后者只在 `Append` 或 `BeforeMessageIndex` 绝对锚点插入 rows。
   - `ProjectionPlanner.plan` 按 base → anchored rows 的 canonical order 去重并 fail-closed 判冲突；`ProjectionRenderer.renderMessagesWithHostIds` 只物化 generic rows，产出对齐的 wire messages、Host ids 与 physical flags。

2. **语义视图与 Wire 视图分离**：
   - `ProviderSemanticProjection` 剥离易失传输元数据，提供跨会话一致的语义等价视图，作为 `CanonicalDigest` 计算的唯一输入。
   - `ProviderWireProjection` 在语义视图之上补充合成 ID 与本地时间线标记，服务于前缀缓存与物理传输。
   - `ProviderWireDecode` / `ProviderWireCapture` 是 raw Host → generic wire/capture 的唯一 decode family；`ProviderProjectionSurface` 是包含这些物理读取/写回操作的 resource surface，不冒充纯代数。
   - `ProjectionMessageEdit.replacePrefixByHostIds`、`suppressHostMessagesByIds` 与 `tryApplyRenderedInsertionsPreservingBase` 是当前通用 Host writeback ports；feature policy 留在各消费 owner。
   - `ProjectionMessageEdit.HostWireEncoding` 只发布 `tryEncodeNonToolParts`、`completedToolPart` 与 `rawMessage` 三个物理原语。Strength-owned native tool pairing adapter 消费该 port；provider-projection 不拥有 Strength 决策或 API。

3. **LlmFacing 语义边界 + SyntheticToml 字节 writer**：
   - `Foundation/LlmFacing.fs` 是所有 LLM-facing 合成内容的唯一 production API。调用方只构造 `LlmFacing.Document`，显式把内容归为 instruction 或 reference data，并在最后一次性 render。
   - `Foundation/SyntheticToml.fs` 退为 `LlmFacing` 背后的 canonical byte writer，统一管理换行规范化（CRLF → LF）、字符串转义、值树编码与注释排版；feature owner 不直接使用其文档/字段/注释构造 API。
   - document composition 只发生在 typed/structured 阶段。appendix、handoff、batch 等必须合并 instruction/data 集合后再 render，禁止拼 rendered string。
   - 分面按 receiver semantics：对当前 Agent 的责任交接、行动要求、推理约束（包括 child → parent LWR）属于 instruction；仅供参考的事实材料属于 data。
   - 故意不提供业务解析器，确保单向渲染安全。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PROVIDER-PROJECTION-001 | `requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-001] online and replay projection share one canonical generic renderer` |
| PROVIDER-PROJECTION-002 | `requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-002] snapshot contains only the current semantic projection` |
| PROVIDER-PROJECTION-003 | `requirements/provider-projection/tests/projection.test.mjs::WHAT[PROVIDER-PROJECTION-003] retry continuation origin stays in Host metadata, outside provider semantics` |
| PROVIDER-PROJECTION-004 | `requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-004] base replacement preserves every Host metadata channel`；`requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-004] BeforeMessageIndex and Append materialize aligned rows` |
| PROVIDER-PROJECTION-005 | `requirements/provider-projection/tests/message-intent-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-005] generic message intents replace feature-owned constructors`；`requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-005] surface exposes only generic projection constructors` |
| PROVIDER-PROJECTION-006 | `requirements/provider-projection/tests/message-intent-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-006] different generic message bases conflict`；`requirements/provider-projection/tests/message-intent-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-006] generic row insertion is registration-order independent`；`requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-006] base and row intents have canonical permutation-invariant order` |
| PROVIDER-PROJECTION-007 | `requirements/provider-projection/tests/provider-projection-boundary.test.mjs::WHAT[PROVIDER-PROJECTION-007] production provider projection owners are policy-free` |
| PROVIDER-PROJECTION-008 | `requirements/provider-projection/tests/synthetic-toml.test.mjs::WHAT[PROVIDER-PROJECTION-008] ARCH_010_every_rendered_string_parses_back_to_the_value_it_was_given` |
| PROVIDER-PROJECTION-009 | `requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs::WHAT[PROVIDER-PROJECTION-009] MISC_join_render_batch_child_to_parent_lwr_stays_entry_local_comment` |
| PROVIDER-PROJECTION-010 | `requirements/provider-projection/tests/pair-thought-transform.test.mjs::WHAT[PROVIDER-PROJECTION-010] C_PH_cursor_keeps_durable_occurrence_without_synthetic_message` |
| PROVIDER-PROJECTION-011 | `requirements/provider-projection/tests/projection-algebra.test.mjs::WHAT[PROVIDER-PROJECTION-011] PROJ_003_semantic_equality_ignores_wire_ids_but_wire_bytes_differ` |
| PROVIDER-PROJECTION-012 | `requirements/provider-projection/tests/synthetic-toml.test.mjs::WHAT[PROVIDER-PROJECTION-012] ARCH_010_identical_input_renders_byte_identical_output`；`requirements/provider-projection/tests/synthetic-toml.test.mjs::WHAT[PROVIDER-PROJECTION-012] ARCH_010_byteCount_measures_UTF8_not_characters` |
| PROVIDER-PROJECTION-013 | `requirements/provider-projection/tests/llm-facing.test.mjs::WHAT[PROVIDER-PROJECTION-013] LLM_FACING_single_representation_owner_is_hard_gated` |
| PROVIDER-PROJECTION-014 | `requirements/provider-projection/tests/llm-facing.test.mjs::WHAT[PROVIDER-PROJECTION-014] LLM_FACING_composition_stays_typed_until_the_final_render` |
