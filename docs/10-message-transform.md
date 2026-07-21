# 10 — 消息变换管线

## 目的

在宿主将消息数组交给 LLM **之前**，插入 caps、review replay、Semble、parallel 提示等。共享逻辑在 `src/Runtime/MessageTransform/Pipeline.fs`；宿主只挂 hook 与 `RuntimeScope`。

## 管线阶段

`runMessageTransformPipeline`（`Pipeline.fs`）：

1. Caps — 按 `scopeId × CapsRevision × PolicyVersion` 缓存，复用段引用
2. `tryInjectParallelToolPrompt`（`ParallelHintStage.fs`）
3. Semble（inspector 断点注入，`SembleSearch.fs`）
4. `replaceArrayInPlace` 原地替换宿主数组

`TransformState` 维护 Caps/Top slot 两段引用；revision/key 未变时复用对象引用减少分配。

## 并行工具鼓励（FEATURE1）

条件（`tryInjectParallelToolPrompt`，`ParallelHintStage.fs`）：

- 过滤后 `Native` 消息链
- **最后一条**带真实 tool 的 `Assistant` 消息中，**有且仅有 1** 个真实 tool part
- 忽略 `semble-call-*`、`caps-call-*`
- 白名单 = `ToolCatalog.all` 名 + `"methodology"`
- 该轮已有对应 `ToolResult`（单步已返回）

动作：追加 synthetic `User`，id `parallel-tool-synth-<callID>`，下轮 `stripSyntheticBySource` 剥离。文案 SSOT 在 `PromptFragments.fs`（架构测试 `parallelToolPromptSSOTGuard`）。

## Semble MCP 注入

- 仅 **inspector** agent 路径启用
- 上下文 ≥ **50** 字符才 search
- 提取 user/assistant 文本，排除所有 ToolPart
- 每条结果 → `assistant` read call + `toolResult`，id 前缀 `semble-synth-`
- Shell 自管 MCP，不注册进宿主 MCP 表；失败静默返回原消息

## 空输出 → Fallback

`SessionIdle` 时最后 assistant 无 tool、text 为空 → `EmptyOutputError`（`FallbackMessageCodec.fs`），Fallback `SendContinue`，`Consumed=true` **短路 nudge**。

## Caps 注入策略

`src/Kernel/MessageTransformPolicy.fs`：

- `CapsInjectionPolicy`：`Include | Exclude`（browser/executor/title/compaction/exec/explore 排除）
- `ParallelHintPolicy`：同轴排除
- `shouldExcludeAgentFromProjection`：子 workspace 额外排除 exec/explore

## Hook 复杂度纪律

热路径 O(消息长度) 或 O(log N)。禁止会话增长导致 O(N²)：
- Read 去重：`Kernel/Dedup`（Set 指纹 + 有界 raw 列表）
- EventLog 缓存：`ResizeArray` 追加，非 `list @ [e]`

## 控制字段生命周期

`warn_tdd`、`warn`、`warn_reuse`：key 存在 value undefined 的软协议字段，注入 Schema description 强制 LLM 注意，宿主执行前后不剥离。

## 宿主入口

`src/Hosts/OpenCode/MessageTransformPipeline.fs`、`src/Hosts/Mux/MessageTransform.fs`、`src/Hosts/Omp/MessageTransform.fs`。OpenCode：**原地 mutate** hook 字段（`AGENTS.md`）。
