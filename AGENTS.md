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

### 57.15 下班 checkpoint — 2026-09-01 22:06 +08:00

本小节覆盖上方“下一任第一动作”对 57.15 的旧现场描述；上方 ReleaseClosure / reliability attribution 仍是历史事实。此 checkpoint 刻意保存两个 `RUNNING` locality 的未完成签名施工，**提交进 Git ≠ node DONE**。通常 `compiler_boundary_localities` 是毕业事实源；但本 checkpoint 恰好冻结在 EventStore 已被并发 writer **预登记**、compiler 仍红的中间点，因此该一条登记是明确的 provisional exception，必须以 executable proof 补绿后才恢复为可信 graduation。恢复施工时禁止把 checkpoint 中的 `.fsi` 或 provisional manifest entry 当成 DONE。

**接管续记 — 2026-09-01：** 上述 provisional exception 与两个 RUNNING locality 均已闭合，下方相关段落仅保留历史取证，不再描述当前施工状态。`durable-events/persistence-eventstore-storetypes` 已完成 `606`-source owner Fable、EventStore focused proof `65/65`、flat build/linkage 与全 `check.mjs`；`delegation/execution-delegation-handle-surface` 已完成 `690`-source owner Fable、delegation proof `125/125`、crash/recovery proof `79/79`、flat build/linkage 与全 `check.mjs`。delegation `.fsi` 首次让 foreign consumers 真正受 extension signature 约束，并暴露 `Agent.fsi` 漏写 implementation 已有的 `[<AutoOpen>]`；补齐该 signature attribute 后 flat build恢复，无需修改 consumer，也未新增 ProjectReference。当前可信 graduation = **`88 / 148` locality、`397 / 701` production `.fs`**，剩余 `60` locality；当前 READY 恰为 `output-distillation/process-largegatesurface`（3）、`durable-convergence/persistence-eventstore-processeventlog`（6）、`context-compression/context-companion-companionfactfold`（12）。后续实时 frontier 仍必须由 `compiler_boundary_localities` + owner-project DAG 机械重算，禁止继续使用本 checkpoint 的 `62 locality` 表作为当前状态。

- checkpoint 前已提交 HEAD：`0b0786ffc refactor: sign strength predictor boundary`。该 committed state 已完成 57.15 的 **有效 `86 / 148`** compiler-boundary locality，覆盖 `369 / 701` production `.fs`；有效剩余 `62` locality。当前未提交现场因 EventStore provisional pre-registration 使 manifest 机械显示 `87 / 148`、`374 / 701`，这 5 个 source 尚未通过 owner compiler，不得计入 DONE。owner graph 保持 `148 localities / 701 sources / 1771 refs / DAG`。
- 本日 57.15 已闭合的后段 checkpoint 包括：`b21d0dc2f` host+sphinx、`b97f06006` OpenCode+degeneration、`fbf24db46` cognitive prompt、`1d4d810c7` session ontology、`cff43e134` dispatch recovery、`33a6e7705` interaction-authority fold、`1de054f22` managed-session recovery、`a771067d9` semantic trace capture、`8f110ed93` work record、`2eb703fa2` interaction-authority runtime surface、`0b0786ffc` strength predictor。更早本次接管还闭合了 RuntimeContract follow-up、authority/opening、delegation runtime、fission、provider attempt、managed chat、time/strength、ESM linkage gate 与 `assume(update, query)` jq canvas。
- 当前 committed `0b0786ffc` 的验证事实：Strength predictor owner project isolated Fable 绿；flat `node scripts/build.mjs` 绿（`1109` compile inputs，`165` registered JS surfaces，`772` emitted ESM modules linkage 绿）；`node scripts/check.mjs` 全绿（deadcode 0、raw-time 0、JS boundary debt 0、requirement trace `773 WHAT / 3909 tests`）。`requirements/speculative-investigation/tests/*.mjs` 中 8 个不依赖后继 Surface artifact 的 proof 绿；其余 16 个仅因尚未毕业的 `Strength/Surface.js`、`Participant/Provider/Projection/Surface.js`、`OpenCode/Codec/ProviderProjectionSurface.js` 不在 flat emit 而 `ERR_MODULE_NOT_FOUND`，不是 assertion red。等对应 locality 毕业后必须重跑整包。
- `interaction-authority` 在 `2eb703fa2` 后已真实重跑 `79 / 79` 绿；此前缺 `RuntimeSurface/CompletedTurnSurface/IntentSurface` 的 module-missing 已消失。`dispatch-protocol` 早前 focused suite 仍被后继 `DispatchSurface.js` / `Journal/Surface.js` 未 emit 阻断，等对应 locality 毕业后重跑，禁止为测试 artifact 倒置 ProjectReference。
- 白盒 FCS 已整体退役：`scripts/checks/owner-symbol-uses.fsx`、`.fable-build/owner-dependencies-fcs`、`.fable-build/authority-fcs`、`OMP_FCS_*` 复用、`composition-root-invariant.mjs` 与 FCS 派生 gate 已删除；`scripts/check.mjs` 不再保留 `owner-dep` lane。`dsl-ownership`、`authority-boundary`、`semantic-decorator-invariant` 保留为静态文本/正则门控，`owner-contracts.mjs` 与 `owner-projects.mjs` 接任 exact consumer/symbol 与 owner locality DAG 验证。
- 本 checkpoint 前所有本仓 Fable/build/check 进程已主动停止；无后台验收可被当作后续事实。恢复后从明确命令重新验证。

#### checkpoint 当时的两个 READY / RUNNING 节点（现均已闭合，仅保留历史取证）

1. `durable-events/persistence-eventstore-storetypes` — `5 fs`，仍未毕业。
   - 5/5 sibling `.fsi` 已存在并接入 owner project：`StoreTypes`、`CanonicalEventCodec`、`EventVocabulary`、`GitObjectDatabase`、`ProcessGitRawStore`。
   - 第一轮 isolated Fable 暴露 29 条 monolith-only `open`；当前 5 个 `.fs` 已删除这些 Repository/Finality/WorkRecord/Enforcer.Cycle 等假依赖，未增加 ProjectReference。
   - 第二轮 isolated Fable 已把问题压到**恰 2 个机械红点**：`StoreTypes.fs` 的 `StoreRef.canonical` implementation 是普通 `let`，`.fsi` 却声明 `[<Literal>] val canonical = "refs/wanxiang/store"`；`ProcessGitRawStore.fs` constructor 参数是 `_repoPath`，`.fsi` 写 `repoPath`。恢复时先裁这两个签名/implementation 事实，再重跑同一 owner project。
   - governance：`StoreTypes` / `CanonicalEventCodec` / `EventVocabulary` / `ProcessGitRawStore` 已有 `DurableEvents.Contract` published contract；`GitObjectDatabase.fs` 已被并发 writer加入 `compile_contract_support`。同一 writer 还已把 5 个 `.fsi` 接入 flattened emitter，并把 locality 预登记进 `compiler_boundary_localities`。这些 shared changes 在本 checkpoint 保留，但**不是完成证明**；修完 2 个 compiler red → owner project isolated Fable 绿 → EventStore focused proof + flat build + check 全绿后，才把该 provisional entry 视为正式 graduation。
2. `delegation/execution-delegation-handle-surface` — `23 fs`，仍未毕业。
   - 23/23 sibling `.fsi` 已创建，owner fsproj 已接入；`fantomas --check` 已通过后才启动 owner Fable，因此 signature formatting 当前已绿。
   - 并发 writer 已继续删除一批 compiler 推出的 monolith-only `open`（当前修改集中在 `ExecutionFactFold`、ChildRecoveryWorkflow、Fork Host Agent/AgentOwner/BusyNudge/ChildDispatch/Join/PendingRun/Pty/RunLifecycle/Runtime、Handle CompletionCodec/Controller/JoinInterruptRegistry、SyncDelegate Runtime/Store 等）；未新增因果不明 ProjectReference。
   - owner project isolated Fable 在下班冻结时被主动中止，**没有最终 verdict**；恢复时第一动作就是在当前 source cleanup 上重跑，不得宣称已编译。
   - 14 个 source 已有 `delegation.Contract`；9 个 owner-local source 需要 signed support：`SyncDelegate/Wait.fs`、`SyncDelegate/Store.fs`、`SyncDelegate/Prompt.fs`、`SyncDelegate/Workflow.fs`、`Fork/Host/BusyNudge.fs`、`Handle/CompletionCodec.fs`、`Handle/JoinDrain.fs`、`ChildRecoveryWorkflow.fs`、`Fork/Host/AgentOwner.fs`。
   - isolated compile 绿后再清 compiler 指出的 monolith-only opens / accidental generics，随后 flattened emitter + support + locality graduation + delegation focused proof + build + check。

#### 恢复施工路线

1. 只收上面两个 READY，不开第三条写线。EventStore 先解 2 个 exact compiler red；delegation 先拿完整 compiler verdict。二者 touched product paths 独立，共享 `Wanxiangshu.fsproj` / `published-contracts.json` 只当 edit mutex，谁先 compiler green 谁先登记。
2. 两个节点一闭合会立刻释放三条关键 fan-out：
   - delegation → `context-compression/context-companion-companionfactfold`（12）+ `output-distillation/process-largegatesurface`（3）；
   - EventStore storetypes → `durable-convergence/persistence-eventstore-processeventlog`（6）。
   三者互不应被人工 wave 串行。
3. `context-companion-companionfactfold` 闭合后优先释放/并行 `behavior-diagnosis/enforcer-codec`、`review-assurance/opencode-host-providerrunbinding`、`review-judgement/mission-review-reviewfactfold`，因为它们继续解锁大量 Host/provider/review/context 子图。
4. durable spine：`storetypes → processeventlog → strength-persistence-durabilityport → canonicalintegrator / eventstore-surface`。这条 spine 是 knowledge/change/finality/verification/Host 大量后继的共同 contract 前置；始终优先于纯整理。
5. dispatch/session spine：`companionfactfold + foldsurface + providerrunbinding → ingresscodec → sharedstatesurface / chatadmission transaction / pluginruntimescope → turnruntimepreparation / host fission surface`。保持 runtime → contract 方向，禁止为了让测试 surface emit 而反向加 ProjectReference。
6. 大 root 最后：`hostsignalbootstrap`、`capability-enforcement managedagentconfig`、`change-integration git-integrationgate`、`finality prompt` 等只在真实直接前置闭合后进入 READY；不人为提前。
7. `148 / 148` 与 FCS retirement 已完成：每个 production `.fs` 恰一 owner locality、每个毕业 source 有 sibling `.fsi`、flat emit 与 owner 并集一致、foreign closure 只经 contract、缺失/越权 ProjectReference 与访问未签名 implementation 的 canary 均编译失败；白盒 `owner-symbol-uses.fsx`、FCS 归一化 evidence、`.fable-build/*-fcs`、`OMP_FCS_*` 复用机制、composition-root-invariant 与 FCS 派生 gate 已整体删除；`owner-contracts.mjs` + `owner-projects.mjs` 继任门控保持 exact policy 验证。
8. 全局唯一 sink：`node scripts/check.mjs` → `node scripts/build.mjs` → `node requirements/verification-system/tests/run.mjs` → `npm run format-build-test`。最后重跑曾被未毕业 Surface 阻断的 package suites；清理 `.fable-build`/临时输出与所有 checkpoint-only 未完成状态；工作区干净后才宣称 57.15 终局。

#### checkpoint 时剩余 62 locality obligation ledger（历史快照；当前以 manifest + owner-project DAG 为准）

格式：`fs数 owner/locality ← 尚未完成的直接前置`。`-` 表示当前 READY。此表来自 checkpoint 当下可执行 owner-project DAG；后续只有真实 contract/ownership/compile/closure 因果才可改边。

- `4 action-affordance/opencode-tools-executortoolsurface ← behavior-diagnosis/enforcer-codec, context-compression/context-companion-companionfactfold, delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, managed-session-lifecycle/opencode-host-pluginruntimescope, process-execution/opencode-tools-ptytool`
- `5 action-affordance/tool-runtime-surface ← action-affordance/opencode-tools-executortoolsurface, managed-session-lifecycle/opencode-host-pluginruntimescope, process-execution/opencode-tools-ptytool`
- `12 behavior-diagnosis/enforcer-codec ← context-compression/context-companion-companionfactfold`
- `3 behavior-diagnosis/enforcer-continuation ← action-affordance/opencode-tools-executortoolsurface, behavior-diagnosis/enforcer-codec, context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, knowledge-reuse/repository-knowledge-casebook-model, managed-session-lifecycle/opencode-host-pluginruntimescope`
- `5 capability-enforcement/opencode-host-managedagentconfig ← action-affordance/opencode-tools-executortoolsurface, context-compression/context-companion-companionfactfold, delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, finality/mission-finality-prompt, intra-participant-parallelism/execution-fission-opencode-host, managed-session-lifecycle/opencode-host-pluginruntimescope, participant-horizon/execution-session-opencode-horizontool, process-execution/opencode-tools-ptytool, repository-investigation/opencode-tools-inspectortool, repository-programming/opencode-tools-filemutationtools, requirement-grounding/opencode-host-requirementgrounding-runtime, review-judgement/mission-review-opencode-judgetool, review-judgement/mission-review-reviewfactfold`
- `17 change-integration/git-integrationgate ← delegation/execution-delegation-handle-surface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport, host-boundary/opencode-host-sharedstatesurface, output-distillation/process-largegatesurface, review-assurance/mission-review-barrier-workflow, review-judgement/mission-review-reviewfactfold`
- `12 context-compression/context-companion-companionfactfold ← delegation/execution-delegation-handle-surface`
- `3 context-compression/context-companion-foldsurface ← behavior-diagnosis/enforcer-codec, context-compression/context-companion-companionfactfold, durable-events/strength-persistence-durabilityport, review-assurance/opencode-host-providerrunbinding`
- `3 context-compression/context-companion-runtimesurface ← context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, managed-session-lifecycle/opencode-host-pluginruntimescope, review-assurance/opencode-host-providerrunbinding`
- `10 context-compression/context-compression-runtime-surface ← context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, durable-events/strength-persistence-durabilityport, managed-session-lifecycle/opencode-host-pluginruntimescope, review-assurance/opencode-host-providerrunbinding`
- `18 delegation/delegation-runtime-surface ← delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, durable-convergence/persistence-eventstore-canonicalintegrator, durable-convergence/persistence-eventstore-processeventlog, durable-events/strength-persistence-durabilityport, managed-session-lifecycle/opencode-host-pluginruntimescope, participant-horizon/execution-session-opencode-horizontool, repository-investigation/opencode-tools-inspectortool`
- `23 delegation/execution-delegation-handle-surface ← -`
- `9 delegation/execution-delegation-hostturnobservedsurface ← change-integration/git-integrationgate, delegation/execution-delegation-handle-surface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-convergence/persistence-eventstore-canonicalintegrator, durable-convergence/persistence-eventstore-processeventlog, durable-events/strength-persistence-durabilityport, managed-session-lifecycle/opencode-host-pluginruntimescope, participant-horizon/execution-session-opencode-horizontool`
- `4 dispatch-protocol/composition-turn-reconcilesurface ← delegation/execution-delegation-hostturnobservedsurface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport, managed-session-lifecycle/opencode-host-pluginruntimescope`
- `9 dispatch-protocol/interaction-dispatch-opencode-ingresscodec ← context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, review-assurance/opencode-host-providerrunbinding`
- `3 durable-convergence/git-hook-sync ← change-integration/git-integrationgate, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes`
- `1 durable-convergence/persistence-eventstore-canonicalintegrator ← durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/strength-persistence-durabilityport, knowledge-reuse/repository-knowledge-casebook-model, repository-programming/opencode-tools-filemutationtools`
- `6 durable-convergence/persistence-eventstore-processeventlog ← durable-events/persistence-eventstore-storetypes`
- `9 durable-events/durable-runtime-surface ← durable-convergence/persistence-eventstore-canonicalintegrator, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport`
- `5 durable-events/persistence-eventstore-storetypes ← -`
- `4 durable-events/persistence-eventstore-surface ← durable-convergence/persistence-eventstore-canonicalintegrator, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/strength-persistence-durabilityport`
- `8 durable-events/strength-persistence-durabilityport ← context-compression/context-companion-companionfactfold, delegation/execution-delegation-handle-surface, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, review-judgement/mission-review-reviewfactfold`
- `2 execution-model-routing/opencode-host-modelroutingsurface ← host-boundary/opencode-host-fissionhostsurface`
- `20 finality/mission-finality-prompt ← delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-events/strength-persistence-durabilityport, managed-session-lifecycle/opencode-host-pluginruntimescope, output-distillation/process-largegatesurface, provider-attempt-recovery/participant-provider-attempt-fallback-ledger, review-assurance/mission-review-barrier-workflow, review-judgement/mission-review-reviewfactfold`
- `3 guidance-delivery/enforcer-guidance-tip ← delegation/execution-delegation-handle-surface, durable-events/persistence-eventstore-surface`
- `3 host-boundary/opencode-host-fissionhostsurface ← context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, intra-participant-parallelism/execution-fission-opencode-host, managed-session-lifecycle/opencode-host-pluginruntimescope, provider-attempt-recovery/composition-turn-ordinaryturnworkflow`
- `17 host-boundary/opencode-host-hostsignalbootstrap ← behavior-diagnosis/enforcer-continuation, capability-enforcement/opencode-host-managedagentconfig, context-compression/context-companion-companionfactfold, context-compression/context-companion-runtimesurface, delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, dispatch-protocol/composition-turn-reconcilesurface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-convergence/git-hook-sync, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport, execution-model-routing/opencode-host-modelroutingsurface, finality/mission-finality-prompt, guidance-delivery/enforcer-guidance-tip, host-boundary/opencode-host-fissionhostsurface, host-boundary/opencode-host-sharedstatesurface, intra-participant-parallelism/execution-fission-opencode-host, knowledge-reuse/repository-knowledge-casebook-bookkeeper, knowledge-reuse/repository-knowledge-casebook-lifecyclesurface, knowledge-reuse/repository-knowledge-casebook-model, managed-chat-execution/opencode-host-chatadmission-transaction, managed-session-lifecycle/opencode-host-pluginruntimescope, managed-session-lifecycle/opencode-host-turnruntimepreparation, obligation-ledger/mission-obligation-todo-magictodosemanticsurface, prefix-stability/context-prefix-wire, repository-programming/opencode-tools-filemutationtools, requirement-grounding/opencode-host-requirementgrounding-runtime, review-assurance/mission-review-barrier-workflow, review-assurance/opencode-host-providerrunbinding, review-judgement/mission-review-opencode-judgetool, review-judgement/mission-review-reviewfactfold, speculative-investigation/strength-opencode-settings, speculative-investigation/strength-turnevidence`
- `4 host-boundary/opencode-host-sharedstatesurface ← context-compression/context-companion-companionfactfold, delegation/execution-delegation-handle-surface, review-assurance/opencode-host-providerrunbinding, review-judgement/mission-review-reviewfactfold`
- `1 interaction-authority/interaction-repair-interactionrepair ← behavior-diagnosis/enforcer-codec, context-compression/context-companion-companionfactfold, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, provider-attempt-recovery/participant-provider-attempt-fallback-ledger`
- `3 intra-participant-parallelism/execution-fission-opencode-host ← delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, managed-session-lifecycle/opencode-host-pluginruntimescope`
- `6 knowledge-reuse/casebook-runtime-surface ← behavior-diagnosis/enforcer-continuation, durable-convergence/persistence-eventstore-processeventlog, durable-events/strength-persistence-durabilityport, knowledge-reuse/repository-knowledge-casebook-bookkeeper, knowledge-reuse/repository-knowledge-casebook-model, repository-investigation/opencode-tools-inspectortool, repository-programming/opencode-tools-filemutationtools`
- `2 knowledge-reuse/repository-knowledge-casebook-bookkeeper ← behavior-diagnosis/enforcer-continuation, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport, knowledge-reuse/repository-knowledge-casebook-model, repository-programming/opencode-tools-filemutationtools`
- `1 knowledge-reuse/repository-knowledge-casebook-lifecyclesurface ← behavior-diagnosis/enforcer-continuation, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport, knowledge-reuse/repository-knowledge-casebook-bookkeeper, knowledge-reuse/repository-knowledge-casebook-model, repository-investigation/opencode-tools-inspectortool, repository-programming/opencode-tools-filemutationtools`
- `9 knowledge-reuse/repository-knowledge-casebook-model ← durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/strength-persistence-durabilityport`
- `5 managed-chat-execution/managed-chat-runtime-surface ← durable-events/persistence-eventstore-surface, managed-chat-execution/opencode-host-chatadmission-transaction`
- `2 managed-chat-execution/opencode-host-chatadmission-transaction ← durable-events/persistence-eventstore-surface, host-boundary/opencode-host-sharedstatesurface`
- `3 managed-session-lifecycle/opencode-host-pluginruntimescope ← change-integration/git-integrationgate, context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, delegation/execution-delegation-handle-surface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, host-boundary/opencode-host-sharedstatesurface, managed-chat-execution/opencode-host-chatadmission-transaction, review-assurance/opencode-host-providerrunbinding, review-judgement/mission-review-reviewfactfold, speculative-investigation/strength-turnevidence`
- `3 managed-session-lifecycle/opencode-host-turnruntimepreparation ← delegation/execution-delegation-handle-surface, durable-events/strength-persistence-durabilityport, host-boundary/opencode-host-fissionhostsurface, host-boundary/opencode-host-sharedstatesurface, interaction-authority/interaction-repair-interactionrepair, intra-participant-parallelism/execution-fission-opencode-host, managed-session-lifecycle/opencode-host-pluginruntimescope, prefix-stability/context-prefix-wire, review-assurance/composition-turn-workflow, review-judgement/mission-review-reviewfactfold, speculative-investigation/strength-turnevidence`
- `8 obligation-ledger/mission-obligation-todo-magictodosemanticsurface ← durable-events/persistence-eventstore-surface`
- `1 output-distillation/opencode-tools-distillationsurface ← context-compression/context-companion-runtimesurface, output-distillation/process-largegatesurface, process-execution/opencode-tools-ptytool`
- `3 output-distillation/process-largegatesurface ← delegation/execution-delegation-handle-surface`
- `2 participant-horizon/execution-session-opencode-horizontool ← delegation/execution-delegation-handle-surface, managed-session-lifecycle/opencode-host-pluginruntimescope`
- `2 prefix-stability/context-prefix-wire ← managed-session-lifecycle/opencode-host-pluginruntimescope, provider-attempt-recovery/participant-provider-attempt-fallback-ledger, speculative-investigation/strength-turnevidence`
- `2 process-execution/opencode-tools-ptytool ← delegation/execution-delegation-handle-surface, managed-session-lifecycle/opencode-host-pluginruntimescope, output-distillation/process-largegatesurface`
- `1 provider-attempt-recovery/composition-turn-ordinaryturnworkflow ← context-compression/context-companion-companionfactfold, delegation/execution-delegation-hostturnobservedsurface, interaction-authority/interaction-repair-interactionrepair, provider-attempt-recovery/participant-provider-attempt-fallback-ledger`
- `3 provider-attempt-recovery/participant-provider-attempt-fallback-ledger ← context-compression/context-companion-companionfactfold, context-compression/context-companion-foldsurface, dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport`
- `1 provider-language/participant-provider-languagesurface ← host-boundary/opencode-host-hostsignalbootstrap, knowledge-reuse/repository-knowledge-casebook-model`
- `1 provider-projection/opencode-codec-providerprojectionsurface ← dispatch-protocol/interaction-dispatch-opencode-ingresscodec`
- `2 repository-investigation/opencode-tools-inspectortool ← delegation/execution-delegation-handle-surface, delegation/execution-delegation-hostturnobservedsurface, durable-convergence/persistence-eventstore-processeventlog, knowledge-reuse/repository-knowledge-casebook-bookkeeper, knowledge-reuse/repository-knowledge-casebook-model, managed-session-lifecycle/opencode-host-pluginruntimescope`
- `6 repository-programming/js-programming-runtime-surface ← durable-convergence/persistence-eventstore-processeventlog, durable-events/strength-persistence-durabilityport, repository-programming/opencode-tools-filemutationtools`
- `7 repository-programming/opencode-tools-filemutationtools ← durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/strength-persistence-durabilityport, knowledge-reuse/repository-knowledge-casebook-model`
- `5 requirement-grounding/opencode-host-requirementgrounding-runtime ← durable-events/persistence-eventstore-surface, guidance-delivery/enforcer-guidance-tip, repository-programming/opencode-tools-filemutationtools`
- `1 review-assurance/composition-turn-workflow ← context-compression/context-companion-companionfactfold, delegation/execution-delegation-handle-surface, finality/mission-finality-prompt, provider-attempt-recovery/composition-turn-ordinaryturnworkflow, review-judgement/mission-review-reviewfactfold`
- `6 review-assurance/mission-review-barrier-workflow ← dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-events/persistence-eventstore-surface, review-assurance/opencode-host-providerrunbinding, review-judgement/mission-review-reviewfactfold`
- `3 review-assurance/opencode-host-providerrunbinding ← context-compression/context-companion-companionfactfold`
- `3 review-judgement/mission-review-opencode-judgetool ← durable-events/persistence-eventstore-surface, managed-session-lifecycle/opencode-host-pluginruntimescope, review-assurance/mission-review-barrier-workflow, review-assurance/opencode-host-providerrunbinding, review-judgement/mission-review-reviewfactfold`
- `7 review-judgement/mission-review-reviewfactfold ← context-compression/context-companion-companionfactfold`
- `1 semantic-trace/context-trace-semantictracesurface ← durable-events/persistence-eventstore-surface`
- `3 speculative-investigation/strength-opencode-settings ← durable-events/persistence-eventstore-storetypes, durable-events/persistence-eventstore-surface, durable-events/strength-persistence-durabilityport, managed-session-lifecycle/opencode-host-pluginruntimescope, review-assurance/opencode-host-providerrunbinding, speculative-investigation/strength-turnevidence`
- `4 speculative-investigation/strength-turnevidence ← durable-events/strength-persistence-durabilityport`
- `2 verification-system/verification-eventstorewritersurface ← dispatch-protocol/interaction-dispatch-opencode-ingresscodec, durable-convergence/persistence-eventstore-canonicalintegrator, durable-convergence/persistence-eventstore-processeventlog, durable-events/persistence-eventstore-storetypes, durable-events/strength-persistence-durabilityport, finality/mission-finality-prompt, managed-session-lifecycle/opencode-host-pluginruntimescope`
- `1 work-record/mission-workrecord-surface ← durable-events/persistence-eventstore-surface`

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

# ReleaseClosure 声明与永久宪法

ReleaseClosure 已达成：71 个语义迁移节点全部 `DONE`，coverage backlog 为 0，`npm run format-build-test` 全绿；临时 migration DAG ledger 与 `.omo/plans/agents-debt-zero-release-closure.md` 已删除，原 ledger 的施工事实已固化为永久 architecture gate。以下三章从已毕业的临时迁移提案中提取并作为仓库永久宪法保留。

## DAG 施工调度宪法

第五十七章：DAG 驱动全仓重构——ready 即执行，无全局 wave barrier

wave 的根本缺陷不是“阶段”这个词，而是全局 barrier：A 与 B 无依赖，只因同属不同 wave 就被强制串行；某个大区域剩一个慢节点，会饿死其它区域全部可执行工作。

从本章开始，施工顺序只来自真实依赖图。目录只负责 coverage，不负责 scheduling。只有 production cutover 算进度。

定义 migration DAG：

G = (V, E)

V = semantic cutover nodes
E = 必须先成立的真实 prerequisite

任意时刻：

READY = { v ∈ V | deps(v) 全 DONE }

调度器持续从 READY 取节点执行。不存在“等同组全部完成再进入下一组”。

---

57.1 DAG 节点——最小可独立交付 semantic slice

一个节点不是目录，不是 package，不是“做完 Interaction”。

一个节点必须能独立回答：
owner 是谁；迁哪条 law/knowledge/authority；发布什么 contract；迁哪些 callers；删什么旧路径；用什么 proof 闭合。

节点固定字段：

id
primary owner
intent / WHAT proposition
current files
target files/modules
classification = KEEP / MOVE / SPLIT / DELETE / COMPOSITION-ROOT / ADAPTER
publishes
consumes
depends_on
production callers to migrate
proofs
architecture gates
touched paths
coverage tags
state = PENDING / READY / RUNNING / DONE
result = CUTOVER / DELETED / PROVEN-KEEP

`coverage tags` 只用于证明全仓没漏，如 OpenCode / Interaction / Mission / Execution / Context / Persistence / Platform；禁止拿 tag 生成先后顺序。

`depends_on` 必须能用一句因果命题解释。解释不了的边就删。

KEEP 不是默认值。PROVEN-KEEP 必须证明：单一 owner、边界凝聚、foreign callers 只经 published contract、无重复知识。

---

57.2 节点内部固定闭环

每个 cutover node 内部仍严格串行：

1. 读对应 WHY / WHAT / HOW + 当前 proof。
2. 先补会因旧实现而失败、因目标边界而通过的最低层 proof。
3. 把 decision / vocabulary / capability / effect ownership 搬到唯一 owner。
4. 建最终形态的最窄 published contract；foreign owner 只见 outcome / evidence / witness / capability / port。
5. 一次迁完该节点声明的全部 production callers。
6. 同节点删除旧 implementation、旧 alias、旧 adapter、旧 compatibility path；禁止临时 facade。
7. 跑 owner proof + affected architecture gate + `node scripts/check.mjs` + `node scripts/build.mjs`。
8. 绿后提交，state=DONE；立即释放所有后继节点。

节点不能用 inventory / report / baseline / gate-only commit 伪装完成。

---

57.3 只有四类合法依赖边

Contract edge：

Provider publishes C
→ Consumer may cut over to C

Consumer 只依赖 C 已成为最终 contract，不依赖 Provider 整个目录“全部重构完”。

Ownership edge：

duplicated knowledge K
→ canonical owner K established
→ foreign copies may be deleted

先确定唯一知识 owner，再删消费者复制品。

Physical/compile edge：

new module/type/path must exist
→ dependent compile edge may migrate

只表达编译上不可逆的前置，不表达“习惯上先做”。

Closure edge：

all nodes in exact closure scope DONE
→ local hard-cut / rotation / requirement-sync / release node may run

closure scope 必须最小。一个 owner 的目录 rotation 只等该 owner 与其直接 consumers 稳定，不等全仓。

禁止边：

same top-level directory
same old wave
“看起来应该先做”
“先把基础设施全部做完”
“等另一个团队/区域整体结束”

没有 contract/ownership/compile/closure 因果，就没有 edge。

---

57.4 并发铁律——最大化 ready frontier，不制造假依赖

∀ READY 节点，只要 touched paths 不冲突，就应并行。

同文件重叠编辑不是 semantic dependency，只是短期 edit mutex：一个节点持锁，另一个保持 READY；锁释放后 rebase/复查上下文再执行。禁止为了避免 merge conflict 给整个 owner/目录加永久 DAG 边。

多节点共享同一 public contract 时，先抽一个 canonical owner node；消费者形成 fan-out：

OwnerContract
 ├→ ConsumerA
 ├→ ConsumerB
 ├→ ConsumerC
 └→ ConsumerD

禁止写成：

OwnerContract → A → B → C → D

除非 B 真的依赖 A 的产物。

多前置汇合使用 join：

ReviewWitness ─┐
CandidatePort ─┼→ FinalityCutover
DrainEvidence ─┤
Authority ─────┘

Finality 不应等待 Todo、Repository、Sphinx 等无关节点。

调度优先级：
1. 解锁后继最多的 contract/owner node。
2. 位于当前 critical path 的节点。
3. 小而独立、可快速释放 fan-out 的节点。
4. 最后才做不解锁生产 cutover 的纯整理。

---

57.5 DAG 必须保持无环

migration graph 出现 cycle 不是“互相等一下”，而是架构债被显式暴露。

A → B → C → A 时只能三选一：

1. 三者其实同一 semantic sovereignty → 合并 owner/node locality。
2. 真实双向物理协作 → 提取窄 published bridge，把语义依赖变单向。
3. 某条边是假依赖 → 删除。

禁止用 baseline、allowlist、temporary facade、双写、双 runtime 打断 cycle。

新节点/新边加入 ledger 时必须立即检查 acyclic；发现 cycle 先裁决图，再写生产代码。

---

57.6 Ledger 不再有“先做完 inventory 才能施工”的总 barrier

migration ledger 逐 semantic slice 增量建立。一个 slice 一旦完成 owner 裁决、依赖识别、proof freeze，就可进入 READY；不必等 src/Wanxiangshu 100% inventory 完成。

但全仓 coverage 是 release invariant：最终每个 production `.fs` 必须被至少一个 node 覆盖，且最终归于恰一个 primary owner。

semantic-owners.json 只能作为候选 owner inventory。文件被填 owner ≠ ownership 已正确；必须由源码职责、WHAT owner、foreign imports 三者共同裁决。

ownership-adjudication.json 多 owner 列表只算 governance/APPLIES-TO evidence，不得冒充 primary ownership。

热点 proof freeze 至少覆盖：
PluginTransforms exact trace
Assistance abort → fresh idle → successor trace
ModelRouting capacity trace
CanonicalIntegrator boot/live equivalence
shutdown/drain
recovery re-entry

---

57.7 Coverage matrix——覆盖全部 production，但绝不代表执行顺序

Coverage A — OpenCode composition shell

OpenCode/Plugin/PluginTransforms.fs
OpenCode/Host/HostSignalBootstrap.fs
OpenCode/Tools/ToolRegistry.fs
OpenCode/Host/PairProgrammingThoughtTransform.fs
OpenCode/Host/ModelRouting.fs
OpenCode/Host/ModelCapacity.fs
PluginBoot / PluginHostWiring / PluginRecoveryWiring / PluginSessionWiring
OpenCode/Host/Tools/Signals/Codec collaborators

Coverage B — Interaction

Interaction/Dispatch/OpenCode/AssistanceHost.fs
Interaction/Dispatch/OpenCode/NeedHelpSensor.fs
Interaction/Dispatch/Send.fs
Interaction/Dispatch/Dispatcher.fs
Interaction/Dispatch/Recovery.fs
Interaction/Dispatch/PhysicalAcceptance.fs
Interaction/Authority/*
Interaction/Repair/*

Coverage C — Mission

Mission/Manager/*
Mission/Finality/*
Mission/Review/*
Mission/Obligation/*
Mission/WorkRecord/*

Coverage D — Execution + Composition

Execution/Session/*
Execution/Delegation/*
Execution/Fission/*
Composition/Turn/*

Coverage E — Context + Strength + Enforcer + Participant

Context/*
Strength/*
Enforcer/*
Participant/*

Coverage F — Persistence + Change + Git

Persistence/*
Change/*
Git/*

Coverage G — Platform + remaining production

Process/*
Repository/*
Sphinx/*
Foundation/*
Resources/*
Host/*
Requirement/Grounding/*
Verification/*
以及所有未被 A–F 覆盖的 production file。

任何 Coverage 都可同时产生 READY 节点。禁止 A→B→C→D→E→F→G 的人工串行。

---

57.8 推荐初始 DAG——按真实 contract fan-out，不按目录排队

以下只是 seed topology，最终 edge 必须以实际源码/import/WHAT 为证据，不得把示例本身变新教条。

Context.TraceContract ───────────────┐
Context.CompanionContract ──────────┤
Context.PrefixContract ─────────────┤
Strength.Contract ──────────────────┤
Enforcer.ContinuationContract ──────┤
PairGuidance.Contract ──────────────┤
RequirementGrounding.Contract ──────┼→ OpenCode.PluginTransformsCutover
Participant.ProviderProjection ─────┘

ModelRouting.Contract ──────────────┐
Session.RecoveryContract ───────────┤
Fission.Contract ───────────────────┤
Finality.Contract ──────────────────┤
Assistance.Contract ────────────────┼→ OpenCode.HostSignalBootstrapCutover
HostSignalPhysicalContract ─────────┘

ToolOwner*.ToolSpec ────────────────→ OpenCode.ToolRegistryCutover

Interaction.Authority ──────────────┐
NeedHelp.Observation ───────────────┤
Session.Quiescence ─────────────────┤
Consultation.Contract ──────────────┼→ Assistance.WorkflowCutover
AdviceDelivery.Contract ────────────┘

Review.Witness ─────────────────────┐
Git.CandidateContract ──────────────┤
HandleDrain.Evidence ───────────────┤
Mission.Authority ──────────────────┼→ Finality.Cutover
Durability.Contract ────────────────┘

Session.Lifecycle ──────────────────┐
Delegation.ChildOutcome ────────────┤
Fission.Lifecycle ──────────────────┼→ Composition.TurnCutover
Participant.Binding ────────────────┤
Context.Contracts ──────────────────┤
Strength.Contract ──────────────────┘

CanonicalIntegrator 已是守江山事实，因此 feature durable cleanup 不需要等待“Persistence 整包重构”；只要依赖的 canonical EventStore contract 已存在，各 feature 可独立删除 history reader / duplicate replay / private backend。

---

57.9 关键节点的目标约束

PluginTransformsCutover：
root 只保留 typed mode + 固定顺序 + owner-published calls + Host shape；禁止 ITransformMiddleware / dynamic pipeline / service locator / giant capability bag。XTrace / Companion / Enforcer / Strength / Pair guidance / Requirement grounding 的 semantic decision 必须回 owner。ordered transform proof 必须锁死。

HostSignalBootstrapCutover：
只保留 construct / subscribe / route typed signal / drain / dispose。model/recovery/fission/finality/assistance policy 全部由 owner contract 提供。

ToolRegistryCutover：
tool owner 发布 ToolSpec + admission；registry 只 aggregate + Host projection，不拥有业务 availability。

Assistance.WorkflowCutover：
保持 AssistanceAbortClaim exact identity、abort 不消费、fresh idle 后 consume once、late child 不复活 owner。workflow 输入只含 assistance 自己的 evidence / witness / capability / ports；不得 import Git/Review/Todo/Strength/Fork internals。

Mission nodes：
Review 生产 review witness；Finality 只消费 typed facts/projection/witness；Todo canonical → Host compatibility 单向；Manager 只 orchestration；WorkRecord 只 materialization/opening。FinalitySurface / MagicTodoMembrane / DedicatedTodoRuntime 按 semantic slice 裁决，不按 LOC 拆。

Execution nodes：
Session 拥有 association/attachment/wait/recovery；Delegation 拥有 child run/handle/join/recovery durable facts；Fission 拥有 admission/lane/takeover/delivery claim；Turn 只 orchestration。父流程只消费 child outcome/evidence/capability，不读 Stage/Step/cursor/registry presence。

Context/Strength/Enforcer/Participant nodes：
Trace/Prefix/Companion、Budget/Prediction/Replica/Persistence、Rulebook/Continuation/Guidance、Persona/Provider Language/Attempt/Projection 分别归主。大 Surface 逐语义 operation 切，不让 Surface 因“方便”拥有多个 owner law。

Persistence/Change/Git nodes：
CanonicalIntegrator 保持唯一 full-history interpreter；feature 只提供 single-event oracle。Journal 只管 append/flush/close/poison 物理 durability。Change 经 ports 使用 Git/Review/Session；Git 只管 repository physical operations/integration/worktree。禁止双写、影子 store、feature 私有 replay、snapshot/state cell 冒充 truth。

Platform nodes：
Process 只发布 PTY/process/deadline/gate/spool physical primitives；Repository 分 investigation/knowledge/programming；Sphinx 分 epistemic domain 与 MCP/codec/session adapter；Foundation 只留真正通用 primitive；Resources 只 materialize resources；Host 顶层只 host contract/projection；Requirement/Grounding 只归 grounding owner；Verification 只 proof primitive/temporal harness。

---

57.10 Hard cut 不再是全仓末尾阶段，而是每个子图的局部 closure

每个 owner/cutover node DONE 后，立刻创建或释放对应 hard-cut node：

OwnerCutover
  → OwnerDependencyGate
  → OwnerTreeRotation
  → OwnerRequirementSync

但三者是否完全串行取决于真实依赖。若 requirement 文档同步只依赖 owner contract 稳定，可与物理目录 rotation 并行；若 rotation 改测试路径，则 requirement proof-link 更新依赖 rotation。

OwnerDependencyGate 必须验证：
每个 production file 恰一 primary owner。
cross-owner dependency 只落 published contract。
composition root 只 wiring/order/lifetime，不 match foreign internals。
foreign owner 不消费 Stage/Step/cursor/registry presence。
unjustified owner cycle = 0。
migration baseline / allowlist 对该 closure scope = 0。

不允许“等所有 production cutover 完成后再统一发现依赖图错误”。边界一旦迁完，就立刻 harden。

---

57.11 Physical Tree Rotation 改成 owner-local rotation

不再等“全仓 owner graph 稳定”。某 owner 子图满足：

canonical owner established
direct consumers cut over
old references = 0
local dependency gate green

即可搬目录。

rotation node：
1. 按 primary owner + runtime locality MOVE/SPLIT。
2. 更新 Wanxiangshu.fsproj compile order 与 namespace/module references。
3. 删除旧目录、空壳 Surface、过渡 facade、alias、namespace wrapper。
4. 合并只因历史原因分离的同 owner 微模块。
5. grep 旧 path / namespace / symbol = 0 production references。

不同 owner 的 rotation touched paths 不冲突时可并行。

---

57.12 Requirement / proof graph 同步也改成 owner-local closure

不再“最后统一改宪法”。owner contract 稳定后即可同步它自己的 WHY/WHAT/HOW/proof：

OwnerCutover
  ├→ RequirementSync
  └→ ProofGraphSync

structured-workflow 的 universal meta law 是唯一需要跨 owner 汇总的部分；owner-specific law 一旦所有权确定就立即迁回真实 package。

每个 RequirementSync node 必须：
1. 分类 Universal Structural Law / Owner-specific Law / Historical-Garbage。
2. APPLIES-TO = governance scope ≠ production ownership。
3. 每条保留 WHAT 有最低足够 proof。
4. 删除 stale path/module/behavior normative prose。
5. 删除该 owner 已无用途的 baseline/census/temporary allowlist。
6. 故意破坏关键边界，证明 gate/test 会红；恢复后提交。

---

57.13 节点拆分与动态发现

DAG 允许施工中发现新节点，但不允许偷偷扩大 RUNNING 节点到大泥球。

若发现一个节点包含两个可独立 owner/law：停止继续扩张，拆成 A/B 两节点，补真实 edge，再继续。

若发现新 dependency：
未开始节点 → 直接加边并重新计算 READY。
RUNNING 节点 → 若前置事实尚未满足，停止该节点，在安全边界拆出 prerequisite；禁止靠临时 adapter 绕过。
DONE 节点 → 不可改写历史语义；创建 follow-up node 修正，并把暴露出的 invariant 加 hard gate。

DAG ledger 是施工事实，不是永恒架构文档。最终 release 后删除；architecture truth 回到代码、requirements、semantic owner manifest 与机械 gate。

---

57.14 全仓完成条件——只有一个 global sink

全局唯一必须等待所有节点的地方是 ReleaseClosure。

ReleaseClosure depends on：
全部 production coverage nodes DONE。
全部 owner dependency closure DONE。
全部需要的 tree rotation DONE。
全部 requirement/proof sync DONE。
migration baseline / temporary allowlist = 0。
old namespace / old path / deleted symbol production refs = 0。
本次迁移 TODO/FIXME/compatibility shim = 0。
bounded compatibility 只有 named external/durable creditor + evidence + writer policy + exit condition。
Chapter 92 Definition of Done 全成立。

最终验证：
1. node scripts/check.mjs
2. node scripts/build.mjs
3. node requirements/verification-system/tests/run.mjs
4. npm run format-build-test

`npm run format-build-test` 是 release sink：format + checks + build + verification integration + package integration + warmup + e2e + npm pack --dry-run 整条通过。

最后人工审查：
git diff 无临时调试/迁移脚手架。
git status 只含本次应交付变化。
删除临时 migration DAG ledger。

最终完成标准只有一句：

&gt; 所有 semantic cutover 节点沿真实依赖 DAG 闭合；独立节点并发执行，局部子图随完成随 harden/rotate/sync；最终 ReleaseClosure 全梯度通过。全仓不存在人为 wave barrier，也不存在下一轮“真正的重构还没开始”。


---

57.15 终局：多工程化，编译输入接管 owner 边，拆掉白盒 check

ReleaseClosure 不是架构终点。本节是路线图的最终章：single-project 只是把全部越权成本集中在 FCS 白盒扫描（`owner-dependencies` / `fsharp-control-pyramid`）上运行的过渡态；终局是把这层扫描管辖的命题逐步转译进编译边界，让语言接管，然后拆除扫描本身。**57.15 已完成**：148 个 owner locality 已全部毕业，白盒 FCS 扫描链路（`owner-symbol-uses.fsx`、`OMP_FCS_*`、`.fable-build/*-fcs`、`composition-root-invariant.mjs`）已整体删除；`owner-contracts.mjs` 与 `owner-projects.mjs` 接任 contract registry 与 owner locality DAG 验证。

终局形态：

一个 owner（或 57.5 裁决出的 contract/runtime/adapter owner locality）对应一个 fsproj；跨 owner 消费只经 ProjectReference + public contract locality。这里必须服从本仓 Fable-only 的真实编译语义：Fable 5 会递归把 ProjectReference closure 的源码合并进顶层 project check，因此 ProjectReference 本身**不是 .NET assembly visibility firewall**，`internal`、top-level `module private` 与 `DisableTransitiveProjectReferences` 都不会阻断这条 source closure。真正可用的终局边界是 compile-input boundary + F# signature：foreign owner 只能引用 dependency-inverted 的 contract/adapter locality，且该 locality 的 transitive ProjectReference closure 不得包含 provider runtime/private locality；contract implementation 必须由 sibling `.fsi` 封口，未签名 implementation symbol 即使被 source-merge 也编译红。signature-only project 不会形成可消费模块，因此 contract 必须有真实自包含 implementation；正确依赖方向始终是 runtime → contract。

顺序不可颠倒：

1. 工程引用图必须是 DAG。SCC 数字只能来自当次 executable `owner-contracts.mjs` / `owner-projects.mjs` 事实，禁止把旧文档中的成员数当现状。live SCC 必须按 57.5 三选一收敛：合并同一 sovereignty locality、提取窄 contract/runtime/adapter locality 单向化、或删假边。允许一个 semantic owner 因真实编译方向被裁成少数有名称的 localities；禁止 `phase-1/2/3` 式编号切片冒充 architecture。
2. 按 57.11 owner-local rotation 逐个立 owner-boundary 工程：一个 locality 满足 canonical owner established、direct consumers cut over、old references = 0，即建立独立 fsproj 并接入 owner ProjectReference DAG。Fable emit 仍由 `Wanxiangshu.fsproj` 一次 flatten 全部 production source；这是 Fable-only 的物理发射优化，不是 owner graph，compile set 必须与 owner projects 并集精确相等，且 owner project 禁止引用 emit project。graduated owner 的每个 production `.fs` 必须有 sibling `.fsi`，并同时进入 owner project 与 flattened emit；owner cutover 时由 `node scripts/compile-owner.mjs <owner-fsproj>` 从 ProjectReference DAG 机械投影精确 transitive closure，再按 `Wanxiangshu.fsproj` 规范顺序生成零 ProjectReference 隔离工程真实跑 Fable。ProjectReference 图仍是编译输入事实源，投影只消除 Fable 重复解析百级 MSBuild 图的偶然税；release 常规路径由 flat build 验 signature↔implementation + 静态 project gate 守 topology。
3. foreign ProjectReference 只允许指向 provider 的 published contract / physical-port locality，或 exact composition-root wiring 所需的 narrow adapter locality；provider runtime locality 不得出现在普通 foreign dependency 的 direct **或 transitive** closure。contract/adapter locality 自身不得 ProjectReference provider runtime/private locality；若 contract 行为需要 runtime，必须反转为 runtime 实现 contract 声明的 capability/port。每拆一个 locality，`owner-contracts.mjs` 与 `owner-projects.mjs` 的管辖面同步收缩；ProjectReference 缺失/多余、compile coverage、project SCC 与 foreign runtime closure 由继任 hard gate + locality compiler check 接管。exact symbol/consumer 承诺仍由 `published-contracts.json` 与 `owner-contracts.mjs` 静态门控守护，直到其 Surface 已窄到可由非白盒机制完整判定。

拆 check 的准确语义：

拆除对象是白盒 FCS 扫描的独有职责——全仓名字解析证明"0 unauthorized cross-owner source import"。该命题已被**signed owner locality + ProjectReference compile-input DAG**完整接管（每个 production file 恰一 owner locality、graduated owner 每个 source 有 `.fsi`、emit compile set 与 locality 并集一致、foreign owner 只拿 contract/adapter locality、漏写 ProjectReference/越过 runtime boundary/访问未签名 sibling implementation 均编译失败），因此 `owner-symbol-uses.fsx` 反射扫描器、FCS 归一化 evidence 管线、`OMP_FCS_*` 复用机制已整体删除。禁止用 flattened emit Fable 的成功代替 topology proof，也禁止用 .NET build 绕过本仓 Fable-only 约束。`published-contracts.json` 登记与其余 gate（spec/deadcode/requirement-trace 等）不随多工程消失：per-consumer exact symbol 承诺由 `owner-contracts.mjs` 静态验证，直到其继任非白盒 gate 完整接管。

毕业标准沿用三件套：production cutover（每个 production source 恰一 owner-boundary locality，flattened emit 与其并集一致）+ executable proof（public contract 可编译、缺失/越权 ProjectReference 的 consumer 独立 Fable check 必须红；同时有 canary 固定 Fable source-merge 可见性事实）+ hard gate（compile coverage、locality 单 owner、foreign contract-only transitive closure、工程 DAG 固化进 `check.mjs` 继任者）。**三者已齐全：57.15 已完成，白盒 FCS 扫描已删除，语言/编译器与非白盒 `owner-contracts.mjs` / `owner-projects.mjs` 已接任。**



## 架构宪法

第九十一章：最终 Architecture Constitution

如果要我把整个重构压缩成十条不可破坏的宪法，我会写：

I.   Every accepted semantic proposition has exactly one owner.

II.  Every production capability has exactly one authority owner.

III. Witness proves; Capability authorizes.

IV.  Business control flow is host-language CE, never a second runtime.

V.   Cross-workflow seams expose outcomes/evidence/capabilities,
     never execution position.

VI.  Higher-order composition is local and owner-named.
     No anonymous semantic middleware.

VII. Durable history stores facts.
    Process-local gates store temporary authority.
    Never confuse the two.

VIII. Feature owners integrate one event.
      Canonical Integrator alone interprets history.

IX.  Physical reality enters only through explicit ports/adapters
     and leaves as typed observation/receipt.

X.   Every mechanically decidable architectural invariant
     must eventually become a failing gate.

这十条比“DDD / Clean Architecture / Hexagonal / Onion”任何标签都更适合你这个系统。



## 教科书级 Definition of Done

第九十二章：最终 Definition of Done——什么叫“教科书级”

不是：

所有文件 &lt; 300 行
所有函数 &lt; 20 行
所有 package 零循环
所有 class 消失
所有 mutable 消失

而是下面这些全部成立。

Semantic Ownership

0 unowned normative propositions
0 multi-owner normative propositions
0 unowned production semantic modules

Dependency Integrity

0 unauthorized cross-owner internal imports
0 hidden semantic dependency edges

CE

0 business workflow runtimes
0 stored program-counter state
0 cross-module Stage/NextAction seams

Capability

all high-risk effect authority typed
all one-shot process capabilities owner-validated
no process capability persisted as durable fact

Witness

subject/version identity explicit where required
no stale witness directly authorizes current effect

Decorator

0 anonymous semantic middleware
every trace-altering wrapper has owner + law + proof

Composition

wide roots are explicit
roots contain wiring/order, not foreign policy

Durability

one event substrate
one canonical integrator
zero feature-owned history loops

Verification

every WHAT law has owned proof
every proof is at the lowest adequate ladder level
every critical gate is demonstrably red

Crash

every acknowledged durable effect survives restart
every ambiguous external cut has an explicit reconciliation law
no recovery path guesses hidden workflow position

达到这个状态，我会很愿意称它：

教科书级 Algebraic Capability Architecture

而且不是“代码看起来函数式”。

是整个仓库具有可证明的语义拓扑。
