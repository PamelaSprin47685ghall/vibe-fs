# PROOF — review-judgement

> 每条 WHAT 命题一行落点。类型：`MOVE`（物理移入本包 tests/）、`REUSE`（留在原处，cutover 拆分）、`NEW`（本包新写）、`MECHANISM`（共享 gate，语义 owner 是本包）。
> 运行命令：`node --test requirements/review-judgement/tests/<file>`；套件级 `node requirements/verification-system/tests/run.mjs`。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REVIEW-JUDGEMENT-001（judge 工具形态：typed verdict、无描述字段、不 echo；Reviewer-facing provider 指令只命名 judge，禁止已删除的 verdict 工具名） | `requirements/review-judgement/tests/judge-tool-contract.test.mjs` → `REVIEW_001_verdict_schema_allows_only_the_verdict_argument`、`REVIEW_001_verdict_parse_is_exact_perfect_or_revise`、`REVIEW_001_tool_spec_exposes_judge_with_a_single_verdict_argument`、`REVIEW_001_reviewer_provider_instructions_name_judge_never_the_removed_verdict_tool`、`REVIEW_001_receipt_does_not_echo_the_verdict` | NEW | `node --test requirements/review-judgement/tests/judge-tool-contract.test.mjs` |
| REVIEW-JUDGEMENT-001（执行面 fail-closed：非 Reviewer / 无 barrier / 非法值拒绝） | `requirements/review-judgement/tests/verdict-tool.test.mjs` → `JUDGE_spec_exposes_the_verdict_input_and_public_tool_identity`、`JUDGE_invalid_input_is_rejected_as_a_natural_consequence`、`JUDGE_missing_input_is_rejected_as_a_natural_consequence`、`JUDGE_is_unavailable_to_non_reviewer_sessions`、`JUDGE_empty_session_is_rejected_before_role_resolution`、`JUDGE_reviewer_requires_a_tool_call_id_before_review_submission`；`requirements/review-judgement/tests/verdict-tool-extras.test.mjs` → `JUDGE_unknown_owner_fails_closed_without_internal_vocabulary`、`JUDGE_missing_tree_fails_closed_without_internal_vocabulary`、`JUDGE_no_open_review_barrier_fails_closed_without_internal_vocabulary`、`JUDGE_non_reviewer_role_is_refused_before_identity_checks` | REUSE | `node --test requirements/review-judgement/tests/verdict-tool.test.mjs requirements/review-judgement/tests/verdict-tool-extras.test.mjs` |
| REVIEW-JUDGEMENT-002（acceptance/rejection 都须挣得；discrimination 不是表演） | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` → `REVIEW_011_acceptance_and_rejection_must_both_be_earned`、`REVIEW_011_discrimination_is_the_craft_not_rejection_theatre`、`REVIEW_011_a_match_is_an_observation_a_defect_is_a_judgement` | NEW | `node --test requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-003（判断相对 root requirement / 当前对象，不是 mood） | `discrimination-fixtures.test.mjs` → `REVIEW_011_judgement_is_against_the_obligation_not_the_reviewer_mood`、`REVIEW_011_a_lens_may_narrow_sight_but_not_responsibility` | NEW | 同上 |
| REVIEW-JUDGEMENT-004（material defect 才 withhold；PERFECT+minor 共存） | `discrimination-fixtures.test.mjs` → `REVIEW_011_blocking_vs_nonblocking_workmanship_are_distinguished`、`REVIEW_011_a_minor_typo_never_purchases_revise`、`REVIEW_011_perfect_does_not_silence_a_true_minor_observation`、`REVIEW_011_materiality_traces_consequence_not_edit_size` | NEW | 同上 |
| REVIEW-JUDGEMENT-005（PERFECT≠全知/字面无瑕；REVISE 必须购买） | `discrimination-fixtures.test.mjs` → `REVIEW_011_perfect_is_not_literal_flawlessness`、`REVIEW_011_rejection_must_purchase_a_materially_better_or_more_truthful_result`、`REVIEW_011_acceptance_does_not_require_omniscience` | NEW | 同上 |
| REVIEW-JUDGEMENT-006（evidence/inference/preference/defect 边界；证据相称；保留 uncertainty） | `discrimination-fixtures.test.mjs` → `REVIEW_011_evidence_must_be_proportional_to_the_claim`、`REVIEW_011_unresolved_uncertainty_is_preserved_not_laundered_into_a_verdict`、`REVIEW_011_evidence_alone_is_not_judgement` | NEW | 同上 |
| REVIEW-JUDGEMENT-007（Ledger/Rulebook 非 checklist；无固定 report schema） | `discrimination-fixtures.test.mjs` → `REVIEW_011_the_ledger_is_a_judgement_direction_not_a_checklist`、`REVIEW_011_no_fixed_report_schema_or_eight_heading_template` | NEW | 同上 |
| REVIEW-JUDGEMENT-007（语义锚 gate：五条 reviewer anchor 文本在场） | `scripts/checks/semantic-anchors.mjs` reviewer family → `discrimination`、`rejection-must-purchase`、`non-blocking`、`perfect-not-flawless`、`acceptance-not-omniscience` | MECHANISM | `node scripts/check.mjs`（semantic-anchors 项） |
| REVIEW-JUDGEMENT-008（过程评审一次 durable judge 即 terminal；无 challenge/dual-PERFECT；prose 义务） | `requirements/review-judgement/tests/process-review-judgement.test.mjs` → `REVIEW_013_the_request_kind_is_typed_process_vs_finality`、`REVIEW_013_ensure_review_stays_outstanding_until_concluded`、`REVIEW_013_process_preamble_commands_exactly_one_verdict_and_disclaims_terminal_witness`、`REVIEW_013_process_assignment_is_request_range_bounded_without_confirmation_vocabulary`、`REVIEW_013_continuation_process_assignment_does_not_replay_opening_authority`、`REVIEW_013_a_process_verdict_never_becomes_a_confirmed_witness_by_itself` | NEW | `node --test requirements/review-judgement/tests/process-review-judgement.test.mjs` |
| REVIEW-JUDGEMENT-009（拒绝把伤口说清；不发明 obligation） | `discrimination-fixtures.test.mjs` → `REVIEW_011_the_wound_must_be_clear_enough_to_purchase_the_repair`、`REVIEW_011_no_invented_obligations_to_look_careful` | NEW | 同上 discrimination 命令 |
| REVIEW-JUDGEMENT-010（不得奖励自信/惩罚不熟悉/因口味拒绝） | `discrimination-fixtures.test.mjs` → `REVIEW_011_judgement_does_not_reward_confidence_or_punish_unfamiliarity`、`REVIEW_011_novelty_and_style_preference_are_not_defects_by_themselves` | NEW | 同上 |

## 本包拥有的 semantic anchor id

`semantic-anchors.mjs` reviewer family（MECHANISM，逐 ID 归本包）：

```text
discrimination
rejection-must-purchase
non-blocking
perfect-not-flawless
acceptance-not-omniscience
```

## REUSE / cutover 拆分计划（SPLIT@cutover）

| 现有测试 | 现状 | 计划 |
|---|---|---|
| `requirements/review-judgement/tests/verdict-tool.test.mjs` | 依赖 `requirements/verification-system/tests/support/plugin-fixture.mjs`（不在 `tests/unit/support/**` 白名单），按契约 §4.6 不可随包移动 | cutover 时将 plugin-fixture 提升为共享 support 后移入本包；或按断言逐条搬入 `judge-tool-contract.test.mjs` |
| `requirements/review-judgement/tests/verdict-tool-extras.test.mjs` | 直接 import `dist/fable_modules/...`，契约 §4.1 禁止移动 | cutover 时剥离 fable_modules import 后移入 |
| `tests/unit/verify/...`（language-parity-gate、tool-referential-integrity 等） | MECHANISM/SPLIT 混合 | `judge` 工具名引用完整性断言归 `action-affordance`/`capability-enforcement`（ARCH-007），与本包无 assertion 冲突 |

## 可红性说明

- `judge-tool-contract.test.mjs`：若 `judge` 增加描述字段、回执开始 echo verdict、或解析器接受第三个值，schema/parse/prose 断言即红。
- `discrimination-fixtures.test.mjs`：若 Role Law / Examiner's Ledger 丢失「discrimination / earned both ways / non-blocking / not-flawless / omniscience / checklist 禁令 / materiality」任一承诺句，对应 fixture 即红——这不是装饰性锚点，而是判断语义合同的文本证明。
- `process-review-judgement.test.mjs`：若过程评审误走 challenge/dual-PERFECT，或 `TodoReviewConcluded` 在无 prose 时被接受，断言即红。

## 未覆盖与理由

- 真实模型行为 canary（模型真的按 discrimination 判断）：judgement 最终由 LLM 执行，行为级 canary 属 `verification-system` 的 e2e 阶梯（Long Stroke / finality-cohort-law canary），不在本包单元证明范围（PROOF-MAP 注明的「不能只靠 prompt anchors」已由 discrimination-fixtures 的可失败文本契约 + 工具面行为测试补齐，剩余缺口在 e2e）。
