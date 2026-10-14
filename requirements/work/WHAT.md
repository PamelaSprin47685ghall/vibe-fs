# work — WHAT

本包是唯一的 work 生命周期合同。Plan work 的 sequence 是 S1、S2、S3。Manager work 与持权交付 spawn 出来的执行会话的 sequence 是 S4。结束工具名只有 deliver。结果名只有 Relayed 与 Delivered。评审结果只有 Promoted 与 Stayed。

## [001] 词表

生命周期事件只有这一套：WorkOpened、DevOpsBound、IncumbencyOpened、ConfirmationCommitted、IncumbencyRetired、Reviewed、Reopened、AdvanceCommitted、RetirementBlocked。TenureReanchored 仍属前缀 epoch，不是本词表的第二套生命周期事件。

- 允许后果：事件名不带 Plan 前缀，也不带 Road 前缀。结果名只在 Relayed 与 Delivered 中取值。评审结果只在 Promoted 与 Stayed 中取值。结束动作的注册名是 deliver。
- 禁止后果：不得签发、携带、失效或重放 QualityCertificate。事件与退休结果不得含 snapshotId，不得使用 WorkspaceSnapshot 这个类型名。不得把 suicide 注册为结束工具，也不得把它留成仍可用的别名。不得把 Road、iteration、assessment、certificate 当作现行词。不得把 Continue 或 Accepted 留成结果名。不得把旧日志翻译进新事件；旧字节不是输入。
- 失败后果：出现证书、snapshotId、suicide 别名、旧词结果名，或把旧账当作新事件输入时，写入必须拒绝，零追加。

## [002] 开立

WorkOpened 的载荷是 workId、root、sequence、权威初值。sequence 在开立时写死。确认次数、是否自动派发、持权 Delivered 是否 spawn，都从 sequence 导出，不另存字段。

- 允许后果：sequence 为 S1、S2、S3 时，确认次数为 1，后继不派发，只有交付授权已置真时 Delivered 才 spawn。sequence 为 S4 时，确认次数为 2，后继由系统派发，Delivered 不 spawn。权威初值由固定标记 authority-initial-v1 与 workId 的确定性摘要构成，不是 0，不是空串，不依赖物理用户消息标识。同一笔追加 WorkOpened 与首任 IncumbencyOpened。IncumbencyOpened 的载荷是 workId、incumbencyId、起始投影、openingCursor，不含 snapshotId。首任起始投影是 sequence 的第一个元素。openingCursor 取开立这一刻已观察到的 XTrace 头序号，不加一，不加十。
- 禁止后果：sequence 长度不是 1 也不是 3 时拒绝开立。不得把确认次数、自动派发或 spawn 写进开立载荷。缺已观察的 XTrace 头序号时不得写空游标。
- 失败后果：重复开立、双重入口或非法 sequence 一律零写。

## [003] review

Reviewed 的载荷只含 incumbencyId、stage、outcome。outcome 只是 Promoted 或 Stayed。每任恰好一次。findings 为空时记 Promoted，非空时记 Stayed。对列表只留在本次调用的返回值里，不追加成事件。

- 允许后果：序列里还有下一个元素且 outcome 为 Promoted 时，投影换成下一个元素，对象仍是这一任。序列已到最后一个元素且 outcome 为 Promoted：sequence 为 S1、S2、S3 时交付授权置真，投影留在该元素；sequence 为 S4 时交付授权保持假，只记下这一任已评。Stayed 不换投影，也不置交付授权。
- 禁止后果：不得把 findings 对、snapshotId 或证书写入 Reviewed。不得凭空 outcome 结束，也不得签发证书。S4 的 Promoted 不得置交付授权。第二评审不得写入。
- 失败后果：无活跃任期、已评、或 outcome 不是 Promoted 与 Stayed 时，零写。

## [004] tenure 尚未 review 时的动作

有任期且这一任还没有 Reviewed 时，只读与观察可用。写源码和向固定 DevOps 派工必须拒绝。

- 允许后果：读、观察、以及不写源码、不派工的工具调用保持可用。review 本身可以调用。sequence 为 S1、S2、S3 时，无权 deliver 不因尚未 review 被拒。
- 禁止后果：不得把本条收成「没有评审就拒绝一切工具」。不得在尚未 review 时写源码，也不得向固定 DevOps 派工。sequence 为 S4 时，尚未 review 且尚无确认的 deliver 不得写入 ConfirmationCommitted，也不得退休；返回已完成未退休，assessment_required 为真，零写。
- 失败后果：越界写入或派工被拒绝，原状态不变。

## [005] 一段 deliver

sequence 为 S1、S2、S3 时，deliver 是一次调用结束。本算法不写 ConfirmationCommitted。

- 允许后果：无权路径在无活跃 blocker 时，同一笔追加 IncumbencyRetired（Relayed）与下一任 IncumbencyOpened。下一任起始投影等于退休当时的折叠投影，不推进。openingCursor 等于这笔退休载荷里名为 retirementCursor 的字段，不加一，不加十。持权且交付授权已置真时，先启动执行会话；失败零写；成功后只写 IncumbencyRetired（Delivered），载荷还含 execSessionId、digest、path，并 spawn。一段式退休载荷保留 retirementId、incumbencyId、outcome、ProviderRunId、ToolCallId、名为 retirementCursor 的字段与期望推进值。
- 禁止后果：不得把一段写成两段。不得写 ConfirmationCommitted。S1 的 Promoted 之后，若交付授权仍为假，deliver 不得写成 Delivered。不得要求证书选出 Relayed 或 Delivered。
- 失败后果：无活跃任期且重放未命中时拒绝，零写。活跃 blocker 非空时写 RetirementBlocked，不写 IncumbencyRetired。持权路径缺 execSessionId、digest 或 path 时拒绝。

## [006] 两段 deliver

sequence 为 S4 时，deliver 是两次调用。第一次只落确认。另一个调用才结束。

- 允许后果：已有评审且尚无确认时，第一次追加 ConfirmationCommitted，载荷只含 incumbencyId、providerRunId、toolCallId，不结束。同一 providerRunId 与同一 toolCallId 重放返回原确认，不第二次结束。另一个 toolCallId 才进入第二次。第二次读折叠上的评审 outcome：Promoted 写 Delivered，Stayed 写 Relayed。两段式退休载荷是 retirementId、incumbencyId、outcome、ProjectionCut 与期望推进值，不含名为 retirementCursor 的字段。S4 的 Delivered 不填 digest、path、execSessionId，不 spawn。Stayed 时，退休与后任 IncumbencyOpened 同一笔追加；后任 openingCursor 来自本次调用已观察到的 XTrace 头序号，不从退休载荷读取名为 retirementCursor 的字段。
- 禁止后果：不得把两段并成一段。不得把第一次收成退休。不得用缺交付授权拒绝已评 Stayed 的第一次确认。S4 因缺 digest 或 path 不得判成 Conflict。无评审不得写下 ConfirmationCommitted。
- 失败后果：无评审且已有确认时拒绝，零写，不返回已完成未退休。同一 toolCallId、不同 providerRunId 返回 RetirementConfirmationReplayConflict。

## [007] 身份与重放

退休身份摘要的字段次序两种 sequence 相同：固定标记、workId、ProviderRunId、ToolCallId、outcome，外加入口读到的 work 计数。确认次数为 1 的标记是 oneshot-retirement-v1。确认次数为 2 的标记是 retirement-v1。两个标记不合并。

- 允许后果：无活跃任期时，同一围栏重入命中已落盘退休，返回原状态。identity 里的 outcome 只是 Relayed 或 Delivered。
- 禁止后果：不得把 retirementCursor、活跃任期标识、XTrace 头序号、snapshotId 或证书放进身份。不得用 Promoted 或 Stayed 重算身份。不得用另一个标记重算而得到同一 id。
- 失败后果：算出的 id 与已写下的 id 不同时，返回 RetirementReplayConflict，零写。无活跃任期时返回 no active incumbency，是违约。

## [008] 后继投影

后继不保留完整物理历史。义务是剥除前任 assistant 与 tool。非首任注入一次 LWR_prev。

- 允许后果：稳定消息身份的首次观察序晚于本任 OpeningCursor 才留。比较单位是 XTrace 观察序。非首任在首条本任物理消息之前插入一次 LWR_prev。首任不注入 LWR_prev。ProjectionCut 只判定退休尝试的请求身份，不当成新任的活跃请求，也不拿来过滤历史。不生成合成消息，不概括前任。
- 禁止后果：不得把完整物理历史写成现行义务。不得把前任 assistant 与 tool 留在后继消息集里。不得重复注入 LWR_prev。不得把旧日志字节翻译成新事件再注入。
- 失败后果：无稳定身份时拒绝装配，不猜测。

## [009] 阻塞与资源

唯一业务 blocker 是当前任期直接或递归拥有的 live child、background job、PTY/process、active tool、side-effect/execution lease、同步 descendant provider work，以及尚未观察到 terminal 的 cancel/join。

- 允许后果：遇阻塞写 RetirementBlocked，载荷是 incumbencyId 与 blockerDigest。清理之后可以再调用 deliver。固定 DevOps 与其已接收工作跨 tenure 继续，不因 Relayed 被孤儿化。
- 禁止后果：不得把评审非空、测试失败或脏工作区当成 blocker。不得在退休没有发生时写 IncumbencyRetired。
- 失败后果：只返回字符串、不写 RetirementBlocked，是违约。

## [010] 旧账不是输入

新 work 只写统一事件轨。不读取既有 AgentJournal，不读取既有 Relay 事件文件，不把旧字节翻译成新事件。

- 允许后果：新开立只追加本包词表里的事件。
- 禁止后果：不得增加能把旧日志翻译进新事件的路径，也不得用测试把旧字节当成合法输入。
- 失败后果：翻译层写入必须拒绝。

## [011] 两段 deliver 的后继派发

S4 两次调用之后，系统派发同一会话的后继。后继启动后，旧尝试被停止，不得再发一次会话中止。

- 允许后果：已评且第一次确认落地后，第二次调用不结束本次尝试，也不中止会话。下一次 transform 停止已退休的尝试。同一会话只启动一个后继。后继 transform 保留旧物理历史，不再中止一次。用户新 prompt 之后，后继 Manager 工具不再因退休冻结被拒。后任已有新根时，裁剪之后的唤醒被清空，不中止会话，也不因拒绝中断而崩溃。
- 禁止后果：不得把第一次确认收成退休。不得在后继已派发后再发一次旧任期的会话中止。不得把旧物理历史从后继消息集里剥掉。
- 失败后果：在途 Host 中断未完成精确的 provider-step 释放与物理中断之前，不得派发后继。

## [012] 开立与评审的内核判定

同一 workId 的第二笔 WorkOpened 不得被折叠成已开立。开立载荷出现确认次数或 spawn 时，真实折叠拒绝。S4 的 Promoted 不得置交付授权，S1 的 Promoted 也不得提前置真。

- 允许后果：第一笔开立读回 ActiveRebound，sequence 为 S1、S2、S3，事件名含 WorkOpened 与 IncumbencyOpened，载荷不含 confirmationCount 与 spawn。S1 的 Promoted 写入且授权保持假；终元素 Promoted 才置真，折叠记在 S3。
- 禁止后果：第二笔 WorkOpened 不得读成 ActiveRebound。带 confirmationCount 或 spawn 的开立不得读成已开立。S4 的 Promoted 不得把交付授权置真。
- 失败后果：重复开立与非法开立载荷的读回位置是 StorageInvalid，不是已开立。

## [013] 一段式内核退休

一段式无评审的 deliver 不得写成 Delivered，也不得被拒绝。S4 退休载荷出现 retirementCursor 必须被拒。缺期望推进值的已落盘 Delivered 不得返回 DeliveredIdempotent。无活跃任期时调用方不得追加 IncumbencyOpened。

- 允许后果：一段式 deliver 写入，明细为 relayed，事件名含 IncumbencyRetired 与 IncumbencyOpened，字节不含 Delivered。合法两段式 Relayed 写入后，读者才看见完整事件。
- 禁止后果：一段式无评审不得写成 Delivered。S4 载荷不得携带 retirementCursor。缺期望推进值不得被读成 DeliveredIdempotent。无活跃任期不得追加 IncumbencyOpened。
- 失败后果：带 retirementCursor 的 S4 读回位置是 StorageInvalid。无活跃任期的追加决定是 Refuse，事件名为空。

## [014] 两段式内核确认

两段式第一次确认落地前不得返回已确认。同一对重放不得第二次结束。

- 允许后果：已有评审且尚无确认时，第一次写入，事件名是 ConfirmationCommitted。同一 providerRunId 与同一 toolCallId 重放返回 Done，事件名为空，不第二次结束。
- 禁止后果：落地前不得返回 confirmed。重放不得写 delivered 或 relayed。
- 失败后果：无评审的第一次返回 Done，明细是 assessment required，不是 confirmed，零写。

## [015] 内核持久化与恢复位置

AppendNotAttempted 与 CommitUnknown 不得被说成已落地。只见 Relayed 不见后任是 PairIncomplete，不是 Conflict。文件不以换行结尾必须 StorageInvalid。S4 的 Relayed 后继进入后任派发，S1、S2、S3 停在等待用户。

- 允许后果：S4 的 Relayed 读回 ActiveRebound，下一步是 EnterSuccessorPrompt。S1、S2、S3 的 Relayed 读回 ActiveRebound，下一步是 WaitForUserPrompt。两个下一步不相同。成对的 Relayed 与后任读回不是 PairIncomplete，也不是 StorageInvalid。
- 禁止后果：AppendNotAttempted 与 CommitUnknown 不得写成 AppendDurable。只见 Relayed 不见后任不得收成 Conflict、ActiveRebound、DeliveredIdempotent，也不得进入 EnterDispatch、EnterSuccessorPrompt 或 WaitForUserPrompt。不以换行结尾的文件不得裁残行后折成已开立。
- 失败后果：只见 Relayed 不见后任的读回位置是 PairIncomplete，授权为假，事件名为空。不以换行结尾的文件整份 StorageInvalid，事件名与 sequence 为空。

## [016] 旧名字不是新事件

旧生命周期名字不得被统一解码收成新事件。

- 允许后果：内核写下的评审事件类型是 Reviewed。同一字节经统一解码，名字仍是 Reviewed，不含 WorkOpened。
- 禁止后果：RoadOpened 与 AssessmentCommitted 不得被解码成 Reviewed 或其它新事件。
- 失败后果：改名后的字节解码失败，事件名为空，故障携带该旧名字。

## [017] 权威推进

同一期望权威的后到推进被拒绝，不得改成后到的 next，也不得因此退休或派工。

- 允许后果：期望权威匹配时写入，事件名是 AdvanceCommitted，明细是 advanced，字节只含被接受的 next。开立权威不是空串，也不是调用方自报的错误期望。
- 禁止后果：后到推进不得改权威、不得写 IncumbencyRetired、不得写 DevOpsBound。错期望的探测不得被写成已推进。
- 失败后果：错期望与后到推进的决定是 Refuse，明细是 advance expected mismatch，事件名为空，字节为空。

## [018] 结束工具的注册名

结束工具的注册名是 deliver。注册表和资源加载都不再认 suicide。

- 允许后果：Manager 的工具名含 deliver。deliver 的准入权威是 office。工具描述含 deliver，不含 suicide。
- 禁止后果：工具名不得含 suicide。suicide 不得仍是可加载的工具资源。
- 失败后果：对 suicide 查询准入权威得到 unknown。`resources/provider/tool/suicide` 不得存在。

## [019] 执行入口按 sequence 与当前事实收口

执行入口按 sequence 与当前事实收口。工具名到权限的映射按角色解析 deliver，不按列表顺序，也不按后缀猜测。

- 允许后果：没有任期时，read、review、fork、resume、deliver 在工具执行入口被拒绝。有任期且评审尚未接纳时，只读与观察放行，派工与 deliver 被拒绝。评审已接纳且尚未确认时，read 与固定 DevOps 派工放行。按角色解析时，Manager 的 deliver 是 Finality，Plan 的 deliver 是 Deliver，两者不相同。
- 禁止后果：有确认且没有评审时，deliver 不得写入。未知名字不得当成 deliver 放行。工具名到权限的映射不得按列表顺序或按后缀猜测。
- 失败后果：上述拒绝发生在工具执行入口，不创建子会话。未知名字解析为 null，既不是 Finality 也不是 Deliver。

## [020] 目标状态形式化与三样输入

当 Plan 模式会话被初始化或任期推进时，系统为该任期装配输入。

- 允许后果：跑者接收且仅接收三样输入：本规划任务范围内的全部真实用户输入原文（`U`）、上一任期的工作记录（`LWR_prev`，由 `LifecycleWorkRecord.materialize` 且 `includeOpening = false` 渲染；若为首任则此项为空）、以及通过编程面读写的 Markdown 伪文件（`P`）。
- 禁止后果：模型不得直接读写或篡改 `U` 与 `LWR_prev`。`P` 不得作为文本直接注入模型输入消息集。模型不得取得任何超出 `P` 之外的仓库或工作区写权限。
- 失败后果：若输入集合包含未经任期剥除的上下文或遗漏必需事实，系统必须在消息投影阶段拒绝生成请求并终止本次调用。

## [021] 角色与严格权限投影

当调度器识别到 CanonicalRole 为 `Plan`（Wire 标识为 `plan`）时，系统投影其专属权限与工具集。

- 允许后果：Plan 角色且仅限 Plan 角色被赋予四个专属工具的访问权限：JS 编程面 `js-plan`，以及编排面工具 `ask`、`review`、`deliver`。`review` 与 Manager 共用同一工具名与同一语义（见 [038] 与 [003]）。
- 禁止后果：Plan 角色禁止获得代码仓库与执行工具的权限，包括但不限于 `read`、`glob`、`grep`、`edit`、`write`、`mv`、`rm`、`run`、`open-terminal`、`send-terminal`、`read-terminal`、`signal-terminal`、`fork`、`fission`、`commission`。严禁向 Plan 开放任何真实 shell 或命令执行通道。`resume` 与 `handoff` 不再属于 Plan 的工具面。
- 失败后果：任何越权工具调用必须在 `ToolRegistry` 执行门禁被直接拦截（fail-closed），返回强类型拒绝错误，零副作用。

## [022] 轮换事实与持久化重放

Plan 的轮换生命周期由不可变 Durable 事件驱动并投影状态。结束工具名是 deliver。

- 允许后果：投影只通过以下事件更新：
  1. `WorkOpened(workId, root)`：标记规划任务开启与根路径绑定。
  2. `DevOpsBound(workId, devopsId, target)`：绑定唯一的固定 DevOps 实例。
  3. `IncumbencyOpened(workId, incumbencyId, stage, openingCursor)`：开启新任期并标记当前阶段。
  4. `IncumbencyRetired(incumbencyId, outcome)`：标记任期退休，outcome 仅限 `Relayed` 或 `Delivered`。`Relayed` 表示接力退休（任期让位于下一任），`Delivered` 表示终态交付退休。
  5. `Reviewed(incumbencyId, stage, outcome)`：记录本任的 review 裁决，outcome 仅限 `Promoted`（晋级或授权）或 `Stayed`（留任原 stage）。
  6. `Reopened(retirementId)`：把已交付的工作回滚到 active 状态并开启一个新任期（见 [037]）。
  同一事件的重复重放必须保持幂等。
- 禁止后果：同一 workId 同时至多一个活跃任期（`Active`）。已退休任期列表（`Retired`）只增不减。`Reviewed` 每任至多一条；已评任期不得再次评。`Reopened` 清除投影中的 `Delivered`，并以同一笔追加开启一个新任期：沿用原 stage，交付授权重置为假，新 incumbencyId 由固定标记 `reopened-incumbency-v1` 确定性派生、与旧任期不同；`Retired` 列表中的历史记录不删除。`Relayed` 退休的任期严禁复活；`Delivered` 退休的任期仅可经 `Reopened` 触发一次重开，旧任期永不复活。禁止在这些事实之外引入旁路状态机、内存标记或外部计数器。禁止 `AskPending` 与 `AskResolved`（该挂起协议已由 [027] 自然停止语义取代）。
- 失败后果：遇到同一身份但载荷不同的冲突事件（含同一任期的第二条不同 `Reviewed`），或检测到不合法的状态跃迁（如重复激活、幽灵任期、无 `Delivered` 时的 `Reopened`、`Reopened` 的 retirementId 与退休记录不匹配），系统必须抛出 `StorageInvalid` 并关闭，严禁跳过或猜测。

## [023] 阶段推进、授权与状态正交

规划流程的阶段（Stage）严格单向：`S1`（事实与取舍）、`S2`（理解与路径）、`S3`（流程与伪代码）。工作状态（state）为 `active | delivered`。stage 与 state 正交。

- 允许后果：阶段推进唯一由 `Reviewed(Promoted)` 驱动：`S1` 晋级到 `S2`；`S2` 晋级到 `S3`；`S3` 晋级即授权（`Authorized = true`，本任获得交付权，见 [026]）。接力（`deliver` 无权路径，见 [025]）开出的下一任沿用退休时的 stage，不推进阶段。`state` 由结果为 Delivered 的 `IncumbencyRetired` 进入 `delivered`、由 `Reopened` 回到 `active`。
- 禁止后果：阶段严禁逆行、跳级或由 review 之外的任何事件推进（接力、交付、重开均不推进 stage）。未获授权（`Authorized = false`）的 `deliver` 严禁走终态交付路径，只能走接力。授权一旦授予，本任保持至退休；重开出的新任期交付授权为假。
- 失败后果：在不满足推进条件时收到 `Reviewed(Promoted)`（如对已退休任期、或 stage 越界），Fold 必须返回强类型错误，底层存储零写入。

## [024] 动作许可矩阵

工具调用进入执行前，动作门必须基于当前任期、授权与工作状态进行单一判定。

- 允许后果：
  1. 仅当调用者为 `Plan` 且工作处于 `active`（有活跃任期）时放行 `js-plan`、`ask`、`review`、`deliver`；
  2. 每一任期在调用 `review` 工具之前，`js-plan` 工具是严格只读的（仅允许读操作 `read`）；若在此之前尝试写入（`edit`、`rewrite` 等修改操作），动作门与执行层均强制拦截拒绝，并向模型报错说明必须先完成 `review`。当前任期成功调用 `review` 工具之后方解锁写权限；接力（无权 deliver）开启的新任期在其成功调用 `review` 之前重新锁定为只读；
  3. 工作处于 `delivered` 时，仅允许 `js-plan` 的 `read`，`ask`、`review`、`deliver` 一律拒绝（再次动作必须先经用户 reprompt 与 `Reopened` 回 active，见 [037]）；
  4. `review` 为硬门：本任已有 `Reviewed` 事实时拒绝再次调用（每任恰好一次，见 [038]）；
  5. `ask` 在同一 work 已有一个未决委派时拒绝新委派（见 [028]）；
  6. `deliver` 要求无活跃 blocker（递归 live 资源）；行为按授权分支：持权走交付（[026]），无权走接力（[025]）。
- 禁止后果：stage 不作为动作门：任何 stage 下四个工具的许可判定一致，阶段推进完全由 review 硬门承担。禁止在未决 ask 委派悬空时开启新的外部动作。`handoff` 与 `resume` 不在许可矩阵中，任何调用一律拒绝。禁止在当前任期完成 review 之前向底稿 `P` 写入任何改动。
- 失败后果：凡不满足许可矩阵条件的调用，一律返回强类型 typed 错误，并保持原状态不变。

## [025] 接力契约（deliver 无权路径）

当跑者调用 `deliver` 且本任未获授权（`Authorized = false`）时，系统执行任期接力。`handoff` 不再是独立工具。

- 允许后果：系统首先核查当前任期的递归活跃资源（包括 child、后台作业、未决工具调用与租约）。确认无 blocker 后，以单个原子事务追加两条事件：
  1. `IncumbencyRetired(incumbencyId, Relayed)`；
  2. `IncumbencyOpened(workId, nextIncumbencyId, stage, openingCursor)`，其中 stage 等于退休时的 stage（接力不推进阶段）。
  退休游标取自当前 `deliver` 所属物理交互的真实 XTrace 边界，表示已消费到的最后一个语义 part；下一任的 `openingCursor` 等于该游标，首个新 part 从其后开始，不另加一。较新的用户输入或其它 provider run 不得扩大本次退休范围。
  当前活跃任期更新为新任期，阶段不变，下一次 Provider 请求触发前缀重锚并重启上下文（新任期按 [034] 过滤旧任交互、插入 `LWR_prev`，保留真实对话的因果顺序）。
- 禁止后果：若存在活跃阻塞资源（blocker），禁止退休任期；系统必须记录 `RetirementBlocked` 并向模型返回 blocker 摘要。接力严禁改写 `stage`、严禁产生 `Reviewed`、严禁影响 `Delivered` 投影。禁止在未成功持久化新任期前注销当前任期。禁止使用固定增量、消息数组下标或缺省值伪造退休游标。
- 失败后果：退休边界不可证明时，在追加事件之前拒绝接力；事务写入失败时，当前任期保持活跃，不产生状态漂移。

## [026] deliver 契约（持权交付路径）

当跑者调用 `deliver` 且本任已获授权（`Authorized = true`）时，系统建立终态交付。

- 允许后果：系统首先核查当前任期无活跃 blocker，随后读取 `P` 文件内容并确认其非空。计算内容哈希（`sha256`）与字节长度。启动一个无 parent 的 Manager 执行会话（init prompt = `P` 全文，见 [039]）并取得 `execSessionId`。追加一笔 `IncumbencyRetired(incumbencyId, Delivered)`。载荷还含 digest、path、execSessionId。
  成功后归还本会话占用的 provider 额度，向用户呈现最终产物路径与回执（含执行会话已启动的事实），规划宣告完成，不再调用任何模型。
- 禁止后果：`P` 为空或不存在时严禁交付。无授权时严禁走本路径（走 [025] 接力）。`execSessionId` 严禁进入 provider 可见面。交付完成后，本任期内严禁再调用模型；再次 `deliver` 只可能由用户 reprompt 触发，且必须先经 `Reopened` 回 active（见 [037]），严禁绕过重开直接复用 delivered 投影。
- 失败后果：若文件读取失败、为空、执行会话启动失败或持久化出错，交付调用必须失败，当前任期保持不退休状态，底层存储零写入。

## [027] 自然停止

当 Plan 跑者需要用户输入时，模型自然停止本轮输出并等待下一条用户 prompt。Plan 是唯一允许空停等用户 prompt 的 role。

- 允许后果：Plan 会话的空停（finish = stop 且正式文本无效）收敛为普通回合完成（`TurnCompleted`），不触发 continuation、不触发 interaction repair、不占用重试预算。用户下一条 prompt 按普通新输入进入本任期。provider error、abort、length 截断等非空停失败仍走既有失败恢复路径，不受本条豁免。
- 禁止后果：严禁为 Plan 空停发起 continuation 或 repair 请求。严禁引入两段式 ask 挂起状态机（`AskPending`/`AskResolved`）或 Continuation 回送协议——问用户就是自然停止。严禁对其它 role 的空停应用本豁免（其它 role 的空停行为保持字节不变）。
- 失败后果：若实现把 Plan 空停误判为需要 continuation，或把其它 role 的空停误判为自然完成，均为违反本合同的缺陷，对应回归测试必须红。

## [028] ask 契约（同步委派固定 DevOps）

当跑者调用 `ask(intent)` 时，系统把调查意图同步委派给本 work 绑定的固定 DevOps。

- 允许后果：系统查询本规划任务绑定的唯一固定 DevOps 实例；若未绑定则由运行时 single-flight 创建并持久化 `DevOpsBound` 绑定事实。将 `intent` 作为调查目标投递给该 DevOps 做只读调查（含真实 bash 执行，只读约束由 office 提示词承担，不做运行时门）。等待本次调用的完成事实，确认收敛后把 DevOps 的生命周期工作记录（LWR，子→父方向，`includeOpening = false`）完整交回 Planner。
- 禁止后果：模型在调用参数中只传 `intent`，不得传递 DevOps 的 id、role、model 等底层拓扑标识。系统严禁对 DevOps 的返回结果做二次模型摘要，必须完整交付。同一规划任务同时至多运行一个未决 ask 委派；已有未决委派时再次调用必须返回强类型拒绝。返回材料不得携带底层 id（SessionId/AgentId/PtyId）。
- 失败后果：若固定 DevOps 处于忙碌状态或响应失败，返回强类型错误，不静默丢弃，不新建替代操作员。

## [029] P 路径推导与编程面工具

底稿 `P` 是模型唯一可读写的持久化文件产物，其路径由系统确定性派生。

- 允许后果：系统以 `workId` 派生安全编码的 `canonical-work-key`，路径固定为 `<git-common-dir>/wanxiangshu/plan/<canonical-work-key>/plan.md`。`js-plan` 是标准的 JS 编程面工具（接受单一参数 `program: string`，在沙箱中执行继承自 `JsProgram` 的类并实现 `async run()`），仅作用于该单一底稿 `P`。它直接模仿其它 `js-*` 工具的锚点编辑法，但因为作用域严格限定于单一底稿 `P`，严禁且无需传入任何路径参数：提供 `file(matches)` 读快照与锚点切片、`edit(changes)` 局部修改与 `rewrite(newText)` 全量覆盖能力；无 `glob`/`grep` 仓库检索能力。写入采用排他临时文件写入、flush 后原子替换的方式发布。
- 允许后果：交给模型的 `js-plan` 工具描述复用其它 `js-*` 的同一套教程面：有序锚点、`text(from, to)`、精确 `edit`、结构重组，以及一份按底稿职责成形的样例。教程按本工具的无路径签名填写：`file(matches = [])`、`edit(changes)`、`rewrite(newText)`。`P` 的单一作用域、禁路径和写前评审只作为作用域约束放在教程前面，不另写一套锚点用法。
- 禁止后果：模型不得在工具参数中使用多余字段，只传递 `program`。在 `JsProgram` 的 `file`、`edit`、`rewrite` 方法中严禁传入任何路径参数，若调用时尝试传入路径参数，沙箱必须直接报错拒绝。严禁路径穿越（path traversal）、符号链接逃逸、不安全 ID 注入或根目录漂移。`js-plan` 严禁提供任何超出 `P` 之外的文件系统能力，严禁暴露目录列表或仓库文件读写。写盘完成后取消读回校验。
- 禁止后果：工具描述不得另写精简教程替代共享教程，不得在教程或样例中教模型传入路径参数，不得把运行时内部对象暴露给模型。
- 失败后果：检测到路径异常、传入路径参数、尝试访问 `P` 之外的文件或目标文件被外部非法篡改时，操作必须立即拒绝。

## [030] 工具注册与分类

系统启动时，必须显式注册 Plan 相关的工具与参数规范。

- 允许后果：在 `ToolRegistry` 中登记 `js-plan`（作为 JS 编程面）、`ask`、`review`、`deliver`（作为编排面 ToolSpec）。在静态工具名单（`StaticTools.fs`）与投机调查分类器（`InvestigationEstimateContract.classifyTool`）中显式登记上述四个工具名称。`review` 与 Manager 共用同一 ToolSpec（共享壳按角色换目标，见 [038]）。
- 禁止后果：严禁使用前缀匹配、通配符或模糊规则对工具进行分类或准入。未显式登记的工具不得进入 Plan 的工具列表。`handoff` 与 `resume` 不得作为 Plan 工具登记或注册。
- 失败后果：若工具名称或分类缺失，系统在启动静态校验或工具构建期必须报错阻断。

## [031] 阶段资源、语言对等与真注入

Plan 角色与各阶段使用的指导文档必须在资源目录中中英双语严格成对提供，并经系统提示转换器真正装配进 Plan 会话。

- 允许后果：以下资源必须同时存在 `en.md` 与 `zh-CN.md` 且内容语义对等：
  - `resources/provider/role/plan/`
  - `resources/provider/planning/s1/`
  - `resources/provider/planning/s2/`
  - `resources/provider/planning/s3/`
  - `resources/provider/tool/js-plan/`
  - `resources/provider/tool/ask/`
  - `resources/provider/tool/review/`
  - `resources/provider/tool/deliver/`
  系统提示转换器在 `experimental.chat.system.transform` 挂钩中真正装配 Plan 提示：`catalogPrompt` 对 `Role.Plan` 返回 Plan 的 role law（`PlanSystemPrompt` 进入 PromptCatalog），`activeRoles` 包含 `Plan`，替换逻辑与其它 office 角色同规则（字节识别自己的段落、按当前全局语言本地化）。在 Plan 会话的系统提示中，按当前任期的 `stage` 以稳定字节幂等追加对应阶段的方法说明（S1 对应事实与取舍，S2 对应理解与路径，S3 对应 Knuth 伪代码）；stage 变化时下一次请求呈现新 stage 方法。
- 禁止后果：任何资源缺失单侧语言文件均属于非法状态。`resources/provider/tool/resume/` 与 `resources/provider/tool/handoff/` 不再属于 Plan 工具面，不得保留为可加载的工具资源。系统不得在 Plan 提示缺失时静默降级为无提示或单侧语言。严禁在 system transform 之外用消息注入代替系统提示装配。
- 失败后果：装载期发现任何双语不对等或缺失，系统必须在 Boot 阶段触发致命失败（fail fast），拒绝启动。

## [032] 非 Plan 路径行为不变

Plan 机制的引入不得对既有系统角色与流程产生任何侵入或行为改变。

- 允许后果：既有角色（Manager、Orchestrator、Engineer、DevOps、Blogger）的权限投影、工具注册、生命周期转换、消息变换逻辑保持字节级行为一致。Manager 的 `review` 字节行为不变（共享壳分派不改变 Manager 后端语义，见 [038]）。
- 禁止后果：非 Plan 角色严禁获取 Plan 专属工具。Plan 的 Baton 隔离逻辑严禁污染非 Plan 会话的消息集。Plan 的自然停止豁免严禁应用于其它 role（见 [027]）。
- 失败后果：任何非 Plan 回归测试失败均表明破坏了全局不变量，改动必须判定为不合格。

## [033] 事件持久化、Canonical 序列化与投影集成

本包的生命周期事实接入 EventStore 统一持久化存储与 CanonicalIntegrator 投影体系。

- 允许后果：本包事件按规范化 JSON 序列化并以自包含的 EventEnvelope 形式写入 `.git/wanxiangshu/events/<WriterId>.ndjson`。JSON 对象键必须按 Unicode 代码点严格升序排列，游标以 64 位整数无损保存。写入路径在提交到底层存储前必须先以 fold 校验当前工作状态的合法性。读取路径由 `ICanonicalIntegrator` 与 `IEventStore` 的统一 `Current` 导出工作视图，并按 `workId` 派生视图。
- 禁止后果：严禁在状态机规则校验失败时向 EventStore 追加事件。严禁使用非规范化、键序混乱或篡改字段的 JSON 格式。严禁绕过 CanonicalIntegrator 私自全量扫描历史文件或在内存中维护第二份状态。
- 失败后果：若追加前 fold 规则校验失败，返回带具体原因的强类型错误，底层存储零写入；遇到标识碰撞或损坏的事件信封，Integrator 必须 fail-closed 拒绝并进入安全关闭。

## [034] 任期隔离与前缀重锚（openingCursor 形式）

当为处于活跃任期的会话组装 Provider 请求消息集时，系统应用通用的 Baton 隔离策略。

- 允许后果：先以稳定 Host 消息及 part 身份捕获本次请求中新观察到的真实交互，再按原消息顺序执行任期过滤：
  1. 当前规划任务因果范围内的全部真实用户输入原文（U）完整保留；
  2. 保留本任期真实 assistant 与 tool 交互，不把用户回答移动到它所回答的 assistant 或工具交互之前；
  3. 非首任存在上任有效范围时，由 `LifecycleWorkRecord.materialize` 且 `includeOpening = false` 渲染 `LWR_prev`，在首条本任期物理消息之前插入一次；尚无本任期消息时，置于保留的历史 U 之后。
  `openingCursor` 表示已消费到的边界。物理消息的任期归属取其稳定身份在 XTrace 中的首次观察游标，跨前缀 epoch 不变；后续追加的 part 或重锚后的重复捕获不得把旧消息变成新任消息。工具调用及结果按完整物理交互保留或剥除，不切半。
  每个任期的首条请求按持久化的 `TenureReanchored` 事实触发一次前缀重锚（reanchorRequested = true）；必须等待该事实提交后才继续请求组装。重复请求、空停后的追问和进程重启不得为同一任期重复重锚。
- 禁止后果：上一任期及更早任期的全部 assistant 消息与 tool 消息必须全部剥除，不得进入消息集。规划底稿 P 的正文严禁直接拼装入消息集。用户消息 U 严禁压缩、重排、重编码或篡改。禁止依赖 Host 消息上并不存在的 `cursor` 字段；缺少稳定归属证据时，禁止静默删除交互或放行旧交互。对于非 Plan 角色或无活跃任期的普通会话，严禁触发隔离与剥除。
- 失败后果：若任期消息装配失败、游标越界、非空上任范围的记录不可得或必需的重锚提交失败，系统必须停止当前请求组装，拒绝发送残缺或受污染的上下文。

## [035] 崩溃恢复判定与位置重绑

系统在进程重启或插件重新加载时执行崩溃恢复判定。恢复仅依据 CanonicalIntegrator 导出的持久化工作视图与确切的底稿文件存在性检查，绝对不按底稿正文猜测阶段，亦不凭同名 session 识别 DevOps。

- 触发条件：进程启动激活持久化层后，对已存在的全部工作视图进行状态判定与恢复。
- 允许后果：
  1. 若工作视图标记为 `Delivered`（且持有合法的交付凭据与摘要），恢复位置为 `Delivered`，幂等呈现交付回执与底稿路径，绝不再调用模型，亦不开启新任期；此后用户 reprompt 经 `Reopened` 回 active（[037]），该路径属于运行时交互，不属于恢复；
  2. 若工作视图处于 `Active(incumbencyId, stage)` 且底稿文件（`plan.md`）存在，恢复位置为 `Active`，准确重绑当前任期、阶段与授权（`Authorized`），并将底稿存在标志设为 true；
  3. 若工作视图处于 `Active(incumbencyId, stage)` 但底稿文件尚未创建，恢复位置仍为 `Active` 并标记底稿不存在，系统视为可继续推进（模型可从头编写底稿），绝对不得凭空假定已交付；
  4. 若工作视图既无活跃任期亦未交付，恢复位置为 `Nothing`，系统不执行猜测与推断。
  5. ask 为同步委派（[028]），不存在悬挂的 ask 状态，恢复判定无需处理 ask。
- 禁止后果：严禁根据底稿文件内部的文本内容、标题或关键词推测任期阶段。严禁在恢复期间无故创建新任务、私自开启新任期、自动派发 DevOps 命令或自动执行 `Reopened`。严禁在恢复路径把 `Delivered` 工作重新激活为 `Active`（重开只由用户 reprompt 触发）。
- 失败后果：若视图数据损坏或出现冲突（例如 Delivered 状态与 Active 任期同时并存），恢复函数必须 fail-closed 返回 `Conflict` 错误状态，保留原始证据，拒绝进入任何执行分支。

## [036] 入口：首 prompt 开 work

当用户在 Plan mode 会话中敲入首条 prompt 时，系统开启规划任务。

- 允许后果：系统读取会话 sessionId，核验 owner role 为 `Plan`，以 single-flight 方式追加三条事件：
  1. `WorkOpened(workId, root)`，其中 `workId = sessionId`，root 为 `<git-common-dir>/wanxiangshu`；
  2. `DevOpsBound(workId, devopsId, target)`（devopsId 由运行时解析）；
  3. `IncumbencyOpened(workId, incumbencyId, S1, openingCursor)`。openingCursor 取开立这一刻已观察到的 XTrace 头序号，不加一，不加十。缺这一序号则拒绝，不写空游标。
  首任任期始于 `S1`。若该 work 已有活跃任期，入口幂等返回，不重复追加。入口挂点在消息 transform pipeline 的 Plan 分支（`resolveTenure` 之前）。
- 禁止后果：非 Plan 会话严禁触发本入口。严禁凭同名物理会话或自然语言内容猜测已有 work。严禁在入口之外的位置追加首条 `WorkOpened` 或首个 `IncumbencyOpened`。
- 失败后果：同一身份重放幂等；异载荷拒绝。若 store 不可得或写入失败，入口必须失败并保持零写入，严禁伪造 S1 兜底状态。

## [037] 工作状态机与 reprompt 重开

Plan 工作的状态（state）为 `active | delivered`，由 Durable 事实驱动，与 stage（[023]）正交。

- 允许后果：结果为 Delivered 的 `IncumbencyRetired` 把 state 置为 `delivered`；`Reopened` 把 state 回滚为 `active`。处于 `delivered` 的 work 收到用户新 prompt（reprompt）时，系统以同一笔追加写入 `Reopened(retirementId)`（retirementId 取自该 work 的 `Delivered` 退休记录）与新的 `IncumbencyOpened`：投影清除 `Delivered`，`Active` 转为新任期——新 incumbencyId 由固定标记 `reopened-incumbency-v1` 与 retirementId、本次请求身份确定性派生，stage 沿用重开时的 stage，交付授权重置为假；旧任期永不复活。恢复后模型在新任期上继续工作：新任期在成功调用 `review` 前重新锁定为只读，之后可修改 `P`、可再次 `deliver`。重开的新任期保持被重开任期最初开任时继承的上任记录范围；被重开任期自己的 `Delivered` 退休记录不作为新任期的上一任期。
- 禁止后果：严禁在无 `Delivered` 事实时追加 `Reopened`。严禁借重开推进或回退 stage、复活旧任期、清除 `Reviewed` 历史或恢复旧任期的交付授权。严禁在非 reprompt 场景（如恢复、调度）自动触发重开。`Retired` 列表的历史记录严禁删除。
- 失败后果：同一身份重放幂等；载荷或 retirementId 与 `Delivered` 退休记录不符时拒绝；底层存储零写入。

## [038] review 晋级门

Plan 的阶段推进由 `review` 硬门承担。`review` 与 Manager 共用同一工具名、同一 schema 与同一三问语义（findings 为 `(acceptance_criteria, work_plan)` pairs，空集表示缺口为空，详见 [003]），按角色换评审目标。

- 允许后果：Plan 后端的评审目标 = 下一里程碑，由当前 stage 推出：`S1` 的目标是 `S2`（理解与路径就绪）；`S2` 的目标是 `S3`（流程与伪代码就绪）；`S3` 的目标是「成为最后一任」（获得交付授权）。评审对象是「离目标的 gap」，第一任与后任同一标准。findings 为空集时，系统追加 `Reviewed(incumbencyId, stage, Promoted)`：非 S3 任期晋级到下一 stage，S3 任期置 `Authorized = true`。findings 非空时，系统追加 `Reviewed(incumbencyId, stage, Stayed)`，本任留在原 stage，gaps 作为修复义务返回模型。每任恰好一次 review：已评任期再次调用返回强类型拒绝。
- 禁止后果：严禁第二评审、reverify 或自证（对齐 [003]）。严禁向评审者泄露晋级后的流程编排。严禁以数值评分或八维必填代替 findings pairs。严禁在无活跃任期或已 delivered（未重开）时调用 review。
- 失败后果：同任第二条不同载荷的 `Reviewed` 必须拒绝（`StorageInvalid`）；工具门禁对已评任期的调用返回强类型拒绝错误，不消耗 provider 请求。

## [039] deliver 起执行会话

持权交付（[026]）时，系统启动一个无 parent 的 Manager 执行会话来承接规划产物。

- 允许后果：会话以 `agent = manager` 创建，不带 `parentID`（与用户手动新建 Manager 会话等效，role law 由其自身配置提供）。init prompt 为 `P` 的全文（逐字节，不做摘要或改写）。启动成功取得 `execSessionId` 并记入这笔记为 Delivered 的 `IncumbencyRetired`。该 Manager 会话即规划产物的执行者，其后续生命周期由既有 Manager 体系管理，与 Plan work 不再耦合。
- 禁止后果：严禁以有 parent 的会话或其它角色承接交付。严禁对 `P` 做模型摘要后再投递。`execSessionId` 严禁进入 provider 可见面（工具参数、消息文本、schema）。启动失败时严禁追加记为 Delivered 的 `IncumbencyRetired`。
- 失败后果：会话创建或 prompt 投递失败时，交付调用整体失败，任期保持活跃，等待模型重试或接力。

## [040] 唯一活跃任期

同一 open Work 至多一个 active tenure。精确事实重放只产生一个逻辑任期。身份或载荷冲突须拒绝。

## [041] 固定 DevOps 控制权

每条 open Work 只有一个逻辑 DevOps。交接时新任取得调用权，退休的旧任立即失去新派工与调用权。

## [042] DevOps 绑定幂等

Work 首次绑定一个逻辑 DevOps 后持久化，之后只可 resume，不可 fork 第二个。初始化、晚到结果、接收未知和崩溃恢复均保持唯一性与幂等。不支持并发 Manager 共享 DevOps。

## [043] 三问与 pairs

review 必填 findings，它是 [(acceptance_criteria, work_plan), …] 的数组。空数组表示缺口为空，结果是 Promoted。非空表示 Stayed。不得缺项，不得使用数值或总评分。

## [044] 资源交接

Relayed 只注销旧 Manager 的派工与控制租约。固定 DevOps、已接收工作及其持久后台进程继续运行，留给后任，不得隐式孤儿化或强制销毁。Work 最终关闭、Delivered 收束或致命异常时，按资源责任规则完成物理收束。不得遗留无主资源，也不得假定并发 Manager 分身。

## [045] 历史不代替工作区

provider 上下文由剥除后留下的本任消息、用户原文与显式资源构成。工作区由调用方自行检查，projection 不转述其内容。当前动作由事实约束的资源与能力表达，不由前任摘要代替。

## [046] durable 结果的观察与启动条件

work host 观察 durable 执行结果时原样保留调用方传入的 requestId，replay 沿用原 id，不铸造替代。物理启动只对 committed 的 DeliveryRequested 为真；Committed 的其它 kind、Replayed、Unknown、NotWritten 与 Conflict 一律不得启动。work journal 固定位于 `<git-common-dir>/wanxiangshu/work.sqlite`，不在事件流目录内。
