# advisor — WHAT

Advisor 是每个工作 Session 专用的第二意见席位：纯聊天、无工具、不改变任何权责。工作者不能直接调用顾问；ScopeProgress 按真实已结算的计费回合自动触发咨询，AdvisorFlow 通过隔离的短命工作者对话取材，AdvisorSeat 保存顾问自身持续增长的会话，最终建议以 agent steer 送回工作会话。宿主不向参与模型解释这套编排。

## [001] 专席身份与连续会话

同一工作 Session 只对应一个 dedicated Advisor。首次按需建立，之后继续同一席位的对话历史。物理子会话挂在家族根，Advisor 不得再派生 Advisor。重启后必须优先通过真实子会话列表认领相同席位，不得无条件新建。原始会话记录必须持久，系统 compaction 不能物理删除唯一来源。

## [002] 进度触发而不是主动工具

同一 Scope 内，只有真实 Provider 回合中被成功受理的 `todowrite` 才贡献计费估计。同回合所有估计取最小值（实数），结算一次。当前最终估计比前一计费回合少降判定门槛（对外承诺 0.1，判定 0.08），或已经报告 0 而仍再次计费时，自动登记一个 Advisor 触发；达到门槛继续工作，没有 `todowrite` 的回合不触发。工作模型知道这条规则，但不能调用 `this.advisor` 或独立的 `advisor` 宿主工具。

## [003] 单飞、合并和身份

同一 Session 同时最多一个四段 Process。运行期间第一条新触发形成等待项，更多触发合并到该项，但保留所有触发报告身份。重复观察同一报告（包括已完成咨询后的重放）不能再次请求模型。每个 Process 绑定 Session、Scope 身份和真实触发；Scope 推进后，旧事件、迟到阶段、迟到 steer 都必须拒绝。

## [004] 隔离的四段咨询

A：通过当前 Session 唯一最新 transform 输出，在只读临时会话自然询问工作者为何进度变差，取得 prose1。
B：把 prose1 发往同一 dedicated Advisor，询问需要哪些背景，取得 prose2。
C：重新读取当前 Session 此刻最新一次 transform，向隔离工作者提出包含 prose1 和 prose2 的自然问题，取得 prose3。
D：把 prose3 发给同一 Advisor，由其给出 prose4，作为 agent-attributed steer 投递。

各阶段先后顺序由执行器控制；参与模型只接收自然聊天文本，不看到临时副本、阶段号、archive、Advisor 编排或内部标记。查询不能写入真实工作会话，也不能替换实际来源模型。

模型返回的空答复、Provider 错误、异常、执行工具违规分别如实结算。只有明确的真实终局失败（宿主端口标记 ConfirmedFailure）才可写 Finished(Failed)；SDK、容量、网络错误的结果未确证时保持 StageBegan Unknown 与全局单飞，不再追加模型提问或结算下一项。不得把工具租约、容量或网络异常当作新的用户消息传给模型，也不自动重发结果未知的 Provider 请求。

## [005] 顾问零工具与物理送达

Dedicated Advisor 的 Host AgentConfig、Provider 请求工具目录和产品工具执行门禁一律无工具，执行前仍独立拒绝所有工具。工作者的临时咨询保持原始 provider 工具 schema 不变，在执行层拒绝，不得为了禁工具破坏来源前缀。Advisor 建议没有操作员身份，不更改当前 Scope 估计或 ResetDue。steer 送达以宿主真实物理接纳（`PhysicalAccepted`／`message_start`）为准；发送前和接纳回执时都必须校验源 Scope。SDK 只把消息排队的回执不算送达。

Advisor 的双语 system prompt 只使用自身的聊天指引（`resources/provider/role/advisor`），不继承工作者 Common Law 中的交付与执行协议；提示词不提 `deliver`、`negotiate` 或 `resetScope`，不规定固定的回答分类，缺少事实时直接用自然语言提问。

## [006] 废除旧成员

JS 工具绑定、Host ToolRegistry 和模型可见双语模板不得暴露 `this.advisor`。仅凭伪造 `Advisor` 能力标签不能重新注册它，模型从旧代码调用该函数也不得抵达专席或外部模型。

## [007] 原子受理与分阶段恢复边界

每一次因计费降幅不足判定门槛触发的咨询，必须在调用任何短命 Provider 或专席模型之前，把 `(session,scope,providerRun)` 与原始问题交给 `wanxiangli/eventstore` 的真实 SQLite 追加事务。只有首次返回 `Committed`，AdvisorFlow 才可运行；`Replayed` 表示同一报告的付费咨询已被受理，不能再次启动；同身份异问题的 `Conflict`、存储异常、`NotWritten` 和提交 `Unknown` 均拒绝外部调用。受理事实属于 ScopeProgress 的原子身份，不是 Work 生命周期或 Host 的第二份业务账本。

同一 SQLite 会话流还写入 `Finished(scope,reportIds,outcome)`：只有终局提交获得 Committed／Replayed 确证，才解除前一咨询的全局单飞。不同进程对同一 Session 的新报告，即使各自拥有空内存队列，遇到未结算的持久受理事实也只能记为等待，不能启动第二条模型请求；正常同进程的合并等待项按顺序推进。完成身份异载荷冲突、Unknown 或存储失败不能释放下一项。Process ID 由 Session、Scope 和首次报告确定性 SHA-256 导出，避免进程重启后物理回执重复。

**阶段事实与冷恢复**：每段 A／B／C／D 的真实外部模型调用前，在同一 SQLite 流提交 `StageBegan(scope,process,phase,question)`；答复确证后才提交 `StageAnswered(...,prose)`。最终物理 steer 单独使用第 5 阶段，Host 真正 `PhysicalAccepted` 后才提交第 5 阶段确认及 `Finished(Completed)`。相同阶段的确定性 requestId、已确认回答重放与不同内容冲突由 `wanxiangli/scope-progress` 领域状态机裁决；仅明确 `RevisionConflict` 可有界重读，提交 Unknown 不重试。`AdvisorFlow.resume` 只能按 SQLite `advisorPending` 最早未决报告、原问题和原报告的 `Replayed` 凭证认领 Process；已确认的 A／B／C／D 回答复用原 prose，跳过已收费的 Provider 请求；某阶段已 `StageBegan` 而无 `StageAnswered` 时保持 Unknown，不再发那条模型请求。OpenCode 在真实 `chat.params` 读取当前 Scope epoch 后主动尝试该恢复：优先使用 AgentJournal 已提交的 `PrefixEpoch.ReanchoredScopes` 身份，该持久身份不可读时才沿用本进程 ScopeProgress 已观察的 epoch；两者都不存在时不得猜测或恢复。已合并的 Pending 报告由持久保存的 `reportIds` 与原问题重建分组（`Queue.restoreGroup`），不能拆分多行文本猜测分组；历史缺少明确 members 的旧记录保持 Unknown，不得冒险重发。

**接纳等待预算**：建议投进正在工作的会话，Host 只在该会话当前回合结束后才落地。OpenCode 的 `ContinuationAcceptanceObserver.Budget` 与 OMP 的 `AdvisorSteerAcceptanceBudgetMs` 都给 Advisor steer 约 30 分钟（1_800_000 ms），而不是 10 秒的 admission 默认；等待期内只有真实 `PhysicalAccepted`／`message_start` 才结算 `Finished(Completed)`，超时保持 Unknown 且不重发，晚到的真实落地仍由 durable claim 接纳、但不改写已超时的等待结果。OMP 的 steer 等待超时后移除待确认回执并按 Unknown 返回，`AdvisorFlow` 保留当前 Process，本 Scope 后续触发合并为等待，直到 resetScope 或进程重启；迟到的 `message_start` 不再补记第五阶段。这样不会重复计费，但本 Scope 余下时间没有新的 Advisor 咨询。

**普通计费与多工作区隔离**：`DurableProgress` 为每个真实 `Session/Scope/ProviderRunId/ToolCallId` 提交 `Recorded`，在 Provider 请求边界只提交一次 `Settled`，用已确认事件重算 min、上回合估计、零预算锁存、未结算回合与 `AdvisorDue`。初始 Scope 0 只在 SQLite 存在确切报告或结算事实时认领。OpenCode 按 SessionId 绑定到本工作区独立的 journal factory，没有 durable workspace 时不能借用另一插件的数据库。

**专席历史与回执**：OMP 的专席问询取同一 Session 跨 Scope 最近 12 组已确认问答，作为一段散文前缀交给零工具的 ephemeral turn（`tools:false`，稳定 conversationKey）；OpenCode 的专席直接复用物理子会话自身的对话。投递回执 `[receipt:…]]` 只用于宿主匹配物理接纳，OMP 在请求副本里从 agent steer 去掉，原始历史保留。

**仍未闭合**：StageBegan 是逻辑请求预留，不是 SDK 返回的物理 ProviderRunId；结果未知的模型请求尚无物理回执可自动裁决。重启后若 ScopeProgress 尚无可信估计而 AgentJournal 已有 ScopeReanchored 身份，可认领该 Scope 的咨询，但不得伪造旧预算；两者均无 epoch 时等待真实工作事实。跨进程强杀后按原物理 PromptKey 自动确认与相应清理未完成。Scope 在提交与确认之间推进时，旧建议不得标记为成功（fence 保证的是不错误结算 Completed；物理 Host 已接纳的消息无法由后续 Scope 判定撤销）。OMP 的内部计费 run 身份以 `turn_start`／`turn_end` 为回合边界、按 toolCallId 记录最小估计并在 turn_end 结算，是受宿主回合生命周期约束的 best-effort 身份，不冒充供应商响应 ID；进程在 turn_end 前退出时，下一次宿主边界从 SQLite 结算已接受的报告。
