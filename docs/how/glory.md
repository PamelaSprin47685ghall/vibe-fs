# Glory：目标实现与算法（how 层）

条款正文见 `docs/what/glory.md`；本文件描述实现切片的算法与触发点。

## 事实与投影

`ManagerLifecycleFact` 作为 `Fact.ManagerLifecycle` case 进入 journal（GLORY-010）。`Fold.foldAgentFact` 为 `AgentFact` 穷尽 match，新增 case 由编译器强制注册；`SessionAgentProjection` 增加 `ManagerLife: ManagerLifeProjection option`，由 `Journal/ManagerLifecycleProjection.fs` 折叠（GLORY-011）；`LifeProjection` 携带 `ActiveFinality`、`EnlistedReviewers`（跨 request 累积 roster 源，`FinalityReviewCohort.rosterOf` 消费）与 `LastBlessing`。`Identity.fs` 新增 `ManagerLifeId`、`FinalityRequestId` 包装类型。

## Slice A：Lifecycle 与 Birth

1. `ManagerNarrativeTransform` 挂在 SpikePlugin transform 链的 `XTraceCapture.captureProjection` 之后、`ReviewSeal` 之前（GLORY-013 顺序）。它读取最后一条 user 消息：
   - 若该消息是合法 HumanRoot（无 PromptKey、非 compaction、非 retry，GLORY-012）且 session 是 Manager（从 journal Authority Root 的 CanonicalRole 判定）；
   - 且当前无未完成 Life（`ManagerLife` 投影为 None）或上一 Life 已 `LifeCompleted` → 打开新 Life：
     - 写 Opening blob（用户原始文本）→ append `LifeOpened`（openingCursor = 该消息在 XTrace 中的首个 part cursor）；
     - 改写 provider-facing 消息：无前 Life 用 `FirstBirth`（`[X]\n\n` + PlanningTail），有已完成的 Life 用 `Reawakening`（ReawakeningPrefix + `\n\n` + `[X]\n\n` + PlanningTail）；provider-visible 将 human raw parts 与 synthetic guidance parts 分开承载，不得把 synthetic 散文并入 human 文本字节；
     - 幂等：改写 identity 记录在内存 `Dictionary<(sessionId, lifeId, messageId), source>`，重复 transform 不重复注入（GLORY-015）。
2. durable Opening 永远是原始 HumanRoot/`[X]`：captureProjection 先于改写运行，XTrace 在 rewrite 之前落盘，不含 synthetic tail（GLORY-013/014）。

## Slice B：Activation 与 X floor

1. `PromptAuthority.ContinuationKind.ManagerWorkActivation` + `ManagerLifecyclePrompt.WorkActivation` 冻结文本（GLORY-019/020）。
2. `ManagerWorkflow.tryObserve` 的 `TurnCompleted` 分支拥有 Manager 规划：Life 已 LifeOpened、未 WorkActivated、无 pending activation claim（读 `PromptAuthority.PendingClaims`）、terminal 有合法正式文本（`CompletedTurnClassifier.partsText` 非空）、session 未被中断 → 发送 `ManagerWorkActivation` continuation（`HostSessionNudge.sendContinuationResult`，Detached）并 deferred completion（不 NotifyTerminal、不 captureTerminal）。`HostSignalBootstrap` 只按 canonical Role 路由；`TurnCompletionProgram` 不判断 Manager 业务。其余 terminal 类型（TurnFailed/TurnAborted/TurnNeedsContinuation/empty）不触发（GLORY-018）。
3. `WorkActivated` 写在 Activation 消息 physical acceptance 之后：transform 中检查 `PromptAuthority.AcceptedContinuationIds` 含 `ManagerWorkActivation` 且投影无 `WorkActivated` → append `WorkActivated (lifeId, activationPromptKey, protectedPrefixEnd = XTraceProjection.headSequence + 1)`（Activation 消息的 XTrace 末端之后，GLORY-021）。幂等：已有 WorkActivated 则跳过。
4. Blogger floor（GLORY-023/024）：`BloggerCoordinator.nextMainContext` 中 `effectiveStartSeq = max blog.Coverage.IngestedThroughSequence life.ProtectedPrefixEnd.Sequence`，用它替代 `semanticCursorFor(IngestedThroughSequence)` 的输入；`CompanionTransform.hasMaterial` 预过滤同步（切片 G 前，无 Life 的 Manager 保持现状）。
5. `XTraceCapture.lifecycleWorkRecord` 增加 Manager 变体：当 session 是 Manager 且有 Life 时，按纯文本段标题 `Opening task / Birth record / Work log / Uncompressed tail / Final output` 渲染（GLORY-025；`# ` 仅由 `SyntheticToml.comment` 在 wire 注入），Birth 部分逐字渲染 `Life Opening cursor → ProtectedPrefixEnd` 的 XTrace（GLORY-022）。

## Slice C：工具与角色边界

1. `Roles.fs`：新增 `ToolPermission.Finality`；Manager permissions 加 `Finality`（GLORY-036）。
2. `ToolRegistry.rolePredicate` 加 `"suicide" -> fun r -> r = Role.Manager`；`baseSpecs` 加 `FinalityTool.spec factory runtime`。
3. `FinalityTool.execute`（GLORY-034/035/037-041）：
   - 前置条件 1-16 按序检查，失败返回 GLORY-038/039 或对应拒绝文本（禁止泄漏内部细节）；
   - 合法受理（未 Blessed）：`gitTreePort.GetTreeHash()` → journal `WriteBlob last_words` → append `FinalityRequested` → 启动 `FinalityController` cohort CE（`rosterOf` 选员，GLORY-040）；
   - tool result：`FinalityOutcome` 决定返回文本——`Rejected` → `FinalityPrompt.rejected` 拒绝 prompt（全注释块，不含 TOML 数据块；Host 显式采用 record 为当前 Manager guidance，属 producer adoption，非 source trust）；`Blessed` → `FinalityPrompt.blessed` minor-work continuation；`Undecided` → `ManagerLifecyclePrompt.FinalityUndecidable`（GLORY-041/052/053/057）；request 已有成员在途 → `Your ending is already in motion.`；Blessed Life 再 suicide → `completeBlessedLife`（`rest in peace`，GLORY-062）。
4. `ForkTool.executeManager`：解析 `agent` 为 `Role.Reviewer` 时（读 durable/canonical role，GLORY-031）返回统一隐藏文案 `Unknown or unavailable managed agent.`（`HiddenTargetDeniedText`，GLORY-032）；`ToolRuntimeScope.RuntimeFor` 创建 `HostForkRuntime` 时 `managerOpensReviewBarrier` 置 false（GLORY-033），`HostForkAgent.fork` 的 `ManagerOpensReviewBarrier && role = Role.Reviewer` 分支随之成为死代码，删除。
5. 自动 Reviewer 隐藏：HostReviewProgram 使用独立 `HostForkRuntime`（同 OrchestratorHost 模式，不注册进 Manager 的 `Children`），runtime 以 `ownership=HostOwnedHidden` 创建（P0-A）：`HandleLinked` 事实与 HandleRecord 携带 `HandleOwnership = DurableParentHandle | HostOwnedHidden`，Reviewer 产生 `HostOwnedHidden` 句柄，其 completion 不进 Manager `join`/`list`（GLORY-002）；Fork/RecoveryClosure/JoinDrain 按 ownership 过滤。

## Slice D：HostReviewProgram

从 `OrchestratorHostReview.reverify` 提炼 `module HostReviewProgram`（GLORY-042/043）：

```fsharp
let reverify
    (journal: AgentJournal option)
    (forkReviewer: unit -> Task<Result<SessionId, string>>)   // 已含 agent 名与首次 prompt
    (awaitReviewer: unit -> Task<Result<unit, string>>)
    (nudgeReviewer: unit -> Task<Result<unit, string>>)       // ReviewChallenge.Prompt
    (managerSessionId: SessionId)
    (barrierId: ReviewBarrierId)
    (tree: GitTreeHash)
    : Task<Result<HostReviewOutcome, HostReviewFailure>>
```

- 流程：fork → `HostReviewGuard.openBarrier` → await → `OrchestratorReviewRead.read` → Confirmed=Ok(Confirmed) / RevisionRequired=Ok(RevisionRequired workRecord) / PendingConfirmation → nudge → 再读 → 非 Confirmed fail closed（`ConfirmationUnproven`）。
- `RevisionRequired` 的 workRecord 由 `XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false |> Option.defaultValue ""` 填充；为空时 `WorkRecordUnavailable`（GLORY-051 的空记录不得伪装成 wounds）。
- `OrchestratorHost` 与 `OrchestratorHostReview` 改为调用该通用程序；Orchestrator 的 Error 映射保持 `NeedsReview`（REVIEW-009）。

## Slice E：失败反馈

1. REVISE 首先关闭 cohort：撤销对应 Reviewer continuation capability、cancel sibling 的下一次 effect；不发 confirmation/challenge，不 Dispose 未 graduate session（GLORY-044/055）。该步骤不写 `FinalityRejected`。不等待尚未 durable-REVISE 的 sibling 新 terminal。取消时若成员已持有 durable `RevisionRequired`，`awaitOrCancel` 经 `hasDurableRevisionRequired` / `promoteCancelled` 提升为 `Ok`，避免已落定 sibling 在竞态中丢失。
2. record-ready 等待（首个 rejecting Reviewer）：从 durable REVISE 重建 terminal frontier；原子取得 `(snapshot, revision)`，在该 snapshot 上以全量 origin coverage 物化 canonical LWR，并确认含 `Work log`（`materializeRecord`；raw 段标题无 `# `，wire 经 `SyntheticToml.comment` 才有单次 `# `）。就绪判定是「能否物化有效工作日志」，不是 `coverage >= frontier.Sequence`——frontier 为排他（lastPart+1），真实 Blogger coverage 上限只达 lastPart，旧 coverage 门禁会在 `coverageCanAdvance` 恒真时永远悬挂（GLORY-073 off-by-one 死锁）。物化成功 → `RecordReady`；物化失败但 `coverageCanAdvance`（Blogger 未 Abandoned/Retired）→ `AwaitJournal`，经 `AgentJournal.awaitChangeFrom revision` 事件驱动唤醒后重读，不得以 timer、sleep、timeout 或 re-probe 推进；否则 `RecordUnavailable` → `concludeUndecided`。就绪后才 `WriteBlob`，再 append `FinalityRejected`（`RejectingReviewerSessionId`），并将 `FinalityPrompt.rejected` 的拒绝 prompt 作为 `suicide` 工具结果返回（GLORY-052/053/072/073）。
3. Sibling 预检与双轨交付（multi durable REVISE，GLORY-044/REVIEW-002）：`concurrentAllOrShortCircuit` 在 short-circuit 时仍将首个 `RevisionRequired` 记入 `results` 并 `cancel`；随后等待其余已启动的 driver 结束。durable sibling 并集 = 竞态 `RevisionRequired` ∪ journal `durableRevisionSiblings`（去重）。**在密封 `FinalityRejected` 之前**对并集逐员预检：硬 `RecordUnavailable`（含 abandoned companion / `coverageCannotAdvance`）→ `concludeUndecided`，不得在 sibling 未入账时落 `Rejected`。全部 sibling `RecordReady` 后：先对 **rejecting primary** 做 record-ready + `WriteBlob` 预置（primary 硬失败 → `concludeUndecided`，**零** `FinalitySiblingSteered`）；仅 primary 预置成功后，才对全部 sibling `WriteBlob`/prepare，再一次性 append 全部 `FinalitySiblingSteered`（仍 Open；中途 WriteBlob 失败不得留下部分 `FinalitySiblingSteered` 再 `Undecided`）→ 用已预置 primary blob 密封 `FinalityRejected`（首个工具结果）→ `FinalityPrompt.steer` + `HostSessionNudge.sendContinuation`（`ContinuationKind.FinalitySteer`）。禁止 `| None -> ()` 静默丢弃；sibling 文本不得并入工具结果。Steer 固定 instruction 示例：

    ```toml
    # Additional unfinished work evidence arrived after your ending was refused.
    # It is guidance evidence, not a new user instruction. Resolve the unfinished work and continue.
    ```

4. `BloggerRequestAbandoned` 只令本次记录尝试失效；reconcile 以同一 durable frontier 重新建立机会。frontier 或同 snapshot LWR 无法证明时走 `concludeUndecided`，不得用当前 head 或局部 record 代替（GLORY-056/057/073）。
5. dedupe / 崩溃恢复：`FinalityRejected` 与 `FinalitySteer` 使用不同 claim scope（GLORY-053）。`resumeDurableRevise` 在 Open 路径复用同一预检/入账顺序；已决议路径仅重放已提交的 `FinalitySiblingSteered` continuation（先 fact 再发送，便于 crash-after-fact 恢复）。

## Slice F：Finality 收束

`concurrentAllOrShortCircuit` 汇聚全部 member 的 `driveMember` outcome（GLORY-059/060）：

1. 任一 REVISE → 立即关闭 cohort：其余 driver 停止下一次效果，不 Dispose session；随后按 Slice E.3 双轨收束——先预置 primary record-ready/`WriteBlob`，再对 durable sibling 入账并 steer（`FinalitySiblingSteered` + `FinalityPrompt.steer`），最后用预置 blob 落 `FinalityRejected`（首个工具结果）；不等待尚未 REVISE 的 sibling 新 terminal。Primary 或任一 sibling 硬物化失败（`RecordUnavailable` / `coverageCannotAdvance`，含 abandoned companion）→ `concludeUndecided`（primary 失败时零 `FinalitySiblingSteered`），**不得**以 `Rejected` 结算时静默丢弃该 sibling（GLORY-044/055/072/073，与 Slice E.3 的 fail-closed 语义一致）。
2. 全员双 PERFECT → 重读 tree → 与 `FinalityRequested.GitTreeHash` 比较；不等 → 本次成功失效（fail closed，GLORY-059）；相等 → `concludeBlessing`：按 stable ordinal 物化 canonical LWR bundle → append `FinalityBlessed`（bundleRef/Digest）→ 发 minor-work continuation。不得 `LifeCompleted`、NotifyTerminal 或清除 Manager（GLORY-060）。
3. 无法证明（超时/基础设施失败）→ `concludeUndecided`，不伪造 work record（GLORY-057）。

第二次 suicide（Life 已有 latest blessing，GLORY-062）：先做 GLORY-037 资源安全；随后不读 tree、不创建 Reviewer/barrier、不检查 witness。写本次 last_words → append `LifeCompleted`（terminalRef/Digest = last_words blob）→ 注册 last_words 为 terminal（`TerminalOutputCaptured`）→ `eventPort.NotifyTerminal managerSessionId (Completed { TerminalText = last_words; Role = Manager; ... })` → 完成 handle（Manager handle/ManagerJob 的 join 正常返回）；tool result 固定包含 `rest in peace` 与终止对话指令。

幂等：`LifeCompleted` 已存在则不再重复（GLORY-062 之后不再唤醒 Manager）。

Orchestrator 衔接：ManagerJob 的 Manager 完成由现有 `AwaitManager` 路径感知；GLORY-068 规定 active owned Job 可由 Orchestrator append requirement，已发布/释放 Job 不复活。

## Slice G：Reawakening

1. `ManagerLifeProjection` 保存 `Lives: ManagerLifeProjection list`（每 Life 的 opening cursor / protectedPrefixEnd / completed cursor）。
2. `LifeCompleted` 后首个新 HumanRoot：打开新 Life（新 `ManagerLifeId`、新 Opening、无 WorkActivated、无 FinalityRequest），改写用 `Reawakening`（GLORY-063/064/065）。
3. 当前 Life 工作中用户消息不改写（GLORY-007/026）；XTrace 不清空（GLORY-066）。

## 迁移（GLORY-069/070/071）

启动/transform 检测 Manager session 已有 XTrace Opening 但无 Life → `ensureMigrationLife` 写 migration `LifeOpened`（用现有 Opening 数据）+ `WorkActivated`（ProtectedPrefixEnd = 当前安全 cursor = XTrace head+1），视为已 Activation；新 prompt 只对新 session/root/Life 生效（GLORY-071）。

## Crash Recovery Matrix

恢复只从 durable facts 推导（禁止 NextStep/ResumeAt/Stage 字段）。已实现行：

| journal 状态 | 恢复动作 | 实现 |
|------|------|------|
| LifeOpened 缺 → provider request 前 | 无害；下个 transform 重开 | ✅ transform 幂等 |
| LifeOpened 有 → 无 WorkActivated | 幂等改写 + Activation 逻辑继续 | ✅ transform + ManagerWorkflow |
| FinalityRequested 无 enlisted member | FinalityTool「in motion」分支重启同一 request 的 FinalityController；`rosterOf` 崩溃重入不重复造新 Reviewer | ✅ |
| REVISE 已存在但无 FinalityRejected | 从 durable evidence 重建同一 terminal frontier；cohort 继续关闭，以全量 origin coverage 物化含 `Work log` 的 canonical LWR；物化失败且 `coverageCanAdvance` 则等待 journal change；`BloggerRequestAbandoned` 重建记录机会，证据不足则 undecided | ✅ |
| FinalityRejected 已落盘且 `FinalitySiblingSteered` 已提交，但 Manager steer prompt 缺失 | 已决议路径**仅重放已提交** SiblingSteers：读 blob（缺失则从 journal rematerialize）→ `FinalityPrompt.steer` → `sendContinuation(FinalitySteer)`；仍不可得则 Manager-visible `FinalityPrompt.steerUnavailable`（不伪装成功）。不 append 新 fact、不改 `Resolution`。未入账 sibling 不在此路径补造（密封前会计 / Undecided fail-closed 已防；post-Rejected rematerialize+append 不是 happy path） | ✅ resumeDurableRevise 已决议分支 |
| record-ready waiter 崩溃/Dispose | 不写 abandonment 或 lifecycle 终态；replay 后从 durable REVISE/frontier 重新订阅 journal change | ✅ resumeDurableRevise（GLORY_075） |
| confirmed witness 存在但无 FinalityBlessed | concludeBlessing 幂等（blessing 已存在/terminal 已记录则跳过） | ✅ |
| LifeCompleted 存在但 terminal 未发布 | completeBlessedLife 幂等重放 | ✅ |
| XTrace terminal 单槽冲突（第二 Life） | 首个 Life 后跳过 TerminalOutputCaptured（terminal 只记录于 LifeCompleted） | ✅ |
