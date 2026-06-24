这份 F# (基于 Fable 编译到 JS) 的代码库实现了一个相当复杂的 AI Agent / MCP（Model Context Protocol）工具集。尽管 `Kernel/` 层做了一定的纯函数抽象，但在命名语义和整体架构边界上确实存在明显的“坏味道”（Code Smells）。

以下为你梳理的代码库中**名实不符（命名问题）**与**结构不当（架构与设计问题）**的清单，供重构团队参考：

### 一、 名实不符（命名违背真实意图）

1. **`Magic` 前缀的滥用 (位于 `MagicTodo`, `MagicCore`, `MagicSessionStore`)**
   * **实际行为**：这部分代码实际上负责的是“工作积压（Backlog）重放”、“已完成历史折叠”以及“UI 状态投影”，目的是防止 LLM 上下文溢出。
   * **问题**：`Magic` 是编程中最忌讳的无语义命名。应该重命名为 `WorkBacklog`、`SessionProjection` 或 `HistoryCompaction` 等能体现业务领域的词。
2. **`OllamaClient` 挂羊头卖狗肉**
   * **实际行为**：代码中的 `ollamaPost` 被用来请求 `web_search` 和 `web_fetch` 接口。
   * **问题**：Ollama 是一个本地/远程的大模型运行器，本身不提供网页搜索和抓取功能。这里显然是后端魔改了 Ollama 的 API 或者用了一个同名代理网关。应改名为 `WebSearchApi` 或 `AgentBackendClient`，避免误导后来的维护者。
3. **`HostReadExec` 并非执行器 (位于 `Mux/Wrappers` 等)**
   * **实际行为**：它只是一个带有 `TryGet` 和 `Capture` 方法的全局闭包容器（一个包含 Mutable Option 的类），用来在系统初始化时“截获”并保存宿主环境的原始 `file_read` 函数指针。
   * **问题**：带有 `Exec` 后缀让人误以为它负责执行逻辑。应当改名为 `NativeReadCallbackRegistry` 或 `HostFunctionCapture`。
4. **`TreeSitterKernel` 混入了非 AST 逻辑**
   * **实际行为**：包含了纯正则匹配的 `pathsFromPatchText` 和 `isFileEditTool` 的硬编码 Set。
   * **问题**：Tree-sitter 是精确的 AST 解析器，但这个文件里混入了粗糙的正则匹配和工具名称分类。这些应该被移到 `PatchParser` 和 `ToolCatalog` 中。
5. **`NudgeHook` 承担了过多生命周期职责**
   * **实际行为**：不仅处理 Nudge（催促/挂起唤醒），还拦截了 `chatMessage`、`toolExecuteAfter` 和 `commandExecute`。
   * **问题**：它实际上是一个 `SessionLifecycleObserver` 或 `AgentEventTracker`，名为 `NudgeHook` 掩盖了它作为全局拦截器的实质。

---

### 二、 结构不当（架构与设计缺陷）

#### 1. 边界类型丢失（“Any/Obj” 满天飞）
* **症状**：在 `Mux/` 和 `Opencode/` 两个宿主接入层中，大量使用了 F# 的 `obj`（相当于 C# 的 `object` 或 TS 的 `any`）。并且通过 `Dyn.fs` 里的 `Dyn.str`、`Dyn.get` 这种字符串反射的方式去读取属性。
* **重构建议**：失去了 F# 强类型的最大优势，代码极度脆弱。应该在边界处定义 Fable 的 `[<Emit>]` 接口或标准的 F# Record，使用 `Thoth.Json` 或类型提供程序在入口处一次性反序列化，内部严禁传递 `obj`。

#### 2. `Mux` 与 `Opencode` 存在大量“平行宇宙”式的重复代码
* **症状**：`Mux/SubagentTools.fs` 和 `Opencode/SubagentTools.fs`，或者两边的 `MessageTransform.fs`，内部逻辑有 80% 以上是完全重复的。仅仅因为两边传入的 Context 对象或回调 API 签名有微小差异，就复制了整个文件。
* **重构建议**：应当提取一个跨宿主的 `HostAdapter` 接口或函子（Functor）。通用逻辑下沉到 `Kernel`，宿主层只负责实现 `ISessionIO` 或 `IToolDispatcher` 接口。

#### 3. 领域错误（Domain Errors）与字符串异常的混用
* **症状**：虽然在 `Domain.fs` 定义了优秀的 `DomainError`（如 `MessageAborted`，`SystemPanic`），但在工具执行（如 WebTools、SearchTools）中，经常直接返回 `JS.Promise<string>`，把错误信息直接拼接成字符串返回给大模型。同时 `ErrorClassify.fs` 又试图通过正则或特征匹配把 JS 异常反向猜回 `DomainError`。
* **重构建议**：应该统一使用 F# 的 `Result<'T, DomainError>` 贯穿整个调用链。只有在最终呈现给大模型的那个 `encode` 环节，才把 Error 统一 format 成字符串说明。

#### 4. 全局可变状态（Mutable State）与并发隐患
* **症状**：`Shell/` 目录下的多个服务，如 `MagicSessionStore`、`FuzzyIteratorStore`、`ChildAgentRegistry`、`SessionExecutor` 都在模块顶层使用了 `mutable` 字典或全局单例。
* **重构建议**：在一个异步且可能处理多个并发 Workspace / Session 的 Agent 环境中，全局 Mutable 极其危险（容易串数据或内存泄漏）。应将这些状态封装到 `SessionContext` 中随请求传递，或使用标准的 Actor 模型（F# MailboxProcessor）来管理并发状态。

#### 5. `KnowledgeGraphRuntime` 职责严重违背单一原则 (SRP)
* **症状**：这个类（无论在 Mux 还是 Opencode 侧）既管理着内存状态（`state <- reducer`），又直接持有 IO 队列（`SerialQueue`），还负责拼接 Markdown Prompt，最后还留了 `TakeBookkeeperLaunchesForTesting` 这种专门给测试用的后门方法污染生产代码。
* **重构建议**：应拆分为三层：`KnowledgeGraphState`（纯逻辑，已存在但未被很好利用）、`KnowledgeGraphStorage`（只负责 NDJSON 的读写互斥锁）、`KnowledgeGraphWorkflow`（负责编排大模型调用）。测试后门应通过依赖注入在测试环境挂载，而非写载生产类里。

#### 6. Tool (工具) 定义与依赖注入方式过于原始
* **症状**：工具的定义（`execute` 闭包）里，强耦合了对巨型 `config`/`context` obj 对象的硬编码解析（例如到处都是 `Dyn.str ctx "directory"`）。而且工具内部直接调用了子 Agent 创建、网络请求等高阶副作用。
* **重构建议**：引入标准的依赖注入（DI）容器或 Reader Monad 模式。工具执行函数的签名应改为 `execute: ToolArgs -> IToolContext -> Async<ToolResult>`。上下文中应直接提供强类型的 `CurrentDirectory`、`AbortToken` 和 `ILogger`，而不是让工具自己去一堆 `obj` 里大海捞针。

#### 7. 文案与提示词分散，重复在各工具执行器里硬编码
* **症状**：工具描述、参数说明、拒绝语、错误前缀、成功摘要、`completedWorkReport` / `select_methodology` 之类提示词字段说明，分散在 `Opencode/`、`Mux/`、`Mimo-Code/` 的多个工具实现里；同义文案在不同宿主里重复出现，只改业务不改文本会立刻漂移。
* **调研参考**：`opencode/packages/core/src/tool/tool.ts` 已把 `description`、`input`、`output`、`permission` 分成独立语义层；`mux/src/node/services/tools/mux_agents_read.ts`、`mux/src/node/services/tools/mux_agents_write.ts`、`mux/src/node/services/tools/mux_config_read.ts`、`mux/src/node/services/tools/mux_config_write.ts` 仍把文案直接拼在 `execute` 内；`vibe-fs/src/Opencode/MimoTodoTool.fs` 与 `Kernel/MagicTodo.fs` 已暴露出提示词字段和工具文案重复的问题。
* **重构建议**：建立统一的文案目录，把 `tool description`、`field description`、`refusal text`、`error prefix`、`success summary`、`prompt snippet` 分层归类；工具实现只引用语义键，不直接写长句。

#### 8. agent 工具权限按字符串直接匹配，缺少语义映射层
* **症状**：权限判断仍围绕工具名字符串、前缀、子串规则做直接匹配，规则分散在 `Mux`、`Opencode`、`Kernel` 不同位置；新增工具时必须手工补多处字符串分支，容易出现漏配、错配、宿主间语义不一致。
* **调研参考**：`opencode/packages/core/src/tool/tool.ts` 已证明工具注册可抽象为 canonical tool + permission action；`opencode/packages/core/src/tool/tools.ts` 说明注册层与执行层应分离；`mux` 侧多个工具已经以 `TOOL_DEFINITIONS` 统一描述工具，但权限语义仍未统一上浮成独立映射表。
* **重构建议**：先把所有 agent/tool 名称映射成有限语义类型（如 read/write/search/edit/submit/notify/capture 等），再用单一 pattern matching 函数按语义分派权限，而不是在权限逻辑里直接匹配裸字符串。字符串只允许存在于最外层注册表或适配层，不允许继续作为核心决策键。
