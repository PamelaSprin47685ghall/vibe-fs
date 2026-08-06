# Fallback — 目标实现

行为见 `what/fallback.md`。写入口见 `shape/fallback.md`。

## FALLBACK-002：Modulo-4 Cursor

```fsharp
type FallbackCursor =
    { Offset: byte                    // 仅 0|1|2|3
      ConsecutiveFailureCount: int }

let side offset =
    match offset with
    | 0uy | 1uy -> SideA              // SelectedAgent
    | 2uy | 3uy -> SideB              // PeerAgent

let advance offset = byte ((int offset + 1) % 4)

let effectiveAgent authority cursor =
    match side cursor.Offset with
    | SideA -> authority.SelectedAgent
    | SideB -> authority.PeerAgent
```

失败推进：

```text
Offset := advance Offset
ConsecutiveFailureCount := n + 1
if ConsecutiveFailureCount >= AutoRecoveryBudget
    then 写 FallbackExhausted，停止自动物理请求
```

成功：Offset 保持；`ConsecutiveFailureCount := 0`。成功不写 cursor 事实——归零由 Host snapshot 的 Completed 派生，避免第二写入口。

## FALLBACK-006：序列示例

```text
SelectedAgent=fast-coder, PeerAgent=deep-coder, AutoRecoveryBudget=12

attempt 1  Offset 0 → fast-coder 失败 → Offset 1, count 1
attempt 2  Offset 1 → fast-coder 失败 → Offset 2, count 2
attempt 3  Offset 2 → deep-coder 失败 → Offset 3, count 3
attempt 4  Offset 3 → deep-coder 失败 → Offset 0, count 4
...
attempt 12 Offset 3 → deep-coder 失败 → Offset 0, count 12 → FallbackExhausted
→ 无自动 attempt 13
```

成功中断：

```text
attempt 1  Offset 0 → 失败 → Offset 1, count 1
attempt 2  Offset 1 → 成功 → Offset 1, count 0
```

Offset 停在 1：后续失败从 SideA 第二格继续，不回到 0。

## FALLBACK-007：持久事实与 Fold

```fsharp
FallbackCursorAdvanced =
    { LogicalRunId; AuthorityRootUserMessageId
      ProviderRunIdentity
      PreviousOffset; NextOffset
      ConsecutiveFailureCount }

FallbackExhausted =
    { LogicalRunId; AuthorityRootUserMessageId
      FinalConsecutiveFailureCount; FinalOffset }
```

Fold 拒绝条件（任一不满足则 fail closed）：

```text
NextOffset = (PreviousOffset + 1) mod 4
ConsecutiveFailureCount = 前值 + 1（无前值时 = 1）
ConsecutiveFailureCount <= AutoRecoveryBudget
FallbackExhausted 之后同 (LogicalRunId, AuthorityRoot) 再收 Advanced → 拒绝
```

## FALLBACK-009：Host 停止自动重试

若 Host 在某次后不再 retry，必须用 `ProviderRetryAttempt` continuation 延续同一 Logical Run：

- 同一 AuthorityRoot  
- 不建新 completion  
- 不重置 cursor  
- 不得伪称「无限 AABB 已由 Host 完成」

## FALLBACK-011：槽内维护子请求

一次自动恢复槽最多两个物理 provider request：

1. 维护子请求：`BloggerSquash`  
2. 业务主请求：`WorkMain` / `BloggerMain`

```text
维护失败 → 槽失败，不发主请求
维护成功 → 不清零 ConsecutiveFailureCount，继续主请求
主失败   → 槽失败
主成功   → 清零 ConsecutiveFailureCount
```

每个失败槽恰好一次 `FallbackCursorAdvanced`，`ProviderRunIdentity` 指向使该槽终止失败的物理 attempt。维护成功单独不算 Logical Run 业务完成。

## FALLBACK-012：armed 合取

恢复槽允许 X prefix probe 或 Y squash，当且仅当：

```text
1. armedByFailure：本槽由本次自动恢复内紧邻的真实失败推进而来
2. primed：Offset 为奇数（A′ / B′）
```

禁止仅根据持久 Offset 奇偶 arm（成功后 Offset 可停在奇数）。  
`armedByFailure` 是执行局部变量，崩溃后丢失（安全侧）。  
新 Logical Run 第一槽永不 armed。  
不变量：任意两次 squash 之间至少隔一次真实失败。
