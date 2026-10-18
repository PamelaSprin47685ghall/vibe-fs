# CUT-02-R6-D：原生终结事实的原型与施工计划

2026-10-19状态：D1与D2声明范围已完成生产接线、非作者复审及固定663验收，随本主线提交交付。结果及原始证据分别见[D1报告](PROCESS-TERMINATION-D1-2026-10-18.md)、[D2报告](PROCESS-BINARY-D2-2026-10-18.md)。D2按pack不可读与missing不可区分的真实反例收紧对象/tree读取，不保留None；R12-U1、完整scope/PTY结算及平台容量不随D关闭。

以下正文保留原型阶段的输入、反例和计划；“当前”“尚未”均指当时，不代表上述交付状态。历史输入为主仓`5bf2767b9`、库`df14f48 / 0.6.8`、固定生产包`4d594fd`：当时正常exit143与SIGTERM仍都返回143，原型本身不算生产修复。本包继续不追查旧自然Unknown。

## 已确认问题及职责

NodeProcessHost把正常退出和本机signal归一成一个整数，旧0d60和当前4d594fd精确包均有实物反例。Wait用-1代缺回执、Runner的timeout/cancel丢终结信息、GitGateway/OrchestratorGit将Process错误变1及Spool变空stdout，是本轮源码核查；新原型证明了新表示可以承载这些区别，没有将它们冒称旧新双包生产回归实测。这些旧协议路径不归因于预算或取消修复。

ProgramEnd只确认目标进程的原生退出；不能单独证明监管范围结束、PTY义务清账或输出完整。这些仍分别由supervision、ownership和extent/capture拥有，D1不复制其状态机。已知目标exit23不能被监管者后来的SIGKILL覆盖；监管者死了却没有目标回执，也不能借其signal补目标事实。

## 拟接生产的契约

下列是待接线的目标，不表示已发布API。直接替换旧结果，不保留整数兼容层。

```fsharp
type ProgramEnd = Exited of int | Signaled of string
type EndEvidence = Confirmed of ProgramEnd | Unconfirmed
type StopRequest = CancellationRequested | DeadlineStopRequested of TimeSpan
type CapturedOutput =
    | InMemory of stdout: string * stderr: string
    | Spooled of path: string * bytes: int64 * chunks: int
type NoExecution =
    | InvalidRequest of string
    | CancelledBeforeStart
    | StartRejected of string
type ProcessObservation =
    { End: EndEvidence
      StopRequested: StopRequest option
      Output: CapturedOutput option
      Fault: string option }
type ProcessResult = NotStarted of NoExecution | Observed of ProcessObservation
type RunProcess = Command -> ProcessEstimate -> ProcessContext -> CancellationToken -> Task<ProcessResult>
```

NotStarted必须有确切零执行依据：预取消、排队取消或物理口确认未调用目标启动。普通starter异常、Started后校验失败和丢回执不能自动归NotStarted；须保留Observed、Unconfirmed、已知stop/输出和Fault，不能重发。字段矛盾或非法时拒绝的是回执解释，不是宣称此前没有执行。

stop是请求事实，不生成Exited/Signaled，也不把“已请求停止”写成“已停止”。运行中取消可立即返回Unconfirmed及CancellationRequested；deadline的kill-ack若取得真实目标终结则保留，未取得则仍未确认。完成后迟到Abort不改写结果。Output=None是没有交付完整输出；空输出须为Some(InMemory("", ""))。Fault可与确切End并存，不因采集失败丢掉已知退出。Some输出的完整性须由现有extent/排空边界授权，纯record本身不提供coverage证明。

原生输入拒绝code/signal同时非空、非整数/负值/越界code和非本机signal；两者皆无时保留Unconfirmed，若声称已关闭则另记缺回执Fault。原型按当前库的int32上界校验，不扩充Windows承诺；本轮物理信号仅覆盖SIGINT、SIGTERM、SIGKILL，STOP/CONT不是终结证据。

## D1与D2的实际编辑面

| 工作包 | 接线顺序与必须删除的旧路径 | 交付条件 |
|---|---|---|
| D1：单次执行及文本消费者 | ProcessRequest纯契约→NodeProcessHost/Wait→Runner/Output/Surface→Executor及双语最低wire预算→GitAdapter/Gateway→Git.Operations/WorktreeResource/Change.Surface与Host/Hook.Sync。删除128+signal、-1未知码、Completed中无用spooled bool、旧整数测试口及错误转1 | 对应Process、Git、Change、hook及预算套件、独立Fable正反编译、真实调用者、Node/Bun精确包和非作者复审完成 |
| D2：独立二进制GitRawRunner | ProcessGitRawStore的execFile回执、GitRawRunner的byte[]结果和所有存储调用者。复用ProgramEnd词汇，保留binary stdin/stdout，不借文本Runner包装；删除缺status取1和未知读失败当不存在 | 真实binary对象往返、已知缺对象/缺ref正控、signal/ENOENT/超时/缺回执拒绝、读写失败与COMMIT前后回执、冷读和精确包验收 |

D1先固定公共契约，D2再接；D1完成不宣称所有Git原生入口已经修复。D1中ChildProcess的目标证据须来自现有库的真实观察，不能只在scope Closed后才取得，或再造目标/监管两套业务账。自然完成仍须等现有输出/监管义务满足；早到的目标exit不提前清组。取消、kill-ack及输出失败路径都保留最近已取得的目标证据。

Git的目标接口为`Command -> Task<Result<GitExited, GitRunFailure>>`，Gateway只将入参绑定为argv。GitExited含真实ExitCode/Stdout/Stderr；GitRunFailure区分NotExecuted和Failed(EndEvidence, StopRequest option, reason)。仅确切Exited、无stop/fault且完整所需输出可用时返回Ok；真实exit1仍可由grep等业务解释。signal、Unknown、停止请求和采集错误走Error，不能再变1。Spool须完整取得消费者需要的输出或明确拒绝并释放资源，不能用空串冒充。

上层可在最终业务呈现边界生成文案，但不能把未知执行当普通Git负码后继续命令。尤其Gateway.resolvePushOutcome目前把所有错误走重试：D1须区分确切Git拒绝与执行未确认，后者停止后续写入；如需对账，只做绑定确切目标ref/oid的只读确认，不因一次当前ref读数就认定旧物理调用已经结束或可重发。Change的ref-moved诊断也只解释确切正常非零退出，不从signal/未知失败日志猜CAS冲突。

同样禁止把typed Error改成false/None后继续写。当前GitAdapter.hasRebaseHead将rev-parse失败折成false，后续可走commit；Git.Operations.checkedOutBranch把任意失败当detached。D1须使查询错误到达分支裁决，只有确切正常退出及对应业务语义才可解释“无rebase/无branch”。验收加入：状态探测被SIGTERM、Unconfirmed或输出失败打断时，零后续commit、rebase及ref写；合法无状态/dirty/exit1等正控仍成立。这是现有调用者迁移的一部分，不另造查询兜底。

该查询分支已补固定4d594fd真实HostSurface消费者对照：控制命令口令rev-parse返回128或143，其余步骤给正常结果，Node/Bun均继续update-ref及commit并返回Ok。`/private/tmp/process-git-query-consumer-{node,bun}.log`与同名probe.mjs保留调用序列。这证明现行消费者在查询失败后仍选择写分支；命令口受控，没有真实Git写入，不冒称完整物理工作流或双包归因反例。该补充不在上述原型26session总计内。

## 本轮原型取得的证据

原型源码及日志在`/private/tmp/process-termination-prototype/`，业务源仅Protocol.fs，以现有Node owner-compile入口调用Fable，没有运行dotnet build。原型是上述契约的窄子集：内存输出、字符串拒绝原因、Git事实投影`Result<int, GitFailure>`；尚无生产Spool、NoExecution完整分类、业务调用者或wire新格式。

最终纯编译正例通过；三个负例分别因Unknown不能作ProgramEnd、signal不能作int、无物理Host命名空间而拒绝。compile脚本核对具体文件及FSHARP diagnostic，不以任意编译失败当负控成立。`compile-frozen.log`及各stdout保留原因。

| 最终物理原型，两种Host分别运行 | 实际观察及限制 |
|---|---|
| Node/Bun目标各9种终结 | 0/1/7/137/143/255及SIGINT/TERM/KILL，共18个真实session；原生事实与纯投影逐项一致 |
| 真实Git两例 | grep无匹配为Exited1；保持stdin开放的hash-object被SIGTERM为Signaled，不投成1。两例均检查实际capture failure为空 |
| 两种停止请求 | 自有真实AbortController监听及100ms timer触发SIGTERM；请求与终结分别表示，补到回执后Git仍为Interrupted。不是生产Runner触发链的验收；未确认报告使用停止前最后一次原生观察，避免假定signal后回执一定尚未到达 |
| 目标先关闭两路FD | ready证明目标已closeSync两路且仍存活，End仍未确认，之后实际exit7；未另证明接收方已观察到EOF |
| 目标先exit23，再收束范围两例 | 公开inspect取得确切23，之后请求KILL范围，supervisor为SIGKILL、目标仍为23；cancel/deadline是原型请求标签，不是生产错误路径验收 |
| 监管者丢失 | 只杀本次已知supervisor，目标无回执、supervisor SIGKILL，保留Unconfirmed+Fault；目标稍后不再执行也不补造退出回执 |

每Host26个手动Supervised session都结束了本次控制口并清理运行目录；监管者丢失不据此声称目标有确切退出回执。另各2次当前4d594fd生产run仍得[143,143]，明确反证尚未接入。计数是物理执行，不冒称标准requirement用例总数。

纯负控另含六种非法回执、MissingOutput、无Fault的MissingEnd、NotStarted、以及带stop和受控输出Fault仍保留Exited23及原错误；受控输出Fault不是物理I/O故障实验。根最终`node-frozen.log`、`bun-frozen.log`均exit0；非作者独立两Host完整复验同样通过，`review-node-physical.log`、`review-bun-physical.log`。其首次受限运行被ps EPERM打断，正确失败并保留`review-*-restricted.log`，没有把权限错误当目标消失。

源码、编译与原生日志已冻结为`/private/tmp/process-termination-prototype-evidence-20261018.tgz`，SHA-256 `56a49bc83ec49421852aa7738113a4fa21aa218117d2a777344e12c2ccad5fe8`。Protocol.fs摘要`1062bee5aee4ca4761080d2973a8546ef45a7ad9e77b1580459076569e64e7f9`，probe.mjs摘要`0e7a79740ac47d1f820d4ae18ade08ed90516a477de65feab7caf8f682fe7e26`；四份源/编译记录摘要见归档source-digests.txt，非作者前后核对相同。该归档是本机调查证据，不是可部署包；实际依赖的固定4d生产包及声明依赖来源仍按取消报告明示。

## 原型错误与未完边界

D2另补源码消费者实物反例：现有runtime owner的16个生产编译条目加Consumer.fs独立Fable编译，直接调用ProcessGitRawStore.createDefaultRunner。在同空目录、同参数的两次独立真实Git执行中，原生execFile为error.code=128且没有status，现有Runner却给1；Node/Bun均复现。该空目录不是Git仓库，不是缺ref正控；根因读取error.status再取默认1。生产源未改，没有作精确npm包消费，也没有以探针中的productUnchanged声明代替字节摘要验证。stderr只证实原文是包装说明的后缀，不称原样保留。

源码、编译、原始失败及最终node-final.log/bun-final.log在`/private/tmp/process-git-raw-probe/`，归档`/private/tmp/process-git-raw-probe-evidence-20261018.tgz`，SHA-256 `15bb0ffcfc9bda8faeabe8b8a6d4226adc2cc32cea36939cff6ff845cd95e10d`。首编误用模块名Factory和首次误要求包装stderr与原文全等分别保留；修正探针前提，关键128→1断言未改。review_process_a只读核对调用链、证据与限制，没有另跑或计额外通过数。signal、ENOENT、超时、失败时非空binary stdout及真实bytes往返仍待D2验证；这项不是D1原型26session的一部分，D2尚未修复。

初版TimeSpan.FromMilliseconds缺float注解，以及Fable不支持Double.IsFinite，均编译失败后修正；未以.NET编译替代。首轮Git grep绝对pathspec实际128，原生无库对照同样128/“outside the directory tree”，相对pathspec才为1；是本探针前提错误，不是native适配故障。默认Git错误曾写Execution was stopped，非作者指出失真，已改请求措辞并加断言。原始失败日志保留，不混成已通过数。

D1下一步按表接生产，再补内存/Spool、正式取消/期限、输出失败、真实Git工作流与失败查询零后续写、双语wire最小额度和精确包；D2另包。review_process_a核对方案/编辑面，review_guidance_r3独立复验原型；第一段证据归因和查询错误防错意见已纳入，两次review不叠加成额外物理通过数。新类型、原型或非作者方案评审均不关闭P1，也不替代生产消费者验收。本轮未发布registry或部署Host。
