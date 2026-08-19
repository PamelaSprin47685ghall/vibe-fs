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
- 只要需要→并行调用多个工具：并行读取+并行编辑+同文件+异文件=绝对安全。
- 强烈鼓励对同文件+异文件提交大量并行编辑。
- 并行工具执行顺序≠线性(系统不保证顺序)→∃依赖时禁止高并发调用。
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

# 文档生命周期

正式语义在 `requirements/<package>/`（每包 WHY/WHAT/HOW + 测试）。
deferred 未来材料归 `proposals/`。
Proposal 的提出、讨论和裁决发生在 Agent 执行工作流之外，由用户或负责人管理。
- 普通小型修复、局部重构、测试或格式修复不要求创建 Change；能在一次修改内完整对齐
  requirements 文档、实现与 proof 的工作直接闭合，不为流程制造空壳 Change。

## 修改纪律

- 工作区可能包含用户改动。修改前查看 `git status` 和相关 diff；保留无关改动。
- 自动提交 git commit。允许推送 `master`；禁止 force push master。

---

# 当前义务账 — 2026-08-18 验收后

我按这份 Repomix 中的生产源码做了全仓复核；它本身是整个仓库的合并表示，适合做这种 repository-level review。 结论是：**现有 `0 GHOST remains` 不能作为“状态机债务已经归零”的语义结论。**它主要证明旧的词法形状消失了；真正剩余的问题恰好集中在你说的“分形 CE 跨边界”上。

仓库自己的规范其实已经把判据写得很准确：F# 调用栈应该就是 workflow stack，不应长期保存“下一步去哪”；跨模块审查还明确要求检查 control token、registry presence、`Advance/Tick/Resume/Step` 以及 recovery 跳入内部 continuation。  按这个判据，我认为当前剩余债务如下。

| 优先级         | 剩余债务                                                | 判定                                                                      |
| ----------- | --------------------------------------------------- | ----------------------------------------------------------------------- |
| **P0**      | Provider recovery 的 `RecoveryArming + AttemptPlans` | **确定的跨 callback 状态机/continuation 存储**                                   |
| **P0**      | Strength `CounterfactualAwait`                      | **旧状态机只是换形，没有真正 CE collapse**                                           |
| **P1**      | `LoopSensor.armed` 跨 Host→Application 驱动 recovery   | **physical state 越界成为业务 PC/cause token**                                |
| **P1**      | `NeedHelpSensor.armed` 跨 abort→idle 驱动 assistance   | **同上，而且跨两个 Host callback**                                              |
| **P1/争议**   | Change `JobProgress` recovery dispatch              | **很接近 durable resume-address；目前靠“这是 evidence”自证合法**                     |
| **P1/边界决策** | Sphinx `PendingRequest → nextTool`                  | **如果 Sphinx 属普通 workflow，就是明确的分布式解释器；若它是领域 protocol automaton，则必须正式豁免** |
| **P2**      | `ManagerFinality.EndingDisposition`                 | **非持久状态机，但仍是 child action-opcode → caller effect 的 CE seam 债务**         |
| **P0 防线**   | 现有 structured-workflow scanner                      | **无法发现上述跨文件/跨 callback 等价状态机，因此制造“0 GHOST”假阴性**                         |

### 1. 最严重的是 Provider recovery：现在仍然在保存 continuation

`PluginRecoveryScope.RecoveryArming` 自己的注释已经把本质说出来了：它连接两个没有共同调用栈的 async entry point——失败后的 `HostTurnObserver.observe` 与下一次 `XWire.applyTransform`；并且直接称它为“一次自动 recovery sequence 的 local control-flow fact”。

这恰恰不是“physical resource”的充分证明，反而是在描述一个**被 heap 化的 continuation**：

`observe failure → [RecoveryArming] → 下一次 transform → plan retry`

`AttemptPlans` 更明显。transform 阶段生成 `AttemptPlan`，存进 Dictionary，等以后完全独立的 reconciliation callback 再取出来；源码解释说不能重算，因为 projection 可能已经变了。 也就是说，它保存的不是 socket、waiter、lease，而是**“当时 workflow 已执行到这里时冻结下来的决策上下文”**。

而且这已经不是理论洁癖，源码记载了两个真实事故：

一处是 Blog frame 稍晚到达，`hasMaterial=false`，导致 probe 被跳过并且 **`ClearRecovery burned the armed slot`**。

另一处是 provisional/unknown turn 时过早清 `AttemptPlan`，后来的 `TurnCompleted` 读到 `TryAttemptPlan=None`，实测结果是 **`FallbackCursorAdvanced without PrefixRebaseCommitted`**。

这两类 bug 本质完全一致：**你把调用栈该保持的局部变量拆到了两个 callback 之间的共享状态里，于是“什么时候清除”变成了新的 transition law。**

正确方向不是把 `RecoveryArming`、`AttemptPlan` 再塞进 Journal。那只是把 process-local PC 升级成 durable PC。应该让一个 owning recovery CE 持有这些值，Host callbacks 只作为 rendezvous/observation adapter；确实需要跨 callback 保存的物理身份，则上升为 opaque typed capability，而不是 `TryGet/Clear` 型共享 workflow cell。

### 2. `CounterfactualAwait` 是典型“改数据结构而没有消状态机”

旧 census 曾明确把 Strength 的两个 pending dictionary 判为 ghost，并给出的修复就是：

`let! first = observeFirst …`
`let! second = observeSecond …`

也就是把两阶段重新变回调用栈。

现在只是变成了单个：

`CounterfactualAwait = AwaitFirst ... | AwaitSecond ...`

再放进一个 Dictionary。自查因此把它标为 “EXORCISED / PhysicalResource”。

但从控制语义看：

`AwaitFirst → 收到 first callback → AwaitSecond → 收到 second callback → remove`

一点没变。**两个 Dictionary 合并成一个 DU Dictionary，只减少非法组合，没有消除 program counter。**“transition 是 pure fold”也不改变这一点；纯函数可以实现状态机，关键在于这个 DU 是否长期保存“程序现在等第几个 observation”。

这里应该真正变成两次 observation 的 CE，或者让一个低层 collector/waiter 对外只返回完整的 `CounterfactualPair`；`AwaitFirst/AwaitSecond` 若必须存在，也只能封死在 physical adapter 内部。

### 3. `LoopSensor.armed` 的 storage 可以是 physical，但它已经泄漏成业务控制权

`LoopSensor` 明说：abort 是 fire-and-forget，真正的 AABB bridge 要等**以后**的 `TurnAborted`，条件就是 armed mark 仍存在。

随后 Application workflow：

`IsArmed → ClearArmed → bool`

再以这个 bool 决定：

`true → continueAfterLoopKill`
`false → 普通 Aborted`

代码就在这里。

这精确命中 SW-017 的“parent 读取 child registry/mutable-cell presence 推导下一业务 effect”。

修复不要求删掉 Host sensor 内部的 one-shot latch；应该删的是 **Application 对 `IsArmed/ClearArmed` 的观察权**。Host boundary 应直接产生例如 `AbortCause.LoopKill permit` / `AbortCause.External` 这样的 typed outcome，CE 消费一次 outcome 即可。

### 4. `NeedHelpSensor.armed` 是同一问题，而且更“分形”

NeedHelp sensor 内部保存 armed provider attempt；之后 Assistance workflow 在 `TurnAborted` 上读取 `sensor.IsArmed`，决定是否转入 assistance handling。

但还没结束。它又要等下一次 fresh `SessionIdle` 作为 transport fence，随后 `sensor.TryTake(...)` 成功才真正发 escalation continuation 或创建 consultation child。

所以实际结构是：

`stream sentinel`
→ `arm`
→ `abort callback`
→ `IsArmed`
→ `claim`
→ `idle callback`
→ `TryTake`
→ `send escalation/create child`

这就是典型的**一个本应连续的 CE 被 Host callback 边界切成三段，再靠 HashSet presence 拼回去**。

它需要一个真正的 `AssistanceAbortClaim`/one-shot capability，把 abort cause 与后续 idle fence 放进一个 owning CE；而不是让业务层反复询问 sensor 当前还记不记得上一阶段。

### 5. `JobProgress` 是我认为最需要重新裁决的 durable 状态债

这一项没有前四项那么绝对，因为作者确实做了大量努力，把每个 case 都附上真实 evidence，并明确写着 “NOT a program counter”。

问题在消费端：

`ManagerStarted → awaitAndPublish`
`CandidateReady → publishEventually`
`ConflictPending → resolveConflict`
`RebasedCandidateReady → reenterRebasedCandidate`
`PublishClaimed → reenterPublishClaim`
`Published/Failed/Abandoned → cleanUp`

源码甚至直接写着：**“Each JobProgress case maps directly to the CE effect that advances the job.”** 

这已经非常接近：

> durable projection 中保存 resume address，然后 restart 时 switch resume address。

尤其 `ManagerStarted` 本身几乎就是 phase；`ConflictPending` 也同时承担“事实”和“现在该执行 resolveConflict”的含义。

这里我不建议粗暴删除那些 Journal facts。`CandidateReady`、`ConflictDetected`、`PublishClaimed`、`Published` 都可能是正当 durable facts/effect claims。应清掉的是**把它们 fold 成一个唯一“当前 Progress case”，然后 case→下一程序地址一一映射**的权威性。

更稳的形式是：semantic entry 从一组 durable facts + 当前外部 reality **重新证明 outstanding obligation**，然后进入普通 CE；而不是恢复一个 latest-stage enum。也正因此，不应该为了“去状态机”再多积分几条 `ResumeAtXxx` 日志。

### 6. Sphinx 仍然有一个非常标准的外部 program-counter seam

Sphinx 当前把 kernel 的 `PendingRequest` 翻译成：

`SemanticAssessmentRequest → assess`
`GenerateCandidatesRequest → propose`
`InvestigateRequest → investigate`
`SynthesizeRequest → synthesize`

并把结果作为 `nextTool` 暴露给外部 caller。

连错误恢复也告诉 caller “call the tool named by nextTool”，并从 `PendingRequest` 计算 `ExpectedTool`。

从 distributed-interpreter 的判据看，这是非常标准的：

`child internal PC → seam nextTool token → external caller executes next instruction → resume child`

这里唯一的问题是**scope**。如果 Sphinx inquiry 本来就是一个有意建模的 epistemic protocol automaton，那么它可以合法存在，但应该明确写成 structured-workflow 的 protocol-boundary exemption；不能一边把它作为协议状态机，一边用 “SessionLifecycle 已删” 证明状态机已经归零。

如果它也属于普通 CE workflow，则应该收成一个 `resume/observe` 入口，caller 只提供 observation，不获得“下一 phase 应调用哪个内部 operation”的解释权。

### 7. `ManagerFinality.EndingDisposition` 不是持久状态机，但仍然暴露了 child action opcode

`EndingDisposition` 包含 `ResumeRequest`、`RecoverRequestWithoutReviewers`、`CompleteBlessedLife`、`BeginFinality` 等。

Tool 层随后逐 case 调不同业务效果：

`ResumeRequest → resumeFinalityRequest`
`Recover... → recoverEmptyMembers`
`CompleteBlessedLife → completeBlessedEnding`
`BeginFinality → beginFinalityEnding`。

它没有长期存储 PC，所以严重程度低很多；但从 CE fractal closure 来看，Finality 模块返回的是“caller 下一步做什么”的 opcode，而不是完成后的领域 outcome。

更干净的切法是让 Finality-owned CE 把 resume/recover/begin/complete 本身执行完，只向 Tool adapter 返回诸如 `Accepted / Refused reason / AlreadyCompleted` 这种边界结果。

---

### 为什么现有门禁会漏掉这些

这是此次最重要的一层债务。Round-3 census 的“0 GHOST”依据仍主要是：没有新的 `State/Stage/Phase/Step/Armed/Pending/InFlight` 可疑声明、所有 mutable 都有分类注释，然后把 RecoveryArming、AttemptPlans、NeedHelp/Loop armed 和 `CounterfactualAwait` 判成 PhysicalResource。

但 HOW 自己承认，`registry-joint-branch` 只识别**同一位置 direct/try probe 两个 registry**的语法；分散在不同函数/模块里的 presence 联合必须人工证明。

因此现在的核心漏洞是：

**`DSL-MUTABLE: resource` 是声明，不是证明。**

把“两张表 + implicit phase”改成“一张表 + DU phase”，或者把 `IsArmed` 的 producer 和 consumer 拆到不同文件，现有 gate 很容易自动变绿，但 transition graph 根本没变。

下一轮门禁不应该再主要 grep 名字，而应该守这条结构不变量：

> **任何 mutable/registry value，只要在 callback A 写入，在 callback B 读取，并且 B 的 presence/value 决定下一个业务 effect，它就必须被证明为 opaque physical capability/outcome；否则按 cross-boundary program counter 处理。**

这能同时抓住 `RecoveryArming`、`AttemptPlans`、`CounterfactualAwait`、Loop/NeedHelp，而不会误杀 PTY、timer、waiter、single-flight。像 `SessionQuiescenceGate` 就是很好的合法对照：它明确只是 process-local side-effect admission gate，重启清空，并通过 `QuiescencePermit` 控制物理发送资格，不声称业务 lifecycle。

所以我会把当前真实状态概括成：**文件内的大型手写状态机基本清干净了；剩下的主要不是“State 类型”，而是被拆散在 callback、registry、durable projection 和 module seam 之间的 continuation。**其中最优先应清的是 `RecoveryArming/AttemptPlans → CounterfactualAwait → Loop/NeedHelp armed seam`；同时升级门禁，否则下一次仍会出现“数据结构改名后 census 归零，实际 transition graph 未变”的情况。
