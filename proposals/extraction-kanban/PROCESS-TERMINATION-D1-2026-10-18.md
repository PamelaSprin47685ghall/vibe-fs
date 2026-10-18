# D1：单次执行与文本消费者

状态：54c7a9266后发现的停止输出完整性回退已修正，随D2原子提交再次验收D1声明范围。输入主仓 `25651051f`、库 `df14f48 / 0.6.8`；另一轮文档提交原样保留。D2 二进制 GitRawRunner、PTY 持久清账、旧自然 Unknown 和 R12-U1 不在D1关闭范围。

## 交付后复审纠正

非作者在固定652包复现：目标已exit23，后代每次实际写出后记录私有计数，deadline强停时Bun已确认写出21168128字节，但Runner把21110784字节spool作为完整输出，少57344字节；Node本次计数相等，不冒称Node也实际丢字节。缺最终extent时scope/capture Closed并不足以授权完整性。此为D1新协议引入，旧timeout路径本会discard；原229项和固定包检查未覆盖该断言，不能用此前通过掩盖缺口。文本Runner现只在无StopRequest且无fault的自然收束后交付完整输出；D2同时保留partial bytes但Complete=false。原反例已封存于[D2证据](evidence/R6-D2/d2-stopped-capture-baseline-20261018.tgz)，660修正物理正反对照及与663主仓代码字节同一性分别见该目录host660、host663档。

另根侧长期replay脚本的仓库路径多退一级，属于本轮验收脚本错误，未影响先前实际运行的日志或固定包字节。修正后已真实重放12/12文件229pass/0fail/0TODO、owned accepted，原/tmp路径与长期脚本分开记账。

最终663再次以长期脚本取得229pass/0fail/0TODO、12/12、owned accepted；Native/Bun输入修正及所有证据见[D2报告](PROCESS-BINARY-D2-2026-10-18.md)。原652收据保留其历史输入与当时成绩，不代证后来修正；本轮追加收据记录在R6-D2，不覆盖旧档。

## 契约与施工范围

以[终结事实计划](PROCESS-TERMINATION-2026-10-18.md)为依据，ProcessRequest 已用 ProgramEnd、EndEvidence、StopRequest、CapturedOutput、NoExecution 和 ProcessResult 直接替换旧结果。TextProcessExit 是确切正常退出及完整分离文本；TextProcessRunner 的 Error 直接保留整个 ProcessResult，避免再复制一份故障词汇。纯契约没有依赖物理 Host。

Host 从现有 SupervisedProcess.inspect 取得目标退出，不取监管者退出；自然完成继续等待现有 scope/capture。启动握手前即交出可监管句柄。取消不等待真实退出；期限有界等收束。Runner 只在完整采集成立时交付输出，提前返回会丢弃本次 collector/spool，保留最近目标事实及首个故障。输出采集不再接收退出码。

Executor 的 wire 分别表示退出、signal、未知终结、停止请求、输出完整性和故障；诊断文本独立有界截断，不伪装成命令输出。未知启动不宣称 executed=true。双语 minimum 由完整 wire 计算，低于 minimum 在任何运行时效果前拒绝。文本 Git 拒绝混合通道 spool 并删除资源；不再返回空 stdout。状态探测或 stale-ref 删除失败阻止下一写入，Gateway 的未知发布不进入重试。Sweep 延迟启动后续删除；Resource 的同一次 Release 共享成功或失败回执，失败后不再删分支。

## 问题归因

| 分类 | 具体问题 | 本轮处理 |
|---|---|---|
| 原有问题 | signal 合成为128+signal，缺回执为-1，停止/采集故障丢失已知目标退出 | 新终结事实协议 |
| 原有问题 | Git 执行失败变1、spool变空stdout、失败状态查询当false、未知push重试 | 类型化消费，错误短路 |
| 原有问题 | Sweep 的 eager tail Task 提前删除；Resource 移除失败仍删分支且失败可重发 | 延迟 continuation；共享释放回执 |
| 原有问题 | 空query当无rebase，stale-ref正常非零也继续写 | 空query拒绝；删除必须exit0。真实缺ref删除本来就是0，不能把锁失败叫absence |
| 本轮引入 | 649对全部Observed写executed=true，未确认启动也被呈现为已执行 | 650双Host绿回验：省略executed、明确execution_unconfirmed，长诊断在exact minimum仍有界 |
| 本轮引入 | Gateway使用Spool却漏声明runtime工程依赖，聚合编译掩盖独立闭包失败 | 显式声明依赖；独立Fable正负消费者2/2通过，纯ProcessRequest仍隔离 |
| 夹具/验证环境 | 受限运行无法建立003旧PTY发布现场；quiet symbolic-ref改为exit1后仍期待旧fatal文案 | 受限日志保留并终止自有runner；有权限运行对应Process套件。更新正常detached断言，不改业务错误断言 |

## 已取得证据及边界

- 第一轮 Fable 编译发现旧 LargeGate launcher 接口及三处不支持的API/常量，修正后 build generation649 成功；174个Surface契约、839个模块链接通过。未运行 dotnet build。
- 固定649包 `/private/tmp/d1-candidate-649.NeduTT/wanxiangshu-0.9.0.tgz`，SHA-256 `6320dc440178f1e29d412340ebab7aa50792aa4313024c7d0902ca669b9fc2d6`。这是中间候选，不是最终修复包。
- 非作者 `/private/tmp/d1-independent-candidate-evidence-ikeMlu/summary.json`：Node/Bun各15次真实目标执行，包括6正常码、3signal、双语注册后的143/SIGTERM、目标先exit23后真实EACCES采集故障、丢启动回执并暂停监管者时取消。2076产品文件及归档摘要不变。普通声明依赖和smol只读借主仓，包内库为精确0.6.8；不是全npm隔离或外部OpenCode会话。
- 非作者 `/private/tmp/d1-independent-candidate-evidence-fEz0ly/summary.json`：同一649包两Host阻止worker真实启动，仍executed=true的特定红断言。该回退与原拆分问题分开记录。
- 非作者 `/private/tmp/d1-git-review-649.NtsMW5/`：每Host30组受控消费者观察，29组安全边界断言、1组空query仍继续写的未修现状。涵盖失败查询、merge不误判CAS、sweep、Release、4种未知push只调用一次及spool拒绝/删除；不是物理Git全流程。
- 已迁移Process的9个对应文件116项回归全绿；真实Git/Change首轮仅detached旧文案断言失败，夹具已改。新增回归和最终候选仍待完整验收。

650补充验收：精确包SHA-256 `ef26c6cbb4c39fcb85865abba6a01aa3de5da6d3c1063e205df18bfe1a1e60a6`。非作者主证据 `/private/tmp/d1-independent-candidate-evidence-3glmwT/summary.json`、Unknown绿回验 `87RbMf/summary.json`：Node/Bun各19次真实目标执行，保留上述15项并补两语言exact minimum的SIGVTALRM与真实后读EACCES；英文347、中文343字节，信号及UTF-8尾部恰好达到各minimum。已完整19000字节的spool由目标改为000，后读失败仍保留exit23、故障、未交付输出并删除spool。长NUL/Unicode故障诊断分别345/343字节。早期Bun的函数补丁未触发是探针前提失败，日志在MM45zO保留；最终采用真实OS权限错误。逐PID独立采样只覆盖手工控制的capture/startup目标、后代或监管者，其余自然case依生产scope/capture收束回执；不冒称所有case都另查过PID。

650文本Git的非作者证据在 `/private/tmp/d1-git-review-650.ywfUKG/`：各Host41组断言全绿，空query已拒绝，普通删ref非零停止写，正常拒绝push仍可重试，Unknown push即使期限已过也保留原错误且只调用一次。另各一次真实Git绑定仓库创建/释放，宿主cwd在另一仓仍只修改绑定仓；这条通过实际native Git端口，未走ProcessRunner监管。完整归档 `/private/tmp/d1-git-review-evidence-20261018.tgz`，SHA-256 `e40e4cfc6bc1b4e0f68d50a20f74f75cf606d42ab3a548022be62cb81d4b8f9b`。

根侧新增回归已运行15个对应文件：252项中249pass、0fail、3TODO。三个TODO分别是change004 GAP-215门内持久写边界，以及durable008 GAP-151插件安装/崩溃竞争范围，均为既有独立问题；不以套件exit0关闭它们。独立契约/Gateway/PTY消费者的2项integration均通过，包含Unknown push只调用一次且原错误不被期限覆盖，以及Unknown不能作ProgramEnd、signal不能作int的编译负例。随后按解析、观察、等待、释放边界整理函数；静态检查通过，pyramid为0，完整Fable generation651和174 Surface/839模块链接通过。整理中出现一次record字段推断歧义，补ProcessObservation类型标注修正；另一次构建遇到并行文档提交使generated inputs改变，正确拒绝，未计通过。

## 最终候选与四类证据

固定候选为 generation652 的 `wanxiangshu-0.9.0.tgz`，SHA-256 `003bc8818969597f9afd6a1c0767c7aae271dd53e5f0392b146367d156ec3887`。generation653 仅刷新并行文档变动后的生成输入，2076个产品文件与652逐字节相同。包内库 commit `df14f489d0a503ce300da6df6ef8f71910b477eb`、版本0.6.8，vendor摘要 `7e5873932a9ddd4519115f5575eda46f09c0f37d50ad4e97def05dbaba76022b`；库仓无改动。环境为 Darwin25.6.0 arm64、Node26.6.0、npm11.18.0、Bun1.3.14、Fable5.13.0。

长期证据位于 [evidence/R6-D1](evidence/R6-D1/receipt.json)。以下归档包含原始命令、状态、日志、清理回执及可携带探针；历史临时路径仅用于对照，不是唯一保存位置。

| 证据类 | 最终观察及范围 | 可复取记录 |
|---|---|---|
| 真实消费者 | Node/Bun分别19次真实目标，覆盖正常码、signal、注册工具、exact minimum、真实写入/后读权限错误、启动期取消；各41组Git边界与各一次绑定仓库真实创建/释放；实际OpenCode Host请求与冷恢复回归通过 | `independent-review.tgz`、`git-review.tgz`；根侧integration原日志 |
| 固定产物与类型 | 全新npm安装，原包2076文件不变；安装子树2904文件含828份正常嵌套zod依赖。相同F#程序按真实fsi编译，删除新编译生产实现后，用未改写的Consumer.js运行固定安装包，两Host通过。npm根export真实Plugin的013用例各3/3 | `independent-review.tgz`中的final/clean-installed、typed-contract、public-export-canary；锁SHA `29beb11dee4da50fe3d1eaac925f7114751a997d057cde8ef30170f61d3cba91` |
| 关键反例与回归 | 直接修改12文件正式监督229pass/0fail/0TODO，12/12完整结束且资源回收accepted；003的两个Wait用例另验2/2。独立Fable正负消费者2/2。扩展15文件249pass/0fail/3TODO。完整补充integration为80pass/0fail/2TODO、6/6结束、最终exit1 | `root-verification.tgz`；独立归档保留4d旧红、649本轮引入红和探针错误，不以新绿抹掉旧失败 |
| 旧职责退出 | 旧整数兼容、错转1、混合spool空文本、失败查询假无状态、未知push重发、提前及重复清理已退出；纯协议、物理观察、等待与真实消费者分别由下表唯一负责 | 本包源代码diff、独立闭包正负编译、最终固定包消费者 |

这里的fsi证明是内部F#契约，不是新增npm d.ts或子路径承诺。真实Git绑定工作流使用native Git端口，不冒称经过ProcessRunner监管。自然案例依生产scope/capture收束回执，只有手工控制的capture/startup场景另作逐PID观察。未覆盖Linux、Windows、部署或npm发布。

三个TODO在开工前`25651051f`已有独立计划：GAP-215是ref-only Gate与门内持久写入裁决，GAP-151是插件激活及完整CAS/替换崩溃矩阵。本次按冻结职责复核不属于D1；没有删改这些TODO。D1自身Unknown push零重发、正常拒绝重试等重叠义务已独立验收。integration的资源回收accepted不等于整套验收通过，其exit1原样保留；父D和完整WHAT不随D1关闭。

## 旧职责、数据与在途效果

| 旧文件/事实 | 新owner与退出依据 | 保留边界 |
|---|---|---|
| ProcessRequest旧Outcome/Error及整数码 | 纯ProgramEnd/EndEvidence/StopRequest/ProcessResult；独立负编译拒绝Unknown作终态、signal作int | 不拥有物理观测或持久结算 |
| NodeProcessHost的128+signal与Exit TCS | 原生inspect目标终结；scope/capture关闭另由Exited及回调表示，旧TCS删除 | 监管者退出不能代目标；库物理owner不变 |
| Wait的-1及Output的exit参数 | Wait只管停止原因，Output只管捕获；Runner组合最近事实 | 取消前台返回不宣称scope或输出完成 |
| Runner在故障时丢End、Executor整数wire | Runner保留事实，Executor明确未知/完整性/故障并有界呈现 | spool后读失败仍保留原End并删除本次spool |
| 文本Git三元组、Error转1、spool空stdout | TextProcessRunner只接受完整分路文本，失败保留ProcessResult；混合spool拒绝并删资源 | binary GitRawRunner仍由D2负责，不能靠本包代证 |
| Change状态查询的bool及stale-ref非零继续写 | Result错误短路，空query拒绝，delete-ref必须确切exit0 | 合法detached及Git grep无匹配仍为正常非零正控 |
| Gateway未知push重试、笼统expected当CAS | 执行失败直接返回；只有已确认拒绝才走有界重试，CAS诊断需完整锁比较事实 | 并发全矩阵仍归GAP-151 |
| Sweep eager tail、Resource失败重发/跨cwd删除 | continuation延迟后续任务；同一Release共享回执且只在移除成功后删分支；绑定仓库cwd | 不清理无归属的用户资源 |

D1不改变持久writer、PTY账及历史数据格式。切换运行代码前应让当前一次执行停止并收束；不能将旧内存回执跨版本拼装，不因结果未知重发命令。已有PTY持久监管、lease及接管仍由原owner持有；本包没有抹账、补造终态或把scope关闭提早到目标退出。binary存储的旧错误路径留待D2，保持可见。

## 验证失败与剩余动作

除了上表已经分开的原问题与本轮引入问题，本轮结构整理曾触发23处嵌套决策门禁及一次record字段推断错误；拆开解析、观察、等待、释放边界并标注ProcessObservation后，完整Fable及静态检查通过，pyramid为0。静态shard-edge-audit仍报告523条既有观察（high90/medium2/low431），该扫描为report-only，不宣称全架构无缺陷。

首轮混用unit静默预算跑长integration，13/15文件后被5秒窗口终止，没有权威总计；另一次因并行文档使生成输入过期而拒跑。改用现有integration预算后6/6结束，结果如实列于前节。受限003旧PTY现场未建立的原日志不当作产品回退，也未将整个003冒称通过。

交付提交前在最终文档输入上刷新构建，并确认2076个产品字节仍等于固定652；结果见证据收据。D2、GAP-151、GAP-215及R12-U1保持各自未完成状态；未部署、未发布。
