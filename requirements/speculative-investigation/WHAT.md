# speculative-investigation — WHAT

## [001] 启用基线与协议呈现

未配置 Predictor 模型时，系统对本机制保持无功能基线：不装饰任何工具 schema、不追加协作说明、不产生委托授权、不创建 Replica，全部会话的 provider 可见字节、工具权限、retry 流程、评审终结逻辑与控制流与不存在本机制时完全一致。

已配置 Predictor 模型时默认提供委托协议：主模型可见的每个工具 schema 增加必选参数 `delegate_readonly_rounds` 与可选参数 `self_note`，并在原工具描述之后幂等追加稳定协作说明；原有的必选项、功能说明与安全约束保持原样。

预算填 0 表示本次不委托，与"未配置 Predictor"是两个可观察区分的不同状态；启用不依赖任何人工确认、指纹或额外设置。

## [002] 委托授权的形成：真实来源批次与同批 max

委托授权只能形成于同一个真实主模型 provider 响应内的完整工具批次：该响应按原始调用顺序冻结，且所有调用都有且只有一个 result；结果乱序到达时按原始调用顺序归一，孤儿结果、重复结果、重复 call id 或跨 user/provider 边界拼接的结果一律不构成有效批次。批次全部结果齐全前，一个 Replica 也不创建。

一批完整且 owner 随后仍能合法普通续行时，预算 N 取该批全部 `delegate_readonly_rounds` 的最大值：`[0,0,0]` 不产生授权；同批取值不一致是合法输入，不得要求一致或借错误恢复改写 max；0 不否决同批其他调用的正值；一个完整批次至多形成一次委托。

当前主模型已经生成的工具调用不计入预算，照常执行一次；它的选型与参数推理成本不发生第二次。

授权的形成还要求：来源是新鲜的真实 owner 输出；owner logical run 未被替代；该授权尚未 Bound 或 Closed；待消费的是合法普通 WorkMain 续行；来源不是 Replica、其他 InternalLeaf、interaction repair、显式恢复特殊分支或 prefix probe；EventStore、Host 边界与进程 fuse 健康。

准入不含任何经济或统计判断：不存在收益估计、成本模型、证据样本量、holdout 分组或预测得分作为条件，授权大小只由主模型填写的整数决定。同批出现非法预算值（缺失、null、字符串、负数、小数、布尔、越界）按新调用参数错误处理：不启动半批委托，不把非法值静默规范化为 0。非只读的来源批次不否决委托——当前操作照常做完，委托针对的是接下来；Replica 输出中的正预算不能产生二次委托。

## [003] 请求预算、提前结束与 N+1 外发门禁

预算是非负整数，单位为同伴获准的 provider 生成请求次数；一次请求可并行产生多个工具调用。一轮不是一次文件读取、一个 tool result、一次 transform callback 或一次重放；宿主按相同范围校验，绝不先截断、取整或钳制再执行。

计数只按运行时真实接纳的外发请求记账：纯文本结束占一轮；已外发后失败的一轮仍占一轮；同一请求的重复 transform 或 terminal 通知不重复计数。Host 截断、移除旧可见批次或发生普通压缩后，已用轮数不降低，也不得从变短的对话历史重建为更小值；owner 镜像中的旧调用不计入本次。

达到 N 后禁止第 N+1 次请求外发；第 N 轮已开始的工具结果必须收完；不追加额外总结请求。Replica 不新增自动 provider 重试，重复通知与真实重新外发必须分清，不得把重发当免费轮次。

预算是委托上限而不是必须做满的次数：同伴可随时提前结束，提前交还不是失败，主模型也不必在预算用尽后立即修改代码。

## [004] 同伴：同一 owner 身份的只读内部执行

同伴以 `InternalLeaf × Attached(owner, StrengthReplica)` 构造，继承 owner 的 participant、Role、Persona、provenance/version 与会话语言；每个 owner 至多一个活跃副本，不跨决策复用 transcript。变化的是执行用途、模型目标、可见工具集合与短期控制权，不是"扮演另一个人"。

模型目标经明确的只读委托用途，由唯一 MJS 调度权威从 Predictor 模型池选择；participant 与 Role 不改写，用途不从工具参数、用户文本、模型自述或角色名推导。Predictor 与 owner 配成相同模型是合法状态，不因模型名相同而关闭。

同伴只能调用本仓已有 `read/glob/grep`；其可见工具 schema 与底层执行门禁同源。尝试写入、调用非白名单工具、借 fork/MCP/通用 JS 工具绕过只读能力，均 fail closed 且不产生任何实际效果；shell 命令不因"看起来只读"而入列。同伴填写的正预算、推理文本与未执行计划不产生嵌套委托。

## [005] 真实完整交换与 digest：无专属字节上限

回传给主模型的只有同伴真实的只读工具交换：每个候选帧保留 request batch 边界、原始调用顺序、原始 arguments、真实执行结果与内容 digest，call/result 严格一对一配对。同伴的纯文本、reasoning、未执行计划与总结不进入主模型上下文；纯文本输出是提前结束信号，不是待主模型采信的研究报告。

不存在 Delegate 专属的字节、token 或批次大小上限，不存在按长度丢弃、保留小前缀、"过大退回零步"或等价替身规则；超过任何历史大小的完整交换仍可构建、持久化、映射与恢复。

工具原有截断、provider 上下文限制与主模型原有压缩照常工作；Frame 保存并回放实际返回给模型的结果（含原有截断标记），不得先对未截断全文计算 digest、回放时再另切一刀。digest、byteLength、UTF-8 字节计量与 payload_refs 保留，服务完整性、存储与观测：长度或摘要被篡改必须拒绝。

## [006] 授权与 Prepared 的持久化前置

一份授权至少绑定：DecisionId（由协议版本、owner logical run 与来源 provider run 确定性派生，不按工具完成顺序或未来目标请求派生）、OwnerSessionId、owner logical run identity、authority root 与来源 physical user message、发出该批的 SourceProviderRun、固定顺序的 SourceToolCallIds、RequestedRounds（同批 max）与 ContractRevision。授权不增加 SelfNote、Hint 或 TrustScore 字段。

持久化时机：来源整批完成并取得真实 owner 新输出证据后、外发副本前，写入 `DelegationRequested`；为合法普通续行冻结 target 与 mirror 后、副本首次外发前，写入 `DelegationBound`，固定 target、ReplicaSessionId 与 anchor digest；候选材料先写 `Prepared` 并持久化引用，之后才可被任何主模型可见路径消费。大对象仅通过 payload_refs 关联，不引入私有存储。

写入失败或状态未知时 fail closed：先解析既有事实，未证明已提交不得外发；不得把存储错误降级为内存里的 consumed。Bound 必须先创建尚未发送 prompt 的空 child 并持久化成功，才允许发送 prompt 与进入模型准入；Bound 写失败时清理空 child，创建空 child 不得预占模型容量。

同一来源重复提交相同 Requested 幂等；同来源改变 N、call 集合或 authority 是冲突。重复 Bound 相同 target/child/anchor 幂等；改其中任何一项不得偷偷开启第二个副本。RequestedRounds 只由 Requested 持有，Bound 与 Prepared 引用同一 DecisionId，不各自复制可修改的预算字段。

## [007] 消费证明、Promotion 与关闭路径

只有协调后的轮次证据明确证明 `turn.ProviderRun` 等于候选的 TargetProviderRun、且该运行产生了真实非空输出时，才可追加 Promoted；"tool.after 跑过""child 完成""owner session 还活着"均不能替代消费证据；请求尚未发起、纯传输错误、空失败或已终止的运行不得 Promotion。Promoted 必须引用与 Prepared 完全一致的 digest 与材料；写入状态未知时重新解析，未证明前保持 fail closed。

无材料结束、不可继续、取消、被替代或恢复放弃时写 `DelegationClosed`：同一授权不得在后续 transform 再次启动；成功路径（Prepared → Promoted → Traced）不额外写 Closed；材料废弃使用 Abandoned。

投影以明确联合类型表达 Requested、Bound、Prepared、Promoted、Traced、Closed/Abandoned，拒绝非法状态跳转，不用布尔值组合猜测状态。

## [008] Promoted replay、XTrace 与普通压缩闭包

目标请求中的候选不进入 XTrace 捕获范围。Promotion 完成后的下一次主变换必须在 XTrace 捕获前，把 Promoted frames 确定性重建到其因果位置（目标 assistant 输出之前）；随后的 XTrace 捕获将其纳入持久化时间线并记录 traced 游标范围。Promoted frames 在被后续压缩机制完整覆盖前保持可 raw replay；Prepared 在 Promotion 前严禁进入 XTrace、Companion 或持久化语义历史。

Delegate 不新增压缩阈值，不主动"压到可以委托为止"，不禁用 Host/工具既有截断。普通压缩或 prefix probe 不消耗预算，不把同一 logical continuation 上未 Bound 的请求判成失效，Requested 事实不因 messages 变短而消失。

主模型确实换成另一个 physical target 时，按既有 exact-target 恢复/废弃规则处理，不把原 Prepared 冒名渲染给新 target。回传注入后形成的实际完整请求仍进入常规上下文大小/失败/压缩路径；一般溢出恢复不得反复重送同一超大候选。

## [009] 镜像、ID 重定位与短记的自然可见性

同伴的 provider 消息基础是 owner 冻结点上的语义投影加本决策已完成的局部批次：完整 call/result、原始 arguments（含可选 `self_note`）与调用顺序全部保留；owner 的 wire-local call id 不得复制，必须确定性重定位为决策内局部标识并保证语义不变。

`self_note` 只沿一条既有路径自然可见：原始工具调用记录 → 冻结 owner 对话 → ID 重定位 → 同伴可见对话。不得复制到 system prompt、bootstrap、额外 user 消息、子会话启动参数或新 hint 事件；不为短记保留特殊上下文窗口、补发消息或独立缓存。

同批多个短记按原始调用顺序各自保留；只对整数取 max，不对短记取 max、不挑选最大预算对应的那一条、不合并成提示段、不丢失其他调用的短记。

当前候选在冻结之后产生，严禁反射回生成它的同伴会话；新决策不复用旧副本上下文。

短记是自省线索，不是已证实的事实，也不扩大权限；常规压缩使其不再可见时，不通过专属通道重新注入。

## [010] 授权一次性、恢复与 Predictor 模型槽位

一次授权只被一个执行消费一次：Bound 之后，同一 DecisionId 不因重试、恢复、重复回调或更换 target 获得第二份预算。

恢复按持久化事实决策：没有 Requested 且存在本版当前真实来源批次时，可重新记录同一请求，不得扫描任意旧历史找正数；Requested 未 Bound 且 logical continuation 未失效时重新准入，被新用户输入或 authority 替代则 Closed；Bound 之后 Prepared 之前进程内原执行已不存在时，写 Closed、主模型继续，不重跑相同预算；Prepared 且 target 尚可合法消费时，加载原 payload 渲染相同材料，不重跑只读工具；Closed/Abandoned 不启动、不复活；追加状态未知时查询解析，未证实前不外发。

Bound 之后崩溃但尚未得到 Prepared，允许损失本次同伴调查机会；不得为挽回它引入自动重复消费。晚到的旧 child 回调必须先恢复/确认身份边界，不得落入普通 owner 分支；没有 Bound 的空 child 从未获准发 provider 请求，只作为空资源清理，不得补发 prompt。

是否启动、启动几轮的判断，不存在任何统计预测器、成本公式、收益门槛、学习样本、control holdout 或 rollout 分支参与。Predictor 模型池槽位保留为本机制的模型配置位。

## [011] 失败、取消、熔断与物理尾部清理

终止只来自显式因果事件：达到预算、真实 provider turn terminal、owner/operator 取消或删除、授权按明确原因关闭。不得以 elapsed time、deadline race、sleep 或超时先后决定是否收集或取消；不增加 deadline、按毫秒竞争的提前结束或新 failure budget。

普通 Replica provider/tool 失败只结束当前委托决策，主会话正常继续；已完成且通过结构/权限校验的完整前缀可以回传，残缺批次不补造；普通文件不存在不触发进程级熔断。

预算用完与主动结束都保持语义终态与物理尾部分离：先停止接纳新请求，再按真实 Host terminal 清理 child 与租约；语义结束后的晚到 callback 仍识别为 Replica，不走普通 Work，资源只清理一次。

owner 取消或删除时级联取消并释放 Replica 与 capacity fence，未消费候选不 Promotion。

出现持久化歧义、投影冲突、权限突破、协议或材料不变量失败时进程全局熔断：新委托全部关闭，熔断在当前进程生命周期内保持生效；已完成的 Promoted 历史不受影响，继续提供正常恢复与重放。

## [012] 同伴叙事、信任与只回传工具事实

面向模型的协议叙事一律称"同伴 / companion"，不按价格或模型等级介绍；副本只增加简短的执行约束，不换成另一套人格或"研究员"身份。

中英文工具说明幂等追加在原描述之后且保持稳定：不含剩余轮数、随机标识、价格或时间戳。说明必须覆盖：每批取 max、0 不否决同批正值、一轮是一次模型请求、同伴看到当前对话且只能只读、可随时提前结束、需要亲自判断时填 0、不要为用满预算增加调查、预算到期也不要求立即修改代码。

协作说明以"逐渐了解、建立信任"为框架：根据对话中实际可见的调查表现调整信任，表现好就尽可能多信任、多让他工作，不擅长某些状况下次就少用或不用；拿不准时，可把"距离第一次 edit 还有几步"当作抛砖引玉的启发式基线，它不代表最优、不替代实际判断、也不要求最终一定 edit；填 0 保留判断权是正常选择。

不新增 trust score、评级字段、奖惩日志或自动增减预算算法；宿主只执行 max，整数由主模型自行填写。

`self_note` 可选：第一人称风格由描述引导，不做字符串前缀门禁；与预算独立（预算 0 可以有短记，预算为正也可以省略）；缺失与空串均合法，不追问、不重试、不自动合成；非字符串值明确拒绝；空串仍原样保留调用证据；不得改成"必填但允许 null"。

只回传真实工具调用与结果：同伴的纯文本、reasoning、总结不回传；主模型依据真实工具结果判断同伴表现；同伴执行期间 `delegate_readonly_rounds` 固定填写 0，不得再次委托。

## [013] 真实 Host 集成证明；无生产 DryRun

本版不提供生产 DryRun / 影子执行模式；交付正确性只能由本版真实 Host 集成证据证明。只在 mock 对象上增加字段、只写 canary 日志而无断言，均不算证明。

真实 Host canary 必须枚举最终 provider-visible tools（内建、插件、MCP/动态工具以及工具发现后的新定义），证明已配置 Predictor 时主模型的全部可见工具都带必填预算、可选短记与协作说明；同一装饰出现在 Replica 或其他内部请求的工具上时，它们将预算填写为 0 且没有发出授权的权能。

provider-wire 证据必须观察 owner 与 Replica 的实际 provider/model 与请求用途，证明后者实际使用用户配置的 Predictor 池。同 provider 容量为 1 时，父等子不得死锁、不得双占；取消时 capacity fence 与 child 正确释放。两个 owner 并发执行不串 schema、call id、授权、预算、结果或模型资源。

E2E 同时覆盖正常完成、提前结束、自然截断/压缩与恢复。旧三参数 routingProtocol 必须被明确拒绝，不得因 JS 忽略第四参数而假通过。

描述文字类测试只证明契约已送达，不得据此宣称模型必然形成理想信任或取得更好任务结果。

## [014] Predictor 配置是唯一启用依据

启用状态只从实际模型配置的 Predictor 槽位派生：槽位不存在或候选为空即未配置；目标合法且非空即已配置；目标结构非法是配置错误，必须明确报告，不得静默变成"未配置"。工具装饰与委托准入共用同一份只读配置存在性查询，不维护第二份 enabled 真相。

不存在独立 enabled 开关、环境变量、二次 opt-in、人工 canary 指纹或生产消融选项能否决或替代该配置；外部特性注册不得把本特性变成 Predictor 配置之外的第二个启用条件。

容量暂满或 provider 暂时不可用不改变已配置状态，不来回改 schema；具体请求按既有等待、失败与取消规则处理，不得偷换成 Predictor 缺失，也不得回退到 owner 的模型池。

未配置 Predictor 时不装饰两个字段、不追加协作说明、不产生新授权、不启动 Replica。移除配置只影响新委托：未 Bound 的请求在新配置加载后明确关闭；已 Bound 的物理执行遵守原有 target 与结束/取消规则；Prepared/Promoted 恢复独立于模型配置继续。

协议版本是代码中的稳定契约版本，不由环境变量改变。合法历史（既有 EventStore、XTrace 与会话）不因本机制被删除或篡改。

## [015] 旧历史离线迁移

新旧协议必须能被入口检查区分：旧存储未迁移就拒绝以新运行时继续消费，并给出明确说明；不得边读边忽略不认识的旧事件。判定依据是旧协议特征本身——旧委托事件类型名，以及类型名相同而仅旧载荷才有的特征（如预算保存为 `"K1"/"K2"` 字符串、tier 档位与 rollout 模式字段）。运行时始终只读新协议存储；不保留新旧双解析，不为迁移另建双读路径。

迁移是离线一次性操作：经 EventStore 所有者的读取、payload 与 append 边界，在备份或副本上完成；不原地改写 append-only 历史、Git raw object 或 refs；迁移不是新运行时启动时的隐式副作用；无授权不清库、不覆盖用户配置。

已 Promoted/Traced 的旧只读材料导入新版的“已接纳历史材料”表示：保留原 owner 因果位置、实际交换、digest 与 trace coverage；不得伪造一条主模型从未发出的 DelegationRequested 给它补授权；历史导入不参与新委托准入，也不产生副本。该表示是仅承载历史材料的导入事实（durable-events-026）；旧档位预算只作为证据随导入与迁移报告落地，不重新进入生产预算选择。

旧未消费的 Prepared 与进程内运行不带新版显式授权：升级前能正常收尾的先收尾，不能收尾的在迁移中明确记录放弃，不升级成可运行的新请求。

迁移必须保留或正确重建全部 envelope 父边、payload refs、decision 引用与 XTrace 因果锚点，并以 cold replay 对照证明业务 Current 一致；迁移工具、输入/输出版本与一次性操作步骤有自动化测试。

迁移工具的输入版本与契约修订取值必须与运行时契约一致：工具以登记表校验 `--input-version`（当前唯一合法值为 pre-delegation）与 `--contract-revision`（取 DelegationContractRevisions 的当前修订，当前为 1），登记值与运行时常量由同一次改动同步更新；未知取值一律拒绝并给出合法值。
