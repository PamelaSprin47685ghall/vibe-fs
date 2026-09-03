# provider-attempt-recovery — WHAT

## PAR-001: Fallback 属于 Logical Run

Fallback 是 Logical Run 的生命周期状态，而非 Session 的永久属性或模型槽位。新的 Authority Root 开启全新的 Fallback 生命周期（Offset 归零，A 侧由 SelectedAgent 承接）。跨 Logical Run 严禁继承上一次的连续失败计数或侧边状态。

## PAR-002: Cursor 是 modulo-4 封闭 DU，损坏字节 fail-closed

FallbackOffset 仅存在 `Fork0 | Fork1 | Fork2 | Fork3` 四个合法取值。反序列化遇到非法字节必须返回解码错误并拒绝损坏的 envelope，严禁抛出未捕获异常或将解码失败伪造为提交未知。

## PAR-003: 唯一写入口与同一失败只推进一次

FallbackLedger 是唯一允许提交 `FallbackCursorAdvanced` 与 `FallbackExhausted` 的写入口。同一已确认失败（基于 SessionId、LogicalRunId、AuthorityRootUserMessageId 与 ProviderRun 唯一去重）最多推进 cursor 一次；重复观察直接幂等吸收，不写新事实、不推进游标。

## PAR-004: 推进不变量与首次失败永久摘要上下文替换

不再使用 AA'BB' 重试法。任意一次已确认失败将使连续失败计数加 1，并永久标记当前 provider 为失败（在进程生命周期内所有 `wanxiangshu.mjs` 指定的该 provider 容量均视为 0）。首次失败立即永久替换为 blogger 摘要上下文重试（开启新 prefix epoch，同一 epoch 内保持前缀不可变）。成功写入 `FallbackSucceeded` 事实并将连续失败计数归零。

## PAR-005: 理论容量耗尽判定与有限自动恢复预算

重试在候选池中切换至其它具备非零容量的 provider（使用已替换的摘要上下文）。当该角色在 `wanxiangshu.mjs` 中的所有 candidate provider 理论容量全部腾出后仍均为 0 时，或者连续失败达到自动恢复预算上限时，判定为容量耗尽并写入 `FallbackExhausted`，停止自动发出物理请求。

## PAR-006: 失败轮换与容量归零维度分离

失败轮换不依赖模 4 侧序号，而是依据 `wanxiangshu.mjs` 中的可用 provider 候选列表。每当一个 provider 发生物理失败，其容量在当前进程生命周期内归零，由调度器自动转向下一可用 provider。

## PAR-007: Fold 拒绝条件

FallbackProjection 必须严格拒绝不合法的游标跃迁：非法的上一 Offset、非模 4 后继的下一 Offset、非单调递增的失败计数、超出预算的计数，以及已耗尽后的再次推进。拒绝必须 fail-closed 停止重放。

## PAR-008: 空 / XML-only terminal 不计入推进

空 terminal 或纯 XML terminal 属于回应内容不可用而非 provider 请求失败：最多触发一次有界的 Interaction Repair，严禁推进 fallback cursor 或消耗 provider 失败预算。

## PAR-009: Host Attempt 不是领域计数

Host 传输层的重试序号是宿主内部状态，不得写入领域连续失败计数，不得用于预算判定或 Offset 推导。

## PAR-010: 槽内维护子请求

一个已确认失败先通过 `FallbackLedger` 推进到下一槽；若该新槽为 primed Blogger 槽且存在 durable frames，则该槽的第一物理请求必须是维护子请求 `BloggerSquash`，严禁先重发 BloggerMain。Squash 成功不清零失败计数，并在同槽继续 `BloggerMain`；Squash 失败即结束该槽、记录下一次 advance，严禁在同一槽继续 Main。没有 squash 材料的槽直接发送 Main。每个失败槽恰好产生一次 `FallbackCursorAdvanced`。

## PAR-011: typed recovery opportunity、精确物理绑定与 stale-primed 陷阱

`RecoveryOpportunity` 仅由“本次已确认失败刚刚 advance”与“该次 advance 得到的新 Offset 为 primed”共同产生；`FallbackLedger` 必须把这一本次 advance 的 typed opportunity 作为 admission 结果向后传递，后继 workflow 严禁再次读取 durable cursor 奇偶来重建 opportunity。材料资格由后续候选/frames 证明，不得提前折叠进 opportunity。Opportunity 只属于紧随该 advance 的一个物理 attempt，发送普通请求也会消费它；新 Run 或崩溃重启后安全归零。WorkMain recovery 的 process-local permit 只能在 Host 已持久确认 `ProviderRetryAttempt` 的 `PhysicalUserMessageId` 后建立，并且消费时必须与当前 transform 的 exact physical user id 相等；仅凭 `SessionId` presence、发送意图或尚未被 Host 接受的 PromptKey 均不得领取该许可。正常成功会按 PAR-004 关闭 primed 子槽；但进程可能在 failure advance 后、成功结算前崩溃，因此 durable odd Offset 仍不能单独证明当前请求 armed。严禁仅凭持久化奇数 Offset 重新 arm，严禁 NoCoverage 后把许可跨 attempt re-arm。

## PAR-012: Host abort / cleanup 残留不计入推进

Host 因 abort 清理将工具调用标记为 interrupted 的残留记录，严禁被当作已确认的 provider attempt 失败，不得推进 cursor 或消耗预算。

## PAR-013: 换 Provider = 换执行者，不换身份

Fallback 轮换仅改变下一次执行的物理 provider/model 目标。同一 durable logical participant run 的 immutable `ParticipantIdentity`（包括本名 Role、稳定 Persona 与 provenance/version）、SessionProviderLanguage、system prompt、CanonicalRole 与 Authority identity 在所有 retry/fallback attempt 中保持严格不变；Persona 与 provenance/version 必须从该 run 的 durable identity evidence 继承。只有该 run 已 exact terminal closure 后建立的新 run 才能取得新 identity，且机器代数严禁泄漏进 provider horizon。

## PAR-014: continuation 只在失败记账后、预算允许时

仅当 Host 已停止自动重试且预算允许时，才允许发送同一 Logical Run 的 continuation。Continuation 发送不触发二次游标推进，不重置计数。

## PAR-015: StrengthReplica 不进 owner 的 FallbackController

StrengthReplica attempt 的成功或失败属于投机调查分支，严禁进入 owner Logical Run 的 FallbackController，不推进游标，亦不清零失败计数。

## PAR-016: RequestKind 决定成功记账

成功 provider attempt 是否写 `FallbackSucceeded` 必须由可证明的 `ProviderRequestKind` 决定：`WorkMain | BloggerMain` 的有效成功清零连续失败计数；`BloggerSquash | InteractionRepair | StrengthReplica` 不清零。`finish=tool-calls` 是 provider attempt 的有效成功而非失败/未完成；它可以在 Host turn 继续执行工具的同时结算本次 provider recovery。Blogger 的 RequestKind 优先由当前 typed request / `BloggerCycleReceipt` 证明，Continuation kind 由 `AcceptedContinuationIds` 证明；禁止仅凭 Role 或 terminal 文本把维护请求误记为业务成功。

## PAR-017: Blogger retry 必须更换物理绑定

Blogger provider attempt 失败后，旧 `BloggerRequestMaterialized` 必须先以 `BloggerRequestAbandoned` 关闭。任何自动 retry（包括相同 Main context 的 retry、Main→Squash、Squash→下一槽 Main）都必须重新 materialize typed context 并绑定新 `PromptKey`，严禁让旧 PromptKey 跨物理 retry 继续充当所有权证明。

## PAR-018: recovery continuation 只由 durable 事件解锁

WorkMain 失败进入 primed recovery opportunity 后，若 linked Blogger 已有 durable open request 且尚无严格更新的 prefix coverage，则 recovery continuation 必须等待该 journal stream 的下一次已提交事实并重算条件；open request 的 commit/abandon 或 coverage advance 是唯一解锁事件。禁止 timer、deadline、sleep、polling、process-local flight/pending 状态参与该等待。若没有 durable open producer，则立即进入本次物理 retry，不等待未来材料。

## PAR-019: 只消费 typed provider recovery licence

FallbackController 只接受 `execution-failure-policy` 针对 `ProviderTransient | ProviderPermanent` 产生的 typed `RetryFreshAttempt` / `AdvanceFallback` licence；licence 必须绑定 exact `ProviderRunIdentity`、request kind 与 policy decision identity，controller 只验证当前 attempt 匹配，不重复计算 budget、breaker 或 failure class。`LocalInvariant`、`ProtocolRejection`、`AuthorizationDenied`、`UserCancelled`、`Superseded`、`CapacityQueueFull`、`AcceptanceUnknown`、`StreamInterruptedAfterFirstToken` 与任一 `PersistenceFailure` 均不得推进 cursor、消耗 provider 失败预算或创建 retry/fallback attempt。严禁 wildcard retry、按异常/terminal 文本重分类，或将未决 acceptance 当作 provider failure。
