# 13 — 上下文预算

## 目标

`todowrite` 触发 backlog 投影压缩存在 **N 代延迟**：投影需 $\ge N$ 个 todo anchor 才缩减，且 `foldedBacklog = backlog.[..length-2]` 丢弃最后一个。若临近窗口上限才写 todo，未折叠细节会先撑爆上下文。本机制在阶段内把新增占用限制在可用空间 $1/(N+1)$ 以内，**强制** LLM 提前 `todowrite` $N$ 次；compact 仅作 backlog 已饱和时的宿主 fallback。

## 投影延迟参数 N

`requiredFoldAnchorCount(foldAfterFirst)` 定义 $N$：

| `foldAfterFirst` | $N$ | 调用路径 |
| :--- | :---: | :--- |
| `true` | 2 | `rebuildPhaseState`（phase 重建） |
| `false` | 3 | `MessageTransformCore`（实际对话投影） |

$N$ = nudge 触发后到投影**首次缩减**所需的连续 todowrite 次数。每次 todowrite = 一个 todo anchor。`foldedBacklog` 丢弃最后一个，故 backlog 有 $k$ 个 entry 时投影只含前 $k-1$ 个。

## 符号

| 符号 | 含义 |
| :--- | :--- |
| $b$ | `maxInputTokens`（物理窗口上限） |
| $b_{eff}$ | $b_{eff} = \lfloor b \times 75\% \rfloor$（compaction 截断前的有效上限） |
| $a$ | 当前请求 token 占用 `currentTokens` |
| $P$ | phase 起点的 totalTokens `phaseBaseTokens` |
| $c$ | phase 起点 backlog token 估算 `backlogTokensAtPhaseStart` |
| $s$ | phase 非 backlog 基线：$s = P - c$ |
| $u$ | 阶段新增：$u = a - P$ |
| $N$ | 投影延迟参数（`requiredFoldAnchorCount`） |

阶段起点 = 最近一次成功 `todowrite` 投影稳定后的首条 prompt；首会话即任务起点。

## 触发函数 F

### 安全条件

触发 nudge 后到 compaction（$b_{eff}$），LLM 需 $N$ 次 todowrite 才让投影缩减。可用空间 $b_{eff} - P$ 分成 $N+1$ 份：$N$ 份给 $N$ 次 todowrite + 1 份 reserve。当阶段新增 $u$ 消耗了 $1/(N+1)$ 份时触发：

$$u \ge \frac{b_{eff} - P}{N + 1}$$

等价：

$$(N+1)(a - P) \ge b_{eff} - P$$

$$(N+1) \, a \ge b_{eff} + N \cdot P$$

$$\boxed{F(a, b_{eff}, P, N) \equiv (N+1) \, a \ge b_{eff} + N \cdot P}$$

### 特例验证

| 场景 | $P$ | $N$ | 阈值 $a \ge$ | 占 $b_{eff}$ |
| :--- | :--- | :---: | :--- | :--- |
| 首次 phase（空 backlog） | 0 | 3 | $b_{eff}/4$ | 25% |
| 首次 phase（空 backlog） | 0 | 2 | $b_{eff}/3$ | 33% |
| Phase 中段 | 60000 | 3 | $(150000+180000)/4 = 82500$ | 55% |
| Phase 末段 | 120000 | 3 | $(150000+360000)/4 = 127500$ | 85% |

旧公式 $2a \ge b_{eff} + P$ 是 $N=1$（无延迟）的特例。实际 $N \ge 2$，旧公式触发过晚。

实现：`Kernel/ContextBudget.fs` 中 `F`（`int64` 防溢出）；`classifyPressure` 接收 `foldAfterFirst` 参数，内部调 `requiredFoldAnchorCount` 得 $N$。

## 极限 compact 守卫

若 `phaseBaseTokens >= (b_eff * 8) / 10`（占有效窗口 80%，即原始窗口 60%），不再注入 budget nudge，交宿主 compact。

## phase reset 语义

`rebuildPhaseState` 检测到 `backlog ≠ LastBacklog` 触发 phase reset。

**核心修复**：phase reset 时 `phaseBaseTokens` **继承旧 $P$**，**非**重测 `stableTokens`。重测值含 phase 边界后的新增内容，会把 $P$ 推到 $\approx a$，导致 $u = a - P \approx 0$、触发阈值退化到 $b_{eff}$（100%）。

继承语义：phase 边界 = todowrite 成功 = 对话历史不变只 backlog 变。故非 backlog 基线 $s = P - c$ 不变，只有 $c$ 更新为新 backlog token。但 $P \ge c$（确保 $s \ge 0$），故 $P = \max(\text{old } P, c)$。

| 条件 | 新 $P$ | 新 $c$ |
| :--- | :--- | :--- |
| `State.IsNone && backlog.IsEmpty` | 0 | 0 |
| `State.IsNone && backlog ≠ []` | `stableTokens` | 重算 |
| `State.IsSome && backlog ≠ LastBacklog` | $\max(\text{old } P, c)$ | 重算 |

`ContextBudgetStore` 更新 `LastBacklog` 时 `NudgeTrack` 经 `afterPhaseBoundaryReset` 回到 `Idle`。

**ContextState 非 SSOT**；重启靠 NDJSON fold backlog → `projectBacklogFor` → `beginPhase`。

## 管线集成

`MessageTransformPipeline.applyContextBudget`（在 backlog 投影之后、parallel prompt 之前）：

1. `GetContextUsage(encoded)` 或 `estimateTokens`（上次 token/byte 比例）
2. backlog 变化则 `rebuildPhaseState`（继承旧 $P$，重算 $c$）
3. `classifyPressure`（传入 `foldAfterFirst=false`，$N=3$）为 `RequireTodoWriteEmergency` → 追加 synthetic **User** 文本：

```text
Attention: the system context is about to be suspended. You must immediately force an emergency stop to all work and call the todowrite tool.
```

`source = Synthetic "context-budget-nudge"`。同 source 已存在则替换而非无限追加。

## ContextBudgetStore

按 `sessionID` 存于 `RuntimeScope`：

```fsharp
{ State: ContextState option
  LastUsage: {| tokenCount; textBytes |} option
  LastBacklog: BacklogEntry list
  NudgeTrack: BudgetNudgeTrack }
```

## 与 nudge 的关系

ContextBudget 是 `messages.transform` 管线内的**同步注入**（投影钩子），不是 nudge 子系统。nudge 是 `SessionIdle` 后的**异步** `session.prompt`。两者正交，无优先级关系。

## 拒绝采用的近似

- $a + c \ge b$（触发过晚）
- 固定字符/token 常数（允许**动态**最近一次测量比例）
- 在 F 内预测下一次 todowrite 大小
- $N=1$（忽略投影延迟，触发过晚）
- phase reset 时重测 `stableTokens` 作 $P$（阈值退化到 100%）

## 测试

`ContextBudgetSpecs`（纯函数 $F$、$N$ 参数、phase reset 退化）、`ContextBudgetAfterTodoTests`（管线集成）、`ContextBudgetIntegrationTests`、`ContextBudgetHookTests`、`ContextBudgetRealApiSpecs`、`ContextBudgetNoReinjectTests`。

## maxInputTokens / currentTokens 获取（OpenCode v1 SDK）

`Shell/ContextBudgetUsageCodec.resolveMaxInputTokens` 接收 `(targets, sessionID, directory)`，用于获取 $b$（上下文窗口上限）：

1. **同步**：在 target/session/client 上找 `model.limit.input` 或 `model.limit.context`
2. **异步**：
   - 调用 `session.get({ path: { id: sessionID }, query: { directory } })`，从响应中提取使用模型的 `data.model.{id, providerID}`（即 `modelID` 和 `providerID`）。
   - 调用 `provider.list({ query: { directory } })` 获取包含全部可用提供商的列表，匹配 `id` 属性为 `providerID` 的项，并在该项的 `models` 中以 `modelID` 检索其限制，最终读取 `data.all[].models[modelID].limit.input`（优先）或 `data.all[].models[modelID].limit.context`（后备）。
3. **兜底**：返回 `0`（API 不可用 → 跳过 budget nudge，安全降级）

`tryGetRealContextUsage` 同样以 `(target, sessionID, directory)` 获取 $a$（当前 token 占用）：

- 以相同的 SDK 形式调用 `session.get({ path: { id: sessionID }, query: { directory } })`。
- 获取返回数据并计算表达式 `data.tokens.input + data.tokens.cache.read`。

> OpenCode v1 SDK `Session.tokens` 无 `total` 字段。当前占用 = `input + cache.read`（同 `overflow.ts:usable()` 语义，其中未定义或非数字的 `cache.read` 视为 `0.0`）。

> `MaxInputTokens <= 0` 时 `applyContextBudget` 直接跳过（不注入 nudge）。

## 相关

- [07-work-backlog.md](./07-work-backlog.md)
- [10-message-transform.md](./10-message-transform.md)