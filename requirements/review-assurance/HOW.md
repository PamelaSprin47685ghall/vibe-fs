# review-assurance — HOW

## 1. 见证模型与因果证明

- **Witness 数据模型**：`ReviewWitness` 仅包含 `NoReview`、`RevisionWitness` 与 `Confirmed` 三种终态。不存在“已第一次 PERFECT、等待第二次”的持久化状态。`Confirmed` 结构自包含两次判断标识、代码树哈希与物理交互消息标识，有效性通过纯函数派生计算。
- **Cohort 资格**：`ReviewWitness.isQualifiedConfirmationFor` 同时校验 cohort reviewer、barrier、outer/nested tree、两次 nested reviewer 与 distinct ProviderRun/ToolCall；Finality projection 复用该谓词，不从外围字段补全或覆盖 witness。
- **物理因果绑定**：工具层通过 `ToolRuntimeScope` 提取当前执行绑定的 `PhysicalUserMessageId`，并在向工作流投递强类型 judgement 时传递 `Accept`、`Challenge` 与 `Reject` 完成能力。第二判断等待器在触发首次 `Challenge` 之前预先就位，因果关系由调用顺序天然保证。

## 2. 终审双重确认与状态流转

终审因果工作流直接由宿主语言控制流驱动：
1. 注册终止观察与首次判断等待器，启动审查。
2. 捕获首次 `judge(PERFECT)` 并持久化事实；预先注册第二判断等待器，随后调用首次投递的 `Challenge()` 能力返回质疑提示。
3. 审查者在同一物理交互或经由 nudge 续接后发起第二次判断；工作流校验两次调用的独立性（不同 ProviderRun/ToolCall）与物理提示一致性，校验通过后一次性写入 `ConfirmedReviewWitness` 并完成调用。
4. 任一阶段收到 REVISE 或发生代码树漂移立即以失败关闭，不持久化半程位置。

## 3. 终审裁决与闭环机制

- **Direct CE 独占驱动**：终审判断由 `ReviewBarrierWorkflow` 直接管理，单次或双重判断通过强类型 `ReviewJudgementInbox` 交互完成。
- **排他 Frontier 与中断**：Reviewer 完成裁决后，通过排他 frontier 冻结闭环，并以事件驱动等待下游消费。

## 4. 依赖声明

```text
DEPENDS ON: review-judgement, semantic-trace, durable-events, causal-wait
```

## 5. 边界（DOES NOT OWN）

- 裁决词的语义与判定哲学 → `review-judgement`
- 过程评审 1:1 节拍与账本消费门槛 → `obligation-ledger`
- 终结前置条件与经验分类 → `finality`
- 规范工作记录 LWR 的表示与格式 → `work-record`
- 事件存储与快照机制 → `durable-events`
- 因果等待底座 → `causal-wait`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REVIEW-ASSURANCE-001 | `requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-001] REVIEW_003_two_attempts_require_distinct_run_and_call`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-001] REVIEW_003_confirmation_still_requires_distinct_attempts` |
| REVIEW-ASSURANCE-002 | `requirements/review-assurance/tests/host-reverify.test.mjs::WHAT[REVIEW-ASSURANCE-002] HOST_reverify_accepts_second_PERFECT_after_typed_challenge_on_same_physical_prompt`；`requirements/review-assurance/tests/host-reverify.test.mjs::WHAT[REVIEW-ASSURANCE-002] HOST_reverify_normal_terminal_before_first_judgement_nudges_without_a_waiter_gap`；`requirements/review-assurance/tests/host-reverify.test.mjs::WHAT[REVIEW-ASSURANCE-002] HOST_reverify_normal_terminal_before_second_judgement_nudges_and_confirms` |
| REVIEW-ASSURANCE-003 | `requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-003] REVIEW_004_attempt_identity_names_all_five_components`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-003] REVIEW_004_duplicate_attempt_is_refused` |
| REVIEW-ASSURANCE-004 | `requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-004] REVIEW_005_confirmedReviewer_is_derived_from_witness`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-004] confirmed_review_witness_is_pure_projection_from_durable_facts` |
| REVIEW-ASSURANCE-005 | `requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-005] REVIEW_006_confirmed_witness_is_self_contained_typed_evidence`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-005] confirmed_review_witness_binds_tree_and_contains_cohort_evidence` |
| REVIEW-ASSURANCE-006 | `requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-006] REVIEW_008_tree_change_invalidates_completed_witness`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-006] REVIEW_008_new_barrier_requires_a_fresh_completed_CE`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-006] REVIEW_008_late_old_confirmation_cannot_satisfy_current_barrier`；`requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-006] candidate_verification_verifies_candidate_tree_and_rejects_stale_witness` |
| REVIEW-ASSURANCE-007 | `requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_positive_unique_incomplete_assistant_with_matching_parent`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_parent_id_mismatch_is_no_bindRunable_run`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_completed_assistant_is_no_bindRunable_run`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_non_assistant_never_bindRuns`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_ambiguous_run_when_two_incomplete_children`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_compaction_assistant_is_no_bindRunable_run`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_not_latest_run_when_newer_assistant_exists`；`requirements/review-assurance/tests/seal-bind.test.mjs::WHAT[REVIEW-ASSURANCE-007] HOST_010_summary_true_is_compaction` |
| REVIEW-ASSURANCE-008 | `requirements/review-assurance/tests/consumable-review.test.mjs::WHAT[REVIEW-ASSURANCE-008] ReviewBarrierWorkflow Direct CE drives review judgements` |
| REVIEW-ASSURANCE-009 | `requirements/review-assurance/tests/consumable-review.test.mjs::WHAT[REVIEW-ASSURANCE-009] judge_only_closure_projects_the_exact_tool_result_as_terminal_frontier` |
| REVIEW-ASSURANCE-010 | `requirements/review-assurance/tests/witness.test.mjs::WHAT[REVIEW-ASSURANCE-010] REVIEW_002_REVISE_is_a_completed_revision_fact` |
| REVIEW-ASSURANCE-011 | `requirements/review-assurance/tests/consumable-review.test.mjs::WHAT[REVIEW-ASSURANCE-011] dual PERFECT witness chain is self-contained` |
| REVIEW-ASSURANCE-012 | `requirements/review-assurance/tests/consumable-review.test.mjs::WHAT[REVIEW-ASSURANCE-012] request-range bounded evidence rejects unbound session head` |
| REVIEW-ASSURANCE-013 | `requirements/review-assurance/tests/review-requirement.test.mjs::WHAT[REVIEW-ASSURANCE-013] requirement identity is the Authority Root and duplicate roots collapse`；`requirements/review-assurance/tests/review-requirement.test.mjs::WHAT[REVIEW-ASSURANCE-013] confirmation clears its covered batch but replay cannot clear a later requirement` |
