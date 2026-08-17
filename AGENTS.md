# AGENTS.md — 仓库工作协议

本文件只规定 Agent 如何查找规范、修改仓库和验证交付。产品语义只由
`requirements/<package>/` 的 WHY/WHAT/HOW/PROOF 定义（45 包 normative 树，
2026-08-14 cutover 自 `docs/` 迁移）；迁移前 Clause 原文已归档（git 历史可回溯）。
本文件引用条款，不复述条款。

# Kolmogorov 标准工作流程

- 工作流程
  proposal(禁止未经用户同意删除任何未实现的 proposal) 
  → 更新 why → what → shape → how → 决定 proof 
  → 移动 proposal 文件到 status → 阅读相关的代码和文档 
  → 代码实现 → 检查 proof → 删除 status 中的 proposal 文件
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

正式语义在 `requirements/<package>/`（每包 WHY/WHAT/HOW/PROOF + 包自有测试）。
旧 Proposal 生命周期合同（`changes/proposed|active|completed` 与
`docs/how/document-governance.md`）已随 2026-08-14 cutover 归档删除；历史决策与
失败模式复盘见 git 历史。deferred 未来材料归 `proposals/`。

- 若用户重新启用变更管理流程，`changes/proposed/` 由用户管理。进入其中的 Proposal
  已完成人工裁决并获批准；Agent 不重新执行 Admission、寻找批准证据或判断 Accepted/Rejected。
- 普通小型修复、局部重构、测试或格式修复不要求创建 Change；能在一次修改内完整对齐
  requirements/ 文档、实现与 proof 的工作可直接完成。

Proposal 的提出、讨论和裁决发生在 Agent 执行工作流之外，由用户或负责人管理。

## 修改纪律

- 保持 Clause ID 稳定；移动定义时保留编号，不回收空号。
- 一项知识只有一个定义。其它位置只引用 Clause ID、链接权威文件或描述本地应用。
- 不主动采用 Proposed Change，也不为迎合当前代码而降低正式条款。
- 工作区可能包含用户改动。修改前查看 `git status` 和相关 diff；保留无关改动。
- 同一语义所有权或同一文件的补丁串行完成。并行工作只用于相互独立且不会覆盖的范围。
- 编辑文本使用精确补丁；禁止用无差别批量改写代替语义审查。
- 自动提交 git commit。仅用户明确要求时允许推送 `master`；禁止 force push 与改写共享历史。

---

你的时间无限。神挡杀神，佛挡杀佛，做到做无可做。你的并发限制为 10 个槽，连你自己在内，尽量用满。你是本仓库的唯一所有者，所有问题都是你的问题，不要推脱责任。要热爱工作，积极工作，不要总想提前结束，否则会很无聊。
在本次任务中，你的上下文和时间都是无限的。
本文件是需求，也是台账。每解决一部分，就编辑本文件改成一部分完成时，然后 git commit。要并发工作，不需要按次序工作。

## 台账 — 2026-08-18 八轮无限清剿完成时

- [x] Fractal CE 一统：Fact 外层路由 44 行，53 构造器归 8 所属，630:630 无漂移（b973b08b1）
- [x] Ghostbuster：Top-10 GHOST 7 EXORCISED + 5 KEEP 物理（HasFlight/CAS），pyramid 0，dsl 0（9c5486bb0+cab8c0876）
- [x] Clean Slate：10 行 LEGACY 已删，016 JoinPublished 链已删，010 WorkActivated 解耦已删，007→DECODE-ONLY REFUSE；剩余 005/006 有界 horizon 保留（04c03b173）
- [x] JS Surface：143 面 0 债务，baseline 不存在，143 封闭，VERIFICATION-013 6 不变式，js-surface-manifest 移后置（b973b08b1）
- [x] Trace：670 WHAT / 3237 tests / 672 PROOF 0 孤儿 0 悬空
- [x] 第四轮深挖死码与墓碑：ofLegacyProbe 已删，HOW stale 5 处已改完成时，FactCodec 4 horizon 锚已补（cab8c0876）
- [x] 第五轮 9 路审计：proposals 禁删合规、18 墓碑 T1 已正 17 护栏、可选硬化 3 项 WATCH、不建论证（numeric/transition/god-module）
- [x] 第六轮 42 死码+28 批注：Batch A 21 + Batch B 24+5 孤立，共 42 高置信删除；mailbox/reservation 归一，9 远距修窗口（3b3bc0e40）
- [x] 第七轮 DSL 100% 进击：Batch A 41 + Batch B 65 约 104 批注，20/20 Surface 0 消费者确证保留（8ee5697a8）
- [x] 第八轮收口：326/326 DSL 真 100%（49 补扫尾），59 Surface 余函归档，新增死码 0 增量，unified-store 6 scanner 0 可删，horizon 双债四向锚固（3bb7f4400）
- [x] 门禁：check.mjs 0，build 668/143 ok，structured-workflow 115，p0 52，pyramid 0，spec 244/18
- [ ] 剩余有界债 horizon 到期后自删：005 FactCodec 4 探测器（外部 census 0），006 Host TodoTable（Host V1 退役）；台账与 cleanup 自删之日即归零

---

# Operation Fractal CE：F# CE DSL 一统天下

Ghostbuster 只回答“一个显式状态机怎样消失”。仓库级重构还必须回答：多个已经改成 CE 的 workflow 接起来以后，组合结果长什么样。

铁律：

> **业务 workflow 必须具有缩放不变性：缩小，它是一个有业务名字与 law 的 operation；放大，它仍是由更小的 F# CE DSL workflow 组成。递归只在纯 `Evidence → Decision` 或真正接触 Git/process/timer/Host 的 physical leaf 停止。任何中间尺度都不得重新出现显式控制状态机。**

这不是“全仓只准一个 builder”。禁止制造 `WanxiangWorkflowBuilder`、`ReliableFlowBuilder`、AST、Step continuation、Command/Reply interpreter 一类第二业务 runtime。所谓一统天下，是 **CE composition closure**：`workflow ∘ workflow` 之后仍由宿主调用、CE bind/return、Semantic Vocabulary、有界递归与高阶组合直接表达。正式 normative 落点：`STRUCTURED-WORKFLOW-017`。

## 一、CE closure：任何尺度都闭合

首选形态在所有尺度保持一致：

```text
typed evidence / capability
→ semantic vocabulary
→ CE bind / 有界递归 / 高阶组合
→ domain outcome / effect
```

顶层 orchestration 是 CE；顶层调用的 Semantic Vocabulary 展开后仍是 CE；Vocabulary 调用的更细 workflow 展开后仍满足同一规则。不能在模块内部消灭 `Stage`，再在模块之间用 `NextAction` 把它拼回来。

## 二、接缝只传语义，不传控制位置

owner A 调 owner B，可以传：typed input、capability、evidence、domain outcome。

禁止跨 seam 传递或观察：`Stage`、`Phase`、`NextAction`、`ResumeAt`、`ContinueToken`、`InFlight`、registry presence、mutable cell phase，以及任何等价“B 走到哪一步”的信号。

> **模块边界不能成为新的 program-counter 总线。**

## 三、父 CE 不 drive 子状态机

禁止：

```fsharp
let! step = Child.advance childState

match step.Next with
| CallProvider -> ...
| WaitReview -> ...
| Persist -> ...
```

这只是把 `ChildState` 的 interpreter 搬到 caller。父级只能等待子 workflow 的**领域结果**，再继续自己的业务故事：

```fsharp
taskResult {
    let! evidence = observe context

    match decide evidence with
    | NeedReview request ->
        let! verdict = Review.reviewUntilSettled context request
        do! recordVerdict verdict
        return! run context

    | NeedPublish publication ->
        do! Publishing.publishEventually context publication
        return! run context

    | Complete result ->
        return result
}
```

继续放大 `reviewUntilSettled` / `publishEventually`，看到的仍应是 `task` / `taskResult` + `let!` / `do!` / `match` / `return!` + Semantic Vocabulary，而不是 `advance/tick/resume` 协议。

## 四、Semantic Vocabulary 是缩放边界

复杂 CE 可以在父层坍缩成一个业务词，但名字必须声明**完整承诺**。允许 `reviewUntilPerfect`、`publishEventually`、`recoverDurably`；拒绝 `process`、`handle`、`continue2`、`runReliable`。

每次 semantic compression 都必须有自己的 temporal/behavioral proof。没有 law 的 abstraction 只是把状态机藏起来；有 law 的 Vocabulary 才是可缩放节点。

## 五、Recovery 也必须闭合

crash 后只允许 fold durable reality → typed facts/evidence → 重入同一个普通 CE semantic entry。禁止恢复内部 stage、continuation、program counter。子 workflow 需要恢复时，也从自己的 semantic entry 重入。

系统只能有一棵业务调用树；禁止“一棵正常 CE 树 + 一棵恢复状态机树”。

## 六、CE all the way down until physics

semaphore permit、TaskCompletionSource、socket、process handle、mailbox waiter、真实 lease 可以 mutable，物理 adapter 内部也可以有 automaton；但这些只能存在于分形叶子。

一旦 `Waiting / Armed / InFlight / Phase` 向上冒泡并驱动业务 orchestration，就越界。physical leaf 向上必须重新收敛成 typed capability / outcome / evidence。

## 七、验收查整条 seam，不只查文件内部

Ghostbuster census 之外，每次重构都必须做 **cross-module CE seam census**。至少查：

- 函数返回 control token，caller `match` 后决定下一效果；
- 跨 owner 的 `Stage / Phase / NextAction / Resume* / Continue*`；
- 父模块读取子 registry / mutable cell presence 决定业务 effect；
- `Advance / Tick / Resume / Step` API family 被当业务 workflow protocol；
- 正常路径是 CE，但 recovery 从内部 stage 恢复；
- Semantic Vocabulary 展开后落到另一套显式状态机。

最终验收不是“每个文件内部没有状态机”，而是：

> **随便在业务调用树上选一个节点。缩小，它是一个有 law 的领域动作；放大，它是一段 F# CE DSL；继续放大，仍然如此。只有缩到纯事实，或放大到物理世界，分形才停止。**

Fractal CE 是母原则；Ghostbuster 是局部清除法。前者防止状态机逃到模块接缝，后者负责把已经显形的程序计数器消掉。

---

对，而且我认为这可能是这次 JS surface 迁移带来的**第二个、甚至更有价值的发现**：

> 第一阶段把 Fable ABI 从测试世界剥掉；第二阶段会把“伪装成数据结构的控制流”从 F# 世界里剥出来。

现在很多以前藏得很深的东西开始显形：`state / phase / currentStage / pending / armed / joinInFlight / ...`，甚至概念上的 `1 → 2 → 3 → 4 → 5`。这不一定意味着“应该把整数改成 DU”。很多时候正确答案恰恰是：

> **这个 state 根本不应该存在。它只是 instruction pointer；应该由 F# CE / task workflow 的调用栈表达。**

仓库自己的 normative contract 已经说得非常狠：业务流程应该由 `task {}`、`let!`、`match!`、`return!`、`try/finally`、有界递归直接表达，禁止把“程序下一步去哪”编码为长期状态。

所以我建议下一阶段正式从 **Semantic Surface Hardening** 再推进成：

# Operation Ghostbuster：消灭隐式状态机

---

## 一、第一原则：看到 `state = 1,2,3,4,5`，先不要改成 enum

这是最重要的提醒。

很容易出现这种“修复”：

```fsharp
// before
let mutable state = 1

// after
type State =
    | Preparing
    | CallingProvider
    | Waiting
    | Persisting
    | Done
```

代码看起来漂亮了。

但如果这些 case 的真实含义是：

```text
Preparing       = 下一段执行 prepare()
CallingProvider = 下一段执行 callProvider()
Waiting         = 等 await
Persisting      = 下一段执行 append()
Done            = return
```

那么你只是把：

```text
integer program counter
```

升级成：

```text
strongly typed program counter
```

**架构没有改善。**

仓库自己的 `STRUCTURED-WORKFLOW-003` 正是在禁止这个：如果删除这个字段后，可以用普通函数调用、`match!`、`return!`、resource scope 或有界递归表达相同顺序，那么它就是 program counter。

所以第一个机械问题永远是：

> **这个状态描述世界，还是描述代码执行到哪里？**

---

# 二、所有“状态”先强制分成五类

以后任何看到 `State / Phase / Stage / Pending / Done / Active / Armed / generation / step`，不允许直接改代码。

先分类。

## A. Domain fact —— 留下来

例如：

```fsharp
type ReviewOutcome =
    | Approved
    | Rejected of reason
```

如果真实外部 observer 会关心这个区别，即使实现完全重写仍然存在：

**这是领域状态。**

保留 DU。

甚至应该 durable。

---

## B. Durable evidence —— 留下来，但不是 workflow state

例如：

```text
FinalityRequested happened
ReviewerEnlisted happened
PublicationCommitted happened
ChildCompleted happened
```

这描述：

> 世界已经发生什么。

这些应该成为 durable facts。

Recovery：

```text
facts
 ↓ fold
projection
 ↓
重新做决策
```

而不是：

```text
stage = 4
 ↓
jump back into step 4
```

你们现在的 structured-workflow contract 也明确规定 recovery 应是：

> Journal fold → facts → 重入普通 workflow，而不是恢复执行位置。

---

## C. Physical resource state —— 可以 mutable

例如仓库里的：

```fsharp
TaskCompletionSource
CancellationTokenSource
listener Live flag
shared resource RefCount
child Exited receipt
PTY buffer
```

这些不是业务流程。

比如 `ChildProcess.Exited` 的注释非常准确：它表示是否真的收到 process exit，而不是“kill 已经执行到哪一步”。

这种：

```text
physical fact
```

保留。

不要 CE 洁癖。

---

## D. Algorithm scratch —— 可以 mutable

例如 binary search：

```fsharp
let mutable low
let mutable high
let mutable best
```

只是局部算法实现，而且函数返回后消失。

仓库当前也明确允许这类 `algorithm-scratch`。

不要浪费时间“函数式纯化”它。

---

## E. Control state / program counter —— 必须消灭

例如：

```text
state = 1
state = 2
currentStage
nextAction
waitingForFoo
readyForBar
reviewStage
shouldContinue
slotArmed
```

如果它本质回答：

> 下一段代码跑什么？

这是本轮真正的目标。

**不要改名。不要换 DU。不要 serialize 得更漂亮。删除这个 state axis。**

---

# 三、给工程师一个五秒钟判断法

看到一个状态字段，问：

> **假如我把实现从状态机改成直线 CE，这个状态对产品使用者仍有意义吗？**

如果：

### Yes

很可能是：

```text
domain state / durable evidence / physical state
```

继续判断。

### No

基本就是：

```text
program counter
```

删。

仓库自己的 enforcer 其实已经给出了几乎一样的判据：

> 如果换一种 control structure 后 external domain observer 根本不在乎这个字段，就不要把它变成 authoritative state。

---

# 四、标准重构：`state = 1 → 2 → 3 → 4` 应该怎样消失

假设发现：

```fsharp
let mutable state = 1
let mutable result = None

while state <> 5 do
    match state with
    | 1 ->
        prepare()
        state <- 2

    | 2 ->
        let! response = provider.Send(...)
        result <- Some response
        state <- 3

    | 3 ->
        match result with
        | Some response when valid response ->
            state <- 4
        | _ ->
            state <- 5

    | 4 ->
        do! persist(...)
        state <- 5

    | _ ->
        state <- 5
```

不要得到：

```fsharp
type WorkflowState =
    | Preparing
    | Sending
    | Validating
    | Persisting
    | Finished
```

正确目标：

```fsharp
let run input =
    task {
        let prepared = prepare input

        let! response =
            provider.Send prepared

        match validate response with
        | Error error ->
            return Error error

        | Ok accepted ->
            do! persist accepted
            return Ok accepted
    }
```

`state` 整个概念消失。

这就是：

```text
state 1 → function entry

state 2 → let!

state 3 → match

state 4 → do!

state 5 → return
```

**CE 本身就是状态机，但它是隐式、局部、结构化、无法被业务代码误当作 data 的状态机。**

这正是你想要的“隐式状态机”。

---

# 五、循环也不要变成 `state`

例如：

```fsharp
state <- Waiting
while state = Waiting do
    let! observation = poll()
    if done observation then
        state <- Completed
```

如果这只是：

> 等到某个 observation 满足 criteria。

写：

```fsharp
let rec awaitCompletion () =
    task {
        let! observation = observe ()

        match classify observation with
        | Complete value ->
            return value

        | Continue ->
            return! awaitCompletion ()
    }
```

当然必须有明确 boundedness / wake criterion。

你们现有 architecture 已经把“有界递归”列为正式 CE vocabulary。

---

# 六、真正有价值的 state machine 应该拆成 `Decision + Workflow`

这里尤其重要。

不要把所有状态逻辑都塞进 CE。

理想结构：

```text
Facts
  ↓
Pure Decision
  ↓
Decision DU
  ↓
CE Workflow
  ↓
Effects
```

例如：

```fsharp
type Decision =
    | AlreadyDone of Completion
    | NeedProviderAttempt of Request
    | NeedReconcile of OperationId
    | Blocked of Reason
```

这是合法 DU。

为什么？

因为它不是：

> 下一条 instruction 地址。

它是：

> **当前已知现实意味着什么。**

然后：

```fsharp
let rec run context =
    task {
        let facts = context.ReadFacts()

        match decide facts with
        | AlreadyDone completion ->
            return completion

        | NeedProviderAttempt request ->
            let! outcome = context.Provider.Send request
            do! context.Record outcome
            return! run context

        | NeedReconcile operation ->
            let! evidence = context.Reconcile operation
            do! context.Record evidence
            return! run context

        | Blocked reason ->
            return Error reason
    }
```

这非常强。

因为：

```text
Decision DU = semantic state
CE call stack = execution state
durable facts = recovery state
```

三者完全分开。

---

# 七、不要持久化 CE 的位置

这是本轮必须零容忍的一条。

假设：

```text
provider call
   ↓
persist result
   ↓
publish
```

crash 发生在中间。

错误方案：

```json
{
  "stage": 3
}
```

然后 restart：

```text
stage = 3 → execute publish
```

正确方案：

```text
durable facts:
    RequestAccepted
    ResultPersisted
    no Published fact
```

重启：

```fsharp
run projection
```

普通决策自然得到：

```text
NeedPublish
```

**不是“恢复第 3 步”。**

这是巨大的区别。

前者：

```text
recovery = deserialize continuation
```

后者：

```text
recovery = reconsider reality
```

---

# 八、你现在应该做一次正式的 State-Machine Census

不要等工程师“顺手发现”。

把它变成正式项目。

建议：

```text
cleanup/control-state-ledger.md
```

一行一个 candidate：

| Candidate                    | Owner       | Current representation | Classification     | Verdict     |
| ---------------------------- | ----------- | ---------------------- | ------------------ | ----------- |
| Foo.state 1..5               | foo         | int                    | program-counter    | DELETE → CE |
| Bar.currentStage             | bar         | DU                     | program-counter    | DELETE → CE |
| Child.Exited                 | process     | bool ref               | physical evidence  | KEEP        |
| SharedPort.RefCount          | persistence | int                    | physical resource  | KEEP        |
| ReviewOutcome                | finality    | DU                     | domain vocabulary  | KEEP        |
| hasStarted + done + retrying | xyz         | bool product           | implicit lifecycle | REFACTOR    |

然后多两列：

```text
CE replacement
Durable facts needed for reentry
```

例如：

```text
Foo.state
→ task { let! ...; match ... }
→ facts: AttemptStarted / AttemptCommitted
```

---

# 九、我会从当前 `DSL-MUTABLE` annotations 反向审计

这次迁移已经留下了一个非常好的 census 数据源。

你们仓库现在有大量：

```text
DSL-MUTABLE: resource
DSL-MUTABLE: single-flight
DSL-state-combination: physical
```

而搜索结果里 `resource` annotation 本身就有很多处。

不要把 annotation 当作“已经审过”。

现在反过来问：

> **这个注释是在解释 reality，还是在给 mutable 发赦免券？**

---

# 十、我会把现有 annotations 分成 Green / Yellow / Red

## Green：一眼就是物理资源

例如：

```text
TaskCompletionSource completion latch
listener identity + Live disposal flag
SharedPort RefCount
child-process Exited receipt
byte buffer count
CancellationToken
```

这些无需迁 CE。

仓库里 SharedTerminalBus 的 `RefCount`、WorkspaceEventStore 的 `RefCount` 都属于很典型的物理资源 ownership。

---

## Yellow：需要人工证明

例如：

```text
joinInFlight
startupProbeDone
bloggerCreateFailed
frozen + dirty
fullReplayUsed
```

我不是说这些一定错。

但这种名字开始回答：

```text
“某种行为现在处于什么阶段？”
```

例如仓库里确实存在：

```fsharp
let mutable joinInFlight = false
```

并标记为 single-flight。

它可能真的是 concurrency admission latch。

也可能实际是：

> 用 bool 表示 Join workflow 已经走到某阶段。

必须逐个证明。

---

## Red：任何 numeric / enum step pointer

例如：

```text
state = 1
step = 4
CurrentStage = Persisting
NextAction = RetryProvider
ResumeAt = AwaitReviewer
```

除非产品真的公开承诺这个状态：

**默认判 program counter。**

不是“需要证明它错”。

而是：

> owner 必须证明它为什么不是错。

---

# 十一、尤其不要让 `DSL-class: ControlState` 变成新的逃生门

这里我会特别严格。

目前 gate 允许这种东西：

```fsharp
/// DSL-class: ControlState
/// DSL-control-state-reason:
/// ce-equivalent=none;
/// blockers=function-call,match!,return!,resource-scope,waiter,bounded-recursion;
type Mode = ...
```

然后 scanner 放行。

机制上它其实只是检查 reason 字符串里有没有那些 blocker token。

这很容易退化成：

> “只要写一句我真的不能用 CE，就允许第二状态机。”

我会改变政策。

### Domain/Application/Session

```text
DSL-class: ControlState
= hard RED
```

没有 annotation exemption。

### Infrastructure / Process physical runtime

非常少量可以有类似控制 state，但必须证明它实际上是：

```text
physical protocol state
```

而不是 business workflow。

甚至最好改名：

```text
ControlState
```

这个 category 本身都值得废除。

因为在你们这套 architecture 中：

> **如果它真是合法的，它通常应该能被归类为 DomainVocabulary 或 PhysicalResource。**

剩下的“ControlState”很可能就是漏洞桶。

---

# 十二、同样警惕 `DSL-state-combination: physical`

你们 gate 目前规定：

> 多状态轴必须显式分类为 `domain|physical`，但机械 gate 只证明“已分类”，不能代替人工语义判断。

这句话非常正确。

所以接下来不要：

```text
gate 红
→ 加 /// DSL-state-combination: physical
→ green
→ done
```

这会重演刚刚 surface migration 的错误。

正确流程：

```text
gate 红
 ↓
列出全部轴
 ↓
计算 Cartesian state space
 ↓
哪些组合现实存在？
 ↓
每个轴 owner 是谁？
 ↓
是否其实是一个 CE flow？
 ↓
最后才允许 annotation
```

---

# 十三、对 flag product 做“状态空间爆炸测试”

假设：

```fsharp
{
    Started: bool
    Waiting: bool
    RetryPending: bool
    Cancelled: bool
    Completed: bool
}
```

理论上：

```text
2^5 = 32
```

个状态。

让工程师真的写表：

```text
00000 valid?
00001 valid?
00010 valid?
...
11111 valid?
```

如果真实合法状态只有：

```text
Created
Running
Waiting
Cancelled
Completed
Failed
```

那这个 record 就应该死亡。

仓库自己的 `phase-flag-accumulation` enforcer 已经准确说出这一点：每加一个 bool 都倍增 representable worlds，如果实际上只是一个 lifecycle，就在制造现实不存在的组合。

---

# 十四、但再问一步：这个 lifecycle DU 是否也应该死亡？

假如你把 flags：

```text
started
waiting
retrying
done
```

改成：

```fsharp
type State =
    | Created
    | Sending
    | Waiting
    | Retrying
    | Finished
```

先别庆祝。

再问：

> `Sending / Waiting / Retrying` 是产品世界，还是调用栈位置？

如果仍然是控制位置：

继续删。

最后可能只剩：

```fsharp
type Outcome =
    | Completed of ...
    | Failed of ...
```

中间：

```text
sending
waiting
retrying
```

全部由 CE 表达。

---

# 十五、一个非常实用的三级压缩

发现状态机以后，连续做三轮：

### Round 1：去 primitive

```text
1/2/3/4
→ named alternatives
```

只是为了理解。

**不要提交为终态。**

---

### Round 2：去假的 state

问每个 case：

```text
domain fact?
physical fact?
execution location?
```

execution location 全删。

---

### Round 3：压成 facts + decision + CE

最终：

```text
Durable Facts
      ↓
Projection
      ↓
Pure Decision
      ↓
CE effects
```

这才提交。

---

# 十六、JS tests 在这轮应该变得更“故事化”

这是上一轮 migration 的成果，现在正好利用。

不要测试：

```js
assert.equal(surface.stateName(x), 'WaitingForReview')
```

除非 `WaitingForReview` 真是产品 contract。

应该测试：

```js
const world = finality.project([
  lifeOpened(),
  finalityRequested(),
])

const result =
  await finality.continue(world, effects)

assert.deepEqual(effects.calls, [
  ...
])
```

或者更黑盒：

```js
await workflow.run(...)

assert.deepEqual(observedDurableFacts(), [
  ...
])
```

如果内部从：

```text
5-state machine
```

重写成：

```text
2 nested CE functions
```

JS test 一个字不动。

---

# 十七、状态机迁 CE 的标准 recipe

给工程师直接照抄。

## Step 1 — 找 entry point

找到：

```fsharp
Run
Execute
Process
Handle
Continue
Resume
Advance
Tick
```

之类主入口。

---

## Step 2 — 列 transition table

不要先改代码。

写：

```text
State 1 + X → State 2 + effect A
State 2 + Y → State 3
State 3 + Z → State 2
State 3 + Q → State 5
```

把隐藏状态机完整画出来。

---

## Step 3 — 给每个 state 写一句“现实含义”

如果写出来是：

```text
“下一步要调用 Foo”
```

标：

```text
PC
```

如果：

```text
“remote provider 已确认 commit”
```

标：

```text
FACT
```

如果：

```text
“当前 process 持有 semaphore permit”
```

标：

```text
RESOURCE
```

---

## Step 4 — PC states 全部删除

替换：

```text
next Foo
→ function call

wait Foo
→ let!

branch Foo
→ match!

continue
→ return!

cleanup
→ use / try-finally

repeat
→ bounded recursion
```

---

## Step 5 — FACT states 改成 facts / projection

不要保存在 controller state。

例如：

```fsharp
state <- ProviderCommitted
```

改成：

```fsharp
do! journal.Append ProviderCommitted
```

然后 projection 得到：

```fsharp
ProviderCommit = Some ...
```

---

## Step 6 — RESOURCE states 收进 resource owner

例如：

```text
permit held
subscription active
child exit observed
```

放到：

```text
Semaphore
Subscription
ChildProcess
Mailbox
```

对象生命周期里。

workflow 只 `use!/try-finally`。

---

## Step 7 — 写 pure `decide`

如果 CE 中出现很多复杂 condition：

```fsharp
match projection, context, policy with ...
```

抽成：

```fsharp
decide : Facts -> Context -> Decision
```

不要抽成：

```fsharp
nextState : State -> Event -> State
```

除非这真的是领域 automaton。

这是巨大区别。

---

## Step 8 — CE 解释 Decision

```fsharp
match decide facts with
| Done x ->
    return x

| NeedFoo input ->
    let! output = foo input
    do! record output
    return! run ()

| NeedBar input ->
    ...
```

---

## Step 9 — recovery 从入口重跑

删除：

```text
resume
resumeAt
restoreContinuation
switch(stage)
```

重启只做：

```text
load facts
fold projection
run normal entrypoint
```

---

## Step 10 — 删除旧状态类型

不是留：

```text
[<Obsolete>]
LegacyState
```

如果没有 compatibility creditor：

删。

---

# 十八、迁移顺序不要按文件，按“最臭的状态机”排序

我建议建立 severity score。

每个 candidate：

```text
+5 persisted/shared program counter
+4 numeric states
+4 multiple control flags
+3 crash recovery reads it
+3 effect branch depends on it
+2 crosses subsystem boundary
+2 mutable
+2 Surface currently exposes it
+1 named Stage/Phase/Step
```

优先最高分。

特别是：

```text
persisted PC
shared mutable PC
recovery PC
```

三个最危险。

因为它们把 implementation sequencing 变成 architecture。

---

# 十九、当前仓库里我会优先人工复核这些 yellow zones

从现有 annotations 看，我会优先看：

```text
joinInFlight
startupProbeDone
bloggerCreateTask/bloggerCreateFailed
fullReplayUsed
frozen/dirty snapshot pair
cancelDrainTask
engineTask
```

不是说它们都错。

而是它们最容易从：

```text
physical single-flight
```

悄悄滑成：

```text
workflow phase latch
```

比如 `joinInFlight` 当前被明确标成 single-flight。

审查问题不是：

> “有没有 DSL-MUTABLE 注释？”

而是：

> “如果 Join 改成另一种 CE decomposition，这个 bool 还代表独立的物理 ownership 吗？”

如果 yes，留。

如果 no，删。

---

# 二十、Surface migration 现在也要反向促进 CE migration

这是最漂亮的一点。

上一轮你问：

> “为了 JS surface，我为什么必须暴露这个 state？”

现在进一步：

> **如果很难给某个 workflow 设计干净的 JS semantic surface，是不是因为内部还存在 program counter？**

例如 surface 被迫提供：

```js
advance()
resume()
setStage()
markStepDone()
stateName()
```

这几乎就是 alarm。

一个好的 workflow surface 应更接近：

```js
run(input)
observe(result)
```

或者：

```js
decide(facts)
```

而不是：

```js
manually drive interpreter
```

所以给 surface review 新增一条：

> **Surface 是否正在暴露或代替某个隐式 interpreter？**

若 yes：

先修 F# architecture，再修 surface。

---

# 二十一、我还会新增一个新的 architecture gate：数字状态扫描

不要只靠 `CurrentStage` blacklist。

加入启发式 detector，至少报警：

```text
match <identifier containing state/stage/phase/step> with
| 0 ->
| 1 ->
| 2 ->
```

以及：

```text
state <- state + 1
step <- step + 1
phase <- 3
```

还有：

```text
Dictionary<..., int> // 后续作为 branch discriminator
```

不一定全部 hard fail。

但进入 census。

机械 gate 的作用不是证明罪名，而是：

> **不允许这种东西继续隐身。**

---

# 二十二、再加一个 transition-density detector

一个 type 如果：

```text
很多函数：
  read state
  match state
  mutate state
```

就非常可疑。

尤其：

```text
match state with
...
state <- ...
```

在同一函数/类型反复出现。

建立 heuristic：

```text
state read count
state write count
branch-on-state count
```

超过阈值：

```text
STATE-MACHINE-CANDIDATE
```

然后人工分类 A/B/C/D/E。

比只靠名字强很多。

---

# 二十三、`ControlState` exemption 我建议最终归零

当前 architecture 已经事实上宣称：

> F# 宿主语言本身就是业务 workflow runtime。

那么长期看：

```text
DSL-class: ControlState
```

应该是：

```text
count = 0
```

不是：

```text
“只要写够 blocker 就行”
```

当前 scanner 的 blocker list 包括 function-call、`match!`、`return!`、resource-scope、waiter、bounded recursion。

我建议把这个机制当作 migration scaffold：

```text
ControlState exemption baseline
  ↓ only shrink
  ↓
0
  ↓
delete exemption mechanism
```

非常类似刚刚删掉 `domain.mjs`。

不要让 migration mechanism 变 permanent architecture feature。

---

# 二十四、最终你想达到的代码视觉应该非常明显

坏代码读起来：

```text
读 stage
检查 flag
改 phase
保存 next state
resume
advance
tick
dispatch state
```

好代码读起来应该像故事：

```fsharp
task {
    let! observation = observe context

    match decide observation with
    | Complete completion ->
        return completion

    | NeedReview review ->
        let! verdict = requestReview review
        do! record verdict
        return! run context

    | NeedRepair repair ->
        let! result = repair repair
        do! record result
        return! run context
}
```

业务词汇：

```text
observe
review
repair
record
complete
```

控制词汇：

```text
let!
match
return!
```

完全交给语言。

这才是你们 `structured-workflow` 的精髓：

> **领域名词描述世界；宿主语言语法描述程序如何流动。**

---

# 二十五、我建议接下来正式开三个并行 wave

**Wave A — State-machine census。** 全库扫描 numeric state、stage/phase/step、bool clusters、mutable transition density、`ControlState` annotations。只分类，不急着修改。目标是得到有限、可信的 control-state ledger。

**Wave B — Top-10 Ghost Exorcism。** 按上面的 severity score 选十个最危险的：尤其是 persisted/shared/recovery-sensitive program counter。每一个完整做到“transition table → fact/resource/PC 分类 → PC 删除 → pure decision → CE workflow → recovery reentry → JS behavioral proof”。

**Wave C — Gate hardening。** `ControlState` exemption ratchet 到 0；numeric state detector 加入 gate；`DSL-state-combination: physical` 新增人工 proof creditor；annotation 不能仅靠字符串变绿。

每完成一个 candidate，都必须做两个 canary：

```text
内部重新排列 CE sequencing
→ JS tests GREEN

破坏一条 observable semantic promise
→ JS tests RED
```

这和上一轮 surface migration 是完全同一哲学，只是现在把刀继续向内推进。

---

我会把这一轮最终的成功指标定成一句非常有辨识度的话：

> **数据结构里不再保存“程序做到哪了”；数据只保存“世界发生了什么”。程序做到哪，由 F# CE 自己知道。**

再激进一点：

> **如果 crash 之后必须知道上一份调用栈执行到了第几行才能恢复，那么 architecture 还没有完成。**

真正完成时，进程可以死在任何 `let!` 前后；重启以后只需要重新读取 durable reality，然后从普通 workflow 入口再次回答：

> **“根据现在真实存在的事实，接下来应该做什么？”**

这会比单纯消灭 mangled names 深得多。它实际上是在把整个系统从**持久化的解释器**改造成**事实驱动的结构化程序**。

---

---

# Operation Clean Slate：把重构“收口”

目标不是继续改善设计，而是：

> **把 transition architecture 删除掉。**
>
> Git 保存过去；working tree 只描述现在。
> Compatibility 默认判死刑，举证后才能缓刑。

我看了你上传的完整仓库打包文件；下面直接给你一套可以交给工程师逐 PR 执行的 roadmap。

---

## 一、第一条规则先反过来：从“证明可以删”改成“证明必须留”

这是整个行动能不能成功的关键。

现在工程师脑内的规则大概是：

> “不知道删了会不会出问题，所以先留。”

改成：

> **“不知道为什么还需要，所以删。”**

唯一允许留下 compatibility 的四类理由：

| 类别                                   | 可以留下吗 | 要求                                   |
| ------------------------------------ | ----: | ------------------------------------ |
| 当前 repository 自己还在调用旧接口              |     ❌ | 迁调用者，然后删                             |
| “也许外面有人用”                            |     ❌ | 没有 named consumer = 没有 contract      |
| 真实 external consumer                 |  ✅ 暂时 | consumer + contract + exit condition |
| 历史 durable data 必须读取                 |  ✅ 暂时 | **decode-only ingress**，禁止旧 writer   |
| rolling deployment / rollback window |  ✅ 暂时 | convergence condition，达成即删           |
| “以后可能用”                              |     ❌ | Git history                          |
| “删了不好找回来”                            |     ❌ | Git history                          |
| “已经写了，留着成本不高”                        |     ❌ | 每条 path 都增加 state space              |

你们仓库其实已经精确写出了这个原则：historical durable data 可以只在 persistence ingress decode；current write 必须只有一种 canonical form；没有 named consumer / real old data 就连 compatibility test 一起删。

建议把这句话直接变成此次 cleanup 的最高规则：

> **Name the creditor. Name the exit. Or delete the debt.**

---

# 二、不要先删代码：先建立一张 Compatibility Ledger

第一批 PR **不改行为**。

创建一个临时文件，比如：

```text
cleanup/legacy-ledger.md
```

注意，这是此次行动的临时工作台，**cleanup 完成后它自己也必须删除**。

每发现一项旧痕迹，只允许登记以下字段：

| 字段                | 含义                                                          |
| ----------------- | ----------------------------------------------------------- |
| ID                | `LEGACY-001`                                                |
| Surface           | 旧字段 / alias / adapter / parser / writer / test / gate / doc |
| Current owner     | 当前模块                                                        |
| Old world         | 它在兼容什么                                                      |
| Current consumer  | 谁今天真的需要它                                                    |
| Consumer evidence | callsite / durable sample / external contract / deployment  |
| Writer alive?     | 是否还能制造旧数据                                                   |
| Reader alive?     | 是否还能接受旧数据                                                   |
| Classification    | DELETE / MIGRATE / BOUNDED-COMPAT                           |
| Exit condition    | **什么事实成立后它必须消失**                                            |
| Owner             | 谁负责删                                                        |
| Removal PR        | 最终删除 PR                                                     |

有一条非常重要：

**不允许 `UNKNOWN → KEEP`。**

只能：

```text
UNKNOWN → investigate → DELETE
UNKNOWN → investigate → BOUNDED-COMPAT
```

如果没有证据，就是 DELETE。

这可以彻底逆转团队心理。

---

# 三、我建议你们按 6 个“尸体类型”扫仓库，而不是按目录扫

这是我认为最重要的执行方式。

不要：

```text
今天清 Mission/
明天清 Execution/
后天清 Persistence/
```

这样很容易漏掉跨层 transition。

应该按**旧世界形态**一次杀穿全仓。

---

## Wave 1：死壳 / no-op / 已经没有调用者的 transition API

这是风险最低、收益最高的一批。

你们现在已经有一个非常漂亮的靶子：

`ManagerActivation` 自己明确写着：

* legacy Activation vocabulary；
* production 不再发送 `ManagerWorkActivation`；
* `WorkActivated` 只剩 inert legacy decode；
* production Activation path 已删除。

更值得注意的是，我对整个打包仓库搜索 `ManagerActivation.ensureAccepted`，**只有两个命中，而且都在 HOW 文档里，没有生产调用点。** 

这就是非常典型的：

> “功能已经没了，但旧 architecture vocabulary 还站在那里。”

### 这里不要“简化 ManagerActivation”。

直接做：

```text
ManagerActivation.ensureAccepted
        ↓
确认无生产调用
        ↓
删除 ManagerActivation module
        ↓
删除 EnsureAcceptedResult
        ↓
删除 architecture whitelist / dependency
        ↓
删除测试
        ↓
修 HOW
```

**不要留下：**

```fsharp
[<Obsolete>]
module ManagerActivation
```

也不要：

```fsharp
let ensureAccepted ... = Ready ...
```

更不要改名：

```text
LegacyManagerActivation
```

都属于给尸体换棺材。

### Wave 1 Done

搜索：

```bash
rg 'ManagerActivation|ManagerWorkActivation'
```

允许出现的位置应该最多只剩：

```text
CHANGELOG / historical ADR
```

如果连历史说明都没有持续价值，**零命中更好。**

---

# 四、Wave 2：内部 compatibility adapter —— 这是最大头

这一类通常是“舍不得删”的核心。

你们代码里已经存在非常明确的例子：

```fsharp
/// Compatibility single-result join ...
/// Projects JoinItem → RunCompletion for callers that still need agent Outcome.
let join ...
```

也就是说，新世界已经有 `JoinItem`，但还保留 `RunCompletion` compatibility projection 给“still need”的内部调用者。

这正是本轮应该重点追杀的对象。

做法不是删 adapter 看测试炸。

而是：

```text
Compatibility adapter
        ↓
枚举所有 caller
        ↓
逐 caller 判断“为什么还需要 old representation”
        ↓
把 caller 改成直接消费 canonical representation
        ↓
adapter 调用数 → 0
        ↓
删 adapter
        ↓
删 adapter tests
        ↓
删旧类型（如果无其它职责）
```

你的指标不是：

> compatibility code 少了多少。

而是：

> **compatibility adapter 的 first-party caller 数必须单调下降到 0。**

### 每个 PR 都要求一个数字

例如：

```text
JoinItem → RunCompletion compatibility callers

before: 11
after:   7
remaining: 7
```

下一 PR：

```text
7 → 3
```

最终：

```text
3 → 0
delete adapter
```

这比“感觉代码干净了很多”强得多。

---

# 五、Wave 3：Deprecated 字段——最容易永生的一类

我建议把所有 `DEPRECATED` 直接当 P1 defect，而不是技术债。

仓库里已经有明确实例：

`RunCompletion.AgentId` 被标记为：

> DEPRECATED；为了 HostFork backward compatibility 保留；新代码应该使用 Map key 或 AgentName。

这就是标准 cleanup ticket。

不要继续问：

> “删 AgentId 会不会影响哪里？”

换一个问题：

> **“谁今天还消费 RunCompletion.AgentId？”**

然后把答案做成 call graph。

你目前至少还能看到 compatibility projection 仍在制造这个字段，例如 PTY → `RunCompletion` 时继续填写 `AgentId`。

所以正确顺序是：

```text
1. 找 read sites
2. 替换 read sites
3. 禁止 new code read deprecated field
4. field 变 write-only
5. 删除 writer
6. 删除 field
7. 删除 codec / fixture / test 中对应形状
```

### 特别推荐增加一个临时 gate

不是：

```text
禁止 AgentId 出现
```

因为 AgentId 本身可能是合法概念。

而是针对精确 AST/type surface：

```text
RunCompletion.AgentId forbidden
```

这样 migration 有棘轮效应：

```text
12 callers → 8 → 4 → 0
```

不会被下一个工程师重新加回来。

最终删除字段时，**这个临时 gate 也一起删除**。

不要留下纪念碑。

---

# 六、Wave 4：Persistence compatibility —— 这里绝不能简单“一刀全删”

这一层要最谨慎。

因为你的仓库目前实际上同时存在两种非常不同的 legacy 行为。

### A. 正确的 clean break

`FactCodec` 对一些无法无损解释的旧 journal 明确拒绝：

```text
pre-0.5.0 → reject
ScoreVectorRef-era → reject
unanchored PairProgrammingGuideline → reject
```

这是健康的。

因为代码不是“兼容旧世界”，而是在**拒绝把旧世界解释成当前世界**。

而 durable-events 甚至已经明确规定旧物理 store：

> 不读、不迁、不 reset、不双写；禁止 legacy importer、migrator、fallback-to-old-store shim。

**这种 refusal boundary 不属于兼容债。**

可以保留。

甚至应该比“智能兼容”更偏爱它。

---

### B. 真正还活着的 migration code

但同一个 `FactCodec` 里也还有：

```fsharp
migrateHandleCompleted
migrateHandleOwnership
migrateHandleByname
migrateManagerJobByname
rewriteLegacyObservationTags
```

而最终 `deserializeFact` 确实依次运行这些 migration。

例如 `HandleCompleted` 旧记录缺字段时，目前会自动注入 `null`。

这类不能因为名字叫 migrate 就直接删。

每一个都必须回答：

```text
还有没有真实 durable sample？
这些 sample 最晚可能活到什么时候？
用户是否承诺升级可跨越这个版本？
是否已有 retention horizon？
```

然后分三类：

```text
有真实旧数据 + 必须支持
    → KEEP decode-only + exit condition

无真实旧数据
    → DELETE

无法知道
    → instrumentation / fixture inventory
      不允许直接 KEEP forever
```

### 一个关键原则

允许：

```text
OLD bytes
  ↓
one decoder
  ↓
CURRENT domain
```

禁止：

```text
OLD bytes ↔ OLD model ↔ adapter ↔ CURRENT model
                       ↕
                   new writer
```

你们自己的 rulebook 已经规定了这个 asymmetry：historical durable compatibility 如果需要，可以 decode-only；不要留下旧 writer。

---

# 七、Wave 5：明确有“债权人”的 compatibility —— 不删，但关进隔离区

这是这次 cleanup 非常容易误伤的一类。

例如你们现在有：

> `Host TodoTable compatibility sink`

而且 HOW 明确说：

* 它服务当前 Host V1；
* canonical truth 不依赖它；
* compatibility 不属于永久需求；
* 未来 sink 可以整体替换。

WHAT 也已经把架构画得很正确：

```text
MagicTodoProjection / Journal facts = canonical truth
Host TodoTable                       = compatibility sink only
```

并禁止 sink 反推 canonical。

**这个不要现在硬删。**

因为它目前至少有一个具名债权人：

```text
OpenCode Host V1 TodoTable
```

但现在缺的应该是：

```text
EXIT CONDITION
```

把它改造成显式的：

```text
COMPAT-001

Creditor:
  OpenCode Host V1 TodoTable

Ingress/Egress:
  canonical obligation → V1 projection only

Forbidden:
  V1 → canonical reconstruction

Exit:
  Host V1 TodoTable no longer part of supported host contract

Owner:
  host-boundary

Removal:
  delete Surface.CompatibilityTodoRow
  delete obligationsToCompatibilityRows
  delete V1 canaries
```

这样 compatibility 不再是：

> “最好别动。”

而变成：

> **“这个东西已经被判死刑，只是执行日期由某个客观条件决定。”**

---

# 八、Wave 6：迁移代码比兼容代码更危险——尤其是“修复历史错误”的 runtime migration

你们还有一类非常典型：

`JoinDrain` 中存在：

```text
migrateRetiredFalseAbort
tryMigrateRetiredFalseAbort
migrateOutcomeToUnit
```

而注释直接说明这是：

> “Retired legacy false abort: deterministic replacement + correction”。

另外还存在：

> “Execute replacement migration when blob identity is known.” 

这一类值得单独做 **Migration Amnesty Review**。

因为迁移逻辑经常是最难删除的代码：

```text
“还有没有人处于迁移前状态？”
        ↓
“不知道”
        ↓
“那先留”
        ↓
永久 runtime architecture
```

对每个 runtime migration 强制问：

```text
它修复的是哪个版本以前制造的数据？

新版本还会制造坏数据吗？

坏数据有没有有限集合？

能否改成：
  离线一次性 repair
而不是：
  runtime 永远懂 repair？

有没有 observable evidence 表明坏数据已经为零？
```

如果系统允许 shock cut / archive-and-restart，那么很多 migration 可以进一步直接变成：

```text
detect → refuse
```

而不是：

```text
detect → reconstruct old semantics → rewrite → continue
```

这会让代码量和 state space 大幅下降。

---

# 九、第二轮不是删 production，而是删“防尸体复活的尸体”

这一步很多团队不会做。

重构之后经常会产生大量：

```text
FORBIDDEN_OLD_THING
LEGACY_TOKEN_GATE
NO_OLD_X
NO_V1_Y
absence-ratchet
```

它们在 migration 期间是对的。

**但它们不是永久 architecture。**

你们仓库已经出现这种情况。

例如 `js-surface-gate` 里还明确保存：

```text
js-student
js-teacher
JsStudent
JsTeacher
StudentCompileJs
...
```

作为 `FORBIDDEN_TOKENS`，目的只是确保旧 Student/Teacher world 不复活。

而 requirement 自己已经把这类东西标成：

> GARBAGE；`FORBIDDEN_TOKENS` 是 absence ratchet，**新世界基线稳定后可删**。

这句话非常重要。

### cleanup 的成熟度有三个阶段

```text
阶段 1
旧世界存在

阶段 2
旧世界删除
+ gate 禁止它复活

阶段 3
设计本身使旧世界不可表达
+ 旧名字已经失去文化记忆
+ 删除针对旧名字的 gate
```

你现在应该开始从 2 → 3。

也就是说：

不要永远维护：

```text
NO_STUDENT_TEACHER_REANIMATION_GATE
```

而应该最终靠：

```text
capability ownership rule
role projection rule
type system
positive architecture gate
```

使其无法重新产生。

---

# 十、`unified-store-gate` 是另一个值得“去考古化”的对象

它现在还记得不少历史：

* Student QA revival；
* no-migrator；
* legacy importer；
* dual-write；
* 甚至注释里写着某个旧 ratchet 已于 **2026-08-14 retired**。

这在迁移期非常有价值。

但最终建议把它拆成：

```text
历史 token gate
        ↓
逐步淘汰

永久 architecture invariant
        ↓
保留
```

例如：

不要永久检查：

```text
LegacyMigrator
LegacyImporter
JournalToEventStore
StudentQaMigrator
```

而检查真正永久的性质：

```text
production durable writer ownership = exactly one

runtime store roots ∈ allowed roots

all writes enter canonical EventStore

business modules cannot own durable backends
```

**Positive invariant > blacklist of historical mistakes。**

因为 blacklist 本身也会让未来工程师不停看到已经死亡的 ontology。

---

# 十一、然后做“墓碑文档清理”

你们现在的 HOW/WHY 中有不少：

```text
GARBAGE
历史与弃权
被拒方案
旧 XXX
```

在设计形成阶段非常有用。

但如果最终目标是：

> working tree 描述现在，

就应该开始区分两种历史知识。

### 必须保留

解释**当前奇怪设计为什么必须如此**的 rationale。

例如：

```text
为什么 historical ambiguous record 必须 fail closed
```

这是现在仍然有效的知识。

### 应该删除/归档

只是记录：

```text
我们以前有 A
后来删了 A
A 还有 A1/A2/A3 字段
曾有工具 FooOld
```

而这些信息对理解当前设计已经没有贡献。

这些应该：

```text
Git history
或 ADR archive
```

而不是继续出现在 active HOW。

最终应该努力让：

```text
WHAT = 永久 contract
HOW  = 今天怎么实现
WHY  = 今天为什么这样设计
```

而不是：

```text
HOW = 今天 + 前三朝考古现场
```

---

# 十二、建议具体按下面的 PR train 做

这是我会实际采用的提交顺序。

| PR        | 内容                                                        | 风险 |
| --------- | --------------------------------------------------------- | -: |
| CLN-01    | 清死代码、无 caller module、commented implementation             | 极低 |
| CLN-08..N | 每种 durable decode 单独裁决（LEGACY-013 已删除，LEGACY-010/011/012/014 BOUNDED-COMPAT 保持） | 中高 |
| CLN-X     | `false abort` runtime migration retirement                |  高 |
| CLN-Y     | Host V1 compatibility sink 加 creditor + exit contract     |  低 |
| FINAL     | 删除 legacy ledger 自身 + permanent architecture gates        |  低 |

注意：

**一个 PR 尽量只消灭一种 old-world concept。**

不要搞：

```text
cleanup legacy stuff
-143 files
```

那样 reviewers 最后一定因为不敢承担风险，把很多东西重新保回来。

---

# 十三、每个删除 PR 强制用同一个五步模板

这是“保姆级”的核心工作流。

```text
STEP 1 — ACCUSE
指出为什么它是 legacy：
“X exists only to support Y.”

STEP 2 — PROVE NO CREDITOR
搜索：
caller
writer
reader
test
fixture
public API
durable sample
deployment consumer

STEP 3 — MIGRATE
如果还有 repository-owned caller，
先迁 caller，不碰 compatibility implementation。

STEP 4 — DELETE
一次删除：
implementation
types
aliases
tests
fixtures
docs
flags
special cases

STEP 5 — ABSENCE PROOF
rg old-name
build
target tests
integration tests
architecture gate
```

尤其 STEP 4：

**不要只删 implementation。**

例如删除 `LegacyFoo` 时，目标是：

```text
LegacyFoo.fs              delete
LegacyFooTests             delete
LegacyFooFixture           delete
LegacyFooAdapter           delete
LegacyFooConfig            delete
LegacyFoo terminology      delete
LegacyFoo docs             delete
LegacyFoo TODO             delete
```

否则旧世界的“幽灵 ontology”还在。

---

# 十四、每个 compatibility survivor 都必须长这样

以后 review 里看到 compatibility，没有下面四句话就不准 merge：

```text
Compatibility creditor:
  <谁>

Old contract:
  <什么>

Boundary:
  <只允许在哪一层存在>

Exit condition:
  <什么可观察事实成立时删除>
```

例如：

```text
Compatibility creditor:
  OpenCode Host V1 TodoTable

Old contract:
  todos[{content,status,priority}]

Boundary:
  Mission/Obligation/Todo/Surface only

Exit condition:
  Host V1 TodoTable is removed from supported host contract.
```

严禁：

```text
// Keep for backwards compatibility.
```

这句话以后应该视为 lint error。

因为它什么信息都没提供。

---

# 十五、建立一个“删除预算”，不要建立“技术债 backlog”

我甚至建议每轮 cleanup 设 **negative LOC objective**。

不是 KPI 式盲删，而是方向性约束：

```text
本轮允许：
+ 100 行证明/architecture gate

但要求：
- 1000 行 transitional machinery
```

特别记录下面这些指标：

| Metric                               |         方向 |
| ------------------------------------ | ---------: |
| deprecated production fields         |        → 0 |
| internal compatibility adapters      |        → 0 |
| compatibility first-party callers    |        → 0 |
| runtime migrations                   |       → 极少 |
| dual representations                 |        → 0 |
| legacy aliases                       |        → 0 |
| old writers                          |        → 0 |
| compatibility without exit condition | → **绝对 0** |
| historical token blacklist           |          ↓ |
| GARBAGE/tombstone active docs        |          ↓ |
| canonical writers per semantic fact  |        → 1 |

真正重要的不是总代码行数。

而是：

> **一个 semantic fact 有几个 live representation / writer / path？**

目标永远是：

```text
1
```

---

# 十六、专门制定“奥卡姆剃刀 review 问句”

以后 code review 里不要问：

> 这个兼容代码有没有害？

问下面这些问题：

```text
如果把它删掉，具体谁会失败？

能给我 consumer 名字吗？

能给我真实 persisted sample 吗？

这是 read compatibility 还是 write compatibility？

为什么 current code 还能制造 old representation？

为什么 compatibility 不在 boundary？

为什么 repository-owned caller 不能迁？

这个 adapter 的 retirement condition 是什么？

如果三个月后没人记得它，代码自己能说明为什么还存在吗？

如果以后真需要它，为什么不能从 Git 找回来？
```

最后一问尤其重要。

因为你最开始说的那个心理：

> “怕删了找不回来”

在 Git repository 里，本质上是一个**错误的风险模型**。

删除的成本通常是：

```text
git log / git show / revert
```

保留的成本却是：

```text
每个新人阅读
× 每次搜索
× 每次重构
× 每次测试
× 每次设计
× 永久
```

---

# 十七、但一定要防止“奥卡姆剃刀”变成“大爆炸式删库”

这点我反而建议你很克制。

你们仓库已经明确提醒：

> anti-cruft 不是破坏真实 contract 的许可证。

所以不要下命令：

> “把所有 legacy、compat、migration 全删掉。”

正确命令是：

> **“所有 legacy、compat、migration 全部重新接受审判。”**

默认 verdict 是 DELETE。

但下面三种必须无罪：

```text
真实 public compatibility
真实 durable decode
真实 deployment overlap
```

区别在于它们不再拥有“永久居留权”。

只是：

```text
bounded exception
```

---

# 十八、我认为你这个仓库现在最值得先砍的四刀

根据当前代码，我会按这个顺序开工。

### 第一刀：`ManagerActivation`

这是最漂亮的 starter PR。

源码自己承认 production path 已删除、模块只剩 no-op vocabulary；而全仓精确搜索 `ManagerActivation.ensureAccepted` 只有 HOW 文档命中。

**目标：0 source occurrence。**

这刀可以给团队建立“真的可以删，而且删完世界没有塌”的信心。

### 第二刀：`RunCompletion.AgentId`

源码已经明确标 `DEPRECATED`、只因 backward compatibility 保留。

把所有 first-party read site 迁掉，然后删除字段。

这是训练团队：

> deprecated ≠ 永久供奉

的最好案例。

### 第三刀：single-result Join compatibility

`JoinItem` 已经是新 representation，但代码还明确给 “callers that still need agent Outcome” 做 `RunCompletion` projection。

迁完这些 caller，然后删 compatibility API。

这一刀开始真正降低 architecture state space。

### 第四刀：FactCodec compatibility census

**先不删。**

把：

```text
migrateHandleCompleted
migrateHandleOwnership
migrateHandleByname
migrateManagerJobByname
rewriteLegacyObservationTags
```

每项单独建立 creditor / durable-sample / exit-condition。

因为这些是最可能既包含真需求、又包含历史恐惧的地方。当前 deserialize pipeline 明确仍会调用它们。

这刀会告诉你真正还剩多少“必须背负的过去”。

---

# 十九、最终完成态不是“没有 legacy 这个单词”

真正的最终态应该是：

```text
Production
    一个 canonical ontology
    一个 authoritative writer
    一个正常 execution path

Compatibility
    只在物理 boundary
    只服务 named creditor
    通常 decode/project one-way
    每条有 exit condition

Tests
    验 current behavior
    验 permanent architecture invariant
    不供奉已删除 ontology

Docs
    描述当前 system
    rationale 保留
    尸体清走

History
    Git 负责
```

这恰好就是你们仓库已经写出的 invariant：

> **Current code has one canonical model; compatibility exists only at boundaries where a real supported past still touches the present.** 

以及我认为最适合成为此次工程结束语的那一句：

> **The migration machinery has nothing left to arbitrate.
> The new architecture is not “preferred.” It is simply the architecture.** 

如果按这个 roadmap 执行，我建议内部不要把它叫“代码清理”或者“第五轮重构”。

叫 **Refactor Closure** 更准确。

因为前几轮是在建设新世界；**这一轮是在宣布旧世界不再享有公民权。**

---

---

我们把它定成一次**从“Fable 测试适配”迁移到“JS-native semantic architecture”**的系统改造。

终态不是“测试更好写”，而是：

> **所有测试都是 JS；所有值得测试的语义都有正式、稳定、JS-native 的边界；实现细节没有边界，因此 JS 根本无法依赖。**

这和仓库已有的测试哲学完全一致：测试应落到 supported input / observable result / durable state / contractual interaction，并允许内部 rename、inline、换数据结构而不受影响。

---

# 0. 先冻结“宪法”

在动代码之前，先把以下六条写进新的 requirement，例如：

```text
requirements/js-semantic-surface/
  README.md
  WHAT.md
  WHY.md
  HOW.md
  PROOF.md
```

内容不要写成“解决 mangled name”，那只是 symptom。

写成：

1. **所有 automated tests 使用 JavaScript。**
2. **JS semantic tests 只能调用正式 semantic surface。**
3. **值得独立测试的 law 必须有独立 semantic owner + JS surface。**
4. **不拥有独立 law 的 helper 不直接测试。**
5. **semantic data 跨边界必须是 JS-native representation。**
6. **Fable runtime representation 不属于 semantic contract。**

再加一句非常重要的：

> A surface exists because a semantic component owns a contract, never because a test needs access.

### JS-native 的定义

普通数据只允许：

```text
string
number
boolean
null / undefined
array
plain object
Promise
JS function/callback
```

必要时可以有：

```text
bigint
opaque resource handle
```

但 opaque handle 只能：

```text
create → pass back → dispose
```

JS 不得读它的 fields/prototype。

禁止作为 semantic data 暴露：

```text
FSharpList
FSharpMap
FSharpSet
FSharpOption
FSharpResult
F# DU instance
F# record runtime class
tag
fields
cases()
Fable DateTimeOffset encoding
curried F# function
mangled instance method
```

---

# 1. 先做 inventory，暂时不改行为

第一步不是写新 API。

先弄清现在 JS 测试到底获得了多少“不该有的权力”。

新增一个临时 inventory script，例如：

```text
scripts/test-surface-inventory.mjs
```

扫描全部：

```text
requirements/**/tests/**/*.mjs
```

记录五类债务。

### A. deep production import

例如：

```js
import '../../../dist/Execution/Session/...'
import '../../../dist/Foundation/...'
import '../../../dist/OpenCode/...'
```

### B. Fable export discovery

例如：

```js
Object.keys(mod)
Object.entries(mod)
startsWith('Foo__Bar_')
endsWith('_Baz')
```

你仓库已经有明确实例：`SessionQuiescenceGate` 测试直接扫描 mangled methods。

### C. Fable representation knowledge

搜索：

```text
.tag
.fields
.cases()
FSharpList
FSharpMap
fable_modules
```

### D. legacy interop authority

搜索：

```text
member(
bind(
fableInstanceMethod(
prod(
toList(
caseOf(
payloadOf(
resultOf(
```

现有 `interop.mjs` 明确承担了 emitted-name resolution、Fable mechanics，而且集中加载大量内部 production modules。 

### E. 合法的 compiler/build verification

**不要误杀。**

例如现有：

```text
VERIFY_008_every_emitted_module_actually_loads
```

故意 import 所有 emitted JS 来证明 Fable build 真能 link。这个测试的 subject 就是编译产物，因此它有资格知道 `dist`。

把这种测试明确分类成：

```text
compiler/build verification
```

而不是 semantic test。

---

# 2. 立刻加“只减不增” gate

inventory 完成后，**马上阻止债务继续增长**。

不要等迁完才加 gate。

建立：

```text
requirements/verification-system/tests/js-boundary-gate.test.mjs
```

规则：

```text
新 semantic test:
    禁止新增 deep dist import
    禁止新增 mangled-name lookup
    禁止新增 Fable representation knowledge
    禁止新增 interop.mjs dependency
```

现存违规先进入临时 baseline：

```text
requirements/verification-system/tests/fixtures/
  legacy-js-boundary-debt.json
```

原则：

```text
baseline 可以删
baseline 不可以加
```

每迁一个测试，就删一个 baseline entry。

### 为什么先做这个？

否则你迁 30 个，别人又新增 20 个。

仓库自己的 boundary rule 已经明确提出应该机械扫描 dependency：foreign layer 只能指向正式 supported entry，禁止 deep path / generated detail。

---

# 3. 定义“surface”是什么，不是什么

这一步尤其重要，否则很快就会造出第二代 `domain.mjs`。

## 错误设计

```text
src/Wanxiangshu/TestApi.fs
```

里面：

```fsharp
let callJoinDrain = Internal.JoinDrain.drainFromJournal
let makeFact = ...
let internalState = ...
let callPrivateThing = ...
```

这是 **test facade**。

禁止。

同样禁止：

```text
PublicFacade
    = re-export everything internal
```

仓库现有规则也明确把这种做法列为假修复。

---

## 正确设计

surface 跟着 semantic owner 走。

例如：

```text
Host/Quiescence/
  Model.fs
  Policy.fs
  Surface.fs

Participant/Provider/Attempt/
  ...
  Surface.fs

Context/Prefix/
  ...
  Surface.fs
```

不是一定必须叫 `Surface.fs`。

也可以叫：

```text
Api.fs
Contract.fs
```

重点是：

> 它属于这个 subsystem，不属于 Tests。

并且它不是简单 forwarding。

它负责：

```text
JS representation
        ↓
semantic input
        ↓
owner
        ↓
semantic output
        ↓
JS representation
```

---

# 4. 先迁一个“纯语义 pilot”

不要第一枪就挑最复杂 Host runtime。

先选一个：

* 输入清晰；
* 输出清晰；
* 没有 resource lifecycle；
* 现在却通过 `domain.mjs` / Fable representation 测试；

的 pure component。

目标形式：

```js
const result = component.operation({
  ...
})

assert.deepEqual(result, {
  ...
})
```

而不是：

```js
const input = toList(...)
const result = resultOf(...)
assert.equal(caseOf(result), ...)
```

---

## pilot 的工作步骤

假设原测试是：

```js
const result = resultOf(
  InternalModule.someFunction(
    sessionId('s1'),
    toList(items)
  )
)

assert.equal(caseOf(result.error), 'Conflict')
```

### 第一步：先写 promise

不用看实现，写：

> Given X, when Y happens, the component rejects it as a conflict.

如果这句话写不出来，先别设计 API。

### 第二步：设计 JS representation

目标：

```js
const result = component.someOperation({
  sessionId: 's1',
  items: [...]
})

assert.deepEqual(result, {
  ok: false,
  error: {
    kind: 'conflict'
  }
})
```

### 第三步：F# 内部继续保持 F# idiom

内部完全可以还是：

```fsharp
SessionId
Item list
Result<'a, Conflict>
Map<...>
DU
```

不要为了 JS 把 domain 污染成 primitive soup。

### 第四步：surface translation

逻辑上：

```text
"s1"
 ↓
SessionId.create

JS array
 ↓
Array.toList

Result<_, DU>
 ↓
{ ok, value/error }
```

转换发生在 owner boundary。

### 第五步：删测试里的 interop helpers

完成后，这个 test 不得再出现：

```text
sessionId()
toList()
resultOf()
caseOf()
```

---

# 5. 给 surface 本身建立 contract test

每建立一个正式 surface，都要有一个非常小的 API contract test。

你仓库已有 `guide-contract.test.mjs` 的机制可以复用：它会检查 emitted surface 的函数是否存在，甚至 pin exact surface。

例如：

```js
import * as quiescence from '...stable surface...'

assert.deepEqual(
  Object.keys(quiescence).sort(),
  [
    'beginAttempt',
    'create',
    'dropSession',
    'observeIdle',
    'revoke',
    'tryConsume',
  ]
)
```

注意：

**只有正式 contract surface 才 pin 名字。**

内部 module 的 emitted names 不 pin。

这正是我们需要的区别：

```text
internal rename
    → irrelevant

public surface rename
    → breaking contract
```

---

# 6. 第二个 pilot：专门攻克 stateful abstraction

接下来迁 `SessionQuiescenceGate` 这类东西。

这是很好的代表，因为现在测试实际上知道：

```text
SessionQuiescenceGate
BeginProviderAttempt
ObserveIdle
TryConsume
RevokeCurrentAttempt
DropSession
```

并通过 mangled method discovery 调用。

而 production implementation 内部实际上维护 `serials` 和 `activities` 两张 mutable map。

这些 state **JS 不应该知道**。

---

## surface 可以长成

```js
const gate = quiescence.create()

quiescence.beginAttempt(gate, 's1')

const permit =
  quiescence.observeIdle(gate, 's1')

assert.equal(
  quiescence.tryConsume(gate, permit),
  true
)

assert.equal(
  quiescence.tryConsume(gate, permit),
  false
)
```

这里：

```text
gate
permit
```

可以定义为 **opaque handle**。

测试只能：

```text
拿到
传回
```

不能：

```js
gate.serials
permit.fields
permit.tag
```

这样将来内部：

```text
Map → Dictionary
serial → generation token
class → actor
mutable state → immutable state + cell
```

JS 测试完全不变。

当前 gate 本身的语义已经非常清楚：新 provider attempt 使旧 permit 失效；idle 产生 permit；permit 只能消费一次；drop/revoke 使旧 permit 无效。

这就是应该发布的 law。

而不是它当前由哪两张 Map 实现。

---

# 7. 建立统一的 representation rules

两个 pilot 完成后，不要继续自由发挥。

把经验固化成规则。

建议建立一个非常小的测试 helper：

```text
requirements/verification-system/tests/support/
  js-contract.mjs
```

它**不是 domain facade**。

它只检查 representation：

```js
assertJsData(value)
assertOpaque(value)
```

比如递归拒绝：

```text
.cases()
.fields + numeric tag union shape
FSharpList tail/head representation
FSharpMap runtime object
Fable reflection metadata
```

最好进一步规定：

> 除 opaque resource handle / callback / Promise 外，semantic values 必须是 JSON-shaped。

那就非常容易理解：

```js
JSON.stringify(result)
```

理论上应该工作。

### 时间也建议归一

不要让 JS boundary 收到 Fable DateTimeOffset。

优先：

```text
ISO-8601 string
epoch milliseconds
```

内部再转换。

现有 facade 专门验证过裸 JS `Date` 与 Fable DateTimeOffset 可以产生 silent timezone bug。

终态不应该是教每个测试正确构造 Fable DateTimeOffset。

终态应该是：

> JS 根本构造不了 Fable DateTimeOffset。

---

# 8. 开始 Wave A：纯函数 / algebra / projection

这是最大批、也是最容易批量迁的部分。

优先迁：

```text
decision
classification
projection
codec
rendering
validation
selection
planning
ordering
```

每个 test 严格套同一个模板。

## 单测试迁移 SOP

### 1. 读测试名和 requirement clause

先别看 helper。

问：

> 它究竟要证明哪句话？

---

### 2. 写成 Given / When / Then

例如：

```text
Given an old permit
When a new provider attempt begins
Then the old permit cannot authorize continuation
```

---

### 3. 圈出真正输入

不是：

```text
FSharpMap
DU tag
InternalProjection
```

而是：

```text
events
commands
identity
policy configuration
```

---

### 4. 圈出真正 observable

例如：

```text
decision
rendered output
durable facts
allowed/rejected
next semantic state
effect request
```

---

### 5. 删掉草稿里的 implementation nouns

如果测试设计里出现：

```text
private field
helper function
module emitted name
cache implementation
Map key layout
DU ordinal
```

重新设计。

---

### 6. 判断是否真的存在独立 law

如果没有：

**不要开 surface。**

改测它的 owner。

---

### 7. 如果存在，找到 semantic owner

把 boundary 放 owner 旁边。

不要塞进中央：

```text
TestApi
DomainFacade
InteropEverything
```

---

### 8. 设计 JS representation

先写理想 JS：

```js
const actual = capability(input)
```

再去写 F#。

不要从现有 F# type 倒推 JS shape。

---

### 9. 写 surface contract test

证明：

```text
名字稳定
参数语义稳定
输出 JS-native
```

---

### 10. 重写原 behavior test

此时测试中 Fable vocabulary 应归零。

---

### 11. 做 positive canary

故意：

```text
rename helper
inline helper
change internal collection
reorder pure calculations
```

测试仍 green。

---

### 12. 做 negative canary

故意：

```text
return wrong decision
publish twice
accept stale permit
swap identity
```

测试必须 red。

这就是你仓库规则要求的“双向验证”。

---

### 13. 删 legacy dependency

删除：

```text
domain.mjs import
interop helper usage
direct dist import
baseline entry
```

**一个测试完成迁移的定义就是 baseline 少一项。**

---

# 9. Wave B：state machine / resource

接着处理：

```text
SessionQuiescenceGate
AttachedSessionRuntime
CompletionMailbox
ForkRuntime
process lifecycle
shared runtime resources
```

这些不要暴露 internal state snapshot。

优先 surface 成：

```text
create/open
command
observe
dispose
```

例如：

```js
const runtime = runtimeApi.create(config)

await runtimeApi.start(runtime, input)

const result =
  await runtimeApi.join(runtime)

runtimeApi.dispose(runtime)
```

opaque handle 不属于 semantic data。

它只是 capability token。

测试不能 inspect。

---

# 10. Wave C：effects

有副作用的 subsystem 尽量拆成：

```text
semantic decision
      ↓
effect request
      ↓
host interpreter
      ↓
effect result
      ↓
semantic transition
```

例如：

```js
const action = policy.decide(input)

assert.deepEqual(action, {
  kind: 'kill-process',
  processId: 'p1'
})
```

这部分可以大量 pure JS behavior tests。

然后单独：

```js
const actual =
  await processHost.execute(action)
```

测真实 effect boundary。

这样就不会为了测试 policy 而 mock 一大坨 Host。

---

# 11. Wave D：integration / plugin / e2e

这些本来就在真正的 external boundary 上，改动反而可能最小。

原则仍一样：

```text
发送真实 supported input
观察真实 supported output/effect
```

不通过内部 state 验证。

如果 E2E 失败需要 diagnostics：

diagnostics 可以存在，但必须是**正式 diagnostics contract**，而不是：

```text
__getPrivateStateForTests
```

---

# 12. 每完成一个 Wave，就收紧 gate

不要最后统一清理。

假设开始时：

```text
legacy violations = 180
```

Wave A 后：

```text
120
```

就把 baseline 永久降到 120。

Wave B：

```text
60
```

继续降。

直到：

```text
0
```

然后删除 baseline 机制本身。

最终 gate 直接：

```text
任何 semantic test deep-import internal dist
→ fail

任何 semantic test 使用 Fable representation
→ fail
```

---

# 13. `domain.mjs` 的退场路线

不要直接删除，因为现在它还是大量测试的 anti-corruption boundary。

当前设计本身很清楚：`domain.mjs` 是 transition entry，真正 Fable mechanics 在 `domain/interop.mjs`，family adapters 建在它上面。

所以分四步。

## 第一步

冻结：

> No new imports from `domain.mjs`.

## 第二步

每迁一个 family：

```text
identity
journal
context
execution
orchestrator
...
```

减少其 exports。

## 第三步

当普通 semantic tests 不再依赖 representation helpers 时，删除：

```text
bind
member
fableInstanceMethod
unionCase
prod
```

## 第四步

最后删除普通测试可见的：

```text
caseOf
payloadOf
toList
listItems
mapEntries
resultOf
unwrapOption
```

注意：

不是因为这些 helper 写得不好。

相反，它们现在非常有价值，甚至保护了真实 silent hazards。现有 meta tests 已经证明 JS array/FSharpList、DU ordinal、DateTimeOffset 等问题确实会产生静默错误。

删除它们意味着：

> **它们成功完成了迁移任务，以后普通测试已经到不了危险区域。**

---

# 14. 保留一个非常小的 Fable quarantine

这里不要走到另一种 dogma。

最终仍然可以有：

```text
requirements/verification-system/tests/compiler-interop/
```

这种测试专门验证：

```text
Fable output links
package emitted correctly
public JS surface exports correctly
compiler/runtime versions compatible
```

这些测试**有资格知道 Fable**。

因为被测对象就是 Fable build。

例如现有“every emitted module actually loads”应该保留。

最终边界应该是：

```text
99% semantic tests
    know zero Fable

tiny compiler/build suite
    explicitly knows Fable
```

而不是假装整个 repository 连 build verification 都不知道编译器存在。

---

# 15. 给 code review 一个固定判定树

以后 PR 新增测试时 reviewer 只问这几步：

```text
这个测试在证明一个独立 semantic law 吗？
              │
      ┌───────┴───────┐
      no              yes
      │                │
测 owner behavior    law 的 owner 是谁？
                       │
                 已有 JS surface？
                  │          │
                 yes         no
                  │          │
               使用它      设计正式 surface
                              │
                       是 JS-native 吗？
                         │        │
                        yes       no
                         │        │
                        done    修 representation
```

永远没有：

```text
“测试需要，所以 export internal”
```

这个分支。

---

# 16. 一组非常具体的 forbidden patterns

终态 architecture gate 可以扫描 semantic tests 并拒绝：

```js
value.tag
value.fields
value.cases()

Object.keys(fsharpModule)
Object.entries(fsharpModule)

startsWith('SomeType__')
endsWith('_someMethod')

import '.../fable_modules/...'

import '../../../dist/<internal-path>.js'
```

以及：

```text
member
bind
fableInstanceMethod
unionCase
```

甚至可以针对名字拒绝新增：

```text
ForTests
TestOnly
UnsafeForTest
DebugState
InternalFacade
TestFacade
```

不是说字符串永远非法，而是任何出现都要求 architecture review。

---

# 17. 不要做的五种“捷径”

### ① 自动把 `domain.mjs` 翻译成 F#

这是失败。

只是：

```text
JS test facade
→ F# test facade
```

问题没变。

---

### ② 给每个 F# module 都生成 JS wrapper

也是失败。

你会得到：

```text
1 implementation module
=
1 JS API
```

这仍然把 decomposition 变成 contract。

---

### ③ 为了测试暴露完整 state

例如：

```js
runtime.snapshotForTests()
```

返回：

```text
all private maps
all internal phases
all cursors
```

也是 white-box test，只是序列化了一层。

---

### ④ 为了 JS 把 F# domain 全 primitive 化

不要。

内部继续：

```text
DU
typed IDs
Map
Option
Result
records
```

强类型越丰富越好。

只在 boundary translate。

---

### ⑤ 建一个超级 `PublicApi.fs`

会逐渐变成 god module。

仓库自己对 cosmetic facade 的警告也适用于这里：facade 不能替 subsystem 制造虚假的 coherent ownership。

surface 应跟着 semantic owner 分布。

---

现在已经明显不一样了。**我认为“大规模重排目录”这件事已经基本完成，可以停止继续折腾顶层树了。** 这一版已经从“新旧两棵树并存”进入了“ownership tree 基本成立，只剩少数错误根和依赖边需要校正”的阶段。

最关键的变化是，`Domain / Application / Session / Infrastructure` 这些历史技术层已经不再出现在生产编译路径里；现在真正存在的是 `Change / Context / Enforcer / Execution / Foundation / Interaction / Mission / Participant / Persistence / Repository / Strength / OpenCode ...`。目录树已经能直接读出业务所有权。  `.fsproj` 也已经实际按这棵新树组织，而不是目录只改了名字、编译关系仍沿用旧层。比如 `Kernel/Fact` 已经变成 `Composition/Durable/Fact`，`CausalWait` 进入 `Execution/Session/Wait`，SyncDelegate 进入 `Execution/Delegation`，PromptAuthority 进入 `Interaction/Authority`。 

而且 capability-specific adapter 的“下旋”已经做得相当漂亮了。现在 Fork 自己拥有 `Fork/OpenCode/{Tool,JoinTool,JoinGuard,JoinResultRenderer}`，Fission、Finality、Review、Todo、Strength、Casebook、Js 也开始把自己的 OpenCode 接口收回自己的子树。  这就是我们之前说的：

> 物理世界是依赖对象，不自动获得业务代码的 ownership。

现在最值得做的不是“第三次整体排排”，而是下面 **5 个局部旋转**（2/3/4 已完成，1/5 仍待）。

1. **最大的剩余错误根是 `Composition/Durable/Fact.fs`。** 文件虽然从 `Kernel` 搬出来了，但实际上还没有完成我们说的那次旋转：`PromptFactCases`、`ReviewFactCases` 等业务 fact family 仍然定义在这个中央文件里。   也就是说现在是：

   ```text
   Composition/Durable/Fact
      ├── Prompt facts
      ├── Review facts
      ├── Execution facts
      ├── ...
   ```

   我仍然建议最终旋成：

   ```text
   Interaction/Authority/Facts.fs
   Participant/Provider/Attempt/Fallback/Facts.fs
   Mission/Review/Facts.fs
   Execution/Delegation/Facts.fs
   Context/Companion/Facts.fs
   Execution/Fission/Facts.fs
   Change/Facts.fs
            \   |   /
       Composition/Durable/Fact.fs
   ```

   `Composition/Durable/Fact.fs` 最终只应该做 outer union / routing vocabulary。**Composition 可以认识所有人，但不应该替所有人定义自己的语言。** 这是当前最有价值的一刀。

5. **最后做一次“假依赖边清理”，再决定是否继续旋转。** 例如 `Foundation/SyntheticToml.fs` 一开头竟然 `open` 了 Composition、Context、Enforcer、Execution、Host、Mission、Participant 等大量上层 subtree。 可它自己的注释却明确说它“knows nothing about Blogger, forks, or any local schema”，只拥有 canonical TOML string/layout rules。 从实际实现看，前面的 `normalizeNewlines / renderString / comment / field / tableEntry` 也确实是纯格式算法。 

   所以这里很可能不是根真的错，而是机械迁移后留下了一堆 unused `open`。**先清 unused imports，再画依赖图。** 否则我们会根据幽灵依赖做错误旋转。清完以后，如果 SyntheticToml 真只依赖 `System`，它放 Foundation 虽然我个人更喜欢 `Participant/Provider/Wire/Toml.fs`，但已经属于命名品味问题，不再是架构问题。

我现在对整体结构的判断可以浓缩成：

```text
上一版：
旧 layer tree + 新 ownership tree
→ 需要继续砍旧根

这一版：
ownership tree 已经成立
→ 不要再大改树
→ 拆中央 Fact（仍待）
→ 清 unused imports（仍待）
→ 再根据真实 cross-tree edges 做少量 rotation
```

还有一点我会特别强调：**现在不要因为 `OpenCode`、`Execution`、`Mission` 文件多，就试图“平衡文件数量”。** 平衡树思想在这里应该平衡的是“语义路径和跨树依赖代价”，不是节点个数。当前不同子树大小明显不一样，但这已经开始像自然生长出来的依赖树，而不是人为铺平的 taxonomy。 

所以如果问我“现在还乱不乱”，我的判断是：**生产目录本身已经不乱了。现在的主要架构债已经转移成了边界门禁、中央 Fact ownership 和少数假 Foundation 节点。** 这是一个好信号——说明目录重构基本可以收工，接下来应该治理依赖边，而不是继续搬树。

以上判断基于你刚上传的最新完整仓库快照。



---

这个方向值得做，而且我建议把它做成**比“覆盖率”更基础的一条仓库不变量**。

你现在已经有：

```text
WHAT ──→ PROOF ──→ test file
```

当前 `meta-verifier` 会枚举 WHAT proposition，检查 PROOF 有对应行，并检查 PROOF 引用的测试文件存在。 

但缺的是反方向：

```text
test case ──→ WHAT
```

这其实正好落实你已经写下的 `REQUIREMENT-SYSTEM-004`：proof ownership 是 **assertion 级，不是文件级**，每条 executable assertion 必须有唯一 owner。

我建议最终把关系做成一个数学上很简单的闭环：

```text
                 PROOF.md
                /        \
               /          \
              v            v
          WHAT-xxx  <───  test()

必须同时满足：

∀ test: exactly one primary WHAT
∀ WHAT: at least one active test
test → WHAT 必须存在
WHAT → test 必须存在
PROOF 中记录的边必须真实存在

skip / todo ≠ proof
```

换句话说，**active tests → WHAT 是一个 total + surjective mapping**。

而且我赞成你想要的压力：找不到 WHAT 的测试不允许用 `N/A` 糊弄过去。

---

## 我推荐的最终写法

不要靠目录推断，也不要靠文件顶部一行注释推断。直接让**每个 test 名自己携带 WHAT ID**：

```js
test(
  'WHAT[PROVIDER-LANGUAGE-005] system transform localizes only Wanxiangshu-owned segment',
  async () => {
    // ...
  },
)
```

动态 case 也一样：

```js
for (const bad of badSignals) {
  test(`WHAT[PROCESS-EXECUTION-003] rejects unsupported signal ${bad}`, () => {
    // ...
  })
}
```

机器合同只认：

```text
WHAT[<CURRENT-WHAT-ID>]
```

不认历史 `PROMPT_017`、`REVIEW_007` 之类 ID，不认文件路径隐式 ownership，也不认注释里的“看起来差不多”。

这有一个非常好的副作用：**CI 报错和本地 test output 本身就回答了“这个测试为什么存在”。**

你现在其实已经有很多人工版雏形。例如 `provider-system-transform.test.mjs` 文件头已经花了一段话解释它属于 `provider-language`，对应 `PROVIDER-LANGUAGE-001/005`，而不是另外几个相邻 owner。 以后这种判断直接进入机器关系，不需要靠考古。

---

# 保姆级 Roadmap

1. **先写新 WHAT，不要先写 gate。** 在 `requirements/requirement-system/WHAT.md` 新增一条，我建议叫 `REQUIREMENT-SYSTEM-018：可执行证明双向可追溯`，不要修改现有 004 的含义。004 继续负责“每个 executable assertion 恰一个 package owner”；018 负责更严格的“每个 test case 恰一个 current WHAT proposition”。你现在已经声明 WHAT 是唯一 normative contract，WHY/HOW/PROOF 都不是 normative，所以这条新规则必须先落 WHAT。

   我建议规范陈述直接写成接近这样：

   > `requirements/**/tests/**/*.test.mjs` 中的每个可执行 test case 必须显式声明恰一个当前 WHAT proposition ID；该 ID 必须存在于唯一 owner package 的 WHAT.md。每个当前 WHAT proposition 必须至少被一个非 skip、非 todo 的 test case 证明。test 与 WHAT 之间不存在无归属、悬空、多 primary 或仅依赖路径推断的关系。

   边界里再明确：helper、fixture、`beforeEach`、普通 `assert` 不是独立 proof case；粒度以 `test()/t.test()` 为准。一个 test 不允许 primary 到两个 WHAT。

2. **把“一个测试只能回答一个 WHAT”定死。** 这是我建议你比现在再严格一步的地方。当前 PROOF 里已经有一个 test anchor 同时服务多个 proposition 的情况，例如 interaction-authority 的表里存在 `001/002` 合并关系。 新规则下不要写：

   ```js
   test('WHAT[A-001,A-002] ...')
   ```

   而应该拆成：

   ```js
   test('WHAT[A-001] receipt cannot become authority root', ...)
   test('WHAT[A-002] only physical message may establish root', ...)
   ```

   两个测试可以共享 setup、helper，甚至共享一次昂贵的物理运行结果，但**failure meaning 必须只有一个**。如果两条命题根本无法分别测试，优先回头问 WHAT 是否其实应该是一条命题。这正是你要的文档反哺。

3. **定义测试宇宙，避免 denominator 偷漏。** 第一版严格限定：

   ```text
   requirements/**/tests/**/*.test.mjs
   ```

   里面所有真正的 `test()`、`test.skip()`、`test.todo()`、nested `t.test()` 都必须被 scanner 看到。`*.fixture.mjs`、support helper、`before/after` 不算 test。`skip/todo` 可以要求带 WHAT 标签，但**不能算作 WHAT 已有 proof**。

   这一条非常重要，否则以后很容易出现一个漂亮的 gate，却漏掉某类 integration/eval/e2e tests。

4. **不要继续把逻辑塞进现在的 meta-verifier；抽一个 trace graph。** 当前 `meta-verifier` 已经同时负责包树、依赖骨架、WHAT ID、PROOF 文件存在等结构检查。 再往里面直接加 JS test AST 解析，会很快变成 god verifier。

   我建议增加：

   ```text
   scripts/lib/requirement-trace.mjs
   scripts/checks/requirement-trace.mjs
   requirements/requirement-system/tests/requirement-trace.test.mjs
   ```

   `requirement-trace.mjs` 只构建一个纯数据图：

   ```text
   WhatNode {
     id
     package
     file
     heading
   }

   TestNode {
     file
     line
     title
     state: active | skip | todo
     whatId
   }

   Edge {
     test
     what
   }
   ```

   `meta-verifier` 后面可以复用这个 graph，而不是各自重新 regex。

5. **test source 用 AST/token parser 扫，不要用粗 regex。** Gate 必须能区分字符串里的 `test(`、注释、`test.beforeEach`、alias、template title、nested test 等情况。这个项目已经很重视 fail-closed gate，我不建议为了省一个轻量 parser 而造一个未来必漏的正则扫描器。

   Scanner 至少要能报这些错误：

   ```text
   TRACE_ORPHAN_TEST
   foo.test.mjs:42
   "rejects invalid carrier"
   has no WHAT[...] owner

   TRACE_UNKNOWN_WHAT
   foo.test.mjs:81
   references WHAT[FOO-999], but that proposition does not exist

   TRACE_MULTI_PRIMARY
   test declares more than one primary WHAT

   TRACE_UNPROVED_WHAT
   FOO-007 has zero active executable tests

   TRACE_PROOF_MISSING
   FOO-003 points to this test, but PROOF.md does not expose the relation

   TRACE_DANGLING_PROOF
   PROOF.md names a test anchor that no longer exists
   ```

6. **在真正迁移前先做 report-only inventory。** 命令建议设计成：

   ```bash
   node scripts/checks/requirement-trace.mjs --report
   node scripts/checks/requirement-trace.mjs --package=provider-language
   node scripts/checks/requirement-trace.mjs --explain=path/to/test.mjs:42
   ```

   第一次不要红 CI，只生成类似：

   ```text
   package                   WHAT   active tests   orphan tests   unproved WHAT
   provider-language            5             12              3               0
   interaction-authority       16             31              8               1
   ...
   ```

   `--explain` 最终应该成为非常好用的维护工具：

   ```text
   test
     requirements/provider-language/tests/provider-system-transform.test.mjs:27

   proves
     PROVIDER-LANGUAGE-005

   normative source
     requirements/provider-language/WHAT.md
     ## PROVIDER-LANGUAGE-005 ...

   proof index
     requirements/provider-language/PROOF.md
   ```

   这样“为什么有这个测试”不再需要 grep。

7. **迁移时绝对不要让脚本自动生成 WHAT。** 脚本可以根据当前位置、现有 PROOF、历史 ID、文件头注释给出 candidate，但只能建议：

   ```text
   likely WHAT:
     PROVIDER-LANGUAGE-005  0.92
     PROVIDER-LANGUAGE-001  0.71
   ```

   人必须做最终裁决。每碰到一个 orphan test，只允许四种处理：映射到现有 WHAT；发现文档遗漏，先补一个真正的 WHAT 再映射；发现测试钉的是 HOW 细节，重写成能够证明现有 WHAT 的行为测试；发现它没有独立 failure meaning，删除或并入别的测试。

   **第四种和第二种就是整个机制最值钱的地方。**

8. **按 package 小批量迁，不要全仓一次机械加标签。** 先 dogfood `requirement-system` 和 `verification-system`，因为它们负责规则本身；然后迁 owner 很清晰的小包；最后再处理 structured-workflow、host-boundary、capability-enforcement 这些交叉很多的包。

   每一个 package 都重复同一个闭环：

   ```text
   inventory tests
        ↓
   给每个 test 找 WHAT
        ↓
   找不到 → 文档 / 测试裁决
        ↓
   WHAT[...] 写入 test title
        ↓
   PROOF exact anchor 对齐
        ↓
   package trace = 100%
        ↓
   package 进入 hard mode
   ```

   不建议在这个阶段顺手大规模重构 production。一次 commit 尽量只做一个 package 的 trace closure，这样 review 能真正判断映射有没有作弊。

9. **迁移期可以有 ratchet，但必须从出生起就写 DELETE 条件。** 你刚刚才清理了一批历史 migration baseline，所以这次不要再造永久白名单。可以临时生成：

   ```text
   scripts/checks/requirement-trace-migration.json
   ```

   里面列当前仍未认领的 test anchor。规则只能：

   ```text
   新 orphan = RED
   已认领项不得重新进入 baseline
   baseline 数量只降不升
   ```

   然后逐包 hard：

   ```text
   strict:
     requirement-system
     verification-system
     provider-language
     ...
   ```

   当最后一个 package 进入 strict，**同一个提交删除 migration file 和 compatibility branch**。不要留下 `--allow-unmapped`。

10. **Hard cutover 时再把 PROOF 从“文件存在”升级到“精确边闭合”。** 你当前 parser 对 PROOF 的检查实际上只提取落点文件 token，然后确认文件存在。 这还不够，因为：

```text
PROOF says foo.test.mjs
```

并不能证明里面真的还有那个 test。

目标应升级成：

```text
WHAT[FOO-003]
    ↕
PROOF.md exact test anchor
    ↕
foo.test.mjs exact test case
```

你的 PROOF 文档已经大量写了“文件 + test/describe 锚点”，所以这是自然强化，不是换模型。

11. **最后我甚至建议让 PROOF 的 executable 部分半生成。** WHAT 必须坚持手写，因为它是 normative authority；test 的 WHAT tag 也必须人工裁决。PROOF 本身是 non-normative evidence index，没有必要让人重复抄几百个 anchor。

可以变成：

```markdown
## Executable proof index

<!-- BEGIN GENERATED TRACE -->

| WHAT | Active test cases |
|---|---|
| PROVIDER-LANGUAGE-001 | ... |
| PROVIDER-LANGUAGE-005 | ... |

<!-- END GENERATED TRACE -->

## Manual / physical evidence

...人工维护...
```

这样真正的 source of truth 是：

```text
WHAT.md          人写：系统必须是什么
test() WHAT tag  人裁决：这个 test 为什么存在
PROOF.md         生成：当前 evidence graph 长什么样
```

不会出现三个地方手工复制同一事实然后互相漂移。

12. **Full hard mode 后，把规则接进最前面的 cheap checks。** 我会把 `requirement-trace` 放在 build/test 之前：

```text
spec
requirement-trace
architecture
build
tests
...
```

新人写：

```js
test('some regression', ...)
```

应该在几十毫秒到几秒的静态门阶段直接收到：

```text
This test has no normative reason to exist.
Choose exactly one:
  1. reference an existing WHAT
  2. add a missing WHAT first
  3. rewrite the test so it proves an existing WHAT
  4. delete the test
```

这比等 code review 问“这个测试到底在测什么”有效得多。

---

## 我会再加一个防“文档作弊”的小机制

否则开发者可能学会这样过 gate：

```markdown
## FOO-999：其它行为

**规范陈述**：系统其它行为必须正确。
```

然后一百个测试全挂 `FOO-999`。

机器无法真正判断散文质量，但至少可以把作弊成本提高。既然你现在 WHAT 已经采用“规范陈述 + 含义/动机 + 边界 + 证据指针”的结构，例如现有 WHAT 就明确把这些组成看成 proposition 的完整表达。

所以 trace gate 在解析被 test 引用的 WHAT 时，还应该要求这些字段**非空存在**：

```text
标题
规范陈述
含义/动机
边界
```

不要用“至少 5 行”“至少 100 字”这种垃圾 heuristic；只检查结构存在。语义是否真的够具体仍交给 review。

同时提供非阻塞统计：

```text
WHAT fan-in:

FOO-001     3 tests
FOO-002     6 tests
FOO-003    47 tests  ← review hint, NOT automatic RED
```

47 个测试指向一个 WHAT 不一定错，但 reviewer 会马上知道应该检查是不是 catch-all。

---

## 关于低层 unit test，我建议你狠一点

以后如果看到：

```js
test('PtyId roundtrips its value', ...)
```

第一反应不要是“给它随便找一个 process WHAT”。

先问：

> **如果这个实现从 wrapper class 换成别的表示，这个 test 仍然应该成立吗？**

如果答案是否，那它很可能只是在 pin HOW。

此时应该考虑把它改成真正的 contract test，或者删除，而不是把 implementation detail 升格成 WHAT。

这会让你的测试数量可能有所下降，但测试的**信息密度会明显提高**：

```text
以前：
代码存在 → 顺手写 test

以后：
WHAT 存在
  ↓
需要 executable evidence
  ↓
test 存在
```

反方向：

```text
发现值得长期保留的 regression test
  ↓
找不到 WHAT
  ↓
说明：
  文档漏了 invariant
  或
  这个 regression 并不是产品合同
```

这正是你要建立的反馈回路。

---

## Cutover 的最终验收标准

到最后，仓库应该能机械证明：

```text
orphan active test                    = 0
test with unknown WHAT                = 0
test with multiple primary WHAT       = 0
WHAT with zero active test            = 0
PROOF anchor missing                  = 0
PROOF dangling anchor                 = 0
temporary trace migration exceptions  = 0
```

并且任意一个 test，你都能得到：

```text
这个测试为什么存在？
        ↓
WHAT[XXX-NNN]
        ↓
requirements/<owner>/WHAT.md
        ↓
这条当前系统真理是什么？
```

我认为这会比传统的“requirements coverage = 100%”强很多。传统 coverage 只能证明**文档没有漏测**；你这个双向闭环还能证明**测试没有偷偷创造第二套需求体系**。而你现有 requirement-system 已经把“WHAT 是唯一合同”和“executable assertion 有唯一 owner”铺好了，实际上只差把这条反向边机器化。 


---

# Fractal CE / Ghostbuster 保姆级施工手册：从旧状态机逐行改成可组合 F# CE DSL

本节只回答一个问题：**工程师拿到一段旧代码，具体从第一刀到最后一次验证应该怎么改。**

不要先讨论“架构美感”。不要先造新 builder。不要先给 `State` 换漂亮名字。按下面顺序机械施工。

适用范围：

```text
State / Stage / Phase / Step / NextAction / ResumeAt
mutable bool cluster
Command / Reply / Interpreter
Advance / Tick / Resume / Continue / Step API
registry presence 驱动业务分支
recovery 恢复内部执行位置
caller 驱动 child workflow
多层 match/if/try 控制金字塔
```

最终目标只有一个：

```text
typed evidence / capability
        ↓
Semantic Vocabulary
        ↓
task / taskResult / result CE
        ↓
let! / do! / match / return!
        ↓
domain outcome / effect
```

沿调用树放大仍是这套形状；只有到纯 `Evidence → Decision` 或 physical adapter 才停止。

对应正式合同：`STRUCTURED-WORKFLOW-001/002/003/005/007/008/009/010/011/012/013/014/015/016/017`。

---

## 0. 开工前先做这 8 件事，少一步都容易改歪

### 0.1 找到真正 owner

先回答：**删除这个文件后，哪个业务概念会不完整？**

不要因为代码“看起来像 Domain/Application/Infrastructure”就按技术层搬家。当前仓库按 bounded owner 成树；CE、Vocabulary、Decorator、Physical Adapter 是 owner 内部代码性质，不是新的目录根。

如果你不知道 owner，先停在调查阶段，不要开始抽象。

### 0.2 找到所有入口和 caller

对目标 symbol 做全引用搜索。至少列出：

```text
definition
public/exported entrypoints
direct callers
callers of callers
recovery caller
tests
serialization / journal / projection readers
```

状态机最常见的失败是只改 callee，caller 还在 drive 老协议。

### 0.3 搜出所有控制状态

对目标 owner 先做一次人工 census：

```bash
rg -n "State|Stage|Phase|Step|NextAction|ResumeAt|ContinueToken|InFlight|Armed|Pending|Should|Advance|Tick|Resume|Continue" src/Wanxiangshu/<owner>
```

这个搜索会有大量合法命中。目的不是“全删”，而是把候选列出来逐个分类。

### 0.4 搜 mutable / ref / registry

```bash
rg -n "let mutable|ref<|: .* ref|Dictionary|ConcurrentDictionary|Has[A-Z]|TryGet|ContainsKey" src/Wanxiangshu/<owner>
```

问每一处：它保存的是物理资源，还是业务执行位置？

### 0.5 搜 recovery

```bash
rg -n "recover|recovery|resume|ResumeAt|checkpoint|continuation|replay" src/Wanxiangshu/<owner>
```

正常路径改成 CE 而 recovery 仍跳 stage，等于没改完。

### 0.6 搜第二 runtime

```bash
rg -n "Command|Reply|Interpreter|Program<|Suspend|Continuation|WorkflowBuilder|MiddlewarePipeline" src/Wanxiangshu/<owner>
```

如果业务层存在 `Command -> Reply -> next Command`、AST node、continuation interpreter，优先判定为 `STRUCTURED-WORKFLOW-002` 问题。

### 0.7 找现有 WHAT / proof

不要边改代码边猜语义。先读目标 owner 的：

```text
requirements/<owner>/WHY.md
requirements/<owner>/WHAT.md
requirements/<owner>/HOW.md
requirements/<owner>/PROOF.md
```

再读 `requirements/structured-workflow/WHAT.md` 与 `HOW.md`。

### 0.8 先写一张迁移表

不需要新文件；review note 或工作记录里列清：

```text
旧字段 / 旧 API                 分类                     新归宿
CurrentStage                    control state            DELETE
RequestedReview                 durable fact             Journal/Projection
reviewerWaiter                  physical resource        physical leaf
ShouldPublish                   derived decision         pure decide function
NextAction                      control token            DELETE
ResumeAt                        recovery PC               DELETE
ReviewOutcome                   domain outcome           KEEP
```

**没有完成这张表，不要开始改类型。**

---

## 1. 每个状态只允许落入 5 类；分类后动作是固定的

### 1.1 Domain fact：保留

判断：实现彻底重写后，产品语义里这个区别仍存在。

```fsharp
type ReviewOutcome =
    | Approved of ReviewVerdict
    | Rejected of ReviewReason
```

动作：

```text
KEEP
必要时让它成为返回值 / durable fact
caller 可以 match
不要把它当“下一步指令”
```

### 1.2 Durable evidence：保留，但只描述已经发生的事

```fsharp
type ReviewFact =
    | ReviewRequested of ReviewRequest
    | ReviewCompleted of ReviewVerdict
```

动作：

```text
KEEP durable fact
Journal fold → Projection/Evidence
workflow 根据 Evidence 重新决策
严禁保存 ResumeAt/Stage
```

### 1.3 Physical resource state：保留在叶子

典型对象：

```text
TaskCompletionSource
CancellationTokenRegistration
semaphore permit
socket/process handle
physical lease
waiter registry
single-flight registry
```

动作：

```text
KEEP mutable if needed
必须属于 physical owner / adapter
向业务层只返回 typed capability / outcome / evidence
禁止向上暴露 Waiting/Armed/InFlight/Phase 让 parent drive
```

### 1.4 Algorithm scratch：保留在局部函数

例如 parser、binary search、buffer scan 的局部 `mutable index`。

动作：

```text
KEEP local mutable
不得跨调用、不得持久化、不得成为业务 API
```

### 1.5 Control state / program counter：删除

典型：

```fsharp
type WorkflowState =
    | Preparing
    | CallingProvider
    | WaitingReview
    | Persisting
    | Done
```

如果每个 case 的解释是“下一段代码去哪”，动作固定：

```text
DELETE type/case/field
DELETE serializer/projection support
DELETE advance/tick/resume dispatcher
把顺序写进 CE 调用栈
```

不要做下面这种“伪修复”：

```text
int state → DU state
bool pending → option PendingInfo
Stage → WorkflowPosition
NextAction → Decision
```

名字变了，若它仍回答“下一段代码跑什么”，仍然是 PC。

---

## 2. 把旧状态机拆成四张表，再开始写新代码

拿到旧实现后，把内容拆成：

```text
Facts       世界已经是什么
Decisions   根据 facts 现在该做什么业务判断
Effects     真正调用 Host/Git/process/timer/port
Sequence    effect 之间的先后关系
```

例如旧代码：

```fsharp
match state.Stage with
| NeedSnapshot ->
    let! snapshot = host.ReadSnapshot()
    state <- { state with Snapshot = Some snapshot; Stage = NeedReview }

| NeedReview ->
    let! verdict = reviewer.Review state.Snapshot.Value
    state <- { state with Verdict = Some verdict; Stage = NeedPersist }

| NeedPersist ->
    do! journal.Append state.Verdict.Value
    state <- { state with Stage = Done }

| Done ->
    return state.Verdict.Value
```

先机械翻译成表：

```text
Facts:
  Snapshot
  ReviewVerdict

Decisions:
  无独立业务判断；只是顺序

Effects:
  host.ReadSnapshot
  reviewer.Review
  journal.Append

Sequence:
  read → review → append → return
```

然后直接写：

```fsharp
task {
    let! snapshot = host.ReadSnapshot()
    let! verdict = reviewer.Review snapshot
    do! journal.Append verdict
    return verdict
}
```

**如果旧 Stage 只贡献 Sequence，它就不该进入新模型。**

---

## 3. 单模块状态机的标准拆法：按这个顺序改，不要倒过来

### Step 1：先找最终业务返回值

问：这个 workflow 完成后，caller 真正需要什么？

坏答案：

```fsharp
Task<WorkflowState>
Task<NextAction>
Task<StepResult>
```

优先改成：

```fsharp
Task<ReviewOutcome>
Task<Result<Publication, PublicationError>>
Task<unit>
```

返回值必须是领域结果、能力结果或真实证据，不是控制 token。

### Step 2：把“读取世界”单独命名成 Evidence

旧代码常见：

```fsharp
if state.HasPending && registry.Contains id && not state.ReviewDone then ...
```

先收敛输入：

```fsharp
type ReviewEvidence =
    { Request: ReviewRequest
      ExistingVerdict: ReviewVerdict option
      ReviewerAvailable: bool }
```

注意：只有字段真的描述领域/物理事实才进入 Evidence。不要把 `CurrentStage` 原样塞进去。

### Step 3：把纯判断提成小 Decision

```fsharp
type ReviewDecision =
    | AlreadyComplete of ReviewVerdict
    | NeedReview of ReviewRequest
    | CannotReview of ReviewReason

let decide evidence =
    match evidence.ExistingVerdict, evidence.ReviewerAvailable with
    | Some verdict, _ -> AlreadyComplete verdict
    | None, true -> NeedReview evidence.Request
    | None, false -> CannotReview ReviewerUnavailable
```

Decision 必须是业务判断，不是：

```fsharp
| Step1
| Step2
| CallPort
| Persist
```

### Step 4：把 effect 调用留在 CE

```fsharp
let run ports request =
    task {
        let! evidence = observe ports request

        match decide evidence with
        | AlreadyComplete verdict ->
            return Approved verdict

        | CannotReview reason ->
            return Rejected reason

        | NeedReview reviewRequest ->
            let! verdict = ports.Reviewer.Review reviewRequest
            do! ports.Journal.RecordReview verdict
            return Approved verdict
    }
```

### Step 5：删旧 `advance/tick/transition`

不要保留一个 compatibility wrapper：

```fsharp
let advance state = run state |> convertBackToOldState
```

这会让旧协议继续活着。clean break 时直接迁 caller，然后删旧 API。

### Step 6：删旧字段写入

搜：

```bash
rg -n "CurrentStage|NextAction|ResumeAt|<旧字段名>" src/Wanxiangshu
```

目标不是只删定义；所有赋值、copy-update、serialize、decode、projection、test fixture 都要清掉。

---

## 4. `while + mutable stage` 怎么改

### Before

```fsharp
let mutable stage = Start
let mutable result = None

while result.IsNone do
    match stage with
    | Start ->
        do! prepare ()
        stage <- CallProvider
    | CallProvider ->
        let! reply = provider.Call ()
        stage <- if reply.NeedsReview then Review reply else Persist reply
    | Review reply ->
        let! reviewed = reviewer.Review reply
        stage <- Persist reviewed
    | Persist value ->
        do! store value
        result <- Some value

return result.Value
```

### After

```fsharp
task {
    do! prepare ()
    let! reply = provider.Call ()

    let! value =
        if reply.NeedsReview then
            reviewer.Review reply
        else
            Task.FromResult reply

    do! store value
    return value
}
```

如果“是否 review”是领域判断，不要直接依赖裸 bool；先变成真实 Decision：

```fsharp
match ReviewPolicy.decide evidence with
| ReviewNotRequired value -> ...
| ReviewRequired request -> ...
```

---

## 5. `NextAction` / `StepResult` 怎么改

### Before

```fsharp
type NextAction =
    | CallProvider of Request
    | WaitReview of ReviewId
    | Persist of Value
    | Finish of Result

let! action = child.next state

match action with
| CallProvider request -> ...
| WaitReview reviewId -> ...
| Persist value -> ...
| Finish result -> ...
```

这是 interpreter protocol。

### 改法

1. 找出 `CallProvider/WaitReview/Persist` 各自真正 owner。
2. 让 child 自己通过 capability 调用这些 effect。
3. child 只返回最终 domain outcome。
4. parent 只 match domain outcome。

### After

```fsharp
let! outcome = ChildWorkflow.run capabilities input

match outcome with
| Completed result -> ...
| Rejected reason -> ...
```

如果 parent 仍然知道 child 的“第 2 步是 review、第 3 步是 persist”，边界还没切干净。

---

## 6. 父模块 drive 子状态机怎么改

### Before

```fsharp
let rec loop childState =
    task {
        let! step = Child.advance childState

        match step.Next with
        | InvokeProvider request ->
            let! reply = provider.Call request
            return! loop (Child.acceptProviderReply step.State reply)

        | AwaitReviewer request ->
            let! verdict = reviewer.Review request
            return! loop (Child.acceptVerdict step.State verdict)

        | Done value ->
            return value
    }
```

### After

把 provider/reviewer 能力传给 child：

```fsharp
type ChildCapabilities =
    { CallProvider: ProviderRequest -> Task<ProviderReply>
      Review: ReviewRequest -> Task<ReviewVerdict>
      Record: ChildFact -> Task<unit> }

let run capabilities input =
    task {
        let! reply = capabilities.CallProvider (requestOf input)
        let! verdict = capabilities.Review (reviewOf reply)
        do! capabilities.Record (ReviewCompleted verdict)
        return childOutcome verdict
    }
```

parent：

```fsharp
let! childOutcome = Child.run childCapabilities input
return ParentDecision.fromChildOutcome childOutcome
```

检查点：

```text
parent 不再 import ChildState
parent 不再 import ChildNextAction
parent 不再调用 Child.advance / acceptXxx
child 不再把执行位置返回给 parent
```

---

## 7. bool cluster 怎么改

### Before

```fsharp
type Runtime =
    { mutable Started: bool
      mutable Pending: bool
      mutable ReviewDone: bool
      mutable Persisted: bool }
```

先不要急着变成 DU。逐字段分类：

```text
Started      世界事实？还是 run 已进入第一步？
Pending      真有 pending physical resource？还是“还没完成”？
ReviewDone   durable ReviewCompleted fact 是否已存在？
Persisted    durable commit receipt 是否已存在？
```

典型结果：

```text
Started      DELETE（PC）
Pending      如果是 physical pending slot → physical registry
ReviewDone   DELETE bool，改由 ReviewCompleted fact/projection 推导
Persisted    DELETE bool，改由 durable commit evidence 推导
```

不要把四个 bool 换成：

```fsharp
type RuntimeState =
    | NotStarted
    | Started
    | WaitingReview
    | Reviewed
    | Persisted
```

那只是压缩后的 PC。

---

## 8. registry presence 驱动业务流程怎么改

### Before

```fsharp
match active.TryGetValue id, pending.ContainsKey id with
| true, _, false -> startWork ()
| true, _, true -> wait ()
| false, _, _ -> recover ()
```

问题不在 Dictionary；问题在 parent 用物理 presence 推导“业务走到哪一步”。

### 合法改法 A：presence 只留在 physical leaf

```fsharp
type AcquireOutcome =
    | Acquired of Permit
    | Busy
    | Gone

let acquire id : Task<AcquireOutcome> =
    // 内部可检查 registry
    ...
```

业务层只看 `AcquireOutcome`，且其语义是资源能力结果，不是 workflow stage。

### 合法改法 B：业务真相来自 durable evidence

如果业务问题是“这项工作是否已经完成”，读 Journal/Projection 的 completion fact；不要用“registry 中还在不在”猜。

### 必须删除的形态

```text
HasFlight && HasPending → Stage X
!HasFlight && HasWaiter → Stage Y
registry combination → next business effect
```

物理 presence 可以决定物理路由，不可以成为跨 owner program counter。

---

## 9. recovery 怎么改：绝不恢复执行位置

### Before

```fsharp
type Checkpoint =
    { Stage: WorkflowStage
      Request: Request
      PartialReply: Reply option }

let resume checkpoint =
    match checkpoint.Stage with
    | WaitingReview -> ...
    | Persisting -> ...
```

### Step 1：列出真正 durable facts

例如：

```text
RequestAccepted
ProviderReplyObserved
ReviewCompleted
PublicationCommitted
```

### Step 2：fold 成 Evidence

```fsharp
type RecoveryEvidence =
    { Request: Request
      ProviderReply: ProviderReply option
      ReviewVerdict: ReviewVerdict option
      Publication: PublicationReceipt option }
```

### Step 3：从普通 semantic entry 重入

```fsharp
let recover ports durableFacts =
    task {
        let evidence = RecoveryProjection.fold durableFacts
        return! Workflow.runFromEvidence ports evidence
    }
```

更理想的是普通 `run` 本身就先 observe/fold evidence，recovery 只负责取得 durable reality 后调用同一个入口。

### Step 4：删除 recovery-only stage API

删除：

```text
resumeAt
resumeStage
continueFromCheckpoint
restoreContinuation
restorePendingStep
```

保留的 `resume` 如果存在，必须表示**领域动作重新发起/重入**，而不是跳转 PC。名字是否保留要结合 owner 语义判断，不能只靠字符串机械删。

### Step 5：补 crash/replay proof

至少覆盖：

```text
crash before first effect
effect happened but process died before next observation
durable fact already exists → no duplicate semantic effect
partial evidence → normal workflow derives next action
terminal fact exists → reentry converges immediately
```

---

## 10. retry / fallback / polling loop 怎么改

### Before

```fsharp
let mutable retrying = true
let mutable attempt = 0

while retrying do
    let! result = call ()
    match result with
    | Ok value -> retrying <- false
    | Error _ -> attempt <- attempt + 1
```

### 如果是业务重试：必须有显式预算 + Semantic Vocabulary

```fsharp
let rec publishEventually budget publication =
    taskResult {
        let! attempt = publishOnce publication

        match attempt with
        | Published receipt ->
            return receipt

        | TargetMoved next when budget > 0 ->
            return! publishEventually (budget - 1) next

        | TargetMoved _ ->
            return! Error PublishBudgetExhausted
    }
```

要求：

```text
预算来自领域/系统规则，不是 while true
名字声明完整承诺
有 temporal/behavioral proof
内部仍是普通 CE + bounded recursion
```

### 如果是物理等待：留在 causal-wait / physical owner

物理 subscribe/recheck/timer 可以有自己的等待机制，但不要暴露 `PollingStage` 给业务层。

---

## 11. control pyramid 怎么改

看到：

```fsharp
match a with
| Some x ->
    match b x with
    | Ok y ->
        if condition y then
            try
                ...
```

按这个优先级处理。

### 11.1 `Result` / `Option` 传播 → CE bind

Before：

```fsharp
match parse input with
| Error e -> Error e
| Ok parsed ->
    match validate parsed with
    | Error e -> Error e
    | Ok value -> Ok value
```

After：

```fsharp
result {
    let! parsed = parse input
    let! value = validate parsed
    return value
}
```

异步 Result 使用仓库自己的 `taskResult` / `TaskResultCE` vocabulary；不要引用 FsToolkit 被 Fable 排除的 .NET-only Task API。

### 11.2 多个独立值共同决定结果 → tuple match

```fsharp
match request, verdict, receipt with
| Some request, None, _ -> ...
| _, Some verdict, None -> ...
| _, _, Some receipt -> ...
```

不要为了避 nested match 把状态切成三个 stage。

### 11.3 prerequisite → guard / flat if-elif

```fsharp
if not authorized then
    Unauthorized
elif budget = 0 then
    Exhausted
else
    Allowed
```

### 11.4 真正复杂领域判断 → 具名纯函数

```fsharp
let decision = ReviewPolicy.decide evidence

match decision with
| ...
```

不要只是把嵌套复制到 `helper2`。

### 11.5 collection short-circuit → traverse / fold CE

不要手写“循环 + mutable error + break flag”。使用项目已有 Result/TaskResult collection vocabulary。

完成后跑：

```bash
node scripts/checks/fsharp-control-pyramid.mjs --root=src/Wanxiangshu --show-all
```

目标：0 nested decisions。

---

## 12. Semantic Vocabulary 怎么抽，避免抽成垃圾 helper

当一段 CE 太长，不要按代码行数切 `step1/step2/helper3`。

只在调用点能形成完整业务承诺时抽：

```text
reviewUntilPerfect
publishEventually
recoverDurably
awaitChildrenSettled
finalizeWhenSafe
fallbackAcross
```

拒绝：

```text
process
handle
executeSafe
doRetry
runReliable
continue2
withPolicy
```

每个新 Vocabulary 必须回答五问：

```text
1. 名字声明什么业务承诺？
2. 隐藏哪些时序？
3. 哪个 temporal/behavioral proof 证明？
4. 是否改变 trace 集？
5. crash 后从什么 durable evidence 重入？
```

回答不出 → 不要抽。

### 抽取标准模板

Before：

```fsharp
taskResult {
    let! head = readHead ()
    let! rebased = rebase head candidate
    let! verdict = review rebased
    match verdict with
    | TargetMoved newer -> ...
    | Accepted value -> ...
}
```

After：

```fsharp
taskResult {
    let! publication = Publishing.publishEventually budget candidate
    do! recordPublication publication
    return publication
}
```

前提：`publishEventually` 自己有 law + proof，而且展开后仍然是 CE，不是内部 interpreter。

---

## 13. Port / capability 怎么切，避免 child 把 effect 请求返回给 parent

错误：

```fsharp
type ChildAction =
    | NeedGitRead of Path
    | NeedProviderCall of Prompt
    | NeedTimer of Deadline
```

然后 parent interpreter：

```fsharp
match action with
| NeedGitRead p -> git.Read p
| NeedProviderCall p -> provider.Call p
| NeedTimer d -> timer.Wait d
```

正确：把 capability 作为参数注入 child：

```fsharp
type ChildPorts =
    { ReadGit: Path -> Task<GitSnapshot>
      CallProvider: Prompt -> Task<ProviderReply>
      WaitUntil: Deadline -> Task<WaitOutcome> }

let run ports input =
    task {
        let! snapshot = ports.ReadGit input.Path
        let! reply = ports.CallProvider (promptOf snapshot)
        ...
    }
```

Port 返回值要按**单一能力的真实结果**建模，禁止一个巨大 Reply DU 吞所有 capability。

---

## 14. Physical Adapter 怎么写，防止“CE 洁癖”误伤底层

physical leaf 可以：

```text
mutable
Dictionary
TaskCompletionSource
CancellationTokenRegistration
resource automaton
socket/process state
```

但出口必须收敛。

### 好出口

```fsharp
type WaitOutcome =
    | Signalled
    | DeadlineReached
    | Cancelled

val wait : WaitRequest -> Task<WaitOutcome>
```

### 坏出口

```fsharp
type WaitState =
    { Armed: bool
      CallbackInstalled: bool
      TimerRunning: bool
      CurrentPhase: WaitPhase }
```

业务层不需要知道 waiter 内部装了几个 callback。

判断 physical state 是否越界：

> caller 是否根据这个状态决定“业务下一步做什么”？

如果是，向上泄漏了。

---

## 15. Command/Reply/Interpreter 第二 runtime 怎么拆

### Before

```fsharp
type WorkflowCommand =
    | ReadSnapshot
    | CallProvider of Prompt
    | Persist of Fact

type WorkflowReply =
    | SnapshotRead of Snapshot
    | ProviderCalled of Reply
    | Persisted

type Program<'result> =
    | Pure of 'result
    | Suspend of WorkflowCommand * (WorkflowReply -> Program<'result>)
```

### Step 1：按 capability 拆 port

```fsharp
type SnapshotPort = unit -> Task<Snapshot>
type ProviderPort = Prompt -> Task<ProviderReply>
type JournalPort = Fact -> Task<unit>
```

### Step 2：把 interpreter 分支搬回普通调用

```fsharp
task {
    let! snapshot = readSnapshot ()
    let! reply = callProvider (promptOf snapshot)
    do! append (factOf reply)
    return outcomeOf reply
}
```

### Step 3：删 Program/Command/Reply/Interpreter

顺序：

```text
migrate callers
remove exported types
remove interpreter
remove AST tests
replace with observable-effect tests
```

不要留下 `LegacyInterpreter`。

---

## 16. 跨模块 seam 必须逐条查，不能只看目标文件

完成一个 workflow 后，从它向上至少查两层 caller，向下至少展开所有 Semantic Vocabulary 一层。

每条 seam 做这张表：

```text
Seam                       输入                     输出                     合法？
Parent → ReviewWorkflow    ReviewRequest + ports    ReviewOutcome            YES
Parent → Child.advance     ChildState               NextAction               NO
Workflow → WaitAdapter     WaitRequest              WaitOutcome              YES
Parent → ChildRegistry     id                       HasFlight/HasPending      通常 NO：若驱动业务
Recovery → Workflow       durable facts/evidence   DomainOutcome            YES
Recovery → resumeAt       Stage                    internal continuation     NO
```

必须人工回答：

```text
1. 返回值是否包含执行位置？
2. caller 是否 match control token 决定下一 effect？
3. parent 是否读取 child registry/mutable cell 推断 lifecycle stage？
4. 是否有 Advance/Tick/Resume/Step family 被 caller 反复 drive？
5. recovery 是否跳 child 内部 stage？
6. Vocabulary 展开后是否仍是 CE + Vocabulary + bounded composition？
```

命中 1–5 默认 REVISE。

---

## 17. 类型迁移顺序：防止编译器一次炸全仓

大型重构不要先删类型定义。按依赖方向做 clean break：

### 17.1 新增最终 domain outcome / evidence / capability

先让新 API 可表达完整新世界。

### 17.2 写新 CE entrypoint

新 entrypoint 不返回旧 state/control token。

### 17.3 迁最内层 caller

从最接近 callee 的 caller 开始，改为直接 `let!` 新 workflow。

### 17.4 一层层向上迁

每迁一层，都删该层对旧 `State/NextAction/advance` 的 import。

### 17.5 迁 recovery caller

改成 facts/evidence → 普通 semantic entry。

### 17.6 迁 tests

测试从“内部 stage 到了 X”改为可观察行为：

```text
facts emitted
port calls
call order where semantically relevant
domain outcome
terminal projection
replay convergence
```

### 17.7 最后删旧类型 / API / serializer

此时删除：

```text
State/Stage/Phase
NextAction/StepResult
advance/tick/resumeAt
old interpreter
old checkpoint fields
legacy converters
```

### 17.8 全仓搜旧 symbol = 0

```bash
rg -n "OldState|OldStage|NextAction|resumeAt|advance" src requirements
```

合法同名词要人工审查，不能因为搜索有结果就机械删除。

---

## 18. 测试怎么改：不要继续测试内部状态机

### 删除这种测试

```text
after first tick stage = WaitingReview
advance returns Persist
resumeAt Stage3 calls append
state.Pending flips false
interpreter visits node 4
```

### 改成这种测试

```text
given evidence X → calls Reviewer once → records ReviewCompleted → returns Approved
existing durable ReviewCompleted → does not review twice
publish target moves → bounded retry → final receipt
crash after durable commit → reentry does not duplicate commit
child completion → parent observes domain outcome only
physical Busy → no fake domain completion emitted
```

### 测试命名必须绑定 WHAT

例如：

```js
test('WHAT[STRUCTURED-WORKFLOW-017] parent observes child domain outcome, never execution position', ...)
```

不要为了保留旧 test 随便找 WHAT 挂上去；如果旧 test 只证明 HOW，应该删或改成真正 contract。

---

## 19. 文档怎么同步改

### 19.1 WHAT

只有当产品/架构当前真理缺失时才新增/修改 WHAT。不要把施工细节塞进 WHAT。

Fractal CE 的组合闭包已经由 `STRUCTURED-WORKFLOW-017` 定义；普通重构通常引用它，不需要重复造 `FOO-999`。

### 19.2 HOW

如果 owner 的实现模型、Vocabulary、physical boundary 发生变化，更新 HOW：

```text
新 CE entrypoint
新 Semantic Vocabulary
新 port/capability
physical adapter 归属
旧 state machine 已删除
```

### 19.3 PROOF

新增或迁移 contract test 后，更新对应 proposition 的 proof 落点。

### 19.4 AGENTS

只有仓库级施工法/长期工程规则才写 AGENTS。具体业务语义仍归 `requirements/<owner>/`。

---

## 20. 每改完一个小块，立刻跑最小验证；不要等全仓最后一起炸

### 20.1 Direct CE / second runtime / composition closure

```bash
node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs
```

### 20.2 Program counter / mutable / state product / registry joint branch

```bash
node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs
```

### 20.3 Recovery

```bash
node --test requirements/structured-workflow/tests/recovery-reentry.test.mjs
```

### 20.4 Semantic Vocabulary

```bash
node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs
```

### 20.5 Control pyramid

```bash
node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs
node --test requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs
```

### 20.6 整个 structured-workflow 包

```bash
node --test requirements/structured-workflow/tests/*.test.mjs
```

### 20.7 仓库 gate

```bash
node scripts/check.mjs
```

### 20.8 Fable build

本仓禁止 `dotnet build`。

```bash
node scripts/build.mjs
```

需要完整交付验证时：

```bash
npm run format-build-test
```

---

## 21. gate 变红后，不要乱试；按错误名修

### `second-runtime-protocol`

你引入/保留了业务 `Command/Reply/Program/Step/Suspend` 协议。

修：拆成 typed ports + direct CE；删 interpreter。

### `business-interpreter`

业务层还有 Interpreter。

修：把 interpreter 的分支顺序搬回普通函数调用/CE。

### `flow-lift`

旧 Flow monad 面还活着。

修：caller 直接进入 task/taskResult CE。

### `program-counter`

字段/类型仍在表达程序位置，或 `ControlState` 分类本身被判红。

修：不要改名；删除 axis，用调用栈表达顺序。

### `behaviour-bool`

bool 名称/结构像行为阶段。

修：判断它是 durable fact、physical fact 还是 PC；PC 删除，事实改由权威来源推导。

### `state-product`

record 中出现多个独立状态轴。

修：

```text
业务轴 → 拆为真实 ADT / independent workflow / evidence
物理轴 → 放回 physical owner 并显式证明
PC 轴 → 删除
```

### `mutable`

mutable 未声明或不属于允许类别。

修：不要先补注释过门；先确认它是否真的 physical/local scratch。若承载业务流程位置，删除 mutable state。

### `mutable-record-field`

record mutable/ref 字段疑似业务 control state。

修：把物理资源移到 physical owner；把业务 PC 删除。

### `registry-joint-branch`

两个 registry presence 被联合用于选择 effect。

修：先问业务真相应来自 durable evidence 还是一个 physical capability outcome；不要让 parent 自己拼 presence。

### `infrastructure-leak`

纯语义 owner 直接引用 OpenCode/Process/Fable interop。

修：定义具名 capability，physical adapter 实现它。

### `fsharp-control-pyramid`

存在第二层 lexical decision。

修复优先级：

```text
bind
→ tuple match
→ guard
→ named Evidence→Decision
→ traverse/fold CE
→ 重新切 workflow boundary
```

不要 suppression。

---

## 22. 常见“看起来改了，其实没改”的 12 种伪修复

### 22.1 `int state` → DU

仍是 PC。REVISE。

### 22.2 `Stage` → `Mode`

改名逃 gate。REVISE。

### 22.3 `NextAction` → `Decision`

如果 case 是 `Call/Wait/Persist`，仍是 opcode。REVISE。

### 22.4 状态机塞进 `private` module

封装不等于消除。REVISE。

### 22.5 parent 不读 Stage 了，改读 registry presence

PC 换载体。REVISE。

### 22.6 normal path CE，recovery `resumeAt`

第二棵恢复状态机还活着。REVISE。

### 22.7 新建万能 `WorkflowBuilder`

如果 builder 解释 AST/continuation，就是第二 runtime。REVISE。

### 22.8 把每一步包装成 `step1/step2/step3`

只是函数名版程序计数器。REVISE。

### 22.9 大 `Decision` DU 接管整个 workflow

如果 case 表示执行步骤，还是 PC。REVISE。

### 22.10 保留旧 API “为了兼容”

clean break 后旧协议继续有 caller 就是双世界。迁完 caller 后删除。

### 22.11 为过 mutable gate 加 `DSL-MUTABLE` 注释

annotation 不能把业务 PC 变成物理状态。先修语义。

### 22.12 测试还在 assert internal stage

说明 contract 仍绑旧实现。改成可观察效果。

---

## 23. 一个完整示例：从“父驱动 child + checkpoint”改到 Fractal CE

### Before：旧世界

```fsharp
type ChildStage =
    | NeedProvider
    | NeedReview
    | NeedPersist
    | Complete

type ChildState =
    { Stage: ChildStage
      Request: Request
      Reply: ProviderReply option
      Verdict: ReviewVerdict option }

type ChildStep =
    | CallProvider of Prompt
    | CallReviewer of ReviewRequest
    | Persist of ChildFact
    | Done of ChildOutcome

let next state =
    match state.Stage with
    | NeedProvider -> CallProvider(promptOf state.Request)
    | NeedReview -> CallReviewer(reviewOf state.Reply.Value)
    | NeedPersist -> Persist(factOf state.Verdict.Value)
    | Complete -> Done(outcomeOf state.Verdict.Value)
```

parent：

```fsharp
let rec drive state =
    task {
        match Child.next state with
        | CallProvider prompt ->
            let! reply = provider.Call prompt
            do! checkpoint.Save { state with Stage = NeedReview; Reply = Some reply }
            return! drive { state with Stage = NeedReview; Reply = Some reply }

        | CallReviewer request ->
            let! verdict = reviewer.Review request
            do! checkpoint.Save { state with Stage = NeedPersist; Verdict = Some verdict }
            return! drive { state with Stage = NeedPersist; Verdict = Some verdict }

        | Persist fact ->
            do! journal.Append fact
            return! drive { state with Stage = Complete }

        | Done outcome ->
            return outcome
    }
```

recovery：

```fsharp
let resume checkpoint = drive checkpoint.State
```

### 第 1 刀：分类

```text
ChildStage      PC                     DELETE
ChildState      Stage 是 PC            DELETE container；保留其中真实数据来源
Request         domain input           KEEP
Reply           evidence               不作为 checkpoint field；由 durable fact/observation 得到
Verdict         domain evidence        durable ReviewCompleted
ChildStep       interpreter opcode     DELETE
checkpoint      保存执行位置            DELETE/改 durable facts
```

### 第 2 刀：定义真实 outcome

```fsharp
type ChildOutcome =
    | Accepted of ReviewVerdict
    | Rejected of ReviewReason
```

### 第 3 刀：定义 capability

```fsharp
type ChildPorts =
    { CallProvider: Prompt -> Task<ProviderReply>
      Review: ReviewRequest -> Task<ReviewVerdict>
      AppendFact: ChildFact -> Task<unit>
      ReadFacts: RequestId -> Task<ChildFact list> }
```

### 第 4 刀：把 durable reality fold 成 Evidence

```fsharp
type ChildEvidence =
    { ProviderReply: ProviderReply option
      ReviewVerdict: ReviewVerdict option
      Completed: ChildOutcome option }
```

### 第 5 刀：普通 CE 根据已有 evidence 收敛

```fsharp
let rec run ports request =
    task {
        let! facts = ports.ReadFacts request.RequestId
        let evidence = ChildProjection.fold facts

        match evidence.Completed, evidence.ReviewVerdict, evidence.ProviderReply with
        | Some outcome, _, _ ->
            return outcome

        | None, Some verdict, _ ->
            let outcome = outcomeOf verdict
            do! ports.AppendFact (ChildCompleted outcome)
            return outcome

        | None, None, Some reply ->
            let! verdict = ports.Review (reviewOf reply)
            do! ports.AppendFact (ReviewCompleted verdict)
            return! run ports request

        | None, None, None ->
            let! reply = ports.CallProvider (promptOf request)
            do! ports.AppendFact (ProviderReplyObserved reply)
            return! run ports request
    }
```

注意：这里 tuple match 是一个扁平 decision level；递归由 durable fact 推进，且必须受具体领域预算/幂等合同约束。若调用可能无限重复，继续补领域预算，不允许把 `run` 变成无限 retry 默认。

### 第 6 刀：parent 只等待 domain outcome

```fsharp
let runParent childPorts request =
    task {
        let! childOutcome = ChildWorkflow.run childPorts request

        match childOutcome with
        | Accepted verdict ->
            return ParentAccepted verdict
        | Rejected reason ->
            return ParentRejected reason
    }
```

### 第 7 刀：recovery 不再有专用 stage

```fsharp
let recover childPorts request =
    ChildWorkflow.run childPorts request
```

`run` 自己从 durable facts 观察当前世界。

### 第 8 刀：删除旧世界

删除：

```text
ChildStage
ChildState
ChildStep
Child.next
parent drive loop
checkpoint.Stage
checkpoint serializer/decode
resume checkpoint.State
stage-based tests
```

### 第 9 刀：补 proof

至少：

```text
fresh request → provider → review → durable facts → Accepted
ProviderReplyObserved 已存在 → provider 不重复调用
ReviewCompleted 已存在 → reviewer 不重复调用
ChildCompleted 已存在 → 直接收敛
crash after ProviderReplyObserved → reentry 从 facts 继续
parent 只观察 ChildOutcome
```

这才是完整迁移。只做到“第 5 刀 CE 看起来很好看”但没删 checkpoint/stage/caller protocol，不算完成。

---

## 24. 提交前逐项打勾；任何一项“否”都不要声称完成

- [x] 所有候选 `State/Stage/Phase/Step/Pending/Armed` 已分类为 Domain fact / Durable evidence / Physical state / Algorithm scratch / PC。
- [x] 所有 PC 字段、case、serializer、projection、fixture 已删除。
- [x] workflow 返回值只剩 domain outcome / evidence / capability result，不返回 control token。
- [x] caller 不再 drive `Advance/Tick/Resume/Step` API。
- [x] parent 不根据 child registry/mutable presence 推导业务 stage。
- [x] physical mutable state 全部停在 physical owner/adapter，向上收敛为 typed result。
- [x] recovery = durable facts/evidence → 普通 semantic entry；无内部 stage/continuation 恢复。
- [x] Semantic Vocabulary 名字声明完整承诺，并有 temporal/behavioral proof。
- [x] 不存在新 WorkflowBuilder / AST / Command-Reply interpreter。
- [x] control pyramid = 0；没有 suppression/allowlist 逃逸。
- [x] 旧 compatibility API 已删，不存在新旧双写/双读。
- [x] 测试断言 observable behavior，不断言内部 stage/node。
- [x] 新/迁移测试绑定正确 `WHAT[...]`。
- [x] owner HOW/PROOF 已同步。
- [x] `node --test requirements/structured-workflow/tests/*.test.mjs` 通过。
- [x] `node scripts/check.mjs` 通过。
- [x] `node scripts/build.mjs` 通过；未运行 `dotnet build`。

最终人工验收只问两句话：

> **在业务调用树任取一个节点：缩小是否是有名字、有 law 的领域动作？放大是否仍是普通 F# CE + Semantic Vocabulary，而不是显式状态机？**

> **如果现在把所有 `Stage/NextAction/ResumeAt` 名字换掉，控制状态还能不能靠结构继续存在？如果能，说明还没改完。**

两句都过，才算 Fractal CE 重构完成。

