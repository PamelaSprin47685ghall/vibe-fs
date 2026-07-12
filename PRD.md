# 总体结论

这 7 个问题表面上分散在 fallback、tool hook、context budget、nudge、review、logging 六个区域，实际上共同根因只有四个：

1. **系统没有可靠区分“真人消息”和“系统合成消息”**，而是依赖零宽字符、文本、时间戳、最近一条消息等启发式判断。
2. **会话没有明确的“当前轮次、取消代次、自动续跑代次、事件所有者”**，所以 Esc、fallback、compaction、nudge 会抢着控制同一个 session。
3. **model、agent、original task 等上下文没有绑定到具体的人类轮次**，而是从“最近看到的某条消息”临时反推，必然读到旧值。
4. **warn、warn_tdd 等控制字段只有分散的 schema 注入和 hook 校验，没有唯一、强制、不可绕过的执行边界。**

当前代码已经有不错的纯状态机、事件日志、fold、nudge snapshot 和 review task 折叠结构，但这些结构还没有形成一个真正的单一事实源。尤其是 fallback 注入虽然已经进入事件日志，消费者仍然大量依赖消息和时间推断。

下面给出建议的完整修复方案。

---

# 一、先建立十条不可破坏的系统不变量

不要直接围绕 7 个 bug 分别打补丁。先把下列规则写进架构设计和测试，否则修完一个还会从另一个入口复发。

## 不变量 1：Esc 是最高优先级、粘性的用户意图

用户按 Esc 后：

* 当前人类轮次立即进入 `Cancelled`。
* 已排队但尚未发送的 fallback、nudge、review continuation 全部失效。
* 已经发送但尚未返回的系统 continuation，其迟到事件不得重新激活会话。
* 只有 Esc 之后一条**明确确认是真人发出的新消息**，才能开启新轮次。

任何零宽消息、nudge、compaction、title、fallback continuation 都不能解除 Cancelled。

## 不变量 2：每条消息必须有来源

消息来源至少要分成：

* Human
* FallbackContinuation
* TodoNudge
* ReviewNudge
* ContextBudgetNudge
* Compaction
* TitleGeneration
* Subagent
* UnknownLegacy

不能再只用 `role=user` 判断“这是用户消息”。

## 不变量 3：每个 session 同一时刻只能有一个 continuation owner

优先级建议固定为：

1. UserAbort
2. FallbackRecovery
3. Compaction
4. ReviewLoop
5. TodoNudge
6. 普通自然结束处理

高优先级 owner 未释放前，低优先级 owner 不得发送 prompt。

## 不变量 4：所有异步行为必须绑定代次

至少需要：

* `humanTurnId`
* `sessionGeneration`
* `cancelGeneration`
* `continuationId`
* `contextGeneration`

迟到回调只要 generation 不匹配，就只能记录，不得改变当前状态。

## 不变量 5：model/agent 属于轮次，不属于 session 全局

模型选择必须绑定到：

* 当前真人消息；
* 或当前 fallback attempt。

不能用“session 最近注入过的模型”覆盖未来所有轮次。

## 不变量 6：控制字段只能在唯一执行入口处理

`warn`、`warn_tdd`、`warn_reuse`、`amend` 必须经历：

1. schema 中强制声明；
2. 执行前验证；
3. 提取到控制信封；
4. 从下游参数中删除；
5. 下游只能拿到已经净化的新对象。

任何 tool wrapper、host adapter、plugin ordering 都不能绕过这个入口。

## 不变量 7：context budget 不允许静默失效

即使拿不到 provider limit 或精确 tokenizer，也必须进入明确的降级模式，而不是让 `MaxInputTokens=0` 后什么都不做。

## 不变量 8：review loop 活跃时，task 不能为空

只要系统判定 review loop 是 Active，生成任何 review continuation 时都必须携带完整原始任务。

缺失任务属于状态损坏，应当阻止 nudge，而不是发送一条失忆的 prompt。

## 不变量 9：compaction/fallback 结束不等于人类轮次自然结束

只有真正的人类轮次完成事件，才有资格触发 todo/review nudge。

## 不变量 10：默认运行不得污染 stdout

正常 capability 缺失、降级、重试、缓存未命中都不应直接打印 `DEBUG:`。

---

# 二、问题 1：Esc 被 fallback auto-continue 干扰

## 2.1 当前根因

当前 Opencode fallback 使用一个零宽空格作为 `SendContinue` prompt。这样 UI 看不见，但 host 仍把它登记为一条 `role=user` 消息。系统再通过注入时间判断这条消息是不是 fallback 自己发出的。仓库中已经明确存在 `zwsChar`、注入时间记录以及 `IsInjectedSince` 这一套路径。

问题在于：

* 零宽字符只是内容，不是身份。
* 时间戳可能缺失、为 0、单位不同、事件顺序倒置。
* fallback 注入和用户 Esc 是两个异步事件，可能交错。
* 状态机的 `NewUserMessage` 会无条件把 lifecycle 恢复为 Active。
* 因而一条迟到的零宽 user message 可能被误判为真人消息，解除 Cancelled。
* 当前注入记录更像“最近注入过什么”，没有精确绑定到 message ID、continuation ID 和 human turn。
* 注入事实在真正发送成功前后没有完整区分，请求、发送、观察、失败、取消被混成一个事实。

这就是用户明明按了 Esc，系统却像收到“新用户消息”一样继续工作的核心原因。

## 2.2 正确的状态模型

为每次 fallback continuation 建立完整生命周期：

* Requested
* Dispatching
* Dispatched
* ObservedByHost
* BusyObserved
* Settled
* Cancelled
* Failed

每个 continuation 带有：

* session ID
* human turn ID
* continuation ID
* 创建时的 cancel generation
* 选中的 model/agent
* provenance = FallbackContinuation

## 2.3 Esc 的处理顺序

Esc 到达时必须在同一 session 串行队列中原子完成：

1. 增加 `cancelGeneration`。
2. 把当前 human turn 标记为 Cancelled。
3. 标记所有旧 generation 的 continuation 为 invalidated。
4. 清除 AwaitingBusy、pending auto-continue、pending nudge。
5. 能调用 host abort API 时，主动中止在途 prompt。
6. 记录持久事件，例如 `user_abort_observed`。
7. 后续所有迟到事件先核对 generation，再决定是否处理。

不要仅仅把 fallback state 的 Lifecycle 设为 Cancelled。必须让所有异步回调都能看见“自己已经过期”。

## 2.4 真人新消息如何恢复

`NewUserMessage` 不应再是无参数事件，而应携带来源和消息身份。

只有同时满足以下条件才能开启新轮次：

* 来源为 Human；
* message ID 尚未处理；
* 消息不是 synthetic；
* 创建时间或 host 顺序位于最后一次 Esc 之后；
* 不是旧 continuation 的迟到映射；
* 不是 compaction/title/nudge。

然后：

* 创建新的 human turn ID；
* 重置 fallback attempt；
* 清除旧 injected model；
* 清除旧 cancellation barrier；
* 保存这条真人消息携带的 model、agent、variant。

## 2.5 零宽字符怎么处理

最佳方案是**彻底停止用零宽字符承担身份识别职责**。

优先级如下：

1. host 支持 metadata：直接附加 provenance、continuation ID。
2. host 返回 message ID：保存请求 ID 和返回 message ID 的映射。
3. host 只能发送文本：使用内部可解析控制封套，并在发送给 LLM 的消息变换阶段剥离。
4. 实在无法关联时，时间戳只能作为兼容旧版本的辅助信息，不能作为权威判断。

即使暂时保留零宽字符，它也只能是 UI 表现手段，不得参与状态判断。

## 2.6 事件日志调整

把单一的 `fallback_continue_injected` 拆成更准确的事实：

* `fallback_continue_requested`
* `fallback_continue_dispatch_started`
* `fallback_continue_dispatched`
* `fallback_continue_observed`
* `fallback_continue_cancelled`
* `fallback_continue_failed`
* `fallback_episode_settled`

旧事件可以继续读取，但新逻辑不能把“写下 injected”直接等价为“host 已经接收并产生消息”。

## 2.7 必测竞态

必须覆盖 Esc 出现在以下每个时间点：

* fallback 决策之前；
* Requested 之后、真正调用 prompt 之前；
* prompt 调用中；
* host 已接收但 message.updated 尚未到达；
* message.updated 已到达但 busy 尚未到达；
* busy 到达后、idle 之前；
* idle 到达时；
* session.error 与 Esc 同时到达；
* 进程重启、旧 injected 事件重放后；
* 消息时间戳缺失、为 0、时钟倒退；
* duplicate message.updated；
* fallback prompt 成功但回调晚于新真人消息。

验收标准很简单：

> Esc 之后，在真人再次输入前，不得再出现任何由旧 human turn 产生的 prompt。

---

# 三、问题 2：warn、warn_tdd 等字段既没有稳定强制，也没有稳定剥离

## 3.1 当前根因

仓库已经有共享的解析和删除函数，例如：

* `requireWarnTddOnArgs`
* `requireWarnOnArgs`
* `requireWarnReuseOnArgs`
* `filterAmendFromArgs`

这些函数在验证成功后会删除字段，方向是对的。

但整个系统仍存在几个结构性漏洞：

### 漏洞 A：schema 注入分散

Opencode、OMP、Mux 分别有自己的 schema 改写路径。

结果是：

* 有些 host 内置工具经过增强；
* 有些自定义工具经过增强；
* 有些 alias 没经过；
* 动态注册工具可能晚于增强阶段；
* 同一类修改工具在不同 host 上要求不同。

### 漏洞 B：工具分类和 schema 分类没有完全共用

`WarnTdd` 中对 modification tool、subagent tool、warn-required tool 有一套集合，但部分 host 又自行硬编码 coder、executor、write 等名单。

名单一旦漂移，就会出现：

* 执行阶段要求字段，schema 没告诉 LLM；
* schema 要求字段，执行阶段没校验；
* 某个 alias 可以绕过。

### 漏洞 C：before hook 不一定真正注册

从 Mux 的组装结构看，before 和 after 逻辑存在，但插件暴露路径可能只稳定注册了 after。这样“执行前剥离”就不是强保证，而只是一种希望。

### 漏洞 D：原参数对象可能继续流向下游

即使某个 hook 删除字段，如果：

* wrapper 保存了原引用；
* host 在 hook 之前复制了参数；
* after hook 又读取旧 input；
* 执行路径不经过该 hook；

控制字段仍可能泄漏到实际工具。

### 漏洞 E：执行后再验证没有安全意义

工具已经执行后才发现缺少 `warn_tdd`，无法撤销副作用。

## 3.2 建立统一的 Control Field Policy

建立唯一策略表，以“工具能力”而不是工具名为主键。

每个字段定义：

* 适用能力；
* 是否必填；
* 合法值；
* 错误消息；
* 是否向 LLM 可见；
* 是否允许到达下游；
* 是否进入审计事件；
* retry 时是否需要重新提供。

例如能力可分为：

* FileMutation
* ProcessExecution
* SubagentDelegation
* SearchOnly
* ReadOnly
* ToolCorrection

然后把具体工具名和 alias 归一化到能力：

* coder、edit、write、apply_patch、patch → FileMutation
* executor、pty_* → ProcessExecution
* investigator、meditator、browser、coder delegation → SubagentDelegation

这样新增 alias 时，只需要注册能力，不需要复制四处名单。

## 3.3 schema 必须在最终导出边界统一增强

正确顺序应为：

1. host 收集内置工具；
2. 插件加入自定义工具；
3. alias/包装器全部建立；
4. 最后统一执行 schema decorator；
5. 启动时检查所有工具。

不能在各工具定义过程中零散增强。

启动检查应 fail closed：

* 任何 FileMutation 工具缺少 required `warn_tdd`，插件启动失败；
* 任何 ProcessExecution 工具缺少 required `warn`，插件启动失败；
* 任何 SubagentDelegation 工具缺少 required `warn_reuse`，插件启动失败；
* required 列表中有字段但 properties 中无定义，同样失败。

这比运行后才发现安全得多。

## 3.4 建立唯一执行网关

所有工具最终都必须经过一个执行入口，概念顺序如下：

1. 接收 host 原始参数。
2. 创建一份新的 execution args。
3. 从原始参数提取控制字段。
4. 解析为强类型 ControlEnvelope。
5. 校验适用性、完整性和合法值。
6. 把所有控制字段从 execution args 删除。
7. 冻结或隔离 execution args。
8. 调用真实下游工具。
9. 将 ControlEnvelope 写入审计事件。
10. after hook 只处理结果，不再负责安全校验。

重点是：

> 下游工具从来没有机会拿到原始参数对象。

`amend` 等字段即使需要保留，也应进入 sidecar/control envelope，而不是重新塞回普通 enumerable 参数。

## 3.5 各 host 的具体改造

### Opencode

* 保留 ToolDefinitionHooks 的 schema 扩展职责，但最终改为调用统一 decorator。
* HookExecute 不再自己定义工具名单。
* before 阶段调用统一执行网关。
* after 阶段不得再次要求已经被剥离的字段。
* 动态工具注册完成后重新执行完整性检查。

### OMP

* 删除或废弃本地重复的 `requireWarn*Omp` 判断。
* coder、executor 之外的 host built-in edit/write/patch/pty 也要走统一能力分类。
* schema 和运行时都调用同一策略源。
* ToolResult hook 不再作为“二次安全兜底”；它只能检查执行网关留下的审计标记。

### Mux

* 明确注册真正的 `tool.execute.before`。
* wrapper 和插件不能各自决定是否剥离。
* PluginCatalog 中只为少数工具注入字段的做法要改成全目录遍历。
* 对由 Mux 转发到其他 host 的工具，也要保证转发参数是净化后的参数。

## 3.6 验收矩阵

对每个 host、每个 tool alias 都验证：

* schema 中字段存在；
* required 正确；
* LLM 缺字段时工具不执行；
* 字段值非法时工具不执行；
* 合法字段可以执行；
* 实际下游序列化参数不含 warn/warn_tdd/warn_reuse/amend；
* after hook 看不到普通参数中的控制字段；
* 重试不会因为字段已被删除而错误失败；
* null、空对象、额外字段行为一致；
* plugin 注册顺序改变后仍然有效；
* 动态加入的新工具要么被增强，要么启动失败。

---

# 四、问题 3：context 长度到阈值时 todowrite 不触发

## 4.1 当前实现存在的关键断点

当前代码已经有数学判定、ContextState、预算 nudge 文本和消息注入逻辑。`buildContextBudgetNudgeMessage` 会创建一条 synthetic user message，要求模型立即调用 todowrite。

但整个触发链有多个会导致“不工作”的地方。

### 断点 A：拿不到 token 就直接退出

当前 token 来源优先使用 host 提供的 token count，否则根据历史 usage 和字节数估算。

如果两者都不可用，结果是 None，预算判定就不执行。

这相当于：

> 最需要保护的未知状态，反而完全关闭保护。

### 断点 B：MaxInputTokens 可能变成 0

Opencode 的 model limit 依赖 provider list 和最近 user model。

provider API 不可用、model 解析失败、读取不到 limit 时，可能把上限解析为 0，随后 budget 功能整体跳过。

### 断点 C：使用的是旧 assistant usage，不是即将发送的 prompt

最近一次 assistant 记录的 input/cache token，只表示上一轮 host 报告的使用量。

它不一定包含：

* 当前 backlog projection；
* 当前 caps；
* 新加入的 synthetic message；
* message transform 后的最终消息；
* compaction 后的新上下文。

### 断点 D：R 的计算可能混入全部历史 todo

若完成 todo 的数量是从全部历史消息统计，旧阶段做过的 todowrite 会持续影响当前公式。

正确的 R 应是：

> 当前 budget episode 或当前 phase 开始后，成功完成的 todo checkpoint 数量。

### 断点 E：NudgeTrack 被写入但没有真正参与防重和状态推进

系统可能记录 EmergencySignaled，但后续判定没有严格依据这个状态来决定：

* 是否已经发过；
* 是否收到 todowrite；
* 是否重置阶段；
* 是否需要升级处理。

### 断点 F：message transform cache 掩盖了预算变化

如果缓存 key 只依赖原始输入消息，而模型 limit、usage observation、context generation 变化了，系统可能直接返回旧 transform 结果，不重新计算预算。

## 4.2 先明确数学量的语义

继续使用现有公式没有问题，但必须把每个变量固定下来。

建议定义：

* `L`：模型有效输入上限；
* `O`：为输出、工具、系统余量保留的 token；
* `B = L - O`：真正可用输入预算；
* `C`：当前最终 outbound prompt 的 token 数；
* `P`：一个 phase 预计新增的 token；
* `N`：允许继续的阶段数量；
* `R`：当前 budget episode 中已完成的 checkpoint 数；
* `A`：当前 phase 起点的 token 数；
* `G`：context generation。

最关键的是：

* 所有 host 对 L、O、C 使用同样定义；
* R 不从全历史计算；
* C 必须尽可能接近最终发送内容。

## 4.3 重建 ContextBudget 状态机

建议状态：

* Healthy
* Approaching
* EmergencyRequired
* EmergencySignaled
* TodoObserved
* PhaseReset
* CompactionRequired
* MeasurementDegraded

每个 budget episode 带唯一 ID。

### Healthy → EmergencyRequired

根据公式达到边界时进入。

### EmergencyRequired → EmergencySignaled

只注入一次 context budget nudge，并记录：

* episode ID；
* 计算时的 C；
* B；
* 阈值左右两侧；
* model；
* measurement source；
* context generation。

### EmergencySignaled → TodoObserved

必须观察到一次真正成功的 todowrite tool result，而不是仅仅看到 LLM 说“我会写 todo”。

### TodoObserved → PhaseReset

成功提交 backlog 后：

* 更新 phase base；
* 更新 todo ordinal；
* 递增 phase generation；
* 清除本 episode 的 signaled 状态。

### EmergencySignaled 长时间无进展

有限次数升级：

1. 第一次普通 nudge；
2. 第二次更强制的 nudge；
3. 再不执行则进入 CompactionRequired 或阻止继续扩展上下文。

不能每次 message transform 都无限重复插入。

## 4.4 token 测量层级

建议按可靠性排序：

1. host 的精确 tokenizer，对最终 encoded outbound messages 计数；
2. provider/model 官方 tokenizer；
3. 已校准的字节/token 上界估算；
4. 保守固定比例估算。

无法精确测量时进入 `MeasurementDegraded`，但仍执行保护。

降级估算应该偏保守，宁可早一点 todowrite，也不能完全不触发。

## 4.5 model limit 解析

集中建立一个 `EffectiveContextLimit` 解析器，返回的不只是数值，还包括：

* limit；
* source；
* model；
* 是否缓存；
* 缓存年龄；
* 是否降级；
* reserve；
* failure reason。

provider list 临时不可用时：

* 使用最近一次成功缓存；
* 没有缓存时使用该 host 的安全保守默认；
* 不得返回 0 并静默关闭。

Opencode、OMP、Mux 应共享这一逻辑，不要各自采用不同 reserve。

## 4.6 在消息流水线中的正确位置

预算判定应放在：

1. 消息清洗；
2. backlog projection；
3. caps/system prompt 注入；
4. compaction 结果处理；
5. 其他稳定 synthetic 内容加入；

之后。

然后：

6. 对将要发送的最终消息计算 token；
7. 决定是否加入 context-budget nudge；
8. 加入 nudge 后再次验证不会超出硬上限。

预算自身生成的 nudge 也占 token，不能假装它是免费的。

## 4.7 修复缓存

message transform 缓存 key 至少要包含：

* raw message fingerprint；
* model identity；
* model limit generation；
* context usage generation；
* backlog version；
* budget phase version；
* compaction generation；
* caps version。

更干净的方案是：

* 稳定消息变换可以缓存；
* context budget 判定移到缓存之后，每次发送前重新计算。

## 4.8 必测场景

* 公式边界前 1 token、正好边界、边界后 1 token；
* N、R、P 的性质测试；
* 历史存在 100 次 todowrite，但当前 phase 的 R 仍为 0；
* provider API 不可用；
* tokenizer 不可用；
* 第一次会话没有历史 usage；
* 中途从小模型切换到大模型；
* 中途从大模型切换到小模型；
* compaction 后 phase 正确重置；
* todowrite 失败时不能算 TodoObserved；
* 相同输入消息、usage 增长后必须重新判定；
* Opencode 和 OMP 对同一输入做出相同决策；
* 已经 Signaled 时不会连续插入重复 prompt；
* 自动 continue 期间预算 nudge 不与 fallback 抢 owner。

---

# 五、问题 4：代码和控制台出现大量 DEBUG

## 5.1 当前问题

至少已明确看到 context budget observation 直接向控制台输出：

* provider list 不可用；
* provider.list 调用失败。

这些属于可预期的能力探测和降级，不应默认污染用户控制台。仓库中的部分 Semble 调试输出已经采用环境变量开关和文件输出，这个方向相对合理，但仍应统一管理。

## 5.2 不要简单全局删除 DEBUG

应先分类：

### 必须删除或改为 trace

* capability 不存在；
* 缓存未命中；
* 正常 fallback 选择；
* 正常 nudge 跳过；
* provider list 不支持；
* 某 optional API 不可用。

### 应改为 warn

* 本应有 model limit，但解析失败；
* 事件日志写入失败；
* schema 完整性检查失败；
* 状态 invariant 被破坏；
* 使用降级 token 估算。

### 应保留为 error

* 数据可能丢失；
* 下游工具执行状态未知；
* event log 与内存状态不一致；
* 用户 Esc 后仍检测到旧 continuation 被发送。

## 5.3 建立统一结构化日志

每条日志至少包含：

* subsystem；
* event name；
* session ID 的脱敏形式；
* human turn ID；
* continuation ID；
* severity；
* reason code；
* host；
* model；
* generation；
* 是否 degraded。

禁止默认记录：

* 完整 prompt；
* 完整工具参数；
* 用户文件内容；
* API key；
* token；
* 私密路径；
* review task 全文。

## 5.4 输出策略

* 默认：只显示 warn/error。
* debug：显式配置开启。
* trace：只写文件或诊断 sink。
* TUI/插件运行时不得直接 `printfn` 或 `console.log`。
* 相同错误需要去重或限频，避免 provider API 每轮失败都刷屏。
* session 结束时可以输出一条汇总，而不是几十条过程日志。

## 5.5 验收

自动化测试启动完整插件并执行普通会话：

* stdout 不包含 `DEBUG:`；
* stderr 不包含普通能力探测；
* debug=false 时没有 debug 级日志；
* debug=true 时结构化字段完整；
* 日志中不含 prompt 和控制字段原值。

---

# 六、问题 5：压缩后错误触发 nudge；自动继续时不应 nudge

## 6.1 当前根因

当前 nudge 主要根据 session idle/error/status idle 判断“自然停止”。

但 session idle 可能来自：

* 真人回答结束；
* compaction 结束；
* title 生成结束；
* fallback continuation 结束；
* recovery 尝试结束；
* nudge 自己结束；
* tool subturn 结束。

这些事件被压扁成了同一个 `isNaturalStop`。

虽然代码会跳过 synthetic assistant agent，例如 compaction、title，但其做法往往是继续往前寻找上一条非 synthetic assistant。这样 compaction 刚完成时，系统可能拿到压缩前那条旧 assistant 消息，误以为它刚刚自然结束，然后发 nudge。

另外，fallback 的 `Consumed` 只反映某一瞬间状态机是否消费了事件。fallback transition 一旦把 phase 改成 Idle，后面的 nudge 观察者可能看到 `Consumed=false`，误以为事件没人处理。

当前 nudge snapshot 已经有 work state、block status、anchor 和 dedup，但没有“终止事件来源”和“当前 continuation owner”这两个关键维度。

## 6.2 引入 TerminalEventOrigin

不要再让 nudge 只接收 `naturalStop=true/false`。

它应接收明确分类：

* HumanTurnCompleted
* HumanTurnAborted
* FallbackContinuationCompleted
* FallbackRecoverySettled
* CompactionCompleted
* TitleGenerationCompleted
* TodoNudgeCompleted
* ReviewNudgeCompleted
* ToolSubturnCompleted
* UnknownLegacyStop

默认只有 `HumanTurnCompleted` 可以进入普通 todo/review nudge 判定。

`UnknownLegacyStop` 应保守跳过，而不是积极 nudge。

## 6.3 continuation owner 仲裁

为 session 设置一个明确的 owner：

* None
* Fallback
* Compaction
* ContextBudget
* Review
* TodoNudge

发送 prompt 前必须尝试取得 owner。

例如：

* fallback 已持有 owner，nudge 直接跳过；
* compaction 持有 owner，任何 nudge 都跳过；
* context budget nudge 已经发送，普通 todo nudge 不重复发送；
* review nudge 优先于普通 todo nudge时，应由统一策略明确决定，而不是两个 hook 各自发送。

owner 必须通过 settle 事件释放，不能只看 session 当前是否 idle。

## 6.4 compaction 生命周期

需要明确记录：

* compaction_started
* compaction_completed
* compaction_failed
* context_generation_changed

compaction 完成后：

* 增加 context generation；
* 清除旧 assistant anchor；
* 清除基于压缩前消息生成的 pending nudge；
* 不触发普通 nudge；
* 等下一次真正人类轮次完成后再重新判断。

不能通过“忽略 compaction agent，然后使用前一条 assistant”来补偿。

## 6.5 fallback auto-continue 的处理

fallback continuation 应从开始到 settle 一直拥有该终止事件。

流程应是：

1. fallback 请求 continuation；
2. 取得 owner；
3. continuation busy/idle/error 全部归 fallback episode；
4. fallback 判断是否继续下一个模型或结束；
5. 发出 `fallback_episode_settled`；
6. 释放 owner。

普通 nudge 不能在第 3 步看到某个 idle 就抢跑。

当 fallback episode settle 后仍有 todo/review backlog，应该由一个明确策略决定：

* fallback 已经完成必要自动继续，则本轮不再 nudge；
* 或下一次真正 human completion 再 nudge。

不要让 nudge 作为 fallback 的隐式第二套继续机制。

## 6.6 必测事件序列

* human answer → normal idle：应允许一次 nudge。
* human answer → compaction started → compaction idle：零 nudge。
* title generation idle：零 nudge。
* fallback send → busy → idle → next fallback：零普通 nudge。
* fallback settle：本 episode 零重复 nudge。
* nudge 自己的 prompt 完成：不能递归 nudge。
* Esc → idle：零 nudge。
* compaction 后旧 assistant 仍在历史：不能拿它作为新 anchor。
* session.error 被 fallback 消费后又出现 status idle：仍然只能有一个 owner。
* restart replay 时 owner 和 episode 恢复一致。

---

# 七、问题 6：todo/review nudge 使用旧模型

## 7.1 当前根因

当前模型解析存在三种来源混用：

* 最近 assistant 的 model；
* 最近 user message 的 model；
* fallback runtime 中最近 injected model。

其中 injected model 是 session 级缓存，而且真人新消息时没有在所有路径稳定清除。

这样会产生典型错误：

1. 用户用模型 A 发起任务；
2. fallback 切到模型 B；
3. 用户随后用模型 C 发新消息；
4. todo/review nudge 仍读取旧 injected model B。

另外，OMP 某些路径直接从最后 assistant 选择模型。compaction/title 或旧 assistant 都可能改变结果。

事件日志中的 nudge snapshot 目前主要记录 `assistant_completed` 的 model，也无法代表“最后一条真人消息指定的模型”。fallback injection fold 本身也只记录最近注入模型和次数，没有绑定 human turn。

## 7.2 建立 TurnRoutingContext

在确认一条消息是真人新消息时，立即建立：

* humanTurnId
* userMessageId
* provider ID
* model ID
* variant
* reasoning effort
* thinking
* temperature/top-p 等需要延续的路由设置
* agent
* timestamp/order
* provenance = Human

这个对象是本轮 todo/review nudge 的权威来源。

## 7.3 模型选择优先级

建议固定为：

1. 当前 human turn 的显式 routing context；
2. 当前 continuation episode 的显式 attempt override；
3. 当前 agent 配置的默认模型；
4. 全局默认模型。

不再允许：

* “最近注入过的模型”无条件覆盖真人消息；
* “最近 assistant 的模型”作为首选；
* synthetic compaction/title 的模型改变当前 human turn route。

## 7.4 fallback model 的作用域

fallback 选中的 model 应保存为：

* humanTurnId；
* continuationId；
* attempt；
* selected route。

它只对这次 fallback attempt 生效。

以下情况必须清除：

* 真人新消息；
* Esc；
* fallback episode settle；
* task complete；
* session cleanup；
* session generation 改变。

## 7.5 事件日志调整

不要只保存一个 `InjectedModel`。

建议折叠为三个独立部分：

* LastHumanTurnRouting
* CurrentFallbackAttemptRouting
* LastAssistantRouting

nudge 只使用 LastHumanTurnRouting。

fallback 自己执行时使用 CurrentFallbackAttemptRouting。

诊断和 UI 才使用 LastAssistantRouting。

## 7.6 统一 model 解码

不同 host 的 model 字段形态可能不同：

* provider/model；
* model object；
* 顶层 providerID/modelID；
* variant 单独存放。

必须由一个共享 decoder 解析。

不要在 route round-trip 时丢失：

* variant；
* reasoning effort；
* thinking；
* max tokens；
* sampling 配置。

否则即使 model ID 正确，也可能表现成“用了错误模型设置”。

## 7.7 必测场景

* Human A → nudge：A。
* Human A → fallback B → fallback continuation：B。
* Human A → fallback B → Human C → nudge：C。
* Human C → compaction model D → nudge：仍为 C。
* Human C → title model E → nudge：仍为 C。
* Human C 无 model → 使用 agent/session default。
* 进程重启后从事件日志恢复：仍为 C。
* model 相同但 variant 不同：variant 必须保留。
* stale injected event 晚到：不能覆盖新 human turn。

---

# 八、问题 7：review nudge 没有原始任务 front matter

## 8.1 当前数据其实已经存在

当前 review loop fold 会保存 Active task。

`loop_activated` 事件包含 task，`ReviewLoopFold.Active task` 也能恢复它。review submission prompt 还已经使用 `original_task` front matter。

但是数据在传到 nudge snapshot 时被丢掉：

* NudgeSnapshotSource 有 reviewLoop；
* SessionSnapshot 只有 todos、assistant text、work state、model 等；
* 没有 original task；
* `NudgeLoop` 最后只调用基于 todos 的 loop nudge prompt。

也就是说，这不是“任务没保存”，而是**在 nudge 领域模型中没有携带它**。review task fold 和 prompt 生成路径的这种断层，在当前仓库结构中非常明确。

## 8.2 扩展 review snapshot

当 review loop Active 时，snapshot 必须包含：

* originalTask；
* reviewLoopId 或 reviewSessionId；
* currentRound；
* latest verdict；
* latest feedback；
* todos；
* nudge anchor；
* humanTurnId。

不要只保存一个 `isLoopActive=true`。

## 8.3 review nudge front matter

每次 review nudge 至少携带：

* `original_task`
* `review_loop_id`
* `review_round`
* 可选 todos
* 可选最新 feedback
* prompt origin = review_nudge

其中 `original_task` 必须是最初完整任务，不是摘要，不是最近 assistant 对任务的复述。

## 8.4 注意 task 与 original_task 的语义区分

当前系统里：

* `task` 往往用于激活 worker review mode；
* `original_task` 用于 reviewer 评审上下文。

不要为了补字段，简单把 `task` 到处复制，否则 review nudge 可能被误识别成一次新的 review 激活命令。

建议：

* 初次进入 With-Review Mode：使用 `task` 表示激活命令的任务。
* 后续 review nudge、double-check、reviewer continuation：统一使用 `original_task`。
* prompt origin 和 review loop ID 明确表明这是一条 continuation，而不是激活新 loop。

## 8.5 建立统一 ReviewPromptContext

当前 reviewer prompt、WIP acknowledgment、needs revision、double check、review nudge 分别拼装 front matter，容易再次漏字段。

应建立一个统一 context，所有 review prompt 生成器只能从这个 context 构建。

字段可以按场景选择是否显示，但以下不变量强制：

* review loop Active ⇒ originalTask 非空；
* review nudge ⇒ originalTask 必须输出；
* needs revision ⇒ originalTask 和 feedback 必须输出；
* double check ⇒ originalTask 必须输出；
* reviewer submission ⇒ originalTask 必须输出。

## 8.6 缺失任务时 fail closed

若 review loop 是 Active，但 originalTask 为空：

* 不发送 review nudge；
* 记录 invariant violation；
* 尝试从事件日志重新折叠；
* 仍无法恢复则终止本次 nudge；
* 不要发送一个无任务 prompt 让 LLM猜。

## 8.7 必测场景

* Active review，无 todos；
* Active review，有 todos；
* needs revision 后继续；
* WIP submit 后继续；
* compaction 发生在 loop activation 和 nudge 之间；
* 进程重启后恢复；
* original task 包含多行、冒号、引号、YAML 特殊字符；
* reviewer child session 收到 nudge，不应重新激活 worker loop；
* task 缺失时零 prompt；
* 多个 review round 始终保持同一 original task。

---

# 九、建议的统一事件和状态结构

为了避免再次分裂，建议围绕以下四个 session 投影组织。

## 9.1 HumanTurnProjection

保存：

* 当前 humanTurnId；
* userMessageId；
* routing context；
* lifecycle；
* cancelGeneration；
* startedAt；
* completedAt。

## 9.2 ContinuationProjection

保存：

* owner；
* continuationId；
* origin；
* humanTurnId；
* generation；
* dispatch stage；
* route；
* settled reason。

## 9.3 ContextProjection

保存：

* contextGeneration；
* compaction state；
* budget episode；
* phase base；
* todo ordinal；
* measurement provenance。

## 9.4 ReviewProjection

保存：

* active/inactive；
* original task；
* review loop ID；
* current round；
* latest verdict；
* latest feedback。

Nudge snapshot 不再从零散消息中自行推理，而是组合这四个投影。

---

# 十、实施顺序

不要七个问题同时大改。推荐按下面顺序实施。

## 阶段 0：先固定复现和观测

先建立端到端测试事件脚本，重放：

* Esc 与 fallback 竞态；
* compaction 后 idle；
* model A/B/C 切换；
* context threshold；
* review nudge；
* warn 字段执行。

同时记录结构化事件，不再增加临时 DEBUG。

## 阶段 1：引入 provenance、turn ID、generation，但暂不删除旧逻辑

新增：

* humanTurnId；
* continuationId；
* cancelGeneration；
* prompt origin；
* routing context。

先双写新旧状态，比较结果。

## 阶段 2：优先修复 Esc 和 nudge owner

这是最高风险问题。

完成：

* sticky cancellation；
* invalidation；
* terminal origin；
* continuation owner；
* fallback settle；
* compaction gate。

此时应先保证“不会擅自继续”，哪怕偶尔少 nudge，也比 Esc 后继续安全。

## 阶段 3：修复模型和 review task

切换 nudge 消费者：

* model 从 HumanTurnProjection 取；
* task 从 ReviewProjection 取；
* 禁止读取 stale injected model；
* 禁止从旧 assistant 推导 review context。

这部分变更相对独立，容易验证。

## 阶段 4：统一控制字段执行网关

先完成能力分类、最终 schema decorator 和启动完整性检查，再切换执行入口。

迁移期间可以保留 host-specific hook 做审计，但不能继续作为权威判断。

## 阶段 5：重建 context budget

这一部分数学、测量、缓存、compaction 耦合最深，应在事件 owner 和 provenance 稳定后改。

否则 context-budget nudge 也会成为新的抢跑来源。

## 阶段 6：清理旧启发式和 DEBUG

删除或降级：

* zws 文本识别；
* timestamp 权威判断；
* `last non-synthetic assistant` 回退；
* session 级 stale injected model；
* 各 host 重复 warn 判断；
* 直接 `printfn DEBUG`；
* post-execute 安全校验。

---

# 十一、兼容旧事件日志

新增事件时不要破坏旧 `.wanxiangshu.ndjson`。

建议提高事件 schema version，并提供纯迁移规则。

## 对旧 fallback 事件

旧 `fallback_continue_injected` 没有 continuation ID 时：

* 可以恢复“曾存在一次 fallback attempt”；
* 不能据此解除 cancellation；
* 不能据此覆盖当前真人模型；
* 重启后若状态不明确，保守结束旧 episode，不自动发送新 prompt。

## 对旧 assistant snapshot

缺少 humanTurnId 时：

* 可用于展示；
* 不可作为 nudge model 的高优先级来源。

## 对旧 review loop

如果 `loop_activated` 中有 task，恢复到 ReviewProjection。

如果 active 状态没有 task：

* 标记损坏；
* 不发 review nudge。

## 对旧 budget state

没有 phase ordinal/generation 时：

* 从当前上下文建立新 phase；
* 不沿用全历史 todo 数量；
* 首次恢复时执行一次保守测量。

---

# 十二、最终验收标准

全部修复完成后，应满足以下可观察结果。

1. **Esc**

   * Esc 后旧轮次发送 prompt 数量严格为 0。
   * 迟到 zws/message.updated/busy/idle 不会解除取消。

2. **warn 字段**

   * 每个适用工具 schema 都强制要求字段。
   * 缺失或非法字段时真实工具调用次数为 0。
   * 下游参数中控制字段出现次数为 0。

3. **context budget**

   * 精确阈值处稳定触发。
   * token API 不可用时仍有保守触发。
   * 一次 episode 只发送一次初始 budget nudge。
   * 成功 todowrite 后 phase 正确重置。

4. **日志**

   * 默认运行 stdout 中 `DEBUG:` 数量为 0。
   * 敏感 prompt、工具参数不进入日志。

5. **compaction/fallback**

   * compaction 完成后的普通 nudge 数量为 0。
   * fallback episode 中普通 todo/review nudge 数量为 0。
   * 每个终止事件只有一个 owner。

6. **模型**

   * todo/review nudge 使用当前真人轮次模型。
   * 旧 fallback injected model 不影响新真人轮次。
   * variant 和 reasoning 设置不丢失。

7. **review task**

   * 每条 review nudge 都包含完整 `original_task`。
   * active loop 缺 task 时不发送任何失忆 prompt。
   * compaction、重启、needs revision 后原任务保持不变。

---

# 最重要的取舍

这次修复中应坚持三个原则：

* **宁可少自动继续一次，也不能违反用户 Esc。**
* **宁可少发一次 nudge，也不能在 compaction/fallback 后重复抢控制权。**
* **宁可因状态缺失阻止 review continuation，也不能让 LLM 在没有原始任务的情况下猜。**

真正要删除的不是某个零宽字符，而是整个系统对“根据文本、时间和最近消息猜测状态”的依赖。只要 provenance、generation、owner、routing context 和 durable projection 建立起来，这 7 个问题会一起消失，而不是换一种形式继续出现。

---

# 附件：针对补充分析的吸收、校正与实施增补稿

## 一、增补说明

经结合仓库现状逐项核对，补充分析中以下内容值得直接吸收：

1. `FallbackSubagentGate.needFallbackContinue` 确实遗漏了对 `Cancelled` 生命周期的最高优先级拦截。
2. Esc/Abort 后应同步清理 `AwaitingBusy`、`SubsessionPending`、`EventHandlingActive`、BusyCount 等派生门禁。
3. Context Budget 在既没有实时 token 数据、也没有历史测量数据时确实会直接退出。
4. `Opencode/NudgeEffect.fs` 中局部模型解析只接受对象，没有接受 `"provider/model"` 字符串。
5. Review task 已经保存在 `ReviewLoopFold.Active task` 中，但在转换为 `SessionSnapshot` 时丢失。
6. 裸 `printfn "DEBUG: ..."` 确实可能污染主机协议输出。

不过，下列建议不能原样采用：

* 不能继续把物理时间戳作为区分真人消息和 fallback 注入消息的权威依据。
* 不应把控制字段处理分散到每个具体工具解码器中。
* 不应通过抛出普通 JavaScript 异常来阻止工具调用。
* 不能使用固定的 100,000 token 作为所有未知模型的默认上限。
* 不应让纯粹的 `NudgeDerivation` 直接依赖可变的 Fallback Runtime。
* Review nudge 中必须使用 `original_task`，不能使用可能重新激活 review loop 的 `task`。

本增补稿用于明确哪些内容作为近期止血修复，哪些内容作为最终架构修复。

---

# 二、Esc 与 Fallback：补充分析定位正确，但修复范围还要扩大

## 2.1 已确认的直接漏洞

当前 `needFallbackContinue` 只把 `TaskComplete` 视为终止状态：

* `TaskComplete` 立即返回 `false`；
* `Cancelled` 则继续检查 `EventHandlingActive`、`AwaitingBusy`、`SubsessionPending` 和 `BusyCount`；
* 因此，只要任意临时门禁仍为活动状态，已经取消的 session 仍会被判定为需要 fallback continuation。

这是一个明确、可立即修复的逻辑错误。当前 `terminalObservation` 和 `isSubagentSettledFromObservation` 也只特别处理了 `TaskComplete`，没有将 `Cancelled` 纳入完整终止语义，因此不能只修改 `needFallbackContinue` 一处。

## 2.2 第一层止血修复

所有由 lifecycle 派生的 gate 判定，第一分支统一为：

* `Cancelled`：终止；
* `TaskComplete`：终止；
* 其他状态：继续判定。

至少同步检查：

* `needFallbackContinue`
* `terminalObservation`
* `isSubagentSettledFromObservation`
* 其他所有由 `FallbackGateObservation` 计算等待、继续或 resolve 的函数。

应建立一条统一不变量：

> Lifecycle 只要是 Cancelled 或 TaskComplete，任何 BusyCount、Consumed、Phase 和 ActiveGates 都无权重新要求 continuation。

## 2.3 Abort 后必须原子清理门禁

补充分析提出同步清理运行时标志，这是正确且必要的。

在 Abort 被状态机正式识别为 `Cancelled` 后，应在同一 session 串行操作中完成：

* `SetAwaitingBusy false`
* `ClearSubsessionPending`
* `SetEventHandlingActive false`，或确保当前 handler 的 finally 不会重新产生继续需求
* `SetNudgeActive false`
* `SetBusyCount 0`
* `ClearConsumed`
* 清除尚未发送的 continuation request
* 清除旧 injected model 的活跃作用域
* 唤醒等待 gate 状态变化的 listener，使其重新计算并结束等待

这里需要一个统一的 `CancelEpisode` 或同等语义操作，不能让不同 host 分别手动清理若干字段，否则以后新增 flag 又会遗漏。

## 2.4 仅靠时间戳仍不足以解决竞态

记录 `InjectedAt` 可以保留，适合作为：

* 诊断信息；
* 旧版本兼容；
* 异常事件排序的辅助信号。

但不能把“消息时间晚于 InjectedAt”当作真人新消息的充分条件。原因包括：

1. fallback 注入消息本身被主机持久化时，消息时间也可能晚于调用注入的时间；
2. 不同时间戳可能来自不同进程或不同时钟；
3. 迟到的 host event 可能获得新的接收时间；
4. 时间字段可能为空、为零或只有秒级精度；
5. 新真人消息和旧 continuation 可能在同一毫秒内交错。

最终判断必须依赖：

* message provenance；
* continuation ID；
* human turn ID；
* cancel generation；
* host message ID 或 request/message 映射。

时间戳只能作为兼容补丁，不能作为单一事实源。

## 2.5 额外验收要求

除了原有 Esc 测试，还应加入：

* `Cancelled + EventHandlingActive=true` 必须返回不继续；
* `Cancelled + AwaitingBusy=true` 必须返回不继续；
* `Cancelled + BusyCount>0` 必须返回不继续；
* `Cancelled + Phase=Retrying` 必须返回不继续；
* Cancelled session 必须被视为 settled；
* 旧 continuation 的迟到 busy/idle 不能重新增加 BusyCount；
* 当前 event handler 的 finally 清理标志后，不能触发新的 fallback send；
* 新真人消息必须创建新 turn/generation 后才允许恢复 Active。

---

# 三、Warn/TDD：应吸收“下沉执行边界”，但不能分散进每个解码器

## 3.1 当前实现已经具备部分能力

当前共享 `ToolHookRuntime` 已经能够：

* 校验 `warn_tdd`；
* 校验 `warn`；
* 校验 `warn_reuse`；
* 校验成功后用 `Dyn.deleteKey` 删除字段；
* 删除 `amend` 并将其转为隐藏元数据。

所以问题不是“完全没有校验或删除”，而是：

* 各 host 入口不统一；
* OMP 又复制了一套本地校验；
* after hook 被当作可能的兜底；
* 某些实际执行路径可能绕过 before hook；
* schema 和运行时工具分类可能漂移。

## 3.2 不建议把删除逻辑写入每个具体 decoder

把 `Dyn.deleteKey` 分散到 `decodeExecutorArgs`、`decodeWriteArgs` 等函数有三个问题。

### 第一，覆盖范围不完整

`ToolArgsDecode` 只负责系统认识的一部分 typed tool。

主机原生工具、动态插件工具、PTY alias、wrapper 直通工具不一定经过这些 decoder。

### 第二，重复违反单一事实源

每个 decoder 都实现一遍控制字段处理后，新增字段或新增工具时仍会漏改。

### 第三，解码器职责被污染

具体 decoder 应只负责把已经净化的业务参数转换为领域类型，不应同时承担安全策略、工具能力分类和审计职责。

因此，应吸收“下沉到不可绕过边界”的思想，但实施位置应当是：

> 统一工具执行网关，位于任何 decoder 和实际 execute 之前。

执行顺序固定为：

1. 规范化工具名和 alias；
2. 查询工具能力；
3. 克隆或创建新的参数对象；
4. 提取并验证控制字段；
5. 从业务参数对象物理删除控制字段；
6. 形成独立的 `ControlEnvelope`；
7. 再调用 typed decoder 或 host-native execute；
8. 审计记录只读取 `ControlEnvelope`。

## 3.3 不建议抛出普通 JavaScript Error

补充分析建议验证失败时直接抛出 `InvalidArgumentError`。这一点风险较大。

普通异常有可能被上层解释为：

* `session.error`；
* 未知执行错误；
* retryable error；
* fallback 触发条件。

而当前 fallback 对无法明确分类的错误采用偏重试的安全网策略。这样可能出现：

> LLM 少填了 `warn_tdd` → 抛异常 → 被当成模型或会话错误 → fallback 自动继续。

这会把问题 2 和问题 1 重新耦合起来。

更安全的做法是：

* 返回强类型 `DomainError`，这一点值得采用；
* 但将它编码为“工具调用被拒绝”的正常 tool result；
* 明确标记 `executionStarted=false`；
* 不产生 session-level error；
* 不进入 fallback 错误分类器。

若某个 host 的 before hook 无法可靠阻止执行，应包装实际 execute 函数，而不是依赖抛出异常碰运气。

## 3.4 after hook 不能作为安全兜底

OMP 当前注释中把 `tool_result` 称为最后安全插入点，但工具结果出现时，副作用已经发生。

after hook 可以：

* 检查不变量；
* 记录“before hook 被绕过”；
* 对开发构建发出高严重度告警；
* 清理仅用于展示的残留字段。

但它不能承担：

* 强制必填；
* 阻止执行；
* 保护文件或进程副作用。

验收时必须确认：

> 验证失败时，底层工具 execute 调用计数严格为零。

## 3.5 Schema 建议可以直接吸收

所有适用工具的 schema 中都应同时满足：

* `properties` 包含对应控制字段；
* 字段使用唯一 enum 值；
* `required` 包含该字段；
* alias 和动态工具同样处理；
* schema 最终导出前执行一次完整性检查。

但 schema 只能提高 LLM 正确生成参数的概率，不能代替运行时强制。

---

# 四、Context Budget：Bootstrap 估算值得采用，但两个建议需要校正

## 4.1 已确认的真实断路

当前 `resolveCurrentTokens` 的行为是：

* 有实时 token count：使用；
* 否则有历史 `LastUsage`：按字节比例估算；
* 两者都没有：返回 `None`。

随后 `applyContextBudget` 直接返回原消息，完全跳过预算保护。

此外，`resolveMaxInputTokens` 在同步和异步解析都失败后可能返回 0，而 `applyContextBudget` 又在 `MaxInputTokens <= 0` 时直接关闭机制。

因此，引入首次会话估算器和非零安全上限是正确方向。

## 4.2 不能简单使用“每 4 个字符或 4 个字节等于 1 token”

固定的四比一换算对英文只能算粗略经验，对中文、代码、JSON、路径、随机标识符并不安全。

尤其是：

* 中文字符按 UTF-8 通常占多个字节；
* 某些 tokenizer 中一个汉字可能接近一个 token；
* 代码符号和长标识符的 token 密度可能明显更高；
* 使用 `bytes / 4` 可能低估当前 token 数。

Bootstrap estimator 应采取保守策略：

1. 优先使用 host/model tokenizer；
2. 其次使用该 host 已校准的语言混合估算；
3. 未知情况下使用偏高估计；
4. 明确记录 `MeasurementDegraded`；
5. 一旦获得真实 usage，就更新校准比例。

宁可提前触发 todowrite，也不能因为低估而在阈值后仍不触发。

## 4.3 固定 100,000 token 默认值不安全

未知模型可能只有：

* 8K；
* 16K；
* 32K；
* 64K；
* 或其他限制。

将未知模型一律当作 100K，会在小上下文模型上严重延迟保护。

更稳妥的顺序是：

1. 当前 session/model 的精确 limit；
2. provider 缓存中的最近成功 limit；
3. model family 的已知保守下限；
4. host 配置的安全默认；
5. 最终全局保守默认。

全局默认应偏小，并允许配置覆盖。必须记录 limit 的来源，不能只返回一个无法审计的整数。

## 4.4 `rebuildPhaseState` 已经会在 backlog 变化时更新状态

补充分析提出“Backlog 变化时强制更新 ContextBudgetStore”，但当前代码实际上已经执行了：

* 检测 `backlog <> currentStore.LastBacklog`；
* 重新计算 stable messages 和 token；
* 更新 `State`；
* 更新 `LastBacklog`；
* 重置 `NudgeTrack`。

因此这里不应重复实现一套逻辑。

真正还应修复的是：

### 问题 A：`completedTodoCount` 统计了全部消息历史

当前代码从全部 flatten messages 中统计 todo result。公式中的 `R` 应表示当前 phase 或当前 budget episode 中的成功 anchor 数，不能把旧阶段的 todowrite 永久累加。

应在 phase state 中保存：

* phase 起始 todo ordinal；
* 当前总成功 todo ordinal；
* `R = currentOrdinal - phaseStartOrdinal`。

### 问题 B：`NudgeTrack` 被写入但没有参与注入判定

代码会把状态设为 `EmergencySignaled`，但 `checkAndInjectNudge` 并没有据此阻止重复注入。

必须明确决定：

* 同一 episode 是否只允许注入一次；
* 什么事件确认 LLM 已经收到；
* todowrite 成功后何时重置；
* 消息变换重复执行时是否重发。

### 问题 C：估算对象应接近最终 outbound prompt

当前 usage 与最终 message transform 之间仍可能存在偏差。预算判定应尽量放到稳定消息变换完成之后，并把预算 nudge 自身的 token 也计算进去。

---

# 五、DEBUG 日志：方向正确，但不能无差别删除所有 Warning 和 console 输出

## 5.1 可直接采用的部分

以下裸输出应优先清理：

* `printfn "DEBUG: ..."`
* 普通能力探测失败；
* provider API 不存在；
* 缓存未命中；
* 正常降级；
* 正常 fallback 分支选择。

`OpencodeContextBudgetObservation` 当前确实直接输出 provider list 不可用和 provider.list 失败信息。

## 5.2 不能简单全局删除所有 WARNING

部分警告代表真实状态损坏，例如：

* event log 无法写入；
* schema 不完整；
* lifecycle 与 gate 状态矛盾；
* Cancelled 后仍发送了 continuation；
* 下游工具执行状态未知。

这些信息需要保留，但应进入统一结构化 logger。

建议分级：

* Trace：正常分支、缓存、能力探测；
* Debug：仅开发模式可见；
* Warn：发生降级或不变量受损，但可以继续；
* Error：可能导致状态或数据错误；
* Fatal：无法保证安全执行。

## 5.3 stdout 必须与协议输出隔离

对于 slave、subprocess、JSON-RPC、MCP 等可能使用 stdout 传输协议的模块：

* stdout 只能输出协议数据；
* 诊断日志写入 stderr、文件或 host logger；
* 默认关闭 trace/debug；
* 日志必须限频和去重。

环境变量门禁可以采用，但最好由统一配置读取一次，不能每个模块自创一个环境变量和输出格式。

---

# 六、Compaction/Fallback 期间 Nudge：运行时门禁值得采用，但不能侵入纯推导核心

## 6.1 补充分析正确指出了门禁缺失

Fallback 正在执行以下状态时，普通 todo/review nudge 不应派发：

* Retrying
* Scanning
* ScanningToolCallText
* RecoveringToolCallText
* EventHandlingActive
* AwaitingBusy
* SubsessionPending
* BusyCount 大于零

这是非常实用的近期止血方案。

## 6.2 不应让 `Kernel.NudgeDerivation.deriveAction` 直接依赖 FallbackRuntime

`deriveAction` 目前是纯函数：

* 输入 snapshot；
* 输出 `NudgeAction`。

直接把可变 runtime 注入内核会造成：

* Kernel 反向依赖 Shell；
* 单元测试变复杂；
* host-specific 状态进入纯领域层；
* 重放结果可能依赖当前内存状态。

更合理的方式是：

1. Shell/host adapter 读取 fallback observation；
2. 将其转换成统一的 `NudgeBlockReason` 或 `NudgeBlockStatus`；
3. 构造 snapshot；
4. `deriveAction` 继续只根据 snapshot 判定。

当前 snapshot 已经有 `blockStatus`，应扩展其表达能力，而不是破坏层次边界。

建议从二值状态扩展为原因枚举，例如：

* Allowed
* UserCancelled
* FallbackActive
* CompactionActive
* SyntheticTurn
* PendingDelivery
* RunnerOwnsTurn
* DuplicateAnchor
* UnknownTerminalOrigin

## 6.3 只检查 Fallback 还不能解决 Compaction 误触发

Compaction 完成时，fallback 可能已经完全 Idle，所有 fallback gate 也可能为 false。

如果系统继续向前寻找“最近一条非 synthetic assistant”，仍可能拿到压缩前旧消息并触发 nudge。

因此还必须引入：

* compaction started/completed 状态；
* context generation；
* terminal event origin；
* synthetic turn provenance。

普通 nudge 只允许由 `HumanTurnCompleted` 触发。

以下来源必须跳过：

* CompactionCompleted
* FallbackContinuationCompleted
* TitleGenerationCompleted
* NudgeCompleted
* HumanTurnAborted
* UnknownLegacyStop

## 6.4 修正 Phase 条件表述

不能写成含糊的“Phase 不等于 Idle 或 Exhausted”。

建议明确：

* Phase 属于 Retrying/Scanning/ScanningToolCallText/RecoveringToolCallText：阻止；
* Phase 为 Idle：仍需检查 owner、gates 和 terminal origin；
* Phase 为 Exhausted：必须等 fallback episode 明确 settled 后再决定；
* Lifecycle 为 Cancelled/TaskComplete：直接阻止 nudge。

---

# 七、Nudge 模型路由：统一模型 decoder 是非常值得采用的修复

## 7.1 已确认的解析缺陷

`Opencode/NudgeEffect.collectSnapshot` 当前从 assistant info 中读取 model 时，只接受：

* `providerID`
* `modelID`

组成的对象。

如果 model 是 `"openai/gpt-4o"` 一类字符串，局部解析会返回 `None`。

仓库中已经存在 `FallbackMessageCodec.decodeModelFromObj`，能够同时处理字符串和对象，因此不应再编写第二套不完整解析器。

应当统一：

* `collectSnapshot`
* last user model 提取；
* last assistant model 提取；
* fallback route 恢复；
* provider limit 模型提取；

全部调用同一 decoder 或同一 model identity codec。

## 7.2 当前优先级确实会让旧 injected model 覆盖新用户模型

当前 `resolveNudgeModel` 对普通 user message 的处理顺序大致是：

1. `fallbackRuntime.GetInjectedModel`
2. 当前 user message model
3. fallbackRuntime.GetModel
4. assistant/default model

这意味着 session 中只要残留 injected model，它就可能覆盖后一条真人消息显式选择的模型。

短期应改为：

1. 最近一条确认是真人的、非 nudge、非 fallback synthetic message 的显式模型；
2. 当前 human turn 保存的 routing context；
3. session/agent 默认；
4. 最后非 synthetic assistant model，仅作为兼容回退。

## 7.3 Fallback model 不应成为普通 Nudge 的默认第二优先级

补充分析建议把“最新活跃 fallback 模型”作为第二优先级。这只在 nudge 本身属于同一个 fallback continuation 时合理。

但正确架构中：

* fallback 活跃时，普通 todo/review nudge本就不应发送；
* fallback settle 后，旧 fallback model 不应覆盖当前真人轮次 route。

所以应区分：

* Fallback continuation：使用当前 fallback attempt route；
* Todo/review nudge：使用当前 human turn route；
* 无 current human turn：使用明确默认，而不是陈旧 injected route。

## 7.4 OMP 显式传递模型的建议可以采用

OMP 当前 `sendMessage` 主要传递：

* `customType`
* `content`
* `display`
* `triggerTurn`
* `deliverAs`

模型并没有从 snapshot 显式传递给主机。当前 OMP event log 中保存的模型还是最后 assistant model，而不是最后真人消息 model。

应完成两件事：

1. snapshot 模型来源改为当前 human turn；
2. 按 OMP 主机真实支持的 API 字段显式传递 route。

模型应进入主机认可的 message/options 配置字段，不能只塞进 `customType` 自定义对象后假设主机会使用。

验收必须检查实际发送请求，而不只是内存 snapshot：

* provider；
* model；
* variant；
* reasoning effort；
* thinking 配置；

均与当前 human turn 一致。

---

# 八、Review Nudge：Snapshot 透传建议正确，但 front matter 字段必须改为 `original_task`

## 8.1 已确认的丢失位置

当前系统已经能够从 event log 折叠得到：

* `ReviewLoopFold.Active task`
* `foldReviewTask`
* `NudgeSnapshotState.reviewLoop`

但 `deriveSnapshot` 构造 `SessionSnapshot` 时只保留：

* todos；
* last assistant message；
* work state；
* block status；
* anchor；
* agent；
* model。

原始 task 在这里被丢弃。

随后 `loopNudgePromptFor` 只接收 todos，因此生成的 front matter 中没有原始任务。

为 `SessionSnapshot` 增加 review task 是正确且直接的修复。

## 8.2 不得在 Review Nudge 中输出 `task`

补充分析建议输出：

```yaml
task: <task_content>
```

这一点必须修正。

仓库已经明确区分：

* `task`：Worker With-Review 激活字段；
* `original_task`：Reviewer、double-check 和后续评审上下文使用，避免重放时错误激活 review loop。

如果 review nudge 再次输出 `task`，重启重放或 front-matter fold 可能把它解释为一次新的 loop activation。

正确字段应为：

```yaml
original_task: <完整原始任务>
```

同时可加入：

```yaml
prompt_origin: review_nudge
review_loop_id: ...
review_round: ...
```

其中 `prompt_origin` 不应参与 loop 激活折叠。

## 8.3 推荐的数据结构调整

建议在 snapshot 中新增：

* `reviewTask: string option`
* `reviewLoopId: string option`
* `reviewRound: int option`
* `latestReviewFeedback: string option`

`selectNudgePrompt` 在处理 `NudgeLoop` 时调用新的 review 专用模板，输入完整的 review context，而不是只传 todos。

## 8.4 活跃 loop 缺 task 时必须 fail closed

若满足：

* work state 表明 review loop active；
* 但 `reviewTask=None` 或空字符串；

则：

1. 不发送 review nudge；
2. 重新从 event log 折叠；
3. 若仍无法恢复，记录 invariant violation；
4. 不允许退回无任务的普通 loop prose。

## 8.5 Compaction 白名单也要复核

当前 compaction front-matter 白名单包括：

* `task`
* `verdict`
* `double-check`
* `squad_event`

但没有 `original_task`。

如果 reviewer 或 review continuation 的上下文需要跨 compaction 保存，应将 `original_task` 纳入白名单，或由 ReviewProjection 在 compaction 后重新注入。

不能只修 nudge 模板，而让 compaction 在更早阶段再次丢弃原任务。

---

# 九、建议吸收后的最终实施清单

## P0：立即止血

1. `Cancelled` 与 `TaskComplete` 同级拦截所有 fallback gate。
2. Abort 原子清理全部 fallback/nudge gate 和 BusyCount。
3. Compaction、fallback continuation、abort 结束事件禁止触发普通 nudge。
4. Review nudge 携带 `original_task`。
5. Opencode model 解析改用统一 decoder。
6. 删除默认控制台 `DEBUG:` 输出。

## P1：收拢边界

1. 建立统一 Control Field Policy。
2. 建立不可绕过的工具执行网关。
3. 删除 OMP、本地 wrapper 中重复的 warn 校验实现。
4. schema 最终导出前执行全目录完整性检查。
5. validation error 作为 tool rejection，不产生 session error。
6. 把 Fallback/Compaction 状态转换成 snapshot block reason。

## P2：重建单一事实源

1. 引入 HumanTurnRoutingContext。
2. 引入 continuation ID 和 cancel generation。
3. 让 injected model 绑定 attempt/turn，而不是 session 全局。
4. Context Budget 引入 Bootstrap estimator 和 limit provenance。
5. `R` 改为当前 phase 内 todo anchor 数。
6. NudgeTrack 真正参与 episode 防重。
7. ReviewProjection 持久保存 original task 和 round。

---

# 十、补充回归测试清单

## Esc/Fallback

* Cancelled 与每一种 active gate 的笛卡尔组合；
* Abort 与 fallback dispatch 同时发生；
* Abort 后迟到 busy/idle；
* Abort 后新真人消息恢复；
* 旧 injected message 晚于新真人消息到达；
* 重启重放旧 injection 事件。

## Warn/TDD

* 所有 host、alias 和动态工具的 schema；
* 缺字段时 execute 次数为零；
* 合法字段执行后下游参数不存在控制字段；
* 验证失败不产生 fallback；
* wrapper 直接调用 execute 仍无法绕过；
* after hook 检测到 before 被绕过时只告警，不假装已阻止。

## Context Budget

* 首轮无 usage 数据；
* provider/model limit 获取失败；
* 中文、英文、代码和 JSON 混合上下文；
* 小上下文模型的默认上限；
* 当前 phase 与历史 phase 的 todo 数隔离；
* 同一 episode 防止重复注入；
* todowrite 失败不推进 phase；
* compaction 后重建基线。

## Nudge

* HumanTurnCompleted 才允许普通 nudge；
* CompactionCompleted 不触发；
* FallbackContinuationCompleted 不触发；
* Nudge 自己完成不递归触发；
* 当前真人模型覆盖旧 fallback injected model；
* 字符串和对象模型格式等价；
* OMP 实际发送请求包含正确 route。

## Review

* Active task 无 todos；
* Active task 有 todos；
* needs revision 后继续；
* WIP 后继续；
* compaction 前后；
* 进程重启后；
* task 包含多行和 YAML 特殊字符；
* review nudge 只包含 `original_task`，不重新生成激活字段 `task`；
* active loop 缺 task 时零发送。

---

# 十一、最终评价

补充分析最有价值的地方，是指出了几个可以直接落到具体文件和具体函数的缺陷：

* `needFallbackContinue` 漏掉 `Cancelled`；
* model string 解析不完整；
* Context Budget 无首轮估算；
* review task 在 snapshot 转换时丢失；
* 裸 DEBUG 污染协议输出。

这些内容应并入近期修复。

但最终方案仍应坚持上一版提出的架构方向：

* 时间戳不是消息身份；
* flag 集合不是 continuation 所有权；
* 最近消息不是当前 human turn；
* before hook 不是不可绕过执行边界；
* `task` 和 `original_task` 不能混用；
* 普通异常不能承担工具拒绝协议；
* Runtime 状态不能直接侵入纯领域推导。

近期补丁负责立即止血，最终修复则必须落到 provenance、generation、owner、routing context、统一执行网关和可重放 projection 上。只有两层同时完成，七个问题才不会在不同 host 或不同事件顺序下再次出现。

---

# 附件：增补稿 II

## ——关于 Abort 空输出、执行前终极净化、Compaction 代次与模型路由单一事实源的进一步说明

## 一、补充评审结论

第二份分析提出了四个值得重点吸收的新视角：

1. **Abort 可能经过“空输出”路径被二次分类**，因此必须审查事件翻译与 idle 补偿逻辑，而不能只看 Fallback 状态机。
2. **控制字段即使在 before hook 中被删除，也可能因 Host 克隆或重新序列化参数而重新出现**，所以执行前需要最后一道净化边界。
3. **Compaction anchor 可以作为近期识别 synthetic continuation 的辅助信号**。
4. **Fallback Action 真正发送之前还应进行一次取消状态校验**，防止状态机决策与异步发送之间发生竞态。

不过，核对仓库后也发现，这份分析对部分现状判断并不完全准确：

* `FallbackEventBridge` 目前已经在创建 `EmptyOutputError` 之前检查最后一条 assistant 是否带有 abort 信息。
* `classifyError` 已经把 Abort 放在认证错误、重试次数等判断之前；`handleSessionError` 也会在识别 Abort 后将生命周期设为 `Cancelled`。
* Opencode JSON Schema 的注入逻辑已经会更新 `required`，并非只增加 `properties`。
* `FallbackRuntimeState.GetModel` 不能成为普通 nudge 模型的绝对 SSOT，因为它本质上是运行期缓存，而且当前代码在识别新用户消息时会主动清除该模型。
* Review nudge 应携带 `original_task`，而不是可能重新触发 review 激活语义的 `task`。

因此，本增补稿的核心原则是：

> 吸收其新增的边界防御思想，但不把运行期缓存、文本 anchor、物理时间或 Host 消息历史提升为最终单一事实源。

---

# 二、问题一增补：Abort 与 EmptyOutput 的真实缺口不在状态机主干，而在“翻译完整性”和“决策后发送竞态”

## 2.1 仓库中已经存在的正确防线

当前 `FallbackEventBridge.handleEvent` 在处理 `session.idle` 时，顺序大致为：

1. 若当前 lifecycle 已为 `Cancelled`，只生成普通 `SessionIdle`；
2. 否则拉取消息历史；
3. 调用 `tryGetLastAssistantAbortInfo`；
4. 若识别到 Abort，则生成带 abort 信息的 `SessionError`；
5. 只有未识别到 Abort，且最后一次输出既无内容又无工具时，才创建可重试的 `EmptyOutputError`。

因此，“所有 Abort 空输出都会直接被误判成 EmptyOutput”并不是当前代码的准确描述。仓库已经具有一层防护。

Kernel 状态机方面也已经实现：

* `errorInputIsAbort` 识别 `MessageAborted`、`ClientCancellation`、`AbortError` 和 `MessageAbortedError`；
* `classifyError` 在其他错误分类之前处理 Abort；
* `handleSessionError` 收到 Abort 类型的 Ignore 后，把生命周期改成 `Cancelled` 并返回 `DoNothing`。

所以真正需要修复的不是重新实现上述逻辑，而是补齐它们尚未覆盖的边界。

## 2.2 真实缺口 A：Abort 翻译可能不完整

`tryGetLastAssistantAbortInfo` 只能识别已经被 Host 写入 assistant message metadata 的 Abort。

下列情况仍可能丢失 Abort 语义：

* Host 只发送 `session.interrupted`，但不写 assistant metadata；
* stream 在生成 assistant message 之前被中止；
* Host 使用新的错误名称；
* Abort 信息位于 event status、cause 或嵌套 error 中；
* OMP、Opencode、Mux 对同一类取消事件的字段编码不同；
* Abort event 先到，idle event 后到，但后者无法再从消息中恢复原因。

因此，值得吸收第二份方案关于“事件翻译层统一 Abort”的建议。

所有 Host translator 应把以下语义归一化为同一种领域事实：

* 用户取消；
* 客户端取消；
* stream abort；
* session interrupted；
* AbortError；
* MessageAbortedError；
* SDK cancellation token；
* 主机明确的 stop-by-user。

归一化结果必须包含：

* `DomainError = MessageAborted` 或明确的 ClientCancellation；
* `IsRetryable = false`；
* cancel generation；
* 原始 Host reason code；
* 当前 human turn ID。

不能仅依赖错误名字字符串。

## 2.3 真实缺口 B：状态机已经返回 DoNothing，不代表旧 SendContinue 不会晚到

典型竞态如下：

1. 错误事件使状态机决定 `SendContinue`；
2. Action 已离开纯状态机，进入 Promise 或 Host 调用队列；
3. 用户此时按 Esc；
4. lifecycle 被改成 `Cancelled`；
5. 先前排队的 `SendContinue` 仍然调用 Host prompt。

因此，第二份方案提出“执行 SendContinue 前再检查一次 Cancelled”非常值得吸收。

但单纯再次读取 `state.Lifecycle` 仍不够，因为：

* 读取后和真正发送之间仍有极短竞态；
* 读取的 state 可能是旧快照；
* 新人类轮次可能已经开始，lifecycle 又恢复为 Active；
* 旧 Action 可能因此误投到新轮次。

最终校验应同时比较：

* session generation；
* human turn ID；
* cancel generation；
* continuation ID；
* continuation owner；
* lifecycle；
* Action 是否仍处于 pending 状态。

只有这些身份全部匹配，才允许真正调用 Host prompt。

换言之，Action 不应只是“发送模型 M”，而应是：

> 为 human turn T、generation G、continuation C 执行一次仍然有效的 continuation。

## 2.4 真实缺口 C：Cancelled gate 仍需统一清理

即使 Kernel 状态机正确进入 Cancelled，外层 gate 中仍可能残留：

* EventHandlingActive；
* AwaitingBusy；
* SubsessionPending；
* BusyCount；
* Consumed；
* NudgeActive；
* pending recovery；
* pending prompt delivery。

这些字段若没有原子清理，仍可能使 `needFallbackContinue` 或其他等待逻辑认为会话尚未结束。

所以应保留上一份增补稿的结论：

* `Cancelled` 与 `TaskComplete` 必须成为所有 gate 计算的最高优先级终止状态；
* Abort 处理必须通过统一的 session cancellation 操作清理所有派生状态；
* 不能依赖各个 finally 分支自行恢复一致性。

## 2.5 建议新增的双层防线

第一层是决策层：

* Cancelled 不再产生新的 Action。

第二层是效果执行层：

* 已经产生但尚未发送的旧 Action，在发送前执行有效性校验；
* 校验失败时记录 `continuation_invalidated`，而不是静默发送或重新分类为错误；
* 被取消的 Action 不得产生 EmptyOutput、RetrySame 或下一次 fallback。

## 2.6 新增验收场景

必须增加如下竞态测试：

* 状态机返回 `SendContinue` 后、Host prompt 调用前按 Esc；
* Host prompt Promise 已建立但未实际发送时按 Esc；
* Abort event 没有 assistant message；
* Abort metadata 晚于 idle event；
* idle 先被判定为空输出，随后收到 abort；
* 新真人消息已经开启新 turn，但旧 Action 仍在队列；
* lifecycle 恢复 Active，但 continuation generation 已过期；
* 同一 Abort 被多个 Host event 重复报告时，只取消一次，不重复发副作用。

---

# 三、问题二增补：Schema “required”并非完全缺失，真正高价值建议是“早验证、晚净化、执行前断言”

## 3.1 当前 Schema 实现的实际情况

Opencode 的 JSON Schema 注入已经包含：

* 为 `properties` 增加 `warn_tdd`；
* 将 `warn_tdd` 追加到 `required`；
* 为 `warn`、`warn_reuse` 执行类似操作；
* 对非 JSON Schema shape 使用 required enum schema。

因此不能把问题简单归因于“只写 properties、没有更新 required”。

仍需要重点检查的是：

* Zod/Effect Schema 转换后的最终 JSON Schema 是否保留约束；
* Host 是否实际使用改写后的 schema；
* tool definition hook 是否覆盖所有工具；
* alias 和动态工具是否在 hook 之后才注册；
* 某些 wrapper 是否替换了 parameters/jsonSchema；
* Mux、OMP 是否共享同一工具能力分类。

所以应增加“最终导出 schema 审计”，而不是再次实现一套 append-required。

## 3.2 Host 参数克隆问题值得高度重视

第二份分析指出：

> before hook 删除的 args，不一定就是实际 execute 收到的 args。

这是非常重要的边界风险。

Host 可能在 hook 前后进行：

* 深拷贝；
* JSON 序列化和反序列化；
* 参数规范化；
* wrapper 重建对象；
* tool call history 与 execute args 分流；
* 从原始 tool call 再次解码。

因此，“在 before hook 中执行过 `Dyn.deleteKey`”不能证明下游参数已经净化。

## 3.3 推荐采用三阶段处理

### 阶段一：Schema 强制

目标是让 LLM 尽可能正确生成。

此阶段必须保证：

* 字段存在；
* 字段进入 required；
* 只能接受 canonical value；
* 所有 alias 和动态工具被覆盖；
* 启动时检查最终 schema。

### 阶段二：入口验证

目标是在任何副作用发生前拒绝非法调用。

这里验证：

* 适用工具是否提供必需字段；
* 值是否正确；
* 不适用工具是否携带了意外控制字段；
* amend 等字段是否满足组合规则。

验证失败应返回正常的“工具调用被拒绝”结果：

* `executionStarted=false`；
* 不产生 session error；
* 不触发 fallback；
* 不调用真实工具。

### 阶段三：执行前终极净化

目标是抵御 Host 克隆和重建。

在最靠近真实副作用的统一 wrapper 中：

1. 从本次实际收到的 execute args 创建业务参数副本；
2. 再次删除全部控制字段；
3. 检查删除后的对象；
4. 若仍发现保留字段，立即拒绝执行；
5. 把控制信息放入独立的 `ControlEnvelope`；
6. 真实工具只能看到净化后的业务参数。

这里可吸收第二份方案中 `stripAllWarnFields` 的思想，但不应只做一个无条件删除函数。

更准确的职责应包括：

* validate；
* extract；
* strip；
* assert clean；
* audit。

## 3.4 “最靠近 execute”不等于指定某一个现有 wrapper

第二份方案提到 `Mux/Wrappers.mkResultWrapper`。这可能是某条路径的合适位置，但不能先假设它覆盖所有工具。

在实施前必须绘制各 Host 的实际调用图：

* 原生工具；
* 插件工具；
* Mux 代理工具；
* Subagent 转发工具；
* PTY；
* 文件修改；
* executor；
* MCP；
  -动态注册工具。

应找到每条路径真正不可绕过的最后公共边界。如果不存在单一公共边界，就需要在少数几个 adapter 上放置同一个共享 sanitizer，而不是复制不同规则。

## 3.5 建议增加净化后断言

在开发和测试构建中，真实 execute 之前应断言业务参数中不存在：

* warn；
* warn_tdd；
* warn_reuse；
* amend；
* `_ui`，若它只属于 UI；
* 未来新增的控制字段。

这条断言比“我们调用过 deleteKey”更有证明力。

## 3.6 对深拷贝问题的专项测试

应建立一个模拟 Host：

1. before hook 收到对象 A；
2. Host 在 hook 前保存 A 的深拷贝 B；
3. before hook 删除 A 中的控制字段；
4. Host 把 B 传给 execute；
5. 最终 wrapper 必须再次净化 B；
6. 真实工具收到的对象不得包含控制字段。

同时测试：

* 浅拷贝；
* 深拷贝；
* JSON round-trip；
* args wrapper 重建；
* alias 重新解码；
* nested args；
* frozen object；
* null prototype object。

---

# 四、问题三增补：遍历最后一个有效 usage 值值得采用，但不能把 usage 快照当作可累加事件

## 4.1 Token 探针的改进方向正确

如果当前实现只检查最后一条 assistant，而最后一条：

* 没有 tokens；
* 是 compaction/title synthetic message；
* SDK 字段结构不同；
* usage 尚未写回；

就可能得到 `None`。

因此可以采用以下策略：

* 从后向前查找最后一个有效 usage observation；
* 跳过 synthetic assistant；
* 同时支持多种 SDK 字段结构；
* 校验数值大于零且在合理范围；
* 记录该 observation 对应的 message ID 和 context generation。

## 4.2 不得简单把多个 message usage 相加

必须先确认 Host 的 token 字段语义：

* 是单条消息 token；
* 是本次 request 总输入；
* 是累计 session usage；
* 还是缓存命中明细。

如果每条 assistant 的 `input` 已经代表该轮完整上下文，则遍历历史后只能选择最后一个有效值，不能把多个值相加，否则会严重重复计算。

同理，`cache.read` 是否应加入 input，要依据 Host 语义：

* 如果 `input` 不含 cache read，则应加入；
* 如果 `input` 已经是总输入，重复加入会高估；
* 如果字段是 cost accounting，而不是 context occupancy，也不能直接使用。

因此应把 usage decoder 写成带来源和语义的结果：

* input tokens；
* cache read tokens；
* total effective context tokens；
* source format；
* whether cumulative；
* confidence。

## 4.3 当前无数据停摆问题仍需修复

当前 `resolveCurrentTokens` 在实时值和历史比例估算都不可用时返回 `None`，预算机制随即跳过。

所以 Bootstrap estimator 必须加入。

但安全估算不能只写“UTF-8 字节数除以四”。建议：

* 英文自然语言采用较宽松比例；
* 中文、JSON、代码采用更保守比例；
* 无法分类时取更高 token 密度；
* 为估算值增加安全系数；
* 获得真实 usage 后动态校准；
* 在日志中标记 measurement source。

## 4.4 固定 120K 默认上限不宜采用

第二份方案把默认值从 100K 改成 120K，本质问题没有改变。

未知模型可能只有 8K、16K 或 32K。错误使用 120K 会让 nudge 迟到数倍。

正确优先级仍应是：

1. 当前 model 的精确 limit；
2. session 中最近成功解析的同模型 limit；
3. provider/model family 的保守下限；
4. Host 配置默认；
5. 全局保守默认。

默认值应偏小而不是偏大，并明确记录其 provenance。

## 4.5 `work_backlog_committed` 应触发失效和重建，而不是用旧测量直接 beginPhase

第二份方案提出：

> todowrite 成功后立即调用 beginPhase，重置 phaseBaseTokens。

思想是正确的：成功 todowrite 必须成为明确的 phase boundary。

但 event 到达时未必具有“最终变换后上下文”的准确 token 值。如果直接使用旧 token measurement 调用 beginPhase，可能建立错误基线。

更安全的流程是：

1. 收到 `work_backlog_committed`；
2. 增加 todo checkpoint ordinal；
3. 标记 budget phase 为 `RebaseRequired`；
4. 清除当前 EmergencySignaled episode；
5. 下一次 message transform 对稳定投影重新编码；
6. 获取或估算新的 token；
7. 再建立新的 phase base。

当前实现已经会在 backlog 与 `LastBacklog` 不同时重建 phase state，并更新 store。

所以需要新增的重点不是再写一次 beginPhase，而是保证：

* event 到达后缓存必然失效；
* todo checkpoint ordinal 正确推进；
* 下一次 transform 不会返回旧缓存；
* phase 重建使用当前 context generation；
* `R` 只计算当前 phase 内 checkpoint。

## 4.6 `NudgeTrack` 语义仍需补齐

当前代码设置 `EmergencySignaled`，但 pressure 满足时仍会再次追加 synthetic nudge。

必须明确：

* 同一个 budget episode 是否每轮都重新注入；
* Host 是否每轮会自动剥离 synthetic message；
* “发送过”与“当前 outbound prompt 包含”是否是同一状态；
* todowrite 成功后如何清除；
* compaction 后如何重置。

如果 Host 确实每轮剥离 synthetic nudge，那么 `EmergencySignaled` 不能简单代表“以后永不注入”，但至少应该区分：

* 上次 prompt 已包含；
* 本次 transform 需要重新附加；
* 已经观察到模型开始响应；
* 已成功执行 todowrite；
* 已超过最大催促次数。

---

# 五、问题四增补：统一日志出口值得采用，但“console.log 一定同步阻塞”不应作为主要论据

## 5.1 应吸收的部分

以下原则正确：

* 业务模块不得直接输出 `DEBUG:`；
* stdout 与协议输出必须隔离；
* Slave、MCP、JSON-RPC 等程序的 stdout 只能输出协议数据；
* 调试日志必须受统一配置控制；
* `OpencodeContextBudgetObservation` 中裸打印应移除或接入 logger。

## 5.2 需要校正的部分

“Node.js 中所有 console.log 都会同步阻塞事件循环”是过度概括。

其具体行为可能取决于：

* 输出目标是终端、文件还是 pipe；
* Node 版本；
* 平台；
* stream 实现。

因此不要把性能问题建立在这一绝对判断上。

日志清理的主要理由应是：

1. 污染 Host 协议；
2. 破坏机器解析；
3. 泄露内部状态或用户内容；
4. 造成高频 I/O；
5. 缺少 severity、结构和限频；
6. 不利于测试和生产观测。

## 5.3 不建议删除所有 Semble trace 调用

若 trace 已经：

* 有环境变量门禁；
* 写入独立 sink；
* 默认关闭；
* 不泄露敏感数据；

则可以保留。

应该删除的是“绕过统一 logger 的裸输出”，而不是删除所有诊断能力。

## 5.4 统一日志规范增补

建议 logger 支持：

* subsystem；
* event code；
* severity；
* session hash；
* human turn ID；
* continuation ID；
* context generation；
* Host；
* degraded flag；
* rate-limit key。

默认禁止记录：

* 完整 prompt；
* 工具业务参数；
* 文件内容；
* API token；
* 用户原始任务全文；
* 模型隐藏推理；
  -绝对私密路径。

## 5.5 启动期静态检查

可以增加构建期或 CI 检查，禁止生产模块新增：

* `printfn "DEBUG`
* `console.log`
* 直接 stdout write；
* 无 logger 包装的 stderr write。

测试模块和明确的 CLI 用户输出需要列入白名单。

---

# 六、问题五增补：Compaction anchor 可作近期识别手段，但不能作为最终状态

## 6.1 Anchor 检测确实有现成基础

仓库已经定义：

* `compactionAnchorBody`
* `hasCompactionAnchorPrompt`
* compaction front-matter 提取和重建机制。

因此，在近期补丁中利用 anchor 辅助识别 compaction continuation，具有较低实施成本。

## 6.2 不应只检查 `lastAssistantMessage.Contains(compactionAnchorBody)`

Compaction anchor 很可能是：

* synthetic user message；
* system message；
* Host 自定义 message；
* 经过重写后的 front-matter message；

而不一定是最后一条 assistant message。

如果只在 `NudgeDerivation` 中查看 `lastAssistantMessage`，可能根本看不到 anchor。

正确的近期补丁应在 Host adapter 收集 snapshot 时识别：

* 最后终止事件来源；
* 最近 synthetic prompt origin；
* compaction anchor 是否存在；
* compaction 是否刚完成；
* 是否正在 auto-continue。

然后转换成 `NudgeBlockReason.CompactionActive` 或类似状态。

纯 `deriveAction` 仍只消费结构化 snapshot，不负责扫描 Host 原始消息。

## 6.3 `compaction_occurred` 事件值得采用，但不要使用模糊的“短暂时间窗口”

采用持久事件记录 compaction 是正确方向。

但“设置一个短暂 Blocked 窗口”存在问题：

* 机器快慢不同；
* 网络延迟不同；
* auto-continue 可能比窗口长；
* 时间到了但 continuation 尚未 settle；
* 重启后难以恢复剩余窗口；
* 测试不稳定。

应使用状态窗口，而不是物理时间窗口：

1. `compaction_started`
2. `compaction_completed`
3. `compaction_continuation_started`
4. `compaction_continuation_settled`

从 started 到 settled 期间，nudge 一直 Blocked。

或者至少使用：

* context generation；
* continuation owner；
* compaction episode ID；
* settled 标志。

## 6.4 Anchor 只能辅助迁移

文本 anchor 有以下局限：

* 文案未来可能改变；
* 用户可能自己输入相同文本；
* compaction prompt 可能被截断；
* front matter 可能被重新编码；
* 不同 Host 使用不同 wording。

所以最终系统不能根据一句英文文本决定 continuation 身份。

Anchor 的正确地位是：

* 旧历史兼容；
* 调试；
* 迁移期补充证据。

最终身份仍来自 provenance 和 event projection。

## 6.5 SessionGateDemand 优先级值得统一

Compaction、fallback、review、todo、runner 之间不应分别在自己的 hook 中判断“现在能不能发”。

建议统一 gate 优先级：

1. UserCancelled
2. TaskComplete
3. FallbackOwner
4. CompactionOwner
5. ContextBudgetOwner
6. ReviewOwner
7. TodoOwner
8. Runner reminder

每个 session 同时只能有一个能发送 synthetic prompt 的 owner。

---

# 七、问题六增补：`FallbackRuntimeState.GetModel` 不能成为模型绝对 SSOT

## 7.1 第二份方案正确指出当前模型优先级存在问题

当前 `resolveNudgeModel` 会先查找最后 user message，但对于普通 user message，其内部优先级是：

1. `GetInjectedModel`
2. user message model
3. `GetModel`
4. assistant/default model

这会造成旧 fallback injected model 覆盖新真人消息模型。

这一问题必须修复。

## 7.2 但把 `GetModel` 提升为第一 SSOT 也不正确

第二份方案主张：

> Model 的 SSOT 是 FallbackRuntimeState 中的 models Map。

这与其实际生命周期并不一致。

当前 `FallbackEventBridge` 在识别 `NewUserMessage` 时会：

* 清空 fallback chain；
* 调用 `runtime.ClearModel sessionID`；
* 重置 fallback state。

这说明 runtime model 至少在当前实现中是 fallback 运行期缓存，不是稳定的当前用户轮次路由事实。

它还可能存在：

* 尚未捕获新模型；
* session.updated 事件缺失；
* 旧事件晚到；
* fallback attempt model 覆盖用户选择；
* 重启后内存丢失；
* 多个 child session 混淆；
* model 存在但 variant、reasoning effort 等信息不完整。

因此不能把它绝对化为 SSOT。

## 7.3 正确的模型事实应分成三类

### Human Turn Route

表示用户当前轮次明确选择的：

* provider；
* model；
* variant；
* agent；
* reasoning effort；
* thinking；
* sampling 设置。

普通 todo/review nudge 使用这个事实。

### Fallback Attempt Route

表示某个 fallback continuation attempt 临时使用的模型。

只有 fallback 自己发送 continuation 时使用。

### Observed Host Session Route

表示 Host 当前报告的 session active model。

它是有价值的观测来源，但必须带：

* observation event ID；
* generation；
* observedAt；
* source；
* confidence。

它可以帮助填补缺失信息，但不能无条件覆盖更明确的 Human Turn Route。

## 7.4 建议的模型选择顺序

对于普通 todo/review nudge：

1. 当前 human turn 的显式 route；
2. 与当前 human turn、当前 generation 匹配的 Host active route；
3. 当前 session/agent 配置默认；
4. 最后非 synthetic assistant route，作为兼容兜底。

对于 fallback continuation：

1. 当前 continuation attempt route；
2. 当前 human turn route；
3. fallback chain 默认。

对于不存在 current human turn 的恢复场景：

* 从 EventLog 恢复 HumanTurnRoutingProjection；
* 无法恢复时使用明确默认；
* 不使用无作用域的 stale injected model。

## 7.5 `session.updated` 和 `session.busy` 捕获模型仍值得采用

第二份方案提出在 Host event 中及时调用 `SetModel`，这个方向有价值，但需要升级为版本化 observation。

不能只做：

* `sessionID -> model`

而应记录：

* session ID；
* human turn ID；
* event sequence；
* session generation；
* source event；
* route；
* observed timestamp。

旧 generation 的 session.updated 晚到时必须丢弃。

## 7.6 实时 Host 查询的限制

Host API 实时查询可以作为补充，但必须考虑：

* 查询结果可能是 session 默认模型，不是当前 message override；
* auto-continue 可能暂时修改 active route；
* 查询与发送之间仍有竞态；
* API 不一定返回 variant；
* child/reviewer session 可能有独立 route。

所以实时查询不能简单排在所有消息和投影之前，而应与当前 human turn identity 做一致性校验。

---

# 八、问题七增补：原任务应从 ReviewLoop 投影透传，避免再复制一个可能漂移的字段

## 8.1 任务丢失点判断正确

当前 `loopNudgePromptFor` 只接收 todos，并不会携带原始任务。

当前 Nudge snapshot fold 中又已经保存 `reviewLoop`，而 `ReviewLoopFold.Active task` 本身包含任务。

因此 review task 并非没有持久化，而是在：

> ReviewLoopFold → NudgeSnapshotSource → SessionSnapshot → loopNudgePromptFor

这条转换链中丢失。

## 8.2 不一定需要在 `NudgeSnapshotState` 再复制 `originalTask`

第二份方案建议给 `NudgeSnapshotState` 增加 `originalTask`，并在 loop activated 时单独更新。

这能工作，但会制造两个需要保持一致的字段：

* `reviewLoop = Active task`
* `originalTask = Some task`

以后处理：

* loop cancellation；
* replay；
* task replacement；
* malformed activation；
* version migration；

都要同步维护二者。

更符合类型防错和单一事实源的做法是：

* task 继续只存于 `ReviewLoopFold.Active task`；
* `sessionSnapshotFromFold` 调用 `activeTask snap.reviewLoop`；
* 将结果写入 `SessionSnapshot.reviewTask`；
* prompt 生成只读取这个值。

只有在确有性能或序列化需求时，才增加派生字段，而且应在 fold 后计算，而不是作为独立可变状态维护。

## 8.3 Front matter 必须使用 `original_task`

第二份方案再次建议输出：

* `task: <原始任务>`

这一点仍然不能采用。

在本仓库语义中，`task` 与 review 激活密切相关，而 reviewer prompt、double-check prompt 已经采用 `original_task` 作为“原始要求”的语义字段。

Review nudge 应使用：

* `original_task`
* `prompt_origin = review_nudge`
* 可选 `review_loop_id`
* 可选 `review_round`
* 可选 `todos`

不能重新输出可能被 replay 逻辑解释为激活命令的 `task`。

## 8.4 Compaction 白名单必须同步调整

当前 compaction front-matter 白名单包括：

* task；
* verdict；
* double-check；
* squad_event。

没有 `original_task`。

如果后续 review nudge 使用 `original_task`，但 compaction 仅保留 `task`，原任务仍可能在压缩后丢失。

有两个可选方案：

### 方案 A：加入白名单

把 `original_task` 纳入 compaction whitelist。

优点是改造简单。

### 方案 B：压缩后由 ReviewProjection 重新注入

Compaction 不负责保留所有 review 字段；在构建 continuation prompt 时，从持久 ReviewProjection 重新加入 `original_task`。

该方案架构更干净，因为 review task 的生存不再依赖历史消息是否被保留。

长期更推荐方案 B，迁移期可以同时使用 A。

## 8.5 Review nudge 的完整上下文

建议 ReviewPromptContext 至少包含：

* original task；
* current round；
* latest feedback；
* affected files，若已经提交过；
* current todos；
* review loop ID；
* current human turn ID；
* prompt origin。

对于普通“提醒提交”场景，可以不全部渲染，但原始任务不得缺失。

## 8.6 Fail-closed 规则

若 review loop 为 Active，但 active task 为空：

* 不发送 review nudge；
* 尝试从 event log 重放恢复；
* 仍无法恢复则记录 projection corruption；
* 不能降级成只有“请调用 submit_review”的无任务 prompt。

---

# 九、对第二份方案“架构三铁律”的校正版

## 9.1 Model SSOT 校正

不应是：

> FallbackRuntimeState.models Map。

应拆分为：

* 普通用户轮次模型：HumanTurnRoutingProjection；
* fallback 模型：ContinuationAttemptProjection；
* Host 当前模型：ObservedSessionRoute；
* 内存 runtime map：上述投影的缓存或执行索引。

Runtime map 可以加速访问，但不能成为跨重启、跨竞态的权威事实。

## 9.2 Task SSOT 基本正确

原始 review task 应来自：

* `loop_activated` 持久事件；
* 由 ReviewLoopFold/ReviewProjection 恢复。

不能从 LLM 历史自然语言中重新猜测。

但 prompt 中应使用 `original_task` 语义，避免激活字段重放。

## 9.3 Abort 是硬中断的原则正确

但完整实现应覆盖三个层次：

1. Event translator：统一识别；
2. State machine：进入 Cancelled，不产生新 Action；
3. Effect executor：使旧 Action 失效。

只有前两层而没有第三层，仍无法阻止已经排队的 SendContinue。

## 9.4 Host args 不可信原则正确

但最终形式不只是“执行前再 deleteKey”，而是：

* 原始参数不可信；
* before hook 对象身份不可信；
* wrapper 输入也需验证；
* 下游只能接收新建的 sanitized object；
* 控制字段必须进入独立 ControlEnvelope；
* 真实 execute 前执行 clean assertion。

---

# 十、建议新增的跨问题统一对象

第二份方案进一步证明，以下几个跨模块对象值得正式引入。

## 10.1 TurnIdentity

包含：

* session ID；
* human turn ID；
* session generation；
* cancel generation；
* user message ID。

用于阻止旧 fallback、旧 nudge 和旧 model observation 污染新轮次。

## 10.2 ContinuationLease

包含：

* continuation ID；
* owner；
* turn identity；
* route；
* status；
* issuedAt；
* invalidation reason。

任何 synthetic prompt 发送前都必须验证 lease。

## 10.3 ControlEnvelope

包含：

* warn；
* warn_tdd；
* warn_reuse；
* amend；
* tool capability；
* validation result；
* audit fields。

它与净化后的业务参数彻底分离。

## 10.4 ContextBudgetObservation

包含：

* effective tokens；
* observation source；
* cache semantics；
* model limit；
* limit source；
* context generation；
* confidence；
* degraded reason。

防止把一个裸整数误认为绝对真实值。

## 10.5 ReviewPromptContext

包含：

* original task；
* loop ID；
* round；
* feedback；
* todos；
* prompt origin。

所有 review prompt 统一由它生成。

---

# 十一、实施顺序增补

## 第一批：可以立即落地的低风险修复

1. 审计所有 Host Abort translator。
2. `SendContinue` 和 `RecoverWithPrompt` 执行前增加 lifecycle 与 generation 校验。
3. 在真实 execute wrapper 前增加统一控制字段净化和 clean assertion。
4. Token usage 改为反向查找最后一个有效 observation。
5. 删除裸 `DEBUG:` 输出。
6. Compaction anchor 被识别时阻止普通 nudge。
7. `SessionSnapshot` 从 `ReviewLoopFold.activeTask` 获取原始任务。
8. Review nudge 使用 `original_task`。

## 第二批：消除结构竞态

1. 引入 TurnIdentity。
2. 引入 ContinuationLease。
3. Abort 原子失效所有旧 lease。
4. Host model observation 增加 generation。
5. Compaction 使用 episode 状态，而不是时间窗口。
6. `work_backlog_committed` 使 budget phase 进入 RebaseRequired。
7. message transform cache key 加入 budget/context generation。

## 第三批：删除兼容性启发式

1. 停止根据零宽字符判断 fallback 身份。
2. 停止把物理时间当作用户消息权威身份。
3. 停止让 injected model 覆盖真人 route。
4. 停止根据 compaction 英文文案作为最终状态。
5. 停止从最近 assistant 推导 review task。
6. 停止依赖 before hook 原对象被原样传给 execute。

---

# 十二、增补验收标准

## Abort 与 Action 执行

* 已生成但过期的 SendContinue 不会调用 Host；
* Cancelled 后所有旧 continuation lease 失效；
* Abort 空输出不会产生 EmptyOutputError；
* 缺少 assistant abort metadata 时，translator 仍可识别取消；
* 旧 generation 的 busy/idle 不改变新轮次。

## 控制字段

* Schema 最终产物中 required 正确；
* before hook 删除失效的模拟 Host 下，execute wrapper 仍能净化；
* 真实工具参数中控制字段数量为零；
* 验证失败不产生 session.error 或 fallback；
* 动态工具和 alias 无法绕过。

## Context Budget

* 最后一条 assistant 无 usage 时，可找到更早的最新有效 observation；
* usage 不被重复累计；
* cache read 语义按 Host codec 正确处理；
* 无测量时使用保守估算，不返回 None；
* 未知模型不会默认成 120K；
* todowrite commit 后下一轮必然重建 phase；
* 同一 budget episode 不产生无界重复 nudge。

## Compaction

* anchor 存在时近期补丁能阻止 nudge；
* 没有 anchor 但存在 compaction event 时同样阻止；
* auto-continue settle 前一直阻止；
* settle 后不依赖固定毫秒窗口；
* 用户自行输入相同 anchor 文本不会被误判。

## 模型路由

* 新真人消息模型优先于旧 injected model；
* fallback attempt 使用自己的 route；
* `GetModel` 中的旧 generation observation 被丢弃；
* Host 实时查询结果与当前 turn 不匹配时不得覆盖；
* variant 和 reasoning 参数完整保留；
* 重启后由事件投影恢复。

## Review

* task 只存于权威 ReviewProjection；
* snapshot 正确派生 active task；
* prompt 使用 `original_task`；
* compaction 后任务仍可恢复；
* active loop 缺 task 时不发送；
* review nudge 不会被误识别为新的 loop activation。

---

# 十三、最终评价

第二份方案最值得借鉴的，不是它对所有根因的判断，而是它新增了四条非常实用的工程防线：

1. **检查 idle 空输出分类之前是否真正掌握 Abort 原因；**
2. **状态机决策完成后，效果发送前再做一次有效性校验；**
3. **不信任 Host 会把 before hook 中的同一 args 对象传给 execute；**
4. **利用现有 compaction anchor 快速构建迁移期阻断。**

这些内容应纳入实施方案。

但以下结论必须修正：

* Abort 核心分类并非完全缺失，现有状态机已经做了正确处理；
* Opencode JSON Schema 的 required 注入已经存在；
* 120K 不是安全通用默认值；
* `FallbackRuntimeState.GetModel` 不是普通 nudge 模型 SSOT；
* `task` 不是 review continuation 应使用的 front-matter 字段；
* compaction 文本 anchor 不是可靠的最终状态；
* 直接调用 beginPhase 不能替代基于新上下文测量的 phase 重建。

综合两份外部分析与仓库现状，最终修复路线应坚持：

> **事件翻译保证语义完整，纯状态机负责决策，Continuation Lease 约束异步效果，EventLog Projection 保存权威事实，Host Adapter 负责不可信协议的最终净化。**

这样既能迅速解决当前七个 Bug，也能避免修复方案成为下一批竞态、重复状态和 Host 兼容问题的来源。

---

# 附件：增补稿 III

## ——基于 OpenCode v1.17.13 官方源码调研的最终校准与实施定案

## 一、证据范围与适用边界

本增补稿依据 OpenCode v1.17.13、commit `3adfb970bf071419599ca016ebd2b08361fa28e9` 的源码调查结果，对前述七项问题及《增补稿》《增补稿 II》进行正式校准。

调查覆盖：

* TUI Esc 到服务端 Abort 的完整调用链；
* Session Runner、Effect Fiber 和 AbortController；
* `session.error`、`session.status`、`session.idle` 的事件语义；
* Plugin event hook 和 trigger hook 的并发差异；
* `tool.execute.before/after` 的真实调用边界；
* 工具参数对象的引用传递；
* Compaction、auto-continue 和 synthetic message；
* User、Assistant、Session 三类模型字段；
* AI SDK v6 的 token usage；
* OpenCode 的 context limit 和 overflow 公式；
* MessageID、ParentID、Event ID 及插件生命周期；
* OpenCode 日志与 stdout/stderr 契约。

必须强调：

1. 以下结论对目标版本 v1.17.13 有源码依据。
2. `experimental.*` Hook 虽然在目标版本存在，但仍需通过 Adapter capability detection 使用。
3. `metadata.compaction_continue`、Runner 内部状态和部分 Effect 调度行为属于内部实现，不应成为跨版本协议。
4. 官方仓库没有提供本系统需要的 `humanTurnId`、`continuationId`、`cancelGeneration`、`contextGeneration` 和 continuation owner，这些仍须由万象术自行维护。

---

# 二、官方调研带来的十项关键定案

## 定案一：OpenCode TUI 的 Esc 是双击取消，不是单击取消

在目标版本中：

* 第一次按 Esc 只增加 UI interrupt 计数，并提示再次按键；
* 五秒内第二次按 Esc 才调用 `sdk.client.session.abort`；
* CLI interrupt 则直接调用 abort，没有双击确认。

因此，任何 Esc 问题必须先区分：

* 用户只按了一次 Esc；
* 用户完成了两次 Esc，OpenCode 实际调用了 Abort API。

如果只按了一次，OpenCode 根本没有发生硬取消，fallback 继续运行在 Host 语义上是正常现象。插件无法根据第一次 Esc 推断用户已经取消。

但只要 Abort API 已经被调用，fallback、nudge 和其他 synthetic continuation 就必须停止。

## 定案二：`session.idle` 绝不等于“自然完成”

官方 `session.idle` 只有 `sessionID`，没有：

* completion reason；
* abort reason；
* current run ID；
* origin；
* generation；
* 是否来自 compaction；
* 是否来自错误；
* 是否即将 auto-continue。

它可能由以下情况产生：

* 正常回答结束；
* 用户 Abort；
* provider error；
* retry 结束；
* compaction；
  -不可恢复 overflow；
* session 本来没有 runner 时调用 abort。

因此：

> 禁止仅凭 `session.idle` 触发 todo/review nudge。

## 定案三：Abort 事件并不完整，也不保证唯一

Abort 通常会形成：

```text
MessageAbortedError
```

但并不保证一定有 `session.error`。

有些取消路径只会：

* 更新 assistant message 的 error；
* 设置 completed time；
* 发布 message.updated；
* 发布一个或多个 idle。

此外：

* `processor.halt` 可能设置一次 idle；
* `Runner.cancel` 又可能设置一次 idle；
* cleanup 和 interrupted finalizer 可能多次更新同一 assistant message。

所以不得依赖“必须先收到 session.error，再收到一次 idle”。

## 定案四：OpenCode 的 plugin event hook 是并发 fire-and-forget

普通 trigger hook，例如：

* `tool.execute.before`
* `tool.execute.after`
* `chat.message`
* `chat.params`

由 OpenCode 按插件加载顺序串行等待。

但是通用 `event` hook 是：

* fire-and-forget；
* 不等待 Promise；
* 同一插件的前后两个事件 handler 可以并发；
* 同一 session 的处理完成顺序不受保证；
* `message.updated` 本身还可能重复发布。

这意味着当前依靠多个 bool 标志和异步 handler 自行读写状态的方式天然存在竞态。

## 定案五：OpenCode 没有 cancellation generation

官方 Runner 内部有 run handle，但没有通过插件协议暴露：

* run ID；
* cancel generation；
* continuation ID；
  -当前 human turn generation。

因此，万象术不能仅靠 OpenCode session lifecycle 判断迟到事件属于旧轮次还是新轮次。

必须自行建立 generation 和 lease。

## 定案六：before Hook 中“原地修改 args”有效，“替换 output.args”无效

目标版本中：

```text
tool.execute.before 收到的 output.args
```

与随后传给真实工具的局部 `args` 通常是同一个对象引用。

因此：

* `delete output.args.warn_tdd` 有效；
* 修改 `output.args.xxx` 有效；
* `output.args = newObject` 无法改变真实 execute 使用的旧局部引用。

这是一项极关键的版本事实。

## 定案七：before Hook 不能覆盖全部内部执行路径

以下类别经过 before/after：

* registry built-in 工具；
* custom plugin 工具；
* MCP server 工具；
* MCP resource 工具；
* Task/subagent 工具。

但以下路径不经过或不完全经过：

* StructuredOutput；
* title agent；
* compaction agent；
  -部分内部直接调用的 read；
* after hook 在工具失败或 Abort 时不会执行。

所以 after hook 不能作为安全校验边界。

## 定案八：OpenCode 已提供正式 Compaction Hook 和事件

目标版本具有：

* `experimental.session.compacting`
* `experimental.compaction.autocontinue`
* `session.compacted`
* `assistant.summary = true`
* `part.synthetic = true`

OpenCode compaction 默认会执行 auto-continue。它会直接创建 synthetic user message，不经过 `chat.message` Hook，并最终产生普通 assistant 过程和 idle。

这正是“压缩后错误触发 nudge”的官方协议根源。

## 定案九：OpenCode 的模型路由必须区分消息类型

官方字段形态不同：

```text
UserMessage:
model.providerID
model.modelID
model.variant

AssistantMessage:
providerID
modelID
variant

Session:
model.id
model.providerID
model.variant
```

不能用一个只支持对象或只支持字符串的局部解析器处理所有来源。

## 定案十：OpenCode 自身的 overflow 判断也不是最终 outbound 计量

OpenCode 主要依据上一轮 assistant usage 判断 overflow。

这意味着：

* 本轮刚产生的大型 tool result 尚未计入；
* message transform 新加入的上下文尚未计入；
* system prompt、工具 schema 和 attachment 变化不会即时重新计量；
* limit 缺失时官方会把 context 设为 0，等于关闭自动 compaction。

因此，万象术不能简单复制 OpenCode 的 overflow 判断，也不能只依赖最后一个 assistant usage。

---

# 三、问题一最终修订：Esc、Abort 与 fallback auto-continue

## 3.1 首先修正文档和测试语义

所有用户文档、测试和问题描述都应明确：

```text
OpenCode TUI 中第一次 Esc 不执行硬取消；
五秒内第二次 Esc 才调用 session.abort。
```

端到端测试必须分别覆盖：

1. 单次 Esc；
2. 双次 Esc；
3. 直接调用 session.abort；
4. CLI interrupt。

否则可能把 Host 的双击取消机制误判成 fallback 缺陷。

## 3.2 不能只监听 `session.error`

AbortProjection 应综合以下证据：

* `session.error.name = MessageAbortedError`
* assistant message `error.name = MessageAbortedError`
* tool part `metadata.interrupted = true`
* `session.interrupted`
* `stream-abort`
* 本插件主动调用 abort 的记录
* Abort 后出现的 idle
* local force-stop 操作

其中任何明确 Abort 证据出现，都要建立取消墓碑：

```text
CancelTombstone
- sessionID
- humanTurnId
- cancelGeneration
- observedEventID
- observedAt
- reason
```

`session.idle` 本身不能创建取消墓碑，但在已有取消证据后，可以帮助确认 Host 正在收尾。

## 3.3 引入每 session 串行事件邮箱

由于 event hook 并发执行，所有下列操作必须进入同一个 per-session serial mailbox：

* event 解析；
* fallback state transition；
* lifecycle 更新；
* BusyCount 更新；
* injected continuation 记录；
* nudge snapshot 更新；
* compaction projection 更新；
* event log append；
* action 派生。

不得继续依赖多个异步 handler 直接修改同一 runtime map。

建议状态流为：

```text
Host event
→ 快速解码 sessionID/eventID
→ 投递 per-session mailbox
→ 去重
→ 更新 projection
→ 派生 action
→ 记录 action intent
→ mailbox 外执行副作用
→ 副作用结果重新投递 mailbox
```

## 3.4 所有 SendContinue 必须改为带 Lease 的动作

状态机输出不应再只是：

```text
SendContinue(model)
```

应当包含：

```text
ContinuationLease
- continuationID
- owner = Fallback
- sessionID
- humanTurnID
- sessionGeneration
- cancelGeneration
- selectedModel
- selectedAgent
- createdEventID
- status
```

真正调用 `session.prompt` 前必须重新进入 session mailbox，验证：

* lifecycle 仍为 Active；
* cancelGeneration 未变化；
* humanTurnID 仍相同；
* continuation owner 仍为 Fallback；
* lease 仍为 Pending；
* 没有 compaction owner；
* 没有新真人消息；
  -没有 TaskComplete；
* 没有 force-stop。

验证失败则把 lease 标记为 Invalidated，绝不调用 Host。

## 3.5 注意 `session.prompt` 可能在 Busy 状态下排队

官方 prompt 路径可以在 Runner 尚未完全释放时等待或排队。

而且 user message 可能在真正开始下一次 run 之前已经创建。

这意味着在 event hook 中直接调用 prompt 存在两个问题：

1. 新 synthetic user message可能已经持久化，但当前 run 尚未结束；
2. 随后发生 Abort 时，这条 user message可能残留在历史中。

因此应当：

* 禁止在 event handler 原始调用栈中直接 prompt；
* 先将 continuation 标记为待派发；
* 等待本地 session mailbox 完成当前事件批次；
* 重新查询 Host session status 和最后消息；
* 再次校验 lease；
* 然后调用 prompt。

固定 sleep 只能作为兼容性缓冲，不能承担正确性。

## 3.6 fallback prompt provenance

OpenCode PromptInput 不允许插件直接设置 `synthetic=true`，也没有正式 correlation ID。

短期可使用双重识别：

1. 本地 pending lease；
2. prompt front matter 中的 opaque continuation ID；
3. `chat.message` Hook 捕获新 messageID；
4. 建立：

```text
messageID → FallbackContinuation(continuationID)
```

随后可在 Host 消息 part metadata 中加入辅助 provenance。

文本 front matter 只负责把 pending request 与官方 messageID 对上，真正 SSOT 仍是本地 projection。

## 3.7 必须立即修复的本地 gate

所有 gate 的第一分支统一为：

```text
Cancelled    → false
TaskComplete → false
```

适用范围包括：

* `needFallbackContinue`
* `terminalObservation`
* `isSubagentSettledFromObservation`
* recovery wait
* nudge block
* pending prompt dispatch
* fallback action executor

任何 BusyCount、AwaitingBusy、EventHandlingActive 都不得覆盖 Cancelled。

---

# 四、问题二最终修订：warn、warn_tdd、warn_reuse 与参数净化

## 4.1 Schema 强制仍然必要

对于 registry tools，应继续通过 `tool.definition` 注入：

* properties；
* enum；
* description；
* required。

该 Hook 在每次 LLM request 前执行，可以覆盖动态 registry tool。

但应增加最终 schema 实物测试，确认经过：

```text
tool.definition
→ ToolJsonSchema
→ ProviderTransform.schema
→ AI SDK inputSchema
```

之后字段仍然是 required。

## 4.2 MCP 工具不经过 `tool.definition`

目标版本中 MCP 工具虽然经过 `tool.execute.before`，但其 schema 不经过通用 `tool.definition`。

因此应明确控制字段的适用能力：

* `warn_tdd`：文件修改类工具；
* `warn`：执行器、PTY、Shell 类工具；
* `warn_reuse`：Task/subagent 类工具。

如果受保护能力全部来自 registry 或 Task，则可以正常注入 schema。

如果未来某个敏感 MCP 工具也需要这些字段，必须：

* 自己包装该 MCP tool；
* 或修改 OpenCode adapter；
* 或推动上游为 MCP 暴露 definition hook。

不能假设现有 `tool.definition` 自动覆盖 MCP。

## 4.3 before Hook 必须原地删除

在 v1.17.13 中，正确做法是：

```text
原地验证
→ 原地提取
→ 原地 delete
```

禁止：

```text
output.args = sanitizedCopy
```

因为真实 execute 仍会使用旧局部引用。

## 4.4 多插件顺序产生新的隐患

trigger hook 按插件加载顺序串行执行，并共享同一个 output 对象。

存在一种危险情况：

1. 其他插件先执行；
2. 它把 `output.args` 替换成新对象；
3. 万象术随后看到的是新对象；
4. 万象术在新对象中删除 warn 字段；
5. OpenCode 最终仍把旧局部 args 传给 execute；
6. 旧对象中的 warn 字段未被删除。

所以在目标版本中，万象术若依赖 before hook 原地净化，应满足至少一项：

* 万象术排在会替换 args 的外部插件之前；
* 启动时声明插件顺序要求；
* 对自有工具包装真实 execute；
* 推动上游将 `plugin.trigger` 返回的 `output.args` 传入 execute；
* 对生产部署做多插件兼容测试。

这是官方调研后新增的重要风险。

## 4.5 阻断方式的定案

OpenCode 没有正式：

```text
block: true
deny: true
```

之类 before hook 协议。

目标版本中，before hook 抛错会：

* 阻止真实工具执行；
* 产生 AI SDK tool-error；
* 通常不会直接形成 session-level error；
* 不会自动进入万象术 fallback；
* 但 LLM 可能重新尝试同一工具。

因此：

### 对无法包装的 Host built-in

验证失败时可在 before hook 抛出确定性的参数错误。

错误必须说明：

* 缺少哪个字段；
* 必须使用什么固定值；
* 工具尚未执行；
* 不要修改其他参数。

### 对万象术自己定义的工具

优先在真实 execute wrapper 中：

* 验证；
* 净化；
* 返回结构化 tool rejection；
* 设置 `executionStarted=false`。

## 4.6 三层防线正式定案

### 第一层：Schema

让 LLM 正确生成。

### 第二层：before Hook

* 运行时验证；
* 原地删除；
* built-in 阻断。

### 第三层：自有 execute wrapper

* 再次验证；
* 再次净化；
* clean assertion；
* 控制字段转为独立 ControlEnvelope。

after hook 只做：

* 成功结果审计；
* 使用统计；
* 不变量告警。

不能承担安全校验，因为失败和 Abort 时 after 根本不会执行。

---

# 五、问题三最终修订：Context Budget 与 todowrite

## 5.1 重新定义两类 Token 数据

必须区分：

### ObservedUsage

上一轮 Host/Provider 报告的实际 usage。

### EstimatedOutbound

本轮经过所有 message transform 后，将要发送给模型的消息估算值。

Context Budget 不能只使用 ObservedUsage，因为：

* 新 tool result 尚未计入；
* backlog projection 尚未计入；
* caps/system prompt 变化尚未计入；
* review task 重注入尚未计入；
* budget nudge 自身尚未计入。

## 5.2 官方 token 字段的正确解释

AI SDK v6 在目标版本中产生：

```text
tokens.total
tokens.input
tokens.output
tokens.reasoning
tokens.cache.read
tokens.cache.write
```

需要注意：

* `tokens.total` 优先用于上一轮整体 usage；
* `tokens.input` 是经过 cache 调整后的值；
* 若重新构造原始 input，应把 cache read/write 加回；
* output 和 reasoning 也要避免重复或遗漏；
  -不能简单执行：

```text
total + cache.read
```

否则可能重复累计。

推荐规则：

```text
若 total 有效：
ObservedUsage = total

否则：
ObservedUsage =
  input
  + cache.read
  + cache.write
  + output
  + reasoning
```

但每个 Host codec 仍须通过测试确认字段语义。

## 5.3 不再只读取最后一条 assistant

应从后向前寻找：

* 非空；
* 数值合法；
* context generation 匹配；
* 非 title；
* 可识别 usage 语义；

的最新 observation。

不过，只能选择最新有效快照，不能把多条 assistant usage 相加。

## 5.4 最终预算输入建议

建议：

```text
C = max(
    LatestObservedUsage,
    EstimatedFinalOutbound
)
```

这样既不忽视 Provider 实测，也不忽视当前新增内容。

如果两者都不可用，则进入：

```text
MeasurementDegraded
```

使用保守字符/字节估算，不能返回 None 后关闭机制。

## 5.5 上下文上限的优先级

OpenCode 模型 limit 为：

```text
context
input?
output
```

推荐顺序：

1. `limit.input`，如果存在；
2. `limit.context - output reserve`；
3. 同模型最近成功缓存；
4. model family 保守值；
5. Host 配置默认；
6. 全局保守默认。

官方在 limit 缺失时把 context 设为 0，最终禁用 compaction。万象术不能照搬该行为。

全局默认必须偏小，例如按配置选择 8K、16K 或 32K 安全档，不能使用 100K 或 120K 这种偏大的通用值。

## 5.6 Reserve 应与 OpenCode 语义协调

OpenCode 自身会根据模型 output limit 计算可用空间。

万象术当前固定 reserve 若与 OpenCode 差异过大，会出现：

* 万象术很早催促，但 OpenCode 很晚压缩；
* 或 OpenCode先压缩，万象术 budget state 尚未触发。

建议统一定义：

```text
EffectiveInputBudget =
  min(
      explicit input limit,
      context limit - output reserve
  )
```

reserve 应由：

* model output limit；
* tool call 余量；
* reasoning 模型额外余量；
* Host 配置；

共同决定。

## 5.7 todowrite 成功后的 phase 更新

收到本地：

```text
work_backlog_committed
```

后不应直接用旧 token 数值建立新 phase base。

正确流程：

1. 增加 todo checkpoint ordinal；
2. 标记当前 BudgetEpisode 完成；
3. 状态转为 `RebaseRequired`；
4. 清除本 episode 的重复 nudge；
5. 使 message transform cache 失效；
6. 下一轮对最终 outbound context 重新测量；
7. 建立新的 phase base。

## 5.8 R 必须是当前 phase 内的计数

公式中的 R 不得等于全历史 todowrite 次数。

应保存：

```text
phaseStartTodoOrdinal
currentTodoOrdinal
R = currentTodoOrdinal - phaseStartTodoOrdinal
```

Compaction、模型切换、context generation 变化时，都必须明确决定是否开启新 phase。

## 5.9 `NudgeTrack` 必须真正参与判定

至少区分：

* NotSignaled
* IncludedInOutboundPrompt
* AssistantProgressObserved
* TodoCommitted
* RetryAllowed
* Exhausted
* InvalidatedByCompaction
* InvalidatedByCancel

不能只写入 `EmergencySignaled`，却在下一轮继续无条件附加同一 prompt。

---

# 六、问题四最终修订：DEBUG 与日志

## 6.1 stdout 禁止规则

OpenCode 的 Effect logger 默认走 stderr，但普通插件中的：

```text
console.log
```

会写 stdout。

在以下模式下 stdout 可能是协议通道：

* JSON output；
* MCP；
* ACP；
* subprocess；
* 自动化脚本。

因此，万象术生产代码中必须禁止：

* `console.log`
* 裸 `printfn "DEBUG"`
* 非协议 stdout write。

## 6.2 推荐日志出口

* trace/debug：独立文件；
* warn/error：stderr；
* CLI 明确用户结果：stdout；
* event log：NDJSON 专用文件；
* 不把调试日志混入 event sourcing 日志。

普通 Hook 无法直接调用 OpenCode 内部 Effect logger，因此万象术应维护自己的 logger adapter。

## 6.3 日志必须结构化

至少包含：

* subsystem；
* event；
* severity；
* timestamp；
* sessionID hash；
* messageID；
* humanTurnID；
* continuationID；
* contextGeneration；
* hostVersion；
* degradedReason。

禁止记录：

* 完整 prompt；
* 工具业务参数；
* 文件正文；
* API key；
  -原始 review task 全文；
  -完整 stack 中的敏感路径。

## 6.4 CI 静态门禁

生产模块新增以下内容时构建失败：

* `console.log`
* `printfn "DEBUG`
* 无 logger 包装的 stderr write
* 直接输出完整 prompt/args

OpenCode 本身没有日志 rate limit，万象术还需自行做错误去重和限频。

---

# 七、问题五最终修订：Compaction 后错误 Nudge

## 7.1 不再猜测，直接使用官方 Compaction 生命周期

建议建立：

```text
CompactionEpisode
- episodeID
- sessionID
- contextGenerationBefore
- state
- autoContinueEnabled
- summaryMessageID
- continueMessageID
- startedAt
```

状态至少包括：

* Compacting
* SummaryProduced
* AutoContinuePlanned
* Compacted
* ContinuationObserved
* ContinuationRunning
* Settled
* Cancelled
* Failed

## 7.2 开始信号

OpenCode没有正式 `compaction.started` event，但有：

```text
experimental.session.compacting
```

该 Hook 是最可靠的开始信号。

在此 Hook 中：

* 创建 compaction episode；
* 将 NudgeBlockReason 设为 CompactionActive；
* 保存旧 context generation；
* 可向 `output.context` 注入 review task 等关键上下文。

## 7.3 auto-continue 信号

在：

```text
experimental.compaction.autocontinue
```

中：

* 记录 `AutoContinuePlanned`；
  -读取默认 `enabled`；
  -通常保持 enabled=true；
  -不要再由 fallback 或 nudge重复创建 continuation。

除非产品明确要求完全接管 compaction continuation，否则不应关闭官方 auto-continue。

## 7.4 识别 Compaction summary

稳定信号优先：

```text
assistant.summary = true
```

`agent="compaction"`、`mode="compaction"`可以作为兼容辅助，但不是最终协议。

## 7.5 识别 synthetic continue

目标版本中：

* synthetic 标记位于 part；
* `part.synthetic=true` 是稳定字段；
* `part.metadata.compaction_continue=true` 是内部字段；
* 该消息不经过 `chat.message`；
* 会通过普通 message.updated 被观察到。

所以近期识别顺序：

1. 当前存在 CompactionEpisode；
2. summary 已产生；
3. 随后出现 synthetic user part；
4. metadata 可用于确认，但不是唯一依据。

## 7.6 `session.compacted` 的作用

正式事件：

```text
session.compacted
```

可以确认压缩处理已完成，但不能单独证明：

* auto-continue 已 settle；
  -下一轮 assistant 已完成；
  -现在可以发普通 nudge。

因此从 Compacted 到 Settled 期间仍然阻止 todo/review nudge。

## 7.7 何时解除阻塞

若 auto-continue enabled：

* 观察 synthetic continuation；
* 观察其对应 assistant；
* 等该 assistant 成为 terminal；
* 确认没有 Abort；
* 才将 episode 设为 Settled。

若 auto-continue disabled：

* 在 `session.compacted` 后；
* 确认 Host idle；
  -确认没有 pending continuation；
  -再解除阻塞。

禁止使用固定 100ms、500ms 或“一秒窗口”作为正确性条件。

## 7.8 终止来源必须进入 Nudge Snapshot

Nudge 不再只看：

```text
lastAssistantMessage
```

而应看：

```text
terminalOrigin
```

值至少包括：

* HumanTurnCompleted
* HumanTurnAborted
* CompactionSummaryCompleted
* CompactionContinuationCompleted
* FallbackContinuationCompleted
* TitleCompleted
* NudgeCompleted
* ToolSubturnCompleted
* Unknown

只有 `HumanTurnCompleted` 默认允许普通 nudge。

---

# 八、问题六最终修订：todo/review nudge 模型路由

## 8.1 不能把 FallbackRuntimeState.GetModel 设为绝对 SSOT

官方调查确认，模型存在于不同层级，且 fallback runtime 只是万象术自己的运行时缓存。

最终应拆分为三类事实。

### HumanTurnRoute

来自真实 user message：

* providerID；
* modelID；
* variant；
* agent；
* messageID；
* humanTurnID。

### HostObservedRoute

来自：

* `chat.message`
* `chat.params`
* Session model 查询

其中 `chat.params` 最接近实际 LLM 请求边界，但也可能属于 compaction、title 或其他 agent，必须结合当前 owner 分类。

### FallbackAttemptRoute

只属于某个 fallback continuation lease。

## 8.2 `chat.message` 是真人轮次的重要入口

普通用户提交 prompt 时会触发 `chat.message`，其中包含：

* sessionID；
* messageID；
* agent；
* model；
* variant；
* parts。

应在此建立 HumanTurnRoute。

但 compaction synthetic continuation不经过该 Hook，所以不能把“没有 chat.message”解释成事件缺失；它可能是官方 synthetic message。

## 8.3 `chat.params` 用于校验实际模型

`chat.params` 可以观察到即将发送给 Provider 的真实模型。

建议记录：

```text
RouteObservation
- owner
- sessionID
- humanTurnID
- model
- variant
- agent
- source = ChatParams
```

如果 owner 是：

* Human：更新当前 human route observation；
* Fallback：更新 fallback attempt route；
* Compaction：只更新 compaction observation；
* Title：不得污染 human route。

## 8.4 普通 Nudge 的模型优先级

1. 当前 HumanTurnRoute；
2. 与当前 humanTurnID 和 generation 匹配的 HostObservedRoute；
3. Session 当前模型；
4. Agent 默认模型；
5. 最后非 synthetic user model，兼容回退。

明确禁止：

-旧 injected fallback model；

* compaction assistant model；
* title model；
  -无 generation 的 runtime cached model；
  -最后 assistant model无条件覆盖。

## 8.5 Fallback continuation 的模型优先级

1. 当前 FallbackAttemptRoute；
2. 当前 HumanTurnRoute；
3. fallback chain 默认。

Fallback 模型不能跨 human turn 生效。

## 8.6 Nudge 必须显式传递模型

调用 OpenCode `session.prompt` 时应显式设置：

```text
model.providerID
model.modelID
model.variant
agent
```

不能只发送 text 和 custom type，然后依赖 Session 默认。

## 8.7 reasoning effort 等无法完全从消息恢复

官方消息和 Session 不持久保存：

* temperature；
* topP；
* reasoning effort；
* thinking；
* provider-specific options。

这些由 agent、model 和当前配置在请求时重新计算。

所以 HumanTurnRoute 至少应可靠保存：

* provider；
* model；
* variant；
* agent。

其他参数若来自万象术自己的 fallback config，可在 FallbackAttemptRoute 中保存；若完全由 OpenCode内部计算，普通 nudge 应服从当前 Host 配置，而不是伪造历史参数。

---

# 九、问题七最终修订：Review Nudge 原始任务

## 9.1 Review task 的权威来源不变

原始任务的 SSOT 是：

```text
loop_activated event
→ ReviewLoopFold.Active task
→ ReviewProjection
```

不从历史自然语言重新猜测。

## 9.2 不要复制两个独立 task 状态

不建议同时维护：

```text
reviewLoop = Active task
originalTask = Some task
```

两个可变字段。

更安全的做法：

* task 只存在 ReviewLoopFold；
* 构建 Nudge Snapshot 时调用 `activeTask`；
* 派生 `reviewTask`；
* PromptContext 再输出。

## 9.3 Review Nudge 使用 `original_task`

后续 continuation 应输出：

```yaml
original_task: <完整原始任务>
prompt_origin: review_nudge
```

不能使用可能具有 loop 激活语义的：

```yaml
task: ...
```

## 9.4 跨 Compaction 采用双重保障

### 第一层：EventLog 重新注入

compaction 后从本地 ReviewProjection 恢复 original task。

这是权威路径。

### 第二层：compacting Hook 增强 summary

在：

```text
experimental.session.compacting
```

中通过 `output.context` 注入：

* 当前 original task；
* review loop active 状态；
  -最新 feedback；
  -未完成 todo。

这只能提高 summary 质量，不能代替本地 EventLog。

## 9.5 本地 Compaction whitelist

当前白名单若只保留：

* task；
* verdict；
* double-check；
* squad_event；

应加入：

* original_task；
* prompt_origin；
  -必要的 review loop identity。

但长期仍应以 ReviewProjection 重注入为主。

## 9.6 Active loop 缺 task 必须拒绝派发

若：

```text
ReviewLoop = Active
reviewTask = None
```

则：

1. 从 NDJSON 重新 fold；
2. 若仍缺失，记录 projection corruption；
3. 不发送 review nudge；
4. 不降级为普通“请调用 submit_review”文本。

---

# 十、跨问题架构定案：必须新增五个核心投影

## 10.1 HumanTurnProjection

保存：

* currentHumanTurnID；
* userMessageID；
* route；
* agent；
* lifecycle；
* generation；
* openedAt；
* terminalOrigin。

## 10.2 CancellationProjection

保存：

* cancelGeneration；
* tombstone；
* source event；
* affected turn；
* invalidated continuation IDs。

## 10.3 ContinuationProjection

保存：

* continuation lease；
* owner；
* route；
* target turn；
* dispatch state；
* host messageID；
* invalidation reason。

## 10.4 CompactionProjection

保存：

* episode；
* summary；
* auto-continue；
* synthetic message；
* context generation；
* settle state。

## 10.5 ReviewProjection

保存：

* original task；
* loop ID；
* current round；
* latest feedback；
* active/inactive。

Context Budget Projection作为第六个独立投影维护 phase、ordinal 和 measurement。

---

# 十一、事件处理的最终纪律

## 11.1 Event Hook 只负责入队

OpenCode event hook 中不直接：

* 调用 prompt；
  -修改多张 runtime map；
  -发 nudge；
  -开始 fallback；
  -写多个相关事件。

只做：

1. 快速解码；
2. 基础字段复制；
3. 投递 per-session queue；
   4.立即返回。

## 11.2 Trigger Hook 保持同步边界

以下 Hook 是 OpenCode真正等待的同步边界：

* tool.execute.before；
* tool.execute.after；
* chat.message；
* chat.params；
* compaction Hooks。

它们可以执行必要的同步状态更新，但不能等待一个正在被当前 Host 调用链占用的同 session queue，否则可能死锁。

因此需要区分：

* Event mailbox；
* Trigger boundary snapshot；
* Effect dispatch queue。

## 11.3 所有事件必须幂等

使用：

* OpenCode event ID；
* messageID；
* partID；
* callID；
* local continuationID；

建立去重键。

`message.updated` 不能以“每出现一次就代表一次新完成”处理。

## 11.4 MessageID 可以辅助排序，但不能代替 generation

OpenCode MessageID 是单调 ULID，parentID 也很有价值。

但以下消息都会创建新 user ID：

* 真人消息；
* compaction request；
* synthetic continuation；
  -插件 nudge；
* fallback prompt。

所以 MessageID 只能帮助构建图，不能直接等价于 human turn。

---

# 十二、修复优先级重新排序

## P0：立即阻止错误行为

1. 修复所有 Cancelled gate。
2. 区分一次 Esc 与实际 Abort。
3. 引入 per-session event mailbox。
4. `session.idle` 不再直接触发 nudge。
5. fallback action 发送前增加 lease 校验。
6. Hook 参数改为原地删除，不替换 `output.args`。
7. after hook 移除安全校验职责。
8. 接入 compaction Hooks 和 `session.compacted`。
9. compaction episode 活跃时阻止 nudge。
10. Review nudge 携带 `original_task`。
11. Nudge 显式传 model/variant/agent。
12. 删除 stdout DEBUG。

## P1：消除主要竞态

1. HumanTurnProjection。
2. CancellationProjection。
3. ContinuationLease。
4. CompactionProjection。
5. Host model observation generation。
6. Context Budget RebaseRequired。
7. Event ID 幂等。
8. plugin load order 检查。
9. MCP schema 能力审计。
10. prompt provenance 与官方 messageID 绑定。

## P2：消除兼容性启发式

1. 删除零宽字符身份判断。
2. 删除物理时间戳权威判断。
3. 删除 stale injected model 优先级。
4. 删除 compaction 文案判断。
5. 删除最后 assistant 推导 human model。
6. 删除全历史 todo 作为 R。
7. 删除固定大 context limit。
8. 删除 event handler 中直接 prompt。
9. 删除依赖 after hook 的安全策略。

---

# 十三、增补后的核心验收矩阵

## Esc 与取消

* 单次 Esc 不错误标记为 Abort；
* 双次 Esc 后旧 continuation 调用数为零；
* session.error 缺失时仍能识别 Abort；
  -重复 idle 不产生重复状态转移；
  -旧 generation 的 message.updated 不恢复 session；
  -已派生但未发送的 action 被取消；
  -已经排队的 prompt 可被识别和失效。

## 工具控制字段

* registry tool schema 中 required 正确；
* MCP 敏感能力被单独审计；
  -原地删除后真实 execute 收不到控制字段；
  -替换 `output.args` 的回归测试明确失败并被检测；
  -其他插件替换 args 时产生兼容性警告；
  -before 抛错时真实 execute 次数为零；
  -after 缺失不影响安全；
  -自有 wrapper 存在 clean assertion。

## Context Budget

-最后 assistant 无 usage 时能找到最新有效值；
-不重复累加 cache.read；
-大型 tool result 在本轮估算中被计入；
-transform 新增内容被计入；
-limit=0 时进入保守默认；
-work backlog commit 后重新建立 phase；
-同一 episode 不无限催促；
-compaction 后 context generation 正确变化。

## Compaction

-`experimental.session.compacting` 打开 block；
-autocontinue planned 时普通 nudge 为零；
-summary assistant 不被视为真人完成；
-synthetic continue 不经过 chat.message 也能识别；
-`session.compacted` 不立即解除 block；
-auto-continue settle 后才解除；
-compaction Abort 时 cancellation 优先；
-用户输入相同文案不被误判。

## 模型

-真人 user model 成为 HumanTurnRoute；
-`chat.params` 按 owner 分类；
-compaction/title 不污染 human route；
-旧 injected model 不影响新轮次；
-nudge 请求显式携带 model/variant/agent；
-fallback attempt route 不跨 turn；
-字符串、User object、Assistant flat fields均可解析。

## Review

-original task 由 ReviewProjection 提供；
-review nudge 使用 `original_task`；
-compaction hook 可增强 summary；
-compaction 后仍从 EventLog 恢复；
-active loop 缺 task 时零发送；
-review nudge 不重新激活 loop。

---

# 十四、最终架构结论

官方源码调查说明，七个 Bug 不能只靠调整若干条件判断解决。

OpenCode v1.17.13 的真实协议具有以下特征：

-取消是异步 Fiber interruption；
-idle 没有原因；
-error 可能缺失；
-message.updated 可以重复；
-event hook 并发执行；
-prompt 可能排队；
-synthetic message 并不统一经过 chat.message；
-tool hook 的参数修改依赖对象身份；
-compaction 自带 auto-continue；
-模型字段在不同消息中形态不同；
-token usage 是上一轮 observation，而非最终 outbound estimate；
-官方不提供万象术需要的 generation 和 continuation identity。

因此，万象术最终应形成如下边界：

```text
OpenCode Host Adapter
    ↓ 归一化官方事件
Per-session Serialized Event Mailbox
    ↓
Durable Projections
    ├─ HumanTurnProjection
    ├─ CancellationProjection
    ├─ ContinuationProjection
    ├─ CompactionProjection
    ├─ ContextBudgetProjection
    └─ ReviewProjection
    ↓
Pure Decision Kernels
    ├─ Fallback
    ├─ Nudge
    ├─ Review
    └─ Context Budget
    ↓
Lease-validated Effect Executor
    ↓
OpenCode SDK / Tool Execute
```

最终原则可以压缩成五句话：

1. **Idle 是信号，不是结论。**
2. **Abort 是墓碑，不是一个瞬时 bool。**
3. **Continuation 是带代次的租约，不是一段零宽文本。**
4. **Host 消息是观测，EventLog Projection 才是业务事实。**
5. **任何副作用在最后一毫米都必须重新验证其合法性。**

这套定案既吸收了 OpenCode v1.17.13 的真实协议，又避免把该版本的内部实现误当成永久契约，应作为后续代码修复、测试重建和跨版本适配的正式设计依据。

---

# 附录 IV：深度实施避坑与边界防御指南

本附录旨在针对万象术（Wanxiangshu）在 Fable 编译 JS 环境、OpenCode v1.17.13 架构及 Mux 环境下，针对上述七项核心漏洞的落地实施提供深度的避坑、防御及细节校验指导。

---

## 一、 Fable 与 JS 互操作（Interop）中的隐性地雷

在 Fable 环境下将 F# 编译为 JavaScript 时，类型系统的擦除和运行时表示的差异容易导致静默失败。在实施本方案时，需特别注意以下互操作陷阱。

### 1.1 `int64` 比较与 JS `Number` 精度断层
在 F# 核心库中，`int64` 是通过自定义对象（包含高 32 位和低 32 位数值字段）在 JS 中模拟实现的。
* **避坑警告**：当从 Host 获取时间戳（通常为 JS 的双精度浮点数 `number`，对应 F# 的 `float`）并与 F# 的 `int64` 进行直接比较（例如 `msgTimeMs >= injectedAt`）时，若其中一方被转换为 `obj` 传入，编译后的 JS 将使用 `===` 或原生的 `>` 运算符。这会导致 JS 运行时将 F# 的 `int64` 结构体与原生 JS 数值进行直接对比，进而产生恒为 `false` 的静默错误。
* **定案方案**：在整个 Fallback 与 Nudge 系统中，时间戳统一使用 `float`（即 JS 原生的 `Number`，精度足以安全表示毫秒级时间戳，直至公元 285428 年）或者进行显式的双向类型转换，禁止混合使用 `int64` 和 `float` 进行逻辑比较。

### 1.2 `Object.defineProperty` 与只读 Host 对象的静默冲突
部分 Host（如 Mux/OpenCode）的 `args` 或 `config` 属性可能是通过 Object 冻结、getter 配置或代理拦截实现的只读对象。
* **避坑警告**：直接对这些对象进行修改（如 `args?warn_tdd <- ...`）在 JS 严格模式下会抛出 `TypeError`，在非严格模式下则会静默失败（属性未成功写入，但程序继续运行）。
* **定案方案**：在执行前终极净化阶段，不要假设可以直接对 Host 传入的参数对象进行写入。应当首先通过浅拷贝（`assignInto` 或 `Object.assign`）甚至深拷贝克隆一个自由的对象副本。对此副本完成净化、校验后，再行使后续的执行和调用，绝不在 Host 的原始参数对象上直接测试边界。

### 1.3 `Object.ReferenceEquals` 对 JS 临时重构对象的失效
在 F# 中常用 `System.Object.ReferenceEquals` 或其别名来判断消息的部分或整体在 transform 过程中是否发生了改变。
* **避坑警告**：OpenCode 和 Mux 在传递消息列表或零件列表时，可能会在每次生命周期 Hook 触发前对其进行内部反序列化。这会导致即使消息内容完全没有改变，前后两次收到的 JS 对象引用地址也完全不同。此时，依赖 F# 引用一致性判断的缓存和防重复机制会彻底失效。
* **定案方案**：建立两层等价性检查。首层使用内容指纹（Fingerprint，结合核心字段及内容长度计算）；仅在指纹完全相同时，才行使备用的结构化等价性检验（`partsEquivalent`）。决不能将引用等价性作为防重复和防刷新的核心依据。

---

## 二、 单线程事件邮箱与异步租约（Lease）的防竞态设计

由于 OpenCode 的 `event` Hook 是并发且不阻塞的 fire-and-forget 调度，因此必须引入严格的 mailbox 机制。

### 2.1 异步 continuation lease 的状态流转与自动失效
为了防止“状态机做出了 SendContinue 决策，但在调用 Host API 之前用户按了 Esc”这类物理竞态，所有的 Continuation 动作必须被约束在一个受代次控制的租约中。
* **避坑警告**：若只在状态机中维护一个 `SessionFallbackState` 的 `Lifecycle` 字段，由于在途的 `session.prompt` 调用是异步 Promise，在 Promise 挂起期间，用户按了 Esc。此时，状态机的 `Lifecycle` 的确变为了 `Cancelled`，但已经进入微任务队列的 `prompt` 仍会强行将消息发送出去。
* **定案方案**：每一个 `SendContinue` Action 必须关联一个唯一的 `ContinuationLease` 对象。
  * 租约包含 `humanTurnID` 和 `cancelGeneration`；
  * 在 Action 真正执行 Host 调用的“最后一毫米”，必须从 per-session mailbox 中读取该 session 当前最新的 `cancelGeneration`；
  * 只要发现当前代次已大于租约创建时的代次，此租约必须被强制宣布为 `Invalidated`，直接拦截调用。

### 2.2 门禁信号的重置收敛
在 Abort 事件被确认为真且更新了 `Cancelled` 墓碑后，必须立即重置该 Session 的所有衍生标志。
* **避坑警告**：如果仅修改了 `Lifecycle = Cancelled`，但没有清除 `AwaitingBusy` 或 `SubsessionPending` 等异步门禁，那么下游的 `needFallbackContinue` 计算由于看到这些门禁依旧为 `true`，仍然会继续产出“需要 fallback 续跑”的诊断结果，使得状态机制动失效。
* **定案方案**：必须设计一个原子重置方法（`CancelEpisode`），当且仅当发生 Abort 时，该方法会强行将当前 Session 的 `AwaitingBusy`、`SubsessionPending`、`EventHandlingActive` 置为 `false`，并把 `BusyCount` 归零。这些门禁信号在生命周期终止时没有资格保留。

---

## 三、 控制字段净化与 Schema 静态增强的协调防御

LLM 可能会出于幻觉或提示词干扰，将 `warn_tdd` 等控制字段误写并传递到真实的下游执行器。必须保证多层净化机制的绝对有效。

### 3.1 Zod/Effect 模式下 required 数组的同步更新
在改写 OpenCode 的 tool parameters 时，必须同时操作 `properties` 字典与 `required` 数组。
* **避坑警告**：如果仅在参数对象的 properties 下注入了 `warn_tdd`，但没有将其键名塞入 required 数组中，Zod/Effect 校验器在导出 JSON Schema 时可能会将其视为“可选（Optional）”。LLM 就会选择不填该字段，从而绕过了验证。
* **定案方案**：在 schema 最终导出前，万象术必须调用 `appendRequiredKey`。由于有些 Zod 结构是只读的，在改写时必须检测当前模式：
  * 若为 Zod 实例，调用其 `extend` 或 `safeExtend` 构建新实例；
  * 若为普通 JSON 树，必须原地操作 properties 并更新 required 数组，保证 required 与 properties 完美同步。

### 3.2 应对 Host 参数重构的“净化后断言”
由于 OpenCode/Mux 在 Hook 执行前后可能进行深拷贝或重新序列化，导致 properties 中的多余字段再次出现在入参中。
* **避坑警告**：在 `tool.execute.before` 中删除了 `output.args` 里的字段，但在真实执行 execute 之前，Host 又根据之前的 tool call 记录重新反序列化出了一份干净的 `args`，此时控制字段死而复生。
* **定案方案**：引入执行前终极净化。在最贴近底层工具 execute 的 wrapper 中，实现如下逻辑：
  ```text
  IF checkControlFieldsExist(args) THEN
      // 仍然发现未净化的控制字段，说明 Host 重建了参数或绕过了 before hook
      THEN LogErrorAndFailClosed()
  ```
  通过这一层净化后断言，即使 Host 机制发生漂移，敏感工具也会直接拒绝执行，从而保证安全。

---

## 四、 上下文预算（Context Budget）公式失效与测量降级

由于 OpenCode 在 token 限制不明确时可能会采取将上限判定为 0 的降级，因此万象术必须有一套独立的健壮保障。

### 4.1 UTF-8 字节数与 Token 密度的保守估算（Bootstrap Estimator）
当 Host 没有提供实时的 token counts，且历史 `LastUsage` 的测量数据也为空时（例如新会话的第一个轮次），系统目前会退回 `None` 并关闭保护。
* **避坑警告**：不能使用单一的 `bytes / 4` 判定。因为：
  * 中文字符在 UTF-8 中占用 3 个字节，但在很多 tokenizer 中一个汉字就是一个或半个 token，字节/token 比例接近 3:1；
  * 代码、长路径、YAML 标记的 token 密度可能会根据标点符号的不同发生巨大抖动；
  * 使用过大的估算分母会导致严重低估上下文，进而延迟 todowrite 催促，发生物理溢出。
* **定案方案**：引入多语境保守估计器：
  * 检测内容是否包含中文字符。若是，采用偏高估计（如每 2 个字符 1 个 token）；
  * 若全为代码，按已知的高密度估计；
  * 计算出的初始估算值必须乘以安全系数（如 1.25）；
  * 显式标记此测量为 `MeasurementDegraded`，允许提前触发 todowrite。

### 4.2 极限阈值 0 的拦截与安全默认值
当 `MaxInputTokens` 经由各类 client 反馈解析出 0、负数或小于 5000（保留空间）的值时，系统不得将其作为合法输入上限。
* **避坑警告**：直接接受 0 限制会导致 `effectiveMaxInputTokens` 无法计算，或者公式 `F` 中分子分母出现除以零及矛盾边界。
* **定案方案**：设定最低安全水位：
  * 任何解析出的 limit 若低于 8192，一律回落到该 Host 的保守默认水位（如 16384 或 32768）；
  * 确保即使完全无法从 Host 获取上限，系统也能以一个适中的安全视界运行，决不静默关闭预算。

---

## 五、 压缩（Compaction）周期的多层状态阻断

OpenCode 的 Compaction 结束时会启动 auto-continue，自动发出一条 `synthetic` 的 user continuation，这会高频误触发普通 nudge。

### 5.1 废弃物理时间窗口，改用状态代次阻断
补充分析曾建议在 Compaction 发生后使用一个短暂的毫秒级时间窗来屏蔽 nudge。
* **避坑警告**：如果系统负载高、网络出现延迟，或者模型的 reasoning 过程超过了该时间窗，当 time window 过期而 compaction 产生的 auto-continue 仍然在途中时，nudge 就会乘虚而入。在测试环境和高并发环境下，这会造成极不稳定的偶发性失败。
* **定案方案**：完全废弃任何物理时间窗口。改用基于 `CompactionProjection` 状态周期的强阻断：
  * 当捕获到 `experimental.session.compacting` 时，Compaction 状态立即变更为 `Compacting`，并生成新的 `compactionEpisodeID`；
  * Nudge 阻断状态（`NudgeBlockReason`）变更为 `CompactionActive`；
  * 直至该 compaction summary 产生、auto-continue synthetic 消息发送、对应 assistant 消息自然结束（`terminal`），且 mailbox 确认该轮无 fallback 要求、彻底闲置后，方可将 compaction 状态标记为 `Settled`；
  * 此时，将 `NudgeBlockReason` 回归为 `Allowed`。状态未 Settled 前，阻断持续存在，无论经历了几万毫秒。

### 5.2 Compaction 过程中原始任务的重注入防护
由于 Compaction 可能会丢弃前文所有的 front-matter 块，包括 `original_task`。
* **避坑警告**：压缩后如果直接使用 `ReviewLoopFold` 去找，由于旧的消息已经被截断，fold 可能会返回 `None`。此后触发的 nudge 就会丢失原任务，LLM 也会失去目标。
* **定案方案**：万象术的 `ReviewProjection` 是一个跨 Compaction 持久生存的状态。在 `experimental.session.compacting` 阶段：
  * 通过 `output.context` 强制重新将 `ReviewProjection` 中存储的 `original_task` 拼装为新的 front-matter 写入即将生成的 summary 中，实现物理重注入；
  * 从根本上解决压缩过程中的原任务失忆问题。

---

## 六、 模型路由与人类轮次单一事实源

避免 fallback state 中陈旧的 injected 模型污染后续的人类轮次。

### 6.1 `NewUserMessage` 对 model map 缓存的原子清洗
当且仅当 `chat.message` 观测到一个明确是 Human 发出的新 user message 时：
* **避坑警告**：如果仅在 fallback 状态机内部清理 `state.CurrentIndex`，由于 `FallbackRuntimeState` 的 `models` Map（曾用作运行期缓存）中依然残留有上一次 fallback 时记录的 `"provider/model"` 值，随后的 Nudge 仍然可能会读取到这个残留值，进而指派错误的模型。
* **定案方案**：
  * 建立统一的 `NewUserMessage` 入口；
  * 原子删除 `FallbackRuntimeState` 中该 session 对应的 `injectedModels`、`injectedAts` 以及 `models` 缓存；
  * 清除 `ContinuationLease`；
  * 将 `HumanTurnRoutingProjection` 更新为新 user message 实际携带的模型设置，以此作为后续所有 todo/review nudge 的唯一权威路由事实源。

---

## 七、 评审提示词中 original_task 的严格隔离

### 7.1 严禁在 Nudge 中使用 `task` 字段名
在为 `SessionSnapshot` 增加 original task 以支持 Nudge 模板时：
* **避坑警告**：在 nudge prompt 渲染的 front-matter 中，键名决不能写成 `task`：
  ```yaml
  # 错误做法
  ---
  task: "Implement feature X"
  ---
  ```
  因为万象术在重启重放（Replay）时，其折叠逻辑（`inferReviewTaskFromTexts`）只要看到 `task` 字段，就会误判定为“用户重新发起了一次 With-Review 激活命令”。这会导致 review 状态和 version 发生严重错乱。
* **定案方案**：
  * 在 nudge prompt 模板中，原始任务一律统一编码为 `original_task`；
  * 同时附加 `prompt_origin: review_nudge` 明确其非激活语义，与首发 With-Review 的激活命令在 schema 层面彻底实现物理隔离。

---

# Wanxiangshu 六项行为修复方案

## 一、先确定统一设计原则

这次不要逐个打补丁，应先确立四条规则。

### 规则 1：Projection、CAPS、Hint、Budget 必须拆成独立策略

当前 `ProjectionPolicy.ExcludeProjection` 会同时造成：

* 不投影 backlog；
* 不注入 CAPS；
* 不注入并行工具 user hint；
* 部分 Host 不加载 subagent 临时文件；
* Context Budget 行为也和投影流程耦合。

这正是 investigator 丢 CAPS、单工具调用丢 user hint 的共同根因。

应将 `MessageTransformPlan` 中单一的 `ProjectionPolicy` 拆成至少四个独立决策：

| 策略                      | 控制内容                      |
| ----------------------- | ------------------------- |
| BacklogProjectionPolicy | 是否折叠、注入 todowrite backlog |
| CapsInjectionPolicy     | 是否注入 CAPS 和 subagent 临时文件 |
| ParallelHintPolicy      | 是否注入“并行调用工具”提示            |
| ContextBudgetPolicy     | 是否计算预算、是否允许提醒 todowrite   |

建议的 agent 策略矩阵：

| Agent                       | Backlog | CAPS | Parallel Hint | Todo Budget |
| --------------------------- | ------: | ---: | ------------: | ----------: |
| 主工作 agent / manager / build |       是 |    是 |             是 |           是 |
| investigator                |       否 |    是 |             是 |           否 |
| reviewer                    |   视现有设计 |    是 |         视工具能力 |      否或专用策略 |
| browser                     |       否 |    否 |             否 |           否 |
| executor                    |       否 |    否 |             否 |           否 |
| title / compaction          |       否 |    否 |             否 |           否 |

investigator 是只读调查 agent：

* 应看到 CAPS；
* 应被提醒并行 read、grep、search；
* 不应因此获得 todowrite、写文件或 methodology 权限；
* 不应收到“立刻调用 todowrite”的 Context Budget 提醒。

因此，绝不能简单把 `"investigator"` 从 `defaultExcludedAgents` 删除。这个函数还被工具权限判断复用，直接删除会扩大 investigator 的工具权限。

### 规则 2：强提示不等于硬拒绝

以下字段都应采用“软要求”：

* `warn_tdd`
* `warn`
* `warn_reuse`
* todowrite 的五个报告字段及 1024 字要求

目标语义是：

* schema 中非常醒目地要求填写；
* LLM 正常情况下应填写；
* LLM 漏填时工具仍执行；
* 工具结果尾部追加严厉的协议违例批评；
* 不把结果标记为工具失败；
* 不抹掉工具原始输出；
* 不要求 LLM 重复执行刚刚已经成功的工具。

### 规则 3：真正的 JSON Schema `required`、`minLength` 与“漏填也执行”冲突

如果 Host 严格执行 JSON Schema：

* `required` 缺失会在工具执行前被拒绝；
* `minLength: 1024` 不满足也会在工具执行前被拒绝；
* Wanxiangshu 的 tool-before/tool-after 根本收不到调用。

因此，要保证 OpenCode、Mux、OMP、Mimocode 行为一致，不能继续把这些“软要求”放进真实的硬校验项。

推荐的 schema 表达方式：

* 字段继续存在；
* enum 继续给出唯一推荐值；
* description 明确写 `MUST`；
* description 明确写“至少 1024 字”；
* 增加 Wanxiangshu 自有元数据，例如：

  * `x-wanxiangshu-soft-required`
  * `x-wanxiangshu-soft-min-length`
* 不放入真实的 `required`；
* 不使用会被 Host 强制执行的 `minLength`。

这样 schema 仍然强烈引导模型，但不会阻止工具执行。

### 规则 4：批评必须发生在所有结果规范化之后

当前多个 Host 会在 tool-after 中重写工具结果：

* OpenCode 的 `ProgressObserver` 会把 todowrite 输出替换成标准文本；
* OMP 的 `TodoHooks` 也会替换 todowrite 输出；
* Mux wrapper 会重新构造 output。

如果先追加批评、后做标准输出替换，批评会再次丢失。

统一顺序必须是：

1. 工具真实执行；
2. 网络错误、语法诊断、livelock 等现有处理；
3. todowrite 等工具进行标准输出规范化；
4. 恢复控制字段；
5. 最后追加协议违例批评；
6. 删除临时 compliance envelope。

---

# 二、investigator 也注入 CAPS

## 现有根因

`Kernel/MessageTransformPolicy.fs` 当前把 investigator 放在：

* `defaultExcludedAgents`

三个 Host 在建立 `MessageTransformPlan` 时都会据此设置：

* `ProjectionPolicy.ExcludeProjection`

随后 CAPS 又被错误地绑定到该 policy：

* `Shell/MessageTransformHostHooks.loadCapsForScope` 遇到 Exclude 直接返回空列表；
* OMP 的 `loadCaps` 也直接根据 ProjectionPolicy 返回空列表；
* `injectSubagentFilesIfAny` 同样受 ProjectionPolicy 限制。

所以 investigator 即使 CAPS 文件实际存在，最终送给 `buildCapsMessages` 的仍然是空列表。

## 正确修改

### 1. 在 Kernel 中增加独立的 agent policy

修改重点：

* `Kernel/MessageTransformPolicy.fs`

保留现有 projection 排除规则，用于 backlog 和某些工具权限。

新增独立判断：

* investigator：`CapsInjectionPolicy.Include`
* investigator：`ParallelHintPolicy.Include`
* investigator：`ContextBudgetPolicy.DisableTodoEmergency`

不要让 `shouldExcludeAgentFromProjection` 再决定 CAPS。

### 2. 扩充 `MessageTransformPlan`

修改重点：

* `Shell/MessageTransformCore.fs`
* 三个 Host 的 `MessageTransform.fs`

计划中显式携带：

* backlog policy；
* caps policy；
* parallel hint policy；
* context budget policy。

这样 pipeline 不再根据一个布尔值猜测四种不同意图。

### 3. 修改 CAPS 加载入口

修改重点：

* `Shell/MessageTransformHostHooks.fs`
* `Omp/MessageTransform.fs`
* `Opencode/MessageTransform.fs`
* `Mux/MessageTransform.fs`

`loadCapsForScope` 只看 `CapsInjectionPolicy`。

`injectSubagentFilesIfAny` 也只看 CAPS/subagent 文件策略，不再看 backlog projection。

需要继续保留：

* child session 查 parent session ID；
* CAPS fingerprint；
* leading synth CAPS 去重；
* repeated transform 不重复叠加 CAPS；
* CAPS 文件变化后 fingerprint 能失效缓存。

### 4. 不要顺手扩大 investigator 权限

必须回归确认：

* investigator 仍不可调用 todowrite；
* investigator 仍不可写文件；
* investigator 仍不可调用 coder；
* investigator 只获得原有 read/search/executor 等允许能力；
* ToolPermission 中依赖 projection exclusion 的逻辑不能被误改。

## 验收用例

1. 主 session 启动 investigator，存在一个 CAPS 文件：

   * investigator 输入历史开头存在 CAPS synth 消息；
   * 内容与主 agent 看到的一致。

2. investigator 是 child session：

   * 使用 parent session 的 CAPS scope；
   * subagent 临时文件正确合并；
   * 相同路径只出现一次。

3. 连续运行 message transform 三次：

   * CAPS 不重复叠加。

4. CAPS 内容改变：

   * transform cache 失效；
   * investigator 收到新内容。

5. investigator 工具列表：

   * 没有因为此次修改出现 todowrite、write、patch 等权限。

---

# 三、单工具调用后稳定注入并行工具 user hint

## 现有根因

`Shell/MessageTransformPipeline.tryInjectParallelToolPrompt` 当前有三个脆弱点。

### 问题 1：整个功能被 ProjectionPolicy 关闭

pipeline 当前只有在 `ProjectionPolicy.IncludeProjection` 时才调用并行提示注入。

因此 investigator 以及其他 ExcludeProjection agent 永远不会收到提示。

### 问题 2：统计的是所有 ToolPart，而不是实际工具调用

当前同时要求：

* `allToolParts.Length = 1`
* `triggerableToolParts.Length = 1`

如果该 assistant message 中存在：

* 一个真实工具调用；
* 一个 synthetic/prefetch/internal ToolPart；

那么从用户语义看是“只调用了一个工具”，但 `allToolParts.Length` 大于 1，提示不会触发。

### 问题 3：结果匹配没有关联 call ID

当前只检查 assistant 后面是否存在任意 `ToolResult`。

这会造成：

* 旧结果可能被误认为当前工具结果；
* 当前工具结果存在但编码角色不同，可能漏判；
* 后面已经出现新 assistant 回合时，还可能追加过期提示。

## 正确修改

### 1. 用独立 `ParallelHintPolicy`

修改重点：

* `MessageTransformPlan`
* 三个 Host 的 agent policy 计算
* `runMessageTransformPipeline`

investigator 应启用 ParallelHint。

title、compaction、browser 等不应启用。

### 2. 定义“真实工具调用”

只统计：

* 非 synthetic call ID；
* 非 CAPS、prefetch、内部伪造调用；
* 属于当前 native assistant turn 的调用。

触发条件只看：

* `triggerableToolCallCount = 1`

不要再要求所有 ToolPart 数量等于 1。

### 3. 按 call ID 找到对应结果

完整判定流程：

1. 找到最新的 native assistant message；
2. 提取其中所有真实 call ID；
3. 真实调用数恰好为 1；
4. 在后续消息中找到该 call ID 对应的 ToolResult；
5. 该 ToolResult 后没有更新的 native assistant message；
6. 当前输出中还没有对应 hint；
7. 追加 synthetic user hint。

不要使用“后面有任意 ToolResult”这种宽松判断。

### 4. hint ID 必须稳定

建议 synthetic ID 基于：

* assistant message ID；
* 或唯一 tool call ID。

例如逻辑身份为：

* `parallel-tool-hint:<callID>`

这样可做到：

* 同一 transform 多次运行不会重复；
* 不需要持久化历史集合；
* 不会将上个回合的 hint 错配到新回合。

### 5. hint 内容保持“提醒”，不成为硬拒绝

现有提示文本总体合理：

* 有独立操作时并行；
* 有数据依赖时允许单调用。

不要把单工具调用变成错误，也不要阻止执行。

## 验收矩阵

| Assistant 工具组成                           |  应否提示 |
| ---------------------------------------- | ----: |
| 1 个真实 read                               |     是 |
| 1 个真实 read + 1 个 synthetic internal part |     是 |
| 2 个真实 read                               |     否 |
| 1 个真实工具但尚无结果                             |     否 |
| 1 个真实工具，结果已返回                            |     是 |
| 结果后已经有新 assistant message                |     否 |
| title/compaction agent                   |     否 |
| investigator 单独调用一次 read                 |     是 |
| 同一消息 transform 重复执行                      | 只出现一次 |

---

# 四、warn、warn_tdd、warn_reuse 在 tool-after 恢复

## 现有根因

`Shell/ToolHookRuntime.executeBeforeGateway` 当前会从 args 中删除：

* `warn_tdd`
* `warn`
* `warn_reuse`
* `amend`

但目前只有 `amend` 被隐藏保存，并在 tool-after 恢复。

`ControlEnvelope` 中虽然保存了三个 warn 值，但各 Host 的 before hook 拿到 envelope 后没有持久保存：

* OpenCode 只保存 `_amend`；
* Mux 只保存 `_amend`；
* OMP 直接把净化后的 args 覆盖回原对象。

因此 tool-after 根本不知道被删除的 warn 值。

## 正确修改

### 1. 引入统一的 Tool Compliance Envelope

建议新增一个纯数据结构，保存：

* tool name；
* tool call ID；
* `warn_tdd` 原值；
* `warn` 原值；
* `warn_reuse` 原值；
* `amend`；
* 每个字段的状态：

  * present；
  * missing；
  * blank；
  * non-canonical；
* todo 报告质量问题；
* 是否已经输出过批评。

### 2. envelope 不能只放在局部变量

最稳妥的方案是建立 transient `ToolComplianceStore`：

键：

* session ID；
* tool call ID。

值：

* compliance envelope。

before hook 写入，after hook读取并删除。

这比仅依赖 args 隐藏属性更可靠，因为 Host 可能：

* clone args；
* JSON 序列化 args；
* 在 before 和 after 之间更换对象引用。

可以额外在 hook input/output 上保存隐藏备份，但 runtime store 应作为主要通道。

### 3. 存储必须有回收机制

正常路径：

* tool-after 读取后立即删除。

异常路径：

* session abort；
* turn end；
* session shutdown；
* tool call 被 Host 拒绝；
* child session 清理；

都要删除残留 envelope。

该状态只与当前并发中的工具调用数量相关，不保存历史。

### 4. tool-after 恢复到所有历史可见 args

OpenCode：

* 恢复到 input args；
* 恢复到 output args；
* 必要时恢复到 decoded args。

Mux：

* 恢复到 decoded args；
* input args；
* output args。

OMP：

* tool_result 中恢复到 event input/args；
* 保证后续 message projection 看到恢复后的字段。

恢复的是 LLM 原始提供值，不是系统自动补上的 canonical value。

缺失字段不要伪造，否则后续无法判断 LLM 是否违规。

### 5. 恢复必须发生在 finally 性质的路径

无论工具结果是：

* success；
* error；
* network error；
* syntax diagnostic；
* livelock；

都要恢复原字段。

不能因为前面某个 after 检查提前返回而丢失。

---

# 五、warn 字段缺失时不拒绝，只在结果中严厉批评

## 现有根因

`executeBeforeGateway` 当前根据工具 capability 执行硬校验：

* FileMutation 缺 `warn_tdd` → `Result.Error`
* ProcessExecution 缺 `warn` → `Result.Error`
* SubagentDelegation 缺 `warn_reuse` → `Result.Error`

OpenCode 随后：

* 设置 hook error；
* 抛出 Tool validation exception。

OMP 返回：

* `block = true`。

Mux 设置：

* hook error。

这与期望行为完全相反。

## 正确修改

### 1. before gateway 不再因 warn 缺失返回 Error

before gateway 只应对以下问题硬拒绝：

* args 完全无法解析；
* 核心业务参数结构无效；
* 安全或权限禁止；
* amend 之类会破坏历史的控制参数非法。

warn 三字段属于 compliance 问题，不属于执行合法性问题。

因此 before gateway 应：

* 提取并删除 warn 字段；
* 记录缺失或错误；
* 始终允许工具继续执行。

### 2. schema 改为 soft-required

修改范围至少包括：

* `Shell/ToolHookRuntime.decorateAndValidateSchema`
* `Opencode/HookSchemaDecode.fs`
* `Omp/OmpToolSchema.fs`
* `Shell/MuxPluginCatalogShell.fs`

要点：

* 保留字段；
* 保留唯一 enum；
* description 用明确、严厉的 MUST 文案；
* 不进入真正 JSON Schema `required`；
* schema 自检不再要求它们必须位于 required 数组；
* 可增加 Wanxiangshu 自有 soft-required 标记。

### 3. tool result 末尾追加统一批评块

批评必须满足：

* 不替换原工具输出；
* 不把 success 改成 false；
* 不设置 hook error；
* 明确指出缺少哪个字段；
* 明确指出该字段适用于什么能力；
* 明确告诉 LLM 工具已执行，不要仅为补字段重复执行；
* 要求下一次调用改正。

建议输出结构：

* 固定机器可识别标题；
* 违规字段列表；
* 每个字段的 canonical value；
* 严厉评价；
* 下一次行动要求。

示例语气应类似：

“严重协议违例：你调用了具备文件修改能力的工具，却遗漏 `warn_tdd`。工具此次已执行，系统不会让你用参数疏忽阻塞工作，但这说明你无视了 schema 中明确列出的 TDD 与 Kolmogorov 纪律。不要重复本次工具调用；下一次调用必须完整提供规定字段。”

如果同时缺三个字段，应合并成一个批评块，不要连续追加三段。

### 4. 只追加一次

使用固定 marker，例如逻辑上的：

* `WANXIANGSHU_COMPLIANCE_REPRIMAND`

after hook 重复执行时，若输出已有 marker，不再追加。

### 5. 三个 Host 的追加位置

OpenCode：

* 放在 `lifecycleObserver.handleToolExecuteAfter` 之后；
* 因为 ProgressObserver 可能重写 todowrite 输出。

OMP：

* 放在 TodoHooks 对 todowrite 标准输出重写之后。

Mux：

* wrapper 和 hook after 统一由最后的结果装饰器追加；
* 避免 wrapper 一次、hook after 又一次。

---

# 六、todowrite 报告不足 1024 字时不拒绝，只批评

## 现有根因

当前存在多层硬拒绝。

### Schema 层

`Shell/WorkBacklogSchema`：

* 五个字段都在 required 中；
* 五个字段都有 `minLength = 1024`。

OpenCode、Mux 会使用该 schema。

Mimocode 使用：

* `strMin 1024`

OMP 虽然 schema 中不一定有 minLength，但 execute 层仍硬拒绝。

### Runtime decode 层

`Shell/WorkBacklogToolsCodec.requireReportField`：

* 少于 1024 → `InvalidIntent`；
* 缺失 → `InvalidIntent`。

### Host 直接执行层

* `Omp/TodoTool.fs` 连续检查五个字段长度并返回 error；
* `Opencode/MimoTodoTool.fs` 同样连续拒绝；
* Mux wrapper 调用严格 decoder，失败则 `success=false`；
* OpenCode `ProgressObserver` decoder 失败时甚至会 `failwith`。

## 正确修改

### 1. 分离“结构合法性”和“报告质量”

硬错误继续保留：

* todos 不是数组；
* todo item 缺 content；
* status 无法解析；
* priority 非法；
* session ID 缺失；
* 权限问题；
  -真正无法执行工具的参数错误。

软错误：

* 五个报告字段缺失；
* 字段为空；
* 字段不足 1024；
* `select_methodology` 缺失是否软化，可单独决定；不应与五个长度问题混在一起。

建议 decoder 返回两部分：

* 可执行的 `TodoWriteArgs`；
* `ReportComplianceViolation list`。

即使字段缺失，也将其解码为空字符串并继续执行。

### 2. schema 改为软要求

五个字段：

* 在 schema 中继续展示；
* description 明确要求不少于 1024 字；
* 不使用真正的 required/minLength 门禁；
* 增加 soft-min-length 元数据。

todos 的结构字段仍可保持真正 required。

### 3. 工具必须继续完成核心工作

无论报告字段长度如何：

* todos 状态必须更新；
* native todo 工具必须被调用；
* EventLog 应记录 work backlog commit；
* 已提供的报告内容必须原样保存；
* 缺失内容保存为空；
* Context Budget 应把这次成功 todowrite 视作有效 anchor。

不要因为报告写短而阻止用户的 todo 状态更新。

### 4. 批评必须列出每个字段的实际长度

结果末尾应明确列出：

| 字段                    | 实际长度 |   要求 |
| --------------------- | ---: | ---: |
| ahaMoments            |    N | 1024 |
| changesAndReasons     |    N | 1024 |
| gotchas               |    N | 1024 |
| lessonsAndConventions |    N | 1024 |
| plan                  |    N | 1024 |

只列违规字段。

批评还应说明：

* 工具已成功；
* 当前报告不足以安全承受 context folding；
* 下一次 todowrite 必须补足；
* 不要为了补报告重复刚才已经完成的 todo 状态变更。

### 5. 各 Host 的具体修改点

#### OpenCode

修改：

* `WorkBacklogToolsCodec`
* `ToolDefinitionHooks`
* `HookSchemaDecode`
* `ProgressObserver`

重点：

* ProgressObserver 不再因短报告 `failwith`；
* 先写 EventLog；
* 标准化 todo 输出；
* 最后追加批评。

#### Mimocode

修改：

* `MimoTodoTool`

去掉五个长度拒绝分支。

保留 todos 核心结构校验。

返回标准 todo 输出加批评。

#### Mux

修改：

* `WorkBacklogSchema`
* `Mux/Wrappers.mkTodoWriteWrapper`

soft decoder 后：

* 调 native todos；
* 捕获已提供报告；
* append EventLog；
* 返回 success；
* 结果末尾追加批评。

#### OMP

修改：

* `OmpToolSchema`
* `Omp/TodoTool`
* `Omp/TodoHooks`

去掉 execute 中五个长度错误。

TodoHooks 当前会覆盖结果，因此批评必须在覆盖成标准 todo 输出后追加。

## 验收用例

1. 五个字段均超过 1024：

   * 工具成功；
   * 无批评。

2. 一个字段 1023：

   * 工具成功；
   * todo 状态更新；
   * EventLog 有 commit；
   * 批评只列该字段。

3. 五个字段全部缺失：

   * 工具成功；
   * todos 正常更新；
   * 批评列五项；
   * 不抛异常。

4. todo item status 非法：

   * 仍然硬拒绝；
   * 因为这不是报告质量问题，而是核心业务参数无效。

5. after hook 执行两次：

   * 批评只出现一次。

---

# 七、修复 Context Budget 开局误触发 todowrite

## 现有根因

当前 `rebuildPhaseState` 在第一次观测且 backlog 为空时，将：

* `phaseBaseTokens = 0`

随后同一次 message transform 立即调用 `classifyPressure`。

对于默认 `foldAfterFirst=false`：

* N = 3；
* 初始阈值约为有效窗口的 25%。

CAPS、系统提示、用户首条消息本身可能已经超过窗口 25%，于是系统把“开局就存在的固定上下文”错误认成“本阶段新增消耗”，立刻触发紧急 todowrite。

此外，第一次没有真实 token usage 时，当前兜底是：

* `totalBytes / 2`

这属于未经校准的粗估，也可能严重高估首轮 token。

所以用户猜测的两个方向都部分正确：

* 初始 context baseline 计算错误；
* 首次 token 估算缺乏可信度控制。

## 正确修改

### 1. 第一次观测只做校准，绝不触发提醒

`rebuildPhaseState` 应返回额外信息：

* 是否为本次刚初始化的 baseline。

若本次刚初始化：

* 保存 state；
* 保存 usage；
* 不调用 emergency injection；
* 直接返回原消息。

这是最关键的防误触发门槛。

### 2. backlog 为空时也必须使用真实初始 baseline

删除特殊语义：

* `State=None && backlog.IsEmpty → phaseBaseTokens=0`

应统一为：

* `phaseBaseTokens = stableTokens`
* `phaseStartTodoOrdinal = 当前 todo ordinal`
* `backlogTokensAtPhaseStart = 当前 backlog token`

即使 backlog 为空，CAPS、系统消息、首条用户消息也属于阶段基线，不属于阶段新增消耗。

初始化时若：

* 当前 tokens = 30,000；
* phase base = 30,000；

则新增量为 0，不会立刻触发。

### 3. 引入 UsageConfidence

token 数值应携带可信度：

| 来源                        | 可信度                | 可否触发 emergency |
| ------------------------- | ------------------ | -------------: |
| Host 返回的真实 usage          | Observed           |              是 |
| 基于上次真实 usage 的 bytes 比例估算 | CalibratedEstimate |              是 |
| 第一次 `bytes / 2` 粗估        | BootstrapEstimate  |              否 |

第一次只有粗估时：

* 仅记录 baseline；
* 不触发 todowrite。

至少取得一次真实 usage 或完成一次校准后，才允许进行压力判定。

### 4. 必须有正的阶段增长

在判定 emergency 前增加基本条件：

* `currentTokens > phaseBaseTokens`
* 或新增量超过最小噪声区间。

避免 token 统计轻微抖动造成提醒。

### 5. investigator 不得进入 Todo Emergency

通过前面拆分的 `ContextBudgetPolicy`：

* investigator 不执行 `RequireTodoWriteEmergency`；
* browser/executor/title/compaction 同样不执行；
* 这些 agent 到达极限时交给 Host compaction 或专用策略。

不能向一个无 todowrite 权限的 agent 提醒“立刻调用 todowrite”。

### 6. NudgeTrack 要表示 episode，而不是 transform 次数

当前 synthetic nudge 每次 transform 会被剥离，随后可能重新注入，并增加 `NudgeCount`。

更稳妥的状态应记录：

* context budget episode ID；
* signal 时的 todo ordinal；
* signal 时 tokens；
* stable synthetic nudge ID。

在压力仍成立且 todo ordinal 未推进时：

* 输出中继续包含同一条提醒；
* 不把每次 message transform 计作新提醒；
* 不不断生成新 GUID；
* 不快速耗尽 “最多 2 次” 限制。

只有以下情况才建立新提醒 episode：

* LLM 完成一次 todowrite，但压力仍再次跨阈值；
* phase baseline 重建；
* compaction 后重新进入压力区。

### 7. 成功 todowrite 后正确重置 baseline

成功的 todowrite EventLog commit 应成为明确 phase boundary：

* 更新 phase base；
* 更新 todo ordinal；
* 清除 emergency signal；
* `NudgeCount` 重置；
* 同一 transform 不得立刻再次触发。

## Context Budget 验收测试

### 开局测试

1. CAPS 很大，占窗口 30%：

   * 第一轮不提醒。

2. 没有真实 usage，只能 bytes/2：

   * 第一轮不提醒。

3. 首次 transform 连续调用两次：

   * 仍不提醒；
   * baseline 不重复漂移。

### 正常触发测试

4. baseline 之后新增 token 未达阈值：

   * 不提醒。

5. 新增 token 跨过 F 阈值：

   * 注入一次紧急提醒。

6. 同一个消息集合重复 transform：

   * 提醒仍可见；
   * 不增加提醒 episode 数；
   * synthetic ID 不变化。

7. 成功 todowrite：

   * baseline 重建；
   * 提醒消失；
   * 不立即再次触发。

8. investigator 上下文超过阈值：

   * 不出现 todowrite 提醒。

9. phase base 已超过窗口 80%：

   * 走 compaction；
   * 不再要求 todowrite。

---

# 八、建议新增的共享模块

为了避免四个 Host 再次漂移，建议增加两个共享概念。

## 1. AgentProjectionPolicy

职责：

* 给定 host、agent、是否 child session；
* 输出四个独立 policy；
* 所有 Host 统一调用。

禁止每个 Host 自己写一套 investigator 特判。

## 2. ToolCompliance

职责：

* 根据 tool capability 判断哪些软字段适用；
* 提取控制字段；
* 产生 compliance envelope；
* 检查 warn 字段；
* 检查 todo 报告长度；
* 生成统一批评文本；
* 判断结果中是否已经有批评 marker。

Kernel 保持纯函数：

* 输入 tool、args；
* 输出净化 args、envelope、violations。

Shell/Host adapter 负责：

* 保存 envelope；
* 执行工具；
* 恢复字段；
* 修改结果文本；
* 清理 transient store。

---

# 九、推荐实施顺序

## 第一阶段：先解除错误拒绝

1. 将三个 warn 的 before 校验改为软违规。
2. 将 todowrite 五字段长度改为软违规。
3. 修改所有 Host schema，避免真正 required/minLength 先行拒绝。
4. 确认工具仍然正常执行。

这一阶段完成后，至少不会继续阻塞工作。

## 第二阶段：建立 after compliance 闭环

1. 新增 envelope/store。
2. before 保存控制字段和违规。
3. after 恢复 warn 字段。
4. after 最后追加批评。
5. session/abort/turn 进行 store 清理。

## 第三阶段：拆分消息策略

1. 拆 `ProjectionPolicy`。
2. investigator 开启 CAPS。
3. investigator 开启 Parallel Hint。
4. investigator 关闭 Todo Emergency。
5. 保持其工具权限不变。

## 第四阶段：重做 Context Budget bootstrap

1. 初次观测只初始化。
2. 初始 phase base 使用当前稳定 tokens。
3. 引入 usage confidence。
4. 使用稳定 emergency episode。
5. 成功 todo 后重置。

## 第五阶段：跨 Host 契约测试

同一组输入依次运行：

* OpenCode；
* Mimocode；
* Mux；
* OMP。

断言四个 Host 在以下方面一致：

* warn 缺失不拒绝；
* todo 报告短不拒绝；
* 批评内容一致；
* 控制字段在历史中恢复；
* investigator 有 CAPS；
* investigator 有并行提示；
  -开局不出现 emergency todowrite。

---

# 十、最终验收标准

全部满足后才能判定修复完成：

1. investigator 收到 CAPS，但没有新增写权限。
2. 一个真实工具调用完成后，下一轮稳定出现并行工具提示。
3. 一个真实调用加任意 synthetic ToolPart，仍视为单工具调用。
4. warn 三字段在 tool-before 对真实工具隐藏，在 tool-after 恢复到历史 args。
5. warn 缺失不阻止工具执行。
6. warn 缺失时结果保留原输出并追加一次严厉批评。
7. todowrite 五个报告字段不足 1024 时仍更新 todos。
8. 短报告仍写入 EventLog，并追加实际长度清单。
9. invalid todo status 等真正业务错误仍然拒绝。
10. 第一轮无论 CAPS 多大都不触发 emergency todowrite。
11. 无真实 token usage 的 bootstrap 阶段不触发。
12. 达到真实增长阈值后能正确触发。
13. investigator 永远不会收到 todowrite emergency。
14. 重复 transform 不重复 CAPS、hint 或批评。
15. OpenCode、Mimocode、Mux、OMP 行为完全一致。
