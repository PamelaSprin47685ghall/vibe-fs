# 待完成的重构项（已完成项已从原报告删去）

# 一、 系统级根上设计缺陷

### 3. 动态类型（`obj`/`Dyn`）对领域模型的深度腐蚀
*   **命令与事件的可变性劫持**：`Opencode/Hooks.fs` 中存在 `replaceArrayInPlace` 这样的原地数组长度修改。这是宿主 API 约束（`messages.transform` 要求数组引用不变），但仍是危险的原生 JS 副作用，背离 F# 不可变语义。

### 4. 全局可变状态
*   **全局缓存的污染**：`Shell/FuzzyFinderShell.fs` 的 `instances` 和 `pending` 直接声明在模块顶层 Dictionary，未封装生命周期。

### 5. 文件长度超标
*   **`Opencode/NudgeHook.fs`**：478 行，臃肿类包裹大量状态转换逻辑。
*   **`Opencode/Hooks.fs`**：251 行，混合工具定义拦截、参数剔除、类型断言、CAPS 插入等毫不相干的事务。

---

# 二、 地毯式文件具体问题清单（仅剩未完成的）

## Kernel/ 目录

### 1. `Boundary.fs`
*   `SessionId`/`WorkspaceId` 等单箱联合类型（Single-case DUs）未在所有调用入口强制推行。`UnifiedContext.fs`、`NudgeHook.fs` 等仍直接使用 `string`，DU 形同虚设。

### 4. `Dyn.fs`
*   动态辅助工具应当只存在于外壳层。内核中仍有 `Dyn.get`/`Dyn.str`/`Dyn.truthy` 调用，核心逻辑未与动态层剥离。

### 8. `HeadTail.fs`
*   `parsePipe` 和 `scan` 逻辑极其 procedural，大量 mutable index 指针和 4-5 层 if 嵌套，易产生死循环或索引越界。

### 9. `HostKernel.fs`  
*   `buildReveriePrompt` 直接用字符串拼接构建 Prompt，缺乏版本化和结构化手段。

### 14. `ReviewSession.fs`
*   `Registry` 基于裸 `Map`，缺乏并发冲突检测与版本链条控制。

## Mux/ 目录

### 2. `Dedup.fs`
*   `foldMuxReadPartsIntoSeenByPath` 嵌套多层 `for` 循环和运行时动态类型检测。
*   深度侵入 AI SDK 消息字段格式手动解包 `parts`，上游协议变更时静默失效。

## MuxPlugin/ 目录

### 1. `MuxTools/AgentTools.fs`
*   `Tool.bindParallel` 并行 subagent 缺乏取消机制，单个失败不会取消其他。

### 3. `MuxTools/ReviewTool.fs`
*   `registerCallWithTimeout` 硬编码超时（300000ms）挂在全局字典，用户中途取消时注册项不会释放。

### 4. `Delegate.fs`
*   `buildParentRuntimeAiSettings` 参数读取基于裸字符串检测，无类型约束，拼写错误会投递非法设置。

### 5. `MuxEventHook.fs`
*   `handleEvent` 充斥着杂乱判断分支（`if workspaceId = "" then () elif Dyn.isNullish helpers ...`），缺乏事件流管道抽象。

## Opencode/ 目录

### 1. `AgentConfig.fs`
*   `applyAgentConfig` 硬编码内置智能体名称（如 `"coder"`、`"reader"`），违背开闭原则。
*   `mergeObj` 使用 in-place 浅拷贝，可能跨作用域篡改配置。

### 2. `ChildAgent.fs`
*   `workspace` 是顶层可变全局引用（`ref empty`），多请求并发时产生竞争条件。

### 3. `Hooks.fs`
*   `applyReadDedup` 直接篡改 `messages` 数组内部对象字段值，破坏不可变数据语义。
*   `stripUiParameter` 原地操作 JSON schema 内部结构，应使用纯净转换。

### 4. `NudgeHook.fs`
*   `StateHolder` 的 Claim 与 Detached Send 两阶段无事务锁，Detached 阶段会话被删除后 `recordSend` 会对不存在会话写回。
*   `collectSnapshot` 深度依赖宿主私有客户端调用，无防抖与重试退避机制。

## Shell/ 目录

### 2. `ExecutorShell.fs` / `ExecutorJavascript.fs` / `ExecutorProcess.fs`
*   `killTree` 使用 `processKill (-pidNum) "SIGKILL"`，可误杀父级宿主进程。
*   `rewriteJavascriptModuleSpecifiers` 用正则表达式修改 ESM import 指令，极易损坏合法代码。

### 4. `SecureFetch.fs`
*   DNS 解析器直接使用外部异步调用，高并发下可能触发系统域名解析限流，缺乏缓存与退避机制。

---

# 三、 未完成的重构工作

### 2. 精准拆分巨型文件
*   将 `Opencode/NudgeHook.fs`（478 行）拆分为纯策略与外壳发送流。
*   将 `Opencode/Hooks.fs`（251 行）重塑为只读组合子流水线管道。

### 3. 以类型立边界
*   在 `Registration.fs`、`Hooks.fs` 等入口处将 JS `obj` 转换为强类型 F# 记录，内核禁止使用 `obj` 参数。
*   `Dyn` 工具仅限外壳层使用。

### 4. 重塑并发安全架构
*   `FuzzyFinderShell.fs` 的 `instances`/`pending` 封装为显式生命周期管理的实例。
*   在 `NudgeHook` 和事件总线间引入单一信道更新队列，保证原子性。
