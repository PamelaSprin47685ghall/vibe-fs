# 13 — 上下文预算

## 目标

`todowrite` 触发 backlog 投影压缩存在 **一代延迟**（`projectBacklogFor` 边界多为倒数第二次成功提交）。若临近窗口上限才写 todo，未折叠细节会先撑爆上下文。本机制在阶段内把新增占用限制在可用空间一半以内，**强制** LLM 提前 `todowrite`；compact 仅作 backlog 已饱和时的宿主 fallback。

## 符号

| 符号 | 含义 |
| :--- | :--- |
| $b$ | `maxInputTokens` |
| $a$ | 当前请求 token 占用 `currentTokens` |
| $c$ | 阶段起点 backlog token 估算 `backlogTokensAtPhaseStart` |
| $s$ | 阶段非 backlog 基线：$s = P - c$，$P$ = `phaseBaseTokens` |
| $u$ | 阶段新增：$u = a - s - c$ |

阶段起点 = 最近一次成功 `todowrite` 投影稳定后的首条 prompt；首会话即任务起点。

## 触发函数 F

由于宿主 OpenCode 在输入 token 达到 75% 时就会触发 compaction 截断历史，因此在管线中传入触发函数的 $b$ 限制为有效上限（即物理限制 $b_{\text{eff}} = \lfloor b \times 75\% \rfloor$），以确保在 75% 之前强制触发 todowrite。

安全条件 $u \le (b_{\text{eff}}-P)/2$，等价：

$$F(a,b_{\text{eff}},c,s) \equiv 2a \ge b_{\text{eff}} + s + c$$

实现：`Shell/ContextBudget.fs` 中 `F`（`int64` 防溢出）；管线 `MessageTransformPipeline.fs` 中传入 effectiveMaxInputTokens。

## 极限 compact 守卫

若 `phaseBaseTokens >= (maxInputTokens * 8) / 10`（此处的 `maxInputTokens` 为经过 75% 折损后的有效上限 $b_{\text{eff}}$，即占原始总窗口大小的 60%），不再注入 budget nudge，交宿主 compact。

## beginPhase / afterSuccessfulTodo

```text
backlogTokens ≈ (totalTokens * backlogBytes) / totalBytes   // totalBytes>0
ContextState { phaseBaseTokens; backlogTokensAtPhaseStart }
```

成功 `todowrite` 后重测并 `beginPhase`；`ContextBudgetStore` 更新 `LastBacklog` 时重置 `NudgeInjected`。

**ContextState 非 SSOT**；重启靠 NDJSON fold backlog → `projectBacklogFor` → `beginPhase`。

## 管线集成

`MessageTransformPipeline.applyContextBudget`（在 backlog 投影之后、parallel prompt 之前）：

1. `GetContextUsage(encoded)` 或 `estimateTokens`（上次 token/byte 比例）
2. backlog 变化则异步重建 `ContextState`
3. `F` 为 true 且未 `NudgeInjected` → 追加 synthetic **User** 文本：

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
  NudgeInjected: bool }
```

## 与 nudge 优先级

`context-budget-nudge` **高于** `nudge-todo` / `nudge-loop`；同轮只注入 budget 提示。

## 拒绝采用的近似

- $a + c \ge b$（触发过晚）
- 固定字符/token 常数（允许**动态**最近一次测量比例）
- 在 F 内预测下一次 todowrite 大小

## 测试

`ContextBudgetAfterTodoTests`、`ContextBudgetIntegrationTests`、`ContextBudgetHookTests`、`ContextBudgetRealApiSpecs` 等。

## maxInputTokens / currentTokens 获取（OpenCode v1 SDK）

`Shell/ContextBudgetUsageCodec.resolveMaxInputTokens` 获取 $b$（上下文窗口上限）：

1. **同步**：在 target/session/client 上找 `model.limit.input` 或 `model.limit.context`
2. **异步**：`session.get({ sessionID })` → `data.model.{id, providerID}` → `provider.list()` → 查找 `models[modelID].limit.input`（优先）或 `.limit.context`（后备）
3. **兜底**：返回 `0`（API 不可用 → 跳过 budget nudge，安全降级）

`tryGetRealContextUsage` 获取 $a$（当前 token 占用）：

`session.get({ sessionID })` → `data.tokens.input + data.tokens.cache.read`

> OpenCode v1 SDK `Session.tokens` 无 `total` 字段。当前占用 = `input + cache.read`（同 `overflow.ts:usable()` 语义）。

> `MaxInputTokens <= 0` 时 `applyContextBudget` 直接跳过（不注入 nudge）。

## 相关

- [07-work-backlog.md](./07-work-backlog.md)
- [10-message-transform.md](./10-message-transform.md)