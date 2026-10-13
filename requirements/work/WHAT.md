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
