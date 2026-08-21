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

本节只收录已经同时落到 production + executable proof + architecture gate 的事实。迁移提案中的某项达到这个标准后，删掉对应长篇施工说明，只把不可退化的不变量压进本节；未达到三件套的内容继续留在 Active Migration Proposal。

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
- 关键 composition trace 已进入静态守城：PluginTransforms 顺序、Assistance Host capability seam、composition-root wiring、recovery/join、cross-callback PC 都有独立 gate；禁止把显式乐谱重新包装成动态 middleware/service-locator。
- production semantic ownership 已全覆盖：每个 production `.fs` 必须出现在 `scripts/checks/semantic-owners.json` 且恰有一个 primary owner；新增/移动文件必须同步 owner，不得靠目录猜 owner。
- 当前 F# control-pyramid、dead private binding、JS semantic-boundary debt 均为 0；这是零基线，不是未来可重新积累的 allowance。
- durable store 已统一到 canonical EventStore spine；feature-owned durable backend/private ref、dual-write migrator、业务层 Git bypass 由 `unified-store-gate` 拒绝。
- provider/context recovery 已切到 failure-driven + durable-event-driven + time-independent；任何重新引入 timeout/polling/process-local recovery proof 的改动都是回归。

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

# Active Migration Proposal — 完成前保留

以下内容是仍在执行中的架构迁移提案，不是已经完成的 repository law。实施时必须继续受上方正式协议与 `requirements/` 约束。

毕业规则：production cutover + executable proof + hard gate 三者齐全才算完成。完成项立即从本提案缩掉，压成上方“守江山”一条；inventory / baseline / report-only / 文档宣称均不算毕业。当前 migration DAG 仍有未闭合节点，所以第五十七章的 dependency-driven production cutover 路线继续保留。

原提案第一章 / Phase 0 的“关键 trace 冻结”已毕业到守江山，不再保留施工长文。以下继续沿用原章节编号，未完成部分保持原意。

    
    第二章：建立全仓统一的语义词典——从此不再允许 Token 万金油
    
    这是整个重构的语言基础。
    
    从这一步开始，任何新类型必须属于下面类别之一。
    
    
    ---
    
    2.1 Evidence：观察到了什么
    
    Evidence 是输入材料，不意味着已经成立某个领域命题。
    
    例如：
    
    type ReviewObservation =
        | ReviewerReportedPerfect of ReviewerId * TreeHash
        | ReviewerReportedRevision of ReviewerId * Report
    
    Evidence 可以：
    
    不完整；
    
    冲突；
    
    过时；
    
    被拒绝；
    
    需要多个来源合并。
    
    
    它回答：
    
    &gt; “我们观察到了什么？”
    
    
    
    
    ---
    
    2.2 Decision：根据 Evidence 应该判断什么
    
    Decision 是纯函数输出。
    
    Evidence -&gt; Decision
    
    例如：
    
    type ReviewDecision =
        | NeedMoreEvidence
        | RevisionRequired of Report
        | CandidateConfirmed of ConfirmedReview
    
    它回答：
    
    &gt; “根据这些事实，我们应该如何分类？”
    
    
    
    Decision 本身不授权 effect。
    
    
    ---
    
    2.3 Witness：命题已经成立
    
    Witness 是已经通过 owner law 建立起来的事实见证。
    
    type ConfirmedReviewWitness = private ConfirmedReviewWitness of ...
    
    它回答：
    
    &gt; “P 已被证明。”
    
    
    
    例如：
    
    P =
        两位合法 reviewer
        对同一 tree
        在同一 review cohort
        给出满足 finality law 的 PERFECT
    
    Witness 应当：
    
    stable
    auditable
    replayable when the proposition itself is durable
    free of physical resource ownership
    
    但不要再定义一个绝对规则：
    
    &gt; “所有 Witness 必须序列化。”
    
    
    
    更准确的是：
    
    &gt; 如果它所证明的命题需要跨 crash 存在，那么建立该 Witness 的事实必须可由 durable evidence 重新推导。
    
    
    
    有些 Witness 只是当前函数调用内的纯证明值，没有必要单独写 EventStore。
    
    因此不要为了“Witness 化”疯狂增加：
    
    FooWitnessCreated
    BarWitnessCreated
    BazWitnessCreated
    
    如果：
    
    HandleCompleted
    +
    ProviderRun terminal evidence
    +
    AuthorityRoot
    
    已经足够纯推导出：
    
    ConsultationCompletedWitness
    
    就直接 project。
    
    不要重复持久化派生真理。
    
    
    ---
    
    2.4 Capability：现在有资格做什么
    
    Capability 不证明“世界是真的什么”。
    
    它证明：
    
    &gt; “当前 owner 允许你执行某个 effect。”
    
    
    
    典型：
    
    QuiescencePermit
    CapacityLease
    IntegrationPermit
    BlessCapability
    PublishCapability
    
    尤其 process-bound effect capability 应具备：
    
    opaque identity
    owner
    scope
    epoch / serial / generation
    resource binding
    validation rule
    consume/revoke semantics
    
    于是以后 code review 中最简单的一问就是：
    
    &gt; 这是 Witness 还是 Capability？
    
    
    
    如果回答：
    
    &gt; “嗯……是一个 token……”
    
    
    
    直接 REVISE。
    
    
    ---
    
    第三章：建立 Proof-Carrying Capability 的标准形状
    
    你们已经有了非常好的教科书范例。
    
    AssistanceAbortClaim 当前就是 private typed capability，携带精确 SessionId + ProviderRunIdentity，并被注释为
        Host-boundary one-shot capability。
    
    QuiescencePermit 更进一步：它不是 registry presence，而是带 serial 的 process-local permit，由 owner gate 在消费时检查
        freshness。
    
    所以不要再发明一套新 framework。
    
    把这个模式抽象成审查协议，而不是抽象成基类。
    
    推荐约定：
    
    type CapabilityId = private CapabilityId of string
    
    type SomeCapability =
        private
            {
                Id: CapabilityId
                Scope: ScopeId
                Epoch: Epoch
            }
    
    但真正的一次性不是：
    
    type SomeCapability = ...
    
    保证的。
    
    而是：
    
    gate.TryConsume capability
    
    保证的。
    
    
    ---
    
    3.1 Owner Gate 的黄金结构
    
    推荐统一成：
    
    type ConsumeFailure =
        | AlreadyConsumed
        | Superseded
        | WrongScope
        | StaleEpoch
        | OwnerDisposed
    
    type CapabilityGate() =
    
        member _.TryConsume(capability)
            : Result&lt;ConsumedCapability, ConsumeFailure&gt; =
            ...
    
    这里我更推荐：
    
    Result&lt;_, ConsumeFailure&gt;
    
    而不是 bool。
    
    bool 适合极小的物理 predicate。
    
    一旦 failure meaning 对调试、reconciliation、law 有价值，就应该显式分型。
    
    注意：
    
    ConsumedCapability 不应该意味着“现在真的执行完 effect 了”。
    
    它只意味着：
    
    &gt; admission 已原子完成。
    
    
    
    正确链条是：
    
    Capability
    → Consume Gate
    → Effect Admission
    → physical operation
    → physical receipt/evidence
    → durable fact
    
    
    ---
    
    3.2 Capability 消费失败绝大多数不是 exception
    
    例如 stale permit：
    
    新 provider attempt 已开始
    旧 quiescence permit 到达
    
    这是系统允许的 race resolution。
    
    应该是：
    
    Error Superseded
    
    而不是异常。
    
    异常用于：
    
    gate internal corruption
    impossible invariant
    disposed resource illegally accessed
    
    
    ---
    
    3.3 Capability 不能“半消费回滚”
    
    这是非常关键的设计纪律。
    
    不要建立这样的模型：
    
    consume capability
    → effect 做了一半
    → 出错
    → restore capability
    
    这会立即陷入分布式事务幻想。
    
    应该明确分两层：
    
    Admission Capability
            ↓ consume
    Effect Attempt
            ↓
    Succeeded / Failed / Unknown
    
    如果物理 effect 已经可能发生，就进入：
    
    receipt
    reconciliation
    idempotency
    dedupe
    compensation
    
    而不是把 capability 复活。
    
    
    ---
    
    3.4 进程重启后的能力失效
    
    process-local capability 最简单可靠的规则：
    
    ProcessEpoch = random fresh identity at boot
    
    Capability carries ProcessEpoch
    
    gate only validates current epoch
    
    或者 capability 只存在于当前 gate 的私有 identity space 中。
    
    这样 restart 后：
    
    旧 Capability
    ≠
    新 Gate 可接受的 Capability
    
    而不需要显式扫描“把全部 permit 标失效”。
    
    
    ---
    
    3.5 已毕业 → 守江山：Fable/JS semantic boundary

    JS 测试只经注册 Surface，opaque capability 只 obtain/pass-back/dispose、不检查 F# representation；`test-boundary` + `js-boundary-gate` 当前零债。以后只守，不再迁移。
    
    
    ---
    
    第四章：正式把 Decorator 理论改成“观察等价 + Trace Relation”
    
    这一章非常重要。
    
    以后不要说：
    
    &gt; Transparent Decorator 是 f(x)=x。
    
    
    
    这是错误的。
    
    定义一个 Port：
    
    P : Req -&gt; Effect Resp
    
    其执行产生 trace：
    
    Trace(P, req)
    
    再定义业务 owner 认可的观察投影：
    
    π_business : Trace -&gt; BusinessObservation
    
    那么 transparent decorator D 应满足：
    
    π_business(Trace(D(P), x))
    =
    π_business(Trace(P, x))
    
    注意：
    
    π_diagnostic(...)
    
    可以不同。
    
    所以 logging / metrics / causal wait observation 才有资格叫“业务透明”。
    
    
    ---
    
    4.1 Transparent Decorator
    
    例：
    
    let withCausalObservation observer port =
        fun request -&gt;
            task {
                use lease = observer.Enter(...)
                try
                    let! response = port request
                    observer.Resolve lease
                    return response
                with ex -&gt;
                    observer.Fail(lease, ex)
                    return raise ex
            }
    
    它可以增加：
    
    diagnostic trace
    timing trace
    causal wait trace
    
    但不得改变 owner 定义的：
    
    business facts
    effect multiplicity
    admission authority
    business outcome
    
    
    ---
    
    4.2 Semantic Decorator
    
    retry / fallback / deadline / dedupe 等不能套透明定义。
    
    应该有一个明确 law：
    
    Trace(D(P))
       R_policy
    Trace(P)
    
    这里 R_policy 是 owner 定义的关系。
    
    例如 fallback 可能规定：
    
    confirmed failure A
    → at most one B attempt
    → B may only begin after A terminal evidence
    → same logical authority
    → retry budget monotonic
    
    这不是“透明”。
    
    这是新语义。
    
    仓库当前 STRUCTURED-WORKFLOW-013 已经明确：
    
    transparent decorator 可叠加；
    
    retry/fallback/recovery/dedupe/claim/deadline 属于 trace-changing semantic decorator；
    
    必须拥有正式 law 或 CE 中明确语义名字；
    
    禁止匿名 MiddlewarePipeline / DecoratorBase / IWorkflowDecorator。
    
    
    这条不要推翻。
    
    只把数学表达再收紧。
    
    
    ---
    
    4.3 Capability Transformer
    
    从这一阶段开始，正式停止把 BorrowingCapacity 叫普通 Decorator。
    
    定义：
    
    T :
    Resource
    × Context
    × Authority
    → DerivedCapability
    
    例如：
    
    CapacityLedger
    × SessionLineage
    × CurrentExecution
    → BorrowedCapacity
    
    这改变的是：
    
    who may act
    under what authority
    against which resource
    
    不是单纯的调用包装。
    
    所以正式词典：
    
    Port Decorator
        invocation transformation
    
    Capability Transformer
        authority derivation
    
    Adapter
        physical implementation
    
    Semantic Vocabulary
        law-bearing temporal compression
    
    
    ---
    
    第五章：绝对禁止建立中央 Decorator Framework
    
    这里请非常坚决。
    
    不要建立：
    
    Foundation/
      Decorators.fs
      Resilience.fs
      Middleware.fs
      Policies.fs
    
    更不要出现：
    
    type IWorkflowDecorator =
        abstract Wrap : Workflow -&gt; Workflow
    
    这会成为下一代大泥球。
    
    正确形态：
    
    Time/
        Deadline.fs
    
    Execution/Session/Wait/
        Observation.fs
    
    Participant/Provider/Attempt/Fallback/
        Workflow.fs
    
    OpenCode/Host/
        ModelCapacity.fs
    
    调用形式可以很函数式：
    
    raw
    |&gt; CausalWait.observe ...
    
    但代码仍属于 capability owner。
    
    原则：
    
    &gt; 抽象复用单位不是“设计模式”，而是“同一条 law”。
    
    
    
    只有发现三个 owner 使用了完全相同的数学 law，才考虑提取一个极小 primitive。
    
    否则宁可有三个五行高阶函数，也不要一个 300 行 ResilienceDecorators.fs。
    
    
    ---
    
    第六章：不要再把 VirtualTimer 叫 Decorator
    
    这是一次重要的词汇清洁。
    
    VirtualTimer 通常不是：
    
    Port -&gt; Port
    
    它是：
    
    ITimerPort 的另一实现
    
    所以它属于：
    
    Test Adapter / Deterministic Physical Model
    
    生产：
    
    NodeTimerPort
    
    测试：
    
    VirtualTimerPort
    
    二者满足相同 port contract。
    
    仓库现在已经明确要求 virtual timer/clock 可精确推进，用于确定性 temporal proof。
    
    所以以后：
    
    withVirtualTime
    
    除非真的是包装现有 timer port，否则不要因为函数式 API 好看就叫 decorator。
    
    这是词汇纪律的一部分。
    
    
    ---
    
    第七章：CE 的最终规则——它不是“什么都塞进去”的 workflow 函数
    
    你们当前 structured-workflow 的一句话其实已经非常接近最终定义：
    
    &gt; CE 讲故事；Vocabulary 负责定理；Decorator 负责能力；Port 负责物理。
    
    
    
    我会稍作修订：
    
    Pure Algebra          决定事实
    Witness               建立命题
    Capability            授予 effect authority
    CE                    编排因果相继
    Semantic Vocabulary   压缩有证明的时序 law
    Port Decorator        修饰 invocation
    Capability Transformer 派生 authority
    Physical Adapter      接触外部现实
    
    
    ---
    
    7.1 什么应该写成 let!
    
    判断方法非常简单：
    
    &gt; 它是不是需要等待另一个 capability/effect/evidence 产生？
    
    
    
    如果是，适合 CE：
    
    taskResult {
        let! evidence = reviewer.Await(...)
        let decision = Review.decide evidence
    
        match decision with
        | ...
    }
    
    
    ---
    
    7.2 什么应该是 pure match
    
    如果一个分支只依赖已经取得的数据：
    
    Evidence -&gt; Decision
    
    放纯函数。
    
    不要写：
    
    task {
        if ...
        elif ...
        else ...
    }
    
    然后里面根本没有 effect。
    
    例如：
    
    let decide evidence =
        match evidence with
        | ...
    
    更正确。
    
    
    ---
    
    7.3 Evidence -&gt; Decision -&gt; match -&gt; Effect 是核心姿态
    
    不要追求整个 workflow：
    
    A
    → B
    → C
    → D
    → E
    
    完全没有 match。
    
    业务分支是真实世界的代数。
    
    好的 CE 往往就是：
    
    taskResult {
        let! evidence = observe ports
    
        match decide evidence with
        | Stop outcome -&gt;
            return outcome
    
        | Continue instruction -&gt;
            let! receipt = execute instruction
            return finish receipt
    }
    
    这比强迫所有东西 monadic-linear 更清晰。
    
    
    ---
    
    7.4 CE 中禁止存在什么
    
    禁止：
    
    CurrentStage
    NextStep
    ResumeAt
    Phase
    StepIndex
    ContinueToken
    
    如果这些只是“程序走到哪里”。
    
    允许：
    
    ReviewOutcome
    HandleLifecycle
    PhysicalAttempt
    QuiescencePermit
    ProviderFailure
    
    如果它们描述真实领域/物理世界。
    
    你们当前 STRUCTURED-WORKFLOW-017 已经非常准确地禁止：
    
    child Stage -&gt; parent match -&gt; effect
    registry presence -&gt; infer lifecycle -&gt; effect
    Advance/Tick/Resume/Step repeatedly drives child
    recovery jumps into child internal continuation
    
    并要求 parent 只能观察 typed input/capability、领域结果、evidence、capability outcome。
    
    这条就是最终宪法之一。
    
    
    ---
    
    第八章：Semantic Vocabulary 的准入制度
    
    不是每个 helper 都配叫 Vocabulary。
    
    一个函数只有在下面五问全部有答案时，才能成为 public semantic vocabulary。
    
    你们当前 HOW 已经有几乎一样的审查表：
    
    1. 名字声明什么业务承诺？
    
    
    2. 隐藏哪些时序？
    
    
    3. 哪个 temporal / behavioral proof 证明？
    
    
    4. 改变 trace 还是 transparent？
    
    
    5. crash 后从什么 durable evidence 重入？
    
    
    
    我建议再加两问：
    
    6. 它的 Primary Owner 是谁？
    
    
    7. 如果删除/替换实现，哪些消费者应该完全不受影响？
    
    
    
    于是禁用：
    
    executeSafe
    process
    handle
    runThing
    manage
    perform
    doWorkflow
    
    除非 domain vocabulary 真的就叫那个。
    
    鼓励：
    
    continueAfterConfirmedFailure
    ensurePerfectConfirmed
    publishEventually
    recoverFamilyDirect
    reviewUntilFirstRevisionOrAllConfirmed
    
    事实上 structured-workflow 当前已经登记了这些高阶 Vocabulary，并要求生产定义真实存在。
    
    这是很好的基础。
    
    
    ---
    
    第九章已毕业 → 守江山

    Production Semantic Ownership Graph 已落到 `scripts/checks/semantic-owners.json` + `semantic-owners` hard gate；APPLIES-TO=治理范围、primary owner≠目录已成为守城事实。本章施工说明删除，后续只允许收紧 owner dependency，不允许退回“目录即 owner”或多 owner 模糊状态。

    ---
    
    第十章：建立 Owner Dependency Gate
    
    有了：
    
    file → owner
    
    才能开始真正消灭大泥球。
    
    目标是生成：
    
    production import graph
    
    然后投影为：
    
    owner A -&gt; owner B
    
    
    ---
    
    10.1 不要求 source graph = requirement graph 一模一样
    
    这是非常重要的。
    
    Requirement dependency 表示：
    
    &gt; law A 的成立以 law B 为前提。
    
    
    
    Source dependency 表示：
    
    &gt; implementation A 编译时引用 implementation B。
    
    
    
    两者不是同一张图。
    
    所以不要写一个幼稚 gate：
    
    F# import edge 必须与 requirements/INDEX.md 一一对应
    
    正确规则是：
    
    &gt; 一个跨 owner 的 production dependency 必须拥有合法的 architectural justification。
    
    
    
    一般是：
    
    A requirement depends on B
    
    或者：
    
    A consumes B&#39;s published physical/semantic contract
    
    或者：
    
    composition root wiring
    
    
    ---
    
    10.2 禁止 cross-owner internal import
    
    这是未来最重要的 L0 gate 之一。
    
    规则：
    
    foreign owner
        ↓
    只能依赖
        ↓
    published contract / port / semantic surface
    
    不能：
    
    Owner A
    → Owner B/private/internal/helper/implementation DU
    
    仓库现有 enforcer 已经把这个问题说得非常精确：
    
    &gt; 语言层面的 public/internal 不是 architecture authorization；真正的问题是 owner 是否承诺它为跨层 contract。
    
    
    
    所以最好建立显式 published contract manifest，而不是仅靠：
    
    文件名叫 Surface.fs 就算 public
    
    
    ---
    
    第十一章：不要立即拆 PluginTransforms——先重新定义它是什么
    
    现在开始第一个大手术。
    
    首先需要改变目标：
    
    &gt; PluginTransforms 不需要“少依赖”。
    
    
    
    Composition Root 本来就应该知道很多 capability。
    
    真正的问题是：
    
    &gt; 它能不能只知道 public capability，而不知道那些 capability 的内部语义实现？
    
    
    
    所以目标不是：
    
    95 opens -&gt; 5 opens
    
    目标是：
    
    wide fan-out
    but shallow knowledge
    
    这是根本区别。
    
    
    ---
    
    第十二章：PluginTransforms 最终结构
    
    我建议最终把它定义为：
    
    Provider Transform Composition Root
    
    它只做四件事：
    
    1. 确定本次 transform 属于哪种 composition mode
    2. 按固定语义顺序调用 owner-published operations
    3. 显式传递必要 capability/context
    4. 返回 Host 所需结果
    
    它不能：
    
    自己重新判断 XTrace law
    自己解析 Strength policy
    自己实现 Enforcer continuation
    自己决定 requirement-grounding 规则
    自己读业务 persistence
    自己维护跨 callback state
    
    
    ---
    
    第十三章：不要建立 ITransformMiddleware
    
    绝对不要：
    
    type ITransform =
        abstract Apply : TransformContext -&gt; Task&lt;TransformContext&gt;
    
    let pipeline =
        [
            xtrace
            companion
            enforcer
            strength
        ]
    
    因为这会把语义顺序变成数据。
    
    以后一定会出现：
    
    if condition then pipeline.Insert(...)
    
    然后恭喜，第二运行时回来了。
    
    
    ---
    
    第十四章：PluginTransforms 使用“静态乐谱”
    
    最终应该看起来像：
    
    let normalTransform
        (cap: NormalTransformCapabilities)
        (input: TransformInput)
        : Task&lt;unit&gt; =
        task {
            do!
                cap.ProviderAttempt.beginPhysicalAttempt
                    input.Session
                    input.Output
    
            let! startedAt =
                cap.SessionTime.bindStartedAt
                    input.Session
                    input.Output
    
            let! replay =
                cap.Strength.prepareReplay
                    input.Session
                    input.Output
    
            do!
                cap.SemanticTrace.captureBeforeProjection
                    input.Session
                    input.Output
                    replay
    
            do!
                cap.Companion.projectOrdinaryMaterial
                    input.Input
                    input.Output
    
            do!
                cap.ProviderProjection.applyXWire
                    input.Output
    
            do!
                cap.BloggerContinuation.continueAfterProjection
                    input.Output
    
            do!
                cap.Strength.completeEligibleSpeculation
                    input.Output
    
            do!
                cap.PairGuidance.projectAfterStrength
                    startedAt
                    input.Output
    
            do!
                cap.RequirementGrounding.project
                    input.Output
    
            cap.BloggerChronicle.project input.Output
    
            cap.HostBoundary.ensureProviderMessageShape input.Output
        }
    
    注意：
    
    NormalTransformCapabilities 不是一个全仓 service locator。
    
    它：
    
    private/local to PluginTransforms composition
    named fields
    fixed topology
    no List&lt;Middleware&gt;
    no dynamic registration
    no generic decorator interface
    
    这样测试很好写，而生产顺序仍然显式。
    
    
    ---
    
    第十五章：PluginTransforms 的顺序怎么永久锁死
    
    不要只做 source-regex test。
    
    需要三层 proof。
    
    L0：composition shape
    
    静态检查：
    
    no middleware list
    no dynamic registration
    no private semantic helper proliferation
    no foreign internal imports
    
    L1/L2：semantic trace
    
    把 NormalTransformCapabilities 替换为 recording fakes：
    
    BeginAttempt
    BindSessionStart
    PrepareStrength
    CaptureXTrace
    ProjectCompanion
    ApplyXWire
    ContinueBlogger
    SpeculateStrength
    InjectPair
    GroundRequirements
    InjectChronicle
    Sanitize
    
    断言：
    
    actualTrace = expectedTrace
    
    而且各 operation 自己仍由 owner package 证明。
    
    L3：
    
    真实 Host adapter 只证明：
    
    OpenCode transform mutation semantics
    
    L4：
    
    One World 只证明：
    
    &gt; 整套 composition 在真实 OpenCode 环境可工作。
    
    
    
    这就符合你们现有 proof ladder。
    
    
    ---
    
    第十六章：如何防止以后又往 PluginTransforms 塞 helper
    
    新增 L0：
    
    PluginTransforms composition-root invariant
    
    允许：
    
    create
    wire
    compose
    dispatch mode
    call owner surface
    small Host shape extraction
    
    禁止：
    
    let private decideXXX
    let private recoverXXX
    let private classifyXXX
    let private calculateXXX
    let private maintainXXX
    
    不是靠函数名黑名单完全判断，而是：
    
    1. composition root 的 pure helper 只能是 representation-level；
    
    
    2. semantic branch 必须调用 owner function；
    
    
    3. 新增超过一定复杂度的 private helper触发人工 owner review。
    
    
    
    不要使用 LOC 限制。
    
    行数不是架构。
    
    
    ---
    
    第十七章：PluginTransforms 的 Strength Replica branch 不要强行消灭
    
    当前源码明确存在：
    
    Replica:
    XWire
    → runtime.HandleTransform
    → sanitize
    
    而普通路径经过完整 normalTransform。
    
    这个分支看起来“不统一”，但可能恰恰是领域差异。
    
    所以不要为了：
    
    &gt; “所有 transform 都应该走统一 pipeline”
    
    
    
    强行合并。
    
    正确做法是定义：
    
    type TransformMode =
        | Ordinary
        | StrengthReplica of StrengthReplicaCapability
        | ExplicitResumeDisclosure
    
    如果这些确实是真实 semantic alternatives。
    
    然后：
    
    match mode with
    | Ordinary -&gt; ...
    | StrengthReplica cap -&gt; ...
    | ExplicitResumeDisclosure -&gt; ...
    
    这是合法领域分支。
    
    不是 program counter。
    
    
    ---
    
    第十八章：第二个手术点 AssistanceHost——先承认它已经有一半是正确答案
    
    这一点很重要。
    
    不要把 AssistanceHost 当作“全坏”。
    
    现在 NeedHelpSensor 已经：
    
    exact sentinel
    exact SessionId
    exact ProviderRunIdentity
    private AssistanceAbortClaim
    one-shot consumption
    
    而且当前源码明确：
    
    AbortWake 只 claim
    fresh SessionIdle 才是 transport fence
    permit 后才 TryConsumeAssistanceClaim
    成功后才能发送 escalation / 创建 consultation
    
    
    
    这其实已经是一个非常漂亮的：
    
    Capability-Passing causal handoff
    
    所以 AssistanceHost 的重构不是：
    
    &gt; “把旧隐式状态机变成 capability。”
    
    
    
    它已经做了一部分。
    
    真正任务是：
    
    &gt; 把这个正确的 capability seam 从巨大跨领域 host 中解放出来。
    
    
    
    
    ---
    
    第十九章：Assistance 的最终业务所有权应该是什么
    
    我建议把它的核心责任写成一句话：
    
    &gt; 当一个受 Wanxiangshu 管理的 provider attempt 产生合法 NEEDHELP authority 时，在不破坏当前 physical
        attempt、Authority Root 与 parent-child ownership 的条件下，将该 authority 转换成一次 bounded assistance successor，并把结果重新交还原
        owner。
    
    
    
    如果一句话再长，就说明 owner 还没切好。
    
    这里实际上有四个概念：
    
    NeedHelp Detection
    Assistance Admission
    Consultation Delegation
    Advice Delivery
    
    它们不一定属于同一模块。
    
    
    ---
    
    第二十章：Assistance 推荐物理拆法
    
    例如：
    
    Interaction/
      Authority/
        Assistance.fs
    
      Dispatch/
        Assistance/
          Workflow.fs
          Decision.fs
    
        OpenCode/
          NeedHelpSensor.fs
          AssistanceAdapter.fs
    
    名字可以调整。
    
    角色应类似：
    
    
    ---
    
    NeedHelpSensor.fs
    
    只拥有：
    
    reasoning delta stream
    fragment suffix
    exact sentinel
    exact provider attempt identity
    abort reservation
    
    不拥有：
    
    deep inquiry
    review
    git
    strength
    todo
    finality
    
    
    ---
    
    Interaction/Authority/Assistance.fs
    
    拥有：
    
    AssistanceAbortClaim
    AssistanceCause
    AssistanceDisposition
    admission law
    
    这里的类型尽量是纯的。
    
    
    ---
    
    Assistance/Workflow.fs
    
    只有 CE：
    
    let handle
        (ports: AssistancePorts)
        (context: AssistanceContext)
        (claim: AssistanceAbortClaim)
        =
        taskResult {
            let! admission =
                ports.Admission.consumeAfterFreshIdle context claim
    
            let! profile =
                ports.Authority.requireCurrentOwner admission
    
            match Assistance.decide profile with
            | EscalateFast request -&gt;
                return! ports.Escalation.continueOwner request
    
            | ConsultDeep request -&gt;
                let! consultation =
                    ports.Consultation.start request
    
                let! evidence =
                    ports.Consultation.awaitOutcome consultation
    
                return!
                    ports.Advice.deliver
                        (AssistanceAdvice.fromEvidence evidence)
        }
    
    关键是：
    
    CE 看 Assistance 世界
    而不是 Git/Strength/Review/Todo 世界
    
    
    ---
    
    第二十一章：不要设计巨型 AssistancePorts
    
    危险形状：
    
    type AssistancePorts =
        {
            Git: IGitPort
            Session: ISessionPort
            Journal: IJournal
            Strength: IStrength
            Todo: ITodo
            Review: IReview
            ...
        }
    
    这只是把大泥球从 imports 搬进 record。
    
    正确形态应该是业务 capability：
    
    type AssistancePorts =
        {
            CurrentAuthority :
                AssistanceOwner -&gt; CurrentAuthority option
    
            StartConsultation :
                ConsultationRequest -&gt; Task&lt;Result&lt;ConsultationHandle, AssistanceError&gt;&gt;
    
    
            AwaitConsultation :
                ConsultationHandle -&gt; Task&lt;Result&lt;ConsultationEvidence, AssistanceError&gt;&gt;
    
    
            DeliverAdvice :
                AdviceDelivery -&gt; Task&lt;Result&lt;AdviceReceipt, AssistanceError&gt;&gt;
        }
    
    注意没有：
    
    Git
    Strength
    Todo
    Review
    
    这些细节由 adapter 负责把真实系统组装成上面四种 capability。
    
    
    ---
    
    第二十二章：不要凭空发明 ConsultationCompletedWitness 持久化事件
    
    先检查现有 durable facts 是否已经足够证明：
    
    consultation child belongs to owner
    child terminal
    terminal belongs exact logical run
    output available
    owner still current
    
    如果已经有：
    
    HandleLinked
    HandleCompleted
    AuthorityRoot
    ProviderRun terminal
    
    那么：
    
    ConsultationEvidence
        -&gt; Result&lt;ConsultationCompletedWitness, InvalidConsultation&gt;
    
    应该是 pure projection。
    
    只有如果存在一个现有事实集合无法表达但必须跨 crash 保存的新领域区别，才新增 event。
    
    这是防止 EventStore 变成“所有中间变量的垃圾桶”的关键。
    
    
    ---
    
    第二十三章：Assistance abort 权限一定要继续保持现在的分型
    
    当前文档已经有一个非常重要的边界：
    
    InterruptAttempt
    
    只允许 physical managed sub-session 的当前 attempt interruption；
    
    而：
    
    AbortSession
    
    才拥有 detach + descendant cascade。
    
    这个边界不要因为“统一 capability”被合并。
    
    最终应该甚至更明确：
    
    type InterruptCurrentAttempt =
        private ...
    
    type TerminateManagedSession =
        private ...
    
    两种不同 capability。
    
    不能：
    
    ISessionControl.Abort(...)
    
    一个万能入口。
    
    
    ---
    
    第二十四章：Assistance 重构的 proof ladder
    
    Pure
    
    证明：
    
    Fast → escalation
    Deep → consultation
    wrong authority root → reject
    recursive NEEDHELP → bounded failure
    late child result → cannot resurrect superseded owner
    
    Temporal
    
    精确枚举：
    
    NEEDHELP
    → abort accepted
    → TurnAborted
    → no fresh idle
    → must not send
    
    fresh idle
    → claim consume
    → exactly one successor
    
    以及：
    
    abort fail
    → claim rollback
    → ordinary failure remains possible
    
    Adapter
    
    证明 OpenCode：
    
    InterruptAttempt affects current attempt only
    reasoning deltas fragment correctly
    SessionIdle produces fresh quiescence evidence
    
    Long Stroke
    
    只证明真实 Host 的：
    
    sentinel
    → abort
    → idle
    → consultation/escalation
    → owner receives advice
    
    组合成立。
    
    
    ---
    
    第二十五章：第三个手术点 HostSignalBootstrap
    
    完成前两个以后再动。
    
    目标：
    
    &gt; Bootstrap 是 wiring，不是 semantics。
    
    
    
    它应该只：
    
    construct
    subscribe
    attach
    route physical signals
    dispose/drain
    
    它不应该：
    
    决定 model policy
    决定 recovery semantics
    决定 fission policy
    决定 finality
    决定 assistance successor
    决定 strength meaning
    
    你们目前其实已经有 plugin load purity tests，明确拒绝在 plugin load 恢复 session/tool durable state。
    
    因此继续沿这个方向推进。
    
    
    ---
    
    第二十六章：Composition Root 允许宽，不允许深
    
    这是整个系统以后非常重要的一句话：
    
    &gt; Wide knowledge is legal at composition roots; deep knowledge is not.
    
    
    
    所以：
    
    PluginBoot
    HostSignalBootstrap
    PluginTransforms
    ToolRegistry
    
    可能依赖很多 owners。
    
    这并不自动是泥球。
    
    判断标准不是 imports 数量。
    
    而是：
    
    &gt; 它是否 match 了 foreign owner 的内部领域类型，并据此做业务决定？
    
    
    
    例如合法：
    
    let reviewer = Review.createCapability ...
    let routing = ModelRouting.create ...
    let assistance = Assistance.create ...
    
    非法：
    
    match Review.InternalCohortState with
    | ...
    
    
    ---
    
    第二十七章：Model Routing 作为全仓“教材样板”
    
    所有其它模块都应该学习它，不是复制实现，而是复制ownership discipline。
    
    当前 architecture test 已经明确要求：
    
    CapacityLedger
    BorrowingCapacity
    
    存在于 ModelCapacity；
    
    routing 可以使用 BorrowingCapacity&lt;ModelRoutingTarget&gt;；
    
    但：
    
    Sessions
    SessionExecutionBinding
    PluginTransforms
    HostSignalBootstrap
    MJS scheduler
    
    都不得重新出现：
    
    ancestorDistance
    CapacityTokenState
    CapacityStepDemand
    ownedTokenByExecution
    
    
    
    这就是教科书级 ownership gate。
    
    未来每个复杂 capability 都应该能写出类似测试：
    
    this knowledge exists here
    and nowhere else
    
    
    ---
    
    第二十八章：建立“Knowledge Exclusivity Gate”
    
    这是下一代 architecture gate 的核心。
    
    例如 Assistance：
    
    AssistanceAbortClaim
    exact NEEDHELP claim consume
    
    只允许 interaction-authority/host adapter owner。
    
    Finality：
    
    ConfirmedReview admission
    
    只允许 finality owner。
    
    Capacity：
    
    ancestor borrowing
    
    只允许 EMR capacity transformer。
    
    规则不是：
    
    字符串全仓只能出现一次
    
    而是：
    
    关键算法/状态机/authority vocabulary
    有一个 positive owner zone
    +
    若干 explicit consumer zones
    +
    其余 zone negative
    
    这就是 model routing 现有测试的普遍化。
    
    
    ---
    
    第二十九章：structured-workflow 不要“退场”，要完成 Meta 化
    
    我建议不用“降级”这个词。
    
    它不是不重要。
    
    恰恰相反：
    
    &gt; 它应该从“拥有很多 workflow 细节的超包”升级为“只拥有宿主语言工作流结构律的 Meta Constitution”。
    
    
    
    最终只保留真正 universal 的法律。
    
    
    ---
    
    第三十章：structured-workflow 最终应保留什么
    
    我建议保留类似：
    
    SW-META-001
    Business flow is expressed directly by host-language CE.
    
    SW-META-002
    No second business runtime / AST interpreter.
    
    SW-META-003
    Stored state describes reality, not execution position.
    
    SW-META-004
    Pure decision and physical effect have an explicit seam.
    
    SW-META-005
    Mutable state is physical resource / projection cache / algorithm scratch,
    not business program counter.
    
    SW-META-006
    Workflow composition is structurally closed.
    
    SW-META-007
    Semantic compression requires owner law + proof.
    
    SW-META-008
    Trace-altering higher-order composition must be named and owned.
    
    SW-META-009
    Cancellation is control plane unless domain explicitly models a cancellation fact.
    
    SW-META-010
    Business recursion/fan-out must be bounded or guarded by explicit physical capability.
    
    差不多就够了。
    
    
    ---
    
    第三十一章：从 structured-workflow 移出去什么
    
    例如：
    
    Provider fallback 怎么 retry
    Manager 如何 rebase
    Blogger 如何 catch up
    Review 怎么 confirm
    Recovery 怎么恢复 family
    
    这些都不是 structured-workflow 的 law。
    
    它只规定：
    
    &gt; 这些东西必须长成什么结构。
    
    
    
    真正行为 law 回：
    
    provider-attempt-recovery
    change-integration
    context-compression
    review-assurance
    crash-reconciliation
    
    
    ---
    
    第三十二章：structured-workflow 不应该拥有 taskResult 实现
    
    这是一个很容易犯的错误。
    
    Meta package 可以规定：
    
    Task&lt;Result&lt;_,_&gt;&gt; plumbing 应有唯一 CE vocabulary
    禁止私建 builder
    
    但实际：
    
    TaskResultCE
    TaskValue
    TaskResult
    
    这些机械实现可以继续归：
    
    Foundation
    
    Meta law 和 implementation owner 不需要是同一个。
    
    否则 structured-workflow 又会成为：
    
    “所有与 CE 有关的代码归我”
    
    然后重新膨胀。
    
    
    ---
    
    第三十三章：APPLIES-TO 可以继续很宽，但 ownership 必须很窄
    
    因此针对你第 78 题，我现在给一个更精确裁决：
    
    不要机械缩 APPLIES-TO。
    
    如果 Meta law：
    
    “全仓不能出现第二 workflow runtime”
    
    那当然要扫描：
    
    /src/Wanxiangshu/**/*.fs
    
    正确修复是明确：
    
    APPLIES-TO = governance scope
    ≠ primary ownership
    
    于是 structured-workflow 可以扫描全仓，但不能宣称：
    
    &gt; “所有 task/async 代码都属于 structured-workflow。”
    
    
    
    
    ---
    
    第三十四章：现有 architecture.mjs 还不够——但不要把所有 gate 塞进去
    
    当前 architecture checker 明确只有一个 production root，并定义 PURE_DIRS 等基础结构检查。
    
    下一阶段不要把它扩成 5000 行 God Gate。
    
    建议：
    
    scripts/checks/
        architecture.mjs
        semantic-ownership.mjs
        owner-dependencies.mjs
        cross-owner-contract.mjs
        capability-boundary.mjs
        composition-root.mjs
    
    然后统一由：
    
    scripts/check.mjs
    
    接线。
    
    你们已有 enforcer 原则也明确指出：如果 ownership/dependency invariant 可以机械判断却只靠 review，就是 missing architecture
        gate。
    
    
    ---
    
    第三十五章：Capability/Witness 静态门应该查什么
    
    不要只按名称。
    
    名称 gate 只能做“报警器”。
    
    例如：
    
    *Token
    *Permit
    *Lease
    *Claim
    *Witness
    *Proof
    *Receipt
    
    触发分类检查。
    
    然后要求 annotation 或 manifest：
    
    kind = witness
    kind = capability
    kind = receipt
    kind = physical-handle
    
    但最终判据是用途：
    
    Witness
    
    允许：
    
    serialize when durable
    project
    compare
    fold
    derive decision
    
    不允许：
    
    直接调用 physical effect，仅凭旧 witness 绕过 admission
    
    Capability
    
    允许：
    
    owner-gate consume
    invoke corresponding effect
    revoke
    dispose
    
    默认不允许：
    
    FactCodec
    EventEnvelope
    JSON persistent state
    
    
    ---
    
    第三十六章：不要追求“所有 Capability 构造器 private”作为唯一保障
    
    推荐 private，但真正规则是：
    
    &gt; 只有 owner 的 issuance function 能建立有效 capability。
    
    
    
    可以有：
    
    private record
    
    但如果同一个 module 到处随便：
    
    { Id = ...; Epoch = ... }
    
    private 没意义。
    
    所以更好的 API：
    
    module BlessCapability =
    
        type T = private BlessCapability of ...
    
        let internal grant ...
        let internal validate ...
    
    foreign owner 只能：
    
    receive
    pass
    consume through published port
    
    
    ---
    
    第三十七章：Capability hierarchy 怎么做
    
    不要做 OO 权限继承：
    
    IAdminCapability
        : IUserCapability
    
    更适合 F# 的是显式 attenuation：
    
    AdminCapability
        -&gt; ReadCapability
    
    AdminCapability
        -&gt; PublishCapability
    
    函数：
    
    let attenuateToRead admin =
        ...
    
    这就是 capability transformer。
    
    父 capability revoke 后子 capability 是否失效，由 law 明确：
    
    shared epoch
    parent lease
    revocation generation
    
    而不是靠对象引用层级猜。
    
    
    ---
    
    第三十八章：有限 N 次 Capability
    
    不要发 N 个 bool。
    
    建：
    
    QuotaCapability
    
    owner gate 维护：
    
    Remaining
    Epoch
    Scope
    
    消费：
    
    tryConsume
        : QuotaCapability
        -&gt; Result&lt;QuotaReceipt * QuotaCapability option, QuotaFailure&gt;
    
    但如果 capability 是 opaque handle，也可以让 caller 永远持同一 handle：
    
    gate.TryUse(handle)
    
    由 gate 内部计数。
    
    哪种更好取决于你是否希望 quota 的剩余量成为 caller-visible semantic data。
    
    通常不要。
    
    
    ---
    
    第三十九章：Decorator 允许持可变状态吗？
    
    可以。
    
    例如：
    
    rate limiter
    single-flight
    circuit health
    causal observation registry
    
    但必须分类。
    
    algorithm scratch
    physical resource
    projection cache
    policy state
    
    不能：
    
    “为了 decorator 工作方便”
    
    就出现：
    
    CurrentStage
    PendingSecondRetry
    FallbackPhase
    
    然后变隐式 workflow runtime。
    
    
    ---
    
    第四十章：Decorator Error 应该如何传播
    
    Transparent decorator：
    
    原则上保持 owner contract
    
    如果它自身失败：
    
    diagnostic failure
    
    需要明确是：
    
    fail open
    fail closed
    process fatal
    
    不能随便吞。
    
    Semantic decorator 可以把底层物理错误映射成领域 policy outcome，但必须由 owner law 决定。
    
    例如：
    
    ProviderError
        -&gt; ConfirmedProviderFailure
    
    不是“error mapping utility”。
    
    它是 provider recovery 的业务语义。
    
    
    ---
    
    第四十一章：不要设置 Decorator 最大嵌套深度
    
    不要：
    
    最多 5 层 decorator
    
    这种指标毫无理论意义。
    
    应该控制：
    
    semantic nesting depth
    
    即：
    
    &gt; 一次调用要理解多少个 trace-altering policy？
    
    
    
    透明 observation 十层可能都没问题。
    
    三个互相影响的 retry/fallback/deadline/claim 已经可能无法理解。
    
    所以 review 看：
    
    有多少独立 policy owner 改变本次 trace？
    
    不是看调用栈层数。
    
    
    ---
    
    第四十二章：Composition Root 里的业务 if 是否全部禁止？
    
    不。
    
    这又是一个容易走极端的地方。
    
    允许：
    
    match TransformMode with
    | Ordinary -&gt; ...
    | StrengthReplica replica -&gt; ...
    
    因为这是 composition alternative。
    
    禁止：
    
    if review.IsPerfect &amp;&amp; strength.Pending &amp;&amp; ...
    
    因为 root 开始决定 foreign domain semantics。
    
    原则：
    
    &gt; Composition Root 可以选择 wiring topology，不能重新实现 domain decision。
    
    
    
    
    ---
    
    第四十三章：事件溯源部分——这一块其实已经很接近最终形态
    
    我不会建议你们大改 durable-events。
    
    当前它已经有非常强的几条 law。
    
    例如本地 append：
    
    &gt; durable witness 是 writer 文件末尾完整 canonical JSON+LF；runtime append 不得创建 Git
        blob/tree/ref/CAS，也不得随历史长度重写旧 bytes。
    
    
    
    每个 process 一个唯一 WriterId 和永久增长的 NDJSON writer file。
    
    Git blob 只出现在 remote sync hook 边界，而且完整 writer file 对应一个 blob。
    
    这已经非常清晰。
    
    
    ---
    
    第四十四章：CanonicalIntegrator 是另一个“教材样板”
    
    当前 normative law 已经规定：
    
    &gt; 生产只有一个 canonical F# CE Integrator 可以把 writer streams 积分成 Current；业务模块只注册 single-event
        integration oracle，不能自己获得 history-reader capability，也不能自行 scan/load/fold history；boot replay 与 live append 使用同一个
        CE program 和同一套 rules。
    
    
    
    这比很多 event-sourcing 系统都干净。
    
    这里的下一步不是重构成更抽象。
    
    而是继续封死旁路。
    
    
    ---
    
    第四十五章：业务模块永远不能有 HistoryReader
    
    建议把这条升成极强 architecture gate：
    
    禁止 feature owner 出现：
    
    loadEvents
    readAllEvents
    scanHistory
    replayHistory
    foldHistory
    EventStoreMerge
    
    除：
    
    CanonicalIntegrator
    remote physical convergence
    
    以外。
    
    已有 durable-events test 本身已经在检查业务模块不能拥有历史读取/重放循环。
    
    继续扩大覆盖即可。
    
    
    ---
    
    第四十六章：Projection 不是数据库 Entity
    
    最终认知：
    
    History = durable truth
    Projection = cached integral
    
    但“所有状态都必须 fold events”也不要宗教化。
    
    分类：
    
    Durable domain truth
        → EventStore
    
    Derived durable view
        → Projection
    
    Process-local capability authority
        → Gate
    
    Physical resource
        → runtime registry
    
    Algorithm scratch
        → local mutable
    
    Diagnostic observation
        → diagnostic store
    
    千万别把：
    
    PTY handle
    timer
    quiescence permit
    socket
    in-flight Task
    
    写 EventStore。
    
    
    ---
    
    第四十七章：Projection 查询不需要全部 O(1)
    
    第 91 题需要特别纠正。
    
    不是所有 incremental query 都必须 O(1)。
    
    正确规则：
    
    &gt; 被明确证明为热路径且历史规模增长会破坏系统目标的查询，才必须拥有复杂度 contract。
    
    
    
    例如：
    
    latest committed checkpoint
    
    如果每个 provider turn 都查，就应该：
    
    projection.LatestCommitted
    
    O(1)。
    
    但一次 rare recovery 可以 O(log n)，甚至 O(n)，如果有明确 budget。
    
    不要把算法 Big-O 变成无差别 architecture dogma。
    
    
    ---
    
    第四十八章：ProjectionCutTail 的正确理解
    
    当前设计非常精细：
    
    bad semantic fact：
    
    仍 durable 保留
    → rule Current 保持 last-good
    → 同次 append 写 ProjectionCutTail
    → replay 先看到 bad fact
    → 再看到 reset
    → 再继续
    
    而且当前进程一旦得到自己 EventId 被 cut 的 typed rejection receipt，就必须 process fatal，因为 durable projection
        能恢复，并不意味着已经发生的 process-local/physical side effect 可以回滚。
    
    这条要保留。
    
    它正确地区分了：
    
    durable recoverability
    ≠
    current process trustworthiness
    
    这是非常重要的分布式系统原则。
    
    
    ---
    
    第四十九章：不要试图证明“任意 SIGKILL 后 100% 无歧义恢复”这种绝对命题
    
    这是你 100 个问题里最后一个，也是最需要降维的一个。
    
    绝对命题：
    
    &gt; “任何一行代码执行期间 SIGKILL，重启后 100% 无歧义恢复。”
    
    
    
    对存在外部 Host / LLM / Git / process effect 的系统，一般不能凭软件内部 EventStore 绝对证明。
    
    典型窗口：
    
    发出外部 effect
    外部已执行
    SIGKILL
    本地 success receipt 尚未 durable
    
    重启时，你不知道：
    
    effect never happened
    
    还是：
    
    effect happened but receipt was lost
    
    除非外部系统提供：
    
    idempotency key
    transactional receipt
    queryable effect identity
    
    所以真正教科书级命题应该是：
    
    &gt; 对于每一个 crash cut，系统要么从 durable facts 唯一恢复，要么通过外部 physical observation/reconciliation
        收敛到一个显式、有限、fail-closed 的合法状态；绝不通过猜测补写事实。
    
    
    
    这才可证明。
    
    
    ---
    
    第五十章：所有外部 effect 都应该逐渐拥有四段式模型
    
    对于高价值 effect：
    
    Intent
    Admission
    Physical Receipt
    Durable Outcome
    
    例如：
    
    PublishRequested
    PublishCapability
    Git fast-forward receipt
    Published
    
    或者：
    
    ProviderExecutionAdmitted
    CapacityLease
    provider attempt
    ProviderAttemptTerminal
    
    不是每个 effect 都必须四个 event。
    
    这是因果概念模型。
    
    有时 Intent 是 CE 内瞬态； 有时 Admission 是 process capability； Receipt 是 Host observation； Durable Outcome
        才写事件。
    
    
    ---
    
    第五十一章：Crash recovery 永远从事实重入普通 CE
    
    不要：
    
    ResumeAtReviewStep3
    
    而是：
    
    taskResult {
        let facts = projection.Current
    
        match ReviewRecovery.decide facts with
        | AlreadyDone witness -&gt;
            return witness
    
        | NeedsReview input -&gt;
            return! reviewWorkflow input
    
        | NeedsReconcile input -&gt;
            return! reconcile input
    }
    
    你们 structured-workflow 当前已经明确禁止 durable ResumeAt... 这类 recovery control token，并要求恢复重入普通 workflow。
    
    
    继续贯彻。
    
    
    ---
    
    第五十二章：生产与测试的最终关系不是 1:1，而是 Proposition → Proof Portfolio
    
    从此不要讨论：
    
    Foo.fs
    应该有
    Foo.test.mjs
    
    这种弱映射。
    
    正确图：
    
    Normative Proposition
          │
          ├── Static proof
          ├── Pure proof
          ├── Temporal proof
          ├── Adapter proof
          └── Long Stroke evidence if necessary
    
    一个 test 可以覆盖多个相关 invariants。
    
    一个 proposition 可以有多个 proof。
    
    但：
    
    proof ownership
    
    仍必须唯一。
    
    这和你们 requirement system 的设计完全兼容。
    
    
    ---
    
    第五十三章：属性测试应该测 Law，不测随机代码
    
    推荐大量增加 deterministic generative tests。
    
    例如 Capability：
    
    生成：
    
    issue
    consume
    consume again
    supersede
    consume old
    issue new
    revoke
    consume
    
    验证：
    
    at most one admitted effect per capability epoch
    
    Event Fold：
    
    生成合法 event sequence：
    
    append online
    
    得到：
    
    Current_online
    
    然后：
    
    replay from zero
    
    得到：
    
    Current_replay
    
    证明：
    
    Current_online = Current_replay
    
    不是“逐字节”所有内存对象。
    
    应该比较 canonical semantic projection。
    
    因为缓存、dictionary enumeration、debug metadata 不一定属于 contract。
    
    
    ---
    
    第五十四章：Event Fold 的顺序性质不要默认交换律
    
    很多事件 fold：
    
    fold [A; B]
    ≠
    fold [B; A]
    
    所以 property-based test 不应该无脑测试 permutation invariance。
    
    应该先声明代数：
    
    commutative events
    causally ordered events
    idempotent duplicate
    independent branches
    conflicting heads
    
    然后分别证明：
    
    independent events commute
    causally related events preserve order
    duplicates are idempotent
    conflicts remain conflicts
    
    这才是数学化 event sourcing。
    
    
    ---
    
    第五十五章：Concurrent Heads 应该是事实，不是异常
    
    两个离线合法分支：
    
    A
     / \
    B   C
    
    如果没有 causal order：
    
    B || C
    
    Structural Projection 应表达：
    
    type HeadState =
        | Single of EventId
        | Concurrent of NonEmptySet&lt;EventId&gt;
    
    或者已有对应 domain shape。
    
    不要：
    
    last timestamp wins
    last file wins
    
    然后把冲突抹掉。
    
    冲突本身就是事实。
    
    
    ---
    
    第五十六章已毕业 → 守江山

    feature-owned durable backend / private ref / dual-write / Git bypass 已由 `unified-store-gate` 机械拒绝；保持零债，不再保留施工说明。
    
    
    ---
    
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
    
    第五十八章：最终代码审查模板
    
    以后每个重要 PR 必须能回答：
    
    1. 这条变化属于哪个 Primary Owner？
    2. 它建立/改变哪条 WHAT proposition？
    3. 输入是什么：
       Evidence / Witness / Capability / ordinary data？
    4. 输出是什么？
    5. 是否改变 business trace？
    6. 如果改变，law 是什么？
    7. 是否产生/派生 authority？
    8. 如果是，谁负责 issue / validate / consume / revoke？
    9. crash 发生在每个 effect 边界时怎样重入？
    10. 是否新增 durable fact？为什么现有 facts 不能推导？
    11. 是否新增 cross-owner dependency？
    12. dependency 是否只经 published contract？
    13. 是否暴露 child execution position？
    14. 是否出现 mutable state？
    15. mutable 属于：
        physical resource / projection cache / algorithm scratch / 什么？
    16. 如何证明：
        Static / Pure / Temporal / Adapter / Long Stroke？
    17. 最低 proof level 是哪层？
    18. 为什么不应该在更低层证明？
    19. 哪个 test/gate 必须因故意破坏而变红？
    20. 如果把 implementation 全部替换，consumer 哪些东西不应该知道？
    
    答不出来：
    
    REVISE
    
    
    ---
    
    第五十九章：Decorator Review 模板
    
    每个 decorator 回答：
    
    Owner:
    Published name:
    Underlying port:
    State held:
    Observation projection:
    Business observation:
    Trace relation:
    Failure policy:
    Cancellation law:
    Deadline law:
    Crash lifetime:
    Proof:
    
    Transparent：
    
    必须明确：
    
    π_business(D(P)) = π_business(P)
    
    Semantic：
    
    必须明确：
    
    allowed trace relation R
    
    不能写：
    
    &gt; “只是 wrapper。”
    
    
    
    
    ---
    
    第六十章：Capability Review 模板
    
    Capability:
    Owner:
    Authority granted:
    Resource:
    Scope:
    Provenance:
    Epoch/freshness:
    Who can issue:
    Who can consume:
    Can be observed without consuming:
    Can be delegated:
    Can be attenuated:
    Can be revoked:
    Can be serialized:
    Restart semantics:
    Consume failure algebra:
    Physical effect after admission:
    Receipt/evidence after effect:
    Proof:
    
    这会极大减少“token archaeology”。
    
    
    ---
    
    第六十一章：Witness Review 模板
    
    Witness:
    Proposition P:
    Evidence required:
    Pure establishment rule:
    Validity scope:
    Subject identity:
    Digest/epoch if required:
    Durable across restart?
    If yes: which facts reconstruct it?
    Can it directly authorize effect?
        NO, unless a named admission rule grants capability.
    
    最后一条非常关键。
    
    
    ---
    
    第六十二章：什么时候 Witness 需要 digest
    
    不是所有 Witness 都带 digest。
    
    判据：
    
    &gt; P 是否只对某个精确版本的 subject 成立？
    
    
    
    例如：
    
    Review PERFECT
    
    显然应该绑定：
    
    TreeHash
    
    否则它会被错误应用到未来 tree。
    
    但：
    
    UserRole = Manager
    
    可能不需要 tree digest。
    
    所以 digest 是 proposition identity 的一部分，不是 Witness 模板字段。
    
    
    ---
    
    第六十三章：旧 Witness 在现实变化后不是“变成非法值”
    
    例如旧：
    
    ConfirmedReview(Tree=A)
    
    Git Tree 变为 B 后。
    
    旧 Witness 仍然是真的：
    
    &gt; “Tree A 曾经被确认。”
    
    
    
    它没有魔法变质。
    
    真正发生的是：
    
    FinalityAdmission.grantBlessing
        currentTree = B
        witnessTree = A
    
    于是：
    
    Error StaleWitness
    
    这再次说明：
    
    Witness = knowledge
    Capability = current authority
    
    必须分开。
    
    
    ---
    
    第六十四章：Negative Witness 与 Capability Failure
    
    Negative Witness：
    
    type ReviewDefectWitness =
        | ConfirmedDefect of ...
    
    它说明：
    
    &gt; 某个负命题成立。
    
    
    
    Capability failure：
    
    Error StalePermit
    
    说明：
    
    &gt; 当前没有 effect authority。
    
    
    
    二者绝对不能共用：
    
    FailureToken
    
    
    ---
    
    第六十五章：Compensation 应该放哪里
    
    分三类。
    
    纯 admission 前失败
    
    不需要 compensation。
    
    物理资源 acquire 后失败
    
    由 resource scope：
    
    use
    try/finally
    dispose
    
    处理。
    
    已提交外部业务 effect 后需要反向业务 action
    
    这是真正 saga compensation。
    
    必须是：
    
    owner-owned workflow
    
    不要藏进 generic decorator。
    
    例如：
    
    CreateChild succeeded
    Link durable failed
    → abort fresh child
    
    这是 delegation owner 的 rollback law。
    
    不是 withCompensation 工具箱。
    
    
    ---
    
    第六十六章：CE 资源清理
    
    F#：
    
    use
    use!
    try/finally
    
    用于 lexical resource lifetime。
    
    但 Node/Fable 环境下需要注意：
    
    &gt; 不要把同步锁持有跨 let!。
    
    
    
    所有 process-local gate：
    
    lock
    → inspect/update minimal synchronous state
    → release lock
    → await external operation
    
    如果 mutation 本身必须跨 async operation 串行，就使用：
    
    serial queue / actor
    
    而不是长时间 lock。
    
    model routing 当前设计已经明确采用 process-wide serial queue 来顺序处理 resource mutation，避免并发 scheduler 基于同一旧
        snapshot 同时提交。
    
    这是正确模板。
    
    
    ---
    
    第六十七章：如何避免 CE 闭包变量变隐式 PC
    
    任何跨 await mutable：
    
    let mutable stage = ...
    
    都触发 review。
    
    但不是全禁。
    
    允许：
    
    local accumulator
    bounded algorithm
    resource handle
    
    危险：
    
    let mutable firstDone
    let mutable waitingSecond
    let mutable phase
    
    如果后续 let! 根据它决定业务 effect。
    
    优先改成：
    
    let! first = ...
    match first with
    | ...
    
    或者有界递归参数：
    
    let rec loop budget evidence =
        taskResult {
            ...
            return! loop (budget - 1) nextEvidence
        }
    
    
    ---
    
    第六十八章：如何证明递归有界
    
    三种合法方式。
    
    结构递归
    
    List tail
    tree child
    
    显式预算
    
    RetryBudget
    
    每次递减。
    
    物理 deadline / cancellation capability
    
    但要小心：
    
    &gt; deadline 只证明时间有界，不一定证明 attempt 数有界。
    
    
    
    如果每个 attempt 可以零耗时，理论上仍可能无限循环。
    
    所以 retry/fallback 最好同时有：
    
    budget
    +
    deadline
    
    如果需要。
    
    
    ---
    
    第六十九章：Semantic Vocabulary 的 Trace Refinement 怎么证明
    
    不要试图 theorem prover 全自动。
    
    对于绝大多数 workflow，采用：
    
    reference algebra
    +
    generated finite trace families
    +
    temporal deterministic tests
    
    例如 fallback：
    
    生成：
    
    A success
    A fail then B success
    A fail then B fail
    deadline before A
    deadline between A/B
    cancel during A
    stale terminal
    duplicate terminal
    
    然后证明每个 trace 满足 owner law。
    
    所谓“形式化”不等于必须 Coq。
    
    把 law 写清楚并对状态空间进行可重复穷举，已经比模糊 unit test 强几个数量级。
    
    
    ---
    
    第七十章：最终不要建立 Free Monad / Workflow AST
    
    这个应该永久禁掉。
    
    你们当前 structured-workflow 已明确拒绝：
    
    WorkflowCommand
    WorkflowReply
    Step
    Suspend
    WorkflowInterpreter
    
    这类第二 runtime pattern。
    
    保持。
    
    F# CE 最大的价值就是：
    
    &gt; 使用语言已有控制流和类型系统。
    
    
    
    不要为了“更形式化”再制造另一门语言。
    
    
    ---
    
    第七十一章：49 Package 怎么办
    
    不要减少到：
    
    10
    
    也不要追求：
    
    60
    
    数字没有意义。
    
    INDEX 已经明确说 49 只是当前 independent WHY/failure meaning/change test 的结果，不是目标。
    
    Package 合并条件：
    
    same WHY
    same failure meaning
    cannot change independently
    same semantic owner
    
    拆分条件：
    
    independent major change
    different authority
    different failure meaning
    different proof obligation
    
    
    ---
    
    第七十二章：110 条 dependency edge 也不是债务数量
    
    不要想着：
    
    &gt; “边越少越干净。”
    
    
    
    有些 semantic dependencies 就是真实复杂性。
    
    目标不是：
    
    minimize edge count
    
    而是：
    
    every edge is truthful
    every edge has direction
    no hidden edge
    no cycle without real mutual domain
    no source internal dependency bypasses graph
    
    本质复杂度应该在图里可见，而不是被 Utils 隐藏。
    
    
    ---
    
    第七十三章：教科书级不是“低耦合”，而是“诚实耦合”
    
    这是我认为你前面说“本质复杂性在耦合里面”的真正答案。
    
    复杂系统不可能没有 coupling。
    
    例如：
    
    Finality
    
    真的依赖：
    
    Review
    Git candidate
    Handle drain
    Authority
    Durability
    
    硬拆掉只是撒谎。
    
    好的 architecture 不是：
    
    0 coupling
    
    而是：
    
    coupling has explicit semantic direction
    
    即：
    
    Finality consumes ReviewWitness
    
    而不是：
    
    Finality reads ReviewerRuntime.internalState
    
    这就是区别。
    
    
    ---
    
    第七十四章：所谓 Functional DDD 最终只保留 DDD 的这一层
    
    不用 Entity/Repository/Aggregate 模板。
    
    保留：
    
    Ubiquitous Language
    Semantic Ownership
    Bounded Context
    Independent Change
    Anti-Corruption Boundary
    Explicit Dependencies
    
    然后具体实现使用：
    
    ADT
    pure functions
    CE
    capability
    events
    ports
    
    所以以后不必再争：
    
    &gt; “这是 DDD 吗？”
    
    
    
    它只是 architecture ownership theory。
    
    
    ---
    
    第七十五章：最终项目目录不要强行镜像 Requirement Packages
    
    不要变成：
    
    src/
      finality/
      time-capability/
      causal-wait/
      execution-model-routing/
    
    除非代码自然如此。
    
    Requirement tree：
    
    semantic law graph
    
    Production tree：
    
    code locality / technology / runtime locality
    
    它们应该相互映射，但不是 1:1。
    
    这个原则和 production-test 非 1:1 是同一种思想：
    
    &gt; 不同投影服务不同目的。
    
    
    
    
    ---
    
    第七十六章：最终 architecture 的三个图
    
    以后架构师不再只看目录树。
    
    要同时看：
    
    图 A：Semantic Ownership Graph
    
    Requirement Owner → Requirement Owner
    
    图 B：Production Dependency Graph
    
    Module Owner → Module Owner
    
    图 C：Runtime Capability Graph
    
    Authority
    → Derived Capability
    → Port
    → Physical Resource
    
    如果这三张图互相矛盾，架构就有问题。
    
    
    ---
    
    第七十七章：再加第四张图——Proof Graph
    
    WHAT law
    → static proof
    → pure proof
    → temporal proof
    → adapter proof
    → long stroke
    
    这就是生产与测试之间真正的连接。
    
    不是文件镜像。
    
    
    ---
    
    第七十八章：最终“完美模块”的定义
    
    打开任何一个 production module，我希望读者能在十分钟内回答：
    
    为什么存在？
    谁拥有？
    输入是什么？
    输出是什么？
    它知道哪些 foreign concepts？
    这些 concepts 是否是 public contracts？
    它建立哪些 facts？
    它颁发哪些 capabilities？
    它消费哪些 capabilities？
    它产生哪些 effects？
    它写哪些 durable facts？
    它失败意味着什么？
    crash 后怎样恢复？
    它的 law 在哪里？
    proof 在哪里？
    谁阻止别人复制它的知识？
    
    全部能回答：
    
    教科书级。
    
    
    ---
    
    第七十九章：最终“完美 CE”的定义
    
    一眼看起来应该像：
    
    taskResult {
        let! evidence = observe ...
    
        let decision = Domain.decide evidence
    
        match decision with
        | Done witness -&gt;
            return witness
    
        | Need capabilityRequest -&gt;
            let! capability = Admission.grant ... capabilityRequest
            let! receipt = Effect.execute capability
            return! continueFrom receipt
    }
    
    而不是：
    
    task {
        let! state = getEverything()
        if state.Flag1 then
           if state.Stage = ...
    
    
    ---
    
    第八十章：最终“完美 Decorator”的定义
    
    一眼可回答：
    
    what port?
    what owner?
    what trace relation?
    what state?
    what failure?
    what proof?
    
    而且：
    
    没有 global registry
    没有 dynamic middleware
    没有 magic ordering
    
    
    ---
    
    第八十一章：最终“完美 Capability Transformer”的定义
    
    例如 BorrowingCapacity。
    
    应该可以写：
    
    Base resource truth:
        CapacityLedger
    
    Context:
        session lineage
    
    Derived authority:
        borrower may temporarily use exact lender occurrence
    
    Invalidation:
        owner recall
    
    Fence:
        provider step boundary
    
    Isolation:
        no cross-provider credit
    
    Knowledge owner:
        BorrowingCapacity only
    
    然后 architecture gate 证明：
    
    &gt; 这些知识不在任何其它模块重复出现。
    
    
    
    这就是教材。
    
    
    ---
    
    第八十二章：最终“完美 Event Owner”的定义
    
    业务 owner：
    
    EventEnvelope -&gt; Current -&gt; Result&lt;Current, SemanticError&gt;
    
    或者等价 single-event oracle。
    
    不能拥有：
    
    history
    iteration
    replay cursor
    file
    Git blob
    remote
    
    CanonicalIntegrator 拥有：
    
    ordered history
    integration frontier
    replay
    live integration
    rule registry
    
    这和你们现有 DURABLE-EVENTS-019 已经完全一致。
    
    所以这部分不要过度设计。
    
    
    ---
    
    第八十三章：最终“完美 Physical Adapter”的定义
    
    它可以非常脏：
    
    Fable obj
    JsInterop
    OpenCode field names
    Node APIs
    Git plumbing
    
    没关系。
    
    要求：
    
    脏知识向内不泄漏
    
    并通过：
    
    typed observation
    receipt
    port outcome
    
    收敛。
    
    Host-boundary 当前也已经把目标描述成：
    
    &gt; 换一个 Host，只要 adapter 提供同等 snapshot/coarse wake/transform/tool/session API/identity observation
        capability，其它 participant/mission/durability WHAT 不变。
    
    
    
    这就是 anti-corruption boundary 的正确姿态。
    
    
    ---
    
    第八十四章：不要追求 100% Pure
    
    物理系统里：
    
    mutable
    Dictionary
    HashSet
    locks
    TaskCompletionSource
    ports
    registries
    
    本来就应该存在。
    
    你们自己的 cross-callback gate 已经识别：
    
    pty
    timer
    waiter
    single-flight
    quiescence permit
    process handle
    socket
    cancellation
    resource
    
    这些合法 physical capability 类别。
    
    真正目标是：
    
    &gt; mutable 只表达现实中确实存在的可变资源，而不是程序员脑中的 workflow position。
    
    
    
    这才是 Functional Architecture，不是 Pure FP 宗教。
    
    
    ---
    
    第八十五章：不要把所有 Result 都领域化
    
    最终 error taxonomy 至少三种：
    
    Domain Rejection
    Physical Failure
    Invariant Violation
    
    Domain：
    
    Result&lt;&#39;T, DomainError&gt;
    
    Physical：
    
    通常由 port contract 映射。
    
    Invariant：
    
    fatal / poison / process termination
    
    不能为了“函数式”全部：
    
    Result&lt;_, string&gt;
    
    然后继续运行。
    
    尤其 durable semantic invariant break，当前设计已经正确规定 current process fatal。
    
    
    ---
    
    第八十六章：推荐最终命名规范
    
    不是硬 gate 全扫，但作为 review vocabulary：
    
    Evidence
    
    Observation
    Evidence
    Snapshot
    Receipt
    
    Witness
    
    Witness
    Proof
    Confirmed...
    Accepted...
    Established...
    
    Capability
    
    Permit
    Lease
    Claim
    Capability
    Handle
    
    Decision
    
    Decision
    Disposition
    Outcome
    Classification
    
    Physical resource
    
    Port
    Host
    Adapter
    Runtime
    Gate
    Registry
    
    Semantic operation
    
    用业务动词：
    
    publishEventually
    continueAfterConfirmedFailure
    ensurePerfectConfirmed
    
    避免：
    
    Manager
    Helper
    Processor
    Handler
    Utils
    Common
    
    除非其业务语言确实如此。
    
    
    ---
    
    第八十七章：100 个问题中的几条需要明确“拒绝题设”
    
    为了防止这套重构再次教条化，有几题的前提不能直接接受。
    
    “Witness 必须序列化？”
    
    否。
    
    跨 crash 必须存在的 proposition，其 durable evidence 必须足够重建 witness。
    
    
    ---
    
    “Capability 必须永远不可序列化？”
    
    对当前 process authority 默认是。
    
    但这是 architecture choice，不是 capability theory 的数学定义。
    
    Wanxiangshu 当前这类 physical authority 应 process-local。
    
    
    ---
    
    “所有 Projection query 必须 O(1)？”
    
    否。
    
    按 performance law 决定。
    
    
    ---
    
    “Transparent Decorator 可以随便加？”
    
    否。
    
    它仍需证明：
    
    business observational equivalence
    
    而且 diagnostics 失败策略必须定义。
    
    
    ---
    
    “所有 retry 都应该藏在 decorator？”
    
    否。
    
    只有 mechanical retry。
    
    Semantic retry 需要 owner law。
    
    
    ---
    
    “Composition Root 不能出现业务分支？”
    
    不能重新决定业务事实。
    
    可以 match owner 已经输出的 topology/domain alternative。
    
    
    ---
    
    “AssistanceHost 应该完全没有状态？”
    
    否。
    
    physical single-flight、subscription、attempt claim gate 可以有状态。
    
    不能有隐式 workflow PC。
    
    
    ---
    
    “所有状态必须 Events -&gt; Fold？”
    
    否。
    
    physical resource / capability authority 不属于 durable history。
    
    
    ---
    
    “SIGKILL 后绝对无歧义？”
    
    只有在外部 effect contract 足够强时才能做到。
    
    否则目标是 deterministic reconciliation + fail-closed ambiguity。
    
    
    ---
    
    第八十八章：整个迁移过程最重要的禁令
    
    这几个事情一定不要做：
    
    ❌ 新建 Workflow framework
    ❌ 新建 global Decorator library
    ❌ 新建 generic Capability manager
    ❌ 新建 giant Ports bag
    ❌ 把所有 Capability 存 EventStore
    ❌ 把所有 Witness 单独写 event
    ❌ 把所有业务 workflow 变成 Event-sourced aggregate
    ❌ 按 LOC 拆模块
    ❌ 追求 production/test 文件镜像
    ❌ 追求 requirement/source 目录镜像
    ❌ 追求 dependency edge 数最少
    ❌ 用 E2E 证明纯 semantic law
    ❌ 为了统一而隐藏真实业务分支
    ❌ 把顺序明确的 composition 改成动态 middleware list
    
    如果这十四件事避免了，重构成功概率会高很多。
    
    
    ---
    
    第八十九章：我建议你把 Model Routing、Quiescence、CanonicalIntegrator 设成“三大正样本”
    
    以后 architecture documentation 不要只写反模式。
    
    固定三个 Reference Architecture：
    
    Reference A — Model Routing
    
    教：
    
    single authority
    base resource
    capability transformer
    knowledge exclusivity
    architecture-negative tests
    
    Reference B — Quiescence
    
    教：
    
    physical observation
    opaque capability
    freshness
    consume-time validation
    one-shot authority
    
    Reference C — Canonical Integrator
    
    教：
    
    single interpreter
    feature-owned oracle
    no feature history reader
    replay/live same program
    durable fact vs process safety
    
    新 feature 设计时先问：
    
    &gt; 我更像 A、B、C 中哪个？
    
    
    
    这样全仓 architecture 会逐渐自相似。
    
    这才是真正值得保留的“Fractal”概念。
    
    不是：
    
    &gt; 所有东西都是 CE。
    
    
    
    而是：
    
    &gt; 同一种 ownership/capability/proof 结构在不同尺度重复出现。
    
    
    
    
    ---
    
    第九十章：重新定义“Fractal CE”
    
    我建议以后不要把“Fractal CE”当 package taxonomy。
    
    把它定义为一个定理：
    
    &gt; 如果一个业务 workflow 被缩成一个具名 operation，那么这个 operation 展开以后仍由 typed
        evidence/results/capabilities、宿主语言 CE、高阶组合和 owner-owned semantic vocabulary 组成；它不会暴露新的 program counter 或要求另一个
        interpreter。
    
    
    
    你们当前 STRUCTURED-WORKFLOW-017 实际上已经是这个定理。
    
    因此：
    
    Fractal CE = composition closure theorem
    
    不是：
    
    Fractal CE = 整个仓库的 owner
    
    这个裁决应该永久钉住。
    
    
    ---
    
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
    
    
    ---
    
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
    
    
    ---
    
    第九十三章：实施顺序只有一个来源
    
    旧的 14 步清单与 wave 顺序全部失效。
    
    原因不是局部建议全错，而是线性计划会把无依赖工作人为串行，并允许 inventory、几个热点、gate、baseline、最后目录 rotation 形成“清单完成”的错觉；它既损失并发，也不能证明 src/Wanxiangshu 每一个 production file 都被真实裁决和迁移。
    
    从现在起，第五十七章《DAG 驱动全仓重构——ready 即执行，无全局 wave barrier》是唯一施工调度来源。
    
    任何后续 issue / todo / agent instruction 可以把第五十七章拆成更小 node/commit，但不得：
    跳过任何 production coverage。
    用目录/旧 wave/团队边界制造假 dependency edge。
    把 report/gate/baseline 当成 migration completion。
    把旧实现删除推给未来节点而保留双路径。
    提前宣布 Debt Zero，而 physical tree / requirements / production callers 仍指向旧拓扑。
    创建第二套线性“实际施工顺序”弱化 DAG 的 ready-frontier 与 exit criteria。
    
    如果第五十七章与其他章节的旧计划性文字冲突，以第五十七章的 node schema、dependency edge law、coverage matrix、per-node cutover protocol 与 ReleaseClosure 为准。
    
    
    ---
    
    第九十四章：为什么这一次“最后才转目录”很重要
    
    你们之前通过 balanced-tree rotations 已经把 production tree 处理得比过去好很多。
    
    但如果现在再次从：
    
    &gt; “这个目录看起来不平衡”
    
    
    
    开始搬文件，会重复原来的循环。
    
    这一次流程应该反过来：
    
    laws
    → owners
    → capabilities
    → dependency edges
    → composition roots
    → proof graph
    → 最后 physical tree
    
    目录树只是结果。
    
    不是 architecture truth。
    
    
    ---
    
    第九十五章：你最初问“答案在哪里”，现在其实可以更精确回答
    
    不是 DDD。
    
    不是 Decorator。
    
    不是 CE。
    
    不是 Event Sourcing。
    
    不是 Capability。
    
    这些都只是不同层的工具。
    
    真正答案是：
    
    把“知识、权力、因果、事实、物理现实、证明”六种关系分开建模，然后只允许它们通过有 owner 的边界组合。
    
    对应：
    
    知识     → Evidence / Witness
    权力     → Capability
    因果     → CE
    事实     → ADT / Event
    物理现实 → Port / Adapter / Gate
    证明     → Proof Ladder
    主权     → Requirement/Capability Owner
    
    一旦这六种东西不再互相冒充，代码自然会开始变简单。
    
    
    ---
    
    第九十六章：最关键的一个未来 code smell
    
    以后看到任何类型同时承担两个以上问题，就停下来。
    
    例如：
    
    type Context =
        {
            State: ...
            Stage: ...
            Permit: ...
            Evidence: ...
            Retry: ...
            Host: ...
        }
    
    问：
    
    这里是不是把：
    knowledge
    authority
    execution position
    physical handle
    揉成了一个包？
    
    这会成为新的 God Object，只是披着 record 的皮。
    
    Functional programming 并不会自动避免 God Object。
    
    一个 25-field immutable record 也完全可以是 God Object。
    
    
    ---
    
    第九十七章：最关键的一个未来 positive smell
    
    理想调用经常会长这样：
    
    let! evidence =
        capability.Observe ...
    
    let decision =
        Owner.decide evidence
    
    match decision with
    | Rejected reason -&gt;
        return Rejected reason
    
    | Admitted witness -&gt;
        let! permit =
            OwnerAdmission.grant current witness
    
        let! receipt =
            port.Execute permit
    
        return Owner.confirm receipt
    
    每一行都能说清：
    
    谁知道什么
    谁决定什么
    谁允许什么
    谁真的做了什么
    
    这就是你追求的“明显正确性”。
    
    
    ---
    
    第九十八章：关于“数学美感”的最后一个提醒
    
    不要追求：
    
    所有接口都有漂亮的同构
    所有 decorator 都结合
    所有 workflow 都满足 Monad 教科书
    
    真实分布式系统有：
    
    partiality
    timeout
    cancellation
    unknown
    crash
    external nondeterminism
    non-idempotent effects
    
    真正数学化的方式不是把这些隐藏掉。
    
    而是把它们放进代数里。
    
    例如：
    
    type PhysicalEffectOutcome&lt;&#39;T&gt; =
        | Confirmed of &#39;T
        | ConfirmedFailed of PhysicalFailure
        | Superseded
        | Cancelled
        | OutcomeUnknown
    
    如果 OutcomeUnknown 真实存在，就让类型说出来。
    
    不要因为它不漂亮而假装不存在。
    
    
    ---
    
    第九十九章：这套路线最值得珍惜的地方
    
    它不会要求你放弃：
    
    F#
    CE
    Decorator
    Higher-order functions
    Event sourcing
    Strong types
    
    恰恰相反。
    
    它会让它们变得更多，但更局部、更有主权、更可证明。
    
    未来你应该看到更多：
    
    taskResult { ... }
    
    更多：
    
    Port -&gt; Port
    
    更多：
    
    Evidence -&gt; Decision
    
    更多：
    
    Witness -&gt; Admission -&gt; Capability
    
    更多：
    
    Resource * Context -&gt; DerivedCapability
    
    但更少：
    
    Manager
    Runtime
    Common
    State
    Context
    Processor
    Middleware
    Controller
    Token
    
    这种无法拒绝新职责的词。
    
    
    ---
    
    第一百章：最终目标图
    
    最后，我认为万象术真正成熟后，从架构上应该呈现成这样：
    
    WANXIANGSHU
                             │
                 ┌───────────┴────────────┐
                 │                        │
          Semantic Constitution      Proof Constitution
          requirement-system         verification-system
          structured-workflow             │
                 │                        │
                 └───────────┬────────────┘
                             │ governs
                             ▼
              ┌──────────────────────────────┐
              │      Semantic Owners          │
              │                              │
              │ finality                     │
              │ review-assurance             │
              │ execution-model-routing      │
              │ interaction-authority        │
              │ durable-events               │
              │ causal-wait                  │
              │ time-capability              │
              │ ...                          │
              └──────────────┬───────────────┘
                             │ publish
                             ▼
                 Evidence / Witness / Ports
                             │
                             ▼
                   Capability Admission
                             │
                             ▼
                  Capability-Passing CE
                             │
                 ┌───────────┼───────────┐
                 ▼           ▼           ▼
           Port Decorator  Transformer  Adapter
                 │           │           │
                 └───────────┼───────────┘
                             ▼
                       Physical World
                             │
                       Receipt / Fact
                             │
                             ▼
                     Universal EventStore
                             │
                             ▼
                     CanonicalIntegrator
                             │
                             ▼
                        Projection
                             │
                        Evidence again
    
    形成一个闭环：
    
    Reality
    → Evidence
    → Knowledge
    → Decision
    → Authority
    → Effect
    → Fact
    → Projection
    → Evidence
    
    而 CE 只是把这条因果链写成可读程序。
    
    这句话我认为可以作为整个重构的最终核心：
    
    &gt; CE 不拥有世界；CE 只编排世界。
    ADT 描述世界；Witness 证明世界；Capability 授权改变世界；Port 接触世界；Event 记住世界；Projection 重新认识世界；Owner
        决定谁有资格定义这一切；Proof 决定我们凭什么相信它。
    
    
    
    当 PluginTransforms 只剩一首显式的组合乐谱，AssistanceHost 缩成 Assistance-owned 的 capability
        workflow，HostSignalBootstrap 只剩物理接线，structured-workflow 只剩组合宪法，而 model routing / quiescence / canonical
        integrator 这种“一个知识一个 owner”的形态遍布全仓时——那时目录树长什么样已经不再重要了。
    
    因为真正的 architecture 已经不再住在文件夹里。
    
    它住在语义所有权图、能力图、因果图和证明图里。

