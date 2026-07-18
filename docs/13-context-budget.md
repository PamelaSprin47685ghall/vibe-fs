# 13 — 上下文预算

## 目标

`todowrite` 触发 backlog 投影压缩存在 **N 代延迟**：投影需 $\ge N$ 个 todo anchor 才缩减，且 `foldedBacklog = backlog.[..length-2]` 丢弃最后一个。若临近窗口上限才写 todo，未折叠细节会先撑爆上下文。本机制在投影周期内把新增占用限制在可用空间 $1/(Q+M+1)$ 以内，**强制** LLM 提前 `todowrite` $Q+M$ 次；compact 仅作 backlog 已饱和时的宿主 fallback。

## 投影延迟参数

`FoldAfterSecond` 策略需要 3 个 anchor 才触发首次折叠。首次折叠后，稳态下每新增 1 个 anchor 即触发一次折叠。

## 符号

| 符号 | 含义 |
| :--- | :--- |
| $b$ | `maxInputTokens`（物理窗口上限） |
| $b_{eff}$ | $b_{eff} = \text{contextLimit} - \text{reserve}(5000)$（物理窗口扣除安全保留值后的有效上限） |
| $a$ | 当前请求 token 占用 `currentTokens` |
| $P$ | 投影周期基线 `BaselineTokens`（最近一次折叠后的 token 估算） |
| $Q$ | 已完成工作段数 `CompletedSegments`（baseline 后已完成且尚未触发下一次折叠的 todo 数） |
| $M$ | 距下一次折叠还需的 todo 数 `RemainingTodoWritesUntilFold` |
| $F$ | 折叠前沿序号 `FoldFrontierOrdinal`（已折叠到的 todo 前沿） |

投影周期起点 = 最近一次真实折叠后的状态；首会话即冷启动。

## 触发函数 F

### 安全条件

考虑到投影延迟，触发 nudge 后到投影首次缩减（释放上下文）前，LLM 还需要完成 $M$ 次 `todowrite`。
如果在当前周期内已经完成了 $Q$ 次 `todowrite`（但因未满折叠条件尚不能触发折叠），那么在下一次折叠前，LLM 实际仅需再完成 $M$ 次 `todowrite`。
因此，触发 nudge 后，在上下文真正释放前，系统需要预留 $M$ 次 `todowrite` 对应的工作段空间，并外加 1 份 reserve 空间。

我们将当前周期的 token 增长 $a - P$ 平均分给已发生的 $Q$ 个 `todowrite` 工作段与当前正在进行的 1 个段，共 $Q + 1$ 个工作段。由此估算每个工作段大小为:
$$\text{segment\_size} = \frac{a - P}{Q + 1}$$

在当前 token 占用 $a$ 处，剩余的有效安全空间为 $b_{eff} - a$。我们要求此剩余空间必须大于等于将来所需的 $M$ 个工作段的总空间以确保不爆窗口：
$$b_{eff} - a \ge M \times \frac{a - P}{Q + 1}$$

由于 $Q \ge 0$，我们将两边同乘以 $Q + 1$ 并进行移项整理：
$$(Q + 1)(b_{eff} - a) \ge M(a - P)$$
$$(Q + 1)b_{eff} - (Q + 1)a \ge Ma - MP$$
$$(Q + 1)b_{eff} + MP \ge (M + Q + 1)a$$

当上述安全条件被破坏时，即触发 nudge（`RequireTodoWriteEmergency`）：
$$\boxed{F(a, b_{eff}, P, Q, M) \equiv (Q+M+1) \, a \ge (Q+1) \, b_{eff} + M \cdot P}$$

### 动态防过度触发机制

在投影周期中，每次成功 `todowrite` 会被识别为 `TodoAcknowledged` 转换，推进 $Q$ 但不重建基线 $P$。
引入参数 $Q$ 和 $M$ 后，每完成一次 `todowrite`，$Q$ 增加 1，$M$ 减少 1，nudge 触发的阈值按 $\frac{(Q+1)b_{eff} + MP}{Q+M+1}$ 动态向上移动（相当于减少未来需要预留的工作段），使得当前未折叠的 $a$ 重新落入安全阈值下方，从而**防范了重复频繁触发**。

只有当折叠前沿真正前进（`FoldFrontierAdvanced`）时，才重建基线 $P$ 并重置 $Q=0, M=1$（稳态）。

### 特例验证 (首次周期 $b_{eff}=150000, P=30000$)

| $Q$ | $M$ | 占用 $a$ 触发阈值 ($a \ge$) | 物理空间状态 |
| :---: | :---: | :--- | :--- |
| 0 | 3 | $\frac{150000 + 3 \times 30000}{4} = 60000$ (40%) | 剩余 90000 空间，够未来 3 次工作段 |
| 1 | 2 | $\frac{2 \times 150000 + 2 \times 30000}{4} = 90000$ (60%) | 剩余 60000 空间，够未来 2 次工作段 |
| 2 | 1 | $\frac{3 \times 150000 + 30000}{4} = 120000$ (80%) | 剩余 30000 空间，够未来 1 次工作段 |
| 0 (fold后) | 1 | $\frac{150000 + 30000}{2} = 90000$ (60%) | 折叠后重建，稳态下一次 fold 距离为 1 |

### 手动模拟 (Manual Simulation)

设有效上限 $b_{eff} = 150000$，初始 $P=30000$（冷启动），$Q=0, M=3$。

1. **第 0 段工作 ($Q=0, M=3$)**：
   - 阈值为 $a \ge 60000$。
   - LLM 从 $P=30000$ 开始工作，至 $a = 60000$（增量 $u = 30000$）。
   - 此时触发 Nudge。LLM 被要求紧急执行 `todowrite`以保存并更新 backlog。
2. **LLM 执行第 1 次 `todowrite` 成功**：
   - 识别为 `TodoAcknowledged`，$Q$ 变为 1，$M$ 变为 2。
   - 假设此时 token 占用因 todowrite 自身的写入微升至 $a_1 = 65000$。
   - 此时的 Nudge 触发阈值动态提升至 $a \ge 90000$。
   - 由于 $65000 < 90000$，nudge **不触发**，LLM 可以在安全环境下继续工作。
3. **第 1 段工作 ($Q=1, M=2$)**：
   - LLM 接着工作，至 $a = 90000$（总增量 $u = 60000$，平均每段工作 $30000$）。
   - 此时 $a = 90000$ 触及阈值，再次触发 Nudge。
4. **LLM 执行第 2 次 `todowrite` 成功**：
   - $Q$ 变为 2，$M$ 变为 1。假设占用微升至 $a_2 = 95000$。
   - 此时触发阈值动态提升至 $a \ge 120000$。
   - 由于 $95000 < 120000$，nudge **不触发**，LLM 继续安全工作。
5. **第 2 段工作 ($Q=2, M=1$)**：
   - LLM 继续工作至 $a = 120000$（总增量 $u = 90000$，平均每段 $30000$）。
   - 此时触及阈值，第三次触发 Nudge。
6. **LLM 执行第 3 次 `todowrite` 成功**：
   - 此时折叠前沿前进（`FoldFrontierAdvanced`），触发投影首次缩减：
     - 最早的 todo anchor 被折叠入 Front Matter，该点之前的历史消息被截断。
     - $a$ 从 $120000+$ 陡降回 $\approx 80000$。
     - 重建基线 $P \approx 80000$，$Q=0, M=1$（稳态）。
   - 系统成功度过窗口崩溃危机。

实现：`Kernel/ContextBudget.fs` 中 `F`；`classifyPressure` 直接从 `ContextState` 读取 $Q, M$ 传入 `F`。

## 极限 compact 守卫

若 `BaselineTokens >= (b_eff * 8) / 10`（占有效窗口 80%，即原始窗口 60%），不再注入 budget nudge，交宿主 compact。

## 四种转换

`ContextBudgetPhase.classifyTransition` 根据投影元数据识别转换类型：

| 转换 | 条件 | 动作 |
| :--- | :--- | :--- |
| `ColdStart` | `State.IsNone` | 以当前 token 估算建立 cycle |
| `TodoAcknowledged` | `TotalTodoOrdinal > BaselineTodoOrdinal` 且未折叠 | 推进 $Q$，不重建 $P$ |
| `FoldFrontierAdvanced` | `FoldFrontierOrdinal > state.FoldFrontierOrdinal` | 重建 cycle，$Q=0, M=1$ |
| `BacklogOnlyChange` | backlog 变化但无新 todo anchor | 仅更新 backlog，不改 cycle |
| `NoChange` | 无变化 | 无操作 |

`ContextBudgetStore` 更新时，只有 `FoldFrontierAdvanced` 和 `ColdStart` 重置 `NudgeTrack` 为 `Idle` 并生成新 `EpisodeID`。

**ProjectionBudgetCycle 非 SSOT**；重启靠 NDJSON fold backlog → `projectBacklogFor` → `beginCycle`。

## 管线集成

`MessageTransformPipeline` 先完成 backlog 投影、parallel prompt、Top slot、Semble 注入和 CAPS 前缀，形成 `withoutBudgetNudge`；随后调用 `applyContextBudget` 测量该最终普通 outbound，最后只追加 budget synthetic message：

1. Host 仅通过 `ObserveLatestUsage()` 观察已完成的 assistant；候选 encoded 由上一轮 outbound bytes 与新 observation 建立 calibration 后纯函数估算
2. 投影结果携带元数据（`TotalTodoOrdinal`, `FoldFrontierOrdinal`, `RemainingTodoWritesUntilFold`），用于识别转换类型
3. `classifyPressure` 为 `RequireTodoWriteEmergency` → 最终 encoded 追加 synthetic **User** 文本：

```text
Attention: the system context is about to be suspended. You must immediately force an emergency stop to all work and call the todowrite tool.
```

`source = Synthetic "context-budget-nudge"`。同 source 已存在则替换而非无限追加。

## ContextBudgetStore

按 `sessionID` 存于 `RuntimeScope`：

```fsharp
{ State: ProjectionBudgetCycle option
  LastUsage: {| tokenCount; textBytes |} option
  PendingOutbound: { Fingerprint; Bytes } option
  LastCalibration: UsageCalibration option
  LastObservedAssistantID: string option
  LastTrace: DecisionTrace option
  LastBacklog: BacklogEntry list
  NudgeTrack: BudgetNudgeTrack
  EpisodeID: string
  NudgeCount: int
  SignalTodoOrdinal: int option
  SignalTokens: int64 option
  StableSyntheticNudgeID: string option }
```

## 与 nudge 的关系

ContextBudget 是 `messages.transform` 管线内的**同步注入**（投影钩子），不是 nudge 子系统。nudge 是 `SessionIdle` 后的**异步** `session.prompt`。两者正交，无优先级关系。

## 拒绝采用的近似

- $a + c \ge b$（触发过晚）
- 固定字符/token 常数（允许**动态**最近一次测量比例）
- 在 F 内预测下一次 todowrite 大小
- 用上一轮未折叠 usage 直接冒充 folded stable token
- 在 CAPS、Semble、parallel 注入前测量预算
- 每次 backlog 变化都重建基线（应仅在前沿推进时重建）

## 测试

`ContextBudgetSpecs`（纯函数 $F$、$Q/M$ 参数、转换逻辑）、`ContextBudgetAfterTodoTests`（管线集成）、`ContextBudgetIntegrationTests`、`ContextBudgetHookTests`、`ContextBudgetRealApiSpecs`、`ContextBudgetNoReinjectTests`、`BacklogProjectionSpecs`（投影元数据）。

## maxInputTokens / currentTokens 获取（OpenCode v1 SDK）

OpenCode 生产路径使用 `provider.list` 获取完整模型目录，计算宿主可用输入 token。

1. `session.get({ path: { id: sessionID }, query: { directory } })` 取 `res.data.model` 的 `providerID` 和 `modelID`。
2. `provider.list({ query: { directory } })` 获取完整模型目录。
3. `findModelInCatalog` 按 `providerID` 和 `id` 匹配模型定义。
4. `extractLimitFromCatalogEntry` 读 `limit.input`（优先）或 `limit.context`，以及 `limit.output`。
5. `computeUsableInputTokens` 计算宿主可用输入：`max(0, context - min(nonzero output, 32000))`。
6. 若模型未知或 `provider.list` 不可用，降级到保守默认 8192。

`ModelResolutionResult` 强类型返回 `{ ProviderID; ModelID; UsableInputTokens; Source }`，`Source` 精确区分 `provider-catalog-input-reserved`、`provider-catalog-context`、`fallback-8192`。

`tryObserveLatestUsage` 读 `session.messages` 取最近一条 assistant 的 `tokens.input + tokens.cache.read + tokens.cache.write`，用于校准估算字节。跳过零 token assistant 和 compaction summary assistant。

`MaxInputTokens <= 0` 时 `applyContextBudget` 直接跳过。

## Compaction 隔离

Compaction summary 请求与正常 transform 共享同一 `experimental.chat.messages.transform` hook，但必须完全隔离于预算管线。

### 机制

1. `FallbackSessionRuntime` 新增 `CompactionSummaryTransformPending: bool` 字段。
2. `compactingTransform` 启动时通过 `beginCompaction` 原子设置 `CompactionSummaryTransformPending = true`。
3. `messagesTransform` 在任何预算/投影/CAPS/nudge 副作用前，调用 `TryConsumeCompactionSummaryTransform(sessionID)` 检查并消费该标志。
4. 若标志为 true，直接 pass-through，保留宿主数组与 part 引用，不进入 backlog projection、parallel/CAPS、ContextBudget 或普通 transform store。
5. `settleCompaction` 和 `handleCompactionError` 清除该标志。
6. Session cleanup 通过 `CleanupSession` 移除整个 session state，标志自然消失。

### 双保险

- A: bypass compaction transform（上述机制）
- B: 过滤 summary assistant usage（`tryObserveLatestUsage` 跳过 `agent=compaction` 或 `details.summary=true` 的 assistant）

任一缺失都会污染下一普通 calibration。

### 测试

`CompactionIsolationTests` 验证标志的设置、消费、清除和双 session 隔离。

> Mux 与 Omp 保持其原本的 `ContextBudgetUsageCodec` 静态 / 获取逻辑，不倒灌此 OpenCode 专属观测路径。

> `MaxInputTokens <= 0` 时 `applyContextBudget` 直接跳过（不注入 nudge）。

## OpenCode TextPart 保真

`MessagingCodec.encodeMessage` 对 native 消息（`raw <> null`）采用 raw-aware 编码：
- TextPart 未修改时直接复用 raw part 引用，保留 `ignored/metadata/synthetic/id/sessionID/messageID/time` 等宿主字段
- TextPart 文本变更时 shallow-copy raw，仅覆写 `text`
- 仅 synthetic 或新 part 才构造新对象

确保预算 transform 不改变宿主 model projection，`ignored=true` 的文本不会被重新送入模型。

## 相关

- [07-work-backlog.md](./07-work-backlog.md)
- [10-message-transform.md](./10-message-transform.md)
- [13-context-budget-fix.md](./13-context-budget-fix.md)
