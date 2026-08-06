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

代表：`tests/unit/context/*`（probe-selection、recovery-slot、blogger-delta）；e2e `context-recovery.test.mjs`（X-A–X-D）。
