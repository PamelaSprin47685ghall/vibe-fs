# STATUS/00-current.md — 当前唯一入口

本文件只回答一个问题：现在在哪一步，下一步做什么。

不放测试数字、不复制完整阻断列表。机器证据在 `evidence/`，工作包状态在
`shock-anneal.md`，条款合规在 `conformance.md`。

## 当前阶段

封炉、休克一至三、退火一与退火二、剧本森林、因果推进门禁、ARCH-010 N0–N5b 均已落地。
退火三进行中：`orchestrator-restart-publish` 的 durable recovery 已修复（5 个生产缺陷），
进入 canary 森林收尾与发布门禁。

分支 `refactor/ssot-shock-anneal`，封炉基线 `274a30aa`，最近生产修复 `783caf3b`。

当前已知：

- `orchestrator-restart-publish` 单 canary 3/3 绿（修复链：
  seal tool-result 解码、sweep `+` 标记、fork 首 prompt 信封、已确认 barrier 不重开挑战、
  终端双投递去重；证据 `evidence/orchestrator-restart-recovery-fixes.md`）；
- `test:mjs` 442 绿、`test:harness` 278 绿、`gate:static` 全过；
- P0 一轮 12/15：残留 = restart-publish（guard 轮 flake）、orchestrator-publish
  （bindChild/join 竞争）、conflict（post-publish blogger seal 残留，Host 系统组装行为，
  见证据文档 §残留）。

## 下一步

canary 森林收尾，按优先级：

1. guard 轮 flake（`manager-guard.2` 偶发超时 + `orchestrator-publish` 的
   bindChild/join 竞争）——同属运行期时序，调查 guard 双触发与 join 消费竞争；
2. conflict canary 的 post-publish blogger seal 残留——评估 canary 侧处理；
3. 上述清零后按 VERIFY-002：P0 单轮 → P0×3 → `test:release`。

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
