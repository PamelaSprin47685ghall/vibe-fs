# host-provider-failure-ownership — WHAT

## [001] Host chat retry 为零

Wanxiangshu 成功启用后，`experimental.chatMaxRetries` 固定为 0（`src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs` 的 config hook 强制写入），不受环境变量、用户配置或 Host 默认值覆盖。`WANXIANGSHU_CHAT_MAX_RETRIES` 不是生产配置项。

## [002] 每个 physical provider run 只请求一次

Host 不为同一 ProviderRunIdentity 再发 provider request。压缩、fallback 或更换 channel/provider/family 必须由 execution-failure-policy 授权新的 provider run。

【程序待改】实测 OpenCode/plugin 1.18.29 在配置为 0 时仍对同一失败物理 run 发起第二次上游请求（GAP-144）；Host 侧重试行为尚未符合本条，升级 Host 版本时需复核。

## [003] 呈现能力边界

外部 server-plugin 的 event hook 只能观测已经发布的 session.error，不能拦截 Host 或 Desktop/CLI 原生提示，也不得声称可以。Wanxiangshu 在活跃 provider recovery 期间不发额外 final presentation（`ProviderFailurePresentation.classify` 返回 `Recovery`，无 final）；耗尽后只发一次自有 typed terminal presentation（`Final`）。已进入终态的执行再收到回调返回 `Ignore`。

## [004] 未认领错误仍由 Host 报出

plugin/config/schema/permission/user validation、filesystem/Git/tool contract、未知类别、用户 cancel 和无恢复计划的错误不得被全局吞掉；未知错误保留 Host presentation。旧角色配置（如 `agent.coder`）由 config hook 原样拒绝并交回 Host 调用方，不静默改写。Host session.error 按 execution-failure-policy[009] 交 provider recovery，耗尽后按 [006] 呈现。

## [005] 恢复决策唯一归属

execution-failure-policy 独占 retry、fallback 与 capacity settlement 决策；后续 provider run 必须有其 opaque recovery authorization。plugin event observer、Change Orchestrator 和 Host retry loop 不得另行作出恢复决策或成为第二 writer（`scripts/checks/retry-owner.mjs` 扫描已知越界模式）。

## [006] 耗尽只呈现一次

全部 provider/channel/family capacity 归零或恢复预算耗尽后，写入 typed exceptional terminal，停止后续 provider admission，并且只产生一次 Wanxiangshu final presentation。中间恢复不产生终态呈现。

## [007] Host 兼容性漂移拒绝

兼容基线为 OpenCode 1.18.29（`package.json` 的 `opencode-ai` 与 `@opencode-ai/plugin` 均钉住该版本）。升级 Host 或 plugin 版本前，必须重新核验 `chatMaxRetries` consumer 与 session.error 的 producer→SDK→Desktop/CLI 链路仍符合 [001]–[006]；核验不过不得升级。版本字符串本身不证明行为，链路要以实测为准。
