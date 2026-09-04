# host-provider-failure-ownership — WHAT

## HOSTFAIL-001: Wanxiangshu enabled 时 Host chat retry 固定为零

plugin 成功启用后必须无条件把 `experimental.chatMaxRetries` 设为 0。环境变量、用户配置与 Host 默认值不能覆盖；`WANXIANGSHU_CHAT_MAX_RETRIES` 不再属于生产 contract。

## HOSTFAIL-002: 每个 physical provider run 只发起一次上游请求

Host 不为同一 ProviderRunIdentity 发起第二次 provider request。后续压缩、fallback、换 channel/provider/family 只能由 ExecutionFailurePolicy 授权新的 provider run。

## HOSTFAIL-003: 可恢复 provider error 保留事实但抑制默认重复提示

被 Wanxiangshu 确认认领且有继续恢复动作的 provider/network failure 必须保留真实 session.error/durable evidence，同时在默认 Desktop/CLI presentation consumer 之前带 typed claimed metadata，使默认 toast/sound/notification/CLI error 不重复展示。

## HOSTFAIL-004: 非认领错误保持 Host 默认 fail-loud

plugin/config/schema/permission/user validation、filesystem/Git/tool contract、unknown class、用户 cancel 与无恢复计划的错误不得被全局吞掉。未知错误默认使用 Host presentation。

## HOSTFAIL-005: provider recovery 只有一个 durable owner

ExecutionFailurePolicy 是 retry/fallback/capacity settlement 的唯一决策 owner；只有其 opaque recovery authorization 能启动后续 provider run。Plugin event observer、Change Orchestrator 与 Host retry loop 不得成为第二 writer。

## HOSTFAIL-006: capacity exhaustion 只产生一个 final presentation

全部 provider/channel/family capacity 归零时写 typed exceptional terminal，停止 nudge/successor provider admission，并向用户显示一份 Wanxiangshu-owned final summary；不得同时再弹 Host 原始中间错误。

## HOSTFAIL-007: OpenCode Host 版本漂移 fail closed

兼容基线固定 OpenCode 1.18.18。gate 必须验证 chatMaxRetries consumer 与 session.error presentation producer→SDK→Desktop/CLI 链路；版本或 owner 漂移时失败并要求重新审计，不允许静默跳过。

