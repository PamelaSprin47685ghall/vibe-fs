# host-provider-failure-ownership — WHAT

## HOSTFAIL-001: Wanxiangshu enabled 时 Host chat retry 固定为零

plugin 成功启用后必须无条件把 `experimental.chatMaxRetries` 设为 0。环境变量、用户配置与 Host 默认值不能覆盖；`WANXIANGSHU_CHAT_MAX_RETRIES` 不再属于生产 contract。

## HOSTFAIL-002: 每个 physical provider run 只发起一次上游请求

Host 不为同一 ProviderRunIdentity 发起第二次 provider request。后续压缩、fallback、换 channel/provider/family 只能由 ExecutionFailurePolicy 授权新的 provider run。

## HOSTFAIL-003: 恢复期不发射额外呈现且不声称越界 UI 抑制

校正 HOSTFAIL 虚假声明：外部 server-plugin 的 `event` 钩子仅在 EventV2Bridge 发布之后被动观测，无法在物理上拦截或抑制上游已发布的 `session.error` 或 Desktop/CLI 原始界面提示。严禁声称具有不存在的 UI 拦截能力。Wanxiangshu 能且只能保证：在内部 provider recovery 处于活跃恢复期时，自身发射零额外 final presentation；仅在所有恢复尝试确定性耗尽时，才发射恰好一次由 Wanxiangshu 拥有的 typed terminal presentation。

## HOSTFAIL-004: 非认领错误保持 Host 默认 fail-loud

plugin/config/schema/permission/user validation、filesystem/Git/tool contract、unknown class、用户 cancel 与无恢复计划的错误不得被全局吞掉。未知错误默认使用 Host presentation。

## HOSTFAIL-005: provider recovery 只有一个 durable owner

ExecutionFailurePolicy 是 retry/fallback/capacity settlement 的唯一决策 owner；只有其 opaque recovery authorization 能启动后续 provider run。Plugin event observer、Change Orchestrator 与 Host retry loop 不得成为第二 writer。

## HOSTFAIL-006: capacity exhaustion 只产生一个 final presentation

全部 provider/channel/family capacity 归零或恢复预算耗尽时写 typed exceptional terminal，停止后续 provider admission，并由 Wanxiangshu 产生恰好一次最终终态呈现（final presentation）；恢复中间状态保持静默，绝不重复生成中间终态呈现。

## HOSTFAIL-007: OpenCode Host 版本漂移 fail closed

兼容基线固定 OpenCode 1.18.29。gate 必须验证 chatMaxRetries consumer 与 session.error presentation producer→SDK→Desktop/CLI 链路；版本或 owner 漂移时失败并要求重新审计，不允许静默跳过。
