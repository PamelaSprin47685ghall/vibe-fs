# finality — WHAT

条款前缀：`FINALITY-`。
本文件是 finality 包的**唯一 normative 合同**。跨域机制（obligation-ledger、review-assurance、
participant-horizon、work-record、crash-reconciliation）只引用本包命题，不复制合同。
历史与弃权、实现模型见 `HOW.md`；每条命题的证明落点见 `HOW.md`。

词汇：`suicide` = Manager 的终结工具调用（当前字面名，HOW）；`FinalityRequest` = 一次终结评审
请求；`cohort` = 该请求的 Reviewer 集合；`Life` = 一个 Manager 生命；`life completion` =
`LifeCompleted` 不可逆结束。

---

## JS semantic boundary

Finality semantic contracts have one production owner: `src/Wanxiangshu/Mission/Manager/FinalitySurface.fs`.
The registered JS module `Mission/Manager/FinalitySurface.js` accepts plain lifecycle,
review, handle, and ManagerJob history objects/IDs. It returns JSON-shaped objects/arrays;
its folded `World` is an opaque capability that callers only pass back. Tests do not
construct F# facts/unions, inspect Fable representations, or import package-internal
compiled modules. The owner and executable contract evidence are registered in
`HOW.md`.


## FINALITY-001：suicide 只属 Manager，且是终结的专门入口

**规范**：`suicide(last_words)` 只属于 Manager；它不是 `verdict` 或普通 completion 的别名。
仅 Manager 具有 `ToolPermission.Finality`。`src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs` 是
`suicide` 的唯一入口；`suicide` 的固定 description 由 `FinalityTool` 唯一拥有（GLORY-001/034/035/036）。

**含义 / 动机**：终结请求与评审判断（`judge`）是不同因果身份；普通 completion 不拥有终结语义。

**边界**：`suicide` 字面工具名与叙事风格是当前实现（HOW），不是永久合同。

**证据** → HOW.md 行 F-1。

## FINALITY-002：终结资格建立在 obligations + current tree + qualified review evidence 上

**规范**：participant 自宣完成 ≠ 世界允许不可逆结束。终结资格由三部分共同构成：
(1) 当前义务（obligation-ledger：checkpoint 协议、零 checkpoint fail closed、drain 未消费
ConsumableReview）；(2) 当前被审对象（review-assurance：request/barrier/tree 绑定的合格
witness）；(3) 终结经验分型（本包：rejection / blessed / rest 各自独立）。

**含义 / 动机**：这是本包的 WHY 总纲（GLORY-037 前置条件 + TODO-010 + GLORY-003/058 组合）。

**边界**：三部分各自的内部机制分别归 obligation-ledger / review-assurance；本包只拥有组合契约与
participant 经验。

**证据** → HOW.md 行 F-2。

## FINALITY-003：受理前置条件按序验证；任一失败不建任何评审对象

**规范**：按序要求：Manager、Journal、accepted authority、open Life（非 completed）、非空
last_words、ToolCallId、ProviderRun、无 outstanding/completed-awaiting-join child、无 live PTY、
正确 worktree ownership、active ManagerJob；并满足 FINALITY-004/005/006（TODO-010）。
任何失败不得创建 Finality Reviewer/barrier/request（GLORY-037）。

`EndingDisposition` 是纯领域分类，不是 program counter（SW-017①）。Tool adapter 不 match disposition case
并分发不同业务效果；Finality-owned `handleEnding` 函数接收分类 + 执行能力，
内部 dispatch 并返回边界结果 `FinalityEndingOutcome`（`Refused path | Result toolResult`）。
Tool adapter 只调用 `handleEnding` 并渲染边界结果。

**含义 / 动机**：终结是资源安全 + 协议纪律的交点；半建 cohort 会留下不可恢复的悬挂评审。
ending dispatch 由 Finality 域拥有，Tool 层不暴露 child action opcode。

**边界**：资源检查的 Host 侧实现（child/PTY/worktree）属 host-boundary；本包拥有「失败即零创建」。
`EndingDisposition` 作为纯分类保留；改变的是 dispatch 所有权从 Tool 层移到 Finality 层。

**证据** → HOW.md 行 F-3。

## FINALITY-004：无 plan commitment 时不得进入 Finality

**规范**：`first unblessed suicide ∧ 本 Life 尚无 accepted planComplete=true → fail closed / ContinuePlanning`。
Pre-T1 的 `planComplete=false` planning checkpoints 不构成终结资格；T1 是第一次 accepted true。

**含义 / 动机**：证明 Manager 已明确完成计划并承担该 road，而不是机械要求 obligations 清空
（与 FINALITY-007 区分）。

**边界**：判定输入 `isPlanCommitted` 来自 obligation-ledger 的 durable projection；本包拥有门禁本身。

**证据** → HOW.md 行 F-4。

## FINALITY-005：suicide 是唯一 tail drain

**规范**：`suicide` 是最后一个尚未被下一次 todowrite 消费的 process review 的**唯一** tail
drain。禁止再调一次 todowrite flush——那会创造 R(k+1) 无限后移。Blessed 之后的再次 suicide
**同样先** drain latest process review（TODO-010/GLORY-062）。

**含义 / 动机**：drain 义务不能转移给 todowrite；否则悬挂 Rk 永远无法被消费。

**边界**：「Rk 产生后必须被消费」的账本侧义务见 `obligation-ledger` OBLIGATION-LEDGER-022；
本包拥有「消费动作发生在 suicide」。

**证据** → HOW.md 行 F-5。

## FINALITY-006：drain 结果分型

**规范**：有 checkpoint 时 await latest ConsumableReview ≡ `TodoReviewConcluded`（TODO-006）：

```text
REVISE  → 返回 canonical ProcessReviewLWR 报告；不建 FinalityRequest；Life 继续
          CurrentObligations 仍是 latest Accepted account，由 Manager 后续 checkpoint 修正
PERFECT → 进入既有 Finality 前置
```

**含义 / 动机**：过程 REVISE 是正常业务结果，不是终结拒绝；它只证明「当前账/工作尚需修正」，
Manager 用后续 todowrite 修正（TODO-005/010）。

**边界**：报告物化与 record-ready 属 review-assurance；账本不被评审涂改属 obligation-ledger。

**证据** → HOW.md 行 F-6。

## FINALITY-007：无机械 terminal-todo completeness gate

**规范**：「已有 plan commitment」≠ 机械要求 obligations 清空。未完成项真实性交给过程
PERFECT/REVISE，不另造机械 terminal-todo completeness gate（TODO-010）。

**含义 / 动机**：机械全 completed 门与用户过程评审需求无关，且与 REVISE 续命冲突
（why「Finality 未完成项」裁决）。

**边界**：obligations 是否为真由 obligation-ledger 的账本 + process review 判定。

**证据** → HOW.md 行 F-7。

## FINALITY-008：受理顺序必须 durable

**规范**：合法受理（未 Blessed）：验证前置条件（含 FINALITY-004/005/006）→ 读 tree →
durable last_words → `FinalityRequested` → 递归 cohort CE（roster 见 FINALITY-009）。
每个 member 的因果顺序恒为：hidden session → durable enlist → barrier → assignment；
首 prompt 不得早于 barrier。Finality 只等待 durable facts，绝不第二次发送 review continuation
（GLORY-040/042）。

**含义 / 动机**：受理 = durable 事实序列；任意中间失败都不允许留下半建评审对象
（GLORY-057 恢复前提）。

**边界**：barrier 的 witness/seal 机制属 review-assurance；`FinalityRequested` 事实形态见 HOW。

**证据** → HOW.md 行 F-8。

## FINALITY-009：roster 与 Dedicated 毕业

**规范**：每个 FinalityRequest 恰有一个新 ordinary Reviewer，另加本 Life 全部未 graduate 历史
ordinary Reviewer；Dedicated 在**首次**进入 terminal Finality 时作为普通 cohort member enlist
（按 physical/session identity 去重），其后完全遵循 ordinary graduate 规则——不发明
「每轮强制回流 / 永不 graduate」特例。REVISE Reviewer 保留 session/X/Y，下一 request 以新
barrier 再入 roster（GLORY-003/045；TODO-010）。

**含义 / 动机**：终末 2N 需要「恰好一个新 Reviewer + 全部未毕业历史 Reviewer」；Dedicated 不应
破坏既有毕业语义，也不应永远缺席终局（why「Dedicated 普通 graduate 却保留 process duty」裁决）。

**边界**：process PERFECT ≠ terminal first PERFECT（FINALITY-010）；roster 推导的纯函数在
`Composition/Bridges/FinalityReview/FinalityReviewCohort.fs`。

**证据** → HOW.md 行 F-9。

## FINALITY-010：graduate 只由 enlistment + 合法 confirmed witness 推导

**规范**：graduate 仅由该 Life 的 enlistment 与合法 confirmed witness 推导。process PERFECT
**不计入** terminal dual-PERFECT；enlist 后仍要求本 request 的 fresh barrier 与 fresh
dual-PERFECT 链（新 request/barrier/tree/authority root；可复用同一 physical session/context，
不可复用过程因果证明）（TODO-010/REVIEW-020/GLORY-058）。

**含义 / 动机**：终结证明只描述当前 tree 和当前请求；历史 process turns 不能冒充终局证据。

**边界**：dual-PERFECT witness 的因果代数（challenge/seal/same-barrier/different-run）属
review-assurance；本包拥有「谁有资格毕业、process 证明不计入」。

**证据** → HOW.md 行 F-10。

## FINALITY-011：REVISE 立即关闭 cohort；FinalityRejected 另行 record-ready

**规范**：REVISE 是合法业务结果。其 verdict fact durable 后，当前 request 的 Reviewer
continuation capability 与 cohort 立即关闭：不发送 confirmation/challenge、不等待尚未 durable
的 sibling terminal；未完成的 PERFECT 确认链同时作废，关闭后不得补发 challenge（REVIEW-002）。
此关闭由 durable REVISE 派生，不以 `FinalityRejected` 为前提——后者只能在 rejecting Reviewer
满足 record-ready 后落盘（GLORY-044/072；record-ready → review-assurance）。

**含义 / 动机**：继续发 challenge 或等待 sibling 只会制造与该判断竞争的事实；`FinalityRejected`
不是 verdict 的别名，它永久引用 canonical LWR（why「REVISE 立即关闭 Reviewer」裁决）。

**边界**：record-ready 判定 / 同 snapshot 物化属 review-assurance；本包拥有关闭语义与
rejection 经验（FINALITY-013/014）。

**证据** → HOW.md 行 F-11。

## FINALITY-012：双轨交付 sibling steer

**规范**：密封 `FinalityRejected` **之前**必须完成 durable sibling 会计。成功路径：首个 durable
REVISE 仍是 suicide **工具结果**（rejection 经验，FINALITY-013）；已完成 RevisionRequired 的
后续 sibling REVISE 各自物化为仅含指令的 steer continuation（instruction-only `# ` Synthetic
TOML）交给 Manager，**不得**并入工具结果字符串、**不得**静默丢弃。Primary 硬物化失败 →
`FinalityUndecided` 且零 `FinalitySiblingSteered`；任一 durable sibling 硬物化失败 →
`FinalityUndecided`，不得在证据未入账时落 `Rejected`（GLORY-044）。

**含义 / 动机**：多个 Reviewer 的 REVISE 都是真证据；拒绝后到达的 sibling 证据必须交付，
否则 Manager 会基于不完整拒绝继续（why「双轨交付」裁决）。

**边界**：sibling 记录的物化与 fail-closed 属 review-assurance；steer 的指令文案见 HOW
（SURFACE-005 约束）。

**证据** → HOW.md 行 F-12。

## FINALITY-013：三种经验分型；Acceptance ≠ rest

**规范**：Provider 可见 Finality 只有三种经验：

```text
not accepted            → rejection evidence + anti-defeatism + continue
                          （Your ending has not accepted you.）
accepted but not at rest → acceptance guarantee + minor work guidance + WorkRecords
                          （Your ending has accepted you. / You are not yet at rest.）
at rest                 → Rest in peace + terminal instruction
```

法则：Non-blocking 不阻断 acceptance，≠ 不必做；Acceptance 与 rest 不同阈；Acceptance 保护工作，
Finishing 保护名字；已知 non-blocking findings 不得仅因选择完成而事后升格为 blocker——新 material
evidence 是另一事实（GLORY-076）。idempotent replay 重放原 result，不发明新 status 枚举。

**含义 / 动机**：把三种经验压成一个「结束」状态会把未接受当安息，或把已接受当禁止收尾
（why「Finality：单一结束文案 vs 分型」裁决）。

**边界**：文案逐字字节属 `provider-language` / SURFACE-004；本包拥有经验分型的语义。

**证据** → HOW.md 行 F-13。

## FINALITY-014：拒绝后同一 Life 继续；Rejected request 永不 blessing

**规范**：拒绝不重生、不重新 Activation、不清 X/Y；Manager 正常继续，checkpoint 协议仍按
obligation-ledger 运转。Rejected request 永不 blessing；其 sibling current attempt 可
best-effort cancel，但不 Dispose 未 graduate session；下次 suicide 建新 request / new barriers
（GLORY-054/055）。

**含义 / 动机**：rejection 是「继续工作」的信号，不是 Life 终点；被拒请求不可复活为 blessing。

**边界**：继续工作的 checkpoint 语义属 obligation-ledger。

**证据** → HOW.md 行 F-14。

## FINALITY-015：未 graduate session 不 Dispose；Dedicated process duty 保留

**规范**：Finality REVISE / Blessing 后 process-review session **不** Dispose；ordinary cohort
仍走既有 carryover/release。Dedicated 即使已从 Finality roster graduate，仍须继续服务后续
todowrite process reviews，至少保留到 `LifeCompleted`（或 proven-loss replacement）
（GLORY-055/TODO-008/010）。

**含义 / 动机**：Blessing 即释放 process session 会让后续 checkpoint / 二次 suicide 无人可审
（why「Dedicated：首次 enlist + ordinary graduate + process 留到 LifeCompleted」裁决）。

**边界**：dedicated session 的物理生命周期归 `managed-session-lifecycle`；process duty 义务见
obligation-ledger OBLIGATION-LEDGER-020。

**证据** → HOW.md 行 F-15。

## FINALITY-016：Blessed 不结束 Life；minor-work 继续

**规范**：所有 current member confirmed（review-assurance 消费）且 Blessing 前重读 tree 一致后
（tree 新鲜性 → review-assurance）：materialize stable-ordinal canonical LWR bundle → append
`FinalityBlessed` → 发 minor-work continuation。**不得** `LifeCompleted`、NotifyTerminal 或清除
Manager。Blessing 后 Manager 收到全部 canonical work records 与 minor-work prompt；必须处理
bundle 中每个 minor problem / concern / uncertainty / cleanup——records 是 evidence
不是新 user instructions（GLORY-059/060/061）。

**含义 / 动机**：第一次全确认只证明「已被接受」，不证明「可以安息」；Acceptance 保护工作，
Finishing 保护名字（GLORY-076 法则）。

**边界**：tree 重读与 witness 有效性属 review-assurance；LWR bundle 物化属 work-record。

**证据** → HOW.md 行 F-16。

## FINALITY-017：rest = 第二次 suicide（last_words 即最终答案）

**规范**：有 latest blessing 的 open Life 仍先做 FINALITY-003 资源安全与 FINALITY-005 过程评审
尾抽干（blessing 后仍有未消费 ConsumableReview：REVISE → 回灌并继续 Life，不 `LifeCompleted`）。
抽干后且无阻塞过程 REVISE 时：**不**读 tree、**不**创建 Finality Reviewer/barrier、**不**检查
witness。写本次 last_words → append `LifeCompleted` → 注册 terminal 后 NotifyTerminal →
tool result 为 at-rest 经验（`Rest in peace` + 终止对话指令）。**成功输出逐字等于 last_words，
Host 零附加文本**（GLORY-062）。

**含义 / 动机**：成功后再唤醒 Manager 写总结会稀释叙事、引入第二轮修改风险；`last_words` 是
Manager 深思后的最终答案（why「成功输出逐字等于 last_words」裁决）。

**边界**：last_words 进入 LWR Recent work 的表示属 work-record。

**证据** → HOW.md 行 F-17。

## FINALITY-018：Manager deferred completion

**规范**：合法首次进入 Finality 的 `suicide` 停放当前 Manager completion；过程或 Finality
REVISE 直接返回 work-record prompt；Blessing 返回 minor-work continuation，不结束 Manager
（GLORY-041）。

**含义 / 动机**：终结受理不是完成；Manager 在评审期间被停放，但 Life 保持 open。

**边界**：completion 停放的 turn 机制属 interaction-authority / host-boundary 交叉。

**证据** → HOW.md 行 F-18。

## FINALITY-019：Manager 面无 Review Guard；idle 只发鼓励

**规范**：删除 Manager completion 对 `HostReviewGuard`、`ManagerGuard` continuation、review
nudge 的引用；ManagerWorkflow 只判 join / finality / planning / handedOff（REVIEW-007/GLORY-070）。
Manager 普通 idle 仅发送 FINALITY 之外的四行鼓励 continuation；它不读取或解释隐藏 Reviewer。automatic encouragement **没有跨 terminal 的业务次数上限**：只要当前 Manager Life 仍活着、Finality 未接管、且出现一个新的 completed Manager terminal，就必须再次获得一份 encouragement。幂等边界只覆盖同一个 `ProviderRunIdentity` / idle occasion 的重放；同一 terminal 不重复发送，但新的 ProviderRun 不得因为 Life/plan-commitment condition 上已有历史 claim 就静默。checkpoint 过程评审结论只经 todowrite / suicide 协议面交付，不经 idle。open finality 或 completed Life 不发送 idle（GLORY-005/029/070）。

**含义 / 动机**：Review Guard 只保留 Reviewer 面；Manager 面的 nudge/guard 会让评审重新变成
显式 checklist（GLORY-070 语义）。

**边界**：idle continuation 的 durable occasion identity 属 interaction-authority；本包只定义 Manager 的 plan-commitment 条件与何时允许鼓励。这里没有 Life 级/condition 级次数预算。

**证据** → HOW.md 行 F-19。

## FINALITY-020：隐藏机制不变成 Manager checklist

**规范**：Manager 不知道隐藏 Reviewer、session、barrier、witness、2N 或 Finality cohort 编排；
不能创建、复用、nudge、`horizon()` 或 `join()` 隐藏 Reviewer。Manager 普通 fork 永不打开
Reviewer barrier；Finality workflow 是 Manager finality barrier 的唯一 owner。Reviewer 只得到
当前 worktree 的权威任务与 Host opening assignment；Manager 看不到它。隐藏终端机制只暴露
**会改变下一行动的 consequence/report**（GLORY-002/030/031/032/033/046；SURFACE-005）。
Todo Checkpoint 过程评审 outcome/report 的 Manager 可见窄例外见 obligation-ledger
OBLIGATION-LEDGER-023 与 participant-horizon；该例外不得扩大为暴露执行评审的隐藏角色。

**含义 / 动机**：把终局质量门从「可勾选的步骤」变成「不可见的命运」（why「Manager 不能知道
隐藏 review 机制」裁决）。

**边界**：信息准入边界（哪些词/哪些句可见）的 admission 归 participant-horizon；本包只拥有
「hidden terminal mechanism 不变成 Manager checklist、只暴露 consequence」。

**证据** → HOW.md 行 F-20。

## FINALITY-021：状态只来自 typed facts

**规范**：内部使用 `ManagerLifecycle`、`FinalityRequested`、`FinalityRejected`、`FinalityBlessed`、
`LifeCompleted` 与 Magic Todo 域事实（见 obligation-ledger）；叙事词只属于 provider surface。
Projection 只推导 Life 身份、`WorkRecordStart`（obligation-ledger T1）、当前未关闭 Finality
request、已拒绝记录、latest blessing、completion、graduate eligibility 与 process-review
retention 所需事实；**不得**保存 rejected/confirmed bool product 或下一函数。禁止故事文本反向
解析状态（GLORY-008/009/011）。

**含义 / 动机**：状态只能来自 typed facts + projection；文本推导（搜索 suicide/glory 字样）
在 crash 后无法重建（why「其他被拒方向」裁决）。

**边界**：无持久程序计数器的一般法则属 structured-workflow。

**证据** → HOW.md 行 F-21。

## FINALITY-022：Life 开启条件与隔离

**规范**：HumanRoot Life 的 opening admission 只认一条 typed evidence：当前 immutable
`AuthorityExecutionProfile` 必须是 `HumanRoot + Manager`，且待处理 physical message id **逐值等于**
该 profile 的 `AuthorityRootUserMessageId`。仅“这个 session 当前有 HumanRoot authority”、消息 role
是 user、文本像 opening、XTrace provenance、suicide 文本、title/compaction 排除表，都不得代替这条
identity equality。无 open Life 时，合法 root 可 Birth；上一 HumanRoot Life 已 completed 后，
`LifeCompleted` 的 canonical fold 原子释放该 HumanRoot active run（INTERACTION-AUTHORITY-018），
下一条真实 external + explicit-agent HumanRoot 才可 Reawakening。

AgentOwnerRoot Manager 不走 HumanRoot opening。它只允许在**该 session 从未有任何 Life 历史**时，
于第一次合法 ending 从 canonical Current XTrace 物化一次 migration Life；一旦 `CompletedLives` 非空，
`CurrentLife=None` 表示该 Life 已终结，绝不能把同一历史 XTrace 再物化成第二个 migration Life。
AgentOwnerRoot 的 authority 本身不因 `LifeCompleted` 关闭，因为 owner-directed publish conflict
resumption 等后续工作仍可合法复用该 session。

新 Life **不继承**旧 request、roster、blessing、witness、prefix 或 Magic Todo canonical list
（正常新 Life 初始为空；legacy seed 仅 obligation-ledger OBLIGATION-LEDGER-019 窗口）
（GLORY-012/063/064/065）。

**含义 / 动机**：Life 是终结语义的边界；opening identity 由 authority owner 提供而不是
NarrativeTransform 自行推断；旧 Life 的 blessing/witness 不能污染新 Life，AgentOwner 的一次性
migration 也不能在 completion 后自激重生。

**边界**：HumanRoot authority closure → `interaction-authority`；XTrace 不清空与 cursor range 物化属
semantic-trace；Magic Todo canonical 空账属 obligation-ledger。

**证据** → HOW.md 行 F-22。

## FINALITY-023：Opening durable 顺序与改写幂等

**规范**：原始 HumanRoot 先写 XTrace 与 `LifeOpened`，后改 provider surface（若有）。改写
identity = SessionId + ManagerLifeId + PhysicalUserMessageId + narrative source；禁止由文本后缀
推断。重复 transform 不重复注入（GLORY-013/015）。

**含义 / 动机**：durable Opening 永远是原始 `[X]`；provider-facing 改写不得先于落盘。

**边界**：OpeningMaterial 的表示与压缩 floor 属 work-record；seal 机制属 review-assurance /
prefix-stability 交叉。

**证据** → HOW.md 行 F-23。

## FINALITY-024：工作期输入不改写；持续完成使命

**规范**：工作期 HumanRoot 不改变 Life、`WorkRecordStart` 或 Magic Todo checkpoint 协议状态。
Post-T1：规划与执行是同一活动；Planning、Delegation、child 或命令成功均非完成；无有用工作且
满足 TODO-010 时才调用 `suicide`。Manager 拆分、委派、收割并填满安全独立 lane；过程评审进行中
应继续有用的独立工作，不得空转专等 review（GLORY-007/026/027/028）。

**含义 / 动机**：工作期输入不产生新 Life / 不重开 Opening；「完成」只能由终结协议判定。

**边界**：Pre-T1 不扛路的规划边界属 obligation-ledger（OBLIGATION-LEDGER-017）。

**证据** → HOW.md 行 F-24。

## FINALITY-025：旧 journal 语义

**规范**：旧 completed Life 保持 completed。旧 active 一对一 finality 不猜造
cohort/graduate，关闭为 undecidable；后续新 request 进入本协议。旧 `WorkActivated` /
Activation 事实仅 inert decode；open Life 的 Opening floor 迁移为 `WorkRecordStart`
（obligation-ledger OBLIGATION-LEDGER-017）；Magic Todo 升级瞬间 seed 见
OBLIGATION-LEDGER-019（GLORY-069）。

**含义 / 动机**：迁移不破坏历史事实；历史证据不足的一对一 finality 诚实关闭为 undecidable。

**边界**：migration 的 decode 机制属 durable-events。

**证据** → HOW.md 行 F-25。

## FINALITY-026：undecidable 是合法结局经验；绝不伪造

**规范**：无法证明时 append / 映射 `FinalityUndecided`，**绝不** bless / reject 或伪造 record。
基础设施失败永远不是 PERFECT / REVISE（GLORY-056/057）；`ReviewerOutcome = Revision of
WorkRecord | Confirmed of ConfirmedWitness` 与 typed infrastructure failure 是合法领域结果，
不是全局 dispatcher 或 durable stage（GLORY-043）。undecided 对 Manager 呈现为
「Your ending could not be decided. You still have time. Continue, and seek your end again.」。

**含义 / 动机**：证据不足时宁可不结束，不猜 blessing/rejection；把系统故障伪装成工作缺陷会让
Manager 去「修复」系统（why「REVISE 是正常业务结果」「失败反馈是完整 LWR」裁决）。

**边界**：record-ready / 恢复机制属 review-assurance + crash-reconciliation；本包拥有
outcome 分型与 Life 继续语义。

**证据** → HOW.md 行 F-26。

## FINALITY-027：后台资源义务

**规范**：背景 child 或 PTY 存在时返回固定 join 提示；Blessed Life 也不例外。有 join 义务且
仍有 outstanding 后台时，本 turn 只发 JoinGuard Continuation；finality 处理停放，Manager 不做
idle 鼓励（GLORY-038/EXEC-016/GLORY-029）。

**含义 / 动机**：终结前必须确保无在途资源；join 是终结前置（FINALITY-003）的运行时体现。

**边界**：JoinGuard 的 turn 机制属 interaction-authority。

**证据** → HOW.md 行 F-27。

## FINALITY-028：ManagerJob 不复活

**规范**：已发布/释放 Job 不复活；active owned Job 可由 Orchestrator append requirement，
仍使用同 session/worktree（GLORY-068）。

**含义 / 动机**：终结后 Job 生命周期不可逆；但 active Job 可被 Orchestrator 追加需求续做。

**边界**：Orchestrator 的 Job 语义属 dispatch-protocol 交叉。

**证据** → HOW.md 行 F-28。
