# STATUS/00-current — 当前位置

本文件只回答一个问题：现在在哪一步，下一步做什么。

不放测试数字、不放阻断列表——那些会立刻过期。机器输出在 `evidence/`，
工作包状态在 `shock-anneal.md`，条款合规在 `conformance.md`。

## 当前阶段

封炉、休克一、休克二、清场、SSOT/12 并入、退火一、退火二、剧本森林与因果推进门禁均已完成。
ARCH-010 的 N0–N5b 已落地。CTX-006 的 X-wire 与 Y squash 生产链路已由 P1–P5 接线闭合
（`blocker-CTX-006.md` 标记解除），`SHOCK-UNMIGRATED` 归零。

当前出口：`dotnet build` 绿（0 warning 0 error）、`npm run test:mjs` 433/433 三时区全绿、
`gate:static` 绿。已具备启动退火三的全部前置。

分支 `refactor/ssot-shock-anneal`，封炉基线 `274a30aa`，最近代码动作 `63e9d5d6`
（`fix(CTX-006): squash failure returns Result so reconcile advances parent cursor`）。

## 下一步

退火三（阶段 7），按序：

```text
1. 包 K9   删除 strict-mock-forest.js / strict-mock-matches.js 旧匹配路径
           （旧 expect* 轨迹须先迁入静态森林或明确淘汰）
2. test:harness                     gate-testkit 全绿
3. canary 修红                      类一（N5 surface）+ 类二（行为债）+ manager-full-loop
4. 包 K8f  X-A–X-D                  恢复剧本接线验收（X-wire 已通，阻断解除）
5. 包 N6   fixture/golden/byte-limit 更新（M5，需全绿 canary 作回归底座）
6. P0×3 → npm run test:release      发布门禁
```

条款：VERIFY-001 六层阶梯，禁止跨级；VERIFY-003 剧本只匹配 provider 真正收到的东西。

## 阅读顺序

```text
1. AGENTS.md §1        动手之前先读规范与状态
2. shock-anneal.md     当前阶段规则 + 工作包 0 的旧入口 / 新入口 / 必须删除
3. SSOT/03 + SSOT/11   身份与持久化条款
4. conformance.md      相关条款当前状态
```

## 休克期反馈纪律

只允许第 0 层：

```bash
node scripts/ssot-lint.mjs
node scripts/shock-audit.mjs
node scripts/strip-doc-bold.mjs
git diff --check
git status --short
```

不运行 `dotnet build`、`npm run build`、`npm run test:next`、任何 canary。

进度指标不是「今天能否编译」，而是 `shock-audit` 的残留数是否下降。

## 已确立的关键决定

| 决定 | 位置 |
|------|------|
| 测试改 `.mjs`，生产保持 `.fs` | VERIFY-008 |
| 行数门禁废除，Gate 只阻断语义 | VERIFY-005 |
| Architecture Gate 迁出测试套件，成为第 0 层静态检查器 | VERIFY-001、VERIFY-005 |
| REVIEW-010 seal→run 绑定可实现，不需 SSOT 例外 | HOST-010、`evidence/host-transform-run-binding.md` |
| 剧本森林整体重建为静态 TOML，禁运行期加载 | VERIFY-003、`design-script-forest.md` |
| Fallback：Offset 循环无界，自动恢复预算有界（默认 12） | FALLBACK-005 |
| Host `Attempt` 与 `ConsecutiveFailureCount` 是不同的量 | FALLBACK-010 |

## 熔断

出现 `shock-anneal.md` 列出的十项任一，立即暂停新增迁移，回到总账重新切分工作包。

已触发次数：0。
