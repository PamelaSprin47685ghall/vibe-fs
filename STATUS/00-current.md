# STATUS/00-current.md — 当前唯一入口

本文件只回答一个问题：现在在哪一步，下一步做什么。

不放测试数字、不复制完整阻断列表。机器证据在 `evidence/`，工作包状态在
`shock-anneal.md`，条款合规在 `conformance.md`。

## 当前阶段

封炉、休克一至三、退火一与退火二、剧本森林、因果推进门禁、ARCH-010 N0–N5b 均已落地。
退火三进行中：单 canary 调查阶段，尚未进入 P0×3 与 `test:release`。

分支 `refactor/ssot-shock-anneal`，封炉基线 `274a30aa`，最近生产修复 `9fcaad24`。

当前已知：

- ORCH-006 durable worktree 被 `NeedsReview` 作用域析构删除的根因已修复；
- `reviewer-restart` 已通过单 canary；
- `orchestrator-restart-publish` 已越过 deep-reviewer / Blogger TOML 声明缺口，现阻断于
  restart 后 `OrchestratorPublished=0`；
- `orchestrator-publish` 尚缺修复后的最终单 canary 绿证据；
- Orchestrator 显式 barrier 与 REVIEW-007 随机 barrier 重叠仍为独立 `UNVERIFIED` 项。

根因与证据：`evidence/manager-worktree-durable-ownership.md`。

## 下一步

只调查 `orchestrator-restart-publish` durable recovery：

```text
restart 前同一 ManagerJobId 的最后 durable progress
→ restart boot 的 activeJobs
→ recoveryAction
→ RecoverManagerJob 是否启动 program
→ terminal verdict 是否进入新 VerdictMailbox
→ OrchestratorPublished 恰好一次
```

禁止继续放宽 TOML、seal 或 assert；禁止用 worktree 文件存在推导 job active。恢复只信 durable
journal projection。

取得该 canary 绿证据后，按 VERIFY-002 顺序继续：

```text
orchestrator-publish 单 canary
→ P0 单轮
→ P0×3
→ test:release
```

## 阅读顺序

```text
1. AGENTS.md §1
2. STATUS/evidence/manager-worktree-durable-ownership.md
3. SSOT/06 + SSOT/07
4. STATUS/conformance.md
5. STATUS/shock-anneal.md
6. PUZZLE.md
```

## 退火三反馈纪律

先跑最小目标测试；该层绿后才扩大。允许并要求编译、mjs、harness 与单 canary，但不得跨过红层。

当前可直接运行：

```bash
npm run gate:static
npm run build
node --test tests-mjs/Orchestrator/runtime.test.mjs
WANXIANG_RUN_ID=<run> timeout 180 node testkit/opencode/tests/orchestrator-restart-publish-canary.mjs
```

单 canary 未绿前，不运行 P0×3 或 `test:release` 宣称交付。

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
