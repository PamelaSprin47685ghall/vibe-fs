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
MagicTodoFact（Domain/MagicTodoFacts.fs）：TodoWritePrepared / TodoWriteAccepted
  （携带 PreparedFactRef + InputDigest/OutputDigest + PhysicalSuccessEvidence）
```

## effect 状态投影（`Change/Orchestration/OrchestratorProjection.fs`）

```text
WorktreeEffectStatus = Requested of {| ManagerJobId; WorktreePath |}
                     | Created   of {| ManagerJobId; WorktreePath |}      // PERSIST-009
WorktreeEffects：Map<WorktreeIdentity, WorktreeEffectStatus>
  requestWorktree：无状态 → Requested；已有 Created → 保持 Created（不回归）
  acceptWorktree：Requested → Created；重复 Created 幂等
JobProgress：ManagerStarted | CandidateReady | ConflictPending | RebasedCandidateReady
  | PublishClaimed {| RebasedCommit; ExpectedHead |} | Published | Failed | Abandoned
recoveryAction（ORCH-007，三分支固定顺序）——见 WHAT 009：
  | PublishClaimed claim ->
      currentHead = None                       → FailClosed "GetTargetHead failed; ORCH-008"
      head = claim.RebasedCommit               → BackfillPublished（ff 已发生，只缺事实）
      head = claim.ExpectedHead                → AttemptPublish（目标未变，重试 ff）
      _                                        → RebaseAndReviewAgain（witness 作废）
```

## fold 拒绝回归（`Change/Orchestration/OrchestratorFactFold.fs`）

```text
PublishClaimed 分支：job 当前无 RebasedCandidateReady → reject
  "publish claimed for a job with no rebased candidate (ORCH-004)"（claim 必须有 durable witness）
Published → JobProgress.Published；terminal job 不再接受任何 progress（重放幂等）
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

## 先证后重试的实现（`Application/Reconciliation/`）

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

- **Worktree 编排**（`Application/Orchestration/Runtime.fs` `forkManagerCore`）：
  `WorktreeCreateRequested`（L99）先于 `WorktreeResource.Create`（L109），
  `WorktreeCreated`（L125）后，`ManagerJobCreated` 最后（L137–140 注释）。
- **Publish 编排**（`Application/Orchestration/Program.fs` `claimAndFf` L179–209）：
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
