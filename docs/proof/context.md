# 上下文恢复 — 证明

行为：`what/context.md`。边界：`shape/context.md`。程序：`how/context.md`。

## 硬禁止

| 证明 | 期望 | 条款 |
|------|------|------|
| 无 token/窗口估算 API 使用 | 生产与测试均无 | CTX-001 |
| 无请求前压缩决策 | 仅失败后恢复槽 | CTX-002 |
| 失败不按错误文字分叉 | 仅 Outcome | CTX-005 |

## 恢复槽

| 证明 | 条款 |
|------|------|
| 需 armed∧primed∧hasMaterial | CTX-006、FALLBACK-012 |
| 无材料 → 正常主请求 | CTX-006、CTX-011 |
| X probe 失败无事实 | CTX-010 |
| squash 成功才提交 | CTX-012 |

## Delta

| 证明 | 条款 |
|------|------|
| 渲染后 ≤200 KiB 合同 | CTX-003、CTX-013 |
| 非窗口比例触发 | CTX-003 |

## ActivePrefixEpoch · TodoCheckpoint

| 证明 | 期望 | 条款 |
|------|------|------|
| 单一 PrefixEpoch SSOT | 无平行 todo-only epoch / 第二 ActivePrefixEpoch | CTX-015、TODO-009、TODO-012 |
| desired 仅自 Accepted | 无 Requested/NeedRebase Stage；Prepared-only 不入链 | CTX-015、TODO-006、TODO-009 |
| T1 无 prior 替换 | k≤1 不提交 TodoCheckpoint epoch | CTX-015、TODO-009 |
| cutoff = Before(T(k-1)) | 上一 checkpoint call/result 仍 raw X | CTX-015、TODO-009 |
| commit 在 seal/绑定前 | 禁止先发后补 committed；todowrite after 不 commit | CTX-015、TODO-009 |
| EvidenceKind=TodoCheckpoint | 进入既有 PrefixRebaseCommitted；字段同级 | CTX-015、TODO-009、COMPANION-009 |
| provider 失败不回滚 epoch | Failed/Aborted 保留已 seal epoch | CTX-015、TODO-009 |
| Y 仅 PrefixCoverage | bundle 无 LWR RawGap；非 RecordCoverage 证明 | CTX-015、TODO-008、TODO-009、COMPANION-003 |
| coverage 不互推 | 不用 PrefixCoverage 填 LWR gap | CTX-015、TODO-008 |
| 非 Magic 行为保留 | 无 Accepted 链时仍仅 probe/reanchor 冷边界 | CTX-010、CTX-012、ARCH-004 |
| restart 可复现 | 同 facts → 同 desired → seal 前同 epoch 投影 | CTX-015、TODO-009、TODO-012 |

## Opening · WorkRecordStart

| 证明 | 期望 | 条款 |
|------|------|------|
| Opening byte-stable | 不随 TodoCheckpoint/Y 改写消失 | CTX-016、TODO-001 |
| WorkRecordStart floor | Blogger effectiveStart ≥ Opening exclusive end | CTX-016、TODO-001 |
| LWR 不复制 Opening | process-review includeOpening=false | CTX-016、TODO-008 |
| 非 Activation floor | 不读 WorkActivated 作 Opening 保护 | CTX-016、TODO-001 |

代表：`tests/unit/context/*`（probe-selection、recovery-slot、blogger-delta）；e2e `context-recovery.test.mjs`（X-A–X-D）。TodoCheckpoint/WorkRecordStart 证明随 Magic Todo 实现落入同目录或 `tests/unit/todo/*`，条款指针仍以本表为准。
