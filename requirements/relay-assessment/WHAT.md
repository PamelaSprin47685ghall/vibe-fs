# relay-assessment — WHAT

## ASSESS-001: review schema 恰有八个必填 0..10 整数

`review` 工具只接受 `language_algorithms`、`simplicity`、`structure`、`granularity`、`tests_evidence`、`logic_reliability_boundaries`、`caller_ergonomics`、`completeness`。每项必须是 JSON integer 且范围 0..10；禁止缺省、额外字段、浮点、字符串数字、null、总 verdict 与平均分。

## ASSESS-002: 每任至多一个 accepted assessment

每个 IncumbencyId 最多一个 semantic AssessmentId。相同 ToolCallId + payload 的精确 transport replay 返回原结果；同任第二个不同有效调用返回 AssessmentAlreadySubmitted，不覆盖第一次结果。

## ASSESS-003: assessment 绑定 exact narrative 与执行身份

accepted assessment 必须绑定 RoadId、IncumbencyId、WorkspaceSnapshotId、AuthorityRevision、PhysicalUserMessageId、ProviderRunIdentity、ToolCallId 与同一 assistant message 中 tool call 之前的公开评审文本 digest。隐藏 reasoning 与 tool call 后文本不得进入证据。

## ASSESS-004: 任一低分原子物化义务并授予工作权

每个低于 10 的 dimension 恰好生成一个 parent QualityObligationId；10 分维度不生成。AssessmentSubmitted、QualityObligationsMaterialized 与 WorkOwnershipGranted 属于同一 durable transaction，不能观察到半状态。

## ASSESS-005: 八项全 10 生成精确绑定证书并立即降权

全 10 assessment 生成 QualityCertificate，绑定 assessment、snapshot、authority、root request digest、requirement set digest、narrative digest、evidence frontier 与 target horizon。证书生成后当前任不得再取得 workspace mutation capability，只可读、清理资源和 suicide。

## ASSESS-006: 同任修改后不存在复评入口

一旦 assessment 被接纳，当前任的 review capability 永久消失。低分后的实现质量必须由 successor 独立 assessment 判断；不得通过第二 review、reverify 或 challenge 自证。

## ASSESS-007: malformed 不消费 semantic slot，冲突 replay fail closed

schema、范围、narrative、snapshot freshness 或 exact binding 校验失败时不写 assessment，当前任仍可提交唯一一次有效调用。相同 idempotency key 不同 payload 属于 conflict，必须 fail closed。

