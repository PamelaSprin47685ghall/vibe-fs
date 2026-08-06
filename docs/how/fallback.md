# Fallback — 目标实现

行为见 `what/fallback.md`。写入口见 `shape/fallback.md`。

## FALLBACK-002：Modulo-4 Cursor

Offset 只有 0|1|2|3 四个合法值。用 DU 在类型层面排除非法态（评审修正：byte 允 0–255，`side` 对 8–255 无分支）；byte 只出现在序列化/反序列化边界。

```fsharp
type FallbackOffset = Fork0 | Fork1 | Fork2 | Fork3

let toByte = function
    | Fork0 -> 0uy | Fork1 -> 1uy | Fork2 -> 2uy | Fork3 -> 3uy

let ofByte = function
    | 0uy -> Fork0 | 1uy -> Fork1 | 2uy -> Fork2 | 3uy -> Fork3
    | _ -> invalidOp "FallbackOffset 非法字节"   // 反序列化失败 fail closed

type FallbackCursor =
    { Offset: FallbackOffset
      ConsecutiveFailureCount: int }

let side offset =
    match offset with
    | Fork0 | Fork1 -> SideA              // SelectedAgent
    | Fork2 | Fork3 -> SideB              // PeerAgent

let advance offset =
    match offset with
    | Fork0 -> Fork1 | Fork1 -> Fork2
    | Fork2 -> Fork3 | Fork3 -> Fork0

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

> 槽位规则：FALLBACK-011（槽内维护子请求）与 FALLBACK-012（armed 合取）见 `what/fallback.md`（GOV-011：行为归 what/）。本文件只承担 cursor 算术（FALLBACK-002）、序列示例（006）、持久事实与 fold（007）、Host 停止注解（009）。
