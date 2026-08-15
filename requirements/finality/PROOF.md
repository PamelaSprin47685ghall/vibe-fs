# finality — PROOF

行为合同：`WHAT.md`（FINALITY-001..028）。实现模型：`HOW.md`。

## 测试资产

### 本包 tests/（`requirements/finality/tests/`）

| 文件 | 来源 | 类型 | 断言数 |
|---|---|---|---|
| `manager-finality-disposition.test.mjs` | NEW | `ManagerFinality.classifyEnding` / `admitLabor` 纯代数 + `Roles_isAllowed` | 11 |
| `manager-job-no-resurrection.test.mjs` | NEW | FINALITY-028：terminal 不复活 / active 同 session·worktree 续做 | 4 |

`node --test requirements/finality/tests/manager-finality-disposition.test.mjs` 单独跑绿。
`node --test requirements/finality/tests/manager-job-no-resurrection.test.mjs` 单独跑绿。

### REUSE（留在原处；glory 族按 PROOF-MAP KEEP，多 owner 交叉 SPLIT@cutover）

| 文件 | 锚点 | 本包拥有的断言 | SPLIT@cutover |
|---|---|---|---|
| `requirements/finality/tests/lifecycle.test.mjs` | `GLORY_010_LifeOpened_opens_the_first_life`、`GLORY_012_a_second_life_cannot_open_while_one_is_active`、`GLORY_045_FinalityRequested_is_rejected_while_a_request_is_open`、`GLORY_055_a_rejected_request_closes_and_a_new_suicide_opens_a_new_one`、`GLORY_060_a_blessing_leaves_the_life_open_until_the_second_suicide`、`GLORY_062_isLifeArchived_true_only_after_life_completed`、`GLORY_057_FinalityUndecided_closes_the_request_without_a_wound_record`、`GLORY_057_a_revise_closes_finality_without_confirming_the_life`、`GLORY_066_lifecycle_facts_round_trip_through_ndjson`、`GLORY_029_idle_encouragement_golden_bytes`、`GLORY_057_host_undecidable_golden_bytes`、`GLORY_052_finality_rejection_renders_work_record_as_guidance_comments`、`GLORY_076_finality_three_experiences`、`GLORY_064_reawakening_golden_bytes` | lifecycle 事实代数、rejection 关闭、blessing 不结束、rest 归档、undecided 收束、三经验文案、idle、Reawakening | `GLORY_075`（participant-identity/prefix-stability）、`SURFACE_002`（provider-language）、`SURFACE_005`（participant-horizon）、`SURFACE_006`（verification-system）、`GLORY_074`（obligation-ledger）、`GLORY_014/019/021`（GARBAGE legacy，迁移窗口后随 absence 政策退役） |
| `requirements/finality/tests/finality-cohort-law.test.mjs` | `rosterOf` / `graduatedReviewer` / enlistment 幂等 / `dropEphemeral` 恢复的 theorem 集 | roster 代数、graduate 推导、durable resolution 恢复 | witness/ConfirmedReviewWitness 的代数断言 → `review-assurance` |
| `requirements/finality/tests/rewrite-consistency.test.mjs` | `GLORY_015_opening_rewrite_is_byte_identical_across_requests`、`GLORY_012_host_title_request_never_opens_a_life`、`GLORY_015_rewrite_survives_a_persisted_rewritten_message` | Opening 改写幂等；host title 请求不开 Life | ARCH-004 seal 断言 → `prefix-stability` |
| ~~`tests/unit/glory/manager-lifecycle-gate.test.mjs`~~ | `GLORY_018_in_progress_manager_turn_never_activates` | 生产 Activation 缺席（GARBAGE 侧回归） | 已 DELETE（Wave 2a）：仅证明迁移完成（PROOF-MAP 强制删除清单第 6 项） |
| `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` | `TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission`、`TODO-006 T2 prepare succeeds once T1 process review is Concluded` | drain 输入的 ConsumableReview gate | 其余 → obligation-ledger / effect-accounting / host-boundary |

### e2e（cutover 范围外，记录指针）

- ~~`tests/e2e/cases/manager-unhappy-path.test.mjs`~~：完整自杀/拒绝/继续/祝福/rest 剧本（glory.md proof 第 3 层；stroke 13 last_words 逐字 terminal；cases/ 已随 G4R cutover 删除，剧本并入 Long Stroke）。
- ~~`tests/e2e/cases/finality-cohort-law.test.mjs`~~：GLORY_074/075 record-ready 崩溃 canary（→ review-assurance 交叉；cases/ 已删除，canary 语义由本包 finality-cohort-law 测试承接）。
- `requirements/verification-system/tests/e2e/support/magic-todo-host-canary-plugin.mjs`：canary A/E/G/H 真实 Host 侧（→ host-boundary）。

## 命题 → 落点

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| F-1 001 | `tests/manager-finality-disposition.test.mjs` `FINALITY-001 only the Manager holds ToolPermission.Finality` | NEW | `node --test requirements/finality/tests/manager-finality-disposition.test.mjs` |
| F-2 002 | F-3..F-7 组合（前置 + drain + 门禁）；总纲由各落点共同承担；REUSE lifecycle `GLORY_010/045/060` | NEW + REUSE | 见 F-1 |
| F-3 003 | `tests/manager-finality-disposition.test.mjs` `FINALITY-007/040 an open request resumes the same ToolCallId replay`、`FINALITY-007/057 an open request with no enlisted members is recoverable`、`FINALITY-040 a request already in motion waits for the current cohort`（受理失败路径零创建）；REUSE lifecycle `GLORY_045_FinalityRequested_is_rejected_while_a_request_is_open` | NEW + REUSE | 见 F-1 |
| F-4 004 | `tests/manager-finality-disposition.test.mjs`：无 durable plan commitment 时 `ContinuePlanning`，即使已有 accepted `planComplete=false` planning checkpoint 也不得进入 Finality；账本侧见 obligation-ledger commitment projection proof | REWRITE + REUSE | 见 F-1 |
| F-5 005 | `tests/manager-finality-disposition.test.mjs` `FINALITY-060/062 a blessing leaves the Life open and the second suicide is the rest path`（二次 suicide 仍走 rest 而非新 cohort）；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded`（drain 的 ConsumableReview gate） | NEW + REUSE | 见 F-1 |
| F-6 006 | `tests/manager-finality-disposition.test.mjs` `FINALITY-054/055 rejection keeps the same Life and a new suicide begins fresh Finality`（REVISE 后不 BeginFinality 之前置）；REUSE lifecycle `GLORY_057_a_revise_closes_finality_without_confirming_the_life` | NEW + REUSE | 见 F-1 |
| F-7 007 | `tests/manager-finality-disposition.test.mjs` `FINALITY-020 disposition never derives from narrative text`（有 obligations 的 Life 无机械 completeness 判定）；HOW 记录机械 gate 缺失 | NEW | 见 F-1 |
| F-8 008 | REUSE lifecycle `GLORY_045_FinalityRequested_is_rejected_while_a_request_is_open`、`GLORY_055_a_rejected_request_closes_and_a_new_suicide_opens_a_new_one`（request 生命周期 durable） | REUSE | lifecycle SPLIT@cutover |
| F-9 009 | REUSE `requirements/finality/tests/finality-cohort-law.test.mjs` roster/enlistment theorems | REUSE | finality-cohort-law SPLIT@cutover |
| F-10 010 | REUSE `requirements/finality/tests/finality-cohort-law.test.mjs` `graduatedReviewer` theorems（graduate 只由 enlistment + witness 推导） | REUSE | 同上 |
| F-11 011 | REUSE lifecycle `GLORY_057_a_revise_closes_finality_without_confirming_the_life`（REVISE 关 cohort 不落 FinalityRejected）；`GLORY_055_...`（rejected 后新 suicide 开新 request） | REUSE | lifecycle SPLIT@cutover |
| F-12 012 | REUSE lifecycle `GLORY_052_finality_rejection_renders_work_record_as_guidance_comments`（rejection evidence 渲染）；steer 双轨交付 e2e 见指针（cutover 范围） | REUSE | lifecycle SPLIT@cutover |
| F-13 013 | REUSE lifecycle `GLORY_076_finality_three_experiences`、`GLORY_052_finality_rejection_...`、`GLORY_057_host_undecidable_golden_bytes` | REUSE | lifecycle SPLIT@cutover |
| F-14 014 | `tests/manager-finality-disposition.test.mjs` `FINALITY-054/055 rejection keeps the same Life...`（同 Life 继续；BeginFinality）；REUSE lifecycle `GLORY_055_...`（Rejected 永不 blessing） | NEW + REUSE | 见 F-1 |
| F-15 015 | REUSE lifecycle `GLORY_060_a_blessing_leaves_the_life_open_until_the_second_suicide`（Blessing 不结束 Life / 不 Dispose process duty 交叉）；过程 duty 保留 → obligation-ledger O-20 | REUSE | lifecycle SPLIT@cutover |
| F-16 016 | REUSE lifecycle `GLORY_060_a_blessing_leaves_the_life_open_until_the_second_suicide`（Blessed 后 Completed=false） | REUSE | lifecycle SPLIT@cutover |
| F-17 017 | `tests/manager-finality-disposition.test.mjs` `FINALITY-060/062 a blessing leaves the Life open and the second suicide is the rest path`（CompleteBlessedLife 分派）；REUSE lifecycle `GLORY_062_isLifeArchived_true_only_after_life_completed`（归档 + CompletedTerminal） | NEW + REUSE | 见 F-1 |
| F-18 018 | `tests/manager-finality-disposition.test.mjs` `FINALITY-040 an open request owns the Life: Manager labor is deferred`（open request 停放劳动；resolved 不阻塞） | NEW | 见 F-1 |
| F-19 019 | REUSE lifecycle `GLORY_029_idle_encouragement_golden_bytes`（idle 只鼓励；open/completed 不发送的 fold 侧由 lifecycle 组合断言） | REUSE | lifecycle SPLIT@cutover |
| F-20 020 | REUSE lifecycle `SURFACE_005_manager_surface_has_no_forbidden_words`（无隐藏机制词——admission 归 participant-horizon，本包引用其 proof）；`GLORY_052_...`（rejection 渲染无机制解释） | REUSE | participant-horizon SPLIT@cutover |
| F-21 021 | `tests/manager-finality-disposition.test.mjs` `FINALITY-020 disposition never derives from narrative text`；REUSE lifecycle `GLORY_066_lifecycle_facts_round_trip_through_ndjson`、`GLORY_010_LifeOpened_opens_the_first_life` | NEW + REUSE | 见 F-1 |
| F-22 022 | `requirements/finality/tests/life-admission.test.mjs` `FINALITY_022_AgentOwner_migration_is_admitted_only_before_any_Life_history` + `FINALITY_022_HumanRoot_opening_requires_the_exact_authority_root_message_id`；`requirements/finality/tests/rewrite-consistency.test.mjs` `FINALITY_022_active_HumanRoot_profile_does_not_make_another_user_message_a_root`；REUSE lifecycle `GLORY_012_a_second_life_cannot_open_while_one_is_active`、`GLORY_064_reawakening_golden_bytes` | NEW + REUSE | `node --test requirements/finality/tests/life-admission.test.mjs requirements/finality/tests/rewrite-consistency.test.mjs` |
| F-23 023 | REUSE `requirements/finality/tests/rewrite-consistency.test.mjs` `GLORY_015_opening_rewrite_is_byte_identical_across_requests`、`GLORY_015_rewrite_survives_a_persisted_rewritten_message` | REUSE | rewrite-consistency SPLIT@cutover |
| F-24 024 | `tests/manager-finality-disposition.test.mjs` `FINALITY-005 ... fail closed`（suicide 只在无有用工作后）；工作期输入不改写 → obligation-ledger O-17/O-25 交叉（Opening 不因工作期输入移动） | NEW + REUSE | 见 F-1 |
| F-25 025 | REUSE lifecycle `GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once`（inert decode 回归）；`GLORY_010/062`（completed 保持） | REUSE | lifecycle SPLIT@cutover |
| F-26 026 | `tests/manager-finality-disposition.test.mjs` `FINALITY-054/055 rejection ...`（undecided/resolved 不阻塞劳动——LaborMayContinue）；REUSE lifecycle `GLORY_057_FinalityUndecided_closes_the_request_without_a_wound_record`、`GLORY_057_host_undecidable_golden_bytes` | NEW + REUSE | 见 F-1 |
| F-27 027 | REUSE lifecycle `GLORY_029_idle_encouragement_golden_bytes`（idle 只在非 finality 情形）；join 资源检查 → host-boundary canary 指针 | REUSE | lifecycle SPLIT@cutover |
| F-28 028 | `tests/manager-job-no-resurrection.test.mjs` `FINALITY-028 a terminal ManagerJob is not active and does not resume` / `later progress cannot reopen` / `replaying ManagerJobCreated cannot re-enlist` / `an active owned job continues on the same session and worktree` | NEW | `node --test requirements/finality/tests/manager-job-no-resurrection.test.mjs` |

## 覆盖统计

- 命题 28 / 落点 28（NEW 2 文件；REUSE 4 文件族；GAP 0）。
- 移动文件：0（glory 族按 PROOF-MAP KEEP 保留原位，SPLIT@cutover 拆分见上表）。
- 新写文件：2（`manager-finality-disposition.test.mjs` 11 断言；`manager-job-no-resurrection.test.mjs` FINALITY-028）。

## semantic anchor id（semantic-anchors.mjs，MECHANISM 逐 ID 归包）

本包声明拥有 `scripts/checks/semantic-anchors.mjs` 中 manager 角色的下列 anchor id
（`ROLE_SEMANTIC_ANCHORS.manager`；机制文件在 cutover 时按此声明标注 owner）：

- `returned-record` —— 返回的记录只通过它所建立的事实改变 mission（FINALITY-012/016：
  rejection/blessing 的 LWR 是 evidence 不是新指令）。
