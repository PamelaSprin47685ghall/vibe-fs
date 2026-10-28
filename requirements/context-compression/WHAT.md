# context-compression — WHAT

## [001] 不推导容量，只向模型披露计量与声明常量

严禁从自身观察推导、估算或缓存任何模型上下文窗口大小；禁止上下文余量、模型容量表，或把字节/token 换算成窗口比例的逻辑。唯一允许的字节计量为合法的文件与进程计量。

可以披露的只有两类事实：已经发生的计量，与 provider 自己声明的常量。宿主通过 `todowrite` 披露本次模型自己报告的剩余计费回合估计及 [034] 的确定性升级后果，并在 `chat.params` 屏障处读取、由 Provider 语言适配披露 provider 对当前模型声明的 context limit（opencode 模型配置里的值，宿主只读不推）以及 provider 实际计费的缓存命中与未命中（`ProviderCalibration`）。二者都只是模型判断自身状态的 datum——模型是唯一知道哪些上下文对任务重要的主体，披露把选择权交给它。宿主不得由这些数字推导窗口大小、剩余预算，也不得替模型做出压缩决定。

## [002] 不主动预测溢出

在请求发送前，宿主严禁判断是否接近容量上限，禁止根据投影长度比例、剩余预算或累计 Token 主动选择压缩点或触发自动裁剪。真实的 provider attempt 失败是唯一的恢复触发信号。

向模型披露进度计量（[001]、[034]）不构成窗口剩余量预测：系统只比较模型实际报告的非负实数。显式 `resetScope` 才推进工作 Scope；系统自动 compaction 是独立容量保护动作，不代表重锚。

## [004] 输出预算属 provider

系统不计算输出应占用的 Token 数量，亦不检查压缩比例。对输出的唯一内容校验为终态文本判定（`Participant/Provider/Attempt/TerminalValidity.fs`，经 `Context/Companion/CompressionSurface.fs` 暴露为 `terminalValidityCheck` / `terminalValidityIsValid`）：非空且非纯 XML 即为有效终态；拒绝原因 `Empty` 与 `XmlOnly` 可区分，只用于诊断。

## [005] 失败不分类

控制流仅识别快照的 `Outcome`（`Completed | Failed | Aborted`），严禁根据错误字符串或异常类型细分溢出、网络或限流等原因。

## [006] 重试不构造 prefix probe，失败预算有界

每个物理 retry attempt 拥有全新 physical identity；不维护恢复材料，也不按前缀候选挑选重试对象。provider attempt 失败后的恢复策略统一为：**第一次原样重试，第二次把该 Provider 拉黑后重试，以此类推**（见 provider-attempt-recovery-021）。每次确认失败经 `ProviderFailureLedger` 记账并推进 `ProviderFailureBudget` 连续失败计数；`verdict` 为 `Exhausted`（单一 `DefaultBudget`）时停止自动重试。业务主请求（`WorkMain` 的有效成功）一旦成功，清零连续失败计数。

## [007] 按 RequestKind 分派结局

每个 attempt 的结局按 `ProviderRequestKind` 进行确定性分派；RequestKind 必须来自 attempt 的 typed context、durable receipt 或已接受的续做归属证据（`Interaction/Authority` 的 `AcceptedContinuationIds`），而非角色猜测或错误文本。同种 RequestKind 的同种结局必须产生完全一致的后继动作。`InteractionRepair` 成功不得清零连续失败计数；只有 `WorkMain` 的有效成功可以清零。

## [008] 主工作会话不发压缩请求

主工作会话不向主模型发送“请总结自身历史以节省容量”的人工压缩指令。工作 Scope 重锚只由模型显式调用 `resetScope`。宿主系统自动 compaction 允许存在，插件把配置固定为 `compaction.auto=true`、`compaction.prune=false`、`compaction.autocontinue=true`（`Host/CompactionPolicy.fs` 的 `requiredSettings`）；被观察到的 compaction 伪运行只记一条 `ContextReanchored`，不推进工作 Scope epoch，也不更改计费估计、Advisor 专席与在途委托事实。`prune` 必须为 false，因为它绕过 transform 边界直接删除持久化消息行，删除的行无法靠重锚修复。

【程序待改】`src/Wanxiangshu/OpenCode/Host/HostCompactionGate.fs` 与 `src/Wanxiangshu/Host/CompactionPolicy.fs` 的头注释仍写“force the compaction settings off”，与现行 `Required=true`（保留宿主 auto 与 autocontinue）自相矛盾，应改写注释。

## [013] 诊断不是控制输入

可观测诊断日志严禁作为控制流输入，不得使用任何诊断字段驱动 retry 的分支决策。

## [017] 真实用户消息与 scope 清理

**真正由操作员编写的用户消息**不是上下文清理边界，且永不删除。每次 `resetScope` 清理时按原序保留这些 operator 用户消息（包括确实来自操作员的普通 prompt 或 steer）；宿主上的 `role: "user"` 载体不自动意味着 operator 身份。协商、旧 Scope、Advisor 等代理答复以 `attribution: agent` 呈现时属于代理过程，不得冒充 operator；处于旧 scope 的这类答复按普通历史清理，最近一次 reset 之后属于当前活跃 scope 的内容原样保留。没有 id 或没有归属标识的消息按原样保留，不得借缺标识在保留名单之外另行删除。

最新一次 `resetScope` 之前，始终保留真人操作员消息、实际可取得的 assistant 文本、历次 `resetScope` 及关键工具的完整调用与配对结果；仅清理不在保留名单上的普通工具过程，以及属于已结束 Scope 的 thinking/reasoning。assistant 消息即使混有被清理的工具 part，其正文仍须保留。最新 `resetScope` 之后的消息（含 thinking）完整保留。两种宿主都必须从中立计划取得 part 类别，不得把 reasoning 折成无类别对象后按“非工具 part”一律保留。

## [019] 重锚边界与辅助注入可见性

任何代表上下文重锚的操作（如 `ScopeReanchored`、`ContextReanchored` 或 `TenureReanchored`），必须将旧 provider horizon 中的辅助注入可见性归零。持久化事件保留作为审计事实，但新 horizon 的初始消息不再回放旧辅助材料，仅在后续各自正常触发时逐步重新生成。

## [023] retry 全事件驱动且时间无关

Provider retry 的 correctness path 严禁读取 wall clock、构造 `TimeSpan`、调用 timer/deadline/Delay 或以超时作为状态转换。确认失败且宿主停止重试后，即按有界预算推进原样重试或拉黑 Provider 后重试，不等待任何后台生产者放行。

## [030] 关键咨询工具原样保留

`assume` 是短小但高价值的判断承诺点。在活跃 scope 内，承载 `assume` 调用的原始 Host 消息与对应 result 消息原封不动保留；不得把 assumption 文本抽出来重写、不得重新渲染参数。

每次 `resetScope` 都必须**原样保留所有历史 `assume` 调用及其成对结果**；`negotiate`、`askPreviousScope`（含旧拼法 `askPrevious`）与 `advisor` 同样不因后续任何重置而删除，无论位于最新 reset 前多少代、与普通调用共处同一 assistant 多 part 批次、或结果独立成消息。其它工具仍严格裁剪：不得保留无对应调用 ID 的孤儿结果，也不得连带保留其相邻普通调用。保留判定只依据真实工具调用身份；程序内成员（如 `this.advisor`）不是物理 `advisor` 调用，必须由生产调用归属给出身份，不得靠猜测脚本文本判定。OpenCode 与 OMP 只能投影 `wanxiangli/pair-hint` 的中立计划，不得按英文工具名各自维护第二份保留清单。普通非豁免工具与过程文本仍按最近一次 reset 清理。

## [033] resetScope 屏障、原文保留与冷前缀边界

`resetScope` 是宿主独占工具，公开参数只有两段文本：

```ts
resetScope(
  oldScopeVerboseVerbatimExperience?: string | null,
  newScopeObjective: string
)
```

**没有** `newScopeTurnBudget` 或 `kpiCompletion`，两宿主也不得为这些字段保留兼容影子判断。成功的 durable `ScopeReanchored` 开始下一 Scope，并使单一 `ScopeProgress` 在新 epoch 重新建立首次计费估计；失败、Unknown 或同调用重放不得提前更新进度。

请求副本清理普通旧工具过程与旧 Scope 的 thinking/reasoning，但历次真人 operator 消息、所有实际可取得的 assistant 文本、`resetScope` 以及 [030] 的关键工具完整调用与按 `toolCallId` 配对的原结果，跨任意次数 reset 保留。不能因 assistant 与被清理的 read/bash 同消息就误删 assistant 正文；不得保留孤儿工具结果。真正在途的 fork/resume/commission 从 durable handle 投影（`AgentProjection` 中 `Lifecycle = Active` 的 handle）与子会话最后一次已接受派出提示词读取，`resetScope` 的成功返回值可以附“尚未归队的外派工作”清单，不改写历史工具返回值。

`resetScope` 的落地同时构成冷前缀边界：持久日志追加一条不可逆的 `ScopeReanchored` 事实，记录前一 epoch、新 epoch 与本次调用的 `toolCallId`；`ActivePrefixEpoch` 恰好加一，已缓存的 provider wire 前缀快照被幂等清空。重锚后的下一次请求按全新 epoch 起点重新建立精确字节前缀，旧 scope 清理造成的历史字节缺失不算前缀漂移、不违反同 epoch 前缀追加律（prefix-stability-001）。同一 `toolCallId` 严禁重复产生重锚事实。

配套的回问工具为 `askPreviousScope(questions: string)`：只访问同会话紧邻上一 epoch 的已封存出站视图，来源身份由持久化 epoch 决定，不暴露任意跨会话查询，也不接受模型指定外部档案引用；会话尚未发生任何 `resetScope` 时调用，宿主返回 `NoPreviousScope`。`questions` 必须是非空字符串，内容由重置后的新模型依交接材料与当前目标自主组织，可以一次问出多个相关问题；宿主不预填模板、不做语义审查或关键词过滤，只校验形式。受理与送达语义见 [043]。

## [034] todowrite 计费回合、最小估计、ResetDue 与 Advisor

进度计费单位是**包含至少一次真实有效 `todowrite` 的 Provider LLM 回合**，不等于所有 LLM 请求。没有 `todowrite` 的回合免费，不提交估计，也不触发进度比较。同一实际 `providerRunId` 无论执行多少次有效 `todowrite`，仅计一次；取所有报告值的最小实数 `min(remainingScopeTurnsEstimate[])`，回合识别以宿主真实回合边界为准，不因某个工具先完成就先扣一次。工具 Schema 保证每项报告的 `remainingScopeTurnsEstimate` 必填、非负实数（允许小数）；额外字段拒绝。每个 toolCallId 仅采信一次，不得因 Provider 回执重放重复结算。同回合全部受理的工具集合结束后，由下一次真实 Provider 请求边界统一结算，不按某个工具完成的时间抢先收费。

当前 Scope 第一份估计建立基线；此后每个计费回合的估计应比上一计费回合至少下降 `0.1`，判定按 `oldEstimate - newEstimate >= 0.08`（`JudgedDrop`）继续工作，否则（持平、上升或降幅不足）自动产生专席 Advisor Process。对外文案只承诺 `0.1`（`DocumentedDrop`）；判定留得更宽 `0.02`，正好覆盖十进制小数无法被二进制精确表示带来的误差，让按承诺报数不被误判，而不是把 0.1 暗中抬高。这个估计同样是模型求助的通道：如实报数，需要帮助时数字自然降不下来，系统据此安排 Advisor。求助要早：一旦判断剩余回合装不下当前 Scope，就在同一回合把估计调到真实预期并接住由此触发的顾问对话；提示词据此鼓励及时、主动求助，而不是闷头硬扛。`todowrite` 参数描述按此措辞公开规则，也**不指示模型不得策略性报数**。

估计到 0 时由宿主记下 `ResetDue`，从下一次正常工作请求开始追加单独的强 agent-attributed `resetScope` steer，直到真实重锚；重置前再次计费也触发 Advisor。当前 Scope 还没有任何已结算估计时（包括冷恢复），正常工作请求追加一条独立强 agent steer，要求下次 `todowrite` 提交首次估计，**不要求重置**；此时既不伪造 `[Remaining turnBudget: 0]`，也不触发零预算弱提醒，也不因冷恢复催 `resetScope`。只有 `wanxiangli/scope-progress` 从真实 SQLite 计费事实（逐 toolCall 的 `Recorded`、经 CAS 一次提交的 `Settled`，库文件 `.Herdchestra/wanxiangshu/advisor-process.sqlite`）重建出该 Session/Scope 的上个付费回合估计、最小值与 `ResetDue`；无证据时保持 `known=false`，只有 SQLite 已确认的零估计锁存才允许继续催真实重锚。代理侧请求／标题摘要请求不追加进度提示，不递减和修改进度。真人新消息、协商答复、旧 Scope 回答与其它代理答复都只是当前 Scope 的继续输入，不自动清零进度、不开启新 Scope，也不因为消息到达催 reset。

工具执行结果、assistant 原文和历史工具调用**绝不**附带 `[Remaining turnBudget: N]` 后缀；不存在 `turnBudget` 倒计时、KPI 倍率、首次无预算强制 reset 与零预算弱提醒。进度所有权在 `wanxiangli/scope-progress`，PairHint 只决定历史过滤与待首问 `negotiate`／`askPreviousScope` 的强 steer；两宿主不得复制一份预算状态。

ScopeProgress 的工作 Scope 身份以持久投影中不同 `ScopeReanchored` 调用的已提交数量为准。`ContextReanchored`（系统 compaction）和 `TenureReanchored` 会改变 PrefixEpoch，但不会开启新的工作 Scope，因而不得使下一次有效 `todowrite` 报告被误判为 epoch mismatch，不得丢失上一估计或清除 `ResetDue`。只有真正成功的 `ScopeReanchored` 才重新建立基线、取消旧 Scope 的 Advisor 在途咨询；重复同一次 reset 不改变身份。

第一次成功 `resetScope` 只建立显式 Scope epoch 与档案，不要求 `askPreviousScope`；从第二次成功重锚起才有可回问的前任工作 Scope 与首问义务。

## [038] Scope 与 Pair Hint 的独立所有权

`wanxiangli/pair-hint` 唯一负责 Scope 历史中立过滤、`negotiate`／`askPreviousScope` 首问 pending（`AwaitingPreviousScope` 等状态）与出站视图封存（`outboundViewStore`），以及每真实 Session 一份可覆盖的最近 transform 输入／输出视图；`wanxiangli/scope-progress` 唯一负责有效 todowrite 报告按 providerRunId 收集、`min` 结算与 `ResetDue`。两个宿主只传真实物理输入／输出、toolCallId、epoch 和 committed reset 事实，不另维护第三份估计，也不为临时会话冻结整份消息历史。

模型消息处理只有一个执行时机：**即将发往 LLM 时**。`wanxiangli/pair-hint` 的自治 ScopeSession 接受每轮请求的中立消息视图（kind、operator、prompt key、调用/结果身份与分段位置），输出保留位置、写入目标和后缀内容。OMP 与 OpenCode 两个 Host adapter 只解码自己的真实 envelope，并把这个 plan 应用到本次请求副本；领域对象既不认识 `role/content`，也不认识 `info/parts/state.output`。用户消息尾巴与工具结果尾巴是两种可独立呈现的注入，统统不修改真实工具执行结果、`tool_result`/`tool.execute.after` 回调、用户原文或持久化历史。

`negotiate`（delegation-034）与 `askPreviousScope` 的首问义务共用同一套 pending 生命周期与呈现路径，只在咨询类型、提醒文案、目标来源与解除条件上区分，且都是**提示词与时序驱动，不设全局工具拦截门禁**：pending 期间模型调用任何普通工作工具（代码修改、读写、只读查询）都正常执行并产生业务效果，宿主不得在 `tool.execute.before` 因首问未完成而阻断；持续提醒只由每次正常 Provider 请求另行生成一条 `attribution: agent`、`steering: true`、`strong: true` 的独立 user 载体消息承载，不挂在工具结果、assistant tool part、旧 reset 结果或操作员原文上，也不改写它们的既有内容。pending 期间工具结果不呈现预算后缀、不参与额外结算，零预算弱提示保持关闭。模型在 pending 未解除时再次调用 `resetScope`，由该工具自身拒绝执行并说明必须先完成当前首问；该拒绝不产生 durable 提交，也不影响其它工具。首问义务登记 receipt 即解除 pending（`negotiate` 首次受理即激活 `resetScope`，不等委托者答复）；之后的追问属于普通咨询，不进入 pending。首次成功 `resetScope` 不建立回问 pending，从第二次成功重锚起每个新 epoch 才建立一次。

`Pair Hint` 原有的持久落点与 synthetic skill 消息仍由 Host 原生机制提供，不能与用户消息、普通工具输出的两条注入线混为一谈；宿主只负责物理消息呈现与日志定位，不负责 scope 的领域判断。

## [039] OpenCode 发往模型前的热修正

OpenCode provider messages transform 必须在宿主持有的原消息数组绑定上替换**本次请求副本**里的元素，不能仅通过赋值 `outObj.messages` 指向新数组。按当前状态注入独立强 steer；工具结果不附加预算后缀或强提醒。原始 Host 消息对象、工具回调结果与 durable history 保持原样。对已删除旧 scope 的结果不得重新插入。

## [040] 同消息工具调用／结果配对

从 `ISessionSnapshotPort` 获取的会话视图可能把多个调用和结果装在同一条 assistant 消息的不同 parts 里。中立过滤必须按 `toolCallId` 同时保留历史 resetScope 调用和同消息内的结果，并按最新 reset 在同批工具块中的位置裁剪；先行非 reset 工具的结果仍应删除。跨会话 Fork、Fission、Advisor 等上下文取材均使用相同的中立过滤计划，不另设 raw-message 判定。

## [041] ScopeSession 退场

OpenCode 的 `session.deleted` 事件到达时，清除对应物理会话在 PairHint 中的单最新 transform 视图与 pending 协议状态、ScopeProgress 的进度和 AdvisorProcessQueue 的在途单飞状态，不得影响其它会话。OMP 的 `session_start`、`session_switch`、`session_branch` 继续清理其明确的会话状态。清理不写日志、不替换真实工具结果、不触碰工作历史。

## [043] resetScope 档案、首问义务与回问送达

`resetScope` 的执行必须与上一 epoch 的出站视图档案及新 epoch 的首问义务建立原子持久化绑定。

1. **不可变档案与原子提交**：执行合法的 `resetScope` 时，宿主依据导致该次调用发生的真实 Provider 出站请求捕获 system、messages、model/variant 与可取得的工具目录及 run 身份，先把它作为不可变内容地址 blob 写入持久存储（`.Herdchestra/`），再在同一笔原子 durable 提交中把 `ScopeReanchored(epoch -> epoch + 1)`、档案的内容地址引用（`archiveRef` / digest）以及**仅从第二次成功 reset 起**的新 epoch 首问义务共同落盘。第一次成功 reset 只建立档案与计费基线，不构造旧我必须答复的义务。
2. **提交确证先于内存状态更新**：内存中的活跃 scope 状态与当前 epoch（进度基线由下一次有效 `todowrite` 独立建立）只有在 durable 提交确证成功（`Committed`）之后方可更新。提交完成前、提交失败或持久化结果 Unknown 时严禁更新内存状态；Provider 出站视图不可捕获或档案 blob 写入失败时，宿主直接返回明确错误，严禁伪造无档案的成功 reset。已消费的旧封存快照不得再被下一 epoch 的 reset 重用。
3. **调用重放与幂等边界**：同一 `resetScope` 的 `toolCallId` 重放时必须识别为已提交的重锚，直接返回既有确证结果，严禁新开第二个 epoch、重复产生首问义务或重复追加 `ScopeReanchored` 事实。
4. **原始返回文本不变**：`resetScope` 在 Host 层面执行成功的原始返回值严格保持为 `"Scope reset. Objective: <newScopeObjective>"`（可附 [033] 的尚未归队工作清单）。首问义务提示与 MUST 提醒只在即将发往模型的请求副本中热修正呈现（见 [034] 与 [038]），严禁改写盘上工具原始执行结果或持久化历史。
5. **受理与送达分离**：模型发起合法的 `askPreviousScope(questions)` 时，宿主核对会话、epoch 与调用身份后持久登记 `ScopeQuestionAsked(session, targetEpoch, actualToolCallId, sha256(questions))`，并立即返回已受理回执（或明确的形式拒绝），不同步等待旧模型回答；首问 pending 在 receipt 登记后即刻解除，不以回答送达、排队成功或物理确认为前置条件。临时只读旧模型在实际 Provider 发问时，经与 `negotiate` 共用的短命只读回合从最近一次真实 transform 输入投影紧邻上一 Scope（按最近 reset 边界重构前任消息并追加当前问题），真实 tool schema 保持不变、工具在执行层拒绝；回答经 PromptDispatcher 路径以真实 Host 用户消息载体（`role: "user"`）注入接收方会话，归属标记为 agent，携带持久 PromptKey 排队供后续 LLM 请求自然消费。回答不得伪装成 operator 消息，不得变更 ScopeProgress 的最新估计，也不得自行制造 `ResetDue`。当前会话模型与档案模型不匹配时在真正 Provider 出站前拒绝回放；没有完整旧档案、模型或原生 side-turn 能力时，只登记问询并返回 `answerDelivered=false`。
6. **回执状态如实**：`ScopeQuestionAsked` 只说明模型提交的问题及宿主对其接纳已持久，绝不等于旧 Provider 已收到、答复已生成、stale 判断已通过、steer 已排队或真实模型已消费。缺后续事实的受理回执必须报告为 pending/unknown，而不是 Answered；外部 Provider 无幂等键时，网络错误或崩溃造成的 Unknown 不得宣称「恰好调用一次」，已记录的 receipt 不得盲目重新计费。同一 `toolCallId` 的串行与并发重放都不得派发第二次旧模型推理，异问题复用同一 callId 拒绝。
7. **Stale、Unknown 与收尾**：回答送达时若接收方会话已发生新的 `resetScope`（epoch 已推进）、已被删除（`session.deleted`）或操作员已取消，该在途回答判定为 `Stale` 并丢弃，严禁跨 epoch 注入。旧模型推理返回 Unknown 时保留持久 Pending 记录与原调用身份，不得伪造成功或失败，不得盲目二次计费重试。模型在当前 epoch 再次成功 `resetScope` 时，前一 epoch 未完成的在途回问立即结清并标记 Stale，其临时执行资源释放，迟到答案不得污染新 epoch；`session.deleted` 或会话销毁事件排空该会话全部在途回问与临时会话资源，未落地的回答一并作废。首问义务解除后模型仍可主动追问，每次真实调用拥有独立 receipt，不阻断执行、不重新进入 pending。
8. **两宿主的档案能力差异（现行边界）**：锁定的 OpenCode V1 插件接口 1.18.29 中，`chat.params` 只有参数与 model，`experimental.chat.messages.transform` 只有 messages，system transform 只有 system，`tool.definition` 仅逐项暴露描述与 schema，没有携带同一 Provider 请求完整工具目录的钩子，因此 OpenCode 的封存快照记 `toolsCaptured=false`，只在确证 system/messages/model/variant 齐备时归档。OMP 通过实际注册的 `before_provider_request` 对同一 Provider 请求捕获完整原生 payload（system/messages/model 与原始工具目录），存 `toolsCaptured=true`。临时旧模型只能基于当时上下文做新的推理，不能复活隐藏思考。进程异常时没有独立的在途旧模型任务恢复机制，也没有精确物理缓存命中率事实。
