# 规划底稿：js- 编程面（S1 事实与取舍）

## 任务来源
用户输入：js- 工具提供 read/edit/write/grep/glob 的统一编程面；先初步了解，再确定本次要做什么。
用户第一轮答复：设想为 js- SDK 增加暂停-恢复机制——js.call_cc(value) 暂停当前程序、控制权交还 AI，{ handle, value } 作为工具返回值；之后 js.???(handle, value2) 恢复执行，原暂停表达式取值 value2。
用户第二轮答复：不用 call_cc，也不能用 resume（恢复 subagent 已占用 resume），要求重新命名一组并把语义打磨清楚。
用户第三轮裁决：采用沙箱现场保活 + 编排层恢复（方法四+六），程序黑盒零感知；All-or-Nothing 是过度保证，写语义全局翻转为 as-is（含直接工具共享合同）；重启即重置，句柄与 subagent 同命随进程死亡；finalizer 指程序内资源收尾能力——被 abandon 时程序要能自己收拾自己占有的资源。
用户第四轮拍板：as-is 全局反转；abandon 定名；交付硬门禁 + 每回合提醒（并入既有耗时提醒通道，列出全部 parked handle）；三态收尾（Done/Abandoned/Reaped）确认。
用户第五轮拍板：abandon/reap 以 ABANDONED rejection 实现程序内 finalizer（原生 try/finally，零新 API）；新增 js.rm（js.mv 同获许可，对齐仓库既有 mv/rm 工具命名）；长期方向 defer——最终废弃全部直接工具、js- 成为唯一工具（本次不做）。

## 目标形式化
（定稿，2026-10-16 用户逐项拍板。）本次围绕 js- 编程面要达成的可判定状态：

A. 编程面写语义全局翻转为 as-is：
1. js.edit / js.write 成功返回那一刻改动已在盘上；无 staging、无虚拟叠加、无结算时刻；同一文件多次 edit 每次读真实当前文件。
2. 程序以任何方式终止或挂起（throw、超时、输出超限、park 挂起）时，那一刻之前的改动已发生、之后的未发生；终止与挂起都是事实分界线，不是回滚点（挂起非终态，终态三态见 11）。
3. 单笔 mutation 原子性保留（MutationFs 文件层）；FILE_CHANGED 保留，校验点移到每次写前。
4. 失败返回渲染附「本次调用已发生的改动」清单（WHAT [016] 失败形态扩展）。
5. Engineer/DevOps 直接工具与程序工具的共享写语义同步 as-is（[027] 合同翻转）。
6. 需修订条款：[012]（一次调用一个事务→按 as-is 重述）、[014]（preflight 时机）、[016]（成功/失败形态）、[027]（共享合同）。

B. 暂停-恢复机制（黑盒续延，保活现场 + 编排层恢复）：
7. 暂停-恢复机制范围内程序内唯一新概念：await js.park(payload)——程序视角即 await 一个外部输入，对「恢复机制」零感知（黑盒）；park 为异步成员（返回宿主保留 resolve 权的 deferred Promise）。js.rm/js.mv 是另一条线的新增成员（见 15），不属本条。
8. 宿主：park 绑定触发暂停信号；runCore 竞速（程序 Promise vs 暂停信号）；挂起时现场 { context, programPromise, api（含可重置 deadline 引用）, readSnapshots } 登记进现场表（as-is 下无 staging；readSnapshots 随现场存活，支撑恢复后的写前校验）；工具返回顶级 parked 载荷（handle + payload）。
9. 编排层三态入参：{ program } | { unpark: { handle, value } } | { abandon: { handle } }；unpark 不做 SDK 成员（恢复是 AI 的动作，不是程序的动作）。恢复调用不建新沙箱：查现场表 → resolve deferred 注入 value → 继续竞速；恢复时 deadline 重置 now+10 秒。
10. 一次性恢复：resolve 后 handle 从现场表移除（不留 tombstone），重复恢复统一报 HANDLE_UNKNOWN；历史经 durable 审计可查（park/unpark/abandon/reap 各自的 durable 事件形态 S2 定）。
11. 三态收尾：unpark → 跑完 → Done（有终值）；abandon/reap → ABANDONED rejection 展开窗口 → Abandoned（无终值）；进程重启 → 无窗口消失（Parked 审计痕迹在 durable）。不存在「无声无息」的结束方式。
12. finalizer（程序内资源收尾）：abandon/reap 时宿主 reject ABANDONED 哨兵（__jsAbandoned 族）→ 栈从 await 处展开 → 程序以原生 try/finally 收拾自己占有的资源（零新 API，Python GeneratorExit 同构）；展开窗口 = 重置后的 10 秒 deadline；清理期 api 照常 as-is 生效；catch ABANDONED 后继续跑视为清理路径，返回值丢弃，结局仍记 Abandoned；清理期再 park 抛 CANNOT_PARK_WHILE_ABANDONING；进程重启无窗口（物理边界，合同写明）。
13. 泄漏兜底：交付级硬门禁（deliver 时现场非空未表态 → 拒绝交付并说明，表态后放行）；每回合提醒（并入既有耗时提醒注入通道，列出全部 parked handle，逼表态不逼恢复）；会话终结强制清理（现场条目记 session 归属，session 关闭时 reap 名下全部现场，durable 记 Reaped）。
14. 现场资源约束：并发现场上限（数值 S2 定）；现场年龄上限自动 reap 为待定项（用户未拍板）。
15. 新增成员：js.rm(path)（as-is 即删即生效、FILE_CHANGED 同 edit、durable 记账到单笔、编程面首个不可逆操作，支撑 finalizer 清理用例）与 js.mv(from, to)（用户口头许可）。行为语义对齐 WHAT [020] POSIX（mv 支持覆盖、源缺失报错；rm 拒绝非空目录）；保护语义继承编程面（resolveInside、写前校验、durable 单笔记账）；EXDEV 中间态是否收紧为 S2 裁决；失败码用 js-* 稳定码词汇（取舍 8：新成员走编程面契约），具体码表 S2 定。
16. durable 语义：现场是进程内活物，寿命与进程同寿（重启即重置，与 subagent 同命）；durable Parked 事实仅为审计痕迹；durable 流记账粒度跟到单笔 mutation（每笔写盘有账，形态 S2 定）。
17. 四层同构齐备：JsFragmentRegistry 加 park/rm/mv（park 的绑定形态为 deferred Promise，全新类别）、ToolsBindings、Description 与双语资源成对、ToolWorkflow（竞速挂起、现场表、三态入参、ABANDONED 协议）；测试 requirements/repository-programming/tests/028 起编号对齐。

C. 非目标：
18. 废弃全部直接工具、js- 成为唯一工具（用户 defer 的长期方向，本次不做；本次合同变更保持与其兼容）。

## 已核实的事实
出处：本任务绑定 DevOps 的只读调查（真实 bash 检索与文件阅读，2026-10-16）。

1. 真实形态：不存在五个独立工具 js-read/js-edit/js-write/js-grep/js-glob（全仓 grep 除一个测试标签外零命中）。实际是按角色生成的 js-ROLE 编程面工具系列：js-engineer、js-devops、js-manager、js-orchestrator、js-blogger、js-bookkeeper、js-plan、js-predictor。每个工具入参为一段 JS 程序（class Js extends JsProgram），运行时注入 SDK 对象 { js: { file, glob, grep, edit, rewrite, write } }。用户所说「read/edit/write/grep/glob 统一编程面」即指这套 SDK 成员。
2. 源码位置与四层同构：src/Wanxiangshu/Repository/Programming/Js/。Capability.fs（JsCapability = Read|Write|Edit|Glob|Grep；JsFragmentRegistry 为 js-* 成员唯一出生地，六 fragment：file/glob/grep/edit/rewrite/write，各带 MemberName/Signature/Description/CanonicalExample/RuntimeBindingKey，注释明言四层必须一致否则表面说谎）→ ToolsBindings.fs（createApi 按能力过滤生成注入沙箱的 { js: {...} }）→ Description.fs（JsCanonicalDescription，约 30 个 prose 锚点，双语资源经 resources/provider/tool/ 加载）→ OpenCode/ToolHost.fs + ToolWorkflow.fs（沙箱→staging→preflight→durable Prepare→写盘→durable Commit 的唯一编排）。注册接入点：OpenCode/Tools/ToolRegistry.fs:356-362，js- 前缀走 JsToolSpec.admissionFor 动态准入；Surface.fs:31 与 StaticTools.fs:104 均以 "js-" + 角色名小写派生工具名。无 .opencode/ 目录。
3. 实现方式：全部自实现（F# 经 Fable 编译到 dist/），不包装 opencode 内置工具、不调外部命令。物理原语同目录：Utf8Fs.fs（严格 UTF-8、拒 NUL 字节）、GlobFs.fs（gitignore 式枚举、跳 .git、不跟随 symlink）、AnchorFs.fs（grep 与锚点定位）、MutationFs.fs（路径逃逸拦截 + 原子写盘回滚）。
4. 成员契约（ToolsBindings.fs）：file(path, matches) 解析路径边界（越界 PATH_DENIED）→ 严格 UTF-8 读取，登记 readSnapshots 与 explicitReads，返回 { ok, path, text, byteCount }；glob(pattern) 返回 { ok, paths }；grep(needle, pattern) 返回 { ok, matches: [{path, line, column, text}] }；edit(path, changes) 为 SDK 派生便利（{ find, put, all? } 定位），底层与 rewrite 共用同一受保护的暂存执行器；rewrite(path, newText) 暂存 Rewrite（缺失报 FILE_NOT_FOUND）；write(path, text) 暂存 Create（已存在报 FILE_ALREADY_EXISTS）。所有 mutation 只暂存不落盘；事务在写盘前 Prepared、写盘后 Committed，崩溃只撤销可证明已写的部分。工具级结果以 Synthetic TOML 渲染（成功 # ok + [data]/[fs]，失败 # failed + code/reason）。
5. 完成度：五能力六成员全部实现；测试 requirements/repository-programming/tests/001-027 共 27 个 .test.mjs，另有 grounding、capability-enforcement、durable-convergence 引用。已知缺口（tests/README.md 自报 + 巡检）：GAP-088/089 未闭合；017「同一助理消息实际并行调用由宿主按序执行」为 TODO；027「直接工具与程序工具是否共用全部保护」为 TODO；capability-enforcement/tests/025.test.mjs:498 有 GAP-075 的 test.todo（零物理读观察端口不存在）；ToolHost.fs:24 的 BuiltinToolDescriptionHook.annotate 为恒等占位。
6. 与内置工具的关系（ToolHost.fs:11-14 注释）：内置 read/edit/write/glob/grep 保留为 primitive fallback，Host 描述不动；偏好意图级程序面一事在 js-* 自身描述与 Ultra Example 中教授。权限约束：StaticTools.fs:217 Manager 对原生 read/grep/glob deny（只能走 js-manager 只读面）；repository-programming WHAT [027] 规定 Engineer/DevOps 同时获得直接工具与 js-* 工具，两者共享路径边界、UTF-8 校验、symlink 防护与 All-or-Nothing 事务语义；requirement-grounding [010] 要求跨工具源统一接地。
7. 近期提交脉络（git log 只读查询）：10-16 chronicle、10-15 wanxiangli 0.7.0 五卡集成、10-13 生命周期统一、10-12 js-plan 锚点教程复用、10-10/11 Plan 模式与 js-plan 编程面重构、10-10 toolgate capability 单源收敛。
8. 沙箱执行模型：Node vm（node:vm 的 createContext + runInContext），程序被包成 async IIFE，runInContext 返回 Promise 被 runCore 一次性 await 到底；双层超时（同步段 vm timeout 选项，异步段注入 Proxy 在每次 api 成员调用时查 Date.now() > deadlineEpochMs 抛 PROGRAM_TIMEOUT）；deadline 每次调用 10 秒硬编码（ToolHost.fs:154），输出预算 1 MiB；无真实内存上限（GAP-088 自报）；一次工具调用一个 vm context，用后即弃，现有代码无任何跨调用保存执行现场的机制；异常按 __jsFailure/__jsProgramFailed/__jsInvalidReturn 前缀识别（JS-019），不嗅探消息文本。
9. 事务链：staging/readSnapshots 是 runCore 内局部 ResizeArray，成功且非空才 commitMutations，任何失败路径 staging 直接随局部变量消亡、不落 durable；durable 流 js-tools/transactions，Prepared payload = {transactionId, workspaceRoot, mutations:[{path, originalText?, newText}]}，Committed = {transactionId}，提交顺序 AppendPrepared → preflight → 写盘 → AppendCommitted；uncommitted Prepared 仅作中断审计证据，重启不自动重放、不自动回滚、不补交。
10. 返回协议：JsToolOutcome 仅两态（Succeeded/Failed），SyntheticToml 唯一渲染（成功 # ok + [data]/[fs]，失败 # failed + code/reason），WHAT [016] 明文「成败由顶级结构区分，不另用 status 字段」；Execute 把渲染串直接交回 Host，全仓无第二处对 js- 结果的结构化解析（provider-projection [008] 禁止解析呈现文本做控制流），扩展 parked 顶级形态不破坏现有消费者，但 [016] 条款本身要修订。
11. ask 全链（同构参照）：askSpec → AskUser（仅 Plan 角色 + 精确 call 证据）→ 幂等检查 → UserQuestionJournal.request 写 durable → 宿主确认 ask 工具结果后 InterruptForUserQuestion → InterruptAttempt；等待门禁 IsWaitingForUser 由 durable pending 派生（非缓存），阻塞除 ask 外一切工具调用；用户真实输入追加 UserQuestionAnswered 解除等待，同一逻辑运行接续，不新建会话。合同：work [049]。
12. 触碰的现行条款：repository-programming [012]（一次调用一个事务）、[016]（顶级形态枚举）、[017]（同一助理消息的并行调用宿主按确定顺序执行、各为独立事务）、[014]（preflight FILE_CHANGED）；managed-session-lifecycle [016]（真实外部输入只许打断 join 等待、不得中断当前 LLM 输出；ask 与 deliver-post-S3 是仅有的两个模型主动等待例外）。
13. 关键语义澄清（由 11/12 推出的设计判断）：park 的恢复者是发起工具调用的 AI 自身，不等待物理人，因此不需要中断 attempt、不需要 waiting 门禁——工具调用正常返回暂停载荷，AI 在下一回合自然接续。park 不触碰 managed-session-lifecycle 的中断白名单，机制重量比 ask 低一个量级：工具返回协议新形态 + SDK 两个成员 + durable handle 登记。
14. 新增成员的改动面：四层（fragment 注册、createApi 绑定、Description stub 与双语资源成对新增、workflow 编排加 Parked 态与哨兵识别）+ 测试编号 028 起；permission 层若新增独立 capability 需同步五处 label 映射，挂现有 capability 则零改动；恢复外壳为编排层三态入参（见目标形式化 B.9）：同一 js-ROLE 工具新增 { unpark } / { abandon } 入参形态，不做 SDK 成员、不建新沙箱；js- 前缀动态准入自动覆盖。
15. 工具级 parked 顶级形态：三态入参的 { program } 态执行中挂起时，该次工具调用返回顶级 parked 载荷（handle + payload）；unpark 恢复调用在恢复后的程序再次 park 时的返回形态为 S2 设计点（见待核实假设），定稿未锁死。
16. GAP/TODO 原文已收：GAP-088（实际注册入口、异步/内存界限、OS 执行与清理缺完整证据）、GAP-089（旧测试适配尚需新产物执行，不沿用旧构建的绿色）、017 TODO（并行调用按序执行只有描述无执行证据）、027 TODO（直接工具与程序工具是否共用全部保护未证）。
17. 仓库既有 mv 与 rm 工具（devops 调查已回收）：定义 src/Wanxiangshu/OpenCode/Tools/FileMutationTools.fs（mv 收 source+destination 两 string，rm 收 path 一个 string）；准入 ToolRegistry.fs:346-347；名字投影 StaticTools.fs:24-25（ToolPermission.Move→["mv"]、Remove→["rm"]）。语义：mv=renameSync（同设备原子），EXDEV 走 crossDeviceMove（cpSync 全量复制后 rmSync source，非原子：copy 成功而删除失败时两侧并存且不回滚）；rm=文件 rmSync recursive=false、目录先 readdirSync 判空（空目录 recursive=true，非空拒绝 tool/rm/directory-not-empty）——POSIX rm 减递归形态（WHAT [020]：「mv 移动或重命名文件及目录，支持覆盖，源缺失报错；rm 删除文件或空目录，拒绝非空目录」）。
18. 每回合提醒通道既有（devops 调查已回收）：实现为 HOST-013 Pair Hint——src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fs（挂 OpenCode experimental.chat.messages.transform hook）+ PairProgrammingCalibration.fs 组装；文本出自 resources/provider/host/ 双语资源模板（elapsed/guideline/tool-estimate），{{elapsed}} 按每条 occurrence 落盘时刻由代码计算，durable 记 HostFact.PairProgrammingGuidelineAnchored，已投递字节冻结（历史按原字节 replay）。触发时机非无条件每回合：最新一条真实消息须承载 terminal 工具结果或 user 文本（载体）才生成新 occurrence，无载体回合不注入；内部会话豁免，同一 placement 只 replay。parked handle 提醒并入同一通道；park 场景下 parked 载荷的工具返回本身就是 terminal 载体，下一回合必然有 placement。
19. 保护面关键事实：宿主 mv/rm 无路径边界（args 路径直通 NodeFs.*，无 resolveInside/realpath/symlink 防护）、无 FILE_CHANGED 快照校验、无 durable 记账（FileMutationTools.fs 全文无 EventStore 引用）；原生 read/write/edit（FileTools.fs）同样直通；宿主层路径控制仅 external_directory 元权限（capability-enforcement [011]）+ 角色 allow/deny。
20. 失败码词汇：mv/rm 失败码是资源键（tool/mv/failed、tool/rm/directory-not-empty；注释「The key is both the localization lookup and the code — there is no second vocabulary」），与 js-* 稳定码（FILE_NOT_FOUND 等）是两套词汇；结果渲染同所有者（ToolHostCodec.fs:607-613 → LlmFacing/SyntheticToml）但顶级结构不同（成功无指令前缀，与 js-* 的 # ok 不同）。
21. 权限：Move/Remove 仅 Engineer（OfficeCapability.fs:90-91）与 DevOps（106-107）；Manager 仅 Read/Glob/Grep（79-81），集成测试证实 Manager 调 mv 被拒（「mv is not available to Manager.」）；「Coder-only」头部注释陈旧（Role.Coder 权限集为空，OfficeCapability.fs:95）。
22. 测试：requirements/repository-programming/tests/020.test.mjs（307 行，锚点 WHAT[repository-programming-020]）双层：Surface 层（FileMutationSurface.js 直调，无权限门）+ 集成层（withExecutablePlugin 真实 hooks + authority root：AGENT_016 拒绝两例、AGENT_017 mv 三例、AGENT_018 rm 四例）；未覆盖 EXDEV 中间态。JsFragmentRegistry 现无 move/remove fragment（六成员现状）。
23. 既有张力：repository-programming [027] 声称直接工具与程序工具「共享路径边界、UTF-8 校验、符号链接防护和 All-or-Nothing 事务语义」，但宿主 mv/rm 与原生 read/write/edit 实际不具备编程面那层保护——条款文字与实现的既有差距。js.mv/js.rm 的语义对齐因此有两个基线：WHAT [020] POSIX 行为（宿主实然）vs [027] 编程面保护（js 家族应然）——S2 裁决点，取舍第 8 条已记录倾向。

## 待核实的假设
- unpark 恢复调用在恢复后的程序再次 park 时的工具返回形态（顶级 parked 新 handle 或其他）：S2 设计点，定稿只锁了 { program } 态直接挂起返回顶级 parked（见事实 15）。
- 现场表在 runCore 竞速挂起下的确切实现路径（Task.WhenAny 语义、programPromise 的存活引用方式、Proxy deadline 引用的可重置点）：S2 设计前置。
- mv/rm ToolSpec 进入宿主注册列表的具体组装点（staticAdmissions 与集成测试已证注册，组装代码位置未读）：js.mv/js.rm 落地前补读。
- EXDEV 跨设备移动的中间态（copy 成功而 rmSync 失败时两侧并存且不回滚）无测试证据：js.mv 是否保留该 POSIX 行为或收紧，S2 裁决。
- 原生 read/write/edit 是否在未读位置另有包装边界（grep 无命中、直读 144 行无边界；Host 层 external_directory permission.ask 运行时行为未验证）：[027] 张力定界的前置。
- 全仓 grep 撞名核查（park/unpark/abandon/rm/mv 落地前，S2 前置）。
- SyntheticToml.document 的字节级布局：扩展 parked 顶级形态与失败形态 [fs] 清单前必读。
- Transaction.fs 的 preflight 现行指纹比对逻辑：as-is 重写（校验点移到每次写前）必读。
- 现场年龄上限自动 reap（如 24 小时）：用户未拍板，待定项。
- annotate 恒等占位是「有意暂不启用」还是「待办」：ToolHost.fs 的 GrandRewrite 注释措辞暗示有意保留，推断未核实。
- BuiltinFilesystemTools 集合含 "patch" 而 knownToolNames 无 patch：性质不明，需查两集合用途差异。

## 不可核实项
（暂无。）

## 根因
本任务不是修缺陷而是加机制，「追到底」的对象是设计驱动力：
1. 为什么需要暂停-恢复：现行「程序一次跑完」的模型下，程序中途需要 AI 决策时只能结束并把中间状态序列化进返回值，由 AI 手抄进新程序——循环状态、闭包、中间计算全部丢失或走样，且靠 AI 复述传递状态本身易错。暂停-恢复把 AI 决策点变成程序内显式控制流，暂停点之后的执行上下文由宿主机制保管（保活现场模型下由现场表的活栈保管；决定性重放模型由「程序文本 + 注入值序列」重放保管，已否决，见 2），消除手抄。
2. 为什么保活现场而非重放（规划者最初推荐重放，经用户以黑盒税论点裁决翻转，见取舍 1）：决定性重放要求每个程序作者理解「可决定性重放」并终身交税——Math.random/Date.now/闭包状态皆可致恢复失败；保活现场程序零感知，代价是宿主侧一次性成本：破现行三大不变量（沙箱 context 一次调用一建一弃、进程会重启、staging 失败即丢弃）并新造一层跨调用现场登记（现场表）。安全性不靠重放，靠既有机制兜底：世界状态只能经 api 显式读取（登记 readSnapshots）+ 写前 FILE_CHANGED 把关，恢复后的执行看的是恢复时刻的真实世界，暂停期间的世界变化不会被蒙混过去。现场是进程内活物、重启即重置（与 subagent 同命），durable 只记 Parked/Reaped 审计痕迹（见 16）——内存现场不落账，账本哲学不受损。
3. 为什么 park 不需要 ask 式中断：ask 等物理人（真实时间流逝，必须中断 attempt 留出等待）；park 的恢复者是 AI 自己，拿到工具返回值即在下一回合决策，无物理等待。见事实 13。
4. 为什么 one-shot：multi-shot 意味着同一暂停点可多次注入多次续跑，等价于「同一暂停点的多个平行未来」，与「事件只追加、分叉必须显式裁决」的账本哲学冲突；one-shot 下 handle 至多消费一次，每个暂停点在账本上至多对应一笔续延裁决。
5. 为什么 as-is：AI 是作者与恢复者的语境下，All-or-Nothing 让失败者对世界状态一无所知（throw 在第 10 个 edit 时前 9 个被悄悄丢掉，AI 只能全部重跑）；as-is 让「发生了的就是发生了」，失败返回附已发生清单，事实分界线清晰。与账本哲学（事件只追加、不回滚）一致。
6. 为什么黑盒优先（复杂度下沉）：编程面合同越薄，AI 作者越不容易写错。重放模型要求每个程序作者理解「可决定性重放」并终身交税；保活模型程序零感知，重量（现场表、竞速挂起、reap）落在宿主侧——系统作者可控的一次成本，优于程序侧的终身税。

## 取舍
1. 续延模型：保活现场 + 编排层恢复（用户拍板，推翻规划者此前的重放推荐）。为什么不是决定性重放：重放把「可决定性重放」的终身税强加给每个程序作者（Math.random/Date/闭包状态皆可致 HANDLE_STALE），违背黑盒目标；复杂度应下沉到系统侧（现场表、竞速、reap 是系统作者可控的一次成本）而非程序侧。为什么不是 generator 驱动器/CPS 编译：绑架程序写法、自造解释器。为什么不是同步阻塞：单线程事件循环死锁。
2. 写语义：as-is 全局翻转（用户拍板：All-or-Nothing 是过度保证）。为什么不是仅新机制用 as-is、旧合同保留：两种写语义并存让 [027] 共享合同与 AI 心智模型双重分裂；as-is 与「事件只发生、不回滚」的账本哲学一致，且顺带消解「结算点 vs 长事务」子取舍——跨调用事务问题不复存在。
3. finalizer：ABANDONED rejection + 原生 try/finally（零新 API）。为什么不是注册回调式 on_abandon(fn)：新 API、宿主侧回调调用栈环境诡异、表达力反而不如语言原生栈展开。先例：Python GeneratorExit（close 在挂起点抛异常让 finally 执行）、Go panic+defer。
4. 恢复外壳：编排层入参字段 { unpark: { handle, value } }，不做 SDK 成员。为什么不是程序内成员：黑盒视角里「恢复」是 AI 的动作不是程序的动作；SDK 成员形态会把恢复逻辑混进程序文本，程序被迫知道恢复机制的存在。
5. 门禁：交付级硬门禁（拒绝交付直到表态）+ 每回合提醒（并入耗时提醒通道）。为什么不是只靠会话终结 reap：泄漏驻留时间过长。为什么不是逼恢复：AI 合法可以不继续，门禁只逼表态（unpark 或 abandon），不替 AI 做决定。
6. 句柄寿命：进程寿命，重启即重置（与 subagent 同命，用户拍板）。为什么不是 durable 现场序列化：活栈不可序列化是物理边界。
7. 命名词族：park / unpark / abandon（停住 / 继续 / 放弃，三个单音节动词）；js.rm / js.mv 对齐仓库既有 mv/rm 工具命名（用户许可）。已排除：resume（subagent 占用）、continue/yield（JS 关键字与 generator 混淆）、deliver/handoff/ask/checkpoint（系统既有术语）、resolve/fulfill/send（实现词外露或语义泛撞）。
8. js.mv/js.rm 语义基线：行为语义对齐 WHAT [020] POSIX（mv 支持覆盖、源缺失报错；rm 拒绝非空目录），保护语义继承编程面（resolveInside、写前 FILE_CHANGED、durable 单笔记账）。为什么不是完全复制宿主工具现状（无边界无记账）：新成员进 JsFragmentRegistry 天然走编程面契约，且「js-* 成为唯一工具」的长期方向下保护面终将统一到编程面一侧；宿主工具的无保护现状是既有差距而非应然。


## S2 · 理解与路径

### 理解

用自己的话重述问题。

**输入**：一段 JS 程序，运行在 Node vm 沙箱（createContext + runInContext）里，被包成 async IIFE 由 runCore 一次 await 到底。程序唯一可见的编程面是注入的 { js: { file, glob, grep, edit, rewrite, write } } SDK。一次工具调用一个 vm context，用后即弃，现有代码无任何跨调用保存执行现场的机制。

**要达成的输出**：给这个编程面加两样东西——(a) 暂停-恢复机制：程序在 await js.park(payload) 处挂起，宿主保活现场，AI 下一回合用编排层入参 { unpark: { handle, value } } 恢复或 { abandon: { handle } } 放弃；(b) 写语义从 All-or-Nothing 全局翻转为 as-is：每笔写成功返回那一刻即落盘，终止/挂起都是事实分界线而非回滚点。

**约束**（用户逐项拍板，S1 已锁）：
- 程序黑盒零感知：只新增 await js.park(payload) 一个成员；「恢复」是 AI 的动作，不是程序的动作，unpark 不做 SDK 成员。
- abandon/reap 用 ABANDONED rejection 走程序内原生 try/finally 实现 finalizer，零新 API。
- 句柄与 subagent 同命，随进程死亡重置；durable 只记 Parked/Reaped 审计痕迹，不序列化现场。
- 单笔 mutation 原子性保留（MutationFs 文件层）；FILE_CHANGED 保留，校验点移到每次写前。
- 三态收尾：unpark→Done（有终值）；abandon/reap→Abandoned（无终值）；重启→无窗口消失（Parked 痕迹在 durable）。不存在无声无息的结束方式。

**难点**（为什么不是改几行就完）：
1. 破三大不变量：沙箱 context 一次调用一建一弃、进程会重启、staging 失败即丢弃。保活现场要求 context 与程序 Promise 跨调用存活，staging 语义被 as-is 取代。
2. 现场表登记与竞速挂起：runCore 要同时 await 程序 Promise 和暂停信号，挂起时把现场 { context, programPromise, api（含可重置 deadline 引用）, readSnapshots } 登记进现场表；恢复时查表 → resolve deferred → 继续竞速。
3. as-is 下校验点移位：不再有统一 preflight 结算点，每笔写前各自校验 FILE_CHANGED；失败返回要附「本次调用已发生的改动」清单。
4. ABANDONED 展开窗口：abandon/reap 时 reject 哨兵，栈从 await 处展开让 try/finally 收拾资源；清理期 api 照常生效；清理期再 park 要报错。
5. 泄漏兜底：交付硬门禁 + 每回合提醒 + 会话终结强制 reap，三者配合保证没有无声无息的结束。

### 探索

候选路径围绕 S2 的设计点展开。S1 已锁死续延模型（保活现场 + 编排层恢复，用户拍板），故候选路径不是重选模型，而是选实现细节。每条三行：核心思路、主要风险、被什么事实支持或否定。

- **路径 A：as-is 写语义改造先行，暂停-恢复叠加其上（分层改造）**
  - 核心思路：先翻写语义（去统一 staging 结算、每笔写前 FILE_CHANGED 校验、失败附已发生改动清单），再在 as-is 地基上加现场表与竞速挂起。
  - 主要风险：两轮改造都动 ToolWorkflow 核心事务链，中间态测试面大；as-is 改造本身要重写 preflight 时机。
  - 事实支持：preflight 现被调三次（ToolWorkflow.fs:282/284 两次、Transaction.fs:191-194 durable 路径第三次），as-is 要求校验点移到每次写前——这是独立于暂停-恢复的纯语义翻转；S1 目标 A 与 B 可分开判定。

- **路径 B：暂停-恢复先行，as-is 后补**
  - 核心思路：先加现场表与竞速挂起（在现有 staging 语义上保活），再翻写语义。
  - 主要风险：现场表形态依赖写语义——staging 语义下现场表保的是未落盘改动，as-is 下保的是已发生改动清单，先做暂停-恢复会先按旧语义建现场表再推翻。
  - 事实支持：S1 目标 B.8 明言「as-is 下无 staging；readSnapshots 随现场存活」——现场表字段形态由 as-is 决定，先做暂停-恢复违背该依赖。

- **路径 C：JsSandbox 层提升 programPromise/context/deadline 为现场可持有对象**
  - 核心思路：改 JsSandbox.runSurface 签名与 wrapProgram 生成代码，把 programPromise 暴露给 runCore；deadline 从内联常量改为 api 闭包捕获的可重置引用。
  - 主要风险：vm context 跨调用保活的语义边界（context 全局状态跨调用可见）、JS Promise 与 F# Task 的桥接、deadline 从内联常量改引用后生成代码的改法。
  - 事实支持：JsSandbox.fs:133-138 把 createContext/runInContext 与一次性 await 封死在 run 内、47-48 把 deadlineEpochMs 内联进生成 JS 不可重置——这是 S1「runCore 竞速挂起 + 现场表保活」方案的结构性障碍，必须先拆。

- **路径 D：现场表放 ToolWorkflow 模块级静态表**
  - 核心思路：模块级 ConcurrentDictionary<handle, JsParkedSite>，handle 用递增计数或 GUID，随进程存活；会话终结按 session 归属清理。
  - 主要风险：并发访问与 handle 生成碰撞、模块级静态在测试间泄漏（需清理钩子）。
  - 事实支持：S1 目标 B.10 一次性恢复（不留 tombstone）、B.13 会话终结强制清理、B.16 现场是进程内活物（重启即重置）——模块级静态表与「进程内活物、不落 durable」直接对应。

- **路径 E：ABANDONED 原生栈展开协议**
  - 核心思路：abandon/reap 时宿主 reject __jsAbandoned 哨兵，栈从 await 处展开，程序以原生 try/finally 收拾资源；清理期 api as-is 生效、再 park 报 CANNOT_PARK_WHILE_ABANDONING。
  - 主要风险：rejection 跨 vm 边界的传播路径、展开窗口（重置后 10 秒）内清理超时判定、catch ABANDONED 后继续跑的截断。
  - 事实支持：S1 目标 12 已锁；JsSandbox.fs:28-30/66-72 现有哨兵识别机制（__jsProgramFailed/__jsHostFailed 前缀）可参照；Python GeneratorExit 先例（close 在挂起点抛异常让 finally 执行）。

- **路径 F：js.rm/js.mv 独立线（新增成员）与 durable 审计事件**
  - 核心思路：JsFragmentRegistry 加 rm/mv fragment、ToolsBindings 加成员、Description 双语资源成对，走编程面契约（resolveInside、写前 FILE_CHANGED、durable 单笔记账）；park/unpark/abandon/reap 各记 durable 审计事件（只记痕迹不序列化现场）。
  - 主要风险：rm 不可逆、EXDEV 中间态是否收紧（FileMutationTools.fs:125-137 现为 cpSync 成功而 rmSync 失败时两侧并存不回滚）、durable 事件字段形态与既有 js-tools/transactions 流的关系。
  - 事实支持：S1 目标 15/16 已锁；宿主 mv/rm 无保护（FileTools.fs 直通 NodeFs、无 FILE_CHANGED、无 durable 记账）是既有差距而非应然；命名撞名核查确认 Repository/Programming 下 park/unpark/abandon/reap 零命中，SDK 成员命名无冲突。

**已淘汰路径（各留一行理由）**：
- 决定性重放（S1 取舍 1）：把「可决定性重放」的终身税强加给每个程序作者（Math.random/Date/闭包状态皆可致恢复失败），违背黑盒目标。
- generator 驱动 / CPS 编译（S1 取舍 1）：绑架程序写法、自造解释器。
- 同步阻塞（S1 取舍 1）：单线程事件循环死锁。
- 回调式 on_abandon(fn)（S1 取舍 3）：新 API、宿主侧回调调用栈环境诡异、表达力不如原生栈展开。
- 程序内恢复成员（S1 取舍 4）：黑盒视角里恢复是 AI 的动作不是程序的动作，SDK 成员形态会把恢复逻辑混进程序文本。
- 现场 durable 序列化（S1 取舍 6）：活栈不可序列化是物理边界，重启即重置与 subagent 同命。
- 完全复制宿主 mv/rm 现状（S1 取舍 8）：无边界无记账是既有差距而非应然，长期方向下保护面统一到编程面一侧。

### 路径

**主路径：分层改造——as-is 写语义先行，JsSandbox 现场可持有化，ToolWorkflow 模块级现场表与竞速挂起，编排层三态入参，ABANDONED 原生栈展开，js.rm/js.mv 独立线并行。**

路径 A 的骨架 + 路径 C/D/E/F 的部件。理由：as-is（步骤 1）是纯语义翻转、独立于暂停-恢复，先做可让现场表字段形态一次定型（B.8「as-is 下无 staging」）；JsSandbox 提升（步骤 2）是现场保活的结构性前提；现场表与竞速（步骤 3/4）在其上叠加；三态入参与 ABANDONED（步骤 5/6）是编排层收尾；门禁、durable、rm/mv（步骤 7/8/9）是外围线。

**分解（九步）**：

1. as-is 写语义翻转（Transaction.fs + ToolWorkflow.fs）：去统一 staging 结算，每笔 mutation 写前 FILE_CHANGED 校验（读当前盘比对 readSnapshots.Text），失败返回附「本次调用已发生的改动」清单；同步修订 WHAT [012]/[014]/[016]/[027] 条款。
2. JsSandbox 现场可持有化（JsSandbox.fs）：runSurface 签名改造，把 programPromise 暴露给 runCore；deadline 从内联常量（47-48）改为 api 闭包捕获的可重置引用（Proxy 检查时读引用当前值）；vm context 不显式提升——由 programPromise 闭包自然持有（反事实检验见下）。
3. 模块级现场表（ToolWorkflow.fs）：JsParkedSite { handle, programPromise, api, readSnapshots, deadlineRef }，handle 生成与登记、会话归属标记、并发现场上限（S2 裁决：默认 8）。
4. 竞速挂起（ToolWorkflow.fs）：runCore 同时 await programPromise 与暂停信号；park 绑定触发暂停信号；挂起时登记现场表，该次工具调用返回顶级 parked 载荷（handle + payload）。
5. 编排层三态入参：{ program } | { unpark: { handle, value } } | { abandon: { handle } }；unpark 查表 → resolve deferred 注入 value → 继续竞速，deadline 重置 now+10 秒；一次性恢复，重复报 HANDLE_UNKNOWN。恢复后程序若再次 park，该次恢复调用返回顶级 parked 载荷（新 handle），语义与首次 park 一致——恢复调用的返回形态是「恢复后程序继续执行的结果」，若再次挂起则该结果即新 parked 载荷（裁决 S1 事实 15「定稿未锁死」的设计点）。
6. ABANDONED 协议：abandon/reap 时 reject __jsAbandoned 哨兵 → 栈从 await 处展开 → 程序 try/finally 收拾资源；清理期 api as-is 生效、再 park 报 CANNOT_PARK_WHILE_ABANDONING；展开窗口 = 重置后 10 秒；catch ABANDONED 后继续跑返回值丢弃，结局仍记 Abandoned。
7. 泄漏兜底：交付硬门禁（deliver 时现场非空未表态 → 拒绝并说明）+ 每回合提醒（并入 PairHint 通道，列出全部 parked handle）+ 会话终结强制 reap（durable 记 Reaped）。现场年龄上限自动 reap（如 24 小时）为 S1 B.14 待定项、用户未拍板，不纳入本次主路径，仅保留会话终结强制 reap 与并发现场上限（默认 8）。
8. durable 记账与审计事件：
   - mutation 记账（as-is 单笔）：js-tools/transactions 流载荷从「多笔 mutations 数组」改为单笔——每笔写盘前记 Prepared {transactionId, workspaceRoot, path, newText}，写盘后记 Committed {transactionId}，取消事务级多笔聚合（裁决 S1 B.16「记账粒度跟到单笔 mutation」）；uncommitted Prepared 仍仅作中断审计证据，重启不自动重放/回滚（沿用 S1 事实 9 语义）。
   - 审计事件：park/unpark/abandon/reap 各记事件（Parked/Unparked/Abandoned/Reaped），只记审计痕迹不序列化现场；与 mutation 记账流并列或同流（S3 定）。
9. js.rm/js.mv 独立线：JsFragmentRegistry 加 rm/mv fragment、ToolsBindings 加成员、Description 与双语资源成对、ToolWorkflow 处理；EXDEV 裁决（倾向收紧为报错不留中间态）；测试 028 起增量编号。

**边界**：
- 步骤 1 只动 Transaction.fs/ToolWorkflow.fs 事务链与 WHAT 条款文字，不碰 JsSandbox 内部、不碰 Description 资源。
- 步骤 2 只动 JsSandbox.fs 的 runSurface 签名与 wrapProgram 生成代码、deadline 注入方式，不碰 ToolWorkflow 编排、不碰渲染协议。
- 步骤 3/4/5/6 只动 ToolWorkflow 编排层与 JsToolOutcome 渲染分支，不碰 JsSandbox 内部、不碰 Description 资源。
- 步骤 7 只动交付门禁（Manager 侧）与 PairHint 通道（PairProgrammingThoughtTransform.fs），不碰程序语义。
- 步骤 8 只动 durable 事件形态（EventStore 流），不碰现场表内存形态。
- 步骤 9 只动四层注册与 Description 资源，不碰既有六成员语义。

**顺序**：
- 串行：1 → 2 → 3 → 4 → 5 → 6。依赖：4 的现场表字段形态依赖 1 的 as-is（无 staging）与 2 的现场可持有对象；3 的现场表是 4 的登记目标；5 依赖 4 产生 parked 现场；6 依赖 5 的三态入参。
- 并行：7（门禁与提醒）在 4 产生 parked 现场后即可开发，与 5/6 并行；8（durable 事件）依赖 4 的 park 事件产生，与 5/6 并行；9（js.rm/js.mv）完全独立，与 1-6 全程并行。
- 测试：028 起随步骤推进增量编号，每步闭环（as-is 语义测试、现场表测试、三态收尾测试、finalizer 测试、rm/mv 测试、durable 审计测试）。

### 检验与反思

**反事实检验**：

- 步骤 2 失败（vm context 跨调用保活不可行）：反事实揭示正确实现不是「显式提升 context 引用」，而是只提升 programPromise——Node 事件循环里 programPromise 未 settle 时，其闭包链自然持有 vm context，现场表只需持有 programPromise 即可让 context 存活；显式持有 context 反而引入 GC 与语义边界问题。若 programPromise 也无法跨调用持有（Fable 桥接失败），则降级为「现场表持有 context + 重建 programPromise 包装」，但此路违背黑盒（程序状态在 context 内、重建包装会丢闭包），不可取——该假设被证伪时步骤 2 整体作废，需回到模型层重新裁决。
- 步骤 1 失败（staging 无法完全取消）：S1 目标 A.3 已锁「单笔 mutation 原子性保留（MutationFs 文件层）」——as-is 与单笔原子性不冲突，冲突的是「多笔统一结算」。若实现发现某路径仍依赖统一 preflight（如 durable AppendPrepared 链），则该路径改为「每笔写前各自校验、各自记账」，步骤 1 的「已发生改动清单」由现场表已发生列表派生，不推翻 as-is 目标。
- 步骤 4 失败（runCore 竞速挂起不可行）：若 F# task 无法与暂停信号竞速，降级为「wrapProgram 生成代码内用 Promise.race（programPromise vs 暂停信号），F# 侧只 await 一个结果」——竞速下沉到 JS 侧，F# 侧语义不变。此降级不推翻黑盒（程序仍零感知），只改竞速实现位置。
- 步骤 6 失败（ABANDONED rejection 跨 vm 边界传播不可行）：Python GeneratorExit 先例说明挂起点抛异常可行，但 Node vm 的 rejection 传播路径需 S3 伪代码验证；若 reject 无法从宿主侧注入程序 await 处，降级为「api 层注入 abandon 检查点（每次 api 调用时检查现场状态，abandon 后抛哨兵）」，但清理期不调 api 会卡死——此降级有缺陷，需在 S3 验证原生 reject 路径，验证失败则回到模型层。
- deadline 可重置引用假设（S1 B.8）：若 wrapProgram 内联常量无法改为引用，替代是「deadlineRef 挂在 api 闭包捕获的可变对象上，Proxy get 时读当前值」——这与「可重置引用」等价，只是实现位置从生成代码移到 api 闭包；该假设被证伪不影响主路径结构，只改步骤 2 的实现细节。

**是否解决 S1 形式化目标（逐条对照）**：
- A.1（成功即落盘）→ 步骤 1；A.2（终止/挂起是事实分界线）→ 步骤 1 + 4；A.3（单笔原子保留）→ 步骤 1（MutationFs 层）；A.4（失败附已发生清单）→ 步骤 1；A.5（共享写语义同步 as-is）→ 步骤 1（[027] 修订）。
- B.7（park 黑盒零感知）→ 步骤 4/5；B.8（现场表登记与竞速）→ 步骤 2/3/4；B.9（三态入参、不建新沙箱）→ 步骤 5；B.10（一次性恢复、HANDLE_UNKNOWN）→ 步骤 5；B.11（三态收尾）→ 步骤 5/6；B.12（finalizer 原生 try/finally）→ 步骤 6；B.13（泄漏兜底三件套）→ 步骤 7；B.14（现场资源约束）→ 步骤 3（并发现场上限）；B.15（rm/mv）→ 步骤 9；B.16（durable 审计）→ 步骤 8；B.17（四层同构）→ 步骤 9 + 各步。
- C（非目标：废弃直接工具）→ 全程不触碰，保持兼容。
- 结论：主路径每一步直接对应 S1 目标形式化的一条可判定状态，不是只解决表象——as-is 翻转解决 A 组全部，暂停-恢复解决 B 组全部，外围线（门禁/durable/rm/mv）解决 B.13/15/16，无一步是装饰性动作。
## S3 · 流程与伪代码

（本段由 S2 晋级后第一任写入。载体为 Knuth 风格伪代码，写给执行者读；每个算法自足，裁决点有结论，失败路径有去处。）

### 算法 A · as-is 写语义翻转

Algorithm A：把编程面写语义从统一 staging 结算翻转为 as-is 逐笔落盘。
输入：一次工具调用收到的 mutation 序列（edit/rewrite/write 各成员已定位为单笔或数组），当前工作树，readSnapshots（本调用内已读文件快照）。
输出：逐笔落盘后的工作树；成功返回 # ok，失败返回 # failed + code/reason + 本次已发生改动清单。

A1. [逐笔遍历] 对 mutation 序列中的每一笔 m，按序执行 A2 至 A5。
A2. [写前校验] 若 m 的目标路径 p 在 readSnapshots 中，则读取当前盘上 p 的字节，与快照比对；若不一致，转 A6（FILE_CHANGED）。
   ▷ 校验点移到每次写前，不再有统一 preflight。结论：单笔写前校验，失败即停，已发生改动保留。
A3. [单笔原子写] 经 MutationFs 对 p 执行原子写盘（临时文件 + rename）；写盘失败转 A6（保留已发生清单）。
A4. [记账] 写盘成功后，向 js-tools/transactions 流追加 Committed { transactionId }（Prepared 已在写盘前追加，见算法 H）。
A5. [记录已发生] 将 p 及其 newText 追加进本调用「已发生改动」列表。
A6. [失败返回] 返回 # failed，附 code（FILE_CHANGED 或写盘错误码）与「本次调用已发生改动」清单；算法终止。
A7. [成功返回] 全部 mutation 逐笔落盘后，返回 # ok + [fs] 改动清单；算法终止。
# 断言：A2 失败时，A5 已记录的改动全部真实在盘上；A6 不回滚任何已落盘改动。
▷ 单笔 mutation 原子性（MutationFs 文件层）保留，as-is 只取消多笔统一结算。结论：单笔原子写不变，跨笔不再回滚。
▷ 失败返回附已发生清单（S1 A.4）。结论：清单由 A5 列表派生，不额外查询。

### 算法 B · JsSandbox 现场可持有化

Algorithm B：改造 JsSandbox，使程序 Promise 与可重置 deadline 引用可被 runCore 跨调用持有。
输入：现有 runSurface（createContext + runInContext 包成 async IIFE 一次性 await）。
输出：改造后的 runSurface 签名，暴露 programPromise；deadline 改为 api 闭包捕获的可重置引用。

B1. [暴露 programPromise] 改 runSurface 签名，使 async IIFE 的返回 Promise 不再在 run 内 await 到底，而是作为 programPromise 返回给调用方（runCore）。
B2. [deadline 可重置] 把 deadlineEpochMs 从 wrapProgram 生成代码的内联常量改为 api 闭包捕获的可变对象 deadlineRef（Proxy get 时读 deadlineRef.current）。
   ▷ 内联常量不可重置，必须改引用。结论：deadlineRef 挂在 api 闭包捕获的可变对象上，Proxy 检查时读当前值（与「可重置引用」等价）。
B3. [context 存活] 不显式提升 vm context；由 programPromise 未 settle 时闭包链自然持有 context。
   ▷ 反事实：显式持有 context 引入 GC 与语义边界问题。结论：只提升 programPromise，context 由闭包自然持有。
B4. [返回] 返回 { programPromise, api }；算法终止。
# 断言：B3 下 programPromise 未 settle 时 context 必存活；若 Fable 桥接失败导致 programPromise 无法跨调用持有，回到模型层重新裁决（S2 反事实）。

### 算法 C · 模块级现场表

Algorithm C：在 ToolWorkflow 建立模块级现场表，登记挂起现场并施加并发上限。
输入：park 触发时由 runCore 提供的现场对象 { programPromise, api, readSnapshots, deadlineRef }。
输出：登记后的现场条目 { handle, site }；或并发已满的拒绝。

C1. [查并发] 若现场表现有条目数 ≥ 上限（默认 8），转 C5（CONCURRENCY_LIMIT）。
   ▷ 并发现场上限默认 8（S2 裁决）。结论：上限 8，超出拒绝，不排队。
C2. [生成 handle] handle ← 单调递增计数（进程内唯一）；登记 session 归属标记。
C3. [登记] 现场表 ← handle → JsParkedSite { programPromise, api, readSnapshots, deadlineRef, sessionId, state: Parked }。
C4. [返回] 返回 { handle, site }；算法终止。
C5. [拒绝] 返回并发已满错误；算法终止。
# 断言：现场表是进程内活物，重启即重置（不落 durable）；handle 一次性消费，不留 tombstone（算法 E）。

### 算法 D · 竞速挂起

Algorithm D：runCore 同时 await 程序 Promise 与暂停信号，挂起时登记现场并返回顶级 parked 载荷。
输入：programPromise（算法 B），api，暂停信号源（park 绑定）。
输出：程序终值（Done）或 parked 载荷 { handle, payload }（挂起）。

D1. [竞速] 同时 await programPromise 与暂停信号；谁先 settle 谁胜出。
D2. [程序先完成] 若 programPromise 先 settle：返回其终值；算法终止（Done，有终值）。
D3. [暂停先触发] 若暂停信号先触发（程序在 await js.park(payload) 处挂起）：登记现场（转算法 C）；该次工具调用返回顶级 parked 载荷 { handle, payload }；算法终止。
   ▷ 竞速实现位置：若 F# task 无法与暂停信号竞速，降级为「wrapProgram 生成代码内用 Promise.race，F# 侧只 await 一个结果」（S2 反事实）。结论：优先 F# 竞速，失败则竞速下沉 JS 侧，语义不变。
# 断言：D2 与 D3 是事实分界线——程序先完成则改动已全部落盘（as-is），暂停则已发生改动保留在盘上，恢复后继续。

### 算法 E · 编排层三态入参与恢复

Algorithm E：编排层接收三态入参，执行恢复或放弃。
输入：{ program } | { unpark: { handle, value } } | { abandon: { handle } }。
输出：恢复后程序继续执行的结果（终值或再次 parked 载荷）；放弃则进入 ABANDONED 协议（算法 F）。

E1. [分派] 按入参形态分派：{ program } 转 E2；{ unpark } 转 E3；{ abandon } 转算法 F。
E2. [新程序] 按算法 B/C/D 执行新程序；算法终止。
E3. [查现场] 按 handle 查现场表；若不存在，转 E6（HANDLE_UNKNOWN）。
E4. [注入] resolve 该现场 deferred，注入 value；从现场表移除 handle（一次性恢复，不留 tombstone）。
E5. [继续竞速] 重置 deadline 为 now+10 秒；继续竞速（算法 D 的 D1 语义）；若再次 park，返回新 parked 载荷 { newHandle, payload }；若程序跑完，返回终值（Done）；算法终止。
E6. [未知句柄] 返回 HANDLE_UNKNOWN；算法终止。
   ▷ 恢复后再次 park 的返回形态：恢复调用返回「恢复后程序继续执行的结果」，若再次挂起则该结果即新 parked 载荷（S2 裁决，闭合 S1 事实 15 设计点）。结论：恢复调用返回终值或新 parked 载荷，语义与首次 park 一致。
# 断言：E4 后 handle 已消费，重复恢复必报 HANDLE_UNKNOWN；历史经 durable 审计可查。

### 算法 F · ABANDONED 协议（finalizer 展开）

Algorithm F：abandon/reap 时以 ABANDONED rejection 展开程序栈，让原生 try/finally 收拾资源。
输入：{ abandon: { handle } } 或会话终结 reap 的 handle。
输出：Abandoned 收尾（无终值）；清理期 api 照常 as-is 生效。

F1. [定位现场] 按 handle 查现场表；若不存在，返回 HANDLE_UNKNOWN（reap 时静默）；算法终止。
F2. [注入哨兵] 对现场 programPromise 的挂起点 reject __jsAbandoned 哨兵；重置 deadline 为 now+10 秒（展开窗口）。
F3. [栈展开] 程序栈从 await 处展开；原生 try/finally 的 finally 块执行资源收尾；清理期 api 照常 as-is 生效。
F4. [清理期再 park] 若清理期程序再次 await js.park，抛 CANNOT_PARK_WHILE_ABANDONING；算法终止。
F5. [窗口超时] 若 10 秒展开窗口耗尽而程序未 settle，强制截断该现场；算法终止。
F6. [收尾] 结局记 Abandoned（无终值）；catch ABANDONED 后继续跑则返回值丢弃，结局仍记 Abandoned；durable 记 Abandoned 事件；从现场表移除；算法终止。
   ▷ ABANDONED 跨 vm 边界传播：优先原生 reject 注入 await 处；若不可行，降级为「api 层注入 abandon 检查点」，但清理期不调 api 会卡死——此降级有缺陷，S3 验证原生 reject 路径，验证失败回到模型层。结论：先验证原生 reject，失败则回模型层，不采用有缺陷的 api 检查点。
# 断言：不存在无声无息的结束方式——abandon/reap 必走 F3-F6 展开，结局记 Abandoned。

### 算法 G · 泄漏兜底（交付门禁 + 每回合提醒 + 会话终结 reap）

Algorithm G：三件套保证没有无声无息的结束。
输入：交付请求、回合提醒通道、会话终结信号。
输出：交付放行/拒绝；提醒注入；会话终结强制 reap。

G1. [交付门禁] 收到 deliver 时，若现场表存在 state=Parked 且未表态的条目，拒绝交付并列出全部 handle；表态（unpark/abandon）后放行。
   ▷ 门禁只逼表态，不替 AI 做决定。结论：拒绝直到全部 Parked 现场被 unpark 或 abandon。
G2. [每回合提醒] 并入既有耗时提醒通道（PairHint），列出全部 parked handle；无载体回合不注入。
G3. [会话终结 reap] 会话关闭时，对该 session 名下全部 Parked 现场执行算法 F（reap）；durable 记 Reaped。
   ▷ 现场年龄上限自动 reap（如 24 小时）用户未拍板，不纳入主路径（S2 裁决）。结论：仅保留会话终结强制 reap 与并发现场上限，年龄自动 reap 待用户裁决。
# 断言：三件套任一单独不完整，合起来保证无无声结束；G1 是交付级硬门禁。

### 算法 H · durable 记账与审计事件

Algorithm H：mutation 单笔记账 + park/unpark/abandon/reap 审计事件。
输入：单笔 mutation 写盘事件、park/unpark/abandon/reap 生命周期事件。
输出：js-tools/transactions 流单笔账目；审计事件流。

H1. [单笔 Prepared] 每笔 mutation 写盘前，向 js-tools/transactions 追加 Prepared { transactionId, workspaceRoot, path, newText }。
H2. [单笔 Committed] 写盘成功后追加 Committed { transactionId }。
H3. [审计事件] park/unpark/abandon/reap 各记 Parked/Unparked/Abandoned/Reaped 事件（只记审计痕迹，不序列化现场）。
   ▷ 审计事件与 mutation 记账流并列还是同流（S2 步骤 8 裁决点）：结论——并列独立审计流。理由：mutation 记账（js-tools/transactions）是写盘账目，语义是「每笔写盘有账」，参与中断审计与重放判定；park/unpark/abandon/reap 是生命周期审计痕迹，只记「发生过」，不参与写盘重放。两者混流会让单一账目同时承载「写盘账」与「生命周期痕迹」两种异质语义，破坏 mutation 记账的可重放纯净性，且按 handle/类型查询审计历史时需跨类型过滤。并列后：mutation 记账流保持单笔 Prepared/Committed 纯净账目，审计事件流按类型追加，互不污染。
   ▷ 审计事件字段形态（并列流的记录格式）：结论——每条审计事件记 { eventType: Parked|Unparked|Abandoned|Reaped, handle, sessionId, at }；不序列化现场、不携带 payload/value（只记审计痕迹）。理由：现场是进程内活物不落账（S1 B.16），审计事件只需可查「某 handle 何时被 park/unpark/abandon/reap」，payload 属程序数据不属审计痕迹。
H4. [中断语义] uncommitted Prepared 仅作中断审计证据，重启不自动重放/回滚/补交；算法终止。
   ▷ 记账粒度跟到单笔（S1 B.16 裁决）。结论：取消事务级多笔聚合，Prepared/Committed 各为单笔。
# 断言：现场是进程内活物不落账；durable 只记审计痕迹，账本哲学不受损。

### 算法 I · js.rm / js.mv 独立线

Algorithm I：新增 js.rm 与 js.mv 成员，走编程面契约。
输入：js.rm(path) 或 js.mv(from, to)。
输出：删除/移动后的工作树；成功返回 # ok + FILE_CHANGED，失败返回 # failed + code。

I1. [注册] JsFragmentRegistry 加 rm/mv fragment；ToolsBindings 加成员；Description 与双语资源成对新增。
I2. [rm] js.rm(path)：resolveInside 校验路径边界；写前 FILE_CHANGED 校验（若在 readSnapshots 中）；若为目录，先判空（非空拒绝 directory-not-empty）；删除文件/空目录；durable 单笔记账；返回 # ok + FILE_CHANGED。
I3. [mv] js.mv(from, to)：resolveInside 校验两路径；写前 FILE_CHANGED 校验；同设备 renameSync（原子）；跨设备走 crossDeviceMove。
I4. [EXDEV] 若跨设备移动且 copy 成功而删除源失败（两侧并存）：▷ 裁决收紧为报错不留中间态。结论：js.mv 收紧为 EXDEV 报错并回滚目标侧，不留两侧并存中间态（与宿主工具现状的既有差距区分）。
I5. [失败] 源缺失报错；写盘/移动失败返回 # failed + code；算法终止。
# 断言：js.rm/js.mv 走编程面契约（resolveInside、写前校验、durable 单笔记账），与宿主 mv/rm 的无保护现状区分。
