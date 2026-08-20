# work-record — WHAT

## WORK-RECORD-001: record 属于一段 work，不属于 receiver

一个 WorkRecord 属于一段确定的工作，而不属于特定的接收方。同一个 canonical record 被不同 receiver 以不同投影消费（例如父→子 `includeOpening=true`，子→父、review、Finality 与 SyncDelegate `includeOpening=false`）。投影选择仅改变呈现视图，绝不改变底层事实。

## WORK-RECORD-002: 边界是因果的，不是会话的

一次 invocation 的边界严格由 XTrace 因果范围定义（`InvocationStartCursor..InvocationEndCursor`），而非对话中的最近转折或 transcript 下标。因果边界对所有观察者保持客观一致。

## WORK-RECORD-003: Chronicle 与 Recent work 描述表示，不是「谁看过」

Chronicle 表示已由 Y 沉淀的 frame；Recent work 表示 Y 尚未覆盖的 X-derived suffix。两者代表 coverage 的表示边界，与特定读者是否近期查看过该内容无关。

## WORK-RECORD-004: reuse 保留记忆，但不扩大下一次 record

可复用的 session 可留存跨调用记忆，但每个语义 batch 仅物化当前 `InvocationStartCursor..InvocationEndCursor` 范围内的 record。先前的 frame 与 trace 不得进入本次 record，后续的 Chronicle 与 terminal 亦不得反向污染已完成 range 的重物化。

## WORK-RECORD-005: Recent work ≠ receiver-relative recentness

Recent work 是 bounded invocation 内 Y 未覆盖的 X-derived safe suffix，而非主观上的近期事件。它由 `max(RecordCoverage.IngestedThrough, openingEnd)` 起算，至 record frontier 结束，并包含最后一条助手文本作为正式陈述。

## WORK-RECORD-006: canonical record 保留 Opening，即使投影省略

Canonical record 必须始终完整捕获 Opening。`includeOpening` 仅控制渲染输出；在 `includeOpening=false` 的投影中即使不输出 Opening 段落，底层 record 依然持有 Opening 事实与锚点。

## WORK-RECORD-007: includeOpening 分向投影

父→子 delegation 投影 `includeOpening=true`；子→父、同 session frozen prefix、process review、Finality 与 SyncDelegate caller 投影 `includeOpening=false`。

## WORK-RECORD-008: Opening 是 preserved，不是 reconstructed

Opening 必须是精确的 XTrace 原始区间 `[work start, OpeningBoundary)`。严禁从任务文本拼接重建，严禁重编号 requirements，亦严禁通过任何第二事实源重建。Opening 在 commitment boundary 确立后永久冻结。

## WORK-RECORD-009: BlindPlan 下首次 planComplete=true commitment 属 constitutive Opening

BlindPlan 模式下的 Opening 包含初始委托、前置调查、用户澄清与 accepted planning checkpoints，直至首次 accepted `planComplete=true` 的 T1 commitment 调用及其 accepted 结果。该 T1 call 与 result 属于 constitutive Opening material，必须完整保留，不得作为附带工具调用过滤。

## WORK-RECORD-010: 一次 invocation，一份 record，处处同一

Sync 与 Async 仅在等待时机上存在差异，在表示上完全一致。`inspect`、`fork`+`join` 物化同一套 WorkRecord 协议，共用同一个 materializer，禁止维护第二套 work-record 渲染逻辑。

## WORK-RECORD-011: 三段形状 + 正式陈述 = Recent work 最后一条助手文本

WorkRecord 由 `Opening? + Chronicle + Recent work` 三段构成，不存在独立的 Closing report 段。Terminal 仅为私有完成标记而非 LWR 段落。正式陈述即为 Recent work 中最后一条助手文本（散文 claim）。`inspect` 的结果即为 bounded record 本身。

## WORK-RECORD-012: 陈述是 prose claim，不是固定 schema

WorkRecord 的陈述采用散文 claim 形式：约束事实诚实，不强制结构骨架。严禁要求通用的固定 report schema（如强制 `### Summary`、files、tests 等字段）。结构化数据仅保留于协议必需处。

## WORK-RECORD-013: LWR 禁 raw tool call/result（Opening 除外）

LWR 的 Chronicle 与 Recent work 中严格禁止包含 raw tool call 与 raw tool result 及 call/result linkage。例外：BlindPlan T1 commitment call 与 result 作为 Opening constitutive material 予以保留。

## WORK-RECORD-014: RecordCoverage ≠ PrefixCoverage

RecordCoverage（XTrace 游标，可位于 turn 中间）与 PrefixCoverage（完整 Host turn 边界 + digest）是不同的证明量纲，禁止互相推导或替代。WorkRecord 允许包含 canonical RawGap，但 RawGap 绝不构成 prefix replaceable 的证明；禁止使用 RecordCoverage 推导可替换前缀，亦禁止使用 PrefixCoverage 填补 LWR gap。

## WORK-RECORD-015: WorkRecordStart 是结构性 floor，不是 Stage

`WorkRecordStart` 等于 Opening 游标终点，由生命周期与 XTrace Opening 游标纯粹推导得出，而非业务阶段状态。Opening 永久保持 raw 形式：不交给 Y 改写，不随 rebase 丢失，亦不在 process-review 中重复复制。

## WORK-RECORD-016: process/finality/sync 一律 request-range bounded

process review、Finality 与 SyncDelegate 消费的 LWR 一律为 request-range bounded，不得使用 session head 冒充特定 checkpoint、review 或 FinalityRequest 的 bounded LWR。同生命周期内复用的 process reviewer 必须按已知范围连续分段，起点不得被后续全局 OpeningBoundary 推进所追溯改写。
