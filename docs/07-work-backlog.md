# 07 — Work Backlog 与 TodoWrite

## 产品契约

每次调用待办写入工具必须提交：

1. **全量** `todos` 列表（禁止部分 diff 更新）
2. 五份 **completedWorkReport** 字段（schema 标注 `minLength_` 1024，供 LLM 感知，不强制拦截）
3. **`select_methodology`**：至少一个方法论 id（与 `methodology_*` 注册表一致）

结构校验失败（缺失 `todos` / `select_methodology` 或 todo 项非法）→ **不** append `work_backlog_committed`。报告字段长度不足不阻塞执行，也不触发批评。

## 宿主工具名

| 宿主 | 工具名 |
| :--- | :--- |
| OpenCode / Mux / OMP | `todowrite` |
| Mimocode | `task` |

别名与规范化：`Kernel.HostTools`（如 `todo_write` → `todowrite`）。

## Todo 项形状

```json
{
  "content": "可验收的下一步",
  "status": "pending | in_progress | completed | cancelled",
  "priority": "high | medium | low"
}
```

业务规则鼓励**至多一个** `in_progress`（由 schema/校验与提示词共同约束）。

## 五份报告字段

| 字段 | 用途 |
| :--- | :--- |
| `ahaMoments` | 突破与关键认识 |
| `changesAndReasons` | 改动与理由 |
| `gotchas` | 陷阱与边界 |
| `lessonsAndConventions` | 惯例与教训 |
| `plan` | 计划或下一步 |

文案 SSOT 片段在 `src/Kernel/Methodology/` / `src/Kernel/CapsPrelude.fs`；schema 与执行边界在 `src/Runtime/Tooling/WorkBacklogToolsCodec.fs`、各宿主工具 schema。

## 事件：`work_backlog_committed`

- **触发**：工具执行成功且参数通过 Kernel/codec 校验
- **写入**：`src/Runtime/EventStore/BacklogEventWriter.fs` 与 EventStore append 链
- **Fold**：`foldWorkBacklogSnapshot` 取**最后一条** committed 为当前快照
- **展示**：`src/Kernel/Backlog/BacklogProjection.fs` 与 MessageTransform 格式化 fold 结果（**非**再从历史 tool 消息 fold SSOT）

## 与 compaction / context budget 的关系

- Backlog **不**依赖 compaction 后注入的 anchor prompt。
- 当上下文预算逼近阈值时，可**主动要求**调用 `todowrite` 折叠进度（见 [13-context-budget.md](./13-context-budget.md)）；触发后仍须满足五报告校验才落盘。

## Mimocode 特例

- `PluginMimoTui`：`task` 与 sidebar todo 回填
- `MimoTodoTool` 与 OpenCode `todowrite` 共享 Kernel 校验语义

## Mux

`muxBacklogUsesMuxHost` 架构测试：backlog 工具须走 Mux 宿主路径，而非误用 OpenCode 注册名。

## OMP

`src/Hosts/Omp/TodoTool.fs`、`TodoHooks.fs`：TypeBox schema + tool_result 后处理；`select_methodology` 与 OpenCode 同源枚举。

## 测试与验收

- `tests/` 中 backlog projection、codec、集成 tool spec（如 `IntegrationMuxToolSpecsTodo`）
- `ContextBudgetAfterTodoTests` 等与预算联动

## 源码索引

| 模块 | 路径 |
| :--- | :--- |
| 投影核心 | `src/Kernel/Backlog/BacklogProjection.fs`、`src/Kernel/WorkBacklog.fs` |
| Append | `src/Runtime/EventStore/BacklogEventWriter.fs`、`EventStore.fs` |
| Sync | `src/Runtime/EventStore/EventLogRuntimeSync.fs`、`SessionProjectionStore.fs` |
| 工具 codec | `src/Runtime/Tooling/WorkBacklogToolsCodec.fs` |

## 相关文档

- [05-event-sourcing.md](./05-event-sourcing.md)
- [09-methodology.md](./09-methodology.md)
- [13-context-budget.md](./13-context-budget.md)
