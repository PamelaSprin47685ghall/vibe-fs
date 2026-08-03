# 活跃 Blocker 账本（当前为空）

0.5.2 已发布，当前无活跃 blocker。

## 已闭合（SSOT 例外协议）

### HOST-010：transform assistant id ≡ ToolContext.messageID 等式不可观测

- 状态：已闭合（Host 能力限制，ARCH-003 例外协议；非实现困难）
- 触发：Reviewer 第七轮 REVISE 缺陷 1/2——条款原文要求 canary 直接断言该顺序，但等式在 testkit 架构下物理不可观测
- 证据（Host 源码，`../opencode`）：
  - `packages/opencode/src/session/prompt.ts:1255` — transform hook 触发点；id 不落盘到插件 journal
  - `packages/opencode/src/session/prompt.ts:1272` — provider 请求在 transform 之后发送
  - `packages/opencode/src/session/prompt.ts:268` 一带 — assistant message 在 transform 前创建并持久化（绑定判据仍成立）
  - `packages/plugin/src/tool.ts:5` — `ToolContext.messageID` 仅 ctx 内字段，不在 provider wire
  - 两侧同一 run 内不共存于 journal → canary 无法共时比较
- 处置：
  - `SSOT/07.md` HOST-010「脆弱性与门禁」修订为 journal 侧可观测代理等式（rev.2）
  - rev.2 supersedes rev.1 的不可观测直接断言要求
  - 绑定语义保留：seal-bind、唯一未完成 assistant、同一 run
- 层 4 代理 canary（方案 B，已接线）：
  - Reviewer 链：`ReviewVerdictRecorded.ProviderRun == ProviderInputSealed.ProviderRun`（`reviewer-verdict-canary.mjs`）
  - X 链：`PrefixRebaseCommitted.SolvingProviderRun` 唯一非空（`x-recovery-canary.mjs`）
- HOST-011：条款原文要求缺失 → fail closed；层 2 codec 已覆盖；层 4 canary 断言双半边存在性（`BlogEntryCommitted.ProviderRun` + `ToolCallIds`，`host-transform-capability-canary.mjs`）。原文未要求 canary 可观测 fail-closed 路径 → 不修订

## 历史归档

- `docs/archive/shock-anneal-2026/FINAL-REPORT.md` §7（Host compaction 裁决 / HOST-006）
- `docs/archive/shock-anneal-2026/evidence/host-context-recovery.md`
- `docs/archive/shock-anneal-2026/evidence/host-transform-run-binding.md`

未来设计（Strength / Enforcer nudge / Student&Teacher）见 `docs/rfcs/`，不属于当前产品合同。
