# Fallback — 证明

行为：`what/fallback.md`。写入口：`shape/fallback.md`。算法：`how/fallback.md`。

## 单一写入口

| 证明 | 期望 | 条款 |
|------|------|------|
| 仅 FallbackController 写 Advanced/Exhausted | 无 continuation/retry 直写 | FALLBACK-003 |
| 同一 failed attempt 只推进一次 | FallbackAttemptIdentity 去重 | FALLBACK-003 |

## 算术与预算

| 证明 | 条款 |
|------|------|
| Offset mod 4；side A/A/B/B | FALLBACK-002 |
| 失败 +1 count，成功清零 count 且 Offset 不变 | FALLBACK-004 |
| StrengthReplica 成败不进 owner FallbackController / 不清零 count | FALLBACK-004、STRENGTH-004/019 |
| count≥预算 → Exhausted，无自动第 13 次 | FALLBACK-005 |
| Host Attempt 不写入 count | FALLBACK-010 |

代表：`tests/unit/fallback/*`、`tests/unit/strength/authority-policy.test.mjs`、`tests/unit/context/attempt-plan.test.mjs`。

## 槽与 arm

| 证明 | 条款 |
|------|------|
| 维护子请求成功不清零 count | FALLBACK-011 |
| armed = 紧邻失败 ∧ 奇数 Offset；禁止仅奇偶 | FALLBACK-012 |
| 空/XML-only 不进 A/B 计数 | FALLBACK-008 |
| Host abort 清理残留（`interrupted=true`）不进 A/B 计数 | FALLBACK-013 |

FALLBACK-013 证据：`tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`
（`LOOP_006_interrupted_blog_repairs_without_advancing_primary_cursor`、
`ENFORCER_065_tool_execution_error_blog_advances_primary_cursor_once`）；
provider 可见 A/A/B/B 的端到端固定：`tests/e2e/cases/fallback-aabb-trace.test.mjs`
（`waitFact FallbackCursorAdvanced eq = 4` + 恰好四次推进）。

Fold 拒绝非法 NextOffset / 超预算 / Exhausted 后再 Advanced（FALLBACK-007）。
