# 07 — Work Backlog 与 TodoWrite

## 产品契约

调用待办写入工具须提交：
1. **全量** `todos` 列表
2. **`select_methodology`**：至少一个方法论 id（与 `Methodology/Registry.fs` 枚举一致）

工具名：OpenCode/Mux/OMP → `todowrite`；Mimocode → `task`。

## Todo 项形状

```json
{ "content": "可验收的下一步", "status": "pending|in_progress|completed|cancelled", "priority": "high|medium|low" }
```

## 事件持久化与 Fold

- **触发**：`todowrite`/`task` 执行成功，通过 `SessionEventWriter.fs` 追加 `assistant_completed` 事件（携带 `openTodosJson`）
- **写入**：`src/Runtime/EventStore/SessionEventWriter.fs` → `EventLogRuntimeStore.appendAndCacheOrFail`
- **Fold**：`NudgeSnapshotProjection.fs` 从事件流提取 `openTodosJson` 更新 `NudgeSnapshotState.openTodos`

`assistantPayload`（`SessionEventWriter.fs`）构造 payload：`assistantMessage`、`agent`、`model`、`turnId`、`openTodos`。

## 去重清理

`nudge_dedup_cleared` 事件（`NudgeEventWriter.fs`）在 WIP 提交 / 新用户消息时清空去重表，释放 nudge 阻塞。

## 各宿主路径

| 宿主 | 路径 |
| :--- | :--- |
| OpenCode | `src/Hosts/OpenCode/Tools.fs` 注册 `todowrite` |
| Mux | `src/Hosts/Mux/Wrappers.fs` → `todowrite` |
| OMP | `src/Hosts/Omp/TodoTool.fs`、`TodoHooks.fs`（TypeBox schema） |
| Mimocode | `PluginMimoTui.fs` → `task` + sidebar todo 回填 |

## 架构测试

`muxBacklogUsesMuxHost`：backlog 工具须走 Mux 宿主路径，而非误用 OpenCode 注册名。
