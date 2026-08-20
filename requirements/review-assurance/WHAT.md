# review-assurance — WHAT

## REVIEW-ASSURANCE-001: 第二次 PERFECT 必须由同一 Direct CE 因果取得

Finality 确认必须由单一工作流的调用顺序严格证明：在同一 Reviewer Session、ReviewBarrier、Git tree 及 PhysicalUserMessageId 下，首先取得首次 `judge(PERFECT)`；持久化记录后，工作流必须先注册第二判断的等待器，再调用首次投递的 `Challenge()` 完成工具调用；唯有该工具结果成功返回给模型后，第二次 `judge(PERFECT)` 才被允许触发。两次调用的 ProviderRunIdentity 与 ToolCallId 必须互不相同，PhysicalUserMessageId 必须一致。任一判断为 REVISE、代码树变化或重用历史调用标识均不得确认。严禁通过文本内容分析、摘要或 Journal 积分推断因果。

## REVIEW-ASSURANCE-002: 单次 PERFECT 不足，challenge 因果只能由 typed physical identity 建立

单次 PERFECT 不构成完成态 witness，不持久化中间半态。工作流在首次判断持久化后注册后续等待，并触发仅包含质疑提示的 tool result。若 Reviewer 在同一会话内继续，第二次判断沿用首次的 `PhysicalUserMessageId`；若 Reviewer 在第二判断前正常结束，工作流必须在保留判断等待器的前提下，先注册下一次终止观察，再发起强类型 nudge，并以 nudge 物理接受返回的 exact `PhysicalUserMessageId` 绑定第二判断。两次调用均要求不同的 ProviderRunIdentity 与 ToolCallId。challenge 与 nudge 文本仅用于模型阅读，控制逻辑严禁解析、匹配或哈希文本。

## REVIEW-ASSURANCE-003: attempt identity 五元组与同 run 额外 PERFECT 不计数

尝试身份由 `(ReviewBarrierId, GitTreeHash, ReviewerSessionId, ProviderRun, ToolCallId)` 五元组唯一界定。同一 ProviderRunIdentity（包括同消息内的并行或重复调用）中出现的额外 PERFECT 不构成第二次独立判断。独立性由工作流直接比较强类型身份，严禁依赖尝试窗口或计数器推算。

## REVIEW-ASSURANCE-004: confirmed 是派生谓词，禁止存储布尔

`confirmed` 状态只能从已完成的 `ConfirmedReviewWitness` 事实中派生，严禁存储「已确认」布尔标志、旁置 Reviewer ID 或保存未完成的中间状态。确认是完整证据链的固有属性，而非附加的控制位。

## REVIEW-ASSURANCE-005: witness 自包含且 Guard 不依赖外围 Map

`ConfirmedReviewWitness` 独立完整记录：审查者身份、任务标识、Git 代码树、两次 ProviderRun 与 ToolCall，以及驱动两次判断的 PhysicalUserMessageId。Guard 判定完全基于 witness 内部数据，严禁依赖外围 Map 补充身份，witness 结构中不设模糊摘要字段。

## REVIEW-ASSURANCE-006: tree 变化使 witness 失效

任何 Git 代码树变化均使既有 confirmed witness 对当前 Guard 校验失效。失效的 witness 作为历史记录保留审计，`witness.IsValid(currentBarrier, currentTree)` 为纯派生谓词。新的 barrier 必须重新通过完整的双重 PERFECT 流程建立证据，rebase 之后必须重新完成评审方可发布。

## REVIEW-ASSURANCE-007: physical binding fail-closed，禁止 provider-input seal

`judge` 工具投递必须携带当前物理执行绑定的 exact `PhysicalUserMessageId`；两次判断必须源于同一物理审查交互。缺少物理标识、来源不一致或调用未刷新均立即 fail closed。严禁使用 provider-input 文本摘要或扫描 transcript 来弥补因果标识缺失。

## REVIEW-ASSURANCE-008: VerdictKnown 与 ConsumableReview 两段式

Reviewer 生成针对当前过程评审的持久化裁决即达到 `VerdictKnown`，立即决定业务 outcome，但不携带 WorkRecordRef，亦不单独构成可消费报告。当且仅当该裁决对应 frontier 的规范 `ProcessReviewLWR` 达成 record-ready，且在同一 Snapshot 内持久化 `TodoReviewConcluded` 时，才形成 `ConsumableReview`。下游 checkpoint 或终结 drain 仅可消费 `ConsumableReview`，严禁提前写入未完成的空壳结论。

## REVIEW-ASSURANCE-009: record-ready 同 snapshot、排他 frontier 且事件驱动

record-ready 判定标准为「能否在同一 Journal snapshot 下物化出有效的规范 LWR」，frontier 采用排他边界（lastPart+1）。等待机制严格依赖事件驱动唤醒，禁止使用定时器、休眠或轮询。工作流必须先采样 revision，再执行就绪判定，最后发起带 revision 锚点的因果等待。等待器中断或崩溃后，基于持久事实与冻结 frontier 幂等重建并继续等待。

## REVIEW-ASSURANCE-010: 基础设施失败永远不是 PERFECT 或 REVISE

Reviewer 创建失败、分配异常、报告物化失败、协议破坏及超时等故障属于基础设施异常，绝对不得被折叠或记录为业务 PERFECT 或 REVISE。发生此类故障时，已派生的评审义务保持未决状态，系统按故障类别执行重试或立即输出诊断并终止进程，严禁制造虚假的评审结算。

## REVIEW-ASSURANCE-011: process verdict 与 terminal witness 代数分离

过程评审的裁决与终审 dual-PERFECT witness 在代数上严格分离，互不计数：过程 PERFECT 不计入终审的两次 PERFECT，过程 REVISE 亦不构成终审拒绝事实。即使同一 Dedicated Reviewer 会话被纳入终局审查，也必须在新的 barrier 下重新建立完整的 dual-PERFECT 证据链。

## REVIEW-ASSURANCE-012: 可消费证据 request-range bounded

可消费审查证据的唯一合法形式为 request-range bounded 的规范 LWR。每个用途均绑定冻结的区间范围，严禁抓取当前会话的最新 head 冒充有界记录，亦禁止将历史各轮过程记录未经界定地塞入终审报告。

## REVIEW-ASSURANCE-013: review requirement 以 Authority Root 标识

等待评审的人类需求以 Authority Root 为主键进行去重与关联，而非基于物理消息标识。确认操作仅幂等清除该确认所覆盖的具体 requirements，重放确认不得清除在其之后到达的新需求。
