# work-record — WHAT（唯一 normative 合同）

条款前缀：`WORK-RECORD-`。证据指针 → `HOW.md` 对应行号。

---

## WORK-RECORD-001：record 属于一段 work，不属于 receiver

**规范**：`A WorkRecord belongs to a piece of work, not to a receiver`（COMPANION-015 ①）。
同一个 canonical record 被不同 receiver 以不同投影消费（parent→child `includeOpening=true`、
child→parent / review / Finality / SyncDelegate `includeOpening=false`），投影选择不改变事实。

**含义/动机**：若 record 是 receiver-relative 的，同一段 work 在父眼里和 review 眼里就是两份
「官方说法」，无法互证。

**边界**：投影**何时**被选择（delegation 协议、review 协议）归 `delegation` / `review-assurance`；
本命题只拥有「record 本身不随 receiver 变」。

**证据**：COMPANION-015 ①/⑥/⑦；`src/Wanxiangshu/Domain/LifecycleWorkRecord.fs`。

---

## WORK-RECORD-002：边界是因果的，不是会话的

**规范**：`Its boundary is causal, not conversational`（COMPANION-015 ②）。一次 invocation
的边界由 XTrace 因果范围定义（`InvocationStartCursor..InvocationEndCursor`、
`MagicTodoLwr.BoundedRange` 的 `StartInclusive`/`EndExclusive`），不是「对话里最近发生了
什么」或 transcript 下标。

**含义/动机**：对话边界会随 reader 视角变化；因果边界对所有人同一。

**边界**：XTrace cursor 机制本身归 `semantic-trace`；本命题拥有「record 的边界取自因果范围」。

**证据**：COMPANION-015 ②；`src/Wanxiangshu/Domain/MagicTodoLwr.fs`。

---

## WORK-RECORD-003：Chronicle 与 Recent work 描述表示，不是「谁看过」

**规范**：`Chronicle and Recent work describe representation, not who has seen the material`
（COMPANION-015 ③）。Chronicle = 已由 Y 沉淀的 frame；Recent work = Y 尚未覆盖的 X-derived
suffix（经 `forWorkRecord` 投影，含最后一条助手文本）。两者是 coverage 表示边界，
与「读者是否新近看过」无关。

**含义/动机**：把 Recent work 解释成「最近新增」会让同一 record 因阅读时间不同而内容不同。

**边界**：frame 怎么产生归 `context-compression`；本命题拥有「三段各自的表示语义」。

**证据**：COMPANION-003/015 ③⑤；`src/Wanxiangshu/Domain/LifecycleWorkRecord.fs`。

---

## WORK-RECORD-004：reuse 保留记忆，但不扩大下一次 record

**规范**：`Reuse preserves memory; it does not enlarge the next WorkRecord`（COMPANION-015 ④）。
Reusable SyncDelegate session 的跨调用记忆可留存，但每个语义 batch **只**物化当前
`InvocationStartCursor..InvocationEndCursor` 内的 record；prior invocation 的 frames/trace
不得进入本次，later invocation 的 Chronicle/terminal 也不得反向污染已完成 range 的重物化。

**含义/动机**：复用是成本优化，不是把多次 invocation 的历史合并进一份 record 的理由。

**边界**：session 复用生命周期归 `managed-session-lifecycle`；本命题拥有 bounded 行为。

**证据**：COMPANION-015 ④/⑩；EXEC-031；`Application/Finality/LifecycleWorkRecordProjection.fs`
（`lifecycleWorkRecordBoundedFromSnapshot`：frames 按 range overlap 过滤、trace 按 range slice）。

---

## WORK-RECORD-005：Recent work ≠ receiver-relative recentness

**规范**：`Recent work` 是 bounded invocation 内 Y 未覆盖的 X-derived safe suffix
（COMPANION-015 ⑤），不是「最近发生的事」。它由 `max(RecordCoverage.IngestedThrough, openingEnd)`
起算、到 record 的 frontier 结束；其中**含**最后一条助手文本（正式陈述）。

**含义/动机**：若 Recent work = 新近性，同一个 record 在不同时刻物化出不同内容，
「一次 invocation 一份 record」即失效。

**边界**：正式陈述 = Recent work 最后一条助手文本，见 WORK-RECORD-011。

**证据**：COMPANION-015 ⑤；`Domain/LifecycleWorkRecord.fs` `materialize`
（`gapStart = max(coverage.IngestedThrough, openingEnd)`）。

---

## WORK-RECORD-006：canonical record 保留 Opening，即使投影省略

**规范**：`Canonical record retains Opening even when projection omits it`（COMPANION-015 ⑥）。
`includeOpening` 只控制渲染段；Opening 必须始终 captured（锚点/gap 起点），
`includeOpening=false` 的投影不复制 Opening 但 record 仍含它。

**含义/动机**：child→parent 回传不复制 Opening，是因为布置者已知任务；若 canonical record
因此真的丢掉 Opening，后续任何需要 Opening 的消费者（例如同 session frozen prefix）就没有了。

**证据**：COMPANION-015 ⑥；`Domain/LifecycleWorkRecord.fs`（`Opening` 字段始终存在）。

---

## WORK-RECORD-007：includeOpening 分向投影

**规范**：`parent→child includeOpening=true；child→parent includeOpening=false（冻结）`
（COMPANION-015 ⑦）。同 Session frozen prefix `true`；process review / Finality /
SyncDelegate caller `false`（REVIEW-016、EXEC-031）。

**含义/动机**：子未见过父任务全文 → 需要 Opening；父布置者已知任务 → 回传 Opening 是噪音
与信息泄漏。

**边界**：frozen prefix 的字节稳定归 `prefix-stability`；本命题只拥有投影方向。

**证据**：COMPANION-015 ⑦；`how/companion.md`（includeOpening 表）。

---

## WORK-RECORD-008：Opening 是 preserved，不是 reconstructed

**规范**：`OpeningMaterial = exact XTrace interval [work start, OpeningBoundary)`
（COMPANION-014）。禁止从 `AssignmentText` / `AuthoritativeRequirements` 拼接重建、禁止
重编号 requirements、禁止任何第二事实源重建。Opening 在 role-defined commitment boundary
关闭后**永不移动**（GLORY-074）。

**含义/动机**：拼接重建会丢掉交托区间内的调查 / 委派回报 / 澄清 / commitment call+result，
并制造第二事实源。

**边界**：Opening 何时关闭归 `obligation-ledger`（GLORY-074 OpeningPolicy）；Opening 的
trace 区间事实归 `semantic-trace`（SEMANTIC-TRACE-010）；本命题拥有「record 里的 Opening
来自 preserved 区间」这一半。

**证据**：COMPANION-014；历史 what/glory GLORY-074。

---

## WORK-RECORD-009：BlindPlan 下首次 planComplete=true commitment 属 constitutive Opening

**规范**：BlindPlan（Manager）的 Opening = InitialCharge + pre-commitment reasoning /
investigation / delegated returns / user clarifications + 任意 accepted `planComplete=false` planning checkpoints
+ T1 commitment call + canonical accepted commitment result。T1 = 第一次 accepted `planComplete=true`；其
`todowrite` call + canonical accepted result 是 **constitutive**
Opening material（COMPANION-014 / TODO-015）：`XTrace.forOpening` 保留，不得当 incidental
tool 滤入 Recent work。

**含义/动机**：交托本身是 Opening 的结尾；滤掉 T1 的 call/result，Opening 就缺了「承诺发生」的证据。

**边界**：T1 / 单调 plan commitment 语义归 `obligation-ledger`；本命题拥有「这些材料
在 record 里属于 Opening 区间」。

**证据**：COMPANION-014 ⑨；TODO-015；`Domain/LifecycleWorkRecord.fs` `withConstitutive`；
`Mission/Manager/Life/OpeningFloor.fs`（WorkRecordStart 含 T1 call）。

---

## WORK-RECORD-010：一次 invocation，一份 record，处处同一

**规范**：`One invocation. One record. Everywhere.`（COMPANION-015 ⑩）。Sync 与 Async
只差等待时机，不差表示：`inspect` / `fork`+`join` 物化同一 WorkRecord 协议，共用同一
materializer（`LifecycleWorkRecord.materialize`），禁止第二套 work-record renderer
（TODO-008 / REVIEW-016）。

**含义/动机**：两套 renderer 迟早产出两种「同一段 work」的官方文本。

**证据**：COMPANION-015 ⑩；TODO-008；`Application/Finality/LifecycleWorkRecordProjection.fs`
（full 与 bounded 都调 `LifecycleWorkRecord.materialize`）。

---

## WORK-RECORD-011：三段形状 + 正式陈述 = Recent work 最后一条助手文本

**规范**：`WorkRecord = Opening? + Chronicle + Recent work`（COMPANION-003）。
**无**独立 `Closing report` 段；Terminal 是私有完成标记，不是 LWR 段。正式陈述 =
Recent work 中最后一条助手文本（散文 claim）。`inspect` 答案就是 bounded record 本身，
不是额外 `answer` 字段（EXEC-031）。若普通 XTrace parts 尚未来得及捕获最终 assistant 文本，
但该 invocation 的 `TerminalOutputCaptured` 已 durable，则 bounded materialization 必须按
ProviderRun + exact EndExclusive 只把**本 invocation** 的 terminal prose 投影进 Recent work；
不得因 completion/transform 时序省略正式陈述，也不得把前后 invocation terminal 串错。

**含义/动机**：独立 Closing = 第二通道 = 同一次 invocation 两个答案。最后一条助手文本
是 participant 自己写下的最新声明，天然是正式陈述。

**边界**：旧标题 `Opening task / Work log / Uncompressed tail / Final output` 与
`Closing report` 已删除、无 alias（COMPANION-003 考古）。

**证据**：COMPANION-003/015；EXEC-031；`Domain/LifecycleWorkRecord.fs` `render`。

---

## WORK-RECORD-012：陈述是 prose claim，不是固定 schema

**规范**：WorkRecord 陈述 = 散文 claim：约束诚实，不约束骨架（ARCH-015）。禁止 universal
fixed report schema（`### Summary` / files/tests/risks/blockers 等强制字段）。machine-semantic
结构只留协议真需处（如 `exit_code`、`verdict`）。

**含义/动机**：固定字段把「提到 files/tests」变成义务，角色为合规而填，诚实反而被格式绑架。

**边界**：角色可自然提及事实；「提及义务」不得写成格式。协议真需处的结构（如
`SyncDelegatePromptRequest { Charge; ProviderPrompt }`）不属于 report DTO。

**证据**：ARCH-015；COMPANION-015 ⑫；历史 why/companion 条款（陈述：散文 vs 固定字段 schema）。

---

## WORK-RECORD-013：LWR 禁 raw tool call/result（Opening 除外）

**规范**：LWR（Chronicle 输入与 Recent work）**硬禁止** raw tool call/result 与
call/result linkage（COMPANION-003；REVIEW-016）。`XTrace.isWorkRecordPart` 将
`SemanticToolCall`/`SemanticToolResult` 排除；`materialize` 的 gap 先经 `forWorkRecord`。
例外：BlindPlan T1 commitment call/result 属 Opening constitutive material（WORK-RECORD-009）。

**含义/动机**：raw tool 流让 record 巨大且含宿主噪声；call id 会随重放变化破坏确定性。
「正式陈述」必须是 participant 的自然语言，不是工具回显。

**边界**：delta 送 Y 时**可含** tool（那是压缩输入）→ `context-compression`（COMPANION-007）。

**证据**：COMPANION-003/007；`Domain/XTrace.fs`（`isWorkRecordPart`/`forWorkRecord`）。

---

## WORK-RECORD-014：RecordCoverage ≠ PrefixCoverage

**规范**：两种 coverage 是不同证明量纲，禁止互推（COMPANION-003 / TODO-008）：

| 量纲 | 位置 | 用途 |
|---|---|---|
| RecordCoverage | XTrace 游标，可落 turn 中间 | LWR gap 起点；review 证据（允许 canonical RawGap） |
| PrefixCoverage | 完整 Host turn 边界 + digest | prefix replacement 证明（禁止 RawGap） |

WorkRecord 可含 canonical RawGap，但 **RawGap 不证明 prefix replaceable**；禁止用
RecordCoverage 推导可替换前缀，禁止用 PrefixCoverage 填 LWR gap。

**含义/动机**：两种量纲对应两个完全不同的消费者（review 要证据，rebase 要完整 turn）。
混用 = 用更窄的证明冒充更宽的。

**边界**：PrefixCoverage 的推进/归零机制归 `context-compression` / `prefix-stability`；
本命题拥有「LWR 侧只消费 RecordCoverage、RawGap 是合法 review 证据」这一半。

**证据**：COMPANION-003；TODO-008；REVIEW-016；`tests/lifecycle-work-record.test.mjs`
`LWR_gap_starts_at_record_coverage_not_prefix_cutoff`。

---

## WORK-RECORD-015：WorkRecordStart 是结构性 floor，不是 Stage

**规范**：`WorkRecordStart = OpeningBoundary = Opening exclusive end`（TODO-001），由
`LifeOpened` / XTrace Opening cursor **纯推导**，不是 Stage fact；不得绑回
`WorkActivated`（该 legacy fact inert）。Blogger effectiveStart =
`max(RecordCoverage, Life.WorkRecordStart)`。Opening 永久 raw：不交给 Y、不随 rebase 消失、
不经 process-review LWR 再复制（includeOpening=false）。

**含义/动机**：删除 planning/Activation 业务 floor 后，Opening 保护必须由结构性 cursor 承担；
否则「压缩能不能碰 Opening」又变回状态机判断。

**边界**：epoch/rebase 语义归 `prefix-stability`；Y 从 floor 起算的压缩行为归
`context-compression`；本命题拥有「floor 的结构性 + Opening 不复制」这一半。

**证据**：TODO-001；COMPANION-014；GLORY-006；`Mission/Manager/Life/OpeningFloor.fs`
（`workRecordStart`/`effectiveOpeningFloor`，从不读 `WorkActivated`）。

---

## WORK-RECORD-016：process/finality/sync 一律 request-range bounded

**规范**：process review、Finality、SyncDelegate 消费的 LWR 一律 **request-range bounded**，
不得取 session head 冒充某次 checkpoint / review / FinalityRequest 的 frontier-bounded LWR
（REVIEW-016 / GLORY-004 / TODO-008）。三个用途同一 renderer、同一协议：
`ManagerCheckpointLWR(k)`、`ProcessReviewLWR(k)`、`FinalityReviewLWR(k)`，全部
`includeOpening=false`。

对于同一 Life 内复用的 dedicated process reviewer，`ManagerCheckpointLWR` 还必须按 reviewer 已知
范围连续分段：第一份从 `next(Life.OpeningCursor)` 这个 checkpoint-time review floor 起；后续份从上一
`TodoReviewConcluded` 实际消费的 assigned Manager review exclusive frontier 起。当前 end 必须在
assignment 前由同一 Host snapshot/XTrace 对 exact tool boundary 再证明并冻结，不能继续使用 before-hook
对尚未稳定 current-message parts 所作的 provisional cursor 估计。当前 checkpoint 若正是 T1，其 acceptance
推进的全局 post-T1 OpeningBoundary 不得 retroactively 改写本次 range start。不得因为 reviewer physical
work-unit 重新 link / continuation 就把起点重置到 Opening；range transport 可复用，但已交付的 manager
history 不重复发送。

**含义/动机**：session head 会混入其它 invocation / 未来工作；bounded range 让 review
可证明「这份 record 恰好覆盖 Rk 的证据区间」。

**边界**：各 frontier 的推导（`Before(Tk)`、上一 concluded manager frontier 等）归 `obligation-ledger` / `review-assurance`；
本命题拥有「materialize 必须 bounded」这一半。

**证据**：REVIEW-016；GLORY-004；TODO-008；`Application/Finality/LifecycleWorkRecordProjection.fs`
（`lifecycleWorkRecordBounded`）。
