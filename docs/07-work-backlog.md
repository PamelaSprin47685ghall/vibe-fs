# 07 — Work Backlog 与 TodoWrite

## 产品契约

调用待办写入工具需要提交：

1. **全量** `todos` 列表
2. **`select_methodology`**：至少一个方法论 id（与 `methodology_*` 注册表一致）

待办写入工具不强制要求写任何报告字段，爱用不用。

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

## 事件：`work_backlog_committed`

- **触发**：工具执行成功且参数通过 Kernel/codec 校验
- **写入**：`src/Runtime/EventStore/BacklogEventWriter.fs` 与 EventStore append 链
- **Fold**：`foldWorkBacklogSnapshot` 取**最后一条** committed 为当前快照

## Mimocode 特例

- `PluginMimoTui`：`task` 与 sidebar todo 回填
- `MimoTodoTool` 与 OpenCode `todowrite` 共享 Kernel 校验语义

## Mux

`muxBacklogUsesMuxHost` 架构测试：backlog 工具须走 Mux 宿主路径，而非误用 OpenCode 注册名。

## OMP

`src/Hosts/Omp/TodoTool.fs`、`TodoHooks.fs`：TypeBox schema + tool_result 后处理；`select_methodology` 与 OpenCode 同源枚举。

## 相关文档

- [05-event-sourcing.md](./05-event-sourcing.md)
- [09-methodology.md](./09-methodology.md)
