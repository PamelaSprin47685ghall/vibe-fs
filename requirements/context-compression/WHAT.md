# context-compression — WHAT

## [001] 不推导容量，只向模型披露计量与声明常量

严禁从自身观察推导、估算或缓存任何模型上下文窗口大小；禁止上下文余量、模型容量表，或把字节/token 换算成窗口比例的逻辑。唯一允许的字节计量为合法的文件与进程计量。

可以披露的只有两类事实：已经发生的计量，与 provider 自己声明的常量。宿主向模型披露本次工具结果的 `turnBudget` 倒计时后缀（见 [034]），并在 chat.params 屏障处读取并由 Pair Hint 披露 provider 对当前模型声明的 context limit（opencode 模型配置里的值，宿主只读不推）以及 provider 实际计费的缓存命中与未命中。二者都只是模型判断自身状态的 datum——模型是唯一知道哪些上下文对任务重要的主体，披露把选择权交给它。宿主不得由这些数字推导窗口大小、剩余预算，也不得替模型做出压缩决定。

## [002] 不主动预测溢出

在请求发送前，宿主严禁判断是否接近容量上限，禁止根据投影长度比例、剩余预算或累计 Token 主动选择压缩点或触发自动裁剪。真实的 provider attempt 失败是唯一的恢复触发信号。

向模型披露计量（[001]）与工具结果携带的 `turnBudget` 后缀（[034]）不构成溢出预测：披露不改变起点，不产生任何自动压缩动作；上下文重置仅由模型在显式调用 `resetScope` 宿主工具（见 [033]）时触发，不是宿主预测。

## [003] 取消独立 Y 轴与 200 KiB Blogger delta 合同

系统不再设独立的 Y 轴、Blogger、Chronicle、200 KiB delta 切块或 `MemoryPreamble` / `FrozenRecordPrefix` 物化管线。上下文的清理边界完全由模型显式调用 `resetScope` 宿主工具作为唯一执行屏障（见 [033]）决定；最新一次 reset 之后的活跃 scope 原样保留，不另设独立的 Y 物化覆盖证明。

## [004] 输出预算属 provider

系统不计算输出应占用的 Token 数量，亦不检查压缩比例。对输出的唯一内容校验为 `isValidTerminal`（非空且非纯 XML）。

## [005] 失败不分类

控制流仅识别快照的 `Outcome`（`Completed | Failed | Aborted`），严禁根据错误字符串或异常类型细分溢出、网络或限流等原因。

## [006] 重试不构造 prefix probe，失败预算有界

每个物理 retry attempt 拥有全新 physical identity；系统已废除 Y 恢复材料与 prefix probe 候选选择机制。provider attempt 失败后的恢复策略统一为：**第一次原样重试，第二次把该 Provider 拉黑后重试，以此类推**（见 `provider-attempt-recovery` [021]）。每次确认失败经 `ProviderFailureLedger` 记账并推进 `ProviderFailureBudget` 连续失败计数；`verdict` 为 `Exhausted`（单一 `DefaultBudget`）时停止自动重试。业务主请求（`WorkMain` 的有效成功）一旦成功，清零连续失败计数。

## [007] 按 RequestKind 分派结局

每个 attempt 的结局按 `ProviderRequestKind` 进行确定性分派；RequestKind 必须来自 attempt 的 typed context / durable receipt / accepted continuation evidence，而非角色猜测或错误文本。同种 RequestKind 的同种结局必须产生完全一致的后继动作。`InteractionRepair` 成功不得清零连续失败计数；只有 `WorkMain` 的有效成功可以清零（`BloggerMain`、`BloggerSquash` 与 `StrengthReplica` 已随 clean-break 彻底退役）。

## [008] 主工作会话不发压缩请求

主工作会话（Work Session）严禁向主模型发送请求压缩历史的指令，宿主绝不在后台自行发起上下文裁剪。上下文重置完全由模型在细粒度任务块（状态闭环）的自然边界显式调用 `resetScope` 工具（见 [033]）自主决定。

## [013] 诊断不是控制输入

可观测诊断日志严禁作为控制流输入，不得使用任何诊断字段驱动 retry 的分支决策。

## [017] 真实用户消息与 scope 清理

用户消息不是上下文清理的边界，且永不删除。在执行 `resetScope` 清理时，所有 operator 用户消息（包括干净 prompt 与 steer）必须全部按原序保留在两个 `resetScope` 调用之间；活跃 scope 内最新一次 `resetScope` 之后的全部消息原样保留。

最新一次 `resetScope` 之前，除所有保留的用户消息与历次成对保留的 `resetScope` 调用及其结果之外，全部 assistant 文本、thinking、非 `resetScope` 工具调用及结果均被彻底清除。系统不得借缺少编号在保留规则之外另行删除用户消息。

## [019] 重锚边界与辅助注入可见性

任何代表上下文重锚的操作（如 `ScopeReanchored`、`ContextReanchored` 或 `TenureReanchored`），必须将旧 provider horizon 中的辅助注入可见性归零。持久化事件保留作为审计事实，但新 horizon 的初始消息不再回放旧辅助材料，仅在后续各自正常触发时逐步重新生成。
## [023] retry 全事件驱动且时间无关

Provider retry 的 correctness path 严禁读取 wall clock、构造 `TimeSpan`、调用 timer/deadline/Delay 或以超时作为状态转换。已废除等待 Blogger open producer 的阻塞逻辑，确认失败且宿主停止重试后即按有界预算推进原样重试或拉黑 Provider 后重试。

## [030] assume 调用原样保留

`assume` 是短小但高价值的判断承诺点。在活跃 scope 内，承载 `assume` 调用的原始 Host 消息与对应 result 消息原封不动保留；不得把 assumption 文本抽出来重写、不得重新渲染参数。

当调用 `resetScope` 进行上下文重置时，旧 scope 内的 `assume` 调用与其他非 resetScope 工具调用一并被清理；若模型在下一个 scope 仍需沿用关键假设或确切判断，必须由模型将其作为经验发现逐字原样抄写进 `oldScopeVerboseVerbatimExperience`。

## [033] resetScope 屏障与原文逐字保留

`resetScope` 是系统核心宿主工具（loadMode: "essential", concurrency: "exclusive", approval: "read"），其签名如下：

```ts
resetScope(
  oldScopeVerboseVerbatimExperience?: string | null,
  newScopeObjective: string,
  newScopeTurnBudget: number
)
```

最新一次 `resetScope` 调用是**唯一**的上下文边界。在发往 provider 的每次请求副本中，清理上一次 `resetScope`（或 session 起点）到本次调用之间的全部上下文，但**必须且仅**保留：
1. 所有 operator 编写的 `role: "user"` 消息（干净 prompt、steer，以及具有 user 归属的消息），按原始顺序夹在历次 `resetScope` 调用之间；
2. 历次 `resetScope` 调用及其成对的工具结果（按 `tool_call_id` 成对保留，清除旧调用上的 thinking 块）。

并发屏障规则：包含最新 `resetScope` 的同批工具调用中，`resetScope` 作为执行 Barrier。排在它之前的工具调用先执行并随旧 scope 一并被清理，排在它之后的工具调用在其返回后执行并保留在新 scope 中。最新一次 `resetScope` 之后产生的活跃 scope 消息原样保留。

关键原文逐字保留义务：由于旧 scope 的原始工具输出与过程文本在清理后被彻底删除，如果函数签名、报错堆栈、精确行号或关键事实在下一个 scope 仍须逐字使用，模型必须将其原样抄写进 `oldScopeVerboseVerbatimExperience`。

工具执行成功后原样返回 `Scope reset. Objective: <newScopeObjective>`；只在发往模型的请求副本中给结果追加新预算倒计时（见 [034]）。

## [034] turnBudget 递减、后缀与提醒

1 次 LLM 请求计为 1 turn。会话状态机在 `turn_start` 时将当前活跃 `turnBudget` 减 1，下限为 0。

1. **结果后缀**：每条已完成工具结果首次进入当前 prefix epoch 的 LLM 请求副本时，记录当时的 `turnBudget`，在结果末尾呈现 `\0\uFEFF[Remaining turnBudget: N]`。**同一 epoch 后续请求必须逐字重放该结果首次呈现的后缀**，不可用最新剩余预算批量改写历史结果；新结果首次呈现时仍显示当时的当前预算。`resetScope` 结果同理；仅提交的 `ScopeReanchored` 允许在新 epoch 重新建立尾巴。真实工具结果、持久化记录与用户原始消息绝不写入该后缀。再次热修正不可重复追加。
2. **用户消息尾巴与预算作废**：当新的 operator 用户消息（干净 prompt 或 steer）到达时，旧预算立即作废（归零）。在发往模型的请求副本中，每条 operator 用户消息末尾均拼接提醒：
   `[System: Estimate the turnBudget for the first fine-grained task block (state closed-loop). Then call resetScope immediately, before doing anything else.]`
   持久化存储中的原始用户消息不被改写。
3. **未初始化提醒**：当最新 operator 用户消息之后尚未发生 `resetScope` 调用时，**新首次呈现**的非 `resetScope` 工具结果在请求副本中携带上述初始化提醒，而不显示预算后缀，直到模型完成一次 `resetScope`。之前已呈现的结果不可因新用户 steer 或预算作废而改变尾巴；普通 steer 不是冷 prefix 边界。
4. **零预算弱提示**：当 scope 已初始化且 `turnBudget` 降至 0 时，若当前请求不是 side-request 快照，宿主可在请求副本末尾追加一条弱提醒用户消息（`steering: true`，不持久化），建议模型在当前任务状态闭环的自然边界调用 `resetScope` 开启新闭环并换取新预算。该弱提醒绝对不得强行中断、暂停或阻断正在进行的工具工作。

## [035] ScopeReanchored 冷前缀边界

`resetScope` 调用的落地构成明确的冷前缀边界（Cold Prefix Boundary）。

当执行并提交一次合法的 `resetScope` 时，系统向持久化日志追加一条不可逆的 `ScopeReanchored` 事实，记录前一 epoch、新 epoch 以及本次 `resetScope` 的精确调用身份（`toolCallId`）。`ActivePrefixEpoch` 恰好加一，且系统幂等清空已缓存的 provider wire 前缀快照。

`prefix-stability` 将重锚后的下一次请求视为全新 epoch 的起点，重新建立精确字节前缀，不把旧 scope 清理造成的历史字节缺失判定为前缀漂移或违反同 epoch 前缀追加律（prefix-stability [001]）。同一 `toolCallId` 严禁重复产生重锚事实。

## [036] 跨会话传递 full post-scope view 的 TOML 承载

所有跨会话上下文传递统一遵循 AXIOM 7（prose 属于指令与主报告，骑乘 `# ...` 注释头；材料属于结构化参考数据，骑乘 TOML 数据平面表格）：

1. **父到子委派（`fork` / `resume` / `commission` / `devops` / `dag`）**：调用方传入的 charge / intent 散文按原样渲染为 `# ...` 注释头；调用方当前的完整 post-scope LLM 可见消息视图（经最新 `resetScope` 清理后的全部保留消息）作为只读背景材料渲染为合法的 Synthetic TOML（如 `[[context]]` 表）。子会话不隐式继承父会话的物理工具调用，也不接受局部材料过滤选择。
2. **顾问咨询（`advisor`）**：与 fork 载荷形式一致，由单一请求文档承载：咨询意图作为 `# ...` 注释头，调用方当前的完整 post-scope 消息视图渲染为 TOML 数据平面（如 `[[context]]` 表）；顾问自然停机后交回的是其末轮 assistant 散文，作为 `# ...` 注释呈现，不返回顾问的完整 post-scope 视图。
3. 传递的数据在最终边界统一 pretty print 为合法 TOML，禁止将拼好的 TOML 作为转义字符串嵌套在字段中。

## [037] 子会话自然停机终态与 deliver 工具分立

子代理会话与生命周期管理实行完成机制与工具分立：

1. **子代理自然停机完成**：Engineer 与 DevOps 角色彻底移除子代理 `this.deliver` 成员与相关工具。子代理完成工作后自然停止输出（无未决工具调用的干净终态）；宿主在观察到子代理自然停机后，把该终态轮次的 assistant 散文（正文散文、空 carry）作为完成材料交回给父会话的 `join`，并在父方边界渲染为 `# ...` 注释。宿主不捕获子代理完整的 post-scope LLM 可见消息视图，返回材料里也绝不出现 `[[session]]` 表。
2. **免除子代理催促**：Engineer 与 DevOps 免除任何完成催促（nudge），允许自然停机。仓内所有教导子代理如何调用 deliver 返回、如何撰写特定交付语法的提示词全部删除。
3. **Manager 与 Plan 的生命周期 deliver 保持**：宿主保留供 Plan（S1–S3 晋级与接力）及 Manager（S4 交付执行）使用的宿主生命周期工具 `deliver`（见 `work` [025]–[026]）。该工具仅用于驱动任期接力与阶段推进事实，不挂接任何上下文或材料选择数组。


## [038] Scope 与 Pair Hint 的独立所有权

`wanxiangli/pair-hint` 是 scope 过滤、提示拼接和预算状态的唯一程序所有者。每个会话独立维护当前 prompt 身份、初始化状态与 `turnBudget`；宿主只能将真实 turn、prompt、`resetScope` 和会话结束事件交给该对象，不再自建第二份预算表或重复实现 filter/suffix 决策。

模型消息处理只有一个执行时机：**即将发往 LLM 时**。`wanxiangli/pair-hint` 的自治 ScopeSession 接受每轮请求的中立消息视图（kind、operator、prompt key、调用/结果身份与分段位置），输出保留位置、写入目标和后缀内容。OMP 与 OpenCode 两个 Host adapter 只解码自己的真实 envelope，并把这个 plan 应用到本次请求副本；领域对象既不认识 `role/content`，也不认识 `info/parts/state.output`。用户消息尾巴与工具结果尾巴是两种可独立呈现的注入，统统不修改真实工具执行结果、`tool_result`/`tool.execute.after` 回调、用户原文或持久化历史。

`Pair Hint` 原有的持久落点与 synthetic skill 消息仍由 Host 原生机制提供，不能与用户消息、普通工具输出的两条注入线混为一谈；宿主只负责物理消息呈现与日志定位，不再负责 scope 的领域判断。对无法观测的宿主行为不得声称执行屏障已经成立，必须由实际宿主测试证实。

## [039] OpenCode 发往模型前的热修正

OpenCode provider messages transform 必须在宿主持有的原消息数组绑定上替换**本次请求副本**里的元素，不能仅通过赋值 `outObj.messages` 指向新数组。对用户文本注入强提醒，对已完成或失败的工具结果字段显示预算或强提醒；原始 Host 消息对象、工具回调结果与 durable history 保持原样。对已删除旧 scope 的结果不得重新插入。物理断言由 `host-boundary/tests/scope-hni-physical.test.mjs` 承担。

## [040] 同消息工具调用／结果配对

从 `ISessionSnapshotPort` 获取的会话视图可能把多个调用和结果装在同一条 assistant 消息的不同 parts 里。中立过滤必须按 `toolCallId` 同时保留历史 resetScope 调用和同消息内的结果，并按最新 reset 在同批工具块中的位置裁剪；先行非 reset 工具的结果仍应删除。跨会话 Fork、Fission、Advisor 等上下文取材均使用相同的中立过滤计划，不另设 raw-message 判定。

## [041] ScopeSession 退场

OpenCode 的 `session.deleted` 事件到达时，仅清除该物理会话 ID 在 wanxiangli 中的 scope state（prompt 身份、初始化、turnBudget），不得影响同时存在的其他会话。OMP 的 `session_start`、`session_switch`、`session_branch` 继续清理其明确的会话状态。清理不写日志、不替换真实工具结果、不触碰工作历史。
