# change-integration — 可观察合同

本文件是 `change-integration` 包的唯一 normative 语义合同。证据指针 → `PROOF.md`。

## CHGINT-001：独立道路进共享 ref 走 publish lifecycle

独立 worktree/job 的 candidate 进入共享 target ref 必须经过完整的 publish lifecycle：
candidate 产出 → rebase → post-rebase review → 短 gate CAS → ff-only 发布 → 持久事实记录
（ORCH-004 `rebaseReviewPublishLoop`）。任何一步缺失或跳过 = 共享 ref 上出现未声称的提交。

含义/动机：共享 ref 的每个新提交都必须有「谁、基于什么 head、经过什么 review」的可重放事实链。

边界：效果记账（Requested/Accepted/Published 分型）→ `effect-accounting`；本包拥有编排侧的生命周期。

证据：MOVE `tests/job.test.mjs`（`ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity`、
`ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out`）。

## CHGINT-002：Clean Gate——工作区必须干净才受理

每次用户消息进入 Orchestrator 前，工作区必须 clean：计入 staged / tracked unstaged / untracked /
submodule dirty；默认不计 ignored（ORCH-002）。**禁止**自动 stash、自动 commit、猜测用户意图清理。
插件 runtime、spool、lock、worktree 必须位于目标工作树之外，以免污染 clean 判定。

含义/动机：编排必须建立在可命名的 Git 事实上；脏工作区上猜意图 = 恢复无法复现。

证据：MOVE `tests/git-operations.test.mjs`（`GIT_is_dirty_true_only_on_nonempty_porcelain`、
`GIT_ff_merge_refuses_dirty_target_worktree`）；REUSE `requirements/change-integration/tests/runtime.test.mjs`
（`ORCH_007_NeedsReview_preserves_the_active_worktree`——worktree 不被随意清理）。

## CHGINT-003：candidate/rebase/publish claim/CAS 的原子边界

候选在独立 worktree 中产生（candidate 状态事实化）；rebase 到目标 head 之上；post-rebase 双 PERFECT
（同 worktree / 同 Manager）；短 gate 内 re-read head 并 CAS（ORCH-004/005）。candidate 与 CAS 之间
的任何一步都有持久事实标记（`CandidateReady` / `RebasedCandidateReady` / `PublishClaimed` /
`Published`），没有「CandidateCreated 然后随便走」的无确定恢复动作分支（ORCH-006）。

含义/动机：原子边界 = 每一步都有可恢复的确定下一动作；没有事实标记的中间态 = 恢复时不可判定。

证据：MOVE `tests/job.test.mjs`（`ORCH_006_only_progress_ever_changes_after_creation`、
`ORCH_007_progress_that_needs_no_head_derives_its_action_from_the_fact_alone`、
`ORCH_007_every_progress_case_yields_exactly_one_action`）。

## CHGINT-004：共享 ref mutation 是唯一短 critical section

Integration Gate **只**覆盖目标 ref 的 mutation 窗口；**不**在 LLM Review 或冲突修复期间持有；
多 Job 可并行 rebase / review；只有推 ref 需要 CAS（ORCH-005）。Gate 不得提前到 `runManagerJob`
入口「先锁后干活」。

含义/动机：长 review 持全局锁 = 把并行工作错误串行化（RED 的第二形态）；锁的粒度 = ref mutation。

证据：MOVE `tests/integration-gate.test.mjs`（`GATE_lock_path_is_stable_per_repo_and_branch`、
`GATE_second_acquire_on_held_lock_eventually_fails`——互斥只在持有期间成立）；
MOVE `tests/job.test.mjs`（`ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out`）。

## CHGINT-005：conflict 在门外 repair/review，再重新 claim

冲突发现（`ConflictDetected`：CandidateCommit + TargetHeadSnapshot + ConflictFiles + DiagnosticsDigest）
后，在同 worktree、同 Manager 上解决；解决完成后重新进 rebaseReviewPublishLoop 重新 claim
（ORCH-003/004/007）。一个 `ManagerJob` 生命周期内不重新创建 worktree、不更换 Manager、冲突仍返回
同一个 Manager。

含义/动机：冲突修复是门外工作（不持锁）；「同一 job 同一 manager」保证修复上下文连续。

证据：MOVE `tests/job.test.mjs`（`ORCH_003_a_conflict_goes_back_to_the_same_manager_with_the_conflicted_files`）、
`tests/worktree-resource.test.mjs`（`WORKTREE_create_returns_owned_resource_and_marks_path_identity`、
`WORKTREE_release_removes_worktree_and_branch_once`）。

## CHGINT-006：restart 后从 Journal 最后事实 fold 出唯一恢复动作

崩溃恢复完全依赖 Journal 最后一条事实折叠，决定**唯一**恢复动作（ORCH-007）：

```text
Published / JobAbandoned → 清理 worktree，移出活跃 Map
JobFailed                → 清理 worktree，明确失败
无事实                   → Job 不存在
RebasedCandidateReady    → 进 CAS：head 仍为 snapshot 则 ff，否则重 rebase+review
ConflictDetected         → 同 worktree/同 Manager 恢复冲突解决
CandidateReady           → 进 rebaseReviewPublishLoop
ManagerJobCreated        → 从 worktree 恢复同一 Manager 继续
```

含义/动机：事实链不可跳步；磁盘状态可伪造、可停写，故恢复只信最后事实（PERSIST-009 边界）。

边界：durable facts 的存储与 fold → `durable-events`；崩溃后重入普通程序 → `crash-reconciliation`。

证据：MOVE `tests/job.test.mjs`（`ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised`、
`ORCH_006_a_terminal_job_accepts_no_further_progress`、`ORCH_006_all_three_terminal_cases_end_the_job`、
`ORCH_007_every_progress_case_yields_exactly_one_action`）；REUSE `requirements/change-integration/tests/runtime.test.mjs`
（`ORCH_007_NeedsReview_preserves_the_active_worktree`）。

## CHGINT-007：PublishClaimed 三分支固定顺序、穷尽互斥

`currentHead = GetTargetHead(TargetRef)`（失败 → fail closed）后按固定顺序判定（ORCH-007）：

```text
1. currentHead = rebasedCommit   → ff 已完成，补写 Published（幂等）
2. currentHead = ExpectedHead    → 从未 ff；短 gate + 再确认 head → ff-only → Published
3. 其它                          → claim 过期；丢弃旧 post-rebase witness；回 rebaseReviewPublishLoop
```

三分支穷尽且互斥，顺序不可换；折叠会造出不可判定的中间态。

含义/动机：CAS 窗口崩溃后必须能区分「已 ff / 未 ff / 过期」；顺序固定 = 恢复确定。

证据：MOVE `tests/job.test.mjs`（`ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order`、
`ORCH_008_an_unreadable_target_head_fails_closed_for_every_head_dependent_case`）。

## CHGINT-008：target ref 安全——冻结 + ff-only CAS

目标 branch 在 `commission` 时冻结（`TargetBranchFrozen`）；读取 target head 失败 → fail closed，
不得静默落到 `HEAD`；ff-only publish 必须同时满足：当前 branch == 冻结 target，且 current head ==
expected head（ORCH-008）。

含义/动机：目标 ref 的身份与 head 在 claim 期间必须确定；静默 fallback 会把 candidate 推给错误的 ref。

证据：MOVE `tests/git-operations.test.mjs`（`GIT_freeze_target_branch_reads_symbolic_ref`、
`GIT_freeze_target_branch_refuses_detached_head`、`GIT_freeze_target_branch_blank_stdout_is_detached`、
`GIT_get_target_head_missing_branch`、`GIT_ff_merge_refuses_when_repo_on_wrong_branch`、
`GIT_ff_merge_refuses_when_target_moved_since_head_read`、`GIT_ff_merge_refuses_non_fast_forward_candidate`、
`GIT_ff_merge_ref_moved_lock_diagnostic_maps_to_cas_error`）。

## CHGINT-009：same-road continuation 与独立 road 的 integration identity

`commission` 续做既有路（省略 `calling`，按 Byname 识别）时，续做 = 同 job / 同 worktree / 同 session
（墙内事实）；不新建 worktree、不换 Manager、不重绑 Persona（AGENT-015、ORCH-003）。独立 road 的
integration identity 由 `ManagerJobId` + `WorktreeIdentity` 承载（identity 稳定，path 仅诊断）。

含义/动机：续做必须继续同一条 integration 路线的物理身份，否则恢复/发布上下文断裂。

边界：road 语义（何时续做/新开）→ `delegation`；本包拥有 identity 的墙内实现。

证据：MOVE `tests/job.test.mjs`（`ORCH_003_only_progress_ever_changes_after_creation`、
`ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic`、
`ORCH_003_a_manager_session_resolves_to_its_one_job`）；REUSE `tests/unit/orchestrator/host.test.mjs`
（`HOST_ContinueManagerJob_resumes_a_forked_job_in_its_worktree`）。

## CHGINT-010：长 review/repair 不占全局门

post-rebase 双 PERFECT 与冲突修复都在 gate 之外进行（ORCH-005）：gate 只覆盖「re-read head →
ff-only → 写 Published」窗口；review 期间其它 Job 可自由 rebase/review/publish。

含义/动机：这是本包 WHY 的正面形态——安全不通过串行化购买。

证据：MOVE `tests/job.test.mjs`（`ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out`、
`ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved`）。

## CHGINT-011：墙内机械不进 provider horizon

Gate / Clean Gate / target head / `job_id` / worktree / CAS 属墙内机械；不得以 `status`/`error` DTO
或 UUID 塞进 provider horizon（ORCH-005、EXEC-030、ARCH-014）。provider 面只有 `commission` 成功后果
（Byname 承接 charge）与 join/horizon 的自然语言 + WorkRecord。

含义/动机：编排者看见集成机械，会把 CAS/worktree 当 craft，污染「拥有道路、不拥有机械」的 epistemic
边界（`archive/docs/why/orchestrator.md` 备选节）。

边界：完整准入过滤法则 → `participant-horizon`；本包只声明发布机械的隐藏义务。

证据：REUSE `tests/unit/orchestrator/host.test.mjs`（provider 面只暴露自然语言后果）；MOVE
`tests/job.test.mjs`（墙内事实不含 provider DTO 字段——结构级）。

## CHGINT-012：恢复禁止扫盘反推、禁跳步

恢复时**禁止**：新建 worktree、换 Manager、跳过 post-rebase review、用文件系统状态代替事实
（ORCH-007）。恢复路径可使用墙内 `job_id`/worktree；不得把这些字段投影回 provider horizon。

含义/动机：扫描磁盘状态反推进度 = 磁盘可伪造；跳步恢复 = 未过 review 的 candidate 进入共享 ref。

证据：MOVE `tests/job.test.mjs`（`ORCH_007_progress_that_needs_no_head_derives_its_action_from_the_fact_alone`）；
REUSE `requirements/change-integration/tests/runtime.test.mjs`（`ORCH_007_NeedsReview_preserves_the_active_worktree`）。

## CHGINT-013：target 变化后旧 post-rebase witness 作废

CAS 重读 head 发现 target 已移动（分支 3）时：丢弃旧 post-rebase witness，回 rebaseReviewPublishLoop
重新 rebase + 重新双 PERFECT；**绝不**复用旧 witness 发布到新 head 之上（ORCH-005/007）。

含义/动机：witness 绑定特定 target head；旧 witness 复用 = 未经验证的提交进入共享 ref。

边界：witness 的有效性/失效语义（rebase 后旧 witness 无效）→ `review-assurance`；本包拥有 CAS 侧
「作废并重新 claim」义务。

证据：MOVE `tests/job.test.mjs`（`REVIEW_008_a_moved_target_discards_the_post_rebase_witness`——
CAS 行为面）；`GIT_ff_merge_refuses_when_target_moved_since_head_read`。
