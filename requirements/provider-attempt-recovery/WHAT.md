# provider-attempt-recovery — WHAT

## PAR-001: Fallback 属于 Logical Run

Fallback 是 Logical Run 的生命周期状态，而非 Session 的永久属性或模型槽位。新的 Authority Root 开启全新的 Fallback 生命周期（Offset 归零，A 侧由 SelectedAgent 承接）。跨 Logical Run 严禁继承上一次的连续失败计数或侧边状态。

## PAR-002: Cursor 是 modulo-4 封闭 DU，损坏字节 fail-closed

FallbackOffset 仅存在 `Fork0 | Fork1 | Fork2 | Fork3` 四个合法取值。反序列化遇到非法字节必须返回解码错误并拒绝损坏的 envelope，严禁抛出未捕获异常或将解码失败伪造为提交未知。

## PAR-003: 唯一写入口与同一失败只推进一次

FallbackLedger 是唯一允许提交 `FallbackCursorAdvanced` 与 `FallbackExhausted` 的写入口。同一已确认失败（基于 SessionId、LogicalRunId、AuthorityRootUserMessageId 与 ProviderRun 唯一去重）最多推进 cursor 一次；重复观察直接幂等吸收，不写新事实、不推进游标。

## PAR-004: 推进不变量

任意一次已确认失败将 Offset 沿模 4 环前进一格且使连续失败计数加 1；主请求成功写入 `FallbackSucceeded` 事实并将连续失败计数归零，但 Offset 保持停留在当前位置（可停于奇数 Offset）。

## PAR-005: 有限自动恢复预算

A/A/B/B 侧循环在结构上无界，但自动恢复预算严格有界（默认为 12 次连续失败）。连续失败达到预算时写入 `FallbackExhausted` 并停止自动发出物理请求，后续恢复必须依赖新 Authority Root 或用户显式动作。

## PAR-006: 侧序列与预算的维度分离

Offset 每次失败前进一格（映射至 A/A′/B/B′ 循环）。第 12 次连续失败落在 Offset=3 并前进至 0，此时立即判定为 final 耗尽，严禁自动发起第 13 次请求。

## PAR-007: Fold 拒绝条件

FallbackProjection 必须严格拒绝不合法的游标跃迁：非法的上一 Offset、非模 4 后继的下一 Offset、非单调递增的失败计数、超出预算的计数，以及已耗尽后的再次推进。拒绝必须 fail-closed 停止重放。

## PAR-008: 空 / XML-only terminal 不计入推进

空 terminal 或纯 XML terminal 属于回应内容不可用而非 provider 请求失败：最多触发一次有界的 Interaction Repair，严禁推进 fallback cursor 或消耗 provider 失败预算。

## PAR-009: Host Attempt 不是领域计数

Host 传输层的重试序号是宿主内部状态，不得写入领域连续失败计数，不得用于预算判定或 Offset 推导。

## PAR-010: 槽内维护子请求

一个已确认失败先通过 `FallbackLedger` 推进到下一槽；若该新槽为 primed Blogger 槽且存在 durable frames，则该槽的第一物理请求必须是维护子请求 `BloggerSquash`，严禁先重发 BloggerMain。Squash 成功不清零失败计数，并在同槽继续 `BloggerMain`；Squash 失败即结束该槽、记录下一次 advance，严禁在同一槽继续 Main。没有 squash 材料的槽直接发送 Main。每个失败槽恰好产生一次 `FallbackCursorAdvanced`。

## PAR-011: typed recovery opportunity 与 parked-cursor 陷阱

`RecoveryOpportunity` 仅由“本次已确认失败刚刚 advance”与“新 Offset 为 primed”共同产生；材料资格由后续候选/frames 证明，不得提前折叠进 opportunity。Opportunity 只属于紧随该 advance 的一个物理 attempt，发送普通请求也会消费它；新 Run 或崩溃重启后安全归零。严禁仅凭持久化奇数 Offset 重新 arm，严禁 NoCoverage 后把许可跨 attempt re-arm。

## PAR-012: Host abort / cleanup 残留不计入推进

Host 因 abort 清理将工具调用标记为 interrupted 的残留记录，严禁被当作已确认的 provider attempt 失败，不得推进 cursor 或消耗预算。

## PAR-013: 换 Peer = 换执行者，不换身份

Fallback 推进仅改变下一次执行的 `EffectiveAgent` 与物理模型目标。SessionPersona、SessionProviderLanguage、system prompt、CanonicalRole 与 Authority 身份等在生命周期内保持严格不变，且机器代数严禁泄漏进 provider horizon。

## PAR-014: continuation 只在失败记账后、预算允许时

仅当 Host 已停止自动重试且预算允许时，才允许发送同一 Logical Run 的 continuation。Continuation 发送不触发二次游标推进，不重置计数。

## PAR-015: StrengthReplica 不进 owner 的 FallbackController

StrengthReplica attempt 的成功或失败属于投机调查分支，严禁进入 owner Logical Run 的 FallbackController，不推进游标，亦不清零失败计数。

## PAR-016: RequestKind 决定成功记账

成功终态是否写 `FallbackSucceeded` 必须由可证明的 `ProviderRequestKind` 决定：`WorkMain | BloggerMain` 的有效成功清零连续失败计数；`BloggerSquash | InteractionRepair | StrengthReplica` 不清零。Blogger 的 RequestKind 优先由当前 typed request / `BloggerCycleReceipt` 证明，Continuation kind 由 `AcceptedContinuationIds` 证明；禁止仅凭 Role 或 terminal 文本把维护请求误记为业务成功。

## PAR-017: Blogger retry 必须更换物理绑定

Blogger provider attempt 失败后，旧 `BloggerRequestMaterialized` 必须先以 `BloggerRequestAbandoned` 关闭。任何自动 retry（包括相同 Main context 的 retry、Main→Squash、Squash→下一槽 Main）都必须重新 materialize typed context 并绑定新 `PromptKey`，严禁让旧 PromptKey 跨物理 retry 继续充当所有权证明。
