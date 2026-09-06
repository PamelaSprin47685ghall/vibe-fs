# provider-attempt-recovery — WHY

单次 provider attempt 已确认失败后，系统必须能在不重新选择 Authority、不改变 participant 身份的前提下有界地更换物理执行绑定继续，同时防止无限自动消耗资源。

**provider-attempt-recovery 保证：一次已确认的 provider attempt 失败之后，系统明确下一步如何切换执行者、最多重试几次，以及何时彻底停止。**

## 核心不变量与张力

- **确认失败 vs 进程失忆**：attempt 失败是业务层已由快照确认的失败（与 `crash-reconciliation` 的进程失忆完全区分），必须通过单一 ledger 推进 cursor。
- **无界槽循环 vs 有界自动预算**：A/A′/B/B′ participant/context 槽循环本身无界，但自动恢复预算严格有界（达到预算后停止自动请求，需等待新 Authority Root 或显式动作）。
- **换执行者 vs 不换身份**：Fallback 仅改变下一次物理执行的 EffectiveAgent；同一 durable logical participant run 的 `ParticipantIdentity`、Persona、语言、system prompt、CanonicalRole 与 Authority identity 全程不变。
- **失败分类 vs 唯一恢复解释器**：本包（具体由 `Wanxiangshu.Participant.Provider.Attempt.Fallback.ProviderRecoveryWorkflow` 独占拥有）是 provider-started retry 与 fallback 的唯一解释器。`execution-failure-policy` 是失败分类与授权的唯一 owner，本包只消费其授权，不解析异常或错误文案。managed-chat 崩溃或资源恢复绝不启动 provider 工作或发布空的 requeue 请求。
- **durable 领域证据 vs resume address**：Fallback cursor 只积分已提交的 root、失败与成功事实，回答当前失败预算与下一物理执行者；它不保存 callback、continuation、待执行动作或 process-local arming。崩溃后恢复 cursor 不得自行恢复流程，仍须重新取得 typed failure licence 与本次 attempt 的 opportunity。
- **恢复 Prompt 身份与精确防重**：Provider 恢复 prompt 的 identity 精确携带 `ProviderRecoveryDecisionId` 与源 `ProviderRunIdentity`，对外可见文本保持完全一致。同一失败事件的重复回放只重入同一持久 claim，绝不发出第二次物理请求；新的失败 provider run 建立新 claim 并发送新的物理请求。
- **provider 健康 vs recovery 槽**：`ModelRouting` 永久 poison 已失败物理 provider；Fallback cursor 只推进 participant/context 槽与预算。两者正交，cursor 不得复活失败 provider，provider 健康表也不得篡改 logical participant identity。

## 违反边界的失败意义

- provider 失败后系统重新选择 Authority、变更 Persona 或改写 system prompt。
- 同一次失败被多个观察者重复记账，导致预算被超额消耗。
- 预算耗尽后系统依然自动发出新的物理请求。
- primed recovery 的主请求已经成功，却把 cursor 永久停在 A′/B′；下一次真实失败因此先被推进到另一侧的普通槽，必须再失败一次才重新获得 recovery opportunity，表现为本可一次处理的 provider failure 成对出现。
- 崩溃后仅凭持久化奇数 Offset 重新构造 armed，导致没有本次 failure advance 的请求错误触发历史压缩。
- 把 cursor 当作恢复程序计数器，凭 Offset 直接重建 continuation、跳过 failure policy 或重发物理请求。

## DEPENDS ON

- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`
