# 10 — 消息变换管线

## 目的

在宿主将消息数组交给 LLM **之前**，插入 caps、backlog、review replay、Semble、parallel 提示、context budget 等。共享逻辑在 **Shell**；宿主只挂 hook 与 `RuntimeScope`。`

## 核心模块

| 模块 | 职责 |
| :--- | :--- |
| `Shell/MessageTransformPipeline.fs` | 主编排、`applyContextBudget`、`tryInjectParallelToolPrompt` |
| `Shell/MessageTransformCore.fs` | 纯变换步骤 |
| `Shell/ToolHookRuntime.fs` | 控制字段软校验与净化 |
| `Kernel/MessageTransformPolicy.fs` | 策略 |
| `Shell/SembleSearch.fs` | investigator 断点注入 |

架构测试：三宿主须 `UsesProjectionPolicy` + Shell caps cache；`noQuadraticListAppend`。

## 管线阶段（实现顺序）

以 `runMessageTransformPipeline` 为准：

1. 剥离 synthetic（含 `semble-synth-`、`parallel-tool-synth-`、`context-budget-nudge`）
2. Caps / 清理
3. Backlog 投影（事件 fold，非历史 tool SSOT）
4. Review replay
5. **applyContextBudget**（见 [13](./13-context-budget.md)）
6. **tryInjectParallelToolPrompt**
7. Semble（investigator + 开关）

## 并行工具鼓励 (FEATURE1)

**条件**（`tryInjectParallelToolPrompt`）：

- 过滤后 `Native` 消息链
- **最后一条**带真实 tool 的 `Assistant` 消息中，**有且仅有 1** 个真实 tool part
- 忽略 `semble-call-*`、`caps-call-*`
- 白名单 = `ToolCatalog.all` 名 + `"methodology"`
- 且该轮已有对应 `ToolResult`（单步已返回）

**动作**：追加 synthetic `User`，id `parallel-tool-synth-<callID>`，`source = Synthetic "parallel-tool-synth-"`；文案 SSOT 在 `PromptFragments`（架构测试 `parallelToolPromptSSOTGuard`）。下轮 `stripSyntheticBySource` 剥离。

## Semble MCP 注入

**原则**：Shell 自管 MCP，**不**注册进宿主 MCP 表；失败静默返回原消息。

| 项 | 规约 |
| :--- | :--- |
| 启动 | `uvx` + `Kernel.Config` 中 semble ref（`SEMBLE_MCP_REF`） |
| 断点 | `lastBreakpoint: session → 消息长度`；上下文 ≥ **50** 字符才 search |
| 提取 | `[startIndex, len)` 内仅 user/assistant 文本，**排除** 所有 ToolPart |
| 伪装 | 每条结果 → `assistant` read call + `toolResult`，id 前缀 `semble-synth-` |
| 格式 | 行 `%6d|content`（对齐 `FileSys` read） |

仅 **investigator** agent 路径启用。测试：`tests/SembleInjectionTests.fs`。

## 空输出 → Fallback（与 [12](./12-fallback.md)）

`SessionIdle` 时若最后 assistant 无 tool、无可见 text → 译为 `EmptyOutputError`，Fallback `SendContinue`，`Consumed=true` **短路 nudge**。实现：`FallbackMessageCodec`、`FallbackEventBridge`。

## Hook 复杂度纪律

热路径须 O(消息长度) 或 O(log N)，禁止会话增长导致 O(N²)：

| 区域 | 要点 |
| :--- | :--- |
| Read 去重 | `Kernel/Dedup`：`Set` 指纹 + 有界 raw 列表 |
| EventLog 缓存 | `ResizeArray` 追加，非 `list @ [e]` |
| Backlog fold | 头插 `::`，边界再 `rev` |
| Mux read dedup | 嵌套 `Map` 索引 |

原 hooks 复杂度审计已并入本节；豁免：`FuzzyFormat` 固定行数拼接等 O(1) 有界操作。

## 宿主入口

`Opencode/MessageTransform.fs`、`Mux/MessageTransform.fs`、`Omp/MessageTransform.fs`。

OpenCode：**原地 mutate** hook 字段（`AGENTS.md`）。

## 相关

- [06-review-and-nudge.md](./06-review-and-nudge.md)
- [07-work-backlog.md](./07-work-backlog.md)
- [13-context-budget.md](./13-context-budget.md)