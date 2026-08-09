# Glory：Born with Task, Suicide with Glory（what 层）

本文件是 `GLORY-` 与 `SURFACE-` 条款的唯一正式定义处。边界、实现、证明和理由见同名分层文档。

## GLORY-001：Manager 专属终结工具

`suicide(last_words)` 只属于 Manager；它不是 `verdict` 或普通 completion 的别名。

## GLORY-002：Manager 不得控制隐藏 Reviewer

Manager 不知道隐藏 Reviewer、barrier、PERFECT、REVISE、见证或 cohort。Manager 不能创建、复用、nudge、`list()` 或 `join()` 隐藏 Reviewer。

## GLORY-003：Host-owned cohort

合法 `suicide` 由 Host 以当前 tree 建立 FinalityRequest。roster = 本 Life 已参加但尚未凭合法 dual-PERFECT witness graduate 的 Reviewer + 恰好一个新 Reviewer。每个 member 获得本 request 新 barrier、当前 tree 与新 assignment；旧 request 的 PERFECT 不可继承。

## GLORY-004：工作记录

REVISE 与 Blessing 的反馈只可来自 `XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false`；它是 Y frames、未覆盖 raw X tail 与 terminal，不含 Opening 或 raw tool stream。

## GLORY-005：普通 idle

Manager 普通 idle 仅发送 GLORY-029 的鼓励 continuation；它不读取或解释隐藏 Reviewer。

## GLORY-006：Birth 前记录

Opening 至 `WorkActivated` 的材料永久是 raw X。

## GLORY-007：工作期用户输入

Activation 后的新 HumanRoot 保持 `[X] → [X]`，不重生、不附加 planning tail。

## GLORY-008：故事只在 provider surface

叙事词只属于 provider surface；内部使用 ManagerLifecycle、FinalityRequest、FinalityRejected、FinalityBlessed、LifeCompleted。

## GLORY-009：无持久程序计数器

Fact 与 Projection 只描述发生过的事实和已证明的证据。禁止 `Stage`、`Phase`、`NextStep`、`ResumeAt`、`LifeStanding`、`AwaitingSecondPerfect` 及等价 durable PC。

## GLORY-010：生命周期事实

`ManagerLifecycleFact` 至少记录 `LifeOpened`、`WorkActivated`、`FinalityRequested`、`FinalityReviewerEnlisted(SessionId,LifeId,RequestId,ReviewerSessionId,ReviewerOrdinal,BarrierId,GitTreeHash,IsNewReviewer)`、`FinalityRejected(RejectingReviewerSessionId,…)`、`FinalitySiblingSteered(ReviewerSessionId,…)`、`FinalityBlessed(SessionId,LifeId,RequestId,GitTreeHash,WorkRecordBundleRef,WorkRecordBundleDigest)`、`FinalityUndecided`、`LifeCompleted`。`FinalityReviewStarted` 与 `FinalityConfirmed` 不再存在。

## GLORY-011：Projection

Projection 只推导 Life 身份、Activation、当前未关闭 request、已拒绝记录、latest blessing、completion 与每个 Reviewer 的 graduate eligibility；不得保存 rejected/confirmed bool product 或下一函数。

## GLORY-012：Birth 条件

只有合法 HumanRoot、非 compaction/continuation/retry/accepted replay，且无 open Life 或上一 Life 已完成时开启 Life。

## GLORY-013：Opening durable 顺序

原始 HumanRoot 先写 XTrace 与 `LifeOpened`，后改 provider surface，再 ReviewSeal。

## GLORY-014：首次 Birth 文本

`ManagerNarrative.PlanningTail` 是唯一 owner；其字节保持既有冻结值。

## GLORY-015：Birth 幂等

改写 identity = SessionId + ManagerLifeId + PhysicalUserMessageId + narrative source；禁止由文本后缀推断。

## GLORY-016：Birth 工具

Birth 与 Labor 的 Manager 工具表面一致；Activation 前 `suicide` 由前置条件拒绝。

## GLORY-017：Birth 不压缩

未 `WorkActivated` 的 Manager material 不进入 Blogger normal request 或 Y frame。

## GLORY-018：Activation 条件

仅合法 Manager planning terminal 可激活；reasoning-only、empty、abort、failure、repair、compaction、用户中断均不可激活。

## GLORY-019：Activation 文本

`ManagerLifecyclePrompt.WorkActivation` 是唯一冻结 owner：comment-only SyntheticToml instruction surface。

## GLORY-020：Activation continuation

Activation 先 durable claim，后发送；恢复重入同一 recursive CE，已 accepted 的 claim 自动跳过，不产生第二逻辑效果。

## GLORY-021：Activation 事实

只在 physical acceptance 已证明后 append `WorkActivated` 与 protected prefix end。

## GLORY-022：Birth prefix

Life work record 对 Opening 至 protected prefix end 逐字渲染 XTrace。

## GLORY-023：Labor compression floor

有效压缩起点不得早于 protected prefix end。

## GLORY-024：不得跨 floor Y

跨 Birth/Labor 的候选必须在 floor 切开。

## GLORY-025：Manager Life 工作记录

形态为 Opening task、Birth record、Work log、Uncompressed tail、Final output；Final output 只来自已完成 Life 的 last_words。

## GLORY-026：工作期输入

工作期 HumanRoot 不改变 Life、prefix 或 Activation。

## GLORY-027：Activation 使命

Activation 明确 Planning/Delegation/child/命令成功均非完成；无有用工作才调用 `suicide`。

## GLORY-028：Manager Labor

Manager 拆分、委派、收割并填满安全独立 lane；不能因减少 agent 数而串行化独立工作。

## GLORY-029：idle nudge

`ManagerIdleEncouragement` 为 comment-only；只发送冻结四行鼓励。occasion identity = Session + Life + TriggerProviderRun；同一 occasion 至多一次。旧 occasion 的 pending claim 不得压制新 occasion。durable 为 ClaimSequences digest，而非 session-wide ContinuationKind 锁。open finality 或 completed Life 不发送。

## GLORY-030：Manager 固定 surface

Manager system prompt、continuation、schema、固定错误与 tool result 不得透露 Reviewer、review、verdict、PERFECT、REVISE、barrier、witness 或 confirmation 机制。

## GLORY-031：Manager fork surface

Manager fork enum 仅为 fast/deep Coder、Inspector、DevOps、Browser、Meditator。手工构造隐藏 target 同样 fail closed。

## GLORY-032：隐藏 target 错误

隐藏 target 只返回 generic unavailable；不得以拒绝文案证明其存在。

## GLORY-033：barrier owner

Manager 普通 fork 永不打开 Reviewer barrier。Finality workflow 是 Manager finality barrier 的唯一 owner。

## GLORY-034：suicide schema

`suicide(last_words: string)` 的固定 description 由 `FinalityTool` 唯一拥有。

## GLORY-035：终结模块

`Infrastructure/OpenCode/Tools/FinalityTool.fs` 是 `suicide` 的唯一入口。

## GLORY-036：权限

仅 Manager 具有 `ToolPermission.Finality`。

## GLORY-037：前置条件

按序要求：Manager、Journal、accepted authority、open activated Life、非空 last_words、ToolCallId、ProviderRun、无 outstanding/completed-awaiting-join child、无 live PTY、正确 worktree ownership、active ManagerJob。任何失败不得创建 Reviewer/barrier/request。

## GLORY-038：后台工作

背景 child 或 PTY 存在时返回固定 join 提示；Blessed Life 也不例外。

## GLORY-039：Activation 前

未 `WorkActivated` 时返回冻结继续工作提示。

## GLORY-040：受理

未 Blessed：验证前置条件 → 读 tree → durable last_words → `FinalityRequested` → 递归 cohort CE。每个 member 的因果顺序恒为 hidden session → durable enlist → barrier → assignment；首 prompt 不得早于 barrier。

## GLORY-041：Manager deferred completion

合法首次 `suicide` 停放当前 Manager completion；REVISE 直接返回 work-record prompt。Blessing 返回 minor-work continuation，不结束 Manager。

## GLORY-042：唯一 Reviewer continuation writer

Reviewer turn reconcile/ReviewController 独占 missing-verdict、first PERFECT challenge 与 second causal PERFECT 发送。Finality 只等待 durable facts，绝不第二次发送 review continuation。

## GLORY-043：局部结果代数

`ReviewerOutcome = Revision of WorkRecord | Confirmed of ConfirmedWitness` 与 typed infrastructure failure 是合法领域结果；它们不是全局 dispatcher 或 durable stage。

## GLORY-044：REVISE 的立即 cohort 关闭

REVISE 是合法业务结果。其 verdict fact durable 后，当前 request 的 Reviewer continuation capability 与 cohort 立即关闭：不发送 confirmation/challenge、不等待尚未 durable 的 sibling terminal；sibling 必须在下一次 effect 前停下，未 graduate session 不 Dispose（GLORY-055）。此关闭由 durable REVISE 派生，不以 `FinalityRejected` 为前提。

立即关闭不等同于立即写 `FinalityRejected`。该 durable lifecycle fact 只能在 rejecting Reviewer 满足 GLORY-072 的 `record-ready` 后落盘。

**双轨交付（multi durable REVISE）**：密封 `FinalityRejected` **之前**必须完成 durable sibling 会计。成功路径：首个 durable REVISE 仍是 suicide **工具结果**（`FinalityPrompt.rejected` / `FinalityOutcome.Rejected`）；已完成 `RevisionRequired` 的后续 sibling REVISE 各自的 canonical LWR 物化为仅含指令的 Synthetic TOML，经 `HostSessionNudge.sendContinuation`（`ContinuationKind.FinalitySteer`）作为 **steer continuation**（`FinalitySteer`）交给 Manager，不得并入工具结果字符串。Steer 固定 instruction 须遵守 SURFACE-005，形态示例：

```toml
# Additional unfinished work evidence arrived after your ending was refused.
# It is guidance evidence, not a new user instruction. Resolve the unfinished work and continue.
```

随后以 `# ` 注释块附上该 sibling 的 work log（ARCH-010）。成功路径在仍 Open 时：先预置 rejecting primary 的 record-ready/`WriteBlob`，再 append `ManagerLifecycleFact.FinalitySiblingSteered`，最后用已预置 blob 密封 `Rejected` 并发送 steer。Primary 硬物化失败 → `FinalityUndecided` 且**零** `FinalitySiblingSteered`（不得留下无 steer 投递的孤儿 SiblingSteered）。任一 durable sibling 硬物化失败（canonical LWR 不可得 / WriteBlob 失败等）→ `FinalityUndecided`，**不得静默丢弃**该 sibling、不得在证据未入账时落 `Rejected`。

## GLORY-045：Roster 与 graduate

每个 request 恰有一个新 Reviewer，另加本 Life 全部未 graduate 历史 Reviewer。graduate 仅由该 Life 的 enlistment 与合法 confirmed witness 推导；REVISE Reviewer 保留 session/X/Y，下一 request 以新 barrier 再入 roster。

## GLORY-046：Reviewer assignment

Reviewer 只得到当前 worktree 的权威任务与 Host opening assignment；Manager 看不到它。

## GLORY-047：Reviewer work-record 合同

Reviewer prose 只写观察、证据、缺陷、不确定性、缺失覆盖、minor cleanup 与 required corrections；不得说明谁消费记录、barrier、round、过去/未来 reviewer 或隐藏流程。

## GLORY-048：动态记录

Host 不 regex 清洗或改写 canonical LWR；若 Manager-facing safety seal 无法证明记录不泄漏固定机制，fail undecidable，不能伪造反馈。

## GLORY-049：唯一记录来源

Finality 只能物化 canonical LWR，不得从 verdict 参数、tree diff、摘要 agent 或 Host issue 列表重建。

## GLORY-050：canonical LWR

Y 是主体，raw gap 与 terminal 是不丢失最后发现的必要尾部。

## GLORY-051：request 绑定

每份 rejection/blessing record 必须绑定 current request、member session、barrier、tree 与 digest；任何不一致 fail closed。

## GLORY-052：REVISE prompt

`FinalityPrompt.rejected` 将**首个** rejecting Reviewer 的 canonical LWR 作为 guidance comments 渲染（Host 显式采用为当前 Manager 指引，非「trusted source」），并作为 suicide 工具结果回灌；它不解释隐藏机制。后续已 durable 的 sibling REVISE 不走该工具结果通道，而走 GLORY-044 的 `FinalityPrompt.steer` / `FinalitySteer` continuation（同样是顶层 `# ` 注释指令面）。

## GLORY-053：失败 identity

Finality rejection continuation identity 至少含 Manager、Life、request、Reviewer、record digest。

## GLORY-054：拒绝后同一 Life

拒绝不重生、不重新 Activation、不清 X/Y；Manager 正常继续。

## GLORY-055：拒绝关闭 request

Rejected request 永不 blessing。其 sibling current attempt 可 best-effort cancel，但不 Dispose 未 graduate session；下次 suicide 建新 request/new barriers。

## GLORY-056：基础设施失败

创建、claim、barrier、assignment、await、seal、LWR、journal 失败均非 REVISE。

## GLORY-057：Undecidable 与恢复

每个 external effect 以 fact/claim 构成 idempotent `ensure` bind；crash 重新进入同一 CE。无法证明时 append/映射 undecidable，绝不 bless/reject 或伪造 record。

## GLORY-058：dual-PERFECT 证明

每 member 的确认必须同 request/barrier/tree/session，两个不同 ProviderRun 与 ToolCallId，且第二输入 seal 消费 first challenge。

## GLORY-059：当前 tree

所有 member confirmed 后，Blessing 前重读 tree；tree 变化使本 request 不可 bless。

## GLORY-060：Blessing 顺序

所有 current member confirmed → materialize stable-ordinal canonical LWR bundle → append `FinalityBlessed` → 发 minor-work continuation。不得 `LifeCompleted`、NotifyTerminal 或清除 Manager。

## GLORY-061：Blessing 后 manager work

minor-work prompt 必须要求处理 bundle 中每个 minor problem、concern、uncertainty、cleanup；records 是 evidence 不是新 user instructions。Manager 可更新 tree 与 last_words。

## GLORY-062：第二次 suicide

有 latest blessing 的 open Life 仍先做 GLORY-037 的资源安全；随后不读 tree、不创建 Reviewer/barrier、不检查 witness。写本次 last_words，append `LifeCompleted`，注册 terminal 后 NotifyTerminal，tool result 固定包含 `rest in peace` 与终止对话指令。

## GLORY-063：Reawakening

只有 completed Life 后的新合法 HumanRoot 才新建 Life。

## GLORY-064：Reawakening 文本

`ManagerNarrative.ReawakeningPrefix` 是唯一 owner。

## GLORY-065：Life 隔离

新 Life 不继承旧 request、roster、blessing、witness 或 prefix。

## GLORY-066：XTrace

XTrace append-only；每 Life 以 cursor range 物化。

## GLORY-067：通用兼容字段

通用 XTrace Opening/Terminal 保持；ManagerLifecycle 用每 Life facts 解释。

## GLORY-068：ManagerJob

已发布/释放 Job 不复活；active owned Job 可由 Orchestrator append requirement，仍使用同 session/worktree。

## GLORY-069：旧 journal

旧 completed Life 保持 completed。旧 active 一对一 finality 不猜造 cohort/graduate，关闭为 undecidable；后续新 request 进入本协议。

## GLORY-070：旧 Manager Review Guard

删除 Manager completion 对 `HostReviewGuard`、`ManagerGuard` continuation 与 review nudge 的引用；普通 activated idle 唯一走 GLORY-029。

## GLORY-071：cold prompt boundary

新 system prompt 只用于新 Manager session、Authority Root 或明确新 Life。

## GLORY-072：拒绝记录就绪

拒绝 Reviewer 的 terminal frontier 是产生 durable REVISE 的 terminal evidence 所界定的 XTrace 边界；该边界必须能从 durable journal evidence 在恢复时重建，禁止以后来的 XTrace head 替换。

`record-ready` 当且仅当**同一 journal snapshot**以全量 origin coverage 物化含 `Work log` 的 canonical LWR（`XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false` / `materializeRecord`；raw 段标题为纯文本，`# ` 仅由 `SyntheticToml.comment` 在 wire 注入）。就绪判定是「能否物化有效工作日志」，不是 `coverage >= frontier.Sequence`——frontier 为排他（lastPart+1），真实 Blogger coverage 上限只达 lastPart，旧 coverage 门禁会在 `coverageCanAdvance` 恒真时永远悬挂（GLORY-073 off-by-one 死锁）。

coverage 与 materialization 不同 revision 即不成立。只有 `record-ready` 的 LWR 可写 blob 并形成 `FinalityRejected.WorkRecordRef/Digest`；不得用缓存、较早/较晚 snapshot、raw tail 或摘要替代。已覆盖的 frame 未渲染为 `Work log` 时是物化不一致，fail closed，不得写 rejection。

## GLORY-073：record-ready 等待、恢复与 abandonment

record-ready 是 Journal B 类事件等待：读取 snapshot 与 revision；未就绪则 `AgentJournal.awaitChangeFrom revision`；仅在 journal change 后重判。禁止 timer、sleep、timeout-driven re-probe 或轮询。

本地 waiter 的取消、dispose 或进程崩溃不是 durable abandonment，且不得写 lifecycle 终态。恢复从 durable REVISE 与同一 terminal frontier 重建等待，Reviewer continuation/cohort 保持关闭，不重发 challenge、不建新 cohort。

`BloggerRequestAbandoned` 只废弃该次 Blogger request，不证明 record-ready，也不得触发 `FinalityRejected`。恢复须为同一 frontier 重新建立可证明的记录机会；无法从 durable evidence 证明 frontier 或 canonical LWR 时，按 GLORY-056/057 `FinalityUndecided` fail closed，绝不写部分或替代 record。

---

# SURFACE- 条款

## SURFACE-001：语言

新增固定 provider text 用英文；用户输入与 LWR 保持原文。

## SURFACE-002：换行

固定文本使用 LF。

## SURFACE-003：动态数据

用户文本、LWR、requirements、child assignment 与工具错误都是 typed data，不得由字符串反向解析状态。

## SURFACE-004：surface 分类合同与唯一 owner

每个 surface 有唯一 owner，并固定分类合同：哪些句子是 Host-owned instruction（comment plane）、哪些动态材料是 data（value plane）、本地 TOML schema、以及哪些字段/顺序 byte-stable。Manager/Orchestrator/Reviewer system prompt、Lifecycle prompt、Finality prompt、review assignment/challenge 与 tool schema 均各有唯一 owner；测试只读取 owner。

## SURFACE-005：Manager 禁止泄漏

除 sealed opaque LWR 外，Manager 固定 surface 禁止 reviewer/review/verdict/PERFECT/REVISE/barrier/witness/confirmation；动态 LWR 不清洗。

## SURFACE-006：surface proof

静态 gate 覆盖 Manager tools、schema、固定 prompts/errors/results；runtime proof 覆盖 hidden reviewer 不进 durable handles、list、join、JoinGuard。
