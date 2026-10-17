# 规划底稿：js- 编程面演进为唯一工具（S1 事实与取舍·收口版）

## 阅读指南（给继任者）

本稿是 S1 的收口版：诊断（为什么 LLM 不太会用 js-）已完成为设计（js- 成为整个系统唯一工具）的输入。二十七轮用户输入把蓝图逐块收敛，全部终裁都在「设计定稿全景」段——那是 S2 的直接输入，每条都已拍板，不要重新发散。「S2 待设计清单」是用户明确下放给后续阶段的设计点。「已核实的事实」是五轮 devops 只读调查的证据链。本任务 S1 阶段曾获用户明示：S1 是唯一可用 ask 的阶段，S2/S3/S4 没有 ask——需要用户拍板的都已在此稿中，后续阶段自主。

## 任务来源与用户输入全录（2026-10-17，二十八轮）

1. 初始委托：@Done.md（上一任务：js- 编程面 as-is + park/unpark/abandon 机制）已经做完了，但 LLM 好像不太会用。调查，然后讨论怎么办。
2. 纠正：现在的 js- 工具远没这么简单——第一轮漏掉了机制面（三态编排、as-is、rm/mv、finalizer、门禁）。
3. 设计价值观：用接口代码讲语义、用示例代码讲用法，现身说法——原范式很好，屡次重构中教学渐渐掉队。
4. 意图一：js- 是 todowrite 的活替代品（死的清单 → 活的程序）。
5. 三项拍板：修复范围连带 fragment 层与测试；unpark.value 放宽 free-form；外部强化暂不动。
6. 意图二（终极愿景）：三大支柱——js.* 结构化返回、js.fork/js.join 与 DAG 调度器、watchdog 剧本（禁随意后台、全流程剧本、10 秒放手、sanity check、显式上界）；终局：js- 成为整个系统唯一工具。委托挖掘痛点（得 17 条）。
7. 意图三（ANCHOR）+ 指令「S1 是头脑风暴，先发散」：压缩可用 js- 表达——[ANCHOR: N] 流水号 + { PrefixUntil, Extra } 视图声明；LWR 全线退役；ultimate universal。
8. ANCHOR 细化拍板 + 吞并调查委托（得 24 项机制盘点）：进程内全局虚时间戳；每次 js- 调用都带下次期望上下文；token 经济汇报；无硬底线；js.fission 在蓝图；defer/publish 退役。
9. mailbox 整体退役；每回合注入机制保留；不准静默停止保留且更严；只有 js.ask 可以有序停止——js.ask 与 js.park 同构（park 等 AI、ask 等物理人），无 unask，回复后与 park 合流。
10. ask 后行为不限制；「更严」= 任何 AI 不准静默停止（基础哲学），subagent 必须显式退出；S1 是唯一可用 ask 的阶段。
11. 五问终答：exit 就是 Done，改名 js.deliver 与主对话 deliver 合流（设计委托规划者）；DAG 用 fork/join 原语搭建不造底层麻烦；watchdog 设为 js.run 必选参数（规格即指导）；重启允许丢失但剧本中多 park 自己；分期实现者决定。
12. return 语义澄清：return 只是交回控制权，与结束无关——程序分段是默认轻协作（先写一部分 return 掉，下次写剩余）；letgo 换词；sanity check 面向对象化（委托研究）。
13. Ultra Example 委托：wget 批量下载真实场景（断点续传、停顿/过慢处置、报错分派、串行）——用它想清楚 watchdog 与 js.run，也是将来的教学示例；工具描述预算充足。
14. watchdog 不可选：用 js.run 就必须写 watchdog——强制 Runnable 类，watchdog 必须实现。
15. js.run 命名与 js.* 一致 vs OO 清晰——平衡委托。
16. 巨类裁决：不要 3 个文件 3 次 js.run——一个巨类装整个工程；复杂任务（npm server+前端测试）是系统化工程不是脚本。
17. 终原则：为强制 watchdog，控制流一定归万象术，LLM 只负责具体语义的回调。
18. 内联类：js.run 后面内联写类——LLM 写到调用点才决定展开，提前写好类 LLM 不高兴。
19. 原语集重构：file/edit/rewrite/write/glob/grep 太不 DRY 太不正交——cat/ls/fuzzyPosition/printf/mkdir 即齐；edit 不过是 cat→fuzzyPosition→拼接→printf；find/replace 组合写进教程。
20. 命名意图：贴近 POSIX，LLM 天然知道怎么用（如 mv/rm）。
21. 开放委托：用户对 POSIX 也不太懂，有更漂亮的工具组可以提（规划者提：printf→write、fuzzyPosition→fuzzy、补 stat）。
22. stat 和 write 认可；fuzzy 不如 where（意图 vs 算法）；模糊查找用 fff 库实现。
23. where 返回全部匹配 [[begin,end],...]；担忧 [0] 偷懒产生惊讶结果。
24. where = whereAll 语法糖：恰一个拿到它，否则 park(whereAll 结果)——「除了 LLM 自己，谁知道多个怎么办」。
25. 零个当然也 park——可能是拼错了，AI 可以 unpark(where("new string"))（注入自己的劳动产物）。
26. unpark 也是 js.unpark——注入任意值恢复原来的控制流（统一性枢纽）。
27. parked 载荷含完整 js- 程序 + 执行点标记（>| 当前行、| 其他行）——信息完备替代防御；注入错就让它 throw，Unix 哲学：你永远自己负责。指令：大修底稿（只读约束是系统噪音），多几次调用彻底修好，保姆级说清楚。
28. 工具改名：js-xxx 改名为 todowrite-for-xxx 工具——充分利用 llm 对 todowrite 的迷恋（命名即教学的门面级应用；与第 4 轮「js- 是活的 todowrite」定位、五期原生 todowrite 退役三者合流）。

## 用户拍板总录（按主题归并）

- **范式**（3 轮）：双轨教学——接口代码讲语义 + 示例代码讲用法（现身说法）。教学掉队是根因，修复方向是按范式补齐，不是描述瘦身（已废弃瘦身方向）。
- **流程**（7/10 轮）：S1 发散；S1 是唯一可用 ask 的阶段（S2/S3/S4 无 ask）；分期实现者决定。
- **静默停止禁令**（9/10 轮）：基础哲学——任何 AI 不准静默停止；subagent 必须 js.deliver 显式退出；只有 js.ask 可以有序停止。
- **责任哲学**（27 轮）：Unix 哲学——机制给全部信息，不替使用者防错；拒绝防御模式。
- **命名**（20/21/22 轮）：贴近 POSIX（先验免费，命名即教学）；write（弃 printf——format string 陷阱）；where（弃 fuzzy——原语名说意图不说算法）；fff 库实现模糊；stat 补入。
- **工具名改版**（28 轮）：js-ROLE → todowrite-for-ROLE——LLM 对 todowrite 的先验迷恋是免费教学红利，门面级「命名即教学」。
- **退役**（8/9 轮）：旧六成员（file/edit/rewrite/write/glob/grep）、mailbox 整套（concern 邮箱/公告）、defer、publish、LWR、Blogger 压缩职责（半退役：chronicle 记账保留）、todowrite。

## 设计定稿全景（S2 直接输入，全部已拍板）

### 一、文件原语面（锁版）

九个原语，每个一个动词一个语义，全部 Unix 先验（命名即教学）：

```
cat(path)                        → 内容（严格 UTF-8）
ls(path)                         → [条目]（目录以 / 结尾）
stat(path)                       → 元信息（exists/size/mtime/isDir）
write(path, content)             → 落盘（覆盖写；append = cat+拼接+write 组合）
mkdir(path)                      → 建目录
mv(from, to) / rm(path)          → 结构操作（已有成员，POSIX 语义保留）
whereAll(content, needle, from?) → [[begin, end], ...]（全部匹配；零个返回 []）
where(content, needle, from?)    → [begin, end]
```

**where 的语义（歧义即 park，永不抛定位错误、永不静默）**：
- 唯一匹配 → 直接返回 [b, e]。
- 零匹配 → park({ needle, candidates: [], nearest })——AI 拿诊断、自己 cat 重看（复述拼错的最终兜底）、unpark 注入正确 span。
- 多匹配 → park({ needle, candidates: [{ span, context }] })——AI 挑选、unpark 注入选中 span。
- 三种结局全部收敛为「返回一个 span」；程序视角 where 总是成功，歧义处理完全黑盒。
- 这是 mechanical-semantic 的原语化：机械分支（唯一匹配）程序自己拿，语义分支（歧义）park 给 AI。
- 模糊语义经 fff 库实现（方向：规范化精确匹配——空白折叠等；精确定义 S2）。
- where/whereAll 命名先验：querySelector/querySelectorAll。

**组合模式（教程内容，替代旧 edit 专有 DSL）**：edit = cat→where→slice 拼接→write；批量替换 = while 循环逐个消费 where；锚点切片 = where(needle) + where(needle2, from)；append/touch/grep/glob 全为组合。旧词汇表（find/put/all、锚点代数、EDIT_NOT_FOUND/EDIT_AMBIGUOUS 诊断族）整体退役——其中唯一性校验哲学下沉为 where 的默认行为。

### 二、生命周期与协作代数

**协作代数**：return（回合内交回控制权，与结束无关）< park（保活挂起现场）< deliver（宣告终值有序退出）；配合 abandon（主动放弃）与 reap（被动收割）。收尾代数：deliver | abandon | reap，park 非终态。

**程序分段模式**（默认轻协作）：不可能一张蓝图画到底——先写一部分 return 掉，下次 js- 再写剩余；状态重建便宜用分段，状态昂贵才 park（显式 park 是重武器）。原语内建 park（where 歧义裁决）是日常控制流，与显式 park 分层并存。

**park/unpark**：
- park(payload)：程序内 await，现场保活（ParkedSites），工具返回 parked 载荷。
- unpark：编排层入参 { unpark: { handle, value } }；value 任意 JSON——**万能接缝枢纽**：where 注入 span、js.ask 注入 AI 消化的答复、剧本注入裁决，同一机制任意值；注入即裁决，裁决可含 AI 劳动（AI 可先 cat、跑辅助程序再注入产物）；注入契约由 park 点定义。
- **parked 载荷定稿**：handle + payload + 完整程序源码带执行点标记（`>|` 当前执行行、` | ` 其他行）——调试器当前帧先验，信息完备替代机制防御；AI 据此知道程序在等什么、注入什么形态。
- 无防御模式（Unix 哲学）：注入错就让它 throw，失败路径带完整诊断（as-is 已发生改动保留），AI 自己负责。
- 一次性 handle、HANDLE_UNKNOWN、清理期 CANNOT_PARK_WHILE_ABORTONING、并发现场上限、交付门禁、每回合提醒、会话终结 reap——已实现语义全部保留。

**js.ask**（等物理人的 park）：程序 await js.ask(payload) → 触发 attempt 中断（与 park 唯一差异：物理时间流逝）→ 现场同表登记 → 物理人回复到达 → AI 新回合（看到挂起现场+用户回复）→ AI unpark 注入（用户原话或 AI 消化裁决）→ 程序继续。无 unask（物理人的话天然到达 AI，AI 是程序世界唯一中介——程序黑盒不破）。ask 后行为不限制（等价 park 后）。现有 ask 全链地基（UserQuestionJournal、等待门禁 work [049]）对齐。

**js.deliver**（有序结束 + 终值 + 接收方视图，三层级合流）：
- 程序级：主会话程序 return 即 Done（无需 deliver 成员）；subagent 程序必须以 js.deliver 显式结束（静默 return 不算退出）。
- subagent 级：js.deliver 终值 → 宿主收割为 HandleWorkCompleted 主动形态，终值 BlobRef+Digest 落 durable。
- 主会话级：现有 deliver 工具对齐改造——入参加接收方视图 { PrefixUntil, Extra }（替代 LWR 交接），交接产物结构化，durable 事件族统一（HandleWorkCompleted / 程序 Done / Plan 交接同族语义）。
- 交付门禁三层级同规（有未表态 parked 现场不能 deliver）。

### 三、工程面（js.run 巨类 / fork / join / DAG）

**终原则（第十七轮）**：为强制 watchdog，**控制流一定归万象术，LLM 只负责具体语义的回调**——宿主拥有执行循环（watchdog 必跑、上界必执行、时间片必切），LLM 的巨类是语义回调集合。控制权与语义分离：宿主保不变量，模型声明语义。

**js.Runnable 契约（规格即指导）**：
- 必选（缺一 → RUNNABLE_SPEC_INCOMPLETE 拒绝执行）：maxDuration（工程总上界）、next(ctx)（返回下一条命令或 null=完工）、check(ctx)（每个时间片末的监督——watchdog 强制）。
- 可选：interval（时间片，默认 10s）、onExit(r)（每条命令退出处置）、onTimeout（默认 abort）。
- 宿主循环：next() → spawn → [interval → check()] → 退出 → onExit() → next() → …直到 next 返回 null 或 maxDuration。watchdog 不可能被绕过——循环在宿主手里。

**内联类形态（第十八轮）**：`await js.run(class extends js.Runnable { ... })`——匿名类表达式内联在调用参数位，LLM 流式生成写到调用点才展开工程规格（决策局部性）；复用同一工程类时 `const P = class extends js.Runnable {...}` 先赋值再传。内联为教学主形态。

**next() 返回代数**（多进程工程预留）：string（前台跑到退出）| null（完工）| { spawn, name }（起后台进程立即继续，如 server）| { waitFor }（等条件成立，如端口就绪）| { signal, name }（收割后台进程）。check 同时监督后台进程（崩溃检测）。

**check(ctx)**：状态在实例字段（this.lastSize、this.stalled——跨时间片保持，LLM 一看就会）；ctx 观察面 = stdout/stderr 增量、exited/exitCode、stat（便利原语，全能面用 js.* 成员）；verdict 三值：continue | abort(reason) | park(payload)——park 是「多 park 自己」的标准位置（不可自动处置的异常上报 AI）。

**用词**：时间片 interval；循环动词 release（「release 执行权一个 interval」）；弃 letgo（不常见）、弃 yield（generator 混淆）。

**js.run 返回结构化**：{ status: completed | aborted | timeout | spawn-failed, exitCode?, stdout, stderr, spooled?, bytes?, durationMs }——对齐进程层 ProcessOutcome 四态地基。

**wget Ultra Example（教学文本雏形，第十三至十六轮迭代定稿）**：批量下载三个大文件——断点续传（wget -c，重试与重启都从断点接上=剧本幂等）；停滞/过慢检测（stat 增量速率，停滞 6 片/过慢 12 片 → abort）；报错分派（onExit 里 exitCode 白纸黑字：0 成功推进、4/8 重试、3 跳过——机械规则写死，语义裁决才 park）；串行推进（下完一个下一个）；多 park 自己（每文件完成 park 汇报——进度可见+重启损失最小）。完整代码见对话记录，S2 落双语资源。其他场景同构：编译大型软件、npm server+前端测试（spawn server → waitFor 端口 → run 测试 → signal 收割）。

**js.fork / js.join**：DAG 的原语。DAG 是声明式糖（可静态分析/可审计/可渲染进度），编译到 fork/join；图表达不了的调度直接写 fork/join。不造新底层（用户明令）。地基：Fission 九事件、keyed union、BlobRef+Digest 聚合（FissionConverged 的 AggregateWorkRecordRef 即 DAG 收拢原型）、CausalAwait/JoinBatch.Max 部分完成——全部现成，提升为编程面成员即可。

**js.fission**：蓝图成员（fork=委派子 agent 与 fission=同身份多 lane 的分工）——**S2 设计点**，用户未展开。

**纯程序并行**：同 vm 内 IO 并行（如 Promise.all 批处理）不需要新原语，程序内直接写——S2 验证 vm 并行边界。

### 四、上下文（ANCHOR 视图声明）

**编址**：每个 user message 与 tool result 前带 [ANCHOR: N]；N 是**进程内全局虚时间戳**（同一进程统一流水，session 内编号可不连续——他 session 也在消耗编号；跨 session 引用因此自然解决：同进程共享地址空间，父直接引用子的段）；多工具调用多编号，不需要保序（编号是流水，因果序在账本里）。

**视图声明**：每次 js- 调用都带下次期望的上下文列表 { PrefixUntil, Extra }——PrefixUntil 保留前缀段；Extra 元素 = 数字（左闭右开段引用）或字符串（散文值）——引用 vs 值（git commit vs patch 二分）；模型自己当自己的 Blogger（可把 N 段压成一段散文放 Extra——压缩创作权归模型）。

**声明进上下文**：调用 js- 时其尚未返回无编号，规定声明作为调用参数进入——字面值天然进上下文；**一回合有效**（下次调用重新声明）。

**token 经济汇报**：每次告知模型 total / used / cached——PrefixUntil 命中 KV cache 前缀区（便宜）、Extra 重新计算（贵）；「说过的前缀如立下的约」的物理基础 = 改前缀缓存全失效；模型拿真实成本信号权衡（唯一知道哪些上下文对任务重要的主体）。

**账本哲学**：durable 账本是真相源，上下文只是视图——Extra 引用的段永远可从事件流取回；坏视图可恢复（下回合把丢的段加回 Extra）——压缩从单向不可逆 squash 变可迭代声明。

**软约束**：无硬底线；教学说清楚「不要连自己在干啥都忘了」。

**吞并清单（24 项盘点结论）**：压缩/squash、Blogger frames 裁决（半退役——chronicle 记账保留）、park 恢复视图、js.deliver 接收方视图、LWR 父子交接（退役）、todowrite（退役）、PairHint（改视图增量）、nudge 内容面、restart-guidance（durable 重建视图的定义场景）、concern 邮箱交付（mailbox 整体退役）、join/horizon 收据、devops 委托 WorkRecord 返回、Bookkeeper 案例检索——以上全部能统一（本质都是「选择给模型看什么」的宿主硬编码）。**不可统一**：静态权威（AGENTS.md/role 提示/工具描述——天然落 cache 前缀区，由 token 汇报覆盖）、sanitize-output-messages（安全边界，必须留宿主）、chronicle 记录产生与 ProtocolArgumentVault 重放保真（账本侧=真相源本身）。

### 五、退役清单（终局形态）

旧六成员（file/edit/rewrite/write/glob/grep）及专有词汇表；mailbox 整套（concern 邮箱、pending 聚合、公告 durable 事实）；defer；publish；LWR（全部交接形态——父子双向）；Blogger 压缩职责（frames/squash）；todowrite；直接工具 read/edit/write/glob/grep/patch/mv/rm（四期，唯一工具达成时）；run/pty 宿主工具（js.run 组合面替代）。

### 六、教学设计（双轨范式 + 组合教程）

- 范式保留：接口代码讲语义 + 示例代码讲用法（现身说法）——js.Runnable 基类签名即规格（LLM 看签名就知道怎么写）；Ultra Example 教组合模式。
- 内容重构（旧「教学补齐 8 项」基于六成员面，**作废重写**）：新原语集 + 组合教程（find/replace、锚点、批量替换、append/grep/glob 模式）+ park/ask/deliver/abandon 协作代数 + run 巨类 + ANCHOR 视图声明 + 「何时 park」判断（机械规则写进程序/类，语义裁决才 park；程序分段为默认、显式 park 是重武器、原语内建 park 是日常）+ 「多 park 自己」剧本要点 + 无防御模式（Unix 责任哲学）。
- 命名即教学：Unix 先验免费获得语义（cat/ls/stat/write/mkdir/mv/rm + where/whereAll 的 querySelector 先验）；fuzzy 语义（fff）是唯一要教的新概念。
- 旧描述的六块 All-or-Nothing 旧文案问题随六成员退役自动消解（新描述全新写）。

### 七、工具名改版（todowrite-for-ROLE，第 28 轮拍板）

**裁决**：八个 js-ROLE 工具（js-engineer/js-devops/js-manager/js-plan/js-orchestrator/js-blogger/js-bookkeeper/js-predictor）整体改名为 todowrite-for-ROLE（如 todowrite-for-engineer）。动机：LLM 对 todowrite 有强烈先验迷恋，名字本身即教学——「命名即教学」的门面级应用；与第 4 轮「js- 是 todowrite 的活替代品」定位、五期原生 todowrite 退役三者合流，终态下 todowrite 名字的语义完全归编程面。

**影响面事实（第六轮 devops 调查：机械可行，改动面收敛）**：
1. 前缀拼接三处：Repository/Programming/Js/Surface.fs:30（toolNameFor，生成面唯一名字派生）；StaticTools.fs:104-105（jsToolName，权限投影侧独立派生）；Strength/Surface.fs:314（js-predictor 交换投影回 owner 面——第三处，调查前未预见的深点）。
2. 前缀分发两处（全仓仅此两处 StartsWith "js-"）：ToolRegistry.fs:360-366 tryAdmissionFor——前缀匹配 + Substring 3 硬编码前缀长度（"todowrite-for-" 为 15 字符，须同步 Substring 15）+ "js-bookkeeper" 特例名；StaticTools.fs:232（权限投影前缀分发）。JsToolSpec.admissionFor 本体收剥离后 roleName，零改动。
3. 完整名硬编码：src 14 文件（PluginHooks.fs:365/573、PluginHostWiring.fs:122、ManagerReviewTools、BookkeeperSurface/BookkeeperTool、StaticTools.fs:163-169（knownToolNames 七名，不含 js-predictor）与 200-208（jsPermission）、ToolRegistry.fs:358/363/968-970、Description.fs:471/472/615/616/1100/1347、Strength/{Delegate,Replay,Frame,Runtime,Surface,InvestigationEstimateContract}.fs）；requirements 142 文件 + scripts 6 处。
4. Strength 侧深点（施工检查单必列）：js-predictor 放行名单三处——Runtime.fs:53（exactReadonlyHostToolMap）、Frame.fs:50（projectionTools 集合）、Surface.fs:314/1763——机械上最易漏。
5. 描述资源三态：js-program 63 键全部 {{toolName}} 运行时模板（零硬编码，改名自动跟随零改动）；js-plan zh/en:5 硬编码 1 处/语言；js-bookkeeper 多键硬编码（program-required、set-answer-once、set-question-once 等）；周边 role/plan 提示 4 处、manager-assess 1 处。
6. durable 零迁移：流名 js-tools/transactions、js-tools/lifecycle-audit 不含角色工具名；.Herdchestra 现状零真实 js-tools 流事件；事件类型名零涉及；资源键路径不含角色工具名。
7. 撞名核查：无机械冲突——todowrite-for-x 与 todowrite 字符串不同，permissionOfToolName（StaticTools.fs:82-94）与 requestToolMap 特判（183-187）全部精确匹配不误伤。

**连带词汇裁决（S2 自主，非用户拍板）**：durable 流名 js-tools/*、资源目录 js-program/js-plan/js-bookkeeper、RuntimeBindingKey（js.read/js.park 等）、哨兵（__jsParked/__jsAbandoned）全部不动。理由：流名是 durable 身份，稳定为贵；RuntimeBindingKey 与哨兵是机器对机器词汇，不进模型视野，与迷恋红利无关。终局是否连带退役 "js" 词汇记入开放问题（低风险延迟决策）。

**[008] 条款张力与处理**：action-affordance [008]「同一工具名称在全系统中处处代表同一契约」——过渡期（改名先行、五期退役未行）todowrite（宿主待办读写）与 todowrite-for-x（文件+park+run 编程面）两个同族名字契约不同。处理：描述层主动弥合——工具描述明示「这不是写死清单的宿主 todowrite，是活的编程面（文件+park+run）」，把迷恋红利引向正确语义；一期上线后经失败码分布监控误用（检验段反事实 5，证伪则回退改名——机械成本已查明，回退便宜）。五期原生 todowrite 退役后张力自然消失，名字与契约终态统一。

## 已核实的事实（五轮 devops 只读调查，全部带出处）

### 教学面掉队清单（第一、二轮调查——诊断证据链，成员面现已重构，掉队结论仍成立）

1. 旧描述约 850 行，约六成是实现代码（file 锚点算法 139 行 + edit 实现约 400 行，Description.fs 312-450/619-1060 行）——按用户范式这是「接口代码讲语义」资产，非负债。
2. park 在旧基类只是 stub 注释（Description.fs 538-543 行）；五个 Ultra Example 零 park；唯一 park 示例只进语义测试面不进描述（Capability.fs 208-214 行 + GeneratorSurface.fs 115-122 行）。
3. HANDLE_UNKNOWN / CANNOT_PARK_WHILE_ABANDONING / CONCURRENCY_LIMIT 零预告；unpark/abandon 无 schema 描述；unpark.value 标注失真（ToolHost.fs 179-199 行）。
4. 教学通道唯一（全部押在 description 一条）；AGENTS.md、role/engineer、role/devops 零着墨；Manager 被强制走 js-manager（StaticTools.permissionFor 217 行）、Engineer/DevOps 无强制——直接工具七件套全开且描述各一句话（FileTools.fs 88/107/130 行）。
5. 旧描述六块文案教 All-or-Nothing 旧语义而实现已 as-is（contract/rules-mutation/rules-edit/rules-write/footer/edit-prelude + Capability.fs fragment 层 3 处旧词）。
6. 机制面现状（截至调查时）：9 成员、三态入参、as-is 逐笔落盘（ToolWorkflow.fs settleLedger 220-222 行）、ParkedSites 现场表、交付门禁与每回合提醒、parked-hint/parked-deliver-blocked 资源——已实现部分是本次设计的地基。

### 痛点清单（第三轮调查，17 条——「js- 成为唯一工具」的证据链）

P1 文件三件套散文返回、工具间数据传递只能经模型转抄（FileTools.fs:88-143 + provider-projection [008] 解析禁令）；P2 三套渲染两套失败码词汇（FileMutationTools.fs:59-66）；P3 直接工具无保护无记账（[027] 应然实然脱节）；P4 run 硬上限 10 秒（ExecutorTool.fs:84）而单包测试实测 150 秒（GAP.md:105）——15 倍鸿沟；P5 terminal 四件套是手动轮询官方形态（PtyTool.fs:19-76，read-terminal「没有新内容」空转文案）；P6 全流程编排（server→浏览器→点击→观察）无法表达；P7 run 输出截断保尾部丢头部（ExecutorTool.fs:189-197）；P8 join/horizon 是快照收据不是数据流；P9 nudge 与自然停机回合税（work [027]/[028]）；P10 中断白名单极窄、程序无「等外部输入」形态（managed-session-lifecycle [016]）；P11 GAP-078 工具描述语义质量无机器 oracle；P12 单回合内不能表达工具间依赖（repository-programming [017]）；P13 js 沙箱欠账（GAP-088/089、process-execution GAP-091/092/093）——统一前要还的施工清单；P14 Fission lane 黑盒收拢、lane 间不能通信（GAP-158 重启残留 FissionGroup 使 join 永久挂起的真实翻车，已修）；P15 验证循环每步一回合；P16 压缩是被动的、丢什么模型无发言权（context-compression [001]/[002]）；P17 PairHint 单方面注入（模型不能程序化消费）。

### 机制盘点（第四轮调查，24 项——ANCHOR 吞并面的证据链）

transform 管线是显式编号的宿主管线（PluginTransforms.fs:1330-1380：13.1 PairHint、13.2 requirement-grounding、13.3 horizon-auxiliaries、14 blogger-chronicle、15 deferred-inspections、16 sanitize），每回合「给模型看什么」全部宿主硬编码、模型零声明权——ANCHOR 接管的对象。冻结/重放纪律已分散存在（concern byte-identical、grounding occurrence 原位重放、PairHint 字节冻结）——ANCHOR 是收拢不是无中生有。九项「宿主替模型做的选择」为最大收益区（详见设计定稿四之吞并清单）；三类不可统一边界已划清（静态权威/安全/账本侧）。

### fork/exit/watchdog 地基（第五轮调查）

- DAG 骨架已存在：Fission 九事件（Execution/Fission/Facts.fs:9-72）、keyed union、ring convergence、N-1 takeover；WorkRecord/Payload 一律 BlobRef+BlobDigest（FissionConverged 的 AggregateWorkRecordRef 即 DAG 收拢原型）。
- fork 现状：ForkTool（Execution/Delegation/Fork/OpenCode/Tool.fs）Manager/Orchestrator 准入；ModelRouting → MJS scheduler 唯一调度器（GAP-016 CLOSED）；lane = 独立 Host session。
- join 现状：CausalAwait 阻塞等待非轮询；JoinBatch.Max 部分完成；JoinGuard 等待期 nudge；重启后 durable handle 收养。
- js.deliver 是全新语义：现状子会话结束 = TurnCompleted 被动收割（Delegation/Fact.fs），无显式退出工具——「自然停止≠完成」哲学已在（work [027]），js.deliver 是其主动化。
- watchdog 真空白但原语全齐：进程层 TimeoutExceeded/SpawnFailed/ProcessCancelled/ExecutionFailed 四态 + Spooled 大输出落盘 + hard limit 1 小时（ProcessRequest.fs:44-66）——工具层 10 秒只是收窄面；pty 是唯一可管理长程实体（ptyId+signal+空输入增量读）；零心跳/健康检查机制。
- 重启恢复现状：ChildWorkRecovery 把残留按 process_interrupted 结算、不复活（GAP-158 已修）——与「重启丢失+剧本幂等+多 park」拍板一致。

### 运行侧

零次 js-* 工具调用、零个 parked 现场、零条审计事件（三轮一致核实，事件流全部 JournalEnvelope）——「模型会不会用」缺实证基础；新原语面上线后应观察失败码分布作验收。

## 目标形式化（S1 收口状态）

A. 诊断底账完备 ✓：全链路事实 + 17 痛点 + 24 机制盘点 + fork/exit/watchdog 地基，每条有出处。
B. 症状归因 ✓：范式掉队（用户确认）+「把计划写成程序」类别在模型心智不存在（最深根因）+ 结构性病灶（散文联邦）。
C. 对策与全景摆桌 ✓：设计定稿全景（六节，全部用户拍板）+ 退役清单 + 教学设计 + S2 待设计清单。
D. 用户输入收齐 ✓：二十七轮全录；分期下放实现者；S1-only-ask 约束下的全部拍板已入稿。
E. **待继任评审**：本任已用完 review 权（第一棒已评），S1 是否闭合晋级 S2 由下一任独立评审裁定。

## 根因

1. 教学单通道 + 范式掉队（用户确认）：机制演进时教学未按双轨范式跟进。
2. 「把计划写成程序」类别在模型心智中不存在（最深根因）：描述未教、外部无名目。
3. 结构性病灶：工具面是「一动词一工具一回合」的散文联邦——数据传递与控制流只能经模型转抄与多回合编排，状态只在模型脑内。17 条痛点是不同切面；js- 统一（唯一工具 + 结构化返回 + 编程面控制流）是根治方向（用户终裁）。

## 取舍（已拍板记录，保留供 S2 查证）

- 双轨范式补齐，废弃描述瘦身（3 轮）。
- 范围连带 fragment 层与测试；unpark.value 放宽 free-form（5 轮）。
- 续延模型：保活现场 + 编排层恢复（Done.md 时代已拍板，语义保留）。
- as-is 全局翻转（已实施，语义保留）。
- js.ask 合流而非独立中断机制（9 轮）；js.deliver 合流而非保留独立 deliver（11 轮）。
- DAG = fork/join 糖，不造底层（11 轮）。
- 控制流归宿主（17 轮）——watchdog 强制的唯一途径。
- 原语集重构一步到位（全退役），不并存两套词汇表（21 轮方向）。
- 无防御模式（Unix 哲学，27 轮）。
- 重启接受丢失 + 剧本多 park + 幂等衔接（11 轮）。
- 分期下放实现者（11 轮）。

## S2 待设计清单（用户下放，S2 自主裁决——S2 无 ask）

1. js.fission 与 fork 的分工（fork=委派子 agent vs fission=同身份多 lane 的编程面形态）。
2. DAG 声明语法（编译到 fork/join 的糖形态）。
3. fff 模糊语义精确定义（规范化规则：空白折叠等；where 零/多匹配的 payload 字段形态）。
4. ls 递归与否；大仓 grep/glob 组合的性能边界。
5. FILE_CHANGED 的 as-is 形态（cat→拼接→write 的跨程序并发窗口）。
6. context-compression 等条款的改写清单（[001] 等字面与视图声明的关系）。
7. DomainConflict 分叉合并的视图形态；跨进程（重启后）ANCHOR 编号接续（账本重放？）。
8. vm 并行边界（纯程序 Promise.all）。
9. 现有 deliver 工具对齐改造细节；ask 全链对齐 js.ask 的改动面。
10. parked 载荷的渲染格式（>|/| 标记的 TOML/文本形态）与 token 预算。
11. unpark.value 的 schema 放宽实现。
12. 分期路线与每期验收标准（实现者定）。
13. GAP-088/089 与 process-execution 欠账的施工顺序。
14. 小体积按状态注入（manager-assess、plan opening）迁移与否。
15. 教学资源全貌：双语资源成对、Ultra Example 家族（wget/编译/server+测试）、组合教程、Runnable 基类描述、四层同构的测试编号。

## S2 · 理解与路径

### 理解（第一棒，2026-10-17）

用自己的话重述问题。

**输入**：两份材料。(1) S1 定稿的七节设计全景（第 28 轮新增第七节：工具名改版 todowrite-for-ROLE）——九文件原语（cat/ls/stat/write/mkdir/mv/rm/where/whereAll）、协作代数（return < park < deliver，abandon/reap 收尾）、js.run 巨类契约（控制流归宿主、LLM 只写语义回调、watchdog 强制）、ANCHOR 视图声明（进程内全局虚时间戳 + {PrefixUntil, Extra} + token 经济汇报）、退役清单（旧六成员、mailbox、defer、publish、LWR、Blogger 压缩、todowrite、直接工具、run/pty）、教学设计（双轨范式重写）。(2) 仓库现状——js- 编程面已落地 as-is 逐笔落盘、park/unpark/abandon 三态编排、rm/mv 成员、四层同构（FragmentRegistry→ToolsBindings→Description→ToolWorkflow）、durable 审计流；宿主层另有 fork/join 地基（Fission 九事件、BlobRef 聚合、CausalAwait）、ask 全链、deliver 主通道、PairHint 注入通道、进程层 1 小时能力（工具层收窄 10 秒）。

**输出**：从现状到「js- 唯一工具终局」的施工蓝图——S2 待设计清单 15 项的裁决（fork/fission 分工、DAG 糖语法、fff 模糊语义、FILE_CHANGED as-is 形态、ANCHOR 跨重启接续、parked 载荷渲染、unpark schema 放宽……）+ 分期路线与每期验收标准（分期下放实现者，S2 给建议案）。

**约束**（用户拍板，不可违背）：控制流一定归万象术（watchdog 强制的唯一途径）；原语集一步到位全退役、不并存两套词汇表；DAG = fork/join 糖、不造新底层；无防御模式（Unix 哲学）；js.ask 与 park 合流、js.deliver 与主通道 deliver 合流；S2 无 ask——15 项全部自主裁决，无用户兜底。

**难点**（为什么这不是照抄定稿开工就行）：
1. 存量与终局重叠断裂：现有六成员（file/edit/rewrite/write/glob/grep）是刚完成 as-is/park 大改造的新代码，却要整体退役换九原语；现有 park/unpark/abandon 语义保留，但 where 的内建 park（零/多匹配自动挂起）要接入同一现场表——「保留什么、重写什么」的切割线要逐成员画清。
2. ANCHOR 是全新子系统，吞并面横跨 13+ 项宿主硬编码（压缩、交接、提醒、收据、nudge、restart-guidance……），且视图声明要动 durable 账本的投影层——真相源不动、只动投影，但「账本重放时视图如何保真」是账本哲学级问题（S2 清单第 7 项）。
3. js.run 把进程层 1 小时能力提升到工具面（现为 10 秒硬上限），宿主拥有执行循环——runCore 的竞速模型（programPromise vs 暂停信号）要扩展成「宿主循环 + LLM 回调」的第三种执行形态，与现有「一次 await 到底」「park 保活」两种形态并存。
4. 退役清单牵动主通道：ask/deliver/LWR/Blogger/todowrite/mailbox 每项退役都是全链改动（durable 事件族、等待门禁、注入通道），退役顺序错了会让中间态系统不可用。
5. 分期是硬要求：终局形态横跨文件面/生命周期/工程面/上下文面/退役面五个子系统，不可能一期做完；每期必须独立可验收（测试绿）且向终局收敛（不做中间态专有物）。

### 探索（第二棒，2026-10-17）

候选路径围绕 S2 待设计清单 15 项展开。S1 已锁模型层（续延、原语集、控制流归宿主、ANCHOR 编址），本段候选全部是实现层裁决。事实基础为第六轮 devops 调查（段内标「六-N」）：六-1 parked 载荷现为 handle+payload 平铺的 Synthetic TOML，程序源码不回传，fs 清单为空；六-2 unpark.value 已 free-form 零校验（schema 声明失真但执行无校验）；六-3 JsParkedSite = { Handle, RunHandle, ReadSnapshots, Ledger, SessionId }，上限 8、park-N 句柄、reapSession 现成；六-4 原语层零挂起通道——ParkState 五槽为 park 成员独占，ParkDeferredResolve 恢复通道同构可复用；六-5 transform 管线 13.1-16 各步原地改 OutObj，PairHint 三资源模板，plan opening 是 durable 捕获非注入；六-6 GlobFs 全仓递归无截断、grep 带回 readSnapshots；六-7 DeliverTool Arguments=[] 无入参，HandleWorkCompleted 终值经 BlobRef+Digest 交接；六-8 context-compression [001] 逐字禁令「严禁读取、查询、推导或缓存任何模型上下文窗口大小」。

**冲突发现（S1 未见的正面冲突，本段最重要新事实）**：ANCHOR token 经济汇报（total/used/cached）与 [001] 禁令正面相撞——裁决见主题 D。

#### 主题 A · 文件原语与 where 内建 park（清单 3/4/5）

- **候选 A1（主）**：九原语全新 fragment 族；where/whereAll 在 vm 内检测零/多匹配时经注入的原语挂起函数触发（ParkState 新增原语级信号槽，workflow race 前安装），恢复经既有 ParkDeferredResolve 注入 span，现场登记走既有 JsParkedSites。主要风险：JsSandbox 状态机加原语级槽（六-4：现为 park 成员独占）；原语调用栈深处 await deferred 的挂起形态需 S3 伪代码验证。事实支持：六-4（ParkDeferredResolve 通道同构可复用、哨兵 settle 机制现成）、六-3（现场表字段无需新增）。
- **候选 A2（fff 规范化位置）**：宿主 F# 侧实现（经 Fable 消费 fff，进 vm 前规范化）。主要风险：每次 where 跨 F#-JS 桥的性能。事实支持：GlobFs 桥接 wanxiangli/fs 的 CUT-24 先例（六-6 同构）。淘汰 vm 内 JS 侧规范化：规则散进生成代码、测试面分裂。
- **ls 递归裁决（清单 4）**：ls 非递归（一目录一列表，POSIX 先验），递归需求 = glob 组合（GlobFs 全仓递归无截断已存在，六-6）。淘汰 ls 递归参数：与 glob 词汇重叠。大仓性能边界：grep/glob 的全仓遍历是既有语义（无截断），性能风险由结果排序与 gitignore 剪枝承担——不新增机制。
- **FILE_CHANGED as-is 形态裁决（清单 5）**：校验窗口随 ParkedSite 存活——ReadSnapshots 与 Ledger 已是 site 字段（六-3），恢复后写前校验用现场快照，设计已半落地。跨程序并发窗口（cat→拼接→write 之间他者改动）由写前比对快照把关；return（分段模式）后快照随程序结束消亡，下次分段程序重新 cat——分段 = 新程序新快照，保护窗口不跨程序边界，这是合理语义，教程写清即可。淘汰全局锁/租约：过度防御，违背无防御拍板（27 轮）。

#### 主题 B · fork / fission / DAG（清单 1/2）

- **候选 B1（主）**：fork = 委派子 agent（js.fork(spec) → 新 Host session，子 agent 以 js.deliver 显式交付，终值走 HandleWorkCompleted 的 CompletionRef+Digest 通道）；fission = 同身份多 lane（js.fission(lanes) → Fission 九事件族，BlobRef 聚合收拢）。分工判据 = 共同法原文映射：fork 造新当事人（新名分新户头），fission 是同一身份的多个当下（名分职权不变）。DAG = 纯声明糖 js.dag({ nodes: [{ id, run, deps }] }) 编译到 fork/join 调用序列，静态可分析。主要风险：DAG 编译器语义（节点失败传播、JoinBatch.Max 部分完成如何暴露给声明方）。事实支持：Fission 九事件、AggregateWorkRecordRef 收拢原型、CausalAwait 非轮询等待全部现成（S1 第五轮调查）。
- 淘汰「DAG 独立执行引擎」：用户明令不造新底层。淘汰「不做糖只教 fork/join」：用户终局明言 DAG 全自动调度；「图表达不了的调度直接写 fork/join」保留为逃生舱（定稿已含）。

#### 主题 C · js.run 巨类（牵动清单 13）

- **候选 C1（主）**：runCore 第三种执行形态——宿主执行循环（next → spawn → interval → check → 退出 → onExit → next…），程序 promise 跨时间片存活（RunHandle 闭包持有，与 park 保活同构）；check 返回 continue | abort | park 三值，park 走既有 ParkState 通道。主要风险：宿主每 interval 跨桥调用存活 vm 函数（check 回调的闭包保活）；js.run 单工具调用横跨多时间片，工具返回形态扩展为四态（completed/aborted/timeout/spawn-failed + spooled）——与 parked 返回并列。事实支持：进程层 ProcessRequest 四态 + Spooled + 1 小时 hard limit 现成（S1 第五轮）；工具层 10 秒只是 ExecutorTool 局部收窄（P4）；ParkState/RunHandle 闭包保活已被 park 机制验证。
- 淘汰「后台任务 + 轮询工具」：正是痛点 P5（terminal 手动轮询官方形态）；用户明令禁随意后台。

#### 主题 D · ANCHOR 落地（清单 6/7/14）

- **[001] 冲突裁决（主候选，清单 6 的纲）**：改写 context-compression [001]/[002]——区分「宿主预测溢出做隐藏控制流」（保持禁止）与「向模型披露计量供其声明权」（解禁）。理由：[001] 立法意图是防宿主偷偷预测容量替模型做决定；ANCHOR 方向相反——计量披露给模型、选择权归模型，与立法意图同向。风险：条款连锁改写（[002] attempt 失败唯一触发保持不变——视图劣化不触发重试，attempt 失败仍触发；squash/prefix probe 等条款随吞并清单逐条改）。淘汰「只报 used 不报 total/cached」：cached 命中是 PrefixUntil 便宜/Extra 贵的物理信号，砍掉等于砍掉权衡依据；[001] 的 200 KiB 字节计量先例说明计量本身合法，禁的是隐藏控制流。
- **跨重启编号（清单 7）**：重启重置编号——「进程内全局虚时间戳」定义的直接推论；旧编号引用失效后经 durable 账本取回（Extra 段永远可从事件流取回，账本哲学节）；失效引用不设防御（无防御拍板）。淘汰「重启重放账本接续编号」：重放成本高，且与「现场是进程内活物、重启即重置」的既有拍板（句柄同命）矛盾——编号与句柄应同命。
- **DomainConflict 视图（清单 7 后半）**：合并裁决事件携带裁决者视图 {PrefixUntil, Extra}，作为合并后默认视图——裁决是「把所有岔路认作父辈」的新事实，视图随裁决落地；岔路期间各自视图各自生效。风险：岔路与裁决视图的衔接时刻（裁决落地即切换）。
- **小体积注入迁移（清单 14）**：manager-assess 迁移（属「宿主替模型做的选择」九项之一，改为状态相关段进视图默认 Extra）；plan opening 不动（六-5：durable 捕获非注入，账本侧边界）。事实支持：机制盘点 24 项的统一/不统一三分界（静态权威/安全/账本侧）。

#### 主题 E · js.ask / js.deliver 合流（清单 9）

- **候选 E1（主）**：js.ask = park 成员变体——置 ParkPayload + 触发 attempt 中断（复用 ask 全链 UserQuestionJournal + 等待门禁）+ 现场同表登记；物理人回复到达 → AI 新回合 → AI unpark 注入（原话或消化裁决）。主要风险：ask 的中断语义与 park 的「工具调用正常返回」形态不同（ask 时工具调用不返回 parked 载荷而是 attempt 中断）；managed-session-lifecycle [016] 中断白名单要加 js.ask。事实支持：ask 全链现成（UserQuestionJournal、等待门禁 work [049]）。
- **候选 E2（主）**：js.deliver 两层——程序内成员（subagent 程序 await js.deliver(value) → 宿主收割为 HandleWorkCompleted 主动形态，CompletionRef+Digest 通道现成，六-7）；主通道入参加可选 { view: {PrefixUntil, Extra} }（DeliverTool Arguments=[] → 有入参，可选参数不破坏现有调用方）。主要风险：deliver 状态机（assessment/finish 判定）与视图参数的正交性。事实支持：六-7（终值 BlobRef 交接已是事实形态，缺的只是程序内主动成员与视图入参）。
- 淘汰「js.deliver 独立于主通道」：用户明令合流（11 轮）。

#### 主题 F · 分期建议与施工顺序（清单 12/13）

- **候选 F1（主，建议案——分期下放实现者，用户拍板 11 轮）**：五期，每期独立验收：
  - 一期·文件原语面：九成员 + where 内建 park + parked 载荷加源码标记 + GAP-088/089 沙箱欠账（正好同动 JsFragmentRegistry，一起还）。验收：组合模式测试全绿（find/replace、锚点切片、批量替换、append/grep/glob 各有测试）+ 失败码分布观察（运行侧零样本的首次实证）。
  - 二期·工程面：js.run 巨类 + 宿主执行循环。验收：wget Ultra Example 真实可跑（断点续传、停滞检测、报错分派、多 park 汇报）。
  - 三期·上下文面：ANCHOR 编址 + 视图声明 + token 经济 + [001]/[002] 条款改写。验收：视图声明一回合有效可测试 + 压缩可迭代（丢段可加回 Extra）。
  - 四期·协作面：js.ask/js.deliver 合流 + fork/fission/DAG 编程面。验收：subagent 显式退出（静默 return 不算退出）+ DAG 糖调度端到端测试。
  - 五期·退役：旧六成员、mailbox、defer、publish、LWR、Blogger 压缩职责、todowrite、直接工具、run/pty——唯一工具达成。验收：退役清单逐项 grep 零残留 + 全量测试绿。
  - 风险：五期跨度大，跨期依赖要显式（四期依赖三期视图声明做交接）。事实支持：理解段难点 5；GAP-088/089 与原语面重构同文件（一期并还最省）。
- 淘汰「退役先行」：中间态系统不可用（难点 4——先拆 ask/LWR/deliver 再建新面，中间任何一刻系统残缺）。

#### 主题 G · parked 载荷渲染与教学（清单 10/15）

- **候选 G1（主）**：parked 载荷 Synthetic TOML 顶层加 source 字段（完整程序源码，行前缀 ">|" 当前行、" | " 其他行——调试器当前帧先验）+ 保留 handle/payload 平铺（六-1 现状不动）。主要风险：token 预算——长程序全量回传；裁决：源码只进 parked 载荷不进每回合提醒（提醒只列 handle），AI 经视图声明自行权衡。淘汰「截断只传执行点附近」：信息完备替代机制防御是拍板哲学（27 轮），截断破坏「AI 据此知道程序在等什么、注入什么形态」。
- **教学资源（清单 15）**：双语资源成对（resources/provider/tool/js-program/ 族先例）+ Ultra Example 家族三场景（wget 批量下载 / 大型编译 / npm server+前端测试）+ 组合教程（find/replace、锚点、批量替换）+ Runnable 基类签名即规格 + 协作代数（return < park < deliver 与「何时 park」判断）+ 测试编号 028 起四层同构。风险：描述体积——用户明示预算充足（13 轮）；旧 850 行六成实现代码是范式资产先例（第一轮调查）。淘汰「描述瘦身」：用户已废弃该方向（3 轮）。

**淘汰路径汇总（各一行，回答「为什么不是另一条路」）**：vm 内规范化（规则散进生成代码、测试面分裂）；ls 递归参数（与 glob 词汇重叠）；全局文件锁（过度防御，违背 Unix 拍板）；DAG 独立引擎（明令不造新底层）；只教 fork/join 不做糖（终局要全自动调度）；后台任务+轮询（即痛点 P5）；只报 used 不报 cached（砍掉权衡依据）；重启接续编号（与句柄同命拍板矛盾）；js.deliver 独立通道（明令合流）；退役先行（中间态不可用）；源码截断（违背信息完备拍板）；描述瘦身（已废弃）。

### 路径（第三棒，2026-10-17）

**先补两个探索段未覆盖的裁决（清单 8/11，使 15 项全部有主）**：

- **清单 8 · vm 并行边界**：程序侧自由——vm 单线程事件循环天然串行化 api 调用，程序内 Promise.all/Promise.allSettled 随便用，宿主不加锁、不加排队、不加互斥（无防御拍板）；并发竞态（两个分支同时 write 同一文件）由程序作者自负（Unix 哲学）；宿主只保单笔 mutation 原子性（MutationFs 层既有，as-is 拍板保留项）。淘汰「宿主侧 api 调用串行化/互斥」：过度防御，且与「js- 程序就是普通 JS」的教学承诺冲突。
- **清单 11 · unpark.value schema 放宽**：六-2 事实——执行已 free-form 零校验，仅 schema 声明失真。裁决：schema 声明改为与执行一致的任意 JSON 值描述，工具描述明写「注入值类型与语义由注入者自负，消费端自然失败」；不新增任何校验代码。改动量≈零（只改声明文字），并入一期。

**主路径：五期递进——文件原语面 → 工程面 → 上下文面 → 协作面 → 退役。**（主题 F1 升格为选定路径；分期下放实现者，本路径是建议案的定稿形态。）

**分解（五步，每步=一期，各含验收）**：

1. **一期 · 文件原语面**：九原语（cat/ls/stat/write/mkdir/mv/rm/where/whereAll）全新 fragment 族；where/whereAll 内建 park（ParkState 新增原语级信号槽，恢复走既有 ParkDeferredResolve）；fff 规范化宿主 F# 侧（候选 A2）；parked 载荷加 source 字段（候选 G1，">|" 当前行、" | " 其他行）；unpark schema 声明修正（清单 11）；GAP-088/089 沙箱欠账并还（同动 JsFragmentRegistry/JsSandbox，一起最省）；工具名改版 todowrite-for-ROLE（第 28 轮——与原语重构同动 Surface/StaticTools/ToolRegistry/描述资源，门面契约一次切换、教学一次重写；施工检查单：前缀拼接三处、前缀分发两处与 Substring 3→15、完整名硬编码 src 14 文件、Strength 三处 js-predictor 放行名单深点（Runtime.fs:53、Frame.fs:50、Surface.fs:314/1763）、js-plan/js-bookkeeper 资源硬编码、requirements 142 文件同步）；旧六成员本期内保留（退役在五期，中间态双词汇表是已知代价，教程以新面为纲）。验收：组合模式测试全绿（find/replace、锚点切片、批量替换、append/grep/glob 组合各有测试，编号 028 起）+ 失败码分布观察（「LLM 不太会用」零样本症状的首次运行侧实证）+ 工具名切换后全量测试绿 + 描述含防混淆文案（明示非宿主 todowrite——[008] 张力的描述层弥合）。
2. **二期 · 工程面**：js.run 巨类 + 宿主执行循环（候选 C1——next → spawn → interval → check → 退出 → onExit 循环；check 返回 continue | abort | park 三值，park 走 ParkState 通道）；Runnable 基类签名即规格（watchdog 强制实现）；工具返回四态（completed/aborted/timeout/spawn-failed + spooled）。验收：wget Ultra Example 真实可跑（断点续传、停滞检测、报错分派、多 park 汇报）。
3. **三期 · 上下文面**：ANCHOR 编址（进程内全局虚时间戳，重启重置）；视图声明 {PrefixUntil, Extra}（一回合有效）；token 经济汇报 + [001]/[002] 条款改写（主题 D 裁决：区分隐藏控制流与计量披露）；manager-assess 小注入迁移（plan opening 不动——durable 捕获非注入）。验收：视图声明一回合有效可测试 + 压缩可迭代（丢段可经 Extra 加回）。
4. **四期 · 协作面**：js.ask（park 成员变体 + 复用 ask 全链 UserQuestionJournal/等待门禁 + managed-session-lifecycle [016] 中断白名单加 js.ask）；js.deliver（程序内成员收割为 HandleWorkCompleted 主动形态 + 主通道 DeliverTool 加可选 { view } 入参）；js.fork（委派子 agent，子以 js.deliver 退出）/ js.fission（同身份多 lane）/ js.dag（fork/join 纯声明糖）。验收：subagent 静默 return 不算退出、js.deliver 显式交付端到端 + DAG 糖调度端到端测试。
5. **五期 · 退役**：旧六成员、mailbox、defer、publish、LWR、Blogger 压缩职责、todowrite、直接工具、run/pty 逐项退役。每项退役前置 grep 零消费者验证。验收：退役清单逐项 grep 零残留 + 全量测试绿。

**边界**：

- 一期触碰：Capability.fs（fragment 注册）、ToolsBindings.fs（成员绑定）、Description.fs 与双语资源、ToolWorkflow.fs（parked 载荷渲染、原语级挂起）、JsSandbox（原语级信号槽）。不触碰：ask/deliver 主通道、transform 管线、Fission 事件族、进程层 ProcessRequest。
- 二期触碰：JsSandbox/ToolWorkflow 执行循环（第三种执行形态）、进程层 ProcessRequest 桥接（1 小时能力提升到工具面）、Runnable 基类与教学资源。不触碰：文件原语成员、transform 管线。
- 三期触碰：transform 管线（13.1-16 各步）、context-compression [001]/[002] 及连锁条款（squash [011]、Blogger delta [012]、prefix probe [009]-[011]）、PairHint 通道、manager-assess。不触碰：原语、执行循环内部、Fission。
- 四期触碰：ask 全链、DeliverTool、managed-session-lifecycle [016]、Fission 九事件族的编程面投影、work [049] 等待门禁。不触碰：文件原语、执行循环内部、transform 管线。
- 五期触碰：退役清单逐项的注册、权限、Surface/StaticTools 投影、测试。不触碰：新面任何成员。

**顺序**：

- 必须串行：一期 → 二期（js.run 的 check-park 走一期建的原语级信号通道）；三期 → 四期（fork/交付交接用 {PrefixUntil, Extra}，无视图声明则交接退回 LWR 旧形态，违背终局）；1-4 → 五期（每项退役物须有新面承接）。
- 天然并行：一期 ∥ 三期开发（ANCHOR 编址与视图声明不触碰原语层，模块零交集；但三期端到端验收需一期 parked 载荷作为视图消费者，故三期验收排一期后）；二期 ∥ 三期（执行循环与 transform 管线零交集）。
- 每期独立验收、独立闭环；不做中间态专有物（每期产物都是终局形态组成部分，非过渡品——一期九原语、二期执行循环、三期视图、四期协作成员全部直达终局形态，五期只删不建）。

### 检验与反思（第三棒，2026-10-17）

**反事实检验**：

1. **一期 where 内建 park 的 vm 挂起通道失败**（原语调用栈深处无法 await deferred——六-4 两处证据并存，wrapProgram 竞速协作方式未定界）：降级为 where/whereAll 返回结构化多匹配结果 + 组合教程强制「程序收到多匹配后自行 park 裁决 span」。裁决权仍归 AI（拍板不变），代价是教程多一步、组合写法变长。若连多匹配结果形态都定不下来，则 where 退化为 whereAll + 程序侧判断——这违背「where 永不抛错、零/多统一 park」拍板（22 轮），须回用户重新裁决，一期其余八原语不受牵连（挂起通道只服务 where）。
2. **二期宿主执行循环跨桥保活失败**（存活 vm 函数 check 回调无法跨时间片调用）：降级为纯数据快照——程序每 interval 主动写状态对象（stdout 尾部、文件大小、进程存活），宿主读快照跑 watchdog 判定。控制流仍归宿主（拍板「watchdog 强制、控制流归万象术」保持），弱化的是「LLM 只写语义回调」——回调退化为状态投影。若快照也不可行，降级为「程序自写循环 + park 汇报」——这直接违背「控制流一定归万象术」拍板（25 轮），js.run 整体作废，回模型层。
3. **三期 [001] 条款改写被证伪**（立法意图判定反转：向模型披露计量本身即被禁）：作废范围仅 token 经济汇报（total/used/cached）整块；ANCHOR 编址、左闭右开分段、{PrefixUntil, Extra} 视图、压缩迭代不受影响（退化为无成本信号的视图声明，模型凭判断选段）。主干不倒，经济层可后补。
4. **五期退役顺序错误**（中间态系统不可用，如先拆 LWR 再建 ANCHOR 交接——难点 4）：兜底为退役逐项前置 grep 零消费者验证 + 单项回滚（退役是删代码，git 单项可回退，不回滚整期）；退役顺序本身由「新面已承接」事实驱动，不靠日历顺序。
5. **改名 [008] 张力被证伪**（过渡期模型把 todowrite-for-x 当宿主 todowrite 使用、失败码分布出现 todo 语义误用且描述弥合不奏效）：回退 = 恢复 "js-" 前缀——机械成本已查明（前缀拼接三处 + 前缀分发两处 + Substring 长度 + 硬编码清单），回退便宜；放弃的只是迷恋红利（教学增益），不损任何语义。改名不牵连五期其余步骤——名字是门面，原语/执行循环/视图语义零关联，回退是纯局部动作。

**S1 六节定稿逐条对照**（主路径步骤 → 目标节）：

- 文件原语面（九原语 + where 内建 park + FILE_CHANGED as-is 形态）→ 一期。
- 生命周期与协作代数（return < park < deliver、abandon/reap、js.ask 合流、禁止静默停止、subagent 显式退出）→ 一期（park 地基已在）+ 四期（ask/deliver 合流、显式退出）。
- 工程面（js.run 巨类、fork/join、DAG 糖）→ 二期 + 四期。
- 上下文（ANCHOR 编址、视图声明、token 经济）→ 三期。
- 退役清单（旧六成员、mailbox、defer、publish、LWR、Blogger 压缩、todowrite、直接工具、run/pty）→ 五期。
- 教学设计（双轨范式、组合教程、Ultra Example 家族、Runnable 签名即规格）→ 贯穿各期验收（一期组合模式测试、二期 wget Ultra Example、四期协作代数教学），非独立步骤。
- 结论：五期覆盖六节全部，无一步装饰性动作；每期验收即对应节的可判定状态。

**表象反思**：

本任务根因有二：「LLM 不太会用」（教学面掉队，第一/二轮调查的诊断链）与「工具不统一、长程两难」（17 痛点清单）。主路径若只做成员替换（一期换九原语）而不动教学，则只解决表象——新成员照样不会用。因此教学资源在各期验收里是硬项不是附属：一期验收含组合模式测试与失败码分布观察（直接回测「不会用」是否被解决——这是零样本症状首次获得运行侧实证的通道）；二期验收就是 Ultra Example 真实可跑（教学即测试）。另一侧表象风险：五期退役若只是删代码，而 17 痛点的行为模式（轮询、假死、双轨工具）没有新面承接，退役即破坏而非收敛——故每项退役前置 grep 零消费者验证，把「承接完毕」做成事实门槛而非假设。

## S3 · 流程与伪代码

（本段由 S2 晋级后第一棒写入。载体为 Knuth 风格伪代码，写给执行者读；每个算法自足，裁决点有结论，失败路径有去处。S3 多任累积：本棒覆盖一期（文件原语面）定界前言与算法 A-E；二期（js.run 工程面）、三期（ANCHOR 上下文面）、四期（协作面）、五期（退役）由第二棒（本棒）补齐：算法 F-L——五期路径全覆盖，S3 验收标准「路径每段流程有伪代码」闭合。）

### 定界前言 · 竞速机制事实（devops 直读，2026-10-17；闭合 S2 开放问题「wrapProgram 竞速定界」）

写一期伪代码前钉死的事实，S4 执行者不必重查：

1. 挂起检测通道唯一：宿主侧 Promise.race（parkSignal vs programPromise，ToolWorkflow.fs:468；Fable 无 Task.WhenAny，Promise.race 是既定模式）。挂起时程序 promise 保持 pending（JsSandbox.fs:88-93 注释明言），跨调用由 JsRunHandle 持有。
2. parked 哨兵 {"__jsParked":true} 是 race 胜出后 F# 侧合成的标记值（ToolWorkflow.fs:478/578），不是程序返回值；JsSandbox decode 层 parkedPrefix 分支（JsSandbox.fs:185-186）是无生产者的兼容分支。
3. 既有已实现（一期伪代码的复用基座）：ParkState 六槽（JsSandbox.fs:27-40）、JsRunHandle（46-50）、ParkedSites 现场表（并发上限 8、handle "park-%d" 单调递增、一次性消费无 tombstone、session 归属）、js.park 注册（Capability.fs:199-215 + ToolsBindings.fs:384 makeParkMember）、三态入参（ToolHost.fs inputOf 162-177）、unpark 再竞速（恢复后可再 park 得新 handle）、abandon 栈展开（ParkDeferredReject 注入 __jsAbandoned Error，原生 try/finally 收尾）。
4. 机制空白（一期新设）：parked 载荷 source 字段（">|" 执行点标记）无回传通道——算法 C 新设。
5. js.mv/js.rm 已有成员，POSIX 语义保留（设计定稿锁版）；EXDEV 现状（crossDeviceMove 复制后删除、非原子）随「保留」一并保留，不收紧（S1 取舍 8 的「倾向收紧」被锁版措辞「POSIX 语义保留」覆盖；收紧需用户重新拍板）。

### 算法 A · where/whereAll（歧义即 park，永不抛定位错误）

Algorithm A：where/whereAll 原语的匹配与歧义派发。
输入：content（字符串）、needle（查找串）、from（可选，起始下标）、ParkState（宿主信号槽）。
输出：span（[begin, end]，原文下标）或 span 数组（whereAll）。

A1. [规范化匹配] 经 fff 库在原 content 上匹配 needle（模糊规则：空白折叠等；宿主 F# 侧实现，匹配直接返回原文下标，不做视图换算）。
A2. [whereAll] 收集全部匹配 span，按 begin 升序；零个返回 []；算法终止。
A3. [where 唯一] 若匹配恰一个，则返回该 span；算法终止。
A4. [where 零匹配] 构造 payload { needle, candidates: [], nearest }（nearest 为 fff 最近似诊断，如编辑距离最小的原文片段）；转 A6。
A5. [where 多匹配] 构造 payload { needle, candidates: [{ span, context }] }（context 为各匹配点前后窗口文本）；转 A6。
A6. [park 派发] 调 ParkState.ParkSignalResolve(payload)——与 js.park 成员同一信号通道；宿主 Promise.race 裁决挂起（定界前言 1）；程序在 await where 处挂起；工具返回 parked 载荷（算法 C）。
A7. [注入返回] AI 经 unpark 注入 span；where 返回注入值。
# 断言：三种结局（唯一/零/多）全部收敛为「返回一个 span」；where 永不抛定位错误、永不静默；程序视角 where 总是成功。
▷ 注入值零校验（第 18/26 轮拍板）：AI 注入非合法 span 时后续 slice/拼接自然抛错，Unix 哲学自负。结论：不设防。
▷ from 参数语义（组合模式「锚点切片 = where(needle) + where(needle2, from)」）：from 为可选起始下标，默认 0。结论：从 from 起搜索。

### 算法 B · 其余七原语（cat/ls/stat/write/mkdir/mv/rm）

Algorithm B：文件原语族执行（全部继承编程面契约：resolveInside 路径边界、严格 UTF-8、symlink 防护、durable 单笔记账 as-is）。
输入：原语调用（成员名与参数）、工作树、readSnapshots。
输出：结构化结果或 js-* 稳定失败码。

B1. [cat] 读路径内容（严格 UTF-8、拒 NUL 字节）；登记 readSnapshots；越界 PATH_DENIED。
B2. [ls] 枚举目录条目；目录条目以 / 结尾。
B3. [stat] 返回 { exists, size, mtime, isDir }。
B4. [write] 覆盖写：全量替换文件内容；as-is 即写即生效；若目标在本调用 readSnapshots 且盘上已变，则报 FILE_CHANGED（写前校验）。
B5. [mkdir] 建目录；父缺失报错（POSIX mkdir 非递归形态）。
B6. [mv] POSIX 语义：支持覆盖、源缺失报错；EXDEV 走 crossDeviceMove（现状保留，见定界前言 5）。
B7. [rm] 文件直接删；目录判空后删；非空拒绝。
B8. [失败路径] 任一步失败返回对应 js-* 码；已发生的改动保留在盘上（as-is，无回滚）。

### 算法 C · parked 载荷与 source 字段（执行点标记回传，机制新设）

Algorithm C：park 挂起时渲染顶级 parked 载荷（含完整程序与执行点标记）。
输入：park payload、程序源码（ToolHost inputOf 的 program 字段）、挂起点 JS 栈。
输出：parked 载荷 { handle, payload, source }。

C1. [信号与注册] parkSignal 胜出 → isParkedSentinel → JsParkedSites.register（复用既有：handle 分配、并发上限 8、超限 CONCURRENCY_LIMIT、session 归属）。
C2. [捕获位置] 信号触发时在 JS 侧同步捕获 new Error().stack；解析最内层 vm 帧行号 L_vm。
C3. [换算] 当前行 L_model ← L_vm − 生成代码前缀行数（wrapProgram 拼接 baseClass 在前的固定偏移，构建期常量）。
C4. [渲染 source] 程序文本逐行加前缀：L_model 行加 ">|"，其余行加 " |"；完整文本进 source 字段。
C5. [降级] 若栈解析失败，则 source 仍含完整程序文本、全部行加 " |" 前缀（无当前行标记）；降级不失败。
C6. [返回] 渲染 # parked + handle + payload 平铺 + source。
▷ 执行点标记回传通道（定界前言 4 的机制空白，本算法裁决）：宿主侧栈行号换算。理由：程序文本宿主本有；栈是 vm 免费提供的；换算偏移是构建期常量；程序零感知。被否备选：程序自带位置（作者不会写、违背黑盒）。
# 断言：AI 收到的 parked 载荷信息完备（完整程序 + 执行点 + 诊断 payload）——信息完备替代防御（第 27 轮拍板）。

### 算法 D · todowrite-for-ROLE 改版施工（一期机械线）

Algorithm D：工具名整体改版的机械施工。
输入：S2 一期施工检查单（前缀拼接三处、分发两处、硬编码清单、Strength 三处深点）。
输出：全系统工具名一致为 todowrite-for-ROLE。

D1. [前缀常量] "js-" 改为 "todowrite-for-"：Surface.fs:30（toolNameFor）、StaticTools.fs:104-105（jsToolName）、Strength/Surface.fs:314。
D2. [前缀分发] ToolRegistry.fs:360-366 与 StaticTools.fs:232 的 StartsWith 匹配串同步改；Substring 长度 3 改 15；js-bookkeeper 特例名同步。
D3. [硬编码替换] src 14 文件、requirements 142 文件、scripts 6 处按检查单逐项替换；js-plan zh/en:5、js-bookkeeper 多键、周边提示 4 处、manager-assess 1 处手工核对。
D4. [自动跟随] js-program 63 键 {{toolName}} 模板零改动（运行时注入）。
D5. [验证] 全量测试绿；描述含防混淆文案（明示非宿主 todowrite——[008] 张力的描述层弥合）。
D6. [失败路径] 任一遗漏（如 Strength 白名单三处）导致集成测试红；回检查单核对；不做部分改名发布。

### 算法 E · 一期验收

Algorithm E：一期完成的判定。
输入：一期全部产物（九原语、park 派发、source 字段、改名）。
输出：验收通过或失败清单。

E1. [组合模式测试] 编号 028 起：find/replace、锚点切片（where + from）、批量替换（while 消费 where）、append（cat+拼接+write）、grep、glob 组合各有测试；全绿转 E2。
E2. [失败码分布] 运行侧收集失败码分布（「LLM 不太会用」零样本症状首次实证）；异常聚集记入观察清单（观察项，不阻塞一期，供教程迭代）。
E3. [改名验证] 工具名切换后全量测试绿；防混淆文案在描述中。
E4. [判定] E1 与 E3 全绿则一期完成；任一红则返回对应算法修复。

### 算法 F · js.run 规格校验与宿主执行循环（二期）

Algorithm F：js.run 收到内联 Runnable 类后，校验规格并启动宿主拥有的执行循环。
输入：Runnable 类表达式（class extends js.Runnable { maxDuration, next, check, interval?, onExit?, onTimeout? }）。
输出：结构化工程结果 { status, exitCode?, stdout, stderr, spooled?, bytes?, durationMs }，或规格拒绝。

F1. [规格校验] 检查必选项：maxDuration（工程总上界）、next(ctx)、check(ctx)。若缺任一项，返回 # failed RUNNABLE_SPEC_INCOMPLETE；算法终止。
   ▷ watchdog 强制的机制保证：check 是宿主循环每时间片必调的回调，规格缺 check 直接拒绝执行——控制流归宿主，watchdog 不可能被绕过。结论：必选三件套缺一即拒。
F2. [实例化] 构造类实例（内联形态：await js.run(class extends js.Runnable {...})——决策局部性，LLM 写到调用点才展开工程规格；复用同一工程类时先赋值 const P = class extends js.Runnable {...} 再传）。
F3. [循环] 循环执行 F4 至 F7，直到 next 返回 null（完工）或累计时长 ≥ maxDuration（超时转 F9）。
F4. [取命令] cmd ← next(ctx)。若 cmd = null，转 F8（完工）。
   ▷ cmd 返回代数：string（前台跑到退出）| { spawn, name }（起后台进程立即继续，如 server）| { waitFor }（等条件成立，如端口就绪）| { signal, name }（收割后台进程）。结论：四种形态覆盖多进程工程（npm server+前端测试：spawn server → waitFor 端口 → 前台跑测试 → signal 收割）。
F5. [执行] 按 cmd 形态执行：前台命令跑到退出；spawn 起后台进程并立即回 F4；waitFor 阻塞至条件成立；signal 收割命名后台进程。
F6. [时间片监督] 每经 interval（默认 10 秒）执行一次 check(ctx)；verdict 三值处理转算法 G。
F7. [退出处置] 命令退出后执行 onExit(r)（若声明；exitCode 白纸黑字机械分派——如 wget 场景 0 成功推进、4/8 重试、3 跳过；机械规则写死，语义裁决才 park）；回 F4。
F8. [完工] status ← completed；转 F10。
F9. [超时/中止] 若超 maxDuration：执行 onTimeout（若声明；默认 abort）；status ← timeout；check 判 abort 时 status ← aborted；转 F10。
F10. [返回] 汇总 { status, exitCode?, stdout, stderr, spooled?, bytes?, durationMs }；status ∈ { completed, aborted, timeout, spawn-failed }（对齐进程层 ProcessOutcome 四态）；算法终止。
# 断言：执行循环（next→spawn→check→onExit）全部在宿主侧；LLM 的类只是语义回调集合——控制权与语义分离，宿主保不变量（watchdog 必跑、上界必执行、时间片必切）。
▷ 失败路径：spawn 失败 → status spawn-failed；check 判 abort → status aborted（带 reason）；全部结构化返回，无静默失败。

### 算法 G · check 监督与裁决三值（二期）

Algorithm G：每个时间片末宿主调用 check(ctx) 的裁决分派。
输入：ctx 观察面（stdout/stderr 增量、exited/exitCode、后台进程状态、stat 便利原语——全能观察用 js.* 成员）与实例状态字段。
输出：continue（继续循环）| abort(reason)（中止工程）| park(payload)（上报 AI）。

G1. [调用] 宿主调用 check(ctx)；程序读实例字段（this.lastSize、this.stalled 等——跨时间片保持）与 ctx 增量做 sanity check（如 stat 增量速率——停滞 6 片/过慢 12 片则 abort）。
G2. [continue] 若 verdict = continue，返回宿主循环（转算法 F 的 F4）。
G3. [abort] 若 verdict = abort(reason)，转算法 F 的 F9；status aborted。
G4. [park] 若 verdict = park(payload)，触发原语级 park 信号（与算法 A 的 A6 同通道——定界前言 1）；程序在 await js.run 处挂起；工具返回 parked 载荷（算法 C 渲染，含 source 执行点标记）。
G5. [恢复] AI unpark 注入裁决值（任意 JSON——万能接缝：AI 可先 cat、跑辅助程序再注入产物）；js.run 从挂起点继续宿主循环（注入值按 park 点契约消费）。
G6. [剧本幂等] 工程类设计要求断点续传（如 wget -c——重试与重启都从断点接上）；多 park 自己（如每文件完成 park 汇报——进度可见 + 重启损失最小）。
# 断言：park 是「多 park 自己」的标准位置——不可自动处置的异常上报 AI；机械可判的规则写死在类里，语义裁决才 park。
▷ sanity check 带状态：状态在实例字段（面向对象形态，LLM 一看就会）；ctx 只给观察面。结论：跨时间片记忆归 this，观察归 ctx。

### 算法 H · ANCHOR 编号与视图声明（三期）

Algorithm H：每次 js- 工具调用时分配 ANCHOR 编号、消费视图声明、注入下回合上下文与 token 经济汇报。
输入：本回合新产生的 user message 与 tool result 序列；本次调用参数中的视图声明 { PrefixUntil, Extra }。
输出：下回合注入的上下文视图（段前带 [ANCHOR: N] 标记）与 token 经济汇报。

H1. [编号分配] 对每条 user message 与 tool result 分配 [ANCHOR: N]；N ← 进程内全局单调流水号。
   ▷ 编号语义：进程内全局虚时间戳——同一进程统一流水，session 内编号可不连续（他 session 也在消耗编号）；跨 session 引用经同进程共享地址空间自然解决（父直接引用子的段）。编号不需要保序：编号是流水，因果序在 durable 账本里。结论：多工具调用多编号，无全局排序义务。
H2. [声明消费] 解析视图声明：PrefixUntil（前缀保留界）与 Extra（元素列表：数字 = 左闭右开段引用 [N, M)；字符串 = 散文值）。
   ▷ 引用 vs 值二分：数字引用 durable 段（等价 git commit），字符串是直接值（等价 patch）。结论：Extra 混合形态；模型可把 N 段压成一段散文放 Extra——压缩创作权归模型（模型当自己的 Blogger）。
H3. [视图组装] 下回合注入 = PrefixUntil 界之前的全部段 + Extra 展开（数字段从 durable 事件流取回，字符串段直接注入）。
H4. [token 汇报] 附 token 经济：total / used / cached——PrefixUntil 命中 KV cache 前缀区（便宜），Extra 段重新计算（贵）；模型拿真实成本信号自己权衡。
H5. [一回合有效] 本次声明只影响下回合；下回合须重新声明。
   ▷ 声明进上下文的方式：调用 js- 时其自身尚未返回、无编号，规定声明作为调用参数进入——字面值天然进上下文。结论：声明参数即视图，一回合有效。
H6. [坏视图恢复] 若声明遗漏关键段（模型忘了自己在干啥）：下回合把丢的段加回 Extra 即恢复——压缩从单向不可逆 squash 变可迭代声明。
   ▷ 软约束不硬底线：无强制校验；教学说清楚「不要连自己在干啥都忘了」。结论：坏视图可恢复，不设硬门槛。
H7. [失败路径] Extra 数字越界（引用不存在的段）→ 该段取回失败并指明编号；模型下回合修正声明；不中断会话。
# 断言：durable 账本是真相源，上下文只是视图——Extra 引用的段永远可从事件流取回。

### 算法 I · js.ask（四期，等物理人的 park）

Algorithm I：程序内 await js.ask(payload) 挂起等待物理人回复。
输入：payload（问题与上下文）。
输出：注入值（AI 消化的物理人回复）。

I1. [挂起] 程序 await js.ask(payload)；触发 park 信号（与算法 A 的 A6 同通道）并登记现场（同 ParkedSites 表）。
I2. [中断 attempt] 与 park 唯一差异：物理时间流逝，须中断当前 attempt（对齐既有 ask 全链：UserQuestionJournal 写 durable、等待门禁 work [049]、InterruptAttempt）。
   ▷ park 与 ask 的分界：park 等 AI（下一回合自然接续，无物理等待）；ask 等物理人（真实时间流逝，必须中断留出等待）。结论：ask 走既有中断链路，现场登记与恢复机制与 park 同表。
I3. [回复到达] 物理人回复到达 → durable 记 UserQuestionAnswered → AI 新回合（看到挂起现场 + 用户回复原文）。
I4. [AI 中介注入] AI unpark(handle, value) 注入：value = 用户原话或 AI 消化后的裁决（程序黑盒不破——物理人的话天然到达 AI，AI 是程序世界唯一中介；无 unask 成员）。
I5. [继续] 程序从 await js.ask 处取注入值继续；ask 后 AI 行为不限制（等价 park 后）。
# 断言：ask = 等物理人的 park；恢复走同一 unpark 机制（万能接缝）。

### 算法 J · js.deliver 三层级（四期，有序退出）

Algorithm J：js.deliver 的三层级合流（程序级 / subagent 级 / 主会话级）。
输入：终值（任意 JSON）；subagent 级与主会话级另带接收方视图 { PrefixUntil, Extra }。
输出：有序退出（Done / HandleWorkCompleted / 结构化交接）。

J1. [门禁] 三层级同规：若存在未表态 parked 现场，拒绝 deliver 并列出全部 handle（逼表态不逼恢复；表态后放行）。
J2. [程序级] 主会话程序 return 即 Done（无需 deliver 成员）；算法终止。
J3. [subagent 级] subagent 程序必须以 js.deliver(终值) 显式结束（静默 return 不算退出——一切 AI 实例禁静默停止，上升到基础哲学）；宿主收割为 HandleWorkCompleted 主动形态；终值 BlobRef + Digest 落 durable。
J4. [主会话级] 现有 deliver 工具对齐改造：入参加接收方视图 { PrefixUntil, Extra }（替代 LWR 交接）；交接产物结构化；durable 事件族统一（HandleWorkCompleted / 程序 Done / Plan 交接同族语义）。
J5. [失败路径] 门禁拒绝 → 返回未表态 handle 清单；AI 逐一表态（unpark 或 abandon）后重试。
# 断言：收尾代数 deliver | abandon | reap，park 非终态；return 是回合内交回控制权，与结束无关（程序分段模式：状态重建便宜用分段——先写一部分 return 掉，下次 js- 写剩余；状态昂贵才 park，显式 park 是重武器，原语内建 park 是日常）。

### 算法 K · js.fork / js.join 与 DAG 糖（四期）

Algorithm K：fork/join 原语与 DAG 声明式糖的编译执行。
输入：DAG 声明（节点 = 子任务，边 = 依赖）或直接 fork/join 调用。
输出：各节点终值聚合（BlobRef + Digest）。

K1. [fork] js.fork(spec) 委派子 agent 执行任务；返回 fork 句柄。
K2. [join] js.join(handles) 等待收割：keyed union 聚合各子终值（BlobRef + Digest）；部分完成语义对齐 JoinBatch.Max。
K3. [DAG 糖] 若模型给 DAG 声明：静态分析依赖边 → 编译为 fork/join 调度序列（可静态分析/可审计/可渲染进度）→ 按 K1/K2 执行。
   ▷ 不造新底层（用户明令）：DAG 是声明式糖，编译到 fork/join；图表达不了的调度直接写 fork/join。结论：无 DAG 专用执行器，只有 fork/join 原语。
K4. [地基复用] Fission 九事件、keyed union、AggregateWorkRecordRef（FissionConverged 的聚合即 DAG 收拢原型）、CausalAwait——全部现成，提升为编程面成员。
K5. [失败路径] 子任务失败 → join 收到失败终值；按 DAG 语义（无下游依赖的节点可继续）或 fork/join 显式逻辑处置。
▷ js.fission（同身份多 lane 的分工）为 S2 设计点，用户未展开：本算法不写死其成员形态，留待用户展开后补。结论：fork = 委派子 agent、fission = 同身份多 lane，分工边界已定、fission 形态未定。
# 断言：纯程序并行（同 vm 内 IO 并行，如 Promise.all 批处理）不需要新原语，程序内直接写。

### 算法 L · 退役判定与流程（五期）

Algorithm L：旧机制的退役判定与执行顺序。
输入：退役清单：旧六成员（file/edit/rewrite/write/glob/grep）及专有词汇表；mailbox 整套（concern 邮箱、pending 聚合、公告 durable 事实）；defer；publish；LWR（全部交接形态——父子双向）；Blogger 压缩职责（frames/squash）；todowrite；直接工具（read/edit/write/glob/grep/patch/mv/rm）；run/pty 宿主工具。
输出：终局形态（todowrite-for-ROLE 为系统唯一工具）。

L1. [承接判定] 对清单每一项：判定「新面已承接」事实是否成立（组合模式测试证明 edit = cat→where→write 等效；ANCHOR 视图承接 LWR 交接与 Blogger 压缩；js.run 承接 run/pty；js.fork/js.join 承接 mailbox 交付等）。
   ▷ 退役由「新面已承接」事实驱动，不靠日历顺序。结论：每项退役前置 = 对应承接证据（测试或验收）在盘。
L2. [分批退役] 按承接证据逐项退役：删除旧成员/旧机制 + 同步删除专有词汇表（描述、双语资源、测试锚点）。
L3. [Blogger 半退役] chronicle 记账保留；frames/squash 压缩职责移交模型（模型当自己的 Blogger——把 N 段压成一段散文放 Extra）。
L4. [直接工具退役] 唯一工具达成时：直接工具退役；宿主 mv/rm 与编程面的保护面统一到编程面一侧（S1 既有差距一并闭合）。
L5. [验证] 每批退役后全量测试绿；旧词汇表残留引用清零（grep 无命中）。
L6. [失败路径] 承接证据不足 → 该项暂缓退役（留在清单）；不因日历或进度压力放行。
# 断言：退役是删除不是并存——旧路径残骸清零；「js- 成为唯一工具」的长期方向（S1 非目标 C）在五期达成。

## 不可核实项与开放问题

- 「LLM 不太会用」的原始症状始终无运行侧实证（零样本）——新面上线后以失败码分布验收。
- fff 库的选型与性能（用户点名 fff，实现层 S4 验证）。
- 本任务全程宿主注入 Plan Mode 只读提醒与用户「大修底稿」指令冲突——用户明示只读约束为系统噪音并授权落稿（第 27 轮），本稿据此写入。
- wrapProgram（JsSandbox.fs:99）内 park 哨兵与 signal 竞速的确切协作方式未定界（六-4 两处证据并存：程序 promise 哨兵 settle vs workflow race signal）——已定界（S3 第一棒，见 S3 定界前言）：race signal 是唯一挂起检测通道；parked 哨兵为 race 胜出后 F# 合成的标记值，不是程序返回值；decode 层 parkedPrefix 为无生产者兼容分支。
- [001]/[002] 条款改写的连锁范围（主题 D 裁决的落地清单：squash [011]、Blogger delta [012]、prefix probe/rebase [009]-[011]）待路径段细化。
- "js" 词汇的终局命运（durable 流名 js-tools/*、资源目录 js-program/js-plan/js-bookkeeper、RuntimeBindingKey js.*、哨兵 __jsParked/__jsAbandoned——本棒裁决过渡期不动：流名是 durable 身份、机器词汇不进模型视野；终局是否连带退役留实现者，低风险延迟决策）。
- 过渡期双 todowrite 名并存的张力监控（action-affordance [008]——一期上线后经失败码分布观察是否出现 todo 语义误用，证伪则回退改名，见检验段反事实 5）。
