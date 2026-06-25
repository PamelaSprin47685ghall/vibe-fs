### 一、 模块与文件体量超标（违反“单文件超二三百行即死”铁律）

宝典规定：*“单函数超五六十行单文件超二三百行即死，新建文件，移走样板，拆为模块，绝不姑息。”* 项目中存在多处体量超标、多重职责耦合的重灾区：

1. **`Shell/TreeSitterShell.fs`（接近 300 行）**
   * **问题**：该文件同时承载了 Node.js 动态 `require` 路径解析、OS 平台架构探测、WASM 语法包加载、Tree-Sitter 抽象语法树深度遍历（`collectDiagnostics` 递归函数）以及高亮分析等多重职责。
   * **重构建议**：应拆分为 `Shell/TreeSitterPlatform.fs`（负责平台探测与动态加载）与 `Shell/TreeSitterAst.fs`（负责 AST 递归遍历与诊断转换）。

2. **`Kernel/Methodology.fs`（超过 200 行）**
   * **问题**：将庞大的方法论描述文本（`methodologyCatalog` 字符串常量）与内核业务逻辑混在一起，导致核心逻辑被海量非结构化文本淹没。
   * **重构建议**：将纯文本元数据剥离至独立的 `.txt` 或 `.json` 资源文件中，通过编译期嵌入或轻量 I/O 加载。

3. **`Omp/Plugin.fs` 与 `Omp/PluginCore.fs`**
   * **问题**：生命周期钩子注册、工具网络请求、语法检查后置处理等逻辑高度纠缠。
   * **重构建议**：提取独立的生命周期中转模块，避免插件入口直接耦合底层执行细节。

---

### 二、 边界类型失守与动态类型偷渡（违反“类型系统是最便宜边防”法则）

宝典规定：*“字符数字布尔最会偷渡错误……概念独立命名在运行时零成本，维护时直击知识边界。”* 

1. **宿主适配层中的 `obj` 与动态算符 `?` 滥用**
   * **代表文件**：`Mux/Wrappers.fs`、`Omp/ChildSession.fs`、`Shell/Dyn.fs`
   * **问题**：项目深度依赖 `?` 动态算符进行隐式属性读写（如 `tool?execute(args, opts)`、`config?(key) <- ...`）。这种非类型安全的“动态偷渡”让 F# 的强类型编译器在边界处失效，将类型推导退化为运行时盲盒，极易在宿主 API 微调时引发 `Undefined` 崩溃。
   * **重构建议**：为 `oh-my-pi` 与 `Mux` 的宿主上下文、消息、工具定义声明严格的 Fable 外部接口（`[<TypeScript.Mangled>]` 或 `[<Emit>]`），禁止使用动态类型逃避契约。

2. **强类型 Domain ID 被中途弃用**
   * **代表文件**：`Kernel/Domain.fs` 定义了严格的 `SessionId`、`WorkspaceId` 区分，但在 `Shell/ToolContextCodec.fs`、`Mux/AiSettings.fs` 及各宿主 Codec 中，大量逻辑依然使用裸 `string` 进行透传。
   * **问题**：防线未能在最外层对齐，导致在核心业务流中依然存在 `SessionId` 与 `WorkspaceId` 混淆的隐患。
   * **重构建议**：从宿主事件解码的第一时间，立刻将其收拢为 `SessionId` 等强类型标签，后续内核管道不允许裸字符通行。

---

### 三、 全局可变状态隐患（违反“并发根本矛盾在共享可变状态”原则）

宝典规定：*“纯函数是内核……真实世界网络文件时钟队列住在外壳……写路径在墙内串行，读路径在墙外并发。”*

1. **Shell 层充斥野生 `mutable` 全局表**
   * **代表文件**：`Shell/RunnerBackground.fs`
   * **问题**：使用多个独立的全局可变 Map（`runnerJobs`、`activeRunnerSessions`、`logBuffers`、`childByParent`、`childDispose`）来维系后台执行状态。这并非并发安全的 Actor 或隔离邮箱，在并发事务下极易发生竞态冲突、断链与内存泄漏。
   * **重构建议**：收拢为一个单一的、受串行执行队列保护的不可变状态记录，或者使用一个局部的并发邮箱处理器（SerialQueue）锁住更新。

2. **闭包内的可变状态隐式共享**
   * **代表文件**：`Shell/ReviewRuntime.fs`
   * **问题**：`createReviewStore` 通过局部 `mutable registry` 与 `mutable effects` 闭包向外暴露修改接口。这种隐式状态虽避开了全局变量，但在多协程重入时无法保证原子性。
   * **重构建议**：ReviewState 应转为纯粹的事件积分器，其更新逻辑应由内核串行管道统一折叠。

---

### 四、 双寄主架构下的对称性破坏与同质化冗余（违反“DRY+KISS”铁律）

宝典规定：*“同一事实多处重复并开始不一致。独立生命周期概念逐字相同也该分居，但技术底座的冗余是架构秩序的塌陷。”*

1. **`TitleFetchGuard` 模块完全同质化复制**
   * **代表文件**：`Omp/TitleFetchGuard.fs` 与 `Opencode/TitleFetchGuard.fs`
   * **问题**：两个文件内的 `titleRequestSignature`、`wrapForTitle`、`isTitleRequestBody`、`rewriteTitleMessages` 逻辑及 XML 标签提取算法完全一致，属于无情的手工复制。
   * **重构建议**：合并并移入 `Shell/MessageTransformCommon.fs` 或新增 `Shell/TitleFetchGuardCommon.fs`，由两端宿主直接引用。

2. **I/O 逻辑在 `Mux` 与 `Omp` 适配层中各行其是**
   * **代表文件**：`Mux/KnowledgeGraphRuntimeIO.fs` 与 `Omp/KnowledgeGraphRuntimeIO.fs`
   * **问题**：针对知识图谱的 Ndjson 写入、草稿格式化逻辑大体相同，却分别实现了两套接口转译。一旦知识图谱格式变更，两端极易发生同步遗漏。
   * **重构建议**：将 I/O 汇聚到 `Shell/KnowledgeGraphWorkflow.fs`，宿主适配层仅保留纯粹的上下文翻译。

---

### 五、 异常捕获流失与不透明流转（违反“业务可预见失败不伪装成异常”规范）

宝典规定：*“业务可预见失败不伪装成异常，找不到未授权库存不足余额不够都是返回类型具体分支，调用方被迫面对。”*

1. **异步 Promise 链中的裸抛与默默吞掉**
   * **代表文件**：`Shell/PromiseQueue.fs`、`Mux/Delegate.fs`
   * **问题**：在 `taskWait` 等异步操作中，异常通过 `try...with` 捕获后，部分分支直接将其转为普通字符串（如 `"Error: " + message`）返回给上游，而另一些分支则直接抛出，导致调用方无法通过类型系统强制穷尽失败分支。
   * **重构建议**：凡是涉及跨代理通信、超时、网络 I/O 的调用，返回值必须统一包装为 `Result<'T, DomainError>`，逼迫消费方在编译期完成安全解包，严禁将异常作为控制流工具。