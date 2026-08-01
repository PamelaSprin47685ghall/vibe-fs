# STATUS/00-current.md — 当前唯一入口

本文件只回答一个问题：现在在哪一步，下一步做什么。

不放测试数字、不复制完整阻断列表。机器证据在 `evidence/`，工作包状态在
`shock-anneal.md`，条款合规在 `conformance.md`。

## 当前阶段

封炉、休克一至三、退火一与退火二、剧本森林、因果推进门禁、ARCH-010 N0–N5b 均已落地。
退火三已完成：canary 森林 16/16 全绿，`test:release`（gate:static → build → unit →
harness → P0×3）完整通过。

分支 `refactor/ssot-shock-anneal`，封炉基线 `274a30aa`，最近生产修复 `71763142`。

当前已知：

- `orchestrator-restart-publish` 单 canary 3/3 绿（修复链：
  seal tool-result 解码、sweep `+` 标记、fork 首 prompt 信封、已确认 barrier 不重开挑战、
  终端双投递去重；证据 `evidence/orchestrator-restart-recovery-fixes.md`）；
- P0 16/16 全绿，`test:release` 完整通过（证据：本轮 `71763142` 修复链 + P0×3 三轮全绿）。
  三处历史残留全部闭合：
  - `reviewer-restart` 并发红：插件构造期 `PromptRecovery.reconcile` 经 SDK 重入未就绪
    Host → 改为 post-init single-flight `RecoveryGate`（`2a2660be`）；
  - `orchestrator-publish` seal-undeclared：guard 轮 continuation 的 `turn.Directory`
    = worktree，worktree 释放后 instruction 丢失 → `liveDirectory` 回退 root + 
    `TurnInProgress` 在 job 离开 `ManagerStarted` 后直接完成 manager（`71763142`）；
  - teardown 端口泄漏 flake：`terminateChild` 在 `terminateTree` 报 survivors 后
    补一次 SIGKILL（非调大超时，`71763142`）。

## 下一步

SSOT/14-16 已合入（`9b4e931d`），三个方案的纯领域内核已实现并测试
（Strength 28 / Enforcer 39 / StudentTeacher 15 项第 1 层测试；`93c421b7`、
`dd1c0553`、`e52cf2be`）。生产接线被各方案自设的 Host canary 门禁阻断
（STRENGTH-078 / ENFORCER-180 / LEARN-082…088），属后续阶段——先建共享 Host
capability canary 证明 transform 挂起/取消/身份绑定，再逐纵向接线（推荐顺序：
SatelliteRuntime → Projection DSL → Strength shadow → Enforcer → Student/Teacher）。

## 阅读顺序

```text
1. AGENTS.md §1
2. STATUS/evidence/orchestrator-restart-recovery-fixes.md
3. STATUS/evidence/manager-worktree-durable-ownership.md
4. SSOT/06 + SSOT/07
5. STATUS/conformance.md
6. STATUS/shock-anneal.md
```

## 退火三反馈纪律

先跑最小目标测试；该层绿后才扩大。当前可直接运行：

```bash
npm run gate:static
npm run build
node --test tests-mjs/Orchestrator/runtime.test.mjs
WANXIANG_RUN_ID=<run> timeout 180 node testkit/opencode/tests/orchestrator-restart-publish-canary.mjs
```

## 已确立的关键决定

| 决定 | 位置 |
|------|------|
| 测试改 `.mjs`，生产保持 `.fs` | VERIFY-008 |
| 行数门禁废除，Gate 只阻断语义 | VERIFY-005 |
| Architecture Gate 是第 0 层静态检查器 | VERIFY-001、VERIFY-005 |
| REVIEW-010 seal→run 绑定可实现，不需 SSOT 例外 | HOST-010、`evidence/host-transform-run-binding.md` |
| 剧本森林为静态 TOML，禁运行期加载 | VERIFY-003、`design-script-forest.md` |
| Fallback：Offset 循环无界，自动恢复预算有界 | FALLBACK-005 |
| Host `Attempt` 与 `ConsecutiveFailureCount` 是不同的量 | FALLBACK-010 |
| durable worktree 只在终态显式释放；`NeedsReview` 不释放 | ORCH-003、ORCH-006、ORCH-007 |
| fork 首 prompt 一律 ARCH-010 信封，continuation 一律原样 | PROMPT-008、N3（`783caf3b`） |
| 已确认 barrier 的多余 PERFECT 不重开挑战 | REVIEW-003（`783caf3b`） |
