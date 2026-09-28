# work-record — WHAT

## [001] record 属于 work，不属于 receiver

一段确定工作只有一份 canonical WorkRecord。不同 receiver 只选择投影视图，不改变底层事实。

## [002] 因果范围定义 invocation

Invocation 由 XTrace 因果范围 [InvocationStartCursor, InvocationEndCursor) 定义，对所有观察者相同；不以对话转折、会话边界或 transcript 下标替代。

## [003] Chronicle 与 Recent work 按 coverage 分界

Chronicle 是 Y 已沉淀的 frame，Recent work 是 Y 未覆盖的 X-derived suffix；划分不取决于谁看过。

## [004] session 记忆不扩大本次 record

复用 session 可保留跨调用记忆，但每个 batch/resume 只物化本次 invocation 范围。早先 frame/trace 不混入，后续 Chronicle/terminal 不改变已完成范围的重物化。

## [005] Recent work 是 bounded safe suffix

Recent work 从 RecordCoverage.IngestedThrough 与 Opening 终点的较大者起算，限于本次 record frontier，并保留最后一条助手文本作为正式陈述。

## [006] canonical record 始终保留 Opening

includeOpening 只控制输出；即使省略 Opening 段，底层 record 仍完整持有其事实与锚点。

## [007] includeOpening 按方向选择

父→子 delegation 包含 Opening；子→父、同 session frozen prefix、process review、Finality 与 SyncDelegate caller 省略 Opening。带 ProviderRun 的子→父 bounded 记录从该 invocation 首个 assistant part 起算，已由 caller 发送的 user charge 不换名混入 Chronicle/Recent work。

## [008] Opening 保留原始区间

Opening 是 XTrace 原始区间 [work start, OpeningBoundary)，在 commitment boundary 确立后永久冻结。不从任务文本或第二事实源重建，不重编号 requirements。

## [009] BlindPlan Opening 截止首次 T1 commitment

BlindPlan Opening 包括初始委托、前置调查、用户澄清及 accepted planning checkpoints，直到首次 accepted planComplete=true 的 T1 调用及其 accepted 结果；该 call/result 必须完整保留。

## [010] sync 与 async 共用记录协议

Sync 与 Async 只在等待时机上不同。inspect 与 fork/join 共用同一 WorkRecord 协议和 materializer，不另建渲染规则。

## [011] 三段形状与正式陈述

WorkRecord 仅由 Opening?、Chronicle、Recent work 构成；正式陈述是 Recent work 最后一条助手散文。Terminal 只是私有完成标记，不设 Closing report 段。inspect 返回 bounded record 本身。

## [012] 散文 claim，不强制 report schema

陈述须诚实，不要求通用固定骨架或 Summary、files、tests 等必填字段。结构化数据只用于协议必需处。

## [013] 工作正文不携带 raw tool 协议

Chronicle 与 Recent work 不含 raw tool call/result 或其 linkage；BlindPlan T1 的 call/result 作为 Opening material 保留。

## [014] 两种 coverage 不互代

RecordCoverage 是可落在 turn 中间的 XTrace 游标；PrefixCoverage 是完整 Host turn 边界与 digest。二者不得相互推导或填补。WorkRecord 可含 canonical RawGap，但 RawGap 不证明前缀可替换。

## [015] WorkRecordStart 是结构性 floor

WorkRecordStart 等于 Opening 游标终点，由生命周期与 XTrace 推导，不取自业务 Stage。Opening 永久保留 raw，不交 Y 改写，不因 rebase 丢失，也不在 process review 中重复复制。

## [016] review/finality/sync 按 request range

process review、Finality 与 SyncDelegate 只消费对应 request-range bounded LWR，不以 session head 代替。复用 reviewer 按已知范围连续分段，后续全局 OpeningBoundary 不追溯改写已有起点。

## [017] Fission 收敛为单次 canonical record

Engineer 多 lane 在最终接管后汇聚为该逻辑 invocation 的一份 canonical WorkRecord。不得向属主暴露各未完成 lane 的碎片 record。
