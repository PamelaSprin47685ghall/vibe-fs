# Fable/JS 互操作与深度实施避坑指南

本附录针对万象术在 Fable 编译 JS 环境、OpenCode v1.17.13 架构及 Mux 环境下，针对七项核心漏洞的落地实施提供深度的避坑、防御及细节校验指导。

## 一、Fable 与 JS 互操作中的隐性地雷

### 1.1 int64 比较与 JS Number 精度断层

在 F# 核心库中，`int64` 是通过自定义对象（包含高 32 位和低 32 位数值字段）在 JS 中模拟实现的。

**避坑警告**：当从 Host 获取时间戳（通常为 JS 的双精度浮点数 `number`，对应 F# 的 `float`）并与 F# 的 `int64` 进行直接比较（例如 `msgTimeMs >= injectedAt`）时，若其中一方被转换为 `obj` 传入，编译后的 JS 将使用 `===` 或原生的 `>` 运算符。这会导致 JS 运行时将 F# 的 `int64` 结构体与原生 JS 数值进行直接对比，进而产生恒为 `false` 的静默错误。

**定案方案**：在整个 Fallback 与 Nudge 系统中，时间戳统一使用 `float`（即 JS 原生的 `Number`，精度足以安全表示毫秒级时间戳，直至公元 285428 年）或者进行显式的双向类型转换，禁止混合使用 `int64` 和 `float` 进行逻辑比较。

### 1.2 Object.defineProperty 与只读 Host 对象的静默冲突

部分 Host（如 Mux/OpenCode）的 `args` 或 `config` 属性可能是通过 Object 冻结、getter 配置或代理拦截实现的只读对象。

**避坑警告**：直接对这些对象进行修改（如 `args?warn_tdd <- ...`）在 JS 严格模式下会抛出 `TypeError`，在非严格模式下则会静默失败（属性未成功写入，但程序继续运行）。

**定案方案**：在执行前终极净化阶段，不要假设可以直接对 Host 传入的参数对象进行写入。应当首先通过浅拷贝（`assignInto` 或 `Object.assign`）甚至深拷贝克隆一个自由的对象副本。对此副本完成净化、校验后，再行使后续的执行和调用，绝不在 Host 的原始参数对象上直接测试边界。

### 1.3 Object.ReferenceEquals 对 JS 临时重构对象的失效

在 F# 中常用 `System.Object.ReferenceEquals` 或其别名来判断消息的部分或整体在 transform 过程中是否发生了改变。

**避坑警告**：OpenCode 和 Mux 在传递消息列表或零件列表时，可能会在每次生命周期 Hook 触发前对其进行内部反序列化。这会导致即使消息内容完全没有改变，前后两次收到的 JS 对象引用地址也完全不同。此时，依赖 F# 引用一致性判断的缓存和防重复机制会彻底失效。

**定案方案**：缓存正确性和键只使用明确的 revision/generation/counter。引用变化时可用规范化 UTF-8 字节或前缀字节相等性作可观察断言（例如验证 transform 输出是否确实未改变），但不得计算、持久化或依赖内容 hash/fingerprint 作为缓存命中或防重复依据。决不能将引用等价性作为防重复和防刷新的核心依据。

## 二、单线程事件邮箱与异步租约（Lease）的防竞态设计

### 2.1 异步 continuation lease 的状态流转与自动失效

为了防止"状态机做出了 SendContinue 决策，但在调用 Host API 之前用户按了 Esc"这类物理竞态，所有的 Continuation 动作必须被约束在一个受代次控制的租约中。

**避坑警告**：若只在状态机中维护一个 `SessionFallbackState` 的 `Lifecycle` 字段，由于在途的 `session.prompt` 调用是异步 Promise，在 Promise 挂起期间，用户按了 Esc。此时，状态机的 `Lifecycle` 的确变为了 `Cancelled`，但已经进入微任务队列的 `prompt` 仍会强行将消息发送出去。

**定案方案**：每一个 `SendContinue` Action 必须关联一个唯一的 `ContinuationLease` 对象。
* 租约包含 `humanTurnID` 和 `cancelGeneration`；
* 在 Action 真正执行 Host 调用的"最后一毫米"，必须从 per-session mailbox 中读取该 session 当前最新的 `cancelGeneration`；
* 只要发现当前代次已大于租约创建时的代次，此租约必须被强制宣布为 `Invalidated`，直接拦截调用。

### 2.2 门禁信号的重置收敛

在 Abort 事件被确认为真且更新了 `Cancelled` 墓碑后，必须立即重置该 Session 的所有衍生标志。

**避坑警告**：如果仅修改了 `Lifecycle = Cancelled`，但没有清除 `AwaitingBusy` 或 `SubsessionPending` 等异步门禁，那么下游的 `needFallbackContinue` 计算由于看到这些门禁依旧为 `true`，仍然会继续产出"需要 fallback 续跑"的诊断结果，使得状态机制动失效。

**定案方案**：必须设计一个原子重置方法（`CancelEpisode`），当且仅当发生 Abort 时，该方法会强行将当前 Session 的 `AwaitingBusy`、`SubsessionPending`、`EventHandlingActive` 置为 `false`，并把 `BusyCount` 归零。这些门禁信号在生命周期终止时没有资格保留。

## 三、控制字段净化与 Schema 静态增强的协调防御

### 3.1 Zod/Effect 模式下软字段 metadata 的同步更新

在改写 OpenCode 的 tool parameters 时，必须在 `properties` 中展示软字段，并同步写入 description、examples 和 `x-wanxiangshu-soft-required` / `x-wanxiangshu-soft-min-length` 元数据；不得把它们加入 Host 会执行的 `required` 数组。

**避坑警告**：如果把 `warn_tdd`、`warn` 或 `warn_reuse` 写入 Host 强制执行的 `required`，或把报告质量写成 Host 强制执行的 `minLength`，漏填/短报告会在 hook 之前被拒绝，工具无法执行，违反软合规契约。

**定案方案**：在 schema 最终导出前，万象术必须调用统一 decorator 写入软字段的 description/examples 和 `x-wanxiangshu-*` metadata。若 host 支持不参与校验的说明性 enum，可在 metadata/examples 中记录唯一推荐值；不得在可执行 schema 中放置会触发硬拒绝的 `required`、`minLength` 或 enum。

### 3.2 应对 Host 参数重构的"净化后断言"

由于 OpenCode/Mux 在 Hook 执行前后可能进行深拷贝或重新序列化，导致 properties 中的多余字段再次出现在入参中。

**避坑警告**：在 `tool.execute.before` 中删除了 `output.args` 里的字段，但在真实执行 execute 之前，Host 又根据之前的 tool call 记录重新反序列化出了一份干净的 `args`，此时控制字段死而复生。

**定案方案**：引入执行前终极净化。在最贴近底层工具 execute 的 wrapper 中，实现如下逻辑：

```text
IF checkControlFieldsExist(args) THEN
    LogErrorAndFailClosed()
```

通过这一层净化后断言，即使 Host 机制发生漂移，敏感工具也会在控制字段泄漏时直接拒绝执行，从而保证安全；控制字段缺失或报告过短不触发该拒绝。

## 四、上下文预算公式失效与测量降级

### 4.1 UTF-8 字节数与 Token 密度的保守估算

当 Host 没有提供实时的 token counts，且历史 `LastUsage` 的测量数据也为空时，系统会退回 `None` 并关闭保护。

**避坑警告**：不能使用单一的 `bytes / 4` 判定。因为：
* 中文字符在 UTF-8 中占用 3 个字节，但在很多 tokenizer 中一个汉字就是一个或半个 token，字节/token 比例接近 3:1；
* 代码、长路径、YAML 标记的 token 密度可能会根据标点符号的不同发生巨大抖动；
* 使用过大的估算分母会导致严重低估上下文，进而延迟 todowrite 催促，发生物理溢出。

**定案方案**：引入多语境保守估计器：
* 检测内容是否包含中文字符。若是，采用偏高估计（如每 2 个字符 1 个 token）；
* 若全为代码，按已知的高密度估计；
* 计算出的初始估算值必须乘以安全系数（如 1.25）；
* 显式标记此测量为 `MeasurementDegraded`，允许提前触发 todowrite。

### 4.2 极限阈值 0 的拦截与安全默认值

当 `MaxInputTokens` 经由各类 client 反馈解析出 0、负数或小于 5000（保留空间）的值时，系统不得将其作为合法输入上限。

**避坑警告**：直接接受 0 限制会导致 `effectiveMaxInputTokens` 无法计算，或者公式 `F` 中分子分母出现除以零及矛盾边界。

**定案方案**：设定最低安全水位：任何解析出的 limit 若低于 8192，一律回落到该 Host 的保守默认水位（如 16384 或 32768）；确保即使完全无法从 Host 获取上限，系统也能以一个适中的安全视界运行，决不静默关闭预算。

## 五、压缩周期的多层状态阻断

### 5.1 废弃物理时间窗口，改用状态代次阻断

**避坑警告**：如果系统负载高、网络出现延迟，或者模型的 reasoning 过程超过了该时间窗，当 time window 过期而 compaction 产生的 auto-continue 仍然在途中时，nudge 就会乘虚而入。在测试环境和高并发环境下，这会造成极不稳定的偶发性失败。

**定案方案**：完全废弃任何物理时间窗口。改用基于 `CompactionProjection` 状态周期的强阻断：
* 当捕获到 `experimental.session.compacting` 时，Compaction 状态立即变更为 `Compacting`，并生成新的 `compactionEpisodeID`；
* Nudge 阻断状态（`NudgeBlockReason`）变更为 `CompactionActive`；
* 直至该 compaction summary 产生、auto-continue synthetic 消息发送、对应 assistant 消息自然结束（`terminal`），且 mailbox 确认该轮无 fallback 要求、彻底闲置后，方可将 compaction 状态标记为 `Settled`；
* 此时将 `NudgeBlockReason` 回归为 `Allowed`。状态未 Settled 前，阻断持续存在，无论经历了几万毫秒。

### 5.2 Compaction 过程中原始任务的重注入防护

**避坑警告**：压缩后如果直接使用 `ReviewLoopFold` 去找，由于旧的消息已经被截断，fold 可能会返回 `None`。此后触发的 nudge 就会丢失原任务，LLM 也会失去目标。

**定案方案**：万象术的 `ReviewProjection` 是一个跨 Compaction 持久生存的状态。在 `experimental.session.compacting` 阶段：通过 `output.context` 强制重新将 `ReviewProjection` 中存储的 `original_task` 拼装为新的 front-matter 写入即将生成的 summary 中，实现物理重注入；从根本上解决压缩过程中的原任务失忆问题。

## 六、模型路由与人类轮次单一事实源

### 6.1 NewUserMessage 对 model map 缓存的原子清洗

**避坑警告**：如果仅在 fallback 状态机内部清理 `state.CurrentIndex`，由于 `FallbackRuntimeState` 的 `models` Map（曾用作运行期缓存）中依然残留有上一次 fallback 时记录的 `"provider/model"` 值，随后的 Nudge 仍然可能会读取到这个残留值，进而指派错误的模型。

**定案方案**：
* 建立统一的 `NewUserMessage` 入口；
* 原子删除 `FallbackRuntimeState` 中该 session 对应的 `injectedModels`、`injectedAts` 以及 `models` 缓存；
* 清除 `ContinuationLease`；
* 将 `HumanTurnRoutingProjection` 更新为新 user message 实际携带的模型设置，以此作为后续所有 todo/review nudge 的唯一权威路由事实源。

## 七、评审提示词中 original_task 的严格隔离

**避坑警告**：在 nudge prompt 渲染的 front-matter 中，键名决不能写成 `task`。因为万象术在重启重放（Replay）时，其折叠逻辑（`inferReviewTaskFromTexts`）只要看到 `task` 字段，就会误判定为"用户重新发起了一次 With-Review 激活命令"。这会导致 review 状态和 version 发生严重错乱。

**定案方案**：
* 在 nudge prompt 模板中，原始任务一律统一编码为 `original_task`；
* 同时附加 `prompt_origin: review_nudge` 明确其非激活语义，与首发 With-Review 的激活命令在 schema 层面彻底实现物理隔离。
