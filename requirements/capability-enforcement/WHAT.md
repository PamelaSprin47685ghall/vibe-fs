# capability-enforcement — WHAT

## [001] 每次请求的唯一能力集

每次 provider attempt 的 `ToolCapabilitySet` 由 CanonicalRole 与 RequestKind 唯一确定，随执行 profile 单点组装，不另设权限来源或旁路字段。

## [002] 可见工具与执行门禁同源

模型可见 Schema 与运行时 Gate 从同一权威能力集派生：Schema 表达固定权限上限，Gate 按当前事实收窄准入，不要求逐轮删除工具。超出权限上限的工具不可见，绕过 Schema 或当前准入的调用也须在执行前拒绝。

## [003] 能力投影不得扩大权能

请求投影可以等于角色固有权限；特定请求按其合同收窄，均不得包含 Office 未授权的能力。

## [004] 执行档位不改变权能

同一 CanonicalRole 的权限与执行档位无关。身份词汇与名字解析由 participant-identity 定义，不因模型档位增加职位。

## [005] StrengthReplica 只读

StrengthReplica 的能力仅为 Read、Glob、Grep。工具面默认拒绝，仅开放对应只读工具；修改、执行及其他工具均在运行时拒绝。

## [006] 内部工具与效用工具隔离

内部专用工具只对相应内部角色可见、可执行，普通角色不得借用。认知、交互及 Host 效用工具不属于领域业务权限，也不能扩大 Office 权能；Blogger 不得获得 skill、assume 等效用工具。

## [007] 工具来源不改变授权

Host 原生、MCP 和插件工具服从同一领域能力策略，不各设权限映射。所有角色均无外部网络浏览能力；Sphinx 由程序工作流调用。

## [009] 同名工具同一契约

同一工具名只对应一份参数 Schema 及生命周期、动作、返回契约，不因角色不同产生语义分叉。fork 与 resume 属于不同生命周期，必须保持独立。

## [010] 身份与配置失败时安全拒绝

角色或 profile 无法解析时，可见工具集为空，执行调用全部拒绝。Host 配置校验失败时，先写入覆盖 Host 宽松默认的 deny 策略，再触发进程级致命终止。

## [011] 路径元权限

每个 managed agent 的 Host 配置由唯一装配所有者显式写入 `external_directory = "allow"`。它是路径边界元权限，不计入角色业务权限，不授权普通工具。

## [012] 工具名称从角色能力派生

角色到工具名称的投影以 CanonicalRole 的能力为唯一来源；Manager 专用别名也在同一入口按角色解析，不引入历史别名兼容表或第二份手写工具子集。

## [013] 权威值的因果职责

Evidence 是已观察输入；Decision 是纯分类结果；Witness 是带精确对象与版本的证明；Capability 是对下一动作的不可伪造许可；Receipt 是已受理或已应用结果；PhysicalHandle 是当前进程资源。六者不可互换。

只描述工具词汇的值由正向 DSL/manifest 明确归类为 vocabulary，不靠类型名相似或名称白名单猜测权威。

## [014] 权威由所有者单点发行

敏感权威由拥有其不变量的 owner 单点发行，并在其公开端口与类型中显式定义。manifest 用 exact file、symbol、source/proof anchor 定位声明；缺失或错配必须拒绝，不以 name-only allowlist、baseline 或 suppression 放行。消费端核验范围与新鲜度见 015/016。

## [015] 精确范围与消费准入

Witness、Capability、Receipt 声明精确 subject、版本或序列，以及内容相关边界所需的 digest/hash。Witness 不能直接授权写入、发送或执行；消费端必须基于当前对象、版本及内容重新准入。退休与变更沿用各自规范的当前请求、快照和 Git tree 证明，不另造审查权威。

## [016] 历史证据不复活旧能力

旧 witness/receipt 可供读取，不授权当前效果。对象、版本、序列或内容变化时，旧 witness 必须拒绝；条件再次成立也由 owner 重新观察并准入。恢复不得跳过普通准入或携带隐藏的执行位置。

## [017] 明示使用次数

每个权威合同声明 one-shot、exact-N、set/closure 或 replayable evidence 的 multiplicity。一次性 Capability/permit 成功消费或 release 时原子关闭该次机会；重复读取 Evidence/Receipt 不等于重复执行效果。

## [018] QuiescencePermit

QuiescencePermit 绑定不透明 gate owner、SessionId 与 attempt serial，仅由 ObserveIdle 发行。消费与释放均返回成功或 typed failure：跨 gate 为 WrongOwner，重复消费为 AlreadyConsumed，新 attempt 淘汰旧 permit 为 Superseded，撤销为 Revoked，删除或缺少有效 idle 为 NoFreshIdle。

所有拒绝均零效果。JS 边界只投影稳定结果，permit 保持不透明。

## [019] 进程能力不可持久化

进程 capability、permit 和 PhysicalHandle 不进入 Fact/Event、journal、JSON 或其他恢复载荷。重启只能恢复 durable Evidence/Receipt，再由当前事实普通准入，不能从历史状态恢复旧能力或隐藏执行位置。Quiescence 必须重新观察当前 physical attempt 的 idle，不消费崩溃前 permit。

## [020] 配置不变量的致命边界

配置不变量破坏通过必需注入的 fatal 能力终止；普通可预期的准入拒绝不升级为 fatal。

## [023] DevOps 修复权不设逐次开关

DevOps 权能与修复范围由 office-capability-017 定义。不设置 allowRepair 等逐次批准参数，也不保留“只有 Manager 另行允许才能修改源码”的提示词限制。

## [024] Fork 与 Resume 的准入

fork 只允许 Manager 新建独立 Engineer，不准入 DevOps 或其他角色。resume 的既有参与者续做语义遵循 delegation-003/024。

固定 DevOps 只通过 resume 续做，同一道路至多一个活跃工作单元。忙碌时拒绝新 assignment，不把新任务伪装为 nudge，也不新建替代操作员绕过忙碌状态。

## [025] Manager 评审专用只读工具固定可见与当前事实收口

Manager 的 `js-manager` 在 Schema 中固定可见，仅提供 Read/Glob/Grep，过滤须到达真实执行 API。已接纳评审、退任冻结、有效证书或清理阻塞均使运行时拒绝。原生 `read`/`grep`/`glob` 及跨角色 `js-engineer`/`js-devops` 始终拒绝。工具名只通过 012 的唯一映射授权，不按后缀或启发式猜测；编程面四层一致性见 repository-programming-002。

## [026] Review 接纳前禁止向固定 DevOps 派工

在当前迭代的 Review 被系统接纳前，严禁 Manager 向绑定的固定 DevOps 派发任何任务；已有只读 Engineer 的合法 `resume` 与 `fork` 只读调查不受误伤。对未接纳评审前向 DevOps 派工的拦截与拒绝，必须发生在任何持久化副作用之前。
