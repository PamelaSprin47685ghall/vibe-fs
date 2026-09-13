本文件只规定 Agent 如何查找规范、修改仓库和验证交付。产品语义只由 `requirements/<package>/` 定义。本文件引用条款，不复述条款。

## Kolmogorov 宝典

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

1. 工作流程
  - 普通小型修复、重构与测试补充不要求创建 Change；在单次提交内原子闭环。
  → 更新 why → what → 阅读相关的规范文档理解为什么要这么做 → 调整测试 → how → GAP
  → 代码实现 → 检查全绿 → GAP Closed → 结束
2. 两种典型失败
  - 写完才想起看文档
    代码已经按旧语义定型，要么返工，要么把旧语义固化，导致规范与代码偏离。
  - 扎进代码细节，丢掉大局
    症状被修好，条款仍被违反（例如给旧类型补字段、加 adapter、让旧测试继续通过——局部合理，合起来是在维护过渡态）。
3. 交付门禁
  - 涉及行为、持久化、Host/provider、Git、分发或跨包边界时，跑对应 requirement suite 即可，尽量不全量跑。
  - 提交前确认无临时文件、无调试输出、无旧路径残留、无生成物误入。
4. 修改纪律
  - 工作区可能包含用户改动。修改前查看 `git status` 和相关 diff；保留无关改动。
  - 自动提交 git commit，推送 `master`；禁止 force push master。

---

# CLEAN：验证入口精简与增量构建收口方案

调研日期：2026-09-13。代码基线：master，bbddd0c85。

本文是待实施方案，不是已完成记录，也不替代 requirements 中的现行合同。本次交付只增加本文，不修改构建、门禁、测试或 CI。下文的新命令、新接口和预期结果均指实施后的目标；实测结果单独列在第 2 节。

## 1. 先定取舍

这次工作的重点不是把 34 个检查藏到一行输出后面，而是少做不值得做的事。

推荐终态：

1. format-build-test 成为日常验证入口：只读格式检查、少量有效静态检查、可信增量构建、产品回归和必要物理适配器测试。
2. verify:release 成为发布验证入口：在同一条执行链中使用干净全量构建，再跑产品回归、发布专属证明、唯一 Long Stroke 和真实包验证。CI 明确调用这个入口。
3. 删除无业务错误探测力的门禁及其自证测试。剩下的检查共享文件读取和工程清单，不再每个检查启动一个 Node 进程、遍历一次全仓。
4. 不缓存测试成功结果，不先做自动 affected-test 选择，不新建任务图框架。先减少重复计算，再考虑更细的调度。
5. 默认输出只保留阶段、结果、耗时和需要处理的问题。详细测试名、编译日志和耗时分布按需展开；失败信息不能被精简掉。

必须保留的底线是：产物确实对应当前输入，必要测试确实运行，失败不会被吞掉，权限、恢复、持久化、Host、进程和分发边界仍然有能变红的证明。

本方案不再次拆分生产工程，不改产品行为，不引入自建 FCS 扫描，不使用 dotnet build，不引入远程缓存、后台编译守护进程、通用插件式检查框架或全仓 mutation 平台。

### 1.1 优先级

| 优先级 | 工作 | 原因 |
|---|---|---|
| 第一批 | 删除 deadcode 与数量、写法、旧迁移清单门禁；替换逐测试刷屏 | 已有直接成本证据，改动边界相对清楚 |
| 第二批 | 修正构建凭据、新鲜度、失效范围与失败发布顺序 | 默认启用增量前的正确性前提 |
| 第三批 | 合并保留检查的扫描；减少真实语料重复派生；合并零散测试进程 | 去掉实际重复劳动 |
| 最后一批 | 切换日常／发布入口，迁移 CI，删除旧路径 | 避免中途出现一个看似成功但少验了一层的入口 |

不把“剩余门禁必须有多少个”“文件必须减少多少行”“必须快一倍”作为验收条件。检查数量、代码行数都不是质量本身。

## 2. 已核实的现状

### 2.1 当前调用链

package.json 中的入口实际是：

```text
npm run format-build-test
  ├─ npm run format:check
  │    └─ dotnet tool run fantomas --check src/Wanxiangshu
  ├─ npm run check
  │    └─ Wireit → scripts/check.mjs → 34 个串行 Node 子进程
  ├─ npm run build
  │    └─ Wireit → scripts/build.mjs
  │         ├─ build.lock
  │         ├─ 删除 dist
  │         ├─ compileIncremental → 一次全量 Fable 编译
  │         ├─ 重新派生 LoopDetectorEnvelope
  │         ├─ 资源、Surface Manifest、JS linkage 检查
  │         └─ 释放锁
  ├─ requirements/verification-system/tests/run.mjs
  ├─ requirements/verification-system/tests/integration/run.mjs
  │    ├─ 一次 OpenCode warmup
  │    ├─ 11 组 node:test 步骤，组间串行
  │    ├─ distribution/package 子入口
  │    └─ verification-system/harness 子入口
  ├─ requirements/verification-system/tests/e2e/entry.test.mjs
  └─ npm pack --dry-run
```

这里要分清两件事：Wireit 命中缓存时会跳过整个 build；但只要 scripts/build.mjs 真正执行，compileFable 就先调用 resetOutputDirectory(dist)，并拒绝 cached 结果。这条入口仍是发布式全量构建，不是日常增量构建。

因此不能简单说“增量编译没有实现”。增量规划已经在 scripts/lib/owner-compile.mjs 中实现；问题是日常入口尚未接上它，而且接上之前还有产物一致性需要补齐。

### 2.2 本次实测

使用已有正式入口，没有为计时改写测试或增加临时测试程序。机器运行环境为 Node v26.5.0、npm 11.17.0、.NET SDK 10.0.111。package.json 声明 npm@11.12.1、Node >=20；CI 当前实际选择 Node 20。两者不能混为同一个验证环境。

| 命令 | 结果 | 墙钟耗时 | 说明 |
|---|---|---:|---|
| node scripts/check.mjs | 通过 | 21.066 秒 | 绕过 Wireit，测检查本体 |
| node scripts/checks/deadcode.mjs | 通过，debt=0 | 14.505 秒 | 单独测死代码检查 |
| node requirements/verification-system/tests/run.mjs | 3983 项通过，0 失败 | 20.170 秒 | 644 个文件；使用工作区已有 dist |

静态检查报告：748 个生产 .fs 文件、276 个编译分片、27 个 subsystem、2220 条 ProjectReference。requirement-trace 报告 814 条 WHAT、4026 个静态识别的测试登记；这不是本次 unit 实际执行数，不能拿它替代 3983 个运行结果。

requirement-trace 还报告 INSTITUTIONAL-LEARNING-007 缺少 active test 和 HOW proof row。这是同一条款的两个缺口诊断，当前设计允许它们非阻塞。删除 trace 脚本不等于修复或关闭该缺口。

deadcode 的单独耗时约为静态链耗时的 69%。这是两次运行的比值，不是严格剖析分摊；缓存与机器负载会影响数值。但它已足够说明：继续优化小检查的进程启动，收益不如先删除这个低价值重扫描。

unit 的慢项如下：

| 测试 | 本次报告耗时 | 判断 |
|---|---:|---|
| DG-004：runtime envelope 从当前仓库重新派生并核对 | 12.615 秒 | 有重复读取、重复 tokenize 和重复真实语料计算 |
| HOST-BOUNDARY-023：已安装 OpenCode admission 契约 | 6.624 秒 | 真实 Host 契约，不能因慢就删 |
| EMR-014：seeded bounded admission soak | 5.992 秒 | 先检查循环与观测成本，保留公平性／对账命题 |
| JS-SEMANTIC-SURFACE-003：Surface registry | 4.669 秒 | 应拆开实际入口完整性与证明登记治理 |
| EMR-003：process restart 与 capacity 重建 | 4.575 秒 | 真正的重启边界，优先保留 |

各测试耗时之和为 112.2 秒，整条 unit 的墙钟是 20.170 秒。并发测试的耗时不可直接相加后当作用户等待时间，更不能把前三个慢项相加当作可节省时间。

本次未执行 format、重新构建、integration、Long Stroke、pack 或完整 format-build-test。没有冷构建、发布链总耗时和优化后耗时数据。unit 开始时通过的是现行新鲜度检查，不是本方案提出的内容凭据验证。

### 2.3 已确认的浪费和薄弱点

| 位置 | 当前行为 | 处理方向 |
|---|---|---|
| scripts/checks/deadcode.mjs::scanDeadBindings | 每个 private 名字重新扫描整仓拼接文本 | 删除，不为它另造缓存 |
| tests/proof-ladder.test.mjs | 锁死命令原文、数组写法、Wireit 配置和门禁数量下限 22 | 改测实际调度、失败传播、执行完整性 |
| scripts/check.mjs | 34 次进程启动，多份源码／规范树重复扫描 | 保留规则同进程运行，共享本次快照 |
| scripts/build.mjs::compileFable | 实际执行即删 dist | 分出可信增量与显式 clean 模式 |
| tests/support/build-freshness.mjs | 最新源码 mtime 与最新 JS mtime 比较 | 改为输入、输出内容凭据 |
| owner-compile.mjs::compileIncremental | 编译成功即写 manifest，早于生成物和后置验证；写失败被吞掉 | 最终成功凭据由整个构建的唯一属主提交 |
| owner-compile.mjs::detectChangedFiles | 输出判断主要是有 JS 和两个入口存在 | 校验完整输出集合与内容 |
| package.json::wireit.check.files | 未列 scripts/check.mjs、resources/** | 精确补全保留检查的输入依赖 |
| package.json::wireit.format.files | 有 .fs、.fsproj，遗漏 .fsi 和格式配置 | 取消写入式格式缓存；只缓存只读检查 |
| run-inner.mjs | spec reporter 打印每个成功测试 | 换成结构化事件摘要，不过滤人类日志 |
| supervise-node-test.mjs | 每组重复统计说明与 top 5 | 总入口汇总，详细分布仅 profile 时显示 |
| distribution/package/run.mjs | 四个小套件各起一个 supervisor | 合并调度，保持文件进程隔离 |
| distribution/package/install.test.mjs | 名为 install，实际只检查工作区布局 | 改名并明确证据边界，发布另验真实包 |

## 3. 删除的标准与不可删除的东西

### 3.1 什么值得删

一项检查如果只证明以下事实，就不应每次构建都收税：旧文件名没有复活、目录仍叫旧名字、代码仍按某段字符串写、某张问卷填满、测试标题与三份登记表逐字一致、检查数量没有下降、声明旁边有指定格式的解释文字。

名字、路径和源代码文本可以服务于很窄的边界检查，但不能自动升级为行为证明。换个局部变量名就红、换个实现却能绕过的检查，最需要重新审视。

删除一个门禁时一并处理：执行入口、专用配置／baseline、只证明该门禁自身的测试、HOW 映射、证明登记消费者和废弃说明。不保留永远返回成功的壳，不转成默认仍全量跑的 audit，不建一个退休清单让后人继续维护。

### 3.2 什么不能靠“已经多工程”来删

当前编译器契约测试已经明确：Fable 会合并 ProjectReference 源码；internal 不是普通 .NET 程序集防火墙；顶层 private module 也不等于外部工程不可访问。模块内 private binding 和 .fsi 隐藏实现符号才有相应 canary。

证据位置：requirements/structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs。

所以不能用“现在有 276 个工程了”替代以下证明：能力不能伪造、越权调用被拒绝、恢复不能制造成功、持久化失败不能推进权威状态、资源读取不能越过属主、源码合并后私有实现仍不可见。

也不能把“合并 impact 编译成功”当作所有独立工程声明都完备。一个更大的闭包可能恰好补齐另一个工程漏掉的 provider。新增或改变独立边界时，仍要编译该边界自己的声明闭包；普通变更只需一次受影响并集，不应逐一重编全部分片。

### 3.3 测试删减只按命题，不按文件大小

先问一个测试让哪个真实错误变红。能直接发现恢复、隔离、并发、序列化、Host 或分发错误的测试保留。若只是读取源码并断言旧名字、旧数组长度、旧注释存在，删除或替换。

同一个文件可能同时包含这两种测试，必须逐项处理，不能整目录删除。旧 JSON 合同册中指向的行为测试也不能随合同册一起删。

## 4. 34 个静态入口的处置表

表中“退役”指删除该独立脚本及其仅为自证而存在的附件；“改为行为证明”要求替代测试先落地并能变红，再撤原规则；“合并”要求规则迁完后删除旧入口。不是先注释掉，等待以后补。

这张表是按已读实现、调用关系和实跑输出作出的实施决策。对包含多种规则的大脚本，执行时仍须按第 4.1 节逐条归类；不能把表中建议当作已经逐条证明其全部规则冗余。

| # | 当前 scripts/checks 下的入口 | 决策 | 最终保留的实质 |
|---:|---|---|---|
| 1 | spec.mjs | 退役全仓文书门禁 | WHAT 保持规范权威；不再每次构建检查文档全套布局、历史路径和散文格式 |
| 2 | architecture.mjs | 拆解后合并 | 工程输入完整性并入 compile-shards；实际依赖方向、资源 I/O 越界留下；旧迁移名字退出 |
| 3 | participant-identity-boundary.mjs | 保留并收窄 | logical run／session 身份不能混用、权威身份创建边界；去掉能由类型与行为测试重复证明的拼写约束 |
| 4 | provider-projection-boundary.mjs | 保留并收窄 | provider 只能看到其应有投影，不能旁路读取内部权威状态 |
| 5 | subsystems.mjs | 保留，作为共享工程清单入口 | 唯一源文件归属、签名配对、闭包完整、DAG、基础设施依赖方向 |
| 6 | dsl-ownership.mjs | 退役启发式治理，迁走必要边界 | 大 DU 阈值、解释注释、状态形状与复杂度推断不再管；越权／副作用边界交类型、窄扫描与行为测试 |
| 7 | authority-boundary.mjs | 退役权威登记册式检查 | 能力创建／消费／不可持久化等命题用真实端口、序列化和 private/.fsi canary 证明；不靠类型后缀和注释获得权威 |
| 8 | fsharp-control-pyramid.mjs | 禁止退役 | 缩进和嵌套数量确实能裁决业务表达；但需要精简输出 |
| 9 | plugin-transforms-invariant.mjs | 改为组合顺序行为测试 | 同一输入的投影、处理次序、次数与禁止旁路；不锁死装配源码文字 |
| 10 | interaction-repair-invariant.mjs | 改为状态／时序回归 | 非终态不能误修复、in-flight 不重复、合法修复不制造 exhaustion |
| 11 | retry-owner.mjs | 保留最小属主约束 | 只允许已定义重试层裁决；重试次数、终止与副作用唯一性由行为证明 |
| 12 | enforcer-bounds-owner.mjs | 保留并合并扫描 | 边界值和拒绝决策只能有一个生产属主；不在每个消费者复制公式 |
| 13 | hook-policy.mjs | 保留真实 Host 接线契约 | 实际 hook 集合、注册、策略齐备；不固定与事实无关的数量常量 |
| 14 | semantic-decorator-invariant.mjs | 退役 | 删除精确源码片段及多字段说明登记；保留回调次数、顺序、取消、失败短路的现有行为测试 |
| 15 | deadcode.mjs | 优先直接退役 | 当前词法计数不构成可靠死代码证明；无需替它发明另一个近似分析器 |
| 16 | p0-recovery-join.mjs | 按命题迁完后退役 | aborted 不能伪装 joinable completion、丢失恢复证据不能变成功；保留 typed result／恢复行为反例 |
| 17 | causal-wait-boundary.mjs | 保留必要读取边界，移走自测输出 | 因果等待只能消费其属主事实；合成反例进入正式 unit，不在每次 CLI 内逐项打印 |
| 18 | cross-callback-pc.mjs | 退役启发式词法门禁 | 当前输出已承认 exemptions 和 coverage gaps；跨回调因果问题交取消、恢复、重放测试 |
| 19 | session-ownership-ratchet.mjs | 退役 | 删除 AttachmentKind 名字清单和烟雾问卷；保留实际会话归属、复用、隔离测试 |
| 20 | js-surface-gate.mjs | 退役旧角色／文件名禁令 | 是否可调用、生成是否完整由真实工具注册和 Surface 边界测试证明 |
| 21 | capability-isomorphism-gate.mjs | 合并到能力／工具注册契约 | 从生产 registry 推导 Host schema 与 JS 投影，比较实际结果；不再 grep 旧实现 token |
| 22 | unified-store-gate.mjs | 保留最小持久化越界守卫 | 唯一 durable substrate、禁止旁路物理写入；历史模型名字黑名单退出 |
| 23 | external-effect-reconciliation.mjs | 退役人工合同册验证 | 保留 11 类外部效果真实的请求、接收、歧义、恢复、幂等反例；不以合同 JSON 完整代替这些证明 |
| 24 | tool-referential-integrity.mjs | 保留 | ToolSpec 名字唯一，实际工具注册与调用暴露一致；旧工具名禁令单独删除 |
| 25 | provider-leak-gate.mjs | 保留且限定投影出口 | 内部控制字段／协议不得泄漏给 provider；不要扩大成全仓文风检查 |
| 26 | llm-facing-format-gate.mjs | 保留表示边界，去掉写法束缚 | 同一种合成表示走同一生产编码路径；输出结构用精确行为断言 |
| 27 | language-parity-gate.mjs | 保留资源契约部分 | 双语文件、协议标识、占位符一致；语义 anchor 词汇清单和作文深度考核退出 |
| 28 | prompt-depth-ratchet.mjs | 退役 | prompt 是否有指定词和指定解释层数，不是产品正确性证明 |
| 29 | provider-prose-ownership.mjs | 退役 prose 治理和 baseline | 保留真实 provider leak 与序列化边界；不维护英文散文命中债务 |
| 30 | g4r-ce-vocabulary.mjs | 退役迁移词汇门禁 | 因果计时与时间注入由测试证明；不继续搜旧 controller 名字；raw-time 环境时间扫描器迁入 `scripts/lib/raw-time-scan.mjs`，由 TIME-004 正式测试直接消费，不再作为常驻门禁 |
| 31 | test-boundary.mjs | 与下一项合并 | 测试不得深层导入未公开的生成实现 |
| 32 | js-boundary-gate.mjs | 保留单一实现 | 一份实际 Surface 入口清单、一遍 JS 语法扫描；删除已清零的兼容债务治理附件 |
| 33 | e2e-watchdog-feed.mjs | 并入 runner 的小范围回归 | 顶层场景不能拿原始流量喂狗；保留 watchdog 的真实因果与挂死测试 |
| 34 | requirement-trace.mjs | 退役精确标题双向登记门禁 | 保留产品测试和问题记录；不再以 WHAT↔HOW↔test 标题全等计算“证明权威” |

### 4.1 混合脚本怎样拆

不要把整份大脚本平移到一个新文件后宣称精简。以 architecture、dsl-ownership、p0-recovery-join、unified-store 为例，每条规则只有四种归宿：

| 规则性质 | 归宿 |
|---|---|
| 编译器能够直接判定的声明／可见性错误 | 正常编译和最小编译器 canary |
| 真正的静态物理边界，如禁止非属主执行资源 I/O | 保留的边界检查，读取共享源码快照 |
| 运行行为，如失败后不能完成 join | owner 包内的产品测试 |
| 命名、注释、退役路径、阈值、问卷 | 删除，不转移 |

每个迁移后的行为命题至少保留一个合法输入和一个精确反例。反例必须调用生产 Surface 或真实边界，不在测试中复制一份算法再对自己断言。

### 4.2 不是只改 check.mjs 数组

以下消费者必须一起迁移，否则表面上删了门，背后仍在计算原来的文书图：

```text
scripts/lib/requirement-trace.mjs
  ├─ checks/authority-boundary.mjs
  ├─ checks/semantic-decorator-invariant.mjs
  ├─ checks/external-effect-reconciliation.mjs
  ├─ checks/js-surface-manifest.mjs
  └─ checks/requirement-trace.mjs
```

前三者完成行为替代后退出。js-surface-manifest 保留真实源码／产物／测试导入边界，删除对精确 HOW 行和标题的证明权威计算。最后再删除无人消费的 trace 实现、proof-levels.json 和专属测试；不能先删库，再靠空结果或白名单让消费者绿。

authority-contracts.json、external-effect-contracts.json、session-ownership-matrix.json、provider-prose-ownership-baseline.json、deadcode-baseline.json，以及已清零的 JS boundary 债务附件，都应按实际引用完成同批清理。release-closure-nodes.json、owner-impact-corpus.json、subsystems.json 等名字相似的文件不能顺手删除：必须先查使用者，仍定义当前编译或实际测试输入的保留。

## 5. 先修订现行合同，再改变执行含义

现行规范确实要求固定整串命令、clean build、proof-level 登记及完整 trace。它们不是改一段 npm script 就能合法绕开的实现细节。本次只提出修订方案；实施时将下面的合同调整与对应代码／测试作为同一项工作对齐。

| 文件与条款 | 修订要点 | 不改变的部分 |
|---|---|---|
| verification-system/WHAT.md：001 | 固定“必要证明层”的语义，不固定 shell 原文；明确日常与发布入口；删除精确标题的层级授权登记 | 发布仍有完整证明顺序，每个实际步骤恰一个执行属主 |
| verification-system/WHAT.md：008 | 新鲜度改为输入与输出内容相符；允许可信增量 | 测试必须消费当前生产字节，不能偷跑旧产物 |
| verification-system/WHAT.md：010 | 允许有依据地撤销低价值检查；不再以门禁个数或旧 baseline 的存在限制删减 | 真实产品断言不能为变绿而放松，范围调整须明确说明 |
| verification-system/HOW.md | 更新入口、缓存、报告、构建凭据及测试落点 | 不把计划写成已经验证过的事实 |
| distribution/WHAT.md：005 | 日常允许增量；发布明确 clean；两者都要求同一批已验生产字节 | 不重新编译后直接发布未经测试的新 dist |
| distribution/WHAT.md：007 | 发布属主改为 verify:release；从只展示 dry-run 改为一次真实 pack、内容与解包消费验证 | 代码和资源的完整交付闭包 |
| requirement-system/WHAT.md：006、016、017、018 | 去掉全仓文书布局、依赖骨架与精确测试标题的常驻构建门禁；HOW 保留导航作用 | WHAT 的规范权威、命题归属、ID 稳定、真实问题不能被假称关闭 |
| js-semantic-surface、verification-system 中的 Surface 合同 | 保留公开契约入口与实现隔离，删除精确证明登记授权 | 不开放深层 dist 私有导入，不复活兼容 facade |
| degeneration-guard 的 DG-004 及 HOW | 日常允许经当前语料内容凭据复用；真实全仓独立重算放在发布层 | 数值仍来自当前仓库，不回填手写数值快照 |
| 各退役门禁的 owner HOW | 以实际行为测试替代旧 checker 落点，删除已失效的精确引用 | 产品命题和已经存在的有效回归 |

若实施者发现某条拟删规则还承载未覆盖的产品命题，应先补该命题的测试，不能把“用户要求精简”解释为允许降低产品正确性。

proof-ladder.test.mjs 中以下测试直接撤销，而不是修改常量继续维持旧观念：

- wired gate count has a non-shrinking floor。
- 对 format-build-test 整串字符串、Wireit.files 数组原文和 checks 数组写法的深度全等。
- 对 process.exit 表达式原文的正则检查。
- checks 目录必须等于 wired 集合加一张例外清单的自我治理。

替代测试验证真实执行结果：阶段失败后停止、缺失程序非零、release 包含唯一物理入口、没有重复 warmup／pack、选定套件发现完整、构建失败不能继续 unit。不要再用另一段字符串解析器检查新的编排器写法。

## 6. 目标命令与执行顺序

### 6.1 package.json 目标形状

以下是接口设计，不是已经存在的命令：

```json
{
  "scripts": {
    "format-build-test": "node scripts/verify.mjs",
    "verify:release": "node scripts/verify.mjs --release",
    "format": "dotnet tool run fantomas src/Wanxiangshu",
    "format:check": "wireit",
    "check": "wireit",
    "build": "node scripts/build.mjs",
    "build:clean": "node scripts/build.mjs --clean"
  }
}
```

取消 build 外层 Wireit。构建的失效判断由现有 owner-compile 及统一构建凭据负责，不让外层文件缓存与内层增量状态争夺所有权。Wireit 仍可服务于无输出的只读格式／静态检查，不因要收口构建就替换整个工具链。

取消 format 的写入式缓存。用户主动执行 format 就运行格式化；日常总入口只运行 format:check，不修改提交内容。

### 6.2 日常入口

```text
format-build-test
  1. format:check
  2. check
  3. build：无变化复用；有变化按已证明的增量范围编译
  4. unit：全部保留下来的产品／验证基础设施回归
  5. integration：必要 Host、Git、进程、持久化、资源与工作区分发契约
  6. 核对本次构建 generation 与输入未变化，输出总结果
```

只减少不需要每次支付的证明，不偷偷把 unit 改成“只跑改动目录”。修改实现即使不改变 .fsi，也可能影响远处消费者的运行行为，所以第一版仍跑全部保留的 unit。

### 6.3 发布入口

```text
verify:release
  1. format:check
  2. check
  3. build --clean：真实全量 Fable 编译，不接受 no-op
  4. 与日常相同的 unit
  5. integration --release
       日常物理契约
       + 编译器边界 canary
       + 全仓 LoopDetectorEnvelope 独立重算证明
  6. 唯一 Long Stroke
  7. verify-package：一次真实 pack、解包与消费验证
  8. 再核对 generation、输入与打包字节一致，输出总结果
```

不要实现成“先调用 format-build-test，再 clean build，再重跑所有测试”。两个入口共享同一个固定阶段定义，构建模式不同，发布增加少数步骤；一次调用只构建一套待验证字节。

OpenCode warmup 仍由 integration 的唯一属主执行一次。没有用到 Host 的纯资源检查不需要为自身增加 warmup。Long Stroke 不分池、不重试、不启动第二个世界。

### 6.4 CI 与单独运行

.github/workflows/ci.yml 的最后一步改为 npm run verify:release。若将来另设快速 PR 检查，它不能替代合并／发布所需的完整结果；本轮不增加一套复杂 CI 分流。

已有 requirement 文件的定向执行能力保留。定向执行必须显示范围，不能输出“全仓验证通过”。完整入口拒绝 TESTS_MJS_FILES、跳过新鲜度、意外 name pattern 等缩窄验证范围的外部设置；runner 自测所需的受控 fixture 调用仍通过自己的正式测试入口进行。

本地 Node 26 通过不能替代 CI 的 Node 20。新 reporter 与子进程协议先以 Node 20 可用接口设计，并在两种环境验证；不为本次精简顺带升级最低 Node 版本。

## 7. 程序落点：复用已有工具，只补缺失边界

### 7.1 文件职责

| 文件 | 改动 | 唯一职责 |
|---|---|---|
| scripts/verify.mjs，新建 | 解析 release／verbose／profile，按固定阶段调用现有入口 | 整次验证的执行顺序与最终结论 |
| scripts/check.mjs，改造 | 导入保留检查，创建一次 context，返回结构化诊断 | 静态检查编排，不再维护 34 个子进程 |
| scripts/lib/check-context.mjs，新建 | 本次调用内缓存文件列表、文本和工程清单 | 同一输入只读一次，不跨运行持久缓存 |
| scripts/build.mjs，改造 | 支持普通／clean，持有写锁，运行编译、派生和后置验证 | 生产构建的唯一成功提交点 |
| scripts/lib/owner-compile.mjs，改造 | 修正失效范围与输出维护，返回编译结果 | Fable 编译规划与执行，不自行宣布整次构建成功 |
| scripts/lib/compile-shards.mjs，复用 | 提供检查与规划共用的清单数据 | 工程、源文件、签名与引用的唯一事实集合 |
| scripts/lib/build-state.mjs，新建或从 owner-compile 精确抽取 | 统一读取、验证、失效、提交现有 build-manifest | 构建与测试共用的新鲜度规则 |
| scripts/lib/derive-loop-detector-envelope.mjs，改造 | 少一次排序，修正并行度 API，允许内容相同复用 | 当前语料到唯一 envelope 产物的派生 |
| tests/support/build-freshness.mjs，改造 | 调用 build-state 的只读验证 | 适配测试入口，不再自建 mtime 规则 |
| tests/support/run-inner.mjs，改造 | 保留进程隔离和环境隔离，调整 reporter／错误处理 | node:test 事件源 |
| tests/support/compact-reporter.mjs，新建 | 从结构化事件生成默认／详细输出 | 显示，不决定通过或喂 watchdog |
| tests/e2e/support/supervise-node-test.mjs，改造 | 保留外部监督，返回准确结果与诊断 | 测试进程生命期、完整性、挂死和失败 |
| tests/support/integration-node-test-steps.mjs，改造 | 只为少数发布专属文件标记 releaseOnly | 测试文件唯一接线表，不登记每个测试标题 |
| scripts/verify-package.mjs，新建 | 一次 pack、检查内容、解包后消费 | 真实发布 artifact 验证 |

表中的 tests/support 等缩写均位于 requirements/verification-system/tests 下；包专属测试继续放在自己的 requirements/<owner>/tests 中。

不再额外建立 Scheduler、GateRegistry、ProofStore、CacheProvider、PolicyEngine 等类。固定的几步流程用数组和函数足够。不要为一个输出行引入日志级别配置系统。

### 7.2 检查接口

保留检查统一返回数据。模块导入不运行 CLI、不打印成功、不调用 process.exit、不跑内部 fixture。

```js
/**
 * @typedef {{
 *   code: string,
 *   path?: string,
 *   line?: number,
 *   message: string
 * }} CheckIssue
 */

// 示例接口；不要求为每条规则再包一层类。
export function check(context) {
  return {
    issues: [],
  }
}
```

scripts/check.mjs 先建立工程结构，再执行依赖该结构的规则。清单损坏、源目录缺失、读取失败立即失败，不将空集合交给后续检查伪装为“没有问题”。结构正常时，彼此独立的检查可以一次报告全部违规，按 path、line、code 稳定排序。

 CLI 最外层根据结果设置退出码。常规违规为 1；参数错误为 2；信号退出保持 130／143。不可启动、不可读取、解析失败不能返回成功。内部检查不自行结束整个进程，便于组合和回归测试。

### 7.3 检查 context 的范围

推荐只包含这些实际需要的能力：

```text
createCheckContext(root)
  sourceFiles()                 一次发现 .fs/.fsi/.fsproj
  testFiles()                   一次发现测试与支持模块
  resourceFiles()               一次发现当前资源
  readText(relativePath)        Map 缓存本次读取
  readFSharpCode(relativePath)  保留行位置的去注释视图，仅按需计算
  compileInventory()           一次解析工程与归属
```

文件列表使用一致的排序和忽略规则。新加入、尚未 Git add 的生产源文件也必须参加“磁盘源文件是否纳入工程”的检查；不能因为 Git 没跟踪就让未编译的源文件消失。

词法工具复用已有 scripts/lib/fsharp-source.mjs 等实现，先核对其支持的字符串／注释形式，不再复制多个略有差异的去注释器。不把有限词法扫描宣传成 F# 类型或控制流分析。

context 只活在一次检查中。第一版不做持久化 AST、不做跨运行依赖追踪、不造文件 watcher。外层 Wireit 已能跳过完全未变化的只读检查；内层只需避免同一趟重复读盘。

### 7.4 工程清单不要重写两套

readCompileShardInventory 已经检查唯一归属、sibling .fsi、缺失引用、DAG、aggregate 与磁盘输入集合。architecture 中同类检查应迁到这里，保留对 aggregate 重复项、必要顺序等真实约束，不在删除旧实现时丢失它们。

owner-compile 的 XML 读取比简单清单正则承担更多输入校验。共享工程模型时先保留这些拒绝能力，不为统一接口改用一个更弱的正则解析器。正确顺序是抽出同一份已验证数据，再让两边消费，而不是同时重写两个解析器。

新数据至少包含：projects、sourceOwner、forwardReferences、reverseReferences、aggregateOrder。构造成本为文件读取加 O(V + E)，其中本次 V=276、E=2220。一次 delta 求根集合，再用 visited 集合做闭包并集；不能给每个根重新生成完整闭包再反复合并。

当前 planImpactCompile 已有大部分线性图算法，最值得改的是重复解析与正确的失效边界，不是把 DFS 换一个名字。

### 7.5 几个需要固定的内部接口

接口应小到可以直接注入假文件系统／假命令执行器进行正式单元测试，而不需要为每个失败场景启动整个仓库。

```text
build({ root, clean, verbose })
  → { mode, reason, generation, changedSources, affectedShards, elapsedMs }

assertBuildFresh({ root })
  → { generation, compilerInputDigest, generatedInputDigest, artifactInputDigest }
  失败时抛出含 code／path／reason 的错误

commitBuildState({ root, state })
  只在完整构建成功后调用；写入失败抛错

superviseNodeTest({ files, label, ...现有监督参数 })
  → { selectedFiles, passed, failed, cancelled, skipped, todo,
      wallMs, drained, exitCode, signal }

verify({ root, release, verbose, profile, runStep })
  固定阶段顺序；任一失败即不启动后续阶段
```

reason 解释实际模式选择，例如 no-change、source-change、generated-input-change、toolchain-change、missing-output；它服务诊断，不另建需要多处同步的政策登记表。

verify 在任何检查开始之前，对本次格式／静态／测试的有效输入集合取得一个内存快照；结束时比较路径集合与内容。新发现的未跟踪测试文件也属于验证输入，不能只靠生成器的 Git 跟踪语料清单代表测试范围。这个快照只防止本次执行期间被编辑，不持久化、不缓存测试结果。

测试 runStep 时注入一个只记录启动事件与返回结果的函数。断言实际 trace 中 build 在 unit 前、release 才启动 Long Stroke、某阶段失败后后续未启动；不要读取 verify.mjs 源码来查字符串。

## 8. 增量构建：先可信，再接入默认入口

### 8.1 分开三类输入

同一个仓库变更不一定需要重编 F#，但可能需要重做生成物或资源验证。

| 输入集合 | 例子 | 影响 |
|---|---|---|
| compilerInputs | .fs、.fsi、工程、props/targets、global.json、Fable 工具配置、编译脚本及其依赖、相关依赖锁定信息 | 编译范围与生成 JS |
| generatedInputs | 当前 Git 跟踪的 source/document 路径集合及工作区字节、派生器及其依赖、tokenizer 版本 | LoopDetectorEnvelope |
| artifactInputs | 资源、package.json 的分发面、Surface 入口与后置检查实现 | 资源与产物闭包验证 |

上述集合可以重叠。例如资源 Markdown 属于语料，改它会使 envelope 失效；package-lock.json 本身不是语料，但 tokenizer 版本来自依赖解析，所以锁文件变化不能被排除在派生器的失效判断外。

完整输入集合不要只依赖手写的几个主脚本名。新增 build-state、共享工程解析、派生 helper 后，它们都属于对应工具输入。第一版允许对 scripts/lib 的相关构建工具采用略宽的明确集合，宁可多算一次，不要漏掉真正的依赖。

global.json、实际选择的 SDK／Fable 版本、配置模式 Debug 都要纳入工具身份。依赖安装以锁文件和 npm ci 为基础；本轮不为 node_modules 实现逐文件远程可信供应链缓存。

### 8.2 沿用一个构建 manifest

复用 .fable-build/build-manifest.json，升级 schema。不另外放 compile-success.json、test-freshness.json、envelope-ready.json 三份各自宣布成功的文件。

建议内容如下。字段是边界设计，最终名称可按现有代码风格调整：

```text
schema
rootIdentity / aggregatePath / outputDir
generation
compiler
  configuration
  toolIdentity
  inputDigest
  inputs: sorted(relativePath, contentHash)
generated
  inputDigest
  selectedInputs: sorted(relativePath, contentHash)
  generatorIdentity
artifacts
  inputDigest
  inputs: sorted(relativePath, contentHash)
outputs: sorted(relativePath, contentHash)
sourceOutputs: 当前已证明输出映射所需的最小信息
```

generation 只在实际产物发布成功时生成新值；无变化复用时保持不变。它用于发现测试期间发生的构建替换，不是用来替代内容验证。

mtime 和 size 可以保留为诊断信息，但不能作为成功依据。源码变了再恢复 mtime、两个文件恰好同 size，都必须由内容 hash 判定。当前 computeFileHash 的逐文件 SHA-256 值得保留，不应“优化”为仅比较时间。

路径使用仓库相对路径进行集合比较；工程与输出根身份明确验证，不能将一个 scratch 工程的成功记录误认成生产 dist。摘要采用已存在的 canonical 编码或有明确长度边界的编码，不能直接拼接可能产生歧义的字符串。

outputs 覆盖完整生产产物集合，而不只是 Plugin.js 与 Sphinx/ServeEntry.js。缺一个叶子模块、增加一份已删除源码的旧 JS、修改某个 JS 内容，都应使凭据失效。资源作为输入和实际分发集合检查，不在 dist 里复制一份。

### 8.3 成功发布顺序

构建步骤必须按以下因果顺序执行：

```text
取得 build.lock
  → 读取并验证旧 manifest
  → 获取当前输入快照
  → 决定 no-op / focused / full
  → 若要修改产物，先使旧成功 manifest 失效
  → 编译与清理该模式应更新的输出
  → 派生或核验 envelope
  → 检查入口、资源、Surface、ESM linkage
  → 核对构建期间输入未变化
  → 收集最终输出集合与 hash
  → 临时文件写入完整 manifest，原子 rename
释放 build.lock
```

任何一步失败都不能留下可以宣称“当前构建成功”的 manifest。旧 dist 可以保留给人排查，但测试必须拒绝消费。manifest 写入失败是构建失败，不再 catch 后继续绿。

compileIncremental 只返回编译结果与本次使用的输入信息；scripts/build.mjs 是唯一最终提交者。compileOwnerProject 的 scratch success marker 只描述该 scratch 编译，不能承担生产构建凭据的职责。

当前 compileOwnerProject 即便发现有效 scratch marker 仍会执行 Fable；不要把它误读成已经缓存了整个编译结果。no-op 的主要判定发生在 compileIncremental 前段。保留现有 restore assets 复用，不把 NuGet restore 也无谓清空。

### 8.4 模式选择

| 情况 | 目标模式 |
|---|---|
| manifest 完整，三类输入和输出均一致 | no-op，不启动 Fable，不重复 tokenize |
| 只有非编译语料变更 | 保留编译 JS，只更新 envelope 并做必要后置验证 |
| 只有不参与语料的资源／打包元数据变更 | 按真实输入分类验证；不凭文件扩展名武断跳过 |
| 普通实现／签名变更且影响和输出映射均可证明 | 一次 focused 并集编译 |
| 工程、工具链、拓扑变更，源文件增加／删除／重命名 | full，并清理旧 dist |
| manifest 损坏、输出缺失／被改写／多出陈旧模块 | full，并清理旧 dist |
| 影响比例超过现有 fullThreshold | full；暂不改变 0.6，待测量后再讨论 |
| --clean | 无条件清理并真实 full compile，不能返回 cached |
| 输入非法、缺 provider、图有环、读取失败 | 直接失败，不用 full compile 掩盖结构错误 |

失效时转 full 是事前明确的构建模式选择，不是失败后自动重试至通过。Fable 编译失败、测试失败、未知异常必须原样失败。

detectChangedFiles 应比较 oldPaths 与 currentPaths 的集合差，不只检查旧文件是否从磁盘消失。从工程移除但仍留在磁盘的文件，同样是输入拓扑变化，并应被源文件归属检查拒绝或明确处理。

### 8.5 不能把 .fsi 未变当作输出不变的充分条件

当前规划将稳定 .fsi 对应的 .fs 修改限制为 owner 的正向依赖闭包。这对某些非内联实现可能成立，但不是所有 Fable 输出的通用保证。

仓库实际存在公开 val inline，如 Composition/Durable/ChatExecutionFact.fsi、Composition/Durable/CompanionFact.fsi、Host/Fact.fsi、Change/Fact.fsi；还有 Literal。F# inline 可能把实现展开到调用方。[E5]

因此这里有一个需要正式编译 canary 验证的风险：provider 的签名不变，inline 实现变化，消费者的旧 JS 仍嵌着旧行为。本次没有执行该缺陷复现，不能写成已经证实线上存在错误；但默认增量切换前必须排除它。

第一版采用容易说明正确的策略：任何 .fs／.fsi 实质内容变化，都先求 owning shard 的反向消费者闭包，再对该集合求正向依赖并集，最后按 aggregate 顺序编译一次。这样可能比当前算法多编一些文件，但保留了 no-op、文档变化不重编、局部闭包等收益。

不通过简单 grep inline 来冒充完整 specialization 分析，不引入 FCS 补洞。以后要重新缩小普通 .fs 影响范围，必须拿实际 Fable 行为和独立 canary 证明受支持的边界，不能只凭类型签名做推断。

新增的永久 canary 至少包含：

- provider 暴露 inline，.fsi 不变，改变实现后，focused 消费者与 clean 消费者返回同一个新值。
- 已移除引用的消费者必须红，不能被另一个大闭包补齐。
- 泛型／Literal 等会影响消费者输出的代表性情形。
- 导出符号改动后，旧消费者导入不能由陈旧 JS 假装满足。

落点：requirements/structured-workflow/tests/fixtures/owner-project-boundary 和对应 integration 编译器测试；纯影响集合反例进入该包现有 owner-impact 规划测试。

### 8.6 focused 产物清理也要有证明

不只源文件删除会产生陈旧 JS。同一路径的 F# 文件改成仅类型／内联等内容后，编译器可能不再产生旧模块。单纯在旧 dist 上覆盖新输出仍可能留尸体。

第一版不引入复杂的分片产物事务仓库。采用一个明确策略：

1. 基于仓库当前 Fable 输出约定，提取一个小的 source→emitted path 映射函数，并用真实 compiler fixture 证明它支持的形态。
2. 对 focused 编译集合，在编译前移除归属该集合的旧 JS；保留未受影响且已由旧 manifest 核验的输出。
3. Fable 编译后重新枚举实际输出，检查入口、Surface、模块链接，再提交完整 outputs 清单。
4. 输出映射无法确定、工具链改变、拓扑变化或遇到不支持的生成形态时，事前选择 clean full，不能猜路径删文件。

共享的 Fable runtime 文件不能按某个业务 owner 的输出随意删除。生产源码输出、运行库输出、手工派生 envelope 必须区分归属。映射规则以实际 Fable emit fixture 为准，不根据 .fs 文件名机械推断后直接上生产入口。

回归必须包含“曾输出 JS 的文件，现在不再输出”的物理场景。若本轮无法证明 focused 输出清理的全部支持范围，可以先交付 no-op／派生物分离快路径，其他变化明确走 full；不得以不完整 focused 路径换取表面提速。

### 8.7 测试期间不得换产物

不把 build.lock 粗暴地从父入口持有到整套测试结束，再让子 build 请求同一把锁；那会制造自锁，也会把编译器 fixture 与生产构建混在一起。

日常和发布入口在 build 后记录 generation 与输入摘要，在 unit／integration／E2E 前后及最后核验同一构建仍有效。任何生产构建要改 dist，必须先使旧 manifest 失效。测试过程中发现另一构建替换了产物，整次结果判为不完整，不能继续宣布通过。

单独运行 unit／integration 也执行同一个新鲜度契约，而不是依赖“调用者应该先 build”。编译器 fixture 的输出必须继续使用自己的隔离临时目录，不能碰生产 dist。

源码在检查／编译／测试期间被改动，同样需要在最终验证输入核对时报告。这里保证普通并发编辑不会得到静默绿灯；不宣称它是抵抗恶意文件竞态的安全快照系统。

不实现自动重跑。诊断直接说明：本次验证使用的输入或产物已改变，请对当前内容重新运行。

## 9. 格式与静态缓存：保留简单工具，修正输入

### 9.1 Fantomas

format 只供用户主动改写，format:check 只读。Fantomas 的 check 与忽略文件行为由其官方接口决定，不自己比对格式化输出。[E3]

只读缓存必须至少覆盖：

```text
src/Wanxiangshu/**/*.fs
src/Wanxiangshu/**/*.fsi
src/Wanxiangshu/**/*.fsproj
.editorconfig 及生效的嵌套 .editorconfig
.fantomasignore（存在或后来新增都要被发现）
.config/dotnet-tools.json
global.json
package.json / package-lock.json 中相关工具配置
```

处理方法是让 Wireit 运行已声明的实际命令，并将上述规则作为输入 glob。先用仓库所固定的 Wireit 版本做新增／删除配置文件的回归，再确认 glob 行为；不能把不存在的可选文件当必需文件导致首次运行直接报错。

若仓库还接受根目录之外的格式配置，必须显式决定支持边界。不能让个人上级目录的配置无声影响结果，却不进入缓存键。优先让仓库配置自足，而不是为任意主目录状态造缓存。

不把已通过格式检查的旧缓存当作“可以自动改文件”的授权。format-build-test 全程保持只读检查语义。

### 9.2 check 的 Wireit 输入

保留检查的第一版缓存可以略宽，但必须正确。至少包含 scripts/check.mjs、保留 checks／lib、src、resources、实际测试边界扫描范围、package.json 和锁文件。全仓文书门禁退出后，AGENTS、proposals 和与产物无关的说明文档不再因为“治理”使 check 失效。

资源变化是否影响 envelope 由构建输入负责，不要误以为从 check.files 去掉一个文档就能绕过生成物更新。

Wireit 对有 output 的任务默认清理输出；这也是 build 不继续套在 Wireit 里的原因之一。[E2] 不用一个 clean:false 开关替代第 8 节的陈旧产物维护。没有这些正确性工作，仅移除 resetOutputDirectory 同样不够。

### 9.3 不增加第三层检查缓存

首版只保留 Wireit 的整步只读缓存和构建 manifest。不要再加 per-rule JSON、每文件结果数据库、长期 AST 缓存和自动 cache repair。先测删减后的静态链；若已经很轻，优化就到此为止。

## 10. 测试调度与删减

### 10.1 unit 保留什么

保留生产纯函数、状态机、时序、序列化、幂等、取消、恢复、权限以及 runner 正确性的回归。fast-check 的固定 seed、numRuns、shrink path 与已发生缺陷的固定反例不动；不能为了变快随意砍生成次数。

删除仅服务于已退役治理器的测试。例如 deadcode-scan、旧控制金字塔判据、门禁数量下限、旧问卷完整性及精确说明表格式。若测试文件还包含产品断言，拆开而非整文件删。

保留的 checker 自测用小的明确输入，不反复读取真实全仓。纯扫描器用几个字符串／文件条目证明能红；另保留一次实际接线验证，证明真实仓库确实扫描到正确范围。不能让每个反例都完整重建一次源码与规范图。

### 10.2 只搬出两类明确的发布专属工作

首轮从日常路径搬出：

| 工作 | 发布落点 | 何时主动定向运行 |
|---|---|---|
| 固定 Fable 工具链语义的多工程／可见性 canary | 现有 structured-workflow integration 文件，标记 releaseOnly | 编译器、工程模型、签名、增量算法变化时 |
| 真实整个仓库 envelope 的独立重算 oracle | 新建 degeneration-guard/tests/integration/loop-envelope-repository.test.mjs | 派生器、tokenizer、语料选择规则变化时 |

编译规划的纯测试、envelope 的小输入定律、Host admission、process restart、持久化与 Git 适配器测试仍在日常入口。不能把所有慢项都塞进 release，让日常变成只有字符串断言的空架子。

从 loop-detector.test.mjs 搬走的是那个真实全仓重算测试，不是整个文件。新的 integration 文件必须被真实发布入口接线，HOW 同步更新，不允许变成仅手工可运行的遗漏文件。

### 10.3 integration 只维护一次接线事实

继续使用 integrationNodeTestSteps，不再另建第二份 YAML／JSON 套件登记库。只有确实不同的发布专属文件增加 releaseOnly 标记。

完整性验证分两步：

```text
全部已发现 integration 文件
  = 常规 steps ∪ 发布专属 steps ∪ 子入口实际拥有的文件

任意两个执行属主的集合交集为空
```

日常模式有意不执行 releaseOnly，不属于漏接线；但 release 模式必须覆盖上面的整个集合。既要测漏掉新文件会红，也要测重复注册会红。

保留现有 discoverSuiteTests 和 integration-entry-coverage 的行为价值。这不是文书治理：测试不接线就根本不会执行。

### 10.4 四个 package 小文件只监督一次

distribution/tests/integration/package/run.mjs 当前按文件串行启动 supervisor，注释称 pack/install 共享 npm cache，但四个文件明确不执行 npm pack/install。

改为一次 superviseNodeTest({ files: suites })，仍由 node:test 隔离各文件。install.test.mjs 改名为 layout.test.mjs，说明它证明工作区的包布局，不证明真正安装。对应发现、HOW 与引用一起迁移。

真实 pack 不放进这个日常小套件，也不在 integration 和顶层各跑一次；由发布尾部的 verify-package 唯一执行。

其他 integration 的资源小文件也可合并进同一次监督调用。真实 Host、共享缓存、可变全局环境和临时目录未证明隔离之前，不能仅因“可并行”就全部 Promise.all。

### 10.5 进程隔离与并行度

run-inner.mjs 中“in-process、one dist load”的注释与当前 node:test 默认文件进程隔离不一致。改注释，不为了让注释成立而关闭隔离。每个文件独立进程是现有测试环境隔离的一部分。[E1]

保留 HOME／USERPROFILE 临时目录和独立 DOTNET_CLI_HOME。不要为省初始化时间让所有测试共同修改开发者配置，也不要把 WANXIANGSHU_NO_FATAL_EXIT 变成生产端根据测试环境偷偷推断的行为。

并行度作为已有 NODE_TEST_CONCURRENCY 的明确正整数输入校验，非法值直接报参数错误。测试 1、2、4、8 等少量实际配置的墙钟、峰值内存与尾部延迟，再确定默认值；不把机器核数当作无限启动 .NET 编译器和 tokenizer worker 的理由。

当前编译器 canary 内一次 Promise.all 启动九个 Fable 进程。移到发布层后仍可用小的有界并发执行同一组 fixture，避免与单元文件池同时争抢 CPU／内存。worker 数量限制是调度，不是降低测试覆盖。

本轮不合并整个 unit 到一个 Node 进程，不做永久 worker 池，不缓存“测试曾通过”，不做按 Git diff 推断测试覆盖。收益不足以补偿这些方案带来的隐式状态。

## 11. LoopDetectorEnvelope：最值得动的算法热点

### 11.1 当前重复在哪里

scripts/lib/derive-loop-detector-envelope.mjs 的实际链路是：读取当前选中语料 → 拼接 → tokenize → affine replay → 求 prior → 对投影值求两个分位数 → 写 JS。

现在 evaluateEnvelope 为下界和上界分别调用 empiricalQuantile；两次调用各复制并排序整个 Float64Array。上界概率为 1，实际就是最大值。

requirements/degeneration-guard/tests/loop-detector.test.mjs 的全仓测试先再次调用 writeLoopDetectorEnvelopeArtifact，再读取真实语料、再 tokenize 一次以运行独立 referenceEnvelope。加上 build，真实全仓的派生／编码确实被反复支付。

这里应拆成两种职责：日常确认“当前输入和已产出的字节对应”；发布确认“真实全仓经过独立算法核对仍正确”。独立 oracle 不该被删除，也不必在每次未变化的日常验证里重新 tokenize 整个仓库。

### 11.2 第一项算法改动：只排序一次

保持原来的经验分位数定义，rank = ceil(p × N)，下标为 rank - 1。一次排序同时给出下界和最大值。

```js
function envelopeBounds(projected, lowerProbability) {
  if (projected.length === 0) {
    throw new Error('Loop detector envelope has no samples')
  }
  if (!(lowerProbability > 0 && lowerProbability <= 1)) {
    throw new Error('Loop detector envelope has invalid probability')
  }
  const sorted = Float64Array.from(projected).sort()
  const lowerIndex = Math.ceil(lowerProbability * sorted.length) - 1
  return {
    minimum: sorted[lowerIndex],
    maximum: sorted[sorted.length - 1],
  }
}
```

这是方案示例。实际修改应在现有 evaluateEnvelope 中完成，不为一段排序另建通用统计库。它仍是 O(N log N)，但去掉一次全数组排序与复制，保持完全相同的次序统计定义。

先做这个直接改动。只有 profile 证明排序仍是主要成本，再考虑线性扫描最大值加第 k 小选择；不要一开始手写复杂 quickselect，使重复值、边界概率和最坏复杂度成为新负担。

affine replay 的累加次序、指数计算、序列拼接顺序和 artifactSource 的数值输出精度不随本次性能改造改变。浮点运算不能任意并行重排后宣称“差不多就一样”。

### 11.3 第二项：修正并行接口，但不盲目增加 worker

encodeParallel 当前从 process.availableParallelism 探测并行度。标准接口属于 node:os。[E4]

改为导入 availableParallelism，使用经校验的上限；将 workerCount 作为可注入参数，便于永久测试覆盖。修正接口后实际进程数可能骤增，所以必须先验证内存和调度，而不是把所有核都填满。

已有 safeSplitPosition 是试图保持整流 tokenizer 等价的分块约定，不是“按行切就天然正确”。必须比较完整 token 数组，不只比较 token 数或最终统计值。中文、Unicode、连续换行、斜线、空白、极长行以及文本接缝都要有小输入反例。

不能独立 tokenize 每个文件后拼接来缓存。当前语义是 texts.join('\n') 后编码，BPE 可能跨接缝合并；改变编码边界就可能改变 envelope。

worker 的失败收口也要先修：任一 worker 错误，或在返回所分配结果前退出，整次派生失败；终止并等待其余 worker 退出；不得仅 terminate 出错的那一个。unref 不等于释放任务，也不保证其他 worker 不再写回结果。

小输入直接串行，大语料的 worker 数由实际测量决定。单元文件并发、Fable 并发和 tokenize 并发不能各自假设拥有整台机器。

### 11.4 第三项：复用当前输入对应的生成物

沿用 generated-artifact-v1.mjs 的 selected input、blob digest 和生成产物信息，将其并入第 8 节的构建 manifest，而不是另造一份手写数值快照。

复用必须同时满足：语料选中的路径集合没变、每条路径的工作区内容没变、派生器及依赖没变、tokenizer 身份没变、生成 JS 本身没被改写。仅比较源码或 envelope mtime 不够。

loopDetectorRepositoryInputFiles 使用 git ls-files --cached，再读取工作区字节。必须保留这个事实边界：

- 已跟踪文件的未暂存修改会使内容失效，不能只 hash Git index blob。
- 新增 Git 跟踪路径会改变集合，即使其内容在磁盘上早已存在。
- 被跟踪但从工作区删除的路径按当前 selector 规则移出；删除也必须体现在摘要里。
- 新的未跟踪笔记当前不进入语料。CLEAN.md 在本次写入后尚未跟踪时也是如此；以后被 Git 跟踪则按同一规则参与，不能为提速专门排除它。
- package-lock 不属于语料文本，不代表 tokenizer 依赖更新可以复用旧派生结果。

只在一次阶段内共享已读的 selectedInputs 与 texts，减少重复 I/O。第一版不保存几百万 token 的跨运行缓存，不做每文件 BPE 缓存，也不更改语料选择范围来掩盖性能问题。

### 11.5 测试落点

日常保留：小语料的独立 reference、单 token／多 token 定律、分位数重复值和边界、selectedInputs 的读取覆盖、非法路径拒绝、生成 JS 与记录 hash 的一致性、缓存失效矩阵。

发布保留：真实当前仓库的独立重算，比较实际 production Surface 与当前语料导出的 prior／bounds。测试依然不能把生成器自己的中间结果直接当 expected 值。允许共享同一份原始输入与 tokenize 结果以减少重复编码，但独立数值 oracle 保持独立，且另有编码完整等价测试。

目标不是固定写死“12615ms 必须变成某个数”，而是证明日常不再重复进行这项整仓计算，发布中也不重复做同一份输入编码。

## 12. Surface、ESM 和资源检查如何收窄

### 12.1 Surface 的两部分分开

保留真正的入口约束：注册的公开 Surface 有对应生产源码和产物，所需导出存在，测试不得转而深层导入内部实现。去掉“测试必须具有某个精确标题、HOW 必须有相同文字、证明册给它授予权威”的部分。

scripts/checks/js-surface-manifest.mjs 当前兼做这两件事，还有对测试 binding 使用的复杂词法判定。改造时先把产物闭包验证从证明治理中分出来，不把整个脚本统一视作无用或统一保留。

真正调用生产 Surface 的测试失败，由 node:test 的实际结果裁决。一个源码扫描器看到 import 后出现某个标识符，不足以替代函数确实被测试，更不能成为新的 coverage 指标。

### 12.2 一次解析 emitted modules

scripts/checks/js-module-linkage.mjs 当前已经对每份 JS 解析一次，建立导出表再查相对导入；主要需要去掉 CLI 为显示数量而进行的第二次 walk，并与后置入口检查共享这份本次 inventory。

建议 validateModuleLinkage 返回 { issues, moduleCount }，调用者直接使用。若 Surface 后置检查也需要相同 AST，则传入同一份 Map；不要在每个 Surface 条目中再次 parse 全仓测试文件。

当前 linkage 主要覆盖静态相对 import 的目标与 named export，不是完整的 ESM 链接器。re-export、动态 import、package imports alias 和裸依赖不能因为该门为绿就宣称全部验证。Plugin/Sphinx 入口与 package import 的实际消费测试继续承担相应边界。

第一版对最终 dist 做一次完整线性枚举／语法链接即可。不要为这点工作造 AST 磁盘缓存或另一个模块图持久数据库。

### 12.3 资源检查从消费者需求出发

保留实际存在的语言对、占位符和协议字段一致性，资源缺失必须失败。删除作文 anchor、固定深度、泛化 prose debt 等写法治理。

scripts/build.mjs::verifyArtifacts 当前对部分 rule 目录和角色做手写抽样检查。应复用生产资源目录／registry 的完整发现结果及现有资源契约测试，不继续维护“抽样第一个目录加一个固定名字”的半套清单。

工具注册、Hook 注册和语义资源都有生产事实来源。以该来源推导期望集合，不再复制一份固定角色数／目录数作第二真源。有效 ID、协议字段或真实对外资源集合的严格相等依然可以检查；删除的是与事实脱节的重复常量。

## 13. 输出设计：成功简洁，失败完整

### 13.1 默认成功输出

下面是版式示意，<...> 表示实际运行时数据，不是本次测得的目标耗时：

```text
verify  daily
  format       OK       <elapsed>  cached
  checks       OK       <elapsed>  <sources> sources
  build        OK       <elapsed>  focused, <affected>/<total> shards
  unit         OK       <elapsed>  <passed> passed
  integration  OK       <elapsed>  <passed> passed
PASS  <wall time>  inputs and build unchanged
```

no-op 构建显示 reused：输入与产物已核对；不能显示“已重新编译”。release 额外显示 clean、Long Stroke 和 package 阶段，明确这才是完整发布验证。

并非必须机械固定在几行。已有警告、skip、todo、资源诊断或失败时增加必要信息，不为了“干净”藏问题，也不建立终端行数硬门禁。

不要在正常运行时重复打印每个 verifier 的 OK、已知反例名、80 字符横幅、每组 top 5、同一套测试的两个总数，以及 npm pack 的全文件列表。

### 13.2 失败输出

```text
verify  daily
  format       OK       <elapsed>
  checks       OK       <elapsed>
  build        FAIL     <elapsed>

src/.../Consumer.fs:<line>: compiler diagnostic
  <original error and source context>

Stopped before unit.
Log: .fable-build/verify-logs/<run>/build.log
FAIL  <wall time>
```

测试失败显示完整名字、文件位置、实际／期望、原始错误链和有用堆栈；超时显示最后一个因果 verdict、尚未完成范围、后台活动、子进程退出／信号及日志位置。默认失败输出就应足以定位问题，不要求用户先猜一个 --verbose 才知道为什么红。

受控反例测试故意启动失败子进程，其输出归该测试上下文；它通过时可以不刷屏，但其断言失败时必须展示这些输出。不要全局过滤包含 error／warning 的行。

### 13.3 verbose 与 profile

仅增加两个与显示有关的选项：

```text
npm run format-build-test -- --verbose
npm run format-build-test -- --profile
npm run verify:release -- --profile
```

verbose 展开完整工具与测试输出；profile 增加检查分项、构建失效原因、阶段墙钟、慢测试与并行配置，并可写一个 .fable-build/verify-profile.json 便于比较。profile 不是通关证书，不供任何产品逻辑消费。

数值明确区分 wallMs、sumTestMs 与 CPU 时间。sumTestMs 不是墙钟。默认无需排序所有测试；要展示 top k 时可以只维护很小的候选集。若 profile 需要分位数，用一份数值数组排序即可，不为几十毫秒的统计另造流式算法框架。

TTY 可以用同一行更新当前阶段，非 TTY 使用稳定普通文本。颜色只用于支持的终端，尊重无颜色设置；不在 CI 原始日志输出强制 ANSI 控制码。进度动画只负责显示，永远不喂 watchdog。

## 14. reporter 改造不得破坏测试监督

### 14.1 结果、监督、显示各管一件事

```text
node:test TestsStream
  ├─ IPC：verdict／执行状态 → supervisor → 权威运行结果
  └─ compact reporter：默认摘要或 verbose 详情

supervisor
  ├─ classifyVerdict → watchdog
  ├─ 文件范围、退出／信号、drained、失败与取消
  └─ 返回结构化 suite result → verify 的阶段摘要
```

不能把 spec 文本经过 grep 再数通过数，也不能靠某行含 PASS 决定成功。Node 官方支持直接消费 TestsStream 的自定义 reporter，应从事件入手。[E1]

现有 IPC 只传 name、file、nesting、durationMs 等字段。补足计数所需的 skip／todo／错误分类及真实 file wrapper 识别信息；失败详情可以由 child reporter 写入受监督的 stderr。不要把原始 Error 对象当作跨进程一定无损的 JSON，需要显式保留其关键字段或已格式化的原始诊断。

计数只认一套归一化规则。同一测试的 test:complete 与随后 test:pass／fail 不能重复计数，suite 和文件 wrapper 也不能当作额外叶子通过。嵌套测试用真实 node:test fixture 验证，不用只含一层 test 的假事件样本证明全部正确。

### 14.2 明确保留的监督语义

外部 supervisor、verdict-silence watchdog、最终物理 backstop、进程组清理和临时 HOME 继续存在。它们不是要删的脚手架。

当前 classifyVerdict 接受 test:pass、test:fail、test:complete 作为 verdict。test:complete 按执行完成报告，pass／fail 则可能受定义顺序约束。[E1] 精简 reporter 时不能只保留排好序的漂亮结果，从而让其他文件的实际完成进展失去喂狗机会。

stdout、stderr、diagnostic、等待动画、构建心跳都不能重置 verdict 静默时钟。后台活动可以记录进诊断，但不能使一个不停打印的挂死测试永远不超时。

不把 per-test timeout 施加到承载多项测试的整个文件 wrapper，不使用一个共享 AbortSignal 扇出数百监听器，不扩大时间预算掩盖竞态，不增加 retry-until-pass。

### 14.3 借这次改造补两个错误路径

第一，run-inner.mjs 当前把 stream 的 end 与 error 都 resolve 到同一条后续路径，随后发送 inner:drained。改为 error 明确失败，不得在未完整排空时发送成功 drained。先有错误 fixture，再改实现。

第二，supervise-node-test.mjs 当前遇到任意带 file 的 test:complete 就从 outstanding 删除该文件。单个测试完成不等于整份文件完成。[E1] 应根据实际 file wrapper 终结信号管理文件状态；对 Node 20 与 26 分别验证，不能靠测试名字恰好等于文件路径来猜。

若某个受支持版本没有足够可靠的公开文件终结信息，保留“完成状态尚未确认”，记录该文件最后 verdict；直到整条结果流正常排空且 child 退出，才确认整体执行结束。不要为提前显示一个完成数引入 Node 私有 API。此时超时诊断必须使用“尚未确认”，不能把已见过 verdict 的文件误称为从未执行。

文件状态首先用于正确诊断。最终成功还必须同时满足：选定范围确实执行、无真实失败／取消、child 正常退出、结果流排空、没有 backstop／silence 超时。某一份文件提前产生一次通过，不足以提前宣告整个文件完成。

### 14.4 子进程输出和退出收口

改用异步 spawn 捕获长阶段输出时，stdout 和 stderr 都要及时消费。等待 close 后再结束报告，避免 exit 已发生但最后的诊断还在管道中。spawn error、信号、非零退出和 reporter 自己抛错全部向上传播。

不要使用固定 maxBuffer 的 exec 把数千条日志攒满后才读取，也不要无限字符串拼接保存所有成功输出。默认把完整日志流式写入 .fable-build/verify-logs/<run>/，内存只保留用于即时诊断的有界尾部。

日志空间有界：正常成功后只保留有限的近期运行；失败日志保留并提示位置。达到单次日志容量上限时明确报告“日志超出记录预算”并失败，不静默截断关键错误再报告成功。日志写入失败同样不能假装已有完整诊断。

这些日志不参加格式、静态或构建输入，也不进入 npm 包。profile JSON 同理。把日志写到当前 build 的宽输入 glob 里会造成自我失效循环，必须在验收中覆盖。

Ctrl-C／SIGTERM 转发到本次拥有的子进程，停止新阶段，清理已启动的进程和临时目录，再以对应非零码退出。现有负 PID 进程组方式面向 POSIX；本次环境和 CI 是 Linux，不顺带宣称已解决 Windows 进程树清理。

## 15. 发布只验证一个真实包

### 15.1 不再拿工作区布局冒充安装证明

当前四个 package integration 文件只是工作区分发契约。保留它们的真实价值，但名称和 HOW 要如实说明。npm pack --dry-run 也只模拟打包，不生成可供解包的正式 tarball。[E6]

本方案选择一次真实 npm pack，而不是先 dry-run 再 pack。新的 scripts/verify-package.mjs 由发布入口在唯一 Long Stroke 之后调用。

### 15.2 执行步骤

1. 核验待打包的 generation 与前面测试消费的一致。
2. 在仓库外创建临时目标目录，执行 npm pack --json --pack-destination <temp>。参数以 argv 数组传递，不拼 shell 字符串。
3. 检查退出码、JSON 可解析、只有当前包的一条结果、声明的 archive 确实存在。不能把 stdout 里偶然出现的 JSON 片段当成功。
4. 根据 npm 的实际 files 列表检查入口、全部 dist 和 runtime resources、package manifest 一致；拒绝源码、tests、scripts、requirements、缓存、日志混入。
5. 在独立目录解包，枚举实际成员，与 pack 清单和构建输出 hash 核对。禁止越界路径与异常成员类型。使用明确可用的标准解包工具，不手写 tar 解析器；缺少工具时给出失败诊断，不跳过。
6. 从仓库外的 CWD 导入解包后的包入口，检查 package imports alias、必要导出与 module-relative 资源定位。用临时 HOME，不接触开发者配置。
7. 再核对生产 generation／输入／打包字节未改变。finally 清理本次临时目录，成功只输出包名、文件数、体积和结果。

npm 自动包含的 package.json、README、LICENSE 等正常元文件，按 npm 的实际打包规则处理，不能机械地因为不在 dist／resources 下就拒绝。[E6] 仍然禁止把这些自动规则变成允许任意根目录文件的通配例外。

### 15.3 不把依赖安装变成新的重负担

解包消费测试使用当前已安装并锁定的生产依赖。可在临时 app 的 node_modules 中连接这些依赖，再将解包目录作为 wanxiangshu 包；不能把原工作区的 wanxiangshu 或原 dist 链进去，否则测试又退回工作区自证。

这证明“交付 artifact 在已有正确依赖的环境中可加载”，不宣称完成了空机器联网 npm install。真实依赖解析仍由 npm ci 和现有版本契约承担。本轮不新增联网安装循环，不执行 npm publish，不改包的发布权限。

npm 生命周期脚本若导致 pack 时再次修改 dist，前后 hash／generation 必须使本次验证失败。不能在 build/test 之后偷偷 prepare 出另一份字节，再以为刚才测试覆盖了它。

### 15.4 永久测试

requirements/distribution/tests/pack-artifact.test.mjs 覆盖清单解析、缺入口、缺资源、混入源码、重复／越界成员、JSON 损坏、非零退出、同一 generation 的字节比较等小输入逻辑。真实 archive 的创建与消费由 verify:release 正式执行，不给每个 unit 反例都真实 pack 一次。

scripts/verify-package.mjs 只负责这件具体事，不扩展为 npm 发布平台。现有 contents、resources、import 和改名后的 layout 测试继续证明其各自边界。

## 16. 实施批次

每批应能独立审查，并保留一个能运行的正式入口。下面是依赖顺序，不是要求一次提交全部完成；是否 Git commit 由实际执行指令决定。

### P0：固定测量方法和回归入口

先记录当前 branch、revision、Node/npm/SDK/Fable 版本、测试文件数和第 2 节同口径计时。新鲜度修复前，不拿旧 dist 的 unit 结果证明新构建算法。

确认将要使用的永久测试位置：

| 领域 | 优先使用的现有位置 |
|---|---|
| 构建新鲜度 | verification-system/tests/build-freshness.test.mjs |
| 静态入口与失败传播 | verification-system/tests/proof-ladder.test.mjs、repository-closure-gates.test.mjs |
| integration 发现 | verification-system/tests/integration-entry-coverage.test.mjs |
| runner、watchdog | verification-system/tests/integration/harness、verdict-feed.test.mjs、e2e-watchdog-feed.test.mjs |
| 多工程编译与 impact | structured-workflow/tests 现有 owner-compile／owner-impact 测试与 compiler-boundary integration |
| envelope | degeneration-guard/tests/loop-detector.test.mjs |
| 分发 | distribution/tests/integration/package |

先把针对本次改动的反例加进这些正式位置，不在仓库根目录造临时测试脚本。需要新的 fixture 时和相应测试一起长期保留；不污染真实生产源码或 dist。

完成标志：每项拟改的执行边界都有明确测试属主，基线数据与未测范围分开记录。

### P1：先去掉有直接证据的低价值成本

改动重点：check.mjs、deadcode.mjs、deadcode-baseline.json、deadcode-scan.test.mjs、proof-ladder 中数量／源码写法断言，以及相关 WHAT/HOW。

先撤数量下限和“checks 目录必须全接线”的自我治理，再删除 deadcode 入口与专属附件。不要把 debt 0 留作一张永远空的清单。

控制金字塔、prompt-depth、session 问卷等纯治理规则可在确认没有夹带产品断言后按同一方法删除；source gate 的真实边界部分留待 P2。

验证：原来保留的产品测试照跑；失败 gate／不可启动 gate 的行为反例仍红；重新测 node scripts/check.mjs。报告实际差值，不提前宣称整条 format-build-test 已节省 14.5 秒。

完成标志：不存在 retired checker 的空壳和仅为它服务的测试；不是仅从数组中暂时隐藏。

### P2：替代混合门禁，退出证明登记链

按第 4 节逐条处理 authority、decorator、p0 recovery、DSL、external effect 等脚本。先保留／补齐生产反例，再撤精确源码片段、类型后缀、注释和表格登记。

对外部效果至少逐一核对 canonical append、writer sync、worktree create、branch fast-forward、prompt dispatch、todo write、JS transaction、provider execution、managed child、managed attempt interrupt、bounded process。原合同册移除，但这些边界的 actual acceptance、歧义和恢复断言不能凭空消失。

同步拆开 js-surface-manifest 的物理完整性与 HOW/title 治理。最后移除没人再需要的 trace、proof-level 与其他登记文件。

验证：保留行为测试、Surface 深层导入反例、private/.fsi compiler canary、恢复错误不得伪装成功的回归。旧 HOW 的声明引用跟着调整；INSTITUTIONAL-LEARNING-007 等既有缺口保持如实未关闭。

完成标志：静态检查不再为了判一个物理边界而重建整仓 WHAT↔HOW↔test graph；删除文件不留活消费者。

### P3：合并保留扫描，修正只读缓存

创建 check-context；将保留脚本改为纯导出；check.mjs 同进程运行；compile-shards 吸收重复工程清单校验；test-boundary 与 js-boundary 合并；post-build linkage 返回 moduleCount 而不再 walk 第二遍。

修正 format:check 与 check 的 Wireit 输入。验证只改 .fsi、.editorconfig、check.mjs 或 resources 时，相应检查不能误命中旧缓存。

将 causal-wait CLI 中的合成 fixture 挪到正式 unit。保留一个对真实扫描根目录的接线测试，不把每项规则都变成一次全仓进程。

完成标志：保留检查共享本次输入，任何缺失／不可读／解析失败都不被当空集合；源码只改局部写法不再撞上已经退役的规则。

### P4：构建凭据与增量正确性

先加第 17 节构建反例，再修改 owner-compile、build-state、build.mjs 和 build-freshness。

将最终 manifest 提交移动到全部后置验证之后，补完整输入／输出集合，校验 tool identity 和根身份，修正集合差、inline 消费者失效、focused 旧产物清理，保留 build.lock。

scripts/build.mjs 提供可调用函数和受保护 CLI 入口。普通与 --clean 共用同一核心，不复制两条构建实现。旧发布入口在切换前仍能明确获得 clean 语义。

验证分两层：小输入验证计划、缓存和失败状态；正式 Fable fixture 验证源合并、inline、输出消失、同一输入的 clean／focused 结果。需要比较完整产物集合、关键导出和行为；对可确定性的 fixture 进一步比较 JS 字节，不用“能 import”代替全部正确性。

完成标志：损坏状态只能明确失败或安全重建；普通 no-op 确实不启动 Fable；--clean 确实启动；失败后不存在有效成功凭据。未证明的 focused 形态仍走 full。

### P5：优化 envelope 并拆开日常／发布证明

先改一次排序，再修正并行度接口和 worker 失败清理。保持 tokenizer 序列、数值定义与输出精度不变。

接入统一 manifest 的生成物输入摘要，避免语料未变时重复 tokenize。移动真实仓库重算测试到 degeneration-guard 的 integration 文件，保留小输入独立 oracle。

发布专属分类可在此定义，但 P8 原子切换前，旧正式发布入口仍执行这些测试；不能在两批之间出现 CI 已经漏验、而新入口尚未接好的窗口。

完成标志：只改文档不重编 F#；当前语料确有变化时 envelope 不能复用；生成器／tokenizer 变更使缓存失效；发布仍独立核对真实语料。

### P6：reporter、supervisor 与小套件合并

改 compact-reporter、run-inner、supervise-node-test，修复 error→drained 和叶子完成误当文件完成的路径；四个 package 小文件只调用一个 supervisor。

verify 的最终摘要消费结构化结果，不从每个子命令的文本里反向提取统计。默认输出短，但慢／挂死时仍能展示当前阶段和最后进展；不改 watchdog 预算。

验证：正常通过、断言失败、取消、嵌套失败、模块加载失败、无测试、只有噪声输出的挂死、结果已通过但进程未退出、stream error、日志写失败和 Ctrl-C。非 TTY 无控制码，verbose 与 compact 的退出码／结果一致。

完成标志：显示可以切换，测试结论与 watchdog 判据不能随显示模式改变。

### P7：接上唯一真实包验证

创建 verify-package 与 distribution 的小输入回归。将 install.test.mjs 更名为 layout.test.mjs，修正“pack/install 共用 npm cache”的旧注释。

发布包验证实际创建 archive、核对清单、解包、外部 CWD 消费。一次调用只 pack 一次，没有网络安装和 publish。资源缺失、源码混入、入口 alias 断裂都必须红。

完成标志：能说明它到底验证了什么环境，不再靠文件名 install 或 dry-run 输出暗示不存在的安装证明。

### P8：原子切换入口、CI 和说明

新增 verify.mjs，更新 package.json、integration profile 和 .github/workflows/ci.yml；将所有发布专属文件真正归入 verify:release，移除旧顶层 dry-run。删除 build 的 Wireit 层，而不是让两套缓存长期并存。

同步 verification-system／distribution／requirement-system 等 WHAT/HOW、README 的运行命令和相关测试。AGENTS 只修正入口导航，不向其中塞一套新规范。

最后执行一次完整日常、一轮干净发布、一轮无改动复用，以及第 17 节的关键增量／失败矩阵。性能对比只在验证语义一致的场景之间比较。

完成标志：两个入口名字、实际执行、CI 和说明相符；生产与测试语义没有减少；仅剩被明确保留的检查；没有无意义兼容命令和退休登记表。

## 17. 验收矩阵

表中反例应落在正式测试／fixture 中，不直接破坏工作区生产文件。无须每行一个新测试文件，同一边界的数据驱动用例放在一起即可。

### 17.1 静态检查与调度

| 场景 | 必须观察到的结果 |
|---|---|
| 生产 .fs 未进入任何工程，包括未 Git 跟踪的新文件 | 静态检查失败，不能因扫描只看 Git 而漏掉 |
| 同一源属于两个 shard／引用不存在／图出现环 | 明确结构错误，后续语义检查不消费坏清单 |
| .fsi 遗漏、孤立或编译顺序不合法 | 静态结构或正式编译失败，不能把集合相等当顺序正确 |
| 实际资源／provider 投影越界 | 保留边界反例失败 |
| 仅重命名无语义局部变量、调整等价代码写法 | 不因已退役的源码模板约束失败 |
| 某个保留 checker 抛异常／缺扫描根 | 顶层非零，不能报告 0 issues |
| 新 integration 文件未接线／同文件重复接线 | 完整性测试失败 |
| 日常跳过明确的 releaseOnly | 输出标明日常范围；不是全发布成功 |
| release 模式漏 compiler canary 或全仓 envelope oracle | 调度回归失败 |
| 外部设置 TESTS_MJS_FILES／skip freshness 缩小完整入口 | 参数或范围错误，不静默接受 |
| 退役 checker 的纯自测被删除 | 产品测试仍正常发现；不由数量下限阻止删减 |

### 17.2 构建与缓存

| 场景 | 必须观察到的结果 |
|---|---|
| 完全无变更，完整成功 manifest 与输出 | no-op，Fable 调用 0 次，envelope tokenize 0 次 |
| 相同输入 --clean | 真实完整 Fable 调用 1 次；不复用旧 compile success |
| 普通实现变化 | 一次已证明的影响并集；行为与干净构建一致 |
| .fsi 变化 | 包含反向消费者；不遗漏消费者导入／导出更新 |
| inline 实现变、.fsi 不变 | 消费者使用新实现，与 clean 相同 |
| 删除／新增／改名源文件或改变工程归属 | full 或明确结构失败，无旧模块残留 |
| 源文件从工程移除但仍在磁盘 | 路径集合差／唯一归属检查发现，不以 existsSync 跳过 |
| 某个选中源不再生成 JS | 旧 JS 被移除，不能继续提供不存在的导出 |
| 只改变已跟踪语料文档 | F# 不重编，envelope 重新派生并核验 |
| 新增 Git 跟踪语料路径／删除已跟踪路径 | selector 集合摘要变化 |
| 修改未跟踪且不属生产／资源输入的笔记 | 不无谓改变 compiler key；不冒充已被语料纳入 |
| tokenizer、派生器、依赖锁改变 | 相应生成物失效 |
| global.json、Fable 配置、编译 helper 改变 | compiler identity／input key 失效 |
| 内容改了但 mtime／size 保持 | 由 hash 发现，不能复用 |
| dist 中缺叶子模块、内容被改、多出陈旧模块 | 完整输出校验失效，安全重建或失败 |
| manifest 损坏／schema 旧／属于其他 outputDir | 不复用，不拿 scratch 结果冒充生产 |
| 编译失败、后置 linkage 失败、manifest 写入失败 | 整次失败，没有有效成功 manifest |
| 构建中断／测试期间另一个构建改 dist | 无假绿，清楚说明输入或 generation 改变 |
| 日志／profile 写入 | 不导致下一次构建自我失效，不进入包 |
| 仅 .fsi 或 formatter 配置变化 | 格式缓存正确失效 |
| 仅 check.mjs 或被检查资源变化 | 静态缓存正确失效 |

### 17.3 数值与并行

| 场景 | 必须观察到的结果 |
|---|---|
| 单样本、重复值、极值、下界概率边界 | 一次排序与原经验分位数定义相同 |
| 空语料／非法概率 | 明确失败 |
| 多种 Unicode／换行／文件拼接边界 | 串行与并行 token 数组逐项一致 |
| 单 worker 与多 worker | 相同输入产生相同有效产物与统计值 |
| worker 出错或无结果提前退出 | 派生失败，其余 worker 被清理，不挂住入口 |
| 缓存生成 JS 被篡改 | hash 不符，不能拿正确输入键掩盖错误输出 |
| 小输入独立 oracle 与生产 Surface | 真实生产行为正确，测试没有复制生产决策作为 expected |
| 当前真实仓库发布重算 | prior／bounds 与当前语料及 production Surface 一致 |

### 17.4 报告与失败路径

| 场景 | 必须观察到的结果 |
|---|---|
| 全部通过 | 阶段摘要与实际结果一致，无逐叶子成功刷屏 |
| 叶子／嵌套断言失败 | 错误链、文件位置、实际／期望可见；顶层非零 |
| skip／todo | 单列，不计为 passed，不宣称该命题已得到通过证明 |
| 取消、模块加载失败、发现范围为空 | 非零，不能由文件 wrapper 的成功状态覆盖 |
| 一个叶子通过，后续同文件挂死 | 文件未提前宣称完成；watchdog 正常触发 |
| 不停 stdout／stderr，但没有 verdict | 静默 watchdog 仍触发 |
| stream error 后 child 正常退出 | 没有成功 drained，顶层失败 |
| verdict 全部通过但进程仍持有事件循环 | 外部 supervisor 保持监督，不能提前返回绿 |
| 重复 complete／pass 事件，suite／file wrapper | 不双计叶子，不把容器算新增测试 |
| compact 与 verbose | 运行范围、退出码、失败数和喂狗语义相同 |
| 日志管道末尾才出现错误／日志无法写入 | 不丢末尾诊断；输出故障不被吞掉 |
| Ctrl-C／SIGTERM | 非零、无遗留本次子进程、不启动后续步骤 |
| Node 20 与当前本地 Node | 对同组正式 runner fixture 得到一致判定 |

### 17.5 分发与最终发布

| 场景 | 必须观察到的结果 |
|---|---|
| 正常发布验证 | clean 1 次、产品测试一轮、Long Stroke 1 个世界、pack 1 次 |
| 缺少资源／入口／imports alias 的目标 | 解包消费或清单核对失败 |
| 包中混入 src、tests、scripts、requirements、日志 | 真实 archive 检查失败 |
| 从仓库外 CWD 消费 | 资源依然从解包模块位置读取，不回退原仓库 |
| pack 非零／JSON 损坏／没有实际 archive | 发布失败 |
| pack 生命周期修改 dist | 字节／generation 核验失败，不能发布另一次构建 |
| 全流程看似测试全绿但发生未执行范围／产物变化 | 不发布完整 PASS 结论 |

Coverage 当前为可选路径，不是本次基线 unit 的默认工作。保留启用时全部生产模块的分母与未加载模块的计入方式，不以少预导入模块、排除难覆盖模块或调低阈值提速。发布／日常拆分也不借机改变既有 coverage 合同。

## 18. 测量方法与收益判定

### 18.1 先比较同一种工作

分别记录：无变化复用、单个普通实现变更、签名／inline 变更、文档变更、工具链变化后的 full、完整发布。冷启动与热启动分开；不同 Node／SDK 和不同并行度不混在一列比较。

日常删掉完整 E2E 后与旧发布入口比较，可以说明用户日常等待缩短，但不能称为“同等发布验证快了多少”。发布前后要比较包含相同必要物理证明的链路。

每个代表性日常场景在无其他重任务时跑少量重复，记录中位数、范围、阶段耗时和峰值内存；完整发布至少有一轮明确成功记录。不要在 CI 加脆弱的绝对秒数门槛。

### 18.2 可执行计时方式

本次环境没有 /usr/bin/time，使用 Bash 自带 time 即可。以下新入口和 --profile 仅在相应批次实施后可用：

```bash
TIMEFORMAT=$'wall=%3R user=%3U sys=%3S'

# 检查本体，不混入 Wireit 的命中收益。
time node scripts/check.mjs

# 构建正确性及重复调用。
time npm run build:clean
time npm run build

# 日常与发布分别记录。
time npm run format-build-test -- --profile
time npm run verify:release -- --profile
```

重定向到 tee 时启用 pipefail，或直接使用 runner 产生的日志。不能把 tee 的 0 当作真实验证退出码。禁止通过临时禁用测试、改超时或替换生产源码来制造性能数据。

### 18.3 可以期待什么，不能承诺什么

最明确的直接收益来自删除 deadcode 的整仓重复计数。真实语料计算不再在每次日常 unit 中重复，也是结构性收益。no-op／文档变更不再触发 full Fable，才会把已经实现的增量能力兑现到主入口。

共享扫描、一次排序、合并小套件和减少成功输出应有额外收益，但需要新测量。不能从本次静态 21.066 秒与 unit 20.170 秒推断完整命令总耗时，也不能承诺具体倍数。

最终报告至少给出：删掉了什么实际工作、仍覆盖什么必要风险、五类代表性变更的编译模式、是否触发 tokenizer、实际运行测试范围、墙钟对比。不要只报告“日志从几千行变成几行”。

## 19. 最后清理与停止条件

最后检查退役脚本的 import、npm 命令、HOW 引用、baseline、proof-level 消费者和单纯自证测试。无人调用的旧工具删除；仍定义当前构建事实的工具保留。不为证明“已经删除”再写一个遍历全仓的常驻负面清单。

具体要避免的收尾错误：

- 把 34 个旧 checker 原封不动塞到新入口，只改变显示。
- 把被删的治理迁到 test 文件或 release，然后仍全量执行。
- 同时保留 Wireit build cache、owner manifest 和第二份 freshness 文件。
- 用 .fsi 未变、两个入口存在或某个最新 mtime 宣称全部产物新鲜。
- 用更大的 timeout、更少的 fast-check 样本或重试掩盖调度／竞态问题。
- 把 retired rule 改成空函数或永久豁免，而不是移除。
- 为将来可能的需求预建通用任务图、长期 AST 缓存或证明数据库。

满足以下事实就停止本轮扩张：日常入口简洁可用，必要反例会红，增量命中可信，CI 完整发布通过，真实包可消费，输出可直接定位失败。余下低成本检查没有明确热点证据，就不继续折腾。

## 20. 证据与查阅入口

### 20.1 仓库证据

本文中的“当前行为”来自以下实际文件和本次执行，不以旧计划代替实现：

| 证据 | 查阅位置 |
|---|---|
| 主命令、Wireit 输入与输出 | package.json；.github/workflows/ci.yml |
| 34 个 gate、顺序与退出传播 | scripts/check.mjs |
| clean dist 与构建后置检查 | scripts/build.mjs::compileFable、verifyArtifacts、main |
| 当前增量规则与成功 marker | scripts/lib/owner-compile.mjs::planImpactCompile、materializeOwnerCompile、compileOwnerProject、detectChangedFiles、compileIncremental |
| 工程清单与图 | scripts/lib/compile-shards.mjs::readCompileShardInventory；checks/subsystems.mjs |
| 死代码复杂度 | scripts/checks/deadcode.mjs |
| 固定命令／数量下限 | requirements/verification-system/tests/proof-ladder.test.mjs |
| mtime 新鲜度 | requirements/verification-system/tests/support/build-freshness.mjs |
| 实际测试发现与监督 | verification-system/tests/run.mjs、integration/run.mjs、support/run-inner.mjs、e2e/support/supervise-node-test.mjs、support/verdict-feed.mjs |
| Fable 合并与 .fsi canary | structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs |
| 语料输入与重复计算 | scripts/lib/loop-detector-repository-corpus.mjs、derive-loop-detector-envelope.mjs；degeneration-guard/tests/loop-detector.test.mjs |
| Surface 与 linkage 的边界 | scripts/checks/js-surface-manifest.mjs、js-module-linkage.mjs |
| 包布局不等于安装 | distribution/tests/integration/package 下的 run、contents、resources、install、import 文件 |
| 需要正式修订的旧约束 | verification-system、distribution、requirement-system 的 WHAT.md 与对应 HOW.md |

表中的 verification-system、structured-workflow、degeneration-guard、distribution 均位于 requirements/ 下。行号会随实施变化，所以以真实函数与文件为落点，不建立一份需要长期跟着更新的行号数据库。

### 20.2 外部接口依据

外部资料只用于核对工具接口，不替代仓库自己的 canary。访问日期为 2026-09-13；Wireit 等仍以仓库锁定版本的实际行为做回归。

| 标记 | 官方资料 | 本文使用范围 |
|---|---|---|
| E1 | Node 20 Test runner：`https://nodejs.org/docs/latest-v20.x/api/test.html` | 文件进程隔离、自定义 reporter、TestsStream 事件及完成／报告顺序 |
| E2 | Wireit README：`https://github.com/google/wireit/blob/main/README.md` | files／output 缓存和默认输出清理 |
| E3 | Fantomas Formatting Check：`https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html`；Ignore Files：`https://fsprojects.github.io/fantomas/docs/end-users/IgnoreFiles.html` | 只读检查与忽略文件 |
| E4 | Node 20 os：`https://nodejs.org/docs/latest-v20.x/api/os.html#osavailableparallelism` | availableParallelism 属于 node:os |
| E5 | Microsoft F# Inline Functions：`https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/inline-functions` | inline 展开到调用方；仓库增量影响范围仍须 Fable canary 确认 |
| E6 | npm 11 pack：`https://docs.npmjs.com/cli/v11/commands/npm-pack/`；package.json：`https://docs.npmjs.com/cli/v11/configuring-npm/package-json/` | dry-run／真实 archive、JSON、目标目录与自动包含的包文件 |

最终要交付的不是一套更精巧的治理机器，而是更少的重复工作、更可靠的当前产物，以及失败时不需要翻屏寻找的诊断。