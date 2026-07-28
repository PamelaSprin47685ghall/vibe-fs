# Wanxiangshu.Next 语义内聚重构清单

## 实施进度（自动更新）

### 2026-07-29 当前状态

- **P1 Correctness 已完成**：
  - Fallback 统一为单一 cursor：`next/Domain/AgentPairCursor.fs` 拥有 `ModelSide`、`FallbackCursor`、`advance`、`effectiveAgent`、`failureIdentity`；删除 `Session/Fallback.fs` 与 `OpenCode/EffectiveAgentResolver.fs`。
  - Prompt Authority 收敛为四模块：`next/Domain/PromptAuthority.fs`（类型与纯规则）、`next/Domain/PromptAuthorityRun.fs`（claim/run/projection 纯操作）、`next/Journal/PromptAuthorityLedger.fs`（durable fold）、`next/OpenCode/PromptIngress.fs`（chat.message 分类）、`next/OpenCode/PromptDispatcher.fs`（runtime/claim/accept）+ `next/OpenCode/PromptDispatcherSend.fs`（claim→send 扩展）。
  - Host Event/Reconcile 三层拆分：`HostEventCodec.fs` 唯一 raw `session.status` 解码点，`TurnBinding.fs` 管理 root/physical/continuation 绑定，`TurnReconcile.fs` 纯 snapshot 分类，`ReconcileSupervisor.fs` 每 session single-flight（dirty latch + ≤3 causal yields），`TurnCompletionProgram.fs` 连续可读 terminal 主程序。
- **修复与门禁**：
  - `PromptDispatcher.fs` 从 395 行拆至 267 行，`Domain/PromptAuthority.fs` 从 345 行拆至 231 行，重新满足 300 行硬门禁。
  - `HostEventCodec.fs` 加入 `sessionStatusAllowlist`，SSE 门禁通过。
  - `ProcessRunner` 取消分类改为 token-based，`Runner_launcher_cancellation` 通过。
  - `DurableFallbackTests` 改为显式 `ProviderAttempt`，不再共享可变计数器。
  - `PromptDispatcher.SendContinuation` 不再在 send-admission 时接受 synthetic id；claim 保持 pending，等待 `chat.message` 接受真实 HostMessageId；`TurnReconcile` 将 admission root/continuation 重新绑定到 SDK snapshot 中的真实 physical user message。
  - Host 停止原生 retry 时，`ProviderErrorFallback` 只在后续 settled-idle 因果边界发送 `ProviderRetryAttempt`；无 debounce/timeout。Cursor 仍仅经 `RetrySignalHandler` 写入，真实 canary 已证明同一 Logical Run 的 `A→A→B→B` provider request 轨迹且无第五次请求。
  - Process 横切文件已收敛为 `ProcessRequest`、`ProcessOutput`、`NodeProcessHost`、`ProcessRunner` 四个纵向职责；删除旧 `ProcessTypes/Command/ProcessBudget/Pump/RunnerCore/RunnerPrimitives`，`Runner.fs` 仅保留兼容 facade。
- **验证（当前通过）**：
  - `dotnet build next/Wanxiangshu.Next.fsproj`
  - `dotnet build tests-next/Wanxiangshu.Next.Tests.fsproj`
  - `npm run build`（Fable 150 源文件）
  - `npm run test:compile`（70 测试源文件）
  - `npm run test:next`：269 passed, 0 failed
  - `npm run test:manager-tools`：1 passed, 0 failed
  - `node testkit/opencode/tests/gate-testkit.mjs`：29 passed, 0 failed
  - `npm run test:e2e:p0:three`：18 个 canary × 3 轮全部通过
  - `npm run test:release`：完整 release gate 通过
- **下一步**：P2 ChildRun/Review、P3 Companion、P4 Process/PTY、P5 Tools/Orchestrator/Journal、P6 清理与文档。

## 一、重构目标

本轮重构不是简单减少文件数量，也不是把若干小文件重新拼成接近 300 行的大文件。目标是：

1. 保留 **每个 F# 文件不超过 300 行**的硬门禁。
2. 让文件边界与领域不变量、资源生命周期和外部协议边界一致。
3. 让核心业务流程重新表现为可顺序阅读的结构化程序。
4. 删除为迁就行数而产生的 `Helpers/Core/Primitives/Fields/Emit/Service` 式横向分层。
5. 消除重复事实来源、镜像状态和模块间隐式协议。
6. 让已经存在的 `agent / process / review / orchestrator / companion` DSL 真正进入生产主流程，而不是只存在于内核与测试中。
7. 保持已经冻结的 Authority、Fallback、Companion、Review、fork/join 等产品语义不变。

冻结设计明确要求源码使用 `let! / do! / use! / while / 尾递归` 直接表达过程，只持久化真实资源、少量缓存和外部事实，不持久化程序执行到了哪一步。当前 300 行门禁本身也明确禁止通过删除空行或压缩语句规避限制。

---

## 二、总体诊断

### 2.1 300 行门禁被执行成了“横向切片门禁”

附件快照中，生产目录包含大量以下形状：

* `Runner.fs / RunnerCore.fs / RunnerPrimitives.fs`
* `ToolSurface.fs / ToolSurfaceFields.fs / ToolSurfaceEmit.fs / ToolSurfaceFork.fs`
* `HostForkRuntime.fs / HostForkRuntimeFork.fs / HostForkRunLifecycle.fs / HostForkChildDispatch.fs`
* `PromptAuthority.fs / PromptAuthoritySend.fs / PromptAuthorityAccept.fs / PromptAuthorityService.fs`
* `AgentFacts.Types.fs / AgentFacts.FoldHelpers.fs / AgentFacts.Authority.fs / AgentFacts.Fallback.fs`
* `Orchestrator.GitPort.fs / Orchestrator.GitPortHelpers.fs / Orchestrator.GitPortWorktree.fs`

这些文件常按“类型、辅助函数、核心函数、发送函数、接收函数”拆分，而不是按完整语义拆分。结果是单个流程虽然没有任何文件超过 300 行，但必须同时打开五至九个文件才能理解。

仓库项目文件也直接反映了这些机械分组已经成为主要编译结构。

### 2.2 DSL 已存在，但生产流程没有真正使用

`Kernel/Flow.fs` 已经定义统一的闭包式 Flow、领域 Builder、`Using`、并行和结构化组合语义；宝典也要求 Reviewer 确认、Fallback、Orchestrator 等用局部递归和资源作用域表达。

但在当前附件快照中，没有发现生产主流程实际使用：

* `agent { ... }`
* `process { ... }`
* `review { ... }`
* `orchestrator { ... }`
* `companion { ... }`
* `Flow.run`

它们主要停留在内核、示例和测试层。生产实现仍以大型 `task {}`、可变字典、完成源和跨文件扩展方法组织。

这比“缺少 DSL”更危险：它形成了**架构表演层**——文档和测试证明 DSL 存在，真实业务却继续沿用另一套过程组织方式。

### 2.3 多处不是显式状态机，却形成了分布式隐式状态机

典型表现包括：

* 同一资源的多个布尔标志共同决定生命周期。
* 同一事实同时存在于字典、HashSet、Journal Projection 和闭包状态中。
* 一个动作由多个 Hook、计时器和回调共同推进。
* 通过字符串内容、固定提示词或 key 子串推断状态。
* 多个模块分别计算同一个 Fallback offset、agent side 或 terminal 判定。
* 主流程只负责注册回调，真正的“下一步”散落在事件处理模块中。

这类设计没有正式的 `Stage` 类型，却仍然把程序计数器展开到了业务状态中。

### 2.4 Host 动态对象泄漏过深

`obj`、动态属性访问、`createObj`、`unbox`、`jsNative` 等 Host/Fable 细节进入工具行为、运行时创建和业务分支，导致：

* 输入解析与领域决策混合。
* 每个工具重复处理空值、数字边界和 JSON 输出。
* 领域模块无法通过类型签名表达真实约束。
* 文件因 Glue Code 变长，再被迫机械拆分。

工具执行代码中已经可以看到参数解析、Host context、进程生命周期和结果编码挤在同一个闭包里的情况。

---

## 三、对初稿的必要修正

初稿正确识别了机械拆分、Host 泄漏和中间人层级，但以下建议不应直接采用。

### 3.1 不应全面禁止 `Types.fs`

`Types.fs` 不是天然错误。满足以下条件时可以保留：

* 是稳定的公开协议。
* 被三个及以上语义模块共同依赖。
* 类型自身表达领域约束，而不是为了给主文件腾行数。
* 文件中没有仅供一个相邻实现使用的私有类型。

错误的是“每个功能默认配一个 Types 文件”，而不是 Types 文件本身。

### 3.2 不应把所有 `TaskCompletionSource` 和布尔值都视为状态机

以下属于允许的物理事实：

* 一个真实任务是否仍在运行。
* 一个 completion cell 是否已经被设置。
* 当前是否存在 single-flight 请求。
* 是否收到新的 dirty 信号。
* PTY 是否已退出。
* Fallback offset 当前是多少。

宝典明确允许正在运行的 Task、完成 Channel 和简单 cursor。问题不是出现布尔值，而是：

* 多个标志描述同一个资源。
* 标志之间存在非法组合。
* 标志代表“流程走到了第几步”。
* 同一标志在多个模块分别维护。

因此应消除的是镜像状态和程序计数器，不是底层并发原语。

### 3.3 SessionReconciler 不应改成 MailboxProcessor

初稿提出使用 `MailboxProcessor`，但当前架构门禁明确排斥该实现方向；冻结设计本身也已经给出 dirty latch 加 single-flight reconcile 的语义。

正确方向是：

* 保留局部 dirty/in-flight 物理事实。
* 把绑定、快照分类、terminal 去重和调度拆成不同语义模块。
* 用单个结构化异步函数表达最多三次因果重读。
* 不再引入 Actor、邮箱协议或新的消息状态机。

### 3.4 不应简单把 Runner 三个文件全部合成一个 280 行文件

`RunnerCore` 已经接近门禁，再叠加 Runner、timer、spool 和 Host interop，只会得到一个更难维护的“合法巨石”。

正确拆分应沿生命周期边界：

* 请求与结果协议。
* Host 进程适配。
* 输出收集与 spool。
* Deadline。
* 一次执行的结构化主程序。

### 3.5 不以“文件数减少 35%”作为目标

减少文件数可能是结果，但不是验收标准。更重要的指标是：

* 理解一个主流程需要打开几个文件。
* 一个事实有几个写入口。
* 一个修改会横跨多少模块。
* 是否存在仅做透传的文件。
* 核心流程是否能在一分钟内顺序读懂。

---

## 四、300 行下的“有脑拆分宪法”

### 4.1 文件的合法拆分单位

一个生产文件应至少拥有以下二者之一：

1. **一个完整领域不变量**

   * 例如 Authority Root 的接受规则。
   * Review witness 的有效性。
   * Fallback cursor 的推进规则。
   * Prefix epoch 的替换规则。

2. **一个完整资源生命周期**

   * 例如一次子 Agent Run。
   * 一个 PTY Session。
   * 一个工作树资源。
   * 一次进程执行。
   * 一个 Blogger in-flight job。

仅仅“这些函数都是 helper”“这些都是 emit”“这些是 private types”不构成文件边界。

### 4.2 推荐行数区间

* 常规语义模块：120–240 行。
* 主流程模块：160–260 行。
* Host codec 或协议枚举：可到 280 行，但必须有明确理由。
* 281–300 行：视为需要架构审查的告警区，而不是正常目标。
* 300 行：硬失败。

### 4.3 主流程文件必须连续可读

以下流程各应有一个明确主文件，读者无需跳转即可看到完整顺序：

* 子 Agent fork 到 completion。
* Reviewer 双 PERFECT。
* Companion 投影到 Blogger 到 B 更新。
* Process spawn 到 cleanup。
* Orchestrator worktree 到 FF publish。
* Prompt claim 到 accept/abandon。
* idle 到 reconcile 到 terminal policy。

依赖模块只提供领域动词，不接管流程控制。

### 4.4 垂直切片优先于横向切片

以工具为例，一个工具切片应尽可能同时拥有：

* 强类型输入。
* 输入校验。
* 领域动作调用。
* 强类型输出。
* Host schema 的局部组装。

不能再把所有字段放 `Fields.fs`、所有 JSON 放 `Emit.fs`、所有执行放 `Fork.fs`。

### 4.5 一个事实只能有一个拥有者和一个权威写入口

例如：

* Fallback cursor advance：只由 retry signal path 写。
* 插件 user-shaped prompt：只由 PromptDispatcher 发。
* PTY exit：只由 backend `onExit` 完成。
* Review confirmed：只从两份有效 witness 派生。
* Companion busy：只由 `inFlightTask option` 表示。
* Child completion：只由 completion cell 首次成功写入。

### 4.6 文件名必须表达业务语义

以下后缀不全面禁止，但新增时必须经过架构审查：

* `Helpers`
* `Core`
* `Primitives`
* `Fields`
* `Emit`
* `Service`
* `Manager`
* `Registry`
* `Utils`

优先使用：

* `PromptIngress`
* `ReviewWitness`
* `ChildRun`
* `ProcessOutput`
* `PtySupervisor`
* `HostEventCodec`
* `PrefixEpoch`
* `IntegrationGate`
* `AuthorityLedger`

---

# 五、P0：立即处理的正确性与唯一事实源问题

## 5.1 Fallback 统一为一个 cursor 模块

### 当前问题

Fallback 语义散落在：

* `Session/Fallback.fs`
* `Session/DurableFallback.fs`
* `OpenCode/EffectiveAgentResolver.fs`
* `OpenCode/FallbackDetect.fs`
* `OpenCode/RetrySignalHandler.fs`
* `OpenCode/PluginFallbackRetry.fs`
* Journal projection/fold

不同模块分别拥有 side、advance、effective agent、failure identity 或 durable append 的部分逻辑。

其中 `PluginFallbackRetry` 仍包含 pending timer、debounce 和 terminal failure 后续调度，使“provider retry 是唯一 durable 推进入口”的冻结规则变得难以验证。

### 目标结构

建立唯一的纯领域模块，例如：

* `Domain/AgentPairCursor.fs`

只拥有：

* Offset 合法性。
* A/A/B/B side 映射。
* `(offset + 1) mod 4`。
* Authority profile 到 EffectiveAgent 的解析。
* Fallback attempt identity。

其他模块不得重新实现以上规则。

### 重构任务

1. 删除 Journal、OpenCode 和 Session 中重复的 side/advance 实现。
2. Journal 只保存 cursor 数据，不拥有第二套算法。
3. `RetrySignalHandler` 成为唯一 durable advance 写入口。
4. `PluginFallbackRetry` 不得根据 terminal error 自行推进 cursor。
5. 删除第四次失败死亡、兼容型 `isDead`、无意义 `NextAttempt` 包装。
6. ProviderRetryAttempt continuation 只能延续同一 Logical Run，不得重置 root、repair 或 cursor。
7. 删除 wall-clock debounce 对语义正确性的依赖。
8. 添加 12 次及以上 retry 的单一轨迹测试。

冻结语义明确要求 retry 是唯一 durable 写入口，永久 A/A/B/B 循环且不存在 Dead。

### 出口标准

* 仓库只有一个 `advance offset` 实现。
* 仓库只有一个 `EffectiveAgent` 映射实现。
* 只有 retry signal handler 可写 `FallbackCursorAdvanced`。
* terminal、idle、repair 和 continuation 不推进 cursor。
* 12 次 retry 后仍产生下一次物理请求。

---

## 5.2 Prompt Authority 收敛为四个语义模块

### 当前问题

当前六层结构包含：

* `PromptAuthority`
* `PromptAuthoritySend`
* `PromptAuthorityAccept`
* `PromptAuthorityRestore`
* `PromptAuthorityService`
* `PromptDispatcher`

其中部分模块主要包装参数或转发调用；Service 又维护全局实例、锁、projection 和 journal，导致纯领域规则、持久化和 Host I/O 相互穿透。

### 目标结构

1. `PromptAuthority.fs`

   * 纯值和纯规则。
   * Authority/Attempt profile。
   * Origin、claim、identity。
   * 不接触 JS、Journal writer 或 Host。

2. `PromptAuthorityLedger.fs`

   * 内存 projection。
   * Journal append。
   * restore。
   * 原子更新。

3. `PromptIngress.fs`

   * 处理 Host `chat.message` 的输入接受。
   * HumanRoot、AgentOwnerRoot、Continuation、Unknown 的分类。
   * Unknown fail-closed。

4. `PromptDispatcher.fs`

   * 插件发出 user-shaped message 的唯一出口。
   * claim → Host send → accept/abandon。
   * metadata correlation。
   * `Agent=EffectiveAgent`、`Model=None`。

### 重构任务

1. 合并 `Send/Accept/Service` 中纯透传层。
2. 删除按 session 隐藏的全局 service registry，改由 Plugin RuntimeScope 显式拥有。
3. 将 hashing、动态 metadata 解码移到 Host codec。
4. 所有 Guard、repair、confirmation、busy nudge、fallback continuation 统一经过 Dispatcher。
5. 禁止模块直接调用 `prompt_async`。
6. Authority ledger 不读取 `sessionRoles` 推断权威。
7. `PhysicalUserMessageId` 与 `AuthorityRootUserMessageId` 始终分离。
8. 删除按零宽文本或固定英文提示识别 origin 的路径。

冻结清单要求所有插件 prompt 发送点经过 Dispatcher，并明确禁止 synthetic 改写 Authority。

### 出口标准

* 出站 prompt 只有一个实现入口。
* 入站 root 接受只有一个实现入口。
* Unknown origin 无副作用。
* Authority 纯领域模块不依赖 Fable、Journal writer 或 Host obj。
* 删除 `PromptAuthorityService` 之类的中间人层。

---

## 5.3 Host Signal 与 Reconcile 去除重复协议解释

### 当前问题

Host event 的 unwrap、type 判定和 session ID 提取在 Adapter、Subscribe、Bootstrap 等多个模块重复。

`SessionReconciler` 同时负责：

* active run 绑定。
* continuation message 绑定。
* consumed assistant 去重。
* snapshot 分类。
* Unknown 重读。
* single-flight 调度。

`TerminalPolicies` 又同时处理：

* A 累积。
* Interaction repair。
* Review/Manager Guard。
* Fallback 后续。
* completion。
* session cleanup。

这使“idle 只是信号，完整 snapshot 才是事实”的简单原则被大量控制胶水包围。

### 目标结构

1. `HostEventCodec.fs`

   * 唯一 raw `obj` 解码点。
   * 输出 `SessionIdle / ProviderRetry / SessionDeleted`。

2. `TurnBinding.fs`

   * 管理 root user、physical user、continuation 的绑定。
   * 不读取 Host payload。

3. `TurnReconcile.fs`

   * 纯函数：typed snapshot + binding → `ReconciledTurn option`。
   * Unknown 无副作用。

4. `ReconcileSupervisor.fs`

   * 每 session 一个 single-flight。
   * `inFlight Task option` 与 dirty latch。
   * 最多三次因果 yield 重读。
   * 不使用 MailboxProcessor。

5. `TurnCompletionProgram.fs`

   * 一个连续可读的结构化程序。
   * 顺序执行 terminal 后的领域动作。

### 重构任务

1. 删除重复 raw event 解析。
2. 让业务层不再接收 `obj`。
3. 将 terminal 分类从 supervisor 中移出。
4. 将 continuation/root binding 从 snapshot 分类中移出。
5. 把 terminal policy 拆成窄动词，但保留一个可连续阅读的主程序。
6. 重复 idle 只能消费一次 assistant。
7. 三次 Unknown 后保留 dirty，等待下一粗粒度信号。
8. retry 不进入 reconcile completion 路径。
9. 删除无宿主契约依据的计时器补丁。

### 出口标准

* Raw Host payload 只在 codec 中出现。
* 同一 session 最多一个 snapshot 请求。
* `Dirty/Running` 不再散落；只由 supervisor 聚合拥有。
* Reconcile 是纯分类，Supervisor 是调度，Completion Program 是业务流程。
* 断开所有碎片事件后产品 E2E 仍通过。

---

# 六、P1：让 DSL 真正替代状态机

## 6.1 Flow DSL 必须进入生产，或者删除

### 当前问题

保留一套没有生产调用者的 Flow DSL，会增加概念数量而不降低复杂度。

### 重构任务

已确定唯一执行方向：正式采用 DSL（方向 A），不再保留备选方案。

#### 方向 A：正式采用（唯一方案）

将以下生产主流程逐步改为相应 DSL：

* `ProcessRunner` → `process { ... }`
* Reviewer confirmation → `review { ... }`
* Child fork/run lifecycle → `agent { ... }`
* Companion update → `companion { ... }`
* Orchestrator publish → `orchestrator { ... }`

要求：

* DSL 只做顺序组合、错误短路、资源释放。
* 不自动重试、不写 Journal、不刷新 projection。
* 领域动作通过窄 Script/Port 注入。
* 真实资源使用 `use!`。
* 循环使用局部递归或 `while`。

本方向为唯一实施路径，所有生产代码迁移必须以此为准执行，不再保留或讨论替代方案。

### 出口标准

* 每种 DSL 至少有一个真实生产主流程调用者。
* GuideContract 编译真实生产 program，而非复制示例。
* 主流程不再靠事件注册和外部 flag 拼装。
* 不产生 Flow AST 或解释器。

---

## 6.2 清理 `Agent/Programs.fs` 与 `ProgramCapabilities.fs`

### 当前问题

附件快照中未发现这两个模块被生产代码调用。它们更像测试用 capability factory，而不是“Agent Program”。

### 目标

二选一：

1. 将其改为真实的角色工具权限 SSOT，并接入 Tool Registry。
2. 删除它们，将静态权限表放入明确命名的 `RoleToolset.fs`。

### 出口标准

* 角色能力矩阵只有一个来源。
* Manager/Orchestrator 的极小 DSL 由静态工具表保证。
* 不保留名称宏大但无生产作用的模块。

---

# 七、P1：子 Agent 运行时重构

## 7.1 用 ChildRun 聚合替代 HostFork 扩展文件森林

### 当前问题

`HostForkRuntime` 的完整行为被按方法拆进多个文件。`HostPendingRun` 又通过 Token、Source、Subscription、Ready、Finished 等字段共同描述一个 run。

问题不是 completion cell，而是一个 run 没有单一聚合拥有生命周期。

### 目标结构

1. `ChildRun.fs`

   * 一个物理 child run 的完整聚合。
   * RunIdentity。
   * Authority root。
   * prompt acceptance。
   * terminal subscription。
   * single-assignment completion。
   * cancellation/disposal。

2. `ChildDispatch.fs`

   * New AgentOwnerRoot。
   * BusyAgentNudge。
   * 统一调用 PromptDispatcher。

3. `ChildRecovery.fs`

   * 重启后恢复 linkage 和未消费 completion。
   * 不创建虚假新 run。

4. `ForkRuntime.fs`

   * Manager 所拥有的 handle map。
   * completion mailbox。
   * `fork/join/list/cancel`。
   * 不处理 Host 动态对象。

### 重构任务

1. 删除 `HostForkRuntimeFork` 式按方法扩展文件。
2. 把 `Ready/Finished` 多标志折叠成一个 run 生命周期资源。
3. Prompt 被 Host 接受后再完成 run 注册，不保留半注册状态。
4. Terminal/SendFailure/Cancel 继续竞争同一个 completion cell，首写胜出。
5. Busy nudge 不创建 completion、不替换 active RunId。
6. Idle existing agent 新任务创建新的 AgentOwnerRoot 和 completion。
7. 对内部 Host 工作流提供返回明确 completion task 的启动 API。
8. LLM 可见的 `fork` 仍保持返回 agent ID，不扩大 DSL。
9. `join()` 仍消费任意完成项，并永久删除 handle。

### 出口标准

* 理解一次 child run 只需打开 `ChildRun` 和 `ChildDispatch`。
* 不存在 Ready/Finished 非法组合。
* Orchestrator 不再通过 generic join 加 stash 等待指定 Manager。
* Busy nudge 的同 Run 语义有单一测试和单一实现。

---

# 八、P1：Review 从计数器窗口改为 Witness 模型

## 8.1 当前问题

现有 Review projection 包含：

* `ConsecutivePerfects`
* `IsConfirmed`
* `AcceptedGuardKey`
* 最近 verdict ID
* confirmation message ID
* Git tree hash

部分逻辑还通过 key 内容或固定提示词判断 confirmation。这把“两个外部审查事实”展开成了一组需要同步维护的过程状态。

## 8.2 目标模型

Journal 只保存外部 witness：

* ReviewBarrierId。
* 第一份 PerfectWitness。
* 第二份 ConfirmingPerfectWitness。
* RevisionWitness。
* 每份 witness 的 ToolCallId。
* ProviderRunIdentity。
* Git tree hash。
* Physical confirmation user message ID。
* AuthorityRootUserMessageId。

以下内容全部派生：

* 是否已有第一次 PERFECT。
* 是否已经确认。
* tree 是否仍有效。
* 是否需要 confirmation continuation。

## 8.3 重构任务

1. 删除 `ConsecutivePerfects`。
2. 删除单独持久化的 `IsConfirmed`。
3. 用结构化 `GuardKind/BarrierId` 替代字符串 key 解析。
4. 第一和第二 witness 必须来自不同 ProviderRunIdentity/ToolCallId。
5. 两份 witness 必须绑定相同 Git tree。
6. post-rebase 创建新的 barrier，不能复用旧 witness。
7. Reviewer terminal 无 verdict 时，由局部 review program 发 continuation 并递归等待。
8. REVISE 立即返回 revision，不参与 PERFECT 计数。
9. Manager Guard 和 Reviewer Guard 只延续原 Logical Run。
10. `ReviewerGuardState.fs` 改为 witness projection 的纯查询，或删除。

## 8.4 出口标准

* Review 主流程在一个 `review {}` 函数中连续可读。
* Journal 只记录发生过的 verdict 和 Host acceptance。
* 不存在 ReviewStage、计数窗口或字符串提示识别。
* 一次 PERFECT 永远无法放行。

---

# 九、P2：Companion 消除双重 busy/reset 状态

## 9.1 当前问题

Companion 内部和 CompanionHost 各自维护一套状态，例如：

* `busy`
* `inFlightTask`
* `bloggerTask`
* `bloggerFailed`
* `bloggerNeedsReset`
* `latestB`
* `activePrefixEpoch`
* `replacementActive`

部分字段是真实缓存，部分字段是同一物理事实的镜像。

## 9.2 目标结构

1. `CompanionState.fs`

   * 单一内存聚合。
   * LatestB。
   * FrozenB/ActivePrefixEpoch。
   * last successful projection。
   * blogger linkage。
   * 一个 `inFlightTask option`。

2. `CompanionProjection.fs`

   * canonical provider-visible view。
   * semantic delta。
   * coverage/digest。
   * 不接触 Host session。

3. `PrefixEpoch.fs`

   * FrozenB 与 LatestB。
   * epoch switch 条件。
   * replacement projection。
   * 缓存不变量。

4. `CompanionRun.fs`

   * 一个 Blogger job 的结构化生命周期。
   * 忙时跳过，不排队、不推进 delta baseline。
   * 成功后原子更新 B 和 baseline。

5. `BloggerHost.fs`

   * 创建、恢复和复用 Blogger session。
   * dispatch 和收集完整 assistant。

6. `CompanionTransform.fs`

   * Host projection hook adapter。
   * 不保存业务状态。

## 9.3 重构任务

1. 用 `inFlightTask option` 作为唯一 busy 真相。
2. 删除独立 `busy` 和 `bloggerTask` 镜像。
3. 删除 `bloggerNeedsReset ref`；reset 由 durable linkage 和请求形状派生。
4. Companion eligibility 只读 ActiveLogicalRun。
5. synthetic continuation 不推进 delta baseline。
6. 所有 canonical hash 和 delta 纯函数化。
7. Host `obj` 解码移到 projection codec。
8. FrozenB 只在 epoch 切换时更新。
9. LatestB 增长不得改变 X 的普通回合前缀。
10. 重启时优先复用原 Blogger session。

## 9.4 出口标准

* 一个 session 只有一份 Companion state。
* busy、reset、epoch 不再由多个模块分别维护。
* Prefix cache、replacement、restart 和 skipped-delta E2E 完全保持。
* Companion 核心不依赖 Fable 动态对象。

---

# 十、P2：Process 按生命周期拆分，而非 Core/Primitive 拆分

## 10.1 目标结构

1. `ProcessRequest.fs`

   * Command。
   * Estimate。
   * Outcome。
   * Error。
   * 稳定公共协议。

2. `NodeProcessHost.fs`

   * spawn。
   * stdin/stdout/stderr binding。
   * signal/kill。
   * JS/Fable interop。

3. `ProcessOutput.fs`

   * stdout/stderr 聚合。
   * byte 计数。
   * spool threshold。
   * completed/spooled 结果。

4. `Deadline.fs`

   * 唯一 deadline/timer 实现。
   * segmented timer 也只在此实现。

5. `ProcessRunner.fs`

   * 完整结构化生命周期：

     * 校验预算。
     * 获取 large gate。
     * spawn。
     * 启动 output collection。
     * 等待 exit/deadline/cancel。
     * kill。
     * cleanup。
     * 返回结果。

6. 保留语义内聚的：

   * `Spool.fs`
   * `LargeGate.fs`

## 10.2 重构任务

1. 删除 `RunnerPrimitives.fs`。
2. 删除 `RunnerCore` 与 `Runner` 间透传。
3. 合并重复的 timer/deadline 实现。
4. 检查并删除无生产引用的 `Pump.fs`、`ProcessBudget.fs`，或将其职责并入明确模块。
5. `ProcessRunner` 使用 `process {}` 和资源作用域。
6. Host child/process handle 使用可释放资源封装。
7. output collector 不负责进程调度。
8. executor summary 与 ProcessRunner 解耦。
9. `estimated_running_secs × 3` 仍是唯一运行时限。
10. spool 和 ripple-carry summary 产品语义不变。

## 10.3 出口标准

* Runner 主文件能从上到下看完一次执行。
* JS spawn 细节不进入业务程序。
* 只有一个 deadline 实现。
* 没有 Core/Primitive 透传层。
* cancel、timeout、exit 三条路径全部保证清理。

---

# 十一、P2：PTY 建立单一 Session 聚合

## 11.1 当前问题

PTY 的 active handle、closed ID、read waiter、exit task、backend state、parent aborter 分散在多个模块和全局字典中，形成同一 PTY 的多份真相。

## 11.2 目标结构

1. `PtyProtocol.fs`

   * PtyId。
   * Command。
   * Signal enum。
   * Read/Write/Resize 请求和结果。

2. `NodePtyHost.fs`

   * Bun/node-pty 加载。
   * spawn/write/resize/signal。
   * data/exit callback。

3. `PtySession.fs`

   * 单个 PTY 的 handle。
   * output buffer。
   * pending read。
   * exit completion。
   * close/dispose。
   * 所有状态由该聚合拥有。

4. `PtySupervisor.fs`

   * `PtyId → PtySession`。
   * parent ownership。
   * list。
   * close one/all。
   * parent cancel。

## 11.3 重构任务

1. 合并 `PtyBackendRegistry` 的双层 live/pending map。
2. 删除 `PtyApi` 中独立的全局 parent abort registry。
3. PTY completion 只由 backend `onExit` 写入。
4. Signal/Close 不提前标记完成。
5. pending read 与 output buffer 归属于同一个 PtySession。
6. close 后 read 行为由一个协议定义，不靠 closed ID 辅助表猜测。
7. grace timing 并入明确的 termination policy。
8. PTY 不伪装为 Agent Session。

## 11.4 出口标准

* 查询一个 PtyId 只得到一个聚合。
* 不存在 active、backend、abort 三份镜像 registry。
* parent cancel 的拥有关系可从 Supervisor 直接看出。
* exit completion 只有一个写入口。

---

# 十二、P3：Tool Surface 改为 DSL 动词垂直切片

## 12.1 目标结构

1. `ToolHostCodec.fs`

   * context 解码。
   * typed args 解码。
   * schema 基础构件。
   * result 编码。
   * abort attachment。
   * 唯一 JS 动态边界。

2. `ForkTool.fs`

3. `JoinTool.fs`

4. `ListTool.fs`

5. `PtyTool.fs`

6. `ExecutorTool.fs`

7. `InspectorTool.fs`

8. `CoderTool.fs`

9. `VerdictTool.fs`

每个动词文件拥有自己的强类型请求、校验、领域调用和返回映射。

10. `ToolRegistry.fs`

    * 按 Agent role 组装允许的工具集合。
    * 不包含执行逻辑。

11. `ToolRuntimeScope.fs`

    * per-session ForkRuntime。
    * reviewer host。
    * orchestrator host。
    * executor runtime。
    * 生命周期与 dispose。

## 12.2 重构任务

1. 删除 `ToolSurfaceFields.fs`。
2. 删除 `ToolSurfaceEmit.fs`。
3. 将 `textArg/outputArg/runtimeArg` 等集中到 codec 的 typed decoder。
4. 工具处理器不再接收裸 `obj`。
5. Manager 只注册 fork/join/list。
6. Orchestrator 只注册 fork/join。
7. Blogger/Executor 内部 agent 名称不进入工具 schema。
8. Tool Registry 不创建全局 runtime 字典。
9. `ToolSurface.fs` 缩减为短 assembly module，或更名 `ToolRegistry.fs`。

## 12.3 出口标准

* 修改 fork 参数时主要只需打开 ForkTool。
* JS 动态访问只存在于 ToolHostCodec。
* 角色能力矩阵只有一个 SSOT。
* 不再出现 Fields/Emit 式横向拆分。

---

# 十三、P3：Orchestrator 恢复为一个可读发布程序

## 13.1 当前问题

Orchestrator 的控制流分布在：

* core engine。
* publish stages。
* publish chain。
* git port helpers。
* OpenCode host。
* manager job。
* review read。
* sweep。
* authority。
* session directories。

此外，generic join 与 stash 被用于等待特定 Manager completion，说明内部程序受到了模型可见 DSL 形状的反向限制。

## 13.2 目标结构

1. `OrchestratorProgram.fs`

   * 唯一主程序：

     * 创建 worktree。
     * 启动 Manager。
     * 获取 candidate。
     * pre-rebase review。
     * 获取 integration gate。
     * rebase。
     * conflict continuation 或继续。
     * post-rebase review。
     * FF publish。
     * cleanup。

2. `ManagerJob.fs`

   * 一个 Manager 工作任务聚合。
   * manager child completion。
   * worktree。
   * candidate。
   * review barrier。

3. `WorktreeResource.fs`

   * create/dispose。
   * 保留策略。
   * 使用 `use!`。

4. `GitOperations.fs`

   * typed Git verbs。
   * 无 workflow stage。

5. `IntegrationGate.fs`

   * 串行 publish 的真实资源。
   * 以可释放 lease 表示，不用 mutable task chain 表示阶段。

6. `OrchestratorRecovery.fs`

   * 从 Journal 的外部 Git 事实恢复。

7. `OrchestratorHost.fs`

   * 仅做 OpenCode session 桥接和 runtime scope 接线。

## 13.3 重构任务

1. 将 `PublishStages` 改为领域动作，或并回主程序的局部函数。
2. 删除 `publishChain` 式手工 Task 链。
3. 内部 child 启动返回明确 completion handle，不再 stash generic join 结果。
4. conflict 返回原 Manager 同一 Logical Run。
5. Manager 已 terminal 时用 continuation，不建新 root。
6. worktree cleanup 使用资源作用域。
7. 真实 Git 恢复事实继续持久化：

   * target ref。
   * expected target head。
   * candidate commit。
   * rebased commit。
   * conflict files。
   * publish claim。
8. 删除只表示“下一步做什么”的 stage 字段。
9. post-rebase 必须创建新 review barrier。
10. `GetTargetHead` 失败继续 fail-closed。

## 13.4 出口标准

* 一分钟内能从主程序看见完整发布链。
* 无 Stage/Scheduler/DAG。
* publish 串行性来自 IntegrationGate 资源。
* 重启恢复依据 Git 与 Journal 外部事实，而非程序计数器。
* 只有 FF 成功才完成 published join。

---

# 十四、P3：Journal 按投影限界上下文拆分

## 14.1 当前问题

当前 `AgentFacts.Types.fs` 集中容纳 Authority、Fallback、Review、Companion、Linkage、Orchestrator 等不同领域类型；相应 fold 又拆成多个按技术类别命名的文件。

把所有 session 事件合成一个近 280 行 `SessionFold.fs` 也不是理想方案，它只是另一种巨型聚合。

## 14.2 目标结构

采用垂直投影切片：

* `AuthorityProjection.fs`
* `FallbackProjection.fs`
* `ReviewProjection.fs`
* `CompanionProjection.fs`
* `LinkageProjection.fs`
* `OrchestratorProjection.fs`

每个文件同时拥有：

* 自己的 projection 类型。
* empty/default。
* 对应事实的 fold。
* 派生查询。

另设：

* `AgentProjection.fs`

  * 组合各子 projection。
  * 顶层路由。
* `Journal/Fold.fs`

  * envelope 解码和全局分发。

## 14.3 重构任务

1. 删除 `AgentFacts.FoldHelpers.fs`。
2. 拆除全局 Types 杂物袋。
3. 每种 Fact 明确唯一 fold owner。
4. 共享 ID 放 `Kernel/Identity.fs`。
5. projection 中只存重启后需要恢复的事实。
6. `IsConfirmed`、EffectiveAgent 等可派生值不持久化。
7. fold 必须保持纯函数。
8. durable effect 与 projection fold 分开。
9. 编码兼容仅针对当前 0.5.0 SSOT，不保留旧控制状态兼容。

## 14.4 出口标准

* 新增一个 Review fact 只修改 ReviewProjection 和 codec。
* 一个 fact 不会被多个 fold 模块重复解释。
* Journal 不保存程序下一阶段。
* 删除 FoldHelpers 和通用状态杂物袋。

---

# 十五、P4：Plugin 与运行时拥有关系清理

## 15.1 当前问题

`ToolSurface`、`HostSignalBootstrap`、`SpikePlugin` 等 composition root 内存在多组字典：

* Fork runtimes。
* Executor runtimes。
* Reviewer hosts。
* Orchestrator hosts。
* tree ports。
* session roles。
* parent mappings。
* verdict sets。
* nudge sets。
* fallback sets。

这些集合分散地承担资源拥有、缓存、去重、权威和业务状态。

## 15.2 目标结构

建立显式 `PluginRuntimeScope`：

* SessionRuntime。
* Child/ForkRuntime。
* Companion runtime。
* Reviewer runtime。
* Orchestrator runtime。
* Executor runtime。
* cancellation/disposal。
* Host association cache。

业务 projection 和 durable facts不放入 RuntimeScope。

## 15.3 重构任务

1. Plugin 只负责读取配置、创建 Scope、注册 hooks/tools、dispose。
2. 所有 per-session runtime 由 Scope 创建和释放。
3. HashSet 去重逐项审计：

   * 是持久事实则进入 projection。
   * 是资源状态则进入对应 aggregate。
   * 是临时调用去重则由调用作用域拥有。
4. 删除 feature module 中隐藏的全局 registry。
5. `sessionRoles` 不再充当 Authority 或 Companion eligibility 来源。
6. session deleted 统一调用 Scope cleanup。
7. 避免 Bootstrap 同时组装业务规则和资源。

## 15.4 出口标准

* 从 Scope 可以直接看出谁拥有哪个长期资源。
* Plugin disposal 不需要遍历多个模块的私有全局表。
* 业务状态不再藏在 composition root 的 HashSet 中。

---

# 十六、删除与引用审计候选

以下文件或职责在附件快照中未发现明确生产调用，或与其他模块高度重复，应逐项做引用审计，不应未经验证直接删除：

* `Process/Pump.fs`
* `Process/ProcessBudget.fs`
* `Orchestrator.Engine.fs`
* `Agent/Programs.fs`
* `Agent/ProgramCapabilities.fs`
* `MessageOriginDecoder.fs` 中旧 origin 推断分支
* `FallbackDetect.fs` 中 terminal failure 检测部分
* 重复的 Review Guard 查询层
* `SpikePluginHelpers.fs`
* `TerminalPolicyHelpers.fs`
* `CompanionTransformHelpers.fs`
* `Orchestrator.GitPortHelpers.fs`

审计规则：

1. 无生产调用且仅测试自身：删除或接入真实主流程。
2. 只有一个调用者且必须同时修改：合并回调用者。
3. 只是参数转发：删除。
4. 提供稳定外部协议或独立纯算法：保留并更名。
5. 为旧实现兼容存在：按 0.5.0 不兼容旧控制状态的原则删除。

---

# 十七、需要新增的架构门禁

现有 300 行门禁只控制文件大小，无法阻止机械拆分。必须增加语义门禁。

## 17.1 文件形状门禁

对生产目录新增以下文件名时要求显式 allowlist：

* `*Helpers.fs`
* `*Primitives.fs`
* `*Fields.fs`
* `*Emit.fs`
* `*Service.fs`
* `*Core.fs`

现存文件在本轮重构后逐步清零，确有合理用途的必须在架构测试中写明理由。

## 17.2 Host 边界门禁

以下内容只能出现在明确的 Adapter/Codec 文件：

* `Fable.Core.JsInterop`
* 动态属性访问。
* `createObj`
* `unbox`
* `jsNative`
* Host raw `obj`

Kernel、Domain、Session program、Review、Orchestrator program 不得引用。

## 17.3 单一写入口门禁

静态扫描要求：

* `FallbackCursorAdvanced` 只能由 RetrySignalHandler append。
* 插件 synthetic prompt 只能由 PromptDispatcher 调 Host。
* PTY completion 只能在 backend exit path 设置。
* Review confirmed 不得直接赋值，只能派生。
* model 字段不得由 Wanxiangshu 设置。

## 17.4 DSL 落地门禁

二选一：

* 生产代码必须存在已批准主程序对 `agent/process/review/orchestrator/companion` Builder 的调用。
* 或删除未使用的 DSL。

禁止继续只靠示例测试证明架构存在。

## 17.5 依赖方向门禁

建议固定方向：

* Kernel
* Domain projections and identities
* Structured programs
* Host ports
* Host adapters/codecs
* Plugin composition

下层不得反向引用 Plugin、Fable host 或 Tool Surface。

## 17.6 重复算法门禁

静态检查以下算法只能存在一个实现：

* modulo-4 cursor advance。
* EffectiveAgent side mapping。
* Agent peer mapping。
* canonical provider-visible hash。
* Review witness confirmation。
* Prompt origin priority。

## 17.7 近门禁告警

* 超过 260 行：架构告警，要求记录拆分理由。
* 超过 280 行：PR 阻断，除非文件位于批准的 codec allowlist。
* 超过 300 行：硬阻断。

这能避免所有文件长期贴着 300 行生长。

---

# 十八、实施顺序（并行化重构版）

## Phase 0：建立架构真相（基线冻结，可并行准备）

任务：

* 决定并落实 Flow DSL 的生产使用。
* 增加新架构门禁。
* 建立模块引用图与无调用文件清单。
* 冻结当前 E2E 作为行为基线。

并行子流：

* A：生成依赖图（模块引用 / 反向引用）
* B：扫描 dead code / orphan modules
* C：E2E snapshot freeze（测试与回放基线）

出口：

* 不再新增机械拆分文件。
* DSL 不再是仅测试架构。
* 所有候选删除项有明确结论。

---

## Phase 1：收敛正确性关键路径（可拆三条并行线）

任务：

* Fallback 单一 cursor。
* Prompt Authority 四模块。
* Host Event Codec。
* Reconcile 三层拆分。

并行子流：

* A（Fallback线）

  * 单一 cursor 收敛
  * retry 写入口统一

* B（Authority线）

  * Prompt Authority 四模块拆分
  * Dispatcher 唯一出口

* C（Host/Reconcile线）

  * Host Event Codec
  * Reconcile pipeline 三层拆分

出口：

* retry 唯一写 cursor。
* Dispatcher 唯一发 synthetic prompt。
* raw Host obj 不进入业务层。
* Unknown 无副作用。

---

## Phase 2：重构 ChildRun 与 Review（双轨并行）

任务：

* ChildRun 聚合。
* ForkRuntime completion mailbox。
* 内部明确 completion handle。
* Review Witness 模型。
* 双 PERFECT 局部递归。

并行子流：

* A（ChildRun runtime线）

  * completion mailbox
  * fork/join 生命周期收敛

* B（Review线）

  * witness model
  * PERFECT 双确认递归逻辑

出口：

* 无 Ready/Finished 镜像状态。
* 无 generic join stash。
* 无字符串 guard 推断。
* Busy nudge 保持同 Run。

---

## Phase 3：重构 Companion（状态与投影分离并行）

任务：

* 单一 CompanionState。
* Projection、PrefixEpoch、BloggerHost 分离。
* in-flight 单一真相。

并行子流：

* A（State收敛）

  * busy/reset/inFlight 合并

* B（Projection线）

  * B / delta / epoch 逻辑拆分

* C（BloggerHost线）

  * session复用与恢复逻辑

出口：

* 无双重 busy/reset。
* FrozenB 和 LatestB 边界清楚。
* cache E2E 不退化。

---

## Phase 4：Process 与 PTY（系统资源并行重构）

任务：

* Process lifecycle 垂直拆分。
* 单一 Deadline。
* PtySession / PtySupervisor。
* JS host 隔离。

并行子流：

* A（Process线）

  * runner / output / deadline 拆分

* B（PTY线）

  * session 聚合
  * supervisor ownership

* C（Host隔离线）

  * JS interop 边界收敛

出口：

* 一个进程主程序。
* 一个 PTY 聚合。
* 一个 completion 写入口。
* parent cancellation 资源化。

---

## Phase 5：Tools、Orchestrator、Journal（垂直切片 + 流程重建并行）

任务：

* 工具按 DSL 动词垂直切片。
* OrchestratorProgram。
* IntegrationGate。
* Journal bounded projections。

并行子流：

* A（Tools线）

  * Fork/Join/List/PTY/Executor 切片

* B（Orchestrator线）

  * publish pipeline 重写为单程序

* C（Journal线）

  * projection bounded context 拆分

出口：

* Manager/Orchestrator 工具面静态可见。
* 发布链一分钟可读。
* 无 FoldHelpers、ToolSurfaceFields、PublishStages 式机械层。

---

## Phase 6：删除兼容与死代码（全局收敛阶段，可分批并行清理）

任务：

* 删除所有已替代路径。
* 删除无生产引用的架构样板。
* 更新 fsproj。
* 更新文档与迁移说明。

并行子流：

* A（dead code removal）
* B（fsproj / build cleanup）
* C（文档与迁移说明）

出口：

* 一个行为只有一个实现。
* 一个事实只有一个拥有者。
* 一个主流程只有一个顺序程序。
* 所有生产文件低于 300 行。

---

# 十九、每个重构 PR 的验收模板

每个 PR 必须回答：

1. 本 PR 删除了哪个重复概念或事实来源？
2. 新文件拥有哪个完整不变量或生命周期？
3. 为什么该边界不会要求调用者同时打开另一个文件？
4. 是否引入了新的 mutable flag、HashSet 或 registry？
5. 这些状态是物理事实还是程序计数器？
6. 主流程是否比之前更连续可读？
7. 是否减少了 Host `obj` 向领域层的泄漏？
8. 是否保持所有冻结语义？
9. 是否删除了被替代的旧路径？
10. 文件虽然低于 300 行，是否只是换了一种机械拆分？

无法清楚回答第 2、3、5、9 项的 PR，不应合并。

---

# 二十、最终验收标准

重构完成后，打开生产主流程，读者应能在一分钟内看清：

* Manager 如何 fork、join 和 nudge。
* Child run 如何从 prompt acceptance 走到唯一 completion。
* Reviewer 如何得到两份绑定同一 Git tree 的 PERFECT witness。
* Fallback 如何仅由 retry 推进 A/A/B/B cursor。
* Companion 如何从投影产生 delta、运行 Blogger 并更新 B。
* Process 如何 spawn、等待、kill 和 cleanup。
* PTY 谁拥有 handle、buffer 和 exit completion。
* Orchestrator 如何从 worktree 到 rebase、复审和 FF publish。
* Prompt Authority 如何 claim、发送、accept 或 abandon。

同时必须满足：

* 无 Stage/Phase/Lease/Owner/Generation 式程序计数器。
* 无按文本猜测 Authority、Guard 或 continuation。
* 无重复 Fallback 算法。
* 无多个模块共同维护同一资源的 busy/finished/reset 状态。
* 无业务层 Host 动态对象。
* 无仅为转发而存在的 Service/Core/Helpers。
* Flow DSL 必须真实运行于生产，并作为唯一主流程编排机制，不得被绕过或降级为示例或测试专用。
* 300 行门禁继续保持，且多数核心文件稳定在 120–260 行区间。
* 文件总数是否下降不是首要指标；跨文件阅读路径、重复事实源和隐式协议必须显著下降。

这才是“保持 300 行门禁，但拆分要动脑筋”的最终含义。
