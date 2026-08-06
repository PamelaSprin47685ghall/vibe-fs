# Journal — 证明

行为：`what/persist.md`。边界：`shape/persist.md`。程序：`how/persist.md`。

## Append / 损坏

| 证明 | 条款 |
|------|------|
| Append 仅 Committed \| CommitUnknown | PERSIST-002 |
| CommitUnknown → fail-closed | PERSIST-003 |
| 中间损坏拒绝启动；仅尾部可截断 | PERSIST-004 |
| 旧 schema 拒绝 | PERSIST-005 |

## Projection / Effect

| 证明 | 条款 |
|------|------|
| 查询 O(1) 积分，不扫全史 | PERSIST-008 |
| Requested→Accepted 幂等；Accepted 不折回 | PERSIST-009 |
| 时间戳 UTC offset 稳定 | PERSIST-001 |

## 上下文事实 fold

Opening 幂等；XTrace 严格序；BlogEntry 原子 coverage；Squash 不改 IngestCursor；PrefixRebase/ContextReanchored Epoch+1（PERSIST-010）。  
禁止 OverflowDetected 等容量事实。

代表：`tests/unit/journal/*`、`tests/integration/journal/boot.test.mjs`、`domain.meta.test.mjs` 时区断言。
