# Glory：目标实现与算法（how 层）

条款正文见 `docs/what/glory.md`；本文件描述实现切片的算法与触发点。Magic Todo checkpoint / membrane / process-review 算法见 `docs/how/todo.md`（TODO-*）；本文件只保留 GLORY Life、Finality 与交界实现。

## 事实与投影

`ManagerLifecycleFact` 作为 `Fact.ManagerLifecycle` case 进入 journal（GLORY-010）。`Fold.foldAgentFact` 为 `AgentFact` 穷尽 match，新增 case 由编译器强制注册；`SessionAgentProjection` 增加 `ManagerLife: ManagerLifeProjection option`，由 `Journal/ManagerLifecycleProjection.fs` 折叠（GLORY-011）；`LifeProjection` 携带 `ActiveFinality`、`EnlistedReviewers`（跨 request 累积 roster 源，`FinalityReviewCohort.rosterOf` 消费）、`LastBlessing`，以及从 Opening cursor **纯推导**的 `WorkRecordStart`（TODO-001）。`WorkActivated` / 历史 `ProtectedPrefixEnd` 仅 inert legacy decode，不得驱动生产资格。`Identity.fs` 新增 `ManagerLifeId`、`FinalityRequestId` 包装类型。

## Slice A：Lifecycle 与 Opening

1. `ManagerNarrativeTransform` 挂在 SpikePlugin transform 链的 `XTraceCapture.captureProjection` 之后、`ReviewSeal` 之前（GLORY-013 顺序）。它读取最后一条 user 消息：
   - 若该消息是合法 HumanRoot（无 PromptKey、非 compaction、非 retry，GLORY-012）且 session 是 Manager（从 journal Authority Root 的 CanonicalRole 判定）；
   - 且当前无未完成 Life（`ManagerLife` 投影为 None）或上一 Life 已 `LifeCompleted` → 打开新 Life：
     - 写 Opening blob（用户原始文本）→ append `LifeOpened`（openingCursor = 该消息在 XTrace 中的首个 part cursor）；
     - `WorkRecordStart` = Opening semantic exclusive end（TODO-001）；
     - 生产路径：**不**注入 planning-only Activation 资格叙事；Manager-only 持续规划/执行指导由 MagicTodoManagerGuideline 投影（TODO-013）；legacy `PlanningTail` / ReawakeningPrefix 字节仅用于 decode 或明确兼容窗口（GLORY-014/064）；
     - 幂等：改写 identity 记录在内存 `Dictionary<(sessionId, lifeId, messageId), source>`，重复 transform 不重复注入（GLORY-015）。
2. durable Opening 永远是原始 HumanRoot/`[X]`：captureProjection 先于任何 provider-facing 改写运行，XTrace 在 rewrite 之前落盘（GLORY-013）。
3. `LifeOpened` 后 Manager **立即**获得正常工作工具（含 `todowrite` 协议面）；无 Activation 前置（TODO-001；GLORY-016）。

## Slice B：Opening floor（无生产 Activation）

1. 生产路径**删除** `PromptAuthority.ContinuationKind.ManagerWorkActivation` 发送与 `ManagerWorkflow` planning-terminal → Activation 分支（GLORY-018/070）。legacy claim/accepted 事实可 decode，不产生新逻辑效果（GLORY-019/020/021）。
2. Blogger floor（GLORY-023/024；TODO-001）：`BloggerCoordinator.nextMainContext` 中

```text
effectiveStartSeq = max(blog.Coverage.IngestedThroughSequence, life.WorkRecordStart.Sequence)
```

禁止使用 `WorkActivated.ProtectedPrefixEnd` 作为生产 floor。`CompanionTransform.hasMaterial` 预过滤同步。

3. lag-1 Manager prefix rebase 由 Magic Todo Accepted 链推导 desired cutoff，并在下一 provider attempt seal 前提交 `PrefixRebaseCommitted(EvidenceKind=TodoCheckpoint)`（TODO-009）；GLORY 不实现第二套 PrefixEpoch。
4. `XTraceCapture.lifecycleWorkRecord` Manager 变体：按纯文本段标题 `Opening task / Work log / Uncompressed tail / Final output` 渲染（GLORY-025；`# ` 仅由 `SyntheticToml.comment` 在 wire 注入）。process/Finality 调用一律 `includeOpening=false`、request-range bounded（TODO-008）。

## Slice C：工具与角色边界

1. `Roles.fs`：新增 `ToolPermission.Finality`；Manager permissions 加 `Finality`（GLORY-036）。`todowrite` 权限与 membrane 见 Host/todo how（TODO-001/004）。
2. `ToolRegistry.rolePredicate` 加 `"suicide" -> fun r -> r = Role.Manager`；`baseSpecs` 加 `FinalityTool.spec factory runtime`。
3. `FinalityTool.execute`（GLORY-034/035/037-041）：
   - 前置条件按序检查，含 TODO-010：first unblessed 且本 Life 零 `TodoWriteAccepted` → fail closed；否则 await latest ConsumableReview ≡ TodoReviewConcluded（TODO-006）→ settle（TODO-005）；过程 REVISE → 回灌 ProcessReviewLWR、sink reconcile（TODO-007）、**不**建 FinalityRequest；过程 PERFECT 才继续 Finality 前置；
   - 失败返回 GLORY-038/039 或对应拒绝文本（禁止泄漏内部细节）；
   - 合法受理（未 Blessed）：`gitTreePort.GetTreeHash()` → journal `WriteBlob last_words` → append `FinalityRequested` → 启动 `FinalityController` cohort CE（`rosterOf` 选员，含 TODO-010 Dedicated 首次 ordinary enlist，GLORY-040）；
   - tool result：`FinalityOutcome` 决定返回文本——`Rejected` → `FinalityPrompt.rejected`；`Blessed` → `FinalityPrompt.blessed` minor-work continuation；`Undecided` → `ManagerLifecyclePrompt.FinalityUndecidable`（GLORY-041/052/053/057）；request 已有成员在途 → `Your ending is already in motion.`；Blessed Life 再 suicide → 先 TODO-010 抽干，再 `completeBlessedLife`（`rest in peace`，GLORY-062）。
4. `ForkTool.executeManager`：解析 `agent` 为 `Role.Reviewer` 时返回统一隐藏文案 `Unknown or unavailable managed agent.`（GLORY-031/032）；`managerOpensReviewBarrier` 置 false（GLORY-033）。
5. 自动 Reviewer 隐藏：HostReviewProgram / dedicated process reviewer 使用独立 `HostForkRuntime`，`ownership=HostOwnedHidden`（GLORY-002；TODO-008）；completion 不进 Manager `join`/`list`。

## Slice D：HostReviewProgram

从 `OrchestratorHostReview.reverify` 提炼 `module HostReviewProgram`（GLORY-042/043）：

```fsharp
let reverify
    (journal: AgentJournal option)
    (forkReviewer: unit -> Task<Result<SessionId, string>>)
    (awaitReviewer: unit -> Task<Result<unit, string>>)
    (nudgeReviewer: unit -> Task<Result<unit, string>>)
    (managerSessionId: SessionId)
    (barrierId: ReviewBarrierId)
    (tree: GitTreeHash)
    : Task<Result<HostReviewOutcome, HostReviewFailure>>
```

- 流程：fork → `HostReviewGuard.openBarrier` → await → `OrchestratorReviewRead.read` → Confirmed / RevisionRequired / PendingConfirmation → nudge → 再读 → 非 Confirmed fail closed。
- workRecord 由 request-range bounded canonical LWR（`includeOpening=false`）填充；为空时 `WorkRecordUnavailable`（GLORY-051；TODO-008）。
- process-review 单次 PERFECT/REVISE（无 dual-PERFECT、ConsumableReview 分型）见 how/todo（TODO-006/008）；不得经本 dual-PERFECT 路径冒充 terminal 证明。
- `OrchestratorHost` 与 `OrchestratorHostReview` 改为调用该通用程序。

## Slice E：失败反馈

1. REVISE 首先关闭 cohort：撤销对应 Reviewer continuation capability、cancel sibling 的下一次 effect；不发 confirmation/challenge，不 Dispose 未 graduate session（GLORY-044/055）。该步骤不写 `FinalityRejected`。
2. record-ready 等待（首个 rejecting Reviewer）：从 durable REVISE 重建 terminal frontier；原子取得 `(snapshot, revision)`，在该 snapshot 上以全量 origin coverage 物化 canonical LWR，并确认含 `Work log`（`materializeRecord`）。就绪判定是「能否物化有效工作日志」，不是 `coverage >= frontier.Sequence`（GLORY-072/073）。物化成功 → `RecordReady`；物化失败但 `coverageCanAdvance` → `AwaitJournal`，经 `AgentJournal.awaitChangeFrom revision` 唤醒；否则 `RecordUnavailable` → `concludeUndecided`。就绪后才 `WriteBlob`，再 append `FinalityRejected`。
3. Sibling 预检与双轨交付（multi durable REVISE，GLORY-044）：密封 `FinalityRejected` 之前对 durable sibling 并集预检；硬 `RecordUnavailable` → `concludeUndecided`。全部 `RecordReady` 后：先预置 primary record-ready/`WriteBlob`，再一次性 append 全部 `FinalitySiblingSteered`，最后密封 `FinalityRejected` 并发送 steer。
4. `BloggerRequestAbandoned` 只令本次记录尝试失效；reconcile 以同一 durable frontier 重新建立机会（GLORY-056/057/073）。
5. dedupe / 崩溃恢复：`FinalityRejected` 与 `FinalitySteer` 使用不同 claim scope（GLORY-053）。`resumeDurableRevise` 在 Open 路径复用同一预检/入账顺序。

## Slice F：Finality 收束

`concurrentAllOrShortCircuit` 汇聚全部 member 的 `driveMember` outcome（GLORY-059/060）：

1. 任一 REVISE → 立即关闭 cohort；随后按 Slice E.3 双轨收束。Primary 或任一 sibling 硬物化失败 → `concludeUndecided`（GLORY-044/055/072/073）。
2. 全员双 PERFECT → 重读 tree → 与 `FinalityRequested.GitTreeHash` 比较；不等 → fail closed（GLORY-059）；相等 → `concludeBlessing`：按 stable ordinal 物化 canonical LWR bundle → append `FinalityBlessed` → 发 minor-work continuation。不得 `LifeCompleted`、NotifyTerminal 或清除 Manager（GLORY-060）。不得因 Blessing 释放 Dedicated process-review duty（TODO-010）。
3. 无法证明 → `concludeUndecided`，不伪造 work record（GLORY-057）。

**Roster（GLORY-003/045；TODO-010）**：`rosterOf` = 未 graduate ordinary historical + 恰好一个 fresh ordinary；若 Dedicated 尚未 Finality graduate，首次 terminal Finality 时 ordinary enlist（physical/session 去重）。process PERFECT 不计入 terminal dual-PERFECT。

第二次 suicide（Life 已有 latest blessing，GLORY-062）：先做 GLORY-037 资源安全与 TODO-010 过程评审尾抽干（REVISE → 回灌、继续 Life）；抽干后且无阻塞过程 REVISE 时：不读 tree、不创建 Reviewer/barrier、不检查 witness。写本次 last_words → append `LifeCompleted` → 注册 terminal → `NotifyTerminal`；tool result 固定包含 `rest in peace`。

幂等：`LifeCompleted` 已存在则不再重复。Orchestrator 衔接：GLORY-068。

## Slice G：Reawakening

1. `ManagerLifeProjection` 保存 `Lives: ManagerLifeProjection list`（每 Life 的 opening cursor / WorkRecordStart / completed cursor）。
2. `LifeCompleted` 后首个新 HumanRoot：打开新 Life（新 `ManagerLifeId`、新 Opening、新 `WorkRecordStart`、无 FinalityRequest、MagicTodo canonical 空除非 TODO-011 legacy seed），改写用 `Reawakening` 前缀（若适用）（GLORY-063/064/065）。
3. 当前 Life 工作中用户消息不改写（GLORY-007/026）；XTrace 不清空（GLORY-066）。

## 迁移（GLORY-069/070/071）

启动/transform 检测 Manager session 已有 XTrace Opening 但无 Life → `ensureMigrationLife` 写 migration `LifeOpened`（用现有 Opening 数据），推导 `WorkRecordStart`；历史若仅有 `WorkActivated` 则 inert decode，**不**再写生产 Activation（GLORY-021；TODO-001）。legacy open Life 的 Magic Todo seed 见 TODO-011。新 prompt / MagicTodoManagerGuideline 只对新 session/root/Life 生效（GLORY-071；TODO-013）。

## Crash Recovery Matrix

恢复只从 durable facts 推导（禁止 NextStep/ResumeAt/Stage 字段；TODO-012）。已实现行：

| journal 状态 | 恢复动作 | 实现 |
|------|------|------|
| LifeOpened 缺 → provider request 前 | 无害；下个 transform 重开 | ✅ transform 幂等 |
| LifeOpened 有 → 生产无 Activation | 立即工作工具 + Magic Todo 协议继续；不发送 ManagerWorkActivation | ✅ transform + ManagerWorkflow（GLORY-018） |
| FinalityRequested 无 enlisted member | FinalityTool「in motion」分支重启同一 request 的 FinalityController；`rosterOf` 崩溃重入不重复造新 Reviewer | ✅ |
| REVISE 已存在但无 FinalityRejected | 从 durable evidence 重建同一 terminal frontier；cohort 继续关闭，以全量 origin coverage 物化含 `Work log` 的 canonical LWR；物化失败且 `coverageCanAdvance` 则等待 journal change；`BloggerRequestAbandoned` 重建记录机会，证据不足则 undecided | ✅ |
| FinalityRejected 已落盘且 `FinalitySiblingSteered` 已提交，但 Manager steer prompt 缺失 | 已决议路径**仅重放已提交** SiblingSteers：读 blob → `FinalityPrompt.steer` → `sendContinuation(FinalitySteer)`；仍不可得则 Manager-visible `FinalityPrompt.steerUnavailable`。不 append 新 fact、不改 `Resolution` | ✅ resumeDurableRevise 已决议分支 |
| record-ready waiter 崩溃/Dispose | 不写 abandonment 或 lifecycle 终态；replay 后从 durable REVISE/frontier 重新订阅 journal change | ✅ resumeDurableRevise（GLORY_075） |
| confirmed witness 存在但无 FinalityBlessed | concludeBlessing 幂等（blessing 已存在/terminal 已记录则跳过） | ✅ |
| LifeCompleted 存在但 terminal 未发布 | completeBlessedLife 幂等重放 | ✅ |
| XTrace terminal 单槽冲突（第二 Life） | 首个 Life 后跳过 TerminalOutputCaptured（terminal 只记录于 LifeCompleted） | ✅ |
| suicide 前 latest Rk 未 ConsumableReview | TODO-010 drain：await TodoReviewConcluded；REVISE 不进 Finality | ✅ FinalityTool 前置 |
| Dedicated 已 Finality graduate | process-review session 仍保留至 LifeCompleted（TODO-008/010） | ✅ |
