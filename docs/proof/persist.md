# Persist — 证明

行为：`what/persist.md`。边界：`shape/persist.md`。程序：`how/persist.md`。

## 门禁 / Clean-break

| 证明 | 期望 | 条款 |
|------|------|------|
| `unified-store-gate` | feature-ref / schema-version-in-store / git-bypass / student-qa-revival / no-migrator / dual-write 全绿 | PERSIST-001、005、006、011 |
| leave-unread | 种植陈旧 `wanxiangshu-next` NDJSON+blobs 后，EventStore open/append/converge **不读**旧档 | PERSIST-004、005 |
| dumb-server | bare remote 无 Domain 链接；object upload/fetch、two-client merge、lease reject+retry | PERSIST-002、006 |
| Student QA absence | `student-teacher-absence` + gate `student-qa-revival`；无 `StudentQaStore` / QA.md backend | PERSIST-011（空缺） |

代表：`scripts/checks/unified-store-gate.mjs`、`tests/unit/verify/unified-store-gate.test.mjs`、`tests/integration/persist/leave-unread.test.mjs`、`tests/integration/persist/dumb-server.test.mjs`。

**不得再证明**：NDJSON journal 为权威历史、Student QA 私有文件权威、LegacyProjection≡NewProjection、dual-write bridge、旧 schema 猜测迁移。

## Append / CAS / 损坏

| 证明 | 条款 |
|------|------|
| CAS：Absent\|R0 → R1；成功仅 Committed snapshot | PERSIST-002 |
| EventId 已在 root → 冲突路径视为已提交；retry 耗尽且缺席 → fail-closed | PERSIST-003 |
| StorageInvalid 拒绝 fold/启动；不跳过坏 event；DomainConflict ≠ StorageInvalid | PERSIST-003、004 |
| 无 schemaVersion / store-v2 / feature ref | PERSIST-001、005、006 |
| Canonical JSON identity；同 id 异 bytes → collision | PERSIST-001 |

代表：`tests/unit/persist/event-store-append.test.mjs`、`event-store-identity-collision.test.mjs`、`event-store-fold.test.mjs`、`event-store-merge.test.mjs`。

## Projection / Payload / Effect

| 证明 | 条款 |
|------|------|
| 查询 O(1) 积分，不扫全史 | PERSIST-008 |
| payload closure：先 blob 后 event；dangling / 缺 closure fail closed | PERSIST-007 |
| AgentJournal 成功路径无 `.ndjson`、无磁盘 `blobs/` 目录 | PERSIST-006、007 |
| Requested-only 先核对领域物理证据；禁止盲重试；Accepted 不折回 | PERSIST-009 |

代表：`tests/unit/journal/event-store-journal-writer.test.mjs`、`event-store-journal-boot.test.mjs`、`workspace-event-store-host.test.mjs`；orchestrator worktree effect unit。

## 上下文事实 fold

Opening 幂等；XTrace 严格序；BlogEntry 原子 coverage；Squash 不改 IngestCursor；PrefixRebase/ContextReanchored Epoch+1（PERSIST-010）。  
禁止 OverflowDetected 等容量事实。

代表：`tests/unit/context/*`、`tests/unit/journal/*` fold / blog-projection。

## Converge

| 证明 | 条款 |
|------|------|
| Converge 经 GitGateway；双向 objects+ref；merge 后 fold+closure | PERSIST-002、006 |
| 无 Domain 逻辑在 dumb server | PERSIST-006 |

代表：`tests/unit/persist/event-store-converge.test.mjs`、`tests/integration/persist/dumb-server.test.mjs`。
