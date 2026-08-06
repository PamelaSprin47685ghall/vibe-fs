# Companion — 证明

行为：`what/companion.md`。边界：`shape/companion.md`。投影：`how/companion.md`。

## 结构

| 证明 | 条款 |
|------|------|
| 每 Work Session 恰好一 Y | COMPANION-001 |
| Y 不递归 | COMPANION-002 |
| 无 eligibility 白名单 | COMPANION-001 |

## Coverage / Epoch

| 证明 | 条款 |
|------|------|
| RecordCoverage ≠ PrefixCoverage | COMPANION-003 |
| 同 epoch 前缀字节稳定 | COMPANION-009、ARCH-004 |
| busy/失败不推进 RecordCoverage | COMPANION-008 |
| 仅 BlogEntryCommitted 原子推进 | COMPANION-008、ENFORCER-045 |

## 投影

| 证明 | 条款 |
|------|------|
| 历史由 durable frames 重建，非 raw transcript append | COMPANION-005 |
| LWR 无 raw tool | COMPANION-003 |
| delta vs LWR gap 分投影 | COMPANION-007 |
| synthetic id 确定性 | COMPANION-013 |

代表：`tests/unit/context/companion-projection.test.mjs`、blogger unit、e2e blogger/recovery。
