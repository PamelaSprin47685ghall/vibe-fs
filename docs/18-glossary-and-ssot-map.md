# 18 — 术语表与 SSOT 对照

## 术语

| 术语 | 含义 |
| :--- | :--- |
| Kernel | 纯 F# 领域层，`src/Kernel/` |
| Runtime | IO 与 codec，`src/Runtime/` |
| Host | `Opencode | Mimocode | Mux | Omp` |
| SSOT | Single Source of Truth |
| Fold | 对事件列表的纯 reduce，得当前投影 |
| With-Review | `/loop` 驱动的审查循环 |
| Caps | 系统前导提示（宝典/铁律/工具说明） |
| Subsession | 轻量 Actor 消息泵，隔离子代理错误与状态 |
| Fallback 续命 | 模型降级后的租约生命周期（Requested → DispatchStarted → Dispatched → Settled/Failed/Cancelled） |
| PromiseQueue | `SerialQueue`：局部可变 `tail` 锁队尾，内部 catch 防断链 |
| EventLog | `[workspace]/.wanxiangshu.ndjson` + lock |
| Outbox | 与领域事件同一提交屏障落盘的 Effect Intent 持久化区 |
| CommandProcessor | 10 步串行提交器（SubsessionActor 三部件之一） |
| EffectSupervisor | Outbox 消费 → 宿主调用 → Command 回流（SubsessionActor 三部件之一） |
| ResourceScope | RAII 定时器管理（SubsessionActor 三部件之一） |
| Durable Resource | 需要跨重启恢复的资源（Turn Deadline、Abort Deadline） |
| Invocation Resource | 调用栈/会话 Scope 内的瞬时资源 |
| Leasing | `SessionOwner` + `LeaseStatus` 驱动的续命/操作互斥 |

## SSOT 总表

| Concern | SSOT | 非 SSOT |
| :--- | :--- | :--- |
| Review task / loop 活跃 | `.wanxiangshu.ndjson` + `foldReviewTask` | 仅读 ReviewStore |
| Todo backlog 内容 | `.wanxiangshu.ndjson`（`openTodosJson`） | 历史 tool 消息全文 fold |
| Nudge 去重 | `NudgeDedupState` 事件 fold | 内存去重表单独为准 |
| Nudge 决策快照 | `NudgeSnapshotState` 事件 fold | 末条消息文本直接嗅探 |
| 工具 description | `Kernel/ToolCatalog/Registry.fs` | 宿主手写重复文案 |
| 工具权限 | `ToolPermission.fs` | 宿主 if/else |
| 方法论 enum | `Methodology/Registry.fs` 派生 | 三处手写列表 |
| 宝典/铁律正文 | `src/Kernel/CapsPrelude.fs` | MessageTransform 内联长文 |
| 宿主工具名映射 | `HostTools.fs` | 魔字符串散落 |
| Subsession 活跃 run | `.wanxiangshu.ndjson` + `subsession_*` 事件 + `SessionSafetyProjection` | 仅内存 actor 注册表 |
| Fallback 续命租约 | `.wanxiangshu.ndjson` + `continuation_*` 事件 + `SessionControl/LeaseTransitions.fs` | 仅内存 FallbackRuntimeStore |
| Fallback 注入记忆 | `recordHostAcceptedContinuation` 事件 | 嗅探消息零宽字符 |
| 续命 Dispatched | `recordHostAcceptedContinuation`（host evidence） | 禁止 prompt Promise 返回即 Dispatched |
| 万象阵 DAG | `.wanxiangshu.ndjson` + `Wanxiangzhen/` fold + git 第二真相源 | session 历史 fold |

## 文件路径速查

| 路径 | 说明 |
| :--- | :--- |
| `[workspace]/.wanxiangshu.ndjson` | 万象术 + 万象阵事件日志（共用） |
| `build/src/Hosts/Mux/Plugin.js` | 默认包入口 |
| `build/src/Hosts/Omp/Plugin.js` | OMP 入口 |
| `src/Kernel/CapsPrelude.fs` | 宝典/铁律 SSOT |
| `src/Runtime/EventStore/EventLogCodec.fs` | NDJSON codec |
| `src/Runtime/EventStore/EventLogRuntimeStore.fs` | 进程内 EventLogStore |
