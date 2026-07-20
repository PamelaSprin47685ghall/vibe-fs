# 18 — 术语表与 SSOT 对照

## 术语

| 术语 | 含义 |
| :--- | :--- |
| Kernel | 纯 F# 领域层，`src/Kernel/` |
| Runtime | IO 与 codec，`src/Runtime/` |
| Host | `Opencode \| Mimocode \| Mux \| Omp` |
| SSOT | Single Source of Truth；此处多指 NDJSON 或指定模块 |
| Fold | 对事件列表的纯 reduce，得当前投影 |
| With-Review | `/loop` 驱动的审查循环 |
| Caps | 系统前导提示（宝典/铁律/工具说明） |
| WIP | `submit_review` 部分完成标记 |
| Iterator | fuzzy_continue / continue 的翻页或子会话句柄 |
| Subsession | 轻量 Actor 消息泵，隔离子代理的错误与状态 |
| Fallback 续命 | 模型降级后的 v1 continuation 事件，或 `src/Kernel/Fallback/Continuation.fs` 定义的 v2 continuation |
| 门闩 (Gate) | `src/Runtime/Fallback/GateState.fs` 与 session runtime 中的互斥标志位 |
| Flow Kernel | 基于 `IAsyncEnumerable` 的最小流程代数，与九条语义法律配套的领域级流程算子集 |
| RAII Resource Scope | 由 `CommittedState → ResourceSpec` 投影驱动的资源管理范式，根据状态 Diff 自动 Acquire/Dispose 层级 Scope |
| Durable Resource | 需要跨进程重启恢复的资源（Turn Deadline、Abort Deadline），由 ResourcePlan 管理 |
| Invocation Resource | 属于调用栈或会话 Scope 的瞬时资源（CallerReplyLease、AbortSignal），重启不恢复 |
| Effect Supervisor | 从持久化 Outbox 消费 Effect 的流式监督器，执行宿主调用后映射为领域 Command 回流至 Inbox |
| Outbox | 与领域事件同一提交屏障落盘的 Effect Intent 持久化区域，供 Effect Supervisor 消费 |
| CommandProcessor | 极薄的串行提交器，执行 Dequeue → Validate → Decide → Persist → Commit → Reconcile 固定十步 |
| CommittedProgress | 来自已提交事件的流，可重放、不丢失、可用于业务决策 |
| EphemeralTelemetry | best-effort 遥测流；latest-wins；不得驱动领域状态转换；不能作为恢复依据 |
| Stable Resource Identity | 资源复用由稳定 Key（如 `TurnDeadline(turnId)`）决定，而非 State 对象引用相等 |
| No Reentrancy Law | Effect 完成后只能 enqueue Command → Inbox，绝不能同步或递归调用 processor.Handle Command |

## 真相源对照表

| Concern | SSOT 位置 | 非真相 |
| :--- | :--- | :--- |
| Review task / loop 活跃 | `.wanxiangshu.ndjson` + `foldReviewTask` | 仅读 `ReviewStore` 捷径、compaction 前消息 |
| Todo backlog 内容 | 最后 `work_backlog_committed` | 历史 todowrite tool 消息全文 fold |
| Nudge 去重 | `foldNudgeDedup` / `nudge_*` 事件 | 内存去重表单独为准 |
| Nudge 决策快照 | `foldNudgeSnapshot` / `assistant_completed` 等 | 末条消息文本直接嗅探 |
| 工具 description | `Kernel.ToolCatalog` | 宿主手写重复文案 |
| 工具权限 | `ToolPermission` + `ToolCatalog.Classification` | 宿主 if/else |
| 方法论 enum | `Methodology.Registry` 派生 | 三处手写列表 |
| 宝典/铁律正文 | `src/Kernel/CapsPrelude.fs`（组装经 Runtime cache） | MessageTransform 内联长文 |
| 宿主工具名映射 | `HostTools` | 魔字符串散落 |
| OpenCode hook 字段 | 原地 mutate + codec | 替换 output 对象引用 |
| Subsession 活跃 run | `.wanxiangshu.ndjson` + `subsession_*` 事件 + `SessionSafetyProjection` | 仅内存 actor 注册表 |
| Subsession Agent 状态 | `src/Kernel/Subsession/Decision.fs` 纯函数 + NDJSON 决策信封 | 宿主 session 消息历史 |
| 万象阵 DAG | **物理**：`[workspace]/.wanxiangshu.ndjson` + `.wanxiangshu.ndjson.lock`（与万象术共用）；**逻辑**：`squad_*`/`task_*` 行 + `src/Kernel/Wanxiangzhen/` fold + `src/Runtime/Wanxiangzhen/CoordinatorReplay.fs` / `EventLogSquadProjection.fs` | 宿主 session 历史、已废止的独立 `.wanxiangzhen.ndjson` 文件名 |
| 子代理 durable 投影 | `subagent_spawned` / `subagent_continued` + `foldSubagents` | 仅 `SubagentIteratorStore` 内存 |
| Fallback 续命租约 | `.wanxiangshu.ndjson` + `continuation_*` 事件 + `src/Kernel/EventSourcing/Fold.fs` session-control projection | `Runtime/Fallback` 内存状态 alone |
| Fallback 注入记忆 | `fallback_continue_injected` 旧事件（兼容读取） | 嗅探消息零宽字符 |
| 续命 v2 projection | `.wanxiangshu.ndjson` + v2 continuation 事件 + `src/Kernel/Fallback/ContinuationProjection.fs` | 仅内存的 projection 通知 |
| 上下文预算 cycle | `src/Runtime/Execution/ContextBudgetStore.fs` 与 projection metadata（重启语义以实现为准） | 仅 `maxInputTokens` 静态值 |
| 会话拥有者 | `continuation_*` / `human_turn_started` 事件 + `SessionOwner` fold | 内存 `FallbackRuntimeState` |
| Continuation effect | `ContinuationCommandProcessor` 产生的 `ContinuationEffect` 与 continuation 事件 | 仅内存通知被误称为 durable outbox |
| 续命 payload 文本 | OpenCode `ActionExecutor.sendContinueImpl` ZWSP `"\u200B"` | 见 `docs/CONTINUATION_PATH.md` |
| 续命 Dispatched | `recordHostAcceptedContinuation`（host evidence only） | 禁止 prompt Promise 返回即 Dispatched |

## 文件路径速查

| 路径 | 说明 |
| :--- | :--- |
| `[workspace]/.wanxiangshu.ndjson` | 万象术事件日志（含万象阵 `squad_*`/`task_*` 行和 `subsession_*` 行） |
| `[workspace]/.wanxiangshu.ndjson.lock` | 追加锁 |
| `build/src/Hosts/Mux/Plugin.js` | 默认包入口 |
| `build/src/Hosts/Omp/Plugin.js` | OMP 入口 |
| `tests/logs/*.verbose.log` | 测试详细日志（若测试入口生成） |
| `REF.md` | 若存在则为架构演进参考，不覆盖源码事实 |

## 文档维护

- 规格只写 `docs/`；冲突以 `src/` 为准后改 docs
- 实施顺序（`AGENTS.md`）：文档 → 行为测试 → 代码
- 万象阵专题：[19-wanxiangzhen.md](./19-wanxiangzhen.md) → 全文 [wanxiangzhen/01-master-spec.md](./wanxiangzhen/01-master-spec.md)

## 跨仓库

| 仓库 | 关系 |
| :--- | :--- |
| `../opencode` | 只读参考，不改上游 |
| `../oh-my-pi` | 只读参考，不改上游 |
| `../mux` | binding 可改，核心少动 |

## 相关

- [00-index.md](./00-index.md)
