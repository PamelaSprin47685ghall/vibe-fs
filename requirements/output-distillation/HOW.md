# output-distillation — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型与历史裁决。

## 实现模型

### 蒸馏管线（`Infrastructure/OpenCode/Tools/Distillation.fs`）

```text
ProcessOutcome.Spooled(spoolPath, …)          // 物理采集（process-execution）
→ Spool.readChunks(spoolPath, 204800B)        // chunk 化
→ map: 每 chunk fork 一个 Distiller（distillFragmentPrompt）
→ 并行 map，按 chunk index 顺序 await（每 agent 恰好一次 AwaitAgentWithPermit）
→ 任一 map 失败 → cancelOwned()（取消全部 owned map/reduce agents）
→ reduce: rippleInsert 在线归并（ReduceFanIn=8）→ foldLevels → mergeDistillationsPrompt
→ 成功: 完整 account
→ 失败: partialWithTail → CondensationIncomplete/Unavailable + 最后 chunk 原始字节 raw_tail
```

关键常数：`ReduceFanIn = 8`、`AwaitAgentTimeoutMs = 600_000`（每 chunk join 预算）、
`Spool.ChunkSizeBytes = 204800`（= `SPOOL_CHUNK_BYTES`）。

### 失败降级（DISTILL-006 / Oracle 2 行为面）

- 全部成功 → 完整 account（不含 `Condensation incomplete`）。
- 任一 chunk 失败（NotFound 硬失败 / 真超时）→ 不 throw；`partialWithTail lang account rawTail`：
  - `account` 非空 → `CondensationIncomplete`（模板含 `Most recent raw output:` + `raw_tail`）；
  - `account` 空 → `CondensationUnavailable`（模板同样携带 `raw_tail`）。
- `raw_tail` = 最后一个 chunk 的原始字节（`rawTailText chunks`）——对未见过原文的读者保留可定位痕迹。
- 失败 chunk 的 work record **不会**出现在 summary（`summary-for-<failedId>` 缺失 = 不虚构成功）。

### 定向 await 合同（DISTILL-008，`DistillationRuntime.fs`）

- `IDistillationRuntime`：`Fork` / `AwaitAgentWithPermit` / `CurrentJournalRevision` /
  `AwaitJournalChangeFrom` / `CancelAgent`。
- permit 门：每次 await 前 `requirePermit()`；`RECOVERY_WAITING:` → `ForkError.TimedOut`（等 readiness
  信号后再一次 fresh permit check）；其它 permit 错误 → `ForkError.NotFound`（hard fail，不重试）。
- `awaitAgentWithPermit`：deadline 内 throttle；journal advance 才重试；超时 → `DISTILL_AWAIT_TIMEOUT`。
- `ofForkRuntime`：纯 ForkRuntime 无 journal → fail closed（不铸造 synthetic permit）。

### 输出预算合同（DISTILL-011/012）

- `Process/LargeGate.fs`：单持有者大进程门（FIFO cancelable waiters；first holder wins；release 泵队）——
  一次只允许一个大输出进程占用全局预算窗口（EXEC-013）。
- `Domain/ToolResultBound.fs`：Host 默认 head truncation（2000 行 / 51200 B）之前完成确定性留尾截断：
  `Marker = "...head truncated (tail kept)...\n\n"` + UTF-8 安全 tail（`ContentMaxLines = 1998` /
  `ContentMaxBytes = 51166`），保证 Host 不再二次截断（ARCH-012）。

### Distiller 私有 runtime（EXEC-014）

Distiller 映射子会话：`distillerAgent = ManagedAgent.nameOf AgentTier.Fast Role.Distiller`（固定名，
非 caller 选择）；`HandleOwnership.HostOwnedHidden`（对父 list/join/horizon/guard/恢复不可见，仍持久）；
`run` 工具同步掌控 fork → permit-gated await → 摘要 → 返回；调用方不 join、不承担生命周期。

## 物理落点（CURRENT EVIDENCE）

- Resource：`resources/provider/role/distiller/`（fragment humility 散文）。
- Wiring：`Agent/AgentProgram.fs`（distill tool）、`Infrastructure/OpenCode/Tools/{Distillation,DistillationRuntime,ExecutorTool}.fs`。
- Failure：`Process/LargeGate.fs`、`Domain/ToolResultBound.fs`。
- Tests：包内 `tests/executor-summarize.test.mjs`（MOVE）、`tests/distiller-fragment-humility.test.mjs`（NEW）。

## 边界与弃权（非 normative）

- **GARBAGE——chunk 统计 wire**：蒸馏不得返回 chunk 统计仪表盘、不得叙述 map-reduce 机械过程、
  不得报告 success ratio（Role Law「切割是你的私务」）；不进入未来 WHAT 的任何字段合同。
- **GARBAGE——Meditator/Executor 角色路径**：与 Distiller 无关的已删算法面，见
  历史 how/execution 已删除清单。
- **HOW——机制常数**：`ReduceFanIn=8`、`AwaitAgentTimeoutMs=600_000`、`MemoryBufferBudget=204800`、
  `Spool.ChunkSizeBytes=204800`、`HostMaxLines=2000`/`HostMaxBytes=51200`、
  `ContentMaxLines=1998`/`ContentMaxBytes=51166`、`MarkerBytes=34`：有界性/诚实性才是 WHAT。
- **HOW——当前实现形状**：Distiller 当前是 fast 固定 agent 的 LLM map/reduce；可整体替换为
  deterministic+LLM hybrid summarizer（独立变化测试），WHAT 不动。
- **归属他包**：spool 物理采集（`Process/ProcessOutput.fs`、`Spool.fs`）→ `process-execution`；
  Distiller child 的生命周期/隐藏 handle → `managed-session-lifecycle`；Assignment 机器字段的
  horizon 过滤 → `participant-horizon`；ARCH-012 的 wire 渲染 owner → `provider-projection`。
- **不复制** `process-execution`（exit/onExit/cancel）、`review-judgement`（PERFECT/REVISE）、
  `context-compression`（Blogger/prefix）的命题。
