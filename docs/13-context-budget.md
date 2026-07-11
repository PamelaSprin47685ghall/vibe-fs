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
| $R$ | 当前消息历史中未折叠（raw）的已完成 `todowrite` 结果数 `completedTodoCount` (限制在 $[0, N-1]$ 以内) |

阶段起点 = 最近一次成功 `todowrite` 投影稳定后的首条 prompt；首会话即任务起点。

## 触发函数 F

### 安全条件

考虑到 $N$ 代投影延迟，触发 nudge 后到投影首次缩减（释放上下文）前，LLM 还需要完成 $N$ 次 `todowrite`。
如果在当前 phase 内已经完成了 $R$ 次 `todowrite`（但因未满 $N$ 次尚不能触发折叠），那么在下一次折叠前，LLM 实际仅需再完成 $N - R$ 次 `todowrite`。
因此，触发 nudge 后，在上下文真正释放前，系统需要预留 $N - R$ 次 `todowrite` 对应的工作段空间，并外加 1 份 reserve 空间。

我们将当前 phase 发生的 token 增长 $u = a - P$ 平均分给已发生的 $R$ 个 `todowrite` 工作段与当前正在进行的 1 个段，共 $R + 1$ 个工作段。由此估算每个工作段大小为:
$$\text{segment\_size} = \frac{a - P}{R + 1}$$

在当前 token 占用 $a$ 处，剩余的有效安全空间为 $b_{eff} - a$。我们要求此剩余空间必须大于等于将来所需的 $N - R$ 个工作段的总空间以确保不爆窗口：
$$b_{eff} - a \ge (N - R) \times \frac{a - P}{R + 1}$$

由于 $R \ge 0$，我们将两边同乘以 $R + 1$ 并进行移项整理：
$$(R + 1)(b_{eff} - a) \ge (N - R)(a - P)$$
$$(R + 1)b_{eff} - (R + 1)a \ge (N - R)a - (N - R)P$$
$$(R + 1)b_{eff} + (N - R)P \ge (N - R + R + 1)a$$
$$(R + 1)b_{eff} + (N - R)P \ge (N + 1)a$$

当上述安全条件被破坏时，即触发 nudge（`RequireTodoWriteEmergency`）：
$$\boxed{F(a, b_{eff}, P, N, R) \equiv (N+1) \, a \ge (R+1) \, b_{eff} + (N-R) \cdot P}$$

在阶段内 $u$ 的等价表示为：
$$\boxed{u \ge (R + 1) \frac{b_{eff} - P}{N + 1}}$$

### 动态防过度触发机制

在阶段（Phase）中，每次成功 `todowrite` 虽因未满 $N$ 代暂不缩减投影，但由于 backlog 变化会触发 phase reset，将 `NudgeTrack` 设回 `Idle`。
若仍使用旧的静态公式 $F(a, b_{eff}, P, N) \equiv (N+1)a \ge b_{eff} + N \cdot P$：由于 $a$ 在写入后并未实质减少，且 threshold 保持较低的 $(b_{eff} + N \cdot P)/(N+1)$，在 `NudgeTrack` 重置后，下一轮 pipeline 会立即重新触发 nudge 造成过度骚扰。
引入参数 $R$ 后，每完成一次 `todowrite`，$R$ 增加 1，nudge 触发的阈值按 $\frac{(R+1)b_{eff} + (N-R)P}{N+1}$ 动态向上移动（相当于减少未来需要预留的工作段），使得当前未折叠的 $a$ 重新落入安全阈值下方，从而**完美防范了重复频繁触发**。

### 特例验证 ($N=3, b_{eff}=150000, P=30000$)

| $R$ | 安全段数 $N-R$ | 阶段 $u$ 触发条件 ($u \ge$) | 占用 $a$ 触发阈值 ($a \ge$) | 物理空间状态 |
| :---: | :---: | :--- | :--- | :--- |
| 0 | 3 | $\frac{150000 - 30000}{4} \times 1 = 30000$ | $\frac{150000 + 3 \times 30000}{4} = 60000$ (40%) | 剩余 90000 空间，够未来 3 次工作段 ($3 \times 30000$) |
| 1 | 2 | $\frac{150000 - 30000}{4} \times 2 = 60000$ | $\frac{2 \times 150000 + 2 \times 30000}{4} = 90000$ (60%) | 剩余 60000 空间，够未来 2 次工作段 ($2 \times 30000$) |
| 2 | 1 | $\frac{150000 - 30000}{4} \times 3 = 90000$ | $\frac{3 \times 150000 + 30000}{4} = 120000$ (80%) | 剩余 30000 空间，够未来 1 次工作段 ($1 \times 30000$) |
| 3 | 0 (Clamped to 2) | \- | $120000$ (80%) | 已达第 3 次 `todowrite`，投影折叠，回到 $R=2$ 并释放大量历史 |

*注：若 $R$ 达到 $N$，为防止公式计算产生负系数或零参数失效，在代码实现中对 $R$ 进行了上限为 $N-1$ 的 Clamp 保护。*

### 手动模拟 (Manual Simulation)

设有效上限 $b_{eff} = 150000$，投影延迟 $N=3$（Dialogue 默认），初始 $P=30000$（已有一个 backlog 的 Phase）。

1. **第 0 段工作 ($R=0$)**：
   - 阈值为 $a \ge 60000$。
   - LLM 从 $P=30000$ 开始工作，至 $a = 60000$（增量 $u = 30000$）。
   - 此时触发 Nudge。LLM 被要求紧急执行 `todowrite`以保存并更新 backlog。
2. **LLM 执行第 1 次 `todowrite` 成功**：
   - 消息历史中增加 1 个 `todowrite`，故 $R$ 变为 1。
   - 假设此时 token 占用因 todowrite 自身的写入微升至 $a_1 = 65000$。
   - 此时的 Nudge 触发阈值动态提升至 $a \ge 90000$。
   - 由于 $65000 < 90000$，nudge **不触发**，LLM 可以在安全环境下继续工作。
3. **第 1 段工作 ($R=1$)**：
   - LLM 接着工作，至 $a = 90000$（总增量 $u = 60000$，平均每段工作 $30000$）。
   - 此时 $a = 90000$ 触及阈值，再次触发 Nudge。
4. **LLM 执行第 2 次 `todowrite` 成功**：
   - $R$ 变为 2。假设占用微升至 $a_2 = 95000$。
   - 此时触发阈值动态提升至 $a \ge 120000$。
   - 由于 $95000 < 120000$，nudge **不触发**，LLM 继续安全工作。
5. **第 2 段工作 ($R=2$)**：
   - LLM 继续工作至 $a = 120000$（总增量 $u = 90000$，平均每段 $30000$）。
   - 此时触及阈值，第三次触发 Nudge。
6. **LLM 执行第 3 次 `todowrite` 成功**：
   - 此时 backlog 变化，对话历史中检测到第 3 个 `todowrite` 结果。
   - `applyBacklogProjection`（$N=3$）被触发，**首次缩减投影开始生效**：
     - 最早的第 1 次 `todowrite` 被折叠入 Front Matter，该点之前的历史消息被截断。
     - $a$ 从 $120000+$ 陡降回 $\approx 80000$。
     - $R$ 从 3 重新退回 2（因为最早的一个 todo 已折叠进 Front Matter，只剩最新的 2 个生 todo 仍作为 raw 留在消息中）。
     - 重建的 $P$ 和 $c$ 重新初始化安全区间。
   - 系统成功度过窗口崩溃危机。

实现：`Kernel/ContextBudget.fs` 中 `F`；`classifyPressure` 接收 `foldAfterFirst` 参数，内部调 `requiredFoldAnchorCount` 得 $N$ 并计算 $R$ 传入 `F`。

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