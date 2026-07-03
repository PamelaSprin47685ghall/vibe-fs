基于对您提供的完整代码库的深度梳理，以下为您指出系统中的设计缺陷、潜在编程隐患，并提供一份**事无巨细、不含任何代码实现、完全侧重于类型推导与架构重组的“保姆式”重构蓝图**。

---

# 第一部分：核心设计与编程缺陷诊断

### 缺陷一：动态类型污染与类型安全屏障失效（`obj` 与 `'raw` 的滥用）
F# 语言的核心优势在于利用其强大的编译期静态类型系统（Algebraic Data Types）消灭不合法状态。然而，在当前的桥接层（Mux、Omp、Opencode 目录）和外壳层（Shell 目录）中，存在大量的动态类型逃逸。
1. **统一承载类型的退化**：在 `Kernel/Messaging.fs` 中定义的 `Part<'raw>` 和 `Message<'raw>` 本意是用泛型保持内核的纯净。但在实际运行中，大部分桥接层直接将 `'raw` 绑定为 `obj`。
2. **动态字典访问（`Dyn.get` / `?` 运算符）扩散**：`Shell/Dyn.fs` 提供的动态操作沦为逃避类型检查的“银弹”。多处业务逻辑（如 `Opencode/HookSchemaDecode.fs` 中的 Zod Schema 修改、`Mux/AiSettings.fs` 中的前端 frontmatter 解析）深度依赖 `?` 运算符和 `unbox`，导致拼写错误和类型不匹配的风险被推迟到运行时。
3. **缺乏契约约束**：工具参数在进入执行管道前没有在边界处完成强类型转换，而是携带着原始的 `obj` 在各个组件之间流转（如 `Mux/SubagentTools.fs` 和 `Opencode/SubagentTools.fs` 里的 `args`）。

### 缺陷二：并发状态一致性漏洞与内存/磁盘状态双写失调
系统采用追加式 NDJSON 日志（`.wanxiangshu.ndjson`）作为唯一的持久化事实来源（SSOT），这符合事件溯源（Event Sourcing）的原则。然而，内存中的状态投影存在严重的并发读写和一致性漏洞：
1. **内存与磁盘的双写无事务保护**：在 `Omp/TodoHooks.fs` 和 `Mux/Wrappers.fs` 中，执行 `todowrite` 时会同时更新内存投影（如 `ProjectionStore`、`ReviewStore`）并异步调用 `appendWorkBacklogCommitted` 写入磁盘。如果磁盘写入失败或在两步之间系统发生异常崩溃，内存状态与物理日志就会产生永久性分歧。
2. **缺乏单向数据流控制**：内存中的 `ReviewStore`（`Shell/ReviewRuntime.fs`）维护着一个独立于日志的 mutable `state` 变量。它在运行时通过 `syncReviewFromEventLog` 被动地从磁盘拉取，同时又通过内存中的 `resolvePendingReview` 主动更新。这种多头写状态极易引发脑裂（Split-Brain）现象。
3. **状态更新缺乏细粒度并发锁**：`ReviewStore` 的 `state` 更新虽然使用了 `state <- ...` 进行原子替换，但这仅能保证引用替换的原子性，无法在异步编排流（例如多级 Reviewer 递归调用）中提供多操作的事务隔离。

### 缺陷三：三端桥接层（Mux, Omp, Opencode）逻辑同质化与高度冗余
该系统同时适配了 Mux、Omp 和 Opencode 三个宿主环境，但它们的内部桥接实现包含大量重复的非纯逻辑：
1. **搜索编排代码冗余**：Fuzzy Find 和 Fuzzy Grep 的核心控制流（如分页迭代器的消费、外部基准路径的解析等）在 `Mux/BuiltinToolsFuzzy.fs`、`Omp/FuzzyTools.fs` 和 `Opencode/SearchTools.fs` 中各复制了一次，结构高度重合，仅仅是承载的输入输出 DTO 格式有细微的宿主差异。
2. **子智能体（Subagent）分发硬编码**：`coder`、`investigator`、`meditator` 和 `browser` 的生命周期管理（启动、轮询、异常捕获、结果整合）分布在三端各自的 `SubagentTools` 模块中，缺乏统一的控制流抽象。
3. **TDD 与 Warn 约束逻辑碎片化**：安全约束校验（验证 `warn_tdd` 和 `warn` 标识）散落在各处的 Execute 拦截器中（例如 `Mux/PluginCatalog.fs` 和 `Omp/HookExecute.fs`）。

### 缺陷四：脆弱的错误传播链路与死锁隐患
1. **无感知的异常吞没**：`Shell/PromiseQueue.fs` 中的 `SerialQueue` 为了防止单次任务失败导致整个链条瘫痪，在 `tail` 链的拼接中使用了 `Promise.catch (fun _ -> ())`。这虽然维持了队列的可运行性，但会导致发生不可恢复的系统恐慌（如物理磁盘满、进程权限不足）时，错误信息被无声吞没，后续任务仍执着运行在错误的基础上。
2. **非类型化的异常分类**：在 `Kernel/FallbackKernel/Decision.fs` 中，fallback 状态机通过比对 `ErrorInput.ErrorName` 是否等于 `"AbortError"` 或 `"MessageAbortedError"` 这种魔法字符串来做出决策。任何网络库或者宿主平台引发的非标命名异常都无法被正确归类，容易误触发高优先级的 Fallback 降级。
3. **长生命周期的异步阻塞**：`Omp/ExecutorTools.fs` 内部通过 `Promise.race` 控制执行时限，但在宿主环境中，若 `AbortSignal` 触发，后台物理进程的 PID 并没有可靠地被强行杀死（例如孤儿进程残留），仅仅在逻辑上中止了 Promise，最终会缓慢耗尽服务器线程池。

---

# 第二部分：保姆式架构重构方案

本重构方案遵循“类型守护边界、纯函数收敛判断、日志驱动局面、适配器解耦宿主”的核心原则。以下为您梳理的五大重构步骤：

## 步骤 1：构建边界类型防御区，彻底根除 `obj` 的生命周期

为了保证内核（Kernel）不触碰任何宿主特有的 JS 原生对象，必须在外壳与内核之间建立一道双向数据类型转换网。

```
                       [ 宿主外壳边界 ] (Mux / Omp / Opencode)
                              │
                              │ (只允许在入口进行 DTO 转换)
                              ▼
                       [ 宿主 DTO 契约类型 ] (例如 MuxMessage, OmpEvent)
                              │
                              │ (利用 Fable 解构并填充入强类型 DU)
                              ▼
                       [ 内核纯静态类型 ] (Messaging, Context, Error)
```

### 1.1 定义标准 DTO 转换模型
* 在 `Shell` 目录下，新建 `Shell.Contracts.fs` 文件。
* 严禁任何组件传递未声明属性的 F# 匿名对象（如 `box {| ... |}`）。
* 针对工具调用输入、宿主上下文、消息记录和事件通知，全部声明对应的具体 F# 记录类型（Record）或判别联合（Discriminated Union）。

### 1.2 升级内核消息定义
* 废除 `Part<'raw>` 中的 `'raw` 泛型，将其替换为严谨的内部代数数据类型。
* 重新设计 `Part` 联合：
  * `Text` 携带纯文本。
  * `Reasoning` 携带辅助推理链。
  * `ToolCall` 携带工具名、唯一调用 ID 及其解析后的参数数据。
  * `ToolResult` 携带完成状态、输出内容、强类型异常数据。
  * `Opaque` 仅作为逃逸阀保留，必须携带宿主名称与一段可以安全序列化的文本。

### 1.3 集中式 Schema 构建器
* 将 `Opencode/HookSchema.fs`、`Omp/OmpToolSchema.fs` 和 `Shell/WorkBacklogSchema.fs` 中的结构定义统一抽取至 `Shell/Schema/ToolSchemaRegistry.fs`。
* 不再在代码中手工构造 JSON 树，而是通过 F# 类型元数据，利用反射或 Fable 统一生成合规的 Zod/Typebox/JSON Schema 树。

---

## 步骤 2：建立无状态的“事件溯源”局面投影引擎

确保内存中绝对不存在可以独立偏离 `.wanxiangshu.ndjson` 日志的 mutable 变量。内存投影只是一个纯粹的、可重构的 Fold 函数。

```
            [ 物理 NDJSON 日志 ] <─────── 追加写入 (仅此一条写通路)
                   │
                   │ 读取完整行 (或从 Checkpoint 恢复)
                   ▼
         [ 纯 fold 函数群 ] (Types, Backlog, Review)
                   │
                   ▼
         [ 内存临时局投影 ] (只读，即算即用，无常驻 mutable 状态)
```

### 2.1 统一的事件日志写通道
* 将 `Shell/EventLogFiles.fs` 提升为系统唯一合法的状态改变入口。
* 设计统一的写通道 `EventLogAppender`：所有业务写操作（如更新 Plan、触发激活、标记 Revision、记录 WIP）一律组织成纯数据载荷，追加到 NDJSON 日志中。
* 物理磁盘写入操作被包装在一个严格的串行 Promise 管道中。一旦磁盘报错，必须立刻中止当前 Turn 的处理，并向客户端抛出强类型物理写异常，绝不在内存中残留不合法的虚假进度。

### 2.2 内存状态投影全面函数化
* 废除 `ReviewStore` 的 mutable `state` 和 `SessionProjectionStore` 的 mutable 缓存表。
* 任何时候需要查询 Session 的 Review 状态或 Backlog 列表时，必须通过 `EventLogStore` 从磁盘或高速文件缓存中顺序读入事件流，调用对应的 pure fold 函数（如 `foldReviewTask`、`foldWorkBacklogSnapshot`）在内存中即时折叠计算出只读投影。
* 对于频繁查询的场景，允许在 `RuntimeScope` 中设立只读的弱引用快照缓存（WeakReference Cache），但该快照必须严格绑定事件的总数与哈希指纹（Fingerprint）。一旦发现当前物理日志文件的大小或哈希值发生改变，快照缓存自动失效并重新执行 Fold。

---

## 步骤 3：建立通用宿主适配器，实现三端控制流大一统

将宿主的特定行为建模为多态的行为（Behavior），内核通过抽象契约与宿主通信。

```
     [ 核心引擎 (Kernel) ]
             │
             │ 通过抽象契约调用 IHostAdapter
             ▼
     ┌───────────────────────┬───────────────────────┐
     │                       │                       │
     ▼                       ▼                       ▼
 [ MuxAdapter ]          [ OmpAdapter ]        [ OpencodeAdapter ]
```

### 3.1 定义宿主多态接口
* 在 `Kernel` 目录下创建 `Kernel.HostAdapter.fs`。
* 声明 `IHostAdapter` 接口，包含以下方法：
  * `GetSessionId`：从宿主上下文对象中提取规范的 Session 标识。
  * `GetCwd`：从宿主参数中提取工作空间绝对物理路径。
  * `ResolveAgentRole`：推导当前 Turn 背后真正的业务智能体身份。
  * `SpawnSubagent`：在当前宿主体内拉起并运行指定的子智能体。
  * `SendNudgePrompt`：向宿主消息总线投递提示词以驱动下一步思考。

### 3.2 抽象多任务分发管道
* 合并三端 `SubagentTools` 中的执行流。
* 设计一个纯内核服务 `SubagentDispatcher`，接受一个 `IHostAdapter` 实例作为核心依赖。
* 无论是 `coder`、`investigator` 还是 `meditator`，其输入参数的强类型解析、子会话生命周期的开启、多路并行调用的并发合并以及生成报告的格式化汇总，统一由 `SubagentDispatcher` 在内核中闭环执行。各宿主适配器只负责实现轻量级的会话派生及数据传输逻辑。

---

## 步骤 4：健全异常层级，加固 Promise 并发调度铁轨

消除魔法字符串匹配和无声异常黑洞，构建类型安全的 Promise 控制轨道。

```
                      [ 异步边界操作 (Promise) ]
                                  │
                                  ├─ 发生 JS 运行时异常/异常抛出
                                  ▼
                     [ ErrorClassify 严苛解构器 ]
                                  │
                                  ├─ 过滤掉真正的 Abort 信号
                                  ▼
                     [ 强类型 DomainError 分支 ]
                                  │
                        ┌─────────┴─────────┐
                        ▼                   ▼
                 [ 容错重试/降级轨 ]      [ 物理回滚轨 ]
```

### 4.1 引入强类型系统异常
* 在 `Kernel/Domain.fs` 中，极大丰富 `DomainError` 的联合分支。
* 严禁将底层原始的 JS Exception 对象传递给内核。通过 `Shell/ErrorClassify.fs` 进行严苛的模式匹配转换，将其收敛为明确的分类：
  * `FileSystemFault` 携带操作系统路径、错误代码与描述。
  * `NetworkTransportFailure` 携带请求目标、HTTP 状态码及网关原始响应。
  * `ClientCancellation` 专门承载由于 `AbortSignal` 触发的合法退出。
  * `HostProtocolMismatch` 用于校验宿主入参字段缺失或格式变异。

### 4.2 修复 Promise 队列的黑洞问题
* 在 `Shell/PromiseQueue.fs` 中，重新梳理 `SerialQueue` 的异常扩散策略。
* 队列不应无条件丢弃前序任务的异常。当任务报错时，应通过专设的 `IExceptionObserver` 将其上报给系统的监控侧，同时阻止无意义的链式操作继续下发，直到外部发出显式的 Reset 命令。

### 4.3 进程生命周期的物理闭环
* 改进 `Shell/ExecutorSpawn.fs`。当 `AbortSignal` 唤醒时，除了在逻辑上触发 Promise 拒绝，还必须确保调用 `killTree` 完成以下动作：
  * 在 Windows 下，执行带 `/T`（Tree）参数的 `taskkill`。
  * 在 POSIX 下，通过负 PID 向整个子进程组发送 `SIGKILL` 信号。
  * 确保所有与该执行关联的物理管道（PIPE）描述符均已被明确关闭，防止出现后台僵尸进程耗尽系统描述符资源。

---

## 步骤 5：对巨型治理模块 `SessionLifecycleObserver` 动骨重构

`SessionLifecycleObserver` 在原本的实现中承担了太多的职能。需要对其进行正交切片，将其拆解为若干职责单一的观察者。

```
                         [ 宿主事件流 / 周期钩子 ]
                                    │
       ┌────────────────────────────┼────────────────────────────┐
       ▼                            ▼                            ▼
 [ 进度监控哨 ]                [ 降级策略仪 ]                [ 唤醒决策器 ]
(ProgressObserver)         (FallbackCoordinator)          (NudgeTrigger)
  负责同步 backlogs            负责监测和驱动 Fallback         负责在 Turn 结束时
  并产生 work_backlog         模型链切换                     依据 FSM 投递 nudge
```

### 5.1 职责解耦与类拆分
* 将该文件解构，分解为以下三个独立组件：
  1. **`ProgressObserver`**：仅专注于同步 todos 列表。在 Turn 完成后拉取 `todowrite` 的历史载荷，核对 backlog 缓存，并按需驱动 `BacklogProjection`。
  2. **`FallbackCoordinator`**：仅负责监听 `session.error` 事件。通过 `FallbackHooks` 对接 `FallbackKernel` 状态机，根据模型列表引导宿主发出带有特定模型参数的 continue 提示。
  3. **`NudgeTrigger`**：仅负责在 `session.idle`（物理 Turn 结束）时，审查会话中的待办状况和 Loop 状态，调用 `tryClaimNudgeDispatch` 并决定是否向宿主发出提示，督促其更新进度或提请 review。

### 5.2 契约式数据通信
* 这三个组件之间不应直接共享内存状态，而应当通过发布-订阅模式（Publish-Subscribe）或共享同一个基于文件追加的安全管道。
* 宿主事件到达时，由系统核心调度中心依次分发给这三个组件，每一个组件的计算过程都应当是局部纯净、易于测试的。

---

# 第三部分：重构落地里程碑及迁移路线图

为确保在庞大的重构过程中，系统始终保持逻辑正确与运行稳定，建议采取**渐进式模式切换**，切忌“毕其功于一役”式的大规模删除和重写。

```
 ┌────────────────────────────────────────────────────────┐
 │ 阶段一：强类型改造 (Contracts & DTO)                    │
 └───────────────────────────┬────────────────────────────┘
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │ 阶段二：宿主多态收拢 (IHostAdapter)                    │
 └───────────────────────────┬────────────────────────────┘
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │ 阶段三：全面无状态化 (Event Sourcing Projection)       │
 └────────────────────────────────────────────────────────┘
```

### 阶段一：建立静态契约与边界防御（不影响现有运行时逻辑）
1. 在 `Shell` 中建立 `Shell.Contracts`，定义所有的宿主 DTO 输入输出。
2. 改造各宿主 wrapper 文件的入口，将获取的 `obj` 在最外侧直接解析为 strong-typed records。若解析失败，在第一公里即刻返回 `wireDecodeFailure`。
3. 检查内核所有调用 `Dyn.get` 处，将其替换为显式的 F# Record 属性读取。

### 阶段二：重构多态宿主适配器与子智能体管道
1. 定义 `IHostAdapter` 并在三端桥接目录中各自实现一个轻量的 concrete class。
2. 将 `coder`、`investigator`、`meditator`、`browser` 等工具的执行流抽调至 `Kernel`，统一接收 `IHostAdapter`。
3. 移除各宿主内部重复拷贝的 Fuzzy Search、TDD 校验、Warn 验证等控制逻辑，改由内核集中代理。

### 阶段三：全面推行“无状态/事件溯源”投影模型
1. 废除 `ReviewStore` 的 mutable `state` 更新。
2. 重塑 `foldReviewTask`。在查询 Review 状况时，通过 `EventLogStore` 从物理日志 NDJSON 逐行重放并折叠得出最新状态，消灭在内存中多头维护状态的漏洞。
3. 给 `EventLogStore` 引入基于哈希校验和物理大小审查的快照缓存机制，加速频繁的文件重放过程。

### 阶段四：异常治理与并发加固
1. 全面重塑 `DomainError` 判别联合。
2. 升级 `ErrorClassify` 解构器，确保不论是 Mux 的 exception 还是 Omp 的 error，都能映射到具体的 `DomainError`。
3. 改造 `SerialQueue`，为其增加错误传递通道，并在物理子进程终止的拦截中，实现精准且无残留的进程树物理抹除（Process Tree Wipe）。
