# HOW —— effect-accounting（实现模型与约束）

> 本文件**非 normative**。行为合同在 `WHAT.md`；本文件回答「代码在哪里、怎么工作」，
> 并收纳历史与弃权裁决。

## typed effect facts（`Change/Facts.fs`）

```text
OrchestratorFactCases（AgentFact.Orchestrator）：
  ManagerJobCreated / CandidateReady / ConflictDetected / RebasedCandidateReady
  PublishClaimed { ManagerJobId; TargetRef; ExpectedHead }   // ORCH-005：CAS 短窗口内写入
  Published { CandidateCommit; ResultingTargetHead }
  JobFailed / JobAbandoned
  WorktreeCreateRequested { ManagerJobId; WorktreeIdentity; WorktreePath }   // Requested
  WorktreeCreated   { ManagerJobId; WorktreeIdentity; WorktreePath }         // Accepted

ExecutionFactCases：HandleLinked / HandleCompleted / HandleRetired / HandleAbandoned
  HandleFalseCompletionRejected / HandleFalseTerminalReported / ParentJoinCorrectionRequested
PromptFactCases：PluginPromptClaimed（send 前持久化）/ PluginPromptSubmitted /
  PluginPromptPhysicalAccepted / PluginPromptAbandoned / AuthorityRootAccepted
MagicTodoFact（Mission/Obligation/Todo/Facts.fs）：TodoWritePrepared / TodoWriteAccepted
  （携带 PreparedFactRef + InputDigest/OutputDigest + PhysicalSuccessEvidence）
```

## effect 状态投影（`Change/Projection.fs`）

```text
WorktreeEffectStatus = Requested of {| ManagerJobId; WorktreePath |}
                     | Created   of {| ManagerJobId; WorktreePath |}      // PERSIST-009
WorktreeEffects：Map<WorktreeIdentity, WorktreeEffectStatus>
  requestWorktree：无状态 → Requested；已有 Created → 保持 Created（不回归）
  acceptWorktree：Requested → Created；重复 Created 幂等
ManagerJobProjection 独立 durable facts（不 fold 成唯一 latest-case enum）：
  CandidateReady: {| CandidateCommit; PreRebaseReviewBarrierId |} option
  ConflictDetected: {| CandidateCommit; TargetHeadSnapshot; ConflictFiles; DiagnosticsDigest |} option
  RebasedCandidateReady: {| RebasedCommit; TargetHeadSnapshot; PostRebaseReviewBarrierId |} option
  PublishClaimed: {| RebasedCommit; ExpectedHead |} option
  Terminal: TerminalOutcome option（Published | Failed | Abandoned）
semantic entry 从 facts + 当前外部现实（target head）重证 outstanding obligation（SW-003 vs SW-009 消歧）；
不恢复 latest-stage enum，不新增 ResumeAtXxx 补偿日志。
classifyPublishClaim（ORCH-007，三分支固定顺序）——见 WHAT 009：
  currentHead = None                       → HeadUnreadable（ORCH-008 fail closed）
  head = claim.RebasedCommit               → AlreadyFastForwarded（ff 已发生，只缺事实）
  head = claim.ExpectedHead                → PublishReady（目标未变，重试 ff）
  _                                        → ClaimExpired（witness 作废）
```

## fold 拒绝回归（`Change/Fold.fs`）

```text
PublishClaimed 分支：job 当前无 RebasedCandidateReady → reject
  "publish claimed for a job with no rebased candidate (ORCH-004)"（claim 必须有 durable witness）
Published → Terminal.Published；terminal job 不再接受任何 progress（重放幂等）
WorktreeCreateRequested → requestWorktree；WorktreeCreated → acceptWorktree
```

## outcome-unknown 机械面（`Persistence/Journal/EventStoreJournalWriter.fs` / `Persistence/Journal/AgentJournal.fs`）

```text
EventStoreJournalWriter：physical append 已开始后失败 → 记录首个 poison 原因并返回
  CommitUnknown(WriteFailed ...) —— 结局未知，不盲重试；之后 poisoned / closing / disposed 的新调用
  返回 NotAttempted(WriterUnavailable)，因为该 EventId 从未进入 physical append boundary
AgentJournal.AppendEnvelope：commit → fold；semantic cut → FactRejected（坏行 + cut durable，当前进程 fatal）；
  physical 写失败 → JournalAppendFailure.WriteUnknown；writer 生命周期拒绝 → WriterUnavailable
JournalAppendFailure.describe：WriteUnknown 保留 physical uncertainty；WriterUnavailable 明确 known-not-attempted 并携带首个 poison 根因
Host Reconciler：由 composition 注入 `AgentJournal.isPoisoned` 作为 durable-unavailable admission predicate；一旦首个 physical failure poison writer，关闭新的 reconcile admission 并清掉尚未开始的 queued wake，当前 pass 退出后不得在 poisoned writer 上继续消费 durable effect。未知提交仍留给 crash-reconciliation 的 canonical witness 判定，禁止 same-process blind retry。
判定手段（哪些 EventId 已 committed）由 durable-events 的 canonical root witness 提供。
```

## 先证后重试的实现（`Interaction/Dispatch/Recovery.fs` / `Mission/Obligation/Todo/MagicTodoMembrane.fs`）

```text
PromptRecovery.reconcileClaim（L71）：先做 snapshot 物理核对，再查 budget；
  结果 = Proven physical | Unreadable | GaveUp | StillPending；
  reconcile（L127）：无 journal 则无 durable claim 可 reconcile；PROMPT-011 禁止 resend
MagicTodoMembrane.prepare（L300）：先 append TodoWritePrepared，再 provider 调用
MagicTodoMembrane.accept（L321）：仅在物理成功 + digest 匹配后 append TodoWriteAccepted
  （InputDigestMismatch / OutputDigestMismatch → 拒绝）
TodoProcessReviewProgram（L106）：CurrentObligations 在 TodoWriteAccepted 移动，
  verdict 不回滚
```

## 实例与边界

- **Worktree 编排**（`Change/Runtime.fs` `forkManagerCore`）：
  `WorktreeCreateRequested`（L99）先于 `WorktreeResource.Create`（L109），
  `WorktreeCreated`（L125）后，`ManagerJobCreated` 最后（L137–140 注释）。
- **Publish 编排**（`Change/Program.fs` `claimAndFf` L179–209）：
  短 CAS 窗口持有 gate；`current <> expectedHead → TargetMoved`（不写 claim）；
  `current = expectedHead → append PublishClaimed → FfMerge → ok → append Published`；
  `publishUnderGate` 保证 gate release；`publishEventually` 在 TargetMoved 上循环
  （fresh rebase + dual PERFECT + claim per round）。
- **session.create 例外**：Host 在 `session.create` 返回前不分配 child SessionId → 不引入
  `SessionCreateRequested`；accepted 证据 = 链接事实 `HandleLinked` /
  `CompanionBloggerLinked`。
- **Prompt**：`PluginPromptClaimed` 在 send 前持久化；PhysicalAccepted 是确认事实；
  PromptKey / at-most-one / no-resend policy 归 `dispatch-protocol`。

## 历史与弃权

1. **0.5.1 通用 `DurableEffectRequested/Accepted` —— 弃权**：`FactCodec.pre050Markers`
   把这两个 marker 列入拒绝集（`OrchestratorPublishClaimed` 同列），decode 给出迁移
   信息而非静默双读；被 typed facts（WorktreeCreateRequested 等）取代。
2. **`CommitUnknown → 永久无法确定` —— 弃权**：storage.md §9 由 canonical root witness
   取代（`durable-events` 006）；本包承接「未知后的 reconcile 政策」。
3. **假 abort 洗成成功 —— 弃权**：EXEC-021/022 拒绝 `LegacyFalseAbort` 作为
   RunCompletion；`HandleFalseCompletionRejected` 做确定性 replacement +
   `ParentJoinCorrectionRequested` 一次性补偿（p0-recovery-join 的
   `false-completion-rejected-fact`/`parent-join-correction-fact` 正向规则）。
4. **p0-recovery-join gate 归属 —— SPLIT@cutover**：`scripts/checks/p0-recovery-join.mjs`
   的 A 组规则（aborted≠terminal：`agent-aborted-type`、`agent-completion-aborted-factory`、
   `child-run-make-aborted`、`aborted-run-factory`、`join-renderer-agent-status-aborted`、
   `codec-encode-finality-aborted`、`try-from-durable-completed`、`publish-completion-agent`、
   `awaiting-evidence-case`、`lifecycle-*`、`fork-recovery-*`、`host-fork-restart-*`、
   `fork-runtime-parent-cancelled-aborted`、`record-completion-single-owner` 负例，以及
   正向 `agent-outcome-completed/failed/abandoned-case`、`agent-join-item-three-cases`、
   `pty-aborted-retained`、`completion-blob-schema-v2`、`legacy-false-abort-decode`、
   `joinable-from-decoded`、`false-completion-rejected-fact`、`parent-join-correction-fact`）
   归本包；B 组 recovery 规则归 `crash-reconciliation`。cutover 时按规则 id 拆分测试。
5. **ABORTED 非 agent 终态 —— HOW 落点**：`Session/ForkTypes.fs` 的 `AgentCompletion`
   无 aborted case；join 渲染/HandleCompletionCodec 不写 aborted finality；
   `ChildRecovery` 的 `PtyAborted` 保留为物理观察，不是业务终态。

## DEPENDS ON `durable-events`

效果事实的 append/CAS/commit witness 由 `durable-events` 提供；本包定义
「effect 的 Requested/Accepted 语义」这一层。

## 边界（DOES NOT OWN）

- EventStore 编码/提交机制 → `durable-events`。
- Prompt 特有 PromptKey / no-resend policy → `dispatch-protocol`。
- Git publish/worktree、repository transaction 的具体 reconcile 算法 → `change-integration`
  （本包拥有其中的 PublishClaimed 三分支与 Worktree Requested/Created 律）。
- effect 的业务授权（谁有权发起）→ `office-capability` / `interaction-authority`。
- 进程中断后重入普通程序 → `crash-reconciliation`。

## 验证与测试落点

### 运行方式

```bash
node --test requirements/effect-accounting/tests/effect-facts.test.mjs   # 本包 NEW
node --test requirements/effect-accounting/tests/reconcile-before-retry.test.mjs   # 本包 NEW
node --test requirements/effect-accounting/tests/write-unknown-explicit.test.mjs   # 本包 NEW
node --test requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs   # 本包 NEW
node requirements/verification-system/tests/run.mjs                                                  # 全量
```

本包 4 个 NEW 测试文件（effect-facts / reconcile-before-retry / write-unknown-explicit /
todo-accepted-precise-ref）单独跑绿；其余落点 REUSE 原位/跨包。

### 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EFFECT-ACCOUNTING-001 | `requirements/effect-accounting/tests/effect-facts.test.mjs::worktree_requested_created_are_distinct_typed_states_not_one_bool` | NEW | `node --test requirements/effect-accounting/tests/effect-facts.test.mjs` |
| EFFECT-ACCOUNTING-002 | `requirements/effect-accounting/tests/join-missing-final-report.test.mjs::EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed`; `requirements/effect-accounting/tests/join-missing-final-report.test.mjs::EXEC_join_empty_Completed_keeps_run_pending_not_failed`; `requirements/effect-accounting/tests/join-missing-final-report.test.mjs::EXEC_join_interaction_repair_exhausted_settles_the_run`; `requirements/effect-accounting/tests/join-missing-final-report.test.mjs::EXEC_join_real_Failed_still_claims_run` | REUSE | `node --test requirements/effect-accounting/tests/join-missing-final-report.test.mjs` |
| EFFECT-ACCOUNTING-003 | `requirements/effect-accounting/tests/runtime-persist-order.test.mjs::PERSIST_009_fork_appends_worktree_request_created_then_manager_job` | NEW | `node --test requirements/effect-accounting/tests/runtime-persist-order.test.mjs` |
| EFFECT-ACCOUNTING-004 | `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::C5_same_request_materialize_is_idempotent`; `requirements/effect-accounting/tests/manager-unhappy-exactly-once.test.mjs::THEOREM_owner_failure_alone_still_exactly_once_under_duplicate_observation` | NEW | `node --test requirements/effect-accounting/tests/blogger-request-materialized.test.mjs requirements/effect-accounting/tests/manager-unhappy-exactly-once.test.mjs` |
| EFFECT-ACCOUNTING-005 | `requirements/effect-accounting/tests/reconcile-before-retry.test.mjs::requested_only_without_physical_evidence_stays_pending_not_blind_retry`; `requirements/effect-accounting/tests/reconcile-before-retry.test.mjs::outcome_unknown_without_physical_evidence_never_becomes_terminal`; `requirements/effect-accounting/tests/reconcile-before-retry.test.mjs::terminal_issued_only_after_proven_physical_evidence` | NEW | `node --test requirements/effect-accounting/tests/reconcile-before-retry.test.mjs` |
| EFFECT-ACCOUNTING-006 | `requirements/effect-accounting/tests/write-unknown-explicit.test.mjs::write_after_dispose_returns_explicit_unknown_not_pretended_commit` | NEW | `node --test requirements/effect-accounting/tests/write-unknown-explicit.test.mjs` |
| EFFECT-ACCOUNTING-007 | `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal`; `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::P0_RECOVERY_JOIN_001_tryFromDurableCompleted_rejects_cancelled`; `requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs::P0_RECOVERY_JOIN_GATE_positive_clean_break_shapes_present`; `requirements/effect-accounting/tests/join-clean-break.test.mjs::P0_CLEAN_BREAK_legacy_aborted_blob_decodes_without_run_completion` | REUSE | `node --test requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs requirements/effect-accounting/tests/join-clean-break.test.mjs` |
| EFFECT-ACCOUNTING-008 | `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::C5_entry_commit_records_receipt_and_clears_open_request` | REUSE | `node --test requirements/effect-accounting/tests/blogger-request-materialized.test.mjs` |
| EFFECT-ACCOUNTING-009 | `requirements/effect-accounting/tests/effect-facts.test.mjs::publish_claimed_recovery_three_branch_order_is_fixed` | NEW | `node --test requirements/effect-accounting/tests/effect-facts.test.mjs` |
| EFFECT-ACCOUNTING-010 | `requirements/effect-accounting/tests/pre050-effect-marker.test.mjs::PERSIST_005_pre050_marker_refuses_with_migration_message`; `requirements/effect-accounting/tests/effect-facts.test.mjs::typed_effect_facts_replace_the_generic_durable_effect_union` | REUSE + NEW | `node --test requirements/effect-accounting/tests/pre050-effect-marker.test.mjs requirements/effect-accounting/tests/effect-facts.test.mjs` |
| EFFECT-ACCOUNTING-011 | `requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs::accepted_without_any_prepared_is_rejected`; `requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs::accepted_naming_another_prepared_envelope_is_identity_corruption`; `requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs::accepted_naming_exact_prepared_switches_current_immediately` | NEW | `node --test requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs` |
| EFFECT-ACCOUNTING-012 | `requirements/effect-accounting/tests/effect-facts.test.mjs::publish_claim_without_durable_rebase_witness_is_rejected` | NEW | `node --test requirements/effect-accounting/tests/effect-facts.test.mjs` |

### 统计

- 命题 12 条；落点行 12；NEW 4 文件（`effect-facts.test.mjs`、`reconcile-before-retry.test.mjs`、
  `write-unknown-explicit.test.mjs`、`todo-accepted-precise-ref.test.mjs`）+ REUSE 11 个现有
  文件（`requirements/effect-accounting/tests/join-missing-final-report.test.mjs`、
  `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`、
  `requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs`、
  `requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs`、
  `requirements/change-integration/tests/runtime.test.mjs`、
  `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`、
  `requirements/durable-events/tests/fact-codec.test.mjs`、`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs`、
  `requirements/change-integration/tests/job.test.mjs`、
  `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs`、
  `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs`）。
- GAP：0。

### SPLIT@cutover 清单

1. `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`：单-owner（本包）但**暂不物理
   移动**——`requirements/structured-workflow/HOW.md`（STRUCTURED-WORKFLOW-015）已按
   当前路径引用其落点 token；`requirements/crash-reconciliation/HOW.md` 亦在
   SPLIT@cutover 清单中点名「effect-accounting owner」。cutover 时移入本包 `tests/` 并
   同步更新 structured-workflow 的 PROOF 落点路径。
2. `requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs` + `scripts/checks/p0-recovery-join.mjs`：
   共享 checker；按规则 id 拆分——A 组（aborted≠terminal）归本包，B 组（recovery）归
   `crash-reconciliation`。cutover 时拆成两个 oracle，各自留在 owner 包。
3. `requirements/crash-reconciliation/tests/join-aborted-race.test.mjs` / `join-recovery-crash-matrix.test.mjs`：
   已由 `crash-reconciliation` 迁移；其 aborted≠terminal 断言与 EA-007 交叉，按
   「恢复矩阵归 crash、false-finality 律归本包」互不复制命题。
4. `requirements/change-integration/tests/job.test.mjs` PERSIST-009 小节、
   `requirements/change-integration/tests/runtime.test.mjs` 顺序断言：`change-integration` 已在其
   SPLIT@cutover 清单中把 PERSIST-009 事实顺序断言划归本包；cutover 时物理拆分。
5. `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`：lag-1 证据门断言
   （TODO-006/005 的 effect 半边）归本包；membrane 的 Host/snapshot 面归各自 owner。

### 本包拥有的 semantic anchor id

空。`scripts/checks/semantic-anchors.mjs` 无 effect-accounting 语义 ID。
