# OpenCode Next Migration Ledger

这是活账本，不是完成声明。状态只允许使用：`Not Started`、`Foundation Only`、`Contract Tested`、`Host Integrated`、`Real E2E`、`Cutover Ready`。

当前结论：`next/` 已有 Structured Flow、Journal、普通 Process、部分 OpenCode Hook 与垂直切片原型；测试主要是进程内 fake、手工构造 DTO 和纯函数契约。仓库当前不支持 OpenCode cutover，不得标记为 `Done`、`Cutover Ready` 或发布完成。

| Behavior ID | Required outcome | Current status | Evidence | Next acceptance condition |
|---|---|---|---|---|
| OC-NEXT-001 | 可被真实 OpenCode 加载的 `Plugin` 入口，拥有生命周期与返回值契约 | Foundation Only | `next/OpenCode/Plugin.fs`; `next/package.json`; `next/Wanxiangshu.Next.fsproj` | 用目标 OpenCode Harness 加载构建产物，验证 init、hook 返回值、dispose 与加载失败语义 |
| OC-NEXT-002 | `chat.message`、transform、tool definition/before/after、event、command、compaction Hook 完成 decode → dispatch → encode | Foundation Only | `next/OpenCode/Plugin.fs`; `next/OpenCode/OpencodeHooks.fs` | 每个 Hook 用真实宿主 payload 完成契约测试；覆盖异常、缺字段、原地修改和异步生命周期 |
| OC-NEXT-003 | 宿主 User/Assistant DTO、MessageID、SessionID、parentID、model 与 metadata 强类型收敛 | Foundation Only | `next/OpenCode/OpencodeTypes.fs`; `Plugin.fs` 中 `obj` 边界 | 真实 OpenCode 消息回放通过 codec，禁止下游再次猜测 `obj` |
| OC-NEXT-004 | `Human`、`PluginGenerated`、`HostInternal` 来源可靠解码，不依赖 prose | Foundation Only | `next/OpenCode/MessageOriginDecoder.fs`; `tests-next/Integration/VerticalSliceIntegrationTests.fs` | 真实消息矩阵覆盖普通、synthetic、compaction、metadata prompt key，且与宿主事件一致 |
| OC-NEXT-005 | Gateway 启动固定 Frontier、建立 Per-Runtime Journal、关闭时有界释放 | Contract Tested | `next/OpenCode/Gateway.fs`; `next/Journal/Boot.fs`; `tests-next/OpenCode/GatewayBootTests.fs`; `tests-next/Journal/JournalIsolationTests.fs` | 真实插件进程重启测试证明 Frontier、CreateNew、半行、单源损坏与资源释放 |
| OC-NEXT-006 | Append → flush → Fold → 更新本地 Projection 的 Read-your-writes 路径闭合 | Contract Tested | `next/OpenCode/Gateway.fs:65-89`; `next/Journal/Writer.fs`; `tests-next/Journal/JournalFoldTests.fs`; `tests-next/Integration/VerticalSliceIntegrationTests.fs` | 失败注入覆盖 write/flush/Apply，证明 CommitUnknown、Poisoned 与 ProjectionBroken 不伪装成功 |
| OC-NEXT-007 | Todo/Review/Prompt 投影按 `StreamId.Session(sessionId)` 隔离 | Contract Tested | `next/Journal/Fold.fs`; `next/Session/Driver.fs:114-118`; `tests-next/Journal/JournalIsolationTests.fs` | 两个真实 Session 并行写入后各自读取完整、互不覆盖；断言事件轨迹而非仅文件数量 |
| OC-NEXT-008 | 本 Runtime 同 Session 至多一个真正运行中的 Driver，Driver 消费 FIFO Inbox | Foundation Only | `next/Session/Driver.fs`; `next/Session/Inbox.fs`; `tests-next/Session/SessionProtocolTests.fs` | Driver 启动主 Flow、处理 Human 抢占/Cancel/Tool/terminal，并完成有界 shutdown 与异常 Fail Closed |
| OC-NEXT-009 | Human turn 等初始宿主 terminal 后才激活 Session Flow | Foundation Only | `next/OpenCode/OpencodeHooks.fs:41-48`; `next/Session/Driver.fs`; `TASK.md:8134-8148` | 真实 Host 先发 user，再发 terminal；验证未终态前不会运行 Todo/Review/Finish |
| OC-NEXT-010 | `PromptKey`、HistoricalPromptIndex、LocalPromptProtocol 实现 send-once 与恢复决策 | Contract Tested | `next/Session/PromptKey.fs`; `next/Session/PromptProtocol.fs`; `tests-next/Session/SessionProtocolTests.fs` | 加入 Requested/Submitted/Unknown/Terminal 的持久事实及重启 reconcile，不只验证纯决策函数 |
| OC-NEXT-011 | Prompt transport 真实调用 OpenCode，记录 Requested/Submitted/Terminal 并等待 parentID 相关结果 | Foundation Only | `next/Session/PromptProtocol.fs`; `next/Session/Driver.fs:163-173`; `TASK.md:240-273` | 真实 `client.session.prompt` 闭环：取得 UserMessageId、关联 Assistant parentID、处理超时/取消/AcceptanceUnknown |
| OC-NEXT-012 | `todowrite` 只经 SessionCommandPort，由 Driver 提交 TodoChanged | Contract Tested | `next/Tools/ToolContext.fs`; `next/Tools/StaticTools.fs:32-69`; `tests-next/Integration/VerticalSliceIntegrationTests.fs` | 真实 OpenCode tool 注册后执行一次 todowrite，验证 schema、权限、取消、队列满和事件轨迹 |
| OC-NEXT-013 | `message.updated`、assistant error、parentID、迟到 terminal 正确归属并去重 | Foundation Only | `next/OpenCode/OpencodeHooks.fs:118-162`; `next/OpenCode/MessageOriginDecoder.fs:68-78` | 真实事件乱序、重复、Abort、compaction 与旧 turn terminal 测试通过 |
| OC-NEXT-014 | MessageTransform 处理真实消息结构且纯、幂等、不修改输入 | Foundation Only | `next/Tools/MessageTransform.fs`; `next/OpenCode/Plugin.fs:64-103`; `tests-next/Tools/MessageTransformTests.fs` | 覆盖 tool call/result、metadata、assistant parts 和真实 transform Hook，不再只处理 role/text |
| OC-NEXT-015 | 普通 Process 使用绝对 Deadline、并发 stdout/stderr pump、取消后完整清理 | Contract Tested | `next/Process/ProcessFlow.fs`; `next/Process/ProcessPump.fs`; `tests-next/Process/ProcessTests.fs` | 故障注入证明 kill/drain/dispose 共用剩余预算、无静默清理失败、无孤儿进程 |
| OC-NEXT-016 | PTY 提供 spawn、resize、read-until-exit/idle/marker、write、kill | Not Started | `next/Process/` 无 PTY 实现；`TASK.md:557-580` | 新增 PTY 领域接口、宿主绑定、泄漏/取消/输出边界测试并接入真实工具 |
| OC-NEXT-017 | Child Session 真实创建、先注册 waiter 后发送、parentID 关联、resume/abort/close | Foundation Only | `next/Session/ChildFlows.fs`; `next/Tools/StaticTools.fs:125-161`; `tests-next/Session/ChildFlowTests.fs` | 用真实 OpenCode child session 替换 `ChildScript` fake，提交 ChildCreated/ChildCompleted 并覆盖 parent abort |
| OC-NEXT-018 | Review 真实 reviewer prompt、报告解码、Invalid 有界重试、复合 ReviewApplied | Foundation Only | `next/Session/Review.fs`; `tests-next/Session/SessionFlowTests.fs`; `TASK.md:495-517` | 真实 reviewer child 返回原始报告，系统完成解析、权限、WIP/空输出、重启恢复与最大轮次失败 |
| OC-NEXT-019 | Fallback 在真实 Prompt transport 上同模重试、换模，AcceptanceUnknown 绝不盲切 | Contract Tested | `next/Session/Fallback.fs`; `tests-next/Session/SessionFallbackTests.fs` | 接入模型解析、错误分类、PromptProtocol、空输出和真实宿主取消；验证重启不重复发 prompt |
| OC-NEXT-020 | compaction/context/title 成为明确流程动词，不依赖 idle 第二调度路径 | Foundation Only | `next/OpenCode/Plugin.fs:168-234`; `next/OpenCode/MessageOriginDecoder.fs:60-66`; `TASK.md:636-648` | 实现 Context Budget、CompactIfNeeded、EnsureTitle、事件事实与真实 compaction/continue E2E |
| OC-NEXT-021 | OpenCode tool schema、权限、输出预算和静态工具注册真实生效 | Foundation Only | `next/Tools/StaticTools.fs`; `next/Tools/ToolContext.fs`; `next/OpenCode/Plugin.fs:106-107` | 完成 `tool.definition`，真实注册 executor/todowrite/subagent/review 工具，验证拒绝为值、截断和无越权执行 |
| OC-NEXT-022 | Wanxiangzhen 完成 DAG、wave、worktree、slave、verify、串行 FastForward、AcceptWave | Foundation Only | `next/Wanxiangzhen/SquadFlow.fs`; `next/Wanxiangzhen/SquadTypes.fs`; `TASK.md:597-632` | 真实 worktree/slave/HTTP 只读流程、取消、孤儿清理、重启恢复与 merge 顺序 E2E |
| OC-NEXT-023 | 垂直切片从真实 Host message → todo → prompt → terminal → TodoChanged → Finish 闭合 | Foundation Only | `tests-next/Integration/VerticalSliceIntegrationTests.fs`; `tests-next/E2E/VerticalSliceE2ETests.fs`; `TASK.md:855-888` | 测试不得直接构造 terminal/verdict，不得直接调用 tool execute；使用真实 OpenCode Harness 并中途重启一次 |
| OC-NEXT-024 | 真实 OpenCode E2E 覆盖行为账本、重启、取消、隔离、工具与子代理 | Not Started | `tests-next/E2E/VerticalSliceE2ETests.fs` 目前为进程内 fake；`TASK.md:671-688` | 建立可重复 Harness、完整事件轨迹断言和行为 ID 映射，覆盖至少一条真实端到端闭环 |
| OC-NEXT-025 | 旧行为与 next 行为完成逐项映射，所有迁移行为达到 Cutover Ready | Not Started | `TASK.md:768-774`; 本账本 | 每个行为达到 `Real E2E`，完成兼容/删除清单、包入口切换、回滚边界与发布验收 |
| OC-NEXT-026 | OpenCode cutover 可执行且不再依赖 legacy 生产实现 | Not Started | `package.json`; `next/package.json`; `TASK.md:835-853` | 真实宿主全量验收通过，legacy import/入口清除，CI 根据本账本阻止未完成行为宣称完成 |

## 状态判定

- `Foundation Only`：实现骨架或纯逻辑存在，但真实外部契约未闭合。
- `Contract Tested`：仓库已有正式测试验证当前模块契约；不等价于 Host Integrated。
- `Host Integrated`：真实 OpenCode 进程加载并使用该行为，但尚无完整 Real E2E 证据。
- `Real E2E`：真实 Host Harness 覆盖端到端行为及故障/重启要求。
- `Cutover Ready`：行为已满足迁移出口，且不再依赖未完成 legacy 路径。

本文件随实现推进更新；任何状态升级必须同时提交对应证据路径和下一层验收证据。当前最高结论仍是：OpenCode next 原型与局部契约存在，尚不支持切换。
