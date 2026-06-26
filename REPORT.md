# Opencode 端 vs Omp 端实现不一致对比报告

对比范围：`src/Opencode/`（35 .fs）↔ `src/Omp/`（39 .fs）。Kernel+Shell 为共享底座，差异全在宿主适配层。

---

## 0. 定位差异总览

| 维度 | Opencode | Omp |
|---|---|---|
| 宿主 | OpenCode / Mimocode 插件 SDK | oh-my-pi (`@oh-my-pi`) Pi 扩展 |
| 入口 | `Plugin.fs` → `pluginFor opencode ctx` / `pluginFor mimocode ctx` | `Plugin.fs` → `wanxiangshuExtension(pi)` |
| 依赖约束 | 可引用 Shell 全部 + Opencode 专属 codec | 仅 Kernel+Shell，禁引用 `Wanxiangshu.Opencode`/`Wanxiangshu.Mux`/`engine/` |
| 工具注册 | 挂到单一 `tools` obj，返回给 host | `pi.registerTool` 逐个注册 |
| schema 技术 | Zod-like (`@opencode-ai/plugin/tool`) | TypeBox (`pi.typebox`) |
| 工具执行签名 | `(args, context) => Promise<string>` | `(id, params, signal, onUpdate, ctx) => Promise<ToolResult>` |
| 返回值 | 纯 `string` | `{content: TextResult[], isError?, display?}` |

---

## 1. 子代理 / Session 启动模型 [user: 暂不合并]

### Opencode
- `ChildAgentRegistry`（Shell 层）维护 `WorkspaceState`，`RegisterChildAgent(childID, agent, parentID)` → 可 `LookupChildAgent`/`ResolveSubsessionParentID`。
- `SessionIoSubagent.startSubagentSession` → `client.session.create` + `session.prompt`，abort 走 `AbortSignal` race。
- 子会话身份由 host 分配的 sessionID 标识，注册进 `ChildAgentRegistry` 供 bookkeeper/nudge 查询。

### Omp
- `ChildSession` 模块自建 `childSessionIds : Set<string>`（进程级 mutable），仅做 `markChildSession`/`isChildSession`，**不**走 `ChildAgentRegistry`。
- `createChildSession` → `getCreateAgentSession(pi)` + `SessionManager.create` → `createAgentSession(body)`，返回 `{session, dispose}`。
- 子会话工具集在创建时通过 `toolNames` 参数硬编码（如 `ompReviewChildToolNames`/`ompRunnerChildToolNames`/`bookkeeperChildTools`），非动态 policy 推导。

### 不一致点
1. Opencode 用 `ChildAgentRegistry` 做 agent↔sessionID 映射；Omp 用独立 `childSessionIds` Set，只判"是否子会话"，**丢失 agent 维度**。
2. Opencode 子会话工具集由 `toolPolicy.disabledTools` 动态计算（`SubagentToolPolicy.disabledToolNamesForRole`）；Omp 硬编码数组。
3. Opencode abort 走 `AbortSignal` + DOMException；Omp 走 `raceWithAbortSignal(signal, cleanup, work)` + `session.abort()`。
4. Opencode 子会话 dispose 隐含在 `UnregisterChildAgent`；Omp 显式 `dispose: (unit->unit) option`。

---

## 2. 消息模型与编解码 [user: 暂不合并]

### Opencode (`Opencode.MessagingCodec`)
- 消息结构：`{info: {id, sessionID, role, agent, isError, toolName, details, time}, parts: [...]}`。
- part type：`"text"` / `"tool"`（字段 `tool`, `callID`, `state`）。
- role 枚举：`user`/`assistant`/`toolResult`/`system`。
- encode 单文件闭环；`encodeMessage` 保留 raw 引用以维持对象身份。

### Omp (`Omp.MessagingCodec` + `MessagingCodecEncode`)
- 消息结构：entry = `{info: {id, role}, parts: [...]}` 或 `{message: {id, role, content}}`（**双形态**，`roleOfEntry`/`idOfEntry`/`decodeEntry` 需兼容两种）。
- part type：`"text"` / `"tool"`（字段 `tool`, `callID`, `state`）——与 Opencode 一致。
- role 枚举：`user`/`assistant`/`tool`/`system`（**注意 `tool` vs `toolResult`**）。
- encode 拆到独立 `MessagingCodecEncode.fs`；encode 逻辑与 Opencode 几乎同构但独立维护。

### 不一致点
1. Opencode part state 含 `operationAction`（从 `input.operation.action` 提取）；Omp `decodeToolState` **丢弃**该字段（`operationAction = ""`）。
2. Omp entry 有 `message.content` 退化路径（`roleOfEntry`/`decodeEntry` 双分支）；Opencode 无此分支。
3. role 映射：Opencode `"toolResult"` ↔ Omp `"tool"`，两边 `decodeRole` 共享 Kernel 映射但 wire 字面量不同。
4. Omp `encodePart` 对 raw state 做字段级 diff（status/output/error 三字段比对）；Opencode 同逻辑但内联在单文件——**两份几乎相同的 diff 代码**。

---

## 3. KnowledgeGraph Runtime [user: 暂不合并]

### Opencode (`KnowledgeGraphRuntime.fs` 单文件)
- 类构造：`(client, initialWorkspaceRoot, nowUtc, registry, portLockTimeoutMs, portLockRetryDelayMs)`。
- `Submit` → `SubmitFromHistory`：先 `loadSessionMessages` 做幂等门，再 `commandQueue.Enqueue` 内 `tryResolveJobContext` + `runWorkspace`。
- `DeleteJob` 同时 `registry.UnregisterChildAgent`。
- 日维护走 Shell 共享 `runMaintenanceIfDue`。

### Omp (`Omp/KnowledgeGraph/` 子目录 5 文件)
- `Runtime.fs` 类构造：`(pi)`。无 client/registry 注入。
- 拆分为 `Fetch`/`Maintenance`/`Snapshot`/`Submit` 四个纯函数模块，Runtime 类做方法转发。
- `Submit`（`Submit.fs`）：用 `magicGetEntries` (ref) 代替 client 调用；幂等门用 `decodeEntries sessionID (load())`。
- 日维护**内联等价逻辑**（`Maintenance.fs` 自实现 `startMaintenanceIfDue`），不走 Shell `runMaintenanceIfDue`——README 明确"OMP 在 `Omp/KnowledgeGraph/Maintenance` 内维护，与 Shell 策略语义对齐"。

### 不一致点
1. Opencode KG runtime 持有 `client` + `registry`；Omp 持有 `pi`，靠 `magicGetEntries` ref 注入 entries 加载器——**注入时机不同**（Opencode 构造时，Omp 运行时 `BindGetEntries`）。
2. Opencode 日维护复用 Shell 共享函数；Omp 自实现一套等价逻辑（`Maintenance.fs` 独立 `launchIfDue` + `recordLaunchOnce`）——README 允许但要求"语义对齐"，**存在分叉风险**。
3. Opencode `DeleteJob` 联动 `ChildAgentRegistry`；Omp 仅 `Map.remove`，**不联动** `childSessionIds`（Omp 无 registry）。
4. Opencode submit 端口锁参数可配（构造注入 30000L/1000）；Omp `submitForKind` 硬编码 `30000L 1000`。
5. Opencode 测试钩子用 `CreateTestPorts()`；Omp 用 `TakeBookkeeperLaunchesForTesting`/`SnapshotRegisteredJobsForTesting`/`WaitForBackgroundJobsForTesting`——**测试 API 面不同**。

---

## 4. Review 循环 [user: 禁止轮询，参考 Opencode 实现，要 double check]

### Opencode (`ReviewerLoop.fs`)
- `createReviewerChild` → `client.session.create`，注册进 `reviewStore` + `ChildAgentRegistry`。
- `runReviewerLoop`：`reviewStore.setPendingReview` + `promptWithAbort` 循环，`decideAfterRound` 决定 nudge/finish。
- 返回 `ReviewResult`（Accepted/Rejected/Terminated）。
- `submit_review` 工具在 `ReviewTools.fs`，`return_reviewer` 单独工具。

### Omp (`ReviewLoop.fs` + `ReviewToolsRegister.fs` + `ReviewToolsLoop.fs`)
- `runReviewLoop` → `createChildSession pi ctx ompReviewChildToolNames`，`childSession.prompt` + `waitForIdle`。
- 自实现 `JsReviewResult` + `waitUntilResolved`（轮询 50ms sleep）+ `runNudgeLoop`（`raceResolvedOr` + grace timer）。
- `submit_review`/`return_reviewer`/`/loop`/`/loop-review` 全在 `registerLoopFeatures`。

### 不一致点
1. Opencode 用 `reviewStore.setPendingReview` 回调驱动 verdict；Omp 用 `ref None` + 50ms 轮询 `waitUntilResolved`——**Omp 是忙等，Opencode 是回调**。
2. Opencode nudge grace 由 Kernel `decideAfterRound`/`promptParts` 纯函数驱动；Omp 自实现 `initialGraceMs=6000`/`subsequentGraceMs=10000` + `raceResolvedOr`——**两套 nudge 节奏参数**。
3. Opencode `return_reviewer` 走 `decideReviewSubmission`（含 double-check）；Omp `return_reviewer` 直接 `resolvePendingReview`，**无 double-check**。
4. Opencode `/loop-review` 走 `command.execute.before` hook + host command 模板；Omp 走 `pi.registerCommand` + `handleLoopReviewCommand`。

---

## 5. Nudge 机制 [user: 参考 opencode，禁止除了历史记录以外的第二真理源]

### Opencode (`SessionLifecycleObserver` + `NudgeEffect`)
- `StateHolder<NudgeShellState>` 持有 Kernel nudge 状态。
- `handleEvent` 解码 host event → `NudgeState.handleEvent` → `decideNudge` → `startNudgeFlow` 异步 `client.session.prompt`。
- todo 来源：`session.todo` API → `decodeTodos`，fallback `recoverOpenTodosFromMessages`。
- lastAssistant 来源：`decodeLastAssistant`（info.finish 或 time.completed）。

### Omp (`NudgeRuntime` + `SessionLifecycleHooks.agentEndHandler`)
- 进程级 `mutable coordinator = freshCoordinator`（**非 per-session holder**）。
- `agentEndHandler` → `tryLoopNudge`/`tryTodoNudge` → `pi.sendMessage(customType, content, {triggerTurn, deliverAs})`。
- todo 来源：`openTodoStatuses(sessionManager)` 解析 `todo-phases` customType。
- lastAssistant 来源：`lastAssistantMessage(sessionManager)` via `readAssistantText`。

### 不一致点
1. Opencode nudge 状态封装在 `StateHolder`（per-observer）；Omp 是**进程级单例 mutable**——多 session 共享一个 coordinator。
2. Opencode nudge 投递走 `client.session.prompt`（host API）；Omp 走 `pi.sendMessage`（Pi 事件）——**投递通道不同**。
3. Opencode todo 读 `session.todo` API；Omp 读 `sessionManager.getEntries` 找 `todo-phases` customType——**数据源不同**。
4. Opencode 有 `tryRecordSend`/`Delivered/Aborted/Busy/Failed` 四态回写；Omp `Coordinator.decideRuntimeAction` 只做 suppression 消费，**不回写 send outcome**。
5. Opencode runner nudge 存在（`NudgeRunner`）；Omp `tryLoopNudge`/`tryTodoNudge` 都含 `hasActiveRunner`，但 runner 概念走 `RunnerBackground` 独立模块。

---

## 6. Executor [user: 对齐 opencode 端实现方法]

### Opencode (`ExecutorTool.fs`)
- `sessionScope.EnqueuePerSession(sessionID, ...)` 串行化。
- `Shell.Executor.execute options sessionID`。
- summary 子代理走 `runSubagentWithCleanup`（host session API）。

### Omp (`ExecutorTools.fs`)
- 独立 `ompScope = RuntimeScope()` + `sessionExecutor = createForScope ompScope`（**模块级单例**，非注入）。
- 每次 executor 调用 `createChildSession pi ctx ompRunnerChildToolNames`，在子会话内跑 `executeWith`。
- 注册 `registerActiveRunnerSession`/`registerRunnerChild` 到 `RunnerBackground`。
- 提供 `executor_wait`/`executor_abort` 独立工具（Opencode 无此对）。

### 不一致点
1. Opencode executor 在当前 session 串行；Omp executor **总是新建子会话**跑——执行隔离模型不同。
2. Opencode 无 `executor_wait`/`executor_abort`；Omp 有——**工具面不对等**。
3. Opencode summary 子代理用 host session API；Omp 在同一 child session 内 `childSession.prompt` 再读 `readAssistantText`。
4. Opencode `sessionScope` 由 PluginCore 注入；Omp `ompScope` 模块级全局。

---

## 7. Fuzzy Search [user: 对齐 opencode 端实现方法]

### Opencode (`SearchTools.fs`)
- `buildFuzzyTool` → `runtime.Execution.SessionId` 作 scopeId。
- `SearchOptions.store = Some iteratorStore`（来自 `RuntimeScope.IteratorStore`）。
- iterator 状态持久化跨调用。

### Omp (`FuzzyTools.fs`)
- `scopeId = sessionId || workspaceId`。
- `SearchOptions.store = None`（**不注入 iterator store**）。
- 无跨调用 iterator 持久化——每次 fuzzy_find/fuzzy_grep 首次调用需带 pattern。

### 不一致点
1. **Omp fuzzy 不挂 iterator store**，分页 iterator 不持久化（除非 host 自带）；Opencode 挂 `RuntimeScope.IteratorStore`。
2. Omp exclude 字段支持 `union(str, strArray)`；Opencode 走 `excludeOpt`（str | strArray nullish）——**schema 形态不同**。
3. Omp fuzzy 描述用 `fuzzyFindDescriptionOmp`（含 iterator hint）；Opencode 用 `ToolCatalog.description`。

---

## 8. Web Tools [user: 暂不合并]

### Opencode (`SearchTools.fs`)
- `websearch`：`webApiPost "web_search"` → 子代理总结（`runSubagentWithCleanup` registry=executor）。
- `webfetch`：`webApiPost "web_fetch"`（含 SSRF 校验在服务端）。

### Omp (`WebTools.fs`)
- `websearch`：`ollamaPost "web_search"` → `runSubagent pi ctx [|"read"|]`。
- `webfetch`：`ollamaPost "web_fetch"`（**客户端先** `validateFetchUrl` SSRF 门）。

### 不一致点
1. Opencode webfetch **不在客户端做 SSRF**；Omp webfetch **客户端先做 SSRF**（`validateFetchUrl`）——**安全门位置不同**。
2. Opencode websearch 子代理角色 "executor"；Omp 子代理工具集 `[|"read"|]`——**子代理能力配置不同**。
3. Opencode 走 `webApiPost`；Omp 走 `ollamaPost`（薄包装，路径归一化逻辑重复）。

---

## 9. Todo / WorkBacklog [user: ../oh-my-pi 有原生 todo 工具! 请调研并使用原生 todo 而不是山寨]

### Opencode
- host 原生有 `todo_write`/`task` 工具，通过 `tool.definition` hook 重写其 schema/description 为 WorkBacklog 语义。
- Mimocode 走 `HookSchema.mergeWorkBacklogReportIntoTaskSchema`（Zod safeExtend/extend）。
- `completedWorkReport` 通过 `tool.execute.after` → `projection.CaptureReport` 捕获。

### Omp (`TodoTool.fs`)
- **直接** `pi.registerTool("todowrite")`，自建 TypeBox schema。
- 无 host 原生 todo 工具需重写——净新增。
- `completedWorkReport` 通过 `tool_result` hook → `backlogSession.CaptureReport`。

### 不一致点
1. Opencode 改造 host 原生工具；Omp 新建工具——**接管策略不同**。
2. Opencode Mimocode 走 Zod `safeExtend`；Omp 走 TypeBox `objectOf`——schema 构建路径完全不同。
3. 两端 `completedWorkReport` 捕获时机：Opencode `tool.execute.after`；Omp `tool_result` hook。

---

## 10. Read Dedup [user: 暂不合并]

### Opencode (`ReadDedupOpenCode.fs`)
- `deduplicateOpencodeReadPartsInPlace`：遍历 `msg.parts`，对 `type="tool" && tool="read"` 的 part 就地改 `state.output`。

### Omp (`Omp.ReadDedup.fs`)
- `applyReadDedup`：遍历 `entry.parts`，对 `type="tool" && tool="read"` 的 part 就地改 `state.output`。

### 不一致点
1. 两份逻辑**几乎逐字相同**（`seenByPath` createObj + `processDedup` + `noChangeEnvelope`），仅模块路径不同——**重复代码**。
2. Opencode 通过 `MessageTransform.dedupFn` 在共享管线调用；Omp 在 `transformEntriesAsyncWithAgent` 的 `dedupFn` 内调用——挂载点对称但各自独立。

---

## 11. Message Transform / Backlog Projection [user: 对齐 opencode 缓存，因为要保持前缀不变]

### Opencode (`MessageTransform.fs`)
- `messagesTransform` hook = `experimental.chat.messages.transform`。
- `compactingHandler` hook = `experimental.session.compacting`。
- `BacklogSession(host, scope)` 绑定 `RuntimeScope.Projection`。
- agent 解析：`resolveMessagesTransformAgent`（registry + 消息回溯）。
- replay 文本：`readSessionTexts client sessionID directory`（host API）。

### Omp (`MessageTransform.fs`)
- `registerContextTransform` hook = `pi.on("context")` + `pi.on("before_context")`。
- `sessionCompactingHandler` hook = `session.compacting`。
- `BacklogSession omp` 用**独立 `ProjectionStore` 单例**（`MagicTodo.projection`），不绑定 RuntimeScope。
- agent 解析：`resolveAgent ctx`（从 `sessionManager.agentName`）。
- replay 文本：`extractHistoryTexts messagesList`（内存 entries，无 host API 调用）。

### 不一致点
1. hook 名不同：`experimental.chat.messages.transform` vs `pi.on("context")`。
2. Opencode BacklogSession 绑 RuntimeScope；Omp 用模块级 `ProjectionStore()` 单例——**可变状态作用域不同**。
3. Opencode replay 走 host client API（网络往返）；Omp 从内存 entries 提取（Pi 已把 entries 交给插件）。
4. Opencode caps 加载用 `getOrLoadCapsFilesForScope`（RuntimeScope 缓存）；Omp 直接 `findOmpCapsFiles cwd`（**无缓存层**）。
5. Opencode capsCodec 用 `sha256HexTruncated`（Shell.FileSys）；Omp capsCodec 用 `sha256HexTruncated`（同源）但 `OmpCaps` 有独立发现逻辑（`discoverFilesInDirAsync` 递归 + `realpath` 防环）——**两套 caps 发现**。

---

## 12. Agent Config [user: 暂不合并]

### Opencode (`AgentConfig.fs` + `OpencodeAgentConfigCodec.fs` + `OpencodeAgentConfigWire.fs`)
- 三文件拆分：Codec（纯编解码）+ Wire（合并/禁用）+ AgentConfig（组装）。
- 含 `disableMimoWorkflowToolsForAgents`（Mimocode 专属：禁 workflow 工具）。
- host=Opencode/Mux/Mimocode 三分支。

### Omp (`AgentConfig.fs` 单文件)
- 内联 `mergeObj`/`emptyObj`/`setKey`。
- 无 workflow 工具禁用逻辑。
- `disableNativeAgents` 禁 `dream`/`distill`/`checkpoint-writer` + 禁 checkpoint/dream/distill/memory section。

### 不一致点
1. Opencode 有 `OpencodeAgentConfigCodec`（纯 Map 编解码）+ `OpencodeAgentConfigWire`（合并语义）；Omp 内联等价逻辑——**Omp 未抽 codec 层**。
2. Opencode Mimocode 禁 workflow 工具；Omp 无此概念。
3. 两端都禁 dream/distill/checkpoint-writer，但 Opencode 走 `disableMimoMemoryAndCheckpoint`（Wire 层），Omp 走 `disableNativeAgents`（AgentConfig 层）——**同语义两处实现**。

---

## 13. Slash Commands [user: 暂不合并]

### Opencode
- `/loop` `/loop-review` 通过 `registerCommands cfg` 写入 host config.command 模板。
- 实际执行走 `command.execute.before` hook（`CommandHooks.commandExecuteBefore`）。
- 模板文本来自 `ReviewPrompts.withReviewCommandTemplate`（YAML front-matter 锚点）。

### Omp
- `/loop` `/loop-review` 通过 `pi.registerCommand`。
- 执行走 `handleLoopCommand`/`handleLoopReviewCommand`（直接函数）。
- `/loop` 还额外走 `pi.on("input")` 做文本拦截（`registerInputHandler`）。

### 不一致点
1. Opencode 声明式模板 + hook 拦截；Omp 命令式注册 + input 拦截——**两套命令接入模型**。
2. Omp `/loop` 有双入口（`registerCommand` + `on("input")`）；Opencode 单入口。
3. Opencode loop-review verdict 经 `runReviewerSession`；Omp 经 `runPreReviewerSession`——逻辑同构但独立函数。

---

## 14. 会话生命周期 Hook 注册 [user: 暂不合并]

### Opencode (`PluginCore.registerHooks`)
```
chat.message / tool.definition / tool.execute.before / tool.execute.after
experimental.chat.messages.transform / command.execute.before
event / experimental.session.compacting / experimental.chat.system.transform
```

### Omp (`SessionLifecycle.registerSessionLifecycle`)
```
before_agent_start / tool_call / tool_result / agent_end
session_start / turn_start / session.compacting / session_shutdown
```
+ `PluginCore.registerAbortHandler` 注册 `event`（session.abort/stream.abort/session.error/session.delete/session.close/session.remove/session.deleted）。

### 不一致点
1. hook 事件名完全不同（host API 差异）。
2. Opencode tool execute 拆 before/after；Omp 拆 tool_call/tool_result。
3. Opencode 无 `turn_start`/`session_start`/`session_shutdown` 显式 hook；Omp 无 `chat.message`/`tool.definition`。
4. Omp `tool_call` hook 可返回 `{block: true, reason}` 阻断 child-only 工具；Opencode 无此阻断机制（靠 host 自身 toolPolicy）。
5. Omp `before_agent_start` 含 `applyActiveToolFilterForMainSession`（过滤主会话工具可见性）；Opencode 无对应。

---

## 15. Abort / Cleanup [user: 暂不合并]

### Opencode
- `EventHooks.eventHandler`：stream-abort → `reviewStore.deactivateReview`。
- `CommandHooks.cleanUpJobContextIfAbortedOrDeleted`：abort/delete 事件 → `knowledgeGraphRuntime.DeleteJob`。

### Omp
- `PluginCore.registerAbortHandler`：统一处理 7 种 session 终止事件 → `reviewStore.deactivateReview` + `clearNudgeSession` + `kgRuntime.DeleteJob`。
- `SessionLifecycleHooks.sessionShutdownHandler`：额外 `clearTypedIteratorScope` + `cleanupRunnerJob`。

### 不一致点
1. Opencode abort 清理分散在 EventHooks + CommandHooks 两处；Omp 集中在 `registerAbortHandler` + `sessionShutdownHandler`。
2. Omp shutdown 额外清 iterator scope + runner job；Opencode 无 iterator shutdown 清理（iterator 在 RuntimeScope 内，随 scope 回收）。

---

## 16. 测试钩子导出 [user: 暂不合并]

### Opencode
- `__knowledgeGraphRuntime` 挂在 plugin result obj（`rawInstance`/`registerJobForTesting`/`takeBookkeeperLaunchesForTesting`/`waitForBackgroundJobsForTesting`）。
- review 测试面通过 `reviewStore` 单例直接暴露。

### Omp
- `_test` 挂在 `Plugin.fs` export（`appendCapsContext`/`buildCapsContext`/`stripHostAgentsPrompt`/`checkSyntax`/`fuzzy`/`getOllamaKey`/`readAssistantText`/`resetRunner`/`setRunnerJobStateForTest`/`setPendingReviewStateForTest`/`stripHeadTailPipes`/`supportsSyntaxDiagnosticsTool`/`reset`/`transformEntries`）。
- KG 测试面在 `Runtime` 类方法（`RegisterJobForTesting`/`TakeBookkeeperLaunchesForTesting`/`SnapshotRegisteredJobsForTesting`/`WaitForBackgroundJobsForTesting`）。

### 不一致点
1. 测试 API 面差异大：Opencode 侧重 KG + review；Omp 侧重 caps/syntax/fuzzy/runner/transform 全覆盖。
2. Opencode review 测试用 `reviewStore` 单例；Omp 用 `_test.setPendingReviewStateForTest` 闭包。

---

报告结束。
