# work-record — HOW（实现模型与约束；非 normative）

## 1. 实现模型

### 1.1 类型层（`src/Wanxiangshu/Domain/LifecycleWorkRecord.fs`）

```fsharp
type OpeningMaterial =
    { AssignmentText: string            // InitialCharge（OpeningPromptCaptured 的 inline 副本）
      AuthoritativeRequirements: string list
      ConstitutiveBody: string }        // BlindPlan 区间（经 XTrace.forOpening 渲染）；Immediate 为空

type LifecycleWorkRecord =
    { Opening: OpeningMaterial          // 永远保留（WORK-RECORD-006）
      Frames: string list               // Chronicle = 已解析的 Y frame 文本
      Gap: XTraceItem list }            // Recent work = 未覆盖 suffix（须已 forWorkRecord）
```

- `OpeningPolicy.immediate` / `forManager = BlindPlan FirstPlanCompleteTodoWrite`；Manager Opening 只在第一次 accepted `planComplete=true` 后关闭。
- `render includeOpening record`：三段纯文本 Markdown；空段整段省略；`includeOpening=false`
  省略 Opening；段标题为纯文本 `Opening` / `Chronicle` / `Recent work`，`# ` 仅由
  `SyntheticToml.comment` 在 wire 注入（避免 `# # Chronicle`）。
- `materialize opening frames trace coverage openingEnd includeOpening`：
  `gapStart = { Sequence = max coverage.IngestedThrough.Sequence openingEnd.Sequence }`；
  gap = `XTrace.sliceFrom gapStart trace |> XTrace.forWorkRecord`（WORK-RECORD-005/013）。
- `withConstitutive`：把 BlindPlan constitutive 区间渲染进 `ConstitutiveBody`（WORK-RECORD-009）。

### 1.2 bounded range（`src/Wanxiangshu/Domain/MagicTodoLwr.fs`）

```fsharp
type BoundedRange = { StartInclusive: XTraceCursor; EndExclusive: XTraceCursor }
```

一个 invocation / request 的排他 XTrace 范围（EXEC-031）。Start 常为 WorkRecordStart /
invocation send head；End 为 ReviewFrontier / invocation completion head。

### 1.3 物化（`src/Wanxiangshu/Application/Finality/LifecycleWorkRecordProjection.fs`）

- `lifecycleWorkRecordFromSnapshot durable snapshot sessionId includeOpening coverageOverride`：
  full-lifecycle 物化。解析 frames（digest 校验失败即丢弃该 frame）、解析 trace parts
  （media_omitted 保留为 omission marker）、`withTerminalFallback` 在最新 assistant turn
  未含 terminal 字节时把 terminal 投影进 Recent work（不写新 XTrace 事实）。
  `openingEnd` 由 `ManagerOpeningFloor.workRecordStart` 推导；无 Life 时 = 第一条 part 之后。
- `lifecycleWorkRecordBoundedFromSnapshot durable snapshot sessionId range`：bounded 物化。
  frames 按 `(Previous, Next]` 与 `[Start, End)` 重叠过滤；trace 按 range slice；
  coverage 夹到 range 内（`max(…, range start)` / `min(…, range end)`）；`includeOpening=false`。
- `lifecycleWorkRecordBoundedFromSnapshotForRun ... providerRun`：completion consumer 使用的 bounded
  物化。只选择 `ProviderRun` 匹配且 terminal occurrence 的 `Frontier = range.EndExclusive` 的
  私有完成证据；自然 assistant parts 尚未含该正文时，把它仅作为 Recent work read-time fallback。
  terminal 不占用 XTrace part cursor，不是第四段，也不会挤压下一 invocation 的首 part。
- frame overlap 的离散区间按 `(CoveredFrom, CoveredThrough] ∩ [Start, End)` 判断；第一可覆盖
  sequence 为 `CoveredFrom+1`，因此 later frame 的首 sequence 恰等于 `End` 时必须排除。
- full 与 bounded 共用 `LifecycleWorkRecord.materialize` —— 单一 renderer（WORK-RECORD-010）。

### 1.4 floor（`src/Wanxiangshu/Mission/Manager/Life/OpeningFloor.fs`）

- `workRecordStart life magic xTrace`：Post-T1 = `MagicTodo.blindPlanOpeningBoundary`
  （首次 true 的 T1 call cursor + callId + part anchors）；此前任意 false planning checkpoints 仍属于 Pre-T1 Opening。
- `effectiveOpeningFloor`：Life 未开 / 已 Completed → None；否则按 acceptedCount 与 T1 anchor
  推导。**从不读** `WorkActivated` / `ProtectedPrefixEnd`（TODO-001 考古）。
- `floorSequence`：session helper，供 BloggerCoordinator / CompanionTransform 的
  effectiveStart = `max(RecordCoverage, floor)`。

## 2. 消费方

| 消费方 | 消费什么 |
|---|---|
| `delegation`（EXEC-004/028/031） | 子→父 / SyncDelegate 的 bounded record（includeOpening=false）。**wire plane 归消费方**：join completed = `# LWR`（`SyntheticToml.comment`）；fork child 首 prompt = TOML field（`commissioner_record` / `attached_work_record`）。本包只物化正文，不决定 plane。 |
| `review-assurance` / `review-judgement`（REVIEW-016） | ProcessReviewLWR（RecordCoverage + RawGap） |
| `finality`（GLORY-004/050） | FinalityReviewLWR（request-range bounded） |
| `obligation-ledger`（TODO-006/008） | ManagerCheckpointLWR（ReviewFrontier） |
| `prefix-stability` / `context-compression` | 只引用 coverage 分型，不把 LWR RawGap 当 prefix 证明 |

## 3. 已知非目标（HOW 层）

- `withTerminalFallback` 是 Host 边界 fallback HOW（consumption 前 terminal 已 durable）；
  「Terminal 不是 LWR 段」才是命题（WORK-RECORD-011）。
- 段标题字面（`Opening` / `Chronicle` / `Recent work`）当前是渲染事实；card 明确
  「当前三段标题字面不必须永久不变」——renderer 可整体重写，只要 boundedness / Opening /
  coverage 分型 / prose-claim 不变。
- `OpeningMaterial.AssignmentText` 是 OpeningPromptCaptured 的 inline 副本：它是 captured
  事实的**物化输入**，不是从 Assignment/requirements 文本重建的第二事实源（WORK-RECORD-008
  禁止的是「reconstruct」，即把 record 的 Opening 当可拼装物）。

## 4. 历史与弃权

### 4.1 弃权（GARBAGE / 明确不归本包）

- **GLORY-016/017/023/024 的「Birth/Labor floor」「Activation 前置」措辞**（COVERAGE GARBAGE 裁决）：
  措辞退役；Opening protection 语义由本包 WORK-RECORD-015 保留，旧 stage 词不升级为命题。
- **`Terminal` 完成标记的捕获机制**：归 semantic-trace（TerminalOutputCaptured 事实）；
  本包只声明「不是 LWR 段」。
- **`exact constants`（如 `InvocationStartCursor` 的具体取值算法）**：是 HOW；命题是
  bounded 语义（WORK-RECORD-016）。
- **`SyncDelegatePromptRequest { Charge; ProviderPrompt }`**：delegation 的 prompt 结构，
  不是 report DTO；本包不拥有（WORK-RECORD-012 边界里已注明）。

## 5. 依赖理由（DEPENDS ON）

- `semantic-trace`：record 的三段全部来自 XTrace 区间（Opening 用 `forOpening`，Recent 用
  `forWorkRecord` + `sliceFrom`）——record 是 trace 的物化，不是第二事实源。
- `context-compression`：Chronicle 的存在依赖 Y frames 的覆盖表示（frame 是压缩产物）。
- `participant-horizon`：record 作为跨 participant 传递物，其内容准入由 horizon 保证
  （INDEX.md 骨架：`work-record → semantic-trace, context-compression, participant-horizon`）。

## 验证与测试落点

### 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| WORK-RECORD-001（record 属于 work 不属于 receiver） | `tests/lifecycle-work-record.test.mjs`：`LWR_same_record_projected_two_ways_shares_work_facts`（同一 canonical record 两投影共享 Chronicle/Recent，仅 Opening 渲染段不同） | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-002（边界因果非会话） | `tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames`（range 过滤 prior invocation）；`requirements/semantic-trace/tests/x-trace-locality.test.mjs`（semantic-trace 包）`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite`（跨包交叉） | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-003（Chronicle/Recent = representation） | `tests/lifecycle-work-record.test.mjs`：`LWR_y_frames_cover_prefix_and_x_supplies_only_suffix`、`LWR_no_y_frames_means_opening_plus_raw_gap_not_alternate_A_path` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-004（reuse 不扩大下一次 record） | `tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_chronicle_heading_omitted_when_invocation_has_no_y`、`same terminal text in a reused child is a fresh occurrence when ProviderRun changes`、`rematerializing an older bounded range never substitutes a later terminal`；range 过滤见 WORK-RECORD-002 锚点 | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-005（Recent ≠ receiver-relative recentness） | `tests/lifecycle-work-record.test.mjs`：`LWR_gap_starts_at_record_coverage_not_prefix_cutoff`；`tests/lwr-record-coverage-vs-prefix-coverage.test.mjs`：`LWR_gap_from_origin_is_full_history_including_partial_turn` | MOVE + NEW | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` |
| WORK-RECORD-006（canonical 保留 Opening） | `tests/lifecycle-work-record.test.mjs`：`LWR_child_opening_excludes_parent_work_record_envelope`（Opening 捕获始终存在、投影才省略）；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_parent_work_record_renders_the_opening_exactly_once` | MOVE + 跨包 | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-007（includeOpening 分向） | `tests/lifecycle-work-record.test.mjs`：`LWR_parent_to_child_includes_opening`、`LWR_child_to_parent_omits_opening` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-008（Opening preserved 非重建） | `tests/lifecycle-work-record.test.mjs`：`LWR_opening_prompt_is_byte_exact_and_appears_exactly_once`、`LWR_reviewer_opening_preserves_authoritative_requirement_order`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_capture_opening_takes_authoritative_requirements` | MOVE + 跨包 | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-009（T1 constitutive Opening） | `tests/lifecycle-work-record.test.mjs`（NEW）：`LWR_t1_commitment_call_result_is_constitutive_opening_material`（`withConstitutive` 保留 T1 call/result 于 Opening）；`requirements/semantic-trace/tests/x-trace-locality.test.mjs`（semantic-trace 包）：`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite` | NEW + 跨包 | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-010（one invocation one record everywhere） | `tests/lifecycle-work-record.test.mjs`：`LWR_materialization_is_deterministic`；`tests/lifecycle-work-record-bounded.test.mjs`（bounded 与 full 共用同一 materializer）；REUSE：`requirements/finality/tests/lifecycle.test.mjs`、`requirements/delegation/tests/join-tool-family.test.mjs`（EXEC-031 交叉） | MOVE + REUSE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-011（三段 + 正式陈述） | `tests/lifecycle-work-record.test.mjs`：`LWR_last_assistant_text_is_in_recent_work_not_a_closing_report`、`LWR_empty_sections_are_omitted`；`tests/lifecycle-work-record-bounded.test.mjs`：`bounded terminal-only completion still yields Recent work after Chronicle covered every durable part`；`tests/lwr-prose-claim-no-schema.test.mjs`：`LWR_statement_is_the_last_assistant_text_in_recent_work`；`tests/work-record-sections.test.mjs`：`WORK_RECORD_SECTIONS_lifecycle_source_declares_three_canonical_headings`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_last_words_land_in_recent_work_not_closing_report` | MOVE + NEW | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs` |
| WORK-RECORD-012（prose claim 无固定 schema） | `tests/lwr-prose-claim-no-schema.test.mjs`（NEW）：`LWR_prose_claim_never_renders_fixed_report_headings`；REUSE：`requirements/finality/tests/lifecycle.test.mjs`（无固定 report DTO） | NEW + REUSE | `node --test requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs` |
| WORK-RECORD-013（禁 raw tool） | `tests/lifecycle-work-record.test.mjs`：`LWR_gap_excludes_raw_tool_call_and_result_but_keeps_text_and_reasoning`、`LWR_recent_work_excludes_raw_tool_parts_and_keeps_last_assistant_text` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-014（RecordCoverage ≠ PrefixCoverage） | `tests/lifecycle-work-record.test.mjs`：`LWR_gap_never_uses_prefix_cutoff`；`tests/lwr-record-coverage-vs-prefix-coverage.test.mjs`：`LWR_recent_work_can_start_mid_turn_at_record_coverage`；REUSE 交叉：`requirements/context-compression/tests/blog-projection.test.mjs`（context-compression 包）`CTX_011_*` | MOVE + NEW | `node --test requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` |
| WORK-RECORD-015（WorkRecordStart 结构性 floor） | `tests/lifecycle-work-record.test.mjs`（NEW）：`LWR_work_record_start_is_structural_floor_not_stage`（`magicTodo.workRecordStart`/`effectiveOpeningFloor` 纯推导）；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_capture_opening_takes_authoritative_requirements`；REUSE：`requirements/finality/tests/lifecycle.test.mjs`（WorkRecordStart 纯推导） | NEW + 跨包 + REUSE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-016（request-range bounded） | `tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_review_consumes_request_range_not_session_head`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs`（semantic-trace 包）`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite` + current-message prefix 去重；`requirements/obligation-ledger/tests/magic-todo-after.test.mjs`（obligation-ledger 包）`first T1 review start is frozen before its own commitment can move the global opening floor` / `persistent process reviewer receives only manager work after its last concluded frontier`，交叉证明首份 range 从 `next(Life.OpeningCursor)`、后续从 last concluded assigned exact frontier，均不受 post-T1 global floor 或 physical reviewer relink 改写；REUSE：`requirements/finality/tests/lifecycle.test.mjs` | MOVE + REUSE | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs requirements/obligation-ledger/tests/magic-todo-after.test.mjs` |

### 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/lifecycle-work-record.test.mjs` | MOVE `requirements/work-record/tests/lifecycle-work-record.test.mjs` | 已跑绿（17 pass，含 009/015 新增） |
| `tests/lifecycle-work-record-bounded.test.mjs` | MOVE `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` | 已跑绿（6 pass；含 bounded terminal/reuse 回归） |
| `tests/lwr-prose-claim-no-schema.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/work-record-sections.test.mjs` | MOVE `requirements/work-record/tests/work-record-sections.test.mjs` | 已跑绿（1 pass） |

### 3. 单跑命令

```text
node --test requirements/work-record/tests/lifecycle-work-record.test.mjs
node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs
node --test requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs
node --test requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs
node --test requirements/work-record/tests/work-record-sections.test.mjs
```

### 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/finality/tests/lifecycle.test.mjs` | canonical LWR materializer、Opening preserved、request-range bound、无固定 report schema | SPLIT@cutover：finality 侧（cohort/blessing）归 finality；LWR 物化锚点归本包 |
| `requirements/delegation/tests/join-tool-family.test.mjs`、~~`tests/unit/execution/sync-delegate.test.mjs`~~ | EXEC-028/031 bounded WorkRecord（includeOpening=false、无 answer 字段） | SPLIT@cutover：delegation 语义归 delegation；record 形状锚点归本包 |
| `requirements/obligation-ledger/tests/magic-todo.test.mjs`、`requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` | TODO-008/009 coverage 分型、TODO-015 T1 constitutive | SPLIT@cutover：obligation-ledger 保留 checkpoint 语义；本包引用 LWR 形状 |
| ~~`tests/unit/review/`~~（review-assurance 相关） | ProcessReviewLWR request-range bounded | 已 SPLIT（Wave 2a）：review-assurance 保留消费资格；本包拥有 record 表示 |
| `requirements/durable-events/tests/fold-context-recovery.test.mjs` | LWR 相关 fold 语义（durable-events） | 归 durable-events |

### 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（LWR 语义由 F# 类型 + fold
测试承担）。若 cutover 后需要散文 canary，建议增加 `WORK_RECORD_*` 锚点并声明 owner 为本包。
