# speculative-investigation — WHAT

## [001] 启用基线与逐工具协议呈现

未配置 Predictor 模型时，系统对本机制保持无功能基线：不装饰任何工具 schema、不追加说明、不产生委托授权、不创建 Replica，全部会话的 provider 可见字节、工具权限、retry 流程、评审终结逻辑与控制流与不存在本机制时完全一致。

已配置 Predictor 模型时，仅对第 5.2 节参与工具（read/glob/grep/js-manager/js-engineer/js-devops/edit/write/mv/rm/fetch/run 共 12 个）装饰参数：必选原生整数 `estimated_readonly_rounds` 与条件参数 `self_note`，并在原工具描述之后幂等追加事实性估计说明；原有的必选项、功能说明与安全约束保持原样。

所有未参与工具（如 fork/join/chronicle/horizon/review/fission/terminal 类工具/assume/enough 等）及未判定动态工具均保持原有定义，无任何本协议增量，也不截取其同名业务字段。

估计填 0 表示当前批次完成后没有可合理展望的连续只读查证，与"未配置 Predictor"是两个可观察区分的不同状态；启用不依赖任何人工确认、指纹、外部开关或额外设置。

## [002] 来源判定、批次封口与参与子集 max

委托授权只能形成于同一个真实主模型 provider 响应内的完整工具批次：该响应按原始调用顺序冻结，且所有调用都有且只有一个 result；结果乱序到达时按原始调用顺序归一，孤儿结果、重复结果、重复 call id 或跨 user/provider 边界拼接的结果一律不构成有效批次。批次全部结果齐全前，一个 Replica 也不创建。

整批封口后，宿主只对参与调用的子集进行校验并取其最大值：
- 若整批全部调用均属不参与工具，判定为无估计机会（NoEstimateOpportunity），不读取、不校验其字段，也不对空集合求 max；
- 若参与子集的估计全为 0，判定为明确零估计（EstimatedZero），不产生授权、不创建 Replica；
- 若参与子集存在合法正数，授权轮数 N 取该子集 `estimated_readonly_rounds` 的最大值；0 不否决同批其他调用的正值；同批取值不一致是合法输入，不得要求一致或改写 max；一个完整批次至多形成一次授权。
- 不参与调用的同名字段不读取、不校验、不消费、不截取，原样保留在业务参数与历史证据中。

当前主模型已经生成的工具调用不计入预算，照常执行一次；它的选型与参数推理成本不发生第二次。

授权的形成还要求：来源是新鲜的真实 owner 输出；owner logical run 未被替代；该授权尚未 Bound 或 Closed；待消费的是合法普通 WorkMain 续行；来源不是 Replica、其他 InternalLeaf、interaction repair、显式恢复特殊分支或 prefix probe；EventStore、Host 边界与进程 fuse 健康。

准入不含任何经济或统计判断：不存在收益估计、成本模型、证据样本量、holdout 分组或预测得分作为条件。同批参与调用出现非法值（缺失、null、字符串、负数、小数、布尔、越界、或条件短记不符）按新调用参数错误处理：整批不产生新执行，不取合法子集假装成功，也不把非法值静默规范化为 0。非只读的来源批次不否决委托——当前操作照常做完，委托针对的是接下来；Replica 输出中的正预算不能产生二次委托。

## [003] 事实性估计到只读执行上限的转换

`estimated_readonly_rounds` 是主模型对任务所处位置的事实性估计，单位为整批工具完成后预期后续连续只读查证的模型请求次数；它不是意图承诺，不是分工指示，也不是已证实的事实。

该事实性估计经一次显式转换（toExecutionBudget）成为内部只读执行上限。内部计数只按运行时真实接纳的外发请求记账：一次请求可并行产生多个只读调用；纯文本结束占一轮；已外发后失败的一轮仍占一轮；同一请求的重复 transform 或 terminal 通知不重复计数。Host 截断、移除旧可见批次或发生普通压缩后，已用轮数不降低，也不得从变短的对话历史重建为更小值；owner 镜像中的旧调用不计入本次。

达到上限后禁止第 N+1 次请求外发；第 N 轮已开始的工具结果必须收完；不追加额外总结请求。Replica 不新增自动 provider 重试，重复通知与真实重新外发必须分清，不得把重发当免费轮次。

上限是安全门禁而不是必须做满的配额：同伴可随时提前结束，提前交还不是失败，主模型也不必在预算用尽后立即修改代码。

## [004] 同伴：同一 owner 身份的只读内部执行

同伴以 `InternalLeaf × Attached(owner, StrengthReplica)` 构造，继承 owner 的 participant、Role、Persona、provenance/version 与会话语言。每个 owner 恰有一个**常驻**只读副本会话：该会话随 owner 存活而被复用，不随单个决策结束而终止或重建，因此 provider 侧看到同一会话并复用其前缀缓存；常驻会话占用一份 provider 容量，直到 owner 结束才释放。每次新决策把该会话的消息基线整份替换为本决策的 mirror，旧决策的上下文不因会话复用而残留。变化的是执行用途、模型目标、可见工具集合与短期控制权，不是"扮演另一个人"。

模型目标经明确的只读委托用途，由唯一 MJS 调度权威从 Predictor 模型池选择；participant 与 Role 不改写，用途不从工具参数、用户文本、模型自述或角色名推导。Predictor 与 owner 配成相同模型是合法状态，不因模型名相同而关闭。

同伴只能调用唯一专用的只读 JS 编程面 `js-predictor`（能力严格限定为 {Read, Glob, Grep}）；`read`/`glob`/`grep` 原生工具不在同伴的可调用集合内，全部只读查证经该 JS 面完成。其模型可见工具 schema 与底层执行门禁同源：会话级权限规则在 deny 全部工具后精准放行此项。尝试写入、调用其他工具、借 fork/MCP/通用 JS 工具（如 `js-engineer`、`js-devops` 等）绕过只读能力，均 fail closed 且不产生任何实际效果；shell 命令不因"看起来只读"而入列。

删除副本必须填 0 的假条件：同伴在共享 schema 下按同一事实性含义填写，防递归靠真实身份（Replica 身份在准入端无权发出新委托），不靠填写约定。同伴填写的正数、推理文本与未执行计划不产生嵌套委托。

## [005] 真实完整交换与 digest：无专属字节上限

回传给主模型的只有同伴真实的只读工具交换：每个候选帧保留 request batch 边界、原始调用顺序、原始 arguments、真实执行结果与内容 digest，call/result 严格一对一配对。同伴的纯文本、reasoning、未执行计划与总结不进入主模型上下文；纯文本输出是提前结束信号，不是待主模型采信的研究报告。
来源整批可能包含非只读调用；Frame 构造必须过滤保留其中的只读交换（read/glob/grep/js-predictor），而不是直接因整批混入非只读工具而拒绝整个帧（UnsupportedTool）。若过滤后合法只读交换为空，则按无材料（NoMaterial）正常处理。

同伴自己看到的交换保留其真实调用名 `js-predictor`；回传注入主人会话时，该交换按主人的角色投影为其自己的 `js-<role>` 工具名（如 devops 主人即 `js-devops`），使主人看到的是它本可自行调用、结果形态一致的证据，而非一个它无权调用的工具。该重命名只发生在面向主人的投影渲染处，Frame 的材料 digest、持久化 payload 与同伴自身的 transcript 均保持原名不变。

新字段随真实 arguments 保存。不存在 Delegate 专属的字节、token、短记长度或批次大小上限，不存在按长度丢弃、保留小前缀、"过大退回零步"或等价替身规则；超过任何历史大小的完整交换仍可构建、持久化、映射与恢复。

工具原有截断、provider 上下文限制与主模型原有压缩照常工作；Frame 保存并回放实际返回给模型的结果（含原有截断标记），不得先对未截断全文计算 digest、回放时再另切一刀。digest、byteLength、UTF-8 字节计量与 payload_refs 保留，服务完整性、存储与观测：长度或摘要被篡改必须拒绝。

## [006] 授权与 Prepared 的持久化前置

一份授权至少绑定：DecisionId（由协议版本 2、owner logical run 与来源 provider run 确定性派生，不按工具完成顺序或未来目标请求派生）、OwnerSessionId、owner logical run identity、authority root 与来源 physical user message、发出该批的 SourceProviderRun、固定顺序的完整 SourceToolCallIds（包含整批中不参与调用的 ID）、RequestedRounds（参与子集 max）与 ContractRevision（当前协议版本 2）。授权不增加 SelfNote、Hint 或 TrustScore 事件。

持久化时机：来源整批完成并取得真实 owner 新输出证据后、外发副本前，写入 `DelegationRequested`；为合法普通续行冻结 target 与 mirror 后、副本首次外发前，写入 `DelegationBound`，固定 target、ReplicaSessionId 与 anchor digest；候选材料先写 `Prepared` 并持久化引用，之后才可被任何主模型可见路径消费。大对象仅通过 payload_refs 关联，不引入私有存储。

写入失败或状态未知时 fail closed：先解析既有事实，未证明已提交不得外发；不得把存储错误降级为内存里的 consumed。Bound 必须先创建尚未发送 prompt 的空 child 并持久化成功，才允许发送 prompt 与进入模型准入；Bound 写失败时清理空 child，创建空 child 不得预占模型容量。

同一来源重复提交相同 Requested 幂等；同来源改变 N、call 集合或 authority 是冲突。重复 Bound 相同 target/child/anchor 幂等；改其中任何一项不得偷偷开启第二个副本。RequestedRounds 只由 Requested 持有，Bound 与 Prepared 引用同一 DecisionId，不各自复制可修改的预算字段。

## [007] 消费证明、Promotion 与关闭路径

只有协调后的轮次证据明确证明 `turn.ProviderRun` 等于候选的 TargetProviderRun、且该运行产生了真实非空输出时，才可追加 Promoted；"tool.after 跑过""child 完成""owner session 还活着"均不能替代消费证据；请求尚未发起、纯传输错误、空失败或已终止的运行不得 Promotion。Promoted 必须引用与 Prepared 完全一致的 digest 与材料；写入状态未知时重新解析，未证明前保持 fail closed。

无材料结束、不可继续、取消、被替代、恢复放弃、或协议升级替换时写 `DelegationClosed`：明确记录关闭原因，同一授权不得在后续 transform 再次启动，并实现终态防重；成功路径（Prepared → Promoted → Traced）不额外写 Closed；材料废弃使用 Abandoned。

投影以明确联合类型表达 Requested、Bound、Prepared、Promoted、Traced、Closed/Abandoned，拒绝非法状态跳转，不用布尔值组合猜测状态。

## [008] Promoted replay、XTrace 与普通压缩闭包

目标请求中的候选不进入 XTrace 捕获范围。Promotion 完成后的下一次主变换必须在 XTrace 捕获前，把 Promoted frames 确定性重建到其因果位置（目标 assistant 输出之前）；随后的 XTrace 捕获将其纳入持久化时间线并记录 traced 游标范围。Promoted frames 在被后续压缩机制完整覆盖前保持可 raw replay；Prepared 在 Promotion 前严禁进入 XTrace、Companion 或持久化语义历史。

老字段历史不重写，历史中的旧字段与格式原样保留；注入的 Replica 记录不是新来源，不被误认作 owner 的新输出。

Delegate 不新增压缩阈值，不主动"压到可以委托为止"，不禁用 Host/工具既有截断。普通压缩或 prefix probe 不消耗预算，不把同一 logical continuation 上未 Bound 的请求判成失效，Requested 事实不因 messages 变短而消失。

主模型确实换成另一个 physical target 时，按既有 exact-target 恢复/废弃规则处理，不把原 Prepared 冒名渲染给新 target。回传注入后形成的实际完整请求仍进入常规上下文大小/失败/压缩路径；一般溢出恢复不得反复重送同一超大候选。

## [009] 镜像、ID 重定位与未来调查展望短记

同伴的 provider 消息基础是 owner 冻结点上的语义投影加本决策已完成的局部批次：完整 call/result、原始 arguments（含条件 `self_note`）与调用顺序全部保留；owner 的 wire-local call id 不得复制，必须确定性重定位为决策内局部标识并保证语义不变。

`self_note` 定义为面向未来调查的简明展望（一至三句，聚焦于准备核对的材料、关系及停点），只沿一条既有路径自然可见：原始工具调用记录 → 冻结 owner 对话 → ID 重定位 → 同伴可见对话。不得复制到 system prompt、bootstrap、额外 user 消息、子会话启动参数或新 hint 事件；不为短记保留特殊上下文窗口、补发消息或独立缓存。

同批多个正数调用的短记按原始调用顺序各自保留；只对整数取 max，不对短记取 max、不挑选最大预算对应的那一条、不合并成提示段、不丢失其他调用的短记。

当前候选在冻结之后产生，严禁反射回生成它的同伴会话。常驻会话的 transcript 不复用旧决策的语义上下文：每个新决策以自身 mirror 替换基线，旧决策内容不参与本决策。

短记是未执行展望，不是已证实的事实，也不扩大权限；常规压缩使其不再可见时，不通过专属通道重新注入。

## [010] 授权一次性、跨版本防重与恢复

一次授权只被一个执行消费一次：Bound 之后，同一 DecisionId 不因重试、恢复、重复回调或更换 target 获得第二份预算。跨版本按真实来源防重，同一真实来源即使按新版本计算 ID 也绝不产生第二份预算。

恢复按持久化事实决策：没有 Requested 且存在本版当前真实来源批次时，可重新记录同一请求，不得扫描任意旧历史找正数；旧版本已记录但未 Bound 的旧 pending 请求，按协议替换原因明确关闭，主模型继续，不重新执行；Requested 未 Bound 且 logical continuation 未失效时重新准入，被新用户输入或 authority 替代则 Closed；Bound 之后 Prepared 之前进程内原执行已不存在时，写 Closed、主模型继续，不重跑相同预算；Prepared 且 target 尚可合法消费时，加载原 payload 渲染相同材料，不重跑只读工具；Closed/Abandoned 不启动、不复活；追加状态未知时查询解析，未证实前不外发。

Bound 之后崩溃但尚未得到 Prepared，允许损失本次同伴调查机会；不得为挽回它引入自动重复消费。晚到的旧 child 回调必须先恢复/确认身份边界，不得落入普通 owner 分支；没有 Bound 的空 child 从未获准发 provider 请求，只作为空资源清理，不得补发 prompt。

是否启动、启动几轮的判断，不存在任何统计预测器、成本公式、收益门槛、学习样本、control holdout 或 rollout 分支参与。Predictor 模型池槽位保留为本机制的模型配置位。

## [011] 失败、取消、熔断与参数错误边界

严格区分普通参数错误与严重不变量错误：
- 参与调用的参数类型错误、范围非法、正数缺失短记、正数短记为空白、0 携带短记、或新旧字段混用，均属于新调用参数错误，仅拒绝当前调用/批次的准入，不执行委托，绝不触发全进程 fuse；
- 只有出现持久化歧义、投影冲突、权限突破、材料 digest 不一致等严重不变量失败时，才执行进程全局熔断；熔断在当前进程生命周期内保持生效，已完成的 Promoted 历史不受影响。

终止只来自显式因果事件：达到上限、真实 provider turn terminal、owner/operator 取消或删除、授权按明确原因关闭。不得以 elapsed time、deadline race、sleep 或超时先后决定是否收集或取消；不增加 deadline、按毫秒竞争的提前结束或新 failure budget。

普通 Replica provider/tool 失败只结束当前委托决策，主会话正常继续；已完成且通过结构/权限校验的完整前缀可以回传，残缺批次不补造；普通文件不存在不触发进程级熔断。

上限用完与主动结束都保持语义终态与物理尾部分离：先停止接纳新请求，再按真实 Host terminal 清理 child 与租约；语义结束后的晚到 callback 仍识别为 Replica，不走普通 Work，资源只清理一次。

owner 取消或删除时级联取消并释放 Replica 与 capacity fence，未消费候选不 Promotion。

## [012] 模型可见协议：事实性估计与条件短记

面向模型的协议完全剥离任何执行分工、信任建立、保留控制权或同伴指派的动机叙事，不要求模型理解宿主的 max 调度算法。

参与工具可见 schema 严格定义为：
1. `estimated_readonly_rounds`：必选原生非负整数（0..2147483647），表示当前整批工具完成后，预期后续连续只读查证的模型请求次数。0 表示当前批次完成后没有可合理展望的连续只读查证，或下一步已到达实质修改、命令执行、用户确认、关键权衡或结论边界。
2. `self_note`：条件性未来调查展望（string）。
   - 当 `estimated_readonly_rounds > 0` 时，必须提供非空白字符串（一至三句，说明准备核对的材料、消除的疑问与停点），验证时保留原始文本（含空白换行引号）；
   - 当 `estimated_readonly_rounds == 0` 时，该属性必须不存在（省略）；若出现任何形式的短记（包含空串、空白、null、undefined 或普通字符串）均属参数错误；
   - 未参与工具无此两字段，亦不接纳此类输入。

中英文工具说明幂等追加在原描述之后且保持稳定：不含剩余轮数、随机标识、价格或时间戳；只回传真实工具结果，同伴推理与总结不回传。

## [013] 逐工具断言的真实 Host 集成证明

本版不提供生产 DryRun / 影子执行模式；交付正确性只能由本版真实 Host 集成证据证明。只在 mock 对象上增加字段、只写 canary 日志而无断言，均不算证明。

真实 Host canary 必须按第 5 节的逐工具判定清单进行独立断言，不再要求对每个可见工具全量加字段：
- 证明已配置 Predictor 时，仅第 5.2 节参与工具带有必填估计与条件短记；
- 证明所有不参与工具（fork/join/chronicle/terminal/fission 等）原样无增量；
- 证明未参与工具的同名业务字段不被侵犯；
- 证明同一工具在真实 provider wire 上原参数执行，且原始 arguments（含协议字段）同源恢复进入下一次历史。

provider-wire 证据必须观察 owner 与 Replica 的实际 provider/model 与请求用途，证明后者实际使用用户配置的 Predictor 池。同 provider 容量为 1 时，父等子不得死锁、不得双占；取消时 capacity fence 与 child 正确释放。两个 owner 并发执行不串 schema、call id、授权、预算、结果或模型资源。

E2E 同时覆盖正常完成、提前结束、自然截断/压缩与恢复。旧三参数 routingProtocol 必须被明确拒绝。

## [014] Predictor 配置是唯一启用依据

启用状态只从实际模型配置的 Predictor 槽位派生：槽位不存在或候选为空即未配置；目标合法且非空即已配置；目标结构非法是配置错误，必须明确报告，不得静默变成"未配置"。工具装饰与委托准入共用同一份只读配置存在性查询，不维护第二份 enabled 真相。

不存在独立 enabled 开关、环境变量、二次 opt-in、人工 canary 指纹或生产消融选项能否决或替代该配置；外部特性注册不得把本特性变成 Predictor 配置之外的第二个启用条件。

容量暂满或 provider 暂时不可用是运行时调度状态，不是配置缺失，不改变已配置状态，不来回改 schema；具体请求按既有等待、失败与取消规则处理，不得偷换成 Predictor 缺失，也不得回退到 owner 的模型池。

未配置 Predictor 时不装饰任何字段、不追加说明、不产生新授权、不启动 Replica。移除配置只影响新委托：未 Bound 的请求在新配置加载后明确关闭；已 Bound 的物理执行遵守原有 target 与结束/取消规则；Prepared/Promoted 恢复独立于模型配置继续。

协议版本是代码中的稳定契约版本，不由环境变量改变。合法历史（既有 EventStore、XTrace 与会话）不因本机制被删除或篡改。

## [015] 历史分界与迁移规范

本规范明确区分两类历史场景：
1. 古老 K1/K2 预测材料的离线迁移：旧存储未迁移就拒绝以新运行时继续消费；已 Promoted/Traced 的旧只读材料通过离线一次性脚本导入为"已接纳历史材料"（DelegationHistoryImported），保留原因果位置、digest 与 trace 范围，旧档位仅作为历史证据落地，绝不伪造主模型从未发出的授权，亦不产生副本。
2. 本次 v1→v2 输入协议修订的历史兼容：已持久化的历史事件（如已有的 Requested/Bound/Prepared/Promoted）和调用记录按原样读取，保留旧字段名与旧数据，绝不改写磁盘历史；运行时新输入严格执行 v2 协议（只接纳 estimated_readonly_rounds），拒绝新输入中的旧字段。

迁移是离线操作，运行时永不静默双解析。迁移工具按登记表校验版本参数，保证升级的确定性与可审计性。


## [016] 协议常量、逐工具分类与严格成对输入合同

推测性调查只读轮次估计与短记协议由代码级纯逻辑共享合同（`InvestigationEstimateContract`）独立定义，不依赖 OpenCode/Plugin、模型配置、日志、文件系统、EventStore、租约或 runtime：

1. **协议常量与字段标识**：
   - 协议修订版本 `ProtocolRevision` 是代码中确定的稳定整数常量 `2`（严格大于 1），由代码确定，不得从环境变量、模型参数或运行时配置读取；
   - 轮次估计字段名为 `estimated_readonly_rounds`，条件短记字段名为 `self_note`。

2. **逐工具判定（classifyTool）**：
   工具策略采用严格的三态联合类型，禁止任何形式的前缀匹配（如 `js-` 前缀）或名称模糊匹配（如包含 read/search）：
   - `EstimateAfterCall`（参与工具，共 12 个）：`read`、`glob`、`grep`、`js-manager`、`js-engineer`、`js-devops`、`edit`、`write`、`mv`、`rm`、`fetch`、`run`。调用完成后允许主模型提供后续只读查证估计；
   - `NoEstimate`（不参与工具，显式白名单共 28 个）：`fork`、`resume`、`commission`、`join`、`horizon`、`review`、`suicide`、`fission`、`open-terminal`、`send-terminal`、`read-terminal`、`signal-terminal`、`skill`、`sphinx`、`assume`、`enough`、`abandon`、`defer`、`subscribe`、`publish`、`celebrate`、`regret`、`chronicle`、`js-bookkeeper`、`bash-honeypot`、`invalid`、`js-orchestrator`、`js-blogger`。本协议不向其装饰任何字段与说明；
   - `Unreviewed`（未判定工具）：所有不在上述 40 个固定名称表内的工具（包括带有已知前缀的衍生工具名如 `read-extra`、`globbing`、`grepper`、`edit_file`、`writer`、`run_command`、`fetch_data`、`js-devops-v2`、`fork_child`、`resume_parent`、`custom_tool` 等）一律判定为未判定，保持原有业务行为，不增加协议字段。

3. **入参容器与协议混合排斥**：
   - 参与工具的参数容器必须是普通非空、非数组的 JavaScript 对象（`isPlainObject`），传入 `null`、`undefined`、数字、字符串或数组等非普通对象一律拒绝；
   - 严格排斥旧协议字段：只要入参容器拥有自有属性 `delegate_readonly_rounds`（无论单独存在还是与新字段同时存在），均作为协议字段混用明确拒绝，不进行旧协议容错或降级。

4. **轮次估计的数值校验与范围**：
   `estimated_readonly_rounds` 必须是原生 JavaScript 数字类型（`typeof === 'number'`），且为有限整数（`Number.isFinite` 且 `Number.isInteger`）：
   - 合法范围严格限定在闭区间 `[0, 2147483647]` 的非负整数；
   - 拒绝字符串形式的数字（禁止隐式类型转换）、布尔值、数组、对象、null、缺失属性；
   - 拒绝负数、浮点数/小数、NaN、Infinity 以及大于 2147483647 的越界数值；
   - `-0`（负零）按原生浮点比较等价于 `0.0`，在语义上归一为整数 `0`；
   - 合法的 `EstimatedReadonlyRounds` 值可通过显式单向转换 `toExecutionBudget` 转换为内部只读执行预算 `ReadonlyRoundBudget`。

5. **成对短记关系与原始文本保真**：
   - 当 `estimated_readonly_rounds == 0` 时：入参对象中 `self_note` 属性必须完全不存在（`hasOwnProperty` 为 false）。若出现任何自有 `self_note` 属性——即使值为空字符串、纯空白字符、`null`、或显式赋值为 `undefined`（如 `{ self_note: undefined }`）——均判定为零估计携带短记错误并予拒绝；
   - 当 `estimated_readonly_rounds > 0` 时：入参对象必须包含 `self_note` 自有属性，且其值必须是字符串类型。该短记经 `trim` 去除两端空白后长度必须大于 0；若缺失短记、短记为空串、纯空白、或为非字符串类型（数字、布尔、对象、数组、null 等），均予以拒绝；
   - 验证通过的正数短记文本完整保留原始字符串（含首尾空格、制表符、换行与特殊字符），不进行 trim 截断或任何修改。

6. **错误类型辨识度**：
   解析错误至少能精准区分以下独立失败原因：
   - `MissingEstimate`：缺失 `estimated_readonly_rounds` 估计字段；
   - `WrongNumberType`：估计值不是 JavaScript 原生数字类型（如为字符串、布尔、对象、数组、null）；
   - `InvalidRange`：估计值不在合法范围（负数、小数、NaN、Infinity、超过 2147483647）；
   - `NotePresentWhenZero`：估计为 0 时出现任何自有 `self_note` 属性；
   - `MissingOrBlankNoteWhenPositive`：估计大于 0 时缺失 `self_note` 属性，或短记为空白字符串；
   - `NoteNotString`：短记存在但不是字符串类型；
   - `MixedProtocolFields`：入参携带旧协议字段 `delegate_readonly_rounds`；
   - `InvalidArgumentObject`：入参不是合法的普通对象。

7. **字段所有权与透传**：
   协议字段 `estimated_readonly_rounds` 与条件字段 `self_note` 的解析与校验责任仅归本协议及 12 个参与工具所有；未参与（NoEstimate）及未判定（Unreviewed）工具的同名业务参数原样透传，本协议不读取、不校验、不拦截、亦不破坏其原有的业务参数传递与持久化证据。

8. **三消费端单一判定来源与行为一致性**：
   逐工具判定函数 `classifyTool` 是全系统唯一的分类来源，供 schema 装饰端（`ReadonlyDelegationContract.decorateDefinition`）、调用边界端（`PluginHooks` 的 `toolBefore`/`toolAfter` 参数暂存与收窄拦截）和来源批次端（`StrengthDelegate.tryCapture` 批次估计聚合）三处共同使用，严禁在各端重复硬编码工具集合或 `Set<string>`。对任何工具名称（至少包括参与工具 `read`、显式不参与工具 `chronicle`、未在表内的未知工具 `js-foo-unknown` 与合成占位工具 `invalid`），三端在「是否装饰协议字段」、「是否在调用边界参与收窄与暂存」、「是否在来源批次中读取并聚合估计」上的行为判定必须完全一致。在协议未开启（未配置 Predictor）时，调用边界端与来源批次端均不拦截、不剥离、不校验任何工具的参数，参数原样透传；在协议开启（已配置 Predictor）时，调用边界端仅对参与工具执行必选校验与剥离隐藏，且对完全缺失估计字段的参与调用无条件按参数错误拒绝，绝不带缺陷放行或静默退化为无估计机会。

9. **已知工具差集清点与未判定候选显式登记**：
   系统通过机械方式清点仓库已知工具名权威来源（`StaticTools.knownToolNames`）与 `classifyTool` 已判定工具名单的差集。任何未判定工具必须显式可见并登记为待审阅候选；门禁严禁 fail-open（空差集无条件放行通过），亦不得在存在差集时未经登记无条件失败，防止新工具加入仓库时通过“未知默认不加”而静默逃避协议审阅。
