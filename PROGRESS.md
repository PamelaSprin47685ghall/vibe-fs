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

- `node scripts/check.mjs` 绿：672 WHAT / 3257 tests 闭合，fsharp-control-pyramid=0。
- `node scripts/build.mjs` 绿：144 registered surfaces。
- `node requirements/verification-system/tests/run.mjs` 绿：3241/0。
- integration harness 275/0；distribution package suites 全绿。
- focused：message-visibility、Persona、routing、Bookkeeper/G6、SyncDelegate、manager plugin contract 全绿。
- `npm run format-build-test` 完整发布阶梯 exit 0；`npm pack --dry-run` 产出 0.8.3、1829 files。

## e2e Long Stroke 已修复

- 第一根因不是 `ModelRouting` 容量：preflow `prompt_async` 只把 agent 写进 session create，未写进物理 user message。c6ecba617 后 Authority 边界正确 fail-closed → 无 `AuthorityRootAccepted` / `SessionPersona` → `NeedHelpSensor` 不接管 sentinel abort。preflow 现显式携 agent。
- 显式 Authority 暴露三条真实 Host 缺口：
  - managed child / internal lane 已继承 Persona，却在自己的 fast Authority profile 上重算 Persona；`CreateChildSession` / sibling lane 现先继承，`SessionExecutionBinding` 区分 internal root，Authority 只消费冻结身份。
  - staged Inspector 在 owner ReuseScope `CaseFinalize` 前被物理删除并丢 Persona/ProviderLanguage；identity 现保留到 finalization，Bookkeeper 用无 physical parent 的 sibling lane，完成后立即 drop。
  - Enforcer `StopPhysicalRun` 在 messages transform 内 await 同一 Host abort，形成 self-deadlock；现 fire-and-observe，abort `Ok` 后用 exact trailing `PhysicalUserMessageId` 释放 lease。
- 原 Long Stroke finality script 只发首个 judge 后 terminal，永远没有同一 physical prompt 的第二个 PERFECT → unconfirmed reviewer roster 线性增长、enlist facts 二次增长。fixture 现首轮 REVISE 后，后续每轮同 physical prompt 发送两个 PERFECT，再 terminal；runaway ceiling 从 785/3750 收紧到 700/3350。
- G2 删除 canary 先显式 retire 两个 Companion Blogger，避免在 active Host stream 上递归 delete。
- trace-free 完整 Long Stroke 连续全绿：journal 622–647，SSE 3118–3213；完整发布阶梯终验 629/3136，均低于 700/3350 ceiling。focused Persona 8/0、routing 14/0、Bookkeeper/G6 18/0。

## 发布闭环

- OBL-001A–F focused proof 132/0；原 11 个 authoritative failures 清零，已从 AGENTS.md 义务账删除。
- 未实现的 requirement-grounding 材料移入 `proposals/requirement-grounding/`；active requirements 仅保留可执行闭环。
- 本文件对应的 release commit 由 tag `v0.8.3` 标识。
