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
- 证据(dist 插桩):applySessionTransform/applyNonReplicaTransform 进入,planArmedWorkMainRetry 从未进入 → `TryRecoveryArming` 返回 None → armed recovery 从未 arm(HostTurnObserver 的 TurnFailed/TurnAborted 路径未触发或未匹配)。blogger 的 provider 请求在 hook 链上游静默消失。
- 排除:与依赖升级无关(旧 deps + 新代码同败);与 MessageVisibilityHub 无关(bbecb2c19 轮询版同败);v0.8.2 e2e 绿(99 mock-req)。
- 结论:回归在 v0.8.2..6195af001 区间,被 delay 加载 bug 掩盖至今。
- **bisect 定位(隔离 clone /tmp/wxs-clone,8 腿收敛)**:first bad = `c6ecba617` "Fix recovery and execution lease boundaries"(76 文件 +1851/−640:HostSignalBootstrap、SessionExecutionBinding、PluginHooks、PluginTransforms、AgentJournal、新增 FatalProcess.fs / ExplicitResumeSuppression.fs)。其左邻 22ace444c 绿;blogger 修复线 aab402e88 绿。
- 相关高风险区:Blogger 恢复链(aab402e88 / 2c735e240 / b95d92e46 / a0d65c1d7 一带)。

## 待办

- [ ] 修 c6ecba617 引入的 blogger armed-recovery 不触发(起点:SessionExecutionBinding / ExplicitResumeSuppression 对 TurnFailed/TurnAborted 的 arm 路径)。
- [ ] `npm run format-build-test` 全绿(e2e 段)后打 tag v0.8.3。
- [ ] AGENTS.md 义务账:OBL-001 的 11 fail 已随根因修复清零,验收后可勾销对应条目。
