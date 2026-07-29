# STATUS/00-current — 当前位置

本文件只回答一个问题：现在在哪一步，下一步做什么。

不放测试数字、不放阻断列表——那些会立刻过期。机器输出在 `evidence/`，
工作包状态在 `shock-anneal.md`，条款合规在 `conformance.md`。

## 当前阶段

封炉（阶段 0）完成。SSOT 冻结于 tag `ssot-freeze-0.5.0`。

分支 `refactor/ssot-shock-anneal`，基线 commit `274a30aa`。

## 下一步

休克一（阶段 1），工作包 0：Identity 与基础类型。

```text
next/Kernel/Identity.fs + next/Domain/
→ 建立 typed identity，消灭裸 string 身份
→ 破坏面最大，必须最先做
```

条款：PROMPT-008、ARCH-006、EXEC-009、ORCH-006。

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
