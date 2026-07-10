# 04 — Shell 副作用与边界层

## 职责

Shell（约 **139** 个 `.fs`）是 Kernel 与 Node/宿主之间的**唯一**常规 IO 通道：文件、锁、子进程、网络、MCP、动态对象编解码、运行时队列、review/nudge/eventlog 运行时。

## 能力分簇

| 分簇 | 代表模块 | 说明 |
| :--- | :--- | :--- |
| 文件系统 | `FileSys`、`WorkspaceFiles` | 读写工作区 |
| 事件日志 | `EventLogCodec`、`EventLogIo`、`EventLogFiles`、`EventLogRuntime*` | NDJSON、锁、缓存、sync |
| 搜索 | `FuzzyFinderShell`、`FuzzySearch/*`、`FuzzyIteratorStore` | fff 后端、iterator 状态 |
| Semble | `SembleMcp`、`SembleSearch` | MCP 客户端与 investigator 断点注入 |
| 执行器 | `Executor*`、`SessionExecutor` | shell/python/js、spawn、会话级执行 |
| Tree-sitter | `TreeSitterShell`、`TreeSitterPlatform` | 可选语法能力 |
| 网络 | `WebSearchApi`、`WebFetch`、`TitleFetchGuardCommon` | 搜索与抓取守卫 |
| 动态类型 | `Dyn`、`DynField`、`ErrorClassify`、`JsArrayMutate`、`PromiseStr` | `obj` 安全访问 |
| 并发 | `PromiseQueue`、`SerialStateHolder`、`LivelockGuard`、`CoordinatorLifecycle` | 串行队列与守卫 |
| 时钟 | `Clock` | 可注入时间（测试） |
| 子代理 | `ChildAgentRegistry`、`SubagentSpawn`、`SubagentIo`、`SubagentToolExecute`、`MuxSubagentToolExecute`、`SessionIoSpawn` | 各宿主 spawn 公共路径 |
| Review/Nudge | `ReviewRuntime`、`ReviewReplaySync`、`NudgeRuntime` | 投影同步与异步 nudge |
| Fallback | `FallbackConfigCodec`、`FallbackRuntimeState`、`FallbackMessageCodec`、`FallbackEventBridge` | 降级运行时 |
| Context budget | `ContextBudgetStore`、`ContextBudgetUsageCodec` | 用量与触发 |
| Caps | `CapsFileCache`、`CapsSynthCommon`、`OmpCaps` | caps 文件缓存与组装 |
| MessageTransform | `MessageTransformPipeline`、`MessageTransformCore`、`MessageTransformHost*`、`Messaging*Codec`、`Chat*Codec` | 共享管线 |
| Tool 编解码 | `ToolArgsDecode`、`ToolExecute`、`ToolRuntimeContext`、`*ToolsCodec`、`JsonSchemaBuilders`、`MuxJsonSchema` | 参数解析与执行分发 |
| 宿主专用 codec | `Opencode*Codec`、`Mux*Codec` | hook 入参/出参 |
| OMP 绑定 | `OmpHostBindings`、`MuxHostBindings` | 宿主 API 薄封装 |

## EventLog 运行时链（写路径）

```text
业务 hook / 工具成功
  → EventLogRuntimeAppend.append*
  → EventLogRuntimeStore.appendAndCache
  → EventLogFiles.EventLogStore.AppendEvent
  → EventLogIo.appendLine (先锁后写)
  → foldWan 更新进程内 SessionState
```

读路径：`GetSessionState` / `ReadAllEvents` → `EventLogRuntimeSync` → `ReviewRuntime` / `ProjectionStore`。

文件名为 **`.wanxiangshu.ndjson`**，锁文件 **`.wanxiangshu.ndjson.lock`**（`EventLogCodec`）。

## Tool 执行路径（概念）

1. 宿主传入 `toolName` + `args`（`obj`）
2. `ToolArgsDecode` → 强类型参数
3. `Kernel` 校验（权限、业务规则）
4. `ToolExecute` / 子代理模块执行 Shell IO
5. 结果编码为宿主 `parts` / `output`

Opencode/Mux 经 `SubagentToolExecute` / `MuxSubagentToolExecute`；OMP 在 `Omp/` 内调用 Shell，但不 `open` Opencode/Mux。

## MessageTransform 管线

`MessageTransformPipeline` 接收计划（session、agent、directory、scope、token 预算等），依次：

- 清理/过滤消息
- caps / backlog 投影注入
- Semble 注入（开关）
- parallel tool prompt（单工具伪装并行时的 SSOT 文案）
- context budget 阶段（见 [13-context-budget.md](./13-context-budget.md)）

宿主入口：

- `Opencode/MessageTransform.fs`
- `Mux/MessageTransform.fs`
- `Omp/MessageTransform.fs`

## RuntimeScope

每个会话/工作区上下文持有：

- Fuzzy iterator store
- 其他 scope 级可变状态

**禁止**在 Shell 模块级复制第二份全局 store（架构测试）。

## Dyn 纪律

Kernel 不得 `open Dyn`。Shell codec 集中 `get`/`set`/数组变异；hook output 必须经 `OpencodeHookInputCodec` / `MuxHookInputCodec` 等，避免 Opencode 插件「换引用」导致 hook 失效（`AGENTS.md`）。

## 与 ../mux、../oh-my-pi 的改动范围

- **Mux**：优先改本仓库；必要时改 `../mux` **binding**，核心最小动。
- **OMP**：禁止改 `../oh-my-pi` 上游，仅参考；实现留在本仓库 `Omp/` + Shell。

## 相关文档

- [05-event-sourcing.md](./05-event-sourcing.md)
- [10-message-transform.md](./10-message-transform.md)
- [11-subagents.md](./11-subagents.md)