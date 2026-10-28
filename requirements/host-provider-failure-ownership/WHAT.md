# host-provider-failure-ownership — WHAT

## [001] Host chat retry 为零

Wanxiangshu 成功启用后，`experimental.chatMaxRetries` 固定为 0，不受环境变量、用户配置或 Host 默认值覆盖。`WANXIANGSHU_CHAT_MAX_RETRIES` 不是生产配置项。

## [002] 每个 physical provider run 只请求一次

Host 不为同一 ProviderRunIdentity 再发 provider request。压缩、fallback 或更换 channel/provider/family 必须由 execution-failure-policy 授权新的 provider run。

## [003] 呈现能力边界

外部 server-plugin 的 event hook 只能观测已经发布的 session.error，不能拦截 Host 或 Desktop/CLI 原生提示，也不得声称可以。Wanxiangshu 在活跃 provider recovery 期间不发额外 final presentation；耗尽后只发一次自有 typed terminal presentation。

## [004] 未认领错误仍由 Host 报出

plugin/config/schema/permission/user validation、filesystem/Git/tool contract、未知类别、用户 cancel 和无恢复计划的错误不得被全局吞掉；未知错误保留 Host presentation。Host session.error 按 execution-failure-policy[009] 交 provider recovery，耗尽后按 [006] 呈现。

## [005] 恢复决策唯一归属

execution-failure-policy 独占 retry、fallback 与 capacity settlement 决策；后续 provider run 必须有其 opaque recovery authorization。plugin event observer、Change Orchestrator 和 Host retry loop 不得另行作出恢复决策或成为第二 writer。

## [006] 耗尽只呈现一次

全部 provider/channel/family capacity 归零或恢复预算耗尽后，写入 typed exceptional terminal，停止后续 provider admission，并且只产生一次 Wanxiangshu final presentation。中间恢复不产生终态呈现。

## [007] Host 兼容性漂移拒绝

兼容基线为 OpenCode 1.18.29。gate 必须核验 chatMaxRetries consumer 和 session.error 的 producer→SDK→Desktop/CLI 链路；版本或 owner 漂移即失败并要求重新审计，不得跳过。
