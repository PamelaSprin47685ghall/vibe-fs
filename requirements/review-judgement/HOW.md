# HOW — review-judgement

> 本文非 normative。它解释 judgement 语义在当前实现里落地的模型与位置，并收纳「历史与弃权」。
> Normative 合同只有 `WHAT.md`。

## 实现模型

### 1. 判断面：`judge` 工具

- `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JudgeTool.fs`：
  - `spec` 返回 `{ Name = "judge"; Arguments = ["verdict", enumSchema ["PERFECT"; "REVISE"]] }`——**只有** `verdict` 一个参数，无描述字段（REVIEW-JUDGEMENT-001）。
  - 成功回执走 `tool/judge/received` 文案（`Your judgment has been received.`），**不 echo verdict**；`description` 文案明言「It does not echo the verdict」。
  - `execute` 把 `verdict` 文本交给 `StaticTools.reviewerVerdictOfString` 解析，任何非 `PERFECT/REVISE` 值 → `Path.VerdictMustBePerfectOrRevise`。
  - fail-closed 分支（非 Reviewer、无 barrier、无 tree、binding 失败）→ `notReceived`，不落 verdict 事实。这些分支的因果侧（seal binding）归 `review-assurance`。
- `src/Wanxiangshu/Tools/StaticTools.fs`：
  - `reviewerVerdictOfString`：唯一解析器，`"PERFECT" → Ok Perfect`、`"REVISE" → Ok Revise`、其它 → `Error`。刻意独立于 assistant 文本：verdict 是工具参数，绝不从 transcript 推断。
  - `reviewerVerdictSchemaJson`：`additionalProperties: false` + `required: ["verdict"]`——从 schema 层杜绝描述字段（REVIEW-JUDGEMENT-001 的可执行证据）。
- 工具注册表（`ToolRegistry`）把 `judge` 挂到 Reviewer 工具面（旧名 `verdict` 无 alias）。
- Reviewer-facing 操作指令（`runtime/reviewer-verdict-required`、`lifecycle/magic-todo/process-reviewer-preamble`、`lifecycle/host-review/opening`）必须显式命名 `judge`。把 verdict 参数名误写成工具名会制造自激 repair loop：Reviewer 只能产 prose → Host 仍观察不到 durable judge → 新 provider run 再收到同一 missing-verdict nudge。

### 2. 判断哲学载体：Role Law + Examiner's Ledger

- `resources/provider/role/reviewer/{en,zh-CN}.md`（Role Law）承载 judgement 语义（REVIEW-JUDGEMENT-002..010 的权威文本）：
  - `Your purpose is discrimination, not rejection.` / `Acceptance must be earned. Rejection must also be earned.`（002）
  - `Judge the work that exists, by the obligation that exists, with the evidence that exists.` + `PERFECT and REVISE are the wire literals of the verdict. They are not moods...`（003）
  - `Blocking and Non-Blocking Workmanship` 一节（004）：non-blocking 不扣 acceptance；禁止 tiny typo → REVISE；禁止 PERFECT 噤声真话。
  - `PERFECT and REVISE` 一节（005）：PERFECT ≠ literal flawlessness / omniscience；REVISE 必须 purchase。
  - `Evidence, Claims, and Uncertainty` 一节（006）：evidence proportionality；保留 unresolved uncertainty。
  - `Acceptance Without Omniscience` 一节（005）：proportionate discrimination 是标准。
  - `A match is an observation. A defect is your judgment about what that observation means.`（002/006）
  - `What Rejection Must Purchase` 一节（009）：把伤口说清；禁止发明 obligation。
- `resources/provider/library/reviewer/quality-ledger/{en,zh-CN}.md`（Examiner's Ledger）：
  - 八维判断方向：Language & Algorithms / Simplicity / Structure / Granularity / Tests & Behavioral Evidence / Logic, Reliability & Boundaries / Caller Ergonomics / Completeness。
  - `The entries are not eight boxes to mark Pass.` + `It does not prescribe a report format...`（007，非 checklist / 无固定 schema）。
  - `On Materiality` 一节（004/006）：defect vs preference；`Size of edit and materiality of consequence are different quantities.`；`Do not invent materiality to justify taste.`
  - `On Evidence` / `The Weight of Judgment`（006/010）：evidence 是证据不是判断；`Do not reward confidence. Do not punish unfamiliarity.`；`Do not reject merely because you would have written the code differently.`
  - `A lens may narrow sight. It may not narrow responsibility.`（003）。
- 装载/组合权威：Role Law 经 `Infrastructure/Resources/PromptResources.fs`（Common Law → Role Law → Ledger）在 Session 加载时成为 Reviewer system prompt——**组合权威归 `cognitive-environment`（REVIEW-012）**，本包只拥有方向内容。
- `scripts/checks/semantic-anchors.mjs` reviewer family 五条 ID 逐条对应本包命题（MECHANISM：gate 校验 Role Law 文本包含这些语义锚）：

| anchor id | 对应命题 | en 正则 | zh 正则 |
|---|---|---|---|
| `discrimination` | 002 | /discrimination/i | /有区分力的判断/ |
| `rejection-must-purchase` | 005/009 | /Rejection must (also be earned\|purchase)/i | /拒绝必须买到/ |
| `non-blocking` | 004 | /non-blocking/i | /非阻断性/ |
| `perfect-not-flawless` | 005 | /PERFECT means\|literal flawlessness/i | /并不意味着字面上的毫无瑕疵/ |
| `acceptance-not-omniscience` | 005 | /omniscience/i | /全知/ |

### 3. 过程评审分型：一次判断即 terminal

- `src/Wanxiangshu/Domain/MagicTodoProcessReview.fs`：
  - `ReviewRequestKind = TodoProcessReview(TodoWriteId) | FinalityReview(FinalityRequestId × ReviewBarrierId)`——typed 分型，禁止用 `pendingChallenge` 运行时猜测混用两种业务（REVIEW-013）。
  - `renderAssignmentUserMessage` 生成过程 assignment 指令（一次判断、有界 LWR 输入、old/proposed todo；不含 challenge/2N/cohort 编排）。
  - `needsEnsureReview(accepted, concluded) = accepted ∧ ¬concluded`——Rk 义务待完成标记（节拍规则归 `obligation-ledger`）。
- `src/Wanxiangshu/Mission/Review/Judgement/Verdict.fs`：
  - `VerdictSubmission` 携带一次已由 owner CE 接受的判断身份（barrier/tree/manager/reviewer/run/call/verdict）。
  - `recordJudgement` 只 append `ReviewVerdictRecorded`，不返回“下一步” opcode。TodoProcessReview 一次 durable `judge` 即其 verdict；Finality 的 first/challenge/second 时序归 `ReviewBarrierWorkflow` direct CE（REVIEW-JUDGEMENT-008 / `review-assurance`）。
- 过程判断的 prose 义务：`TodoProcessReviewProgram.tryConclude` 只在 `ReviewerRecordFrontier` 内有非空 canonical LWR 时才 append `TodoReviewConcluded`；无 prose → `Pending "process-review LWR not record-ready"`（REVIEW-JUDGEMENT-008 的「无 prose 的 PERFECT 无效」→ 可消费侧归 `review-assurance`）。
- process PERFECT 不进入 terminal dual-PERFECT 代数：见 `review-assurance` HOW（REVIEW-020 / GLORY-058）。

### 4. 当前实现里 judgement 的「消费者」

判断被消费的路径（消费资格本身是 `review-assurance` 的事）：

```text
Reviewer prose + judge(verdict)
  ├─ FinalityReview：JudgeTool typed delivery → ReviewBarrierWorkflow CE → recordJudgement
  │                  → physical challenge → second typed delivery → completed witness
  └─ TodoProcessReview：recordJudgement → VerdictKnown → record-ready → ConsumableReview
```

## 依赖（DEPENDS ON）

| 依赖 | 理由（一句话） |
|---|---|
| `cognitive-environment` | 判断方向内容由 Role Law / Examiner's Ledger 承载，其提示词组合/装载权威由 cognitive-environment 提供（REVIEW-012）。 |
| `participant-horizon` | judgement 的参照系是 root requirement 与被审对象；root/Authority 身份与 horizon 准入由 participant-horizon 定义。 |

## 历史与弃权

### 被拒方案（保留考古，不进入 WHAT）

来自历史 why/review「备选与被拒」与历史 why/glory 条款：

- **固定 8 维 report schema / Pass 表**：拒。审查退化为填表（REVIEW-011）。→ 由 REVIEW-JUDGEMENT-007 正面规定。
- **tiny typo → 自动 REVISE**：拒。把无关痛感抬成 withhold。→ REVIEW-JUDGEMENT-004。
- **「谨慎 = 多 REVISE」/「可描述偏好即缺陷」**：拒。→ REVIEW-JUDGEMENT-002/010。
- **单 PERFECT 即确认**：拒（可被随口同意）→ 确认代数归 `review-assurance`（REVIEW-003）。
- **`verdict` 名词工具名**：拒。把判断伪装成可回声状态对象；选 `judge` 动词。→ REVIEW-JUDGEMENT-001。
- **把 review 显式化为 Manager checklist 的最后一步**：拒（GLORY-002）→ 隐藏质量门语义归 `finality`/`participant-horizon`。
- **`verdict`/`judge` 重命名为 `suicide`**：拒。judge 属于 Reviewer、suicide 属于 Manager，因果身份不同。

### 弃权记录（GARBAGE / HOW 裁决）

| 内容 | 判定 | 理由 | 记录位置 |
|---|---|---|---|
| 旧工具名 `verdict` 非法、无 alias | HOW | 当前 vocabulary；参数名非永久 contract（COVERAGE review.md GARBAGE 行） | 本 HOW §1；不进入 WHAT 命题 |
| 双 PERFECT 屏障由 Host 执行、Reviewer 提示词不灌输 | HOW | 实现位置，非 ontology（COVERAGE） | 本 HOW §2（装载权威 → cognitive-environment） |
| `ChallengeTextVersion=1`、英文 canonical 字节不变版本保持 | HOW | 文案世代机制（COVERAGE）；challenge 代数归 review-assurance | 本包不持有；见 `review-assurance` HOW |
| 历史 change（fix-revise） | GARBAGE（review transcript） | REVISE follow-up 登记；其 Gap A（record-ready fail-closed 回归）已由 review-assurance 命题 + `tests/unit/execution|temporal` 回归与 `requirements/review-assurance/tests/consumable-review.test.mjs` 承接 | 本 HOW；`review-assurance` HOW「历史与弃权」 |
| 历史 change（ce-revise-review） | GARBAGE（review transcript） | CE 复审记录；Student–Teacher 争议已被 `universal.md` / `ce-student-teacher-collapse.md` 处理（session-ontology/delegation），与本包无 normative 关系 | 本 HOW；CHANGES-AUDIT 对应行 |
| `fast-reviewer` / `deep-reviewer` 机器名 | GARBAGE | HANDOFF §12：当前 machine names 不进入永久 WHAT | 本 HOW §4 不提及；HOW.md 不落点 |
| 八维判断方向的 exact 标题清单 | HOW | 当前 craft guidance 措辞；方向集可整体重写（INDEPENDENT CHANGE） | WHAT REVIEW-JUDGEMENT-007 只冻结「非 checklist」，不冻结八个名字 |

## 边界（DOES NOT OWN）

- 一次 judgement 是否被因果确认/可消费 → `review-assurance`（witness/seal/challenge/record-ready）。
- dual-PERFECT 的计数代数、tree invalidation、attempt identity → `review-assurance`。
- 1:1 lag-1 过程评审节拍、Rk 义务派生 → `obligation-ledger`。
- 终末 cohort / rejection / blessing / rest → `finality`。
- Reviewer 提示词的组合权威（Common Law → Role Law → Ledger）→ `cognitive-environment`。
- Reviewer hidden session 生命周期 → `managed-session-lifecycle`。
- Manager 可见面（outcome/report 窄例外）→ `participant-horizon`。

## 验证与测试落点

> 每条 WHAT 命题一行落点。类型：`MOVE`（物理移入本包 tests/）、`REUSE`（留在原处，cutover 拆分）、`NEW`（本包新写）、`MECHANISM`（共享 gate，语义 owner 是本包）。
> 运行命令：`node --test requirements/review-judgement/tests/<file>`；套件级 `node requirements/verification-system/tests/run.mjs`。

### 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REVIEW-JUDGEMENT-001（judge 工具形态：typed verdict、无描述字段、不 echo；Reviewer-facing provider 指令只命名 judge，禁止已删除的 verdict 工具名） | `requirements/review-judgement/tests/judge-tool-contract.test.mjs` → `REVIEW_001_verdict_schema_allows_only_the_verdict_argument`、`REVIEW_001_verdict_parse_is_exact_perfect_or_revise`、`REVIEW_001_tool_spec_exposes_judge_with_a_single_verdict_argument`、`REVIEW_001_reviewer_provider_instructions_name_judge_never_the_removed_verdict_tool`、`REVIEW_001_receipt_does_not_echo_the_verdict` | NEW | `node --test requirements/review-judgement/tests/judge-tool-contract.test.mjs` |
| REVIEW-JUDGEMENT-001（执行面 fail-closed：非 Reviewer / 无 barrier / 非法值拒绝） | `requirements/review-judgement/tests/verdict-tool.test.mjs` → `JUDGE_spec_exposes_the_verdict_input_and_public_tool_identity`、`JUDGE_invalid_input_is_rejected_as_a_natural_consequence`、`JUDGE_missing_input_is_rejected_as_a_natural_consequence`、`JUDGE_is_unavailable_to_non_reviewer_sessions`、`JUDGE_empty_session_is_rejected_before_role_resolution`、`JUDGE_reviewer_requires_a_tool_call_id_before_review_submission`；`requirements/review-judgement/tests/verdict-tool-extras.test.mjs` → `JUDGE_unknown_owner_fails_closed_without_internal_vocabulary`、`JUDGE_missing_tree_fails_closed_without_internal_vocabulary`、`JUDGE_no_open_review_barrier_fails_closed_without_internal_vocabulary`、`JUDGE_non_reviewer_role_is_refused_before_identity_checks` | REUSE | `node --test requirements/review-judgement/tests/verdict-tool.test.mjs requirements/review-judgement/tests/verdict-tool-extras.test.mjs` |
| REVIEW-JUDGEMENT-002（acceptance/rejection 都须挣得；discrimination 不是表演） | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` → `REVIEW_011_acceptance_and_rejection_must_both_be_earned`、`REVIEW_011_discrimination_is_the_craft_not_rejection_theatre`、`REVIEW_011_a_match_is_an_observation_a_defect_is_a_judgement` | NEW | `node --test requirements/review-judgement/tests/discrimination-fixtures.test.mjs` |
| REVIEW-JUDGEMENT-003（判断相对 root requirement / 当前对象，不是 mood） | `discrimination-fixtures.test.mjs` → `REVIEW_011_judgement_is_against_the_obligation_not_the_reviewer_mood`、`REVIEW_011_a_lens_may_narrow_sight_but_not_responsibility` | NEW | 同上 |
| REVIEW-JUDGEMENT-004（material defect 才 withhold；PERFECT+minor 共存） | `discrimination-fixtures.test.mjs` → `REVIEW_011_blocking_vs_nonblocking_workmanship_are_distinguished`、`REVIEW_011_a_minor_typo_never_purchases_revise`、`REVIEW_011_perfect_does_not_silence_a_true_minor_observation`、`REVIEW_011_materiality_traces_consequence_not_edit_size` | NEW | 同上 |
| REVIEW-JUDGEMENT-005（PERFECT≠全知/字面无瑕；REVISE 必须购买） | `discrimination-fixtures.test.mjs` → `REVIEW_011_perfect_is_not_literal_flawlessness`、`REVIEW_011_rejection_must_purchase_a_materially_better_or_more_truthful_result`、`REVIEW_011_acceptance_does_not_require_omniscience` | NEW | 同上 |
| REVIEW-JUDGEMENT-006（evidence/inference/preference/defect 边界；证据相称；保留 uncertainty） | `discrimination-fixtures.test.mjs` → `REVIEW_011_evidence_must_be_proportional_to_the_claim`、`REVIEW_011_unresolved_uncertainty_is_preserved_not_laundered_into_a_verdict`、`REVIEW_011_evidence_alone_is_not_judgement` | NEW | 同上 |
| REVIEW-JUDGEMENT-007（Ledger/Rulebook 非 checklist；无固定 report schema；语义锚 gate：五条 reviewer anchor 文本在场） | `requirements/review-judgement/tests/discrimination-fixtures.test.mjs` → `REVIEW_011_the_ledger_is_a_judgement_direction_not_a_checklist`、`REVIEW_011_no_fixed_report_schema_or_eight_heading_template`；MECHANISM `scripts/checks/semantic-anchors.mjs` reviewer family → `discrimination`、`rejection-must-purchase`、`non-blocking`、`perfect-not-flawless`、`acceptance-not-omniscience` | NEW + MECHANISM | `node --test requirements/review-judgement/tests/discrimination-fixtures.test.mjs`；`node scripts/check.mjs`（semantic-anchors 项） |
| REVIEW-JUDGEMENT-008（过程评审一次 durable judge 即 terminal；无 challenge/dual-PERFECT；prose 义务） | `requirements/review-judgement/tests/process-review-judgement.test.mjs` → `REVIEW_013_the_request_kind_is_typed_process_vs_finality`、`REVIEW_013_ensure_review_stays_outstanding_until_concluded`、`REVIEW_013_process_preamble_commands_exactly_one_verdict_and_disclaims_terminal_witness`、`REVIEW_013_process_assignment_is_request_range_bounded_without_confirmation_vocabulary`、`REVIEW_013_continuation_process_assignment_does_not_replay_opening_authority`、`REVIEW_013_a_process_verdict_never_becomes_a_confirmed_witness_by_itself` | NEW | `node --test requirements/review-judgement/tests/process-review-judgement.test.mjs` |
| REVIEW-JUDGEMENT-009（拒绝把伤口说清；不发明 obligation） | `discrimination-fixtures.test.mjs` → `REVIEW_011_the_wound_must_be_clear_enough_to_purchase_the_repair`、`REVIEW_011_no_invented_obligations_to_look_careful` | NEW | 同上 discrimination 命令 |
| REVIEW-JUDGEMENT-010（不得奖励自信/惩罚不熟悉/因口味拒绝） | `discrimination-fixtures.test.mjs` → `REVIEW_011_judgement_does_not_reward_confidence_or_punish_unfamiliarity`、`REVIEW_011_novelty_and_style_preference_are_not_defects_by_themselves` | NEW | 同上 |

### 本包拥有的 semantic anchor id

`semantic-anchors.mjs` reviewer family（MECHANISM，逐 ID 归本包）：

```text
discrimination
rejection-must-purchase
non-blocking
perfect-not-flawless
acceptance-not-omniscience
```

### REUSE / cutover 拆分计划（SPLIT@cutover）

| 现有测试 | 现状 | 计划 |
|---|---|---|
| `requirements/review-judgement/tests/verdict-tool.test.mjs` | 依赖 `requirements/verification-system/tests/support/plugin-fixture.mjs`（不在 `tests/unit/support/**` 白名单），按契约 §4.6 不可随包移动 | cutover 时将 plugin-fixture 提升为共享 support 后移入本包；或按断言逐条搬入 `judge-tool-contract.test.mjs` |
| `requirements/review-judgement/tests/verdict-tool-extras.test.mjs` | 直接 import `dist/fable_modules/...`，契约 §4.1 禁止移动 | cutover 时剥离 fable_modules import 后移入 |
| `tests/unit/verify/...`（language-parity-gate、tool-referential-integrity 等） | MECHANISM/SPLIT 混合 | `judge` 工具名引用完整性断言归 `action-affordance`/`capability-enforcement`（ARCH-007），与本包无 assertion 冲突 |

### 可红性说明

- `judge-tool-contract.test.mjs`：若 `judge` 增加描述字段、回执开始 echo verdict、或解析器接受第三个值，schema/parse/prose 断言即红。
- `discrimination-fixtures.test.mjs`：若 Role Law / Examiner's Ledger 丢失「discrimination / earned both ways / non-blocking / not-flawless / omniscience / checklist 禁令 / materiality」任一承诺句，对应 fixture 即红——这不是装饰性锚点，而是判断语义合同的文本证明。
- `process-review-judgement.test.mjs`：若过程评审误走 challenge/dual-PERFECT，或 `TodoReviewConcluded` 在无 prose 时被接受，断言即红。

### 未覆盖与理由

- 真实模型行为 canary（模型真的按 discrimination 判断）：judgement 最终由 LLM 执行，行为级 canary 属 `verification-system` 的 e2e 阶梯（Long Stroke / finality-cohort-law canary），不在本包单元证明范围（PROOF-MAP 注明的「不能只靠 prompt anchors」已由 discrimination-fixtures 的可失败文本契约 + 工具面行为测试补齐，剩余缺口在 e2e）。
