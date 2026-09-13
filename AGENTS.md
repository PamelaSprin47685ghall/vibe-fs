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

# 万象术 fatal 地毯式治理提案

审计日期：2026-09-14，日本时间。
代码基线：master，HEAD 5b57b32a6，带审计开始前已有的未提交改动。
本轮：只读审计、运行现有测试、交付提案与初始清单。没有修改工作区，没有提交或推送。

## 一、结论与边界

目标不是让 fatal 数量归零，而是让每处 fatal 都有说得清、验得出的根据。

每个入口必须收敛到四种处理之一：合法执行不可达，并有封闭前提下的证明；真实可达，修复制造非法状态的上游；真实的完整性破坏，保留熔断并证明其处置顺序；不是生产入口，给出可达性依据后移出清单或删除死代码。属性测试是强证据，不是全称证明。

目前同时存在四种问题：可达外部失败被升格为内部矛盾；fatal 前的结算没有真正完成；测试没有触及生产实现或允许 fatal 返回；真正需要熔断的路径使用可选 handler，可能根本没有接线。

这份文档给出审计发现和实施方案，不宣称完成了全仓不可达证明。附表覆盖本次词法普查的 37 条直接 fatal 引用及 11 个 Magic Todo 上游入口；178 个文件中的 533 条异常/偏函数候选还需要逐条做生产可达性归属。这几个数字不能相加成“缺陷总数”。

代码位置在下文省略共同前缀 src/Wanxiangshu/。行号对应审计时工作区，实施前应按符号重新定位。

## 二、已经取得的证据

### 2.1 真实测试基线

执行了标准 runner，没有绕过构建新鲜度检查，没有临时修改测试，也没有把现有改动据为本轮修复。

```sh
TESTS_MJS_FILES=requirements/execution-failure-policy/tests/policy.test.mjs,requirements/host-boundary/tests/diagnostics.test.mjs,requirements/host-boundary/tests/chat-hook-settlement.test.mjs,requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs,requirements/context-compression/tests/enforcer-cycle-convergence.test.mjs,requirements/context-compression/tests/m6-fatal-boundary.test.mjs \
node requirements/verification-system/tests/run.mjs
```

runner 确认构建为 current：1783 sources、875 artifacts。6 个文件，38 项测试，37 通过，1 失败。

失败项：

```text
requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs:286
WHAT[ENF-021] repair settlement failure rejects every waiting observer without fatal or release

:295
expected: ['rejected', 'rejected']
actual:   ['fulfilled', 'fulfilled']
```

这是现有工作区的测试结果，不是新增回归的结果。没有运行全仓所有测试；没有运行真实 Host canary。

### 2.2 结算失败却通知成功——已复现

Context/Companion/Blogger/Runtime/Coordinator.fs:132–144 的 abandonAndResolve，在 abandonEpisode 抛错后仍先 Resolve(AbandonedExhausted)，再调用 FatalProcess.trip。

因果链：journal 关闭 → durable abandon 失败 → catch 宣布 AbandonedExhausted → 等待者看到 fulfilled → 物理 fatal。关闭退出的测试里尤其清楚：错误被回写成了成功分支。

修复不是换一个报错文案。rendezvous 必须支持同源失败传播，所有已接收但未完成的观察者都收到同一个失败；失败时不宣称 abandonment 已提交，不发成功 terminal，不释放 flight。后续到达者也不能挂在已经结束的 owner 上。成功、失败、取消、supersede 都要有封闭的 episode 结束规则。

### 2.3 fatal 之后安排结算——代码直接证明顺序错误

Enforcer/Continuation.fs:491–503：fatalClearWorking 先 Diagnostic.fatal，再 BloggerAbandon.openRequest，再 releaseExact。

Interaction/Repair/InteractionRepair.fs:75–86、142–150：NotifyTerminal 在 fatal 之后。

Foundation/FatalProcess.fs:16–25：生产使用 SIGKILL，失败才尝试 process.exit(1)；测试环境变量会让 kill 返回。真实退出后不能指望这些后继语句或 finally 完成清理。Node.js 官方 Process 文档也明确：强制退出会丢弃尚未完成的异步工作，SIGKILL 不能靠退出监听器补救。

不能全仓机械地把 fatal 搬到最后：NotCommitted、Committed、Unknown、semantic cut 对应的合法后继不同。尤其 Unknown 不能为了“结算”再写 abandonment 或重放 effect。

### 2.4 修复只覆盖了一部分分支

现有未提交改动已把 invalid-cardinality/interrupted 路径的 AbandonedExhausted 改成 StopPhysicalRun，也让 BloggerAbandon 检查 append 结果。这些改动不是本轮完成的。

但 Continuation.fs:513–542 的空文本路径仍把 AbandonedExhausted、NudgeSent、AabbSent、Completed 合并进 fatal。它们不是同一种状态，不应共享一个无差别失败分支。

进一步，Continuation.fs:861–880 的 ensureNonEmpty 同时用于“发送消息”和“停止本轮”。停止本轮不应要求先构造一份可发送的非空正文。BlogSurface.continueTerminal 自己注入 Stop 函数，不能代替真实 handleContinuation 的这条边界证明。

### 2.5 返回 StopPhysicalRun 仍不保证物理执行停止

Continuation.fs:958–1010：requestPhysicalStop 将任务 ignore；handlePhysicalStopResult 只有在 terminateSession 返回 Ok 后才 suppressProviderStep/releasePhysicalExecution。applyContinuationOutcome 可以先返回 Host。

可以确认的事实是：代码没有在 hook 返回前建立停止屏障。真实 Host 是否会在这段窗口再次进入 provider，尚需契约测试，而不是凭单元测试的枚举值认定已解决。

修复方向：将确切 execution 的停止决定在下一次 provider admission 之前生效；物理 abort 可以独立完成，但不能承担唯一的禁止继续执行职责。不要简单 await 一个可能与 Host hook 互相等待的 abort。用可控 Promise 压住 abort，测试真正注册的 hook 和 provider 入口。

### 2.6 错误边界制造了没有证据的身份和 phase

OpenCode/Host/PluginHostInterop.fs:295–322：未识别异常默认变成 LocalInvariant、NoAcceptedFact、NoOwnedExecution，ExecutionKey=None。

:240 附近还有 placeholderKey；policy 输入 Capacity 固定为 NoCapacityFence。

问题不是最后一层必须识别所有异常，而是“无法识别”不能证明“没有已接受执行、没有资源”。hook 可能在 await 或 effect 之后抛错。把未知位置填成未执行状态，会绕开实际结算义务。

修复方向：registered hook 从实际 owner 取得 exact key、phase、资源与提交证据；边界 I/O 异常在最早可信点类型化。装配/程序缺陷仍 fail-loud，不能把全部 hook 异常改成 ProviderTransient。EXECFAIL-009 只规范 Host 上报的 session/provider 错误，不授权清洗任意插件故障。

### 2.7 Detached transport 丢失了晚到结果的归属

OpenCode/Host/OpenCodePort.fs:125–152：SDK/HTTP 的后台发送失败都直接 fatal，仅携带 session 和文本。

同文件 :213–220：SDK Promise 尚未得到最终结果，就返回 AdmittedWithReceipt。这个 receipt 本身被代码说明为 invocation/transport receipt，不是 physical acceptance。

Interaction/Dispatch/Send.fs:182–237 又有一层 detached observer，把 Retryable、Fatal、AcceptanceUnknown、异常压成字符串，然后 fatal。

要修的是跨层交付契约：一个 exact PromptKey 的晚到结果必须回到 dispatch owner。确定未发送可按既有规则 abandonment；已接受不重复发；结果未知保留 pending 并核对事实。不能为了去掉 fatal 而等待整个 provider 生命周期，也不能在“没查到消息”时当作未发送。

### 2.8 Magic Todo 把不同失败压到一个漏斗

Mission/Obligation/Todo/MagicTodoMembrane.fs:474–704 的 fatalInfrastructure 有 11 个上游调用，涵盖 Host 字段缺失、缺 port、prepare 拒绝、快照不可用、locality/materialization 失败、XTrace append、accept、after 找不到 prepare。

“快照暂时不可读”和“确切 tool identity 被两份内容占用”不能同判。需要把每个已有拒绝联合类型穷尽到底，保留真实提交状态；不能继续用 string 传到末端再猜。

### 2.9 真熔断可能没有接线

Strength/Persistence/Durability.fs:15–17、50–51，以及 Repository/Knowledge/Casebook/Store.fs:127–129、155–156：fatalTripHandler 默认 None，以 Option.iter 调用。

本次全仓名称扫描只找到 setter 的定义和签名，未找到注册调用。因此可以确认“源码中没有找到接线”，还不能把它夸大为对所有运行环境的动态证明。实施时必须核对装配和公开导出。

修复方向：能力若必需，构造时就必需；不使用可选全局变量，也不加默认 handler 掩盖装配缺口。若设计决定把 typed cut 上送 owner，就删除死 handler，让唯一 owner 执行。保留两条半生不熟的路径最危险。

### 2.10 当前一些“证明”只证明了测试自己

requirements/structured-workflow/tests/support/m6-boundary-proof.mjs:3–40 自定义 validateFatalBoundary，再给它喂人工构造的 legal 对象和几个人工 mutant。它不读取生产依赖，也不调用生产 fatal 链。

context-compression 的 m6-fatal-boundary.test.mjs 只有一次 assertFatalBoundary('context-compression') 调用。扫描还发现多个其他 requirement suite 使用同一 helper。

这不是说这些测试断言写错，而是它们不能作为“生产代码已注入、已结算、仅退出一次”的证据。可以用来验证一个真实产品中的描述器校验器；现在却没有连到那个对象。

## 三、普查不能只搜 fatal

### 3.1 三层清单

第一层是物理出口：FatalProcess 的 kill/trip、Diagnostic.fatal、直接 process.exit/kill、自身进程未捕获异常。正常子进程终止和进程存活探测分别登记，不能误算。

第二层是转入出口的边界：fatalInfrastructure、ReportFatalDiagnostic、TripFatal、可选 handler、HookFailurePolicy.FatalAfterSettlement、异常的统一归类、detached observer、初始化 catch。

第三层是可能到达这些边界的上游：failwith/failwithf、invalidOp/invalidArg、raise/reraise、Option.get、Map.find、head/tail/last/find/item/reduce、索引访问、非穷尽匹配、obj/unbox/Emit、JSON parse、跨语言 SDK 调用，以及无观察者的 Task/Promise。

本次 533 条只是一个较窄正则的候选行数，还没有包含上述所有形式，且包括测试 surface 与注释。不能把这个数字宣传为异常入口的全量。

### 3.2 生产可达性

从实际发布入口和 Fable 编译/导出关系反向追踪，不按文件名猜测。Surface.fs 既可能是测试桥，也可能是真实生产接口；整个目录豁免不可接受。公开且可由下游调用的 surface 也要明确合约。

fsproj 引用、F# 调用边、注入能力、Emit 内 JavaScript、发布产物链接关系都要纳入。源码清单为主，最终产物只做链接/边界核验，不修改生成物。

当前 code_map 工具报错，已转用目录、源码检索和精确读取；没有借此宣称已经做完自动调用图分析。

### 3.3 清单字段与门禁

建议新增 requirements/execution-failure-policy/fatal-inventory.json。它是入口索引，不是第二套产品策略。

每条记录：稳定 ID、owner requirement、source symbol、触发分支、输入来源、exact identity、phase、被断言的不变量、提交/资源处置、影响范围、证据类型、正式测试 ID 或证明位置、状态。

稳定 ID 不使用行号。行号从源码生成，仅用于定位。一个汇入口的十一个分支不能只登记一个 ID。记录状态建议仅用 Open、Proved、PropertyChecked、FixedWithRegression、RetainedFuse、ExcludedWithEvidence。

门禁比较“扫描出的入口集合”和“有依据的清单集合”。新增入口、未登记别名、入口迁移后证据指向旧实现、测试名称失效都失败。旧存量可以暂时保持 Open 供分批治理，但新增及本批声称关闭的入口不得是 Open；最终发布验收要求治理范围内 Open 清零。不得靠把全部旧入口加入永久豁免来通过。

扩展现有 scripts/check.mjs / scripts/checks 体系，不另造测试 runner。扫描器自己要有正反样例：包括别名、动态转接、Emit、注释中的假匹配和无调用的声明。纯文本扫描不能独自证明完备性；必须由物理出口的模块边界约束和装配契约补上。

## 四、证明要证明什么

### 4.1 不可达证明模板

对入口 f，定义状态不变量 I、合法初态 S0、允许的命令和环境输入 A、生产迁移 T。

```text
1. 初态：∀ s ∈ S0, I(s)
2. 保持：I(s) ∧ Pre(s,a) ∧ T(s,a)=s'  ⇒  I(s')
3. 排除：I(s) ∧ Pre(s,a)  ⇒  ¬Trigger_f(s,a)
4. 边界：所有进入内核的输入都实际建立了 Pre，且所有构造/恢复/写路径都属于 T
```

漏掉第 4 步就是“开发时想当然”的数学版本。不能把结论本身放进前提，例如先假设“回调永不过期”“存储永不失败”，再宣称线上入口不可达。

证明应尽量短，紧靠真正 owner 的 HOW 与生产构造器。类型封装、有限枚举、纯函数局部推理优先；不为了这次治理给整个工程加证明框架。

### 4.2 已找到的局部证明候选

BloggerMainContext.mainContextFromChunk（Context/Companion/Blogger/MainContext.fs:78–142）：

```text
返回 Some(Main c)
⇒ 通过 isAfter(next, previous) 分支
⇒ c.NextIngestedThroughSequence > c.PreviousIngestedThroughSequence

c.Toml = chunk.Toml
c.DeltaDigest = BlobDigest(SHA256(chunk.Toml))
⇒ c.DeltaDigest = BlobDigest(SHA256(c.Toml))
```

这里 isAfter 在 Context/Trace/Cursor.fs:43 明确比较 sequence 大小。第二条只需要同一确定性散列函数和同一输入，不需要假设 SHA256 无碰撞。

但这不是全局证明：Request.fsi:5–18 暴露可任意组合的 record；Enforcer/Cycle/Recovery.fs:294–344 会直接接受非空 delta_digest，读取 prev_ingest/next_ingest，没有同等不变量校验；RuntimeSurface/BlogSurface 也能组装 record。

实施：把“已验证请求”构造权交回 owner，外部只读字段；恢复先解码未验证数据，再校验 digest、覆盖推进、request/epoch/owner 绑定及必要上下文，最后构造。损坏、版本不兼容、I/O 不可用不能统一变 None 或默认零。相同 typed constructor 服务新建与恢复，测试桥不得绕过。

性质：合法请求 encode→decode 恒等；边界被破坏时明确拒绝；任一成功构造都满足两个不变量；旧 epoch 不取得新请求提交权。对资源耗尽、运行时崩溃等外部条件另列适用范围，不夸大局部证明。

### 4.3 属性测试不能靠自证

测试必须调用当前构建的生产入口。测试模型只记录少量独立事实，例如当前 request、已知 pending key、已提交事件、确切 fence；不能复制生产 decision 再拿两份相同逻辑比较。

随机数固定主 seed，并允许记录/重放 seed、path。采用 fc.commands 时还保存 replayPath。失败缩减出的命令序列必须落成独立回归。固定 seed 与新增已记录 seed 并用，避免永远只探索一组轨迹。

并发用可控 Promise、fast-check scheduler/scheduledModelRun 或现有 owner 测试端口，不靠 sleep。scheduler 只控制真正接入调度器的异步边；没接入的 I/O 不能算被探索过。不要只 await 完一个命令再发下一个，然后把随机顺序称为并发证明。

建议先按成本分层设置预算：纯不变量数千次，带真实 store 的状态序列数百次；这些是启动预算，不是正确性门槛。每个目标分支必须有命中证据，包括旧回调、不同 request/epoch、重复观察、所有 append commitment、settlement 失败与新请求并存。只看 numRuns 不够。

## 五、统一测试矩阵

| 维度 | 必须覆盖的情况 | 核心断言 |
|---|---|---|
| 生命周期 | NoAccepted、AcceptedBeforeProvider、ProviderStarted、Terminal、AcceptanceUnknown | 不伪造 phase，不跨 phase 终结 |
| 提交 | 明确未尝试/未提交、Committed、Unknown、semantic cut | 不把 Unknown 降成未发生；不先发布成功 |
| 所有权 | exact、重复同一 token、旧 epoch、不同 request、不同 physical message、无 owner | 不释放别人的资源，不接受旧完成 |
| 观察次序 | before/after、idle/transform、terminal/delete、supersede/late callback、重启 | 幂等，合法进度，未处理等待者有明确结局 |
| 外部输入 | 空/缺字段/错误类型、SDK reject、HTTP 拒绝、读取失败、权限拒绝 | 最早边界类型化，不误当内部矛盾 |
| 物理停止 | abort pending、reject、throw、重复 stop | stop 生效后不得开始下一次 provider effect |
| 真 fatal | 报告器失败、重复观测、不同实例、真实子进程退出 | fatal 不返回；不在其后安排结算；必要事实可重放 |
| 恢复 | 每个 await/effect 边界前后中断、重放、旧版本数据 | 重建状态诚实；不重发 outcome unknown |

不能把不合法组合全部用 fc.pre/check 过滤掉：外部入口必须单独生成并验证非法输入的拒绝行为。状态机命令的前提用于区分合法操作，不用于把待发现的 bug 排除出测试。

两个全局性质贯穿所有包：

```text
announceSettled(r) ⇒ 有该 r 的 settlement receipt
release(f) ⇒ f 是本次确切持有的 fence，且该 phase 的依赖已经满足
unknown(effect) ⇒ 不出现同一逻辑 effect 的第二次物理发射
stale(callback) ⇒ 新 owner 的状态、额度和终态不变
fatal(incident) ⇒ 必须的结算/unknown 证据先成立，且此后没有业务 effect
```

不要无条件断言“所有请求最终成功或终结”。在外部证据永远缺失时，Unknown 可能必须保持。应证明它是可观察、可重放的待核对状态，不会被悄悄重发；在需要的证据确实到达后能够推进。测试超时只用来发现死等，不作为产品推进规则。

## 六、实施工作包

### W0：建立真实基线与入口门禁

归属 execution-failure-policy、verification-system、structured-workflow。

先保存已有失败回归与源码基线；完成第三节的入口索引和可达性归属。对 m6 helper 的每个调用点检查：它究竟观察生产能力，还是只判断人工 descriptor？后者不能再列为行为证明。不要只删掉这些测试；同一批补上实际调用或实际依赖关系的门禁。

验收：加入一个未登记 fatal 或用别名绕入出口会变红；删除生产结算动作会使相应测试变红；将一条日志伪造成 committed 不能通过。

### W1：先修 Blogger 的成功/失败语义

文件：Blogger/Runtime/Coordinator.fs、Abandon.fs、rendezvous 所属实现、Enforcer/Continuation.fs、BlogSurface 的测试接口。

先保留当前失败测试，修 abandonAndResolve 的失败传播；成功 abandon、release、terminal 通知的顺序按 exact ownership 明确，覆盖 NotifyTerminal 同步重入。再统一 invalid cardinality、interrupted、empty-text 的 outcome 解释。不要引入第二个 coordinator 或第二份 repair budget。

扩充现有 blogger-repair-trace.test.mjs。最小轨迹：建立请求 → 第一次 repair → 第二次 repair → 关闭 journal → 两个观察者并发到达。失败时两者都 reject，错误同源，flight 保留，不发 terminal；成功时 abandonment 只一次，释放确切 flight，终态只一次。

加入 NotCommitted 与 Unknown 注入，不能只用 closed journal 代表所有存储故障。增加旧请求回调与新请求并存、重复相同 provider run、空文本与多工具两条路径。

关闭标准：现有失败测试变绿；删除失败传播或把任一已终结 outcome 恢复成 fatal 时，正式回归会变红。不能只证明 surface 返回字符串 StopPhysicalRun。

### W2：使停止决定真正约束 Host

文件：Continuation.fs 的 ensureNonEmpty、stopPhysicalRun、requestPhysicalStop、applyContinuationOutcome；相应 registered transform/provider admission owner。

把“可发送消息”与“必须停止”分开。停止决定先阻断确切 execution 的后续 provider admission，物理 abort 作为独立效果观测结果。释放容量仍按 phase 和 committed receipt，不因“发出了 abort 请求”就释放。

测试走真实 registered hook：压住 abort Promise，同时尝试下一次 provider admission；必须没有新 provider call。abort reject/throw 也不能恢复继续执行。不同 execution 不受影响。覆盖空消息停止，防止 stop 又绕到 ensureNonEmpty fatal。

关闭标准：从 hook 返回到 abort 完成的窗口中没有失控发射；没有等待闭环造成的死锁。

### W3：修复错误归类与 exact settlement 合约

文件：Execution/Failure/Model.fs、Policy.fs、OpenCode/Host/PluginHostInterop.fs、相关 owner 异常边界。

沿用已有唯一策略 owner。先把跨边界的 string failure 改为含 commitment/identity 的封闭结果，再迁移调用方；只有确有新语义才扩充 failure 代数，不为每个文案造一类错误。

删除将未知 hook 位置等同 NoOwnedExecution 的推断。真实无 ownership 的启动阶段与已有 execution 的 hook 分开。JournalAppendException 的现有新增处理也要核对：当前生命周期由 settlement 枚举推导，不自动等于真实 execution phase。

针对 EXECFAIL-006 做真正顺序测试。AcceptedBeforeProvider：terminal append Committed 后才能 release exact fence。NotCommitted 保持当前 phase/fence；Unknown 不重复 effect。其他 phase 按既有策略的依赖顺序处理，不能套用统一的 release-first 或 append-first 模板。

测试：扩展 chat-hook-settlement.test.mjs、policy.test.mjs、persistence-mapping.test.mjs；补真实 hook 在 effect 前后抛错的契约回归。对未知来源不作“无资源”假设，同时不能把配置/权限/schema/hook 错误洗成 provider retry。

### W4：收拢 Detached 晚到结果

文件：OpenCodePort.fs、Interaction/Dispatch/Send.fs、DispatchSessionPort、PromptKey/receipt owner、相关 Host recovery 合约。

SDK 和 HTTP 统一交付最小必要的 typed evidence，但保持各自真实协议差别。Invocation/Submitted、PhysicalAccepted、terminal 分层。晚到的错误归 exact PromptKey 与 physical attempt，不从 session 猜测。

确定未发送走原有显式 abandonment；已接受后失败由真实 phase 的 owner 收敛；Unknown 保留 pending 并读取 durable/Host 证据。新的 typed evidence 才能授权下一步，重试次数和时间不构成证据。

必须测试：同步 throw、异步 reject、确定 HTTP 拒绝、Host 已接受但响应丢失、先收到 physical acceptance 后 promise reject、重复晚到通知、用户已 supersede。每个轨迹都查 durable claim 和物理调用数，Unknown 永不自动重发。

关闭标准：消除两层后台 observer 各自裁决 fatal；不能为求可见成功而直接返回伪造 physical acceptance；没有“await 整个 provider 才返”的新死锁。

### W5：Magic Todo 的 11 条支路逐条关闭

文件：MagicTodoMembrane.fs、Todo/OpenCode/HostCodec.fs、PrepareRejection/AcceptanceFailure/LocalityRejection 的真正 owner、Host 注册边界。

以附表 T01–T11 为逐条清单。装配缺失在构造期消除；模型输入不合法返回既有 ProtocolRejection；快照/持久化失败在边界携带 commitment；旧/重复 callback 不持有新事务权限；真正 identity corruption 保留熔断。

桥的生命周期覆盖 before 开始、prepare 进行中、before 拒绝、工具 effect 成功、after 接受、重复 after、删除/取消、恢复。测试不仅断言返回值，还断言无幽灵 checkpoint、无重复工具 effect、无遗留 completionToken。

扩充 obligation-ledger 的 magic-todo-membrane、magic-todo-after、magic-todo-provider-boundary，以及 host-boundary 的真实 canary。合成不完整快照与 Host pending tool stub，不能只用已完成 transcript。

### W6：封住 Blogger 请求和恢复的构造权

文件：Request.fs/.fsi、MainContext.fs、Enforcer/Cycle/Recovery.fs、相关 JS surface。

按第四节完成局部证明的前提封闭。优先消除冗余事实的自由组合；必要的 digest/sequence 仍可存储，但只能由 owner 一起建立或在恢复时一起验证。

新增 request-context-invariants.property.test.mjs（建议位置 requirements/context-compression/tests/）。测试生产构造器和生产恢复路径；不直接在测试里拼一个“已经合法”的 record，再断言它合法。

关闭标准：所有成功构造都推进 coverage 并满足 digest 关系；损坏/不支持版本/无 material 分开；旧请求不能被重新赋予当前提交权。不存在绕过验证的公开构造路径。

### W7：确切 flight、lease、capacity 的并发证明

文件：Blogger/Runtime/Host.fs、Coordinator.fs、Continuation.fs、IBloggerFlightLease 所属实现；ModelCapacity 与相应 settlement owner。

尽量使用已有 lease/fence，不再叠加另一套锁或状态机。旧回调无操作权限；真正当前 owner 自相矛盾才进入 invariant incident。Claim/Refreshed/Conflict、Released/Missing/Conflict 不经字符串压扁。

生成最小交错：A claim → A await → A 被 supersede → B claim → A release/terminal 回来。断言 B 的 flight、容量、provider 绑定和终态完全不变。再测同一 A 的重复回调是幂等，不是第二次 fatal。

复用现有 identity-capacity-interleaving.property.test.mjs 的基础设施，不把两份近似的并发驱动长期并存。

### W8：持久化 cut、事务与重放

文件：AgentJournal.fs、EventStoreJournalWriter.fs、Js/TransactionStore.fs、Strength/Persistence/Durability.fs、Casebook/Store.fs、实际 integrator/fold owner。

保留真正 cut 的 fail-stop。对每个生产事实构造器证明：合法前态和合法命令下，生成的 fact 被对应 fold 接受。把发现的拒绝反推到命令/事实界面，不能让投影层做无依据的补偿。

Prepared 与 Committed 分开测试。所有 effect 边界前后做中断/恢复，核对本地权威 state 没有领先 durable receipt。跨文件写入看完整 write set，不只测一个文件。

将可选全局 handler 改为确定的单一所有权路径。真实持久化 unavailable 不用字符串冒充 semantic cut；真正 cut 不变成一般可重试失败。

存储不可写时，不能凭日志声称“已持久化 unknown”。必须证明此前 durable intent 足以在恢复时导出不确定状态，或明确停止受损运行时。不能无限尝试再追加一条“我追加失败了”的事件。

### W9：委派完成与会话删除

文件：SyncDelegate/Workflow.fs、Runtime.fs、Fork/Host/RunLifecycle.fs、Handoff owner、HostSessionDeletion.fs、Casebook finalization owner。

先迁移 CheckpointCompletedHandoff / CheckpointCompleted / finalizeInspector 的失败合约。保留“子任务已经完成”和“完成证据尚未确切写入”两个事实；不重新执行已完成子任务，不提前向父请求公布可复用完成。

对 session deletion 建立能够重放的 exact finalize/drain 结果。失败路径不能靠 finally 丢掉完成恢复还需要的 identity。能否保留该 identity、需要哪份 durable 证据由 owner 决定，不由通用 cleanup 猜测。

测试 Completed 与 Delete 两种先后、重复完成、旧 authority、写入未提交/未知、父请求 supersede。沿用 reusable-handoff.test.mjs 与 managed-session-lifecycle 的 join/reuse 属性测试。

### W10：启动、能力和运行环境

文件：ManagerConfig.fs、SpikePlugin.fs、HostSignalBootstrap.fs、资源/事件订阅/模型路由的实际 owner；Sphinx CLI 与 Git hook 单独列账。

尽可能先做无 effect 的验证，之后原子激活必须的 owner。配置非法、插件 ABI 不匹配、文件/权限拒绝属于可达环境问题，但不能降级成半运行。没有安全的宿主卸载/拒绝能力时，进程退出仍可能是必要策略；必须把这个限制写成可验证契约，而不是说它永不发生。

测试注册失败后的零业务 effect、已获取资源的准确处置、重复初始化、Git Hook 开关关闭/开启两侧、事件源不可用。正常子进程 SIGKILL、SIG0 存活探测、命令行退出码不与插件请求 fatal 混账。

### W11：退出边界与反作弊验收

文件：FatalProcess.fs、Diagnostic.fs、正式 child fixture、各 owner 的真实 fatal 集成测试。

保持只有 composition 连接物理出口。生产不依赖可选全局 handler。测试 spy 可以记录 incident，但不能以“fatal 返回后完成了结算”为断言依据。

复用 requirements/host-boundary/tests/fixtures/fatal-process-child.fixture.mjs 做真实子进程：先触发实际 owner 的路径，父进程观察退出，再重开 store 检查必须事实。正向验证会退出；反向验证正常拒绝、旧回调、修复耗尽不退出。支持平台按自己的退出语义断言，不硬把所有平台都当 Unix 信号。

诊断失败不能阻止必要熔断；日志成功不代表状态落盘。测试覆盖 console/error renderer 抛错、输出被管道承接、重复 incident。不要把原有 SIGKILL 简单换成自然退出来让 finally 看似有机会运行，那会改变 fail-stop 契约。

## 七、批次和依赖

先 W0，同时修复已经红的 W1。W1 与 W2 原子闭环后，才可宣称 repair exhausted 已安全停止。

W3 定下 typed commitment/identity 合约后，再迁 W4、W5、W9 的调用方。W6 与 W8 的纯构造/折叠证明可独立推进；W7 与 W1/W3 共享所有权接口，需要先定契约再落测试。W10 与业务重构独立，但不得与同一装配文件重叠编辑。W11 的真实退出回归要从第一批就接入，不等最后才查。

每批只关闭明确的一组入口，先红测试，再生产修复，再重构；更新该 owner 的 WHY/WHAT/HOW 和实际测试索引。存在新旧路径时本批迁完，不留第二套 fatal wrapper、第二个重试预算、第二份状态真相。若需要改变既有“外部失败也全进程退出”的产品规则，规则和代码同批调整。

本轮工作区已有 EXECFAIL-010 和 CONTEXT-COMPRESSION-025 的新增文字，应在现有修改基础上核对，不重复新建相同条款。现有改动仍有红测试，不能直接盖章已完成。

## 八、每个入口的关闭标准

不可达入口：声明前提、真实生产构造路径封闭、局部证明/有限穷举、跨边界属性测试、所有恢复入口均验证；保持运行时断言作为最后防线，不以删除断言当作证明。

可达失败入口：最小反例固化、旧实现失败新实现通过、错误在真实 owner 形成明确结果、未知 effect 不重发、其他请求资源不变、等待者与恢复路径不失联。

保留熔断入口：说明当前运行时为何不可信、影响范围为何必须到该层、具体 settlement/unknown 证据、真实子进程退出与恢复证明、报告与退出责任唯一。不能简单写一句“属于基础设施所以 fatal”。

非生产入口：有实际编译/导出/调用依据，说明是 fixture、死接口或独立 CLI；不能只因名字含 Surface 就豁免。废弃代码应删除，不在版本里保留供后人猜。

每项证据要可反驳。至少对关键性质验证一个能破坏它的真实生产变体会使测试变红，例如去掉 exact owner 检查、把 Unknown 映射成未提交、先 release 后 append、让 fatal 后继续结算。变体验证在正式隔离的测试/构建机制进行，不改当前用户工作区，不靠一次性探针充当交付。

## 九、验证命令与交付

运行相关 requirement suite 即可；不要在每个小改动后反复全仓构建。改变生产 F# 后先使用项目规定的构建入口，再运行保持新鲜度检查的 scoped runner。

```sh
node scripts/build.mjs
TESTS_MJS_FILES=具体的正式测试文件,另一个正式测试文件 \
node requirements/verification-system/tests/run.mjs
```

治理批次完成后：

```sh
npm run check
npm run format-build-test
```

正式发布级核验按仓库既有 verify:release 管线及真实 Host canary 执行。不能用跳过新鲜度的测试、只运行 dist 的旧产物、无请求接口的私有 helper 测试替代。

最终交付包括：机器可检查入口清单；各 owner 的短证明与反例；实际生产属性/契约/重放测试；真实退出测试；入口门禁；本批验证结果。验收数字是“未归属、无证据、未关闭入口为零”，不是“fatal 字样为零”。

## 附录 A：37 条直接 fatal 引用初始索引

这是词法引用清单，包括调用、能力绑定和 surface。每条还须完成生产可达性判断；不是 37 个已确认缺陷。

| ID | 位置（相对 src/Wanxiangshu/） | 入口 | owner | 处理要求 |
|---|---|---|---|---|
| F01 | Persistence/Journal/AgentJournal.fs:241 | FatalProcess.trip / journal-semantic-cut | durable-events | 保留真正的 semantic cut 熔断；追查事实生产者，补 phase/settlement 证据，不能在 catch 中降级。 |
| F02 | Interaction/Repair/InteractionRepair.fs:75 | Diagnostic.fatal / interaction-repair-infrastructure-failed | interaction-authority | Failed 混合存储、authority、传输错误；NotifyTerminal 在 fatal 后。先恢复类型与提交证据。 |
| F03 | Interaction/Repair/InteractionRepair.fs:142 | Diagnostic.fatal / blogger-protocol-repair-failed | interaction-authority | 缺少 AgentJournal 代表装配缺口；改为必需能力或启动拒绝，不能运行中靠 option 暴露。 |
| F04 | Persistence/Journal/EventStoreJournalWriter.fs:278 | FatalProcess.trip / runtime-started-semantic-cut | durable-events | 证明 RuntimeStarted 构造与 integrator 合约一致；对真实 cut 留存证据并停止，不假装 ordinary append failure。 |
| F05 | Interaction/Dispatch/DispatchSurface.fs:91 | FatalProcess.trip / ReportFatalDiagnostic 转接 | dispatch-protocol | 先判定公开 surface 的生产可达性；若保留，纳入能力装配证明。 |
| F06 | Interaction/Dispatch/DispatchSessionPort.fs:35 | FatalProcess.trip / ReportFatalDiagnostic 转接 | dispatch-protocol | 本次引用扫描只见声明及实现，未见调用点；需编译/公开入口确认后删除死能力或补真实消费者测试。 |
| F07 | Interaction/Dispatch/Send.fs:223 | FatalProcess.trip / detached-prompt-dispatch-failed | dispatch-protocol | 不得把确定未发、提交未知、回调异常合为字符串；保持 exact PromptKey，禁止未知重发。 |
| F08 | Interaction/Dispatch/OpenCode/SessionNudge.fs:38 | FatalProcess.trip / ReportFatalDiagnostic 转接 | dispatch-protocol | 与 F05/F06 同一接口；检查死转接、重复报告及直连物理能力。 |
| F09 | Repository/Programming/Js/TransactionStore.fs:102 | FatalProcess.trip / JsTransactionPrepared semantic cut | repository-programming | Prepared 阶段未获可信 receipt 时不得发生文件 effect；证明事务事实构造满足折叠规则。 |
| F10 | Repository/Programming/Js/TransactionStore.fs:127 | FatalProcess.trip / JsTransactionCommitted semantic cut | repository-programming | 可能已有文件 effect；与 F09 不同义，须证明恢复/回滚证据，不能把 outcome unknown 当未写。 |
| F11 | Execution/Delegation/SyncDelegate/Runtime.fs:309 | TripFatal = FatalProcess.trip | delegation | 这是能力绑定，不是独立失败分支；装配注入、一次性执行和消费者 F14 一并审计。 |
| F12 | Mission/Obligation/Todo/MagicTodoMembrane.fs:481 | Diagnostic.fatal / fatalInfrastructure | obligation-ledger | 通用汇入口；11 个上游入口必须分别分类，见附表。 |
| F13 | Context/Companion/CompressionSurface.fs:349 | Diagnostic.fatal / 动态 operation 转接 | context-compression | surface 可达性待核对；测试桥不能替代真实 continuation/Host 入口证明。 |
| F14 | Execution/Delegation/SyncDelegate/Workflow.fs:165 | deps.TripFatal / checkpointCompletedHandoff | delegation | CheckpointCompletedHandoff 的 Result<unit,string> 丢失提交类别；先修 handoff 合约，不重跑已完成委派。 |
| F15 | Mission/Obligation/Todo/OpenCode/HostCodec.fs:163 | Diagnostic.fatal / missing output.args | host-boundary | Host ABI/装配失败与模型输入错误分开；边界解码后不允许内部再得到缺失 args。 |
| F16 | OpenCode/Plugin/SpikePlugin.fs:46 | Diagnostic.fatal / plugin-initialization-failed | host-boundary | 半初始化不能继续是合理约束；先做无 effect 验证，再激活资源，失败有明确启动处置。 |
| F17 | OpenCode/Plugin/PluginStrengthPorts.fs:66 | Diagnostic.fatal / strength-semantic-cut | durable-events | 保留 cut 熔断；核验 promotion 前置事实及当前 ownership，存储失败不能丢类型后经 hook 再误判。 |
| F18 | Enforcer/Continuation.fs:87 | FatalProcess.trip / blogger-flight-release-conflict | context-compression | 区分旧 callback 与确切当前 owner 的冲突；不能误放新 flight。 |
| F19 | Enforcer/Continuation.fs:469 | Diagnostic.fatal / missing CurrentRequest | context-compression | 追查 durable open、live flight、恢复、删除和 supersede 的先后，不能只凭缺失缓存认定进程坏。 |
| F20 | Enforcer/Continuation.fs:473 | Diagnostic.fatal / live blog without cycle authority | context-compression | 先证实消息属于当前请求；历史/外来观察不获得修改权或 fatal 权。 |
| F21 | Enforcer/Continuation.fs:499 | Diagnostic.fatal / fatalClearWorking | context-compression | 已确认代码顺序缺陷：fatal 在 durable abandon 与 release 之前。不是机械搬行，先分 commitment。 |
| F22 | Enforcer/Continuation.fs:541 | Diagnostic.fatal / empty-text repair outcome | context-compression | 仍将 AbandonedExhausted、Completed 等不同结果合并 fatal；与已改 cardinality 分支不一致。 |
| F23 | Enforcer/Continuation.fs:585 | Diagnostic.fatal / squash commit unknown | context-compression | Unknown 可达，必须保留 request/commit evidence；不得 abandon、释放错误资源或再发 effect。 |
| F24 | Enforcer/Continuation.fs:624 | Diagnostic.fatal / main commit unknown | context-compression | 与 F23 同 commitment 要求，但独立覆盖 main commit 路径，不能只测 squash。 |
| F25 | Enforcer/Continuation.fs:869 | Diagnostic.fatal / enforcer-empty-projection | context-compression | 发送与停止共用 ensureNonEmpty；Stop 不应以有可发送正文为成立条件，真实 Host 边界需测试。 |
| F26 | OpenCode/Host/PluginHostInterop.fs:92 | Diagnostic.fatal / emitFatalRecord | host-boundary | 真正入口来自 normalizeHookFailure/registeredHook；默认 LocalInvariant + NoOwnedExecution 不是证据。 |
| F27 | OpenCode/Host/ManagerConfig.fs:17 | Diagnostic.fatal / managed-agent-config-invalid | capability-enforcement | 配置错误可达；证明激活前拒绝，不能半安装。不能用默认模型/权限掩盖错误。 |
| F28 | Context/Companion/Blogger/Runtime/Host.fs:160 | FatalProcess.trip / blogger-flight-claim-conflict | context-compression | 保留 Claim/Refresh/Conflict 的类型；同 request 重入幂等，旧 epoch 请求无权覆盖现 owner。 |
| F29 | Context/Companion/Blogger/Runtime/Coordinator.fs:107 | FatalProcess.trip / blogger-flight-release-conflict | context-compression | abandonEpisode 当前先通知 terminal 再 release；核验同步重入，release 结果与通知因果。 |
| F30 | Context/Companion/Blogger/Runtime/Coordinator.fs:143 | FatalProcess.trip / blogger-repair-abandon-failed | context-compression | 已确认失败回归：catch 先回复 AbandonedExhausted；结算失败被宣布成功，多个等待者需同源失败。 |
| F31 | Execution/Delegation/Fork/Host/RunLifecycle.fs:262 | FatalProcess.trip / checkpointCompletedHandoff | delegation | 缺 handoff capability 与 checkpoint 写失败合并；不可把已完成子任务当可重跑任务。 |
| F32 | OpenCode/Host/Diagnostic.fs:135 | FatalProcess.kill / 诊断物理出口 | host-boundary | 物理出口本身；验证报告故障也不能绕过熔断，真实子进程不返回、日志不能冒充 durable evidence。 |
| F33 | OpenCode/Host/OpenCodePort.fs:136 | Diagnostic.fatal / SDK prompt-async-dispatch-failed | dispatch-protocol | 后台 Promise reject 缺 exact key/phase；把最终 transport evidence 送回 dispatch owner，不能只带 session。 |
| F34 | OpenCode/Host/OpenCodePort.fs:147 | Diagnostic.fatal / HTTP prompt-async-dispatch-failed | dispatch-protocol | HTTP 适配器独立契约测试：确定拒绝、响应丢失、Host 已接受后失败不得同判。 |
| F35 | OpenCode/Host/HostSessionDeletion.fs:85 | Diagnostic.fatal / inspector-case-finalization-failed | managed-session-lifecycle | finalization 返回 string 且外层 finally 删除 identity；保留 exact finalize 状态，按 commitment 决定后续。 |
| F36 | OpenCode/Host/HostSignalBootstrap.fs:435 | Diagnostic.fatal / signal-subscribe-failed | host-boundary | 无可信事件源不可运行；区分能力缺失、订阅拒绝与运行中断，验证激活之前拒绝。 |
| F37 | OpenCode/Host/HostSignalBootstrap.fs:525 | Diagnostic.fatal / durability-activation-failed | host-boundary | Git Hook 安装/运行环境拒绝可达，且受 WANXIANG_GIT_SYNC 开关控制；不是纯算法不变量。 |

## 附录 B：Magic Todo 的 11 个间接入口

共同文件：src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs。不能拿 F12 一个汇入口覆盖这十一种前提。

| ID | 行号 | 触发 | 处理要求 |
|---|---|---|---|
| T01 | 486 | requiredText：字段缺失 | Host 契约解码；未获有效身份不能进入有 ownership 的业务分支。 |
| T02 | 491 | requiredText：字段为空 | 与缺失独立覆盖；同时测试非字符串结构，不靠 string 转换“合法化”。 |
| T03 | 531 | requirePort：能力缺失 | 构造时强制能力或显式不启用；拒绝运行到半途才发现 None。 |
| T04 | 536 | preparationFailure：未列出的 PrepareRejection | 穷尽领域拒绝、重复/过期、写失败、真正矛盾；不能 wildcard → invariant。 |
| T05 | 557 | requireMessages：快照不可用 | 边界读取失败不等于程序矛盾；保留来源及 effect 是否开始。 |
| T06 | 565 | requireLocality：定位失败 | 乱序/未 materialize/身份冲突分开；不把所有 lookup miss 判成 fatal。 |
| T07 | 573 | requireMaterialized：input materialization 失败 | 区分模型输入错误与同一 physical identity 的内容矛盾。 |
| T08 | 598 | locateToolCall：找不到 current run | 用真实 before/after 与 Host pending part 顺序测试，不能靠理想完整快照。 |
| T09 | 612 | XTrace 前缀捕获失败 | 保留 typed persistence commitment；失败不得伪造 checkpoint 已成立。 |
| T10 | 654 | acceptResolvedCheckpoint：accept 失败 | 已发生工具 effect 与其 checkpoint 结果分别记账，Unknown 禁止再执行。 |
| T11 | 704 | after：没有 deferred prepare | 重复 after、进程内桥丢失、旧 callback、真正 Host 违约分别生成并断言。 |

## 附录 C：额外出口、排除项与尚待完成的工作

额外出口：Foundation/FatalProcess.fs 的物理 adapter 本身；Strength/Persistence/Durability.fs:50 与 Casebook/Store.fs:155 的可选 handler；Sphinx/ServeEntry.fs:28 的 exitFailure（:62、:76 调用）；resources/git/wanxiang-hook.mjs:49 的失败退出。

明确分账而非简单计入请求 fatal：Process/NodeProcessHost.fs:27 和 Process/PtySupervisor.fs:76 杀的是受管子进程；Persistence/EventStore/ProcessEventLog.fs:46 的 kill(pid,0) 是存活探测；Utf8Fs 的 TextDecoder({fatal:true}) 是严格解码选项；Outcome.SendOutcome.Fatal 是发送结果联合分支，是否退出要追消费者。脚本正常退出码也不能算内部不变量。

仍未完成：533 条候选的全量逐项分析；别名/Emit/未捕获异常的完整生产可达图；每处 fatal 的现实发生频率；线上最小故障事件；真实 Host 停止窗口的重现；所有构造器的全称证明；全仓回归和发布 canary。本轮没有读取用户真实会话日志，不能推断哪条路径贡献了最多线上崩溃。

因此优先级是按潜在损害和已经拿到的证据排的，不是按未经测量的故障频率排的。

## 外部语义核对

fast-check 官方文档 Model based testing：commands/asyncModelRun/scheduledModelRun；模型应是独立的简化观察，不复制生产实现；commands 重放除 seed/path 外还需 replayPath。

fast-check 官方文档 Race conditions：调度器只能控制纳入调度的异步行为，使用时需避免调度依赖造成测试自身死等。

Node.js 官方 Process 文档：SIGKILL 不能注册监听器；强制退出不会等待未完成异步 I/O；console 输出也不能替代 durable receipt。

以上于 2026-09-14 核对。仓库 package.json 声明 fast-check 4.9.0。本方案沿用既有依赖，不以升级依赖作为开始治理的前提。
