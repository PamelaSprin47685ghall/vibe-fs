# Companion — 证明

行为：`what/companion.md`。边界：`shape/companion.md`。投影：`how/companion.md`。

## 结构

| 证明 | 条款 |
|------|------|
| 每 Work Session 恰好一 Y | COMPANION-001 |
| Y 不递归 | COMPANION-002 |
| 无 eligibility 白名单 | COMPANION-001 |

## OpeningMaterial / WorkRecord（COMPANION-003/014/015）

| 证明 | 期望 | 条款 |
|------|------|------|
| OpeningMaterial = preserved 区间 | 恰为 XTrace `[work start, OpeningBoundary)`；禁 `OpeningPromptRaw` / Assignment 拼接重建 | COMPANION-014 |
| OpeningBoundary | = WorkRecordStart；BlindPlan 下含 T1 call + canonical accepted result | COMPANION-014、TODO-001/015 |
| 三段标题 | 仅 `Opening` / `Chronicle` / `Recent work`；旧四标题与 `Closing report` **absent**、无 alias | COMPANION-003、COMPANION-015 |
| T1 constitutive | BlindPlan T1 call/result 留在 Opening；不得当 incidental tool 滤入 Recent work | COMPANION-014 ⑨、TODO-015 |
| Opening 永 raw | 永不进 Y / 永不 prefix-replace；survives compaction / reanchor / recovery | COMPANION-014、TODO-001 |
| includeOpening | 父→子 true；子→父 / process / Finality / SyncDelegate false | COMPANION-015 ⑦、TODO-008 |
| Recent work 末条助手文本 | prose claim；禁固定字段 schema；无独立 Closing 段 | COMPANION-015、ARCH-015、EXEC-031 |
| LWR 无 raw tool | 禁 call/result linkage（T1 Opening 材料除外） | COMPANION-003 |

§17.1 / §19：`WorkRecord has only 3 sections`；`Manager BlindPlan Opening never compressed`；`T1 commitment in Opening`。

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
