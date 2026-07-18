# 10 — 消息变换管线

## 目的

在宿主将消息数组交给 LLM **之前**，插入 caps、backlog、review replay、Semble、parallel 提示、context budget 等。共享逻辑在 `src/Runtime/MessageTransform/`；宿主只挂 hook 与 `RuntimeScope`。

## 核心模块

| 模块 | 职责 |
| :--- | :--- |
| `src/Runtime/MessageTransform/Pipeline.fs` | 主编排、`applyContextBudget`、阶段组合 |
| `src/Runtime/MessageTransform/Stack.fs` | TransformState 三段状态（Caps、Backlog、Top slot） |
| `src/Runtime/Tooling/ToolHookRuntime.fs` | 控制字段软校验、净化、after 还原、违例批评 |
| `src/Kernel/MessageTransformPolicy.fs` | 策略（Backlog/Caps/ParallelHint/ContextBudget 四分） |
| `src/Runtime/MessageTransform/ParallelHintStage.fs` | 并行提示阶段 |
| `src/Runtime/Search/SembleSearch.fs` | inspector 断点注入 |

架构测试：三宿主须 `UsesProjectionPolicy` + Shell caps cache；`noQuadraticListAppend`。

## 管线阶段（实现顺序）

以 `runMessageTransformPipeline` 为准：

1. Caps — 按 `scopeId × CapsRevision × PolicyVersion` 缓存，复用段引用
2. Backlog 投影（事件 fold，非历史 tool SSOT；`BacklogRevision` 驱动）
3. **applyContextBudget**（`BudgetRevision` + `TopSlotKey` 驱动，见 [13](./13-context-budget.md)）
4. **tryInjectParallelToolPrompt**（与 budget nudge 互斥，`ParallelHintTop` key）
5. Semble（inspector + 开关）
6. **replaceArrayInPlace** 原地替换宿主数组

管线不再剥离 synthetic 段：host 每轮从 DB 重读数组，上一轮 synthetic 自然消失；`TransformState` 维护三段引用，revision/key 未变时复用对象引用以减少分配。发送前以 canonical outbound bytes/prefix equality 验证 prompt-cache 可命中。

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

仅 **inspector** agent 路径启用。测试：`tests/SembleInjectionTests.fs`。

## 空输出 → Fallback（与 [12](./12-fallback.md)）

`SessionIdle` 时若最后 assistant 无 tool、无可见 text → 译为 `EmptyOutputError`，Fallback `SendContinue`，`Consumed=true` **短路 nudge**。实现：`src/Runtime/Fallback/FallbackMessageCodec.fs`、`Coordinator.fs`。

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

`src/Hosts/OpenCode/MessageTransformPipeline.fs`、`src/Hosts/Mux/MessageTransform.fs`、`src/Hosts/Omp/MessageTransform.fs`。

OpenCode：**原地 mutate** hook 字段（`AGENTS.md`）。

## 控制字段生命周期

控制字段（`warn_tdd`、`warn`、`warn_reuse`）在工具执行边界经历完整生命周期：

| 阶段 | 动作 | 模块 |
| :--- | :--- | :--- |
| Schema 注册 | 注入 `required_` 元数据，不放入 Host 强制 `required`/`minLength` | `ToolHookRuntime.decorateAndValidateSchema` |
| Before hook | 提取并原地删除字段，构造 `ControlEnvelope` 存入 `ToolComplianceStore` | `executeBeforeGateway` + `saveCompliance` |
| 真实执行 | 工具收到净化后的业务参数 | Host execute |
| After hook | 追加违例批评（`WANXIANGSHU_COMPLIANCE_REPRIMAND`），调用 `restoreWarnToArgs` 将原始字段恢复到历史可见 args | `tryGetCompliance` → `appendCriticism` → `restoreWarnToArgs` → `removeCompliance` |
| Finally | 删除 compliance envelope | `removeCompliance` |

缺失/空白/非规范值的字段不阻止工具执行，仅在 after 阶段追加一次严厉批评。硬拒绝只保留给 malformed business args、权限/安全拒绝、解析失败或净化后仍泄漏的控制字段。

## 相关

- [06-review-and-nudge.md](./06-review-and-nudge.md)
- [07-work-backlog.md](./07-work-backlog.md)
- [13-context-budget.md](./13-context-budget.md)
