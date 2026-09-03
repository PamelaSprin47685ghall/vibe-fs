# finality — WHAT

## FINALITY-001: suicide 专属 Manager 且为终结专用入口

`suicide(last_words)` 工具仅赋予 Manager 角色，具有专门的 `ToolPermission.Finality` 权限，不是普通 completion 或判定工具的别名。`suicide` 是终结请求的唯一合法入口，其描述文本由终结工具独立拥有。

## FINALITY-002: 终结资格建立在 obligations、current tree 与合格 review evidence 上

参与者单方宣告完成不构成不可逆结束的许可。终结资格必须同时满足三重条件：
1. **当前义务**：遵守 checkpoint 协议，具备 plan commitment 且零 checkpoint 严格 fail closed。
2. **当前被审对象**：具备绑定当前请求、barrier 与 Git tree 的合格 witness。
3. **经验分型**：严格按 rejection、blessed 与 rest 分流处理。

## FINALITY-003: 受理前置条件按序验证且失败零创建

终结受理严格按序验证前置条件：Manager 身份、Journal 在场、已接受权威、处于 open Life、非空 last_words、有效 ToolCallId 与 ProviderRun、无在途或等待合并的子任务、无存活 PTY、正确的 worktree 归属、活跃 ManagerJob，并满足 T1 承诺要求。任一前置检查未通过，绝对不得创建 Reviewer 会话、barrier 或终结请求对象。`EndingDisposition` 是纯领域分类，不是 program counter。Tool adapter 不 match disposition case 并分发不同业务效果，而是统一调用 `handleEnding` 并接收 `FinalityEndingOutcome` 边界结果进行渲染。

## FINALITY-004: 无 plan commitment 时不得进入 Finality

未获得 blessing 的首次 `suicide` 在当前 Life 尚未形成 accepted `planComplete=true` 事实时，必须 fail closed 并分派为 `ContinuePlanning`。Pre-T1 阶段的 planning checkpoint 不赋予终结资格。

## FINALITY-005: suicide 驱动 Finality Review

`suicide` 是进入终局评审（Finality Review）的唯一途径。获得 blessing 后的再次 `suicide` 直接触发生命周期完结。

## FINALITY-006: 终审结果分型（REVISE 回灌与 BLESSING 授予）

终审裁决严格分为两种业务结果：
- 若结论为 **REVISE**：回灌规范的 `ProcessReviewLWR` 报告，当前 Life 继续运行，由 Manager 修正代码与账目。
- 若全部确认：授予 Blessing 并准入完成。

## FINALITY-007: 无机械 terminal-todo completeness gate

存在 plan commitment 不等同于机械要求 obligations 列表必须为空。未完成项的真实性与合规性完全交由终局评审的 PERFECT 或 REVISE 裁决，系统不设立死板的任务清空拦截门。

## FINALITY-008: 受理顺序必须 durable

合法受理（未获 Blessing 状态下）严格按序执行：校验全部前置条件 → 读取当前代码树 → 持久化 last_words → 写入 `FinalityRequested` 事实 → 递归驱动 cohort 工作流。每个审查成员的因果顺序恒为：创建隐藏会话 → 持久化 enlist 事实 → 打开 barrier → 分配审查任务。首个提示词发送不得早于 barrier 建立。

## FINALITY-009: roster 与 Reviewer 毕业

每次 FinalityRequest 包含恰好一名新 Reviewer，以及当前 Life 中尚未毕业的历史 Reviewer。裁决为 REVISE 的 Reviewer 保留会话与历史记录，在后续请求中以新 barrier 重新入组。

## FINALITY-010: graduate 只由 enlistment 与合法 confirmed witness 推导

审查员的毕业资格仅由当前 Life 的登记事实与合法的 confirmed witness 严格推导。登记进入终审后，必须针对当前请求、当前代码树与新 barrier 重新完成完整的双重确认链。

## FINALITY-011: REVISE 立即关闭 cohort 且 FinalityRejected 另行 record-ready

REVISE 是合法业务结果。当 REVISE 事实持久化后，当前请求的审查能力与整个 cohort 立即关闭：停止发送确认提示与 challenge，废除未完成的确认链。该关闭由 REVISE 事实直接派生，不等待 `FinalityRejected`；`FinalityRejected` 仅在拒绝审查员的记录达成 record-ready 后独立落盘。Reviewer terminal 的物理收束不得反过来饿死这条 record-ready 路径：尚无 canonical Chronicle 时才允许等待已经 durable-open、能够建立首次记录的 Blogger producer；已有 Chronicle 时不得等待 terminal transform 自己后续生成的 producer。随后由 runtime-owned background interrupt 退休 Reviewer 当前物理 attempt。Finality 不拥有私有 Abort 解释规则，而与 Change review 共用 barrier-scoped `ReviewerTerminalAwait`；只有当前 `(ReviewerSessionId, ReviewBarrierId)` occasion 已具有 durable `ReviewAttemptClosed` 时，该主动 Abort 才是 clean terminal，无 closure、旧 barrier closure 或真实 failure 都是基础设施失败。

## FINALITY-012: 双轨交付 sibling steer

在密封 `FinalityRejected` 之前，必须完成并行的审查证据记账。首个 durable REVISE 作为 `suicide` 的工具返回结果交付；后续到达的其他 REVISE 则分别物化为指令型 steer continuation（Synthetic TOML）交付给 Manager，严禁并入工具返回字符串或静默丢弃。物化失败必须 fail closed。

## FINALITY-013: 三种经验分型与 Acceptance 不等于 rest

模型可见的 Finality 严格区分三种经验：
1. **Not Accepted（拒绝）**：交付拒绝证据并指导继续改进（`Your ending has not accepted you.`）。
2. **Accepted But Not At Rest（已接受未安息）**：交付接受保证、工作记录与收尾指导（`Your ending has accepted you. / You are not yet at rest.`）。
3. **At Rest（安息）**：交付安息确认与最终终止指令（`Rest in peace`）。
Non-blocking 观察不阻断 acceptance，已接受的事实受保护；幂等重放原样返回原始结果，不引入虚构枚举。

## FINALITY-014: 拒绝后同一 Life 继续且 Rejected request 永不 blessing

遭遇拒绝后不触发重生或重置，Manager 在同一 Life 内继续执行，账本协议维持运转。被拒绝的请求永久不得转为 blessing；后续再次调用 `suicide` 将开启全新的请求与 barrier。

## FINALITY-015: 未 graduate session 不 Dispose 且 Dedicated process duty 保留

终审 REVISE 或 Blessing 发生后，过程评审会话严禁 Dispose。Dedicated Reviewer 即使已从 Finality roster 毕业，仍必须继续承担后续过程评审职责，至少保留至 `LifeCompleted`。

## FINALITY-016: Blessed 不结束 Life 且 minor-work 继续

当所有当前成员均达成 confirmed 且代码树重读一致后，物化规范 LWR bundle，持久化 `FinalityBlessed` 并下发收尾 continuation。此时绝对不得触发 `LifeCompleted` 或注销 Manager。Manager 收到工作记录后继续处理所有 minor 问题。

## FINALITY-017: rest 对应第二次 suicide 且 last_words 为最终答案

已获 blessing 的 Life 再次调用 `suicide` 时，执行安全检查与过程评审尾抽干；确认无阻塞 REVISE 后，不再重复审查与检查见证，写入本次 last_words，持久化 `LifeCompleted`，向模型返回 at-rest 经验。`LifeCompleted` 只终结业务 Life，不得由 `suicide` tool-call step 直接伪造 provider `TerminalOutcome`；当前 physical execution 必须保留至 Host 观察到同一 `PhysicalUserMessageId` 的真实 final assistant terminal。若该 Manager 属于 Orchestrator job，Life 已归档只能在该真实 final assistant 被 reconcile 为 `TurnCompleted` 时作为 handoff 证据，由普通 terminal reporter 发布完成；`TurnInProgress` 绝不得因 Life 已归档而提前 terminal。成功输出逐字等于 `last_words`，严禁附加多余文本。

## FINALITY-018: Manager deferred completion

首次合法进入终审的 `suicide` 挂起当前 Manager 执行；过程或终审 REVISE 直接返回工作记录提示，Blessing 返回收尾指导，均不终止 Manager 生命周期。

## FINALITY-019: Manager 面无 Review Guard 且 idle 只发鼓励

Manager 视野内完全移除 Review Guard 与评审催促逻辑。Manager 的普通空闲仅发送标准鼓励提示；只要当前 Life 存活且未被终审接管，每次新的 completed terminal 均可获得鼓励，不设人为次数上限。终审处理期间或已终结会话不发送鼓励。

## FINALITY-020: 隐藏机制不变成 Manager checklist

Manager 对隐藏 Reviewer、会话、barrier、witness 及 2N 编排完全无感知，严禁在 Manager 视野内暴露执行评审的隐藏角色与中间编排细节，仅暴露影响下一步行动的事实反馈与结论。

## FINALITY-021: 状态只来自 typed facts

系统生命周期状态完全基于强类型持久化事实与增量投影推导，严禁通过自然语言故事文本匹配反向推断状态。

## FINALITY-022: Life 开启条件与隔离

HumanRoot Life 仅在权威配置与消息标识严格匹配时开启。旧 Life 终结后，其资源与执行标识被原子释放，下一条合法外部请求方可触发 Reawakening。新 Life 绝不继承旧 Life 的请求、花名册、blessing 或 witness 记录。

## FINALITY-023: Opening durable 顺序与改写幂等

原始 HumanRoot 先行持久化 XTrace 与 `LifeOpened` 事实，随后方可执行模型端呈现改写。改写操作具备强类型幂等标识，重复转换不得重复注入。

## FINALITY-024: 工作期输入不改写

工作期间的输入消息不改变 Life 归属、工作起点或账本协议状态，亦不重开 Opening。

## FINALITY-025: 旧 journal 语义

历史已完成的 Life 保持完成状态。历史遗留事实仅作向后兼容解码，现代终结工作流、审查逻辑与工具接口绝对不得新增或返回 `FinalityUndecided` 结果。

## FINALITY-026: 现代 Finality 必有业务裁定且基础设施失败直接 fatal

现代 Finality 的业务裁定空间仅由 `PERFECT` 与 `REVISE` 构成，最终必定收敛至拒绝或祝福，不存在第三种业务结果。审查传输失败、通道异常、存储损坏或状态不变量破坏等基础设施故障必须直接输出诊断并终止进程，绝对不得伪装为业务结论或改写已有裁决。

## FINALITY-027: 后台资源义务

存在活跃背景子任务或 PTY 时，终结请求被拦截并返回 join 提示，包括已获 Blessing 的状态。后台资源未清偿前，停放终结流程且不发送空闲鼓励。

## FINALITY-028: ManagerJob 不复活

已发布或已释放的 ManagerJob 永久不得复活；处于 active 状态的 Job 可由编排方追加需求并在同一会话与 worktree 中延续执行。

## FINALITY-029: Finality infrastructure fatal只经mandatory injected fuse执行

现代Finality的closed business adjudication完成后，只有typed infrastructure incident可请求fatal；已有review、Life与ManagerJob settlement必须先保留。Finality tool/runtime不得直接引用fatal physical adapter或使用optional/default/global fallback；composition注入唯一capability，同一incident只允许一次report与kill，且不得把业务REVISE/阻塞裁决升级为fatal。
