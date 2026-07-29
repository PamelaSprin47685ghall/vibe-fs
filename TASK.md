# Wanxiangshu.Next 语义内聚重构进度

更新时间：2026-07-29

## 当前状态

本轮没有完成“全量执行”目标；工作暂停于 OpenCode canary 失败诊断阶段。当前单元/Fable 套件已通过，但 release gate 尚未通过，不能宣称完成。

## 已完成并验证

- 统一 Fallback cursor 主线：`AgentPairCursor` 保持 modulo-4 A/A/B/B；删除非 retry provider-error 路径的 durable cursor 写入；`session.status=retry` 仍是 durable advance 入口。
- Prompt Authority Host 边界收敛：新增 `HostPromptHash.fs`、`PromptIngressCodec.fs`、`PromptMetadataCodec.fs`；Prompt ingress/dispatcher 使用 typed codec；发送继续 `Model=None`。
- Host signal/reconcile：Host Event codec 为唯一 raw event 解码点；碎片事件继续丢弃；reconcile signal match 已覆盖 typed signal。
- Child run：`ChildRun.CompletionCell` 保存完成值，修复 Fable 下读取 `Task.Result` 导致的 undefined；`ForkRuntime` 的物理 child 执行已通过 `AgentProgram`/`ChildRunProgram` Flow。
- Flow DSL：GuideContract 现在引用 `AgentProgram`、`CompanionProgram`、`ReviewProgram`、`OrchestratorProgram`、`ProcessRunner`；语义架构门禁识别 F# escaped builder 名称。
- Companion：busy 判断仍使用 Fable-safe completion cell；没有使用不存在的 Fable `Task.IsCompleted`。
- 语义模块清理：删除孤立 `next/OpenCode/CompanionTransformHelpers.fs`；`AgentRoleHelpers.fs` 改为 `AgentRoleIdentity.fs`；`TerminalPolicyHelpers.fs` 改为 `TerminalPolicy.fs`；`SpikePluginHelpers.fs` 改为 `PluginHostInterop.fs`；更新 fsproj 与生产引用。
- 文档：README 更新到 0.5.0 语义、双 PERFECT witness、无限 A/A/B/B、PromptDispatcher/Model=None、retry 入口、PTY onExit 规则。
- 架构门禁：raw Host interop 扫描限定生产目录；durable fact 单写入口改为 constructor-aware；加入明确 semantic boundary allowlist；TASK §17 gate 当前通过。

## 最近验证结果

通过：

- `dotnet build next/Wanxiangshu.Next.fsproj`
- `dotnet build tests-next/Wanxiangshu.Next.Tests.fsproj`
- `npm run build`
- `npm run test:compile`
- `npm run test:next`：292 passed / 0 failed / 0 skipped
- `node tests-next/runner.js --build-dir build/tests-next`：292 passed / 0 failed / 0 skipped
- ArchitectureGates17：通过

失败：

- `npm run test:release`
  - `fallback-aabb-trace-canary.mjs` 超时，阻塞 `req-a2`、`req-b1`、`req-b2`。
  - `orchestrator-publish-canary.mjs` 失败。
  - `orchestrator-canary.mjs` 失败。
  - `orchestrator-restart-publish-canary.mjs` 失败。
- 单独重复 `CANARY_REPEAT=1 node testkit/opencode/tests/fallback-aabb-trace-canary.mjs` 仍失败。

## 当前未完成根因

最近新增 `ProviderFailureContinuation`，试图处理 Host 停止自动 retry 的 non-retryable provider failure：

- `HostSignal` 增加 `ProviderFailure` / `ProviderFailureWakeup`。
- `HostEventCodec` 解码 `session.error` 为非 durable wakeup；abort error 仍丢弃。
- `HostSignalBootstrap` 监听 failure 并调用同 Logical Run continuation，不推进 cursor。
- 但 canary 日志显示 `session.error` 后仍没有产生下一次 provider request；说明 failure signal 未到达 continuation，或 Dispatcher/authority correlation 在该时机 fail-closed。必须继续定位，不能用放宽权限、直接 `prompt_async` 或伪造 retry 事实绕过。

另一个已观察问题：orchestrator 三个 canary 在多个 chat request 后未达到预期 barrier，需在 fallback 修复后单独重现并读取其完整诊断。

## 下次恢复顺序

1. 先定位 `ProviderFailureContinuation` 为什么未触发下一物理请求：确认 `HostSignalSubscribe -> HostSignalRouter -> HostSignalAdapter -> HostSignalBootstrap` 的实际运行链；优先用 typed diagnostic/测试，不恢复旧 cursor writer。
2. 保持唯一事实规则：failure continuation 只延续同 Logical Run，不写 `FallbackCursorAdvanced`；只有 typed retry 才推进 A/A/B/B。
3. 重跑 fallback canary；随后单独重跑三个 orchestrator canary。
4. 再运行完整：`npm run test:release`，并核对 `npm run test:manager-tools`、`node testkit/opencode/tests/gate-testkit.mjs`、`npm run test:e2e:p0:three`。
5. 只有 release gate 全部通过后，才可将剩余 todo 标记完成并宣布重构完成。

## 重要工作树事实

- 由于 `lsp.rename_file` 曾报告成功但实际未保留目标文件，已手工恢复并创建 `next/Session/AgentRoleIdentity.fs`，并验证 .NET/Fable 编译通过；该工具不一致已报告给 harness。
- `TASK.md` 是本次暂停 checkpoint；不要根据 todo 已完成项推断 release 已完成。
