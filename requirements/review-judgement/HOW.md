# review-judgement — HOW

## 1. 工具契约与判断面

- **`judge` 接口定义**：工具参数仅包含单个必填强类型字段 `verdict`（枚举值为 `PERFECT` 与 `REVISE`），禁止包含描述、说明或附加元数据。
- **回执语义分型**：
  - 终局评审（FinalityReview）首次 PERFECT 触发 `Challenge` 逻辑，仅渲染质疑提示（`resources/provider/review/challenge`），严禁拼接收束回执；仅在第二次判断或 REVISE 时完成工具调用。
- **防重与幂等范围**：已裁决状态（already judged）的作用域限定于当前 `(ReviewerSessionId, PhysicalUserMessageId)` 物理请求。单次请求内的重复调用被收束以避免空转。
- **terminal 回执的物理收束**：首次 terminal judgement durable 后只写 request-scoped submitted 标记，不在 `judge` 的 duplicate 分支中等待第二次提交再杀。普通 provider transform 在 XTrace 已捕获该 judge `tool_result`、完成消息投影后检查“当前 PhysicalUserMessageId 是否已有 submitted judgement”；命中时先由 Reviewer owner 用 exact `(ProviderRun, ToolCallId)` 找到 durable tool-result part，以 `cursor+1` 写入幂等 `ReviewAttemptClosed`。随后以当前 snapshot 的 canonical Chronicle 判断 record capture：已有 Chronicle 立即通过，避免等待同一 transform 后续才会生成的 self-dependent Blogger request；仅当 Reviewer 已链接 Blogger、尚无 Chronicle 且当前确有 durable-open producer 时，才通过 `AgentJournal.snapshotWithRevision` / `awaitChangeFromOrCancel` 等待 producer 结算，不得用 flight、pending slot、timeout 或 polling 冒充 record-ready 证明。closure 与必要的首次 Chronicle settlement 都成立后，transform 只向 `PluginRuntimeScope.RunBackground` 投递一次异步 `InterruptAttempt`，并立即从 transform 正常返回。

## 2. 判断哲学载体与引导机制

- **Role Law 与质量基准**：Reviewer 的判断哲学由 Role Law 及 Examiner's Ledger 承载，提供区分力、实质性缺陷、非阻断工艺以及不确定性处理等规范原则。
- **提示词组装与权威**：系统提示词在会话启动时由认知环境统一组装注入，Reviewer 在执行判断时遵循引导方向，但不把质量基准固化为机械的填表格式。

## 3. 终结流转分型

- **终审双重确认解耦**：终审的双重 PERFECT 编排由独立的因果工作流驱动，不设中间过程评审。

## 4. 依赖声明

```text
DEPENDS ON: cognitive-environment, participant-horizon
```

## 5. 边界（DOES NOT OWN）

- 评审结论的因果确认、witness 结构与 seal 绑定 → `review-assurance`
- 过程评审 1:1 节拍与义务派生 → `obligation-ledger`
- 终局 cohort 编排、rejection 与 blessing 经验 → `finality`
- 提示词资源的组装与装载权威 → `cognitive-environment`
- 隐藏 Reviewer 视野与信息准入隔离 → `participant-horizon`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REVIEW-JUDGEMENT-001 | `requirements/review-judgement/tests/judge-tool-contract.test.mjs::WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_verdict_schema_allows_only_the_verdict_argument`；`requirements/review-judgement/tests/judge-tool-contract.test.mjs::WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_verdict_parse_is_exact_perfect_or_revise`；`requirements/review-judgement/tests/judge-tool-contract.test.mjs::WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_tool_spec_exposes_judge_with_a_single_verdict_argument`；`requirements/review-judgement/tests/judge-tool-contract.test.mjs::WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_reviewer_provider_instructions_name_judge_never_the_removed_verdict_tool`；`requirements/review-judgement/tests/judge-tool-contract.test.mjs::WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_receipt_does_not_echo_the_verdict`；`requirements/review-judgement/tests/judge-tool-contract.test.mjs::WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_already_judged_receipt_prompts_to_conclude` |
| REVIEW-JUDGEMENT-002 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-002] REVIEW_011_acceptance_and_rejection_must_both_be_earned`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-002] REVIEW_011_discrimination_is_the_craft_not_rejection_theatre`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-002] REVIEW_011_a_match_is_an_observation_a_defect_is_a_judgement` |
| REVIEW-JUDGEMENT-003 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-003] REVIEW_011_judgement_is_against_the_obligation_not_the_reviewer_mood`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-003] REVIEW_011_a_lens_may_narrow_sight_but_not_responsibility` |
| REVIEW-JUDGEMENT-004 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-004] REVIEW_011_blocking_vs_nonblocking_workmanship_are_distinguished`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-004] REVIEW_011_a_minor_typo_never_purchases_revise`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-004] REVIEW_011_perfect_does_not_silence_a_true_minor_observation`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-004] REVIEW_011_materiality_traces_consequence_not_edit_size` |
| REVIEW-JUDGEMENT-005 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-005] REVIEW_011_perfect_is_not_literal_flawlessness`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-005] REVIEW_011_rejection_must_purchase_a_materially_better_or_more_truthful_result`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-005] REVIEW_011_acceptance_does_not_require_omniscience` |
| REVIEW-JUDGEMENT-006 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-006] REVIEW_011_evidence_must_be_proportional_to_the_claim`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-006] REVIEW_011_unresolved_uncertainty_is_preserved_not_laundered_into_a_verdict`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-006] REVIEW_011_evidence_alone_is_not_judgement` |
| REVIEW-JUDGEMENT-007 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-007] REVIEW_011_the_ledger_is_a_judgement_direction_not_a_checklist`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-007] REVIEW_011_no_fixed_report_schema_or_eight_heading_template` |
| REVIEW-JUDGEMENT-008 | `requirements/review-judgement/tests/process-review-judgement.test.mjs::WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_a_single_verdict_never_becomes_a_confirmed_witness_by_itself`；`requirements/review-judgement/tests/process-review-judgement.test.mjs::WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_first_terminal_receipt_is_physically_enforced_at_provider_transform`；`requirements/review-judgement/tests/process-review-judgement.test.mjs::WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_ensure_submitted_attempt_closed_returns_ok_false_when_tool_result_missing_and_does_not_interrupt`；`requirements/review-judgement/tests/process-review-judgement.test.mjs::WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_ensure_submitted_attempt_closed_returns_error_on_append_failure_and_fails_closed` |
| REVIEW-JUDGEMENT-009 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-009] REVIEW_011_the_wound_must_be_clear_enough_to_purchase_the_repair`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-009] REVIEW_011_no_invented_obligations_to_look_careful` |
| REVIEW-JUDGEMENT-010 | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-010] REVIEW_011_judgement_does_not_reward_confidence_or_punish_unfamiliarity`；`requirements/review-judgement/tests/discrimination-fixtures.test.mjs::WHAT[REVIEW-JUDGEMENT-010] REVIEW_011_novelty_and_style_preference_are_not_defects_by_themselves` |
