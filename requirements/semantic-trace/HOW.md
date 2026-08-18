# semantic-trace — HOW（实现模型与约束；非 normative）

> 本文件解释「当前实现怎么满足 WHAT」，不是第二条规范。命题只在 `WHAT.md`。

## 1. 实现模型

### 1.1 类型层（`src/Wanxiangshu/Domain/XTrace.fs`）

```fsharp
type XTraceCursor = { Sequence: int64 }
type XTraceItem  = { Cursor: XTraceCursor; Provenance: string; Role: string; Part: SemanticPart }
type RecordCoverage = { IngestedThrough: XTraceCursor }
type PrefixCoverage = { HostEpochId; CutoffExclusive; CoveredPrefixDigest; CoverableFrameCount }
```

- `XTrace.nextCursor` / `isAfter` 提供严格单调；`sliceBetween`/`sliceFrom`/`head` 提供半开定位。
- `flatten` 是 SemanticMessage → 带 role 的 part 序列的唯一平铺（SEMANTIC-TRACE-007）。
- `forWorkRecord` 过滤 raw tool（LWR 用）；`forOpening` = identity（Opening 保留一切，SEMANTIC-TRACE-010）。
- `render` 永不输出 Provenance；assistant 正文不带 role 前缀；空列表渲染为空字符串。

### 1.2 durable 投影（`src/Wanxiangshu/Context/Trace/Projection.fs`）

`XTraceProjectionState = { Opening: OpeningMaterial option; Parts: XTracePartRef list; Terminals: XTraceTerminalRef list }`

- `Parts` 存储 newest-first（replay cons O(1)），`parts` 恢复 oldest-first。
- 三个 fold 规则（PERSIST-010）：
  - `applyOpening`：同文本幂等；异文本 `OpeningAlreadyCaptured` 拒绝；
  - `applyPart`：cursor 必须严格大于 head；否则 `CursorNotAfterHead` 拒绝；
  - `applyTerminal`：幂等域 = ProviderRun；同 run + 同 ref/digest no-op，同 run + 不同 terminal 拒绝；
    不同 run 追加独立 occurrence，并冻结当时 exclusive XTrace frontier。reusable child 不覆盖历史 terminal。
- `provenanceGeneration` 解析 `g:N/...`（reanchor 后），legacy `turn:N/part:M` → 0。
- `currentGenerationParts` 只取最新 generation，避免跨 reanchor 混用 Host turn 编号。

### 1.3 捕获链路（`src/Wanxiangshu/Context/Trace/Capture.fs`）

- `semanticPart`：唯一的 `MessagePart → SemanticPart` mapper；`Activity` → `None`（丢弃）。
- `captureSources`：按 provenance `g:N/turn:M/part:P` 幂等 append（recorded 集合去重）。
- `captureSourcesStable`：新 capture 优先按 `g:N/msg:<id>/host-part:<physical-part-id>`；Host 未提供
  physical part id 时才保留 positional fallback。升级前已有 `g:N/msg:<id>/part:P` 通过
  semantic-equivalent legacy slot + exact HostToolPart identity 兼容去重，不能让 index drift 复制 tool
  或吞掉新 materialized part（STRENGTH-008 stable insertion 前提）；legacy turn-positional trace 仍只读、强制 Strength K0。
- `captureGeneration`：generation = `ReanchoredRuns` 集合大小，reanchor 后 +1。
- `captureOpening` / `captureTerminalText` / `captureLastWords`：Opening 与 Terminal 的捕获入口。

### 1.4 fold 接线（`src/Wanxiangshu/Composition/Durable/Fold.fs` + `ContextFactFold.fs`）

- XTrace 事实经 `Fold` 维护（durable-events substrate）。
- `ContextReanchored` 在 `ContextFactFold` 只更新 `PrefixEpoch` / `Blog` / `TipDelivery`，
  **不动 XTrace**（SEMANTIC-TRACE-009 的结构保证）。

## 2. 消费方（为什么其它包依赖本包）

| 消费方 | 消费什么 |
|---|---|
| `work-record` | `XTrace.forOpening` + `forWorkRecord` + `sliceFrom` 物化 LWR |
| `context-compression` | Blogger chunker 的 `SemanticCursor → XTrace cursor` 映射 |
| `prefix-stability` | `CoveredPrefixDigest` 的源语义投影 |
| `review-assurance` / `finality` | review frontier 的 canonical 证据源 |

## 3. 与 `durable-events` 的分工

- 本包不拥有：事件如何编码/落盘/拒绝。`PERSIST-010` 的拒绝规则类型
  （`XTraceFoldRejection`）定义在 `XTraceProjection.fs`，但「拒绝 = 启动失败 vs 幂等吸收」
  的 fold 语义归 `durable-events`（见 `fold-context-recovery.test.mjs` 的注释）。
- 本包拥有：capture 边界、cursor 语义、provenance、frontier/range 合同。

## 4. 已知非目标（HOW 层，不升级为命题）

- `XTracePartRef.Turn/PartIndex` 是 Host semantic 坐标，仅供 writer 把 BlogEntry 的
  SemanticCursor 映射回 XTrace cursor；XTrace cursor 本身独立于它们（`XTraceProjection.fs` 注释）。
- `supportsStableInsertion` 的存在（STRENGTH-008）是 Strength 优化 HOW；「Candidate 永不入迹」
  才是命题（SEMANTIC-TRACE-008）。
- `Provenance` 字符串格式（`g:N/turn:M/part:P` / legacy `g:N/msg:id/part:P` /
  current `g:N/msg:id/host-part:id`）是定位实现，可演进；规范只要求 stable Host capture 不以可漂移数组位置
  冒充 physical identity。

## 5. 历史与弃权

### 5.1 弃权（GARBAGE / 明确不归本包）

- **UI delta / usage / cost / timestamp 的「计量格式」**：HOST-005 只要求它们不进 XTrace；
  它们是否别处该记、怎么记，不是本包命题（journal 诊断有独立 owner）。
- **`TerminalOutputCaptured` 的「私有完成标记」语义**：terminal 不是 LWR 段、不经 Y ——
  这个事实归 `work-record`（WORK-RECORD-011 边界）；本包拥有「terminal 是 XTrace 的
  第三事实族、按 ProviderRun 幂等、绑定捕获时 exclusive frontier」。不同 ProviderRun 即使正文
  相同也必须保留独立 occurrence；同 ProviderRun 的不同正文 fail closed。
- **Host compaction 的预防/收容两层机制**：`HostCompactionPolicy` 的 setting 清单与
  probe verdict 归 `context-compression`；本包只保留「XTrace 不删除」的结果。
- **`WorkActivated` / Birth-Labor 等 legacy 措辞**（GLORY-016/017/023/024 的 GARBAGE 裁决）：
  Opening protection 语义归 `work-record`；本包不重复。

## 6. 依赖理由（DEPENDS ON）

- `durable-events`：XTrace 的 append-only 与可重放必须由不可变事实 + 原子提交 + 确定性 fold
  提供（INDEX.md 当前 109-edge 骨架的唯一 hard edge）。

## 验证与测试落点

### 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| SEMANTIC-TRACE-001（唯一 append-only 历史，Opening/Part/Terminal 三事实） | `tests/x-trace-fold.test.mjs`：`PERSIST_010_opening_is_captured_verbatim_and_idempotent`、`PERSIST_010_opening_preserves_authoritative_requirement_order`、`PERSIST_010_parts_append_in_strict_cursor_order`、`PERSIST_010_terminal_is_captured_once_and_idempotent`、`PERSIST_010_a_second_provider_run_gets_a_distinct_terminal_occurrence_for_reuse`、`PERSIST_010_identical_terminal_bytes_are_fresh_when_provider_run_changes`、`PERSIST_010_one_provider_run_cannot_publish_two_different_terminals`、`PERSIST_010_xtrace_facts_survive_NDJSON_and_still_fold`；`tests/x-trace-capture-hardening.test.mjs`：`COMPANION_007_capture_projection_appends_only_new_turns`、`COMPANION_003_terminal_only_completion_projects_into_recent_work_without_appending_a_trace_part`、`COMPANION_003_last_words_land_in_recent_work_not_closing_report` | MOVE | `node --test requirements/semantic-trace/tests/x-trace-fold.test.mjs requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs` |
| SEMANTIC-TRACE-002（typed capture 边界） | `tests/x-trace-capture.test.mjs`：`COMPANION_012_*`（text/reasoning/tool_call/tool_result 映射、`COMPANION_012_activity_is_dropped_not_mapped`）；`tests/x-trace-capture-boundary.test.mjs`（NEW）：`SEMANTIC_TRACE_capture_boundary_excludes_transport_metadata`、`SEMANTIC_TRACE_appendable_xtrace_facts_are_exactly_three`；`tests/x-trace-fold.test.mjs`：`PERSIST_010_parts_carry_turn_part_and_tool_name`；`tests/x-trace-locality.test.mjs`：`TODO-004 preserves a captured tool call identity on its durable XTrace range`、`TODO-004 captures the SDK-visible assistant run and Host ToolPart without index inference`、`stable snapshot capture keys one physical Host part independently of later semantic index drift` | MOVE + NEW | `node --test requirements/semantic-trace/tests/x-trace-capture.test.mjs requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs requirements/semantic-trace/tests/x-trace-locality.test.mjs` |
| SEMANTIC-TRACE-003（cursor 严格单调、独立于 Host 坐标） | `tests/x-trace.test.mjs`：`XTRACE_cursor_is_strictly_monotonic`；`tests/x-trace-fold.test.mjs`：`PERSIST_010_a_duplicate_cursor_is_refused`、`PERSIST_010_a_retreating_cursor_is_refused` | MOVE | `node --test requirements/semantic-trace/tests/x-trace.test.mjs requirements/semantic-trace/tests/x-trace-fold.test.mjs` |
| SEMANTIC-TRACE-004（provenance 按 provider run 分段） | `tests/x-trace-capture-hardening.test.mjs`：`COMPANION_007_capture_projection_provenance_is_stored_verbatim`、`HOST_006_capture_projection_after_reanchor_uses_next_generation`；`tests/x-trace-provider-run-provenance.test.mjs`（NEW）：`SEMANTIC_TRACE_provider_run_segments_fold_projection` | MOVE + NEW | `node --test requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs requirements/semantic-trace/tests/x-trace-provider-run-provenance.test.mjs` |
| SEMANTIC-TRACE-005（semantic parts 与 transport identity 分离） | `tests/x-trace.test.mjs`：`XTRACE_render_is_deterministic_and_never_emits_provenance`；`tests/x-trace-capture.test.mjs`：`COMPANION_012_tool_call_drops_the_call_id`、`COMPANION_012_tool_result_drops_the_call_id` | MOVE | `node --test requirements/semantic-trace/tests/x-trace.test.mjs requirements/semantic-trace/tests/x-trace-capture.test.mjs` |
| SEMANTIC-TRACE-006（稳定 frontier / range / cutoff） | `tests/x-trace.test.mjs`：`XTRACE_slice_between_is_half_open_and_order_preserving`、`XTRACE_slice_from_takes_suffix_to_head`、`XTRACE_head_is_after_last_item_and_origin_for_empty`；`tests/x-trace-locality.test.mjs`：`TODO-004 joins the persisted ToolPart to its exact durable XTrace range`、`duplicate legacy captures of one identical physical tool collapse to its first durable cursor`、`conflicting legacy captures of one physical tool remain ambiguous`、`TODO-004 localizes a pending before-hook ToolPart from snapshot before XTrace capture`、`TODO-004 pending empty todowrite stubs are not semantic sibling calls`、`TODO-004 a populated sibling todowrite remains a real protocol sibling`、`TODO-004 pending before-hook ReviewFrontier includes last assistant text in the same message`、`TODO-004 pending ReviewFrontier does not double-count a current-message prefix already captured in XTrace`、`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite`；`requirements/obligation-ledger/tests/magic-todo-after.test.mjs` 交叉锁定 before-hook provisional boundary 与 assignment-time exact Host ToolPart/XTrace re-proof 分离；`requirements/work-record/tests/lifecycle-work-record.test.mjs`（work-record 包）：`LWR_gap_starts_at_record_coverage_not_prefix_cutoff`（跨包交叉） | MOVE + ADD | `node --test requirements/semantic-trace/tests/x-trace.test.mjs requirements/semantic-trace/tests/x-trace-locality.test.mjs` |
| SEMANTIC-TRACE-007（单一 source，Y delta/LWR gap 同源分投影） | `tests/x-trace.test.mjs`：`XTRACE_flatten_is_the_single_semantic_source`、`XTRACE_forWorkRecord_drops_raw_tools_keeps_text_reasoning_media`；`tests/x-trace-capture-hardening.test.mjs`：`COMPANION_007_capture_projection_is_idempotent_across_transforms` | MOVE | `node --test requirements/semantic-trace/tests/x-trace.test.mjs requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs` |
| SEMANTIC-TRACE-008（未发生材料永不写成历史） | `tests/x-trace-capture-boundary.test.mjs`（NEW）：`SEMANTIC_TRACE_appendable_xtrace_facts_are_exactly_three`；REUSE 交叉：`requirements/speculative-investigation/tests/frame-projection.test.mjs` `STRENGTH_008_traced_requires_promotion_and_monotonic_nonempty_range`、`requirements/speculative-investigation/tests/invisibility.test.mjs`；`requirements/context-compression/tests/recovery-slot.test.mjs`（已移至 context-compression 包）`CTX_010_only_the_work_main_request_may_carry_a_prefix_probe` | NEW + REUSE | `node --test requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs` |
| SEMANTIC-TRACE-009（Host compaction 不得删除 XTrace） | `tests/x-trace-compaction-survival.test.mjs`（NEW）：`SEMANTIC_TRACE_reanchor_preserves_xtrace_parts_and_opening`；REUSE 交叉：`requirements/durable-events/tests/fold-context-recovery.test.mjs` `HOST_006_reanchor_retires_the_prefix_and_zeroes_prefix_coverage_in_one_fact`（durable-events fold，reanchor 不动 XTrace 的半边由本包 NEW 测试钉死） | NEW + REUSE | `node --test requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs` |
| SEMANTIC-TRACE-010（Opening preserved） | `tests/x-trace-capture-hardening.test.mjs`：`COMPANION_003_capture_opening_takes_authoritative_requirements`、`COMPANION_003_opening_capture_is_idempotent_for_the_same_text`、`COMPANION_003_parent_work_record_renders_the_opening_exactly_once`；`tests/x-trace-fold.test.mjs`：`PERSIST_010_a_different_opening_is_refused` | MOVE | `node --test requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs requirements/semantic-trace/tests/x-trace-fold.test.mjs` |

### 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/x-trace.test.mjs` | MOVE `requirements/semantic-trace/tests/x-trace.test.mjs` | 已跑绿 |
| `tests/x-trace-capture.test.mjs` | MOVE `requirements/semantic-trace/tests/x-trace-capture.test.mjs` | 已跑绿 |
| `tests/x-trace-capture-hardening.test.mjs` | MOVE `requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs` | 已跑绿 |
| `tests/x-trace-fold.test.mjs` | MOVE `requirements/semantic-trace/tests/x-trace-fold.test.mjs` | 已跑绿 |
| `tests/x-trace-locality.test.mjs` | MOVE `requirements/semantic-trace/tests/x-trace-locality.test.mjs`（XTrace range 可定位 + MagicTodoLocality 交叉） | 已跑绿 |
| `tests/x-trace-capture-boundary.test.mjs` | NEW | 已跑绿 |
| `tests/x-trace-compaction-survival.test.mjs` | NEW | 已跑绿 |
| `tests/x-trace-provider-run-provenance.test.mjs` | NEW | 已跑绿 |

### 3. 单跑命令

```text
node --test requirements/semantic-trace/tests/x-trace.test.mjs
node --test requirements/semantic-trace/tests/x-trace-capture.test.mjs
node --test requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs
node --test requirements/semantic-trace/tests/x-trace-fold.test.mjs
node --test requirements/semantic-trace/tests/x-trace-locality.test.mjs
node --test requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs
node --test requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs
node --test requirements/semantic-trace/tests/x-trace-provider-run-provenance.test.mjs
```

### 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/speculative-investigation/tests/frame-projection.test.mjs` | `STRENGTH_008_traced_requires_promotion_and_monotonic_nonempty_range`（Promoted 才可入 XTrace 的另一半） | SPLIT@cutover：strength 包保留 promotion 侧，本包 NEW 测试钉 capture 侧 |
| `requirements/speculative-investigation/tests/invisibility.test.mjs` | Candidate 不可见性（未入历史） | SPLIT@cutover：同上 |
| `requirements/durable-events/tests/fold-context-recovery.test.mjs` | `PERSIST_010_entry_and_squash_fold_into_the_blog_projection`、`HOST_006_reanchor_*`（fold 语义 = durable-events） | SPLIT@cutover：fold 部分归 durable-events；reanchor 的 XTrace 存活半边由本包 NEW 测试承担 |
| ~~`tests/unit/enforcer/blogger-convergence-gaps.test.mjs`~~、~~`tests/unit/enforcer/blogger-crash-recovery.test.mjs`~~ | Blogger delta 从 XTrace 消费的收敛（capture 侧同源；已分别迁 context-compression / crash-reconciliation） | SPLIT@cutover：enforcer/behavior-diagnosis 保留协议面；本包引用同源事实 |
| `tests/x-trace-locality.test.mjs`（本包）的 `TODO-008 ManagerCheckpointLWR range` 锚点 | work-record 交叉 | 已在包内文件保留（该文件本包 MOVE） |

### 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（XTrace 语义由 F# 类型 + fold
测试承担；锚点散文属 2026-08-14 cutover 迁移范围）。若 cutover 后需要散文 canary，建议在
`semantic-anchors.mjs` 增加 `SEMANTIC_TRACE_*` 锚点并声明 owner 为本包（见 §6 协调备注）。
