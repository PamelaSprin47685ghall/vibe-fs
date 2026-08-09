# Review — 证明

行为：`what/review.md`。所有权：`shape/review.md`。程序：`how/review.md`。

## 因果 PERFECT

| 证明 | 期望 | 条款 |
|------|------|------|
| 单次 PERFECT 不足 | 无 Confirmed | REVIEW-003 |
| 第二次含 challenge digest（seal） | Confirmed | REVIEW-003、REVIEW-010 |
| 同 ProviderRun 额外 PERFECT | 不计数 | REVIEW-004 |
| REVISE | 立即清未完成 PERFECT/关闭 cohort；延迟 `BlogEntryCommitted` 前不得写 `FinalityRejected` | REVIEW-002、GLORY-044/072 |
| tree 变化 | pending 拒绝；confirmed 对 Guard 无效 | REVIEW-008 |
| 8 大代码质量支柱评估 | Reviewer 必须在 formal report 给出 8 维评估且通过方可 PERFECT | REVIEW-011 |
| Reviewer 提示词权威资源 | `resources/prompts/reviewer-system.md` 承载 8 维质量支柱与工具规范，且不含双 PERFECT 流程 | REVIEW-012 |


## Seal / Witness

| 证明 | 条款 |
|------|------|
| 绑定 0 或 ≥2 assistant → 不写 seal | REVIEW-010、HOST-010 |
| Witness 自包含，Guard 无外围 Map | REVIEW-006 |
| confirmed 只能派生不能赋值 | REVIEW-006 |

代表：`tests/unit/review/witness.test.mjs`；e2e `reviewer-verdict.test.mjs`。

## Guard 顺序

JoinGuard 优先于其它 Manager completion 分支（EXEC-016）；Manager completion 不检查 review
witness，Manager 面无 Review Guard（REVIEW-007、GLORY-070）。`ReviewerWorkflow` 是 ReviewerGuard /
ReviewConfirmation 唯一 writer；durable REVISE 关闭 cohort 后不补发 challenge，record-ready 等待不重开该路径。代表：
`tests/unit/reconciliation/turn-completion-program.test.mjs`、
`tests/unit/execution/finality-cohort-law.test.mjs`、e2e `reviewer-verdict.test.mjs` 与
`temporal-ownership-unhappy-path.test.mjs`。
Post-rebase 必须新双 PERFECT（REVIEW-009、ORCH）。
