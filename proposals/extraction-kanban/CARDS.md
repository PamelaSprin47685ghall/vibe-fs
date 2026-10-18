# 全部职责卡

返回 [看板](BOARD.md) / [操作指南](README.md)。本文件只定义范围，不维护第二份状态或认领名单。

**路径是调查和候选修改面，不是整目录删除授权。** 开工时 owner 必须列出精确文件、公开符号、当前调用者及拟删除的代码；`.fs` 与相应 `.fsi`、工程声明、注册 Surface 和测试映射一起检查。下列未实现对象名和 npm 子路径是建议，不是已经存在的 API。

每卡都执行指南 A—H，分别交付独立库证据、真实接线证据、旧职责退出证据。下面的“顺序”是该卡内部可独立验收的步骤；需要多人分担时先把步骤登记为子卡，不能口头共享一张大卡。

**拆分 agent 开工必读：** [根 AGENTS.md 的模块拆分与接线交付规则](../../AGENTS.md#模块拆分与接线交付)和[上游拆分交付检查清单](UPSTREAM-DELIVERY-CHECKLIST.md)。每卡的完成条件须同时满足真实消费者、公开产物、关键反例、旧职责退出的证据要求；不得只凭下文单卡局部反例或库内测试标 DONE。跨仓交接须向接手 agent 提供清单的可访问位置、固定版本和本卡验收责任。

## 已交付与第一批接线

<a id="cut-00"></a>
### CUT-00　两仓准备、差异裁决与并行开工

- **交付**：确认两仓主线、工具版本、精确锁包；登记协调者、发布者、reviewer、工作树、首批编辑面。只做准备，不建通用任务运行时。
- **依据**：[根规则](../../AGENTS.md)、[合并基线](../lifecycle-cutover-closeout.md)、独立库根与 `src/AGENTS.md`。后者仍有“旧仓接线未授权/未完成”等历史句，先按实际 cutover 和当前任务授权校正，保留原证据时间。
- **顺序**：对照 COVERAGE 的差异 → 逐卡写契约/反例 → 登记首批 READY 与认领 → 分配两仓串行发布责任。
- **反例**：两个工作树指向同一 SQLite/common-dir；发布包来源不明；同一文件有两个 owner；新库依赖旧源码；以草稿删除仍生效的需求。
- **完成**：首批每卡有精确写集合和可执行验收，不以所有人提交了“收到”代替；协调者可合并认领。后续维护看板，不把本卡当永久阻塞所有工作的组织工程。

<a id="cut-01"></a>
### CUT-01　工作生命周期与原子日志接线（基线已完成）

- **现有对象**：Work、SqliteJournal、DurableWork；入口在 [DurableWorkHost.fs](../../src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs)。[work WHAT](../../requirements/work/WHAT.md)
- **不重做**：Plan/S4 开立、评审、交付、接力、重放；旧 Lifecycle 的 Facts/Fold/Kernel/Codec 等已退出。不为迁移再次复制 Work。
- **维护重点**：同一 records 快照投影；身份与模型目标分离；DeliveryRequested → SessionBound → prompt → Accepted；pending 和资源 blocker 不被恢复绕过。
- **反例**：旧回执结算后任、未知接受再次启动、并发投影混 revision、坏 work 阻塞好 work、退休前异步新增资源。
- **边界**：这张历史卡的完成不是全部 `work` / Host / release GAP 闭合。将来改它必须链接新的修复卡与当前证据；相邻库不得接管它的阶段或退休判断。

<a id="cut-02"></a>
### CUT-02　非 PTY 进程执行接线

- **对象**：现有 `wanxiangli/process` / ProcessSession。调查 [Process](../../src/Wanxiangshu/Process/) 的 NodeProcessHost、NodeProcessWait、ProcessRunner、ProcessRequest、ProcessOutput、LargeGate；参照 [process-execution](../../requirements/process-execution/WHAT.md)。
- **归属**：ProcessSession 管真实进程和管道；主仓的有界执行 C 管请求预算、硬期限、输出外存和资源归属，不把 Work/Journal 注入 ProcessSession。
- **顺序**：列出基础库与主仓硬期限/大输出合同差异 → 跑通一个非 PTY `run` 消费者 → 接回其取消、输出和真实退出 → 删除该路径旧的 spawn/wait 实现。
- **反例**：非法预算仍 spawn；大量输出堵管道；TERM 被忽略；取消返回就释放物理归属；晚到退出碰新请求；父退出但子仍占输出管道。
- **退出**：所选 pipe 路径不再调用旧 NodeProcessHost/Wait；Spool、LargeGate 和 PTY 中尚有消费者的部分留给 CUT-20/21，明确列出，不整目录删除。
- **不做**：不用“wait 超时不杀进程”的库合同替代主仓显式硬期限；不把留尾冒充完整日志。共享 Surface 先预约。

<a id="cut-03"></a>
### CUT-03　原始轨迹与固定区间接线

- **对象**：现有 `wanxiangli/trace` / Trace。调查 [Context/Trace](../../src/Wanxiangshu/Context/Trace/)、[semantic-trace](../../requirements/semantic-trace/WHAT.md)，与 CUT-06/15/16/28 共同审查范围协议。
- **先补差异**：主仓需要推理部件、typed retry 原文排除、Opening 的完整起始区间、并发捕获、稳定身份查询和 Fission 归并；基础 Trace 的单观察者总序不自动满足这些要求。两边区间形式不同，不由调用方随手加减一转换。
- **顺序**：记录 D-02 的逐项承接/留旧/子卡 → 完成一种真实 capture 与持久恢复 → C 提供精确取材，不共享可写历史 → 将相关下游切至同一原件 → 删除对应旧捕获/重复物化。
- **反例**：同身份异字节；重锚重置游标；先分配并发游标再冲突；旧消息新增部件变成新任；retry 原文进入 LWR；非 UTF-8/BOM/NUL 丢失；合法前缀截尾却被报完整。
- **退出**：同一会话同一语义轨迹只剩一个可写 owner；尚未迁的 lane 聚合可以由明确 C 提供输入，不得暗存第二份原文。
- **不做**：本卡不做摘要、不改变来源授权、不把历史字符串重编码后当原件。原件容量差异必须先处理。

<a id="cut-04"></a>
### CUT-04　唯一资源归属与关闭接线

- **对象**：现有 `wanxiangli/ownership` / Ownership。调查 [Execution/Session](../../src/Wanxiangshu/Execution/Session/) 的 Ownership、Attachment、Recovery 及 Work 收尾；依据 [session-ontology](../../requirements/session-ontology/WHAT.md)、[managed-session-lifecycle](../../requirements/managed-session-lifecycle/WHAT.md)。
- **归属**：登记簿回答 scope、lease 和 blocker；实际资源 owner 回答承接/终结；C 先提交预留、再执行并观察、最后结算。不要把 session 的所有行为都塞进 Ownership。
- **顺序**：挑一个资源族 → 持久预留接线 → Cancel/Drain 与终态分开 → Work 退休使用同一 blocker 源 → 删除这一资源族旧的独立活动计数/清空出口。
- **反例**：创建后才登记；Unknown 自动 NotStarted；取消减一释放别人；旧 PID/容器回执释放新实例；关闭子任期误收固定父 DevOps；没有资源 owner 就当空列表。
- **退出**：被迁资源族有唯一 owner，含在途旧实例处置。Attachment/ReuseScope 其余语义由 CUT-12 接手，不能因 ownership 表为空就删会话合同。

<a id="cut-05"></a>
### CUT-05　双语资源与包内定位

- **对象**：拟独立资源目录与加载/渲染对象，承接 `wanxiangli/src/resources` 草稿。调查 [Resources](../../src/Wanxiangshu/Resources/)、[Participant/Provider](../../src/Wanxiangshu/Participant/Provider/) 的 Language/Prose；依据 [provider-language](../../requirements/provider-language/WHAT.md)、[cognitive-environment](../../requirements/cognitive-environment/WHAT.md)。
- **归属**：资源对象管语义键、字节、语言、占位符；C 选择当前全局语言。角色内容的业务责任留给内容 owner，不把所有长提示复制成库内常量。
- **顺序**：先搬一个真实成对资源族 → 任意 cwd 的安装包消费 → 替换其旧加载器 → 逐族完成后统一删除重复定位路径。
- **反例**：缺一语言静默回退；占位符少填；协议名被翻译；包资源缺失却回读源码；全局语言被错误改成会话持久绑定。
- **退出**：被迁资源族只有一份权威字节与定位实现；工具描述、Role Law、Office Library 等语义层不因资源搬家混为一层。包 files 由 CUT-33 串行处理。

## 身份、权限、调度与执行

<a id="cut-06"></a>
### CUT-06　WorkRecord 取材与视图

- **对象**：独立工作记录物化职责；输入是 C 给出的经验证区间、Opening、Chronicle coverage、Recent work 和接收方向，不持有 Trace/Compression 服务。
- **入口**：[Mission/WorkRecord](../../src/Wanxiangshu/Mission/WorkRecord/)；[work-record](../../requirements/work-record/WHAT.md)、[participant-horizon](../../requirements/participant-horizon/WHAT.md)。
- **顺序**：先原字节 Opening 与 Recent work → 接收既有已沉淀材料完成三段 → 父子/接力方向视图 → 全部当前记录消费者接回。
- **反例**：session 全历史扩大 invocation；Opening 被当工具碎片删除；跨 resume 串记录；最后助手散文变固定字段表；tool 原件误进展示正文；同范围不同消费者各造一份事实。
- **退出**：Materialize/Model 的已承接判断只在新对象；宿主保留获取证据的 C。当前 Trace.statement 不是本卡已经完成的证明。

<a id="cut-07"></a>
### CUT-07　参与者身份与执行绑定

- **对象**：自治身份解析/绑定能力，不建立包含全仓所有 ID 的万能库。调查 [Participant/Persona](../../src/Wanxiangshu/Participant/Persona/)、[Foundation](../../src/Wanxiangshu/Foundation/)、AttemptExecutionProfile 与 SessionExecutionBinding；依据 [participant-identity](../../requirements/participant-identity/WHAT.md)。
- **归属**：逻辑参与者的角色/provenance 与物理模型、run、session 绑定分开；身份来自明确创建与已保存事实，不从日志描述反解析。
- **顺序**：冻结首个真实 profile 合同 → F# 与 JS 公共边界共同拒绝混用 → 换模型不换人的真实调用链 → 按消费者逐个退出旧身份拼装。
- **反例**：重试改变权限/Persona；两个不同实例因同字符串混用；以当前全局配置重算旧身份；Blogger 话语冒充主参与者；JS 调用绕过仅静态类型检查。
- **不做/退出**：不机械迁完全部标识符；只删除已归属的新旧重复解析。与 CUT-09 的 D-01 一起确定未知来源规则。

<a id="cut-08"></a>
### CUT-08　能力事实与双侧门禁

- **对象**：按明确 role、request kind、office 限制回答能力的对象；可见工具面与执行 gate 消费同一结果。
- **入口**：[Foundation/OfficeCapability.fs](../../src/Wanxiangshu/Foundation/OfficeCapability.fs)、Persona、ToolRegistry 与 JS capability/generator；[capability-enforcement](../../requirements/capability-enforcement/WHAT.md)、[office-capability](../../requirements/office-capability/WHAT.md)、[action-affordance](../../requirements/action-affordance/WHAT.md)。
- **顺序**：矩阵与拒绝行为 → 原生/JS 工具共同使用 → schema/描述/示例/运行门禁一致 → 删除每角色手写的同义权威表。
- **反例**：unknown role 放行；切模型扩权；工具隐藏但 callable；副本取得写能力；Plan review 前写入；内部专用动作出现在普通工具面。
- **边界**：工具实际执行仍属各能力 owner；不要在权限库里启动进程或读仓库。C 负责将冻结身份与当前请求条件组合。

<a id="cut-09"></a>
### CUT-09　来源、逻辑根与关闭权威

- **对象**：独立 authority 个体，持有逻辑根及关闭事实，接收显式 provenance/physical evidence；不把“当前有人跑”当用户新委托的完整证明。
- **入口**：[Interaction/Authority](../../src/Wanxiangshu/Interaction/Authority/)、ChatAdmission；[interaction-authority](../../requirements/interaction-authority/WHAT.md)、[crash-reconciliation](../../requirements/crash-reconciliation/WHAT.md)。
- **顺序**：先裁定草稿 D-01 → 根建立与 continuation → exact-run closure → 业务消费者与恢复接线；现有五类 closure 的缺口逐条承接，不批量删掉。
- **反例**：缺来源当 human；正文伪造根；子消息扩大上层权威；旧 run 的关闭命中新 run；重启恢复已关闭根；物理历史消息被当 fresh root。
- **退出**：根和 closure 不再同时由新对象及旧 Ledger/Fold 独立裁决；旧只读历史兼容有消费者时明确保留旧侧 codec，不能混用新事实。

<a id="cut-10"></a>
### CUT-10　模型路由与精确容量许可

- **对象**：可独立验证的路由准入/容量 owner。MJS 选择策略保留在显式策略边界；纯记录和真实配置加载分开，不把整个 Host 对象注入。
- **入口**：[OpenCode/Host/ModelCapacity](../../src/Wanxiangshu/OpenCode/Host/ModelCapacity/)、ModelRouting、Attempt profile；[execution-model-routing](../../requirements/execution-model-routing/WHAT.md)。
- **顺序**：冻结 running 的真实 token 语义 → 排队与 exact lease → callback/取消/替代的次序 → Host before-provider 接线。
- **反例**：按 live session 数当容量；按计数减一；同一请求占两次；取消释放别人；null 被当失败；旧 snapshot 在新 physical execution 生效；运行中追加材料偷换身份。
- **退出**：旧容量集合/Queue 的已迁策略不再独立维护；用户 MJS 配置位置、协议版本、语义不因移动默认改变。

<a id="cut-11"></a>
### CUT-11　派发认领与外部效果记账

- **对象**：独立发送意图/claim 与效果观察对象；Host 发送另有 owner，由 C 提交认领后发出、再核实物理承接。
- **入口**：[Interaction/Dispatch](../../src/Wanxiangshu/Interaction/Dispatch/)、PromptJournalAdapter；[dispatch-protocol](../../requirements/dispatch-protocol/WHAT.md)、[effect-accounting](../../requirements/effect-accounting/WHAT.md)。
- **顺序**：确定性 logical act 与请求身份 → Requested/承接/终态的数据协议 → 一条真实发送链 → 崩溃查询与重复回执 → 旧 claim 写入口退出。
- **反例**：同 payload 的两次独立行为被合并；transport receipt 被当消息身份；请求已发但提交未知后再次发送；只有 title 匹配就宣布唯一 session；重复回执重复派发。
- **退出**：同一发送行为的唯一认领由本对象负责，不能 Work.operation 和 dispatch claim 各自有权启动两次。C 明确两类不同事实的关联，而不是强行合为一张账。

<a id="cut-12"></a>
### CUT-12　会话关联与执行事实

- **对象**：会话关联、Reusable/OneShot 与一次 chat execution 的合法变化。与 CUT-04 的资源范围、CUT-07 的参与者身份分开；同目录可以多个独立对象，不设巨型 SessionManager。
- **入口**：[Execution/Session/Attachment](../../src/Wanxiangshu/Execution/Session/Attachment/)、[ChatExecution](../../src/Wanxiangshu/Execution/Session/ChatExecution/)、SessionRecoveryHost；[managed-session-lifecycle](../../requirements/managed-session-lifecycle/WHAT.md)、[managed-chat-execution](../../requirements/managed-chat-execution/WHAT.md)、[session-ontology](../../requirements/session-ontology/WHAT.md)。
- **顺序**：关联先于首 prompt → Accepted/ProviderStarted/Terminal → exact 结算与复用 → 删除和重启的真实 Host 链。宽卡分这四个连续子闭环，不同时改全部 session 代码。
- **反例**：未关联先提示；重复 ProviderStarted；旧终态污染新复用；删除先释放后提交；非最终拒绝变 NotStarted；父关系误当逻辑 owner。
- **退出**：每个迁移子闭环只有一个事实 owner；SharedState/SharedTerminalBus 不再藏第二份真相。不能仅凭 Ownership 已就绪删除 handle 或 session 分类。

<a id="cut-13"></a>
### CUT-13　委派、复用与结果认领

- **对象**：明确区分 fork、resume、同步委派、join 的自主工作单元；输入委托与真实观察，输出自己的状态与工作记录，不返回宿主调用脚本。
- **入口**：[Execution/Delegation](../../src/Wanxiangshu/Execution/Delegation/)；[delegation](../../requirements/delegation/WHAT.md)、[managed-session-lifecycle](../../requirements/managed-session-lifecycle/WHAT.md)。
- **顺序**：先普通 fork + join 的闭环 → 固定 DevOps resume → SyncDelegate → restart/旧回执与回收。每段都接回实际工具后再扩展下一段。
- **反例**：busy 时另建重复工作；handoff 传输回执被当完成；旧 handle 结算新任务；join 被用户打断后重复执行；丢失关联后以名字猜复用；结果泄漏机器 topology。
- **退出**：Controller/Handle/Join 等已承接规则与旧账本逐段删除；Orchestrator commission 的 Git 发布职责交 CUT-23，不塞入委派库。

<a id="cut-14"></a>
### CUT-14　失败预算、恢复决议与结算

- **对象**：给定精确执行阶段、已记录失败、预算和观察，回答下一步允许的行为；物理 retry、清理与 fatal 由外层各自执行并确认。
- **入口**：[Execution/Failure](../../src/Wanxiangshu/Execution/Failure/)、[Participant/Provider/Attempt](../../src/Wanxiangshu/Participant/Provider/Attempt/)、Interaction/Repair；[execution-failure-policy](../../requirements/execution-failure-policy/WHAT.md)、[provider-attempt-recovery](../../requirements/provider-attempt-recovery/WHAT.md)、[host-provider-failure-ownership](../../requirements/host-provider-failure-ownership/WHAT.md)。
- **顺序**：预算单次推进与决议互斥 → 精确清理/容量许可 → 真实失败后下一 attempt → Host 内建 retry 边界核实。
- **反例**：同失败记两次；预算随物理 session 而不是 logical run；错误文本/等待时间授权重试；Unknown 变失败；fatal 在清理前发生；库说一次但宿主又自动重发。
- **退出**：相同决议不再由 Failure/RetryPolicy/Host presentation 多处竞争；旧分类未证明等价时不只改枚举名。历史 Host 自动重试缺口必须保留，不许关闭 GAP 冒称修好宿主。

## 上下文、存储与物理加强

<a id="cut-15"></a>
### CUT-15　前缀世代与候选提交

- **对象**：独立 prefix 状态，输入当前事实、候选及完整请求组成，回答追加/新世代/拒绝；不调用 Trace 或 Host。
- **入口**：[Context/Prefix](../../src/Wanxiangshu/Context/Prefix/)；[prefix-stability](../../requirements/prefix-stability/WHAT.md)、[provider-projection](../../requirements/provider-projection/WHAT.md)。
- **顺序**：同世代精确字节前缀 → candidate/committed 分离 → 四类已提交冷边界 → 宿主请求组装实际接线。
- **反例**：工具集/模型/思考深度变了仍叫追加；未提交候选推动 epoch；新世代复用旧 coverage；宿主重排导致原文越界；两条请求顺序改变语义结果。
- **退出**：旧 Epoch/ProbeSelection/PhaseWindow 的已迁决议删除，Host 只提供真实事件。不得用 prefix 哈希反推 authority，实际缓存是否命中也不由本对象自证。

<a id="cut-16"></a>
### CUT-16　压缩覆盖与 checkpoint

- **对象**：已沉淀覆盖与候选压缩结果的独立职责；模型压缩、Trace 原件、prefix 提交分别是参与者，由 C 安排。
- **入口**：[Context/Companion](../../src/Wanxiangshu/Context/Companion/)、Blogger runtime、todo hook；[context-compression](../../requirements/context-compression/WHAT.md)、[obligation-ledger](../../requirements/obligation-ledger/WHAT.md)、[cognitive-workspace](../../requirements/cognitive-workspace/WHAT.md)。
- **顺序**：精确 coverage/checkpoint → 保留用户原文的材料切分 → 真实压缩候选接受/拒绝 → 退休/重启后从原件恢复。先承接一个会话，不跨父子偷偷归并。
- **反例**：todowrite 失败仍落 checkpoint；重写原生 todo 列表；压缩改写 human 字节；未提交产物作 coverage；旧 Blogger 完成覆盖新任；根据容量猜 overflow 并授权效果。
- **退出**：沉淀范围只有一个 owner，无第二份 desired/applied todo；旧 Cognition/canvas 已退役，不重新引入。Blogger 的行为诊断/指导另归 CUT-30，不把两个职责捆在一起。

<a id="cut-17"></a>
### CUT-17　请求投影与信息边界

- **对象**：声明式 ProjectionIntent、冲突裁决和渲染；输入不可变语义快照，输出 provider-visible 材料，不修改来源历史或底层状态。
- **入口**：[Participant/Provider/Projection](../../src/Wanxiangshu/Participant/Provider/Projection/)、OpenCode/Codec、Mission/Work/OpenCode/NarrativeTransform、TenureIsolation；[provider-projection](../../requirements/provider-projection/WHAT.md)、[participant-horizon](../../requirements/participant-horizon/WHAT.md)。
- **顺序**：planner/renderer 接口 → 角色与工具材料 → 任期过滤与 LWR 交接 → 完整 provider wire 观测。Host hook 顺序只由 C 持有。
- **反例**：注册顺序改变输出；旧任 tool call/result 被切半；机器 ID 进入 prose；全局 DTO 泄漏；Semantic 与 Wire 混用；投影回写成为 durable truth。
- **退出**：已迁功能不再在 PluginTransforms 与业务域各渲染一份；必须证明实际发出的字节，不只给中间数组拍快照。

<a id="cut-18"></a>
### CUT-18　规范事实编码与校验

- **对象**：当前多 writer 事实协议的独立 codec/validator，输入字节与显式 vocabulary，输出合法事实或明确拒绝；业务事件解释属于各业务 owner。
- **入口**：[Persistence/EventStore](../../src/Wanxiangshu/Persistence/EventStore/) 的 CanonicalEventCodec、EventVocabulary、Model、IntegrationKernel；[durable-events](../../requirements/durable-events/WHAT.md)。
- **顺序**：选一个既有事实族与精确字节语义 → canonical/identity/parent 校验 → 真实存储消费者接线 → 消除旧同义 codec。多协议确有不同契约时分别声明，不强造一个通用 Envelope。
- **反例**：同 ID 异字节；键序/编码漂移；缺父/环/哈希错误；合法并发被当损坏；未知事件被跳过；把 Work 规范 record 强塞进旧 NDJSON envelope。
- **退出**：被迁协议的编码和合法性判断只剩一个实现。CUT-01/03/04 的专用格式不因本卡自动改版；数据迁移或破坏性变更另需完整计划。

<a id="cut-19"></a>
### CUT-19　多 writer 存储与收敛

- **对象**：独立存储与集合合并职责，明确区别本地追加、全流验证、合法分叉和领域冲突。`SqliteJournal` 已完成的单流 CAS 不是本卡完整替代品。
- **入口**：[Persistence/EventStore](../../src/Wanxiangshu/Persistence/EventStore/)、[Persistence/Journal](../../src/Wanxiangshu/Persistence/Journal/)、Git/Hook；[durable-events](../../requirements/durable-events/WHAT.md)、[durable-convergence](../../requirements/durable-convergence/WHAT.md)。
- **顺序**：先一个真实消费者的 Commit/Unknown/fatal 传播 → 原 writer 原子追加 → retained union / KWayMerge → Git 同步与 retention → 每个事实族逐批退出旧存储接线。
- **反例**：两个真实 writer 的合法分叉丢一支；mtime 选赢家；坏尾裁剪；release 出错掩盖已提交事实；unknown 被当未写；GC 删除仍有读取义务的数据；不同 common-dir/root 混用。
- **退出**：数据格式、恢复、同步和关闭均有对应 owner，最后一个旧消费者退出后才能删旧 Store/Journal 相关实现。不要一次性改所有持久化格式，也不要为每个库强制继承旧仓的大 Journal。
- **协调**：原 J3、H0b 等存储工作先合并认领，现有报告只按原范围继承，详见 COVERAGE。

<a id="cut-20"></a>
### CUT-20　PTY 与完整输出外存

- **对象**：独立 PTY 会话与 spool 能力，二者可分步，各自最简完整。创建/写入/读取/信号的物理语义不能伪装成 pipe。
- **入口**：[Process](../../src/Wanxiangshu/Process/) 的 Pty/PtyApi/PtyBackend/PtySession/PtySupervisor、Spool/ProcessOutput；[process-execution](../../requirements/process-execution/WHAT.md)。
- **顺序**：先 spool 原始字节与容量切换 → 真实 PTY 四动词 → 断开/取消/终结 → 各消费者迁移；两条独立责任需要多人时拆子卡和文件预约。
- **反例**：大输出留尾却丢失必须外存的原件；截断破坏 UTF-8；完整返回超过总预算；PTY 被假子进程代替；EPIPE 后补写；父死仍有后代/终端句柄未回收。
- **退出**：每条物理路径只有一个适配器和输出 owner；ProcessSession.closed 不被扩义成整树终止。没有真实 macOS/Windows 证据，不增加支持声明。

<a id="cut-21"></a>
### CUT-21　显式时间与因果等待

- **对象**：deadline/elapsed 计算、虚拟时基和等待订阅；C 向时间/等待者取值，不把时钟作为跨库服务偷塞进领域对象。
- **入口**：[Process](../../src/Wanxiangshu/Process/) 的 Deadline/NodeTiming/VirtualTiming、[Execution/Session/Wait](../../src/Wanxiangshu/Execution/Session/Wait/)、Foundation/Temporal；[time-capability](../../requirements/time-capability/WHAT.md)、[causal-wait](../../requirements/causal-wait/WHAT.md)。
- **顺序**：纯时间值 → 虚拟等待/取消 → 一条真实定时器消费 → 其余等待者按域迁移。已存在可用小块就直接继承，不从零重写。
- **反例**：领域读取 ambient clock；负/非有限预算；超时写出业务终态；一个取消偷掉其他 waiter；解绑遗漏计时器/订阅；诊断活动重置权威 watchdog。
- **退出**：已迁调用者不再重复计算 deadline 或散落私有 timer；等待诊断不进入提示/持久事实。不是所有物理能力都必须依赖此库才能独立验证。

<a id="cut-22"></a>
### CUT-22　attention、concern 与邮箱归属

- **对象**：按 owner life 和 occurrence 定位的待办提示/公告队列；信息传播不授予执行权限，消费不建立第二份 todo 系统。
- **入口**：[Interaction/Attention](../../src/Wanxiangshu/Interaction/Attention/)、[Interaction/Concern](../../src/Wanxiangshu/Interaction/Concern/)、HorizonTool；[attention-regulation](../../requirements/attention-regulation/WHAT.md)、[concern-routing](../../requirements/concern-routing/WHAT.md)。
- **顺序**：occurrence 与 life → defer/publish 的明确消息 → 当前合法消费者 → 退休/重启边界；可由两个独立对象组成而非共享全局邮箱。
- **反例**：同名新 owner 收到旧 life 邮件；重复公告触发两次；订阅赋予授权；defer 阻塞当前收尾；全局 reset 误删其他会话。
- **退出**：旧 Projection/JournalPort/消息装配中的已迁队列不再各存副本；horizon 只呈现已许可的信息。

## 仓库操作与产品能力

<a id="cut-23"></a>
### CUT-23　Git 工作树、委任与集成

- **对象**：Git 物理能力与 Change 发布/委任协议分开，由 C 执行“创建/工作/清理/集成”的真实事务次序。
- **入口**：[Git](../../src/Wanxiangshu/Git/)、[Change](../../src/Wanxiangshu/Change/)；[change-integration](../../requirements/change-integration/WHAT.md)、[delegation](../../requirements/delegation/WHAT.md)。
- **顺序**：只读 Git 定位与工作树资源 → 一次 commission/manager 绑定 → Clean/Integration Gate 与冲突归属 → 崩溃恢复和工作树回收。
- **反例**：common-dir 当 worktree root；工作树名复用误删他人目录；未知 merge/commit 后再执行；原仓脏树被 reset；冲突归错 manager；hook 安装改写共享配置但未声明。
- **退出**：Git 命令与路径校验不再跨 Change/Process/Host 重复；业务发布状态不留两份。所有破坏性测试必须用独立临时 Git 仓，禁止操作开发主仓的真实状态。

<a id="cut-24"></a>
### CUT-24　文件读取、锚点与原子编辑

- **对象**：真实文件观察/编辑能力，输入有界路径与明确变更，输出原始字节或已发生修改结果；不含角色决策或 LLM 会话。
- **入口**：[Repository/Programming/Js](../../src/Wanxiangshu/Repository/Programming/Js/) 的 Anchor/AnchorFs/Utf8Fs/GlobFs/MutationFs/Transaction；[repository-programming](../../requirements/repository-programming/WHAT.md)。
- **顺序**：只读观察与精确锚点 → 显式 edits/transaction → 真实文件失败与并发保护 → 原工具接回。C 负责把权限与具体文件能力组合。
- **反例**：同名锚点误改；CRLF/BOM/NUL/无末尾 LF 变化；路径逃逸或符号链接换目标；部分提交后伪报全成功；旧文件被并发改写后覆盖；返回值别名改内部。
- **退出**：被迁路径的文件算法只在新库；JsSandbox、模板/角色与 Host 协议留 CUT-25。新库不能通过旧 ToolHost 来实现“独立”文件操作。

<a id="cut-25"></a>
### CUT-25　受限 JS 编程面与执行组合

- **对象**：受限程序执行、能力面生成和工具组合各自有边界；对公开能力组合，而不是 JS 任意访问 OS 的借口。
- **入口**：[Repository/Programming/Js](../../src/Wanxiangshu/Repository/Programming/Js/)、[Process/JsSandbox.fs](../../src/Wanxiangshu/Process/JsSandbox.fs)、OpenCode/Tools；[repository-programming](../../requirements/repository-programming/WHAT.md)、[js-semantic-surface](../../requirements/js-semantic-surface/WHAT.md)。
- **顺序**：冻结基类/生成能力协议 → 沙箱真实加载与拒绝逃逸 → file/run/Plan P 等能力接线 → 教程、描述、schema 与执行同源。
- **反例**：别名/原型/闭包绕权限；注入全局 require/fs/process；readonly 程序改文件；Plan 获得任意路径；测试只过 TypeScript 而实际 Fable 产物暴露私有字段。
- **退出**：同一能力不再有两套 generator/runtime gate；公开 Surface 坚持普通 JS 数据，不依赖 F# union 内部布局。不为了抽取暴露额外工具或降低原沙箱保证。

<a id="cut-26"></a>
### CUT-26　需求发现与材料溯源

- **对象**：工作区需求 catalog、APPLIES-TO 匹配、明确内容身份及就近引用；实际 Host 投递是外层组合。
- **入口**：[Requirement/Grounding](../../src/Wanxiangshu/Requirement/Grounding/)、OpenCode/Host/RequirementGrounding；[requirement-grounding](../../requirements/requirement-grounding/WHAT.md)、[requirement-system](../../requirements/requirement-system/WHAT.md)。
- **顺序**：仓库文件发现与覆盖规则 → 真实重叠/相对路径/符号链接案例 → 重放时保持原锚点字节 → 插件按当前请求投递。
- **反例**：包外文件冒充规范；来源路径漂移；同一版本重复注入；新内容沿用旧证明；用户引用被当任意系统指令；大小写/别名路径绕覆盖。
- **退出**：发现和匹配规则只有一份；说明文档的业务 owner 不随迁移搬成通用规则数据库。主仓 WHAT 仍是当前产品语义来源。

<a id="cut-27"></a>
### CUT-27　Casebook / 知识复用去向（先裁决）

- **现状与冲突**：[Repository/Knowledge](../../src/Wanxiangshu/Repository/Knowledge/) 与 [knowledge-reuse](../../requirements/knowledge-reuse/WHAT.md) 仍是产品范围；新库草稿“不再建”不是删除授权。
- **准备交付**：当前真实消费者、best-effort 与致命存储错误的差异、Case 双基线/来源区间和收益证据；裁定保留在独立产品模块、抽库、或经授权删除。裁决前默认维护原功能。
- **若抽取的顺序**：Case 索引/原件 → Bookkeeper capture/fetch → 精确 Store/incident 的并发复用与关闭 → Host 消费。纯缓存不接管原始轨迹与存储权威。
- **反例**：工作区路径别名串案；复用第二 Store 掩盖真实失败；Unknown 被吞成普通 miss；同一 cut/fatal 多 owner 处理；独立 invocation 互相污染；清理误删原件。
- **退出**：按已裁决范围删除旧 Casebook 对应 owner，或登记明确 RETAINED；原 K1/J3 等工作需先核对当前进度，不能再写平行计划。

<a id="cut-28"></a>
### CUT-28　Fission 多 lane 执行

- **对象**：一位 participant 的 lane 集合及 all-or-none 准入，不是一组可各自发明身份的新角色；实际执行/容量/轨迹分别由参与者负责。
- **入口**：[Execution/Fission](../../src/Wanxiangshu/Execution/Fission/)、FissionHostSurface 与 Trace 聚合；[intra-participant-parallelism](../../requirements/intra-participant-parallelism/WHAT.md)。
- **顺序**：canonical lane 请求 → 原子准入/整体拒绝 → 真实并行执行与收束 → keyed trace/结果合流。
- **反例**：部分 lane 接纳后隐藏失败；lane 次序漂移；替换 sibling 继承旧执行 lease；按到达顺序拼历史；打断重复执行；某 lane 未结束却宣布整体完成。
- **退出**：lane 所有权和最终集合只有一个 owner；与 CUT-29 的 replica/Strength 流程不能仅因“都并行”混为同一概念。

<a id="cut-29"></a>
### CUT-29　Strength / 投机调查去向（先裁决）

- **现状**：[Strength](../../src/Wanxiangshu/Strength/)、[Repository/Investigation](../../src/Wanxiangshu/Repository/Investigation/)；[speculative-investigation](../../requirements/speculative-investigation/WHAT.md)、[repository-investigation](../../requirements/repository-investigation/WHAT.md)。草稿未承诺重建，但主仓功能不能默默消失。
- **准备交付**：说明估计、预算、Replica、只读调查、WarmStart 各自真实消费者；逐个选抽取/保留/删除，不一揽子造“智能调查引擎”。
- **若抽取的顺序**：只读调查契约 → 精确 Prepared/估计与预算 → Replica 身份和批次 → 真实 Host 观察/恢复及材料晋升。
- **反例**：WarmStart 低信任文本冒充证据；估计等于授权；未提交 Prepared 就启动；内部 replica 越权；多个回执重复晋升；未知结果擅自复发调查。
- **退出**：所选算法和对应流程有唯一 owner，未搬部分明确保留；长期先验、历史迁移和工具配置不得凭“新架构更简单”删掉。

<a id="cut-30"></a>
### CUT-30　Rulebook、指导与退化观察去向（先裁决）

- **现状**：[Enforcer](../../src/Wanxiangshu/Enforcer/)、Execution/Session/LoopDetector、Host/LoopSensor、Blogger 诊断；[behavior-diagnosis](../../requirements/behavior-diagnosis/WHAT.md)、[guidance-delivery](../../requirements/guidance-delivery/WHAT.md)、[degeneration-guard](../../requirements/degeneration-guard/WHAT.md)。
- **准备交付**：把 Rulebook 事实、检测算法、Frontier/Coverage、指导投递和压缩历史分开盘点。仅共用 Blogger 不代表同一职责；不要把提示段名当模块边界。
- **若抽取的顺序**：独立流检测与有界状态 → 成对规则/诊断结果校验 → 指导覆盖/身份视图 → 当前 Host/Blogger 真正投递。
- **反例**：规则表与内容版本脱节；检测包络自动漂移掩盖回归；重复提示视作新事实；重锚丢 coverage；机器诊断升格 human authority；无限缓存增长。
- **退出**：各算法和投递账只剩一个 owner；包络手动更新要求、内容审查与真实测量继续保留。没有收益裁决不强造新库，也不删除产品保护。

<a id="cut-31"></a>
### CUT-31　Sphinx 与认识运行时去向（先裁决）

- **现状**：[Sphinx/V2](../../src/Wanxiangshu/Sphinx/V2/) 已有 Core、Runtime、Persistence、Plugins、Host 与 Wire；[sphinx-v2](../../requirements/sphinx-v2/WHAT.md)、[epistemic-reasoning](../../requirements/epistemic-reasoning/WHAT.md)。不因 draft 忽略它，也不因模块多就再整体重写。
- **准备交付**：核对最新原施工包的实际 owner/证据，给每个现存层选择保留/抽取/删除；目标、attempt、fence、资源与认识判断严格分离。
- **若抽取的顺序**：纯状态与 slot 证据 → bounded runtime admission/settlement → persistence/重开 → 一个实际 Host 答案闭环 → 再接其余插件。每段独立子卡，不平行重建三套 Runtime。
- **反例**：改用户 goal；伪固定评分表；预算/消耗混淆；未提交授权便 provider effect；取消后发布答案；插件判断抢 runtime continuation 权；recovery 重发未知效果。
- **退出**：已迁层的原 runtime/ledger 删除，仍有效的插件作为显式参与者。Work 质量证书退役不等于本域所有证据/证书概念都可删。

## 验证、宿主与最终交账

<a id="cut-32"></a>
### CUT-32　可独立验证的执行设施

- **职责**：测试运行、输入归属、子进程清理、日志与 verdict 分开；设施可以保持独立 CLI/脚本，不为了“所有东西都是 F# 库”把成熟工具再实现一遍。
- **入口**：[scripts](../../scripts/)、[verification-system](../../requirements/verification-system/WHAT.md)、[structured-workflow](../../requirements/structured-workflow/WHAT.md)、[Verification](../../src/Wanxiangshu/Verification/)。
- **顺序**：先沿用已存在 supervisor 和独立构建 → 为某张真实切刀提供更窄但完整的验证入口 → 绑定固定输入/工具/产物 → 验证进程与只读输入等物理能力；原 N00/N04 工作先对齐再接续。
- **反例**：诊断字节污染结果协议；未收完文件却报告全绿；TODO/skip/取消当 pass；子进程残留被忽略；修改后恢复哈希绕过固定输入；从旁边 checkout 借产物。
- **退出**：重复的验收实现可删，证明义务不能删；日志不授予流程权威。每个子包注明真实平台范围，不把 Linux 通过算成 Darwin 只读挂载通过。

<a id="cut-33"></a>
### CUT-33　两仓构建、打包与发布闭环

- **职责**：保持显式工程 → Fable → npm 公开 ESM 与类型 → 主仓 tarball 消费；版本、锁与打包由一个发布 owner 负责，不再设计增量编译框架。
- **入口**：两仓 package/lock/build 脚本，主仓 [scripts/verify-package.mjs](../../scripts/verify-package.mjs)、[distribution](../../requirements/distribution/WHAT.md)。
- **顺序**：记录已工作的 0.5.2 vendor/bundle 链 → 为新导出补实际包验收 → 从精确库提交产生不可变包 → 主仓干净安装、删除额外顶层副本后消费 → 发布批次收尾。
- **反例**：同版本不同字节；类型有导出但 JS 没有；main/exports/files 漏项；开发目录可用、安装包不可用；本机 node_modules 兜底；并行卡锁文件互相覆盖；文档入 Git 后指纹仍借旧 manifest。
- **退出**：不再存在源码链接/隐式 fallback/个人路径；主仓只依赖明确 tarball。阶段性交付不意味着已获 npm registry 发布许可，实际 release 证明另记。

<a id="cut-34"></a>
### CUT-34　OpenCode 适配边界

- **职责**：把当前真实 Host 的装载、会话、消息、prompt、signal、tool、删除等能力隔离为适配对象；适配只报告实际观察，领域判据不藏在 SDK 分支里。
- **入口**：[OpenCode/Host](../../src/Wanxiangshu/OpenCode/Host/)、[OpenCode/Codec](../../src/Wanxiangshu/OpenCode/Codec/)、[OpenCode/Signals](../../src/Wanxiangshu/OpenCode/Signals/)、[Host](../../src/Wanxiangshu/Host/)；[host-boundary](../../requirements/host-boundary/WHAT.md)。
- **顺序**：先真实 snapshot/发送契约 → session 创建/查找/绑定 → tool before/after 与信号收束 → 完整插件载入。每个能力与对应业务卡相邻验收；早期可以做只读 canary，不必等全部库完成。
- **反例**：列表满页当唯一；缺 agent/identity 被过滤成成功；transport ACK 当 accepted；after 未执行却假定恢复了参数；删除吞结算错误；SDK 不提供的能力靠名字或正文猜。
- **退出**：宿主 SDK 只在明确适配区域被消费；内核不导入本层。可以留在主仓，不要求另发布一个 npm；实际不支持的操作不伪造可用接口。

<a id="cut-35"></a>
### CUT-35　顶层组装与逐域恢复

- **职责**：主仓保留少量显式 C，选择对象、安排消息/提交/副作用顺序、关联结果与收尾；没有通用消息总线、第二套运行时或全局业务状态副本。
- **入口**：[Composition](../../src/Wanxiangshu/Composition/)、[OpenCode/Plugin](../../src/Wanxiangshu/OpenCode/Plugin/)、Manager/Planning/Change 应用流程；[crash-reconciliation](../../requirements/crash-reconciliation/WHAT.md)、[structured-workflow](../../requirements/structured-workflow/WHAT.md)。
- **顺序**：每次迁移一个完整调用链的 C → 原 aggregate projection 拆为各 owner 查询 → 启动/停止/恢复的相同协议 → 删除无消费者的旧共享桥。不要先重写整个 PluginTransforms。
- **反例**：C 解析所有事件重做领域 fold；第二份 global Context；一条坏历史阻断独立工作；异步前后身份漂移；未知提交走到新执行；关闭漏等 owned 工作。
- **退出**：每份共享状态说明真实主人；委托/模型/Trace/Work 等公开组合均可追踪。暂留产品组件也要是显式参与者，不能藏在“最后一个兼容层”。

<a id="cut-36"></a>
### CUT-36　第二宿主真实 canary（可选，不挡主线）

- **起点**：独立库有 `host-omp` 草稿，但当前规划未核实它对应的实际产品、版本、入口和能力。不在这里按名字编造 API，也不宣布它已获全面实现授权。
- **顺序**：先明确目标与实际环境 → Fable 产物被真实宿主加载 → 读工作区根、观察 provider 请求、创建并查询子会话 → 再决定适配子卡和复用范围。
- **反例**：文档有字段、实际快照没有；回调模拟被当物理 canary；没有子会话能力却造假接口；仅切换宿主就被迫重写 Work/Trace；继续调用旧 OpenCode SDK 当第二宿主实现。
- **完成**：只按真实 canary 覆盖的能力报结果；没有环境就保持 OPTIONAL。CUT-38 可以不等它，但不得把“可移植设计”说成“已经支持第二宿主”。

<a id="cut-37"></a>
### CUT-37　消融、诊断与实测成本

- **职责**：帮助检验切刀是否带来净改进；不是新业务权威。保留有实际消费者的诊断、消融 DAG 与准确性能观测，不用指标压迫边界。
- **入口**：[Ablation](../../src/Wanxiangshu/Ablation/)、Host Diagnostic/ReliabilityDiagnostics、[scripts/diagnostics](../../scripts/diagnostics/)；[feature-ablation](../../requirements/feature-ablation/WHAT.md)、[causal-wait](../../requirements/causal-wait/WHAT.md)。
- **顺序**：先记录原基线的真实步骤与成本 → 区分等待、CPU、外部 I/O 和未完成工作 → 一个可证伪优化/独立诊断能力 → 原正控/负控与可拆验证。
- **反例**：以测试数量、库数量或行数判断质量；只算成功样本；把 log 进度当 watchdog verdict；消融绕过必要正确性；变化跨机器却声称因果加速；常驻诊断泄漏资源。
- **退出**：无效计数和无用包装删除，现有生产消融节点不能随意变成新的开关矩阵。没有实测收益时允许不新增库，登记保留边界。

<a id="cut-38"></a>
### CUT-38　全仓残余职责与终局交账

- **职责**：逐行核对 COVERAGE 的每个需求包和源码区域，而不是再写一份“大重构计划”。只允许四种去向：已抽取并清旧、明确保留、经批准删除、尚未闭合。
- **入口**：[COVERAGE](COVERAGE.md)、两仓编译清单/exports、旧公共入口与数据格式、[GAP](../../requirements/GAP.md)、所有卡证据。
- **顺序**：调用者和依赖清单对账 → 每个旧文件的 owner/保留理由 → 旧格式及在途资源交账 → 安装包/完整链/失败与恢复验证 → 记录相对当前 master 的净改进与剩余发布证明。
- **反例**：没有 import 却透过 Context 调用旧系统；没有代码引用但历史数据仍依赖旧 decoder；GAP 因文件删除消失；打包夹带旧实现；期末为了“全完成”把保留组件批量改名为适配层。
- **完成**：没有“无人认领的残余职责”。明示保留项继续有人维护；未闭合项有具体 successor card，不能计入抽干。真正没有消费者且无数据义务的桥、旧 fsproj、别名与过期文档才删除。
- **验收口径**：全部上述卡完成处置，不等于整个软件再无缺陷；CUT-36 可选不算遗漏，其他暂留项必须公开。完整发布仍按原门禁报告，不修改它来证明蓝图成功。

## 给每张卡补出的最小契约

以上目录级职责卡还不是某次 PR 的施工范围。进入 READY 前，按 [模板](TEMPLATES.md#contract)补出这一刀实际输入、结果、错误、状态、调用者、数据过渡和验证命令。优先缩小可兑现的完整职责，不通过扩大 fallback、共享 Context 或“先接上以后补”把缺口藏起来。

跨卡发生冲突时，先比较它们是否在分别实现同一个判断。若是，保留一个 owner；若只是同一个文件的两种职责，先约定拆分和接线顺序。不靠两个团队互写对方测试来定义接口。
