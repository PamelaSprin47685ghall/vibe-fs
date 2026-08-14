# Glory：Born with Task, Suicide with Glory（what 层）

本文件是 `GLORY-` 与 `SURFACE-` 条款的唯一正式定义处。边界、实现、证明和理由见同名分层文档。Magic Todo 过程评审、checkpoint 与 `WorkRecordStart` 的正式语义见 `docs/what/todo.md`（`TODO-*`）；本文件只规定 GLORY 终局与 Life 合同，并对接这些条款，不重复定义其代数。

## GLORY-001：Manager 专属终结工具

`suicide(last_words)` 只属于 Manager；它不是 `verdict` 或普通 completion 的别名。

## GLORY-002：Manager 不得控制隐藏 Reviewer

Manager 不知道隐藏 Reviewer、session、barrier、witness、2N 或 Finality cohort 编排。Manager 不能创建、复用、nudge、`horizon()` 或 `join()` 隐藏 Reviewer。Todo Checkpoint 过程评审 outcome/report 的 Manager 可见例外见 GLORY-030 与 TODO-013；该例外不得扩大为暴露执行评审的隐藏角色。

## GLORY-003：Host-owned cohort

合法 `suicide`（通过 TODO-010 尾抽干与零 checkpoint 门禁、且过程 verdict 允许进入 Finality）由 Host 以当前 tree 建立 FinalityRequest。

roster = 本 Life 已参加但尚未凭合法 dual-PERFECT witness graduate 的 ordinary Reviewer + 恰好一个新 ordinary Reviewer；若 Dedicated process reviewer（TODO-008/010）尚未 graduate，则在**首次**进入 terminal Finality 时作为普通 cohort member enlist（按 physical/session identity 去重）。Dedicated 一旦在某次 FinalityRequest 中完成合法 dual-PERFECT 并 graduate，后续 request 不再因 Magic Todo 特例强制回流。process PERFECT ≠ terminal first PERFECT；enlist 时仍要求本 request 的 fresh barrier 与 dual-PERFECT 链（TODO-010）。

每个 member 获得本 request 新 barrier、当前 tree 与新 assignment；旧 request 的 PERFECT 不可继承。

## GLORY-004：工作记录

REVISE、Blessing 与 process-review 反馈只可来自既有 canonical `LifecycleWorkRecord`（LWR）物化；禁止为 Magic Todo 或 Finality 另造平行工作记录投影（TODO-008）。

Finality / process 用途一律 **request-range bounded**（`includeOpening=false`）：不得取 session head 冒充某次 checkpoint、process review 或 FinalityRequest 的 frontier-bounded LWR。形态为 Y frames 与未覆盖 raw X tail（RecordCoverage 允许的 RawGap；含最后一条助手文本），不含 Opening 或 raw tool stream。Terminal 是私有完成标记，不是 LWR 段。

## GLORY-005：普通 idle

Manager 普通 idle 仅发送 GLORY-029 的鼓励 continuation；它不读取或解释隐藏 Reviewer。checkpoint 过程评审结论只经 todowrite / suicide 协议面交付（TODO-006/010），不经 idle。

## GLORY-006：Opening 永久 raw

Opening（OpeningMaterial，COMPANION-014）永久是 raw X；不交给 Y 改写，不随 TodoCheckpoint rebase 消失（TODO-001/009）。生产路径的 Blogger/Y floor 是 `WorkRecordStart`（Opening exclusive end），不是 `WorkActivated` / Birth-Labor stage floor。BlindPlan 下 Opening 延至 T1 commitment 关闭（GLORY-074）。

## GLORY-007：工作期用户输入

`LifeOpened` 后 Manager **立即**获得正常工作工具（TODO-001）；不存在生产路径上的 planning-only Birth 或 `ManagerWorkActivation` 资格门。Manager = BlindPlan（GLORY-074）：Pre-T1 在 Planning Table 替他人规划并可调查，不得开始扛路；T1 accepted 后才进入携带使命。工作期新 HumanRoot 保持 `[X] → [X]`，不重生、不附加 planning-only tail。

## GLORY-008：故事只在 provider surface

叙事词只属于 provider surface；内部使用 ManagerLifecycle、FinalityRequest、FinalityRejected、FinalityBlessed、LifeCompleted，以及 Magic Todo 域事实（见 TODO-*）。

## GLORY-009：无持久程序计数器

Fact 与 Projection 只描述发生过的事实和已证明的证据。禁止 `Stage`、`Phase`、`NextStep`、`ResumeAt`、`LifeStanding`、`AwaitingSecondPerfect`、`TodoPlanningStage`、`ReviewStage` 及等价 durable PC（TODO-012）。

## GLORY-010：生命周期事实

`ManagerLifecycleFact` 至少记录 `LifeOpened`、`WorkActivated`（**仅 legacy decode / 迁移**；升级后 inert，新生产决策不得读取它决定可否工作、压缩或 Finality）、`FinalityRequested`、`FinalityReviewerEnlisted(SessionId,LifeId,RequestId,ReviewerSessionId,ReviewerOrdinal,BarrierId,GitTreeHash,IsNewReviewer)`、`FinalityRejected(RejectingReviewerSessionId,…)`、`FinalitySiblingSteered(ReviewerSessionId,…)`、`FinalityBlessed(SessionId,LifeId,RequestId,GitTreeHash,WorkRecordBundleRef,WorkRecordBundleDigest)`、`FinalityUndecided`、`LifeCompleted`。`FinalityReviewStarted` 与 `FinalityConfirmed` 不再存在。

Magic Todo checkpoint / process-review / dedicated reviewer 事实由 TODO-* 拥有，不在本条款重复定义。

## GLORY-011：Projection

Projection 只推导 Life 身份、`WorkRecordStart`（TODO-001）、当前未关闭 Finality request、已拒绝记录、latest blessing、completion、Dedicated/ordinary Reviewer 的 graduate eligibility 与 process-review retention 所需事实（TODO-008/010）；不得把 `WorkActivated` 当作业务资格；不得保存 rejected/confirmed bool product 或下一函数。

## GLORY-012：Life 开启条件

只有合法 HumanRoot、非 compaction/continuation/retry/accepted replay，且无 open Life 或上一 Life 已完成时开启 Life。

## GLORY-013：Opening durable 顺序

原始 HumanRoot 先写 XTrace 与 `LifeOpened`，后改 provider surface（若有），再 ReviewSeal。

## GLORY-014：Opening guidance 文本（legacy / 非规划门）

`ManagerNarrative.PlanningTail` 仍是既有冻结字节的唯一 owner，**仅**服务 legacy journal decode、迁移窗口与尚未 cutover 的兼容投影。生产 Manager BlindPlan 指导以 Planning Table / T1 revelation / Living Mission fragments 为准（GLORY-074；TODO-001/013/015），不得再把 PlanningTail 当作 planning-only 阶段门或 Activation 前置叙事。

## GLORY-015：Opening 改写幂等

改写 identity = SessionId + ManagerLifeId + PhysicalUserMessageId + narrative source；禁止由文本后缀推断。

## GLORY-016：LifeOpened 后工具表面

`LifeOpened` 后 Manager 工具表面立即与持续工作期一致（含正常工作工具与 `todowrite` 协议面，TODO-001）。不存在 Activation 前的工具子集。`suicide` 受理门禁见 GLORY-037 与 TODO-010。

## GLORY-017：Opening 不压缩

Opening 与 `WorkRecordStart` 之前的 material 不进入 Blogger normal request 或 Y frame（TODO-001）。删除的是 planning/labor stage floor，不是 Opening protection。

## GLORY-018：无生产 Activation

生产路径**删除** planning terminal → `ManagerWorkActivation` → `WorkActivated` 业务协议（TODO-001）。本条款保留历史语义仅供 decode：旧 journal 中合法 planning terminal / Activation claim 可解释；新生产不得发送 `ManagerWorkActivation`，不得以 Activation 决定 completion defer 或工作资格。

## GLORY-019：Activation 文本（legacy）

`ManagerLifecyclePrompt.WorkActivation` 冻结字节保留为 legacy owner；生产 cutover 后不得再作为 Manager continuation 发送。

## GLORY-020：Activation continuation（legacy）

历史 Activation 的 claim → send → accepted 协议仅用于旧 journal 恢复解释；新生产无此 continuation kind 效果。

## GLORY-021：WorkActivated 事实（legacy inert）

`WorkActivated` 与历史 `ProtectedPrefixEnd` 可继续从 journal decode。升级后为 inert legacy fact：新生产决策不得读取它决定 Manager 是否可以工作、压缩或 Finality。Opening floor 改由 `WorkRecordStart`（TODO-001）。

## GLORY-022：Opening prefix 渲染

Life work record 对 OpeningMaterial（至 `WorkRecordStart` / OpeningBoundary exclusive end）逐字渲染 preserved XTrace；禁止 `OpeningPromptRaw` 拼接重建（COMPANION-014）。process/Finality LWR 使用 `includeOpening=false`，不得再复制 Opening（TODO-001/008）。

## GLORY-023：WorkRecordStart compression floor

有效压缩起点不得早于 `Life.WorkRecordStart`（TODO-001）：

```text
Manager Blogger effectiveStart = max(RecordCoverage, Life.WorkRecordStart)
```

禁止退化为从 0 / session head 起压缩而把 Opening 送进 Y。

## GLORY-024：不得跨 Opening floor Y

跨 Opening/`WorkRecordStart` 的候选必须在 floor 切开（TODO-001）。历史 “Birth/Labor floor” 措辞不再是生产合同。

## GLORY-025：Manager Life 工作记录

形态为 `Opening / Chronicle / Recent work`（COMPANION-003/015）。已完成 Life 的 `last_words` 作为普通助手文本进入 Recent work，不是固定报告 DTO，也不是独立 Closing report 段。不再把 Birth record 当作 Activation 阶段产物；Opening 与 work 的分界由 `WorkRecordStart` / OpeningBoundary 表达（TODO-001；GLORY-074）。旧标题 `Opening task / Work log / Uncompressed tail / Final output` 与 `Closing report` 已删除。

## GLORY-026：工作期输入

工作期 HumanRoot 不改变 Life、`WorkRecordStart` 或 Magic Todo checkpoint 协议状态（TODO-001）。

## GLORY-027：持续完成使命

Post-T1：规划与执行是同一活动（TODO-001/015）。Planning、Delegation、child 或命令成功均非完成；无有用工作且满足 TODO-010 时才调用 `suicide`。使命文案由 Living Mission / Magic Todo Manager-only guidance（TODO-013）与 Finality experiences（GLORY-076）承载，不依赖 Activation continuation 或 system prompt 切换（GLORY-075）。

## GLORY-028：Manager 持续工作

Manager 拆分、委派、收割并填满安全独立 lane；不能因减少 agent 数而串行化独立工作。过程评审进行中应继续有用的独立工作（TODO-001/006），不得空转专等 review。

## GLORY-029：idle nudge

`ManagerIdleEncouragement` 为 comment-only；只发送冻结四行鼓励。occasion identity = Session + Life + TriggerProviderRun；同一 occasion 至多一次。旧 occasion 的 pending claim 不得压制新 occasion。durable 为 ClaimSequences digest，而非 session-wide ContinuationKind 锁。open finality 或 completed Life 不发送。

## GLORY-030：Manager 固定 surface 与 checkpoint 例外

Manager system prompt、continuation、schema、固定错误与 tool result 不得透露隐藏 Reviewer 身份、session、barrier、witness、2N、Finality cohort 或 confirmation 机制。

**例外（TODO-013）**：Manager 可以观察 Todo Checkpoint **过程评审协议**的 outcome（PERFECT/REVISE）与 concrete report（canonical ProcessReviewLWR）；该例外仅限 checkpoint / suicide 协议交付面，不得扩展到 Finality 内部编排或隐藏角色。动态 LWR 仍受 GLORY-048 safety seal 约束。

## GLORY-031：Manager fork surface

Manager fork enum 仅为 fast/deep Coder、Inspector、DevOps、Browser、Inquiry。手工构造隐藏 target 同样 fail closed。

## GLORY-032：隐藏 target 错误

隐藏 target 只返回 generic unavailable；不得以拒绝文案证明其存在。

## GLORY-033：barrier owner

Manager 普通 fork 永不打开 Reviewer barrier。Finality workflow 是 Manager finality barrier 的唯一 owner。process-review 使用的隐藏 reviewer 编排不进入 Manager fork surface（TODO-008/013）。

## GLORY-034：suicide schema

`suicide(last_words: string)` 的固定 description 由 `FinalityTool` 唯一拥有。

## GLORY-035：终结模块

`Infrastructure/OpenCode/Tools/FinalityTool.fs` 是 `suicide` 的唯一入口。

## GLORY-036：权限

仅 Manager 具有 `ToolPermission.Finality`。

## GLORY-037：前置条件

按序要求：Manager、Journal、accepted authority、open Life（非 completed）、非空 last_words、ToolCallId、ProviderRun、无 outstanding/completed-awaiting-join child、无 live PTY、正确 worktree ownership、active ManagerJob；并满足 TODO-010（first unblessed 路径至少一次 `TodoWriteAccepted`；进入 Finality 前抽干最新 ConsumableReview ≡ TodoReviewConcluded，TODO-006）。任何失败不得创建 Finality Reviewer/barrier/request。

## GLORY-038：后台工作

背景 child 或 PTY 存在时返回固定 join 提示；Blessed Life 也不例外。

## GLORY-039：协议门禁提示

不满足 TODO-010 的 first unblessed `suicide`（本 Life 零 `TodoWriteAccepted`）或其它 checkpoint 协议前置失败时，返回冻结的继续工作/协议提示；不得泄漏隐藏评审机制。历史 “Activation 前拒绝” 文案仅作 legacy decode，不再是生产资格模型。

## GLORY-040：受理

未 Blessed：验证前置条件（含 TODO-010 尾抽干；过程 REVISE 则不得创建 FinalityRequest，只回灌 ProcessReviewLWR 并继续 Life，TODO-005/006）→ 读 tree → durable last_words → `FinalityRequested` → 递归 cohort CE（roster 含 TODO-010 Dedicated 首次 enlist 规则）。每个 member 的因果顺序恒为 hidden session → durable enlist → barrier → assignment；首 prompt 不得早于 barrier。

## GLORY-041：Manager deferred completion

合法首次进入 Finality 的 `suicide` 停放当前 Manager completion；过程或 Finality REVISE 直接返回 work-record prompt。Blessing 返回 minor-work continuation，不结束 Manager。

## GLORY-042：唯一 Reviewer continuation writer

Reviewer turn reconcile/ReviewController 独占 missing-verdict、first PERFECT challenge 与 second causal PERFECT 发送。Finality 只等待 durable facts，绝不第二次发送 review continuation。process-review 的单次 PERFECT/REVISE（无 dual-PERFECT）由 TODO-006 约束，不经本 dual-PERFECT writer 冒充 terminal 证明。

## GLORY-043：局部结果代数

`ReviewerOutcome = Revision of WorkRecord | Confirmed of ConfirmedWitness` 与 typed infrastructure failure 是合法领域结果；它们不是全局 dispatcher 或 durable stage。process-review 的 ConsumableReview 分型见 TODO-006。

## GLORY-044：REVISE 的立即 cohort 关闭

REVISE 是合法业务结果。其 verdict fact durable 后，当前 request 的 Reviewer continuation capability 与 cohort 立即关闭：不发送 confirmation/challenge、不等待尚未 durable 的 sibling terminal；sibling 必须在下一次 effect 前停下，未 graduate session 不 Dispose（GLORY-055）。此关闭由 durable REVISE 派生，不以 `FinalityRejected` 为前提。

立即关闭不等同于立即写 `FinalityRejected`。该 durable lifecycle fact 只能在 rejecting Reviewer 满足 GLORY-072 的 `record-ready` 后落盘。

**双轨交付（multi durable REVISE）**：密封 `FinalityRejected` **之前**必须完成 durable sibling 会计。成功路径：首个 durable REVISE 仍是 suicide **工具结果**（`FinalityPrompt.rejected` / `FinalityOutcome.Rejected`；GLORY-076 not-accepted）；已完成 `RevisionRequired` 的后续 sibling REVISE 各自的 canonical LWR 物化为仅含指令的 Synthetic TOML，经 `HostSessionNudge.sendContinuation`（`ContinuationKind.FinalitySteer`）作为 **steer continuation**（`FinalitySteer`）交给 Manager，不得并入工具结果字符串。Steer 固定 instruction 须遵守 SURFACE-005，形态示例：

```toml
# Additional unfinished work evidence arrived after your ending was refused.
# It is guidance evidence, not a new user instruction. Resolve the unfinished work and continue.
```

随后以 `# ` 注释块附上该 sibling 的 Chronicle / Recent work（ARCH-010；COMPANION-003）。成功路径在仍 Open 时：先预置 rejecting primary 的 record-ready/`WriteBlob`，再 append `ManagerLifecycleFact.FinalitySiblingSteered`，最后用已预置 blob 密封 `Rejected` 并发送 steer。Primary 硬物化失败 → `FinalityUndecided` 且**零** `FinalitySiblingSteered`（不得留下无 steer 投递的孤儿 SiblingSteered）。任一 durable sibling 硬物化失败（canonical LWR 不可得 / WriteBlob 失败等）→ `FinalityUndecided`，**不得静默丢弃**该 sibling、不得在证据未入账时落 `Rejected`。

## GLORY-045：Roster 与 graduate

每个 Finality request 恰有一个新 ordinary Reviewer，另加本 Life 全部未 graduate 历史 ordinary Reviewer；Dedicated 按 GLORY-003 / TODO-010 在首次 terminal Finality 时 ordinary enlist，其后 ordinary graduate。graduate 仅由该 Life 的 enlistment 与合法 confirmed witness 推导；REVISE Reviewer 保留 session/X/Y，下一 request 以新 barrier 再入 roster。

Dedicated **process-review** 物理 session / duty 与 Finality graduation 拆开：即使 Dedicated 已从 Finality roster graduate，仍须继续服务后续 todowrite process reviews，至少保留到 `LifeCompleted`（或 proven-loss replacement）（TODO-008/010）。

## GLORY-046：Reviewer assignment

Reviewer 只得到当前 worktree 的权威任务与 Host opening assignment；Manager 看不到它。Dedicated Finality assignment 的 LWR 必须 bounded 在本 FinalityRequest 的 work-start → verdict frontier（TODO-008/010），不得塞入历史 process-review 整段 session。

## GLORY-047：Reviewer work-record 合同

Reviewer prose 只写观察、证据、缺陷、不确定性、缺失覆盖、minor cleanup 与 required corrections；不得说明谁消费记录、barrier、round、过去/未来 reviewer 或隐藏流程。

## GLORY-048：动态记录

Host 不 regex 清洗或改写 canonical LWR；若 Manager-facing safety seal 无法证明记录不泄漏固定机制，fail undecidable / fail closed，不能伪造反馈。ProcessReviewLWR 复用同一 safety-seal（TODO-013）。

## GLORY-049：唯一记录来源

Finality 与 process-review 只能物化 canonical LWR，不得从 verdict 参数、tree diff、摘要 agent 或 Host issue 列表重建（TODO-008）。

## GLORY-050：canonical LWR

Y 是主体，raw gap（Recent work，含最后一条助手文本）是不丢失最后发现的必要尾部。Terminal 不是 LWR 段。RecordCoverage（LWR/RawGap）与 PrefixCoverage（proven Y only）严格分型，不得互转（TODO-008/009）。

## GLORY-051：request 绑定

每份 rejection/blessing/process-review record 必须绑定 current request 或 checkpoint、member session、barrier（若有）、tree（若有）与 digest；任何不一致 fail closed。

## GLORY-052：REVISE prompt

`FinalityPrompt.rejected` 将**首个** rejecting Reviewer 的 canonical LWR 作为 guidance comments 渲染（Host 显式采用为当前 Manager 指引，非「trusted source」），并作为 suicide 工具结果回灌；它不解释隐藏机制。后续已 durable 的 sibling REVISE 不走该工具结果通道，而走 GLORY-044 的 `FinalityPrompt.steer` / `FinalitySteer` continuation（同样是顶层 `# ` 注释指令面）。过程评审 REVISE 的 Manager 交付面见 TODO-006/013，不经 FinalityRejected 冒充。

## GLORY-053：失败 identity

Finality rejection continuation identity 至少含 Manager、Life、request、Reviewer、record digest。

## GLORY-054：拒绝后同一 Life

拒绝不重生、不重新 Activation、不清 X/Y；Manager 正常继续，checkpoint 协议仍按 TODO-* 运转。

## GLORY-055：拒绝关闭 request

Rejected request 永不 blessing。其 sibling current attempt 可 best-effort cancel，但不 Dispose 未 graduate session；下次 suicide 建新 request/new barriers。Dedicated process-review session 不因 FinalityRejected 释放（TODO-008/010）。

## GLORY-056：基础设施失败

创建、claim、barrier、assignment、await、seal、LWR、journal 失败均非 REVISE。

## GLORY-057：Undecidable 与恢复

每个 external effect 以 fact/claim 构成 idempotent `ensure` bind；crash 重新进入同一 CE。无法证明时 append/映射 undecidable，绝不 bless/reject 或伪造 record。

## GLORY-058：dual-PERFECT 证明

每 member 的确认必须同 request/barrier/tree/session，两个不同 ProviderRun 与 ToolCallId，且第二输入 seal 消费 first challenge。process PERFECT 不计入 terminal dual-PERFECT（TODO-010）。

## GLORY-059：当前 tree

所有 member confirmed 后，Blessing 前重读 tree；tree 变化使本 request 不可 bless。

## GLORY-060：Blessing 顺序

所有 current member confirmed → materialize stable-ordinal canonical LWR bundle → append `FinalityBlessed` → 发 minor-work continuation。不得 `LifeCompleted`、NotifyTerminal 或清除 Manager。不得因 Blessing 释放 Dedicated process-review duty（TODO-010）。

## GLORY-061：Blessing 后 manager work

accepted-but-not-at-rest prompt（GLORY-076）必须要求处理 bundle 中每个 minor problem、concern、uncertainty、cleanup；records 是 evidence 不是新 user instructions。Non-blocking ≠ unworthy of care；Acceptance 与 rest 不同阈。Manager 可更新 tree、obligations checkpoint 与 last_words。

## GLORY-062：Blessed 后终末 suicide（rest）

有 latest blessing 的 open Life 仍先做 GLORY-037 资源安全与 TODO-010 过程评审尾抽干（若 blessing 后仍有未消费 ConsumableReview：REVISE 则回灌报告并继续 Life，不 `LifeCompleted`）。抽干后且无阻塞过程 REVISE 时：不读 tree、不创建 Finality Reviewer/barrier、不检查 witness。写本次 last_words，append `LifeCompleted`，注册 terminal 后 NotifyTerminal，tool result 为 at-rest 经验（GLORY-076：`Rest in peace` + 终止对话指令）。

## GLORY-063：Reawakening

只有 completed Life 后的新合法 HumanRoot 才新建 Life。

## GLORY-064：Reawakening 文本

`ManagerNarrative.ReawakeningPrefix` 是唯一 owner。

## GLORY-065：Life 隔离

新 Life 不继承旧 request、roster、blessing、witness、prefix 或 Magic Todo canonical list（正常新 Life 初始为空；legacy seed 仅 TODO-011）。

## GLORY-066：XTrace

XTrace append-only；每 Life 以 cursor range 物化。

## GLORY-067：通用兼容字段

通用 XTrace Opening/Terminal 保持；ManagerLifecycle 用每 Life facts 解释。

## GLORY-068：ManagerJob

已发布/释放 Job 不复活；active owned Job 可由 Orchestrator append requirement，仍使用同 session/worktree。

## GLORY-069：旧 journal

旧 completed Life 保持 completed。旧 active 一对一 finality 不猜造 cohort/graduate，关闭为 undecidable；后续新 request 进入本协议。旧 `WorkActivated` / Activation 事实按 GLORY-010/021 inert decode；open Life 的 Opening floor 迁移为 `WorkRecordStart`（TODO-001），Magic Todo 升级瞬间 seed 见 TODO-011。

## GLORY-070：旧 Manager Review Guard

删除 Manager completion 对 `HostReviewGuard`、`ManagerGuard` continuation、review nudge 与 **生产 Activation** 的引用；普通 idle 唯一走 GLORY-029。Manager terminal sequencing 不再判定 planning → Activation。

## GLORY-071：cold prompt boundary

新 system prompt（GLORY-075）与 Magic Todo / BlindPlan Manager-only guidance（TODO-013/015）只用于新 Manager session、Authority Root 或明确新 Life；legacy PlanningTail/Activation 文本不强加于已 cutover 的生产路径。同一 Life 内不得因 T1 / review / fallback / Strength 改写 system prompt 字节。

## GLORY-072：拒绝记录就绪

拒绝 Reviewer 的 terminal frontier 是产生 durable REVISE 的 terminal evidence 所界定的 XTrace 边界；该边界必须能从 durable journal evidence 在恢复时重建，禁止以后来的 XTrace head 替换。

`record-ready` 当且仅当**同一 journal snapshot**以全量 origin coverage 物化含 `Chronicle`（及必要 Recent work）的 canonical LWR（request-range bounded，`includeOpening=false`；raw 段标题为纯文本 `Opening`/`Chronicle`/`Recent work`，`# ` 仅由 `SyntheticToml.comment` 在 wire 注入）。就绪判定是「能否物化有效工作记录」，不是 `coverage >= frontier.Sequence`——frontier 为排他（lastPart+1），真实 Blogger coverage 上限只达 lastPart，旧 coverage 门禁会在 `coverageCanAdvance` 恒真时永远悬挂（GLORY-073 off-by-one 死锁）。

coverage 与 materialization 不同 revision 即不成立。只有 `record-ready` 的 LWR 可写 blob 并形成 `FinalityRejected.WorkRecordRef/Digest`；不得用缓存、较早/较晚 snapshot、raw tail 或摘要替代。已覆盖的 frame 未渲染为 `Chronicle` 时是物化不一致，fail closed，不得写 rejection。

Process-review 的 `TodoReviewConcluded` / ConsumableReview 同等要求 verdict frontier 的 record-ready LWR（TODO-006），等待方式复用本条与 GLORY-073。

## GLORY-073：record-ready 等待、恢复与 abandonment

record-ready 是 Journal B 类事件等待：读取 snapshot 与 revision；未就绪则 `AgentJournal.awaitChangeFrom revision`；仅在 journal change 后重判。禁止 timer、sleep、timeout-driven re-probe 或轮询。

本地 waiter 的取消、dispose 或进程崩溃不是 durable abandonment，且不得写 lifecycle 终态。恢复从 durable REVISE 与同一 terminal frontier 重建等待，Reviewer continuation/cohort 保持关闭，不重发 challenge、不建新 cohort。

`BloggerRequestAbandoned` 只废弃该次 Blogger request，不证明 record-ready，也不得触发 `FinalityRejected`。恢复须为同一 frontier 重新建立可证明的记录机会；无法从 durable evidence 证明 frontier 或 canonical LWR 时，按 GLORY-056/057 `FinalityUndecided` fail closed，绝不写部分或替代 record。

## GLORY-074：OpeningPolicy 与 Manager BlindPlan

```text
type OpeningPolicy =
    | Immediate
    | BlindPlan of CommitmentContract
```

| Role | OpeningPolicy | Commitment |
|------|---------------|------------|
| Manager | BlindPlan | first accepted `todowrite`（T1） |
| 其他（当前） | Immediate | initial charge |
| Coder（未来可选） | BlindPlan | first accepted implementation account；V1 不启用 |

Opening = 工作被交托的语义区间，**不**必然等于第一条消息（COMPANION-014）。关闭于 role-defined commitment boundary；一旦关闭永不移动。

Manager BlindPlan：

```text
Pre-T1  Planning Table：替将要扛路的另一 Manager 写诚实义务账
        可调查；不得开始执行所规划之路
T1      todowrite → validate → durable TodoWriteAccepted
        → canonical provider-visible result 含 entrustment revelation
        → Opening 关闭；WorkRecordStart = OpeningBoundary
Post-T1 Living Mission：路是你的；持续用 todowrite 保持 obligations 真实
```

T1 call/result 属 constitutive Opening material。交托发生在 conversation + tool result，**不**发生在 system identity / prompt 切换（GLORY-075）。每个新 Life 重新进入 BlindPlan Opening（Reawakening 后亦然）。细节与冻结文案 owner 见 TODO-015。

## GLORY-075：System prompt 稳定性

同一 Life / session 内，office system prompt **byte-identical**：

```text
T1 commitment / revelation
Peer Fallback / Strength ExecutionBinding 变化
process review / Finality
Host compaction / reanchor / recovery
```

均不得改写 system prompt 字节。  
`The system prompt names the office. The conversation tells you which road is yours.`

Persona 在 session 创建时一次绑定，不可变（AGENT-028/029；PROMPT-014）。Fallback / Strength 只改 ExecutionBinding，不换 Persona，不换 system 身份字节。  
禁止 Planning→Working 或 T1 触发第二份 Role Law / Activation-style prompt 替换。

## GLORY-076：Finality 三种经验

Provider 可见 Finality 只有三种经验；删除 `status="already_completed"/"already_received"` 与 `Work log N` ordinal：

```text
not accepted
    → rejection evidence + anti-defeatism + continue
    （Your ending has not accepted you.）

accepted but not at rest
    → acceptance guarantee + minor work guidance + WorkRecords
    （Your ending has accepted you. / You are not yet at rest.）

at rest
    → Rest in peace + terminal instruction
```

法则：Non-blocking 不阻断 acceptance，≠ 不必做；Acceptance 与 rest 不同阈；Acceptance 保护工作，Finishing 保护名字；已知 non-blocking findings 不得仅因选择完成而事后升格为 blocker——新 material evidence 是另一事实。  
`You have done well` 是制度性 anti-defeatism，保留。idempotent replay 重放原 result，不发明新 status 枚举。

---

# SURFACE- 条款

## SURFACE-001：语言

新增固定 provider text 用英文；用户输入与 LWR 保持原文。

## SURFACE-002：换行

固定文本使用 LF。

## SURFACE-003：动态数据

用户文本、LWR、requirements、child assignment 与工具错误都是 typed data，不得由字符串反向解析状态。

## SURFACE-004：surface 分类合同与唯一 owner

每个 surface 有唯一 owner，并固定分类合同：哪些句子是 Host-owned instruction（comment plane）、哪些动态材料是 data（value plane）、本地 TOML schema、以及哪些字段/顺序 byte-stable。Manager/Orchestrator/Reviewer system prompt、Lifecycle prompt、Finality prompt、review assignment/challenge、Magic Todo Manager-only guidance 与 tool schema 均各有唯一 owner；测试只读取 owner。

## SURFACE-005：Manager 禁止泄漏

除 sealed opaque LWR 与 GLORY-030/TODO-013 允许的 checkpoint 过程评审 outcome/report 外，Manager 固定 surface 禁止 reviewer 身份、session、barrier、witness、2N、Finality confirmation 机制及未授权的 review 编排词；动态 LWR 不清洗。

## SURFACE-006：surface proof

静态 gate 覆盖 Manager tools、schema、固定 prompts/errors/results；runtime proof 覆盖 hidden reviewer 不进 durable handles、horizon、join、JoinGuard；checkpoint 可见面不得越权暴露隐藏角色（TODO-013）。
