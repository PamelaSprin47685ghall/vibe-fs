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

# CLEAN：测试精简、提速与输出收口

调研日期：2026-09-13。代码基线：master，ec6c363bd。本文是第二轮待实施方案，不是完成记录。

本轮只交付 CLEAN.md，不实施测试删除、生产代码修改、运行器改造或规范修订。文中的“新增、删除、迁移”均为后续施工动作。实测与推断分开记录；没有测过的性能不写成收益。

## 1. 这次做什么

上轮已经完成日常／发布入口拆分、部分门禁退役、构建内容凭据、共享检查上下文、compact reporter 和真实包验证。本轮不重做这些工程，也不把上轮已删除的 deadcode 等检查重新列入施工。

现在要解决的是：一些测试还在检查脚手架的样子，而不是系统的行为；一些真正有价值的测试反复做相同准备；测试自己的运行器还存在空证明、重复计数和日志混杂。

推荐顺序：

1. 先补运行器的真实回归，防止删完测试后，只剩一个更快的假绿灯。
2. 直接删除纯文案、注释、历史迁移清单和重复全仓扫描测试；不要把它们搬到另一个每次仍运行的 audit 入口。
3. 对混合测试逐条处理：保留真实行为和反例，删掉只锁源码写法的部分。确实缺行为证明的，先补再删。
4. 优化必要测试的输入准备、重复解析、时间推进和调度。不要先减随机轮数、放宽超时或缓存测试成功。
5. 终端只显示当前层级的结果。完整失败、诊断和重放信息保留在本次日志中，不用正则过滤器伪造安静。

不设“至少删掉多少项”“测试文件必须少于多少个”的指标。目标是少做无效工作，不是把数量压成另一个治理目标。

### 1.1 不动的边界

保留 Fable 编译目标，不使用 dotnet build；不重拆生产工程；不引入自建 FCS 扫描；不添加通用任务图、测试插件平台、全仓 mutation 框架或测试成功缓存；不把普通验证改成只跑 Git 差异关联用例。

保留权限拒绝、身份隔离、持久化失败、恢复与重放、取消与重复投递、真实 Host/进程/Git、产物新鲜度、JS 链接和真实包闭包的证明。

“删治理测试”不等于“删测试基础设施测试”。监督器能不能杀掉挂死进程、报告有没有把失败丢掉，本来就是必须测试的行为。

## 2. 本轮实际看到了什么

### 2.1 当前入口，不沿用旧方案里的命令链

package.json 目前声明：

```text
format-build-test → node scripts/verify.mjs
verify:release    → node scripts/verify.mjs --release
build             → node scripts/build.mjs
build:clean       → node scripts/build.mjs --clean
check             → wireit
format:check      → wireit
```

scripts/verify.mjs 的日常链是 format:check → check → build → unit → integration；release 使用 --clean 构建，再增加 e2e 和 package。

unit 由 requirements/verification-system/tests/run.mjs 发现测试，经 supervise-node-test.mjs 启动 run-inner.mjs。后者仍使用 node:test 的文件进程隔离，外层监督器负责静默与物理 backstop。

integration 另有 integration-node-test-steps.mjs 清单，并串行启动 distribution/package 与 verification-system/harness 两个 child。harness 不是 node:test reporter 的消费者，而是自己的 case 数组和 8 个并发 worker。

关键事实：owner-project-compiler-boundary.test.mjs 与 owner-impact-compile-cli.test.mjs 所在组目前没有 releaseOnly 标记。selectIntegrationSteps 的默认过滤只排除带标记的组，所以这组仍在日常链中。不能因 verify.mjs 的注释称其“release-only”，就当迁移已完成。

当前根目录没有 CLEAN.md；AGENTS.md 后半段保留了上一轮方案全文。它描述的 bbddd0c85、34 个检查、3983 个测试等是旧基线，不应覆盖本轮对实际代码的判断。本文不修改 AGENTS.md。

### 2.2 实测环境与范围

本机 Node v26.5.0、npm 11.17.0。调研开始和写文档前 Git 工作树均干净，HEAD 均为 ec6c363bd。

使用现有正式入口计时，没有用一次性探针充当验收。所有下列运行均发生在新增本文之前，使用当时已有 dist；新鲜度检查报告 1781 个输入、874 个产物。未进行冷构建，也未运行整个 format-build-test 或 verify:release。

| 运行 | 结果 | 墙钟 | 边界 |
|---|---|---:|---|
| 全 unit 正式入口 | 退出 0；发现 625 个文件 | 17.873 秒 | reporter 与 supervisor 计数不同，见下文 |
| Surface charter 单文件，串行 | 17 项通过 | 外壳 4.825 秒；runner 约 4.40 秒 | 显式 TESTS_MJS_FILES；不是全量验证 |
| capacity-soak 单文件，串行 | 2 项通过 | 外壳 2.718 秒；runner 约 2.30 秒 | 显式 TESTS_MJS_FILES；不是全量验证 |
| harness 正式入口 | 279 项通过，0 失败 | 10.287 秒 | 8 worker；不等于 integration 或 Long Stroke 通过 |

全 unit 的 compact reporter 报告 624 个文件、3660 passed、0 failed；supervisor 报告 3661 passed、0 failed。两者口径尚未统一，本文不把任意一个数字冒充已核实的叶子测试总数。

全 unit 的 test time 约 88.1 秒，外壳 user time 192.676 秒、sys time 37.138 秒。并发下这些值不是墙钟；不能把慢项相加后称为可节省的总等待时间。

### 2.3 有定位价值的慢项

| 项目 | 全 unit 中 | 单文件中 | 结论 |
|---|---:|---:|---|
| HOST-BOUNDARY-023 installed OpenCode admission canary | 6.254 秒 | 未单测 | 真实 Host 边界，不能因慢删除 |
| EMR-014 seeded bounded admission soak | 5.832 秒 | 2.074 秒 | 资源争用放大耗时；尚不能断言全是算法问题 |
| EMR-003 process restart/capacity | 4.283 秒 | 未单测 | 真实重启语义保留 |
| JS_SURFACE_003_law_owner_surface_registry | 3.999 秒 | 1.496 秒 | 与下两项执行同一 validator |
| JS_SURFACE_003_every_registered_surface_has_a_contract_test | 3.174 秒 | 1.333 秒 | 重复全仓调用 |
| JS_SURFACE_002b_registered_surfaces_exist_in_the_production_source_tree | 未列进全量 top 5 | 1.224 秒 | 第三次重复全仓调用 |

单文件 Surface 的三次相同 validator 调用合计约 4.05 秒，17 项测试总 test time 约 4.27 秒。这是本轮最明确的重复计算证据。不同运行的冷热状态与负载不同，不能据此保证全链等额提速。

harness 的主要慢项包括：verdict 续期约 8.926 秒、持续打印的挂死 child 约 7.033 秒、恢复默认 watchdog 窗口约 5.032 秒、泄漏 handle 约 3.597 秒，以及 waitFact 的约 2 秒等待。很多扫描／清单用例只有 0–36 毫秒：它们值得删主要因为维护成本与误报，不是因为能省几秒。

### 2.4 已确认的薄弱点

| 位置 | 实际行为 | 本轮处置 |
|---|---|---|
| scripts/verify.mjs::hashTreeFiles | 从 src/scripts/requirements/resources/.github 往下走，却只收相对仓库根等于 package.json 或 package-lock.json 的文件；也没遍历根目录这两个文件 | 选择器逻辑收不到预期输入；补真实文件变更回归 |
| proof-ladder.test.mjs 的 snapshots input state 用例 | 注释声称修改中途输入；实际只设布尔值、spy 返回成功，并断言 exitCode=0 | 删除空证明，改为真实 fixture 中修改输入且必须失败 |
| reporter-supervision.test.mjs 的 single leaf completion 用例 | 测试内复制 handleComplete，没有调用 supervisor 实现 | 测同一个真实状态函数与真实进程路径 |
| 同文件的 stream error 用例 | 写出独立临时 runner，测试它自己发送 runner:error | 不证明 run-inner.mjs；改为真实 drain 路径的注入失败 |
| compact-reporter.mjs 与 supervisor | 一个过滤容器，另一个累加所有 pass/fail；IPC 还丢掉 skip/todo、details.type 等字段 | 一个统计属主；保留完整必要元数据 |
| compact-reporter.mjs | 原样透传测试 stdout/stderr；失败到流结束才集中打印 | 成功原始日志入文件；失败尽早显示，不等可能挂死的 end |
| proof-ladder.test.mjs::verify 调用 | spy 仍触发真实日志目录/latest 链接和终端打印 | 注入根目录、输出接收器、日志目录，不污染工作区 |
| surface-charter.test.mjs | 三次 validateSurfaceManifest(SURFACE_MANIFEST, ROOT)，另有多个 scanAll | 删除重复真实仓库扫描；validator 用小型反例测试 |
| harness/run.mjs | 自建 279 case runner 逐项打印通过；expected fatal 也出现在顶层 | 对 expected child 输出做局部捕获；只汇总真实结果 |
| run-inner.mjs 的 coverage 分支 | 动态 import ../../../scripts/lib/walk.mjs 少一级；实际指向 requirements/scripts | 补 coverage 真实入口测试，不只测 coverage-policy 纯函数 |
| run.mjs 的 ROOT | 从 tests 向上两级落在 requirements，而非仓库根；coverage 输出目录随之错位 | 单一 root 解析；本轮未运行 coverage，不能称已复现其全部问题 |

输入快照的缺陷由代码路径直接推出；没有为复现而改写用户源文件。现场出现的 inputs=44136fa355b3678a 与空对象摘要一致，也与这一判断相符。其修复必须由新增的永久 fixture 回归验证，不能只再补一条源码字符串检查。

## 3. 判定一条测试值不值得留下

### 3.1 用“能抓住什么错误”判断，不用标题判断

留下前，写清三个事实：调用的是哪个实际入口；故意引入哪种错误它会红；已有哪个测试是否已经覆盖同一个错误、同一个边界和同一个观察。

示例：

| 断言 | 判断 |
|---|---|
| McpContract.fs 包含 Not a scheduler 注释 | 只锁注释；删 |
| 未授权调用进入真实 tool handler 后，被 typed rejection 拒绝且没有 Host 调用 | 真实权限边界；留 |
| 测试自己的 helper 能识别自己构造的源码字符串 | 只有 helper 确实服务于必要门禁时才值得留；否则整条链删 |
| 相同 validator 对同一仓库调用三次 | 留一处实际接线或由 build 唯一拥有；其余删 |
| 同一事件重复应用，不产生第二份 durable effect | 真正幂等性；留 |
| 旧内部文件名不能重新出现 | 一般是迁移遗留；删 |
| 旧持久化输入必须被当前 decoder 明确拒绝 | 仍在输入边界上的兼容／拒绝合同；留 |
| 提示资源里的 protocol id、占位符、字段语法和双语配对 | 运行时资产合同；不因读取 .md 就删 |
| HOW 必须包含某句解释 | 文档写法治理；删 |

### 3.2 四种施工动作

删除：断言只锁脚手架，且没有独立行为需要接替。连同专用 helper、fixture、baseline、注册行和失效 HOW 引用一起退役。

合并：边界、输入和观察完全相同，只是拆成多个标题。保留可定位的 case 名；不要把所有测试塞进一个失败后无法分辨来源的大函数。

替换：旧测试想守住的是有效业务约束，却只看源码。先写调用真实入口的正反例，再删除源码断言。

保留并优化：语义和反例有效。只减少重复准备、重解析、墙钟等待和不合理并发，不改变被检查的事实。

### 3.3 禁止以精简为名发生的退化

不能把 unknown、skip、todo、未启动、未完成的文件算成通过；不能只校验 expected 与 expected；不能为了安静把 stderr 全丢弃；不能缩减 seed/domain/rounds 后仍声称证明相同范围；不能把“unit 全绿”写成“发布全绿”；不能因删了 trace 登记器就宣布原来的 GAP 已闭合。

测试文件不是处置单位，断言才是。混合文件里的一条脆弱断言不构成删除整个文件的依据。

## 4. 删除与替换清单：验证体系自身

以下清单以已经读到的实现为依据。路径省略的共同前缀是 requirements/verification-system/tests/。后续施工时先读整个目标文件，保留本表没有授权删除的行为。

| 文件／用例 | 动作 | 删除理由／保留落点 |
|---|---|---|
| degradation-list.test.mjs | 退役整文件及专用文案解析链 | 重复固定 14 条中文、ID 对、顺序和行号；不是 watchdog 证明 |
| e2e/support/degradation-list.mjs | 随消费者迁移后删除 | 规范散文不应成为运行测试的数据库；先移除 harness 的“每项必须有 case 引用”关联 |
| proof-ladder.test.mjs 的命令 exact string 断言 | 删除写法锁定 | package 入口可有薄封装；保留实际 plan、顺序、选区和失败传播 |
| proof-ladder.test.mjs 的 releaseOwnershipViolations | 删除字符串计数器与 duplicate 字符串 mutant | 用真实调度结果证明每个 leaf 只执行一次，不数代码中出现几次路径 |
| proof-ladder.test.mjs 的 wiredGates 正则 | 删除源码数组解析 | 检查实际导出的阶段／检查列表及实际执行，不要求 const checks 的某种布局 |
| proof-ladder.test.mjs 的输入快照 spy | 立即替换 | 当前没有制造输入变化，正是空证明 |
| reporter-supervision.test.mjs 的 single leaf completion | 替换测试内镜像函数 | 新测试直接导入实际运行路径用的状态函数；再通过真实 child 验文件完成 |
| reporter-supervision.test.mjs 的 stream error | 替换独立 mock runner | 向真实 drain 函数注入抛错事件流；真实 supervisor 验证非零和无 drained |
| reporter-supervision.test.mjs 的 compact/verbose 比较 | 保留但加强 | 两套同错也会相等；必须同时等于手写的精确期望计数，并检查失败内容 |
| reliability-runbook.test.mjs | 删除目录标题和 API 文案断言 | 留操作手册，不让改写自然语言触发测试；incident 的脱敏数据断言迁到证据导出／重放测试 |
| guide-contract.test.mjs | 拆分后退役大部分 | 删除“旧模块导入必须失败”和精确导出名单；真实模块闭包归 build；独立数据／调用约束迁领域 suite |
| domain.meta.test.mjs | 按领域搬迁、比较后去重 | 文件名像 meta，内容却有 deadline 时区、codec、重放、provider failure 行为，不能整体删除 |
| no-line-count-check.test.mjs | 建议随规范修订退役 | 不再用一个扫描器检查“没有另一种扫描器”；行数政策仍由规范说明，不新建反治理门禁 |
| js-boundary-gate.test.mjs | 去掉重复真实全仓扫描，保留窄反例 | 未授权 deep import 和表示泄漏仍应被实际保留门禁拒绝；不要重复跑同一整仓现状 |
| build-freshness.test.mjs | 保留、补运行中污染与 fixture 隔离 | 内容凭据是日常增量的前提，不是治理装饰 |
| integration-entry-coverage.test.mjs | 保留、调整成 daily/release 集合测试 | 发现测试必须有一个可运行入口；缺失、重复、悬空都要能红 |
| verdict-feed.test.mjs、strict-mock-signals.test.mjs | 保留 | verdict 续期、background 不续期、waitAny 取消与监听器清理有独立失败价值 |
| scenario-turn-registry.test.mjs、temporal-harness.test.mjs | 保留真实时序行为 | 不因“测 harness”而删去防假绿、重放和因果一致性证明 |
| walk-fail-closed.test.mjs | 保留 | 缺根、不可读目录、符号链接等不能悄悄缩小扫描集合 |

guide-contract.test.mjs 中的 200 KiB 等数值有可能是产品边界，不应随旧模块名断言一起删除。先把真实大小边界转入拥有该语义的测试，使用临界值前后两个输入证明，再删除原断言。

domain.meta.test.mjs 的具体转移：deadline 偏移／时区到 process-execution；Journal 和 Fact codec 到 durable-events；context 重放到 context-compression；provider failure 去重和预算到 provider-attempt-recovery。没有相同反例的先搬原行为，不把“同一个 WHAT 已有测试”当作重复证明。

## 5. Surface 测试：先删重复，再删登记治理

落点：requirements/js-semantic-surface/tests/surface-charter.test.mjs、scripts/checks/js-surface-manifest.mjs、scripts/lib/test-surface-scan.mjs。

### 5.1 17 项现有测试的逐项取舍

| 标题中的稳定部分 | 动作 | 接替方式 |
|---|---|---|
| JS_SURFACE_001_all_semantic_tests_are_mjs | 合并到一次测试发现／边界扫描 | 不单独再 walk 全 requirements；保留不支持测试后缀的负例 |
| JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests | 不在 unit 重复全仓现状扫描 | 正式 check 已承担实际门禁；unit 保留 scanner 反例 |
| JS_SURFACE_002c_whole_semantic_test_zone_is_scanned | 保留 | 临时 support/.mjs/.js 里的越界应被发现；这是扫描范围的真实反例 |
| JS_SURFACE_003_law_owner_surface_registry | 过渡期作为三次扫描的唯一一次；终态归 build | validator 自测不再读真实仓库 |
| JS_SURFACE_003_every_registered_surface_has_a_contract_test | 删除重复调用 | 与上一项同函数、同输入、同输出；没有第二个错误模型 |
| JS_SURFACE_002b_registered_surfaces_exist_in_the_production_source_tree | 删除重复调用 | 源文件缺失的能力由 fixture 反例证明，实际仓库由 build 校验 |
| JS_SURFACE_003_manifest_rejects_unemitted_or_unauthorized_evidence | 改名、缩小 fixture | 已读实现的反例是删除 dist，保留 missing emitted；标题不能继续声称测了所有授权 |
| JS_SURFACE_004_helper_not_directly_tested | 删除“helper 必须有 law”的治理部分 | helper 中的 deep import 仍由实际边界扫描递归覆盖 |
| JS_SURFACE_005_js_native_representation_rules | 保留，整理成纯数据表 | validator 是边界工具；把 bad DU、日期、非普通对象等反例放在一处 |
| JS_SURFACE_006_fable_representation_not_contract | 删除按隔离名单文件内容猜用途的断言 | 是否有 quarantine 由明确执行目的决定，不看文件是否写了 FSharp/dist 等词 |
| JS_SURFACE_006_emitted_relative_imports_are_package_closed_and_named_exports_link | 保留 | 调用实际 validateModuleLinkage；缺模块、缺导出和逃出 dist 的 fixture 有价值 |
| JS_SURFACE_002f_template_dist_import_is_detected | 保留 | 防模板字符串绕过 import 边界扫描 |
| JS_SURFACE_004b_support_to_support_transitive_edge_is_scanned | 保留 | 防 support 链绕过扫描 |
| JS_SURFACE_003c_usesSurface_rejects_dead_string_and_recognizes_active_imports | 在取消静态 proof authority 后删除 | 不是产品行为；不再对测试作者的调用闭包发证明许可证 |
| JS_SURFACE_003f_shadow_and_nonterminal_alias_cannot_forge_surface_use | 同上 | 不保留只服务于退役 authority 计算的 scope 引擎测试 |
| JS_SURFACE_003d_manifest_rejects_unauthorized_active_consumer | 随测试包 consumer 授权登记一起退役 | 不影响产品运行时权限；语义测试可通过正式公共 surface 验跨域合同 |
| JS_SURFACE_003e_manifest_rejects_stale_consumer_metadata | 随 SURFACE_CONSUMERS 生命周期退役 | 不留下已不再参与决策的登记表和检查 |

上表后五项中涉及取消 proof authority 的动作，必须先修订 JS-SEMANTIC-SURFACE-003。当前 WHAT 明确要求静态可达 callback 闭包，因此不能先删除实现、再解释“只是优化”。第一步删除两次完全重复扫描不需要改变此语义。

### 5.2 推荐终态

Surface 清单只管理物理接口边界：哪些模块是合法公共 surface、对应哪个生产源文件、跨边界用什么数据表示。必要时保留 owner 作为领域归属，但不再用 laws、lawOwners、consumer 包名和测试 callback 静态分析授予证明权威。

运行时权限仍由生产 Capability/Authority 和实际拒绝测试守卫。不要把“测试文件属于哪个 requirements 包”混同为产品调用者的权限。

真实仓库上的校验只由 build/post-build 持有一份。结构反例放在 unit：manifest 非数组、重复模块、缺 source、未编译 source、缺 emitted module、非法相对路径、导入缺失和解析失败。

删除 active-use 分析前，查清 analyzeSurface、usesSurface、importsSurface、proofHasLaw 等导出的实际调用。仍被必要扫描器使用的语法能力留在 scripts/lib/js-syntax.mjs；不能为了删一个治理 validator 把所有 JS import 解析也一起删掉。

### 5.3 即使暂不修订合同，也能先省掉什么

先做两件无争议的事：三次全仓 validator 变成一次；把同一调用内 WHAT 文档解析缓存为 Map<package, lawIds>，而不是每个 surface/law 重新读文件。暂时保留的 callback 分析只解析每个测试文件一次。

不要建立磁盘级“测试证明缓存”。本次内存快照用完即弃；删除文件、换 fixture 根、修改内容后不能继承上一轮扫描的绿色结果。

## 6. 源码与文档形状测试：按断言拆，不按文件扫光

### 6.1 已精读的典型：boundary-exemption-ratchet.test.mjs

路径：requirements/structured-workflow/tests/boundary-exemption-ratchet.test.mjs。

直接删除以下七项的文案／注释断言：

```text
McpContract_fs_carries_not_a_scheduler_comment
EPI_013_WHT_records_protocol_boundary_exemption
SW_017_WHT_records_protocol_boundary_exemption_conditions
CHGINT_006_WHT_states_no_fold_and_no_ResumeAtXxx
SW_003_WHT_carries_SW003_vs_SW009_disambiguation
CHGINT_HOW_restates_no_fold_constraint
RETIREMENT_WHT_documents_retirement_dispatch_and_blockers
```

它们分别锁英文注释、中文解释、章节消歧、HOW 重述和条款名出现。产品 continuation、持久化、退休流程不因这些文本匹配而被证明。

其余三项先替换：

| 旧用例 | 真实回归应怎样写 |
|---|---|
| production_source_has_no_ResumeAtXxx_durable_log_pattern | 调用实际重放／义务查询入口，给定中断前事实与新物理观察，检查重新推导的义务，不依赖恢复地址字段 |
| SuicideTool_fs_defines_spec_executePrepared_and_retirement_freeze | 调实际 tool handler；无 Finality 权限时拒绝且不产生 effect，有权且满足条件时才可进入退休 |
| SuicideTool_fs_freezes_retirement_without_session_abort | 通过真实边界 port 记录因果 trace，验证 freeze 在后续检查前；任何分支不得产生 session-scoped abort |

替换测试优先放在 relay-retirement 的现有 suite，而不是新造 verification-system 通用 façade。缺少测试入口时，只开放领域已有承诺的窄合同面，不导出内部状态机全部字段。

### 6.2 源码检索发现的下一批候选

以下文件已命中明确的源码字符串断言，但本轮没有逐条读完其中全部测试。因此这里授权的是“定位并替换命中的断言”，不是批量删除整文件。未核实的其他断言默认保留。

| 文件 | 已见问题 | 实施落点与接替行为 |
|---|---|---|
| structured-workflow/tests/error-handling-vocabulary.test.mjs | 检查 TaskResultBuilder、TryFinally、Using、While、For 和 open 的写法 | 用真实 Fable builder 执行 success/error/throw/dispose，验证结果与清理；语法存在由编译器决定 |
| obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs | 检查 task/taskResult 与 let!/return! 字符串 | 验实际顺序、typed Error 短路与 effect 不发生 |
| obligation-ledger/tests/magic-todo-after.test.mjs | 检查 Error/Ok 分支、TodoWriteAccepted、enrichAcceptedResult 名字 | 注入 capture 拒绝/成功，验持久化、返回值和后续动作是否发生 |
| effect-accounting/tests/pre050-effect-marker.test.mjs | marker 在源码中存在 | 将真实旧输入交 decoder，验 typed rejection；旧输入合同未退役前不删拒绝能力 |
| durable-events/tests/hook-dispatcher.test.mjs | 检查 HookKind、fetch、remoteTracking 与函数签名文本 | 调 hook dispatcher，核验传给 Git/transport 的精确命令和错误处理 |
| durable-events/tests/event-store-journal-writer.test.mjs | 检查 store.Append 名字 | 验 append 成功前无内存权威推进；失败后不产生后继事实 |
| durable-events/tests/event-store-append.test.mjs | 检查若干 FatalProcess.trip 字符串 | 用隔离进程或真实注入 fuse 验语义损坏时 fatal 分类及 durable aftermath |
| durable-events/tests/local-process-event-log.test.mjs | 检查 AppendAllText/appendFileSync、目录变量名 | 写两条真实事件后检查追加字节、重启可读、尾部损坏处理 |
| durable-convergence/tests/writer-retention.test.mjs | writer-manifest、版本字面量、BlobOid/LastActivityMs、nextExpiry 文本 | 以虚拟时间和真实 retention 入口验到期／未到期／重新活跃的行为 |
| durable-convergence/tests/writer-stream-sync.test.mjs | WriteBlob、WriterId、materialize 的源码词汇 | 以两端实际状态证明 sync 后收敛、重复 sync 幂等、损坏失败 |
| speculative-investigation/tests/dry-run-shadow.test.mjs | 检查 CreateChildSession/Detached 等名字 | 用实际 lifecycle port trace 验 shadow 与 live effect 分离 |
| interaction-authority/tests/terminal-policy.test.mjs | 检查 Role.Manager 与父子关系变量名 | 对顶层 manager、linked child、非 manager 输入做实际 terminal policy 表驱动测试 |
| participant-horizon/tests/horizon-surface.test.mjs | 检查 HandleProjection.horizonVisible 名字 | 直接验证两个身份看到的 public projection，不凭调用名断言隔离 |
| work-record/tests/work-record-sections.test.mjs | 源码包含标题、旧标题不存在 | 验真实 render 的完整 section 结构与空段省略；用户可见标题确属协议时保留精确值 |
| delegation/tests/delegated-tool-estimate-surface.test.mjs | 检查 Set<ToolCallId> 及 forbidden token | 用重复 ToolCallId 输入验不重复计数；跨边界只返回必要投影 |
| intra-participant-parallelism/tests/fission-source-ratchet.test.mjs | 源码调用／命名守卫 | 先核查现有 fission-domain 和 lifecycle 行为；只删除有接替的形状断言 |

这一步不改 production 算法来迎合测试。原实现若本来正确，只调整测试入口和输入；若新行为测试暴露真实缺陷，单独修根因并记录旧败新胜。

### 6.3 不能误删的“看起来也像静态检查”

ProjectReference DAG、每份源码的唯一归属、.fsi 配对、禁止遗漏编译输入，是多工程构建的真实结构合同，仍需校验。已有 delegation 的闭包数字预算写进了 WHAT，不能当作普通重复测试擅自取消；要调整数字政策时，另行同步该条款，不在本轮顺手抹去。

同样，provider 资源内的字段语法、占位符、locale 配对和禁止泄漏的内部标识，属于产品输出边界，不是普通说明文档。测试恰好读取 .md，不是删除理由。

## 7. 规则库与领域测试的重复准备

### 7.1 已确认的重复

requirements/behavior-diagnosis/tests/catalog.test.mjs 与 tip-v2-contract.test.mjs 都验证真实 120 项目录、名称集合、字段对应关系。catalog.test.mjs 内又通过 rules() 多次取得相同真实资源。

tip-v2-contract.test.mjs 不是纯治理文件。它还验证 missing tip、unknown tip 映射、重复 cycle 拒绝、RecentTips 的长度与顺序、squash 边界、pairing 和 assistant-step protocol。不能因开头两项重复就删整文件。

### 7.2 明确迁移表

| 现有内容 | 目标 |
|---|---|
| TIP_01、TIP_02_and_16 的真实目录计数／集合重复 | 在 catalog 的一次实际资产合同中合并，删重复用例 |
| catalog 的 ruleId/fieldName/ordinal/nonempty 检查 | 同一加载结果上做多个清晰断言；失败指出 ruleId，不反复读目录 |
| TIP_05/06/07 与 codec 的类似解码 | 逐条对照输入与拒绝种类；只有完全等价的才删，保留未知 tip、空值和精确映射的不同边界 |
| TIP_08–12 的重复、顺序、cap、squash | 迁入 observation 所属行为文件或保留原文件；不再为选一个字段重复加载 120 项目录 |
| TIP_13/14 与 observation-pair/projection 的重叠 | 比较 exact input/output 与边界；保留唯一的未配对项和 cycleId 反例 |
| TIP_15 的 0/1/多 call protocol | 与 enforcer-cycle-protocol.test.mjs 对照；保留完整分类结果，不退化成 acceptedCalls 的真值断言 |
| 旧字段 Scores/family/catalogOrdinal 不存在 | 若只记录迁移史则删除；若字段泄漏仍是当前序列化禁令，则在真实 DTO 输出里校验一次 |

120 的产品目录合同与“至少要有 N 个测试”不是一回事。当前产品仍承诺目录数量时，保留一个权威资产测试，不把数值变成 rules.length 与 rules.length 的自我比较。

### 7.3 夹具设计

真实资产测试每个 suite 只加载一次只读资源。测试不修改共享资源；需要变更资源的用例使用独立临时根，不能修改真实 resources/ 再恢复。

纯 decoder／fold 测试优先使用明确的小输入。字段必须来自产品合法有限域时，使用一个稳定的合法例值；不要为了获取一个可用 ruleId 调用五次 fieldNames()。当测试的目的就是校验资源加载或资源变化，应继续走真实 loader，不能缓存掉待测读取。

scope 只到 suite／file，不做跨 worker 全局对象缓存；不得共享 journal、runtime、随机生成器、临时时钟或可变 host mock。共享不可变 fixture 不等于共享被测状态。

## 8. harness 的 279 项：删自证，留下执行可靠性

### 8.1 单独算账

requirements/verification-system/tests/integration/harness/run.mjs 有自己的 allCases、worker、Watchdog 和结果打印。这 279 项不会被修改 compact-reporter.mjs 自动优化。

终态不急于把它们全部翻译成 node:test。先在现有 runner 上删除无价值 case、隔离日志、返回结构化结果；强行一次性迁移会把执行生命周期也改掉，反而扩大风险。

### 8.2 可直接退役的治理链

| 位置／已运行的 case | 动作 | 理由 |
|---|---|---|
| single-source-cases.mjs 的四个 cardinality scanner case | 整组退役，删除专用扫描器 | 通过变量名、英文复数规则、数组字面量猜两个概念是不是同一数量；成本主要是维护扫描器 |
| source-cases.mjs：every retired field names its replacement | 删除 explanation 长度 >20 等断言 | 解释长短不决定拒绝行为 |
| source-cases.mjs：retired vocabulary is reported before structural problems | 取消诊断先后写法锁定，保留明确拒绝与定位 | 用户需要知道哪里错，不需要固定错误被哪段旧迁移说明先报告 |
| mutation-cases.mjs 中 K10 enforcing symbol／case／presence table 三项 | 退役 presence 自证 | 不能用符号、case 名和表存在代替真实拒绝；先保留下方实际 mutation 行为 |
| degradation-cases.mjs：every forbidden degradation has a covering case, and every citation resolves | 退役文案到 case 登记关联 | 不维护第二张 proof completeness 数据库；具体挂死和续期反例保留 |
| path-criterion-cases.mjs 的源码路径猜测器 | 用实际入口的 missing root／unwired file fixture 替换后退役 | 运行配置在运行时验证，不再解析三种方法和三种引号风格 |
| 多处 every scenario in forest compiles | 合并一次真实森林加载 | source-cases 与 forest loader 已有重复；反例中的坏文件、漏文件仍保留 |
| stages 的精确打印次数／输出 marker 子串约束 | 在迁移到明确 stage 事件后删除 | 不能直接先删；若当前 Host 就绪识别还靠这些 marker，先完成事件接线 |

不是所有带 retired 字样的 case 都应退役。source-cases.mjs 调真实 compileScenario 拒绝未知／旧输入字段的测试，是输入边界证明。应把零散历史名字整理成“未知字段与不支持语法拒绝”的小表，保留真实 parser 调用，而非全部删除。

### 8.3 明确保留的行为族

保持 schema 编译的拒绝能力：重复键、模糊声明、悬空 fault/wait、错误 runtimeStep、非法 optional/must、坏 TOML、部分结果不能流出。

保持 runtime 匹配：同输入确定性、最长合法前缀、歧义拒绝、不同 lane/step 隔离、retry 复用相同内容、seal 的追加与重写边界。

保持物理契约：子进程 HOME/环境隔离、临时目录与 listen(0)、健康检查期限、进程回收、SSE 重连、监听器释放、未完成 child 与非零退出。

保持 watchdog 的可红性：持续打印不能续命；真实 verdict 能续命；绿 verdict 后泄漏 handle 仍失败；结束干净不能被 watchdog timer 拖住；timeout 前有诊断。

没有逐项核实的其他 harness case 暂时保留。本表不授权把 arch010、schema、delivery、runtime-key 等整组按名字删除。

## 9. 先补运行器的真证明，再精简其他测试

### 9.1 让 verify 在 fixture 中运行，而不是只让 spawn 返回成功

主要修改 scripts/verify.mjs。保留固定执行顺序，不另造可配置工作流框架。将当前耦合的三件事分开：确定阶段、执行阶段、展示结果。

建议接口如下。名字是目标设计，不是当前已存在的 API。

```js
export function verificationSteps({ root, release }) { /* 固定阶段数组 */ }

export async function verify({
  root,
  release = false,
  verbose = false,
  runStep,
  output,
  logDirectory,
}) { /* 实际采集输入、执行固定阶段、比较输入、返回结果 */ }
```

CLI 为这些参数提供真实默认值；测试提供临时 root、内存 output 和临时 logDirectory，只替换物理 runStep。不要注入一个永远相同的 snapshot，这会再次绕过最需要验证的部分。

建议结果包含 mode、stages、outcome、inputChanges、wallMs、logDirectory。库函数返回结果，不调用 process.exit；CLI 只在最外层设置 process.exitCode。失败后不执行后继依赖阶段，保留尚未执行阶段为 not-run，不能把较短的 results 数组渲染成全流程完成。

verify 本身就是唯一阶段顺序来源。测试断言关键因果关系与实际调用轨迹：format/check 失败不构建；build 失败不测试；日常不运行 release leaf；release 的 build 带 --clean；每个测试／包步骤一次。不要另抄一份规范表再验证两张表相等。

### 9.2 输入快照的正确范围

复用 scripts/lib/build-state.mjs 已有 collectCompilerInputs、collectGeneratedInputs、collectArtifactInputs 和 computeDigest；另收测试运行所需的 verification inputs。收集器可以放在同模块，避免为一件事新增配置平台。

verification inputs 至少覆盖实际使用的测试／support／fixture、scripts、资源、工程与签名、工具配置、package.json/package-lock.json、格式配置及 CI 配置。构建输入与验证输入有重叠是正常的，最终按规范化相对路径去重；不要把“只有两个 package 文件”误写成整个树的过滤条件。

快照是排序后的路径集合与逐文件内容摘要，不是最大 mtime，不是文件总数。目录成员增删必须可见；未跟踪但会被测试发现的源码或 fixture 也必须可见。生成物、日志、node_modules、.git 内部文件不作为验证源再次递归输入；依赖版本通过 lockfile 与实际运行环境标识记录。

收集器必须拒绝缺少必要根和读取失败，不能 catch 后给空集合。两个空快照相等不构成成功。路径选择和数据摘要分开测试：前者验覆盖集合，后者验内容变化。

新增永久回归建议放 requirements/verification-system/tests/verification-inputs.test.mjs：创建最小仓库 fixture，包含 src、scripts、requirements、resources、.github、package 文件和构建配置。fake runStep 在指定阶段真正执行 writeFile、rename、unlink；最终必须得到 input-changed 和非零。不同测试各用独立目录，不能碰用户真实源码。

用例包括：修改 .fs、.fsi、测试、support、运行资源、package-lock；新增被发现的测试；删除一个输入；只修改忽略的日志；读取失败；根路径错误；同大小同 mtime 内容被替换。既测收集集合，也测整条 verify 结果。

前后快照只能发现端点变化，不能保证发现中途修改后又恢复的 ABA。本文不把它称为文件系统事务。正式 release 应在 CI 的隔离工作区运行；本地边编辑边验证出现污染应失败，但没有隔离就不能承诺对所有并发写入具有强一致性。

### 9.3 消除 --skip-staleness-check 的默认旁路

当前 verify 已构建后仍向 unit 传 --skip-staleness-check。先修正快照，再让正式 unit 走 assertBuildFresh 内容验证。对于目前规模，一次正确的 hash 校验比维护“相信父进程”的隐式协议简单。

后续只有测量证明 hash 重复形成瓶颈，才考虑把同一不可变构建凭据向子进程传递；不能凭环境变量 BUILD_OK=1 绕过。测试自己要求跳过新鲜度的 runner fixture 必须明确标注为基础设施自测，不得流入正式日常／发布 verdict。

### 9.4 覆盖率分支也必须走真实入口

先修正 run-inner.mjs 的相对 import 与 run.mjs 的仓库 root。然后用最小实际项目／runner fixture 驱动 --coverage，而不是只验证 parseCoverageThreshold。

证明三个事实：未加载的生产模块仍在分母里；发生模块导入错误不能继续报告好看的百分比；产物摘要写到指定目录且低于阈值确实返回非零。要用实际 Node 进程验证父 runner 的预导入如何进入 worker 覆盖率汇总，不凭注释假定它有效。

本轮没有跑 coverage，因此这里只认定上述路径错误和测试覆盖缺口，不宣称已核实覆盖率数值。覆盖率继续保持显式选择，不为了这轮精简把它强塞进每次日常链，也不取消现有分母合同。

## 10. 一个统计属主，显示不参与判定

### 10.1 当前双计数为什么必须结束

现在 run-inner 把原始事件发给父进程，又把同一个流交 reporter。reporter 自己识别叶子、suite 和文件 wrapper；supervisor 则直接数所有 test:pass/test:fail。多写一份逻辑没有增加可靠性，只产生了本次 3660/3661 的分歧。

终态由内层实际消费 TestsStream 的一份状态计算结果，reporter 只显示，supervisor 只监督其生命周期。父进程仍有独立失败权：child 崩溃、静默超时、backstop、缺少结束确认、日志写失败，都可以使整层失败，不能被内层的 green summary 抵消。

### 10.2 最小程序落点

新增 requirements/verification-system/tests/support/test-run-state.mjs，集中放事件规范化和累计状态；不要再建立 event bus。run-inner.mjs 调用它，compact-reporter.mjs 消费它的结果，reporter-supervision.test.mjs 直接测试同一函数。

```text
Node TestsStream
  → 版本适配／事件规范化
  → 一个 TestRunState
      ├─ 进度通知 → supervisor watchdog
      ├─ 失败详情 → renderer／本次日志
      └─ 完成结果 → renderer + supervisor
```

规范化结果至少区分叶子结束、文件结束、容器失败、输出、诊断和运行器错误。测试名不能当唯一 ID；不同文件同名、同文件 nested subtest、suite 容器都可能存在。身份以文件、实际父子关系及该运行中的事件身份组合，不把标题当主键。

文件完成必须来源于真实文件执行完成事件，不能因为其中一个 test:complete 到达就移出 outstanding。也不能用 file.endsWith(testName) 之类后缀判断：一个恰好叫文件尾部名字的叶子不能关掉整个文件。

取消、skip、todo 要独立记录，且不能计入 passed。容器虽不算叶子，容器上的导入失败、hook 失败、取消或异常退出仍然是失败；去掉容器计数不能顺便把容器错误去掉。

### 10.3 兼容 Node 20 与本机 Node 26

当前 CI 明确使用 Node 20，开发机是 26.5.0。Node 当前在线文档的版本比本机更新，不能把文档中新出现的 test:summary 字段直接当作这两个环境都具备的协议。[N1]

先用永久 fixture 在两个支持环境实际运行：普通叶子、嵌套、同名、skip、todo、导入异常、before/after 异常、显式 timeout、空文件和正常退出。用这些执行结果确定当前需要的事件适配，而不是在测试内手造一个想象中的 Node 事件格式。

原生 summary 在当前版本可用时用来核对结束统计；不可用时只走经该版本 fixture 证明过的适配路径，不猜字段。必要生命周期事件丢失或格式无法识别时返回明确 runner-error，不能伪造 filesCompleted=filesPlanned。

不为了方便写 reporter 擅自抬高 engines，也不静默取消 Node 20 CI。若确实要升级支持版本，属于单独的运行环境变更。

### 10.4 结束协议

保留父子 IPC，但将最终确认与实际排空绑定。建议 inner 的正常结束顺序是：

```text
源测试流正常结束
→ 事件状态完成，检查文件集合与叶子结果
→ renderer 和日志 sink 写完，确认无错误
→ 发送一次 runner:summary
→ 发送一次 inner:drained
→ 关闭 IPC，进程自然退出
```

错误流、formatter 抛错、日志写失败、文件未完成、异常退出，均不能进入这条正常确认链。不要仅监听源流 end，却不等待 compose/pipeline 的下游消费结束。Node 的写流有背压和结束语义，调用 end 不等于已经写完；使用 finished/pipeline 或等价的显式结束处理。[N2]

supervisor 正常通过需要同时满足：没有失败／runner-error／超时；child exit=0 且无 signal；本次计划文件均完成；收到唯一合法 summary 与 drained；必要的输出写入成功。所有叶子绿而 child 留着 handle 不退出，仍由现有物理监督判失败。

父进程的“当前已完成数”来自同一状态的进度通知，不再独立解释 raw pass/fail。日志流量只记录为背景，不给 watchdog 续期。不要为了安静而删掉 background 信息，因为超时诊断仍需知道“最后在做什么”。

### 10.5 把当前两项镜像测试换成真实路径测试

将 run-inner 的流消费整理为一个被 CLI 实际调用的导出函数，例如 drainTestStream({ stream, state, output, send })。为它提供抛错的 async iterator 是合法边界注入，因为运行器实际用的是同一个 drain 函数；复制一段错误处理代码再运行则不是。

保留少量真实 child fixture 验外层：导入错误、持续打印但不完成、绿叶子后 handle 泄漏、正常快速结束。错误消息要来自实际 run-inner/supervisor，而非专门为用例写一个假 runner 证明假 runner 自己正常。

compact/verbose 两种模式的测试不仅彼此相等，还必须等于手写的期望对象。例如一个成功、一个断言失败、一个跳过、一个 todo，加一个导入失败容器，期望 passed=1，leafFailed=1，skipped=1，todo=1，containerFailures=1，整体失败。最终展示可将失败归纳，但底层不能丢掉这两类错误。

## 11. 测试算法优化：删重复计算，不删观察

### 11.1 Surface 扫描从笛卡尔遍历改为输入索引

当前 validateSurfaceManifest 对每个 surface 遍历全部测试文件，虽有字符串前置过滤，仍重复做 M×T 次候选判断；每次整仓调用又重新读取和解析。M 是 surface 数，T 是测试文件数。

第一步已经确定：删除两次完全重复调用。第二步在剩余实际校验中，先遍历一次输入，形成 module → import edges、source → compile membership、package → parsed metadata 的 Map。需要检查的每一条边只访问对应模块，不再从全部测试反向过滤。

如果正式取消 callback proof authority，就连同它的静态调用闭包遍历删除，不为已退役问题做算法优化。若过渡期间保留，则 AST 只解析一次，按实际 import edge 调分析，不为每个无关 surface 重建作用域索引。

目标工作量是读取／解析输入总量，加上实际导入边和清单项的遍历；排序只发生在最终确定性输出阶段。不能承诺所有正则与 AST 操作严格线性，但可以消除明确的“每项都重读全仓”。

内存缓存只存在于本次 context，fixture root 是其身份的一部分。模块级永久 canonicalRegistry、按路径不按内容缓存、由文件 mtime 猜有效，都不应重新引入。

### 11.2 owner-impact 属性测试：把文件系统留给边界测试

现有 owner-impact-compile.property.test.mjs 对 chain/diamond/fanout/arbitrary 各生成 25 个图。每个图落盘 .fsproj/.fs/.fsi，再对 impl、signature、mixed、reordered、single-change union 和 config change 多次调用 planner。大量重复读写不是所要证明的图性质。

在 scripts/lib/owner-compile.mjs 内明确分成两段：readImpactInventory 读取并验证项目输入；planImpactFromInventory 只接收不可变 inventory 和 changed paths，计算真实 plan。原 planImpactCompile 保留为两者组合，所有 CLI／生产构建仍经过同一纯 planner，不保留第二套算法。

属性测试把生成的图直接转成 inventory，调用 planImpactFromInventory。保持现有 seed、100 个总样本及性质：并集、重复变更幂等、签名影响单调、aggregate canonical order、互不相关分片不进入、配置变化触发 full。fullThreshold 边界、未知源码、环和非法引用保留固定反例。

另留少量物理适配测试：一个正常工程夹具、一个缺失 provider、一个重复 Compile、一个坏 XML／路径，以及输入文件增删。用相同小图分别经过 readImpactInventory+planner 与既有 CLI，核验结果一致。真实 Fable 编译边界仍由发布 canary 证明；在内存里生成图不等于证明 Fable 私有性。

这项改造涉及生产构建 planner，必须先用现有固定反例锁定结果，再抽纯函数。不要把图算法复制到 test helper，也不要顺手改变 inline／签名／full fallback 规则。

### 11.3 Watchdog：时间判断虚拟化，物理终止保留

当前 Watchdog 直接使用 Date.now、setTimeout、clearTimeout、console 和 process.exit。为时序测试做最小依赖注入：clock.nowMs、schedule/cancel、diagnostic sink、terminate。真实默认实现仍用原生计时和非阻塞 timer；不引入通用调度器类层级。

现有 createVirtualClock 返回 nowMs、advance 和 port.delay；port.delay 的返回值具有 delay()/cancel()。它不是原生 Timeout，不能假装直接塞给 clearTimeout。写一个薄适配器把虚拟 delay 转为 callback 调度，取消后不再触发回调，并处理取消结果，避免 unhandled rejection。

用虚拟时钟直接测试：首次启动窗口、blocking 续期、background 不续期、stop 取消、临界点前后、setWindow 与恢复默认窗口、timeout 只触发一次、诊断先于 terminate。当前 setWindow(null) 恢复的是中央 WATCHDOG_TIMEOUT_MS，不是构造参数中的局部 override；先保持这个实际语义，不能在提速时暗改。

这类测试不再真实等 5 秒。不要全局替换 Date.now/setTimeout：harness 当前 8 个 worker 同进程并发，全局 monkey patch 会串扰其他 case。

原生 timer.unref、进程组清理、真实 stderr、信号与泄漏 handle 必须继续由实际 child 证明。虚拟时钟证明期限计算，不能证明操作系统已经回收进程。保留代表性的物理挂死用例，不把全部物理边界换成 mock。

### 11.4 Capacity soak：先减少争用，再动算法

createAuditor 已经使用 owner Set、ledger Map、token Set，不要再提一个“把查找改成 Map”的空优化。它在每次 operation 后检查容量、引用完整性、去重、counter 单调和 reconciliation；这些观察是该测试的价值。

保持 seed=0x36c0ffee、rounds=32、queueWidth=32、lineageCycles=64、capacity=4 和当前 retained bound。先删旁边的重复全仓扫描、控制文件并发，再重测此项。本轮单独 2.074 秒而全量中 5.832 秒，已表明争用值得先处理。

如仍需优化，分别计时 capacitySnapshot、reconcileCapacityEvidence、结构比较与 fixture 创建。这个分段测量应成为可重用 profile 输出，而不是每次散落 console.time。若生产 snapshot 本身慢，单独优化生产投影，不在测试内用另一份预期快照替代生产结果。

第二次 capacitySnapshot 是为了证明 reconcile 不改变被测状态，不能无条件删。可以把相同的非变异性质集中到一组更明确的回归，但必须说明从“每次 operation 都查”变成“选定边界查”减少了哪一层覆盖，并据此修订测试意图；本轮默认不做这一步。

### 11.5 其余属性测试

先保留 durable merge、tail truncation、provider recovery、subagent reuse、wire algebra 等真正调用 production 的性质。文件叫 property 不表示需要保留；但没有读过全部 oracle 也不能直接宣布它是脚手架。

fast-check 的 seed、run budget 与失败重放配置应显式绑定每项测试；保留 shrink path，失败日志不得裁掉它们。[N3] 小有限域用穷举，大域用有界生成。不要用更多随机次数代替缺失的错误输入，也不要因测试变慢只减 numRuns。

减少状态空间只在能证明等价时做。例如两个独立操作可交换，需要先有真实 production 的交换律证明；不能按“最终输出相同”合并所有历史，因为不同历史可能涉及不同 effect、身份或失败路径。

## 12. 调度：只保留日常与发布两种正式范围

### 12.1 日常入口的测试范围

format-build-test 继续执行全部保留的日常产品回归和必要适配器测试。不要引入一串 quick/medium/governance/core/smoke 名称让使用者猜测自己漏验了什么。

现有 unit 名字可以暂留，但报告中说明它包含纯逻辑、确定性时序及部分物理适配器，不把名字当作证据层级。实际需要真实 Host 的 admission canary 不因迁到 integration 就少跑一次；需要跨文件移位时，发现入口也同步调整，保证日常集合仍含它且只含一次。

### 12.2 发布专属范围

owner-project-compiler-boundary.test.mjs 与 owner-impact-compile-cli.test.mjs 所在组明确标为 releaseOnly。它们是工具链／真实编译边界 canary，不需要每次业务改动都重新启动一批 Fable；修改构建工具的人仍应显式运行该组。

loop-envelope-repository.test.mjs 已经标为 releaseOnly，保留独立真实语料重算。不要因为 unit 中不再全仓重算，就删掉发布层独立 oracle。

release 保留日常回归、clean build、编译边界、全仓 envelope、唯一 Long Stroke、真实 package。CI 已经运行 npm run verify:release，不要在本轮再改回日常入口，也不要让 --release 先跑完一个日常构建、再重建一次 clean 产物。

测试选区用实际文件集合验证：日常集合无重复，发布集合覆盖日常并增加明确发布项，所有发现到的 integration 测试恰有一个可执行 owner。测试只核对集合与实际执行，不维护“发布用例至少几个”的数字门槛。

### 12.3 合并父进程，不误称共享了文件 worker

resources/prompts、enforcer-rulebook 与其他不共享可变物理资源的小 integration 文件，可以合成一次 superviseNodeTest 调用。原来每组打印一套 banner、统计和 top5，现在只有组级汇总。

保留 Node 的文件进程隔离。把多个文件放在一个 run({files}) 中，只减少外层 supervisor／runner 的重复初始化，并不意味着这些文件共享了一次 dist import。不能把“单父 runner”宣传成“所有测试只有一次模块加载”。

同一资源的纯断言要真正复用昂贵准备，应该在同一领域文件里围绕同一不可变 fixture 组织；不是把不同文件搬到同一目录。不要为共享缓存取消所有文件隔离。

Host、compiler、npm pack/install、真实共享 store 的并发按物理资源分组处理；同一资源不可竞争时串行，互不相关时才并发。不要把每个 child 的 CPU 并发都乘到外层 8 worker 上。

### 12.4 并发参数收口

保留一个显式正整数并发参数。旧 NODE_TEST_CONCURRENCY=false 如继续支持，只作为明确的 serial 别名；NaN、0、负数、Infinity 必须报参数错误，不能悄悄退回 true。

默认值先保留当前策略，再在相同机器上对 2、4、8 和默认并发做 profile，比较总墙钟、Host/soak 尾部耗时和内存峰值。选择较小且稳定的值，不先宣布 8 就是最优。CPU 数只是容量线索，不能代表多个 Fable/Host 子进程的合适并发。

编译器 canary 内部的 Promise.all 也有物理成本；发布组单独执行时再测其并发，不与几百个 unit worker 一起竞争。没有瓶颈证据，不改造中央资源调度器。

### 12.5 局部运行不能冒充正式全量

保留 TESTS_MJS_FILES 作为现有局部／harness 入口，但正式 verify 默认拒绝外部悄悄缩小发现集合。局部执行报告必须显式标明 scoped、文件集合和未运行的范围。

不根据 test 标题自动判断是否昂贵，不根据注释自动分类，不扫描 WHAT 给测试打调度许可证。范围配置只服务执行需要，由固定入口的实际集合定义。

## 13. 终端输出设计：一层只说一层的事

### 13.1 日常成功输出

以下只演示布局；N、F 和耗时均为占位，不是优化后的实测数据。

```text
verify daily
  format       ok       <time>
  checks       ok       <time>
  build        no-op    <time>
  tests        ok       <time>   N passed · F files
  integration  ok       <time>   N passed · F files

PASS daily · <wall time>
```

build 的 no-op 指复用已校验的产物，不意味着 tests 可以 no-op。不要输出 cached tests passed，因为本方案不缓存测试成功结果。

单独执行 unit 时，它自己显示一份摘要；从 verify 启动时，摘要交父级显示。不能内层打印一份 [test-summary]，父层再打印 authoritative，再在 verify 末尾重复所有阶段。harness 单独跑也按同一原则显示一份结果。

非 TTY 使用普通逐阶段行，不输出光标控制符和假进度动画；TTY 可以更新当前阶段一行。长阶段只显示阶段名和真实已完成数量，不为了保持热闹重复成功测试名。显示刷新不是 watchdog 的因果进展。

### 13.2 失败输出

```text
FAIL tests · 1 failed · 2 files incomplete

requirements/<owner>/tests/<file>.test.mjs:<line>
  <完整测试标题>
  expected: <断言期望>
  actual:   <实际值>
  cause:    <原始失败原因及有用调用栈>

未执行：integration
日志：.fable-build/verify-logs/<run>/tests.log
```

每个独立失败都要有可定位的条目。相同子测试失败向上传播形成的容器重复消息可以折叠成一个因果链，但不能少报独立失败。seed、shrink path、反例输入、错误 code、expected/actual、文件和行号保留。

第一次真实失败到达就展示，不等可能永远不到的 end；末尾只补充总状态与未完成集合。失败后仍运行的同层独立测试是否收尾，沿用明确策略；后继依赖阶段不得继续。若进程崩溃没有结构化错误，展示该 child 的有界日志尾部及完整日志路径。

### 13.3 日志分流与夹具隔离

stdout/stderr 不是成功／失败协议。字符串含 FAIL 的 expected fixture 不能把顶层染红；stderr 没输出也不能代表通过。判定来自真实退出与结构化结果。

改造分三层：

1. 测试内启动的预期失败 child：测试自己捕获其输出，断言 code、诊断和清理结果；只有断言失败时展示捕获内容。
2. 产品 canary／Sphinx 等正常诊断：进入本次按 suite/child 归属的日志，不默认把完整 JSON 倾倒到终端；遇到真实失败立即显示诊断摘要。
3. verify 的真实 warnings、参数错误、构建错误和 runner-error：以明确类型显示，不能统一重定向 stderr 到黑洞。

mock verify 必须使用临时目录与内存 sink，不能创建真实 latest 链接，更不能打印 PASS verify release 让读者误以为已运行发布。单独加一项回归，验证 spy 模式执行没有向调用方外部终端或真实日志目录写入。

并发 case 不用全局替换 console.log/console.error 来静默；那会互相截走日志。对实际可注入 logger 传局部 sink；不可注入的第三方程序在 child 边界捕获。暂时无法区分的诊断完整保存在 suite 日志中，不作内容猜测式丢弃。

### 13.4 详细与性能模式

只增加或打通两个明确选项：--verbose 显示执行细节；--profile 显示耗时分布。两者不改变选择文件、测试参数、超时和判定。未知参数直接拒绝。

verify 必须把显示设置传给 Node 测试入口和 harness；不能只打开父级 stdout，而内层仍不知道 verbose。单独 runner 可继续识别 NODE_TEST_VERBOSE，正式入口统一转换一次。

profile 默认只记录每文件墙钟、每层墙钟、叶子耗时和必要的慢项，不为每条成功断言再加一套时钟。区分并发工作量之和与用户等待时间。只有 profile 才排序 top5；普通模式不每组重复分位数。

不建立历史性能数据库。一次运行产物放现有 .fable-build/verify-logs 下；一次 profile 比较可记录到实施提交说明，不能因此给仓库再添指标治理平台。

### 13.5 日志的可靠边界

每个真实执行有独立目录，避免内层 fixture 改 latest。stdout 与 stderr 保留通道标记；接收顺序只是日志观察顺序，不能冒充跨进程全局因果顺序。

内存只保留有界 tail，完整流写文件并处理背压；不能把所有输出累加进字符串等 suite 结束。文件写入失败要报告基础设施失败，不能仍打印 PASS 后才发现磁盘满。

默认摘要不显示每个成功日志文件路径，失败或 verbose 再给定位信息。保留必要的脱敏：不能为方便排障把用户 HOME 配置、凭据、请求秘密或任意文件内容塞入日志。已有 incident 脱敏的行为测试随导出工具保留。

## 14. 必须保留的测试布局

不要把此表变成另一份强制 registry。它只是施工时辨别责任的说明，实际运行仍由现有发现入口负责。

| 领域 | 必留的错误模型 | 优先优化什么 |
|---|---|---|
| 身份／权限／admission | 跨身份误用、重复 acceptance、stale fence、未授权 effect | 缩小 fixture，不删真实入口拒绝 |
| durable events | 写盘失败、损坏尾、重复事件、codec 不兼容、重启重放 | 纯 fold 小输入；保留少量真实物理追加 |
| durable convergence | merge 幂等／交换／结合及其适用前提、partial sync、retention | 避免为每个纯性质起完整仓库；保留实际 Git／传输契约 |
| provider recovery | terminal 吸收、stale/duplicate、互斥 resolution、最多一次 effect | 固定 seed 的真实 Surface 性质与最小反例 |
| Host／process | SDK 可观察契约、生命周期、超时与 handle 回收 | 物理实例复用仅限同一明确定义的生命周期；不共享全局脏状态 |
| compiler／build | 输入闭包、.fsi 隐藏、inline 传播、失败不发布、缺输出、新鲜度 | 纯 planner 属性在内存；真实 Fable canary 到 release |
| resources／distribution | 完整语义资源、定位不依赖 cwd、真实包可导入 | 一次资产遍历，真实包只验一次 |
| test infrastructure | 漏执行、假绿、错误统计、失控输出、静默不回收 | 真正运行同一状态／drain 函数，小型进程负例 |

编译器可以拦截的类型错误，不需要再检查某行源码拼写；但“代码编译了”不能替代合法输入产生正确结果。同理，接口存在、能 import，不等于该业务行为正确。

## 15. 规范调整范围：先说清要取消哪种义务

本轮的用户目标支持继续裁减治理性测试，但具体规范修订仍应与实现原子对齐。不要用保留中的“判据只收紧”条款去维护已经决定退役的无效门禁，也不要借精简取消有效行为保障。

| 规范／说明 | 建议修改 | 不改什么 |
|---|---|---|
| verification-system WHAT 001 | 执行层序由真实固定调度器保证；允许移除重复接线与写法检查 | 日常／发布集合、一次执行、唯一 Long Stroke、clean release |
| verification-system WHAT 004、005 | 明确 verifier 自测必须运行实际 verifier；禁止测试内复制决策冒充回归 | 可红、异常失败、非零传播 |
| verification-system WHAT 006 及 HOW | 不再要求中文退化清单与 case 表逐条绑定；列出实际监督行为与测试落点 | causal silence、诊断、background 不续期、物理回收 |
| verification-system WHAT 008、010 | 明确删除冗余／无效证明不等于降低 retained contract；新鲜度与内容摘要仍是硬要求 | 失败不能改成 warning，不能放宽超时／manifest 判据 |
| verification-system WHAT 011 | 如实际 worker coverage 机制需调整，仅修 HOW 或精确机制描述 | 全生产模块分母与缺失报告失败 |
| verification-system WHAT 012 | 删除为政策存在再建扫描器的 HOW 落点 | 不以代码行数作为质量门槛 |
| js-semantic-surface WHAT 003 | 取消静态 callback reachability 与 consumer/law 登记授予 proof authority 的规定 | 领域归属、真实公共 Surface、生产行为 oracle |
| js-semantic-surface WHAT 004 | 以独立失败价值判断 helper 测试；不为内部纯函数发 law 许可证 | 不测纯实现协作、不暴露无意义内部状态 |
| js-semantic-surface WHAT 002、005、006 | 只同步接口检查的新落点 | 公共边界、JS-native 数据、禁止 mangled 私用、完整 JS linkage |
| structured-workflow 相关 HOW | 删除注释／文案豁免测试引用，指向真实 continuation／退休行为 | 领域控制流和权力归属 |
| 各领域 HOW | 去掉被删除的 exact path/title 引用，把搬迁行为指到新归属 | 唯一产品语义、未完成 GAP 的真实状态 |

无需重新生成 proof-levels、requirement-trace 或每测试 law 授权数据库。既有 WHAT 标题标签可以保留作导航；新增测试使用正确的领域命题，不再延续 P6-REPORTER 一类施工批次标签冒充产品合同。

若遇到本文未授权的产品语义调整，例如改变容量上限、允许新权限、放宽恢复拒绝、改变闭包预算，停止该局部改动并单独记录。不影响其他已明确的测试精简继续推进。

## 16. 程序落点总图

尽量改现有模块。新增模块只有确实出现一个被运行器和测试共同使用的概念时才成立。

| 位置 | 施工内容 | 交付后的责任 |
|---|---|---|
| scripts/verify.mjs | 固定 plan 导出；root/output/logDirectory 注入；正确快照；无默认 staleness 旁路；单份阶段输出 | 验证执行的唯一外层属主 |
| scripts/lib/build-state.mjs | 复用内容摘要与输入收集；加入明确验证输入选择 | 输入／输出内容凭据，不负责展示 |
| scripts/lib/owner-compile.mjs | 从已有 planner 抽 read inventory 与纯 plan 两段 | CLI 与 property 调同一个真实算法 |
| scripts/checks/js-surface-manifest.mjs | 先去重读写，后按规范修订退役 proof authority 分析 | 窄 Surface 物理边界验证 |
| scripts/lib/test-surface-scan.mjs | 清理失效 laws/consumer 元数据消费者；保留必要 import／representation 扫描 | 测试边界，不是 proof 登记中心 |
| verification-system/tests/run.mjs | 修 ROOT；校验局部选择和并发参数；把显示交同一输出策略 | 单元／领域 suite 的真实发现入口 |
| verification-system/tests/support/run-inner.mjs | 正常化事件、调用实际 drain、覆盖率路径、完整 flush/error | Node TestsStream 的唯一消费与统计 |
| verification-system/tests/support/test-run-state.mjs（新增） | 规范化事件、文件完成、叶子统计与容器失败 | 唯一运行状态；无文件 I/O、无终端输出 |
| verification-system/tests/support/compact-reporter.mjs | 去掉自有计数；接收结果；即时失败；完整错误信息 | 只做显示，不决定 PASS |
| verification-system/tests/e2e/support/supervise-node-test.mjs | 消费 canonical 结果；验证完整结束；保留进程组监督 | 物理生命周期，不再二次累加 pass/fail |
| verification-system/tests/e2e/support/watchdog.js | 最小 clock/timer/output/terminate 端口 | 保留原生默认行为，时间逻辑可确定验证 |
| verification-system/tests/support/integration-node-test-steps.mjs | 正确 releaseOnly；合并合适的小组；固定范围集合 | 文件到执行组的唯一映射 |
| verification-system/tests/integration/run.mjs | 消费组选区，统一输出；不从源码字符串推断接线 | 一次 warmup、一次各 integration child |
| verification-system/tests/integration/harness/run.mjs | 删除退役 case 接线；输出注入；捕获 expected diagnostics；返回一份结果 | 保留已有 case 执行与物理监督，不重造框架 |
| verification-system/tests/reporter-supervision.test.mjs | 删除两份镜像逻辑，改跑真实状态／drain／child | 防错误结果与假绿 |
| verification-system/tests/verification-inputs.test.mjs（新增） | 真正创建、修改、增加、删除 fixture 文件 | 证明 snapshot 覆盖集合与输入污染会红 |
| verification-system/tests/support/fixtures/ | 必要的正常、异常、挂死、skip/todo 等 child fixture | 可重复的真实物理负例，命名不可被普通发现扫入 |
| 各领域现有 *.test.mjs 与 HOW | 迁移有价值断言，合并重复准备，删除文字治理 | 测试跟随真正语义属主 |
| .github/workflows/ci.yml | 保留 release；增加必要的 runner 兼容性验证安排 | 支持版本真实通过，不凭本机推断 CI |

不必为每一行新增文件。watchdog 的虚拟时间回归可放现有 timeout/budget 测试；source checker 反例可留现有文件。新增 test-run-state 是为了消除已经存在的两份真源，不是为了把一个函数拆成五个模块。

## 17. 分批施工计划

每批完成顺序：确认要保留的行为 → 让新反例在旧路径上红 → 修改实现／删除重复 → 跑该范围 → 清理失效引用 → 独立提交。只删除文案型测试的批次不需要人为造一条假的产品失败；其证据是断言确实没有调用产品或必要 verifier，并且依赖已清干净。

### T0：锁住真实 runner 缺口

改 verification-inputs.test.mjs、proof-ladder.test.mjs、reporter-supervision.test.mjs 及最小支撑接口。首先让三种错误显式变红：验证期间真的改文件；删除实际文件完成处理；实际消费流中抛错。

在旧代码上，snapshot 测试应因 verify 错判成功而失败；runner 测试应证明调用的是 run-inner 使用的函数，而非测试自己的复制品。完成 root/output/logDirectory 注入后，断言测试没有污染真实 verify-logs/latest。

验收：固定阶段的实际轨迹、错误传播、mock 安静、fixture 清理。此批不删除领域测试，不调整随机预算，也不切 releaseOnly。

### T1：去掉已经证实的重复工作

改 surface-charter.test.mjs 的三次 validator 为一次；对同一文件中重复的 scanAll 现状调用按职责收口。合并 catalog/TIP_01/TIP_02 的重复真实资产检查，保留其他业务输入。

暂不删除 JSSEM003 的静态 proof authority，避免把小优化与规范变更绑在一起。重复调用去掉后，运行相同的 Surface 单文件 profile，与第 2 节的基线对比；记录执行范围变了什么，不要求固定提速比例。

验收：missing emitted／deep import／支持文件扫描等负例仍红，资源数量／字段／加载行为仍对。用例减少应能逐条对应删除表，而不是只报少了几十项。

### T2：退役纯文案和历史清单治理

处理 degradation-list 文案链、boundary-exemption 的七个纯文字断言、runbook 标题/API 文案、harness cardinality 与 K10 presence 表。先删除消费者接线，再删只剩它们使用的 helper/fixture。

保留 runbook 的人类说明；迁走 incident 数据脱敏断言；保留真实 compileScenario、watchdog、runtime selection 反例。不保留一个返回空数组的 parser 壳，让旧 case 表假装还有效。

验收：领域行为 suite 通过、harness 能正常加载，必要反例仍失败；HOW 不指向已删路径。不要求“检查目录等于固定 allowlist”，只检查实际 import 没有断链。

### T3：取消 Surface proof 登记治理

先修订 JS-SEMANTIC-SURFACE-003/004 及对应 HOW，再删除 callback authority、consumer/law 授权登记和只服务它们的自证测试。保留最小公共 Surface 物理边界与完整 emitted ESM linkage。

从所有调用点移除旧 API，不能只从主入口跳过而留下整套 latent scanner。仍被窄 import checker 使用的 parse/scan 能力留在已有语法模块。

验收：正式 build 仍对缺 emitted module、named import 不存在、越界相对路径失败；合法公共 Surface 的领域测试可运行。不得因为测试文件换包目录就出现新的运行时权限含义。

### T4：替换业务源码字符串断言

按领域逐个小提交，不一次改完第 6 节所有文件。先处理退休权限、持久化失败和恢复，因为误删的损失最大；再处理无副作用的 projection/render/codec。

每项保留原命题，使用实际入口和明确输入观察结果。仅在真实业务边界必须有调用约束时记录 port trace，例如禁止第二次 durable append；不把私有 helper 调用次数换个名字重新钉死。

验收：旧违规行为使新测试失败，正常行为通过；无未知内部字段被开放为 test-only API。搬迁 domain.meta 时逐项确认时区、旧 codec 拒绝和 provider 去重没有消失。

### T5：算法与夹具提速

抽 owner-impact 的生产纯 planner，property 走内存图；保留小型文件解析契约和真实 Fable canary。给 Watchdog 注入局部虚拟时间端口，将期限语义的真实等待替换为离散推进。

保持 capacity soak 参数和每步关键观察，重测独立与全套差异。真正资源瓶颈应出现在 profile 数据里，再决定是否单独优化生产 snapshot；本批不靠删第二次 snapshot 直接得分。

验收：旧 seeds、固定反例、输出集合与时序语义一致；Watchdog 物理 child、timer 不拖进程和清理仍通过。并发测试不修改全局时间或共享 journal。

### T6：一个统计器和一套显示

引入 test-run-state，run-inner 与 reporter 真正使用同一结果。supervisor 删除二次计数，补 complete/summary/drained/exit 联合验证。修 coverage 的实际路径，并补可运行入口测试。

日志按第 13 节分流；失败到达即显示；下游写入失败不能继续 drained。harness 不再每项通过打印，预期 mock fatal 被所属 case 捕获。

验收：Node 20 与26.5 的实际 fixture；compact/verbose/profile 的选区与最终判定完全一致；输入同一套失败时不能丢 expected/actual/cause/seed。普通成功运行无模拟发布 PASS 和 giant Host JSON。

### T7：调度收口

为 compiler group 补 releaseOnly；保留已经存在的 envelope releaseOnly。合并小型 integration 的外层调度，确认 warmup/child 不重复，真实 Host admission 若搬家依然每天验一次。

用 runtime plan 和集合测试防漏接线。参数非法、外部 TESTS_MJS_FILES 偷缩正式范围、空选择都失败。明确显示 scoped 与 release-exclusive 不同概念。

验收：日常集合正确；release 包含日常与全部发布项；没有测试既由 unit 发现又由 integration 启动；改动某个必要文件路径后缺失会被入口发现，而不是静默少跑。

### T8：清理引用并做最终验收

逐个检查删除文件的 import、HOW 映射、APPLIES-TO、fixtures 和旧环境变量。不要重新生成退役的 proof registry；也不要只降低计数断言让它变绿。

完成后运行日常与发布入口，记录真实环境、范围、exit code、墙钟与失败信息。用相同机器比较改前／改后；把仍无法完成的验证明确写出来，而非声称“测试都过了”。

最终交付应删除旧代码，不保留两份 reporter、一份始终空返回的 scanner、一条永远启用的 compatibility 路径，或一个没人使用的 pass count。

### 17.1 批次依赖

```text
T0 真实运行器回归
 ├─ T1 重复扫描／资产去重
 ├─ T2 文案与历史清单退役
 └─ T6 统一结果与显示

T1 + T2 → T3 Surface 治理退役
T0 → T4 业务形状替换
T0 → T5 算法与夹具
T3 + T5 + T6 → T7 调度收口
T4 + T7 → T8 最终验收
```

图是语义依赖，不要求工具盲目并行。相同 runner/support 文件的重叠修改必须串行；各领域独立断言审阅可并行，公共接口确定后再迁调用方。

## 18. 永久验收矩阵

这些是实际回归用例的设计，不是要再落一张机器 proof 登记表。正常用例与故意违约用例成对存在；错误注入不修改用户真实工作区。

### 18.1 输入、范围与阶段

| 输入／故障 | 必须观察到 | 落点 |
|---|---|---|
| fixture 中途修改 .fs/.fsi／测试／support／资源 | verify 非零，指出变化路径 | verification-inputs.test.mjs |
| 中途新增／删除被发现输入 | 文件集合变化，不能 PASS | 同上 |
| 同大小同 mtime 替换内容 | 内容摘要变化，不能 PASS | 同上 |
| 只新增本次忽略日志 | 输入快照不变 | 同上 |
| 缺少必要输入根／读取失败 | 明确失败，不返回空快照 | 同上 |
| format 或 check 返回非零 | build 及后续没有执行 | proof-ladder.test.mjs |
| build 失败或产物凭据无效 | unit 没运行或新鲜度拒绝，不能跳过校验 | build-freshness/proof-ladder |
| --release | 恰一轮 clean build，恰一轮 E2E/package | proof-ladder |
| 日常运行 | 不含 release-only 编译器／envelope／E2E/package leaf | integration-entry-coverage/proof-ladder |
| 某 integration 文件未接线／重复接线 | 入口失败并指明路径 | integration-entry-coverage |
| 正式 verify 携带局部 override | 拒绝缩小范围，或明确进入非正式 scoped 模式 | runner／CLI fixture |
| 空文件列表／非法并发／未知参数 | 参数失败，不回退宽松默认 | runner／CLI fixture |

### 18.2 结果流与日志

| 输入／故障 | 必须观察到 | 落点 |
|---|---|---|
| 一个成功、失败、skip、todo，加容器失败 | 精确独立计数，整体失败 | reporter-supervision |
| 两个文件同名叶子、嵌套同名子测试 | 身份不冲突、不重复扣完成数 | 同上 |
| 叶子恰好叫文件名后缀 | 不能被当文件 wrapper | 同上 |
| 首个叶子结束，第二个挂住 | 文件仍 outstanding，最终监督失败 | 真实 child fixture |
| 模块导入异常，尚无叶子 | 文件／容器失败可见，不显示0失败通过 | 同上 |
| before/after 出错 | 失败不因容器过滤而消失 | 同上 |
| 源流 error | runner-error；无正常 drained；非零 | 实际 drain 函数测试 |
| reporter 抛错／sink 写失败 | 无正常完成确认，整体失败 | 同上 |
| 日志写流慢、有背压 | 不丢块、不无限积压，完成前写完 | 输出边界测试 |
| 先有一个失败，后有挂死 | 首个失败及时展示，最后报未完成集合 | 真实 child fixture |
| 绿结果后留下 interval/server | 父级非零并回收该组进程 | 真实 supervisor fixture |
| child 信号退出／无法 spawn | 非零，保留信号或启动错误 | 同上 |
| child exit0 但缺 summary/drained | incomplete，不得 PASS | 同上 |
| 正常快速结束 | 无多余等满 watchdog 窗口 | 物理退出 fixture |
| compact/verbose/profile 三模式 | 相同文件、计数、判定；只显示不同 | reporter + entry 契约 |
| expected failure fixture 输出 FAIL | 外层仍由断言判定；成功终端不混入假故障 | harness output 契约 |
| mock verify 运行 | 不改真实 latest，不写真实终端 | proof-ladder/output 注入测试 |

### 18.3 算法与业务保真

| 输入／故障 | 必须观察到 | 落点 |
|---|---|---|
| Surface 缺 emitted module、缺 named export、越界 import | 实际 build validator 失败 | Surface／linkage 小 fixture |
| support 再导入 support 隐藏 deep import | 必要边界扫描仍发现 | surface-charter 保留反例 |
| 大 graph 多次 plan，同一变更重复或重排 | 输出 canonical、幂等、无重复 | owner-impact property |
| signature 影响、断开分片、配置变化 | 原规则保持；不能漏消费者或假 focused | 同上及固定反例 |
| 真实工程缺 provider／.fsi 隐藏符号被使用 | Fable compile 红 | release compiler canary |
| watchdog background 与 blocking 输入 | 只有后者续期；边界前后确定 | 注入虚拟时钟的真实 Watchdog |
| stop、setWindow(null)、取消后的回调 | 无泄漏、无迟到触发、默认语义不变 | 同上 |
| 原 capacity seed 与参数 | 每步引用／容量／fence／reconciliation 仍验证 | capacity-soak |
| rulebook 实际资源丢失／重复／坏字段 | loader 或 validator 红 | catalog 一次真实资产测试 |
| 0、1、多 chronicle call，空／未知 tip | 原错误及分类保持 | codec／cycle protocol |
| 未授权退休、冻结后检查、中断 | 拒绝与 effect 因果正确，无 session abort | relay-retirement 行为测试 |
| append 失败／尾损坏／重复恢复 | 权威状态不越过 durable 事实 | durable 领域 suite |
| 未加载模块＋坏模块的 coverage fixture | 分母完整；坏模块不报虚假高覆盖 | 真实 coverage 入口测试 |

## 19. 执行命令与验证阶梯

### 19.1 本轮已经执行的基线命令

从仓库根执行，TIMEFORMAT 使用 Bash 内置 time。本机没有 /usr/bin/time，不依赖它。

```bash
node --version
npm --version
git status --short
git rev-parse --short HEAD

TIMEFORMAT='unit wall=%3R user=%3U sys=%3S'
time node requirements/verification-system/tests/run.mjs

TIMEFORMAT='surface wall=%3R user=%3U sys=%3S'
time TESTS_MJS_FILES=requirements/js-semantic-surface/tests/surface-charter.test.mjs \
  NODE_TEST_CONCURRENCY=1 NODE_TEST_VERBOSE=1 \
  node requirements/verification-system/tests/run.mjs

TIMEFORMAT='capacity wall=%3R user=%3U sys=%3S'
time TESTS_MJS_FILES=requirements/execution-model-routing/tests/capacity-soak.test.mjs \
  NODE_TEST_CONCURRENCY=1 NODE_TEST_VERBOSE=1 \
  node requirements/verification-system/tests/run.mjs

TIMEFORMAT='harness wall=%3R user=%3U sys=%3S'
time node requirements/verification-system/tests/integration/harness/run.mjs
```

这些结果是第 2 节的调研证据，不是后续实现后的验收结果。正式验证入口有输出，没有把返回码管道给 grep 后误读为成功。

### 19.2 后续每批的最小验证

纯测试／JS runner 的局部红绿循环先跑对应真实入口或 node --test 的明确文件。消费 dist 的测试必须先确认构建内容凭据有效；输入变更后按仓库正常构建流程刷新，不能习惯性使用 --skip-staleness-check。

| 批次 | 最小验证 | 扩大验证的条件 |
|---|---|---|
| T0 | 新 verification-inputs 与修订后的 proof-ladder、reporter-supervision | 改实际默认 runner 后跑全 unit |
| T1 | Surface charter、catalog、tip-v2 相关 suite | 更改公共 loader 或 Surface metadata 时跑领域集合和 build |
| T2 | 受影响领域与 harness | 删除公用 helper 后跑所有实际 importer 所在范围 |
| T3 | 必要边界 scanner 反例、build、全 unit | 改 shared import scanner 必须检查全部消费者 |
| T4 | 对应领域行为 suite | Host／durable／跨域端口改动时跑对应真实 integration |
| T5 | owner-impact 固定/property、Watchdog 时间与物理 child | 改编译器输入 planner 时跑真实 compiler group |
| T6 | 真实 Node fixture、reporter、supervisor、coverage fixture、harness | 必须在 Node20 与本机版本分别验证事件边界 |
| T7 | 日常／发布计划与发现集合测试 | 最终跑两种正式入口，确认实际选区 |
| T8 | format-build-test 与 verify:release | 失败不重跑到绿；定位根因并补永久回归 |

新增 verification-inputs.test.mjs 等文件目前只是设计，不能在尚未实现时把它们列为“已运行通过”。正式回归放 requirements 对应包内，不以 /tmp 下手写一次性命令代替提交后的自动化测试。

### 19.3 实现完成后使用的入口

下列 --profile/--verbose 行为是本方案要求打通的目标，不表示当前每层已经正确传播了这些选项。

```bash
npm run format-build-test
npm run verify:release

npm run format-build-test -- --profile
npm run format-build-test -- --verbose
```

release 单次运行内部只编译一次 clean 产物；独立执行日常和 release 是为了验两个入口各自的行为，不是让 release 内部重复两轮构建。正常开发无需每次把上述四条全部执行一遍。

失败的命令保留其失败结论，不用更窄子集的绿色结果覆盖。局部 run 可用于定位，但最后要恢复实际受影响的正式范围。

## 20. 怎么判断这轮是真的精简了

### 20.1 正确性先于耗时

先检查是否消除了四种假绿来源：空输入快照、测试内复制 runner、两个统计口径、流未排空就确认完成。随后确认没有因为删检查而失去产品拒绝、持久化和物理回收能力。

一个原本无效的测试删除后，不需要另一个无效测试来证明它已经删除；一个真实约束的弱测试删除前，则必须有明确接替。审查依据是具体反例与实际调用，不是总用例数不变。

### 20.2 性能比较口径

同一机器、同一 Node／依赖、相同资源负载，分开记录冷构建、已有产物的日常运行、单独 unit、单独 harness 和 release。不要拿本次有产物 unit 与未来冷 release 比速度。

对轻量可重复的目标运行可取五次墙钟的中位数，同时列出范围和离散程度；物理 Host/release 成本较高时减少基准重复并说明样本数。profile 是测量，不是把不稳定测试反复跑到通过。

记录：阶段墙钟、叶子耗时总量、慢文件、实际 worker 数、额外 compiler/Host 进程数、默认终端行数、日志字节与可用时的峰值内存。不要为了每个指标安装一套新监控依赖；没有测到的留空。

### 20.3 可以预期、但不能预先承诺的收益

| 动作 | 证据 | 合理预期 |
|---|---|---|
| Surface 三次同输入变一次 | 本次后两次合计约2.56秒 | 确定减少两次相同计算，不保证全量墙钟等额减少 |
| 取消 static proof authority | 现有约450行级别的分析链及专用反例 | 减少长期维护与 AST 工作；先完成规范修订 |
| owner-impact 生成图不逐次落盘 | 当前100个样本均写项目／源文件再多次解析 | 减少系统调用与重复 XML 解析；保持100样本与原性质 |
| Watchdog 时间用例离散推进 | harness 中恢复默认窗口约5.03秒 | 时间语义测试不再等满真实窗口；物理子进程用例仍有真实成本 |
| 收敛默认输出 | 当前 unit 原始JSON、模拟PASS；harness279行通过 | 成功日志只剩阶段摘要，故障更容易看见 |
| 并发实测后收口 | soak 单独2.07秒／全量5.83秒 | 可能缩短慢尾和降低资源峰值；不能仅凭CPU核数定结论 |

不制定“删掉30%测试”“日常必须5秒内”一类无依据硬指标。只要求本次消掉的工作不以另一个 runner、baseline 或 registry 形式重新出现；保留测试的实际判别能力不下降。

### 20.4 最终交付检查

确认所有删除有对应理由，所有混合文件保留的行为仍在；无 dangling import、旧环境变量、无人消费的 helper、虚假迁移 façade；README/HOW 指向实际入口，GAP 未被绿色计数冒充关闭。

确认默认输出没有每项通过、模拟发布成功、expected fatal 故障墙；真实失败仍有位置、原因、反例和日志，count 和 exit verdict 来自同一运行事实；中断不会留下本次 child process。

确认正式日常没有被局部环境变量缩小，发布独有测试真正执行，所有 manifest 内容证据与真实包闭包仍有效。最后检查 Git 只包含本轮有意提交的源码／测试／文档，不包含 dist、日志、临时目录和用户配置。

## 21. 调研边界与依据

### 21.1 本文覆盖到哪里

本轮实际运行了全 unit 和独立 harness，并精读了编排／结果流、Surface charter、部分元测试、规则库、capacity soak、owner-impact property 和关键 harness 源码。第 6.2 节明确标出仅检索到形状断言、尚未逐条精读的候选。

没有声称人工审过625份 unit 文件和279个 harness case 的全部断言；没有声称已经确定最终应该删除多少条；没有测过整个 integration、clean build、E2E、真实 package 或 coverage。最终删减数量应在逐项迁移完成后由实际运行结果给出，而不是在计划阶段编造。

当前测试通过只能说明当前入口的已有断言通过，不能反证本文发现的空证明不存在。输入快照用例正说明：测试可以绿，但所声称的事情根本没测。

### 21.2 仓库依据索引

| 主题 | 可复核位置 |
|---|---|
| 实际执行范围 | package.json；scripts/verify.mjs；.github/workflows/ci.yml |
| 快照缺陷与空证明 | verify.mjs::hashTreeFiles/takeInputSnapshot；proof-ladder.test.mjs 的 snapshots input state 用例 |
| 双计数与丢字段 | compact-reporter.mjs；run-inner.mjs 的 IPC 事件投影；supervise-node-test.mjs 的 pass/fail 累加 |
| 镜像 runner 测试 | reporter-supervision.test.mjs 的 handleComplete 和临时 runnerScript |
| 重复 Surface 整仓校验 | surface-charter.test.mjs 的三个 validateSurfaceManifest 调用 |
| 仍在运行的登记治理 | js-surface-manifest.mjs::analyzeSurface/validateSurfaceManifest；js-semantic-surface/WHAT.md 的003条 |
| 文案／注释测试 | boundary-exemption-ratchet.test.mjs；degradation-list.test.mjs；reliability-runbook.test.mjs |
| 真实混合行为 | domain.meta.test.mjs；behavior-diagnosis/tests/catalog.test.mjs 与 tip-v2-contract.test.mjs |
| 图性质的物理重复准备 | structured-workflow/tests/owner-impact-compile.property.test.mjs::writeFixture/verifyGraph |
| 容量逐步校验 | execution-model-routing/tests/capacity-soak.test.mjs::createAuditor |
| 独立 harness 与慢等待 | integration/harness/run.mjs；timeout/budget/unit-runner 等 case；e2e/support/watchdog.js |
| 现有虚拟时钟接口 | verification-system/tests/support/temporal-harness.mjs::createVirtualClock |
| 发布组选择 | integration-node-test-steps.mjs::integrationNodeTestSteps/selectIntegrationSteps |
| 内容凭据 | scripts/lib/build-state.mjs；verification-system/tests/support/build-freshness.mjs |

索引中的短路径按正文给出的对应目录解析；所有结论绑定 ec6c363bd。本方案实施时若 HEAD 已改变，先复核相关实际代码，不恢复已经被用户删掉或替换的路径。

### 21.3 外部技术依据

- [N1] Node.js 官方 Test runner 文档：https://nodejs.org/api/test.html 。调研页面为26.8.2；用于核对 TestsStream、reporter 与执行模型，不代表本机或CI已升级。
- [N2] Node.js 官方 Stream 文档：https://nodejs.org/api/stream.html 。用于核对背压、pipeline/finished 与错误／结束边界。
- [N3] fast-check 官方 Configuration：https://fast-check.dev/docs/configuration/ 。用于核对每次 assert 的 seed、run budget、报告配置及其作用范围。
- [N4] Node.js 20 官方 Test runner 文档：https://nodejs.org/docs/latest-v20.x/api/test.html 。调研页面为20.20.2；它没有把 test:summary 列为公开事件，因此不能让当前CI依赖新版本才公开的 summary 协议。

外部文档用于校准库边界；仓库实际调用和支持版本上的永久可执行 fixture 才能证明本项目接线正确。本文没有引入第三方测试框架或把新版本 API 当作已安装能力。

### 21.4 本次交付与构建语料的关系

本次新增的唯一文件是 CLEAN.md。它是待实施计划，不是新的产品规范，不应被复制到 AGENTS.md 变成长期执行负担。

仓库的 LoopDetector 语料选择会读取 Git 跟踪的源码／文档。本文被纳入 Git 后，也可能成为该构建输入的一部分；此前的测试基线不能据此宣称覆盖了新增文档后的最新产物。下一次正常构建应按内容凭据刷新相关派生物，不手工修改 dist 来伪造新鲜度。

本轮新增文档后只核验文件内容与差异，未重跑发布链。实施各批后，再按第19节完成对应验证。
