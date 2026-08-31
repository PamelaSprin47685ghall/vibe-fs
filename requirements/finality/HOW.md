# finality — HOW

## 1. 终结流转与状态投影

`finality` 统一管理 Manager 生命周期的终结判定与审查编排：

- **状态投影模型**：`LifeProjection` 与 `FinalityRequestProjection` 增量维护活跃请求、审查员名册、最新 Blessing、完成状态以及决议状态。状态由事实事件纯推导，不包含内存程序计数器。
- **终结意图分类（classifyEnding）**：纯函数将调用上下文分派为 `ContinuePlanning`、`AlreadyCompleted`、`ResumeRequest`、`RecoverRequestWithoutReviewers`、`WaitForCurrentRequest`、`CompleteBlessedLife` 或 `BeginFinality`。分派结果由 Finality 内部处理，不向外层暴露操作码。
- **JS 语义边界**：`FinalitySurface` 统一对外暴露生命周期与任务历史的投影视图，测试与外围系统仅消费结构化数据，F# 内部 union 与实现细节封装在领域内部。

## 2. 审查编排与收束机制

- **Cohort 组装与毕业推导**：每次终审请求由纯函数 `rosterOf` 组装花名册，包含一名新审查员与历史未毕业审查员。审查员按普通规则随合格 witness 毕业。
- **REVISE 双轨收束**：任一审查员给出 REVISE 后，工作流立即关闭该 cohort；首个 REVISE 作为工具结果返回，后续到达的 REVISE 物化为独立 steer continuation 交付。
- **Blessing 与 Rest 路径**：全员达成双重 PERFECT 且代码树一致后，物化规范 LWR bundle 并写入 `FinalityBlessed`；二次调用 `suicide` 时在排查阻塞 REVISE 后直接落盘 `LifeCompleted`，输出逐字等于 `last_words`。

## 3. 依赖声明

```text
DEPENDS ON: obligation-ledger, review-assurance, participant-horizon
```

## 4. 边界（DOES NOT OWN）

- 账本维护、计划承诺与过程评审 1:1 节拍 → `obligation-ledger`
- 见证结构、因果确认代数与 record-ready 判定 → `review-assurance`
- 隐藏机制的词汇过滤与信息准入规则 → `participant-horizon`
- 规范工作记录 LWR 的物化与格式 → `work-record`
- 进程级故障终止与崩溃恢复 → `crash-reconciliation`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| FINALITY-001 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-001] only the Manager holds ToolPermission.Finality` |
| FINALITY-002 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-002] finality eligibility is the combination of commitment, request, and experience typing`；`requirements/finality/tests/blessing-admission.test.mjs::WHAT[FINALITY-002] finality_admission_grants_blessing_for_matching_tree_witness`；`requirements/finality/tests/blessing-admission.test.mjs::WHAT[FINALITY-002] finality_admission_rejects_structurally_forged_confirmations`；`requirements/finality/tests/blessing-admission.test.mjs::WHAT[FINALITY-002] finality_admission_rejects_stale_witness_when_tree_differs` |
| FINALITY-003 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-003] an open request resumes the same ToolCallId replay`；`requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-003] an open request with no enlisted members is recoverable`；`requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-003] a request already in motion waits for the current cohort` |
| FINALITY-004 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-004] no accepted planComplete=true commitment stays at Planning Table` |
| FINALITY-005 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-005] the rest-path suicide is a drain, not a new cohort` |
| FINALITY-006 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-006] drain outcomes are two-typed: Revision (REVISE) vs Confirmed (PERFECT)` |
| FINALITY-007 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-007] no mechanical terminal-todo completeness gate` |
| FINALITY-008 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-008] FinalityRequested is rejected while a request is open`；`requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one` |
| FINALITY-009 | `requirements/finality/tests/finality-cohort-law.test.mjs::WHAT[FINALITY-009] roster is ungraduated history plus exactly one new`；`requirements/finality/tests/finality-cohort-law.test.mjs::WHAT[FINALITY-009] crash reentry reuses already created new slot exactly once`；`requirements/finality/tests/finality-cohort-law.test.mjs::WHAT[FINALITY-009] historical enlist order confluent for roster`；`requirements/finality/tests/finality-cohort-law.test.mjs::WHAT[FINALITY-009] replay preserves an open finality roster source` |
| FINALITY-010 | `requirements/finality/tests/finality-cohort-law.test.mjs::WHAT[FINALITY-010] graduated reviewer excluded from roster` |
| FINALITY-011 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-011] a revise closes finality without confirming the life` |
| FINALITY-012 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-012] finality rejection renders work record as guidance comments` |
| FINALITY-013 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-013] finality three experiences` |
| FINALITY-014 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-014] rejection keeps the same Life and a new suicide begins fresh Finality` |
| FINALITY-015 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-015] a blessing keeps the enlisted process-review standing: no dispose` |
| FINALITY-016 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`；`requirements/finality/tests/blessing-admission.test.mjs::WHAT[FINALITY-016] blessing_admission_requires_complete_cohort_witness` |
| FINALITY-017 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-017] the second suicide is the rest: LifeCompleted archives the Life`；`requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-017] isLifeArchived true only after life completed` |
| FINALITY-018 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-018] an open request owns the Life: Manager labor is deferred` |
| FINALITY-019 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-019] idle encouragement golden bytes` |
| FINALITY-020 | `requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-020] rejection rendering exposes no mechanism vocabulary`；`requirements/finality/tests/lifecycle.test.mjs::WHAT[FINALITY-020] manager surface has no forbidden words` |
| FINALITY-021 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-021] disposition never derives from narrative text` |
| FINALITY-022 | `requirements/finality/tests/life-admission.test.mjs::WHAT[FINALITY-022] unknown authority kind and tier fail closed`；`requirements/finality/tests/life-admission.test.mjs::WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history`；`requirements/finality/tests/life-admission.test.mjs::WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id` |
| FINALITY-023 | `requirements/finality/tests/rewrite-consistency.test.mjs::WHAT[FINALITY-023] opening rewrite is byte identical across requests`；`requirements/finality/tests/rewrite-consistency.test.mjs::WHAT[FINALITY-023] opening rewrite survives a persisted rewritten message` |
| FINALITY-024 | `requirements/finality/tests/rewrite-consistency.test.mjs::WHAT[FINALITY-024] work-time messages are never rewritten` |
| FINALITY-025 | `requirements/finality/tests/manager-finality-disposition.test.mjs::WHAT[FINALITY-025] a completed Life replays as AlreadyCompleted, never restarts` |
| FINALITY-026 | `requirements/finality/tests/finality-fatal-contract.test.mjs::WHAT[FINALITY-026] modern Finality has no Undecided business outcome or failure sink`；`requirements/finality/tests/finality-fatal-contract.test.mjs::WHAT[FINALITY-026] Finality infrastructure exceptions terminate through the diagnostic fuse` |
| FINALITY-027 | `requirements/finality/tests/finality-background-obligation.test.mjs::WHAT[FINALITY-027] malformed handle role ownership and completion fail closed`；`requirements/finality/tests/finality-background-obligation.test.mjs::WHAT[FINALITY-027] Manager without journal or handles is never outstanding`；`requirements/finality/tests/finality-background-obligation.test.mjs::WHAT[FINALITY-027] Manager with a listable child handle has a join obligation`；`requirements/finality/tests/finality-background-obligation.test.mjs::WHAT[FINALITY-027] hidden Reviewer handles do not become a Manager join obligation`；`requirements/finality/tests/finality-background-obligation.test.mjs::WHAT[FINALITY-027] completed-but-unjoined handles remain outstanding until retired` |
| FINALITY-028 | `requirements/finality/tests/manager-job-no-resurrection.test.mjs::WHAT[FINALITY-028] a terminal ManagerJob is not active and does not resume`；`requirements/finality/tests/manager-job-no-resurrection.test.mjs::WHAT[FINALITY-028] later facts cannot reopen a terminal ManagerJob`；`requirements/finality/tests/manager-job-no-resurrection.test.mjs::WHAT[FINALITY-028] replaying ManagerJobCreated cannot re-enlist a terminal job`；`requirements/finality/tests/manager-job-no-resurrection.test.mjs::WHAT[FINALITY-028] an active owned job continues on the same session and worktree` |
