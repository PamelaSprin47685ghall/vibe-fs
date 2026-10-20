# context-compression — WHAT

## [001] 不推导容量，只向模型披露计量与声明常量

严禁从自身观察推导、估算或缓存任何模型上下文窗口大小；禁止上下文余量、模型容量表，或把字节/token 换算成窗口比例的逻辑。唯一允许的字节计量为合法的文件与进程计量。

可以披露的只有两类事实：已经发生的计量，与 provider 自己声明的常量。宿主向模型披露本次请求的计量数字（`total / used / cached`，见 [031]），以及在 chat.params 屏障处读到的 provider 对当前模型声明的 context limit（opencode 模型配置里的值，宿主只读不推）。二者都只是模型判断自身状态的 datum——模型是唯一知道哪些上下文对任务重要的主体，披露把选择权交给它。宿主不得由这些数字推导窗口大小、剩余预算，也不得替模型做出压缩决定。

## [002] 不主动预测溢出

在请求发送前，宿主严禁判断是否接近容量上限，禁止根据投影长度比例、剩余预算或累计 Token 主动选择压缩点。真实的 provider attempt 失败是唯一的恢复触发信号。

向模型披露计量（[001]）不构成溢出预测：披露不改变起点，不产生任何自动压缩动作；模型在视图声明中的选择是显式请求，不是宿主预测。

## [003] 取消独立 Y 轴与 200 KiB Blogger delta 合同

系统不再设独立的 Y 轴、Blogger、Chronicle、200 KiB delta 切块或 `MemoryPreamble` / `FrozenRecordPrefix` 物化管线。历史截断与保留完全由模型在编程面显式提交的 ANCHOR `view` 声明（`prefixUntil` / `thenMap`，见 [031]）决定；完整 turn 的覆盖由 `view` 声明的锚点边界不证自明，不另设独立的 Y 物化覆盖证明。

## [004] 输出预算属 provider

系统不计算输出应占用的 Token 数量，亦不检查压缩比例。对输出的唯一内容校验为 `isValidTerminal`（非空且非纯 XML）。

## [005] 失败不分类

控制流仅识别快照的 `Outcome`（`Completed | Failed | Aborted`），严禁根据错误字符串或异常类型细分溢出、网络或限流等原因。

## [006] 重试不构造 prefix probe，失败预算有界

每个物理 retry attempt 拥有全新 physical identity；系统已废除 Y 恢复材料与 prefix probe 候选选择机制。provider attempt 失败后的恢复策略统一为：**第一次原样重试，第二次把该 Provider 拉黑后重试，以此类推**（见 `provider-attempt-recovery` [021]）。每次确认失败经 `ProviderFailureLedger` 记账并推进 `ProviderFailureBudget` 连续失败计数；`verdict` 为 `Exhausted`（单一 `DefaultBudget`）时停止自动重试。业务主请求（`WorkMain` 的有效成功）一旦成功，清零连续失败计数。

## [007] 按 RequestKind 分派结局

每个 attempt 的结局按 `ProviderRequestKind` 进行确定性分派；RequestKind 必须来自 attempt 的 typed context / durable receipt / accepted continuation evidence，而非角色猜测或错误文本。同种 RequestKind 的同种结局必须产生完全一致的后继动作。`InteractionRepair` 成功不得清零连续失败计数；只有 `WorkMain` 的有效成功可以清零（`BloggerMain`、`BloggerSquash` 与 `StrengthReplica` 已随 clean-break 彻底退役）。

## [008] 主工作会话不发压缩请求

主工作会话（Work Session）严禁向主模型发送请求压缩历史的指令。上下文裁剪与保留完全由模型在 [031] ANCHOR `view` 声明中自主选择。

## [013] 诊断不是控制输入

可观测诊断日志严禁作为控制流输入，不得使用任何诊断字段驱动 retry 的分支决策。

## [017] 真实用户消息与 view 取代规则

没有显式 view 替换时，真实 Host 内容原样保留；显式替换按 [031] 的最新完整区间规则执行，区间内的内容及旧摘要一起被新摘要取代。无独立编号的内容归所属区间，不得借缺少编号在区间替换之外另行删除。

## [019] 重锚边界与辅助注入可见性

任何代表上下文重锚的操作（如 `ContextReanchored` 或 `TenureReanchored`），必须将旧 provider horizon 中的辅助注入可见性归零。持久化事件保留作为审计事实，但新 horizon 的初始消息不再回放旧辅助材料，仅在后续各自正常触发时逐步重新生成。

## [023] retry 全事件驱动且时间无关

Provider retry 的 correctness path 严禁读取 wall clock、构造 `TimeSpan`、调用 timer/deadline/Delay 或以超时作为状态转换。已废除等待 Blogger open producer 的阻塞逻辑，确认失败且宿主停止重试后即按有界预算推进原样重试或拉黑 Provider 后重试。

## [030] assume 调用原样保留

`assume` 是短小但高价值的判断承诺点。在未被模型显式 `thenMap` 折叠之前，承载 `assume` 调用的原始 Host 消息与对应 result 消息原封不动保留；不得把 assumption 文本抽出来重写、不得重新渲染参数。

## [031] ANCHOR 编址、视图声明与 token 经济

带稳定 Host 消息 id 的 user 消息及带终态 tool result 的消息获得真实锚点编号；tool call、assistant thinking 与 prose 不获得独立锚点，其内容仍归所属存活区间。上述可编址消息获得真实锚点编号，从 1 开始，按首次观察顺序全局递增；同一 session 的同一消息 id 复用编号。锚点 0 隐含存在，永远指向内容最开头；提示词必须告诉模型它的存在。0 可以被替换，但永不消失。真实锚点被替换后退出可见锚点映射，不得复活。进程重启重置编号与进程内视图状态。

生成工具接受可选 view 对象。一旦提供，形状严格为 { prefixUntil, thenMap }，两字段均必填。prefixUntil 是非负整数；thenMap 是对象映射，键是非负整数编号字符串，值只能是 null 或非空摘要字符串。不保留旧字段或兼容别名。

存活锚点划分当前内容区间：从自身位置到下一个存活锚点之前；0 从内容流最开头开始，最后一个区间到当前内容末尾。本轮 map 中 null 表示保留当前内容，摘要表示把该锚点的最新完整区间替换为摘要，包含其代表的原文、无独立锚点的内容及此前并入的摘要。替换永久生效。真实锚点消失，其新内容归左侧最近存活锚点的区间；例如先替换 6，再替换 5，第二次必须覆盖原 6 的摘要。1 消失后由 0 代表，以后替换 0 也要整段覆盖。

同轮各项以本轮输入视图的存活分区定位，不依赖对象键顺序。同轮相邻区间的摘要不能互相误吞；后续替换必须依据最新分区，不能只按最终被替换编号集合重新折叠。保存替换时的边界与先后关系，保证重放一致，且旧的末尾替换不吞掉后来新增内容。

prefixUntil 同时是价格边界与本轮修改保护边界：编号小于它的部分位于便宜的缓存前缀，本轮 thenMap 对这些编号的项静默忽略，不报错、不改内容；等于边界的编号可以替换。0 也受保护，要替换 0 可用 prefixUntil: 0。边界变化不撤销历史压缩；null 和漏项也不恢复原文。

提示词必须要求 thenMap 逐个覆盖所有编号大于等于 prefixUntil 的可见、存活锚点，保留项也要明确写 null。但运行时不以漏项为错误：漏项按 null 保留。非法形状（缺字段、非法编号、非对象 map、非法值或空白摘要）则整次调用以 VIEW_DECLARATION_INVALID 拒绝，不执行程序、不留下部分效果。

声明只供下一请求消费一次；累积压缩状态不随声明消费而消失。无声明时保持既有视图，不自行解压。摘要在真实区间位置呈现，区间内其余内容整体折叠；0 的摘要呈现于开头，0 仍是开头代表。视图是纯投影，输入由宿主提供，advisor 与交付路径复用同一投影规则。

应用回执包含声明、保留/摘要/折叠计数、累计替换编号与 token 经济 total / used / cached。used 是本次实际内容的 token 数（含摘要），cached 是本轮保护范围内现有呈现的 token 数（包括历史摘要），total 是投影前消息的 token 数；计量用 gpt-tokenizer o200k_base。锚点以 `[ANCHOR: N]` 后紧接实际 NUL（U+0000）和 BOM（U+FEFF）的格式直接呈现在所属 user 消息及 tool result 内容的开头，不在尾部另列消息 ID 映射。编号与标记字节在同一进程内稳定；新增消息、消费声明或改变 prefixUntil 不得重写未被显式替换的历史标记。标记只属于请求投影，不写回原始 Host 消息；0 始终隐含存在。

Pair Hint 另披露 provider 实际计费的缓存命中、未命中 token 数及其声明的 context limit（[001]）。这些都是 datum，不授予宿主预测溢出或自动压缩的权力。
