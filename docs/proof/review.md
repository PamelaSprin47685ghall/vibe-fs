# Review — 证明

行为：`what/review.md`。所有权：`shape/review.md`。程序：`how/review.md`。

## 因果 PERFECT

| 证明 | 期望 | 条款 |
|------|------|------|
| 单次 PERFECT 不足 | 无 Confirmed | REVIEW-003 |
| 第二次含 challenge digest（seal） | Confirmed | REVIEW-003、REVIEW-010 |
| 同 ProviderRun 额外 PERFECT | 不计数 | REVIEW-004 |
| REVISE | 清未完成 PERFECT | REVIEW-002 |
| tree 变化 | pending 拒绝；confirmed 对 Guard 无效 | REVIEW-008 |

## Seal / Witness

| 证明 | 条款 |
|------|------|
| 绑定 0 或 ≥2 assistant → 不写 seal | REVIEW-010、HOST-010 |
| Witness 自包含，Guard 无外围 Map | REVIEW-006 |
| confirmed 只能派生不能赋值 | REVIEW-006 |

代表：`tests/unit/review/witness.test.mjs`；e2e `reviewer-verdict.test.mjs`。

## Guard 顺序

JoinGuard 优先于 Review Guard（EXEC-016、REVIEW-007）。  
Post-rebase 必须新双 PERFECT（REVIEW-009、ORCH）。
