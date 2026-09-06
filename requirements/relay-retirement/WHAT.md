# relay-retirement — WHAT

## RETIRE-001: suicide 是唯一正常模型出口

正常 assistant stop 不产生 retirement。模型正常离场只有 accepted `suicide`；authority revocation、session deletion、fatal fuse 与 provider capacity exhaustion 属于独立 exceptional terminal，不伪造 suicide。

## RETIRE-002: 评分不可跳过；进度、测试、义务与 Git 状态不阻塞 retirement

Manager 严禁跳过八维质量评分直接 suicide。若未提交 review assessment 试图 suicide，工具直接在返回值中提示必须先调用 review 完成质量评分。一旦质量评分已提交，则不论分数高低、测试成败、open obligations 多少、worktree 是否 dirty/unmerged，均不得成为 suicide 阻塞项。

## RETIRE-003: 递归 live resources 是唯一业务 blocker

suicide 只检查当前 IncumbencyId 直接或递归拥有的 live child、background job、PTY/process、active tool execution、side-effect/execution lease、同步 descendant provider work 与未观察到 terminal 的 cancel/join。

## RETIRE-004: retirement 必须 freeze-before-check

admission 先冻结，再读取 exact recursive ownership projection。冻结前已 accepted 的资源必须阻塞；冻结后的创建因 stale fence 被拒绝。freeze fence 绑定精确 `IncumbencyId`，不得只绑定可复用的物理 `SessionId`；前任退休后下一迭代即使复用同一 SessionId，也不得继承前任 fence。若有 blocker，只恢复当前迭代 cleanup capability，不恢复新工作 admission。

## RETIRE-005: 已删除——normal-stop nudge 归属 interaction-authority

此编号永久空缺。manager guard 的 admission、飞行态与 fresh-terminal re-arm 只由 INTERACTION-AUTHORITY-019 定义。

## RETIRE-006: 已删除——provider failure 归属 execution-failure-policy

此编号永久空缺。provider/network failure、capacity settlement 与 fresh-attempt authorization 只由 execution-failure-policy 和 managed-chat-execution 定义。

## RETIRE-007: retirement 提交闭合 outcome 与 cut

成功 retirement 在同一 durable transaction 中记录 `IncumbencyRetired` 与 `RetirementCommitted({ Id; IncumbencyId; SnapshotId; AuthorityRevision; ProjectionCut = { ProviderRunId; ToolCallId }; Outcome })`，其中快照与 authority 修订是 load-bearing retirement binding。`Outcome = Continue` 表示工作待续：同一 LogicalRun 保持开放，下一迭代就位后继续；Continue 的 retirement 快照取退休时当前快照，允许与 assessment 时快照不同并前向携带给下一迭代。`Outcome = Accepted certificateId` 要求快照等于 assessment/证书快照：证书须有效且属于当前迭代并绑定该快照，不同快照的 Accepted 一律拒绝。两者 authority 都必须等于当前。CleanupBlocked 的 perfect 迭代在 blockers 清除后可重试 Accepted。证书有效期间不激活任何新迭代，后续显式 `QualityCertificateInvalidated` 使该证书失效后允许普通新迭代。崩溃恢复不得看到永久的“已退休但无 outcome/cut”状态；ManagerLoopSignal 由匹配 Outcome 派生（Accepted 证书→Candidate，Continue→Continue）。

## RETIRE-008: 退休工具返回与下一迭代派发之间建立物理中断边界

suicide 工具体只提交 durable retirement 并返回结果，不调用 session 级 `InterruptAttempt`/`AbortSession`。两种 retirement 后，退休 run 的后续 provider 请求都在 transform 钩子按退休边界与正式 manager-loop gate 身份拦截：清空旧 transform 消息，释放该请求已取得的 exact provider-step admission，再等待旧 attempt 的 Host interrupt 完成。Continue 随后自动派发下一迭代；Accepted 只终止旧 attempt，永不由 Narrative 自动派发。下一迭代复用同一物理 SessionId，因此禁止在其派发后补发针对已退休迭代的 session abort。显式证书失效后，Change ContinueLoop 可派发普通新迭代，该新迭代同样使用 LatestRetirement cut。已退休输出由 durable cut（`RetiredProviderRunIds` 吸收迟到 parts）与 Retired phase tool denial 隔离；新迭代不能复活已退休迭代，也不能结束承载 Road 的 active authority。
