# 万象术 — 用户侧功能清单

> 供审查部门核对各功能点完成情况。权威顺序：实现与测试 > docs。

## 1. 子代理委派

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| coder | 代码编写子代理，支持多意图并发、TDD 红/绿阶段声明 | ✓ |
| inspector | 代码/仓库调查子代理，只读权限 | ✓ |
| browser | 网页浏览与信息获取子代理 | ✓ |
| meditator | 结构化方法论笔记本推理子代理 | ✓ |
| continue | 对同一子会话追问续跑，支持多轮迭代 | ✓ |
| 多意图并发 | coder/inspector 等工具的 `intents[]` 参数支持多项并发执行 | ✓ |
| Iterator 持久化 | 子代理迭代器 ID（`sci_s:<childID>:<agent>:<host>`）自包含，不依赖内存 | ✓ |

## 2. 审查循环 (With-Review / /loop)

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| /loop 开启审查 | `/loop <任务描述>` 进入 With-Review 模式，嵌入独立 reviewer 子代理 | ✓ |
| /loop 空任务取消 | 空任务取消当前 loop | ✓ |
| submit_review | worker 提交报告，支持 `wip: true` 标记部分进度 | ✓ |
| return_reviewer | reviewer 子代理返回 verdict（PERFECT / REVISE） | ✓ |
| 状态机驱动 | 5 状态 DU（Inactive/Active/Locked/Accepted/NeedsRevision）穷举转移 | ✓ |
| Nudge 上限终止 | reviewer 轮次超 `maxNudges` 则终止 loop（Terminated） | ✓ |

## 3. 方法论笔记本工具

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| 54 个方法论工具 | `methodology_<id>` 供 LLM 在复杂推理时写入结构化 note | ✓ |
| 七大类覆盖 | 逻辑推理(7)、问题转换(10)、数学推理(9)、优化(7)、系统工程(9)、批判探究(12) | ✓ |

## 4. 文件操作

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| read | 读取文件内容 | ✓ |
| write | 写入/覆写文件 | ✓ |
| edit | 精确字符串替换（支持 replaceAll） | ✓ |
| swap | 交换两个文件或同文件内的行范围（结构保持重构） | ✓ |

## 5. 搜索

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| fuzzy_find | 按模糊路径文本搜索文件，frecency 排名 | ✓ |
| fuzzy_grep | 模糊感知内容搜索，支持正则自动检测 | ✓ |
| fuzzy_continue | 对上一次搜索结果翻页 | ✓ |

## 6. 网络

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| websearch | 实时网络搜索，可配置结果数和搜索类型 | ✓ |
| webfetch | URL 抓取，支持 llms.txt 探测与内容提取 | ✓ |
| SSRF 防护 | `webfetch` 经 `Kernel.WebFetchGuard` 拒绝私网/回环 | ✓ |

## 7. 执行器 (Executor)

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| 多语言执行 | shell / python / javascript 同步执行 | ✓ |
| 输出上限 | 必填 `max_bytes` 参数，超限触发摘要 | ✓ |
| 超时控制 | `short`(10s) / `long`(100s) 两档超时预算 | ✓ |
| 依赖声明 | python/javascript 可声明依赖自动安装 | ✓ |

## 8. PTY (伪终端)

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| pty_spawn | 创建后台 PTY 会话（dev server、watch mode 等） | ✓ |
| pty_write | 向 PTY 发送输入，支持转义序列 | ✓ |
| pty_read | 读取 PTY 输出缓冲，支持偏移/限制/正则过滤 | ✓ |
| pty_list | 列出所有活跃 PTY 会话 | ✓ |
| pty_kill | 终止 PTY 会话并可选清理 | ✓ |

> PTY 工具仅 OpenCode/Mimocode 宿主注册。

## 9. Nudge (智能催促)

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| 待办催促 | 有未完成 todo 时提示继续 | ✓ |
| Loop 催促 | 审查循环活跃时催促提交/修订 | ✓ |
| Runner 催促 | 子代理运行时催促关注 | ✓ |
| 去重机制 | `NudgeDedupState` 防止重复催促 | ✓ |
| 六阶段生命周期 | requested → dispatched/failed/cancelled → settled → dedup_cleared | ✓ |
| 事件驱动决策 | 宿主禁止用内存布尔代替事件 fold 的 loop 态（架构测试强制） | ✓ |

## 10. 模型降级 (Fallback)

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| 自动降级 | 上游 401/402/403、429、5xx 或断连时自动按链切换模型 | ✓ |
| 续命机制 | Fallback 注入 `continue` 探测，链耗尽才向父 agent 传播 | ✓ |
| 配置驱动 | `AGENTS.md` frontmatter `models:` 配置降级链 | ✓ |
| 子代理降级 | 子代理错误路由到子会话 Fallback 桥，不污染主 session | ✓ |
| 完美平方启发式 | `Recovery.fs` 决定重试策略 | ✓ |
| 双重门闩 | `EventHandlingActive` + `MainContinuationAwaitingStart` 防并发冲突 | ✓ |
| 空输出检测 | assistant 无 tool、text 为空 → `EmptyOutputError` → 触发 fallback | ✓ |

## 11. 事件溯源与持久化

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| NDJSON 事件日志 | `.wanxiangshu.ndjson` 一行一自包含事件，追加只碰末尾 | ✓ |
| 文件排他锁 | `.wanxiangshu.ndjson.lock` 跨进程互斥 | ✓ |
| 内存 = fold | `SessionState` 28 轴复合投影，可从 NDJSON 完整重建 | ✓ |
| 先盘后内存 | append 成功后才更新缓存投影；失败 = 命令未发生 | ✓ |
| 损坏行截断 | 遇无法解析行截断（不跳过继续），宁可少恢复 | ✓ |
| 子会话决策信封 | `subsession_decision_committed` 原子打包多事件为一行 | ✓ |

## 12. 子会话 Actor

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| SubsessionActor 状态机 | 9 状态（Available/Dispatching/Running/Draining/IssuingAbort 等）穷举转移 | ✓ |
| 10 步串行提交管线 | Dequeue → Validate → Decide → Persist → Commit → ... → Next | ✓ |
| 超时保护 | TurnDeadline / AbortDeadline / ReconciliationDeadline RAII 管理 | ✓ |
| 崩溃恢复 | `SubsessionReconcile.reconcileUnfinishedRuns` 原子标记中毒 | ✓ |
| SafetyProjection | 重启后检查未完成 run，直接以 Poisoned 创建防止幽灵会话 | ✓ |

## 13. 消息变换管线

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| Caps 注入 | 系统前导提示（宝典/铁律/工具说明）按策略注入 | ✓ |
| 并行工具提示 | 仅 1 个真实 tool 调用时追加 synthetic 提示鼓励并行 | ✓ |
| Semble 搜索 | inspector 路径上下文 ≥50 字符时注入 MCP 搜索结果 | ✓ |
| 空输出 Fallback | SessionIdle 时最后 assistant 无 tool/text 为空触发降级 | ✓ |

## 14. 多宿主支持

| 宿主 | 描述 | 状态 |
| :--- | :--- | :--- |
| OpenCode | Zod schema，完整 hook 注册，PTY 支持 | ✓ |
| Mimocode | TUI sidebar todo 回填，`task`/`actor` 命名映射 | ✓ |
| Mux | MuxJsonSchema，`normalizeToolNameForMux` 名称统一 | ✓ |
| oh-my-pi (OMP) | TypeBox schema，`pi?registerTool` 动态注册 | ✓ |

## 15. 万象阵 (Wanxiangzhen) — 独立协调器

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| /squad 拆解 | coordinator LLM 把需求拆解为 DAG 任务图 | ✓ |
| /squad-kill | 终止 slave 进程（保留 worktree 现场） | ✓ |
| HTTP 协调 | coordinator 自起 HTTP server，slave 经短连接通信 | ✓ |
| DAG 状态机 | 6 状态（Pending/Running/Submitted/Merged/Done/Cancelled）穷举转移 | ✓ |
| Git worktree 隔离 | 每个任务独立 worktree，共享 `.git` gitdir | ✓ |
| FF-only 合并 | 仅 fast-forward，并行竞争后到者 rebase | ✓ |
| PID 监控 + Done Beacon | 双保险探测 slave 退出（无 child-exit hook） | ✓ |
| NDJSON 事件共用 | `squad_*`/`task_*` 与万象术共用 `.wanxiangshu.ndjson` | ✓ |
| 启动恢复 | coordinator 重启后仅依赖 NDJSON 可恢复 DAG | ✓ |

## 16. 工具权限

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| 角色矩阵 | Manager/Coder/Inspector/Meditator/Browser/Reviewer 各有工具语义权限 | ✓ |
| warn_tdd / warn_reuse | Schema description 强制 LLM 注意 TDD/复用约束 | ✓ |

## 17. 并行工具鼓励

| 功能 | 描述 | 状态 |
| :--- | :--- | :--- |
| 自动检测 | assistant 仅 1 个真实 tool 调用且该轮已有对应 ToolResult 时触发 | ✓ |
| Synthetic 提示 | 追加 synthetic User 消息鼓励并行，下轮自动剥离 | ✓ |
| 白名单控制 | 仅 `ToolCatalog.all` 名 + `"methodology"` 触发 | ✓ |

---

## 功能统计

| 类别 | 功能点数 |
| :--- | :--- |
| 子代理 | 7 |
| 审查循环 | 6 |
| 方法论 | 2 |
| 文件操作 | 4 |
| 搜索 | 3 |
| 网络 | 3 |
| 执行器 | 4 |
| PTY | 5 |
| Nudge | 6 |
| Fallback | 7 |
| 事件溯源 | 6 |
| 子会话 Actor | 5 |
| 消息变换 | 4 |
| 多宿主 | 4 |
| 万象阵 | 9 |
| 工具权限 | 2 |
| 并行鼓励 | 3 |
| **总计** | **81** |
