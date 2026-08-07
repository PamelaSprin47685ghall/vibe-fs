# Journal — 证明

行为：`what/persist.md`。边界：`shape/persist.md`。程序：`how/persist.md`。

## Student QA

| 证明 | 期望 | 条款 |
|------|------|------|
| Git-private 路径与权限 | 不进 worktree/index；目录 0700、文件 0600 | PERSIST-006、PERSIST-011 |
| 原子追加受控反例 | write/rename 中断后只有旧完整或新完整 bytes | PERSIST-011 |
| 顺序故障注入 | 写失败时不发送 Teacher/不交付 Student | PERSIST-011、EXEC-025 |
| fatal UTF-8 | 保留文件、拒绝编译，不跳过坏字节 | PERSIST-011 |
| tail dedupe | 完整尾部相同不重复；不确定则保留 | PERSIST-011 |
| final/cancel delete | absent 幂等；失败无 terminal；unknown orphan 保留 | PERSIST-011 |
| 知识旁路扫描 | Journal/log/metadata 不含 QA 正文、问题或回答 | PERSIST-011 |

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
| Requested-only 先核对领域物理证据；禁止盲重试；Accepted 不折回 | PERSIST-009 |
| 时间戳 UTC offset 稳定 | PERSIST-001 |

## 上下文事实 fold

Opening 幂等；XTrace 严格序；BlogEntry 原子 coverage；Squash 不改 IngestCursor；PrefixRebase/ContextReanchored Epoch+1（PERSIST-010）。  
禁止 OverflowDetected 等容量事实。

代表：`tests/unit/journal/*`、`tests/integration/journal/boot.test.mjs`、`domain.meta.test.mjs` 时区断言。
