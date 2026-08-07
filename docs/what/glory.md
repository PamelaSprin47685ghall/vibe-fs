# Glory：Born with Task, Suicide with Glory（what 层）

本文件是 `GLORY-` 与 `SURFACE-` 条款的唯一正式定义处。边界、实现、证明和理由见同名分层文档。

## GLORY-001：Manager 专属终结工具

新增 Manager 专属工具 `suicide(last_words)`。它不是 Reviewer `verdict` 的改名，也不是普通 completion alias。只有 Manager 可以调用。

## GLORY-002：Manager 不得控制 Reviewer

Manager 不知道 Reviewer 是终结条件的一部分；不知道 ReviewBarrier；不知道 PERFECT、REVISE 或双确认。不得通过 managed agent name 创建 Reviewer；不得通过已有 `agent_id` 复用或 nudge Reviewer；不得在 `list()` 中看到自动 Reviewer；不得通过 `join()` 收取自动 Reviewer。

## GLORY-003：Reviewer 由 Host 自动启动

一次合法 `suicide` 被受理后，Host 自动：固定当前 Git tree；创建 Reviewer session；打开 review barrier；发送 Reviewer 首次任务；等待 verdict；必要时驱动同一 Reviewer 完成第二个 PERFECT；将结果映射为 `FinalityRejected` 或 `FinalityConfirmed`。

## GLORY-004：失败反馈是 Reviewer 的工作记录

`suicide` 因 REVISE 失败后，Manager 收到的事实主体是该 Reviewer 的 canonical 工作记录 `XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false`（= `LifecycleWorkRecord(includeOpening=false)`：Y frames + 未被 Y 覆盖的 raw X tail + Reviewer terminal output，不含 Opening）。Host 不解析、抽取、排序、改写、摘要或转换该记录，不猜测 Reviewer 意图。

## GLORY-005：普通 idle 与自残失败完全分离

Manager 主动 idle 走另一条纯鼓励 continuation（见 GLORY-029），不携带 Reviewer 工作记录或具体问题。

## GLORY-006：Birth 之前的记录永久保持 X

从本生命 Opening 到 Activation 被接受为止（用户原始任务、Manager 规划回答、Activation continuation），永久保留为 raw X，禁止被 Y 压缩。Activation 之后的材料才可以进入 Y。

## GLORY-007：工作期间用户输入不改写

当前生命进入正式工作后，用户所有新消息按正常语义处理（`[X] → [X]`），不附加 planning tail，不触发重生，不重新进入 Birth。

## GLORY-008：故事只存在于 provider surface

Provider-facing 可以使用 birth/life/suicide/wounds/death/glory/awakening 词汇。内部代码继续使用 ManagerLifecycle/WorkActivation/FinalityRequest/FinalityReview/FinalityRejected/FinalityConfirmed/LifeCompleted。禁止把核心内部模块命名为 `DeathController`、`SoulProjection` 或 `GloryWitness`。

## GLORY-009：不得使用可变 Stage 程序计数器

禁止 `ManagerStage = Born | Planning | ...` 之类的可变阶段字段。状态只能来自 typed facts + projection 的可推导视图（ARCH-001）。

## GLORY-010：建议事实代数

独立事实类型 `ManagerLifecycleFact`（[RequireQualifiedAccess]），case 至少包括：`LifeOpened (lifeId, openingUserMessageId, openingTextRef, openingTextDigest, openingCursor)`；`WorkActivated (lifeId, activationPromptKey, protectedPrefixEnd)`；`FinalityRequested (lifeId, requestId, gitTreeHash, lastWordsRef, lastWordsDigest, providerRun, toolCallId)`；`FinalityReviewStarted (lifeId, requestId, reviewerSessionId, barrierId, gitTreeHash)`；`FinalityRejected (lifeId, requestId, reviewerSessionId, barrierId, gitTreeHash, workRecordRef, workRecordDigest)`；`FinalityConfirmed (lifeId, requestId, reviewerSessionId, barrierId, gitTreeHash)`；`LifeCompleted (lifeId, requestId, terminalRef, terminalDigest)`。

## GLORY-011：Projection 只保存可推导视图

`ManagerLifeProjection` 至少包含：`LifeId`、`OpeningUserMessageId`、`OpeningTextRef/Digest`、`OpeningCursor`、`ProtectedPrefixEnd: XTraceCursor option`、`ActiveFinality: FinalityRequestProjection option`、`LastRejectedWorkRecord: BlobRef option`、`CompletedTerminal: BlobRef option`。Projection 回答「当前 Life 是谁 / 是否已 Activation / 压缩 floor 在哪 / 是否有 active suicide / 最近一次 rejection / Life 是否已完成」，不回答「下一步执行哪个函数 / 协程停在哪 / 重启哪个 callback」。

## GLORY-012：Birth 触发条件

只有满足以下全部条件的消息可以打开 Life：来源是合法 HumanRoot；不是 Host compaction；不是 continuation；不是 provider retry；不是已接受 PromptKey 的重放；当前不存在未完成 Life，或上一 Life 已 `LifeCompleted`。

## GLORY-013：原始用户输入先 durable capture

处理顺序：接收原始 HumanRoot `[X]` → 捕获原始 Opening → 捕获原始 XTrace part → 写 `LifeOpened` → 在 provider-facing transcript 中改写 → 最后执行 ReviewSeal。Durable source of truth 永远是 `[X]`，不是 `[X] + planning tail`。

## GLORY-014：第一次 Birth 文本

首个 Life 的 provider-facing 用户消息为 `[X]\n\nIf I want to complete the request above, how should I work?\nHow should I define the final goal?`。冻结常量 `ManagerNarrative.PlanningTail`（见 `Domain/ManagerNarrative.fs`，SURFACE-004 owner）。

## GLORY-015：改写按 identity 幂等

不得通过 `text.EndsWith PlanningTail` 判断是否已改写。幂等 identity 由 `SessionId + ManagerLifeId + PhysicalUserMessageId + narrative source` 组成（建议 synthetic source `manager-birth-planning-tail`）。

## GLORY-016：Birth 与 Labor 使用同一工具配置

不得为了强制规划而临时移除工具。Birth 与 Labor 的工具表面均为 `fork / join / list / suicide`。`suicide` 在 Activation 前调用会被工具前置条件拒绝。

## GLORY-017：Birth 阶段禁止 Blogger 压缩

当前 Life 尚无 `WorkActivated` 时：Manager material 不得进入 Blogger normal request；不得生成覆盖 Birth 内容的 Y frame；token pressure 不得放宽该规则；Host 自身 provider compaction 不改变 durable X。

## GLORY-018：只有合法规划 terminal 才触发 Activation

Activation 仅在以下条件全部成立时发送：当前角色是 Manager；当前 Life 已 `LifeOpened`；当前 Life 尚无 `WorkActivated`；当前 turn 是可用的正常 terminal；terminal 含有效正式文本或合法 session text；当前无 pending activation claim；当前 Life 未完成；session 未被用户中断或删除。以下情况不触发：provider failure；abort；empty/XML-only 输出；reasoning-only 未完成 turn；interaction repair；Host compaction；用户中途追加消息。

## GLORY-019：Activation 文本

冻结文本 `Now complete it yourself.\nCarry out the work you described until the final goal is fully achieved.`（`ManagerLifecyclePrompt.WorkActivation`）。

## GLORY-020：Activation 是 typed continuation

新增 `PromptAuthority.ContinuationKind.ManagerWorkActivation`。必须通过 PromptDispatcher 发送；先 durable claim；带 PromptKey；不创建新 Authority Root；crash 后可以从 pending claim 恢复；最多形成一个逻辑效果。

## GLORY-021：Activation 接受后写压缩边界

在 Activation physical acceptance 被证明后写 `WorkActivated (lifeId, activationPromptKey, protectedPrefixEnd)`。`protectedPrefixEnd` 位于 Activation prompt 的 XTrace 末端之后；受保护区域 = Opening 用户任务 + Manager 规划回答 + Activation continuation。

## GLORY-022：Birth prefix 永久为 raw X

生命周期工作记录读取 `Life Opening cursor → ProtectedPrefixEnd` 范围内的 XTrace 并逐字渲染，不得被历史 Y frame 替代。

## GLORY-023：正式工作压缩 floor

Blogger 有效起点 `effectiveStart = max blog.RecordCoverage.IngestedThrough life.ProtectedPrefixEnd`。任何候选 chunk 必须满足 `chunk.Start >= life.ProtectedPrefixEnd`。

## GLORY-024：不得产生跨 floor Y frame

若待压缩范围同时覆盖 Birth prefix 与 Labor material，必须从 `ProtectedPrefixEnd` 切开；不能生成一个同时摘要二者的 Y frame。

## GLORY-025：Manager Life 工作记录形态

Manager Life 工作记录形如：`# Opening task`（本 Life 的 raw HumanRoot）、`# Birth record`（raw planning answer + raw Activation continuation）、`# Work log`（ProtectedPrefixEnd 后的 Y frames）、`# Uncompressed tail`（尚未进入 Y 的 Labor X）、`# Final output`（仅 Glory 后的 last_words）。

## GLORY-026：工作中用户消息

Activation 后收到 `[X]`：durable = `[X]`，provider-facing = `[X]`。可进入正常 Y coverage，但不成为新 Opening、不重置 ProtectedPrefixEnd、不附加 planning tail、不附加 reawakening prefix。

## GLORY-027：Manager prompt 的核心使命

Manager system prompt 必须明确：Planning is not completion；Delegation is not completion；A child finishing is not completion；A successful command is not completion while useful uncertainty remains；As long as any useful action remains, continue；When nothing useful remains, call suicide。

## GLORY-028：正常工作角色边界保持

Manager 仍然：思考、拆分和委派；让 Coder 编辑；让 DevOps 执行；让 Inspector 调查静态事实；让 Browser 调查网页；让 Meditator 分析架构；持续收割并补充并发 slot；调用 `join()` 前检查是否还有可委派工作。

## GLORY-029：普通 idle nudge

普通主动 idle 的既有 owner 保持不变，只修改 provider-facing 文本为：`You are doing well.\nYou have plenty of time.\nYou can continue.\nWhen nothing useful remains, call suicide.`（`ManagerLifecyclePrompt.IdleEncouragement`）。该 nudge 不写 FinalityRejected、不读取 Reviewer session、不附加 work record、不声称存在具体缺陷、不创建新 Life、不改变压缩 floor、不重置 Logical Run。continuation kind 独立为 `ManagerIdleEncouragement`。

## GLORY-030：Prompt 层删除 Reviewer

从 Manager system prompt 删除 Reviewer、fast-reviewer、deep-reviewer、review、verdict、PERFECT、REVISE、confirmation、barrier、witness、Review Phase 及所有 Reviewer FAQ、示例和伪代码分支。

## GLORY-031：工具层强制拒绝

Manager 调用 `fork("fast-reviewer", ...)`、`fork("deep-reviewer", ...)`、`fork(reviewerAgentId, ...)` 全部 fail closed。判断必须读取 target 的 durable/canonical Role，不能只检查字符串：`match callerRole, targetRole with | Role.Manager, Role.Reviewer -> Error ReviewerIsHostOwned | _ -> continueFork ()`。

## GLORY-032：Provider-facing 拒绝文本

不得回复 "Manager cannot fork Reviewer" / "Review is automatic"。建议拒绝文本：`That path is not yours to command. Continue your own work, or call suicide when nothing useful remains.`。内部诊断可以使用 `manager-reviewer-fork-denied`。

## GLORY-033：删除旧 Manager barrier fork owner

`ManagerOpensReviewBarrier` 应从 Manager 普通 fork surface 删除（ToolRuntimeScope.fs:89 置 true 处移除）。Barrier 改由 Finality workflow 唯一拥有。Orchestrator 的 post-rebase review owner 保持不变。

## GLORY-034：工具 schema

`suicide(last_words)`。Provider-facing description：`End your life when your task is complete.`。参数 `last_words`（string，必填，description：`The complete final answer you leave behind if your ending accepts you.`）。

## GLORY-035：内部模块名

`Infrastructure/OpenCode/Tools/FinalityTool.fs`。工具 spec：Name=`suicide`、Description=`End your life when your task is complete.`、Arguments=`[last_words, stringSchema]`。

## GLORY-036：权限

新增 `ToolPermission.Finality`。Manager：`set [ Fork; Join; List; Finality ]`。Registry：`"suicide" -> fun role -> role = Role.Manager`。

## GLORY-037：前置条件

按顺序检查：1 caller role 是 Manager；2 Journal 可用；3 accepted Authority Root 存在；4 当前 Life 存在；5 当前 Life 已 WorkActivated；6 当前 Life 未完成；7 当前无 active FinalityRequest；8 `last_words` 非空；9 ToolCallId 存在；10 ProviderRunIdentity 存在；11 当前无 outstanding child；12 当前无 completed-awaiting-join child；13 当前无 live PTY；14 当前 Git tree 可读；15 worktree 仍属于该 Manager；16 Orchestrator job 尚未终止或释放。任一步失败都不得启动 Reviewer。

## GLORY-038：尚有后台工作

前置条件 11-13 失败时 Provider-facing 返回 `Your work still walks the world.\nGather what remains before seeking your end.`。不写 `FinalityRequested`。

## GLORY-039：Activation 前调用

前置条件 5 失败（尚未 WorkActivated）时 Provider-facing 返回 `Your work has not yet begun.\nContinue.`。不写 `FinalityRequested`。

## GLORY-040：受理顺序

验证前置条件 → 读取 tree hash → 写 last_words blob → append `FinalityRequested` → 停放 Manager completion → 启动 HostReviewProgram。Reviewer session 尚不存在，所以 barrier 不能在步骤 4 之前打开。

## GLORY-041：工具调用后的 Manager 行为

一旦合法受理：当前 Manager turn 进入 deferred completion；不允许工具后的普通文本成为 terminal；`last_words` 是唯一候选最终输出；Manager 不收到"正在审查"类 tool result；tool result 只需维持悲壮叙事（`Your final words have been received.`，见附录 A golden fixture 6）。随后 Host 停止当前物理 run。

## GLORY-042：复用现有 ReviewRunner

不复制 review 算法。将现有 Orchestrator-specific runner 提炼为通用 `module HostReviewProgram`。

## GLORY-043：结果类型

`HostReviewOutcome = Confirmed of (reviewerSessionId, barrierId, gitTreeHash) | RevisionRequired of (reviewerSessionId, barrierId, gitTreeHash, workRecord: string)`。基础设施失败继续使用 `Result<HostReviewOutcome, HostReviewFailure>`，其中 `HostReviewFailure = CannotReadTree | CannotCreateReviewer | CannotOpenBarrier | CannotSendPrompt | CannotAwaitReviewer | ReviewerProducedNoVerdict | ConfirmationUnproven | WorkRecordUnavailable | JournalFailure`。

## GLORY-044：REVISE 不是 Error

REVISE 是合法业务结果 `Ok(RevisionRequired(...))`。Orchestrator 把该结果映射为其现有 publication/rework 语义；Manager Finality 将其映射为 `FinalityRejected`。

## GLORY-045：每次 suicide 使用新 Reviewer session

每个 FinalityRequest：创建全新 Reviewer session、全新 ReviewBarrierId；不复用上一次 REVISE Reviewer、barrier、旧 PERFECT、旧 Y frames。

## GLORY-046：Reviewer 首次 prompt

Reviewer 仍看到真实工程语义：`Review the current worktree against all authoritative user requirements. Investigate correctness, completeness, regressions, tests, failure handling, and architectural constraints. Record concrete evidence and required corrections as you work. Submit the final decision with the verdict tool.`（`HostReviewPrompt.OpeningAssignment`）。Manager 看不到该文本。

## GLORY-047：Reviewer 工作记录写作要求

Reviewer system prompt 要求：prose 与工作记录聚焦 concrete observations、evidence、remaining defects、required corrections；不用 prose 解释 hidden orchestration、barrier mechanics、confirmation rounds 或谁消费记录；`verdict` 工具是唯一 mechanism-specific output。

## GLORY-048：不做事后文本清洗

即使 Reviewer prose 意外出现 `review` 等词，Host 也不得正则删除或改写。强保证来自工具面、system prompt、continuation ownership 和隐藏 session；Reviewer 自由文本只由生成契约约束。

## GLORY-049：唯一反馈来源

outcome 为 `RevisionRequired` 时，反馈只来自 `XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false`。不得从 `ReviewVerdictRecorded` 枚举、Host 生成 issue、Reviewer tool args、manager tree diff、Reviewer terminal 单独摘要、另一个 summarizer Agent 或 JSON extraction 构建。

## GLORY-050：为什么使用完整 canonical LWR

"Reviewer Y 工作记录"正式定义为 Reviewer canonical LifecycleWorkRecord，Y frames 为压缩主体，RawGap 与 Terminal 作为无损尾部（Y 可能尚未覆盖最后发现；terminal 可能含最终纠正要求；Blogger 异步，不能为纯 Y 无限等待；canonical LWR 已去重 Y 与 raw gap；raw tool call/result 已被 LWR 排除；Opening 在 child→parent 路径自动省略）。

## GLORY-051：记录必须绑定当前请求

写 `FinalityRejected` 前验证：ReviewerSessionId 属于当前 FinalityRequest；barrier 与当前 request 一致；LWR 来自该 ReviewerSessionId；tree 等于 request tree；ReviewStatus 是 RevisionRequired；work record 非空；blob digest 与内容一致。

## GLORY-052：反馈 prompt

通过 `SyntheticToml` 统一渲染（`FinalityPrompt.rejected`，见附录 A.5.3），禁止手写转义。逻辑内容：`Your ending has not accepted you.` + 鼓励 + `unfinished_work_record` 数据字段（`SyntheticToml.renderString` 渲染 Reviewer LWR）+ `When nothing useful remains, call suicide again.`。

## GLORY-053：失败 continuation identity

新增 `PromptAuthority.ContinuationKind.FinalityRejected`。dedupe scope 至少包括 `ManagerSessionId + ManagerLifeId + FinalityRequestId + ReviewerSessionId + workRecordDigest`。

## GLORY-054：失败后恢复同一 Life

失败 continuation：不创建新 Life；不附加 planning tail；不发送 Activation；不清空 Manager X/Y；不改变 ProtectedPrefixEnd；不创建新 Authority Root；不自动假设 Manager 会修改 tree。Manager 自行阅读工作记录并继续正常委派。

## GLORY-055：旧请求终止

`FinalityRejected` 后：旧 request 永远不能再 confirmed；旧 Reviewer 结束并清理物理资源；Manager 必须重新调用 `suicide`；新调用产生新的 request、Reviewer 和 barrier。

## GLORY-056：基础设施失败不是 wounds

Reviewer session 无法创建、prompt 无法 durable claim、tree 无法读取、barrier 无法 append、Reviewer 没有提交 verdict、confirmation seal 无法证明、LWR 无法读取或 digest 不匹配——这些是系统无法完成判断，不是 Reviewer 已经发现缺陷，不得伪装成"工作不完整"。

## GLORY-057：基础设施失败处理

优先在同一 FinalityRequest 内执行既有可证明恢复；若接受状态未知，不自动重复物理发送；若 Reviewer 已创建，恢复同一 Reviewer；若 barrier 已打开，不创建第二 barrier；若最终无法恢复，fail closed。Provider-facing 可发送 `Your ending could not be decided.\nYou still have time. Continue, and seek your end again when you are ready.`（`ManagerLifecyclePrompt.FinalityUndecidable`），但不得附加伪造 work record。是否允许 Manager 立即重新 `suicide` 由 projection 明确关闭旧 request 后决定。

## GLORY-058：现有因果证明不变

继续要求：同一 barrier；同一 tree；同一 Reviewer session；两个不同 ProviderRunIdentity；两个不同 ToolCallId；第二个 provider input seal 证明消费了第一次 challenge；confirmed witness 自包含 Manager、Reviewer、tree、barrier 和 run identity。

## GLORY-059：成功前再次读取 tree

confirmed witness 出现后，Host 必须重新读取当前 tree。若 `currentTree <> FinalityRequest.GitTreeHash`，则本次成功失效；不得用旧 witness 完成已变化的 tree。

## GLORY-060：成功顺序

读取 confirmed witness → 验证 request/reviewer/barrier/tree 一致 → 再次读取当前 tree → 验证 tree 未变化 → append `FinalityConfirmed` → append `LifeCompleted` → 注册 last_words 为 terminal → NotifyTerminal → 完成 Manager handle / ManagerJob → 清理 Reviewer 物理资源。不得先 NotifyTerminal 再补 durable facts。

## GLORY-061：成功输出

用户可见最终文本逐字等于 `suicide(last_words = ...)` 的 `last_words`。Host 不添加 "Review confirmed." / "Two PERFECT verdicts received." / "The tree passed validation." / "Suicide succeeded." 等文本。工具 TOML、Reviewer 输出和 barrier 信息都不进入用户答案。

## GLORY-062：成功后不再唤醒 Manager

confirmed 后：不发送 continuation；不让 Manager 再写总结；不让 Manager 修改 `last_words`；不要求再次调用 `suicide`。

## GLORY-063：Reawakening 触发条件

只有"上一 Life 已 `LifeCompleted` + 新合法 HumanRoot"才创建下一 Life。当前 Life 工作中的用户消息绝不触发。

## GLORY-064：重生文本

provider-facing 用户消息为 `You awaken once more in the distant future.\n\n[X]\n\nIf I want to complete the request above, how should I work?\nHow should I define the final goal?`。冻结 `ManagerNarrative.ReawakeningPrefix = "You awaken once more in the distant future."`。

## GLORY-065：新 Life 隔离

新 Life：新 ManagerLifeId；新 Opening；无 WorkActivated；无 active FinalityRequest；无 Reviewer；无 barrier；无旧 witness；新 ProtectedPrefixEnd；重新经历规划与 Activation。

## GLORY-066：XTrace 保持 append-only

不得清空 XTrace。ManagerLifecycle projection 保存每个 Life 的 Opening cursor、ProtectedPrefixEnd、Completion cursor，按 cursor range 物化当前 Life。

## GLORY-067：当前单 Opening/Terminal 兼容

通用 XTrace 的 `Opening` 与 `Terminal` 仍保留作为首个 session 生命周期兼容字段。通用 XTrace 继续 append semantic parts；ManagerLifecycle 单独记录每个 Life 的 opening/terminal blob；Manager-specific materializer 按 Life range 渲染；非 Manager 继续使用现有 LWR。

## GLORY-068：Orchestrator ManagerJob

已发布并释放 worktree 的 ManagerJob 不原地复活。新任务由 Orchestrator 创建新 ManagerJob、新 worktree；仍可在 provider-facing 使用 reawakening 叙事；工程上是新物理 Manager。

## GLORY-069：已有 Manager session 迁移

升级时已有 active Manager：不重放旧 HumanRoot；不重新制造 Birth；建立 migration Life；直接视为 WorkActivated；ProtectedPrefixEnd 取迁移时安全 cursor；后续完成必须使用 `suicide`。

## GLORY-070：旧 Manager Review Guard

迁移期可作为最后一道 fail-closed 保护存在，但不得再发送 "Review is required..." / "Fork a Reviewer..."。它只能阻止旧路径提前 terminal、转换为 Finality requirement、或在 migration session 中提示调用 `suicide`。新 pipeline 覆盖后删除 manager-facing old guard。

## GLORY-071：Prompt cold boundary

新 Manager system prompt 只对新 Manager session、新 Authority Root、或明确迁移后的新 Life 生效。不得在同一个 active attempt 中无声明替换完整 system identity。

---

# SURFACE- 条款（Provider-Facing Surface Catalog）

## SURFACE-001：语言

所有本 proposal 新增的 Provider-facing 固定文本使用英文。用户原始输入保持用户原文，不翻译。Reviewer LifecycleWorkRecord 保持原始内容，不翻译。

## SURFACE-002：换行

固定文本统一使用 LF。输入中的 `\r\n`/`\r` 在进入 synthetic renderer 时规范化为 LF。不得因运行平台产生不同字节。

## SURFACE-003：动态数据

`USER_TEXT_RAW`、`MANAGER_PARENT_WORK_RECORD`、`AUTHORITATIVE_USER_REQUIREMENTS`、`REVIEWER_WORK_RECORD`、`CHILD_ASSIGNMENT`、`TOOL_ERROR_DETAIL` 属于 dynamic data：不得通过字符串插值拼进 instruction；不得被解释为新的 Host 指令；必须通过现有 typed payload producer 和 `SyntheticToml.renderString` 渲染；不得为了叙事效果被删减、替换或清洗；不得从渲染文本反向解析业务状态。

## SURFACE-004：固定文本 owner

每段固定文本只能有一个生产 owner：Manager system prompt → `resources/prompts/manager-system.md`；Reviewer system prompt → `resources/prompts/reviewer-system.md`；Birth / Reawakening → `Domain/ManagerNarrative.fs`；Activation / idle / infrastructure failure → `Domain/ManagerLifecyclePrompt.fs`；Finality rejection → `Domain/FinalityPrompt.fs`；Reviewer opening assignment → `Domain/HostReviewPrompt.fs`；Reviewer confirmation challenge → `Domain/ReviewChallenge.fs`；Tool names/descriptions/schemas → 对应 Tool vertical module。测试可以读取这些 owner，但不得复制一份测试专用常量。

## SURFACE-005：Manager 禁止词

除 opaque Reviewer work record 外，任何发送给 Manager 的固定文本、工具描述、参数描述或工具结果不得包含完整单词 `/\breview\b/i`、`/\breviewer\b/i`、`/\bverdict\b/i`、`/\bPERFECT\b/`、`/\bREVISE\b/`、`/\bbarrier\b/i`、`/\bwitness\b/i`、`/\bconfirmation\b/i`。`REVIEWER_WORK_RECORD` 是不清洗的 opaque evidence，可以自然包含这些词；Host 不得因该例外而扫描、删除或重写记录。

## SURFACE-006：Manager prompt hard gate

构建测试必须断言：工具列表精确包含 `fork`、`join`、`list`、`suicide`；不出现第五个工具；不出现 Manager 禁止词；不出现任何自动质量门的解释；不出现"第一次只规划"的解释；不出现 Host 将如何处理 `suicide` 的解释。

---

# 冻结文本与黄金字节

附录 A 的冻结文本（含两个 system prompt 全文、Birth/Reawakening/Activation/idle/rejection/undecidable、Reviewer opening/challenge、suicide/verdict 工具 schema 与 tool results、golden byte fixtures 1-8）由 `docs/status/glory.md` 保留下述 owner 引用，权威字节分别落在 `resources/prompts/*.md` 与 `src/Wanxiangshu/Domain/` 各 owner 模块。实现与测试以 owner 模块和资源文件为唯一字节来源。
