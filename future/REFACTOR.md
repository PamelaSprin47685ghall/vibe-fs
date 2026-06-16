根据《宝典》的极简架构与编码铁律，针对当前代码库 vibe-fs-refactor 进行地毯式、从根源到细节的彻底排查。本报告不提供具体重构代码，而是以严苛的“君子不立危墙之下”标准，列出所有不合理、勉强工作、违反架构边界、以及冗余复杂的设计问题，并给出无保留的重构蓝图。请你按照本报告实现。

---

# 一、 系统级根上设计缺陷彻底排查

### 1. 内核（Kernel）与外壳（Shell）边界严重混淆
《宝典》指出：“纯函数是内核，外壳是效果。”内核不应感知任何具体的宿主环境、时钟、文件系统或网络。但在当前 `Kernel/` 目录中，大量文件直接引入了副作用或平台依赖：
*   **平台环境泄漏**：`Kernel/FuzzyQuery.fs` 和 `Kernel/UtilPath.fs` 导入了 `node:path`。这使内核强绑定在 Node.js 环境中，在浏览器或 Web Worker 中运行时会直接崩溃。
*   **加密与指纹依赖**：`Kernel/CapsFormat.fs` 导入了 `node:crypto` 的 `createHash`。计算指纹是外壳在准备上下文时该做的事，或者应由外壳传入纯粹的哈希函数。
*   **状态与副作用驻留**：`Kernel/IteratorStore.fs` 内部声明了可变的 `Dictionary` 和全局计数器 `globalIteratorStore`；`Kernel/ReviewRuntime.fs` 包含了可变的 `registry` 与 `effects`（甚至保存了外部异步回调 `resolve`）；`Kernel/Nudge.fs` 包含了可变的 `NudgeCoordinator`。这些本属于外壳维护的状态运行在内核目录中，污染了核心域。

### 2. “兜底（Fallback）”设计掩盖真实问题
《宝典》明示：“任何时候，只要听到 fallback 兜底两个字，就极端厌恶，兜底就是在逃避问题。”当前逻辑中存在大量逃避 LLM 响应不合规、或者执行链异常的掩盖设计：
*   **计划引擎的妥协**：在 `Kernel/PlanEngine.fs` 中，当分支、审查或池生成失败时，使用 `emptyCandidate`、`emptyCritique`、`fallbackPoolEntries`、`fallbackDecision` 进行降级处理。这导致生成的计划文件可能包含大量的空白 markdown 和无意义的“Fallback”占位，掩盖了模型调用失败的根源。
*   **模糊搜索隐式降级**：`Shell/FuzzyGrepCmd.fs` 中的 `resolveResult`，在正则搜索未命中且无迭代器时，隐式自动发起模糊搜索进行兜底。这种不通知上层、不报错的隐式转换，使得调用方无法区分“无精确匹配”与“模糊推荐”的区别。
*   **文本匹配脏黑客（Dirty Hack）**：`MuxPlugin/MuxTools/ReviewTool.fs` 中的 `fallbackParseVerdict`，在结构化解析失败时，使用正则表达式去强行匹配 PASS、REJECT 等文本。这破坏了类型安全的交互边界。

### 3. 动态类型（`obj`/`Dyn`）对领域模型的深度腐蚀
核心业务领域（特别是 `Plan*` 相关的多分支规划引擎）由于过度依赖 JS 互操作，导致领域模型被 `obj` 彻底腐蚀：
*   **无安全约束的数据读取**：`Kernel/PlanCommon.fs`、`Kernel/PlanHypotheses.fs`、`Kernel/PlanRevision.fs` 等文件充斥着对 `obj` 的手动拆包（`optString`、`optStringArray`、`optFloat`）。这些复杂的动态字段防御逻辑本应收敛在宿主插件接入的第一公里，将输入转换为强类型 F# 记录（Record），而不是让 `obj` 渗透到整个规划管道的每个节点。
*   **命令与事件的可变性劫持**：`Opencode/Hooks.fs` 中存在 `replaceArrayInPlace` 这样的原地数组长度修改（直接修改 JS 数组的 `length` 并进行 `push`）。这是极其危险的原生 JS 副作用，违背了 F# 的不可变更新和数据驱动语义。

### 4. 全局可变状态与副作用导致的线程安全隐患
JavaScript 虽为单线程事件循环，但并发的异步调用（如并发调用 `editor` 意图）会因为共享全局可变状态而发生隐式竞争：
*   **测试劫持的隐式状态泄漏**：`Mux/CapsFileRead.fs` 中的 `timestampSource` 采用全局可变变量，并在 `Index.fs` 中暴露了 `setCapsFileReadTimestampSource` 修改方法；`MuxPlugin/MuxSlashCommands.fs` 中的 `dateNowSource` 亦采用类似的可变引用。这种硬编码的可变函数桩在并发测试或多实例运行时会相互覆盖，导致严重的时间不一致问题。
*   **捕获引用的生命周期混淆**：`MuxPlugin/MuxTools/IoTools.fs` 中的 `hostFileReadExecute`，在 `MuxWrappers.fs` 拦截到宿主工具时直接对该全局变量赋值。一旦存在多个 Mux 实例或并发会话，全局变量会被覆盖，导致旧会话调用了新会话绑定的宿主工具。
*   **全局缓存的污染**：`Shell/FileReadCache.fs` 的文件缓存和 `Shell/FuzzyFinderShell.fs` 的 `instances` 均直接声明在 F# 模块的顶层，且无显式生命周期控制。

### 5. 严重违背文件与函数长度铁律
《宝典》警告：“单函数超40-60行单文件超200-300行死。拆为模块。”以下文件与函数已彻底“炸弹化”：
*   **`Kernel/PlanEngine.fs`**：`runPlanPipeline` 逻辑膨胀至 110 行以上，混合了 Lenses 列表初始化、并行异步任务构建、四阶段（分支、诊断、池拓展、修订）任务编排、结果解包与异常处理、法官决策等复杂工序。
*   **`Shell/CapsShell.fs`**：长达 170 行，且混合了文件过滤正则、Stat 检查、字节预算计算、递归目录发现等。
*   **`Opencode/NudgeHook.fs`**：接近 300 行的臃肿类，内部包裹着多达 10 个以上复杂的私有辅助状态转换（`handleSessionDelete`、`handleSessionNextPrompted` 等）。
*   **`Opencode/Hooks.fs`**：混合了工具定义拦截、参数剔除（`_ui` 处理）、类型断言以及 CAPS 插入等毫不相干的事务。

---

# 二、 地毯式文件具体问题清单

## Kernel/ 目录

### 1. `Boundary.fs`
*   **问题**：单箱联合类型（Single-case DUs）设计未在接口边界强制推行，导致在所有调用入口（如 `UnifiedContext.fs`、`Nudge.fs`、`NudgeHook.fs`）均使用 `string`，在使用时再通过 `Id.sessionId` 临时转换或通过原始值操作，未能起到阻断类型偷渡的作用。

### 2. `CapsFormat.fs`
*   **问题**：
    *   直接导入了 `node:crypto`，违背纯内核原则。
    *   `buildCapsMessages` 函数嵌套层数过深，多次对 raw 数组进行动态切片（`messages.[2..]`），若输入消息数组不满足预期结构，将直接引发数组越界异常。

### 3. `Dedup.fs`
*   **问题**：`deduplicate` 使用了低效的 `output.Contains(seen)` 来做内容重复判定。当大文本发生部分重叠时，这种判定不仅计算复杂度高（$O(N^2)$），还会由于子字符串误匹配导致输出内容被意外截断为 `dedupMarker`。

### 4. `Dyn.fs`
*   **问题**：虽然是动态辅助工具，但通过抛弃编译器静态检查来实现 JS 互操作。它应当只存在于极薄的外壳层。在内核中随处可见对 `Dyn.get`、`Dyn.str`、`Dyn.truthy` 的调用，说明核心逻辑并未与动态层剥离。

### 5. `ExecutorKernel.fs`
*   **问题**：
    *   `byteLength` 内部手动遍历高低代理项（Surrogate Pairs）和移位运算，这本应是外壳利用 Node `Buffer.byteLength` 解决的偶然复杂度。
    *   `formatSafetyWarning` 针对 `Shell` 脚本做首词分割校验，直接用硬编码字符进行 `Trim()` 和 `Split()`，在脚本包含首行注释或管道命令前缀时会完全失效，无法起到防御作用。

### 6. `FuzzyFormat.fs`
*   **问题**：`formatGrepOutput` 的折叠算法（`List.fold`）可读性极差，内部不断进行 `lines @ before @ main @ after` 的列表拼接操作。在 F# 中，大列表拼接（`@`）是 $O(N)$ 复杂度的，频繁调用会产生大量内存分配垃圾，严重拖慢搜索渲染性能。

### 7. `FuzzyQuery.fs`
*   **问题**：
    *   导入 `node:path`。
    *   `normalizeExcludes` 接受 `obj option` 类型的排除规则，然后通过复杂的反射类型检测（`Dyn.isArray`）和分词正则去兼容。这些脏类型兼容工作必须移至外壳 API 输入端进行归一化，内核应只接收不可变的、强类型的参数列表。

### 8. `HeadTail.fs`
*   **问题**：`parsePipe` 和 `scan` 逻辑极其 procedural。内部充斥着大量的 mutable index 指针更新（`i <- next`，`buf.Add(ch)`），且代码嵌套极深（`if` 嵌套 4-5 层），极易在修改时产生死循环或索引越界，不符合函数式编程的声明式风格。

### 9. `HostKernel.fs`
*   **问题**：`buildReveriePrompt` 函数直接用原始字符串拼接构建复杂 Prompt。对大模型 Prompt 的管理缺乏版本化和结构化手段，不便于后期对提示词进行正交微调。

### 10. `IteratorStore.fs`
*   **问题**：一个状态容器，却被置于 `Kernel/`。在多用户/多工作区高并发场景下，依靠一个全局的、非线程安全的 `Dictionary` 极易引发竞态崩溃。

### 11. `Lru.fs`
*   **问题**：`set` 逻辑中包含 `Map.remove` 和 `Map.add`，但其辅助函数 `removeKey` 的实现是 `List.filter`（$O(N)$ 复杂度）。由于 LRU 操作属于高频核心链路，频繁的列表过滤和重构在缓存容量增大时会产生严重的性能退化。

### 12. `McpConfig.fs`
*   **问题**：直接读取 `nodeProcess?env`，使得核心配置解析变成了非纯函数。

### 13. `Nudge.fs`
*   **问题**：
    *   直接内置了 `NudgeCoordinator`  mutable 状态管理类，暴露了 `suppress` 等副作用方法。
    *   `decide` 的匹配规则极度脆弱：直接依靠正则表达式（如 `skipTodoRe`、`questionRe`）去识别大模型输出的 HTML 标签和尾部问号。如果大模型输出的 Markdown 中包含了类似缩进的代码段，将会产生误判。

### 14. `Permission.fs`
*   **问题**：`computePermissions` 对规则集（`UniversalRule`）进行合并时，采用首个命中规则覆盖的原则，这依赖于规则序列在输入时的微秒级相对顺序。一旦规则列表构建逻辑发生重构导致顺序调整，整个权限判定可能会发生致命的反转，缺乏防呆机制。

### 15. `Plan*` 模块群 (PlanBranches/PlanCritique/PlanHypotheses/PlanJudge/PlanPool/PlanRevision)
*   **问题**：
    *   各个模块内部手工声明了高度重复的 JSON Schema 构建逻辑（`mkSchema`、`strProp`）。
    *   `parsePlan*ToolCall` 函数群包含了大量的重叠解析代码，每个字段都独立使用 `ResultBuilder` 进行链式防空检测。
    *   严重违反“兜底”禁令：在 `PlanEngine.fs` 内部深度依赖各种 Failback 结构来消化中间步骤抛出的错误，导致规划链條变成了一个即使完全损坏也能正常输出空白产物的假装工作系统。

### 16. `ReviewRuntime.fs` 与 `ReviewSession.fs`
*   **问题**：
    *   `ReviewStore` 使用非原子的 `mutable` 变量在宿主插件钩子之间同步数据，且保存了高风险的 `resolve` 外部引用，可能导致内存泄漏。
    *   在 `ReviewSession.fs` 中，事件源（Event Sourcing）设计在 `reduce` 中虽然优雅，但是 `Registry` 依然是基于裸 `Map`，缺乏并发冲突检测与版本链条控制。

---

## Mux/ 目录

### 1. `CapsFileRead.fs`
*   **问题**：
    *   硬编码的全局测试插桩变量 `timestampSource` 破坏了单例隔离。
    *   文件内容行号生成（`withLineNumbers`）直接使用行分割并遍历追加制表符，当遇到包含海量行数的二进制或日志文件时，会直接造成宿主事件循环阻塞。

### 2. `Dedup.fs`
*   **问题**：
    *   `foldMuxReadPartsIntoSeenByPath` 嵌套多层 `for` 循环和运行时动态类型检测，其逻辑极其重。
    *   为了实现去重，深度侵入了 AI SDK 模型的消息字段格式，手动解包 `parts`，一旦上游协议发生细微更迭（例如 `toolName` 命名空间变更），该去重逻辑将直接失效并静默退化为不再去重。

---

## MuxPlugin/ 目录

### 1. `MuxTools/AgentTools.fs`
*   **问题**：`Tool.bindParallel` 创建了多个 subagent 任务，并通过 `Async.Parallel` 并行执行。由于宿主环境本身是非阻塞的，如果某一个子 subagent 失败并抛出致命错误，其他分支的 `Async` 资源缺乏协同取消（Cooperative Cancellation）机制，会导致资源在后台死锁或悬空。

### 2. `MuxTools/IoTools.fs`
*   **问题**：
    *   `hostFileReadExecute` 全局静态变量可能引发多工作区实例的数据覆盖。
    *   `writeTool` 中针对写操作失败的处理过于简单，没有对异常进行精确的强类型归类，仅仅粗暴地通过捕获外壳抛出的普通文本 `ex.Message` 返回给调用方。

### 3. `MuxTools/ReviewTool.fs`
*   **问题**：
    *   通过 `registerCallWithTimeout` 创建了一个带有硬编码超时（300000ms）的异步任务挂在全局字典上。一旦用户中途强行取消该动作，该注册项不会被安全地释放，只能等待超时过期，造成资源缓慢泄漏。
    *   依赖文本模糊判定 `fallbackParseVerdict` 作为终审依据，鲁棒性极差。

### 4. `Delegate.fs`
*   **问题**：`buildParentRuntimeAiSettings` 的参数读取完全基于裸字符串检测，没有任何针对大模型名称及思考档位的类型级别约束，容易因为拼写错误而将非法设置投递给大模型。

### 5. `MuxEventHook.fs`
*   **问题**：`handleEvent` 充斥着极其杂乱的判断分支（`if workspaceId = "" then () ... elif Dyn.isNullish helpers ...`），缺乏主线事件流管道（Pipeline）抽象。

### 6. `MuxSlashCommands.fs`
*   **问题**：`/plan`  slash 命令直接在外壳层充当了整个规划引擎的拼装中心，不仅负责初始化 GUID、声明并自增闭包计数器 `callCounter`，还负责文件去重写、格式化报告投递。代码严重违反职责单一原则。

### 7. `ResolveAiSettings.fs`
*   **问题**：`mergeNamedSettings` 采用极其脆弱的循环合并，若上游配置出现环路引用或不规整的空对象（`null`），将会直接崩溃并抛出未经归类的底层 JS 异常。

---

## Opencode/ 目录

### 1. `AgentConfig.fs`
*   **问题**：
    *   `applyAgentConfig` 内部手工枚举和转换特定的内置智能体名称（如 `"editor"`、`"greper"`）。如果系统未来进行垂直能力扩展，必须回过头修改硬编码的列表和转换规则，这完全违背了开闭原则（OCP）。
    *   `mergeObj` 的合并逻辑直接依靠 in-place 浅拷贝，极易把上层配置项的引用共享给内部默认值，引发难以调试的跨作用域状态篡改。

### 2. `ChildAgent.fs`
*   **问题**：`workspace` 是一个位于顶层的可变全局引用（`ref empty`）。这种在 F# 模块中共享全局状态的作法，使得多用户并发请求同一个工作区上下文时，不可避免地产生竞争条件，引发底层会话注册表的读写撕裂。

### 3. `Hooks.fs`
*   **问题**：
    *   `isStringArray` 采用古老的命令式 `while` 循环加变迁条件，极其不优雅。
    *   `applyReadDedup` 极其危险地直接篡改了 `messages` 数组内部引用的对象字段值（`setOutput state dedupMarker`）。在不可变架构下，严禁进行这种破坏原始消息足迹的 in-place 动态篡改。这会导致历史重放调试和日志排查变得完全不可能。
    *   `stripUiParameter` 破坏性地直接操作 JSON schema 内部结构来剔除 UI 专属定义，本应使用更优雅的映射函数进行结构体纯净转换。

### 4. `NudgeHook.fs`
*   **问题**：
    *   `StateHolder` 类在单线程模型中看似防止了竞态，但为了实现非阻塞 I/O，将逻辑割裂为 Claim（抢占）和 Detached Send（独立发送）。由于两个阶段之间没有事物级别锁，如果在 Detached 阶段会话被删除，后续在 `recordSend` 时将会对一个不存在的会话强行进行过时的写回操作，进而破坏一致性。
    *   `collectSnapshot` 内部深度依赖宿主的私有客户端调用，没有任何防抖与重试退避机制。

### 5. `PlanCommand.fs`
*   **问题**：`jsonSchemaToZodShape` 是一个手写的高复杂递归转换器，用于将原始 Schema 结构动态解析并塞入 SDK 原生对象中。这里逻辑层级极多，由于对外部传入的 AST 节点缺乏充分的格式校验，一旦遇到包含多重嵌套 Union 或递归属性的参数规范，该转换器将直接引发堆栈溢出崩溃。

---

## Shell/ 目录

### 1. `CapsShell.fs`
*   **问题**：
    *   `discoverFilesInDirAsync` 重度依赖尾递归和自制 accumulator，且在处理大目录结构时会并发分配巨量临时任务。
    *   硬编码排除目录集合 `excludedDirNames` 采用大写判定，对非英文目录（或混合大小写多层目录路径）的正则捕获与不敏感处理可能造成敏感信息泄漏或意外包含庞大目录。

### 2. `ExecutorShell.fs` / `ExecutorJavascript.fs` / `ExecutorProcess.fs`
*   **问题**：
    *   `killTree` 中在 Unix 下使用 `processKill (-pidNum) "SIGKILL"`，这种杀死整个进程组（Process Group）的设计非常强硬，如果没有精确处理权限、或进程组所有权被共享，会导致直接干掉父级宿主进程。
    *   `rewriteJavascriptModuleSpecifiers` 极其暴力地采用正则表达式去定位和修改 ESM 的静态/动态 import 指令。众所周知，单凭正则表达式无法在保留字符串边界、混淆注释、以及模板字符串语义的前提下绝对安全地解析语法，该实现极易损坏合法代码。

### 3. `FileReadCache.fs`
*   **问题**：采用纯手动操作、非线程安全的两组可变变量（`store` 和 `order`）来实现 LRU Evict 机制。逻辑没有封装成类，极其松散。

### 4. `SecureFetch.fs`
*   **问题**：DNS 拦截与安全判定（`resolveAll`）虽然防止了 SSRF，但其解析器直接使用外层异步调用。一旦进入高并发流量，大量的 DNS 请求可能遭遇系统级别的域名解析限流，缺乏内置的 DNS 缓存和抖动退避机制。

---

# 三、 不可妥协的重构方案蓝图

为将此系统彻底升级为“君子不立危墙之下”的优雅、绝对可工作的系统，重构时必须遵循以下蓝图进行改造，严守职责与边界隔离。

```
+-----------------------------------------------------------------------------------+
|                                  SHELL (外壳层)                                   |
|  - File System, Node process, Network & IO                                        |
|  - Stateful Stores, Global Registry Instances                                     |
|  - Boundary Parsers & Error Translators (from dynamic obj to typed Records)        |
+----------------------------------------+------------------------------------------+
                                         | (Input parsed directly into strict types)
                                         v
+-----------------------------------------------------------------------------------+
|                                 KERNEL (纯内核层)                                 |
|  - Zero Platform Imports (No node:path, node:crypto, node:net, etc.)              |
|  - Immutable Data Structures (Pure Records, Discriminated Unions)                 |
|  - Pure Domain Workflows (Lenses, Planning Pipelines, Review State Transitions)  |
+-----------------------------------------------------------------------------------+
```

### 1. 彻底纯化内核，剥离所有外壳副作用
*   **无宿主环境污染**：
    *   严禁在 `Kernel` 下的任何文件中直接导入 Node.js 原生模块（如 `path`、`fs`、`crypto`）。
    *   所有路径操作采用纯 F# 实现的跨平台逻辑（以正斜杠作为唯一标准分割符），在输出至宿主系统的一瞬间再由外壳进行平台自适应规整。
    *   哈希值计算抽象为纯函数签名（`string -> string`），由外壳层（例如 `Shell/Crypto.fs`）实现并作为依赖在调用时注入。
*   **状态与可变对象外移**：
    *   将 `IteratorStore`、`NudgeCoordinator`、`ReviewStore` 等具备生命周期的可变状态管理器彻底移出内核。
    *   内核中只保留无状态的纯数据流：例如 Nudge 的决策仅输入 `NudgeContext` 返回 `NudgeAction`；工作区状态的更新（`WorkspaceState`）仅通过事件溯源折叠纯数据。

### 2. 精准拆分巨型文件，严守行数铁律（300/60 行）
*   **规划管道分工化（Decoupled Pipeline）**：
    *   将 `Kernel/PlanEngine.fs` 那个庞大的 `runPlanPipeline` 彻底爆破。
    *   将管道的五个核心节点（Hypotheses, Branches, Critique, Pool, Revision, Judge）重构为各自独立且单纯的无状态模块。
    *   整个编排过程退化为一个轻量级的协调函数，仅负责并发任务的装配。
*   **外壳能力颗粒化（Granular Shell）**：
    *   将 `Shell/CapsShell.fs` 按领域事实拆分为：`CapsFilter.fs`（纯正则和黑名单匹配）、`FileBudget.fs`（大小和容量审计）和 `DirectoryTree.fs`（异步 walk 操作）。
    *   将 `Opencode/NudgeHook.fs` 拆分为：`NudgePolicy.fs`（纯判定内核）与 `NudgeWorker.fs`（外壳异步发送流）。
*   **拦截钩子流水线化（Pipeline Hooks）**：
    *   将 `Opencode/Hooks.fs` 重塑为高度抽象的、只读的组合子流水线（Pipe），每一个中间钩子执行完毕后通过强类型输出给下一个，彻底废除 in-place 动态篡改。

### 3. 以“类型立边界”，阻断动态类型和 Fallback 毒素
*   **零 Fallback 契约设计（Zero-Fallback Design）**：
    *   全面移除一切带有妥协色彩的“兜底”机制。如果大模型输出的 JSON 结构损坏、或关键字段缺失，管道必须立即抛出极其精准的强类型管道错误（如 `PipelineError.BranchSchemaMismatch`）。
    *   外壳层（如插件端）捕获该错误后，应明确发起 retry 决策，或者终止流程向用户抛出具体的诊断报告，严禁假装正常工作。
*   **在入口一公里完成强类型映射（Parse, Don't Validate）**：
    *   在宿主环境工具接入（如 `Registration.fs`、`Hooks.fs`）的第一公里，立即使用类型验证器（类似于基于 F# 强类型反射自动生成的契约解析器），将 untyped JS 传入对象（`obj`）转换为内核不可变的、类型严苛的 F# 记录。
    *   自此往后，不允许再调用 `Dyn` 工具。禁止在内核业务代码中使用任何 `obj` 作为参数。

### 4. 重塑异步与并发安全架构
*   **消灭全局静态 mutable 变量**：
    *   彻底清除 `timestampSource`、`hostFileReadExecute`、`dateNowSource`、`pendingCalls` 等顶层全局可变宿主变量。
    *   所有的状态注册和外部调用对象（如宿主的 `read` 执行器引用、超时等待 promise）通过上下文实例（`WorkspaceContext`）在运行周期内伴随流转，该上下文由外壳实例在每一个独立的会话启动时全新创建。
*   **基于安全事务原语的事件监听**：
    *   在 `NudgeHook` 和事件总线间引入单一信道的更新队列，事件监听和决定抢占完全保证原子性，杜绝多进程/并发请求因状态更新延迟导致的历史记录覆盖。
