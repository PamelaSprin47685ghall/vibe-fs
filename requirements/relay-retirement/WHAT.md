# relay-retirement — WHAT

## [001] suicide 是唯一正常模型出口

正常 assistant stop 不产生 retirement。模型正常离场只有 accepted `suicide`；authority revocation、session deletion、fatal fuse 与 provider capacity exhaustion 属于独立 exceptional terminal，不伪造 suicide。

## [002] 评级不可跳过；进度、测试、义务与 Git 状态不阻塞 retirement

Manager 严禁跳过八维质量评级（PERFECT/REVISE/N/A）直接 suicide。若未提交 review assessment 试图 suicide，工具直接在返回值中提示必须先调用 review 完成质量评级。一旦质量评级已提交，则不论评级结果如何（是否含 REVISE）、测试成败、open obligations 多少、worktree 是否 dirty/unmerged，均不得成为 suicide 阻塞项。

## [003] 递归 live resources 是唯一业务 blocker

suicide 只检查当前 IncumbencyId 直接或递归拥有的 live child、background job、PTY/process、active tool execution、side-effect/execution lease、同步 descendant provider work 与未观察到 terminal 的 cancel/join。

## [004] retirement 必须 freeze-before-check

admission 先冻结，再读取 exact recursive ownership projection。冻结前已 accepted 的资源必须阻塞；冻结后的创建因 stale fence 被拒绝。freeze fence 绑定精确 `IncumbencyId`，不得只绑定可复用的物理 `SessionId`；前任退休后下一迭代即使复用同一 SessionId，也不得继承前任 fence。若有 blocker，只恢复当前迭代 cleanup capability，不恢复新工作 admission。

## [007] retirement 提交闭合 outcome 与 cut

成功 retirement 在同一 durable transaction 中记录 `IncumbencyRetired` 与 `RetirementCommitted({ Id; IncumbencyId; SnapshotId; AuthorityRevision; ProjectionCut = { ProviderRunId; ToolCallId }; Outcome })`，其中快照与 authority 修订是 load-bearing retirement binding。`Outcome = Continue` 表示工作待续：同一 LogicalRun 保持开放，下一迭代就位后继续；Continue 的 retirement 快照取退休时当前快照，允许与 assessment 时快照不同并前向携带给下一迭代。`Outcome = Accepted certificateId` 要求快照等于 assessment/证书快照：证书须有效且属于当前迭代并绑定该快照，不同快照的 Accepted 一律拒绝。两者 authority 都必须等于当前。持有合规有效质量证书（PERFECT）提交退休时，直接以 Accepted 完满闭合；当前任期的义务账本随同退休出清，不随 Manager 继任而死板继承；新迭代就位后根据最新物理世界与输入建立属于新任期的独立账本。CleanupBlocked 的 perfect 迭代在 blockers 清除后可重试 Accepted。证书有效期间不激活任何新迭代，后续显式 `QualityCertificateInvalidated` 使该证书失效后允许普通新迭代。崩溃恢复不得看到永久的“已退休但无 outcome/cut”状态；ManagerLoopSignal 由匹配 Outcome 派生（Accepted 证书→Candidate，Continue→Continue）。

## [008] 退休工具返回与下一迭代派发之间建立物理中断边界

suicide 工具体只提交 durable retirement 并返回结果，不调用 session 级 `InterruptAttempt`/`AbortSession`。两种 retirement 后，退休 run 的后续 provider 请求都在 transform 钩子按退休边界与正式 manager-loop gate 身份拦截：清空旧 transform 消息，释放该请求已取得的 exact provider-step admission，再等待旧 attempt 的 Host interrupt 完成。Continue 随后自动派发下一迭代；Accepted 只终止旧 attempt，永不由 Narrative 自动派发。下一迭代复用同一物理 SessionId，因此禁止在其派发后补发针对已退休迭代的 session abort。显式证书失效后，Change ContinueLoop 可派发普通新迭代，该新迭代同样使用 LatestRetirement cut。已退休 attempt 的后续 provider 请求由 durable cut（`RetiredProviderRunIds` 吸收迟到 parts）与 Retired phase tool denial 在请求级隔离；已退休迭代的物理消息作为历史保留，对继任迭代完整可见。新迭代不能复活已退休迭代，也不能结束承载 Road 的 active authority。

## [009] 固定 DevOps 与跨任期资源在退休中的交接与收束边界

Manager 迭代正常退休（`Outcome = Continue`）时，道路绑定的固定 DevOps 及其后台持久进程不因前任离场而隐式孤儿化或被强制销毁；旧任退休仅注销其自身的派工与控制租约，已接收的工作继续由固定 DevOps 运行并保留给后继迭代接管。只有在道路最终关闭、Accepted 证书终结或遇到致命 exceptional terminal 时，才按资源责任规则触发对固定 DevOps 及其衍生进程的物理收束。严禁假定 Manager 分身并发持有或随意遗留无主执行资源。
