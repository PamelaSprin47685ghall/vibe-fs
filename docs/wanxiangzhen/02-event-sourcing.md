# 万象阵：工作区事件溯源

> **权威顺序**：实现 > 本文件 > `03-dev-talk` 历史叙述。
> **物理文件**：`[workspace]/.wanxiangshu.ndjson` + `.wanxiangshu.ndjson.lock`（`src/Runtime/EventStore/EventLogFile.fs`），与万象术 review/nudge 等**共用**。
> **逻辑 SSOT**：`src/Kernel/Wanxiangzhen/SquadEvent.fs` fold + `src/Runtime/Wanxiangzhen/CoordinatorReplay.fs` + `SquadEventWanCodec.fs` + `SquadEventLogRuntime.fs`。

## 0. 动机

DAG 状态机生命周期长（贯穿整个 session），compaction 跨越概率高。不能依赖 `session.messages()` 或 `messages.transform` 切片作为 DAG 重放依据。

## 1. 公理

| 公理 | 含义 |
| :--- | :--- |
| 意图不落盘 | 未校验的自然语言、草稿参数不写入 |
| 事件才落盘 | 命令经校验产生不可抵赖事实后追加一行 |
| 当前状态 = 积分 | 内存 `Dag` = 对 NDJSON 按 `session` 过滤后纯 fold 的结果 |
| 先写盘后改内存 | append 成功 → 更新内存 DAG；失败 = 该事实未发生 |
| 一行一事件 | NDJSON：每行一个自包含 JSON 对象 |
| 按 session 分区 | 每行含 `session` 字段；fold 时按 sessionId 过滤 |
| git 仍为第二真相源 | `merge-base --is-ancestor` 校正 `Running`/`Submitted` 任务 |

## 2. 物理文件

```
[workspace]/.wanxiangshu.ndjson
[workspace]/.wanxiangshu.ndjson.lock
```

- **追加**：只写文件末尾（万象阵事件与万象术事件同行序追加）
- **损坏行**：读取时遇无法解析的非空行 → 截断（该行及之后丢弃），不跳过坏行继续 fold
- **锁**：与万象术同一 lock 文件；进程内 `EventLogStore` + `SerialQueue` 串行 append
- **迁移对照**：早期 `.wanxiangzhen.ndjson` / `.wanxiangzhen.ndjson.lock` 已废止

## 3. 行格式

每行 `WanEvent`：

```fsharp
type WanEvent =
    { V: int; Session: string; Kind: string; At: string
      Payload: Map<string, string>
      EventId: string option; WriterId: string option
      Sequence: int option; Checksum: string option }
```

编解码：`SquadEventWanCodec.fs`（`squadEventToWanEvent`/`wanEventToSquadEvent`）。

## 4. 事件类型（`SquadEvent.fs`）

| kind | 触发时机 | payload 要点 |
| :--- | :--- | :--- |
| `squad_created` | `/squad` command | `requirement` |
| `tasks_created` | `squad_update` execute | `tasksJson`（TaskItem[]） |
| `task_started` | Scheduler 启动 task | `task_id`, `worktree_path`, `branch_name` |
| `task_submitted` | `POST /submit` 进入 ff 前 | `task_id`, `commit_sha` |
| `task_merged` | ff 成功 | `task_id`, `master_sha` |
| `task_done` | done beacon / PID 退出 | `task_id`, `merged` (bool) |
| `task_error` | worktree/git 启动失败 | `task_id`, `error` |
| `squad_cancelled` | `/squad-kill` | — |

增 kind 须先改 `SquadEvent.fs` + `EventKind.fs` + `SquadEventWanCodec.fs` + 测试。

## 5. 分层职责

```
HTTP / squad_update / Scheduler
    → Kernel: validate → SquadEvent（意图）
    → Shell: withFileLock → append NDJSON → on OK foldEvent into Dag
```

| 层 | 模块 | 职责 |
| :--- | :--- | :--- |
| `src/Kernel/Wanxiangzhen/SquadEvent.fs` | `SquadEvent` DU、`foldEvent`/`foldEvents`、`eventTypeName` |
| `src/Runtime/Wanxiangzhen/SquadEventWanCodec.fs` | `SquadEvent ↔ WanEvent` 持久化编解码 |
| `src/Runtime/Wanxiangzhen/SquadEventLogRuntime.fs` | 经共用 `EventStore` 读写 `.wanxiangshu.ndjson` |
| `src/Runtime/Wanxiangzhen/CoordinatorReplay.fs` | `replayFromEventLog`：读 NDJSON → fold → git reconcile |
| `src/Runtime/Wanxiangzhen/EventCodec.fs` | **仅**展示用 yaml frontmatter + prose 编解码（`encodeEvent`/`decodeEvents`）；**不参与** durable 重放 |

## 6. 已删除/废弃的行为

| 旧行为（已删除） | 新行为 |
| :--- | :--- |
| `session.messages()` 扫文本 fold DAG | `.wanxiangshu.ndjson` 内 `squad_*`/`task_*` 行 fold |
| `session.prompt` 写事件作为 SSOT | `appendSquadEvent` = SSOT；prompt 可选且失败不丢事实 |
| `.squad/state.json` MVP 兜底 | MVP 不需要；NDJSON 已足够 |
| 独立 `.wanxiangzhen.ndjson` | 与万象术共用 `.wanxiangshu.ndjson` |

**保留**：`SerialQueue` 保护 masterBranch git 操作；`commitEvent` 内 `InjectQueue` 现阶段仍串行 append→通知链，但 `session.prompt` 仅在需要 LLM 行动时显式发起，失败不回滚 NDJSON 事实。

## 7. 验收标准

- 重启 coordinator 后，**仅**依赖 `.wanxiangshu.ndjson` 中 `squad_*`/`task_*` 行可恢复当前 DAG
- compaction 后无 anchor 注入，DAG 仍正确
- `npm run build-and-test` 全绿
- Kernel 无 `Dyn`；fold 在 Kernel；append/fs 仅在 Shell
