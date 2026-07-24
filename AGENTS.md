---
import: 
  - README.md
  - TASK.md
---

# 当前工程状态

# 本轮推进记录

- 已恢复 `next/Doc/SSOT.md`，冻结 Agent DSL、Companion、Fork/Join、durable facts、Review、Process 与 Orchestrator 最终语义。
- 已完成 per-Run terminal listener、输出增量切片、existing-agent nudge 重装 listener、标准 workspace Journal Boot；真实 Manager→Coder→Join 与 Companion B1/B2 已通过 OpenCode P0。
- 已拆出 `OpenCode/CompanionTransform.fs`、`OpenCode/HostEventRouter.fs`、`OpenCode/ToolSurface.fs`、`OpenCode/ExecutorTool.fs`、`Orchestrator.Types.fs`、`Orchestrator.GitPort.fs`，恢复 300 行架构门禁；此前 `npm test` = 135/135、Manager contract = 1/1、TestKit = 11/11。
- Manager provider request 已证明只暴露 `fork/join/list`，禁止 `read/write/edit/bash/glob/grep/verdict`；P0 默认 3×稳定性通过，`CANARY_REPEAT` 仍可提高门槛。
- Companion 真实 Blogger child 已产生 B1/B2，同一 child 被复用，角色 sidecar 门禁通过；durable B/baseline/replacement 已有重启 Port/Fake 测试，真实近上限投影 E2E 仍未验收。
- Companion 前缀覆盖比较已优先使用稳定 message ID，避免 OpenCode 对同一消息补充 summary/diff 元数据后误判前缀断裂；真实近上限 replacement 仍未验收。
- Process 已完成 lossless pump、动态 `3×estimated_output_bytes` spool、200KB chunk、SIGKILL 后等待 pipe EOF；真实 Inspector→Executor map/reduce canary 已通过，SIGKILL/PTY 压力仍待纳入稳定性门。
- Review verdict 已接入真实 GitTreePort、Journal、ToolCallId 去重、双 PERFECT 与 reviewer terminal nudge；真实双 PERFECT canary 已通过，Fallback 真实模型调用仍未接线。
- Orchestrator agent 已接入 HostSessionContext 与静态 `fork/join` 权限面；真实 Manager worktree 创建、冲突回交、复审与 ff-only 发布仍未接入 OpenCode Host。
- Orchestrator 已有 durable facts、candidate/published 投影、rebase 冲突同 Manager 继续、post-rebase 双 Review 与 ff-only 纯 Port 路径；真实 OpenCode/Git 发布 E2E 仍未验收。
- 用户最终裁决：稳定性门槛由 20× 降为 3×；本仓库默认执行 3 次，3×是当前验收门，不等价于 release-ready。

## 已完成并验证

- Fable 是唯一目标平台；`next/` 不得出现 `#if`、`#else`、`#endif` 或非 Fable 分支。
- Structured Flow 已支持 Promise defer，递归 10000 步不栈溢出；取消异常保持外抛。
- `ForkRuntime.Join()` 是等待型 completion mailbox；completion 先入邮箱，existing-agent fork 是 nudge。
- Runner 注入执行路径遵守唯一 `3 × estimated_running_secs` deadline；超时结果为 `TimeoutExceeded`。
- Runner、Journal Facts、Programs、Flow、Events 测试均已按 300 行门禁拆分，`ArchitectureGates` 自动化门禁已恢复通过。
- Fable 测试框架不依赖 Xunit 程序集；架构门禁、角色权限、Journal、Flow、Process、PTY、Review、Orchestrator 测试均纳入统一入口。

## 已验证通过但存在关键限制

- `npm test` 及 `npm run test:release` 已真正执行 F# 测试与 TestKit，不再只编译测试项目.
- `HostEventPort`/`DeterministicEventPort` 已移除按 Session 永久吞 terminal；真实 P0 已覆盖 child 创建、terminal、A 版切片与 Manager join。
- Companion 角色纯门禁与真实两轮 Blogger 请求已通过；真实 near-limit projection replacement 与 OpenCode 进程重启仍需单独 E2E。
- `Reviewer` verdict 已读 Git tree、写 Journal、按 ToolCallId 去重，并以同 tree 双 PERFECT 确认；真实 Reviewer tool→Git 工作区→重启 E2E 仍未闭合。
- 标准入口从 `input.directory` 自动启用 `<workspace>/.wanxiangshu-next/runtimes/` Boot + AgentJournal；真实 AgentLinked 生成已由 Manager canary 覆盖，HostForkRuntime 已按持久 child/session/role linkage 恢复可 nudge 句柄，真实 OpenCode 重启 reconcile 仍需验证。

## 当前边界：不得误称已完成

- `npm run test:release` 已通过不等于 production-ready；默认 P0 稳定性是 3×，不是 20×。
- 真实 Host parent abort、跨重启 child reconcile、Fallback provider failure、Process/PTY 长压、Orchestrator 发布 E2E 尚未闭合；同一 child 三轮 nudge 已有真实 canary。
- 在上述边界闭合前，不切换 production entry，不删除黑盒 Oracle 测试资产，不宣称 release-ready。

## 当前已知关键 Bug 与未修复缺口

### 🟢 SSOT 宪法已恢复
- `next/Doc/SSOT.md` 已恢复并冻结用户最终裁决；后续实现与测试以该文件为产品语义依据。

### 🟡 Host terminal：已改 per-Run，待真实验收
- Session 不再永久标记 terminal；每次新 prompt 都安装独立 listener，使用启动前输出边界截取本轮增量并在完成后 dispose。
- 真实 Manager→Coder→Join 已通过；parent abort 已由 HostEventRouter 向已登记 child 传播，连续多轮、迟到 terminal 与真实 assistant part 边界仍需 E2E。

### 🟡 A 版输出：当前为新增输出切片，仍待完整 Host part 验证
- `HostForkRuntime` 不再直接返回全历史；按 Run 启动边界截取新增输出并排除本地 prompt 标记。
- `CompanionHost` 已按本轮输出边界读取 Blogger B；迟到 part、reasoning/tool 混合与跨重启 Host reconcile 仍待真实 E2E。

### 🟢 Companion 侧车递归已阻断
- `MessageTransform` 与 OpenCode transform 调用均按角色排除 Blogger/Executor/Inspector/Browser/Meditator/Reviewer，保留 Manager/Coder/Orchestrator。
- 上游 `experimental.chat.messages.transform` 在每次 outbound projection 前收到空 input；OpenCode 的 model `limit.context` 只在更晚的 `experimental.chat.system.transform` 暴露，无法直接作为同一轮 projection 的可靠 near-limit 门禁；必须先冻结 Host 预算传递契约，禁止用固定字节阈值冒充真实上限。

### 🟢 Manager 工具权限已由 provider request 验证
- `SpikePlugin` config hook 原地注入 manager agent 的 deny-all + fork/join/list allow 配置；P0 已证明真实 provider request 无 read/write/edit/bash/glob/grep/verdict。

### 🟡 Journal 默认路径已接线，跨重启仍待生产验证
- 标准入口从 `input.directory` 推导 `<workspace>/.wanxiangshu-next/runtimes/`，Boot 后创建 AgentJournal；AgentLinked 写入已进入真实 Manager 纵切，child/session/role linkage 的 Port/Fake 恢复测试已通过，真实 OpenCode 重启与 Review/Fallback/Companion reconcile 仍未闭合。

### 🟡 Fallback 阈值已修复，待真实模型调用验证
- `Fallback` 纯函数与 durable wrapper 现按 A1→B2→B3→Dead 计算；第一次失败重试 A，第二次失败才永久切 B。
- OpenCode prompt 的 `model` 已按 Host 契约收敛为 `{ providerID, modelID, variant? }` 对象；A/B 的真实 model resolver、failure 注入与 provider 错误分类仍未定义，禁止硬编码模型名伪造 E2E。
- durable projection 仍需接入真实模型请求并验证重启后的累计失败。

### 🟢 Process 已闭合命令与摘要主路径，压力边界待验收
- Pump、增量 spool、动态输出阈值、200KB chunk、唯一 deadline 与 SIGKILL 后 EOF 已通过本地 Process 测试。
- Inspector executor 已真实创建无工具 Executor child，完成 200KB map/reduce；SIGKILL、PTY、大输入与孤儿检测仍待稳定性 E2E。

### 🟢 Projection 历史复制已压成有界槽位
- Manager、Orchestrator、DurableEffect、ReviewGuard Projection 已移除无限 History/PublishedCommits/Effects/AcceptedGuardKeys；审计历史仍只在 NDJSON，bounded recent ToolCallId 仅用于重复投递防护。

### 🟡 Orchestrator 纯 Port 路径已成形，真实发布仍未验收
- 已有 AgentJournal、candidate/published facts、初次与 rebase 后双 PERFECT、冲突交回同一 Manager、Git authority reconcile 与 ff-only；真实 OpenCode Manager worktree 发布 E2E 仍未闭合。
## 下一阶段唯一优先级：跨域闭合
1. 冻结真实 Host 的 projection budget、parent abort 与跨重启 reconcile 契约，补齐对应 OpenCode E2E。
2. 接通真实模型失败注入后的 A/B Fallback durable 恢复。
3. 将 Process PTY、大输入、SIGKILL 与孤儿检测纳入默认 3×稳定性门。
4. 将 Orchestrator durable Port 路径接到真实 OpenCode Manager worktree、冲突回交、复审与 ff-only 发布 E2E。
5. 全部边界通过后才允许 release 入口切换与旧资产删除。
## 已完成路线与剩余门禁

### 已完成：宪法与测试基座
- `next/Doc/SSOT.md` 已恢复；`AGENTS.md` 保留当前状态与执行纪律。
- Fable build、tests-next、Manager contract、TestKit gates 已纳入 `npm test`。

### 已完成：Host terminal 与 Manager 纵切
- 删除 `completedSessions: HashSet<SessionId>`，改为 per-Run listener + event watermark
- `attach listener before send` → record watermark → `send prompt` → `await next matching assistant terminal` → `extract only this run's assistant text` → `dispose listener`
- `existing agent` 每次 fork 重新安装 waiter
- `A 版`只含本轮 assistant 正文，排除 reasoning/tool IO/user prompt/旧历史
- parent abort 跨 pending Run 传播 CancellationToken
- 迟到 terminal 按 RunId 忽略
- 同 session 连续三次 prompt 均可独立完成

### 已完成：真实 Manager→Coder→Join E2E（3× stability；可用 CANARY_REPEAT 提高）
- Manager provider tools = ONLY `fork/join/list`，验证真实 provider request 不含 `read/write/edit/bash/glob/grep`
- Coder provider tools 包含 `write`，Manager 通过 `fork(coder)` 委托写文件
- Manager `join()` 收到 Coder 本轮 A 版正文（非全历史拼接）
- 验证：AgentLinked Journal 写入 & completion 恰好一次 & 无 PID/session 泄漏 & 无 fixed sleep

### 已完成：Companion Blogger 纵切
- 先加角色排除：Companion only = Manager/Coder/Orchestrator，禁止 Blogger/Executor/Inspector/Browser/Meditator/Reviewer
- 只读当前 Run 的 assistant 正文，不读全历史拼接
- 验证：
  - X projection 1 → Y output B1
  - X projection 2 → 同一 Y output B2
  - Y busy → skip，不推进基线
  - Y 空闲 → delta 含跳过内容
  - Port/Fake 覆盖 replacement flag 与前缀保留；真实 near-limit 自动触发仍受 Host projection budget 契约阻塞
  - restart → 恢复 B/baseline/replacement
  - Y self-rebase → CurrentB 只等于 B'（旧 B 自然退出 transcript）

### 当前未闭合
- Reviewer：真实 verdict canary 已通过；parent terminal 无 verdict 的重复 nudge、重启 reconcile 仍待真实 Host E2E。
- Fallback：A/B durable 纯函数已通过；真实 provider failure 注入与 session 重启仍待 E2E。
- Process：真实 Executor map/reduce canary 已通过；SIGKILL、PTY 与大输入压力仍需纳入稳定性门。
- Orchestrator：纯 Port 与 durable facts 已通过；真实 Manager worktree 发布链仍待 E2E。

## 验证命令

```bash
npm test
npm run test:e2e:p0
npm run test:release
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
  并发→Promise.all，超时→Promise.race 组合子。可见失败禁止裸抛异常。promise 内部就地 try...with 捕获→转为强类型 Result Branch→逼迫调用方匹配，不给异常留改道机会。

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

# 新架构 Agent DSL 最终 SSOT 裁决与路线图

## 一、唯一真理源原则 (SSOT Authority)

1. `next/Doc/SSOT.md` 是产品语义唯一真理源；`AGENTS.md` 是工程约束与当前状态真理源。彻底废除 `todowrite`、通用 `Nudge` 协调器、`select_methodology`、`fuzzy_*` 工具与上一代 Stage/Phase 状态机。
2. 产品语义裁决优先级：`next/Doc/SSOT.md` 用户最后明确纠正 > 最终 Agent DSL 架构设计 > `next/Doc/kiss-docs/` > 当前代码 > 旧 `src/`。
3. `next/Doc/SSOT.md` 已恢复，记录用户最终纠正；实现前必须读取并遵守该文件，恢复后的 SSOT 获得本项目产品语义最高优先级。

## 二、双层 DSL 架构 (Dual-Layer DSL Architecture)

### 1. 模型可见 Agent DSL (Model-Facing Agent DSL)
- **Manager**: 仅 `fork`、`join`、`list`。无常规文件工具，强制扮演纯协调角色。
- **Orchestrator**: 仅 `fork`、`join`。专门 fork `ManagerJob`。
- **Reviewer**: `verdict` 工具仅接受结构化枚举 `PERFECT | REVISE`。

### 2. 实现者 F# Structured Program DSL (Builder Architecture)
- 基于单一底层闭包内核：`type Flow<'ctx, 'error, 'a> = 'ctx -> CancellationToken -> Task<Result<'a, 'error>>`
- 语法原语：`let!` (等待领域动作)、`do!` (执行动作)、`use!` (异步资源作用域 DisposeAsync)、`match` (匹配强类型 Result)、`while` / 尾递归 (条件重试与确认)、`parallel` (相互独立局部并发)。
- 严禁建立 Flow AST、解释器、Workflow Engine 或动态 Stage/Phase 注册表。

## 三、15 条冻结架构不变量 (15 Architectural Invariants)

1. **Context 投影法则**：X 每次模型请求都先构造 canonical JSON projection；启用 replacement 后，以当前 B 等价替换已覆盖前缀，未覆盖 raw tail 必须原样保留。
2. **Blogger 隔离**：Companion Blogger (Y) 失败、延迟或崩溃永远不阻塞主会话 (X)。
3. **稳定 Cursor 增量**：Delta 由稳定消息/事件身份 JSON 粒度产生，不做模糊文本 diff。
4. **认知与控制分离**：B 版工作记录是认知缓存与背景上下文，不是控制与调度事实。
5. **单活跃 Run + fire-and-forget nudge**：一个物理 Agent 在同一时刻最多有一个活跃 Run 等待回复。对 busy agent 再 fork 是 nudge 语义，系统向同一 OpenCode child session fire-and-forget 发送 prompt，不排队、不阻塞、不返回 Busy 错误。宿主 Runner 自然在后续 LLM 请求尾部吸收该 prompt。
6. ** Run 独立身份**：每次 fork/prompt 分配全局唯一 `RunId`，防止迟到输出覆盖新一轮 Run。
7. **邮箱优先 (Mailbox First)**：Completion 必须先入 `completionChannel` 邮箱，`join()` 消费邮箱，避免 Fast Completion 丢失。
8. ** Join 无侧重**：`join()` 随机/顺序弹出邮箱中任意最早到达的 completion，严禁按指定 AgentId 阻塞筛选。
9. ** Join 空状态**：completion mailbox 为空但仍可能有 active Run 时，`join()` 等待下一项；只有明确无 active Run 且协议要求结束时才返回 Empty。
10. **父级取消作用域**：父 Session 取消/abort 时，通过 CancellationToken 递归有界清理所有子 Run 和 PTY。
11. **Fallback Session 闭环**：Fallback 按 Session 累计失败，A1→B2→B3→Dead；成功不清零、不切回 A，失败事实持久化并可从 Projection 恢复。
12. **Reviewer Hash 绑定**：Reviewer `PERFECT` 结论严格绑定审查时的精确 Git tree hash。
13. **Reviewer 变化失效**：`PERFECT` 之后工作区发生任何修改，连续确认数立即清零。
14. **串行发布门禁**：多个 ManagerJob 共享目标 Git ref 的发布过程必须过 `SemaphoreSlim(1)` 严格串行。
15. **Rebase 后强制复审**：Rebase 到最新目标 HEAD 后，必须重新获得 double PERFECT 才能 fast-forward 合入。

## 四、三类状态隔离与 CQRS 事实分类 (Three-Tier State Architecture)

| 状态类别 | 内存/持久化位置 | 典型代表 |
| :--- | :--- | :--- |
| **1. 进程内资源状态** | 内存 (Task / Channel / Handle) | 运行中 `Task`、completion `Channel`、Process/PTY handle、Large Semaphore 锁、调用栈 |
| **2. 跨重启领域事实** | 追加写 NDJSON (`Fact`) | `AgentLinked`、`CompanionAdvanced`、`PrefixReplacementEnabled`、`VerdictRecorded`、`GuardPromptAccepted`、`ModelAttemptFailed`、`ManagerCandidateCreated`、`Published` |
| **3. 外部权威事实** | 直接查询外部系统 (Git / OS / Host) | Git tree hash、Git ref 指向、OpenCode transcript 正文、进程真实存活状态 |

- **禁止持久化程序计数器**：严禁把 `ReviewPhase`、`FallbackStage`、`JoinOwner`、`NudgeLease`、`CompactionGeneration` 写盘。

## 五、角色与能力静态装配矩阵 (Role Matrix)

| 角色 | 模型工具 | 伴随 Blogger | 内部能力与约束 |
| :--- | :--- | :--- | :--- |
| **Manager** | `fork`、`join`、`list` | 是 | 无文件/终端工具，仅能通过 fork 协调子代理 |
| **Orchestrator** | `fork`、`join` | 是 | 仅 fork ManagerJob，自动管理 worktree 与发布串行锁 |
| **Coder** | `read` / `write` / `edit` / `glob` / `grep` | 是 | 允许同步调用一次性 `Inspector` |
| **Inspector** | `executor` | 否 | 无文件工具，仅启动 CLI 命令并返回摘要/结果 |
| **Browser** | `read`、网页工具 | 否 | 只读本地文件与浏览网页，写工作区受限 |
| **Meditator** | `read` / `glob` / `grep` / `inspector` | 否 | 自由推理，无固化方法论目录与章节限制 |
| **Reviewer** | `read` / `glob` / `grep` / `inspector` / `verdict` | 否 | 正文输出 A 版说明，`verdict` 提交结构化结论 |
| **Executor** | 无工具 | 否 | 无工具思考代理，仅生成 CLI 摘要 |
| **Blogger** | 无工具 | 否 | 无工具代理，仅接收 delta 生成 B 版工作记录 |

## 六、子系统物理实现规范

### 1. Companion Blogger & 零延时前缀替换
- **JSON Delta 机制**：每次 X 准备请求模型，取 outbound JSON 与 `LastSuccessfulProjection` 算 JSON 级差量。
- **忙时跳过 (Busy Skip)**：Y 处于 Busy 时直接跳过，不排队、不更新基线，下一次空闲时的 delta 包含累积变化。
- **B 版定义**：仅由 Y 的 assistant 正文构成。Y 接近上限时，旧 B 作为 Y 输入生成新 B'，旧 B 自然替换出局。
- **前缀替换**：X 接近上限开启 `ReplacementActive`，以 `B` 替换已被 BlogBase 覆盖的前缀消息，尾部 `(cursor=C, current]` 原样保留。

### 2. ForkRuntime & PTY 表面
- **Fork New**：创建子代理 -> 注册 terminal listener -> 发送 prompt -> 返回 AgentId。
- **Fork Existing (Nudge)**：向已有 AgentId 发送 prompt -> fire-and-forget 催促 -> 返回 Nudged。
- **PTY 结构化参数**：统一占用 `fork` 表面，无魔法字符串：
  - 创建 PTY：`fork(agent="pty", prompt="command")`
  - 写入 stdin：`fork(agent="<pty-id>", prompt="content")`
  - 发起 read：`fork(agent="<pty-id>", prompt="")`
  - 结构化信号：`fork(agent="<pty-id>", signal="TERM" | "KILL")`

### 3. Process, Command & 200KB Map/Reduce 摘要
- **绝对 Deadline**：唯一进程超时时间为 `3 × estimated_running_secs`。超时触发 SIGKILL 进程树。
- **内存 Semaphore**：`Medium` 不限并发；`Large` 内存全局限制 `SemaphoreSlim(1)` 串行。
- **Spool & Summarizer**：进程启动即安装 byte pump；总输出超 3 倍 `estimated_output_bytes` 时流式写入临时 spool 文件；200KB 只是 Executor Map/Reduce 分块大小。

### 4. Reviewer & ReviewGuard 双 PERFECT 确认
- **Reviewer 程序**：`verdict` 工具仅接受 `PERFECT | REVISE`，以 `ToolCallId` 去重。第一次 `PERFECT` 工具返回“请再次确认”；同一 tree hash 下第二次连续 `PERFECT` 确认通过。
- **Manager Guard**：Manager 尝试结束时，若当前 Git tree 未获得双 PERFECT 确认，Guard 注入提示发回同一 Manager 继续审查。

### 5. Session 4 次失败 A/B Fallback 递归
- 单 Session 递归规则：失败 0-1 次（Side A 重试） -> 失败 2 次（永久切 Side B 并在本 Turn 立即尝试） -> 失败 3 次（Side B 重试） -> 失败 4 次（SessionDead 强行关闭）。成功不清零 Failures 计数。

### 6. Orchestrator 隔离 Worktree & 串行发布
- **脏工作区拒绝**：用户发消息前若工作区脏，直接报错。
- **工作流**：用户消息 -> `fork ManagerJob` -> 创仓库外隔离 worktree -> Manager 自动进入 ReviewGuard -> 初次双 PERFECT -> 生成 candidate commit -> 申请串行 publish 信号量 -> rebase 最新目标 HEAD -> 冲突回交同一 Manager 解决 -> rebase 后重新双 PERFECT -> fast-forward 合入 -> 清理 worktree -> join 返回 `Published`。

## 七、NDJSON 日志物理规范 (Per-Runtime NDJSON Physics)

- **路径**：`.wanxiangshu-next/runtimes/<runtime-id>.ndjson`
- **写入**：单 Runtime 独立文件，追加写模式 (`CreateNew`)，写入后立即 flush 并更新内存 Projection。
- **启动 Boot**：确定性截取所有已有日志文件的稳定 byte frontier，归并 ObservedAt / LocalSeq 后 Fold 出初始内存 Projection。遇到 EOF 截断半行自动丢弃。

## 八、Phase 0–8 实施阶段与战役出口

- **Phase 0 (Host Spike)**：验证 OpenCode `events.listen` 订阅、A 版正文提取、Companion 消息替换 hook。
- **Phase 1 (ForkRuntime)**：完成 `fork/join/list`、RunId、completion channel、父级取消。
- **Phase 2 (Companion)**：完成 JSON delta、busy skip、B 版生成、前缀替换。
- **Phase 3 (Process & Inspector)**：完成流式 pump、spool、3× Deadline、Large 信号量、200KB Map/Reduce 摘要。
- **Phase 4 (Roles)**：完成 Manager/Coder/Inspector/Browser/Meditator/Reviewer 静态能力装配。
- **Phase 5 (ReviewGuard)**：完成双 PERFECT 挑战与 Manager 结束门禁。
- **Phase 6 (PTY)**：完成结构化 PTY 句柄、读写、信号与 completion 统一化。
- **Phase 7 (Orchestrator)**：完成隔离 worktree、ManagerJob、rebase 复审、串行 FF 发布。
- **Phase 8 (删除旧实现)**：仅在真实 Host/Provider/PTY/Orchestrator 边界全部闭合并完成 release 审计后，才物理删除旧代码、旧测试与万象阵 DAG 基础设施；当前阶段禁止执行。

# 设计决策历史与补充规范

## 一、初始方案 6 个必须纠正的协议漏洞 (来自第一次架构评审)

### 1. 压缩时不能只放 B，必须放 "B + 未覆盖尾部"
- 假设 Blogger 已总结到游标 100，X 又产生 101-108，Y 忙不排队。此时 X 溢出。若仅用当前 B 替换全部上下文，101-108 永久丢失。
- 不变量：`压缩后 X 上下文 = B 版记录(through=C) + 原始尾部消息(C, current]`
- 每版 B 必须携带运行时赋予的水位 `BlogCheckpoint { through: TranscriptCursor; text: string }`，`through` 不由 blogger 生成。
- 结果：**零等待压缩**而非仅靠 B 压缩。B 落后一两轮没关系，未总结部分原样跟在后面。

### 2. Delta 不能是文本 diff，必须是稳定消息身份切片
- `delta = transcript events in (lastAcknowledgedCursor, currentCursor]`
- OpenCode `message.updated` 可能对同一消息发布多次，依赖到达次数或文本 diff 产生重复/乱序/漏记。
- 应使用 `TranscriptCursor = messageId + partId + stable revision/finalized marker`。
- YAML 只是表示层，不是内部事实格式。所有原始内容必须放在 YAML literal block 中并标记为不可信 transcript。

### 3. "B 是 Y 所有输出拼接" 会无限增长
- 应区分为：`BlogSegments = 自上次重基以来的新段落`，`BlogCheckpoint = 已重基的完整 B 版记录`，`B = BlogCheckpoint + BlogSegments`
- Y 接近溢出时：旧 B 作为输入 → 要求重写紧凑版本 → `BlogCheckpoint = 新输出; BlogSegments = []`
- 这不是 CompactionState 状态机，是两个普通函数：`AppendDelta` / `RebaseCheckpoint`

### 4. B 不能成为事实源
- B 可以告诉模型 "目前大概做到了哪里"，但不能决定：child 是否仍在运行、reviewer 是否 PERFECT、文件是否修改、命令是否通过。
- 权威来源表：
  | 事项 | 权威来源 |
  | :--- | :--- |
  | 活跃代理/未领取结果 | fork runtime |
  | 代码内容 | 工作区文件 |
  | 完成变更 | Git tree/commit |
  | 审查结论 | reviewer 结构化 verdict |
  | 命令结果 | process result |
  | 用户要求 | 原始当前请求 + B 记录 |
- **文本负责认知，结构化值负责控制** — 旧架构的复杂度正是来自把模型文案、idle、todo、nudge 和协议状态互相解释。

### 5. "包含思考" 不应成为正确性前提
- 宿主不提供 reasoning 时也完全可运行。
- 流式未结束的 reasoning 不进入已确认 delta。
- B 不保留原始思维流，只保留可验证的结论、尝试、失败与决策。
- 不把 reasoning 当作恢复协议。
- Blogger 提示词："根据可观察 transcript 记录目标、决策、动作、结果、错误、未决问题和精确标识符。不要复述代码，不要记录思维流，不要把推测写成事实。"

### 6. Executor 命名冲突
- `fork(agent=executor)` 与 `inspector 调用 executor(command=...)` 中 executor 同时表示无工具思考代理和系统进程启动工具。
- 建议：无工具思考型代理 = `advisor`/`analyst`；进程执行工具 = `exec`/`command`。但用户最终裁决保留 executor 角色作为 executor 工具的 summarizer，语义清晰。

## 二、用户最终纠正清单 (与初始建议不同之处)

| 议题 | 初始 ChatGPT 建议 | 用户最终裁决 |
| :--- | :--- | :--- |
| Delta 机制 | 稳定消息事件 cursor 增量 | 本次投影与上次成功投影在 JSON 层做 delta |
| Busy Agent 再 fork | 返回 Busy 错误，不排队 | fire-and-forget nudge，系统自然排队 |
| 上下文压缩 | B through cursor C + raw tail | 通过投影等价替换前缀，记住以后次次替换 |
| Executor 角色名 | 改为 advisor/analyst | executor 保留，只作为 executor 工具的 summarizer |
| Fallback | 单 Turn reconcile，AcceptanceUnknown 停止 | 每 Session 累计四失败，A/B 永久切换 |
| Fallback 持久化 | 不需要 fallback journal | 必须持久化累计失败次数和当前 Side |
| 事件溯源 | 曾经在路线图中标记删除 | 必须保留 Event Sourcing 和 Per-Runtime NDJSON |
| CQRS | 同上 | 必须保留，写入 append-only Fact，读取内存 Projection |
| estimated 参数 | clamp 到全局硬上限 | 巨大 estimate 合法，不 clamp |
| Medium 并发 | 固定并发上限 | 不限并发 |
| 输出摘要触发 | 3x estimated_output_bytes | 固定 200KB = 摘要 chunk 大小 |
| PTY 信号 | magic string `[#SIGTERM]` | 结构化 `signal="TERM"` enum |
| Review 确认 | 双 PERFECT | REVISE 立即生效，PERFECT 两次确认 |
| Companion recursion | 无特别说明 | 必须角色排除，禁止 Blogger-of-Blogger |

## 三、结构化 DSL 主程序伪码

### Companion 主程序
```
每次 X 即将请求模型
→ 构造 canonical JSON projection
→ 与 LastSuccessfulProjection 算 JSON delta
→ Y idle：发送 delta + blogger instruction
→ Y busy：跳过，不排队，不更新 baseline
→ 收到 Y assistant 正文：更新 CurrentB 与 baseline
→ 接近 X 上限：启用 remembered prefix replacement
→ 每次后续投影继续 B 替换
```

### Y 自身压缩
```
Y 接近上限
→ 旧 B 作为本次输入
→ Y 输出新的 B'
→ B 定义只包含 Y assistant 输出
→ 旧 B 不再属于 B（自然退出 transcript）
```

### Fork/Join 主程序
```
fork new
→ create child with parent B
→ register handle
→ attach terminal listener（必须在 send 之前）
→ send prompt fire-and-forget
→ return AgentId

fork existing
→ send prompt fire-and-forget
→ return Nudged（不返回 Busy）

child terminal
→ extract A（只含本轮 assistant 正文）
→ write completion mailbox
→ update in-memory handle

join
→ read any completion from mailbox
→ 不能指定 AgentId

list
→ snapshot active agents and PTYs
```

### Process 主程序（3× Deadline + SIGKILL + Spool）
```
Inspector.executor(request)
→ 如 Large，acquireGlobalLargeLease()
→ spawn process group
→ 立即安装 stdout/stderr pump（无损 byte pump）
→ 等 exit 或唯一 deadline（3 × estimated_running_secs）
→ deadline 到达则 SIGKILL process tree
→ await exit + pump EOF（不主动 cancellation）
→ 输出未超 3×estimate：直接返回
→ 超过：完整输出流式写入 spool
→ 按 200KB 分块
→ 每块启动一次性 Executor Agent 摘要（map）
→ 最后一次 Executor reduce
→ 返回摘要 + byte count + truncation diagnostics
```

### Fallback 主程序（每 Session 累计四失败）
```
type ModelSide = A | B
type FallbackMemory = { mutable Side: ModelSide; mutable Failures: int }

rule:
  Failures 0, Side A → normal
  A 失败 → Failures=1, retry A
  A 再失败 → Failures=2, 永久切 B, 立即尝试
  B 失败 → Failures=3, retry B
  B 再失败 → Failures=4, SessionDead

成功不清空 Failures，不切回 A
```

### Reviewer 主程序
```
start Reviewer
→ wait assistant terminal or verdict
→ REVISE：立即有效，返回 revision
→ PERFECT 第一次：工具返回 "请再次确认"
→ 同一 tree 第二次 PERFECT：Confirmed
→ assistant terminal 无 verdict：fork(existingReviewer, nudge)
→ 继续等待
```

### Manager Guard
```
Manager assistant terminal
→ 检查当前 Git tree 是否已双 PERFECT
→ 是：允许完成
→ 否：向同一 Manager nudge（注入提示）
→ Manager 继续 fork/join 审查
```

### Orchestrator 主程序
```
用户向 Orchestrator 发消息
→ 检查目标工作区 clean（否则报错）
→ fork ManagerJob
→ 创建 repo 外隔离 worktree
→ Manager 自动进入 ReviewGuard
→ 初次双 PERFECT
→ 创建 candidate commit（运行时自动 stage+commit）
→ 获取目标 ref 的短 integration semaphore（串行门禁）
→ rebase 最新目标 HEAD
→ 有冲突：保留 worktree，恢复同一 Manager 解决
→ rebase 后重新双 PERFECT
→ fast-forward 发布
→ 清理 worktree
→ completion mailbox
→ Orchestrator join 收到 Published
```

### 两条必须遵守的资源时序
1. 子代理必须**先安装 terminal listener，再发送 prompt**。否则极快完成的 agent 可能在 listener 注册前 terminal，结果永久丢失。
2. `fork Manager` 不能在返回 handle 前 `use! worktree`；worktree 所有权必须转交后台 ManagerJob，由整个发布程序的 `use!` 最终释放。

## 四、保留 Event Sourcing，删除 Event-Sourced Workflow

- **应当删除的**：用事件溯源保存程序执行到哪个 Stage/Phase（`ReviewPhase = WaitingSecondPerfect`、`JoinOwner = manager-1` 等）。
- **必须保留的**：用事件溯源保存跨进程/跨重启仍然成立的领域事实。
- 正确架构 = `结构化程序负责"接下来做什么"` + `事件日志负责"过去确实发生过什么"` + `Projection 负责"重启后我们已经知道什么"`

### 写入端 (Write Side) — 领域动词自己验证并 append
```
let recordVerdict input =
    review {
        let! tree = git.currentTree input.Worktree
        let fact = VerdictRecorded(managerSessionId, reviewerSessionId, toolCallId, tree, verdict)
        do! journal.append fact
        return projections.review.ForManager input.ManagerSessionId
    }
```

### 读取端 (Read Side) — 启动 Fold 一次，之后 O(1) 查询
```
type Projections =
    { Review: ReviewProjection
      Companion: CompanionProjection
      Fallback: FallbackProjection
      Agents: AgentProjection
      Orchestrator: OrchestratorProjection }
```

### 本 Runtime append 流程
`编码一行 → append → flush → 更新内存 Projection → 返回成功` = read-your-writes

### 不需要的重量级框架
```
CommandBus / QueryBus / Projection Worker / Event Subscriber
Saga Manager / Aggregate Repository / Generic AggregateRoot / 动态 Handler Registry
```

## 五、Per-Runtime NDJSON 正式物理规范（10 条规则）

1. 每个 Runtime 只写自己的文件，路径：`.wanxiangshu-next/runtimes/<runtime-id>.ndjson`
2. 文件以 `CreateNew` 创建，避免多进程冲突。
3. 每行自包含：`schema version | RuntimeId | LocalSeq | ObservedAt | Fact`。
4. 启动时记录每个文件稳定 byte frontier。
5. 只读取 frontier 以前内容；EOF 半行忽略。
6. 中间损坏只隔离该来源，不能 hang。
7. 本进程 append 后先 flush，再 Fold 到内存 Projection。
8. 不实时 tail 其他 Runtime（避免跨进程同步平台）。
9. 新 Runtime 启动时重新枚举、归并、Fold。
10. Journal 不应知道 Driver / PromptProtocol / Todo / Review Phase / Child Actor / Session Stage。

### 最小 Fact 总表（第一版）
```
type Fact =
    | Companion of CompanionFact
    | Agent of AgentFact
    | Review of ReviewFact
    | Fallback of FallbackFact
    | Orchestrator of OrchestratorFact
    | DurableEffect of DurableEffectFact
```
`DurableEffectFact` 只用于有崩溃窗口且不可回滚的外部动作。

### 需要持久化的领域事实
| 领域 | 持久事实 |
| :--- | :--- |
| Companion | X 与 Y 的关联 |
| Companion | 最近一次成功的 B |
| Companion | JSON delta 基线 (`LastSuccessfulProjection`) |
| Companion | 该 X 已启用永久前缀替换 |
| Fork | AgentId 对应 OpenCode child session、角色和父 session |
| Review | Reviewer 对某 Git tree 的 REVISE/PERFECT |
| Review | Guard nudge 已被宿主接受 |
| Fallback | Session 累计失败次数 |
| Fallback | Session 已永久从 A 切到 B |
| Orchestrator | ManagerJob/worktree/candidate commit 关联 |
| Orchestrator | Candidate 已发布到哪个目标 commit |
| 外部效果 | 宿主返回的 message/session/commit ID |

### ReviewGuard 跨重启证明
```
Reviewer 第一次 PERFECT:
→ append + flush VerdictRecorded(PERFECT, treeHash=T)
→ Fold → AwaitingPerfectConfirmation(T)
→ 工具返回 "请再次确认"
→ 进程在此处崩溃

重启后:
→ Boot → Fold ReviewFact → 恢复 AwaitingPerfectConfirmation(T)

Reviewer 第二次 PERFECT（同一 tree）:
→ append + flush 第二个 VerdictRecorded(PERFECT, T)
→ Fold → PerfectConfirmed(T)
→ 确认通过
```
不需要持久化 `ReviewPhase = WaitingSecondPerfect`。

### Fold 规则（无 ReviewInvalidated Fact）
```
任何 REVISE → NeedsRevision
第一次 PERFECT(tree=T) → AwaitingPerfectConfirmation(T)
紧接着第二次 PERFECT(tree=T) → PerfectConfirmed(T)
PERFECT(tree=T1)后又 PERFECT(tree=T2) → AwaitingPerfectConfirmation(T2)
当前 Git tree ≠ confirmed tree → 视为未确认（每次 Guard 重新读 Git）
```

## 六、旧测试资产迁移点名册 (Legacy Test Migration Roster)

| 旧测试族 | 处置 |
| :--- | :--- |
| `EventLog*` / `ReplayEquivalence*` | 淘汰 |
| `FallbackLease*` / `Governor*` / `Continuation*` | 淘汰 |
| `Nudge*` / `Todo*` / `Methodology*` | 淘汰 |
| `ReviewSessionStateMachine*` | 淘汰 |
| `Subsession*` | 提炼隔离/取消/terminal/transcript 场景 |
| `Subagent*` | 提炼 fork/listener/completion/A 版场景 |
| `Executor*` | 提炼进程/输出/kill/spool 场景 |
| `Pty*` | 提炼 PTY 生命周期 |
| `Opencode*Codec*` | 作为 Host 调研和 fixture 矿 |
| `Integration*Loop*` | 改写为 Manager/Reviewer Guard E2E |
| `Integration*Todo*` / `Integration*Methodology*` | 淘汰 |
| `p0-canary-ndjson/recovery*` | 淘汰 |
| `p0-canary-fuzzy*` | 淘汰 fuzzy；保留大输出场景 |
| `p0-canary-child-pty*` | 改写为 fork/list/join/PTY |
| `p0-canary-compaction*` | 改写为 Blogger 投影替换 |
| 万象阵 harness | 只提炼 Git/worktree 场景 |
| OpenCode harness/TestKit | 黄金资产，独立搬迁 |

## 七、提交序列纪律

任何提交不得同时：
1. 新增新架构行为
2. 顺手重构旧实现
3. 修改 TestKit
4. 改写多个角色
5. 删除尚无新测试接管的旧行为

## 八、每日作战规则

每个阶段开始前工程师必须回答：
1. 本阶段对应哪条 SSOT？
2. 唯一行为 ID 是什么？
3. 最低层测试在哪里？
4. 真实 Host 边界是否需要 E2E？
5. 本阶段结束能删除什么旧资产？
6. 是否引入了一个只用来记"程序走到哪里"的字段？
   - 答案为"是"立即停工，改写为：普通局部变量 / match / 尾递归 / use! / Task / Channel / Semaphore / 真实资源句柄

# 18 战役迁移蓝图 (18-Battle Migration Blueprint)

## 战役 0：停火、立宪、封存
- **目标**：阻止旧系统和当前 next/ 继续同时生长。
- **动作**：给当前代码打不可变 tag `legacy-before-agent-dsl`；新增 `MIGRATION.md` 写明 SSOT 裁决顺序；冻结 `src/`、`tests/`、旧 integration、Mux/OMP/Mimocode。
- **必须测试**：旧插件仍能构建作为黑盒 Oracle；当前 tag 可复现。
- **出口**：所有人知道新功能只能进入清理后的 next/；MIGRATION.md 已建立行为总账。
- **禁止**：给旧 Fallback/Nudge/Subsession 增加新字段；修旧测试以提高断言数；为新旧兼容设计 adapter。

## 战役 1：抢救 TestKit
- **目标**：先把测试武器从旧军营搬出来。
- **动作**：创建 `testkit/opencode/`；搬迁并改名独立 env/ProcessHost/EventProbe/StrictMockProvider/Scenario/Diagnostics/StabilityChecker；所有插件路径由参数注入；TestKit 不 import `src/` 或 `next/`。
- **必须测试**：TESTKIT-ENV-ISOLATION、TESTKIT-STRICT-FIFO、TESTKIT-SSE-RECONNECT、TESTKIT-PID-LEAK-DETECTED、TESTKIT-DIAGNOSTICS-COMPLETE。
- **出口**：TestKit 可以在不知道 src/ 和 next/ 类型的情况下启动任意插件。
- **禁止**：搬旧 provider 里与 nudge/todo/fuzzy 绑定的 matcher；在 TestKit 里加新架构业务逻辑。

## 战役 2：清空假彼岸
- **目标**：把当前 next/ 从"两代架构混合物"清成真正 Agent DSL 工地。
- **动作**：删除 next/Journal、Driver、PromptProtocol、Script、旧 Review；重写 GuideContract 只允许 Flow/AgentId/Role/RunCompletion/CommandRequest 等最小类型。
- **必须测试**：ARCH-NO-LEGACY-IMPORT、ARCH-NO-JOURNAL、ARCH-NO-DRIVER、ARCH-NO-PROMPT-PROTOCOL、GUIDE-CONTRACT-COMPILES。
- **出口**：next/ 可以很少、暂时不能完成用户任务，但结构上已站在正确大陆。
- **禁止**：用 `[<Obsolete>]` 假删除；创建 `LegacyCompatibility.fs`。

## 战役 3：重建 Flow 和真实资源基座
- **目标**：建立所有 DSL 共用但不含业务的极小执行内核。
- **动作**：实现并验证 Return/Bind/Zero/Delay/Combine/TryWith/TryFinally/Using/While/For/run；提供 map/mapError/bind/attempt/parallel 组合函数。
- **必须测试**：Error 短路、throw 保持 throw、cancellation 保持 OCE、DisposeAsync 在所有路径下被 await、尾递归 10000 步、semaphore 异常释放。
- **禁止**：Flow AST、解释器、动态 Stage、Workflow Registry、序列化 continuation、通用 EventBus。

## 战役 4：OpenCode Host 生死 Spike（第一道生死关）
- **目标**：用真实 OpenCode 证明 SSOT 所依赖的宿主能力。
- **必须证明**：
  - 每次 LLM 请求前获得完整 outbound JSON → 生成 stable canonical JSON → 关闭官方 compaction → 用 B 替换历史前缀 → replacement 持续生效 → 未替换尾部保留。
  - 创建指定角色/模型的 child session → 先注册 terminal listener 再发送 prompt → prompt fire-and-forget → busy child 再收 prompt 在后续请求吸收 → child A 版正文可靠提取 → reviewer terminal 无 verdict 可识别。
  - parent abort 后找到并关闭 child → assistant terminal/idle/abort 事件序列可记录 → terminal listener 不依赖 fixed sleep。
- **失败处理**：任何一条关键能力不存在→停止业务开发→修改 Agent DSL 与 Host 边界→不允许用 Journal、轮询状态机或协调器掩盖宿主事实。

## 战役 5：ForkRuntime
- **目标**：实现 Manager 最核心的异步 DSL。
- **必须测试**：AG-FORK-RETURNS-BEFORE-CHILD、AG-LISTENER-BEFORE-SEND、AG-FAST-COMPLETION-NOT-LOST、AG-EXISTING-FORK-IS-NUDGE、AG-EXISTING-FORK-NEVER-BUSY、AG-JOIN-ANY、AG-COMPLETION-ONCE、AG-A-VERSION-EXCLUDES-REASONING、AG-PARENT-CANCEL-CLOSES-CHILDREN、AG-SIBLING-ISOLATION。
- **出口**：Manager 可以并行 fork 三个无工具 child 并通过三个 join 收回结果。
- **禁止**：Join(agentId)、RunId 协议平台、Prompt queue manager、AgentActor、AgentStateMachine。

## 战役 6：Companion / Blogger
- **目标**：实现 B 版工作记录和零等待前缀替换。
- **必须测试**：BLOG-CANONICAL-JSON-STABLE、BLOG-DELTA-JSON-LEVEL、BLOG-BUSY-SKIPS、BLOG-BUSY-DOES-NOT-ADVANCE-BASELINE、BLOG-B-CONTAINS-ONLY-Y-OUTPUT、BLOG-FAILURE-NEVER-BLOCKS-X、BLOG-REMEMBERED-PREFIX-REPLACEMENT、BLOG-SELF-REBASE-REMOVES-OLD-B、BLOG-NO-OFFICIAL-COMPACTION。
- **禁止**：PendingDeltaQueue、Watermark event protocol、Blogger cancellation、等待 Blogger 后再请求 X、独立 Compaction Coordinator。

## 战役 7：角色与工具表面
- **目标**：权限由静态装配决定，不是运行时判断。
- **必须测试**：每个角色做工具名称 snapshot（ROLE-MANAGER-EXACT-TOOLS 等），不仅测试"应该有"也测试"不应该有"。
- **禁止**：全局注册工具后靠 prompt 劝模型不用、运行时 permission switch、万能 AgentConfig。

## 战役 8：Process / Inspector / Executor
- **目标**：完成可信命令执行和大输出摘要。
- **冻结取舍**：大 estimate 合法不 clamp；Medium 不限并发；Large 同时一个 `SemaphoreSlim(1)`；唯一 timeout = 3×estimated_running_secs；SIGKILL 后不添加第二层 cleanup timeout；SIGKILL 无法收敛是实现 bug；200KB = 摘要 chunk 不是输出上限。
- **必须测试**：PROC-PUMP-INSTALLED-BEFORE-RETURN、PROC-EXACT-THREE-X-DEADLINE、PROC-HUGE-ESTIMATE-ACCEPTED、PROC-MEDIUM-CONCURRENT、PROC-LARGE-SERIAL、PROC-LARGE-LEASE-RELEASED-ON-ERROR、PROC-SIGKILL-TREE、PROC-SPOOL-COMPLETE-BYTES、PROC-CHUNK-200KB、PROC-MAP-REDUCE-SUMMARY、PROC-EXECUTOR-HAS-NO-TOOLS、PROC-NO-ORPHAN。
- **出口**：无限输出 + stdout/stderr 同时写 + fork 子进程 + 忽略 SIGTERM + 超时 + 500KB/10MB 输出 + Large 并发，全部无 hang 无孤儿。

## 战役 9：Coder / Inspector / Browser / Meditator
- **Coder**：有 B；文件工具；每次 inspector 调用创建一次性 Inspector；可并行启动多个 Inspector。
- **Inspector**：无 B；只持有 executor；command summary 作为 A 版正文。
- **Browser**：只读本地文件；只使用网页能力；不写工作区。
- **Meditator**：无方法论目录；只做自由推理；可 read/glob/grep/inspector；不强制输出固定章节。

## 战役 10：A/B Fallback
- **唯一内存**：`{ mutable Side: ModelSide; mutable Failures: int }`
- **必须测试**：FB-A-FIRST-RETRY-A、FB-A-SECOND-SWITCH-B、FB-SWITCH-PERMANENT、FB-B-FIRST-RETRY-B、FB-FOURTH-SESSION-DEAD、FB-SUCCESS-KEEPS-FAILURE-COUNT、FB-SUCCESS-KEEPS-SIDE、FB-PER-SESSION-ISOLATION。
- **禁止**：AcceptanceUnknown、Reconcile、FallbackPhase、RetryGovernor、Lease、Episode、Event log。

## 战役 11：Reviewer 与 ReviewGuard
- **必须测试**：REV-REVISE-IMMEDIATE、REV-FIRST-PERFECT-CHALLENGE、REV-SECOND-PERFECT-CONFIRMS、REV-TREE-CHANGE-INVALIDATES、REV-NO-VERDICT-NUDGES-SAME-REVIEWER、REV-A-TEXT-SEPARATE-FROM-VERDICT、MGR-FINISH-WITHOUT-REVIEW-NUDGED、MGR-FINISH-AFTER-CONFIRM-ALLOWED、MGR-EDIT-AFTER-CONFIRM-INVALIDATES。
- **禁止**：Todo、Review Registry、Review StateMachine、Review Event Fold、通用 Nudge service。

## 战役 12：PTY
- **工具表面**：仍然只有 fork，`signal` 使用结构化 enum：TERM/KILL。
- `list()` 同时列 agent 和 PTY；`join()` 返回任意 PTY operation completion 或 agent completion。
- **必须测试**：spawn、write、empty prompt read、structured signal、exit completion、agent/PTY 混合 list、parent abort、无魔法字符串、无重复 completion。

## 战役 13：Manager 纵向全链路（第一个 P0 canary）
- **场景**：用户要求修改 fixture → Manager 并行 fork Inspector/Coder → join 任意结果 → nudge Coder → Coder 调一次性 Inspector → Coder 修改文件 → Manager fork Reviewer → 双 PERFECT → Manager Guard 放行。
- **强制检查**：Manager 从未看到文件工具；Blogger 忙时不阻塞；无 todo；无旧 Journal；无官方 compaction；无进程/session 泄漏。

## 战役 14：Orchestrator
- **必须测试**：ORCH-DIRTY-REJECTS-BEFORE-FORK、ORCH-FORK-RETURNS-IMMEDIATELY、ORCH-MANAGERS-WORK-CONCURRENTLY、ORCH-PUBLISH-SERIAL、ORCH-INITIAL-DOUBLE-PERFECT、ORCH-REBASE-TO-LATEST、ORCH-CONFLICT-RETURNS-SAME-MANAGER、ORCH-POST-REBASE-DOUBLE-PERFECT、ORCH-FF-ONLY、ORCH-CLEANUP-WORKTREE。
- **禁止**：DAG、Wave、Task scheduler、Squad state、HTTP control plane、MergeOrder recovery state。

## 战役 15：新 E2E 军团
- **L0 纯函数**：Flow、JSON delta、fallback、double PERFECT、role matrix。
- **L1 Port/Fake**：ForkRuntime、Process orchestration、Orchestrator program、Git failure branches。
- **L2 真实 Node/Process**：spool、SIGKILL、PTY、process tree、semaphore。
- **L3 真实 OpenCode + Mock LLM**：projection、child sessions、busy nudge、join、blogger、role tools、reviewer、manager。
- **L4 真实 Git E2E**：并行 worktree、conflict、rebase、re-review、ff。
- **稳定性门禁**：P0 场景连续运行 20 次 + 随机延迟 + 随机完成顺序 + 无 fixed sleep + 无泄漏。

## 战役 16：切换生产入口
- **动作**：新 package build 完整通过 → 完整 P0 通过 → 修改 package export 指向 next/Host/OpenCode/Plugin → README 重写 → 删除旧 /loop/todo/methodology/fuzzy → 不设 feature flag → 不做 live shadow 双跑。
- **切换门禁**：生产 build 无 src import、工具表面 snapshot 精确、官方 compaction 关闭、Manager 全链路通过、Process 压力通过、ReviewGuard 通过、Orchestrator 通过、3× stability 通过；可用 `CANARY_REPEAT` 提高稳定性门槛。

## 战役 17：总清算
- **删除顺序**：旧 OpenCode src/Hosts → 旧 integration → 旧 unit tests → 旧 Fallback/Nudge/Subsession Kernel → 旧 Runtime → 旧 Methodology/Todo/Caps/fuzzy → 万象阵 DAG/server/lock → Mux/OMP/Mimocode → 旧 fsproj/build scripts/README/空目录。
- **保留**：next/、tests-next/、testkit/、next/Doc/SSOT.md、host-docs/。
- **最终零残留检查**：
  ```
  rg "SessionActor|SubsessionActor|FallbackPhase|Lease|Owner|Generation" next tests-next
  rg "Todo|Methodology|fuzzy_|Squad|Wave" next tests-next
  rg "experimental.session.compacting|autocontinue" next
  rg "src/" next tests-next testkit
  ```
  结果必须为空或仅出现在专门的 forbidden-source architecture test 中。

## 战役 18：其他宿主重生（非关键路径）
- OpenCode 稳定一个正式周期后，再决定 Host/Mux / Host/Omp / Host/Mimocode。
- 每个宿主只能实现：projection / sessions / events / tools / plugin composition。
- 不得把旧宿主代码复制回来。

# 技术风险与关键约束 (5 Technical Risks)

## 风险一：投影 hook 不够强
- 如果无法在每次请求前看到并替换完整 outbound JSON，Companion 主方案不成立。
- **处置**：战役 4 最先验证，不得后移。

## 风险二：busy child 的 fire-and-forget prompt 行为与想象不同
- 必须验证：是否接受、何时进入后续 LLM 请求、是否改变事件顺序、abort 时如何表现。
- **处置**：真实 OpenCode E2E，不建立自制 queue 补偿。

## 风险三：terminal 与 verdict 时序
- Reviewer 可能：verdict 先到 / assistant terminal 先到 / terminal 无 verdict / abort 后迟到。
- **处置**：本地 listener + completion once，不建立 Review StateMachine。

## 风险四：SIGKILL 与 process tree
- 真正困难：process group / descendant / pipe EOF / PTY / Windows/macOS/Linux 差异。
- **处置**：真实进程压力测试，不用第二层 timeout 掩盖。

## 风险五：rebase 后语义变化
- 无文本冲突不等于无需复审。
- **处置**：rebase 后强制重新双 PERFECT，之后才能 ff。

# 四类资产分类 (Asset Classification)

## A 类：黄金资产（原样或轻改搬迁）
- **A1 OpenCode E2E TestKit**：isolated-env / process-host / event-probe / strict-mock / scenario / diagnostics / stability-checker。每场景独立 HOME/XDG/workspace；严格 FIFO；SSE 事件探针；无固定 sleep；PID/端口/子进程泄漏检测。
- **A2 Host 调研资料**：`host-docs/` 保留为事实调查资料（投影 hook、message/part 身份、assistant terminal、abort、compaction 关闭）。
- **A3 Flow 内核思想与部分测试**：`Flow<'ctx,'error,'a> = 'ctx -> CancellationToken -> Task<Result<>>` 方向正确。保留契约：Bind 只负责短路、OCE 不伪装业务错误、use! 管理异步资源、尾递归表达重试。
- **A4 Process 与 Git 极少量原语**：Command 类型、Deadline 绝对时间思想、spawn 前后泵测试场景、Large semaphore 概念。

## B 类：银矿资产（只提炼场景，不搬代码和类型）
- **B1 Subagent/Subsession** → 提炼：listener 先于 prompt send、快速完成不能丢、父取消清理、sibling 物理隔离、terminal 只一次、迟到事件不重复 completion。
- **B2 Executor/PTY** → 提炼：spawn 后立即 pump、200KB 分块摘要、3× deadline、SIGKILL 进程树、Medium 不限、Large 单例、PTY write/read/signal、dispose 无孤儿。
- **B3 Fallback** → 只保留：A/B 模型选择、模型参数注入、哪些 provider 错误算一次失败、session 内持续计数。
- **B4 Review** → 只保留：REVISE 立即生效、PERFECT 请求确认、同一 tree 第二次才通过、tree 改变失效、reviewer 无 verdict nudge、manager 无审查 nudge。
- **B5 文件工具/Browser/角色权限** → 可提炼：Unicode、空文件、路径边界、write/edit 原子性、角色可见工具快照。

## C 类：负债（测试与实现一起删除）
- **C1 旧控制状态**：Stage、Phase、Lease、Owner、Generation、Coordinator、Governor、Actor、Registry-driven workflow、SessionControl、各种 StateMachine。
- **C2 旧领域**：Todo SSOT、select_methodology、Methodology catalog、通用 Nudge、Caps、fuzzy_*、ContextBudget、官方 compaction 集成、万象阵 DAG/wave/scheduler、Squad HTTP 控制面、EventStore 驱动工作流。
- **C3 旧恢复幻想**：把执行到哪个 Stage 持久化、把 owner/lease/generation 当作恢复依据、恢复 pending continuation、跨 Runtime 实时协调、把日志投影当作正在运行的程序。

## D 类：冻结资产（不参与当前战争）
- Mux、OMP、Mimocode 相关实现和测试。立即冻结，不重构，不为了复用污染新 Kernel，OpenCode 稳定后从主干删除旧实现。将来需要支持其他宿主时重新从 Host Adapter 接入。

# 最终胜利条件 (18 条)

1. SSOT.md 每条规范都有 Behavior ID。
2. 所有 Behavior ID 有正确层级的测试。
3. Manager 只看 `fork/join/list`。
4. Orchestrator 只看 `fork/join`。
5. Reviewer 只用结构化 `PERFECT/REVISE`。
6. Blogger 通过投影维护 B，忙时零阻塞。
7. 官方 compaction 完全关闭。
8. Process 只有一个 `3×estimate` deadline。
9. 大输出完整 spool，按 200KB 摘要。
10. Fallback 只有 A/B 和累计四失败。
11. ReviewGuard 同时守 Manager 和 Reviewer。
12. PTY 使用结构化 fork 参数。
13. Manager worktree 经 rebase 后重新双 PERFECT。
14. 发布严格 ff-only。
15. next/ 中不存在旧 Journal/Driver/Actor/StateMachine。
16. 生产 package 不再引用 src/。
17. src/、旧测试和旧宿主实现从主干删除。
18. 整个旧时代只存在于 Git 历史。

# 总指挥最终裁决

# 补充架构规范


## 必须否决的四项原始建议 (Four Vetoed Proposals)
1. 只用 B 压缩而不带 raw tail → 必须 B + 未覆盖尾部。
2. 把所有 blogger 输出永久拼接 → 必须区分 BlogCheckpoint + BlogSegments，B = BlogCheckpoint + BlogSegments。
3. `executor` 角色与 `executor` 工具同名 → 最终裁决保留 executor 角色只做 summarizer。
4. 用 `[#SIGTERM]` 一类魔法字符串控制 PTY → 必须使用结构化 `signal="TERM"` enum。

## 新架构优于旧万象阵的核心差异
| 维度 | 旧架构 | 新架构 |
| :--- | :--- | :--- |
| 子代理调用 | 同步伪装工具 | 自然异步 fork/join |
| 控制流持久化 | Stage/Phase/Lease/Owner/Generation | 结构化 F# 程序 + 尾递归 |
| 调度器 | todowrite SSOT | fork/join 表达任意 DAG |
| DAG 平台 | 独立万象阵 | Orchestrator 即上层 Manager |
| 协调角色 | Manager+万象阵 | Manager(纯协调)+专业代理(执行) |
| 语义保留 | 全部塞进"全局状态" | Git/子会话/进程/输出各自真实语义 |
| 上下文压缩 | 官方 compaction agent (90%触发) | Blogger 增量写作 + 零等待前缀替换 |
| 审查闭环 | Nudge 通用调度器 | ReviewGuard 纯门禁 + Git tree hash |
| 资产分类 | 全部保留 | 黄金/银矿/负债/冻结四类 |

## fork/join 表达能力证明
Manager 通过 fork/join 序列：
```
fork → fork → fork → join → 根据结果继续 fork → join
```
可以表达：并行调查、串行依赖、投机执行、多轮修订、Map/Reduce、审查闭环、动态任务分解。
表达能力比固定万象阵 DAG 更强，但天然恢复能力更弱——依赖图存在于对话和 fork 表中，进程重启后需单独决定恢复策略。

## join() 返回结构
```
{
  agent_id: string,      // 物理伴随子会话
  run_id: string,         // 该子会话本次请求
  role: AgentRole,        // coder/inspector/reviewer/pty
  status: RunStatus,      // Completed/Failed/Cancelled
  output: string,         // 本 Run 的 A 版工作记录
  verdict?: ReviewVerdict // reviewer 才有 PERFECT/REVISE
}
```
六位十六进制 ID 可保留为 UI 简写，但生成时必须检测碰撞。内部使用完整随机 ID。

## Agent 生命周期二分
| 对象 | 寿命 |
| :--- | :--- |
| 物理 agent session | 可多次继续 |
| 单次 Run attachment | 一次 fork 到 terminal |
`join()` 消费的是 Run completion，不是销毁 agent。只有父 session 结束、明确 close 或用户 abort 时才关闭物理 agent。

## 旧架构删除时机 (Phase 8 细化)
当新架构覆盖以下全部场景后方可删除旧实现：
1. 普通编码（Coder 写文件）
2. 并行调查（Manager fork 多个 Inspector）
3. Reviewer 循环（双 PERFECT 确认）
4. Fallback（累计四失败 A/B 切换）
5. Context replacement（Companion 前缀替换）
6. PTY（结构化 fork 参数）
7. 多 worktree Manager（Orchestrator 并行隔离）

一次性删除清单：
```
todowrite SSOT / select_methodology / SubsessionActor 旧层
同步 coder/inspector/browser 工具 / 通用 nudge / fuzzy_*
万象阵 DAG/Scheduler/HTTP 控制面 / ContextBudget/Compaction 协调器
```

## 单元测试分层 (L0-L4)
- L0 纯函数：Flow、JSON delta、fallback、double PERFECT、role matrix。
- L1 Port/Fake：ForkRuntime、Process orchestration、Orchestrator program、Git failure branches。
- L2 真实 Node/Process：spool、SIGKILL、PTY、process tree、semaphore。
- L3 真实 OpenCode + Mock LLM：projection、child sessions、busy nudge、join、blogger、role tools、reviewer、manager。
- L4 真实 Git E2E：并行 worktree、conflict、rebase、re-review、ff。

稳定性门禁：P0 场景连续运行 20 次 + 随机 LLM completion 延迟 + 随机 child 完成顺序 + 无 fixed sleep + 无 PID/port/session/worktree 泄漏。

## 四条迁移纪律
### 纪律一：搬行为不搬目录形状
旧 SubsessionActor → 新 ImprovedSubsessionActor? 禁止。旧测试"快速完成不能丢" → AG-JOIN-FAST-COMPLETION。
### 纪律二：旧实现只当黑盒 Oracle
禁止新代码 import src/、新旧共享状态、双写、feature flag 混合 Runtime。
### 纪律三：每个旧测试三种结局
Keep TestKit / Port Behavior / Obsolete。无"暂时全保留"。
### 纪律四：新测试不继承11800断言目标
覆盖标准=SSOT每条款有Behavior ID、每ID有最低层测试、跨边界有真实E2E。

## ForkRuntime持久化分界
持久化：AgentLinked(parentSessionId,agentId,childSessionId,role)。
不持久化：Task句柄/terminal listener/completion Channel/join waiter/busy缓存。
重启恢复：Fold AgentLinked→查询OpenCode child→terminal则补completion/still running则重装listener/missing则标记不可用。

## Architecture Gate关键词规则
允许：fork/join/nudgeExisting/completionChannel/ReviewGuard/Journal/Fact/Fold/Projection/PerRuntimeWriter/BootSnapshot。
禁止：ReviewPhase/FallbackPhase/SessionStage/JoinOwner/NudgeLease/CompactionGeneration/SessionActor/SubsessionActor/WorkflowRegistry/JournalDrivenWorkflow/TodoState/Methodology/SquadWave。
文件门禁：F#源文件超300行ArchitectureGates必须报红。AGENTS.md豁免。

## Verdict去重
同一PERFECT ToolCallId重复投递=两次PERFECT。Fold必须保留第一和第二ToolCallId并要求不同。ReviewerHost.SubmitVerdict先查Projection再append Journal。

## Coder同步Inspector边界
Manager→Coder(异步)→Inspector(一次性同步)→Command(同步)。Coder每次创建一次性inspector session，并行调用各自独立。

## Behavior ID测试索引
| 组件 | Behavior ID | 验证 |
| :--- | :--- | :--- |
| TestKit | TESTKIT-ENV-ISOLATION | 每场景独立HOME/XDG/workspace |
| TestKit | TESTKIT-STRICT-FIFO | 严格FIFO |
| TestKit | TESTKIT-SSE-RECONNECT | SSE重连 |
| TestKit | TESTKIT-PID-LEAK | PID/端口泄漏检测 |
| ForkRuntime | AG-FORK-RETURNS-BEFORE-CHILD | fork在child完成前返回 |
| ForkRuntime | AG-LISTENER-BEFORE-SEND | listener先于prompt |
| ForkRuntime | AG-FAST-COMPLETION-NOT-LOST | 快速完成不丢失 |
| ForkRuntime | AG-EXISTING-FORK-IS-NUDGE | existing fork=nudge |
| ForkRuntime | AG-EXISTING-FORK-NEVER-BUSY | nudge不返回Busy |
| ForkRuntime | AG-JOIN-ANY | join等任意completion |
| ForkRuntime | AG-COMPLETION-ONCE | completion恰好一次 |
| ForkRuntime | AG-PARENT-CANCEL | 父取消清理子 |
| ForkRuntime | AG-SIBLING-ISOLATION | sibling隔离 |
| Companion | BLOG-CANONICAL-JSON | canonical JSON稳定 |
| Companion | BLOG-DELTA-JSON | JSON级delta |
| Companion | BLOG-BUSY-SKIPS | 忙时跳过 |
| Companion | BLOG-B-CONTAINS-ONLY-Y | B只含Y正文 |
| Companion | BLOG-FAILURE-NEVER-BLOCKS-X | 失败不阻塞X |
| Companion | BLOG-REMEMBERED-REPLACEMENT | 记住前缀替换 |
| Companion | BLOG-SELF-REBASE | 自重基移除旧B |
| Companion | BLOG-NO-COMPACTION | 无官方compaction |
| Process | PROC-PUMP-BEFORE-RETURN | pump在返前安装 |
| Process | PROC-THREE-X-DEADLINE | 唯一3x deadline |
| Process | PROC-HUGE-ESTIMATE | 大estimate合法 |
| Process | PROC-MEDIUM-CONCURRENT | Medium不限并发 |
| Process | PROC-LARGE-SERIAL | Large全局单例 |
| Process | PROC-SIGKILL-TREE | SIGKILL进程树 |
| Process | PROC-SPOOL-COMPLETE | 完整输出入spool |
| Process | PROC-CHUNK-200KB | 200KB分块 |
| Process | PROC-MAP-REDUCE | Map/Reduce摘要 |
| Process | PROC-NO-ORPHAN | 无孤儿进程 |
| Review | REV-REVISE-IMMEDIATE | REVISE立即生效 |
| Review | REV-FIRST-PERFECT-CHALLENGE | 首次PERFECT需确认 |
| Review | REV-SECOND-PERFECT-CONFIRMS | 二次PERFECT确认 |
| Review | REV-TREE-CHANGE-INVALIDATES | tree变化失效 |
| Review | REV-NO-VERDICT-NUDGE | 无verdict nudge |
| Fallback | FB-A-FIRST-RETRY-A | A失败重试A |
| Fallback | FB-A-SECOND-SWITCH-B | A二次失败切B |
| Fallback | FB-SWITCH-PERMANENT | 切B永不回A |
| Fallback | FB-FOURTH-DEAD | 四次SessionDead |
| Fallback | FB-SUCCESS-KEEPS-COUNT | 成功不清零 |
| Orchestrator | ORCH-DIRTY-REJECTS | 脏工作区拒绝 |
| Orchestrator | ORCH-CONFLICT-SAME-MANAGER | 冲突回交同Manager |
| Orchestrator | ORCH-REBASE-DOUBLE-PERFECT | rebase后双PERFECT |
| Orchestrator | ORCH-FF-ONLY | 严格ff-only |
| Architecture | ARCH-NO-LEGACY-IMPORT | 无旧import |
| Architecture | ARCH-NO-JOURNAL-DRIVEN | 无Journal驱动 |

旧代码不渡河。旧状态机不渡河。旧断言数量不渡河。只有经过鉴定的经验、场景和基础设施渡河。

唯一的桥 = 独立 TestKit + 行为总账 + 一条条由新测试接管的外部契约。

当前版本值得保留作为新大陆的地基，但上面的样板房全部不验收。下一份代码必须证明它闭合了一个真实的产品纵切，而不仅仅是目录看起来像最终架构。

---
# 当前审计结论

本节替代旧审计草稿，只记录当前代码与最新验证。不得把历史审计中的旧缺口重新当成事实。

## 当前已证实

- npm run test:release 已通过：Fable build、tests-next 136/136、Manager contract 1/1、TestKit 11/11、P0 全链路。
- P0 Manager DSL 默认稳定性门槛为 3×；三次 Manager→Coder→Join 均通过。CANARY_REPEAT 可提高门槛；3×不等价于 release-ready。
- Manager provider request 只暴露 fork/join/list；read/write/edit/bash/glob/grep/verdict 均被真实请求断言为不可见。
- Host child 已支持 listener-before-send、per-run terminal、A 版增量切片、existing-agent nudge 的运行时路径；真实同一 child 双轮 nudge canary 已通过，HostEventRouter 已接通 parent abort→登记 child abort。
- ISessionHostPort 已显式提供 AbortChildren，InjectedSessionPort 以真实 parent-child 表递归取消；npm test 为 136/136，Fable fake 不再依赖不存在的 CompletedTask 导出。
- InjectedSessionPort.AbortSession 现等待 AbortChildren 完成后才返回，避免 parent 已完成而 child 仍在清理；真实 Host parent-abort 事件仍未闭合。
- Companion 真实 Blogger child 已完成 B1/B2；同一 Blogger 被复用，Blogger/Executor/Inspector/Browser/Meditator/Reviewer sidecar 被阻断；B、JSON baseline、replacement flag 有 Port/Fake 重启测试。
- Reviewer verdict 已读取真实工作区指纹、append Journal、按 ToolCallId 去重；同 tree 两次不同 PERFECT 才确认。真实 Reviewer canary 通过。
- Inspector executor 已接入真实 shell Runner；大输出按 3×estimated_output_bytes 阈值 spool，按 200KB map/reduce 到无工具 Executor；真实 canary 通过。
- Process 本地测试覆盖唯一 3×estimated_running_secs deadline、SIGKILL、pipe EOF、Large gate、Medium 并发、PTY 基础路径。
- Fallback A1→B2→B3→Dead、ReviewGuard 双 PERFECT、Journal Boot/Fold、Projection 有界槽位、Orchestrator durable Port 逻辑均有测试。

## 当前未闭合边界

- 真实 parent abort、迟到 terminal/reasoning-part 混合仍需独立 OpenCode E2E；当前已用真实同一 child 三轮 nudge canary 证明 per-run listener，已有 L0/Fake 覆盖不等价于完整 Host 证明。
- 真实跨重启 Host reconcile 尚未闭合：Journal 已能 Boot/Fold，Runtime 重新连接仍需真实 child transcript 验证。
- Fallback durable 事实尚未接入真实 provider failure 注入；当前只证明纯规则和 durable Fold。
- SIGKILL/PTY/大输入/孤儿进程的长压稳定性尚未纳入默认 P0。
- Orchestrator 已完成 durable facts、worktree/rebase/冲突回交/复审/ff-only 的 Port 路径；真实 OpenCode Manager worktree 发布 E2E 尚未闭合。
- 因此禁止宣称 production release-ready，禁止提前删除仍可作为黑盒 Oracle 的旧测试资产。

## 下一步顺序

1. 先补真实 parent abort、nudge 三轮、跨重启 reconcile 的 OpenCode 场景。
2. 再接真实 provider failure，证明 A/B Fallback 累计失败跨请求与重启不洗白。
3. 加入 Process/PTY 压力与泄漏门禁。
4. 闭合真实 Orchestrator 发布 E2E；rebase 后必须重新双 PERFECT，再允许 ff-only。
5. 所有边界通过后才切换 production entry、清理旧实现与旧测试。

## 资产处理纪律

- testkit/opencode、host-docs、Journal codec/Boot/Fold 测试、Process/PTY 故障场景、OpenCode 事件 fixture 是资产。
- 旧测试只按行为迁移：保留测试基础设施、提炼外部契约、淘汰绑定 Stage/Phase/Lease/Actor/Todo/Methodology/fuzzy/Squad 的实现与断言。
- 事件溯源只保存跨重启领域事实；结构化 Flow/普通程序负责当前控制流；不得把 ReviewPhase、FallbackPhase、JoinOwner、NudgeLease、CompactionGeneration 写进 Journal。
- 每次功能闭合必须：更新本文件 → npm test/目标 E2E → 单独 commit → push。未经直接证据不得上调完成状态。
