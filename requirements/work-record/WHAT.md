# work-record — WHAT

## [001] record 属于 work，不属于 receiver

一段确定工作只有一份 canonical WorkRecord。不同 receiver 只选择投影视图，不改变底层事实。

## [002] 因果范围定义 invocation

Invocation 由 XTrace 因果范围 [InvocationStartCursor, InvocationEndCursor) 定义，对所有观察者相同；不以对话转折、会话边界或 transcript 下标替代。

## [003] Chronicle 与 Recent work 按 coverage 分界

Chronicle 是 Y 已沉淀的 frame，Recent work 是 Y 未覆盖的 X-derived suffix；划分不取决于谁看过。

## [004] session 记忆不扩大本次 record

复用 session 可保留跨调用记忆，但每个 batch/resume 只物化本次 invocation 范围。早先 frame/trace 不混入，后续 Chronicle/terminal 不改变已完成范围的重物化。

bounded 视图只采用完整落在本次工作范围内、且不跳过尚未表示原件的连续 Chronicle 前缀。跨界 frame 不能整段借用，也不能按摘要文字裁切；从已采用前缀的末端读取本范围的 canonical 原件，保留本轮全部剩余材料。视图的取材起点由实际采用的材料证明，不沿用已排除 frame 的 coverage，不改写 durable RecordCoverage 或 PrefixCoverage。

已验证的 Terminal 完成事实固定该 ProviderRun 的因果终点和当时可见的 Chronicle 引用；并发事实不因冷回放排在 Terminal 前就成为其取材依据。WorkRecord 在同一次事实折叠中派生完成表示；首次读取、后续 entry/squash/Terminal/reanchor 和冷恢复均不重新选择摘要。带 ProviderRun 的历史读取须匹配其确切完成终点；不带 ProviderRun 的 bounded 读取使用该终点因果上首个完成事实的摘要引用，但不借用 Terminal 正文。无法证明唯一完成表示的并发 Terminal 返回缺证，不任意按回放次序选胜者，也不回退到当前摘要。尚未完成的范围与全 session 记忆继续使用当前视图。

真实完成消费者以该 ProviderRun 的 Terminal 终点关闭 invocation，不取完成后增长的 session head。已保存的旧完成 blob 继续作为其原有消费者的完成表示，不根据缺失的 range 关联自动迁移或改写旧历史。

## [005] Recent work 是 bounded safe suffix

Recent work 从 RecordCoverage.IngestedThrough 与 Opening 终点的较大者起算，限于本次 record frontier，并保留最后一条助手文本作为正式陈述。

## [006] canonical record 始终保留 Opening

includeOpening 只控制输出；即使省略 Opening 段，底层 record 仍完整持有其事实与锚点。

## [007] includeOpening 按方向选择

父→子 delegation 包含 Opening；子→父、同 session frozen prefix、process review、Finality 与 SyncDelegate caller 省略 Opening。带 ProviderRun 的子→父 bounded 记录从该 invocation 首个 assistant part 起算，已由 caller 发送的 user charge 不换名混入 Chronicle/Recent work。

## [008] Opening 保留原始区间

Opening 是 XTrace 原始区间 [work start, OpeningBoundary)，在 commitment boundary 确立后永久冻结。不从任务文本或第二事实源重建，不重编号 requirements。

## [009] Opening 在 invocation 起点一次冻结

Opening 由 trace owner 在 invocation 开始时捕获且只捕获一次：首个 prompt 的原始委托文本与权威 requirements 按原字节保存；相同文本重放幂等，不同文本被拒绝，此后任何 turn、coverage 或 terminal 都不改写它。Opening 游标终点（= WorkRecordStart）等于该 record 首个语义 part 的 frontier，纯由 XTrace 与生命周期推导，不取业务 Stage、对话转折或 planning checkpoint；旧 BlindPlan T1 / planComplete 判据已退役，不再是边界来源，首个语义 part 之后的材料一律归 Chronicle 或 Recent work。Opening 段原样渲染该 evidence，并原样接纳 trace owner 供应的 Opening 区间渲染（该通道不过滤 raw tool call/result）；供应的 call/result 必须完整保留，不得降级为 incidental tool。

## [010] sync 与 async 共用记录协议

Sync 与 Async 只在等待时机上不同。inspect 与 fork/join 共用同一 WorkRecord 协议和 materializer，不另建渲染规则。

## [011] 三段形状与正式陈述

WorkRecord 仅由 Opening?、Chronicle、Recent work 构成；正式陈述是 Recent work 最后一条助手散文。Terminal 只是私有完成标记，不设 Closing report 段。inspect 返回 bounded record 本身。

## [012] 散文 claim，不强制 report schema

陈述须诚实，不要求通用固定骨架或 Summary、files、tests 等必填字段。结构化数据只用于协议必需处。

## [013] 工作正文不携带 raw tool 协议

Chronicle 与 Recent work 只渲染 text/reasoning（media 以 omission 占位），不含 raw tool call/result 或其 linkage；同一输入必须产生同一文本。Opening 段是唯一例外通道：trace owner 以 Opening 投影（恒等，不滤除 tool）供应的 call/result 作为 Opening material 保留，不得混入 Chronicle 或 Recent work。

## [014] 两种 coverage 不互代

RecordCoverage 是可落在 turn 中间的 XTrace 游标；PrefixCoverage 是完整 Host turn 边界与 digest。二者不得相互推导或填补。WorkRecord 可含 canonical RawGap，但 RawGap 不证明前缀可替换。

## [015] WorkRecordStart 是结构性 floor

WorkRecordStart 等于 Opening 游标终点，由生命周期与 XTrace 推导，不取自业务 Stage。Opening 永久保留 raw，不交 Y 改写，不因 rebase 丢失，也不在 process review 中重复复制。

## [016] review/finality/sync 按 request range

process review、Finality 与 SyncDelegate 只消费对应 request-range bounded LWR，不以 session head 代替。复用 reviewer 按已知范围连续分段，后续全局 OpeningBoundary 不追溯改写已有起点。

## [017] Fission 收敛为单次 canonical record

Engineer 多 lane 在最终接管后汇聚为该逻辑 invocation 的一份 canonical WorkRecord。不得向属主暴露各未完成 lane 的碎片 record。
