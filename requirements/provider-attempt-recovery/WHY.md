# provider-attempt-recovery — WHY

单次 provider attempt 已确认失败后，系统必须能在不重新选择 Authority、不改变 participant 身份的前提下有界地更换物理执行绑定继续，同时防止无限自动消耗资源。

**provider-attempt-recovery 保证：一次已确认的 provider attempt 失败之后，系统明确下一步如何切换执行者、最多重试几次，以及何时彻底停止。**

## 核心不变量与张力

- **确认失败 vs 进程失忆**：attempt 失败是业务层已由快照确认的失败（与 `crash-reconciliation` 的进程失忆完全区分），必须通过单一 ledger 推进 cursor。
- **无界侧循环 vs 有界自动预算**：A/A/B/B 侧循环本身无界，但自动恢复预算严格有界（达到预算后停止自动请求，需等待新 Authority Root 或显式动作）。
- **换执行者 vs 不换身份**：Fallback 仅改变下一次物理执行的 EffectiveAgent；同一 durable logical participant run 的 `ParticipantIdentity`、Persona、语言、system prompt、CanonicalRole 与 Authority identity 全程不变。
- **失败分类 vs 恢复许可**：本包不解析异常或错误文案。只有 `execution-failure-policy` 已分类并授权的 `ProviderTransient | ProviderPermanent` 才能进入 retry/fallback；其他失败类别必须在各自 owner 结算。

## 违反边界的失败意义

- provider 失败后系统重新选择 Authority、变更 Persona 或改写 system prompt。
- 同一次失败被多个观察者重复记账，导致预算被超额消耗。
- 预算耗尽后系统依然自动发出新的物理请求。
- primed recovery 的主请求已经成功，却把 cursor 永久停在 A′/B′；下一次真实失败因此先被推进到另一侧的普通槽，必须再失败一次才重新获得 recovery opportunity，表现为本可一次处理的 provider failure 成对出现。
- 崩溃后仅凭持久化奇数 Offset 重新构造 armed，导致没有本次 failure advance 的请求错误触发历史压缩。

## DEPENDS ON

- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`
