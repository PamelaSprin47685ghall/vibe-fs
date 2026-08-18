# PROGRESS — 2026-08-18 0.8.3 发布线

## 已完成（已提交 master）

| commit | 内容 |
|---|---|
| bbecb2c19 | 依赖更新:bun-pty ^0.4.10、gpt-tokenizer ^4.0.0、@opencode-ai/plugin ^1.18.18、opencode-ai 1.18.18、smol-toml 1.8.0;删除零引用 eventsource;canary 指纹同步(Settings.fs) |
| ba347d3bf | fantomas 6.3.0 normalization(post-0.8.2 漂移,48 文件纯格式化) |
| 4806f06a9 | feat: provider run binding 的 projection catch-up 事件驱动化(MessageVisibilityHub:message.updated 唤醒 + ITimerPort deadline backstop) |
| dc507948f | fix: fsharp-control-pyramid 门禁压平 + 类内 let private 编译错误 |
| 369133be6 | chore: bump 0.8.3(package.json/lock、CHANGELOG、README) |

外部并行提交:ccee00634(docs,非本线)。

## 根因记录(Fable delay 缺口)

- Fable 5.13.0 把 `Task.Delay` 编译成 `import { delay } from fable-library-js/Task.js`,但其自带 JS lib **没有** `delay` export → dist 模块图整体不可加载(node ESM link 期失败)。
- 该缺口由 1dcac8cea(有界重读)引入,使 master 上所有依赖 Wire.js 的模块不可用;AGENTS.md 义务账记录的"11 known fail"实际是这同一根因的级联,修复后 authoritative suite **3235 passed / 0 failed**。
- Fable 与 @fable-org/fable-library-js 均已是最新(5.13.0 / 2.5.1),无法靠升级消除;按用户指示用 JS 原语自造等待,最终形态为事件驱动 hub(非轮询)。
- TIME-004 合规:raw timer token 不进业务层;等待唤醒 = message.updated 事件,deadline 仅 backstop,由 OpenCode/Host 物理层持有。

## 验证状态

- `node scripts/check.mjs` 绿(含 fsharp-control-pyramid)。
- `node scripts/build.mjs` 绿(144 registered surfaces,+MessageVisibilitySurface)。
- `node requirements/verification-system/tests/run.mjs` 绿:3235/0。
- integration + distribution package suites 绿。
- focused:message-visibility.test.mjs(事件唤醒/无信号 deadline/跨 session 隔离/waiter 不泄漏)+ host010 全绿。

## 阻塞:e2e Long Stroke 红(pre-existing,非本线引入)

- 现象:10 个 mock provider 请求后停滞,watchdog 5s silence;blocked expectation = blogger.0;journal 尾部 BloggerRequestMaterialized ×2 后无 provider dispatch。
- 10 个请求时序实测 trace:
  1. `strength-canary-title.0` (strength-canary-owner)
  2. `strength-canary-replica.0` (replica 0)
  3. `blogger.0` attempt 1 (strength companion blogger, chronicle tool-call)
  4. `blogger.0` attempt 2 (chronicle tool-call)
  5. `strength-canary-replica.1` (replica 1)
  6. `blogger.0` attempt 3 (chronicle tool-call)
  7. `strength-canary-owner.0` (owner 完成)
  8. `needhelp-owner-title.0` (needhelp-owner title)
  9. `needhelp-owner-fast.0` (needhelp fast 启动并被 NeedHelpSensor 命中 sentinel abort)
  10. `blogger.0` attempt 4 (needhelp companion blogger, chronicle tool-call)
- 根因排查进展:
  - `needhelp-owner-fast.0` 命中 sentinel 后触发 `sessionPort.AbortSession`，产生 `AbortWake`（`context.Quiescence = None`）。
  - `withFreshAssistanceQuiescence` 在 `AbortWake` 时先 mark owner claim 并延迟到 `IdleWake` 执行 `sendEscalationContinuation`。
  - 同期 `needhelp-owner` 的 transform 触发 `CompanionTransform` 启动了新的 Blogger session，派发了 `blogger.0` attempt 4。
  - Blogger attempt 4 调用 `chronicle` 成功后由 `EnforcerHost.handleContinuation` 给出 `StopPhysicalRun` 并 abort 该 Blogger session。
  - 待排查点：`needhelp-owner-fast.0` 的 `SessionIdle` 事件到达后，`AssistanceHost` 的 `sendEscalationContinuation` 派发 `needhelp-owner-deep.0`（或 `ModelRouting.acquireManagedExecution`）与 Blogger 停机之间的交汇状态。
- bisect 定位:first bad = `c6ecba617` "Fix recovery and execution lease boundaries"。

## 待办

- [ ] 深入定位 c6ecba617 中 `AssistanceHost.sendEscalationContinuation` / `ModelRouting` / Blogger 停止在 `needhelp` 边界处的调度停滞根因并修复。
- [ ] `npm run format-build-test` 全绿(e2e 段)后打 tag v0.8.3。
- [ ] AGENTS.md 义务账:OBL-001 的 11 fail 已随根因修复清零,验收后可勾销对应条目。
