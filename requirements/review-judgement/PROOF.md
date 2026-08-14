# PROOF — review-judgement

> 每条 WHAT 命题一行落点。类型：`MOVE`（物理移入本包 tests/）、`REUSE`（留在原处，cutover 拆分）、`NEW`（本包新写）、`MECHANISM`（共享 gate，语义 owner 是本包）。
> 运行命令：`node --test requirements/review-judgement/tests/<file>`；套件级 `node tests/unit/run.mjs`。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REVIEW-JUDGEMENT-001（judge 工具形态：typed verdict、无描述字段、不 echo） | `requirements/review-judgement/tests/judge-tool-contract.test.mjs` → `REVIEW_001_*` 4 条（schema 只许 verdict / parse 精确 / spec 单参数 / 回执不 echo） | NEW | `node --test requirements/review-judgement/tests/judge-tool-contract.test.mjs` |
| REVIEW-JUDGEMENT-001（执行面 fail-closed：非 Reviewer / 无 barrier / 非法值拒绝） | `tests/unit/tools/verdict-tool.test.mjs` → `JUDGE_invalid_input_is_rejected_as_a_natural_consequence`、`JUDGE_missing_input_...`、`JUDGE_is_unavailable_to_non_reviewer_sessions`、`JUDGE_empty_session_...`、`JUDGE_reviewer_requires_a_tool_call_id_...`；`tests/unit/tools/verdict-tool-extras.test.mjs` → `JUDGE_*_fails_closed_without_internal_vocabulary` | REUSE | `node --test tests/unit/tools/verdict-tool.test.mjs tests/unit/tools/verdict-tool-extras.test.mjs` |
| REVIEW-JUDGEMENT-002（acceptance/rejection 都须挣得；discrimination 不是表演） | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` → `REVIEW_011_*_earned_both_ways`、`REVIEW_011_*_discrimination_not_rejection_theatre` | NEW | `node --test requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-003（判断相对 root requirement / 当前对象，不是 mood） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_judged_against_obligation_not_mood`、`REVIEW_011_*_wire_literals_not_moods` | NEW | 同上 |
| REVIEW-JUDGEMENT-004（material defect 才 withhold；PERFECT+minor 共存） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_blocking_vs_nonblocking`、`REVIEW_011_*_minor_typo_never_purchases_revise`、`REVIEW_011_*_perfect_does_not_silence_minor`、`REVIEW_011_*_materiality_traces_consequence_not_size` | NEW | 同上 |
| REVIEW-JUDGEMENT-005（PERFECT≠全知/字面无瑕；REVISE 必须购买） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_perfect_not_flawless`、`REVIEW_011_*_rejection_purchases`、`REVIEW_011_*_acceptance_not_omniscience` | NEW | 同上 |
| REVIEW-JUDGEMENT-006（evidence/inference/preference/defect 边界；证据相称；保留 uncertainty） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_evidence_proportional_to_claim`、`REVIEW_011_*_uncertainty_preserved_not_laundered` | NEW | 同上 |
| REVIEW-JUDGEMENT-007（Ledger/Rulebook 非 checklist；无固定 report schema） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_ledger_not_checklist`、`REVIEW_011_*_no_fixed_report_schema` | NEW | 同上 |
| REVIEW-JUDGEMENT-007（语义锚 gate：五条 reviewer anchor 文本在场） | `scripts/checks/semantic-anchors.mjs` reviewer family → `discrimination`、`rejection-must-purchase`、`non-blocking`、`perfect-not-flawless`、`acceptance-not-omniscience` | MECHANISM | `node scripts/check.mjs`（semantic-anchors 项） |
| REVIEW-JUDGEMENT-008（过程评审一次 durable judge 即 terminal；无 challenge/dual-PERFECT；prose 义务） | `requirements/review-judgement/tests/process-review-judgement.test.mjs` → `REVIEW_013_*_process_verdict_is_one_judgement`、`REVIEW_013_*_process_assignment_omits_challenge_vocabulary`、`REVIEW_013_*_needs_ensure_review_until_concluded` | NEW | `node --test requirements/review-judgement/tests/process-review-judgement.test.mjs` |
| REVIEW-JUDGEMENT-009（拒绝把伤口说清；不发明 obligation） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_wound_clear_enough_to_purchase`、`REVIEW_011_*_no_invented_obligations` | NEW | 同上 discrimination 命令 |
| REVIEW-JUDGEMENT-010（不得奖励自信/惩罚不熟悉/因口味拒绝） | `discrimination-fixtures.test.mjs` → `REVIEW_011_*_no_reward_for_confidence`、`REVIEW_011_*_novelty_and_preference_not_defect` | NEW | 同上 |

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
| `tests/unit/tools/verdict-tool.test.mjs` | 依赖 `tests/unit/plugin/plugin-fixture.mjs`（不在 `tests/unit/support/**` 白名单），按契约 §4.6 不可随包移动 | cutover 时将 plugin-fixture 提升为共享 support 后移入本包；或按断言逐条搬入 `judge-tool-contract.test.mjs` |
| `tests/unit/tools/verdict-tool-extras.test.mjs` | 直接 import `dist/fable_modules/...`，契约 §4.1 禁止移动 | cutover 时剥离 fable_modules import 后移入 |
| `tests/unit/verify/...`（language-parity-gate、tool-referential-integrity 等） | MECHANISM/SPLIT 混合 | `judge` 工具名引用完整性断言归 `action-affordance`/`capability-enforcement`（ARCH-007），与本包无 assertion 冲突 |

## 可红性说明

- `judge-tool-contract.test.mjs`：若 `judge` 增加描述字段、回执开始 echo verdict、或解析器接受第三个值，schema/parse/prose 断言即红。
- `discrimination-fixtures.test.mjs`：若 Role Law / Examiner's Ledger 丢失「discrimination / earned both ways / non-blocking / not-flawless / omniscience / checklist 禁令 / materiality」任一承诺句，对应 fixture 即红——这不是装饰性锚点，而是判断语义合同的文本证明。
- `process-review-judgement.test.mjs`：若过程评审误走 challenge/dual-PERFECT，或 `TodoReviewConcluded` 在无 prose 时被接受，断言即红。

## 未覆盖与理由

- 真实模型行为 canary（模型真的按 discrimination 判断）：judgement 最终由 LLM 执行，行为级 canary 属 `verification-system` 的 e2e 阶梯（Long Stroke / finality-cohort-law canary），不在本包单元证明范围（PROOF-MAP 注明的「不能只靠 prompt anchors」已由 discrimination-fixtures 的可失败文本契约 + 工具面行为测试补齐，剩余缺口在 e2e）。
