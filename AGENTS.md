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
# 子系统解环施工蓝图

> 编写日期：2026-09-12。现场基准：`4bc0b5de74fe793a08805aad6a0c684ab6995fe1`，`master`，编写前工作区干净。
>
> **终点：完整、诚实的 subsystem 图无环；领域能够只凭公开合同被替换；存储、重放、取消、失败和资源生命周期不变。** 不以删了多少引用、增加多少端口或通过多少结构测试代替这三个终点。
>
> 本次交付是施工方案，不是已经实施的重构。下文标为“新建”“迁入”“目标”的文件、签名、分片和 subsystem 都尚待施工。现行 WHAT 仍是产品合同；本文件不自动修改产品语义，不授权删除运行数据，也不表示所有批次已通过编译。

## 阅读与使用

从本文件执行任务时，先读第 1—5 节，再按第 6 节批次顺序施工。第 7 节覆盖全部现有 subsystem；第 8—11 节是每批都要使用的施工、验证和异常处理办法。第 12 节是终验，第 13 节给出可直接运行的调查命令，第 14 节是第一批任务书。

这是一份临时施工设计，不是新的 owner、ACL、kind 或符号治理系统。新增边界性质放进已有 `subsystem-boundaries`、compiler-boundary 与所属 package 的正式测试；证明写回已有 HOW。不要把本文件复制成若干互相竞争的计划，不另建权限表、全仓符号数据库或第二套源码依赖图。

**一张蓝图画到底的含义是固定职责和最终依赖方向，不是预先猜死全部文件数。** 后续发现本方案对某个调用方的理解有误，修正相应切口和完整消费者集合；不能退回全仓零符号剪枝，也不能偷偷换掉终点。产品行为的变更仍须按 requirement-system 处理。

---

## 1. 起点：以本次现场为准

### 1.1 本次重新执行的结构结果

| 项目 | 基准实测 | 计数口径 |
|---|---:|---|
| subsystem | 26 | 当前实际分片归属，不是目录数 |
| compile shard | 245 | 库存中真实工程 |
| production source | 732 | `.fs`，不把 sibling `.fsi` 再计一次 |
| ProjectReference | 1,877 | 声明引用，不等于源码调用次数 |
| 跨 subsystem ProjectReference | 1,464 | 同一 subsystem 对可有多条分片引用 |
| 不同 subsystem 有向边 | 220 | 将相同有向 subsystem 对去重 |
| 最大 SCC | 18 | 完整声明图聚合，不按 kind 过滤 |
| 18 节点内部不同有向边 | 151 | SCC 内 subsystem 对去重 |
| compile-shard 图 | DAG | 分片无环不代表聚合后无环 |

18 个节点是：`authority`、`change`、`chat-execution`、`context`、`delegation`、`dispatch`、`enforcer`、`host`、`interaction`、`knowledge`、`persistence`、`process`、`provider`、`repository-programming`、`session-lifecycle`、`sphinx`、`strength`、`work`。

`scripts/checks/subsystems.mjs` 当前将 SCC 作为报告，不将非空 SCC 判为失败。因此它输出 `OK` 只证明其实际检查的结构条件，不能关闭 GAP-033。

### 1.2 当前集中依赖点

以下前向闭包包含自身，按真实 ProjectReference 递归；生产 `.fs` 去重。不是 Fable parsed source 数，也不是构建耗时。

| 分片 | 前向分片数 | 前向 `.fs` 数 | 直接消费者数 |
|---|---:|---:|---:|
| `persistence-journal-agentjournal` | 51 | 144 | 57 |
| `composition-durable-projection` | 43 | 118 | 45 |
| `durable-journal-port-adapter` | 78 | 208 | 12 |
| `persistence-eventstore-canonicalintegrator` | 94 | 261 | 6 |
| `delegation-recovery-runtime` | 22 | 45 | 3 |
| `opencode-tools-toolruntimescope` | 146 | 427 | 8 |
| `opencode-host-pluginruntimescope` | 150 | 434 | 3 |
| `context-prefix-wire` | 98 | 275 | 3 |

### 1.3 已核实、必须进入施工设计的事实

1. **存储机制和全域历史程序混装。** `Persistence/EventStore/CanonicalIntegrator.fs` 同时定义 `StructuralIntegration`、`JournalIntegration` 和 integrator 执行机制。`JournalIntegration` 直接认识 `ProjectionSet`、Journal codec 与 `Fold.foldEnvelope`；`baseRules` 固定包含它。因此仅把额外领域规则改成注入，并没有隔离执行机制。
2. **Canonical codec 仍被物理工程包住。** `eventstore-core-runtime` 同时编译 `CanonicalEventCodec.fs`、`ProcessEventLog.fs`、`Store.fs`；`eventstore-merge-runtime` 为使用其中内容引用整个 core。DURABLE-EVENTS-023 要求完整 canonical codec 作为有界纯边界，此处必须一起处理。
3. **`AgentJournal` 是应用聚合接口，不是低层文件句柄。** `Persistence/Journal/AgentJournal.fsi` 公开 `Snapshot`、`SnapshotWithRevision`、`Writer`，追加输入为 `AgentFact` / `MagicTodoFact`，返回带聚合状态或聚合错误。它可以留在高层 durable composition 内，不能继续被业务当通用 capability。
4. **共享 adapter 已成为新集中依赖点。** `Composition/Durable/AgentJournalPortAdapter.fsi` 同时暴露多个域的工厂。只消费端口的代码可能变小，但自己调用共享工厂的中间层仍拉入 208 个 `.fs`。
5. **Wire 的源码接口迁移没有完成编译边界收口。** `context-prefix-wire` 仍直接引用 `persistence-journal-agentjournal` 与 `composition-durable-projection`。更下层的 `context-prefix-wire-port` 也直接引用 `composition-durable-projection`。不能只看 `Wire.fsi` 中已无 Journal 类型。
6. **权限身份与执行组合混在同一个公开模型。** `Interaction/Authority/Model.fsi` 的 `AuthorityExecutionProfile` 本身不需要 Prefix，但同文件的 `AttemptExecutionProfile` / `buildAttemptExecutionProfile` 需要 `XProjectionChoice`。Wire 的视图又需要 `AuthorityExecutionProfile`。只提一个文件名或改一个引用不能解开这条类型环。
7. **Provider planner 同时包含跨域执行组合与本域纯折叠。** `participant-provider-attempt-planner` 编译 `Planner.fs`、`ProviderFailureFactFold.fs`、`Evidence.fs`，并引用 authority、chat-execution、context 和 Journal codec。要按职责拆，不能把整个工程下沉。
8. **Git 同步入口与 Change Git gateway 互相聚合。** `persistence/git-hook-sync → change/git-gateway → persistence/eventstore-sync-runtime`，分片是 DAG，subsystem 回环。应分开持久化 Git transport 与业务 Git 操作，不把其中一个整包改标签。
9. **存在重复决策。** `AgentJournalPortAdapter.forWire.RecordConfirmedSuccess` 与 `Participant/Provider/Attempt/Fallback/Ledger.recordConfirmedSuccess` 持有同一成功结算规则。adapter 应只适配，不复制该规则。
10. **验证仍有双重判据。** `requirements/delegation/tests/delegation-compile-boundary.test.mjs` 仍使用 `legacyKind` 作裁决；`port-observation-timing.test.mjs` 的四例目前是源码/合同文字断言，不是运行时观察实验。两者须分别修，不能删掉有效保护。

上述路径都相对于 `src/Wanxiangshu/`，测试与脚本路径除外。HOW 中更早的计数、不可行判断和“已经删除引用”的文字不能覆盖这些当前事实。

---

## 2. 固定终态：领域在下，跨域组合在上

### 2.1 总图

箭头表示“编译时知道、依赖”，由使用者指向提供者。

```text
真实产品入口 / 跨进程入口 / verification
                     │
                     ▼
             application-composition
              │           │        │
              ▼           ▼        ▼
             host   durable-composition   dispatch
              │           │        │
              └─────┬─────┴────────┘
                    ▼
     各领域的公开合同、纯决策、带窄 capability 的工作流
       interaction / change / delegation / chat-execution ...
                    │
                    ▼
     低层领域词汇、session 基础、存储合同和物理存储实现
       authority / provider / context ... / persistence
                    │
                    ▼
              participant / runtime-platform
```

图是阅读概览；具体可依赖方向以 2.3 节为准。`host` 与 `durable-composition` 是并列的适配/装配层，二者不互相构造。必须同时认识 Host 与 Journal 的接线，放进 `application-composition`。

领域工作流不是全部搬进最顶层。顺序与业务决策仍由其所属领域或 `dispatch` 持有；最顶层只负责构造、把已有能力接上、启动和释放。

### 2.2 两个需要新增的真实 subsystem

**`durable-composition`：拥有全程序历史解释。**

它拥有 `AgentFact` 外层 union、跨域 `ProjectionSet`、canonical journal codec、跨域 fold 组合、应用 Journal、具体规则程序、统一词汇装配、Journal 到各消费端口的适配。其不变量是同一历史导出同一聚合、一次提交原子更新相关切片、同一 revision 的观察一致、事实包装与 codec 唯一。

它不拥有领域决策；不允许通过 import Host 或运行时容器取得物理权限；不新增重放循环。领域 fact / state / rejection 的定义留在领域。改变一个领域内部算法而不改变其公开合同，不应迫使其它领域重新理解该算法。

**`application-composition`：拥有真实进程/插件的生命周期装配。**

它拥有插件启动与关闭接线、Host 与 durable adapters 的实例配对、全工具注册、工作区 store 装配、Sphinx 服务进程入口以及必须同时认识多个物理世界的测试/产品入口。其不变量是装配一次、同一身份和资源实例贯穿调用、取消与销毁只由明确持有者执行。

它不拥有新的业务状态机、成功/失败决策或通用调度总线。出现 `if 某业务状态 then 决定追加何种事实` 时，先找应调用的领域 decision，不能借“装配”名义吞业务。

新增这两个名字本身不算收益。只有第 4 节所述反向知识路径清零、完整图确实形成 DAG，才算边界成立。不同 subsystem 不因为统一重放就合并；也不把全部有环代码移进这两个新包。

### 2.3 最终依赖顺序

下表是固定施工方向。相同层之间默认没有互相引用；每个跨层引用仍必须有真实知识理由，不能因为“允许向下”就引入大包。层号只在本设计中帮助阅读，不写成第二套生产 ACL，不按层号过滤 SCC。

| 顺序 | subsystem | 最终保留的职责 / 关键限制 |
|---:|---|---|
| 0 | `runtime-platform`、`participant` | 现有泛化原语与角色/身份目录；不收编未知领域知识。participant 不反向读取任何业务状态。 |
| 1 | `resources`、`requirements`、`persistence`、`session-lifecycle`、`repository-investigation` | 资源、规范 catalog、存储内核、会话基础与调查能力。只依赖第 0 层和自身；session 内不保留 Delegation 专用 mailbox、恢复编排或全局 scope。 |
| 2 | `provider`、`relay`、`process`、`repository-programming`、`sphinx` | Provider 自身消息/请求/失败/路由决策；Relay 核心；进程能力；JS 操作与事务；Sphinx 核心。可依赖必要的 0—1 层，不反向认识 authority/context/dispatch/Host。 |
| 3 | `authority` | 权限身份、claim、接受/拒绝与本域投影；可认识 provider 自有请求词汇，不认识 Prefix 选择和跨域 Attempt 组合。 |
| 4 | `context` | trace、prefix、blog、companion 的领域模型与纯算法；可读取 authority 窄证据，但不认识 chat-execution、dispatch 或 Journal 聚合。跨域 Wire 编排拆到 dispatch。 |
| 5 | `enforcer`、`work` | 检测/纠偏本域决策与工作记录/义务；可消费 context 与更低公开合同，不认识更高的执行、注册与宿主容器。 |
| 6 | `chat-execution` | Accepted / ProviderStarted / Terminal 的执行协议、投影和恢复决策；可引用 authority、context、provider 的窄证据。 |
| 7 | `delegation`、`knowledge`、`strength` | 委派核心、知识核心、推测核心；依赖必要的更低合同。三者不通过共享 scope 相互调用；跨三者协作由更高层组合。 |
| 8 | `change` | 发布、作业、工作树与恢复业务；消费 delegation/process/relay 等合同，不直接创建 Host 或全局 Journal。 |
| 9 | `interaction`、`output` | 工具语义、交互决策与输出蒸馏；消费较低领域合同。物理 ToolSpec 包装和全工具注册不留在领域核心。 |
| 10 | `dispatch` | 跨域 attempt / turn / ingress / reconcile / recovery 编排及其窄端口；不认识具体 Host adapter、Journal、全局 scope 或最顶层工厂。 |
| 11 | `host`、`durable-composition` | 分别实现 Host 边界和统一历史程序；各自可依赖 0—10 层。互不依赖，不依赖 application-composition。 |
| 12 | `application-composition` | 将领域、Host、Journal、真实进程装配到一起；业务不能反向 import 它。 |
| 13 | `verification` | 只能作为证明消费端，生产分片不得引用它。其它 package 的 Surface 按其真实职责归属，不统统转入 verification。 |

这是对现有 26 个 subsystem 的职责收口，加上两种原本混装的独立职责。**不把最终数量 28 设成 gate。** 只有真实出现本方案无法容纳的新领域职责，才按现行规范重新裁决，而不是为过图随意增删名字。

### 2.4 这张图要求进行的几项实质改变

- Provider 的跨域 Attempt planner 不再与 Provider 本域 failure fold 同片；组合部分归 dispatch。
- Authority 的权限身份不再携带 Prefix 选择；`AttemptExecutionProfile` 组合归 dispatch，权限证据仍由 authority 构造，不能在 dispatch 伪造。
- Context 保留 Prefix 纯算法；读取权限/失败预算、等待 Host snapshot、提交重基线的跨域 Wire 流程归 dispatch。它收到的仍是 typed capability，不是 Journal。
- Host 原始 SDK 数据在 Host 边界解码成消费者的窄输入。领域不会为了读一个 host message 而导入 Host 实现，也不能靠 `obj` 擦除后偷读 SDK 私有字段。
- Session 基础与具体 Delegation 资源分开。`CompletionMailbox` 等本来认识 delegation 的业务机制归 delegation；生命周期根容器归 application-composition；不是把全部 session 文件下沉 platform。
- 跨域 JavaScript Surface 若需要全程序装配，归相应装配层；领域纯 Surface 留在原域。生成文件路径可以保持，不以破坏现有 JS 测试入口换取图无环。

---

## 3. 不变项：切文件前先写清语义

### 3.1 存储与重放

本方案不改 event identity、canonical bytes、已发布 `event_type` 的载荷、retention 时刻和 24 小时 TTL，不改 parents、heads、CAS、append-only、cut-tail、unknown settlement、poison 与 fatal 次序。不新增 schema/store generation，不重新开放已退役物理格式。

仍由唯一 canonical Integrator 解释历史；领域只提供现有单信封 fold。保留跨域原子提交、集中 persistence、统一重放。**存储机制从具体规则程序分离，不等于每个领域建立一套 store 或重放循环。**

`createWithRules` 目前对缺失 Structural / Journal 的拒绝是现有行为。新内核和装配工厂分开后，真实 store 构造入口仍须拒绝缺失这些必需规则。不能为了“通用”放宽公开入口的接受条件。

### 3.2 观察时序

迁一个 reader 前，在原实现旁记录四个事实：何时取 snapshot、哪些字段来自同一次 snapshot、await 发生在哪里、await 后是否重新观察。按下面三种情况迁，不能统一套一种端口。

| 原语义 | 目标端口形状 | 禁止的替换 |
|---|---|---|
| 一次观察内相关切片同 revision | 一个返回完整窄视图的 `ReadView`；adapter 内只取一次 snapshot | 多个 getter 各取最新状态，造成拼接视图 |
| 每次成员调用读最新状态 | 每成员在调用时读取；构造端口不冻结 | 构造时缓存 snapshot，然后长期复用 |
| 先读 revision，再等待变化 | 保留 read-with-revision 与 await-after-revision 的顺序和取消语义 | 先读数据后重新拿一个 revision、丢失唤醒；以轮询替代原 wait |

Wire 的 `ReadView` 属于第一种；当前 TurnObservation / TerminalPolicy / HostJoinGuard 的已裁定成员观察属第二种。不能为了统一类型把两者改成同一种。

### 3.3 权限、失败与资源

权限 profile 的合法构造器、identity seed 来源、exact session / run / root / handle 身份保持。跨层转换只能消费已有证据，不能重新猜测或从字符串重新造证据。

将 `Result<ProjectionSet, ...>` 缩为 `Result<unit, ...>`，仅限逐个证明旧消费者不读返回投影、不靠其 revision 实施后续 CAS 的地方。未知提交、拒绝、取消与致命异常不能合成一个 `false` 或 `None`。预期错误可以由领域拥有闭合类型；既有 JS 文案与对象形状只在外部适配边保持。

所有资源必须写明 acquire、register、cancel、drain、dispose 的拥有者。注入后必须仍用同一个 clock、digest、blob store、abort handle 和 session identity；不能无意复制 mutable registry。特别是 `ExplicitResumeSuppression` 的进程级状态与 `ToolRuntimeScope` 的 PTY/join 资源，只允许转移唯一拥有者，不允许“双实现过渡”。

### 3.4 编译与公开面

`.fs` / `.fsi` 同步；同一 source 恰归一个 subsystem 和一个 shard；aggregate 输入集合精确对应。实际编译依赖必须声明，不能动态回捞模块。既有 Fable / JS 外部边界维持合法，不能用手写 union tag、字符串路径、默认成功 fallback、复制 adapter 或 `obj` 偷渡依赖。

内核中现有 `IntegrationRule` 的 `obj` 状态槽是既有机制，本次不顺手重造，也不将其推广成跨域万能对象。不得借此把所有领域数据擦成 `obj` 以获得无环图。

---

## 4. 目标代码边界：先约定接口，再拆项目

所有新名字都是施工建议名；落地时可为避免实际命名冲突小幅调整，但不改变职责。下面的 F# 只表达目标公开面，不是可直接覆盖生产文件的补丁。

### 4.1 存储执行机制

| 当前内容 | 目标归属 | 处理 |
|---|---|---|
| `CanonicalEventCodec.fs/.fsi` 六个公开操作 | persistence / 新 `eventstore-canonical-codec` | 完整成组提取，不按某个 consumer 复制一个 hash helper |
| `ProcessEventLog.fs/.fsi` | persistence / 新 `eventstore-process-log` | 独立物理 shard，仍保留文件/锁/写入顺序 |
| `Store.fs/.fsi` | persistence / `eventstore-core-runtime` | 消费 codec、log、port 和 integrator contract；不构造具体 Journal program |
| `StructuralIntegration` 与规则执行机制 | persistence / 新 `eventstore-integrator-engine` | 不再 import Journal codec、ProjectionSet 或全域 Fold |
| `JournalIntegration.rule` | durable-composition / 新 `journal-integration-rule` | 唯一包装 Journal codec、fold 和 rejection 渲染 |
| `CanonicalIntegrator.baseRules/createWithRules` | durable-composition / 历史程序工厂 | 继续保证 Structural / Journal 必需条件；将执行交给新 engine |
| `AuthoritativeVocabulary` | durable-composition | 收集各域词汇，不反向编入内核 |
| `Sphinx/ServeEntry` 等真实入口 | application-composition | 调用合法历史程序工厂与物理 Store，不反向被内核引用 |

目标调用关系：

```fsharp
// persistence：只知道当前规则机制，不认识具体业务状态。
// 新fsi须公开这条既有结构规则，供上层baseRules装配；不是复制第二份规则。
module StructuralIntegration =
    val rule: IntegrationRule

module IntegratorEngine =
    val create:
        rules: IntegrationRule list ->
        isEventTypeKnown: (string -> bool) -> ICanonicalIntegrator

// durable-composition：保留现有对外工厂语义，并拥有必需规则程序。
module CanonicalIntegrator =
    val baseRules: IntegrationRule list
    val createWithRules:
        rules: IntegrationRule list ->
        isEventTypeKnown: (string -> bool) -> ICanonicalIntegrator
```

不新造注册协议，不加动态服务查找。保留现有 rule 名、FaultScope、规则次序和 `PrepareLive → Commit` 约定。对 factory 的真实消费者逐个确认，不能只编一个空 engine 后宣布完成。

### 4.2 权限身份与 Attempt 组合

Authority 留下 `AuthorityExecutionProfile`、`PromptClaim`、`AcceptedDispatch`、`PromptAuthorityProjection` 和本域身份/claim 决策。它们不得为 `XProjectionChoice` 拉入 context。

将 `AttemptExecutionProfile`、`buildAttemptExecutionProfile` 及只服务该组合的辅助操作迁入 dispatch 的新纯分片，例如 `dispatch-attempt-contract`。组合类型继续包含强类型的 Authority 证据、Provider request kind 与 Context projection choice，不能把这些字段改成 `string`。

`ChatExecution.Facts` 当前的 `ProviderStartedEvidence` 本来就直接包含较低域证据，可保留在 chat-execution。dispatch 的 Attempt 组合向它转换，方向是 dispatch → chat-execution。**禁止把 chat-execution 改为引用新的 dispatch Attempt 类型，否则又形成反向边。**

当前 `OpenCode/Host/ChatAdmission/TransactionSurface.fs` 还直接调用 `PromptAuthority.buildAttemptExecutionProfile`。这个跨域构造型 Surface 要与 Attempt 迁移同批拆入 dispatch/app 的适当 Surface shard，保持原生成路径和外部调用契约；不能把它留在 chat-execution 后给 chat 补一条指向 dispatch 的引用。

`Model.fs` 中同一 `PromptAuthority` 模块若无法跨文件/模块合法拆分，就新建具名 Attempt 模块并迁移全部调用点。不能在 Authority 中永久留下指向 dispatch 的类型 alias 或转发函数。

### 4.3 领域事实与外层包装

| 领域拥有 | durable-composition 拥有 |
|---|---|
| fact case 的领域数据与合法构造 | 将 case 包入 `AgentFact` / 统一 envelope |
| 本域 state、projection、closed rejection | 聚合 `ProjectionSet`、跨 projection 原子应用 |
| 纯 fold / decision | fold 分派顺序、拒绝报告的外部呈现 |
| 自有 append/query/wait/blob capability 类型 | 对真实 Journal 的实现与参数适配 |

不要按 `Fact.fs` 文件名批量移动。有的文件只有 wrapper，有的同文件还包含纯模型、provider terminal validity 或物理路径。按实现内容拆，完整读取 `.fs/.fsi` 与构造点后才改归属。

### 4.4 端口工厂不能再集中在一个公共大分片

将 `AgentJournalPortAdapter` 拆成消费族对应的真实文件与 shard。起步族：Delegation、Attention/Concern、InstitutionalLearning、ProviderFailure、Grounding、Session、Wire、Turn/Terminal；Orchestrator 已有独立适配器，按目标归属继续收口。

每个 adapter 只实现本族端口。需要另一个领域决策时调用该领域唯一实现，不复制。目标消费者只引用端口的分片，不引用 adapter 分片；只在已确认的装配位置调用工厂。

`AgentJournalPortAdapter.fs/.fsi` 最终删除，不能保留一个重新导出全部工厂的大 facade。旧函数是否可以删除，要先穷举 F# 构造点、JS Surface 与直接消费测试。

### 4.5 普通工作流的终态

`OrdinaryTurnWorkflow`、Provider recovery、Change host、Wire 等不能一面接收窄 port，一面继续向下传 `AgentJournal option`。完整收口时，每个保留 Journal 的形参都必须由以下之一替代：本域窄读取、必要的追加能力、revision-aware wait、typed trace/settlement 能力。

端口数不是越少越好，也不是越多越好。同一次原子观察应是一份视图；互相独立的副作用应分成可说明权限的能力。禁止再造 `EverythingPort`、整只 `RuntimeContext` 或装满所有域的 `Services`。

---

## 5. 批次与提交纪律

### 5.1 每批的共同顺序

1. 固定该批基准 HEAD、已有改动、正式条款、真实 consumer 集合与旧观察过程。
2. 写清本批结束后“不再知道什么”，列出所有通向旧类型/旧实现的路径。
3. 先建立对应边界反例；行为有变动风险时建立旧实现能失败的行为证明。不能为仪式伪造失败。
4. 固定目标 public contract 与 `.fsi`，处理同接口上下游迁移顺序。
5. 先使领域实现消费新合同，再把真实适配和构造移到正确位置；删除全部旧消费者。
6. 最后删旧引用、旧包装与空 shard，核对 aggregate 顺序。
7. 对本批关键分片做各自 focused compile；对普通签名影响按既有 impact 合并编译；跑相关行为 suite。
8. 收口时重新计算全图、关键闭包和反向影响；记录最终字节上的证据。

可以拆成多个可编译的小提交；**不允许把第 4 步当整批完成**。中间提交 SCC 不降可以接受，必须列出仍未迁移的确切路径和下一提交收口条件。

### 5.2 协同与并行

共享 `.fsi`、`AgentFact`、aggregate、Journal adapters、ToolRegistry、PluginHooks、Git index、dist 或真实 Host 世界的改动由同一负责人串行协调。调查可并行，修改不得只按文件不同就并行。

一批最多只有一个接口裁决者。Worker 只接受本批任务书，不从历史 HOW 的“下一刀”自行扩工。每个 Worker 必须收到基准、正式条款、读写范围、禁止事项和验收入口。

---

## 6. 从现在到终验的固定施工顺序

总顺序：`B00 → B01 → B02 → B03 → B04 → B05 → B06 → B07 → B08 → B09 → B10 → B11`。

不预先承诺每批 SCC 减几个节点。中间收益以本批实际知识路径和闭包证明；B11 必须对完整图验收到无环。每批失败只停止受影响的切口，不用全仓剪枝填充进度。

### B00 — 清理判据冲突，保存诚实起点

**目标：** 施工人员不再被过期结论、旧 kind 和混合编译口径误导。不改产品行为。

**读：** 根 `AGENTS.md`；structured-workflow WHAT 011—016；verification-system WHAT/HOW；requirement-system WHAT；structured-workflow HOW 第 29—41 条；delegation 的 DELEG-028/029。

**改：** 只修相关 HOW 的现状导航与失效标记，以及已有边界测试中的退役判据。没有必要时不改 WHAT；确与现行条款冲突的预算/证明政策，按 requirement-system 在既有记录中明确处理，不能静默放宽。

**提交切分：**

- `B00.1`：在已有 HOW 的当前缺口入口引用本蓝图，标明旧“单边删除不下降 ⇒ 迁移无益”结论只对当时试验成立；历史数字保留原版本，不覆盖历史记录。
- `B00.2`：清理 `delegation-compile-boundary.test.mjs` 用 `legacyKind` 裁决的问题。用真实 subsystem、源码/签名归属、必要 provider 和禁止实现进入闭包的反例替代。同步核对同包 `m6-slice-boundary`，不制造两套判断。
- `B00.3`：在 verification HOW 明确独立边界编译与合并 impact 编译的分工；纠正“只编极大元即证明全部独立边界”的不完整说法。

**反例：** 同样源码与实际 refs 仅删除 legacy kind 仍应成立；把 foreign adapter 伪装成 contract 仍应拒绝；给小分片漏声明一个真实 provider，即使 aggregate 能编译，独立 canary 仍须失败。

**验收：** 第 8 节结构/impact 测试通过；现有源码集合与 refs 不因这一批变化。静态/文档检查通过。若修改被纳入 repository corpus 的已跟踪文档，后续 dist-backed 测试前重建。

**停工：** 不知道某旧断言保护什么时先读其 WHAT/HOW；不直接删除。旧 proof gap 不得凭导航整理关闭。

### B01 — 把端口的观察语义变成正式行为证明

**目标：** 后续端口迁移不会把 live read 变成初始化快照，也不会打碎同 revision 视图。

**具体文件：** `requirements/durable-events/tests/port-observation-timing.test.mjs`；现有 `Persistence/Journal/Surface.fs/.fsi` 与该包已注册的生产 Surface；相关 HOW 与正式 JS Surface 注册。先用仓库现有 surface inventory 查找合法入口，不深导入 dist 内部模块。

**必须补的行为用例：**

| 用例 | 操作序列 | 必须观察到 |
|---|---|---|
| Live read | 建端口 → 读 A → 经真实 append 改为 B → 同端口再读 | 第二次得到 B；构造时冻结 mutant 失败 |
| 同 revision | 一次提交同时改两个相关切片 → 一次 `ReadView` | 两切片属于同次提交，不允许旧/新混拼 |
| 等待不丢唤醒 | 读 view+revision → 在 waiter 注册边界提交 → await-after-revision | 观察到变化，不挂死、不靠 sleep |
| 取消 | 注册 wait → 取消 → 后续提交 | 原 waiter 已释放，不偷吃后续合法 waiter 的唤醒 |
| poisoned / unknown | 通过现有真实故障注入入口制造该状态 | 不返回默认成功，不把 unknown 记成 confirmed |

只用真实 production surface 和实际 Journal；测试可以注入受控 clock/physical port，但不能复制 Journal 或 projection 算法做 oracle。已有源码断言可作为补充边界保护，不能再把它们描述为运行时证明。

**提交切分：** 合法 Surface 扩口和证明注册一提交；测试与能使错误实现失败的 mutant 证据一提交。Surface 扩口需做签名影响编译与新产物测试。

**验收：** 至少在冻结快照和分裂相关观察的错误实现上分别得到明确失败，在正确实现上通过。记录真实执行的入口，不将临时探针留在 `/tmp` 后当正式证据。

### B02 — 拆出 canonical codec、物理 log 与 Integrator 执行机制

**前置：** B00/B01 完成。此批不要求 Journal 消费者全迁。

**读：** DURABLE-EVENTS-007、016—024；DURABLE-CONVERGENCE-001—007、011；完整读取 `CanonicalEventCodec`、`ProcessEventLog`、`Store`、`CanonicalIntegrator` 的 `.fs/.fsi`。

**提交切分和操作：**

1. `B02.1`：将完整 `CanonicalEventCodec.fs/.fsi` 从 `eventstore-core-runtime` 提到新 codec shard。merge、sync、integrator 只为用 codec 的引用改指它。不要删除仍真实使用 Store/log 的引用。
2. `B02.2`：将 `ProcessEventLog.fs/.fsi` 单独编译，Store 留在原 core。核对 log 依赖是否只需 codec/model 与物理原语。aggregate 保持定义先于使用。
3. `B02.3`：把 `JournalIntegration` 搬入新文件 `Composition/Durable/JournalIntegration.fs/.fsi`；把结构投影与执行机制放入新 `Persistence/EventStore/IntegratorEngine.fs/.fsi`。保留原 `CanonicalIntegrator` 工厂的必需规则检查和公开签名，由它装配规则并调用 engine。
4. `B02.4`：迁移全部 `CanonicalIntegrator.createWithRules` 构造点。按真实规则集与词汇接受集逐项记录：Journal-only、Sphinx 服务、Workspace、全程序 Surface 等不能随便互换。维持传入的完整词汇判定，而不是只根据本次注册的 rules 缩小接受集。

本次值层搜索确认以下8个文件中至少9个直接构造表达式：`Verification/TemporalSurface.fs`（两处）、`Sphinx/ServeEntry.fs`、`OpenCode/Host/WorkspaceEventStore.fs`、`Persistence/Journal/Surface.fs`、`Execution/Delegation/SyncDelegate/Surface.fs`、`Persistence/EventStore/Surface.fs`、`Execution/Delegation/Handle/JournalSurface.fs`、`Execution/Delegation/Fork/OpenCode/ToolSurface.fs`。这八个文件全部进入本批消费者核对；再检查别名和间接工厂，不能继续使用旧记录中的“八个构造点”数字。

**必读补充：** `WriterStreamSync` 的 retention 可能读取 Journal producer activity。沿调用链定位这是 canonical envelope 级协议还是应用 Journal payload 解释；后者由 durable 层提供既有 activity 解释能力，底层仅执行 retention。保持切尾反向越过 metadata 的规则，不把 producer activity 变成 fetch mtime。

**边界反例：** 新 engine、codec 和 merge 的闭包不得出现 `Composition/Durable/Projection.fs`、`Composition/Durable/Fold.fs`、`Persistence/Journal/PromptFactCodec.fs`、具体领域 runtime、Host factory。向 codec 回加 core reference 必须被拒绝。新 engine 不能因 caller 增加一个领域而扩大自身闭包。

**行为验收：** `canonical-integrator`、`unified-store-gate`、append、journal codec/writer、cut-tail、retention、integrator-current-parity；删除 Structural/Journal/任一已有领域注册的 mutant 必须导致该注册对应行为失效或入口拒绝，不能仍产生正确 Current。

**真实编译根：** codec、log、engine 各自独立；原 CanonicalIntegrator 工厂；core Store；eventstore merge/sync；真实 Workspace 与 Sphinx 入口。普通签名影响交一次 compile-impact 求并集。不要只编全程序 Surface。

**本批结束：** engine 已不认识 Journal，factory 仍保持原产品语义。仍在旧 persistence 名下的高层内容明确待 B04 迁移，不能提前宣称 persistence 已出环。

### B03 — 拆开低层词汇中的类型环

**目标：** 为全域聚合上移创造真实的低层接口。优先拆下面四组混装，不能先给它们换 subsystem 标签。

| 组 | 现有文件 / shard | 具体动作 | 后续消费者 |
|---|---|---|---|
| Authority identity 与 Attempt | `Interaction/Authority/Model`；`interaction-authority-fact` | 纯 Authority 模型脱离 `Fact.fs` 包装；Attempt 组合与构造迁 dispatch；原模块不留反向 alias | `ChatParamsHook`、`Planner`、`Wire`、authority/chat 的 Surface |
| Provider failure 纯状态与包装 | `Fallback/Fact`、`FailureBudget`、`RetryPolicy`、`Projection`、`ProviderFailurePort` | 纯状态/预算/端口独立；`AgentFact.ProviderFailure` 包装迁 durable；fold 与跨域 planner 分开 | WirePort、Ledger、Evidence、ChatExecution、durable bridges |
| Context 纯状态与包装/运行时 | `context-companion-fact` 的 13 个 `.fs`；trace model；prefix epoch | `BlogProjectionState`、`ActivePrefixEpoch`、`XTraceProjectionState` 等由真实 context 纯分片提供；wrapper、物理 materialization 分开 | Authority 模型、WirePort、聚合 Projection、WorkRecord |
| Session 基础与业务具体资源 | wait contract / CompletionMailbox / Attachment / scopes | 时间/wait/association 基础保持；Delegation-specific mailbox 和 attachment 高层适配不再被基础类型拖入 | Delegation、Process、Host、dispatch、runtime scopes |

**细分提交：** 每组按“新纯合同 → 原实现改消费 → 全消费者迁移 → 删除旧导出/引用”四步，可合并已证明无风险的机械步骤。Authority/Attempt 与 Provider planner 共用接口，必须同一协调者，不允许不同 Worker 各造一个 Attempt 类型。

**特别处理：** `context-prefix-wire-port` 当前直接依赖聚合 Projection，不能只在 `.fsi` 中隐藏它。先查该依赖为哪个纯 state 提供定义；从原域纯分片获得该类型，修改完整 `.fs/.fsi` 的所有路径，再移除 aggregate 引用。

**边界反例：** Authority identity 闭包无 Prefix / dispatch；Provider failure 纯合同无 Authority / Context / Journal；Context 纯状态无 durable aggregate、chat、dispatch 或 Host；Session 基础闭包无 Delegation、Change 与 runtime scope。不按类型名字或 kind 作结论。

**行为：** authority identity/root/continuation；provider failure/retry；context projection与prefix；wait/attachment原 suite。移动类型时保留 private 构造约束、结构比较、DU case 与 JS codec 的既有语义。

**退出：** 上述低层合同可独立编译。新 dispatch Attempt 组合可以很小但依赖多个较低域，不能为了追求它的零依赖复制领域证据。

### B04 — 建立 durable-composition，并迁出外层 union / 聚合 / Journal

**目标：** persistence 真正只剩存储机制；领域不再从 persistence 反向取得自己的 fact/state。此批是边界迁移，不是把旧文件统一改一遍 subsystem 字段。

**新增 authority 入口：** 只在 `scripts/checks/subsystems.json` 增加真实 `durable-composition`；新 shard 用显式 subsystem/shard 元数据。无需新增 legacy kind。下面迁移允许先产生中间跨域环，最终 B10/B11 才作全图无环验收。

**迁入清单：**

- `Composition/Durable/Fact`、`MagicTodoFacts` 及真正外层 envelope 包装。
- `Composition/Durable/Projection`、`ProjectionState`、`ProjectionUpdate`、`Fold`、`DomainFamilyBridge`、`DelegationProjectionBridge`、`HostFactFold` 和外层 `FoldRejection` 报告。
- Journal 的 codec、应用 Writer/AgentJournal、shared Journal、Journal boot 与 Journal integration rule。
- AuthoritativeVocabulary 和全域规则程序工厂。
- 全域 durable Surface 中真正执行聚合装配的部分；codec/merge 的纯 Surface 留在底层，不跟着整包上移。

**先拆后迁的文件：** 各域 `Fact.fs` 若还同时定义纯领域内容与 `AgentFact` wrapper，分开后只迁 wrapper。`RuntimePath` 与 Git physical helper 不能因为当前在 change-fact 就随 Fact 一起移动。`Host/Fact.fs` 和 Grounding projection / Port 也先拆，Grounding 纯数据归已有领域，外层包装归 durable。

**不要重写 AgentJournal：** 保留它作为 durable 内部的聚合对象，先把用途从公开业务能力收回内部。不是把它的 `Snapshot` 成员硬拆到另一个无法编译的 F# 类型分片；不是换成 `obj`；不是建第二只 Journal。具体业务消费者在 B05—B10 逐族迁出。

**提交顺序：** 纯 fact/state 定义已在 B03 稳定 → 外层 routing/codec → 聚合 projection/fold → Journal 与 boot → 程序装配 → 相应 Surface。每一步同时更新直接消费者与 aggregate 集合。

**边界证明：** persistence 的完整 forward closure 不含任何 durable-composition source；外层 `AgentFact` 与 `ProjectionSet` 只有 durable 定义者；领域自有 fact/state 不通过 durable 取得定义。恢复包裹方向的 mutant 必须失败。

**行为证明：** 同一历史经新装配 live append 与重新打开 Journal 得到相同领域观察、revision/identity与失败结果；不能只比较聚合对象字段数。执行注册删除反例与已有 replay / cut / unknown / fatal 测试。

**收口条件：** `persistence → durable-composition` 为零。此刻仍存在的 `领域 → durable-composition` 是后续消费者债，必须逐条可定位，不能写成“高层已分离，全部完成”。

### B05 — 拆分端口适配器，完成第一组真实消费者

**目标：** 不再用一个 208-source adapter 分片服务所有域。先选已经有窄合同的族，验证“业务只收端口，工厂留在装配”的完整模式。

**第一组固定为：** Delegation recovery、Attention/Concern、InstitutionalLearning、SessionStartedAt、RequirementGrounding。这些已有端口，不再重复发明。

| 族 | 领域侧起点 | adapter 起点 | 本批必须连带处理的构造位置 |
|---|---|---|---|
| Delegation | `Execution/Delegation/JournalPort`、Handle recovery | `fromAgentJournal` | Fork Host / Fission / runtime Surface 的实际创建者 |
| Attention / Concern | `Interaction/Attention/JournalPort`、`Interaction/Concern/JournalPort` | `forAttention`、`forConcern` | `OpenCode/Tools/ToolRegistry` 及对应 Tool Surface |
| InstitutionalLearning | `Enforcer/InstitutionalLearning/JournalPort` | `forInstitutionalLearning` | 同一个 ToolRegistry，不可另一个 Worker 同时改 |
| SessionStartedAt | `Execution/Session/SessionStartedAtPort`、Ledger | `forSessionStartedAt` | Calibration、Host/turn 的真实调用点 |
| Grounding | `OpenCode/Host/RequirementGrounding/Port` | `forRequirementGrounding` | Grounding runtime/gate/transform 的实际接线点 |

**步骤：**

1. 从共享模块提取一个族的 adapter 文件和独立 shard，暂保留未迁族。新 adapter 归 durable-composition。
2. 核对领域端口所在 shard 是否仍因其它同片源码引入 aggregate；必要时把合同单独成片，而不是复制 record。
3. 领域工具/运行时必须只收已有端口。若它在内部调用 `forX journal`，把这一次构造移到其上层真实装配入口。
4. 所有构造点迁完再删除共享模块对应导出。外部 JS Surface 的参数对象、optional Journal 的既有缺省行为保持；由 Surface 的上层装配内部完成适配，不要求 JS 测试突然传 F# 闭包。
5. 各族 adapter 用真实 Journal 跑 B01 的适用观察用例，再跑原业务 suite。

**特别保护：** Delegation 的 digest 与 blob 仍由同一物理实现提供；append / write blob 的先后不变。恢复运行时不能因新增一个 unrelated Wire 端口而扩大编译输入。

**本批闭包验收：** 对一个族新增合同或实现变更，另一个无关族的 adapter 与领域核心 forward closure 不增长；Delegation recovery 仍不认识 aggregate；工具核心不引用 adapter 工厂。禁止把 208 的旧预算按新总数整体上调来掩盖混装。

**退出：** 上述五族完成。共享 adapter 的未迁导出清单继续保留为剩余工作，不立即删除整个文件。没有编译实际构造者，不能交付。

### B06 — 迁完 Authority / Provider / Context / ChatExecution 的业务读写

**目标：** B03 拆开的纯词汇不再被各域运行时绕路拉回 Journal；跨域组合移到 dispatch，物理历史适配移到 durable。

**施工顺序固定为 A → P → C → E：**

**A. Authority。**

- 处理 `Interaction/Authority/Ledger`、`Child`、`Interaction/Repair/CompletedTurn`、`OpenCode/Host/ChatAdmission/Intent` 和 `TerminalPolicy`。
- Authority 的 claim/identity 决策只接本域 projection 与合法证据。Ledger 中从聚合选出 Authority 的读取移到 adapter。
- 同时看 child handle、Orchestrator jobs、poison 的 TerminalPolicy 是跨域终端裁决，其组合部分迁 dispatch。Authority 只提供自己的角色/权限决策，不能让 Authority 下层合同引用 delegation 或 change。
- CompletedTurn 与 InteractionRepair 中跨域 repair 流程迁 dispatch；纯权限判断留 Authority。不得整体移动 Authority 业务实现来减少节点数。

**P. Provider。**

- 将 `participant-provider-attempt-planner` 拆为本域纯 failure fold/evidence 与 dispatch Attempt planner。`PlannerSurface` 若装配跨域 Attempt，不再编进低层 Provider routing shard。
- `Fallback/Ledger` 只接已有 `ProviderFailureJournalPort`；`Fallback/Workflow` 中跨域 ordinary turn 恢复归 dispatch，保留重试许可、budget、owner 与取消语义。
- `ChatParamsHook` 的 Host 物理包装迁 Host/app；ModelCapacity/ModelRouting 的纯判定留 provider，读取 Authority/Context/Session 通过本地必要输入或 capability，不 import 它们的 runtime。
- 删除 Wire adapter 内的成功结算副本，统一调用 `ProviderFailureLedger.recordConfirmedSuccess`。顺序是先使 Ledger 不依赖 durable，再让 adapter 调它，防止新建分片环。

**C. Context。**

- `CompanionFactFold` / `ContextFactFold` 已采用纯 change-list 的部分保持；从同片的 Runtime/Host/Coordinator 分离，避免纯 fold 因这些文件继承胖闭包。
- Blogger Runtime / Coordinator 收本域 journal capability 与宿主能力，不收 `ProjectionSet`、`AgentJournal`、`PluginRuntimeScope`。一次操作同时需 Blog/Enforcement/PrefixEpoch 时，用一份同 revision 视图；不要各加一个独立 getter。
- 将 Wire 分成 Context 纯 prefix decision / render 与 dispatch 的 async Wire workflow；`WireJournalPort` 的跨域观察合同随其消费者归 dispatch，Context 的 state record 留 Context。
- `Context/Trace/Materialization` 拆出纯 materializer 与 blob/read adapter；`Capture` 的协议输入留领域，真实 Journal 写入归 durable adapter。`TerminalReporter` 的跨域结算顺序归 dispatch，保留合法 Surface 入口。
- `XWireSurface` 中调用纯 decision 的部分留相应纯 shard，构造跨域 Attempt 的部分编进 dispatch 的 Surface shard。生成路径需要不变时不移动源路径；编译归属和文件内容按职责真实拆开。

**E. ChatExecution。**

- `Facts/Projection` 留 chat-execution；`Fact` 的外层包装归 durable。
- Acceptance/Settlement/Recovery 只接 Accepted/ProviderStarted/Terminal 证据及本域状态；不得收全局 Journal。
- `ChatAdmission/Transaction` 把跨域原子追加表达为具名能力，由 durable 在原单次事务边界完成。不能为了各域独立把原原子事务拆成数次可部分提交的 append。
- Host session recovery 接线归 app/Host，恢复决策留领域/dispatch。

**真实消费者必须包含：** `OpenCode/Host/HostTurnObserver.fs` 的两处 `XWire.reconcileAttempt` 与 `OpenCode/Plugin/PluginTransforms.fs` 的两处 `XWire.applyTransform`；ChatParamsHook；OrdinaryTurnWorkflow；Provider failure Surface；Chat admission/recovery Surface。本基准不存在 `OpenCode/Host/Host.fs`，不能照抄历史记录虚构第三个 Wire 文件。用第 13 节库存与值层搜索扩齐，不能把本表当全部调用方的永久名单。

**验收：** Authority 不依赖 Context/Delegation/dispatch；Provider 核心不依赖 Authority/Context/chat/dispatch；Context 不依赖 chat/dispatch/Host/durable；ChatExecution 不依赖 dispatch/Host/durable。四组各自独立 focused compile，加签名影响并集与相关 package suite。

**停工点：** 有一个公开组合类型同时被上下两层需要，就读其字段与构造者，拆真正的低层证据与上层组合。禁止为了过 DAG 把组合全部丢进 platform 或退成字符串。

### B07 — 收回全局 scope，清理 Session / Process / Delegation 的资源回环

**目标：** 领域不再靠整只运行时容器取得权限；会话基础不反向认识具体委派或插件装配。

**先增 `application-composition`，再分配实际拥有者。** `PluginRuntimeScope` 与 `ToolRuntimeScope` 的“创建、持有、销毁多个领域实例”的部分归此层；它们的领域使用面继续迁成窄能力，不把所有运行时方法原样暴露为巨大接口。

**提交切分：**

1. `B07.1`：在 Session 内分开基础与业务资源。`CausalWait/Registry/Await`、association、ownership、started-at、clock/deadline 保持低层；`CompletionMailbox` 若读取 Delegation completion vocabulary，迁 delegation。Attachment 的基础 lease/ownership 不携带具体 Fork runtime；高层恢复协调归 dispatch/app。
2. `B07.2`：逐消费者处理 scope。Executor/Chronicle 收终止能力；PtyTool 收进程启动/观察及必要身份；Blogger 收已有 `IBloggerRuntimeHost`；scheduler 收明确的观察/执行端口。已有窄接口优先复用，不另写同名接口。
3. `B07.3`：将纯 Process request/output/spool/PTY 实现与 PtyTool、ExecutorTool、Distillation 装配分片分开。Process 只依赖低层时间/取消与原语，不依赖 Delegation 或 dispatch。Delegation 的 PTY adapter 使用 Process capability，不能让 Process 反向读 HostForkRuntime。
4. `B07.4`：迁 `SessionExecutionBinding` / `RecoveryClosureProjection` / `TurnRuntimePreparation` / `HostTurnObserver`。真实身份绑定与恢复决策保持在所属域；从聚合读多域并装配运行时的部分归 dispatch/app，不能继续放在 session 基础 shard。
5. `B07.5`：Delegation Host adapter、JoinGuard、Fission Host、HorizonTool 逐一不再接整个 scope / Journal。已有 Delegation projection change-list 与 journal port 保留。移动 owned handle 的注册表时保留唯一实例和 drain-before-dispose 顺序。

**特殊禁止项：** 不把 `ISessionRuntimeOwner` 重新扩成全套 Host/Fork/Journal service locator；不复制 `ExplicitResumeSuppression` 的 mutable map；不引入第二个 CancellationTokenSource 掩盖旧 token 的生命周期；不以 no-op fallback 处理缺失 runtime。

**行为证明：** 取消后零发送、waiter 释放、join completion exactly-once、PTY drain/exit、重复 dispose、独立 session 不串状态、fission/recovery 与 Host 身份映射。复用正式 deterministic temporal harness，不靠随机 sleep。

**闭包验收：** Session 基础无 Delegation/dispatch/Host/durable/app；Process 无 Delegation/dispatch/Host/durable/app；Delegation 核心无 Host/durable/app。资源装配根允许宽，但业务核心不能反向引用它。

### B08 — 逐个迁完领域集成，消掉剩余外围反馈边

**目标：** Sphinx / JS programming / Knowledge / Strength / Work / Change 不再因入口、Surface 或持久化 adapter 被留在同一个 SCC。

按下面顺序，每个领域一个闭合子批，不并行修改公共 aggregate 与 Journal 接口。

| 顺序 | 领域 | 必动位置 | 固定处理方式 | 行为验收重点 |
|---:|---|---|---|---|
| 1 | Sphinx | `IntegrationRules`、`GenericDurability`、`McpServer`、`ServeEntry`、`Surface` | 单领域纯 integration rule 可继续归 Sphinx并依赖 persistence contract；全程序服务入口归 app，全域词汇在 durable，McpServer只收已注入 store | 原服务路径发布、stdio、持久化重启、未知词汇拒绝、缺失规则失败 |
| 2 | repository-programming | `ToolsBindings`、`TransactionStore`、`OpenCode/ToolWorkflow`、`ToolHost`、`GeneratorSurface` | 纯/物理 JS 能力与 Host 工具包装分片分开；前者不认识 Host，包装在 Host/app；领域 IntegrationRules 只依赖低层存储合同 | mutation/CAS、事务拒绝、capability enforcement、发布 JS 面的参数与结果 |
| 3 | knowledge | 混合 `repository-knowledge-casebook-model`、Bookkeeper/Lifecycle、Fetch/Bookkeeper tools | Model/replay/index 纯核心拆出；ObservationCollector只接所需输入；全局 WorkspaceEventStore获取归app；Host工具包装迁出 | 同事实 capture/replay/index、refresh、身份/digest、缺失数据不伪造成功 |
| 4 | strength | Predictor/Replica/Transform/Runtime、TurnEvidence、Settings/Speculate、DurabilityPort | Budget/prediction/replica纯决策留域；跨域 turn/model/Host装配迁dispatch/app；单领域store adapter只依赖persistence窄合同 | rollout身份、budget、commit/promotion、重复终结、持久化重启与拒绝 |
| 5 | work | `mission-obligation-todo-model`、`Materialize`、`LedgerWorkflow`、`MagicTodoMembrane` | 义务模型/语义留work；外层MagicTodo事实、全域投影codec归durable；blob读取注入；Host membrane的物理适配归Host/app | 义务顺序、重复追加、prefix rebase、统一renderer、原子提交与unknown |
| 6 | change | `Git/Gateway`、`IntegrationGate`、Change runtime/recovery、`OrchestratorJournalAdapter`、Host files | Orchestrator业务与纯恢复留change；Journal adapter归durable；Host构造归app；Git同步helper与业务Git操作分离 | claim互斥、超时、worktree释放、恢复视图同revision、relay/await次序 |

**Git 回环的具体切法：** `git-hook-sync` 不能继续为了通用 Git transport 调用 Change 的 `GitGateway`。抽出真实被 Hook 使用、且不认识 Change job/policy 的 Git 操作实现，归 persistence 的 Git adapter，或由已有无领域原语提供。Change gateway 调同一实现/能力，禁止复制一份 Git 代码。Hook 的进程入口装配归 app；同步算法、retention、writer/payload cache 与 CAS 留 persistence。

**Sphinx 的出环判据：** 全域词汇与全程序 Surface 不再编进 persistence，所以 persistence 不再反向依赖 Sphinx；Sphinx 合法依赖存储合同可以保留。不是把 `sphinx → persistence` 强行抹掉。剩余 Sphinx entry 若还引用 durable，移入真实 app 入口而不是修改统计过滤器。

**每个子批验收：** 对该域所有跨域入/出边追到具体 shard，检查是否只指向第 2.3 节较低的窄合同/decision；全部直接关键消费者独立编译、必要签名影响并集、正式领域 suite。若该域仍在 SCC，必须输出一条带真实分片见证的闭合路径，而不是一句“其它环还在”。

### B09 — 清理 Host 反向业务装配和全工具注册

**目标：** Host 是物理协议边界，不再同时代表所有业务和整套插件。

**具体迁移：**

- `HostSignalBootstrap`、PluginHostWiring/SessionWiring/RecoveryWiring、PluginBoot、WorkspaceEventStore 的真实构造归 app；Host 仅实现订阅、消息/信号解码、SDK调用、物理终止等边界能力。
- `Host/Message`、SDK 类型和外部事件结构留 Host。Domain 所需输入由 Host 对域合同做 typed 转换，不能把 Host codec 下沉到 domain 仅为了共享一个字段。
- `OpenCode/Signals/EventContract` 若带 ChatExecution outcome，分清原始 Host 信号与业务解读；原始协议留 Host，业务 outcome 来自已定义领域类型，由适配转换。不要让低层 domain 为了一个信号名字反向 import Host。
- `ToolRegistry`、ManagedAgentConfig / ManagerConfig 中决定装配全部具体工具的部分归 app；权限判断依据和规则留 participant/authority/interaction。所有工具都必须真实注册，不能因模块缺失静默跳过。
- Attention/Concern/InstitutionalLearning/Review/Suicide/Coder/Inspector/Executor/Pty/Horizon/Bookkeeper 等工具拆为领域语义入口和 SDK ToolSpec 包装。领域工作流接收自己的参数/能力；Host wrapper负责 SDK schema、abort signal、result bounding 与回传格式。
- ProviderSystemTransform 的跨域 prompt/role 选择是dispatch/interaction决策；资源/Host读取作为能力接入，Host不负责复制知识规则。

**保留边界：** Sphinx launch 物理 adapter 可以依赖 Sphinx 发布的纯入口数据；nodeTool codec保留真实SDK validator；fatal capability 的report owner/kill owner保持。Host不得反向构造durable或查全局Journal；需要两者的地方只在app接线。

**证明：** host codec/abort/signals/plugin-load-purity、工具注册与权限矩阵、fresh build 的 plugin smoke；缺失必要工具、重复register、构造时读持久化或加载时业务重放均要失败。

**退出：** Host全图出边仅指向更低领域合同/纯decision及必要原语；Host→durable/app 为零。interaction/output不再为纯业务语义依赖Host实现；app之外不再存在全工具注册工厂。

### B10 — 完成 Dispatch / Turn 的最后贯通，删掉全部宽入口

**目标：** 前面拆出的合同真正贯穿最终产品调用链；dispatch不再从应用装配层取得工厂。

**必须逐个收口的工作流：**

| 工作流 | 起点 | 最后要删掉的知识 |
|---|---|---|
| OrdinaryTurnWorkflow | `Composition/Turn/OrdinaryTurnWorkflow` 的 observe/idle/completed/outcome/failed | `AgentJournal option` 形参、内部 `forX`、透传给 sibling 的 Journal |
| Wire / Attempt | 原 `Context/Prefix/Wire`、Provider `Planner` 与对应Surface | 全域聚合、PluginRuntimeScope、对durable工厂的引用 |
| Dispatch / Ingress / Recovery | `Interaction/Dispatch/{Dispatcher,Send,Ingress,Recovery}`、`Composition/Turn/{Binding,TurnReconcile,ReconcilePass}` | 聚合快照、Host物理实现与全局registry |
| Scheduler / Reconcile | `Composition/Turn/Scheduler/Program/Observation` | Journal、物理Host句柄、应用scope；保留本来就有的显式业务顺序 |
| Terminal / Join / Repair | TerminalReporter、TerminalPolicy、JoinGuard、InteractionRepair | 跨域runtime探针、Journal pass-through与重复成功结算 |
| Manager / Relay | `Mission/Manager/Workflow`、`Composition/Turn/Workflow`、NarrativeTransform | durable工厂、Host内部状态、通过legacy角色标签找运行时 |

**最后的端口形状：** 只包含本工作流真正需要的观察、追加、等待和物理能力；同次相关读取返回一个窄不可变视图。若本工作流需要对几个域做原子提交，用一个有明确事务语义的 capability，不能拆成各域独立append后再补偿。

**OrdinaryTurn 特别清单：** `recordSuccessIfValid` 调领域Ledger；terminal policy、join guard和trace各用已迁端口；`awaitRecoveryMaterial`保留revision/cancel语义；InteractionRepair 的 send/idle repair走同一合法dispatch能力。删任何一个Journal形参前，同时读完它的所有sibling和调用者。无消费的端口直接删除，不能为了证明“迁过了”永久保留。

**最终构造点：** `PluginHooks/PluginTransforms/PluginHost` 等app入口从同一Journal实例构造durable adapters，从同一Host实例构造Host ports，再把它们注入dispatch。业务函数内部不调用app/durable工厂。

**删除清单：** 共享`AgentJournalPortAdapter`、已退役Journal宽重载、跨层Attempt类型alias、临时双入口、未使用端口、已空分片、旧aggregate条目与过期源码形状断言。删除前检查F#和正式JS Surface全部消费者。

**验收：** dispatch→host/durable-composition/application-composition为零；所有已迁领域→装配层为零；两个装配层之间只允许app→durable，且durable不依赖Host。以完整图确认，不根据目录前缀筛出漂亮子图。

### B11 — 完整图无环、可替换性和正式验收

**进入条件：** B00—B10的剩余列表为空；没有“以后再迁”的宽入口。完整命令见第8和12节。

1. 在完整 `buildSubsystemInventory()` 上要求 `cyclicComponents.length === 0`。分片DAG、唯一归属、aggregate等价仍必须同时成立。
2. 对尚有的任何SCC，输出一条真实分片引用见证，按第10节五类故障归位修复。不得把节点合并或按kind剔除后宣告通过。
3. 对persistence内核、Authority身份、Provider failure、Context纯状态、Delegation recovery、Process核心、dispatch纯合同和实际app入口分别做独立编译。普通影响集合仍走现有合并流程。
4. 对典型领域用同一公开合同的测试capability代替物理adapter，执行真实生产decision/workflow，证明不需要foreign runtime。替换测试不复制生产算法。
5. 跑匹配当前字节的正式`npm run format-build-test`，保留所有层的真实结果、已知proof gap和环境阻塞。不能用本蓝图的静态检查替代Long Stroke。
6. 补齐HOW中的当前实现与proof map，按现行验收条件关闭GAP-033；不是只把SCC数字改为0就关闭。
7. 清理本轮退役材料与AGENTS施工提醒。保留正式合同、有效测试、编译资产和必要日常入口。蓝图归档/撤下时先清理导航，不留第二套永久治理。

---

## 7. 全部现有 subsystem 的归宿与交接表

本节不按旧kind划分合法性。列出的混合片是现场入口；每一行都要通过第13节重新读取当前库存，扩齐消费者。各域对应的原requirement package归属不因source subsystem迁移而重编ID。

| subsystem | 保留的核心 | 必须迁出的混装 / 具体入口 | 主批次 | 结束时的硬条件 |
|---|---|---|---|---|
| authority | IdentitySeed、Origin、Authority身份/claim/run投影与决策 | Fact包装→durable；Attempt组合/TerminalPolicy/Repair协调→dispatch；Host物理包装→host/app | B03/B06/B10 | 不再指向context、delegation、dispatch、Host或聚合；不存在指向新Attempt的旧alias |
| change | Orchestrator facts/fold、Job/Program、纯Recovery、业务Git能力 | `OrchestratorJournalAdapter`→durable；Host构造→app；Hook通用Git操作从Gateway拆开 | B04/B08 | 不创建Journal/Host，不为通用Git同步反向拉入装配 |
| chat-execution | Facts、Projection、Acceptance/Settlement、纯Recovery | 外层Fact→durable；全域transaction实现→durable；Host恢复接线→app | B06 | 只消费较低证据与本域能力；不认识dispatch Attempt类型 |
| context | Blog/Companion/Trace/Prefix模型、纯fold/render、窄能力运行时 | `Fact`wrapper、Journal materialization→durable；Wire跨域流程/TerminalReporter→dispatch；Host构造→app | B03/B06 | 无chat/dispatch/Host/Journal聚合闭包；state真实自有 |
| delegation | Facts、Linkage、pure fold、recovery、fork/sync核心、委派专用mailbox | Factwrapper→durable；Host/PTY接线→Host/app；JoinTool等SDK包装→Host；跨域协调→dispatch | B05/B07 | recovery与核心不认识Host/durable/app；PTY不读HostForkRuntime私有registry |
| dispatch | Attempt/Turn/Reconcile/Ingress/Repair/Manager跨域业务流程与合同 | PluginBoot/Hooks/Registry/真实实例装配→app；所有forX工厂→durable/Host | B03/B10 | 对host、durable、app无引用；没有Journal pass-through |
| enforcer | Catalog/Projection/Repair决策、LoopDetector、InstitutionalLearning | Host LoopSensor接线→Host/app；Blogger/Journaling→端口；Chronicle/Surface混装拆开 | B05/B06/B09 | 不拉chat/dispatch/Host/Journal；不复制Blogger或Provider规则 |
| host | SDK协议/codec、物理session/message/signal/tool/fatal adapters | Bootstrap、SharedState全域装配、WorkspaceEventStore、PluginScopes、全工具注册→app | B07/B09 | 不引用durable/app；不承担业务state machine或业务store装配 |
| interaction | Attention/Concern、Guidance、工具领域语义与capability决策 | ToolRegistry/ManagedAgentConfig装配→app；所有SDK工具壳→Host；Journal工厂→durable | B05/B09 | 工具核心不直接拿Host/durable/app；注册完整、无静默缺项 |
| knowledge | Casebook模型/replay/index/capture决策、Bookkeeper业务 | 混合model中的Host/Workspace acquisition拆出；工具包装→Host；app持有store生命周期 | B08 | 不因调用Bookkeeper拉入Host、scope或Journal aggregate |
| output | 输出蒸馏业务流程与语义 | `DistillationSurface`的真实Journal/Host装配→高层；复用Process端口 | B07/B09 | 不依赖Host/durable/app；不再因Surface把本域变成孤立名义节点 |
| participant | Role、OfficeCapability、Persona、ManagedAgent词汇 | 不接收别域状态；不为了消环塞入Attempt组合 | B03核对/B11 | 独立、无业务反向引用；身份合法构造保持 |
| persistence | event model/port、canonical codec、merge/retention/sync、物理log/Store、engine | aggregate/Journal/rules program/vocabulary→durable；Hook真实入口→app | B02/B04/B08 | 整体闭包无业务域、Host、durable/app；只有低层原语与必要自身合同 |
| process | ProcessRequest、Spool、ProcessRunner、PTY实现与能力 | PtyTool/ExecutorTool/Distillation业务与Surface装配拆出；委派资源不归Process | B07 | 不引用Delegation/dispatch/Host/durable/app |
| provider | RequestKind、FailureBudget/RetryPolicy/FailureProjection、消息与projection原语、路由决策 | 跨域Planner/恢复Workflow→dispatch；Host codec捕获/ChatParamsHook物理面→Host；Journal→durable | B03/B06 | 不引用Authority/Context/chat/dispatch/Host/durable；纯routing决策只接必要输入 |
| relay | Contract/Facts/Fold/Assessment/Retirement、snapshot能力接口 | Review/Suicide工具壳及Narrative跨域编排不迁回relay | B08核对 | 保持既有出环；不为省一次参数重新引入Journal/Host |
| repository-investigation | Semble/WarmStart业务与物理调查能力 | 全工具注册不归此域；不搬进通用platform | B09核对 | 无更高领域/Host/装配引用；已有纯/物理分片按实际用途保持 |
| repository-programming | Capability/Transaction/Anchors/Mutation、纯integration rule | ToolsBindings中Host部分、Generator/ToolHost混装拆开 | B08 | 核心不指向Host；真实物理mutation权限和失败语义保留 |
| requirements | Model/Catalog、纯glob与Grounding事实词汇 | 全域HostFact包装→durable；运行时注入在interaction/dispatch，Host包装在高层 | B03/B05 | catalog不拉runtime/store；不扩大APPLIES-TO当任务传播机制 |
| resources | PromptCatalog、Provider资源访问 | Role/运行时prompt组合归原决策域；全域runtime安装在app | B09核对 | 不读取高层状态，不当shared-domain DTO桶 |
| runtime-platform | 既有无领域决策原语与物理原语 | 不接收Journal/Projection/Attempt/Session工作流；新抽取先审查领域知识 | 全程 | 对任何domain出边为零；不靠复制crypto/Git等实现绕边界 |
| session-lifecycle | CausalWait、时钟/期限、association/ownership、基础lease与生命周期 | Delegation mailbox→delegation；恢复编排→dispatch；Plugin/Tool scope与构造→app | B03/B07 | 核心不引用Delegation/dispatch/Host/durable/app |
| sphinx | Sphinx纯核心/MCP协议、单领域integration rule、窄store适配 | ServeEntry/全程序Surface装配→app/durable | B08 | 只向低层存储合同/原语；stdio/restart证明保持 |
| strength | Budget、prediction/replica状态与决策、单领域integration rule | TurnEvidence跨域协调→dispatch；Settings/Speculate Host接线→app；Journal映射→durable | B08 | 不读取Host scope/aggregate；当前failure/commit/identity不变 |
| verification | 正式证明入口/Surface | 不成为生产算法提供者；不把所有Surface按名字搬入此域 | B01/B11 | 生产无入边依赖verification；证明调用真实生产代码 |
| work | Todo/义务模型、工作记录、纯renderer与ledger决策 | MagicTodo外层routing/aggregate codec→durable；Host membrane壳→Host；blob/admission只收capability | B08 | 不拉Journal/Host/装配；单次提交与canonical materialization不变 |

**迁移后新层的反向检查：** 搜索所有`durable-composition`与`application-composition`的直接消费者。允许前者被app及合法高层证明入口消费，后者只被真实入口/证明入口消费；其它命中逐个追到公开签名和调用点，不靠改标签赦免。

### 7.1 当前共享 adapter 的全部直接消费者

本次库存中共有12个，拆工厂时至少要逐个处理，不能只改PluginHooks：

```text
change/git-integrationgate
delegation/delegation-host-adapter
delegation/delegation-runtime-surface
delegation/execution-fission-opencode-host
dispatch/composition-turn-ordinaryturnworkflow
dispatch/composition-turn-workflow
dispatch/plugin-composition
interaction/enforcer-guidance-tip
interaction/opencode-host-managedagentconfig
interaction/opencode-host-requirementgrounding-runtime
provider/participant-provider-attempt-fallback-ledger
session-lifecycle/opencode-host-turnruntimepreparation
```

这是基准事实，不是永久白名单。迁完后重新计算真实集合；新增的adapter/shard也必须进入库存与真实消费者证明。

### 7.2 容易遗漏的三类消费者

**只传句柄的中间层。** 它没有调用`snapshot`不代表没有类型依赖。必须继续追到最终使用者，连同签名和构造点一起迁。

**同片Surface。** 生产文件已迁完，但Surface还编译在同一个shard，完整闭包仍宽。这时按证明用途拆Surface，而不是删除测试或改变现有JS参数契约。

**借传递闭包的消费者。** 删除C→T后，C自身能编译，反向消费者可能原来借C看见T。对真实依赖给它补正确声明；对不该有的依赖迁合同，不能默认全部补回以保持绿色。

---

## 8. 验证手册：每种结论用对应的证明

### 8.1 从仓库根执行的现有命令

以下命令和CLI参数在编写本蓝图时已核对。新分片创建后用真实库存定位工程，不能照猜项目文件名。

```sh
# 现场。检查输出，不要仅保存一个看似干净的HEAD。
git status --short --branch
git rev-parse HEAD

# 结构事实和既有编译边界/impact性质。
node scripts/checks/subsystems.mjs
node --test \
  requirements/structured-workflow/tests/subsystem-boundaries.test.mjs \
  requirements/structured-workflow/tests/owner-project-boundaries.test.mjs \
  requirements/structured-workflow/tests/owner-impact-compile.test.mjs

# 原始静态检查，不将wireit缓存命中描述为本次重新检查。
node scripts/check.mjs

# 已有证明注册完整性，输出proof gap不等于已证明。
node scripts/checks/requirement-trace.mjs

# 全量构建后才运行依赖dist的语义测试。
npm run build
node requirements/verification-system/tests/run.mjs

# 大边界批次收口 / 全局最终验收。
npm run format-build-test
```

`format-build-test` 当前包含format检查、check、build、semantic、integration、唯一e2e Long Stroke入口和pack dry-run。执行时再看当前`package.json`；不能复制旧编排私自漏项。禁止`dotnet build`；本项目正常Fable内部编译与Fantomas不在此禁令内。

### 8.2 单分片独立验证

下面的脚本直接通过现有库存定位真实项目。它只编指定分片的forward closure，不把其它消费者的额外源码混进来。

```sh
# 示例为当前真实存在的分片。换成当批关键边界的subsystem/shard。
SHARD_KEY='persistence/eventstore-core-runtime' node --input-type=module <<'NODE'
import { spawnSync } from 'node:child_process';
import { buildSubsystemInventory } from './scripts/checks/subsystems.mjs';
const key = process.env.SHARD_KEY;
if (!key) throw new Error('SHARD_KEY is required');
const inventory = buildSubsystemInventory();
if (!inventory.ok) throw new Error(inventory.violations.join('\n'));
const project = [...inventory.projects.values()].find(p => p.shardKey === key);
if (!project) throw new Error(`Unknown current shard: ${key}`);
const result = spawnSync(process.execPath,
  ['scripts/compile-owner.mjs', project.projectRepoPath], { stdio: 'inherit' });
if (result.error) throw result.error;
if (result.signal) throw new Error(`Compile terminated by ${result.signal}`);
process.exit(result.status ?? 1);
NODE
```

每批选的是正在承诺独立的边界，不是全仓245个工程逐个重编。选中多少关键边界就报告多少；没独立编过的分片不能由一个大工程代为宣称通过。

`compile-owner`沿用旧文件名，不恢复owner治理。当前CLI帮助要求候选工程名使用`Wanxiangshu.Owner.*.fsproj`；新工程沿用该可工作的命名即可。不要顺带修改工具命名系统。

### 8.3 签名影响合并验证

本批所有变化先经既有impact求并集，不为每条引用启动一轮全量编译。

```sh
# 读取当前工作区变化并打印计划；这里只是计划，不是编译证明。
node scripts/compile-impact.mjs --plan-only

# 实际执行。也可显式传入本批所有改变路径；列表不能漏掉fsi/新增删除项。
node scripts/compile-impact.mjs
```

工程/aggregate/toolchain变化触发full是现有保守行为，不擅调`--threshold`掩盖影响。若分批提交后自动检测不再包含上一提交，显式提交本批全部变化路径，或在最终批次边界重新运行相应完整验收；不能用“工作区无diff”证明整批输入没变。

**两种验证各管一件事：** 独立focused compile证明新边界的声明足够；impact合并编译证明这批变化的消费者兼容。后者不能替代前者，也不要求所有不相关消费者都独立重编。

### 8.4 纯声明清理何时可以复用编译证据

只有当同一分片的输入字节、完整compile顺序、`.fsi`、props、defines、工具链、资源及有关配置都一致时，才可复用其旧编译证据。仅比较文件数或basename不够。

删一条冗余声明后，若按同一工具生成的flat工程和所有输入完全相同，可以记为“声明清理，输入未变”；不要宣称编译加速或知识隔离。只要闭包/source/signature/config任一改变，按本节重新验证。

### 8.5 批次到正式行为suite的映射

这是最小关注面，不豁免现有impact或正式整体验收。先build，再执行引用dist的test文件。

| 批次 | 已存在的重点测试入口 | 新增证明要放哪里 |
|---|---|---|
| B00 | structured-workflow的subsystem/owner-boundary/impact；delegation compile-boundary | 修现有测试的性质与反例，不建新ACL |
| B01 | `requirements/durable-events/tests/port-observation-timing.test.mjs`、`journal-subscription.test.mjs` | 原包正式Surface、原HOW；把行为证明与源码辅助断言分清 |
| B02/B04 | durable-events的`canonical-integrator`、`unified-store-gate`、`event-store-append`、`event-store-journal-{codec,writer,boot}`、`event-store-compile-boundary` | engine闭包、必需注册、未知词汇、cut/unknown/replay的真实反例 |
| B02/B08 | durable-convergence的`integrator-current-parity`、`writer-retention`、`writer-stream-sync`、`hook-performance-fast-path`、`event-store-merge.property` | codec与物理边界；同cutoff retained-history的真实Current一致 |
| B03/B06 Authority | interaction-authority的`authority-execution-profile`、`authority-root`、`authority-acceptance-identity`、`continuation-origin` | 合法证据不可伪造；Attempt拆分不改来源与拒绝 |
| B03/B06 Provider | provider-attempt-recovery的`provider-failure-ledger`、`freeze-admission`、`failure-budget`、`retry-policy`、`retry-owner` | 成功结算单一实现、预算不重复推进、原owner与取消 |
| B06 Context/Chat | context-compression、prefix-stability、semantic-trace、managed-chat-execution、host-boundary的`xwire.test.mjs` | 单revision视图、既有projection序列、accepted/started/terminal证据 |
| B07 | delegation、causal-wait、time-capability、process-execution、managed-session-lifecycle的正式suite | waiter/PTY/drain/identity与dispose的deterministic反例 |
| B08 | epistemic-reasoning、repository-programming、knowledge-reuse、speculative-investigation、work-record、obligation-ledger、change-integration | 每域真实store/Host适配后的原行为，不能只有接口构造smoke |
| B09 | host-boundary的`plugin-load-purity`、`tool-host-codec`、`tool-host-abort`、`host-signal-bootstrap-composition`、`session-execution-binding`及权限相关suite | 实际注册、加载时无业务副作用、Host/durable实例配对 |
| B10/B11 | structured-workflow、相关dispatch/repair/terminalsuite及完整format-build-test | 完整图无环、关键独立canary、正式Long Stroke |

表内省略`.test.mjs`的名称均指同目录的该后缀测试；施工时用实际文件确认。领域suite的注册与执行仍以verification-system现行HOW/run.mjs为准，不自行跑一个目录里的部分测试就声称全包通过。

### 8.6 必须写成性质反例的结构保护

已有结构测试可以扩展下列性质，不复制一套manifest。

- 未改变知识，仅伪造legacyKind或改项目标签，不能使非法依赖通过。
- 任一分片闭包把自己的宽aggregate或foreign runtime重新带回来，必须失败。
- `A1(alpha) → B1(beta) → A2(alpha)`分片DAG仍正确报告subsystem环；不能因为分片无环就忽略。
- 窄合同误引用adapter、codec误引用physicalcore、领域误引用composition、生产误引用verification，分别有真实临时工程反例。
- 编译计划缺一个真实provider时独立编译红，aggregate混入provider不能被视为该边界修好。

迁移期先对已经完成的边界加真实保护；不提前把全图无环测试放进release后长期skip。B11完成时再把完整图无环变成最终release性质。若现行WHAT仍只要求迁移期报告而不要求release拒绝，则先按现行修订流程明确终态验收与持续保护的关系，不偷偷改门禁语义。

---

## 9. 每一刀的操作单

### 9.1 下刀前填写

```text
批次 / 子批：
基准HEAD与当前工作区差异：
适用WHAT与HOW：
要迁移的真实consumer（文件、模块、函数）：
当前宽依赖（类型/构造器/读取/写入）：
所有通向旧依赖的路径（含同片Surface、codec和helper）：
目标public contract及定义subsystem：
唯一adapter构造位置及资源拥有者：
本次保留的snapshot/revision/await语义：
本次保留的失败/unknown/cancel/fatal语义：
预期删除的旧入口与消费者：
关键独立编译根 / 签名impact范围 / 正式行为test：
当前未证明而不能宣称的事项：
```

不要求每刀新建文件；写入当前已有任务记录或对应HOW即可。本模板不是新的治理数据库。

### 9.2 一个完整接口迁移的机械顺序

1. 读现有`.fsi`与全部构造位置。对record/DU检查所有字段、private constructor、成员与扩展成员，不能只看type名。
2. 新增领域自有合同；新`.fsi`紧挨并先于`.fs`编译。新项目只声明实际provider，不拷贝旧项目全套refs。
3. 写领域实现使用新合同；保持业务分支和effect次序。必要适配不得把领域规则再实现一次。
4. 在唯一正确装配层创建adapter，调用者从此只收到capability。所有参数通路同批迁，禁止只改局部调用而留下宽重载。
5. 同步正式Surface：保持JS输入/输出和路径；只在真实边界做类型转换。若必须改公开产品行为，停止该变化并按规范处理，不以内部重构之名直接改。
6. 删除旧类型导出/过渡alias/废弃工厂；用值层搜索和库存确认全部消费者已迁。
7. 最后删refs、空项目、旧aggregate条目。删除文件前确认不是仍被合法测试import的生成路径来源。
8. 用第8节验证。失败先定位原始错误，不能增加一个无关宽引用“让编译器闭嘴”。

### 9.3 每批必须交付的证据

| 证据 | 必须包含 | 不能替代它的东西 |
|---|---|---|
| 知识迁移 | 旧consumer知道什么、新consumer不再知道什么、实际调用链 | “增加了一个port” |
| 图变化 | 前后HEAD/工作区、完整边集合、SCC成员、变动边见证 | 只报refs净减少 |
| 编译闭包 | 同口径的forward/reverse、真实新adapter和consumer全部计入 | 单边删边释放集的上界 |
| 独立编译 | 分片、输入fingerprint、实际命令与退出码 | aggregate绿、plan-only、materialize-only |
| 行为 | 正式Surface、测试名、旧错实现如何失败、新实现如何通过 | 源码token、注释或自写算法oracle |
| 交付范围 | 改动/删除/新增、未跑项、已知gap、commit/push状态 | “全面完成” |

闭包减少可以作为收益，但没有计时实验就不写“编译更快”。资源行为没有测到就不写“完全等价”。真实编译因环境阻塞与断言失败要分别记录。

### 9.4 可以并行的调查与必须串行的修改

| 可并行 | 必须串行 |
|---|---|
| 读不同consumer、核对不同package条款、只读闭包统计 | 修改共享public类型、全域Fact/Projection、同一adapter、aggregate |
| 为不同已独立接口列行为不变量 | 修改ToolRegistry/PluginHooks/HostSignalBootstrap |
| 独立审查某批最终diff | 同一Git index、dist、Fable cache的生成与消费 |
| 静态定位未来待迁路径 | 运行真实Host/e2e Long Stroke与修改其输入 |

不能以CPU空闲为理由强行开Worker；不能让多个Worker分别“修完自己编译”后把全局环留给下一轮。

---

## 10. 失败时怎么处理：不再来回试同一刀

### 10.1 新增ProjectReference后出现分片环

先还原闭合路径到实际类型与构造器。按以下顺序判断：

1. **合同与工厂混片：** 将消费者真正需要的合同独立，工厂上移。
2. **外层wrapper与领域模型混片：** wrapper进durable，纯模型留域。
3. **组合类型放在低层：** 拆低层证据与上层组合；禁止反向alias。
4. **纯Surface与全程序装配混片：** 按证明用途分片，保持有效JS入口。
5. **真实业务决策循环：** 找唯一拥有不变量的领域；另一端返回事实/纯decision，由较高工作流组合，不新建eventbus或递归service调用。

不能只把新增引用删回去并写“此路无收益”。上述五种均处理后仍不成立，记录具体符号和调用约束，修正这一切口；蓝图的“领域不依赖装配层”方向不变。

### 10.2 删一条引用后闭包不变

这只说明还有别的路径。列出全部残余路径；它可以是中间步骤，但不应作为本批结束。完整迁移设计必须同时计入新合同、adapter、真实consumer与同片Surface。

不再为保持进度发明第五版零符号启发式。仅清理已核实的残余声明，不把它作为主线。

### 10.3 闭包增加，但确实隔离了职责

先区分正常新增小合同与新的共享宽依赖。若一个无关领域新增端口就让大量adapter膨胀，这是结构未收口，不是简单更新预算。

对于必要小合同引起的暂时增长，记录同一批最终收口条件，按已获授权同步真实预算与证明。不能未经裁决改WHAT；不能为了过旧budget复制实现或隐藏依赖。

### 10.4 编译报namespace/type/module未定义

先辨别dead open与真实provider缺失。搜索值层builder、扩展成员、union case、全限定名和`.fsi`的`val`。`taskResult`等不能只用大写type扫描。

真实使用就找正确窄provider并声明；dead open删除后对拥有该source的分片实际编译。不要把aggregate通过当修复，也不要一律补回旧宽项目。

### 10.5 源码没有宽类型，分片仍然很大

检查同片Surface、`.fsi`公开面、adapter工厂、模型文件中的跨域组合、codec和其它传递provider。接口文本变窄与编译闭包变窄是两件事。把不同变化原因真正拆成shard后，再决定哪些引用可以删。

### 10.6 行为测试只能靠非法dist深导入

扩充现有合法production Surface并更新正式注册；不能因此放弃真实行为证明，也不能只留下函数名/注释匹配。不要把私有F#运行时对象直接暴露给JS测试绕过边界。

### 10.7 需要放宽观察、失败或持久化语义才能继续

停止这项语义变化，保存原始失败和对应WHAT冲突。可以继续与其无关、确已授权的独立工作；不能一边改变行为一边称为“等价重构”。不得自动把取消当成功、把unknown当confirmed、把同revision观察拆开。

### 10.8 发现别人的未提交改动或HEAD变化

停下修改共享路径，重新核对diff、HEAD与当前验证字节。只提交本任务确认的hunk；禁止全仓reset/clean/强推。无法可靠分离时保留文件并交接，不替别人清理。

### 10.9 回退规则

优先精确回退本批未提交hunk；已提交的本批修改用可审查的revert或前向修复。任何回退都先检查共享工作区，不能撤销他人输入。回退后的工程同样要验证，不以旧报告默认它仍正确。

---

## 11. 审查者最后要问的十个问题

1. 被迁移的消费者现在确实不认识旧类型/实现，还是只是从别处绕回去？
2. 新端口的类型所在shard是否仍携带旧聚合？
3. adapter有没有重新实现领域决策或复制物理实现？
4. capability是由真实装配入口注入，还是业务内部自己调大工厂？
5. 同snapshot/revision、await、取消与资源释放语义有没有改变？
6. Source、`.fsi`、真实JS消费者和aggregate是否全部迁完？
7. 本批宣称独立的每个关键分片是否各自实际编译，而非仅超集编译？
8. 结构测试是否仍依赖旧kind/名字/注释，行为测试是否调用真实生产实现？
9. SCC结果是否来自完整图，是否把新增的adapter和所有实际依赖算进去？
10. 最终diff是否只包含本任务，旧入口是否真实删除，未证明项是否如实保留？

任一项答不上来，本批最多算中间进展。不能用全量suite绿色盖掉具体边界缺陷。

---

## 12. 全部完成的验收清单

以下是B11的全部完成条件，不是“任选几项”。

- [ ] 完整声明subsystem图无非平凡SCC；shard图DAG；source/fsi唯一归属；aggregate并集精确对应。
- [ ] persistence内核不认识Journal aggregate、具体领域、Host与装配层；codec完整纯边界；merge不借physical core取得codec。
- [ ] 领域核心/合同无通往durable-composition、application-composition或Host实现的路径；provider/authority/context/chat等方向符合2.3节。
- [ ] 所有外层fact包装、跨projection组合和Journal实现只由durable-composition拥有；各域唯一保留自己的事实、状态与决策。
- [ ] 全工具注册、Host+Journal实例配对、插件/进程生命周期只由application-composition拥有；没有反向import与第二个应用运行时。
- [ ] `AgentJournalPortAdapter`大工厂、重复成功结算、死端口、宽pass-through、跨层alias和临时双入口均已清理。
- [ ] 现有合法集中持久化、唯一重放、跨域原子提交、event身份/载荷、unknown/cut/fatal、retention/CAS未改变。
- [ ] 正式行为证明覆盖live read、同revision视图、wait不丢唤醒、取消释放与资源唯一拥有；不以源码断言替代。
- [ ] 新边界的关键独立compile、真实consumer影响编译、相关package suite和完整format-build-test均对最终字节通过。
- [ ] 所有新增/删改证明有现行HOW落点；未证明项未被状态或数字抹去；GAP-033按完整合同关闭。
- [ ] 最终HEAD、工作区差异、结果、未跑项、commit/push状态清楚；无无关临时产物或运行数据变更。
- [ ] 本轮临时蓝图与AGENTS规则按退出要求清理，不变成永久平行治理。

即使图无环，只要领域还需要了解对方内部过程或adapter在复制业务规则，也不完成。反过来，某中间批次SCC没变但确实隔离了一个领域，记录真实进展，不推翻正确切口。

---

## 13. 可直接运行的只读调查命令

### 13.1 一次输出完整图、闭包、消费者和环见证

从仓库根运行。脚本使用现有库存，不重新解析fsproj，不扫描FCS，不改任何文件。需要保存快照时自行将stdout重定向到本次临时目录；不要把每次输出都提交进仓库。

```sh
node --input-type=module <<'NODE'
import { execFileSync } from 'node:child_process';
import { relative } from 'node:path';
import { buildSubsystemInventory } from './scripts/checks/subsystems.mjs';

const i = buildSubsystemInventory();
if (!i.ok) throw new Error(i.violations.join('\n'));
const projects = [...i.projects.values()];
const byPath = i.projects;
const normalize = p => relative(process.cwd(), p).replaceAll('\\', '/');
const reverse = new Map(projects.map(p => [p.projectPath, []]));
for (const p of projects) {
  for (const r of p.references) reverse.get(r).push(p.projectPath);
}
const closure = (root, next) => {
  const seen = new Set();
  const visit = p => {
    if (seen.has(p)) return;
    seen.add(p);
    for (const q of next(p)) visit(q);
  };
  visit(root);
  return seen;
};
const shardRows = projects.map(p => {
  const forward = closure(p.projectPath, q => byPath.get(q).references);
  const backward = closure(p.projectPath, q => reverse.get(q));
  backward.delete(p.projectPath);
  const sources = new Set([...forward].flatMap(q => byPath.get(q).implementationFiles));
  const signatures = new Set([...forward].flatMap(q => byPath.get(q).signatureFiles));
  return {
    shard: p.shardKey,
    project: p.projectRepoPath,
    ownSources: p.implementationFiles.map(normalize).sort(),
    directProviders: p.references.map(q => byPath.get(q).shardKey).sort(),
    directConsumers: reverse.get(p.projectPath).map(q => byPath.get(q).shardKey).sort(),
    forwardShards: [...forward].map(q => byPath.get(q).shardKey).sort(),
    forwardSourceCount: sources.size,
    forwardSignatureCount: signatures.size,
    forwardSources: [...sources].map(normalize).sort(),
    reverseShards: [...backward].map(q => byPath.get(q).shardKey).sort(),
  };
}).sort((a, b) => a.shard.localeCompare(b.shard));

const edgeWitness = new Map();
for (const p of projects) for (const r of p.references) {
  const q = byPath.get(r);
  if (p.subsystem === q.subsystem) continue;
  const key = `${p.subsystem}\0${q.subsystem}`;
  if (!edgeWitness.has(key)) edgeWitness.set(key, []);
  edgeWitness.get(key).push({
    consumer: p.shardKey, provider: q.shardKey,
    consumerProject: p.projectRepoPath, providerProject: q.projectRepoPath,
  });
}
const findOneCycle = component => {
  const allowed = new Set(component);
  const adjacency = new Map(component.map(s => [s, []]));
  for (const [a, b] of i.subsystemEdges) {
    if (allowed.has(a) && allowed.has(b)) adjacency.get(a).push(b);
  }
  const start = component[0];
  const queue = [[start]];
  const seen = new Set([start]);
  for (let index = 0; index < queue.length; index += 1) {
    const path = queue[index];
    for (const next of adjacency.get(path.at(-1))) {
      if (next === start && path.length > 1) {
        const cycle = [...path, start];
        return {
          cycle,
          witnesses: cycle.slice(0, -1).map((from, n) => ({
            from, to: cycle[n + 1],
            references: edgeWitness.get(`${from}\0${cycle[n + 1]}`),
          })),
        };
      }
      if (!seen.has(next)) { seen.add(next); queue.push([...path, next]); }
    }
  }
  throw new Error(`No cycle witness found for reported SCC: ${component.join(',')}`);
};
console.log(JSON.stringify({
  head: execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(),
  status: execFileSync('git', ['status', '--short'], { encoding: 'utf8' }),
  method: 'declared ProjectReference; complete subsystem graph; forward includes self; .fs and .fsi counted separately; no timing claim',
  counts: { subsystems: i.subsystemCount, shards: i.shardCount,
    sources: i.sourceCount, references: i.projectReferenceCount,
    crossReferences: i.crossSubsystemReferences, subsystemEdges: i.subsystemEdges.length },
  cyclicComponents: i.cyclicComponents,
  cycleWitnesses: i.cyclicComponents.map(findOneCycle),
  edges: [...edgeWitness].map(([key, references]) => ({
    pair: key.split('\0'), references,
  })).sort((a, b) => a.pair.join('/').localeCompare(b.pair.join('/'))),
  shards: shardRows,
}, null, 2));
NODE
```

“环见证”证明某条声明环存在，不证明所有这些边的源码知识都合法。对被选中的边仍要读具体`.fsi`、构造点和实现。`reverseShards`是声明反向闭包，不等于所有节点每次都必须重编。

### 13.2 找宽类型与工厂消费者

```sh
git grep --untracked -n -E \
  'AgentJournal|AgentProjectionSet|SessionAgentProjection|ProjectionSet|JournalHandle|AgentJournalPortAdapter|PluginRuntimeScope|ToolRuntimeScope' \
  -- src/Wanxiangshu

git grep --untracked -n -E \
  'AttemptExecutionProfile|buildAttemptExecutionProfile|createWithRules|RecordConfirmedSuccess|recordConfirmedSuccess' \
  -- src/Wanxiangshu requirements
```

命中包含注释和测试，必须分类，不按正则直接删。未命中也不是无依赖证明：还要检查alias、全限定名、计算表达式、extension、实际调用与声明闭包。此命令只是定位，不建立符号ACL。

### 13.3 最终完整图无环断言

迁移结束再使用此断言作完成判断。基准18节点SCC下它理应失败。

```sh
node --input-type=module <<'NODE'
import assert from 'node:assert/strict';
import { buildSubsystemInventory } from './scripts/checks/subsystems.mjs';
const i = buildSubsystemInventory();
assert.equal(i.ok, true, i.violations.join('\n'));
assert.equal(i.cyclicComponents.length, 0,
  `Remaining subsystem SCCs: ${JSON.stringify(i.cyclicComponents)}`);
console.log('Complete subsystem graph is acyclic; shard/source checks also passed.');
NODE
```

它不替代行为、独立编译或正式整体验收。也不把最终指标写成某个固定subsystem/shard数量。

---

## 14. 第一位执行者可直接领取的任务书

**任务：B00，不扩展到生产重构。**

**必须先读：** 根AGENTS；本文件1—5、8、10节；structured-workflow WHAT 011—016与HOW当前缺口；verification-system WHAT/HOW；DELEG-028/029及对应两组边界测试。

**可以修改：** `requirements/structured-workflow/HOW.md`、`requirements/verification-system/HOW.md`、`requirements/delegation/tests/delegation-compile-boundary.test.mjs`，以及为保持同一正式证明必需的原包HOW/现有测试。若具体修改确需改WHAT，先按现有requirement-system处理，不以本文件替代授权。

**禁止修改：** 生产F#、fsproj、运行数据、其它GAP、预算阈值、超时、整个AGENTS重写；禁止恢复legacy kind权威，禁止加新的全仓符号扫描器。

**输出：** 旧判据到新性质的逐项对应；去标签仍合法和伪装adapter仍非法的反例；明确独立compile与impact并集各证明什么；本批实际测试结果与剩余事项。

**通过后下一任务固定为B01。** 不再开一轮全仓零符号普查；B01行为证明收口后才进入B02存储机制拆分。后续执行者按批次接续，不从HOW历史流水任意另挑“下一刀”。

### 14.1 后续任务书的最小交接格式

```text
已完成至：Bxx.y（附HEAD，不只写“上一班”）
当前未完成：一个具体边界及仍存在的路径
下一提交：对应本蓝图批次与明确文件/接口
验证：命令、退出码、字节/HEAD、未跑项
共享接口与资源：负责人和不可并行项
工作区：本任务改动、他人改动、commit/push状态
```

### 14.2 本蓝图自身的交付范围

本文件是根据基准源码、工程库存、现行合同和已有测试内容编写的施工设计。这里没有宣称未来任何签名变更已通过Fable，也没有给出未经实施验证的SCC下降数字。

本次仅新增本文件，不修改生产代码或规范正文。它尚未成为被Git跟踪的构建语料时，不会被当前`loopDetectorRepositoryInputFiles`的`git ls-files --cached`读取；**一旦加入Git跟踪，Markdown会进入repository corpus，运行dist-backed证明前必须按正式流程重建。** 不可加入“生成文档”标记或移入排除目录绕过这一事实。

完成施工后，本文件随临时重构规则退出；长期必须保留的行为和边界性质归WHAT/HOW及正式测试，而不是永久靠这份施工蓝图维持。

### 14.3 本次文档交付已执行的检查

2026-09-12，在上述基准HEAD且只新增本文件的工作区：

| 检查 | 本次结果 |
|---|---|
| 当前subsystem实测 | 26 / 245 / 732 / 1877；完整最大SCC仍18；没有实施本蓝图中的解环 |
| `node scripts/check.mjs` | exit 0；现有静态检查通过，未关闭已有证明缺口 |
| subsystem / owner-project-boundaries / owner-impact-compile 三组测试 | 24 passed，0 failed；这是结构与planner测试，不是全仓Fable实际编译 |
| 文档shell代码块语法 | 6个shell块均通过`bash -n`；其中3个Node heredoc均通过Node语法检查 |
| 第13.1节图/闭包脚本 | 实际执行成功，输出全部245个分片；计数与官方gate一致；获得带分片见证的环 |
| 第13.3节最终无环断言 | 对当前18节点SCC按预期失败；证明没有把“当前OK”误写成无环 |
| 完整Fable / format-build-test / 产品行为回归 | 本次未执行；本次是文档交付，不宣称未来设计已编译或行为等价 |

静态检查仍报告`INSTITUTIONAL-LEARNING-007`的两条现有缺口：没有active executable test、没有对应HOW证明行。它们不在本次文档修改范围；不得因为检查exit 0就记为已证明。


