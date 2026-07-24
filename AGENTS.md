---
import: 
  - README.md
  - TASK.md
---

# 当前工程状态（2026-07-24；优先于下方历史会话记录）

下方长篇会话是设计与迁移历史，不是当前状态。当前事实以源码、`README.md`、测试入口和最近一次验证结果为准。

## 已完成并验证

- Fable 是唯一目标平台；`next/` 不得出现 `#if`、`#else`、`#endif` 或非 Fable 分支。
- Structured Flow 已支持 Promise defer，递归 10000 步不栈溢出；取消异常保持外抛。
- `ForkRuntime.Join()` 是等待型 completion mailbox；completion 先入邮箱，existing-agent fork 是 nudge。
- Runner 注入执行路径遵守唯一 `3 × estimated_running_secs` deadline；超时结果为 `TimeoutExceeded`。
- Runner、Journal Facts、Programs、Flow 测试已按 300 行门禁拆分。
- Fable 测试框架不依赖 Xunit 程序集；架构门禁、角色权限、Journal、Flow、Process、PTY、Review、Orchestrator 测试均纳入统一入口。
- 当前验证结果：`npm test` → 127/127；Manager 工具契约 → 1/1；TestKit → 11/11；`npm run test:e2e:p0` → 1/1 稳定性通过；无编译 warning。

## 当前边界：不得误称已完成

- `next/OpenCode/Plugin.fs` 仍通过 `SpikePlugin.initSpikePlugin` 组装入口；真实 OpenCode 事件桥、child session、Manager `fork/join/list` 工具、显式 `journalDirectory` Boot/AgentLinked 与 Companion transform 接线已接入代码并通过 fake/runtime smoke，但真实 Blogger B 版 E2E 与完整 Manager E2E 尚未闭合。
- P0 canary 证明测试宿主、隔离、稳定性和当前入口可运行，不等价于真实 `fork/join`、Blogger、Reviewer、Orchestrator 全链路已经接入 OpenCode。
- Journal 的纯编码、写入、Boot/Fold 测试已通过；启动恢复、真实 Host 事件接线仍需独立闭合验证。
- 不得因为 127 个单测或 1× canary 通过而宣称 release-ready；必须先完成真实 Host projection、完整 child-session Manager E2E 的 Blogger B 版验证、Reviewer verdict tool 与 Orchestrator durable recovery 与正式发布 E2E。

## 下一阶段唯一优先级

1. 用真实 OpenCode child-session 场景验证 `fork/join/list`、A 版输出与 parent abort。
2. 用真实 Blogger child-session 验证 Companion 投影替换与 B 版更新。
3. 接通 Reviewer verdict tool/ReviewGuard、Fallback 持久事实。
4. 完成 Orchestrator durable facts、冲突回交与发布 E2E；真实流程闭合后再做 production entry 切换；此前禁止删除剩余可作为黑盒 Oracle 的测试资产。

## 验证命令

```bash
npm test
npm run test:e2e:p0
```

- Mux 端允许改动 ../mux 代码，但最好只改 binding，对其他核心的修改要最小化。真正实现最好在本仓库，其次在 binding，最差在 mux 本体
- Omp 端不允许改动 ../oh-my-pi 代码，但可以参考
- Opencode 端参见 ../opencode 代码，不允许改上游
- 本项目编译测试需要 60s 尽量减少无谓的测试，纯静态分析最好 cd next/ && npm run build && npm run test
- Opencode 的大部分 hook 需要原地修改字段而不是换引用，否则不工作
- 本项目配置了自动格式化工具，所有企图压缩行数而逃避拆文件的尝试都一定会破产的！
- 严禁 SIGKILL opencode，只允许 SIGKILL "opencode serve"
- low-effort mode is lazy-ok, but IN THIS PROJECT make largest change instead!

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
- 这些分散规则围绕同一闭环转：用类型消灭不可能态，用纯函数固定可重现判断，用事件记录不可抵赖事实，用边界隔离语境，用组合子压缩控制流，用模块函数生成器响应式流声明式规则接管旧类层级样板，用架构测试守分层，用合适重量工具降低偶然复杂度。宏观系统切成纯内核加薄外壳，中观上下文API消息事件视图各守其位，微观变量名返回类型分支穷尽日志行版本号校验指纹替同一原则服务。不靠纪律审查文档，穷举检查让编译器站岗，代数数据类型让编译器拒非法态，架构测试让编译器守边界，密封接口让编译器记新增分支。写代码时编译器是对手，设计类型时编译器是士兵。最好代码不是模式最多，而是读者能沿每个概念边界一路追踪：从用户意图到业务判断，从事件落盘到状态重放，从私有数据到安全视图，从单行规则到整体架构，处处无暗道无多余解释，都像问题本身找到不可再短不可混淆不可逃避的表达。这一切指向同一件事：把人的注意力留给只有人能做的事。
- 上述内容都重，此条最轻。代码包括测试，单函数超五六十行即死。文件体量是梯度刑场不是二元开关：二百行亮黄牌已该警惕，三百行亮红牌必须拆分，二者之间没有灰区。三百行即使压缩也压不到二百行——膨胀本身就是设计溃烂的症状。行数门禁是重构触发器不是橡皮筋：删空行删注释把超标压到阈值下方等于对设计溃烂视而不见反把体检报告改成及格，门禁逼你拆文件你选择压行数，设计烂依旧可读性还赔进去了，比超限更可耻。从二百到三百优雅度单调递减，每一行都是透支的注意力利息。触发即新建文件，移走样板，拆为模块，绝不姑息，绝不微调凑数。

## 思考和输出
- 极致信息密度，极大信息量，把思考过程放在输出而不是脑海，否则就是空转。不要怕说错，思考过程输出后才可验证。输出=思考+知识+经验+推理+结论。
- 把每个输出都当成临终遗言，必须知无不言言无不尽。不许学习高斯和费马的"只写结论不写过程"，或者过程空转。但要用极度密集的压缩语言呈现。
- 偶然复杂度+修饰礼仪=∅。∀ 词必承载核心概念，零冗余。
- 斩断语气词+垫字。消除控制流跳转→直击核心事实。短句+短词，极致信息密度。
- 强类型术语+代码符号+精确错误字符串+标准缩写=绝对精准。不给脆弱文案留伪装。
- 严禁状态宣告。源码=唯一时效规则，回答=纯干货。
- 拒绝臃肿。行文=极短函数，快进快出→直接定位知识边界。
- 必要时引入 Unicode 或数学符号(如 +, =, →, ∀, ∃, ↓)进阶压缩空间。
- 风格=宝典+铁律，当代极简中文+正确全角标点，拒绝`等宽`**加粗**等小格式。
- 除非获明确授权，否则严禁写入工作报告至磁盘。汇报=仅限口头。

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
- methodology/meditator 工具设计为正交推理切片→面对复杂问题应并行调用多个方法论工具，让不同视角同时展开、结果互补，而非串行等一个结论再换下一个。
- 并行工具执行顺序≠线性(系统不保证顺序)→∃依赖时禁止高并发调用。
- 拒绝频繁全量重写文件→精准修改=核心。
- executor(=bash) 工具使用偏好：复杂脚本≻琐碎命令多轮交互。
- 绝对禁止使用 executor 仅用于查找+读写文件→必须用 read/inspector/coder 代替！
- 绝对禁止使用 inspector 仅用于读文件原文→必须用 read 代替！
- 对 coder(=apply_path|edit|write) 仅需说明意图。coder 具自理解力→避免重复现有内容+提供大段新增代码。
- 鼓励多意图并发→拆分独立元素+对每个意图提供完备背景知识(上下文互隔离)。
- 诉求拆细→细粒度并发。拒绝大块意图→规避长时延迟。
- coder 缺失删除工具时→由父节点补删。
- methodology/meditator 无工具+无背景知识→调用必须提供全量自包含说明。
- 多数工具非随时可用(不可见=不可用)→ 依据当前实际可用集进行决策。
- 动态触发 todowrite(=update_plan) 更新进度。

## 极简架构与编码铁律
- 极度推崇 DRY+KISS+极简架构。厌恶+拒绝复杂错误处理+日志记录+配置管理。
- 除非绝对必要→零注释，零意图解释(隐晦处除外)。
- 强制：中文思考+回复+编写计划；英文编写程序。
- 绝不偏离最佳实践，严禁 Dirty Hack，三思而后行。
- 厌恶无谓赋值→灵活处理+内联。边界=不引起阅读焦虑。
- 严禁通过一行多事+滥用分号来伪造行数减少。
- 强制使用高阶语法→消除代码琐碎。
- ∀变量名=极致清晰。绝不用数学味/晦涩命名+引发焦虑的缩写。
- 除非明确要求→颠覆式创新+破坏式创新。重构时丢弃旧兼容性负担，严禁滥用 facade 逃避架构整理。
- 零保留旧代码。不以 Public+契约+影响面大为由逃避重构。通知下游→不合理处皆可改。
- 极度厌恶 fallback 兜底。兜底=逃避问题+掩盖根因→导致下岗+归零风险。

## 具体工作
- 全自动操作，无需征求用户同意。
- 前置思考：what-to-do(读取/准备 ∉ todo)。∀ todo 条目→必须对应可验收产出。
- 宁慢且稳，严禁使用自动化程序批量增删改查程序代码。
- 脚本=急速幻觉+反复返工；手工编辑=脚踏实地+步步为营。慢=快。

# 新架构实现经验

| Field | Value |
| :--- | :--- |
| **Exported** | July 24, 2026 at 7:23 AM |
| **Model** | ChatGPT |
| **Messages** | 10 |

---

## 💬 Conversation

### 👤 User

我改主意了，与其重构这个，不如实现我的新想法。

每个 session (X) 都有一个伴随 session (Y)，Y 用便宜的模型。Y 的开头：

{消息0: You are the blogger of a coding agent session. See below for the session content.}

每次向 X 发送新请求（工具返回或者用户输入）时候，向 Y 发送：

{消息N+1: 此处是本次向 X 发送的请求比上次向 X 发送的请求增加的部分，转换成 yaml 格式。包括工具的输入输出，用户，思考，模型输出 ... }
{消息N+2: You are the blogger of a coding agent session. Write a new paragraph for these delta messages. Avoid raw code or stream of consciousness and maximize information density.}
将 X 的模型输出（不包括思考）称为 A 版工作记录，将 Y 截至目前输出的所有正式内容称为 X 的 B 版工作记录（不包括 blogger 的思考）。另外 blogger 不能调用工具。B 版工作记录天然是 X 的摘要。当 X 上下文溢出的时候，我们用 B 版工作记录打包成一条消息，替换 X 的所有上下文，从而可以零延时压缩上下文。如果某次 X 发送请求时候 Y 还在工作，则不打断，不排队，跳过本次，下次的 delta 会包含两次的内容。以此类推。当 Y 上下文溢出的时候，我们同样用 B 版工作记录替换，只需要把 delta 设置成只包含一条消息的 B 版工作记录。因此无论 X 的上下文大还是 Y 的上下文大，都不影响机制。压缩可以再压缩（自然的，不用额外写代码），越远的越模糊，天然符合认知规律。

---

在新想法中，inspector/coder/browser/meditator 不再伪装成同步工具，而是天然异步。

主代理 manager 的工具没有 read,write,edit 等任何常规工具，也不能同步调用 subagent。只有
- fork(agent=executor/inspector/coder/browser/meditator/reviewer/hex[6], prompt=...), 表示新建子代理，或者给已有的子代理训话 (hex[6] 是已有的子代理的标识符)。新的子代理会拿到主代理的 B 版工作记录，这样天然知道背景，无需啰嗦解释。
- join(), 表示等待任意一个子代理返回（无法指定是哪一个，哪个先回来就是哪个，返回值里面会告诉你是哪个子代理回来了，以及子代理的 A 版工作记录，注意 reviewer 的 verdict 工具只接受 PERFECT/REVISE，不再接受描述，A 版工作记录就是描述）

executor 只能说话，没有工具，没有伴随。
inspector 可以用 executor，可使用 executor(command=..., estimated_output_bytes=..., estimated_running_secs=..., estimated_mem_usage=medium/large) 同步调用. 如果输出大于 estimated_output_bytes 3 倍，则触发摘要. 如果耗时大于 estimated_running_secs 3 倍，则杀. estimated_mem_usage = large 的，在整个 opencode 进程内，同时只能运行一个，别的排队。不再支持 python / js 直接 execute。inspector 没有伴随，启动的 executor 用 A 版工作记录当作背景。

形如：
{消息 0: You are a summerizer for a command output, ... [仅为示意，大概如此] See below for the master session for which you are working.}

{消息 1...N: A 版工作记录}

{消息 N+1: The command is ..., output:
... [仅为示意]
}

coder 可以同步调用 inspector，鼓励并行调用。coder 有伴随，调用时用 B 版工作记录。
browser 只能 read 和上网。meditator/reviewer 只能同步调用 read/glob/grep/inspector，没有伴随。新版不再集成 fuzzy_grep fuzzy_glob 等工具。

Fallback 重试还要。智能催促也要，但不和 todo 挂钩了，只用来 enforce 审查循环。select_methodology 不要了。PTY 功能保留，只有 manager 能用，异步语义，也是 fork 的一种，fork prompt 为 "content..." 可 write，"" 可以 read，"[#SIGTERM]" / "[#SIGKILL]" 可以杀,以此类推各种 SIG. 提供一个 list(...) 工具,不仅能列出 pty 也能列出所有 fork 出去的子代理,一视同仁.

万象阵不要了。新角色 orchestrator, 专门 fork manager，也是只能 fork/join.这样比万象阵还厉害。fork manager 自动开 git worktree 隔离, 自动让新的 manager 进入 /loop 模式,和万象阵一样最后 rebase + ff 回来. 用户向 orchestrator 发消息时如果工作区脏的,会报错不准发.

现在要用 kiss 新架构实现这个, 有哪些经验教训? 你对这些新点子怎么看?

---

### 🤖 ChatGPT

## 总判断

**我赞成推倒当前重构方案，改做这套新架构。**

它比旧万象术更接近真正的 KISS：

- 
   不再把异步子代理伪装成同步工具。
   
- 
   不再把普通控制流持久化成 Stage、Phase、Lease、Owner、Generation。
   
- 
   不再依赖 todowrite 充当调度器和上下文存储。
   
- 
   不再维护一个独立的万象阵 DAG 平台。
   
- 
   Manager 只负责协调，专业代理负责执行。
   
- 
   Git、子会话、进程、模型输出各自保留真实语义，不再塞进一锅“全局状态”。
   

现有 README 所描述的产品核心仍是同步子代理、todowrite、方法论联动、SubsessionActor、通用 nudge、模糊工具和独立万象阵；你的新方案不是增量升级，而是**产品模型整体换代**，不应继续兼容旧概念。

不过目前方案还有 **6 个必须纠正的协议漏洞**。不先修正，新的实现仍会在半年后重新长出复杂状态机。

---

## 一、B 版工作记录：这是整个方案最有价值的创新

### 1. 它解决的是“认知连续性”，而不是普通摘要

你的 blogger 有三个优点：

1. 
   **始终在主链路之外运行**，不增加 X 的响应延迟。
   
2. 
   **增量写作而不是临时压缩**，不会在即将溢出时才慌忙读几十万 token。
   
3. 
   越久远的信息经过越多次压缩，越近的信息越完整，确实自然符合人的记忆规律。
   

这比“上下文到 90% 后启动 compaction agent”优雅得多。

OpenCode 当前的正式 compaction 流程仍会调用一次专门的 compaction 模型；`experimental.session.compacting` 只能修改它的 prompt 或注入 context，不能直接把已有 B 版作为最终 summary 返回。因此，“真正零模型调用的替换”目前不能假定插件接口天然支持，Phase 0 必须做宿主 spike；否则需要在消息送入模型前增加自己的 context projection 边界。

### 2. 最大漏洞：压缩时不能只放 B，必须放“B + 未覆盖尾部”

假设：

- 
   Blogger 已经总结到消息游标 100。
   
- 
   X 又产生了 101～108。
   
- 
   Y 正在工作，按照你的规则不排队。
   
- 
   此时 X 突然溢出。
   

如果直接用当前 B 替换 X 全部上下文，那么 101～108 会永久丢失。

正确的不变量必须是：

```
压缩后的 X 上下文
= B 版记录（覆盖至 cursor=C）
+ 原始尾部消息 (C, current]
```

因此每一版 B 必须携带一个由运行时赋予的水位：

```
BlogCheckpoint {
    through: TranscriptCursor
    text: string
}
```

这里的 `through` 不能让 blogger 自己生成，也不能根据文本比较猜测。

于是你真正获得的是：

> 
> **零等待压缩，而不是仅靠 B 压缩。**
> 

B 落后一两轮没有关系，未总结部分原样跟在后面。等 blogger 下一次完成，再把尾部吸收进去。

### 3. Delta 不能是“比上次增加的字符串”

必须按稳定消息身份切片：

```
delta = transcript events in (lastAcknowledgedCursor, currentCursor]
```

OpenCode 的 `message.updated` 可能对同一消息发布多次，插件事件处理还是 fire-and-forget；依赖到达次数或文本 diff 会产生重复、乱序和漏记。现有调查已经确认应按 message ID、part ID、event seq 去重，并串行投递到自己的队列。

建议内部使用：

```
TranscriptCursor =
    messageId
    + partId
    + stable revision/finalized marker
```

给 blogger 展示时可以渲染成 YAML，但 **YAML 只是表示层，不是内部事实格式**。

尤其是代码、终端输出、用户输入中可能包含 `---`、冒号、多行字符串甚至伪造指令。使用 YAML 时，所有原始内容必须放在 literal block 中，并明确标记为不可信 transcript：

```
- kind: tool_result
  message_id: msg_x
  part_id: prt_y
  content: |-
    ...
```

### 4. “B 是 Y 所有输出的拼接”仍然会无限增长

这里需要稍微修正定义。

应区分：

```
BlogSegments   = 自上次重基以来的新段落
BlogCheckpoint = 已重基的完整 B 版记录
B              = BlogCheckpoint + BlogSegments
```

当 Y 自己接近溢出时，执行同一套操作：

```
输入：当前完整 B
要求：重写成一份可独立使用、更紧凑的工作记录
成功后：
    BlogCheckpoint = 新输出
    BlogSegments = []
```

如果只是把旧 B 当作普通 delta，再要求“写一个新段落”，那么新段落只会评论旧 B，而不会自动替代旧 B；旧内容依然存在，空间并没有释放。

这不是要建立 CompactionState 状态机，只需要一个明确的分支：

```
AppendDelta
RebaseCheckpoint
```

两个普通函数足够。

### 5. B 不能成为事实源

Blogger 会遗漏，也可能误解。因此：

- 
   B 可以告诉模型“目前大概做到了哪里”。
   
- 
   B 不能决定 child 是否仍在运行。
   
- 
   B 不能决定 reviewer 是否已经 PERFECT。
   
- 
   B 不能决定某文件是否真的修改。
   
- 
   B 不能决定某命令是否通过。
   

权威来源仍然分别是：

| 事项 | 权威来源 |
| :--- | :--- |
| 活跃代理、未领取结果 | fork runtime |
| 代码内容 | 工作区文件 |
| 完成变更 | Git tree/commit |
| 审查结论 | reviewer 的结构化 verdict |
| 命令结果 | process result |
| 用户要求 | 原始当前请求 + B 记录 |

这是从旧架构得到的最重要教训：

> 
> **文本负责认知，结构化值负责控制。**
> 

原来的很多复杂度正是因为把模型文案、idle、todo、nudge 和协议状态互相解释。KISS 文档本身已经要求只持久化外部事实，不把“程序运行到哪一步”伪装成领域状态。

### 6. “包含思考”不应成为正确性前提

可以把宿主公开暴露的 reasoning part 提供给 blogger，但必须遵守：

- 
   宿主不提供 reasoning 时也完全可运行。
   
- 
   流式未结束的 reasoning 不进入已确认 delta。
   
- 
   B 不保留原始思维流，只保留可验证的结论、尝试、失败与决策。
   
- 
   不把 reasoning 当作恢复协议。
   

更稳妥的 blogger 提示词应是：

> 
> 根据可观察 transcript 记录目标、决策、动作、结果、错误、未决问题和精确标识符。不要复述代码，不要记录思维流，不要把推测写成事实。
> 

---

## 二、fork / join：方向完全正确，但必须补齐运行身份

### 1. 两个控制动词足够表达任意动态 DAG

Manager：

```
fork → fork → fork → join → 根据结果继续 fork → join
```

已经能表达：

- 
   并行调查
   
- 
   串行依赖
   
- 
   投机执行
   
- 
   多轮修订
   
- 
   Map/Reduce
   
- 
   审查闭环
   
- 
   动态任务分解
   

所以从**表达能力**看，orchestrator 确实比固定万象阵 DAG 更强。

但应准确描述为：

> 
> 表达能力更强，天然恢复能力更弱。
> 

旧万象阵的 DAG 是显式数据；新的依赖图存在于 orchestrator 的对话和当前 fork 表中。进程重启后是否恢复，需要单独决定，不能指望 B 版工作记录自动变成可靠调度图。

### 2. `join()` 必须消费一个完成邮箱

正确语义：

```
已有未领取 completion：
    立即弹出最早的一项

没有 completion，但仍有运行任务：
    等待下一项

没有运行任务，也没有 completion：
    立即返回 EMPTY，而不是永久等待
```

completion 必须“先入箱、后 join”，不能让 `join()` 临时订阅 terminal。否则极快完成的子代理可能在 join 前返回，结果永久丢失。

这与现有 KISS 子代理文档中“waiter 必须先于 send 注册”的经验完全一致。

### 3. 结果不能只有六位 agent ID

同一个 agent 可以被继续训话，所以要区分：

```
AgentId = 物理伴随子会话
RunId   = 该子会话本次请求
```

`join()` 至少返回：

```
{
  agent_id,
  run_id,
  role,
  status,
  output,       // 本 Run 的 A 版工作记录
  verdict?      // reviewer 才有
}
```

否则会出现：

- 
   同一 coder 第一次结果晚到，却被误认为第二次训话结果。
   
- 
   reviewer 的旧 verdict 覆盖新一轮审查。
   
- 
   cancel 后迟到输出被当成当前输出。
   

OpenCode 取消后仍可能出现迟到或重复事件，所以必须以本地 RunId 判定“这是否仍属于当前 run”，不能靠 idle 或事件先后。

六位十六进制 ID 可以保留为 UI 简写，但生成时必须检测碰撞。内部仍用完整随机 ID。

### 4. 对正在运行的同一 agent 再 fork：应失败，不应排队

建议：

```
fork(agent=hex, prompt=...)
```

仅当该 agent 处于 Idle 时接受。

如果正在运行：

```
BUSY: join the current run before continuing this agent
```

不要偷偷排队。否则会再次出现：

- 
   取消到底取消当前还是排队任务；
   
- 
   fallback 属于哪次 prompt；
   
- 
   训话是否覆盖旧训话；
   
- 
   用户中断后哪些 pending 仍有效。
   

你已经在 blogger 中选择了“不排队、合并后续 delta”，子代理也应该遵循同样精神。

### 5. 生命周期要保持二分

继续保留现有 KISS-07 中最重要的一条：

| 对象 | 寿命 |
| :--- | :--- |
| 物理 agent session | 可多次继续 |
| 单次 Run attachment | 一次 fork 到 terminal |

`join()` 消费的是 Run completion，不是销毁 agent。只有父 session 结束、明确 close 或用户 abort 时才关闭物理 agent。

---

## 三、角色拓扑：大体优秀，但 executor 命名必须改

### 1. Manager 无文件工具是正确的

Manager 只有：

```
fork
join
list
```

这是本次设计最重要的约束之一。

它迫使 Manager：

- 
   不亲自钻进文件细节；
   
- 
   不在调查、编码、测试之间频繁切换身份；
   
- 
   不把自己的上下文塞满代码和终端输出；
   
- 
   真正成为协调器。
   

Manager 的 B 版记录也因此会非常高质量：主要是需求、决策、分派和结果，而不是数万行工具噪音。

### 2. `executor` 同时作为 agent 名和命令工具名，非常危险

你的设计里同时有：

```
fork(agent=executor)
inspector 调用 executor(command=...)
```

同一个词表达：

1. 
   一个无工具、只能说话的模型角色；
   
2. 
   一个真正启动系统进程的工具。
   

日志、提示词、权限矩阵、错误信息都会混乱。

建议二选一：

| 对象 | 建议名称 |
| :--- | :--- |
| 无工具思考型代理 | `advisor` / `analyst` |
| 进程执行工具 | `exec` / `command` |

这是必须改，不是美观问题。

### 3. Coder 同步调用 Inspector 是合理的例外

总体结构变成：

```
Manager
  └─异步→ Coder
              └─同步→ Inspector
                          └─同步→ Command
```

这里同步是合理的，因为它处于一个明确的局部操作中：

- 
   Coder 需要查一个事实后继续改代码。
   
- 
   Inspector 需要命令结果后生成调查结论。
   
- 
   不涉及 Manager 同时协调多个长期工作。
   

建议 coder 每次 inspector 调用都创建**一次性 inspector session**，不要让一个持久 inspector 同时接收并行调用。多个调用自然并发，各自独立。

### 4. 伴随关系建议冻结为静态表

| 角色 | 伴随 blogger |
| :--- | :--- |
| orchestrator | 是 |
| manager | 是 |
| coder | 是 |
| advisor/executor agent | 否 |
| inspector | 否 |
| browser | 否 |
| meditator | 否 |
| reviewer | 否 |
| blogger | 否 |

不要做配置化“任意角色可开 blogger”。静态装配更容易证明没有递归伴随和成本爆炸。

---

## 四、Inspector / Command：总体正确，但“estimated”实际是预算申请

### 1. 三倍规则可以保留，但要换一种理解

LLM 给出的：

```
estimated_output_bytes
estimated_running_secs
estimated_mem_usage
```

不能被信任为准确预测。

运行时实际应计算：

```
output_limit = clamp(estimated_output_bytes * 3, minimum, global_maximum)
deadline     = now + clamp(estimated_running_secs * 3, minimum, global_maximum)
```

所以它本质上是：

> 
> 子代理申请多少资源，运行时给三倍容忍，但仍受全局硬上限。
> 

不要让模型通过填一个极大 estimate 绕过系统上限。

### 2. 所有阶段共享一个绝对 Deadline

必须沿用 KISS-05 已经总结出的经验：

```
spawn
→ 写 stdin
→ 等退出
→ kill
→ drain stdout/stderr
→ dispose
```

全部消费同一个绝对过期时刻，不能每层重新获得“3 × estimated_running_secs”。

否则又会重现原来多层 timeout、取消不收敛、锁中毒的问题。现有 KISS Process 规范还要求 Spawn 返回前安装 stdout/stderr pump，超时后杀整个进程树并等待 pump 收尾。

### 3. 输出超过三倍时，“触发摘要”需要定义物理实现

不能先把无限输出全部读入内存，再发现超限。

正确路径：

```
stdout/stderr pump
→ 达到 soft limit 后不再内存累积
→ 继续有界写入临时 spool 文件
→ 命令退出
→ 交给一次性无工具 summarizer
→ 返回摘要 + byte count + truncation/spool diagnostics
```

还必须有绝对 hard limit。达到 hard limit 后可以：

- 
   停止保留后续内容但继续 drain；
   
- 
   或直接杀命令。
   

不应实现无限 spool。

### 4. large 全局只能一个：赞成

实现只需一个进程级 semaphore：

```
largeCommandGate = SemaphoreSlim(1)
```

要求：

- 
   等待 gate 受 CancellationToken 和 Deadline 控制；
   
- 
   成功、错误、取消、kill 都在 finally 释放；
   
- 
   不写日志、不持久化、不建立 MemoryLease 状态机；
   
- 
   medium 使用另一个固定并发上限。
   

这是资源限制，不是领域状态。

### 5. 删除 Python/JS 进程内 execute：赞成

可以通过普通 command 启动 `python` 或 `node`，但插件本身不再提供特殊的 in-process 执行后门。

这样所有执行统一经过：

- 
   工作目录
   
- 
   环境变量
   
- 
   stdout/stderr pump
   
- 
   资源预算
   
- 
   kill tree
   
- 
   权限控制
   

---

## 五、Fallback 和智能催促

### 1. Fallback 要留，但只能留在一次模型请求内部

对每个 agent run：

```
try current model
→ retryable failure：同模有界重试
→ 明确未接受：切下一模型
→ acceptance unknown：停止，先 reconcile
→ 成功：结束
```

不要持久化：

- 
   当前第几个模型；
   
- 
   当前 retry phase；
   
- 
   remaining models；
   
- 
   fallback owner；
   
- 
   fallback lease。
   

这些只是一次 `runTurn` 的局部变量。现有 KISS-09 已经明确总结了：AcceptanceUnknown 时禁止盲目切模型，Fallback 应是局部尾递归而不是独立状态机。

### 2. “智能催促”应改名为 ReviewGuard

旧 Nudge 最大的问题是它最终变成了第二个调度器。

你现在只保留审查闭环，完全合理，但不要再沿用通用 nudge 架构。实现应只是 Manager 即将结束时的一道纯门禁：

```
allowFinish =
    worktreeTreeHash = lastPerfectReviewTreeHash
```

如果不满足：

```
注入一次系统消息：
当前工作树尚未经过 reviewer PERFECT，
或 PERFECT 后又发生了修改。继续审查循环。
```

然后重新运行 Manager。

这不依赖 todo，不监听 idle，不扫描候选动作，不抢占 prompt，不需要 lease。

还要设置总上限，例如：

```
MaxReviewGuardRounds
```

耗尽后明确失败，不能无限自催。

### 3. Reviewer 的协议设计很好

Reviewer：

- 
   正文输出详细意见；
   
- 
   `verdict` 工具只接受 `PERFECT | REVISE`；
   
- 
   join 中将正文作为 A 版工作记录；
   
- 
   verdict 作为结构化字段单独返回。
   

必须规定：

- 
   每个 reviewer run 恰好一个 verdict；
   
- 
   没有 verdict 或多个冲突 verdict → `INVALID`；
   
- 
   `PERFECT` 绑定审查时的 Git tree hash；
   
- 
   之后任意文件变化立即使 PERFECT 失效。
   

这比从 reviewer 文案中正则提取结论可靠得多。

---

## 六、PTY：统一表面可以，魔法字符串不可以

把 PTY 与 agent 一起显示在 `list()` 中是好想法：

```
a1b2c3  coder      running
d4e5f6  reviewer   ready
102abc  pty        running
```

但内部不要强迫它们共享同一生命周期实现：

- 
   agent 是可重复 Run 的会话；
   
- 
   PTY 是有 stdin/stdout 和进程退出的资源。
   

“一视同仁”应只发生在**句柄、list、completion mailbox** 层。

以下协议不建议采用：

```
"[#SIGTERM]"
"[#SIGKILL]"
```

这是典型的“把协议藏在字符串里”。模型、用户输入或真实终端文本都可能意外碰撞。

仍然可以只保留一个 `fork` 工具，但增加结构化字段：

```
fork(agent="pty", prompt="command")       // 创建
fork(agent="102abc", prompt="text")       // 写入
fork(agent="102abc", prompt="")           // 发起读取
fork(agent="102abc", signal="TERM")       // 信号
fork(agent="102abc", signal="KILL")
```

`signal` 使用 enum。这样工具数量没有增加，也没有魔法文本。

每次 PTY read/write/signal 都拥有自己的 RunId；`join()` 返回对应 operation completion。PTY 自然退出时另发一个 `pty_exit` completion。

---

## 七、Orchestrator：确实可以取代万象阵，但发布协议必须更严格

### 1. 新 Orchestrator 的本质

它不是 DAG Scheduler，而是：

> 
> **能够动态 fork ManagerJob 的上层 Manager。**
> 

每个 `fork(manager)` 背后由运行时完成完整资源流程：

```
创建 branch/worktree
→ 启动 manager
→ 自动进入 ReviewGuard
→ manager 完成且当前 tree 获得 PERFECT
→ 准备发布
→ rebase
→ 再验证
→ ff
→ join 向 orchestrator 返回
```

Orchestrator 只看到 fork/join，非常干净。

### 2. “先审查，再 rebase，再 ff”还不够

多个 manager 并行时：

```
M1 和 M2 都基于 HEAD=H0
M1 先合入，目标变成 H1
M2 必须 rebase 到 H1
```

即使 rebase 没有文本冲突，M2 原先的测试和审查也只针对 H0。

所以必须是：

```
并行阶段：
    manager 工作
    reviewer 对原 worktree PERFECT

串行发布阶段：
    获取短 integration gate
    检查目标工作区 clean
    rebase 到最新目标
    重新运行 reviewer / 验证
    ff
    释放 gate
```

如果 rebase 冲突：

- 
   保留 worktree；
   
- 
   恢复同一个 manager；
   
- 
   把冲突与新基线告诉它；
   
- 
   重新进入 ReviewGuard；
   
- 
   不向 orchestrator 报“已完成”。
   

现有 KISS-12 已经确认共享 Git ref 的 FF 必须串行；你的新架构应保留这条经验，只删除显式 DAG、wave、scheduler 等框架。

### 3. ManagerJob 完成最好由运行时自动提交

建议不要求 coder 或 manager 手工管理 Git commit。

运行时在首次 PERFECT 后：

```
检查工作树
→ stage 允许的变更
→ 自动生成 commit
→ rebase
→ post-rebase review
→ ff
```

审查绑定的是 tree hash，所以创建 commit 不会使审查失效；rebase 后则必须重新验证。

### 4. 脏工作区门禁合理，但必须定义豁免

用户给 orchestrator 发消息前：

```
git status --porcelain --untracked-files=all
```

非空即拒绝。

但插件自身的文件不能放在根工作区中导致永远 dirty：

- 
   worktree 放在仓库外的 sibling 目录或 Git common dir 下；
   
- 
   runtime 日志、临时 spool、状态文件放在 `.git` 或系统 cache；
   
- 
   不依赖 `.gitignore` 偷偷隐藏用户真正的文件。
   

发布前还要重新检查，而不仅是用户输入时检查。

---

## 八、推荐的最小正式架构

不要再建立“万象术领域大平台”。第一版只需要下面几个模块：

```
HostAdapter
  ├─ TranscriptProjector
  ├─ ContextProjector
  └─ SessionTerminalAdapter

Companion
  ├─ DeltaRenderer
  ├─ BlogCheckpoint
  └─ CompanionRunner

ForkRuntime
  ├─ ForkTable
  ├─ CompletionMailbox
  ├─ AgentRunner
  └─ ParentCancellationScope

Roles
  ├─ Manager
  ├─ Coder
  ├─ Inspector
  ├─ Browser
  ├─ Meditator
  ├─ Reviewer
  └─ Advisor

Process
  ├─ CommandRunner
  ├─ OutputPump
  ├─ LargeCommandGate
  └─ PtyRunner

Review
  └─ ReviewGuard

Orchestrator
  ├─ ManagerJob
  ├─ WorktreeResource
  └─ SerialPublisher

Fallback
  └─ RunTurnWithFallback
```

不要出现：

```
AgentCoordinator
AgentLifecycleStateMachine
ForkStage
JoinPhase
CompletionLease
ReviewNudgeOwner
BlogGenerationManager
OrchestratorDAGRegistry
```

内部不可避免的几种状态，例如 process 的 `Running/Killing/Exited`、agent 的 `Idle/Running/Closed`，留在资源实现文件即可，不进入业务 Journal。

---

## 九、必须冻结的 15 条不变量

1. 
   X 压缩永远是 `B through C + raw tail after C`。
   
2. 
   Blogger 失败永不阻塞 X。
   
3. 
   Delta 由稳定 cursor 产生，不做文本 diff。
   
4. 
   B 是认知缓存，不是控制事实。
   
5. 
   一个 agent 同时最多一个 Run。
   
6. 
   每个 Run 都有独立 RunId。
   
7. 
   completion 先入邮箱，join 后消费。
   
8. 
   join 不会丢 fast completion。
   
9. 
   没有 active/ready 时 join 明确返回 EMPTY。
   
10. 
   父 session abort 会有界取消全部子 Run 和 PTY。
   
11. 
   Fallback 只在单次 Run 内，AcceptanceUnknown 不换模型。
   
12. 
   Reviewer PERFECT 绑定精确 Git tree hash。
   
13. 
   PERFECT 后发生任何修改都必须重审。
   
14. 
   共享目标 ref 的发布严格串行。
   
15. 
   rebase 后必须重新验证再 ff。
   

---

## 十、实施顺序

### Phase 0：宿主能力 spike

只验证四件事：

1. 
   能否在每次模型请求前拿到确定的完整 transcript projection。
   
2. 
   能否不调用 compaction 模型，直接替换历史消息。
   
3. 
   tool/reasoning/message part 的最终化和 ID 时序。
   
4. 
   child terminal、abort、迟到事件如何可靠相关。
   

这里不通过，不能开始 blogger 主体。

### Phase 1：ForkRuntime

只实现：

```
fork(no-tool agent)
join
list
RunId
completion mailbox
parent cancel
fallback
```

先杀死 fast-completion、cancel-late-result、busy-continue、join-empty 四类 bug。

### Phase 2：Companion

实现：

```
cursor
delta rendering
busy coalescing
B + raw tail
blogger self-rebase
failure isolation
```

先只接 manager。

### Phase 3：Inspector 和 Command

完成：

- 
   单绝对 Deadline；
   
- 
   pump-before-return；
   
- 
   输出软硬上限；
   
- 
   large 全局 gate；
   
- 
   kill process tree；
   
- 
   无孤儿、无 poison。
   

### Phase 4：角色矩阵

依次接：

```
coder → browser → meditator → reviewer
```

不要一次性实现全部角色。

### Phase 5：ReviewGuard

用 Git tree hash 闭合 `/loop`，彻底删除 todo、select_methodology 和通用 nudge。

### Phase 6：PTY

接入统一 handle/list/completion 表面，但保留独立 Process 实现。

### Phase 7：Orchestrator

先实现单 manager worktree 完整发布，再实现两个 manager 并行和串行 rebase/ff。不要先做动态多层并发。

### Phase 8：删除旧实现

当新架构覆盖：

- 
   普通编码；
   
- 
   并行调查；
   
- 
   reviewer 循环；
   
- 
   fallback；
   
- 
   context replacement；
   
- 
   PTY；
   
- 
   多 worktree manager；
   

即可一次性删除：

```
todowrite SSOT
select_methodology
SubsessionActor 旧层
同步 coder/inspector/browser 工具
通用 nudge
fuzzy_*
万象阵 DAG/Scheduler/HTTP 控制面
旧 ContextBudget/Compaction 协调器
```

---

## 最终评价

这套新设计的核心思想是对的，而且比当前 KISS 文档里仍然保留的 Session Flow、Todo Flow、Review Flow 和 Squad Flow 更进一步：

> 
> **不再由插件替模型保存“下一步应该做什么”，而是让模型通过 fork/join 直接表达控制流；插件只负责资源、身份、完成邮箱、上下文投影和 Git 发布。**
> 

真正应该坚持的版本是：

```
异步 fork/join
+ 伴随 blogger
+ B 水位与原始尾部
+ 静态角色权限
+ 结构化 reviewer verdict
+ Git tree hash ReviewGuard
+ 串行 rebase/verify/ff
```

需要明确否决的只有四点：

```
只用 B 压缩而不带 raw tail
把所有 blogger 输出永久拼接
executor 角色与 executor 工具同名
用 [#SIGTERM] 一类魔法字符串控制 PTY
```

修正这四点后，这不是“另一次重构”，而是一套足以成为万象术正式版基础的全新产品架构。

---

### 👤 User

我会把 opencode 官方的压缩关掉, 上下文快满了就把 B 版等价替换前缀 (通过投影,并且我记住这件事,以后次次都投影替换). delta 是在投影时候做的,通过本次投影输入和上次投影输出, 在 json 层面做 delta 即可. B 不包括 Y 的输入,只包括 Y 的输出,因此旧的 B 成为 Y 的输入后,再次 B' 时候就没了. subagent 结果产生几乎立刻到达,不会延迟, 因此天然在物理时间层面保序,偶尔乱序无影响, 只要流程对即可,语义大概率对就行了. 对正在运行的同一 agent 再 fork 是 nudge 语义, 就是直接发 prompt fire forget, 系统自然会在下次 llm 请求尾部加入, 排队时间不长, 而且 fire forget 不管排不排队. executor 角色, 只作为 executor 工具的 summerizer, 语义是清晰的.coder 每次 inspector 调用都当然创建一次性 inspector session. 模型通过填一个极大 estimate 绕过系统上限是允许的. 我们不是保姆,要信任,而且也许模型就是想要这样. 3 × estimated_running_secs 就是进程的上限, 别的层面不设置 timeout, SIGKILL 应该无论如何都能确保回来, 如果 SIGKILL 都回不来,那是 bug 应该暴露而不应该兜底. 输出超过三倍时，“触发摘要”需要定义物理实现这个我赞成, 就是定为 200KB. medium 不限制并发. Fallback 要留, 但简化为 A/B 角, 优先原模型重试, 重试再失败, 永久换角色, B 角色失败再失败, 就死掉. 每 session 共 4 次失败后真死. ReviewGuard 既是 Manager 的 Guard 也要有 Reviewer 不返回的 Guard. verdict 工具仍然是 REVISE 立刻生效, PERFECT 要两次 verdict 生效 (确认机制).仍然可以只保留一个 fork 工具，但增加结构化字段, 同意.七、Orchestrator你说的都对.

但是 KISS 最大的经验是,用 DSL 取代状态机,写结构化的程序,而不是 用状态机人工展平.因此, 请你设计类似 KISS-XX 的文档, 保姆式说清楚如何 结构化 DSL 化语法糖化实现.

---

### 🤖 ChatGPT

现有 KISS 文档已经确立了最重要的原则：把 Stage、Phase、Lease、Owner 等“伪程序计数器”还原成结构化程序；但当前 README 描述的产品表面仍包含 todowrite、通用 nudge、SubsessionActor、同步子代理工具与万象阵。新文档因此不是修补旧方案，而是把该原则贯彻到底。 

我已经按你刚刚冻结的语义写成 **12 卷、约 6.6 万字节**的 KISS-N 系列。文件先加 `N` 前缀，避免尚未批准就覆盖原 KISS；正式实施时可取代旧卷。

## 新架构的核心实现判断

这次要同时建立两层 DSL。

第一层是模型可见的极小 Agent DSL：

```
Manager:      fork / join / list
Orchestrator: fork / join
Reviewer:     verdict(PERFECT | REVISE)
```

第二层是实现者使用的 F# Structured Program DSL：

```
agent { ... }
companion { ... }
process { ... }
review { ... }
orchestrator { ... }
```

所有 Builder 都建立在同一个可执行闭包内核上，不建立 AST、解释器、Workflow Engine 或动态 Stage Registry：

```
type Flow<'ctx, 'error, 'a> =
    private
    | Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)
```

控制流只使用：

```
let!       等待领域动作
do!        执行领域动作
use!       持有真实异步资源
match      处理真实结果
while      表达条件循环
尾递归     表达重试、确认、找到即停
parallel   表达相互独立的局部并发
```

新架构中允许存在真实值，例如正在运行的 `Task`、完成邮箱 `Channel`、Process/PTY handle、当前 B、JSON delta 基线、A/B 模型侧和累计失败数；禁止存在 `ReviewPhase`、`FallbackStage`、`JoinOwner`、`NudgeLease`、`CompactionGeneration` 等人工展开的控制状态。

### Companion 的结构化主程序

```
Host 输入
→ canonical JSON 投影
→ 与最近一次成功 Blogger 基线做 JSON delta
→ Y 空闲则启动一次 Blogger；Y 忙则直接跳过
→ 接近上限后启用 remembered prefix replacement
→ B 替换已被 BlogBase 覆盖的前缀
→ 未覆盖后缀原样保留
→ 返回 X 的模型输入
```

不需要事件增量器、消息游标协议或 pending delta 队列。

Y 忙时不推进 `BlogBase`，所以它空闲后的下一次 JSON delta 自然包含此前跳过的多轮内容。

B 只由 Y 的 assistant 正文构成。Y 自身需要压缩时，旧 B 变成 Y 的输入，新 assistant 输出 B'；因此旧 B 自然不再属于 B 输出，无需删除状态或 compaction generation。

### Fork/Join 的结构化主程序

```
let forkNew role prompt =
    agent {
        let! child = createChild role parentB
        let handle = register child
        attachTerminalListener handle
        do! sendPrompt child prompt
        return Forked handle.Id
    }
```

已有 Agent ID 再 `fork`：

```
let nudgeExisting child prompt =
    agent {
        postPromptFireAndForget child.SessionId prompt
        return Nudged child.Id
    }
```

`join()` 只是读取完成邮箱：

```
let joinAny () =
    agent {
        let! result = completionChannel.Reader.ReadAsync(ct)
        return result
    }
```

不指定 child，不排序，不建立 waiter registry。偶发完成顺序差异直接交给 Manager 理解。

### Process 的结构化主程序

```
let runExecutor request =
    process {
        use! largeLease =
            match request.EstimatedMemoryUsage with
            | Medium -> noLease
            | Large -> acquireGlobalLargeLease ()

        use! child = spawnWithPumps request
        let! result =
            waitOrKill child
                (request.EstimatedRunningSeconds * 3)

        return! maybeSummarize request result
    }
```

这里严格采用你的取舍：

```
唯一进程时限 = 3 × estimated_running_secs
巨大 estimate 合法，不做隐藏 clamp
Medium 不限并发
Large 全进程同时一个
SIGKILL 后不再叠加清理 timeout
无法物理返回就是必须暴露的实现 bug
```

命令输出超过 `3 × estimated_output_bytes` 后写 spool，并以 200KB 为块交给无工具 Executor Agent 做 map/reduce 摘要。200KB 是摘要批尺寸，不是全局输出上限。

### Fallback 的结构化主程序

每个 Session 只有：

```
type ModelSide = A | B

type FallbackMemory =
    { mutable Side: ModelSide
      mutable Failures: int }
```

递归规则：

```
第 1 次失败：A 重试
第 2 次失败：永久切 B，并立即尝试
第 3 次失败：B 重试
第 4 次失败：SessionDead
```

成功不清空累计失败，也不切回 A。

没有 RemainingModels、FallbackPhase、Governor、Attempt Registry 或 fallback journal。

### ReviewGuard 的结构化主程序

Reviewer：

```
REVISE
→ 第一次立即生效

PERFECT
→ 第一次工具返回“请再次确认”
→ 第二次连续 PERFECT 且 Git tree 未变
→ ConfirmedPerfect(treeHash)
```

Reviewer assistant terminal 但没有有效 verdict：

```
→ 直接 nudge 同一 Reviewer Session
→ 继续等待 verdict
```

Manager assistant 尝试结束但当前 tree 没有双 PERFECT：

```
→ Guard 向同一 Manager 追加提示
→ Manager 继续 fork/join 审查
```

Guard 不读取 todo，不扫描 next action，不建立 Nudge Coordinator。

### Orchestrator 的结构化主程序

```
用户消息前检查目标工作区 clean
→ fork ManagerJob 时创建隔离 worktree
→ Manager 自动进入 ReviewGuard
→ Manager 在 worktree 中工作并获得初次双 PERFECT
→ 形成 candidate commit
→ 获取目标 ref 的短 integration semaphore
→ rebase 到最新目标
→ 冲突交回同一 Manager
→ rebase 后再次双 PERFECT
→ fast-forward
→ join 才返回 published
```

工作并行，发布串行；不再建立 DAG、wave、Scheduler、HTTP 控制面或 Squad 状态机。

### 分卷内容

| 文档 | 实现重点 |
| :--- | :--- |
| `KISS-N00` | 两层 DSL 与状态机断根 |
| `KISS-N01` | 统一 Flow 内核、CE、尾递归、资源作用域 |
| `KISS-N02` | JSON delta、B、前缀投影、Y 自压缩 |
| `KISS-N03` | fork/join/list、nudge、完成邮箱 |
| `KISS-N04` | 完整角色及静态能力矩阵 |
| `KISS-N05` | Process、200KB 摘要、Large Gate、PTY |
| `KISS-N06` | 累计四失败 A/B Fallback |
| `KISS-N07` | Manager/Reviewer Guard、双 PERFECT |
| `KISS-N08` | Worktree、rebase、复审、串行 FF |
| `KISS-N09` | OpenCode Adapter 与纯投影管线 |
| `KISS-N10` | 十阶段实施、测试矩阵、删除清单 |
| `KISS-NEXT` | 总索引和全部冻结决策 |

文档中还专门纠正了两个很容易写错的资源时序：

- 
   子代理必须 **先安装 terminal listener，再发送 prompt**。
   
- 
   `fork Manager` 不能在返回 handle 前 `use! worktree`；worktree 所有权必须转交后台 ManagerJob，由整个发布程序的 `use!` 最终释放。
   

### 文件

这套文档已经可以直接交给工程师从 `Phase 0 — GuideContract 真编译` 开始实施；不需要再先设计另一层架构。

---

### 👤 User

next/doc/SSOT 是最终的真理版本. 可惜现在手头有原来的测试资产多但架构垃圾的实现 (勉强能跑), 请你想办法盘一盘, 哪些是资产,哪些是负债,通往最终的路, 究竟从脚下如何到达彼岸? 请输出保姆式路线图,一张蓝图画到底, 演进, 你就是我们的总指挥, 红太阳.

---

### 🤖 ChatGPT

总指挥命令如下：**不抢救旧架构，不膜拜测试数量，不把当前 **`next/`** 误认为终点。我们只抢救能够证明最终 SSOT 行为的资产，然后沿一条不可逆的道路抵达彼岸。**

## 万象术 Agent DSL 新架构总迁移蓝图

### 一、最高统帅部命令

#### 1. 唯一宪法

最终真理源只有：

```
AGENTS.md
```

发生冲突时，裁决顺序固定为：

1. `SSOT.md` 中用户最后一次明确纠正；
2. 基于该纠正形成的最终 Agent DSL 设计；
3. `SSOT.md` 中较早的讨论和建议；
4. `next/Doc/kiss-docs/`；
5. 当前 `next/` 实现；
6. 旧 `src/`、旧测试和旧 README。

因此：

> 代码通过测试，不代表代码正确；
> 测试符合旧实现，不代表测试正确；
> 只有符合 SSOT 的外部行为，才有资格继续存在。

SSOT 最终要求的是：模型侧只有 `fork/join/list/verdict` 等极小 DSL，实现侧使用 F# Structured Flow、资源作用域、普通分支和尾递归表达程序；禁止重新制造 Stage、Phase、Lease、Owner、Coordinator 等人工程序计数器。

#### 2. 当前战场判断

按本次 Repomix 快照静态统计：

|  |  |  |
| :--- | :--- | :--- |
| 区域 | 规模 | 判断 |
| `src/` | 约 522 个文件、5.17 万行 | 旧生产实现，主体是负债 |
| `tests/` | 约 290 个文件、4.19 万行 | 大量旧实现耦合测试 |
| `e2e/` | 约 97 个文件、1.75 万行 | 混合了高价值 TestKit 与旧行为测试 |
| `integration/` | 约 12 个文件、1760 行 | 多数绑定旧插件表面 |
| `next/` | 约 82 个文件、1.44 万行 | 上一版重写与新 Agent DSL 混合体 |
| `tests-next/` | 约 39 个文件、4750 行 | 部分新资产，部分已经过时 |

旧测试基线记录了约 11,800 个断言，但其中相当一部分在证明 `Lease`、`Generation`、`Nudge`、`Todo`、`Actor`、`Journal`、`StateMachine` 等旧概念按设计运行。**断言数量不是资产价值，能够证明最终行为才是。** 当前仓库的目录和测试结构也清楚显示：旧生产、旧测试、新旧两代 `next` 实现正在同时存在。

#### 3. 当前最大的危险

最大的危险不是旧 `src/`。旧代码丑，但大家知道它是旧代码。

最大的危险是：

> **把当前 **`next/`** 当成最终架构，在上一代 Journal/Driver/PromptProtocol 重写上继续添加 Companion、ForkRuntime 和 Orchestrator。**

当前 `next/Wanxiangshu.Next.fsproj` 同时编译：

- `Journal/*`
- `Session/Driver*`
- `Inbox`
- `PromptProtocol`
- `Script*`
- `SessionFlows`
- `Fact/Fold/Outcome`
- 新的 `Companion`
- 新的 `ForkRuntime`
- 新的 `Fallback`
- 新的 `ReviewGuard`
- 新的 `Orchestrator`

这不是彼岸，而是两艘船焊在一起。

## 二、资产、矿石与负债

以后不再使用“这个测试很多，所以要保留”的判断方法。

统一分成四类。

### A 类：黄金资产——可以原样或轻改搬迁

#### A1. OpenCode E2E TestKit

以下基础设施是真正的战略资产：

```
testkit/opencode/isolated-env.js
testkit/opencode/process-host*.js
testkit/opencode/event-probe*.js
testkit/opencode/event-shape.js
testkit/opencode/strict-mock-*.js
testkit/opencode/scenario*.js
testkit/opencode/diagnostics*.js
testkit/opencode/stability-checker.js
testkit/opencode/tests/gate-*.mjs
```

它们提供：

- 每场景独立 HOME、XDG、workspace；
- 独立 mock provider；
- 独立 OpenCode 进程；
- 严格 FIFO LLM 请求期望；
- SSE 事件探针；
- 不依赖固定 sleep 的条件等待；
- PID、端口和子进程泄漏检测；
- 失败后的消息、事件、请求、进程树、文件诊断。

这些是**测试基础设施**，不是旧万象术领域模型。应迁往：

```
testkit/opencode/
```

并做到：

```
TestKit 不 import src/
TestKit 不 import next/
TestKit 只依赖 OpenCode 公共行为和 Node 标准库
```

当前 gate 测试已经覆盖环境隔离、严格 mock、SSE 重连、无固定 sleep、进程泄漏和诊断收集，这些都应保留。

#### A2. Host 调研资料

```
host-docs/
```

保留为事实调查资料，用于：

- 投影 hook；
- message/part 身份；
- assistant terminal；
- abort；
- tool schema；
- session prompt；
- compaction 关闭；
- 子 session 创建和事件接收。

它们是研究证据，不是规范。Host 版本变化后可以重新验证，但不应删除。

#### A3. Flow 内核思想与部分测试

`next/Kernel/Flow.fs` 的基本方向正确：

```
Flow<'ctx, 'error, 'a>
= 'ctx -> CancellationToken -> Task<Result<'a, 'error>>
```

可保留的不是每一行实现，而是这些契约：

- `Bind` 只负责短路和传值；
- `OperationCanceledException` 不伪装成业务错误；
- `use!` 管理异步资源；
- 尾递归表达重试；
- 不创建 Flow AST；
- 不创建解释器和 Stage Registry。

`tests-next/Flow/FlowTests.fs` 可以作为合同种子，但需要重新审查资源释放异常、取消和 semaphore 行为。

#### A4. Process 与 Git 的极少量原语

可保留作为种子：

- `Command` 数据类型；
- `Deadline` 的绝对时间思想；
- spawn 前后泵的测试场景；
- `hanging-test.js`；
- Large 全局 semaphore 的概念；
- 万象阵旧 Git harness 中创建 worktree、检测 dirty、rebase、ff 的场景构造方式。

这里保留的是**外部动作和故障场景**，不是旧 Process、Squad 或 Scheduler 实现。

### B 类：银矿资产——只提炼场景，不搬代码和类型

这些测试中包含真实踩坑经验，但断言对象已经过时。

#### B1. Subagent/Subsession 测试矿

重点提炼：

```
SubsessionPhysicalIsolationTests
SubsessionConcurrentCancelTests
SubagentCleanupCompletionTests
SubagentOutputTranscriptTests
SubsessionTranscriptBoundaryTests
SubagentPromptAbortTests
SubagentSpawnTests
SubsessionEvidenceRaceTests
```

提炼成新行为：

- listener 必须先于 prompt send；
- 极快完成不能丢；
- 父 session 取消后子 session 有界清理；
- 子代理 A 版正文提取正确；
- sibling 物理隔离；
- terminal 只完成一次；
- 迟到事件不造成重复 completion。

禁止继承：

- SubsessionActor；
- Evidence；
- Draining；
- Decision State；
- Reconciliation State；
- Generation；
- PendingEpoch。

#### B2. Executor/PTY 测试矿

重点提炼：

```
ExecutorSpawnPathTests
ExecutorTests
ExecutorFormatCoverageTests
PtyReadThrottleTests
p0-canary-tests-fuzzy-executor.js 中的进程场景
p0-canary-tests-pty.js
```

提炼成：

- spawn 后立即安装 stdout/stderr pump；
- 完整输出进入 spool；
- 200KB 分块摘要；
- `3 × estimated_running_secs` 唯一 deadline；
- SIGKILL 结束进程树；
- Medium 并发不受限；
- Large 全进程单例；
- PTY write/read/signal；
- dispose 后无孤儿。

禁止继承：

- fuzzy pipe；
- JS/Python in-process executor；
- 多层 timeout；
- 输出先截断再“摘要”；
- throttle 状态机。

#### B3. Fallback 测试矿

重点提炼：

```
ErrorClassifyTests
FallbackAgentAndModelInjectionTests
FallbackAgentAndModelTests
ModelResolutionTests
```

只保留：

- 如何选择 A/B 模型；
- 模型参数是否注入正确；
- 哪些 provider 错误算一次失败；
- session 内失败计数是否持续。

全部删除：

```
FallbackLease*
RetryDispatchGovernor*
ContinuationDispatch*
FallbackPhase*
EpisodeIdentity*
AcceptanceUnknown/Reconcile*
Fallback EventStore
```

#### B4. Review 测试矿

重点提炼：

- 第一次 `REVISE` 立即生效；
- 第一次 `PERFECT` 请求确认；
- 同一 Git tree 第二次 `PERFECT` 才通过；
- tree 改变后确认失效；
- reviewer 结束但未调用 verdict 时，nudge 同一个 reviewer；
- manager 想结束但未获双 PERFECT 时，nudge 同一个 manager。

旧测试已经包含“双 PERFECT challenge”的行为雏形，但当前实现仍混杂 Registry、Pending Review、Todo 和 replay。只搬场景，不搬这些容器。

#### B5. 文件工具、Browser 和角色权限测试矿

可提炼：

- Unicode；
- 空文件；
- 路径边界；
- write/edit 原子性；
- 浏览失败；
- 网络异常；
- 角色可见工具快照；
- 工具 schema 精确性；
- Coder 一次性 Inspector。

禁止把旧的全局工具注册表搬进来。

### C 类：负债——测试与实现一起删除

下列东西即使有几千个测试，也不进入新架构：

#### C1. 旧控制状态

```
Stage
Phase
Lease
Owner
Generation
Coordinator
Governor
Actor
Registry-driven workflow
SessionControl
Subsession state machine
ReviewSession state machine
Fallback state machine
```

#### C2. 旧领域

```
Todo SSOT
select_methodology
Methodology catalog
通用 Nudge
Caps
fuzzy_find / fuzzy_grep / fuzzy_continue
旧 ContextBudget
官方 compaction 集成
万象阵 DAG / wave / scheduler
Squad HTTP 控制面
EventStore 驱动工作流
```

#### C3. 旧恢复幻想

```
把执行到哪个 Stage 持久化
把 owner/lease/generation 当作恢复依据
恢复 pending continuation
跨 Runtime 实时协调
把日志投影当作正在运行的程序
```

Git 历史已经是墓地，不再建立 `legacy/` 目录保存尸体。

### D 类：冻结资产——不参与当前战争

Mux、OMP、Mimocode 相关实现和测试：

- 立即冻结；
- 不重构；
- 不为了复用而污染新 Kernel；
- 不作为 OpenCode 新架构的切换门禁；
- OpenCode 正式稳定后，从主干删除旧实现；
- 将来需要支持其他宿主时，重新从 Host Adapter 接入，不从旧代码“升级”。

## 三、当前 `next/` 的具体清算

### 1. 可留下的种子

暂时保留并重新审查：

```
next/Kernel/Flow.fs
next/Kernel/Identity.fs

next/Process/Command.fs
next/Process/Deadline.fs

next/Session/Companion.fs       # 只作场景种子
next/Session/ForkRuntime.fs     # 只作场景种子
next/Session/Fallback.fs        # 只作场景种子
next/Session/ReviewGuard.fs     # 可保留纯函数核心
next/Orchestrator.fs            # 只保留 Port 和场景思想
```

“保留种子”不等于原样保留实现。

### 2. 应立即从 `next/` 清出的上一代架构

```
next/Journal/**

next/Kernel/Fact.fs
next/Kernel/Outcome.fs
next/Kernel/SendOutcomeMap.fs

next/Session/Driver.fs
next/Session/DriverDispatch.fs
next/Session/Inbox.fs
next/Session/PromptKey.fs
next/Session/PromptProtocol.fs
next/Session/Script.fs
next/Session/ScriptTypes.fs
next/Session/ScriptViews.fs
next/Session/SessionFlows.fs
next/Session/ChildFlows.fs
next/Session/Review.fs

next/OpenCode/Gateway.fs
```

原因不是这些代码“质量差”，而是它们在解决已经被 SSOT 删除的问题：

- Journal；
- Driver；
- Prompt 幂等协议；
- 事件 Fold；
- 本地 Epoch；
- Script interpreter；
- 旧 review facts。

### 3. 当前 Companion 的偏差

当前实现使用：

```
TranscriptEvent.Cursor
BlogCheckpoint.Watermark
连续 cursor delta
按消息列表 index 替换前缀
```

最终 SSOT 要求：

```
本次 canonical JSON 投影输入
vs
上次成功投影输出/基线
的 JSON 层 delta
```

因此应重写为：

```
type ProjectionSnapshot = JsonValue
type BlogText = string

type CompanionMemory =
    { LastSuccessfulProjection: ProjectionSnapshot option
      CurrentB: BlogText option
      BloggerBusy: bool
      ReplacementActive: bool }
```

这里的字段都是当前资源事实，不是状态机阶段。

Y 忙时：

```
不取消
不排队
不推进基线
本次跳过
```

下次 Y 空闲时，新 delta 自然包含此前跳过的变化。

### 4. 当前 ForkRuntime 的偏差

当前实现：

- busy agent 返回 `ForkError.Busy`；
- `Join()` 非阻塞，空则立即 `Empty`；
- 存在 `Join(agentId)`；
- 还没有真正接入 OpenCode child session/prompt；
- 没有父 session 资源作用域。

最终应是：

```
fork(role, prompt)
→ 创建 child
→ 注册 terminal listener
→ fire prompt
→ 立即返回 AgentId

fork(existingId, prompt)
→ fire-and-forget nudge
→ 不返回 Busy
→ 不管理 prompt queue

join()
→ 等任意 child/PT Y operation completion
→ 不允许指定 child
```

当前 completion 先入邮箱、再标 idle 的思想可以保留，但 API 必须重写。

### 5. 当前 Process 的偏差

当前 ProcessPump 会先截断 stdout/stderr，ProcessBudget 再根据已经截断的字符串计算大小，并把截断内容写入临时文件。这无法实现“完整输出超过三倍后交给 Executor 摘要”。

此外，目前是 SIGTERM 路径，不是最终要求的 SIGKILL 进程树；错误分类还通过异常字符串猜测。当前 200KB 也被当成 spool threshold，而最终定义是摘要分块大小。

必须改成：

```
spawn
→ 立即启动无损 byte pump
→ 内存保留小前缀
→ 超阈值切 spool
→ 到唯一 deadline
→ SIGKILL process tree
→ await pump EOF
→ 按 200KB 分块
→ Executor Agent map
→ Executor Agent reduce
→ Inspector 获得摘要
```

### 6. 当前 ReviewGuard 的判断

纯函数：

```
同一 tree 连续两次 PERFECT 才 Confirmed
tree 变化重置
REVISE 重置
```

方向正确，可以留下。

但现在缺少：

- reviewer terminal 无 verdict 的 nudge 程序；
- manager finish guard；
- verdict tool 的完整运行协议；
- reviewer A 版正文与结构化 verdict 分离。

`Review.fs` 中的 ReviewFact、Todo、轮数和 Journal 应删除。当前 `ReviewGuard.fs` 只能算一块正确砖，不是完整建筑。

### 7. 当前 Orchestrator 的偏差

当前 `ForkManager` 实际 await `RunManager`，所以并非异步 fork；`JoinPublished(managerId)` 还能指定 manager；并保留显式 `ManagerStatus` 状态枚举。

它还没有完整表达：

- fork 立即返回；
- completion mailbox；
- manager 自动 ReviewGuard；
- candidate commit；
- rebase 冲突交回同一个 manager；
- rebase 后双 PERFECT；
- 串行 ff；
- 发布后才向 orchestrator join 返回。

可保留 GitPort/ManagerPort 的依赖倒置思想和串行 publish semaphore，但主程序应整体重写。

### 8. 当前 Architecture Gate 也已经过期

现有 `tests-next/Gates` 仍可能把 `Fork`、`Nudge` 等词视为禁止项，而新 SSOT 明确要求：

- `fork` 是核心 DSL；
- 对 existing agent 的 fork 就是 nudge；
- ReviewGuard 需要 nudge。

所以 Architecture Gate 不是越严格越好。它必须禁止**旧结构**，而不是禁止新领域词汇。

新门禁应禁止：

```
ReviewPhase
FallbackPhase
JoinOwner
NudgeLease
CompactionGeneration
SessionActor
SubsessionActor
WorkflowRegistry
Journal-driven execution
TodoState
Methodology
SquadWave
```

不能禁止：

```
fork
join
nudgeExisting
completionChannel
ReviewGuard
```

## 四、最终目标目录

到达彼岸时，主生产结构控制为：

```
next/
  Core/
    Flow.fs
    Identity.fs

  Agent/
    Types.fs
    Roles.fs
    ForkRuntime.fs
    Companion.fs
    Fallback.fs
    CompletionMailbox.fs

  Process/
    Command.fs
    Runner.fs
    Pump.fs
    Spool.fs
    ExecutorSummarizer.fs
    Pty.fs

  Review/
    Verdict.fs
    Guard.fs

  Orchestrator/
    Program.fs
    Git.fs
    Worktree.fs

  Host/
    OpenCode/
      Types.fs
      Projection.fs
      Sessions.fs
      Events.fs
      Tools.fs
      Plugin.fs

  Wanxiangshu.Next.fsproj

testkit/
  opencode/
    isolated-env.js
    process-host*.js
    event-probe*.js
    strict-mock-*.js
    scenario*.js
    diagnostics*.js
    stability-checker.js

tests-next/
  Contract/
  Core/
  Host/OpenCode/
  Agent/
  Companion/
  Process/
  Review/
  Pty/
  Orchestrator/
  E2E/
```

最终不出现：

```
Journal/
Driver/
PromptProtocol/
SessionActor/
SubsessionActor/
StateMachine/
Todo/
Methodology/
Squad/
```

## 五、迁移总纪律

### 纪律一：搬行为，不搬目录形状

错误做法：

```
旧 SubsessionActor
→ 新 ImprovedSubsessionActor

旧 FallbackCoordinator
→ 新 SimpleFallbackCoordinator
```

正确做法：

```
旧测试里发现“快速完成不能丢”
→ AG-JOIN-FAST-COMPLETION

旧测试里发现“父取消必须清理子进程”
→ AG-CANCEL-PARENT-SCOPE

旧测试里发现“第一次 PERFECT 需要确认”
→ REVIEW-DOUBLE-PERFECT
```

旧 KISS 文档本身已经指出：迁移应从旧测试中提取行为 ID 和外部结果，而不是复制旧 module、Stage、Coordinator 或日志布局。

### 纪律二：旧实现只当黑盒 Oracle

旧插件可以在独立场景中回答：

- OpenCode 实际如何调用插件；
- 某种事件形状如何出现；
- 旧用户行为如何表现。

禁止：

- 新代码 import `src/`；
- 新旧共享状态；
- 新旧双写；
- 一个 session 同时运行新旧插件；
- 用 feature flag 在一次运行里混合两套 Runtime。

### 纪律三：每个旧测试只有三种结局

建立 `MIGRATION.md` 总账：

|  |  |
| :--- | :--- |
| 字段 | 意义 |
| Behavior ID | 新行为编号 |
| SSOT 条款 | 它证明哪一条最终要求 |
| Legacy 来源 | 旧测试或 E2E 场景 |
| Disposition | Keep TestKit / Port Behavior / Obsolete |
| New test | 新测试位置 |
| Implementation owner | 唯一新模块 |
| Proof | 测试名称或 E2E 证据 |
| Deletion gate | 何时允许删除旧资产 |

没有第四种“暂时全保留”。

### 纪律四：新测试不继承 11,800 断言目标

新的覆盖标准是：

```
SSOT 行为条款是否全部有唯一 Behavior ID
每个 Behavior ID 是否有最低层正确测试
关键跨边界行为是否有真实 OpenCode E2E
故障与取消是否有场景
```

不是“新测试断言数量必须超过旧版”。

## 六、从脚下到彼岸的完整战役

### 战役 0：停火、立宪、封存

#### 目标

阻止旧系统和当前 `next/` 继续同时生长。

#### 动作

1. 给当前代码打不可变 tag，例如：

```
legacy-before-agent-dsl
```

1. 在主干新增：

```
MIGRATION.md
```

1. 写明 SSOT 裁决顺序。
2. 冻结：`src/``tests/`旧 integrationMux/OMP/Mimocode
3. 旧实现只允许：阻止数据损坏的紧急修复；修复会阻止迁移测试运行的 host 兼容问题。
4. 禁止任何“顺便重构旧代码”。

#### 必须测试

- 旧插件仍能构建，作为迁移期间黑盒 Oracle。
- 当前 tag 可复现。

#### 出口

- 所有人知道新功能只能进入清理后的 `next/`。
- `MIGRATION.md` 已建立行为总账。

#### 禁止

- 给旧 Fallback/Nudge/Subsession 增加新字段；
- 修旧测试以提高断言数；
- 为新旧兼容设计 adapter。

### 战役 1：抢救 TestKit

#### 目标

先把测试武器从旧军营搬出来，防止清理生产代码时一起炸掉。

#### 动作

1. 创建：

```
testkit/opencode/
```

1. 搬迁并改名：isolated env；ProcessHost；EventProbe；StrictMockProvider；Scenario；Diagnostics；StabilityChecker。
2. 所有插件路径由参数注入。
3. TestKit 不知道“万象术”领域类型。
4. 建立两个自测场景：无插件 OpenCode；旧插件 OpenCode。
5. 保留严格 mock 默认开启。
6. 保留无固定 sleep 的事件等待。
7. 每个 scenario dispose 后检查：OpenCode PID；子进程；端口；mock server；临时目录。

#### 必须测试

```
TESTKIT-ENV-ISOLATION
TESTKIT-STRICT-FIFO
TESTKIT-SSE-RECONNECT
TESTKIT-EVENT-EXISTING-IMMEDIATE
TESTKIT-PID-LEAK-DETECTED
TESTKIT-PORT-LEAK-DETECTED
TESTKIT-DIAGNOSTICS-COMPLETE
```

#### 出口

TestKit 可以在不知道 `src/` 和 `next/` 类型的情况下启动任意插件。

#### 禁止

- 搬旧 provider 里与 nudge/todo/fuzzy 绑定的 matcher；
- 在 TestKit 里加入新架构业务逻辑。

### 战役 2：清空假彼岸

#### 目标

把当前 `next/` 从“两代架构混合物”清成真正的 Agent DSL 工地。

#### 动作

1. 删除前述 `next/Journal`、Driver、PromptProtocol、Script 和旧 Review。
2. 重写 `Wanxiangshu.Next.fsproj`，只编译最小种子。
3. 删除对应 `tests-next`：Journal 测试；SessionProtocol；Driver vertical slice；Gateway boot；旧 review flow。
4. 重写 `GuideContract`。
5. 重写 Architecture Gate。
6. 当前插件若因清理不能工作，允许暂时只有空插件入口。

#### 新 GuideContract 只允许定义

```
Flow
AgentId
AgentRole
AgentHandle
RunCompletion
CompanionMemory
ModelSide
FallbackMemory
CommandRequest
ReviewVerdict
ReviewGuardResult
OrchestratorHandle
```

#### 必须测试

```
ARCH-NO-LEGACY-IMPORT
ARCH-NO-JOURNAL
ARCH-NO-DRIVER
ARCH-NO-PROMPT-PROTOCOL
ARCH-NO-WORKFLOW-STATE
ARCH-NO-TODO
ARCH-NO-METHODOLOGY
ARCH-NO-OFFICIAL-COMPACTION
GUIDE-CONTRACT-COMPILES
```

#### 出口

`next/` 可以很少、可以暂时不能完成用户任务，但结构上必须已经站在正确大陆。

#### 禁止

- 为保持现有 `tests-next` 绿色而留下旧模块；
- 用 `[<Obsolete>]` 假删除；
- 创建 `LegacyCompatibility.fs`。

### 战役 3：重建 Flow 和真实资源基座

#### 目标

建立所有 DSL 共用但不含业务的极小执行内核。

#### 动作

实现并验证：

```
Return
ReturnFrom
Bind
Zero
Delay
Combine
TryWith
TryFinally
Using
While
For
run
```

另外提供普通函数：

```
map
mapError
bind
attempt
parallel
```

`parallel` 只接受相互独立的 Flow，不能共享可变 Context。

#### 必须测试

- Error 短路；
- throw 保持 throw；
- cancellation 保持 OCE；
- `DisposeAsync` 在成功、Error、throw、cancel 下都被 await；
- cleanup 失败不会被静默吞掉；
- 尾递归 10,000 次不依赖 Stage；
- semaphore 在异常下释放；
- listener/send 时序可以用结构化程序明确表达。

#### 出口

之后所有业务都写成可读程序，而不是 reduce/action/state transition。

#### 禁止

```
Flow AST
解释器
动态 Stage
Workflow Registry
序列化 continuation
通用 EventBus
```

### 战役 4：OpenCode Host 生死 Spike

这是整个项目的第一道生死关。

#### 目标

用真实 OpenCode 证明 SSOT 所依赖的宿主能力确实存在。

#### 必须证明

##### 投影

1. 每次 LLM 请求前能获得完整 outbound message JSON。
2. 可以生成稳定 canonical JSON。
3. 可以关闭官方 compaction。
4. 可以在投影阶段用一条 B 消息替换历史前缀。
5. replacement 一旦启用，后续每次请求都继续应用。
6. 未替换的当前尾部仍保留。

##### Child session

1. 可以创建指定角色和模型的 child session。
2. 可以先注册 terminal listener，再发送 prompt。
3. prompt 可以 fire-and-forget。
4. busy child 再收到 prompt 时，OpenCode 会在后续请求中吸收。
5. child A 版正文可以可靠提取。
6. reviewer terminal 但没有 verdict 可以识别。

##### 生命周期

1. parent abort 后能够找到并关闭本侧 child。
2. assistant terminal、idle、abort 的实际事件序列被记录。
3. terminal listener 不依赖固定 sleep。

#### 产物

只实现：

```
Host/OpenCode/Projection.fs
Host/OpenCode/Sessions.fs
Host/OpenCode/Events.fs
Host/OpenCode/SpikePlugin.fs
```

#### 出口

以上 15 条真实 E2E 全部通过。

#### 失败处理

任何一条关键能力不存在：

- 停止业务开发；
- 修改 Agent DSL 与 Host 边界；
- 不允许用 Journal、轮询状态机或复杂协调器掩盖宿主事实。

### 战役 5：ForkRuntime

#### 目标

实现 Manager 最核心的异步 DSL。

#### 结构化主程序

```
fork new
→ create child with parent B
→ register handle
→ attach terminal listener
→ send prompt fire-and-forget
→ return AgentId

fork existing
→ send prompt fire-and-forget
→ return Nudged

child terminal
→ extract A
→ write completion mailbox
→ update in-memory handle

join
→ read any completion

list
→ snapshot active agents and PTYs
```

#### 必须测试

```
AG-FORK-RETURNS-BEFORE-CHILD
AG-LISTENER-BEFORE-SEND
AG-FAST-COMPLETION-NOT-LOST
AG-EXISTING-FORK-IS-NUDGE
AG-EXISTING-FORK-NEVER-BUSY
AG-JOIN-ANY
AG-JOIN-PHYSICAL-FIRST
AG-COMPLETION-ONCE
AG-A-VERSION-EXCLUDES-REASONING
AG-PARENT-CANCEL-CLOSES-CHILDREN
AG-SIBLING-ISOLATION
AG-LIST-SNAPSHOT
```

#### 出口

Manager 可以并行 fork 三个无工具 child，并通过三个 join 收回结果。

#### 禁止

```
Join(agentId)
RunId 协议平台
Prompt queue manager
AgentActor
AgentStateMachine
Evidence/Reconcile
Completion ordering coordinator
```

### 战役 6：Companion / Blogger

#### 目标

实现 B 版工作记录和零等待前缀替换。

#### 主程序

```
每次 X 即将请求模型
→ 构造 canonical JSON projection
→ 与 LastSuccessfulProjection 做 JSON delta
→ Y idle：发送 delta + blogger instruction
→ Y busy：跳过，不排队，不更新 baseline
→ 收到 Y assistant 正文：更新 CurrentB 与 baseline
→ 接近 X 上限：启用 remembered prefix replacement
→ 每次后续投影继续 B 替换
```

#### Y 自身压缩

```
Y 接近上限
→ 旧 B 作为本次输入
→ Y 输出新的 B'
→ B 定义只包含 Y assistant 输出
→ 旧 B 不再属于 B
```

不需要 CompactionState。

#### 必须测试

```
BLOG-CANONICAL-JSON-STABLE
BLOG-DELTA-JSON-LEVEL
BLOG-BUSY-SKIPS
BLOG-BUSY-DOES-NOT-ADVANCE-BASELINE
BLOG-NEXT-DELTA-INCLUDES-SKIPPED-CHANGES
BLOG-B-CONTAINS-ONLY-Y-OUTPUT
BLOG-FAILURE-NEVER-BLOCKS-X
BLOG-REMEMBERED-PREFIX-REPLACEMENT
BLOG-REPLACEMENT-REPEATS-EVERY-PROJECTION
BLOG-CURRENT-TAIL-PRESERVED
BLOG-SELF-REBASE-REMOVES-OLD-B
BLOG-NO-OFFICIAL-COMPACTION
```

#### 出口

构造一个超小 context limit 的真实 OpenCode 场景，证明 X 多次超过原始上下文长度仍持续工作，且官方 compaction 从未运行。

#### 禁止

```
PendingDeltaQueue
Watermark event protocol
Blogger cancellation
等待 Blogger 后再请求 X
独立 Compaction Coordinator
```

### 战役 7：角色与工具表面

#### 目标

让权限由静态装配决定，而不是运行时判断。

#### 最终矩阵

|  |  |
| :--- | :--- |
| 角色 | 模型工具 |
| Manager | `fork`、`join`、`list` |
| Orchestrator | `fork`、`join` |
| Coder | 文件工具、同步一次性 Inspector |
| Inspector | `executor` |
| Browser | `read`、网页工具 |
| Meditator | `read/glob/grep/inspector` |
| Reviewer | `read/glob/grep/inspector`、`verdict` |
| Executor | 无工具 |
| Blogger | 无工具 |

Companion：

- Manager：有；
- Coder：有；
- Orchestrator：有；
- 其他：无。

#### 必须测试

每个角色做完整工具名称 snapshot：

```
ROLE-MANAGER-EXACT-TOOLS
ROLE-CODER-EXACT-TOOLS
...
```

不仅测试“应该有”，也测试“不应该有”。

#### 出口

Manager 的请求中看不到 `read/write/edit/executor`。

#### 禁止

- 全局注册工具后靠 prompt 劝模型不用；
- 运行时 permission switch；
- 一个万能 AgentConfig。

### 战役 8：Process / Inspector / Executor

#### 目标

完成可信命令执行和大输出摘要。

#### 最终程序

```
Inspector.executor(request)
→ 如 Large，进入全局 semaphore
→ spawn process group
→ 立即安装 stdout/stderr pump
→ 等 exit 或唯一 deadline
→ deadline 到达则 SIGKILL process tree
→ await exit + pump EOF
→ 输出未超 3×estimate：直接返回
→ 超过：完整输出进入 spool
→ 按 200KB 分块
→ 每块启动一次性 Executor Agent 摘要
→ 最后一次 Executor reduce
→ 返回摘要
```

#### 冻结取舍

- 大 estimate 合法；
- 不 clamp；
- Medium 不限制并发；
- Large 同时一个；
- 唯一 timeout 是 `3 × estimated_running_secs`；
- SIGKILL 后不添加第二层 cleanup timeout；
- SIGKILL 无法收敛是实现 bug；
- 200KB 是摘要分块，不是总输出上限。

#### 必须测试

```
PROC-PUMP-INSTALLED-BEFORE-RETURN
PROC-OUTPUT-NOT-TRUNCATED-BEFORE-SPOOL
PROC-EXACT-THREE-X-DEADLINE
PROC-HUGE-ESTIMATE-ACCEPTED
PROC-MEDIUM-CONCURRENT
PROC-LARGE-SERIAL
PROC-LARGE-LEASE-RELEASED-ON-ERROR
PROC-SIGKILL-TREE
PROC-PUMP-DRAINS-AFTER-KILL
PROC-SPOOL-COMPLETE-BYTES
PROC-CHUNK-200KB
PROC-MAP-REDUCE-SUMMARY
PROC-EXECUTOR-HAS-NO-TOOLS
PROC-NO-ORPHAN
```

#### 出口

运行：

- 无限输出；
- stdout/stderr 同时写；
- fork 子进程；
- 忽略 SIGTERM；
- 超时；
- 500KB、10MB 输出；
- Large 并发；

全部无 hang、无孤儿。

### 战役 9：Coder、Inspector、Browser、Meditator

#### 目标

完成专业代理，不引入新的调度框架。

#### Coder

- 有 B；
- 文件工具；
- 每次 inspector 调用创建一次性 Inspector；
- 可并行启动多个 Inspector；
- Inspector 结果作为同步工具结果返回。

#### Inspector

- 无 B；
- 只持有 executor；
- command summary 作为 A 版正文。

#### Browser

- 只读本地文件；
- 只使用网页能力；
- 不写工作区。

#### Meditator

- 无方法论目录；
- 只做自由推理；
- 可 read/glob/grep/inspector；
- 不强制输出固定章节。

#### 出口

每个角色至少一个真实 OpenCode E2E，并验证工具表面、结果回传和清理。

### 战役 10：A/B Fallback

#### 目标

用一个尾递归函数替换整个旧 Fallback 帝国。

#### 唯一内存

```
type ModelSide =
    | A
    | B

type FallbackMemory =
    { Side: ModelSide
      Failures: int }
```

#### 规则

```
Failures 0，Side A
A 第一次失败  → Failures 1，重试 A
A 第二次失败  → Failures 2，永久切 B，立即尝试
B 第一次失败  → Failures 3，重试 B
B 第二次失败  → Failures 4，SessionDead
```

成功：

- 不清空 Failures；
- 不切回 A；
- 后续请求继续当前 Side。

#### 必须测试

```
FB-A-FIRST-RETRY-A
FB-A-SECOND-SWITCH-B
FB-SWITCH-PERMANENT
FB-B-FIRST-RETRY-B
FB-FOURTH-SESSION-DEAD
FB-SUCCESS-KEEPS-FAILURE-COUNT
FB-SUCCESS-KEEPS-SIDE
FB-PER-SESSION-ISOLATION
FB-ROLE-MODEL-INJECTION
```

#### 禁止

```
AcceptanceUnknown
Reconcile
FallbackPhase
RetryGovernor
Lease
Episode
Event log
```

### 战役 11：Reviewer 与 ReviewGuard

#### 目标

建立严格而极小的审查闭环。

#### Reviewer 程序

```
启动 Reviewer
→ 等 assistant terminal 或 verdict
→ REVISE：立即返回 revision
→ PERFECT 第一次：工具返回“请再次确认”
→ 同一 tree 第二次 PERFECT：Confirmed
→ assistant terminal 无 verdict：fork(existingReviewer, nudge)
→ 继续等待
```

#### Manager Guard

```
Manager assistant terminal
→ 检查当前 Git tree 是否已双 PERFECT
→ 是：允许完成
→ 否：向同一 Manager nudge
→ Manager 继续工作
```

#### 必须测试

```
REV-REVISE-IMMEDIATE
REV-FIRST-PERFECT-CHALLENGE
REV-SECOND-PERFECT-CONFIRMS
REV-TREE-CHANGE-INVALIDATES
REV-PERFECT-REVISE-PERFECT-NOT-CONFIRMED
REV-NO-VERDICT-NUDGES-SAME-REVIEWER
REV-A-TEXT-SEPARATE-FROM-VERDICT
MGR-FINISH-WITHOUT-REVIEW-NUDGED
MGR-FINISH-AFTER-CONFIRM-ALLOWED
MGR-EDIT-AFTER-CONFIRM-INVALIDATES
```

#### 禁止

```
Todo
Review Registry
Review StateMachine
Review Event Fold
通用 Nudge service
```

### 战役 12：PTY

#### 目标

统一外部 DSL，不统一内部资源类型。

#### 工具表面

仍然只有 `fork`：

```
fork(agent="pty", prompt="command")
fork(agent="<pty-id>", prompt="stdin content")
fork(agent="<pty-id>", prompt="")
fork(agent="<pty-id>", signal="TERM")
fork(agent="<pty-id>", signal="KILL")
```

`list()` 同时列 agent 和 PTY。

`join()` 返回任意 PTY operation completion 或 agent completion。

#### 必须测试

- spawn；
- write；
- empty prompt read；
- structured signal；
- resize；
- exit completion；
- agent/PTY 混合 list；
- parent abort；
- 无魔法字符串；
- 无重复 completion。

### 战役 13：Manager 纵向全链路

#### 目标

第一次证明新万象术作为产品成立。

#### 场景

```
用户要求修改一个真实 fixture
→ Manager 获得请求
→ Companion 形成 B
→ Manager 并行 fork Inspector 和 Coder
→ join 任意结果
→ nudge 已有 Coder
→ Coder 调一次性 Inspector
→ Coder 修改文件
→ Manager fork Reviewer
→ Reviewer 第一次 PERFECT
→ Reviewer 第二次 PERFECT
→ Manager Guard 放行
→ 用户得到最终结果
```

#### 强制检查

- Manager 从未看到文件工具；
- Manager 没有同步等待某个指定 child；
- Blogger 忙时不阻塞；
- 没有 todo；
- 没有旧 Journal；
- 没有官方 compaction；
- 场景结束无进程和 session 泄漏。

#### 出口

这是新插件的第一个 P0 canary。

### 战役 14：Orchestrator

#### 目标

用结构化程序取代整个万象阵。

#### 主程序

```
用户向 Orchestrator 发消息
→ 检查目标工作区 clean
→ fork ManagerJob
→ 创建 repo 外 worktree
→ Manager 自动进入 ReviewGuard
→ 初次双 PERFECT
→ 创建 candidate commit
→ 等待 integration semaphore
→ rebase 最新目标
→ 有冲突：把冲突交回同一个 Manager
→ 修复后重新双 PERFECT
→ 无冲突：rebase 后重新双 PERFECT
→ ff-only 发布
→ 清理 worktree
→ completion mailbox
→ Orchestrator join 收到 Published
```

#### 必须测试

```
ORCH-DIRTY-REJECTS-BEFORE-FORK
ORCH-FORK-RETURNS-IMMEDIATELY
ORCH-WORKTREE-OUTSIDE-TARGET
ORCH-MANAGERS-WORK-CONCURRENTLY
ORCH-PUBLISH-SERIAL
ORCH-INITIAL-DOUBLE-PERFECT
ORCH-REBASE-TO-LATEST
ORCH-CONFLICT-RETURNS-SAME-MANAGER
ORCH-POST-REBASE-DOUBLE-PERFECT
ORCH-VERIFY-FAIL-NO-FF
ORCH-FF-ONLY
ORCH-CLEANUP-WORKTREE
ORCH-JOIN-ANY-PUBLISHED
```

#### 禁止

```
DAG
Wave
Task scheduler
Squad state
HTTP control plane
MergeOrder recovery state
```

### 战役 15：新 E2E 军团

#### 目标

把所有最终行为编成新的覆盖清单。

#### 测试分层

##### L0：纯函数

- Flow；
- JSON delta；
- fallback；
- double PERFECT；
- role matrix。

##### L1：Port/Fake

- ForkRuntime；
- Process orchestration；
- Orchestrator program；
- Git failure branches。

##### L2：真实 Node/Process

- spool；
- SIGKILL；
- PTY；
- process tree；
- semaphore。

##### L3：真实 OpenCode + Mock LLM

- projection；
- child sessions；
- busy nudge；
- join；
- blogger；
- role tools；
- reviewer；
- manager。

##### L4：真实 Git E2E

- 并行 worktree；
- conflict；
- rebase；
- re-review；
- ff。

#### 稳定性门禁

所有 P0 场景：

```
连续运行 20 次
随机改变 LLM completion 延迟
随机改变 child 完成顺序
不允许 fixed sleep 决定正确性
结束后无 PID/port/session/worktree 泄漏
```

不要求系统人为恢复某种顺序；只要求结果语义正确。

### 战役 16：切换生产入口

#### 目标

让新插件成为唯一 OpenCode 生产路径。

#### 动作

1. 新 package build 完整通过。
2. 新插件完成全套 P0。
3. 修改唯一 package export：从旧 `src/Hosts/OpenCode/Plugin`指向新 `next/Host/OpenCode/Plugin`
4. README 重写为新产品行为。
5. 删除旧 `/loop`、todo、methodology、fuzzy、旧工具说明。
6. 不设置运行时 old/new feature flag。
7. 不做 live shadow 双跑。
8. 出问题只允许 Git revert 到切换前 commit。

#### 切换门禁

```
生产 build 无 src import
生产 package 无旧 OpenCode entry
工具表面 snapshot 精确
官方 compaction 关闭
Manager 全链路通过
Process 压力通过
ReviewGuard 通过
Orchestrator 通过
20× stability 通过
依赖树中无旧专用依赖
```

### 战役 17：总清算

#### 目标

让旧实现从主干物理消失。

#### 删除顺序

1. 旧 OpenCode `src/Hosts/OpenCode/**`
2. 旧 OpenCode integration
3. 旧 OpenCode unit tests
4. 旧 Fallback/Nudge/Subsession/EventStore Kernel
5. 旧 Runtime
6. 旧 Methodology/Todo/Caps/fuzzy
7. 万象阵 DAG、server、lock、wave harness
8. 冻结的 Mux/OMP/Mimocode 生产与测试
9. 旧 `wanxiangshu.fsproj`
10. 旧 build scripts 和不再使用的 npm 依赖
11. 旧 README 和文档
12. 清空后的空目录

保留：

```
next/
tests-next/
testkit/
AGENTS.md
host-docs/
```

旧代码只存在 Git tag 和 Git 历史。

#### 最终零残留检查

```
rg "SessionActor|SubsessionActor|FallbackPhase|Lease|Owner|Generation" next tests-next
rg "Todo|Methodology|fuzzy_|Squad|Wave" next tests-next
rg "experimental.session.compacting|autocontinue" next
rg "src/" next tests-next testkit
```

结果必须为空，或仅出现在专门的 forbidden-source architecture test 中。

### 战役 18：其他宿主重生

这不属于抵达彼岸的关键路径。

OpenCode 稳定一个正式周期后，再决定：

```
Host/Mux
Host/Omp
Host/Mimocode
```

每个宿主只能实现：

- projection；
- sessions；
- events；
- tools；
- plugin composition。

不得把旧宿主代码复制回来。

只有三个宿主都出现相同代码后，才抽取共享 Host abstraction。

## 七、旧测试迁移点名册

|  |  |
| :--- | :--- |
| 旧测试族 | 处理 |
| `EventLog*`、`ReplayEquivalence*` | 全部淘汰 |
| `FallbackLease*`、`Governor*`、`Continuation*` | 全部淘汰 |
| `Nudge*`、`Todo*`、`Methodology*` | 全部淘汰 |
| `ReviewSessionStateMachine*` | 全部淘汰 |
| `Subsession*` | 提炼隔离、取消、terminal、transcript 场景 |
| `Subagent*` | 提炼 fork、listener、completion、A 版场景 |
| `Executor*` | 提炼进程、输出、kill、spool 场景 |
| `Pty*` | 提炼 PTY 生命周期 |
| `Opencode*Codec*` | 作为 Host 调研和 fixture 矿，不搬旧 codec 类型 |
| `Integration*Loop*` | 改写为 Manager/Reviewer Guard E2E |
| `Integration*Todo*` | 淘汰 |
| `Integration*Methodology*` | 淘汰 |
| `p0-canary-ndjson/recovery*` | 淘汰 |
| `p0-canary-fuzzy*` | 淘汰 fuzzy；保留命令大输出场景 |
| `p0-canary-child-pty*` | 改写为 fork/list/join/PT Y |
| `p0-canary-compaction*` | 改写为 Blogger 投影替换 |
| 万象阵 harness | 只提炼 Git/worktree 场景 |
| OpenCode harness/TestKit | 黄金资产，独立搬迁 |

## 八、必须优先解决的五个技术风险

### 风险一：投影 hook 不够强

如果无法在每次请求前看到并替换完整 outbound JSON，Companion 主方案不成立。

处置：战役 4 最先验证，不得后移。

### 风险二：busy child 的 fire-and-forget prompt 行为与想象不同

必须验证：

- 是否接受；
- 何时进入后续 LLM 请求；
- 是否改变事件顺序；
- abort 时如何表现。

处置：真实 OpenCode E2E；不建立自制 queue 补偿。

### 风险三：terminal 与 verdict 时序

Reviewer 可能：

- verdict 先到；
- assistant terminal 先到；
- terminal 无 verdict；
- abort 后迟到。

处置：本地 listener + completion once；不建立 Review StateMachine。

### 风险四：SIGKILL 与 process tree

平台上的真正困难不是 F# 类型，而是：

- process group；
- descendant；
- pipe EOF；
- PTY；
- Windows/macOS/Linux 差异。

处置：真实进程压力测试，不用第二层 timeout 掩盖。

### 风险五：rebase 后语义变化

无文本冲突不等于无需复审。

处置：rebase 后强制重新双 PERFECT，之后才能 ff。

## 九、推荐提交序列

工程队不要自由发挥，按下面顺序提交：

```
01  chore: freeze legacy and add migration ledger
02  testkit: extract opencode isolated harness
03  next: remove journal driver and obsolete guide contracts
04  core: rebuild structured Flow kernel
05  spike: prove opencode projection and child-session contracts
06  agent: implement fork join list runtime
07  companion: implement JSON delta and remembered B projection
08  roles: install exact static tool surfaces
09  process: implement lossless pump spool and executor summarizer
10  agents: implement coder inspector browser meditator
11  fallback: implement cumulative A/B four-failure recursion
12  review: implement verdict and manager/reviewer guards
13  pty: integrate structured PTY fork operations
14  manager: complete reviewed vertical slice
15  orchestrator: implement worktree rebase re-review ff program
16  e2e: establish stability and leak gates
17  release: switch package entry to next
18  delete: remove legacy production tests docs and dependencies
```

任何提交不得同时：

- 新增新架构行为；
- 顺手重构旧实现；
- 修改 TestKit；
- 改写多个角色；
- 删除尚无新测试接管的旧行为。

## 十、每日作战规则

每个阶段开始前，工程师必须回答：

1. 本阶段对应哪条 SSOT？
2. 唯一行为 ID 是什么？
3. 最低层测试在哪里？
4. 真实 Host 边界是否需要 E2E？
5. 本阶段结束能删除什么旧资产？
6. 是否引入了一个只用来记“程序走到哪里”的字段？

最后一问只要答案为“是”，立即停工，改写为：

```
普通局部变量
match
尾递归
use!
Task
Channel
Semaphore
真实资源句柄
```

## 十一、最终胜利条件

不是“新版也能跑”。

不是“测试比旧版多”。

不是“旧版还留着以防万一”。

胜利条件是同时满足：

1. `AGENTS.md` 每条规范都有 Behavior ID。
2. 所有 Behavior ID 有正确层级的测试。
3. Manager 只看 `fork/join/list`。
4. Orchestrator 只看 `fork/join`。
5. Reviewer 只用结构化 `PERFECT/REVISE`。
6. Blogger 通过投影维护 B，忙时零阻塞。
7. 官方 compaction 完全关闭。
8. Process 只有一个 `3×estimate` deadline。
9. 大输出完整 spool，并按 200KB 摘要。
10. Fallback 只有 A/B 和累计四失败。
11. ReviewGuard 同时守 Manager 和 Reviewer。
12. PTY 使用结构化 fork 参数。
13. Manager worktree 经 rebase 后重新双 PERFECT。
14. 发布严格 ff-only。
15. `next` 中不存在旧 Journal/Driver/Actor/StateMachine。
16. 生产 package 不再引用 `src/`。
17. `src/`、旧测试和旧宿主实现从主干删除。
18. 整个旧时代只存在于 Git 历史。

## 十二、总指挥最终裁决

这次迁移不是把 5 万行旧代码慢慢修成 1 万行新代码。

正确路线是：

```
旧实现
  ├─ 抽走 TestKit
  ├─ 抽走真实故障场景
  ├─ 抽走 Host 调研事实
  └─ 其余冻结

当前 next
  ├─ 删除上一代 Journal/Driver 重写
  ├─ 保留 Flow 和少数纯函数种子
  └─ 按 SSOT 重建 Agent DSL

新实现
  ├─ Host spike
  ├─ fork/join
  ├─ Companion
  ├─ Process
  ├─ Agents
  ├─ Fallback
  ├─ Review
  ├─ PTY
  ├─ Manager
  └─ Orchestrator

切换生产
  → 删除旧时代
```

唯一的桥不是兼容层。

唯一的桥是：

> **独立 TestKit + 行为总账 + 一条条由新测试接管的外部契约。**

旧代码不渡河。

旧状态机不渡河。

旧断言数量不渡河。

只有经过鉴定的经验、场景和基础设施渡河。

工程队下一步不是继续实现 Companion、ForkRuntime 或 Orchestrator，而是严格执行 **战役 0、1、2**：冻结旧时代、抢救 TestKit、清空假彼岸。任何越级开发一律退回。

---

### 👤 User

事件溯源还需要吗? 我看 review guard 也需要状态持久化, 如果你把 CQRS 和 per process ndjson 砍掉,还怎么实现?

---

### 🤖 ChatGPT

**需要。你抓得对。**

我上一版路线图中“删除 `next/Journal/**`、`EventLog*` 全部淘汰”的命令**过度清算了，必须撤回**。

我把两件完全不同的东西混为一谈了：

1. 
   **应当删除的：用事件溯源保存程序执行到哪个 Stage/Phase。**
   
2. 
   **必须保留的：用事件溯源保存跨进程、跨重启仍然成立的领域事实。**
   

新架构仍然需要：

> 
> **Per-Runtime 单写 NDJSON + 启动时归并 Fold + 内存 Projection。**
> 

也就是精简后的 Event Sourcing/CQRS。原 KISS 设计已经把它限定为“每个 Runtime 只写自己的日志，启动时读取所有日志的稳定前缀，运行期间 read-your-writes，不做跨进程实时同步”。这个边界本身是正确资产，不应该被删除。

---

## 一、真正应当砍掉的不是事件溯源，而是“事件溯源驱动程序”

错误架构是：

```
读取 Projection
→ 看 ReviewPhase
→ 看 NudgeOwner
→ 获取 Lease
→ 判断 Generation
→ 发出 Command
→ 写下一 Stage
→ 等事件
→ 再驱动下一 Stage
```

这种实现把本来应该写成：

```
review {
    let! first = waitVerdict reviewer
    match first with
    | REVISE -> return NeedsRevision
    | PERFECT ->
        let! second = waitVerdict reviewer
        ...
}
```

的普通程序，人工展平为状态机。

正确架构则是：

```
结构化程序负责“接下来做什么”
事件日志负责“过去确实发生过什么”
Projection 负责“重启后我们已经知道什么”
```

所以准确的原则应当是：

> 
> **保留 Event Sourcing，删除 Event-Sourced Workflow。**
> 

---

## 二、三类状态必须彻底分开

### 1. 进程内资源状态：不持久化

这些东西进程死了就失效，写入 NDJSON 反而会骗人：

| 状态 | 存放位置 |
| :--- | :--- |
| 正在运行的 `Task` | 内存 |
| completion `Channel` | 内存 |
| 当前 `join()` waiter | 内存 |
| child session listener | 内存 |
| Process/PTY handle | 内存 |
| Blogger 当前是否忙 | 内存 |
| Large semaphore 是否持有 | 内存 |
| 当前函数递归到哪一步 | 调用栈 |
| Reviewer 当前请求是否正在等待 | `Task`/调用栈 |

禁止持久化：

```
ReviewPhase = WaitingSecondPerfect
JoinOwner = manager-1
BloggerStage = Summarizing
ProcessStage = Killing
NudgeLease = ...
CurrentFallbackStep = ...
```

这些只是程序计数器。

### 2. 跨重启领域事实：必须持久化

这些事实即使进程死掉也仍然成立：

| 领域 | 持久事实 |
| :--- | :--- |
| Companion | X 与 Y 的关联 |
| Companion | 最近一次成功的 B |
| Companion | JSON delta 基线 |
| Companion | 该 X 已启用永久前缀替换 |
| Fork | 某个 AgentId 对应哪个 OpenCode child session、角色和父 session |
| Review | Reviewer 对某个 Git tree 给出了 REVISE/PERFECT |
| Review | 某次 guard nudge 已被宿主接受 |
| Fallback | 某 session 已累计多少次模型失败 |
| Fallback | 该 session 已永久从 A 切到 B |
| Orchestrator | ManagerJob、worktree、candidate commit 的关联 |
| Orchestrator | 某 candidate 已发布到哪个目标 commit |
| 外部效果 | 宿主返回的 message/session/commit ID |

这些应该成为 append-only Fact。

### 3. 外部权威事实：不要复制成另一套真理

| 事实 | 权威来源 |
| :--- | :--- |
| 当前 Git tree hash | Git |
| 当前 ref 指向 | Git |
| child assistant 正文 | OpenCode transcript |
| tool call/tool result | OpenCode transcript |
| session 是否仍存在 | OpenCode |
| 进程是否还活着 | 操作系统 |
| PTY 是否退出 | 操作系统 |

Journal 可以存储这些事实的**引用和观察结果**，但不能建立一套与 Git/OpenCode 对抗的“真相”。

例如：

```
ReviewVerdictRecorded {
    ToolCallId = ...
    ReviewerSessionId = ...
    TreeHash = ...
    Verdict = PERFECT
}
```

它引用的是 OpenCode 中已经存在的工具调用，并记录调用时的 Git tree。

---

## 三、ReviewGuard 确实需要持久化，但持久化的是 Verdict 事实

不能只在内存里保留：

```
PendingPerfect treeHash
```

否则：

```
Reviewer 第一次 PERFECT
→ 插件返回“请再次确认”
→ OpenCode/插件进程重启
→ 第二次 PERFECT
```

系统会误认为这是第一次 PERFECT。

### 正确 Fact

```
type ReviewFact =
    | ReviewerLinked of
        managerSessionId: SessionId *
        reviewerSessionId: SessionId

    | VerdictRecorded of
        managerSessionId: SessionId *
        reviewerSessionId: SessionId *
        toolCallId: string *
        treeHash: GitTreeHash *
        verdict: ReviewVerdict

    | GuardPromptAccepted of
        targetSessionId: SessionId *
        guardKey: GuardKey *
        hostMessageId: HostMessageId
```

其中：

```
type ReviewVerdict =
    | REVISE
    | PERFECT
```

不需要持久化：

```
ConfirmedPerfect = true
ReviewRound = 3
WaitingForConfirmation = true
ReviewPhase = ...
```

这些全部由 Fold 推导。

### Review Projection

```
type ReviewView =
    | NoVerdict
    | NeedsRevision of lastVerdictId: string
    | AwaitingPerfectConfirmation of
        treeHash: GitTreeHash *
        firstVerdictId: string
    | PerfectConfirmed of
        treeHash: GitTreeHash *
        firstVerdictId: string *
        secondVerdictId: string
```

Fold 规则：

```
任何 REVISE
→ NeedsRevision

第一次 PERFECT(tree=T)
→ AwaitingPerfectConfirmation(T)

紧接着第二次 PERFECT(tree=T)
→ PerfectConfirmed(T)

PERFECT(tree=T1) 后又 PERFECT(tree=T2)
→ AwaitingPerfectConfirmation(T2)

任何情况下当前 Git tree ≠ confirmed tree
→ 对当前工作树视为未确认
```

最后一条甚至不需要写 `ReviewInvalidated` Fact。

只要每次 Guard 检查时重新读取 Git：

```
let! currentTree = git.treeHash worktree

match reviewProjection managerSessionId with
| PerfectConfirmed(confirmedTree, _, _)
    when confirmedTree = currentTree ->
        return ReviewSatisfied

| _ ->
        return ReviewRequired
```

所以这里仍然是结构化程序，不是 Review 状态机。

---

## 四、Reviewer Guard 如何跨重启继续

### 正常路径

```
Reviewer 调用 verdict(PERFECT)
→ 读取当前 Git tree T
→ 检查 toolCallId 是否已经记录
→ append + flush VerdictRecorded(PERFECT, T)
→ Fold 内存 ReviewView
→ 发现这是第一次 PERFECT
→ 工具返回“请再次确认”
```

第二次：

```
Reviewer 调用 verdict(PERFECT)
→ 读取当前 Git tree T
→ append + flush 第二个 VerdictRecorded(PERFECT, T)
→ Fold
→ 得到 PerfectConfirmed(T)
→ 工具返回确认成功
```

### 中间重启

```
启动
→ 读取所有 per-runtime NDJSON 稳定前缀
→ 时间线归并
→ Fold ReviewFact
→ 恢复 AwaitingPerfectConfirmation(T)
```

第二次 PERFECT 到来后自然确认。

没有：

- 
   ReviewActor；
   
- 
   ReviewSessionRegistry；
   
- 
   WaitingSecondPerfect Stage；
   
- 
   PendingReviewOwner；
   
- 
   ReviewGeneration。
   

### Manager Guard 重启

Manager assistant terminal 时：

```
managerGuard {
    let! currentTree = git.treeHash worktree
    let view = projections.review.ForManager managerSessionId

    match view with
    | PerfectConfirmed(tree, _, _) when tree = currentTree ->
        return AllowFinish

    | _ ->
        do! sendGuardNudge managerSessionId
        return ContinueManager
}
```

Guard 程序每次触发都重新查询持久 Projection 和当前 Git，不需要恢复“上次执行到第几行”。

---

## 五、Reviewer 不返回 verdict 的 Guard 也需要持久化边界

场景：

```
Reviewer assistant terminal
→ 没调用 verdict
→ Guard 应 nudge reviewer
→ prompt 发出后插件崩溃
→ 重启
```

如果完全没有持久记录，可能重复 nudge。

重复 nudge未必致命，但至少应做到可识别。

### 最小 GuardKey

```
type GuardKey =
    { TargetSessionId: SessionId
      TriggerMessageId: HostMessageId
      TreeHash: GitTreeHash
      Reason: GuardReason }

type GuardReason =
    | ReviewerMissingVerdict
    | ManagerMissingPerfectReview
```

发送成功后记录：

```
GuardPromptAccepted(
    targetSessionId,
    guardKey,
    returnedHostMessageId
)
```

再次处理同一 terminal message 时：

```
Projection 已有同一个 GuardKey
→ 不再发送
```

### 崩溃窗口

仍然存在：

```
宿主已经接受 prompt
→ 本进程尚未 append GuardPromptAccepted
→ 进程崩溃
```

这是经典的外部提交不确定窗口。

有两种 KISS 策略。

#### 策略 A：接受 Guard prompt 至少一次

Guard nudge 本身是幂等提醒，偶发重复可以接受：

```
send
→ 得到 HostMessageId
→ append GuardPromptAccepted
```

崩溃窗口最多导致一次重复。

这是最简单的第一版。

#### 策略 B：宿主支持稳定 provenance 时做 reconcile

```
append GuardPromptRequested(GuardKey)
→ 带 GuardKey marker 发送
→ append GuardPromptAccepted(GuardKey, HostMessageId)
```

启动发现 Requested 无 Accepted：

```
查询 OpenCode transcript 是否已有对应 GuardKey
→ 有：补写 Accepted
→ 无：重发
```

这仍然可以封装成一个结构化动词：

```
let! messageId =
    durableEffect guardKey {
        return! host.sendPrompt target prompt
    }
```

而不是把 Requested/Accepted/Unknown 暴露为整个业务流程的 Phase。

OpenCode 自身没有公开的通用 queued-message API，busy prompt 最终由宿主 Runner 排队，因此真实的 prompt 接受和重入行为仍需 Host spike 验证。

---

## 六、Companion 也离不开持久化

你要求：

> 
> 一旦 X 开始使用 B 替换前缀，以后每次投影都继续替换。
> 

如果只存在内存：

```
ReplacementActive = true
```

重启后就会忘掉，下一次又把完整原始上下文投影给 X。

至少需要：

```
type CompanionFact =
    | CompanionLinked of
        primarySessionId: SessionId *
        bloggerSessionId: SessionId

    | CompanionAdvanced of
        primarySessionId: SessionId *
        bloggerSessionId: SessionId *
        successfulProjection: JsonValue *
        bText: string *
        bloggerMessageId: HostMessageId

    | PrefixReplacementEnabled of
        primarySessionId: SessionId
```

Fold 得：

```
type CompanionView =
    { BloggerSessionId: SessionId
      LastSuccessfulProjection: JsonValue option
      CurrentB: string option
      PrefixReplacementEnabled: bool }
```

不持久化：

```
BloggerBusy
CurrentBloggerTask
PendingDelta
QueuedDelta
CompactionStage
```

Blogger 是否正在运行，启动后向 OpenCode 查询；旧进程里的 `Task` 没有恢复价值。

当前 SSOT 明确要求 Y 忙时不打断、不排队、不推进基线；下次 delta 自然包含跳过内容。这个行为只需要持久化“最后一次成功基线”，不需要 pending queue。

---

## 七、Fallback 也要持久化，否则“四次失败”会被重启洗白

你的规则是：

```
A 失败一次：重试 A
A 再失败：永久切 B
B 失败一次：重试 B
B 再失败：SessionDead
```

“永久切 B”和“每 session 累计四次”显然是跨请求、跨重启语义。

应记录原始失败事实：

```
type FallbackFact =
    | ModelAttemptFailed of
        sessionId: SessionId *
        requestId: RequestId *
        modelSide: ModelSide *
        errorClass: ErrorClass
```

Projection：

```
type FallbackView =
    { Failures: int
      Side: ModelSide
      Dead: bool }
```

可以纯推导：

```
Failures 0–1 → A
Failures 2–3 → B
Failures ≥4  → Dead
```

甚至不必另写 `ModelSideSwitched`，避免同一事实表达两次。

成功不写“清零”，因为你的规则不清零。

---

## 八、ForkRuntime 哪些需要持久，哪些不需要

### 持久化

```
type AgentFact =
    | AgentLinked of
        parentSessionId: SessionId *
        agentId: AgentId *
        childSessionId: SessionId *
        role: AgentRole
```

用途：

- 
   重启后重新构造 `list()`；
   
- 
   找到某 AgentId 对应的 child；
   
- 
   对 existing AgentId 执行 nudge；
   
- 
   重启后扫描 child transcript，发现已经 terminal 的结果。
   

### 不持久化

```
AgentHandle 当前 Task
terminal listener
completion Channel
某个 join 正在等待
child busy/idle 的本地缓存
```

重启时：

```
Fold AgentLinked
→ 查询 OpenCode child session
→ terminal：重新形成 completion
→ still running：重新安装 listener
→ missing：标记不可用
```

是否需要避免“已经 join 过的结果再次投递”，优先检查 Manager 的 OpenCode tool result 是否已经持久存在；必要时再增加：

```
CompletionDelivered of joinToolCallId * childSessionId
```

而不是持久化一个 completion queue。

---

## 九、Orchestrator 更需要 Durable Facts

否则进程在下面任一点崩溃：

```
worktree 已创建
candidate commit 已生成
rebase 已完成
post-rebase reviewer 已确认
ff 已完成
```

都会无法判断该清理、继续还是已经发布。

最小事实：

```
type OrchestratorFact =
    | ManagerJobLinked of
        orchestratorSessionId: SessionId *
        managerAgentId: AgentId *
        managerSessionId: SessionId *
        worktreePath: string *
        targetRef: string

    | CandidateCreated of
        managerSessionId: SessionId *
        commit: GitCommit

    | RebasedCandidateObserved of
        managerSessionId: SessionId *
        commit: GitCommit *
        targetHead: GitCommit

    | Published of
        managerSessionId: SessionId *
        candidate: GitCommit *
        resultingTargetHead: GitCommit
```

但恢复判断仍然要查询 Git：

```
Journal 说 Published(C → H)
Git target 当前包含 C
→ 已发布

Journal 无 Published
但 Git target 已包含 candidate
→ 补写 Published

worktree 存在且 candidate 未发布
→ 恢复同一个 ManagerJob

worktree 不存在且无 candidate
→ Job 失败
```

Git 仍然是权威，Journal 是可恢复的业务时间线。

---

## 十、CQRS 应保留，但不要建立“CQRS 平台”

正确实现规模很小。

### Write Side

领域动词自己验证并 append：

```
let recordVerdict input =
    review {
        let! tree = git.currentTree input.Worktree
        let fact =
            VerdictRecorded(
                input.ManagerSessionId,
                input.ReviewerSessionId,
                input.ToolCallId,
                tree,
                input.Verdict
            )

        do! journal.append fact
        return projections.review.ForManager input.ManagerSessionId
    }
```

### Read Side

启动 Fold 一次，之后 O(1) 查询：

```
type Projections =
    { Review: ReviewProjection
      Companion: CompanionProjection
      Fallback: FallbackProjection
      Agents: AgentProjection
      Orchestrator: OrchestratorProjection }
```

本 Runtime append 流程：

```
编码一行
→ append
→ flush
→ 更新内存 Projection
→ 返回成功
```

这是 read-your-writes。

### 不需要

```
CommandBus
QueryBus
Projection Worker
Event Subscriber
Saga Manager
Aggregate Repository
Generic AggregateRoot
动态 Handler Registry
```

所以“CQRS”只是一个清楚的读写分离原则：

- 
   写入 append-only Fact；
   
- 
   读取内存 Projection；
   
- 
   业务流程仍然是普通 F# 程序。
   

---

## 十一、Per-Runtime NDJSON 仍然是合适的物理模型

目录继续使用：

```
.wanxiangshu-next/
  runtimes/
    <runtime-id>.ndjson
```

规则保持：

1. 
   每个 Runtime 只写自己的文件。
   
2. 
   文件以 `CreateNew` 创建。
   
3. 
   每行自包含：
   
   
   schema version；
   
   
   RuntimeId；
   
   
   LocalSeq；
   
   
   ObservedAt；
   
   
   Fact。
   
   
   
4. 
   启动时记录每个文件稳定 byte frontier。
   
5. 
   只读取 frontier 以前内容。
   
6. 
   EOF 半行忽略。
   
7. 
   中间损坏只隔离该来源，不能 hang。
   
8. 
   本进程 append 后先 flush，再 Fold 到内存。
   
9. 
   不实时 tail 其他 Runtime。
   
10. 
   新 Runtime 启动时重新枚举、归并、Fold。
   

原 KISS 文档对这一模型的定义是 Lifetime Snapshot Isolation：本进程看到启动时的其他日志稳定前缀，并在生命周期内只实时看到自己的写入；这样避免 workspace 总锁、owner 转移、watcher 和 IPC 同步平台。这个设计仍然适合新 Agent DSL。

---

## 十二、最小 Fact 总表

第一版不要重新扩张成几十种 Fact。

```
type Fact =
    | Companion of CompanionFact
    | Agent of AgentFact
    | Review of ReviewFact
    | Fallback of FallbackFact
    | Orchestrator of OrchestratorFact
    | DurableEffect of DurableEffectFact
```

其中 `DurableEffectFact` 只用于有崩溃窗口的外部动作：

```
type DurableEffectFact =
    | EffectRequested of key: EffectKey
    | EffectAccepted of
        key: EffectKey *
        externalId: string
```

不是所有工具调用都写日志。

只有这些条件同时满足时才记录：

1. 
   有不可回滚的外部效果；
   
2. 
   进程可能在效果与本地记录之间崩溃；
   
3. 
   重复执行会造成真实问题；
   
4. 
   重启后产品承诺继续该工作。
   

---

## 十三、对上一版路线图的正式修正

以下命令撤回：

```
删除 next/Journal/**
EventLog* 全部淘汰
ReplayEquivalence* 全部淘汰
Architecture Gate 禁止 Journal
```

改为：

### 保留并瘦身

```
next/Journal/Envelope.fs
next/Journal/Writer.fs
next/Journal/Boot.fs
next/Journal/FactCodec.fs
next/Journal/Fold.fs
```

职责限定为：

```
Envelope
单文件 Writer
稳定前沿 Boot
Codec
跨 Runtime 确定性归并
纯 Fold
```

### 根据旧协议耦合程度重写或删除

```
next/Journal/GatewayTypes.fs
next/Journal/JournalFlows.fs
```

Journal 不应该知道：

- 
   Driver；
   
- 
   PromptProtocol；
   
- 
   Todo；
   
- 
   Review Phase；
   
- 
   Child Actor；
   
- 
   Session Stage。
   

### 保留的测试资产

```
FactCodecTests
JournalWriterTests
JournalBootFoldTests
JournalIsolationTests
JournalFoldTests
Journal corruption/torn-tail tests
Replay determinism tests
multi-runtime snapshot tests
```

删除的是测试中的旧 Fact 和旧 Projection，不是 Journal 基础设施测试。

### Architecture Gate 改为

允许：

```
Journal
Fact
Fold
Projection
PerRuntimeWriter
BootSnapshot
```

禁止：

```
JournalDrivenWorkflow
ReviewPhase
FallbackPhase
SessionStage
Lease
Owner
Generation
Actor
Coordinator
```

---

## 十四、最终架构一句话

```
Agent DSL / F# Structured Flow
    决定现在做什么
            │
            ▼
Durable domain verbs
    对外执行并写入事实
            │
            ▼
Per-Runtime NDJSON
    保存跨重启事实
            │
            ▼
Boot Fold / Memory Projection
    告诉结构化程序已经发生过什么
```

所以最终裁决是：

> 
> **事件溯源要保留，CQRS 要保留，Per-Runtime NDJSON 要保留。**
> 

但它们只能承担：

- 
   持久事实；
   
- 
   启动恢复；
   
- 
   read-your-writes；
   
- 
   跨 Runtime 启动归并；
   
- 
   审计和幂等依据。
   

它们绝不能承担：

- 
   调度程序；
   
- 
   保存调用栈；
   
- 
   保存当前 Stage；
   
- 
   决定下一步；
   
- 
   模拟正在运行的资源。
   

**ReviewGuard 正是事件溯源应该存在的正面案例：持久化两个原始 PERFECT verdict 及其 tree hash，通过 Fold 得到确认；而不是持久化一个 **`ReviewPhase = WaitingSecondPerfect`**，再让状态机驱动后续。**

---

### 👤 User

现在呢

---

### 🤖 ChatGPT

## 总裁决

**现在比上一版好很多，但仍远未完成。**

工程师不是偷懒：旧 `src/`、旧测试主体和旧万象阵已经清出主干，OpenCode TestKit 被独立出来，`next/` 也删掉了 Driver、PromptProtocol、JournalFlows 等上一代“假彼岸”。事件溯源方向亦已恢复，出现了 Companion、Agent linkage、Review、Fallback、Orchestrator 等持久事实。

但工程师犯了另一种严重错误：

> 
> **战役 0～2 做完后，没有闭合真实 Host 纵切，而是把战役 3～14 全部铺成了 Stub 骨架，然后提前切换生产入口、提前删除旧测试军火库，并用没有真正执行的测试证明自己。**
> 

当前版本应定性为：

> 
> **结构清场基本成功，Agent DSL 产品实现尚未开始闭合。**
> 

粗略判断：

| 维度 | 状态 |
| :--- | :--- |
| 旧架构去债 | 较好 |
| TestKit 抢救 | 较好 |
| Journal 基础设施 | 部分完成 |
| 真实 OpenCode Host | 未闭合 |
| 真实 fork/join | 未完成 |
| Companion | 未接线 |
| Process | 实现语义错误 |
| Review/Fallback | 只有局部纯逻辑 |
| Orchestrator | 模拟骨架 |
| 生产可用性 | 接近零 |
| 测试可信度 | 目前不可信 |

当前目录、项目文件、生产入口和测试结构支持以上判断。

---

## 一、值得肯定的部分

### 1. “假彼岸”确实清掉了

上一快照中的这些东西已经不在当前生产项目：

```
JournalFlows
GatewayTypes
Driver
DriverDispatch
Inbox
PromptProtocol
Script
SessionFlows
ChildFlows
旧 Review Flow
```

`next/Wanxiangshu.Next.fsproj` 当前只剩下 Flow、Journal 基础设施、Agent DSL 领域模块、Process、OpenCode Adapter 和工具模块。说明工程师确实理解了“结构化程序取代状态机”的总方向。

### 2. TestKit 成功脱离旧生产树

当前已有：

```
testkit/opencode/
```

环境隔离、严格 Mock Provider、SSE EventProbe、ProcessHost、诊断和泄漏检测都被保存下来。这是本轮最有价值的资产抢救成果。

### 3. Event Sourcing 被正确救回来了

现在有：

```
Journal/Writer.fs
Journal/Boot.fs
Journal/Envelope.fs
Journal/Fold.fs
Journal/AgentFacts.fs
Journal/AgentJournal.fs
```

同时 Fact 大体开始围绕真实跨重启事实：

- 
   Companion 基线、B 和 replacement；
   
- 
   Agent linkage；
   
- 
   Review verdict；
   
- 
   Guard prompt；
   
- 
   Fallback failure；
   
- 
   Orchestrator candidate/published；
   
- 
   Durable effect。
   

这比完全删除 Journal 正确得多。

### 4. 静态角色矩阵方向正确

`Manager` 只有 fork/join/list，Coder 有文件能力和 Inspector，Inspector 只有 exec，Executor/Blogger 无工具。这一层没有重新引入万能 AgentConfig 或动态权限状态机。

---

## 二、第一颗红雷：SSOT 已经不是完整真理源

当前 `AGENTS.md` 已作为唯一真理源，必须同时包含 Agent DSL 与 Event Sourcing/CQRS/Per-Runtime NDJSON 约束。

结果是：

- 
   早期文档曾写着“没有 fallback journal”；
   
- 
   `MIGRATION.md` 又写“durable facts correction，Journal 必须保留”；
   
- 
   当前实现选择了后者；
   
- 
   旧 `kiss-docs` 仍大量描述 Driver、PromptProtocol 和上一代架构；
   
- 
   README 又写了第三套表述。
   

这曾经形成多个相互冲突的材料源；现在裁决顺序只有：

```
AGENTS.md
MIGRATION.md
旧 kiss-docs
当前代码
```

这必须立刻修复。用户已经指定：

```
AGENTS.md = 最终真理版本
```

所以第一刀不是继续编码，而是把后续纠正补进 SSOT，并明确推翻其中冲突的早期 Assistant 建议。否则工程师下一轮仍然会“严格遵守错误条款”。

---

## 三、第二颗红雷：现在的测试是假绿

这是当前最严重的工程问题。

### 1. 根目录 `npm test` 没有执行 F# 测试

当前脚本是：

```
dotnet build tests-next/Wanxiangshu.Next.Tests.fsproj --no-restore
```

这是**编译测试项目**，不是运行测试。

之后只运行：

```
node testkit/opencode/tests/gate-testkit.mjs
```

它只测试 TestKit 自己。

因此以下测试实际上不在根 release gate 中运行：

```
FlowTests
JournalTests
ForkRuntimeTests
CompanionTests
ProcessRunnerTests
ReviewGuardTests
OrchestratorTests
ArchitectureGates
GuideContract
ManagerCanaryTests
```

### 2. Architecture Gate 若真正执行，当前代码会立即失败

Gate 规定每个 `.fs` 不超过 300 行，但当前至少有：

```
Agent/Programs.fs       348 行
Journal/AgentFacts.fs   428 行
Process/Runner.fs       421 行
Flow/FlowTests.fs       418 行
```

当前 release test 仍能“绿”，恰好证明 Gate 根本没跑。

这里重点不是 300 行限制是否合理，而是：

> 
> **测试声称存在的门禁，没有进入实际测试链。**
> 

### 3. 20× canary 很可能在测试旧构建产物

新生产入口是：

```
build/OpenCode/Plugin.js
```

但 TestKit 的插件解析器仍然搜索：

```
build/src/Hosts/OpenCode/Plugin.js
```

这正是旧版输出路径。

若本地 `build/` 没清理，canary 可能加载残留旧插件；若全新 checkout，则可能根本找不到新插件。

### 4. “Manager DSL canary”没有测试 Manager DSL

它让普通 OpenCode 模型直接调用：

```
write
```

写一个文件。

它没有验证：

- 
   Manager 只能看到 fork/join/list；
   
- 
   Manager fork Coder；
   
- 
   Coder 修改文件；
   
- 
   Manager join；
   
- 
   Reviewer 双 PERFECT；
   
- 
   Companion；
   
- 
   新 Journal；
   
- 
   新 ForkRuntime。
   

所以即使连续成功 20 次，也只能证明：

> 
> OpenCode + Mock Provider + write 工具大致能跑。
> 

不能证明新万象术能跑。

### 5. 部分测试主动固化了错误语义

`ManagerCanaryTests` 把下面行为当正确结果：

```
join 无结果时立即返回 Empty
```

但 SSOT 冻结的正式语义是：

```
join()
→ 等待任意一个完成结果
```

测试中还使用 `Task.Delay(20)` 等待后台任务，却又有“No sleep”门禁。

**总命令：在修好测试接线以前，任何“全部通过”“20× 稳定”“发布可用”的报告一律无效。**

---

## 四、Journal：方向正确，但现在只是装饰性持久化

### 1. 最致命问题：真实 Runtime 没有从 BootSnapshot 恢复

`AgentJournal.create` 当前执行：

```
创建新的 Writer
→ 写 RuntimeStarted
→ 从空 Projection fold 这一个事件
```

它没有：

```
Boot.boot
→ Fold 外部 Runtime 稳定前缀
→ 用恢复后的 Projection 创建 AgentJournal
```

测试中的“重启恢复”只是手工调用：

```
Boot.boot
Fold.apply
```

生产代码并没有这样启动。

所以目前所谓 durable fallback、durable review：

> 
> 写入文件可以；重启后真实 Runtime 不会使用恢复状态。
> 

### 2. Journal 没接入 Plugin

当前生产 `Plugin.initPlugin` 直接委托 `SpikePlugin`。

没有地方：

- 
   生成 RuntimeId；
   
- 
   Boot 所有日志；
   
- 
   创建 AgentJournal；
   
- 
   把 Projection 注入 Companion/ForkRuntime/Review/Fallback；
   
- 
   Dispose 时 flush。
   

因此整套 Journal 目前不参与产品运行。

### 3. 内存 Projection 重新变成 O(N)

以下字段随历史无限增长：

```
ManagerState.History: CandidateStatus list
OrchestratorProjection.PublishedCommits: string list
DurableEffectProjection.Effects: Map<EffectId, EffectStatus>
ReviewGuardProjection.AcceptedGuardKeys: Set<string>
```

这违反此前明确的：

> 
> 内存积分状态相对于事件历史长度有界。
> 

应改成最后状态或固定槽位：

```
每 Manager 当前 Candidate 状态
当前 Published 结果
每 GuardReason 最近 claim
当前未决 Durable Effect
```

审计历史留在 NDJSON，不复制进内存。

### 4. Verdict 没有防重复

`ReviewVerdictRecorded` 虽然记录 `ToolCallId`，Fold 却不使用它去重。

同一个 PERFECT tool call 若因 Hook 重复投递两次，会被计成“两次 PERFECT”，直接越过确认机制。

必须至少保留：

```
第一 PERFECT 的 ToolCallId
第二 PERFECT 的 ToolCallId
```

并要求二者不同。

---

## 五、OpenCode Host Spike 实际没有通过

当前 Plugin 仍然是：

```
initPlugin → SpikePlugin.initSpikePlugin
```

而 SpikePlugin：

- 
   注入旧式 `[CAPS: coder, inspector, browser]`；
   
- 
   暴露测试用 `projection/events/sessions` 对象；
   
- 
   没有注册 fork/join/list/verdict；
   
- 
   没有装配 Journal；
   
- 
   没有装配 Companion；
   
- 
   没有装配角色；
   
- 
   没有装配 Process；
   
- 
   没有装配 ReviewGuard。
   

更严重的是 `InjectedSessionPort.SendPrompt`：

```
宿主接受 user prompt
→ 立即 NotifyTerminal Completed
```

它把“prompt 已接受”伪装成“child assistant 已 terminal”。

所以现有 Spike 没有证明：

- 
   assistant terminal 事件；
   
- 
   A 版正文提取；
   
- 
   tool/reasoning 完成；
   
- 
   idle/abort 时序；
   
- 
   迟到事件；
   
- 
   busy nudge；
   
- 
   真实 completion。
   

它只证明了一个自造 Fake Port 的回调能够触发。

**战役 4 Host 生死 Spike 仍然是红色，后续所有 Agent 功能都没有资格验收。**

---

## 六、ForkRuntime：概念样机，不是真实实现

当前存在以下问题。

### 1. `join()` 语义错误

现在是：

```
if mailbox.Count > 0 then dequeue
else Error Empty
```

正式语义应是异步等待：

```
completionChannel.Reader.ReadAsync(ct)
```

### 2. existing agent 的 nudge 实现错误

目前 existing agent 再 fork 会：

```
startRun
→ 启动新的 childRunner Task
→ 更新 CurrentRunId
```

正确语义是：

```
向同一个 OpenCode child session fire-and-forget 发送 prompt
```

不应由 ForkRuntime 并发启动另一个模拟 runner。

### 3. 存在同步完成竞态

`startRun` 可能在 AgentRecord 写入 `Busy` 前快速完成：

```
completion 把状态尝试改为 Idle
→ 此时字典中还没有 AgentRecord
→ 外层随后写入 Busy
```

最终 Agent 永久显示 Busy。

### 4. Cancel 没有取消真实任务

`Cancel()` 只是：

- 
   把状态设为 Closed；
   
- 
   调 cleanup callback。
   

后台 Task 仍可完成并向 mailbox 写 completion。

### 5. 没有 durable linkage/reconcile

Journal 虽有 `AgentLinked`，ForkRuntime 从未写它，也没有从 Boot Projection 重新连接 child。

所以当前 ForkRuntime 应重新实现，而不是继续修补。

---

## 七、Companion：纯函数种子可留，运行机制尚不存在

正确部分：

- 
   Y busy 时跳过；
   
- 
   跳过时不推进 baseline；
   
- 
   Blogger 成功后同时更新 B 与 baseline；
   
- 
   失败不阻塞 X。
   

缺口：

1. 
   没有创建真实 Blogger Session。
   
2. 
   没有发送 blogger prompt。
   
3. 
   没有从 OpenCode assistant 正文提取 B。
   
4. 
   没有写 Companion Journal Fact。
   
5. 
   重启后没有恢复。
   
6. 
   `ReplacementActive` 没有接入真实投影 hook。
   
7. 
   仍通过人工 `watermarkIndex` 替换 `HostMessage`，没有从 canonical projection 计算实际前缀。
   
8. 
   `jsonDelta` 只比较顶层 key：
   
   
   数组变化时发送整个数组；
   
   
   key 被删除时可能完全检测不到；
   
   
   canonical 化依赖调用者，没有单一 canonical encoder。
   
   
   

因此当前 Companion 只能算：

> 
> 已验证 busy-skip 思路的内存原型。
> 

---

## 八、Process：这部分必须返工，不能验收

### 1. `estimated_output_bytes` 被读出后完全没有使用

正式规则：

```
总输出 > 3 × estimated_output_bytes
→ 触发摘要
```

当前规则：

```
总输出 > 固定 200KB
→ spool
```

200KB 应是摘要 chunk 大小，不是触发阈值。

### 2. 不是流式 spool

当前先把全部 stdout/stderr 存进：

- 
   Node `ResizeArray<Buffer>`；
   
- 
   .NET `MemoryStream`。
   

进程退出后才写临时文件。

这意味着 20GB 输出先吃掉 20GB 内存，完全失去 spool 的意义。

### 3. SIGKILL 后主动取消 pump

.NET 路径现在：

```
kill process tree
→ pumpCts.Cancel()
→ 等 pump
```

这会截断管道中尚未读取的尾部。

正确顺序：

```
SIGKILL
→ 等进程/pipe EOF
→ pump 自然完成
```

不额外加 timeout，但也不能主动截断。

### 4. stdin 写入不受唯一 deadline 控制

同步写大 stdin 时，如果子进程不读取，可能在进入 `WaitForExitAsync` 前永久阻塞。

### 5. Executor summarizer 是假的

当前默认 summarizer 实际是：

```
MapChunk = bytes 转字符串
Reduce = 字符串拼接
```

它没有调用无工具 Executor Agent，也没有摘要。

### 6. executor 工具 schema 不符合契约

当前工具只接受：

```
{"command":"..."}
```

并硬编码：

```
30 秒
200000 bytes
Medium
```

正式 schema 应包含：

```
command
estimated_output_bytes
estimated_running_secs
estimated_mem_usage
```

所以 Process 当前不能进入下一阶段。

---

## 九、Fallback 与 Review：有正确砖块，但没有房子

### Fallback

Journal Fold 的四次失败趋势大体正确，但 `DurableFallback.recordFailure` 在追加失败后又调用 `nextAttempt`，造成第一次失败后就计算出切 B 的决定，存在明显 off-by-one。

而且尚未接到真实模型调用边界，A/B 只是独立纯函数。

### Review

正确砖块：

- 
   同一 tree 两次 PERFECT；
   
- 
   REVISE 清零；
   
- 
   当前 Git tree 与确认 tree 比较；
   
- 
   Guard prompt accepted 写 Journal。
   

仍缺：

- 
   verdict tool 真正接入；
   
- 
   ToolCallId 去重；
   
- 
   Reviewer terminal 无 verdict 的事件循环；
   
- 
   Manager terminal Guard；
   
- 
   Guard nudge 后恢复同一 session；
   
- 
   初审与 rebase 后复审；
   
- 
   与 Orchestrator 串联。
   

`Programs.createReviewerCapability.SubmitVerdict` 目前只是：

```
Ok verdict
```

没有读 Git、没有 Journal、没有确认机制。

---

## 十、Orchestrator：只有漂亮的模拟程序

当前完成了：

- 
   ForkManager 后台运行；
   
- 
   publish 串行链；
   
- 
   rebase；
   
- 
   reverify；
   
- 
   ff-only。
   

但距离正式协议仍有根本缺口：

1. 
   默认 worktree 建在仓库内部 `.worktrees/`，可能直接让主工作区变脏。
   
2. 
   `join()` 无结果时返回 Empty，而不是等待。
   
3. 
   没有 Manager 初次双 PERFECT。
   
4. 
   没有自动 candidate commit。
   
5. 
   rebase 冲突直接报失败，没有交回同一 Manager。
   
6. 
   rebase 后只有泛化 `Reverify`，没有明确双 PERFECT。
   
7. 
   发布前不重新检查目标工作区 clean。
   
8. 
   没有 Journal 记录 ManagerJob/candidate/rebase/published。
   
9. 
   重启后不能恢复。
   
10. 
   失败后 worktree 的保留和再次继续没有协议。
   

所以它是良好的 Port 草图，不是可发布 Orchestrator。

---

## 十一、删除旧资产删得太早

`MIGRATION.md` 声称：

> 
> 每个 legacy test 都有且仅有一种 disposition。
> 

但行为表只有大约 9 条，而上一版有数百个测试文件。

现在旧 `tests/` 已从主干删除，说明：

> 
> 测试资产尚未盘完，就先把军火库拆了。
> 

幸好 Git 历史还在，不需要把旧代码恢复进主干。正确补救是：

```
创建只读 legacy tag/worktree
→ 逐族提炼场景
→ 补 MIGRATION ledger
→ 新测试接管后标记已完成
```

绝不能以当前 9 条 ledger 宣称资产迁移结束。

---

## 十二、当前准确战役位置

不是战役 15，也不是准备切生产。

准确位置是：

```
战役 0：冻结与立宪          🟡 SSOT 尚未补齐
战役 1：抢救 TestKit        🟢 基本完成
战役 2：清空假彼岸          🟢 基本完成
战役 3：Flow/资源基座        🟡 部分完成
战役 4：OpenCode Host Spike  🔴 未通过
战役 5：ForkRuntime          🔴 原型不合格
战役 6 以后                  ⚪ 只有骨架，不计完成
战役 16：生产切换            🔴 严重抢跑
战役 17：删除旧实现          🔴 严重抢跑
```

**项目必须退回“战役 3 收尾、战役 4 生死 Spike”位置。**

不是恢复旧架构，而是撤销“已经完成新产品”的假设。

---

## 十三、接下来严格按这五个提交走

### 提交 1：修宪法

1. 
   把 Event Sourcing 后续纠正完整写进 `SSOT.md`。
   
2. 
   明确：
   
   
   哪些 Fact 持久化；
   
   
   哪些资源状态只在内存；
   
   
   Journal 不驱动 workflow；
   
   
   Projection 必须有界。
   
   
   
3. 
   删除或标记旧 `kiss-docs` 为历史材料。
   
4. 
   修正 README 中：
   
   
   Busy agent；
   
   
   watermark；
   
   
   Process；
   
   
   已完成能力。
   
   
   
5. 
   暂时明确产品状态为 pre-alpha scaffold。
   

### 提交 2：修测试接线

根测试必须真正执行：

```
生产编译
→ tests-next Fable 编译
→ tests-next runner
→ runner 自测
→ TestKit gates
→ P0 E2E
```

同时：

- 
   每次 E2E 前删除 `build/`；
   
- 
   TestKit 插件路径固定为 `build/OpenCode/Plugin.js`；
   
- 
   插件不存在时必须失败；
   
- 
   插件启动后验证唯一 plugin marker；
   
- 
   strict mock 默认开启；
   
- 
   删除所有 `Task.Delay` 等待；
   
- 
   让 ArchitectureGates 真正运行。
   

### 提交 3：闭合 Journal Runtime

新增唯一启动入口：

```
Boot.capture stable frontiers
→ Boot.fold
→ CreateNew own writer
→ RuntimeStarted
→ AgentJournal(initial Boot Projection)
→ open hooks
```

并完成：

- 
   重启恢复 Companion；
   
- 
   重启恢复 fallback；
   
- 
   重启恢复首个 PERFECT；
   
- 
   重启恢复 Agent linkage；
   
- 
   删除所有 O(N) Projection 字段；
   
- 
   ToolCallId verdict 去重。
   

### 提交 4：只做真实 fork/join 纵切

暂时不要继续 Companion、Process、PTY、Orchestrator。

只完成：

```
真实 Manager tool surface
→ fork(coder)
→ create real OpenCode child
→ listener before send
→ fire-and-forget prompt
→ assistant terminal
→ 提取 A
→ completion channel
→ blocking join()
→ parent abort cleanup
→ AgentLinked Journal
→ restart reconcile
```

用真实 OpenCode + Mock LLM 连续跑 20 次。

### 提交 5：第一个真实产品 canary

场景必须是：

```
用户要求写文件
→ Manager 只能调用 fork
→ Coder 拥有 write/edit
→ Coder 写文件
→ Manager join 收到 Coder A 版
→ Manager 回复用户
```

断言：

- 
   Manager provider request 不包含 read/write/edit/executor；
   
- 
   Coder provider request 包含文件工具；
   
- 
   新插件确实被加载；
   
- 
   Journal 中有 AgentLinked；
   
- 
   completion 恰好一次；
   
- 
   无 PID/session 泄漏；
   
- 
   不使用 fixed sleep。
   

只有这个 canary 20× 通过后，才准许进入 Companion。

---

## 最终评价

现在不是“工程师偷懒”。

恰恰相反，他们写了很多代码、删了很多代码、铺了很多模块。

问题是：

> 
> **把“目录看起来像最终架构”误当成“最终架构已经实现”，把 Stub 单测误当成 Host 证明，把编译测试误当成执行测试，把普通 write canary 误当成 Manager DSL canary。**
> 

总指挥最终命令：

```
立即停止继续铺功能
立即停止宣称 release-ready
先修 SSOT
再修测试接线
再闭合 Journal Boot
然后只做一个真实 fork/join 纵切
```

当前版本值得保留作为新大陆的地基，但上面的样板房全部不验收。静态审计所依据的是当前完整仓库快照；当前执行环境没有安装 `dotnet`，因此我不能替它实际跑编译，但测试脚本未执行 F# 测试、canary 指向旧构建路径等问题，从源码本身已经可以确定。


---

*Exported by [AI Chat Exporter for ChatGPT](https://github.com/Rajat-XR/ai-chat-exporter) · July 24, 2026 at 7:23 AM*
