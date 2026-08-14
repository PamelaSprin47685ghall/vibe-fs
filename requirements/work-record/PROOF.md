# work-record — PROOF（测试落点表）

## 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| WORK-RECORD-001（record 属于 work 不属于 receiver） | `tests/lifecycle-work-record.test.mjs`：`LWR_parent_to_child_includes_opening`、`LWR_child_to_parent_omits_opening`（同一 record 两投影）；`tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-002（边界因果非会话） | `tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames`（range 过滤 prior invocation）；`tests/x-trace-locality.test.mjs`（semantic-trace 包）`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite`（跨包交叉） | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-003（Chronicle/Recent = representation） | `tests/lifecycle-work-record.test.mjs`：`LWR_y_frames_cover_prefix_and_x_supplies_only_suffix`、`LWR_no_y_frames_means_opening_plus_raw_gap_not_alternate_A_path` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-004（reuse 不扩大下一次 record） | `tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames`、`COMPANION_015_bounded_chronicle_heading_omitted_when_invocation_has_no_y` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-005（Recent ≠ receiver-relative recentness） | `tests/lifecycle-work-record.test.mjs`：`LWR_gap_starts_at_record_coverage_not_prefix_cutoff`；`tests/lwr-record-coverage-vs-prefix-coverage.test.mjs`（NEW）：`LWR_recent_work_can_start_mid_turn_at_record_coverage` | MOVE + NEW | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` |
| WORK-RECORD-006（canonical 保留 Opening） | `tests/lifecycle-work-record.test.mjs`：`LWR_child_opening_excludes_parent_work_record_envelope`（Opening 捕获始终存在、投影才省略）；`tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_parent_work_record_renders_the_opening_exactly_once` | MOVE + 跨包 | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-007（includeOpening 分向） | `tests/lifecycle-work-record.test.mjs`：`LWR_parent_to_child_includes_opening`、`LWR_child_to_parent_omits_opening` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-008（Opening preserved 非重建） | `tests/lifecycle-work-record.test.mjs`：`LWR_opening_prompt_is_byte_exact_and_appears_exactly_once`、`LWR_reviewer_opening_preserves_authoritative_requirement_order`；`tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_capture_opening_takes_authoritative_requirements` | MOVE + 跨包 | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-009（T1 constitutive Opening） | `tests/x-trace-locality.test.mjs`（semantic-trace 包）：`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite`；REUSE：`tests/unit/glory/lifecycle.test.mjs`（canonical LWR materializer，BlindPlan T1 区间） | 跨包 + REUSE | `node --test requirements/semantic-trace/tests/x-trace-locality.test.mjs` |
| WORK-RECORD-010（one invocation one record everywhere） | `tests/lifecycle-work-record.test.mjs`：`LWR_materialization_is_deterministic`；`tests/lifecycle-work-record-bounded.test.mjs`（bounded 与 full 共用同一 materializer）；REUSE：`tests/unit/glory/lifecycle.test.mjs`、`tests/unit/execution/join-tool-family.test.mjs`（EXEC-031 交叉） | MOVE + REUSE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-011（三段 + 正式陈述） | `tests/lifecycle-work-record.test.mjs`：`LWR_last_assistant_text_is_in_recent_work_not_a_closing_report`；`tests/lwr-prose-claim-no-schema.test.mjs`（NEW）：`LWR_statement_is_the_last_assistant_text_in_recent_work`；`tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_last_words_land_in_recent_work_not_closing_report` | MOVE + NEW | `node --test requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs` |
| WORK-RECORD-012（prose claim 无固定 schema） | `tests/lwr-prose-claim-no-schema.test.mjs`（NEW）：`LWR_prose_claim_never_renders_fixed_report_headings`；REUSE：`tests/unit/glory/lifecycle.test.mjs`（无固定 report DTO） | NEW + REUSE | `node --test requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs` |
| WORK-RECORD-013（禁 raw tool） | `tests/lifecycle-work-record.test.mjs`：`LWR_gap_excludes_raw_tool_call_and_result_but_keeps_text_and_reasoning`、`LWR_recent_work_excludes_raw_tool_parts_and_keeps_last_assistant_text` | MOVE | `node --test requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-014（RecordCoverage ≠ PrefixCoverage） | `tests/lifecycle-work-record.test.mjs`：`LWR_gap_starts_at_record_coverage_not_prefix_cutoff`；`tests/lwr-record-coverage-vs-prefix-coverage.test.mjs`（NEW）全部；REUSE 交叉：`tests/unit/context/blog-projection.test.mjs`（context-compression 包）`CTX_011_*` | MOVE + NEW | `node --test requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` |
| WORK-RECORD-015（WorkRecordStart 结构性 floor） | `tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_capture_opening_takes_authoritative_requirements`；REUSE：`tests/unit/glory/lifecycle.test.mjs`（WorkRecordStart 纯推导）；`tests/unit/todo/admission.test.mjs`（TODO-001 交叉） | 跨包 + REUSE | `node --test requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs` |
| WORK-RECORD-016（request-range bounded） | `tests/lifecycle-work-record-bounded.test.mjs`：`COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames`；`tests/x-trace-locality.test.mjs`（semantic-trace 包）`TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite`；REUSE：`tests/unit/glory/lifecycle.test.mjs` | MOVE + REUSE | `node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |

## 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/lifecycle-work-record.test.mjs` | MOVE `tests/unit/context/lifecycle-work-record.test.mjs` | 已跑绿（13 pass） |
| `tests/lifecycle-work-record-bounded.test.mjs` | MOVE `tests/unit/context/lifecycle-work-record-bounded.test.mjs` | 已跑绿（2 pass） |
| `tests/lwr-prose-claim-no-schema.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` | NEW | 已跑绿（2 pass） |

## 3. 单跑命令

```text
node --test requirements/work-record/tests/lifecycle-work-record.test.mjs
node --test requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs
node --test requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs
node --test requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs
```

## 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `tests/unit/glory/lifecycle.test.mjs` | canonical LWR materializer、Opening preserved、request-range bound、无固定 report schema | SPLIT@cutover：finality 侧（cohort/blessing）归 finality；LWR 物化锚点归本包 |
| `tests/unit/execution/join-tool-family.test.mjs`、`tests/unit/execution/sync-delegate.test.mjs` | EXEC-028/031 bounded WorkRecord（includeOpening=false、无 answer 字段） | SPLIT@cutover：delegation 语义归 delegation；record 形状锚点归本包 |
| `tests/unit/todo/admission.test.mjs`、`tests/unit/todo/magic-todo-facts.test.mjs` | TODO-008/009 coverage 分型、TODO-015 T1 constitutive | SPLIT@cutover：obligation-ledger 保留 checkpoint 语义；本包引用 LWR 形状 |
| `tests/unit/review/`（REVIEW-014/016 相关） | ProcessReviewLWR request-range bounded | SPLIT@cutover：review-assurance 保留消费资格；本包拥有 record 表示 |
| `tests/unit/context/fold-context-recovery.test.mjs` | LWR 相关 fold 语义（durable-events） | 归 durable-events |

## 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（LWR 语义由 F# 类型 + fold
测试承担）。若 cutover 后需要散文 canary，建议增加 `WORK_RECORD_*` 锚点并声明 owner 为本包。
