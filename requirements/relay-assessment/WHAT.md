# relay-assessment — WHAT

## ASSESS-001: review schema 恰有八个必填 0..10 整数

`review` 工具接受 `language_algorithms`、`simplicity`、`structure`、`granularity`、`tests_evidence`、`logic_reliability_boundaries`、`caller_ergonomics`、`completeness` 八个必填维度评分与可选 `note` 参数。每个维度评分必须为 `"PERFECT"`、`"REVISE"` 或 `"N/A"` 三个枚举量之一；禁止缺省维度、未知字段、整数、浮点、null、总 verdict 与平均分。额外 `note` 参数必须是字符串，仅用来写文本，不返回，不做其他用途。

## ASSESS-002: 每任至多一个 accepted assessment，精确重放须全一致

每个 IncumbencyId 最多一个 semantic AssessmentId。assessment identity、binding、snapshot、authority 与 scores 完全一致的精确 replay 幂等返回原结果；同任第二个不同 assessment 返回 AssessmentAlreadySubmitted，相同 ToolCallId 不同 payload 返回 AssessmentReplayConflict，一律不覆盖第一次结果；跨迭代重放另一迭代的 assessment 一律拒绝。

## ASSESS-003: assessment 绑定 exact narrative 与执行身份

accepted assessment event 必须携带 RoadId、IncumbencyId、WorkspaceSnapshotId 与 AuthorityRevision；其中 AssessmentBinding 绑定 PhysicalUserMessageId、ProviderRunIdentity、ToolCallId 与同一 assistant message 中 tool call 之前的公开评审文本 digest。隐藏 reasoning 与 tool call 后文本不得进入证据。

## ASSESS-004: 任一 REVISE 维度直接定义修复义务并授予工作权

只有 REVISE 算不通过，但任何维度只要有 REVISE 就不通过。已提交 ScoreVector 中每个为 REVISE 的 dimension 都定义一项修复义务，PERFECT 与 N/A 维度不定义；这些义务由唯一 ScoreVector 直接查询，不再物化第二份状态。WorkOwned phase 在同一次 AssessmentCommitted fold transition 中进入。当前任务账本的推进是负责人接责后的执行行为，不是评审本身。

## ASSESS-005: 八项无 REVISE 生成精确绑定证书并立即降权

无任何 REVISE 维度的 assessment（全 PERFECT 或 N/A 算通过）生成 QualityCertificate，绑定 assessment、snapshot、authority、root request digest、requirement set digest、narrative digest、evidence frontier 与 target horizon。证书生成后当前迭代不得再取得 workspace mutation capability，只可读、清理资源和 suicide。Accepted 退休后证书有效期间不得开启新迭代；后续显式证书失效后允许以普通 opening 开启下一个 AuditPending 迭代，由其独立 assessment 重新判断质量。

## ASSESS-006: 同任修改后不存在复评入口

一旦 assessment 被接纳，当前迭代的 review capability 永久消失。低分后的实现质量必须由下一迭代独立 assessment 判断；不得通过第二 review、reverify 或 challenge 自证。

## ASSESS-007: malformed 不消费 semantic slot，冲突 replay fail closed

schema、范围、narrative、snapshot freshness 或 exact binding 校验失败时不写 assessment，当前迭代仍可提交唯一一次有效调用。相同 idempotency key 不同 payload 属于 conflict，必须 fail closed。

## ASSESS-008: 评审前后信息时域隔离

评审被接纳前，迭代可见的 Role、账本与工具描述只能包含独立只读的评估请求文档（`runtime/manager-assess`、`tool/review/description`），不得包含任何评审后指派文档（`runtime/manager-work`、`runtime/manager-finish`）。评审被接纳后，结果按分数选择恰好一条当前指令：非全 10 选择修复指派（`runtime/manager-work`），全 10 选择关闭退场（`runtime/manager-finish`）。评审前可见内容不得陈述低分承接修复、满分结束工作或还存在下一次迭代；循环机制只存在于 durable fold 与 projection，不存在于 provider 可见文本。
