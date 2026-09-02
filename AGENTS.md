# AGENTS.md — 仓库工作协议

本文件只规定 Agent 如何查找规范、修改仓库和验证交付。产品语义只由 `requirements/<package>/` 定义。本文件引用条款，不复述条款。

# Kolmogorov 标准工作流程

- 工作流程
  → 更新 why → what → 阅读相关的规范文档理解为什么要这么做 → 调整测试 → how → GAP
  → 代码实现 → 检查全绿 → GAP Closed → 结束
- 两种典型失败
  - 写完才想起看文档
    代码已经按旧语义定型，要么返工，要么把旧语义固化，导致规范与代码偏离。
  - 扎进代码细节，丢掉大局
    症状被修好，条款仍被违反（例如给旧类型补字段、加 adapter、让旧测试继续通过——局部合理，合起来是在维护过渡态）。

# Kolmogorov 宝典

- 从最重要的开始。构建软件设计有两种方法：一种是使其足够简单，以至于明显没有缺陷；另一种是使其足够复杂，以至于没有明显的缺陷：请思考你想要哪种。取法于上，仅得其中；取法于中，不免为下。记住：君子不立危墙之下。当你写下勉强工作的代码时，透支的是未来的可控性，你在完全清醒的状态下，看着自己的逻辑链条一环扣一环地走向疯狂。毁灭你，或者拯救你，取决于你是否愿意写出明显正确的代码。
- 软件设计把不可消除复杂度压成不可再短的充分描述。好代码每行承载真实概念，名字指向领域事实，分支对应业务边界，类型拦截非法世界。文件数百行函数数十行通常是样板框架礼仪错误抽象挤占空间而非业务变深。工程第一洁癖是拯救读者注意力，让人和机器只付本质复杂度之账。小问题免框架税，大问题不手工搬砖，合适工具让问题露本相，不在配置生命周期隐式约定调试黑箱里绕路。
- 压缩不是合并，复用不是提前抽象。两段像只说明此刻长得像，不说明同一份知识。唯一表示是同一事实多处重复并开始不一致。独立生命周期概念逐字相同也该分居。边界先于抽象成熟，规则网络协议持久化权限语境视图各有领土。同个用户在认证后台订单会话是四种概念，正确解法是在上下文设海关，只传真需信息，模块包画国界，显式转换通关，架构测试守国界不被赶工磨穿，靠口头纪律的分层迟早被无意导入击穿。
- 类型系统是最便宜边防。字符数字布尔最会偷渡错误，账户号订单号用户标识若同属基本类型则编译器分不清。概念独立命名在运行时零成本，维护时直击知识边界。状态不靠可空字段和布尔开关拼凑，那会凭空造出不存在的非法组合。有限状态用有限构造表达，合法状态携带此刻有意义数据，矛盾状态在源码层生不出来。处理状态必穷尽分支，不让万能分支吞掉未来。新增状态编译器红线标红比线上日志收尸可靠。业务可预见失败不伪装成异常，不混null，不变解析字符串，找不到未授权库存不足余额不够都是返回类型具体分支，调用方被迫面对，前端直接匹配，不对文案做脆弱正则。异常只留给程序无法继续的事故。
- 非全封闭的错误处理会导致倒霉的嵌套解析。在多语言或前后端交界处，未能在边界处第一时间将其收敛为强类型，就会迫使下游编写大量胶水代码来进行运行时类型推导。
- 类型立起边界，行为回归数据。仅有字段没有规则等于敞开保险柜贴纸条。不可变数据自带约束，外界不能绕过方法偷改内部事实。变化时旧值算出新值，不在原物涂改。复杂对象构建链式设置加运行时检查只是延迟爆炸，构建阶段状态可编码进类型，必填步骤由编译器审查。派生新对象不用克隆可变原型再改字段，直接用不可变复制表达差异。纯函数内临时累加器如草稿纸允许局部可变，只要不改入参不碰外部同入同出。高频大状态更新若成瓶颈再引结构共享持久化数据结构只重建变化路径，瓶颈出现前别让优化成新偶然复杂度。为时间无关测试让路，依赖注入是好武器。
- 二十三式设计模式在代数数据类型+高阶函数+不可变数据三面棱镜下坍成三条原理。选实现的模式本质是语言缺密封类型和穷举匹配时用类层级模拟编译期分支：全局唯一实例由模块作用域承载，条件创建由密封类型加匹配表达，正交维度稳的建数据变的变函数参数，树形由和类型递归，状态切换成不可变状态机，新增扩展由模式匹配保证，编译器替你记遗漏。换行为的模式本质是语言缺一等函数时用继承接口模拟参数化：创建策略退成创建函数注入，算法骨架变化点交高阶函数，增强是函数组合，策略退成函数变量和声明式规则，处理链交组合子，操作请求退成可序列化纯数据由纯函数解释，语法解释退成小函数组合，遍历交生成器，函数可赋值传递组合后继承结构失去理由。共享缓存通知的模式本质是语言缺不可变数据和响应式原语时手工模拟信息流：接口不兼容有类型纯转换就是适配器，复杂子系统入口优先收敛公开API，内部混乱加门面只是遮羞，共享计算用纯函数缓存，观察变化交响应式流，网状通信退成发布订阅，历史快照退成事件重放，并发访问和延迟加载交Actor位置透明。GoF翻到末页只剩数据函数类型组合。
- 系统可理解性来自把判断写成规则原文，不是写成脑内单步调试的控制流。校验逻辑由签名统一小函数组成，每条独立命名，组合子串联。规则有依赖就短路：先确认轮到谁再检查手里有没有牌；规则独立就一次收全错，调用方获完整失败集合。业务表达式由是否有效有权限越界这类查询函数拼成，读起来像制度文本，不像一团if临时变量跳转路径。这样写是让源码成唯一不过期规则说明，业务方能指着一行讨论，测试能覆盖组合，编译器能保证分支完整。
- 纯函数是内核：不读时钟不掷骰子不查库不发网不写盘不改入参不造返回值外可见效果，同入同出。测试不用启服务器，重放不担心今明不同，审计不靠环境运气。真实世界网络文件时钟队列住在外壳，外壳收输入转命令，内核用当前状态和命令算结果，外壳把事件持久化广播投递。核心状态机压成一个签名：给定状态和命令返回下一状态加事件列表或强类型错误。旧状态不被修改，副作用不从函数体偷跑，事件成广播审计恢复投影共同事实来源。
- 验证不靠手工回放与临时脚本：禁止临时测试、一次性探针、只跑不提交的调试片段充当验收。调试过程永久化→排查与复现结论写成仓库内正式自动化回归（单元/集成/契约，随项目惯例命名与目录），纳入团队标准测试入口，可重放、可失败、可 CI。调试过程未落盘=未发生；注释掉的 print、随手 shell 试探、本地改完即删的断言=技术债预付款。
- 命令和事件必须分，意图可拒事实不可驳。用户说我要这样做，系统检查权限顺序资源规则，任何不过返回失败。事件说事已发生，重放历史只能忠实应用，不能因今天规则升级否定昨天写入事实。当前状态不是唯一真理，只是事件流积分，从历史折叠出的当下。银行信流水推余额，系统信不可篡改事件推局面报表时间线审计视图。原地赋值和UPDATE覆盖旧字段本质都在销毁从A变到B的事实，丢掉A存在过的证据。事件溯源是对信息完整性最基本尊重。修正历史追加补偿事件不改旧行，历史可涂改溯源就退化成覆盖写的伪装。
- 并发根本矛盾在共享可变状态，Actor将其翻转：每个处理单元拥己态，外界只发消息，内部一次处理一条不需要锁。事件循环用少量线程服务大量连接，每次上环快进快出，只做解析纯计算分发。数据库查询文件读写外部调用等阻塞操作交工作线程池，否则一个等待拖住同循环所有连接。实时共享态让写路径在墙内串行，读路径在墙外并发。写者独占态，更新后把只读数据推入管道，订阅者只消费不修改。给客户端推状态时安全边界在服务器最后一公里完成，每个接收方得己视图，私有数据完整，他人私密只留摘要计数或状态标记，别信客户端不展示，抓包工具不看界面。
- 事件落盘顺序决定记忆伦理。收到命令不能先改内存再写盘，内存会看见无证据未来。正确顺序是先追加持久化介质，确认成功后再替换内存权威状态。写盘失败等同命令未发生，写盘成功即使崩溃重启重放也回同一局面。物理载体顺应事件流，NDJSON一行一个自包含事件，追加只碰末尾，恢复逐行读取折叠。普通JSON数组追加要改已有结构，风险和语义都错。恢复时首行损坏应在损坏处截断，不跳过后续行。事件前后相扣，缺了中间后续事实就建在错基上，宁可少恢复一步，不恢复矛盾态。历史变长格式演化机器故障需要少而硬的约束，快照只是书签非真理，要记录事件总数、完整状态前缀、事件校验指纹。恢复重算指纹，对不上就弃快照从头重放，不靠文件大小字节数修改时间猜测对齐。事件结构变更每条携版本号，旧版逐级升级转最新语义，升级函数纯且幂等，不读时钟不碰网不依赖环境，否则同一历史不同时间重放出不同世界。大量独立日志，每个房间恢复独立隔离，一个文件坏只牺牲自己。启动拿文件排他锁防两个实例同时读写撕裂历史。这条链上铁律说同一件事：别信刚写入已安全，除非证明安全。先写盘后改内存因内存会骗，前缀完整性因后行完整不代表站对基础，版本号校验因大小时间撒谎，快照指纹因快照可能对不上。整条持久化纪律本质是信任负向清单。
- 调查不是猜谜。改代码前先定位真正拥有者、读周边合约、理解影响路径——跳过任一环节是盲编辑。工具报错是信号不是噪音，解决或显式放弃再前行。API形态、文件内容、Host语义不靠猜测靠读源码跑验证；消除错误不靠试错消症状靠因果解释加回归测试——猜的修复不是修复。独立源无依赖就该并行调查，饿着并行度空转等于浪费。大段替换不比精准修改便宜，保已知正确结构做最小改动。补丁绕开根因不修模型是借债不是还债。大意图塞给单次操作不拆独立可审单元→分拆并行执行。重构停中途新旧并存未迁完→完成所有权转移再删旧路径。无瓶颈证据引入的复杂度不是优化是浪费。
- 知识不记录等于没发生。教训不落文档，重复已知错误是迟早——流程有洞自己不补。决策理由不留，下次面临同样权衡重新推演，每步重走一遍弯路。不变量不写进文档，新来者无意中破坏，事后才知那里有条线不能碰。文档与源码不同步比没文档更危险——读者信了错的比没信更糟。门面封装内部混乱而不清理，遮羞布不是架构，债在墙后越长越大。手动重复三次以上不自动化，人力不可持续，错误不可消灭。实验原型直接进生产不清理，每个人都在踩临时搭的桥。旧兼容路径保留超过明确声明周期，兼容性负担是隐性税，不删就永远交。架构决策缺门禁验证，退化只等一次赶工。
- 名字是代码的第一份文档。名字骗人，读者每读一次就被误导一次。缩写引发焦虑，读者得解码才能理解——每次解码都是一次无意义的上下文切换。数学味命名把领域直觉赶出代码，通用工具桶收容无数无关函数——进去容易出来难。多层转换在无差别边界间叠加翻译成本，隐式约定靠人记不靠编译器检查——迟早有人忘。注释只描述代码已经在做的事→那是剧场不是文档。状态宣告的注释噪音冲淡真正信号，领域词汇在代码与业务讨论中对不上→各说各话，最后没人知道这个词到底什么意思。偶然复杂度超过问题本身，框架礼仪或错误抽象压倒了业务——读者的注意力被无关细节耗尽。
- 红→绿→重构不是可选仪式：生产代码前先有失败行为测试，缺陷修复必有回归测试证实旧败新胜。测试断言公开行为而非内部协作——调用次数、辅助布局、私有结构是今天怎么写的证据，不是正确性的定义。不能为变绿而削弱断言——那不是修复是掩盖。不确定性测试本身是债：隔离随机源、消除时序依赖使每趟确定，多次重跑直到绿不算验证，消除不确定性才是真绿。测试不依赖真实时钟、墙钟延迟、套件顺序或全局残留——每项都是隐式依赖，今天绿明天红而你不知为何。新错误处理、取消、回滚、重试路径若不直接测试，它们一定在你最需要的时候第一次执行。Host、provider、存储、网络、语言边界被改时必有契约级测试——否则你不知道破坏了谁。
- 验证靠阶梯不靠跳跃。纯函数测试→契约测试→重放测试→真实canary，每级过完才能晋升——绕过一级等于未验证那级。逃过门禁的自测等于没自测，门禁不能变红等于假门——没锁的门不是门。超时放大掩盖资源泄漏而非修复因果信号，mock由可见请求决定而非隐式计数器——可变场景状态是藏在mock里的幽灵。Host边界依赖靠canary证明不靠文档假设，测试走真正接口而非私有路径——走私有路径测的是实现不是行为。覆盖本身不验证行为，断言要有失败价值——断言永远不会失败的断言是安慰剂。通用不变量靠属性测试而非几组例子——例子能过不保证性质成立。完成宣称靠实际运行而非口头声明，一次性探针不转持久测试等于浪费了那次发现。
- 范围扩张使交付失焦。临时脚手架和实验分支保留在交付结果中→要么转维护工具要么删——保留是犹豫，犹豫是债务。旧兼容路径在明确clean break后仍保留→完了就该断，不断就是两个未来都要维护。不可达或废弃生产代码留给后人猜疑→版本控制记历史不记尸体，删了还能找回。TODO FIXME defer正确性工作→要么完成要么拒绝当前改动，TODO是最贵的注释——它让你相信未来会做而未来从不来。旧实现被注释保留替代删除→版本控制不欠存储费，注释里的代码是死的。调试打印断点在产线残留→转有意诊断或删，调试输出不是日志。令牌凭据嵌入源码提交→立即轮换转机密边界，泄露的时间窗口越短越好。破坏性操作缺显式授权及目标验证→停下确认再动手，误删比不删贵万倍。新依赖对现有平台性价比不足→用已有或小实现替代，一个依赖是一个你需要永远维护的合同。

这些分散规则围绕同一闭环转：用类型消灭不可能态，用纯函数固定可重现判断，用事件记录不可抵赖事实，用边界隔离语境，用组合子压缩控制流，用模块函数生成器响应式流声明式规则接管旧类层级样板，用架构测试守分层，用合适重量工具降低偶然复杂度。宏观系统切成纯内核加薄外壳，中观上下文API消息事件视图各守其位，微观变量名返回类型分支穷尽日志行版本号校验指纹替同一原则服务。不靠纪律审查文档，穷举检查让编译器站岗，代数数据类型让编译器拒非法态，架构测试让编译器守边界，密封接口让编译器记新增分支。写代码时编译器是对手，设计类型时编译器是士兵。最好代码不是模式最多，而是读者能沿每个概念边界一路追踪：从用户意图到业务判断，从事件落盘到状态重放，从私有数据到安全视图，从单行规则到整体架构，处处无暗道无多余解释，都像问题本身找到不可再短不可混淆不可逃避的表达。这一切指向同一件事：把人的注意力留给只有人能做的事。

## 思考和输出
- 偶然复杂度+修饰礼仪=∅。∀ 词必承载核心概念，零冗余。
- 斩断语气词+垫字。消除控制流跳转→直击核心事实。短句+短词，极致信息密度。
- 强类型术语+代码符号+精确错误字符串+标准缩写=绝对精准。不给脆弱文案留伪装。
- 严禁状态宣告。源码=唯一时效规则，回答=纯干货。
- 拒绝臃肿。行文=极短函数，快进快出→直接定位知识边界。
- 必要时引入 Unicode 或数学符号(如 +, =, →, ∀, ∃, ↓)进阶压缩空间。
- 风格=宝典+铁律，当代极简+正确标点，拒绝`等宽`加粗等小格式。

## 铁律输出示例
> Fable 编译 JS 环境，如何选择异步原语？全库开除 Async+Task。规避运行时装箱开销+状态机断层。
  JS.Promise<'T>=唯一异步货币。async{}→promise{}，原 Async 静态方法→Promise 模块。
  调用 Node.js 异步 API 或对外暴露接口，如何处理类型转换？
  拒绝任何装箱拆箱与强转。原生 JS Promise 完美融入 promise { }→直接 let! 解析。外发 Hook 签名直写 JS.Promise<unit/obj>→消除边界摩擦。
  Fable 禁用 MailboxProcessor 后，如何实现 Actor 模型防并发泥潭？
  JS 单线程串行化本质=Promise 链。造 SerialQueue 局部可变变量 tail 锁住队尾。内部捕获异常防止断链。异步变更强行排队→无锁保护内部状态。
  异步操作中如何处理并发、超时与异常？
  并发→Promise.all，超时→Promise.race 组合子。可见失败禁止裸抛异常。promise 内部就地 try...with 捕获→转为强类型 Result 分支→逼迫调用方匹配，不给异常留改道机会。

## 关于工具调用
- 只要独立→并行读取/调查/验证；并行度服务于因果清晰，不服务于工具数量。
- 同文件重叠编辑、存在先后依赖的编辑、依赖上一结果的命令必须串行。系统不保证并行工具执行顺序。
- 异文件也先判断语义依赖；共享类型/公共接口先定边界，再迁调用方，最后删旧路径。
- 拒绝频繁全量重写文件→精准修改=核心。
- 鼓励多意图并发→拆分独立元素+对每个意图提供完备背景知识(上下文互隔离)。
- 诉求拆细→细粒度并发。拒绝大块意图→规避长时延迟。

## 极简架构与编码铁律
- 极度推崇 DRY+KISS+极简架构。厌恶+拒绝复杂错误处理+日志记录+配置管理。
- 除非绝对必要→零注释，零意图解释(隐晦处除外)。
- 绝不偏离最佳实践，严禁 Dirty Hack，三思而后行。
- 厌恶无谓赋值→灵活处理+内联。边界=不引起阅读焦虑。
- 严禁通过一行多事+滥用分号来伪造行数减少。
- 强制使用高阶语法→消除代码琐碎。
- ∀变量名=极致清晰。绝不用数学味/晦涩命名+引发焦虑的缩写。
- 除非明确要求→颠覆式创新+破坏式创新。重构时丢弃旧兼容性负担，严禁滥用 facade 逃避架构整理。
- 零保留旧代码。不以 Public+契约+影响面大为由逃避重构。通知下游→不合理处皆可改。
- 任何时候，尽量精准实现，优雅实现，拒绝兜底实现或者看似“双保险”其实是弄不清楚原理不得不乱来的实现方法。

## 具体工作
- 严禁使用 dotnet build。本仓只有 Fable 编译目标，构建必须使用 node scripts/build.mjs 或 npm run format-build-test，严禁引入或依赖 .NET 编译构建。
- 宁慢且稳，严禁使用自动化程序批量增删改查程序代码。
- 脚本=急速幻觉+反复返工；手工编辑=脚踏实地+步步为营。慢=快。

## 守江山 — 已完成架构，禁止倒退

本节只收录已经同时落到 production + executable proof + architecture gate 的事实；未达三件套的新增工作必须经正式 owner 裁决后建立 node，按下方 DAG 施工调度宪法执行，禁止遗留为口头提案。

以下关于 ParticipantIdentity、managed chat、capacity/failure policy 与 provider lifecycle 的条目只记录 reliability-stabilization 已落地闭环；不改变第五十七章其他节点、coverage backlog 或其他工作流的完成状态。

### 当前开发者移交 — 2026-09-01

- 总目标仍是 debt-zero ReleaseClosure。原计划文件 `.omo/plans/agents-debt-zero-release-closure.md` 不在工作区，其 Task 1–14 与后续 frontier 扩展的成果已全部反映在本文件“守江山”。临时 `scripts/checks/migration-ledger.json` 已删除；71 个节点全部 `DONE`、coverage backlog 0 的状态由 `scripts/checks/semantic-owners.mjs`、`owner-contracts.mjs`、`owner-projects.mjs`、`architecture.mjs`、`requirement-trace.mjs` 永久门禁承接。ReleaseClosure 已达成；57.15 白盒 FCS 扫描链路已退役；后续新增工作必须经正式 owner 裁决后建立新 node，禁止重造线性 wave。
- reliability-stabilization 的原始设计与实现归属 `wanxiangshu-fix`：`947e144f1 spec(reliability): redefine identity and chat execution ownership`、`e956455cf Stabilize managed chat admission and recovery`、`980dbb5c7 Document reliability stabilization guardrails`，作者记录为 `test <test@example.com>`。当前开发者只负责把该分支语义合入 master，不宣称这些成果。
- master 侧 Task 10–13 的已提交成果分别是 `4adde35f8`、`9e6d1f2bb`、`6ce8c710c`、`219cd919b`。Task 13 的生产/证明实现由 `PersonaTypedCore`、`IdentityOfficeSplit`、`IdentityShimCutover`、`PersonaHostBinding`、`IdentityRedProof` 等协作 agent 完成；本次 merge 语义解冲突由 `MergeIdentityCore`、`MergeReliabilityFlow`、`MergePeripheralFlow`、`MergeGovernance` 完成。保留此 attribution，禁止后任把合并动作写成个人原创。
- merge 后的最终 identity law 以本节守江山条款与 `requirements/participant-identity/WHAT.md` PID-001..011 为准：`ParticipantIdentity` 属于 exact logical run，并随 `AuthorityRootAccepted` 原子 durable 安装；Task 13 早期的 process-local `SessionPersona`/`PersonaBinding` 方案已被可靠性语义取代并删除。`OfficeCapability` 的 `ToolPermission`/权限矩阵切分仍保留，`Roles` 只拥有 Role/AgentTier。
- 该 merge 的验证闭合已完成，成果在 `60532ec23`、`696e67cad`、`d89fb0511`、`89ff8f809` 及本次提交。当时"只编译不跑测试"留下的账已全部结清：`npm run format-build-test` 整条阶梯绿 —— fantomas 696 文件 unchanged 2s、build 38s、unit 3909/3909 15s、Long Stroke e2e 11s（57 步 5.2s flow）、`scripts/check.mjs` 门禁（semantic-owners 0、owner-contracts 0、owner-projects DAG 0、requirement-trace 0、authority-boundary 0、architecture 0）、integration 14/14 step 304s、package 与 `npm pack --dry-run` 各 1s。全部按 production/contract 根因修复，未新增 baseline、suppression、allowlist，未削弱断言。
- 三条必须继承的工程事实：① owner check 已从 F# 白盒扫描迁移为编译边界与静态元数据门控，FCS evidence 管线与 `OMP_FCS_*` 复用机制整体删除，常规路径以 `scripts/checks/owner-contracts.mjs` + `owner-projects.mjs` 验证 148 个 owner locality 的 DAG 与契约；② 真实 F# 工程检查不能放进 unit tier（5s verdict 静默预算），实扫 lane 属于 `requirements/<package>/tests/integration/`，且该 step 需自报 `perTestTimeoutMs`；③ `npm run format-build-test` 仍是唯一 release sink。整条阶梯时间由 flat build + owner project scans 决定。
- 下一任第一动作：`git status` 确认工作区干净，`npm run format-build-test` 绿且 `semantic-owners.mjs`、`owner-contracts.mjs`、`owner-projects.mjs`、`architecture.mjs`、`requirement-trace.mjs` 全绿、coverage backlog 0 时方可宣称 ReleaseClosure。无未裁决 node 时不应再选下一节点；后续新增工作必须经正式 owner 裁决后建立新 node。

### 57.15 完成记录 — 2026-09-02

57.15 终局已闭合：148 个 owner locality 全部毕业，`compiler_boundary_localities` 与 `owner-projects.mjs` 一致，`owner-contracts.mjs` 验证 778 条 published contract 与 release-closure 节点。白盒 FCS 扫描链路（`owner-symbol-uses.fsx`、`.fable-build/*-fcs`、`OMP_FCS_*` 复用、`composition-root-invariant.mjs`）已整体删除；`dsl-ownership`、`authority-boundary`、`semantic-decorator-invariant` 保留为静态文本/正则门控，`scripts/check.mjs` 不再保留 `owner-dep` lane。最终验证 `npm run format-build-test` 绿通过。2026-09-01 的 62 locality 历史 ledger 已归档，当前 scheduling 唯一事实源为 `compiler_boundary_localities` / `owner-projects.mjs` / `owner-contracts.mjs`。

- 状态只能被事实改变，等待只能被事件结束。correctness path 禁止用 wall clock、`TimeSpan`、timer、deadline、sleep、polling 或 timeout 推断业务状态；取消是显式事件，不是时间流逝。
- durable fact 是业务事实唯一来源。process-local registry/waiter/flight 只拥有物理资源，不得充当持久业务状态、恢复资格或成功证明。
- 一条语义只有一个 owner、一个 vocabulary、一个推导公式。durable/domain 类型不得为了复用反向依赖 Host/OpenCode adapter；adapter 只能消费/产生领域词汇。
- 恢复必须 failure-driven 且 typed。Provider attempt 的 `ProviderRequestKind`、slot outcome、durable receipt/authority evidence 决定推进与成功记账；禁止按错误字符串、Role 猜测、terminal 文案或 parked cursor 位置恢复。
- 禁止平行状态机与“测试镜像实现”。production 与 test surface 必须调用同一纯 decision；surface 只做表示转换。发现 callback 已接线却无生产调用者、facade 只遮旧实现、第二套 reconstruction/formula 时，完成所有权迁移并删除旧路径。
- 类型必须拒绝非法世界。不要用 `bool + option + stage/phase/generation` 拼状态；有限状态用 DU，合法 case 携带所需数据。wake/resume 这类结果必须把证据放进 case，而不是先返回 bool 再查第二槽。
- F# 主因果流必须从上到下读。`fsharp-control-pyramid` 是绝对零门禁：Error/Ok plumbing 用 `result`/`taskResult`，Option plumbing 用组合子/CE，独立状态一次 tuple match，领域内层 decision 提取为有业务名字的 Evidence→Decision。严禁 suppression、allowlist、baseline 增长或换语法骗 gate。
- 测试只能通过注册 semantic surface 穿越 Fable 边界。禁止 requirements 测试 deep-import production `dist/**` 私有模块、读取 F# DU `.tag/.fields/.cases()` 或靠私有声明排版证明行为；需要能力就增加最窄的正式 Surface，并登记 owner/law/consumer。
- 每个 executable test 恰有一个 primary `WHAT[ID]`。跨包性质拆成多个独立 proof；HOW 只链接真实可执行 proof，不用 prose 代替测试。
- clean break 完成即删尸体：旧类型、旧 namespace、dead callback、兼容 facade、重复 formula、失效 baseline/ledger 不得保留。版本控制负责历史，不让生产树负责考古。
- 改公共边界时先迁 owner，再迁 consumer，再删旧 owner；每一步都让编译器暴露遗漏。不要先造 adapter 保两套世界同时活着。
- Git/store 协议字符串也属于单一 vocabulary。hook/脚本若必须使用同一 ref/path/protocol，应从 owner 的定义派生或通过正式 surface 暴露，不复制第二份正则/字符串常量。
- 关键 composition trace 已进入静态守城：PluginTransforms 顺序、composition-root wiring、recovery/join、cross-callback PC 都有独立 gate；禁止把显式乐谱重新包装成动态 middleware/service-locator。
- production semantic ownership 已全覆盖：每个 production `.fs` 必须出现在 `scripts/checks/semantic-owners.json` 且恰有一个 primary owner；新增/移动文件必须同步 owner，不得靠目录猜 owner。
- 当前 F# control-pyramid、dead private binding、JS semantic-boundary debt 均为 0；这是零基线，不是未来可重新积累的 allowance。
- durable store 已统一到 canonical EventStore spine；feature-owned durable backend/private ref、dual-write migrator、业务层 Git bypass 由 `unified-store-gate` 拒绝。
- provider/context recovery 已切到 failure-driven + durable-event-driven + time-independent；任何重新引入 timeout/polling/process-local recovery proof 的改动都是回归。
- participant identity 已 clean break 到 opaque `ParticipantIdentity`：authority acceptance、execution profile、child ownership evidence 与 durable recovery 只消费这一词汇；旧 `SessionPersona` 与字符串 Role/agent 推断路径已删除。新增或复用 session 必须由 owner-issued typed identity 建立，`participant-identity-boundary` + reusable-session proofs 拒绝平行身份源。
- managed chat admission 已收敛为单一 transaction：纯 `ChatAdmissionIntent` 决定动作，exact `SessionId + PhysicalUserMessageId` acceptance durable 后才可取得并提交 `ExecutionAdmissionLease`；provider 前失败必须先写 typed pre-provider terminal 并释放 exact fence，再按 Hook policy 传播失败。Host callback 不得另写 acceptance、绕过 lease 或从 process-local binding 推断已接纳。
- capacity 与 provider failure 已形成封闭代数：`ExecutionAdmissionQueue` 有硬上界，lease lifecycle/fence transition 显式区分 Applied / AlreadyApplied / StaleFence / Conflict；`ExecutionFailurePolicy` 是 retry、fallback、breaker、capacity settlement 与 message disposition 的唯一决策 owner，只有其 opaque `ProviderRecoveryAuthorization` 可推进 durable fallback。`retry-owner` 与 capacity interleaving/soak proofs 拒绝第二 writer、bool/option admission collapse、跨 session credit 与 stale release。
- managed provider lifecycle 只接受 exact public assistant evidence：`Accepted → ProviderStarted → Terminal` durable 顺序绑定同一 `ChatExecutionKey + ProviderRunIdentity + ProviderRequestKind`；重复 exact start/terminal 幂等，冲突 identity、terminal 后 start、unbound coarse failure 均 fail-closed。恢复由 durable projection + typed physical observation + closed failure decision 重入普通 CE；`HookPolicy` 静态表与 `hook-policy` gate 固定 critical/degradable failure 行为，process-local diagnostic/counter 只观测因果结果，不充当恢复资格。
- managed provider → tool handoff 已是显式 capacity step 边界：tool body、role/capability gate 与同步 descendant provider work 之前必须先按 exact `ProviderRunIdentity + PhysicalUserMessageId` 结束当前 provider step；严禁把 `InFlight` capacity 持到 tool 返回或用 timeout 解死锁。Distiller cleanup 对同一 owned child 的物理 cancel 至多一次。
- Strength lifecycle 已时间无关：`StrengthReplicaRuntime` 不拥有 timer/deadline/elapsed-time terminal arbitration。Treatment 显式开启后等待 Replica 的真实因果终态；DryRun 启动后立即放行 Owner，只由 K gate、Replica terminal、exact Owner `TargetProviderRun` terminal 或 owner cancel/delete 收口。语义 completion 与物理 retirement 分离：K gate/取消可先阻止后续 provider admission，但 Replica 身份必须保留到 Host terminal/session deletion，以吸收在途 transform；严禁靠 sleep/timeout 或“DryRun 必须抢在 Owner 前跑满 K”证明正确性。
- 子→父 run-bounded LWR 从本 invocation 首个 assistant part 起算；caller 已知的 user charge 不得伪装成 Chronicle/Recent work 回传。父→子普通 bounded delta 仍保留原语义。
- Fable build 已收敛为跨进程 lock 下先清空上一轮 `dist/`，再执行一次真实 `Debug` compiler invocation；compiler 成功退出后才验 artifact/Surface Manifest，configuration 不得依赖 Fable 的 watch/one-shot 默认值。源码删除不得留下可被 package 收走的陈旧 JS；watch daemon、`FableBarrier.fs`、ack、source-touch barrier、artifact-exists fast path 均已删除；现存 `dist`、日志静默、mtime 与 wall-clock 不能证明构建成功。
- `Interaction/Repair` 切片（`CompletedTurn.fs`、`CompletedTurnSurface.fs`、`InteractionRepair.fs`）primary owner 已切至 `interaction-authority`，`Port.fs` 归 `dispatch-protocol`；跨 owner 消费通过 `published-contracts.json` 的 `Interaction.Repair.Classification` / `Interaction.Repair.Workflow` / `Interaction.Repair.SendOutcome` 授权；`semantic-owners.json` 与 `interaction-repair-invariant` gate 已闭合。

## 交付门禁

- 生产代码/规范/测试变更至少通过 `node scripts/build.mjs` 与 `node scripts/check.mjs`。
- 涉及行为、持久化、Host/provider、Git、分发或跨包边界时，继续跑对应 requirement suite；准备提交主干时优先跑 `npm run format-build-test`。
- `scripts/check.mjs` 中 architecture / ownership / deadcode / semantic surface / requirement trace 等门禁只能修源码或契约使其变绿；禁止扩大 baseline、增加 suppression/allowlist、删测试或降低阈值。
- 提交前重新 `git status` + diff 审查：确认无无关用户改动被覆盖、无临时文件、无调试输出、无旧路径残留、无生成物误入。

# 文档生命周期

正式语义在 `requirements/<package>/`（每包 WHY/WHAT/HOW + 测试）。
deferred 未来材料归 `proposals/`。
Proposal 的提出、讨论和裁决发生在 Agent 执行工作流之外，由用户或负责人管理。
- 普通小型修复、局部重构、测试或格式修复不要求创建 Change；能在一次修改内完整对齐
  requirements 文档、实现与 proof 的工作直接闭合，不为流程制造空壳 Change。

## 修改纪律

- 工作区可能包含用户改动。修改前查看 `git status` 和相关 diff；保留无关改动。
- 自动提交 git commit。允许推送 `master`；禁止 force push master。

目标定为：

> 多工程负责语义边界；Contract/Runtime 反转负责缩小源码闭包；扁平投影负责规避 Fable 的 `ProjectReference` 图税。

## 最终结构

```text
                     ┌────────────→ Consumer.Runtime
Domain.Contract ─────┤
                     └────────────→ Provider.Runtime

CompositionRoot.Runtime
  → Consumer.Runtime
  → Provider.Runtime
```

禁止：

```text
Consumer.Runtime → Provider.Runtime
Provider.Contract → Provider.Runtime
Contract → Host / Adapter / Workflow / Persistence implementation
```

例如 EventStore：

```text
EventStore.Contract
  EventEnvelope
  EventStreamId
  AppendRequest
  AppendResult
  EventStorePort

EventStore.Runtime
  → EventStore.Contract
  CanonicalEventCodec
  GitObjectDatabase
  ProcessGitRawStore

Mission.Runtime
  → EventStore.Contract

Host.Composition
  → Mission.Runtime
  → EventStore.Runtime
  注入 EventStorePort
```

这样修改 `Mission.Runtime` 时，其编译闭包不再包含 Git、Process、EventStore 实现；修改 `EventStore.Runtime` 时，也不需要编译 Mission。

## 编译模型

仍然只保留 F# → Fable：

```text
owner fsproj DAG
    ↓ 计算所需源码闭包
零 ProjectReference 的临时 flat fsproj
    ↓
单次 Fable compile/watch
```

不能直接执行：

```bash
fable Some.Owner.fsproj
```

因为这会让 Fable/MSBuild 递归解析几十个 `ProjectReference`。之前实测，同一组 223 个 `.fs`：

```text
原生 56-project graph：83.27s
扁平源码投影：        13.13s
```

所以**反转解决闭包大小，flat projection 解决工程图开销**，两者缺一不可。

## 能获得什么加速

### 明确能加速

- 单 owner 开发编译
- focused test 前编译
- PR impact compile
- 修改 provider runtime，但 consumer contract 未变
- 不涉及 composition root 的局部开发
- watch 启动后的持续编辑

之前 EventStore 原型已经证明：

```text
反转前：
69 projects
284 production .fs
18.15s clean

反转后原型：
4 projects
14 production .fs
5.58s clean
```

### 不承诺的部分

全量 release clean build 仍然需要编译全部 production F# 源码：

```text
node scripts/build.mjs
```

如果总源码量不变，它不可能凭拆工程神奇地从 38s 变成 5s。正确目标是：

```text
局部开发：显著加速
全量发布：不因多工程而减速
```

全量构建继续只用一个 flattened emitter，一次编译全部源码，绝不逐 owner 编译 148 次。

## Contract 的硬标准

Contract locality 必须满足：

1. 只包含类型、port、capability、request/result、稳定 vocabulary。
2. 不包含数据库、Git、文件、网络、Host、进程、工作流实现。
3. 不引用自己的 Runtime。
4. 传递闭包也不得包含自己的 Runtime。
5. 不为了复用四个字符串引用一整个业务 runtime。
6. 每个公开实现由 `.fsi` 封口。
7. Contract 可以依赖更基础的 Contract，但不能依赖 adapter。
8. Composition root 是唯一同时看见 Contract 与具体 Runtime 的位置。

特别注意：

```text
Contract ≠ 把 Runtime 文件改名为 Surface
```

现在一些标为 `contract` 的 locality 仍混有 Controller、Workflow、Store、Host、PTY、Recovery；这种必须真拆。

## 接口应该偏粗，不要把实现细节泄漏出去

错误：

```fsharp
type EventStorePort =
    abstract ReadGitObject: ...
    abstract ParseJournalLine: ...
    abstract OpenProcess: ...
    abstract ResolveRef: ...
```

这只是把 Runtime 内部结构搬进 Contract。

正确：

```fsharp
type AppendRequest = {
    StreamId: EventStreamId
    ExpectedVersion: StreamVersion
    Events: NonEmptyList<EventEnvelope>
}

type AppendResult =
    | Appended of StreamVersion
    | VersionConflict of actual: StreamVersion
    | Rejected of AppendRejection

type EventStorePort = {
    Append: AppendRequest -> JS.Promise<AppendResult>
    Read: ReadRequest -> JS.Promise<ReadResult>
}
```

消费者只认识业务效果，不认识 Git、文件路径、process、codec 和恢复步骤。

## 变更影响规则

需要区分 `.fs` 与 `.fsi`：

### 只改 Runtime `.fs`，`.fsi` 不变

```text
编译 Runtime 自身 closure
不编译普通 consumers
```

### 改 Contract `.fsi`

```text
Contract
  + 所有 reverse consumers
  + 它们所需的 forward contract closure
```

这些输入合并成一个 flat project，只启动一次 Fable。

### 改 fsproj、props、package lock、Fable 版本

保守走全量 flattened compile。

## 需要加的架构门禁

至少固定以下规则：

```text
contract-runtime-direction
  Contract 的直接/传递闭包不得包含 Runtime

foreign-runtime-reference
  外域普通 consumer 不得引用 provider Runtime

composition-only-runtime-binding
  只有 composition root 可绑定 foreign Runtime

contract-closure-budget
  Contract 闭包不得无界增长

owner-compile-flat-only
  owner/impact compile 禁止把原生 ProjectReference 图交给 Fable

compile-input-union
  flat project 输入必须与计算出的 owner closure 精确一致

implementation-change-impact
  Runtime .fs 改动且签名不变时，consumer 不进入 impact set

signature-change-impact
  Contract .fsi 改动时，reverse consumers 必须全部进入 impact set
```

## 第一批拆分

### 1. EventStore

从当前混装 locality 中拆出：

```text
EventStore.Model.Contract
EventStore.Port.Contract
EventStore.Core.Runtime
EventStore.Git.Runtime
EventStore.EventVocabulary.Contract
Strength.EventVocabulary.Contract
```

重点删除：

```text
EventStore → Strength.Predictor.Runtime
```

四个 event type 字符串不应拖入 261 个额外 `.fs`。

### 2. Delegation

当前 `execution-delegation-handle-surface` 实际混有 23 个实现文件，应拆成：

```text
Delegation.Contract
Delegation.Fold
Delegation.Sync.Runtime
Delegation.Fork.Runtime
Delegation.Host.Adapter
Delegation.Pty.Adapter
Delegation.Recovery.Runtime
```

普通 consumer 只引用 `Delegation.Contract`，不能引用 Fork、PTY、Host 或 Store。

### 3. Host Session Quiescence

拆成：

```text
Host.Session.Contract
Host.Signal.Contract
Host.Diagnostics.Runtime
Host.Signal.Adapter
Host.Session.Runtime
Sphinx.Host.Adapter
```

避免一个 quiescence consumer 连带编译 Sphinx、诊断、消息投影与整个 Host runtime。

## 验收标准

每次切一个 locality，必须拿数字验收：

```text
before closure projects / .fs / clean seconds
after  closure projects / .fs / clean seconds
```

建议预算：

```text
Contract locality：
  transitive production .fs ≤ 100
  目标 clean compile ≤ 8s

Focused runtime locality：
  transitive production .fs ≤ 185
  目标 clean compile ≤ 10s

超过全仓 60%：
  不伪装为 focused compile，直接使用 full flat build/watch
```

最终路线就定为：

```text
真 Contract/Runtime 反转
    +
粗粒度 typed port
    +
composition root 注入
    +
owner/impact flat projection
    +
Fable watch
    +
一次全量 release build
```

不做 JS 链接，不造第二套包系统；只把现在“名义多工程、实际源码全透传”改成“消费者只看到 contract 源码闭包”。
