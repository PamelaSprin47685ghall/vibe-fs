# Fallback — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在 LLM 对话中，当 SelectedAgent 遇到模型偶发故障、超时或输出退化时，系统需要在**不重新选举 Authority** 的前提下，自动在 SelectedAgent 与 PeerAgent 之间按 Modulo-4 Cursor 顺序轮换重试（AABB 策略），同时在达到指定连续失败上限后安全熔断（`FallbackExhausted`），防止无限消耗 Token 与预算。Fallback 模块必须保证该轮换过程具备严格的强类型防错、单一写入口与 Fail-Closed 恢复能力。

### 2. 输入输出与规则边界
- **输入**：Reconciler 交付的 `TurnOutcome`（`Completed` | `Failed` | `Abandoned`）、`HostSignal` 唤醒信号与物理尝试身份 `FallbackAttemptIdentity`。
- **输出**：`FallbackCursorAdvanced` 与 `FallbackExhausted` 领域事实、`FallbackVerdict` 恢复决策（`MayContinue` | `Exhausted`）。
- **核心边界与不变量**：
  1. 单一写入口：`FallbackController` 为提交 cursor 变更事实的唯一写入口（FALLBACK-003）。
  2. Modulo-4 强类型 DU：`FallbackOffset` 只能为 `Fork0 | Fork1 | Fork2 | Fork3`；反序列化非法字节拦截为 `Error` 并 fold 为 Fail-Closed（`CommitUnknown` / `ReconcileFailed`），严禁抛出异常。
  3. `armedByFailure` 内存隔离：`armed` 标志必须仅在紧邻物理 attempt 失败时为 `true`，崩溃后归零，严禁仅凭奇数 Offset 自动触发 squash（FALLBACK-012）。

---

## FALLBACK-002：Modulo-4 Cursor

Offset 只有 0|1|2|3 四个合法值。用 DU 在类型层面排除非法态；byte 只出现在序列化/反序列化边界。在反序列化边界，非法字节属于可预见的数据损坏/版本不兼容场景，**严禁使用 `invalidOp` 抛出异常**，必须返回 `Result`，由 Journal fold 解析为 `CommitUnknown` / `ReconcileFailed` 走 Fail-Closed 路径。

```fsharp
type FallbackOffset = Fork0 | Fork1 | Fork2 | Fork3

let toByte = function
    | Fork0 -> 0uy | Fork1 -> 1uy | Fork2 -> 2uy | Fork3 -> 3uy

let ofByte = function
    | 0uy -> Ok Fork0
    | 1uy -> Ok Fork1
    | 2uy -> Ok Fork2
    | 3uy -> Ok Fork3
    | b -> Error (sprintf "FallbackOffset 非法字节: 0x%02x" b)

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

---

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

---

## FALLBACK-007：持久事实与 Fold

```fsharp
FallbackCursorAdvanced =
    { LogicalRunId; AuthorityRootUserMessageId
      ProviderRunIdentity
      PreviousOffset; NextOffset
      ConsecutiveFailureCount }

FallbackExhausted =
    { LogicalRunId; AuthorityRunRootUserMessageId
      FinalConsecutiveFailureCount; FinalOffset }
```

Fold 拒绝条件（任一不满足则 fail closed）：

```text
PreviousOffset 必须经 ofByte 解码为 Ok prevOffset，否则拒绝并触发 Fail-Closed Reconcile
NextOffset = (PreviousOffset + 1) mod 4
ConsecutiveFailureCount = 前值 + 1（无前值时 = 1）
ConsecutiveFailureCount <= AutoRecoveryBudget
FallbackExhausted 之后同 (LogicalRunId, AuthorityRoot) 再收 Advanced → 拒绝
```

---

## FALLBACK-009：Host 停止自动重试

若 Host 在某次后不再 retry，必须用 `ProviderRetryAttempt` continuation 延续同一 Logical Run：

- 同一 AuthorityRoot  
- 不建新 completion  
- 不重置 cursor  
- 不得伪称「无限 AABB 已由 Host 完成」

---

## 槽内维护子请求编排算法（FALLBACK-011）

一次自动恢复槽最多包含两个物理 provider request（按序编排）：

```text
Step 1: 维护子请求（BloggerSquash）
Step 2: 业务主请求（WorkMain / BloggerMain）
```

执行算法与状态判定：

```text
executeSlot(slot):
    if slot 需要 maintenance (BloggerSquash):
        res1 = executeAttempt(MaintenanceAttempt)
        if res1 == Failed:
            // 维护子请求失败 -> 槽失败，终止槽执行，不发主请求
            FallbackController.recordConfirmedFailure(res1.ProviderRunIdentity)
            return SlotFailed
        else:
            // 维护子请求成功 -> 不清零 ConsecutiveFailureCount，继续发主请求
            pass

    res2 = executeAttempt(MainAttempt)
    if res2 == Failed:
        // 主请求失败 -> 槽失败，记录唯一的 FallbackCursorAdvanced
        FallbackController.recordConfirmedFailure(res2.ProviderRunIdentity)
        return SlotFailed
    else:
        // 主请求成功 -> 槽成功，清零 ConsecutiveFailureCount
        ConsecutiveFailureCount := 0
        return SlotSuccess
```

每一个失败槽在终态时**恰好产生一次** `FallbackCursorAdvanced` 事实，其 `ProviderRunIdentity` 指向导致该槽终止失败的物理 attempt。

---

## armed 合取算法与状态求值（FALLBACK-012）

恢复槽允许运行 X prefix probe 或 Y squash 当且仅当合取断言为真：

```text
canRunSquash = armedByFailure && primed && hasMaterial
```

各分量求值算法：

1. **`armedByFailure: bool`**：内存执行标志。
   - 新 Logical Run 创建时初始化为 `false`。
   - 当本槽是由本次自动恢复流程中**紧邻的前一次真实物理 attempt 失败**推进而来时，置为 `true`。
   - 进程崩溃或重启后自动丢失并恢复为 `false`（安全侧 Fail-Closed）。
2. **`primed: bool`**：由当前 FallbackCursor 求值，`cursor.Offset = Fork1 || cursor.Offset = Fork3`（即 Offset 为奇数 A′ / B′）。
   - **禁止**仅凭持久化的奇数 Offset 自动判定为 `armed`（若前次主请求成功，Offset 可停在奇数，但此时 `armedByFailure = false`）。
3. **`hasMaterial: bool`**：检查当前是否有未压缩的 Blog frames / delta 待合并。

不变量保证：任意两次 squash 之间必须至少存在一次真实的物理 attempt 失败。
