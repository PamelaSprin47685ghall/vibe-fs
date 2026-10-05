# 04：认知材料、诊断、制度学习与 Sphinx

新排期以[总计划 N06/N07](../TODO施工总计划-2026-10-03.md)为准：Sphinx先真实创建/读取，再首个持久答案与结果、资源、取消恢复；制度学习先统一canonical Rulebook和原子BIRTH，再私有机制提炼及有限语义验收。二者可与S03并行准备，不等待所有基础设施债务清零。现有MCP解码、机械BIRTH及下述015交付不重复施工。

2026-10-05 当前状态：cognitive/015 注册投递切片已验收完成。原 physical、R1—R6、缺 physical/另一 session 与 raw Host 重放隔离已证；实际安装版两次 POST、exact chronicle completed、随后 SDK 历史及 journal 清洁已证。晚到实际 HTTP 错误旧版退出成功的正式红例已修；初始化清理受保护、Host/server 停止及 callbacks 排空后判决。最终完整 integration 21/0、无 skip/TODO，原 project 5030ms 超时及旧无 completed 屏障证据仍保留。以[本批附件](../archive/2026-10-04/目录所有权与生产接线-2026-10-04.md)为准，GAP-077 其他语义 PARTIAL，不重复旧 lease 调查。

返回[总计划](../TODO施工总计划-2026-10-03.md)。基线 `8cf51cc84`；本分册覆盖 11 个有 TODO 的包、80 个运行时 TODO。每行文件号对应 `requirements/<包>/tests/NNN.test.mjs`；完整标题和理由在[逐项清单](05-逐项清单.md)。本文描述待做工作，不是已执行证明。

共同开工条件：读现行 WHAT 与测试 README，按总计划区分 A—F 类；修改生产前立正式反例；用本包已有真实入口；共享 Rulebook、MarkerText、journal 或 Host run 契约先由一人定边界。表中“变异”是候选验证手段，只在隔离候选副本使用，不改共享 dist、不拿测试内自建状态机充当生产。

`b7768f478`实施增量见[记录](../archive/2026-10-03/Upstream增量-b7768f478-2026-10-03.md)。该历史阶段只有资源化/direct maybeInject，后续registered transform和两个真实请求的历史隔离已经完成，见本页当前状态。制度学习已有caller candidate机械检查、Born facts与纯revision重评；003虽已没有原TODO，私有机制提炼与输入能力隔离的规范余债仍须核对，不能将非空字段当抽象能力。

制度学习后续按N07执行：先明确唯一live Rulebook、完整双语revision与life冻结，再在生产提交口引入expected revision和Born/Disposition/必要Deferred事实同批提交；以并发、物理失败与冷重开证明零半状态。接着证明celebrate闭合后恰一次resurface，最后实现只接经验和canonical Rulebook的私有Enhancer与有限语义验收。现在Born→Committed二追加会半落地，私有liveRules也不等于Blogger/chronicle已经消费，均须真实接线。现行WHAT008已经规定必要事实同批，不再等待旧D12二选一审批。

## action-affordance

**入口与前提。** [WHAT](../../requirements/action-affordance/WHAT.md)、[证明范围](../../requirements/action-affordance/tests/README.md)、真实插件注册工具、provider 的 tool definition、各角色实际收到的调用描述。GAP-078；总计划 D14；W6，但材料清点可从 W0 开始。现有 014 的单 assumption、015 的 Host 原生 todowrite 已有独立证明，不恢复旧 canvas 或插件 todo 参数。

先建立“调用面→描述来源→schema→执行 owner→实际接收角色/语言→结果/后果”清单。列举所有活跃选择，不只挑几个示例；用运行时注册结果查缺项，人工判定语义。审阅条目须引用完整交付文本和具体反例，不能用是否含某个词作为结论。

| 文件 | 具体施工步骤 | 观察、可红反例与完成标准 |
|---|---|---|
| 001 | 对每个非平凡动作逐项回答 WHAT 的五问；捕获插件注册和 provider 最终描述；缺项修所属资源 | 遗漏副作用/责任/返回后果的描述要被审阅拒绝；合规改写不得因不同措辞被误判。全活跃动作有矩阵与交付证据 |
| 002 | 按高风险动作列中英核心约束对照；逐句核对准入、禁止事项、后果 | 只翻译正常例却漏掉拒绝条件的反例；语言存在性门禁与人工等义审阅分开记录 |
| 003 | 对真实只读调查入口给“查原因”和“顺手修复/生成新验证结果”两个任务 | 描述确实拒绝后者，执行权限同样拒绝；旧 inspect 已退休不能替代存活动作的证明 |
| 004 | 用小改动但行为未定、较大改动但机械规则已定的对照任务；核对 WorkRecord 呈现 | 不按 diff 大小判 mechanical，不把工作记录当验证结果；实际调用者看到这两层区别 |
| 005 | 对源代码修改和新执行证据分两条链捕获说明与结果 | 修改成功不能暗示验证已执行；反例是未执行时结果暗示通过；由真实返回与材料审阅判断 |
| 006 | 逐个列 read/query/execute 的物理副作用与 owner；先解决 query-shell 归属 D14 | 同名“查询”若启动有副作用执行，描述/权限须诚实；不得以只读名称猜实际副作用 |
| 007 | 列所有活跃工具名称和其动词含义，单列 horizon/js 的现行规则冲突 | 先给命名裁决和迁移清单，再改目录/注册/schema/资源/调用方；不是正则动词检查器 |
| 008 | 同名动作在原生工具、程序化路径、双语资源的 schema/准入/生命周期/终态完整对比 | 注入同名但字段或结果语义不同的真实 owner，正式检查或注册边界应拒绝；扫描到名字相同不等于语义同源 |
| 009 | 遍历实际 calling 选项，捕获 schema 与各角色描述；与 office/delegation 现行职责对应 | 选项文案不得暗示新增授权；保留旧 coordinator 拒绝回归，同时证明其它选项 |
| 010 | 对每个委派动作列 recipient 的任务、产物、调用者后续责任 | 只写角色名称不算；以“派出后谁欠什么、何时完成”场景审阅并观察实际后果 |
| 011 | 从所有真正做选择的 surface 捕获调用者材料，与 callee 的限制逐项对应 | 只改 resource 而漏掉某个实际决策面时测试/审阅可辨别；避免只看选中的文件 |
| 012 | 建立易混动作与禁止请求成对场景，使用实际交付文本判别 | 同词不同职责与近义合规改写均要有；不用一个只认自造 mutant 的正则 |
| 013 | 审阅纪律描述是否暗藏调度、审批或后台执行承诺；核对双语及真实 provider 文本 | “记录意图”被描述成“自动执行”必须拒绝；向模型泄露隐藏 Reviewer、双重确认或 barrier 的材料同样违反边界，即使没有扩权；语义审阅和实际执行轨迹分别证明 |

**闭合方式。** 机器证明目录完整、精确 schema/字节与实际权限；人审证明有限语义场景。不能宣称自动化覆盖了所有自然语言理解；若 WHAT 的普遍义务仍缺充分证据，保留对应 TODO 并清楚收窄。

## cognitive-environment

**入口。** [WHAT](../../requirements/cognitive-environment/WHAT.md)、[README](../../requirements/cognitive-environment/tests/README.md)、真实 system prompt 生成、Host request transform、provider-language 资源交付。GAP-076/077；W4 的交付完成后进入 W6。001—012 各有一个 TODO；015 的 GAP 虽不在 424 项中仍有义务，另列补充卡。

| 文件 | 具体施工步骤 | 独立观察与反例 |
|---|---|---|
| 001 | 列实际交付的 Common/Role Law、library、mission、lifecycle、tool material；逐项指定语义 owner | 完整 provider 请求与 owner 表一一对应；材料数量相同但权威来源错位必须失败 |
| 002 | 构造使命、技术建议、角色权能冲突的合法任务输入；抓实际交付通道 | 技术建议不能冒充职位授权；冲突按相关领域语义所有权裁决，不能按材料层级一概覆盖；语义审阅和能力拒绝分别证明 |
| 003 | 用真实 Main/child/internal 请求逐层捕获 system、tool schema、user/tool message | lifecycle 或 mission 被塞进替换后的 system 时可红；比较完整分区内容，不只判断词是否出现 |
| 004 | 比较工具集增加/减少前后的请求与准入 | 不能把 system prompt 当全工具手册，工具可见不等于可执行；新增工具名不增加 authority |
| 005 | 在真实 model binding 变化前后捕获 Role Law、自我模型和权限 | 模型变更应不改角色职责；改 model 后缺一段 Role Law 或角色字段漂移会被发现 |
| 006 | 以 library 建议不适用当前 mission 的例子审阅；捕获建议的分类与投递 | library 不得定义新公理/权力；反例为技术建议强制越权，不能用“不含 authority”正则 |
| 007 | 向某 office 提供另一 office 技术知识，再调用其无权动作 | 材料交付确实成功，但真实 before/execute 权限不扩大；拒绝零副作用联查 capability |
| 008 | 从实际 library selection 列 Class/Delivery/Audience 及 role/request 输入 | 同角色同请求只变 reasoning-depth 不该变成不同知识阶层；反例与选取结果完整比较 |
| 009 | 遍历实际选择路径与依赖，审阅“全知手册”、tier 变体、暗中评审链 | 以真实 selection/调用 trace 和人工审查记录证明范围；文件里没这些词不构成证明 |
| 010 | 首次、后续、重启、压缩后请求分别捕获 lifecycle 材料 | 该出现时出现、历史重放冻结、正常后续不重复教育；系统提示职责保持 |
| 011 | 运行中更新 mission/运行材料后捕获下次真实请求 | 变化进入消息通道，不伪造用户授权或替换 Role Law；检查 authority 根及历史字节 |
| 012 | Manager 接收可核对证据与相互矛盾评价，完成实际评审轨迹；检查真实 provider 输入是否泄露隐藏 Reviewer、双重确认或 barrier | 结论可追溯当前证据，缺证据不照搬隐藏过程；泄露本身也须拒绝，不能只查有没有扩权；与 relay-assessment 共用输入场景，独立记录有限语义判断 |
| 015，保持回归 | registered R1—R6、原physical身份、两次真实provider POST及completed后历史隔离已完成；晚到HTTP错误和清理也已正式证明，integration21/0 | 保留白名单、去重、缺physical不借session模型、canonical投影保持原physical、另一session隔离、缺committed lease拒绝与真实终态回归，不再列为新工单；其他认知语义/GAP余债按N07和001—012逐条推进 |

**衔接。** 若所有接缝只覆盖纯 prompt 组装，先扩展已有正式 Host fixture 的请求捕获，再做语义审阅；不要在测试里组装一份“正确请求”后自证。013/016 等 README 人审余项随 GAP-076 补查，不虚增 TODO 统计。

## attention-regulation

**入口。** [WHAT](../../requirements/attention-regulation/WHAT.md)、[AttentionTools](../../src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs)、[Attention Projection](../../src/Wanxiangshu/Interaction/Attention/Projection.fs)。GAP-118；001 是 W6，006 的架构审阅可早做。002/004 上轮曾有多文件时序问题，保留正式回归，不能把此次 planning 当作复验。

| 文件 | 步骤 | 验收 |
|---|---|---|
| 001 | 准备同一判断无新事实、无关新信息、足以改路径的新证据三组任务；记录 enough 前后的实际调用和理由；检查传给 participant 的材料 | 无新证据的重复调查与合理重开能被区分；工具成功不直接证明模型已停止。有限轨迹+语义审阅，不能用关键词声称普遍认知停止 |
| 006 | 从三个工具入口追踪所有依赖、durable facts、恢复路径和可执行能力；实际关闭重开 DeferredWork | 持久状态只含 WHAT 允许的追加投影/消费凭据；defer 不创建后台任务、优先级或授权；用真实 create/send/obligation 观察验证零调用 |

## causal-wait

**入口。** [WHAT](../../requirements/causal-wait/WHAT.md)、[CausalWait](../../src/Wanxiangshu/Execution/Session/Wait/CausalWait.fs)、[Registry](../../src/Wanxiangshu/Execution/Session/Wait/Registry.fs)、[Bridge](../../src/Wanxiangshu/Execution/Session/Wait/Bridge.fs)。GAP-094/095/096；总计划 D16；W2/W3。007 的分叉/循环算法已修，不重复计施工成果。

| 文件 | 步骤 | 验收与反例 |
|---|---|---|
| 002 | 清点跨 owner/Host/provider/物理能力的实际 wait；为每个定义谁产生满足事实与终止事实；在真实等待中逐个推进关联/无关事件 | 快照的 Last causal progress 可定位已发生事实；无关 diagnostic 不刷新。运行中的无等待 producer 与断裂依赖须靠真实活跃信息区分，不靠枚举标签 |
| 009 | 真实 Fable 源码闭包写正向消费者和非法获取 reader/直接构造 physical wait 的消费者；启动两个实际 plugin 实例观察首次绑定 | 编译负例在非法消费者处失败；后实例不能重定向目标；Application 只获窄 write 能力，Domain 无物理依赖。静态 import 扫描仅辅助 |
| 008，补充 | D16 决定后检查旧诊断文件与新进程注册表、plugin 重启流程 | 明确文件是否允许、是否被误恢复、新鲜度如何判断；当前“子进程 B 看到旧文件但注册表空”只证明不恢复，不证明不落盘 |

## behavior-diagnosis

**入口。** [WHAT](../../requirements/behavior-diagnosis/WHAT.md)、[Rulebook](../../src/Wanxiangshu/Enforcer/Rulebook.fs)、[Cycle Decode](../../src/Wanxiangshu/Enforcer/Cycle/Decode.fs)、[Cycle Commit](../../src/Wanxiangshu/Enforcer/Cycle/Commit.fs)、[Blogger Coordinator](../../src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs)。GAP-112/113；W1/W4/W5。BIRTH 相关卡与 institutional-learning 共用真实准入 owner，不能两边各建规则库。

| 文件 | 施工步骤 | 独立断言/故障点 |
|---|---|---|
| 001 | 在同一真实 store 接受 built-in 与 institutional 来源，读取 provider tip enum、Blogger prompt 和 Main index；加入跨来源同名 | live union 三面同一身份；碰撞拒绝且 journal/Current 不变，不能只测 catalog.validate |
| 002 | 隔离安装资源损坏后启动真正 plugin 子进程；再在 institutional preflight 与 append 间改变 RulebookRevision | 前者实际 fail-fast、无 fallback；后者 KnownNotCommitted 且整学习事务零提交；只 loader 抛异常不够 |
| 004 | 真 BIRTH 后取得实际 Blogger prompt；观察规则目录、持久日志和其它可能写入面 | 只允许规范事件变化，合成 prompt 不写为第三真源；读实际文件变化及 I/O owner trace，不能以字符串不存在自证 |
| 005 | 同一路准入依次缺 en/zh-CN 的 EnforcerText/MainText，各保留其它合法字段 | 每例都在 append 前拒绝；四叶完整对照可接受；真正 writer 未被调用且旧 Current 保留 |
| 009 | Host terminal 原始 assistant step 同时含两个 chronicle，其中一个不可解码；另设 0/1/2 合法调用对照 | 原始基数先于解码过滤；两个调用不提交 BlogObservationCommitted、不推进 coverage；删掉 raw cardinality gate 的变异须红 |
| 010 | 先解决 D11；按缺 messageId、非法 ToolCallId、缺 tip 分别驱动真实 terminal | 观察具体 typed incident、修复次数、结算/fatal 范围；不把所有错误统一 fatal 或统一 attempt-only |
| 012 | 非空旧 frame/coverage/tip/identity 前态，实际 append 注入拒绝、未知、重开 | frame、coverage、tip、identity 同生共死；磁盘重开只见完整旧/新事务，没有半笔；使用真实 canonical fact |
| 013 | stage 后分别改变 cursor/cutoff/epoch，在提交 barrier 释放竞争 | 每种 stale 在物理 append 前拒绝；磁盘零新事实、Current 不推进；允许真正新合法 cycle 接续 |
| 018 | 固定全局语言，暂停 live Blogger cycle，真实 BIRTH，新旧 life 各捕获四视图；语言切换另按总计划 D05 对齐后验证 | 固定语言下当前 life 的 prompt/enum/decode map/Main index 全部旧字节/身份；下一 fresh life 全部使用新 revision，没有半新半旧；不能借 RulebookRevision 冻结恢复会话语言绑定 |
| 019 | 先按 D11 联审 behavior019 与 context025 的 protocol exhaustion 终结范围；mandatory fatal capability 的负向编译可独立做，已明确的不变量 incident 才进入 settlement→fatal 证明 | 普通协议耗尽不得被测试暗定为进程 kill；对合同明确需要 fatal 的 incident，settlement 未完成不 kill、重放只 report/kill 一次；真实物理退出和端口顺序分别报告 |

**实现顺序。** 先 009/013 的正式反例和原子提交观察，再完成 live union/BIRTH admission，接 018；010/019 等 D11 和 failure-policy 终结合同。完整前后链联查 context-compression、work-record、guidance-delivery。

## guidance-delivery

**入口。** [WHAT](../../requirements/guidance-delivery/WHAT.md)、[Tip](../../src/Wanxiangshu/Enforcer/Guidance/Tip.fs)、[DeliveryProjection](../../src/Wanxiangshu/Enforcer/Guidance/DeliveryProjection.fs)、真实 Pair Hint/MarkerText 生产提交。GAP-115/116/117；D10；W4。

| 文件 | 施工步骤 | 可红反例与验收 |
|---|---|---|
| 001 | 先使当前 options.todo 真实反例红；同 TipName 不同 TipOccurrence，两次首投，再同 occurrence 重放 | Frontier 按 occurrence，Coverage 按 rule name；新 occurrence 有自己的首投事实，重放没有新事实；不直接把去重键全换成名字 |
| 002 | 实际 Full 准备后阻断 append，分别拒绝/WriteUnknown，再重开 | 不向 Host 报已成功 Full、不提前推进 Frontier；未知写入按真实重开事实结算，不能直接当失败后重做 |
| 005 | 已首投 occurrence→ContextReanchored→恢复 Full→再重放 | 只恢复 Coverage，Frontier 和首投事实不增加；旧冻结字节不被新规则覆盖 |
| 008 | Blogger history 含看似命令的低信任文本，经真正 Main Host projection 交付 | 检测正文/处置正文分受众；authority root、office、执行能力均不变；只测 resolver 字符串不足 |
| 009 | 新 guidance 在真实终端结果 NUL+BOM 后缀出现，读取 durable 事实与权限前后态 | 不伪造 user/tool call，不新建 root，不改变 permission；有其它 owner 的非空对照 |
| 012 | 各动态 owner 提供可辨认材料；读到一半暂停并更新 owner；提交或放弃，再 exact replay | 每 owner 只读一次，同 occurrence 原子冻结；concern 消费同提交；重放零动态读取/消费，不能从调用次数推出内容正确 |

**不要遗漏。** GAP-117 的 owner/Blogger 关联缺失和历史别名仍按现行条款审查，虽然不全都有 TODO；与 context reanchor、time-capability 的首次采样/历史 marker 共同回归。

## concern-routing

**入口。** [WHAT](../../requirements/concern-routing/WHAT.md)、[ConcernTools](../../src/Wanxiangshu/OpenCode/Tools/ConcernTools.fs)、[Projection](../../src/Wanxiangshu/Interaction/Concern/Projection.fs)、[AttentionConcernJournalAdapter](../../src/Wanxiangshu/Composition/Durable/AttentionConcernJournalAdapter.fs)。GAP-155/156/157；W1/W4。

| 文件 | 施工步骤 | 可红反例与验收 |
|---|---|---|
| 001 | 两个真实 Git workspace，同名 id 不同 owner；单 workspace 两个 concurrent subscribe 卡在 commit 前 | 跨 workspace 互不污染；同 workspace 只能一 owner，无 last-writer overwrite；重开保持赢家与冲突事实 |
| 003 | publish 解析 generation 后暂停，owner 退休/后继重订，再恢复 append | stale claim 拒绝，不转投新 owner；已真正接受的旧消息仍有诚实证据，不被事后无声丢掉 |
| 004 | 同 occurrence 准备 mailbox+公告+Pair Hint，分别放弃/写失败/进程中断，再重开 | 两类 coverage 和 MarkerText 同生共死；未提交材料仍 pending，成功重放 byte-identical |
| 005 | 捕获接收方完整 obligation、office、authority 投影及实际 peer message | 只收信息不产生债务或扩权；另以有限任务让接收方用领域证据核验错误声称，语义部分单记 |
| 006 | 将当前失败 TODO 变正式红例；从真实 participant 终结调用链接到 mailbox generation 退休 | 终结后 publish 拒绝；replacement/child 不继承；后继显式同义 subscribe 获新代、无旧消息/coverage；不以手调 retire 当证明 |
| 007 | 沿注册工具→journal→Pair Hint→恢复所有调用点列职责/能力；观察 publish 时无 Host 打断/新 obligation | 没有从 presence 推 authority、调度 ACK/优先级/自动执行的隐藏路径；人工依赖审计加实际反例，禁词扫描不够 |

先做 006 的 owner 接线，再做 003 retirement/append 竞争；004 与 guidance012 共用提交边界，但两个包各保留自己的业务断言。

## degeneration-guard

**入口。** [WHAT](../../requirements/degeneration-guard/WHAT.md)、[LoopDetector](../../src/Wanxiangshu/Execution/Session/LoopDetector.fs)、`OpenCode/Host/LoopSensor`、真实 Host turn observer、Fission 生命周期。GAP-145/147；GAP-146 已关闭；W2/W3。

| 文件 | 施工步骤 | 可红反例与验收 |
|---|---|---|
| 002 | 从实际 Host 投入 assistant 文本、user 文本、非文本/错误 owner delta；保存 journal 与 terminal 前后态 | 只有合格 assistant 流进入 sensor；delta 不能自行结算 terminal 或形成权威事实；codec 局部拒绝不是全链证明 |
| 004 | 实际 staged build 同时记录 selector、选中输入 bytes/digest、生成 envelope、runtime traversal | 单改 selector/输入/产物任一会失败；只在当前目录重新派生不能证明同一次构建 lineage |
| 006 | 真 run A→run B 切换，在 interrupt/continuation pending 时删除 session | detector reset 符合 run 边界；删除等待 owned tasks 收束，旧任务迟到不能消费/清除 B 的状态 |
| 008 | 子进程 A armed 后终止，B 从同业务 journal 重开；真实 Host reconciliation 提供 exact physical run | 进程内 armed 不恢复；不认错同 session 的另一 physical run；新 sensor 对象不是 OS 重启证明 |
| 009 | 经真实 dispatch 分别产生 accepted、unknown、definitely refused，取消时阻塞子工作 terminal | 各状态保留真实分类；unknown 不盲重派；cancel 等 owned work；已有受控端口证明保留，不扩称物理成立 |
| 010 | 建立 root、compaction、unmanaged internal、owned managed child 的真实拓扑 | 前三类豁免、后一类可触发；用实际身份/parent/admission，不能只注入 eligibility=true/false |
| 011 | 真实 anomalous run 触发 continuation，捕获 authority 和中英 anomaly material | exact DegenerationGuard authority 对应本次异常，不能借旧 run/另一异常资源；完整请求比对 |
| 012 | turn/Fission 中制造 guard 已拥有的恢复，观察 provider requests、nudge/AABB、repair budget facts | 其它路径让出恢复权，无第二恢复/重复预算推进；取消和迟到 terminal 同样验证 |

联查 host-provider-failure-ownership、provider-attempt-recovery 与 intra-participant-parallelism。数值 detector 的有限样例、算法复杂度审查和实际 Host 误杀范围分别记录。

## institutional-learning

**入口。** [WHAT](../../requirements/institutional-learning/WHAT.md)、[Enhancer](../../src/Wanxiangshu/Enforcer/InstitutionalLearning/Enhancer.fs)、[InstitutionalLearningTools](../../src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs)、[JournalAdapter](../../src/Wanxiangshu/Composition/Durable/InstitutionalLearningJournalAdapter.fs)。GAP-180/181/182；D12；W5。

当前已接入调用方提供 candidate 的机械 BIRTH 准入、Born 持久事实/投影及纯 revision 重评。接续先限定 Enhancer 输入并补实际机制提炼与语义准入，再将生产 revision CAS、Born/Disposition/Deferred 同批原子提交和重放冻结落实到真实 writer；最后接入统一 Blogger 规则索引。当前两次 Append 可能留下 Born 已写而收据未写的半状态，重试可改判 ABSORB/DISCARD。受控 provider 的机械协议证据不能替代真实有限样例的机制提炼审阅。

| 文件 | 施工步骤 | 可红反例与验收 |
|---|---|---|
| 002 | 同 occurrence 经实际 celebrate/regret 调一次 Enhancer；BIRTH stage 后改 revision，重评；再次改 revision | 正常一次；第一次冲突最多额外一次；第二冲突明确失败、零中间提交；重放零评估；保留 ABSORB/DISCARD 对照 |
| 003 | 私有 Enhancer 仅接经验与 canonical live Rulebook；检查注入能力和实际调用；用机制相同措辞不同/词同机制不同场景 | 不借网络或仓库调查；不是 substring 匹配；有限语义审阅说明为什么泛化成立，完整输入捕获证明隔离 |
| 004 | 真 BIRTH candidate 交 behavior-diagnosis，唯一 TipName 与四个语言正文齐全；碰撞/缺叶/非法身份 | 合法经过同一准入持久生效；所有拒绝 live union 不变；禁止第二 learned catalog |
| 005 | 场景分别缺 trigger、negative/distinction、泛化、非重复、注意力价值；准备有效正例 | 候选有明确可识别机制；人工审阅与结构校验分开；不创造新数值评分阈值 |
| 006 | 同等可复用的成功经验与代价经验分别触发真实候选 | 两者都能进入 BIRTH opportunity；不强迫每例 BIRTH，也不以两个 DISCARD 证明机会相同；正面经验不被一律改为惩罚 |
| 007 | 在 BIRTH admission 尚未完成时暂停，另有待弹出 DeferredWork；释放成功/拒绝两分支 | 闭合前无提前 resurface；闭合后 celebrate 一次、regret 不弹；不启动新任务或 obligation |
| 008-A | 实际 staging、writer、revision 三边界注入失败；非空旧 rulebook+deferred+receipt，关闭重开 | 没有局部 learning/rule/deferred 提交；exact replay 冻结结果、零重评/重复消费 |
| 008-B | 按现行 WHAT008 记录 LearningDispositionCommitted、必要的 InstitutionalRuleBorn/DeferredWorkResurfaced；同步编码/fold/消费者与合法历史恢复 | 同批原子提交，任一非法事实使整批拒绝；若提议单事实字段替代才另走 D12 的规范变更，不先等待是否保留旧实现的裁决 |

**跨包验收。** 本包剩余义务完成后仍联查 behavior001/002/005/018、attention004/005、guidance 的冻结交付。BIRTH 生效、活跃 life 不变、下个 life 看到新规则是一条可追溯的业务链。

## speculative-investigation

**入口。** [WHAT](../../requirements/speculative-investigation/WHAT.md)、Strength 的真实 DryRun/Replica coordinator 及正式 Host fixture。GAP-015/183/184；W3/W7。

上游`2c4ff4c36`同步增量：当前[005]/[009]/[012]/[020]要求真实assistant正文降为Main reasoning，原生reasoning不回传，返回Replica时恢复原text并合并自身reasoning一次；混合工具正文及终止正文也须保留，同样文本在不同批次真实发生两次不能全局去重。旧“只回传工具交换、纯文本一律NoMaterial”的断言不再有效。SharedCompletion仍须共享一个Prepared和一次bootstrap，不能因材料合同变化撤下并发资源断言；异模型切换的传输坐标仍要独立验证。

| 文件 | 步骤 | 反例与验收 |
|---|---|---|
| 001 | 固定同一个可重放 Work 输入，依次运行优化 absent、Off、fused、K0；抓 owner provider 请求、权限、retry、finality 与持久事实 | 四种关闭状态的可观察业务行为一致，没有 Prepared/Promoted/额外 authority；真实 child 端口/调用计数为零。只比较开关枚举或纯分类器不算 |
| 002/013，GAP 补查 | 真实 owner dispatch DryRun，暂停 child，再让 owner 继续；owner/target terminal 与取消分别收口 | nonblocking 由 owner 后续事实证明；exact owner/target 终结，owner 历史不被 shadow 污染。既有局部 coordinator 正反例保留 |

与 feature-ablation 的关闭证明可复用请求捕获，但不要用同一测试脚本生成自己的正确预期。

## sphinx-v2

2026-10-05 N06-B前置Core答案来源守门已验收：必需resultObservationId、当前成功attempt、accepted结果work/fence/schema、原子prepare拒绝、真实semantic cut及合法冷重开。gen131包含完整Sphinx套件，212/212、1547/0；017/T406仍保留，因为实际profile renderer和Runtime/公开入口未接通。[记录](../archive/2026-10-05/实际读取版本与答案来源-2026-10-05.md)。下一主线仍是N06-A唯一command owner的真实创建/读取，然后完成B；不能把Core守门当用户已获得答案。

2026-10-05 N06-A有限验收完成：gen142正式73/73排空、437pass/0fail、24skip/29TODO、5.85s wall；前后freshness一致，group65775 accepted=true、17.144ms，exit1仅pending。真实SDK/JS创建、内容绑定原receipt、native查询/accepted trace与新OS进程重开已证。唯一Integrator.Current成对保存accepted state/envelope；查询不得再走History读取。启动拒绝、disposed、非法identity/list与合法特殊资源名的reserve绑定均有正式回归。[验收记录](../archive/2026-10-05/Sphinx持久创建与读取-2026-10-05.md)明确未证边界。下一主线N06-B，完整036两个TODO、T406/T411/GAP-219保留。f0ead CI实质超时813/818、2 active/3 queued，另见[完整receipt](../archive/2026-10-05/baselines/f0ead-ci/receipt.txt)，选集不代替全仓。

**入口与判断。** [WHAT](../../requirements/sphinx-v2/WHAT.md)、[SUPERSEDES](../../requirements/sphinx-v2/SUPERSEDES.md)、[README](../../requirements/sphinx-v2/tests/README.md)、[MCP Server](../../src/Wanxiangshu/Sphinx/V2/Hosts/Mcp/Server.fs)、[OpenCode Adapter](../../src/Wanxiangshu/Sphinx/V2/Hosts/OpenCode/Adapter.fs)、[Runtime Driver](../../src/Wanxiangshu/Sphinx/V2/Runtime/Driver.fs)、[Wire Surface](../../src/Wanxiangshu/Sphinx/V2/Wire/Surface.fs)。GAP-219/222；D01/D02；W5。

创建计划时“MCP handler忽略参数”的诊断，已被上游`e1e7dd3f1`替代；N06-A又完成真实start/status/export，JS模板成功与traceUnavailable已删除。work_next/work_submit/goal_amend仍unsupported。OpenCode adapter的owner仍从InquiryId错误构造，ReadStatus仍Unknown、ReadResult/Reconcile仍因所持port缺读取能力拒绝；实际Host另有snapshot API，尚未组合到adapter。GAP-219仍PARTIAL。从下面B的真实物理绑定与唯一driver继续，不重复造decoder/store或把Capabilities()声明当派发证明。

### N06-B 接手卡：先让运行事实可恢复，再接真实执行

本卡取代下方完整义务索引中的开工顺序。A和B0有限验收已完成，不重做创建/读取及canonical派发接纳。[B0记录](../archive/2026-10-05/Sphinx派发事实与资源预留-2026-10-05.md)保留派发红绿与旧宽选集014墙钟失败；[观察与估值记录](../archive/2026-10-05/Sphinx执行观察与估值守门-2026-10-05.md)另记gen153四业务红、真实sort变异与最终gen155全静态及正式225/225、1125/0。B1-A/B2-0有限验收完成；B1—B6整体未完成，不用空delta或测试Surface冒充生产执行。

B1-A的普通terminal有限观察完成后，必须先做[B1-A2恢复因果接手卡](07-Sphinx恢复因果.md)，再接Sphinx实际Host。现same-root+continuation-kind没有assignment归属；retry、repair、guard三类producer都需在effect前绑定具体call，并从真正PhysicalAccepted回传successor。只修一类、按时间排除旧id或仅匹配初始physical都不够。该缺口已完成源码调查，尚无真正successor fixture的正式业务红灯，不记作修复。

| 子包 | 状态 | 本包退出条件 |
| --- | --- | --- |
| B0 canonical派发事实 | 有限验收完成 | 同一Current完整Request+Receipt option，当前身份/round/依赖/本work预留及冲突守门；重放、原子拒绝、冷重开、完整state与semantic hash已证。两项预留错误已修。真实Host来源、usage与receipt丢失对账未完成 |
| B1 实际Host绑定 | B1-A有限验收完成；A2待施工 | typed Admission/Completion保原生carrier/key、持久physical/root、普通formal terminal与原checkpoint；四真实业务红后修fallback身份/单次结算和observed准备误发。最终gen155全静态及225/225、1125/0通过。先补A2的retry/repair/guard归属，再接真owner/family/public prompt与Sphinx持久绑定；034不关闭 |
| B2 executable profile | B2-0有限验收完成；完整profile待施工 | Unestimated带rank仍不可用，空集合不称数值比较，ordinal/provisional由真实Decision保种类；gen151正式红、最终gen155宽绿与全静态通过，原rank0保持。完整schema/plugin/ABI lock、prompt、授权/资源与Goal/material另接，不关闭002/029整体TODO |
| B3 公开claim/submit与派发 | 待施工，依赖B0—B2 | 同一Commands持久lease、先intent后effect，append失败零派发；局部ticket/scope/attempt/fence准入 |
| B4 实际结果与两事务解释 | 待施工，B完成前必做 | 唯一实际结果保存，locked Observe产生真实delta，applied/failed推进pending，失败不重调模型 |
| B5 accepted renderer答案 | 待施工，依赖B3/B4 | 实际renderer结果先accepted，再AnswerCommitted；公开正文和来源可取、停止标签诚实 |
| B6 公开闭环验收 | 待施工，依赖B0—B5 | 017/036实际入口闭环、新OS冷重开、034实际Host证据，局部/受控/实际Host分别结算 |

**B0，已验收的有限边界。** 原no-op路径已退出：DispatchRequested保完整Work/publicEnvelope/privateTicket，Receipt保完整intent/work/attempt/fence/physicalRef/native envelope，内部只有Dispatches一个map。fresh派发要求当前Ready/同fence Leased、完整Spec、真实成功依赖、已存在Some round及本work持久预留；None独立work允许。同intent exact replay先于fresh守门，同identity改内容拒绝；receipt同时核对Spec及Running物理引用。native字段仍叫physicalBindings，空值字节保持原seal；实际gen143旧创建/未派发work原样恢复，缺round旧派发和错误aggregate预留分别durable cut，原历史不删不重封。004/007/010/019/020覆盖正式Persistence/Current、原子批次、冷重开及hash。真实Host来源、副作用前append失败与receipt丢失对账、usage/容量完整义务保留，T401/T402/034不关闭。

**B1，实际Host调用的第一条合法路径。** 先读sphinx WHAT010/023/034、session-ontology WHAT006、delegation WHAT007及真实Sessions.SendPrompt。Adapter目前把InquiryId转成SessionId，且没有terminal订阅就SendPrompt；真实Host会以listener-before-send拒绝。由真实Host入口注入typed owner SessionId，独立MCP的执行上下文由composition明确供应，缺能力仍具名拒绝；不能用createdBy/configHash替代物理身份。遵循真实family root压平和标准Engineer的authority/委托owner。先订阅再send，订阅由同一effect owner持有至真实terminal/Dispose；仅public envelope进入prompt，private ticket留Host侧。receipt、physical message、child session与provider run分型保存，acceptance-unknown不重发。先让owner SessionId与InquiryId故意不同击红，再观察真实parent、prompt、订阅顺序和实际Host返回；能力字符串不算派发证据。Provider adapter现用HostForkRunLifecycleSurface.create(receipt string)制造无关联pending cell，必须清理，消费真实执行owner，不能走这条捷径。

**B2，先选一个真实可执行profile。** 现Registry/ExecutablePlugin只有声明，Plan/Render主要是验证器；不把存在类型当已接线。供应真实canonical schema文档/hash、executable/ABI lock、版本化prompt、model/provider授权、实际资源与executionMode。材料须由真实读取证据供应，纯插件输入须有原Goal，不把缺目标塞成空Graph。首条有限链做实际规划→成稿，实际计划集合含有真实成本的answer.now；Unestimated保持缺估值，不给默认收益。question/probe全面覆盖留E，有限profile范围写入验收记录。

**B3，公开命令与唯一effect调度。** Commands接共享claim/submit，MCP/JS/原生入口只解码和调用。先定WHAT012的ticketHash/scope真实来源、公开字段和持久匹配；当前submit DTO只含attempt/fence等，不足以称完整局部准入。claim持久lease；BudgetReserved+DispatchRequested同批接受后才能派发，真实append拒绝的Host观察器必须为零。Driver当前Hash="empty"、plugin="unbound"和空interpretation提议全部退出此路径；缺真实schema、上下文或授权时具体拒绝。先定公共类型，再调调用方和正式反例，不在各adapter另造driver。

**B4，ReadResult前置从原N06-C移入B。** 实际Host已有ISessionSnapshotPort.GetMessages，组合到最窄读取能力，核对actual child、physical user message、唯一provider run、work token、attempt与实际schema；idle只表示通知。第一事务保原响应、ResultAccepted和InterpretationPending；第二事务locked Observe形成真实Graph/Work delta与applied/failed。当前InterpretationApplied/Failed为no-op，会让pending永不完成，必须以Current和冷重开红例落实。usage缺失保reservation，不填零；plugin失败后只重做纯解释，不另买模型响应。完整receipt丢失/unknown对账仍留C，先前受控port不升级成实际Host恢复。

**B5，真实renderer到公开持久答案。** renderer work来自实际规划并保留成稿资源，输出通过真实schema和Render.validate。先成为accepted observation，再提交exact resultObservationId的AnswerCommitted，复用已完成Core来源守门。答案正文从这份结果取得，不把ref、排名数组或固定文本当答案。先证明未提交/拒绝renderer不能完成，再证明实际成功与独立重开；只声称实际停止证据支持的标签。

**B6，最后接公开验收。** 017从start→claim→真实授权工作→submit→Observe→renderer→AnswerCommitted→公开正文，不能直接append完成事实；至少两个目标及不同command可区分。036核对共享Commands、跨入口和新OS进程读回；真实lease/model观察器先有正向对照，再证明status/export零effect。034另取得实际Host receipt与真实terminal，取消请求、abort返回和idle不冒充drained。Core、受控端口和实际Host的证明各自记账，C/D的完整乱序、usage、恢复、取消与late result继续保留。每个子包交付立即同步本表、总计划、逐项T状态与证据，不能到B6才回填。

### 全包实施义务索引（开工顺序以上方接手卡为准）

1. **Sphinx 合同封口。** 对 36 条现行 WHAT 与 SUPERSEDES 建逐条矩阵。重点是旧算法合格条件/退化、标准 Engineer、全链取消、旧 inquiry 明确拒绝；未承接的规则先进入现行 WHAT。不得恢复旧价格公式、四阶段接口或旧 SessionStore。
2. **唯一 durable Runtime 外壳。** 先读已有 Contracts/Ports/Admission/Core/Integrator，确定 command admission→canonical atomic batch→Current publication→effect dispatch 的唯一所有者。MCP/OpenCode/JS 都调用它，adapter 不判下一阶段、不持第二份 Current。复用现有真实 store，不创造仅供测试的 runtime。
3. **先做一条最短纵向链。** 真实 MCP start 接收不同目标与 command identity→持久 inquiry→一个可派发 work→真实 submit→accepted renderer→AnswerCommitted→status/export。至少两个 inquiry 并存；完整输入、回执和 disk reopen 能区分，禁止固定 unknown/status 模板仍混过。
4. **补结果准入。** exact replay、changed payload、attempt/fence/ticket/scope 错配、同轮乱序、control expectedRevision 各独立分支。用真实存储前后态和原 receipt 断言，不只调用 key helper。
5. **补派发与资源账。** 预留+意图同批持久后才能执行，receipt 丢失从 actual Host 对账；unknown 不当 absent。消费与容量分别计算；缺 usage 保留 outstanding；物理重复调用计费但语义结果最多一次。
6. **补观测、解释、计划和完成。** 第一事务保 raw result，第二事务纯 Observe；失败不重调模型。Decision 选计划，Agenda 只装箱；renderer 真被接纳才可完成。启用探针须走提出→派发→吸收→重估的公开可达链。
7. **补真实 OpenCode 收口。** 真实 receipt 绑定 inquiry/work/attempt/fence 与物理调用；读取 Host 的 message/tool-result 校验 token/schema；取消等待资源终结，late result 只允许规范的归档/计费，不改语义答案。
8. **补数学保证与持久恢复。** 跨插件传递 certificate 地址、scope/model/guarantee 类别；与独立数值/组合例对照。冷重开、删非权威 cache、重放不联网/不随机/不再调 LLM。
9. **全接入面对照。** 相同逻辑命令经 MCP 与 OpenCode 进入同一 runtime，允许物理 binding 不同，按 WHAT 分别比较 trace/state/semantic hash；status/export 为读，不创建 lease。Host 原生 `/sphinx` 不被迫启动或注入 MCP。

### 创建时12项TODO的逐文件验收与同步增量

下表保留原义务边界；上游新增019/018/036等正式行为证明，已经覆盖的局部项不重做。实际剩余TODO按现行文件逐实例核对，历史表格不充当新运行统计。

| 文件 | 具体场景 | 独立 oracle / 必须击红的违约 |
|---|---|---|
| 001 | 原始目标含 CRLF/Unicode；未经授权建议、用户明确 amendment、旧 expectedRevision 三条命令 | GoalSpec 原字节；只有授权推进 goalRevision；依赖旧目标估值失效，观测保留；建议自动改目标必须红 |
| 004 | 正常/失败/取消/真实重复调用、usage 缺失与后来到账；并发容量占满再释放，冷重开 | 从已接受事实独立算 signedFree/available/overrun；缺 usage 不归零、不释放预留；超支诚实保存、不拒绝真实 usage |
| 010 | append 前拒绝、append 后派发前崩溃、Host 已接受但 receipt 未记、明确不存在四切点 | 前者零 dispatch；有 intent 的恢复对同一物理工作 reconcile；未知不得新建第二 child；actual receipt 才能认接受 |
| 011 | 同 key+同 payload 并发/重启重放；同 key 改 payload；旧 global revision 的 exact replay | 返回同 receipt，只有一语义结果；冲突先于一般 stale；usage receipt 另行幂等，重复不重扣 |
| 012 | 同 round 三 work 按逆序回包；夹入另 work 引起 revision 变化；错 ticket/scope/attempt/fence | 合法局部回包都接受；无关全局变化不拒票；控制命令仍严格 expectedRevision |
| 013 | DAG A→B，与独立 C 同批；A 仅被选中/运行/失败/成功各状态 | 只有 A 真成功后 B 可装箱；DecisionReceipt 与 Agenda 可行性分层；不得把本批选中当完成 |
| 017 | renderer 未提交/拒绝/成功；只有停止理由没有 AnswerCommitted；缓存删除后重开 | Completed 必须引用 accepted renderer；停止原因分类诚实；无答案不能被模板 completed 掩盖 |
| 021 | first transaction 已接受 raw，Observe 抛错；重启；同版本重试或按合同创建新派生 inquiry | raw 永存、LLM 调用次数不增加；只有 pure interpretation 重试；修代码版本不能悄改旧 inquiry 的解释历史 |
| 022 | 同快照换序/遮蔽和增加新理由两种 probe；问前后回包变化/不变 | 持久可见输入、回答和 protocol；准确区分 measurement/intervention；不由变化推“改善”或不变推“正确” |
| 026 | posterior→ranking→decision，adaptive samples→refiner，A* 模型内 bound 的真实算子链 | 每槽 scope/model/guarantee 保留限制；不得升级 external truth/fixed-time coverage/无条件 bound；既有 decode 类别证明不重复冒充全链 |
| 034 | 真 Host accepted/unknown/拒绝、idle 无结果、错误 work token、取消 pending、真实 terminal、晚结果 | physicalRef 来自 Host；token/schema/attempt 精确核对；取消前 cancelling，资源终结后才 cancelled；晚结果不改变已终结语义态 |
| 036 | 用真实 SDK client 调七工具，传不同 inquiry/work/command 与非法权限字段；重复 status/export | 参数确实被分流到唯一 runtime；worker 不能注入事件/预算/证书/目标修订；查询前后 journal/lease/provider 次数无变化；旧阶段 alias 拒绝 |

### 没有 TODO 占位也必须联查的合同

| 条款 | 补查内容 |
|---|---|
| 005/006/007/019 | canonical batch 中非法后项使整批不成为 Current；缺引用拒绝；语义 graph 可循环但 work DAG 不可；Core 不解释“真/可靠” |
| 008/015/020 | schema hash 来自 canonical 文档内容；同长度不同内容的三哈希能区分；物理 binding 只影响其规定对象；不能保留模板哈希 |
| 014/023/024/025 | ballot cluster、真实 seed/assignment/label map/missingness；BIBD 实际曝光条件；协方差与 tie/abstain 独立 oracle，算法升级仍能从原始 ballot 重拟合 |
| 027/029/030/031 | 有界迭代不冒称收敛；answer.now 和成稿预留真实可达；停止只覆盖已列计划；每个启用 probe 公开 API 可达，空发现不重试到预设答案 |
| 018/032/033/035 | worker 输入权限、动态问题只用已注册 schema/授权工具、无 durable store 启动失败、业务 API 与 MCP 协议版本分离 |
| 历史/承接 | legacy inquiry 明确不支持而非空状态；标准 Engineer 权限和 Fission 不被旧只读身份收窄；全链取消按 D01 明确的现行合同验证 |

**Sphinx 完成的最小含义。** 不只 12 条 TODO 转绿，还要真实入口创建、派发、结果、完成、查询、取消、恢复都经过同一 Runtime，未完成的承接条款显式列出。新核心已有的纯算法是有用基础，但不能用其通过数字证明整个产品已经可运行。

## 零 TODO 包与跨包收尾

`cognitive-workspace` 已按 GAP-221 退役持久 canvas/jq/TodoSink；只维护真实 assume 与历史 no-op 解码边界，不重新造认知状态机。`epistemic-reasoning` 的旧测试只作归档/设计对照，不进入新通过集。`feature-ablation` 的 GAP-053 由分册 06 逐包关停证明牵头，先借 speculative001 的实际 Work 对照建立一例，再覆盖 registry 中全部现行能力。

每包完成时记录：现行 WHAT、触达入口、独立观察、红例、已执行 tier、未证明范围。认知质量的有限任务证据和机械协议证明分别报告；没有一份关键词检查可以包办二者。
