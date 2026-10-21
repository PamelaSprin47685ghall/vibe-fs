# MF01：按用途投影 Callable 结果的最窄合同

此文是只读设计。本轮没有运行测试、真实命令、构建，也没有改候选、主仓、metadata或index。依据是当前主仓的实际 F# 和现有 compiled JS；此前 B 独立验收的原始正反控另行引用，不作为本轮新运行。

结论：下一包应把 **next命令、watch裁决、onExit完成、sanity证明** 分开处理。原回调对象留在guest，按原用途解释成很小的被动DTO；Host继续持有物理执行、权限、期限与终局。不能把整份返回值交给通用 JSON/passive 转换。当前B只能接收为隔离RPC机制，不能直接替代生产this.call。

## 1. 真实调用链与依据

主仓入口合同见 `requirements/repository-programming/WHAT.md [050]/[051]/[058]`，实现集中于 `src/Wanxiangshu/Process/JsCallLoop.fs`：

| 边界 | 实际位置 |
| --- | --- |
| 动态方法查找、receiver、thunk/apply | 105–123 |
| Promise.resolve/await | 444；调用方raceDeadline |
| watch verdict | 607–619 |
| StepCommand解析 | 675–779 |
| 规格及首次实例 | 829–878 |
| watch驱动 | 1112–1131、1318–1341 |
| onExit等待及丢弃结果 | 1185–1202；1220–1221按HasOnExit决定调用 |
| 命令执行及拒绝 | 1416–1429 |
| next后取消/期限检查及step推进 | 1432–1460 |
| sanity、诊断、错误优先级及fresh实例 | 1739–1878 |

现主仓 `JsSandbox.fs:256–309` 是 `createContext({api,...})` 后直接 `runInContext(wrapProgram(...))`；回调返回的raw对象可以由JsCallLoop解释。**它没有使用B依赖Sandbox bundle的unwrap membrane。** 不应按“旧版先遍历全部own getter”推导迁移行为；原主仓只在这些解释器和最终展示边界读取相应字段。

F#源码看起来只写一次的表达式，Fable Emit可能重复求值。本设计同时核对了现有 `dist/Process/JsCallLoop.js`，尤其 `verdictOf`、`parsePortWait`、`isVeto`。这轮不重新编译，因此后续实施应冻结所对齐的实际产物版本，再用真实入口对比，不能只比一份手写镜像parser。

## 2. 端口形状：只增加用途，不增加反射框架

保留B已有run incarnation、exact offer/instance/thunk、commandId与终局门。Host不接收原class/instance/function/Proxy，不调用原getter、toJSON或toString。建议四种私有用途结果：

```text
NextStep = End | Run(command) | Spawn(name,command)
         | Signal(name,signal) | Send(name,input) | WaitFor(port,timeoutMs)
         | Unsupported
WatchDecision = Continue | Abort(reasonText) | Park(payloadRef或已展示payload)
ExitCompletion = Done
ProofVerdict = NextTerminated | WatchVetoed | Failed(diagnosisRef)
```

跨进程编码可以是固定tag及对应字段。字符串保持原字节内容，port/timeout为已验证整数；Host只校验DTO形状与其command/ref权威，映射到本地DU并调用现有执行函数，不再对DTO运行第二套raw语义parser。

不要给 `callMethod` 增加任意路径getter、属性枚举、远端Reflect或用户指定转换函数。guest只有固定的 `next/watch/onExit` 用途及专门proof操作；用途由Host可信调用点决定，不能让模型用一个字段把next伪装成onExit来跳过验证。raw结果槽必须绑定原run、instance、调用id和用途，单次消费、不可跨offer复用。

### 一个必须保留的时序点

当前next回调await完成后，Host先推进step，再看取消/绝对期限，只有仍可执行才解析raw字段（1439–1444）。若新RPC在callback完成时就自动读全部command getter，再由Host检查期限，会多执行原本应被取消跳过的模型getter；getter仍可调用真实Host API，不能说这只是表现差异。

严格保持该边界的最小做法：guest调用并await next后保留一个exact raw-result槽，只回“该调用已完成”的被动回执；Host沿现有step/取消/期限判断，允许时发一次 `projectNext`，否则退休槽且不读取命令字段。投影返回后，物理执行前再次检查当前权限与期限，迟到DTO不授权新物理动作。槽只需服务这个固定调用边界，不必建通用远程对象系统。若要合并成单RPC，必须另外证明取消到界时不会新增getter/Host副作用；目前没有这份证明。

## 3. next：保留StepCommand原代数

### 判定顺序

1. `undefined/null` → End。不能收窄为仅null。
2. 原始string：全空白或空串 → Unsupported；否则Run，保留原字符串，不trim，不先JSON化。
3. 其他值：按 `spawn → signal → send → waitFor` 顺序，对四个名字做**全部四次own检查**并计数；不是恰好一个就Unsupported。不能找到第二个字段便提前返回。
4. 计数通过后，仍按上述顺序重新做own检查，直到选中分支。当前实现会重复own检查；Proxy的getOwnPropertyDescriptor可能在guest执行并改变结果，不能偷偷改成只取一次Object.keys或缓存presence。
5. 不添加plain-object、prototype或 `typeof raw==='object'` 前置限制。class实例、null-prototype、带合法own字段的可调用function等不因prototype/typeof本身被拒。反之，只有继承的command discriminator不计为own，不能用 `in` 替代。

own检查不等于枚举，不要求enumerable；不得Object.assign/spread整对象、Object.keys/entries或读无关字段。尤其**不读取toJSON**；原next解释器不调用它，哪怕该字段是普通方法或会产生真实副作用。

### 字段及求值

| 分支 | 原行为，投影应保留 |
| --- | --- |
| spawn | 先读name、后读spawn；即使name非法也已读取spawn。两者必须原始string且非全空白；值不trim。name不另要求own，顶层spawn discriminator已要求own。 |
| signal | 同样先name后signal；两者文本合法后，signal仅允许SIGTERM/SIGKILL/SIGINT/SIGHUP。非法signal是Unsupported，不升级成新错误码。 |
| send | 顶层send只读一次并缓存。原始string走flat send，再读外层name；input可为空字符串，不做非空白校验。 |
| nested send | 仅send为非null object时进入（function不进入）。先读send.input，再检查send是否own name；有own name就取内层name，哪怕非法也不fallback；无own name才读外层name。input必须原始string，可为空。即使input非法，name分支仍已求值。input及name getter不要求own。 |
| waitFor | 顶层waitFor只读一次；nullish则Unsupported，否则读取其timeoutMs再读port。对象不要求plain，嵌套字段不要求own。 |

整数规则：Number.isFinite，不做数字字符串/valueOf强转；值>0、<=上界、Math.floor(value)===value。port上界65535，timeoutMs上界2147483647；timeoutMs缺省/nullish取30000。0、-0、小数、NaN、Infinity、BigInt均不因“可转成数字”而合法。

**timeoutMs的实际重复读取：** compiled条件为 `waitFor.timeoutMs===undefined || waitFor.timeoutMs===null`；若不走缺省，再由integerField读一次。故按短路可读1、2或3次；读取timeout完成后才读port。不能用 `const timeout=...` 合并，否则带状态getter可选择不同期限或改变抛错优先级。顶层waitFor本身则已经缓存，不重复获取。

输入换行规范化仍由原Host `normalizeChildInput`/writeInput做；guest投影不要补换行、重发或假造写入成功。名称冲突、unknown child、物理write确认、waitFor实际探测与关闭仍是Host执行阶段的责任。

### 失败分层

形态/字段类型/范围非法 → `Unsupported`，Host沿现有executeStepCommand产生 `CALL_COMMAND_UNSUPPORTED` 及原reason；不能改为CALL_RESULT_INVALID。

字段getter、own检查trap或必要转换抛错 → callback/projection异常，沿原执行错误通道；不能catch后变Unsupported，更不能继续取下一个字段。到界/取消先于物理动作，原任务状态继续由Host决定failed/timeout/aborted。没有实际命令执行，不写成notStarted以外的伪exitCode。

## 4. watch：执行裁决与sanity否决是两个用途

### 执行watch裁决

在真实execution instance上动态查找watch，以它为receiver，传入Host原watchContext的被动snapshot并await（包括合法thenable）。不要缓存校验规格时取到的方法。

按607–619行顺序：

- null/undefined → Continue。
- 原始false → Abort，reason为`watch returned false`。
- 原始true → Continue。
- 其余值：先own abort；有且非nullish时Abort；否则再own park；有就Park（payload允许null/undefined，不能因truthy判断丢弃）；最后Continue。
- abort先于park，二者同存不能选park。own abort为false、0、空串仍否决；仅继承的abort/park不生效。不要把“返回非对象”一概拒绝，原代码对其他primitive或带own字段的function仍按此规则判断。

**abort getter顺序按实际compiled保留：** own通过后，先读值作undefined比较；必要时再读一次作null比较；两者都没短路才再读一次交给原字符串转换。因此非nullish稳定getter最多读3次。若getter依次返回不同值，第三次reason可以与前两次不同；不能为“保险”缓存一次，也不能把false/0当继续。若abort不成立才检查park，park payload只读一次；无关字段/toJSON不得访问。

### reason转换不能粗略换成JSON或原生String

F# `string(raw.abort)` 当前编译为 `fable-library-js.5.13.0/Types.js:26` 的toString，而不是一律原生String。它可能调用自定义toString、iterator或结构格式化，相关模型代码必须在实际guest物理owner内执行并受外部期限约束，不能让Host拿raw值来转。

还须注意跨realm身份：原formatter运行于Host realm，当前raw对象来自sandbox realm；把同一段formatter源码直接放进raw对象所属realm，会改变与Object.prototype.toString/Object构造器的身份比较。尤其自定义toString的抛错/循环绝不能消失或转为JSON拒绝。下一包至少应固定原始字符串及primitive reason、用户自定义转换的调用/错误行为，并为普通object/array及Host-context来源reason补真实旧入口对照；未对齐前不能声称“复制formatter即全兼容”。若仅默认object的文字格式差异被明确记录为P2，它仍不能掩盖决定Abort/Continue的getter或异常差异。本轮未运行新的reason用例，未把此静态边界冒充已复现失败。

### park payload的JSON边界在这里才合法

原 `OpenCode/ToolWorkflow.fs:187–192,446,464` 展示park时明确做 `JSON.parse(JSON.stringify(payload))`，所以**payload展示**与**watch verdict选择**是不同边界。可在guest完成这个展示转换后传被动值，但不能对整份watch verdict先toJSON后再决定abort/park。

应捕获可信JSON stringify/parse，模型payload的getter/toJSON只在guest执行，不能Host再次执行。缺失值省略、数组洞/undefined、NaN等按原JSON展示行为；不能换成通用strict passive后悄悄拒掉旧可展示payload。循环/BigInt/root undefined等原JSON失败也不能变成假Parked。必须在发布可用park材料/观察T前完成需要guest执行的转换，不能SIGSTOP后要求该guest继续跑序列化；注册/持久park与转换失败的具体顺序需随真正Host接线单独验收，本包不假造ledger已通过。

## 5. onExit：await后只回完成

原1185–1202行只关心Promise是否在deadline内完成，不解释其fulfilled值。仅初次规格认定HasOnExit时，才在对应前台命令结算后调用；新execution实例仍动态查找onExit，保持它的receiver与exitContext。原HasOnExit来自规格实例，不能在fresh实例上重验并静默跳过原本应调用的方法。

guest应执行 `await Reflect.apply(currentOnExit, exactInstance, [context])`，然后返回固定Done。**不要对fulfilled值passive/JSON/枚举/读toJSON或prototype。** 可以合法返回function、class实例、循环对象、带会抛错toJSON的对象而被忽略；但Promise/thenable assimilation仍必须完成，then getter/then抛错或循环仍是回调错误/超时，不能因为“结果忽略”跳过await。

方法getter抛错、方法后来变成非function、真正throw/reject、timeout/abandon沿原控制路径，不变成Done。Done只证明callback完成，不证明先前子进程已经关闭、全部scope收尾或finalizer完成。真实exitCode/signal/scopeClosed仍来自Host，不从模型结果推断。

当前B将onExit结果也交passive，静态上会给原来“忽略的返回值”新增拒绝/副作用。这与已实测next P1是同一根因；本轮未新增onExit兼容测试，不把静态推论记成实际失败数。

## 6. sanity：只投影证明，不投影成执行命令

依1739–1878行：

1. 构造proof实例，metadata校验后调用其当前sanity(null)并await。
2. 从返回pair先读nextDone，再读watchVeto。两次读取均完成后才检查二者是否function；如果nextDone getter先throw，不能继续读watchVeto。属性不要求own或plain。
3. 调用nextDone thunk，await其值；唯一参数容器要求是Array.isArray。不是数组就不调用next；不能要求JSON数组、dense数组、可串行化成员。
4. 使用捕获的Reflect.apply，在原proof instance上动态查找next并应用原args。function、undefined、共享对象、sparse位、receiver、闭包和引用身份都留在同一guest；不用用户可覆盖的method.apply，也不重建函数。
5. next结果仅检查null/undefined。失败不运行watch证明，更不解析为StepCommand/触发物理命令。返回class command或带toJSON对象也只是“未展示null出口”，不能为了诊断先解释或运行命令。
6. next通过后再调用watchVeto thunk并应用同一proof instance的watch。它检查的是`isVeto`，不是完整watch执行裁决：false，或own abort非nullish。**这里不读park，不格式化abort reason，不读取第三次abort，不调用toJSON。** 当前compiled非nullishabort getter最多2次，不应复用执行watch的3次读取路径。
7. 当前compiled isVeto没有先防nullish verdict，hasOwnProperty.call(null/undefined,...)会抛错并进入proofFailure；B目前把nullish值判accepted=false。若下一包声称完整诊断兼容，不能悄悄把这条异常诊断改成另一种reason；若仅保最小proof判据，应继续明确该未迁边界。
8. 只有next/watch都展示出口才构造fresh execution实例。proof的mutable instance状态不可灌入execution；外部lexical闭包的自然共享仍保留。fresh构造不能因复制proof对象而被省略。

### 错误码、诊断与顺序

pair缺少函数：原返回 `CALLABLE_SPEC_INCOMPLETE` 与明确nextDone/watchVeto消息，不是B目前的CALLABLE_SANITY_INVALID。

非数组或出口未展示：原park通道的 `{sanity:{failure,returned}}`，消息分别为nextDone参数数组、next无null路径、watchVeto参数数组、watch无否决路径。未调用方法时returned是null；非null实际返回值保留到诊断展示边界，不能只回accepted=false就声称完整诊断已迁。没有park通道则CALL_PARK_UNSUPPORTED。恢复注入值可直接作为this.call结局，不重做证明或执行。

proofFailure优先：ABANDONED标记继续抛出；然后Host cancellation → CALL_CANCELLED_BEFORE_START；再TimeoutException/ERR_SCRIPT_EXECUTION_TIMEOUT → CALL_SANITY_TIMEOUT；其他异常走带error.Message、returned=null的proof诊断park。不要用统一CALL_RESULT_INVALID遮掉这些分流。

诊断最终也会走原park payload的JSON展示。只有那里按原JSON规则处理returned，而不是在失败判据之前强迫raw returned变被动数据；比如function返回值可在最终JSON中被省略，不能因此让证明通过。完整returned诊断的转换、异常和持久park顺序未由B最小bool回执闭合。

## 7. 规格与Host错误分工不要随投影混改

此包聚焦结果投影，但生产接收时需保留829–878行原metadata次序：构造 → maxDuration → next → watch → interval并规范化 → outputBudgetBytes → worldLock；随后按maxDuration、next、watch、sanity、budget、worldLock的优先级拒绝；仅通过后读onExit。不能为了通用DTO收集提前访问onExit getter。

maxDuration须原number、finite、>0；原转换截断到int64。interval缺省/非finite默认10000，整数转换及下限10按实际Fable产物；budget缺省65536，非负safe integer；worldLock缺省false，提供时必须boolean。这些是现有Host语义，不应把B readSpec的float/undefined字段直接当作全规范化已完成。

Host保留：ReadOutput授权、输出预算、worldLock、借用期限/物理owner、子进程名称与input receipt、实际start/close/Unknown、全scope排空。投影返回的Run/Spawn/Abort/Done都不是物理效果回执。Closing/ClosingUnknown/Closed与已settled终局不能因迟到投影/finish/activated复活；已经接纳的Host工作仍由原owner drain，Unknown不授权重做。

不要复刻原有容易丢失marker的错误字符串化路径来削弱B已明确的强制Abandoned终局。物理owner的Abandoning/Closing线性化仍优先；为兼容投影只需迁移适用的DSL拒绝/诊断分流，不应回退已修的终局保证。

## 8. 可直接复用的证据与下一包最小验收

已存在的实际证据，**本轮未重跑**：

- `/private/tmp/vibe-fs-mf01-callable-command-old-control.test.mjs` 与 `...old-control-node.log`：现compiled WorkflowSurface两项2/0，class own spawn和plain toJSON command真实执行scratch printf，toJSONCalls=0、scopeClosed=true、endUnconfirmed=false。
- `/private/tmp/vibe-fs-mf01-callable-command-compatibility.test.mjs`、`...compatibility-{node,bun}.log`：B各0/2真实红，class被CALL_RESULT_INVALID拒绝，plain toJSON改为different并多做一次write。此文件是生产接收反证，不是要求当前机制B谎称已具备shell功能。
- `/private/tmp/vibe-fs-mf01-callable-independent.test.mjs`、`...independent-extra-{node,bun}.log`：各7/0，raw thunk数组/receiver/identity、own veto表、错误族、finish丢回执、跨run/ref、onExit park和真实Closing。
- B原callable.test的16项：callback各处loop、TERM/KILL/native close、caught/loopingfinally事实。原始B9文件freeze manifest、A Closing红绿与测试自身fixture前提未成立日志均保留，不能覆盖。

下一包只为固定用途投影补有限对照，不全量漫跑：

1. 先让现有两项command兼容红在新用途端口转绿，同时旧compiled正控不变。不要改旧parser或弱化断言。
2. 对next做class/function own command、继承discriminator拒绝、多discriminator零启动、nonenumerable字段、无关toJSON/getter不读、spawn name非法仍读spawn、nested send内外name选择/空input、port/timeout范围及重复getter顺序。
3. 对watch区分执行与proof：abort false/0/空串、abort与park优先级、getter短路/重复次数、own/继承、class/function verdict、toJSON不读；必要字符串转换抛错/loop仍被guest物理owner切断。
4. onExit返回function/循环object/会throw的toJSON仍Done，真实throw/thenable rejection/after-await loop仍不是Done；不使用“无返回”唯一正控。
5. sanity保证raw参数同realm，证明失败零物理启动，先next后watch；missing thunk稳定码、非数组/无出口诊断与恢复注入值、fresh execution实例。returned展示单列合同，不以最小accepted布尔替代。
6. 继续复用真实Closing/丢finish确认反例，确保新投影槽单次消费、旧/跨run槽不能影响新offer，且收尾等待真实Closed/ESRCH/FD/FIFO。

实施边界建议：先在新的/tmp候选只改固定realm解释器、guest用途分派、owner精确结果槽及对应编号/独立测试；不碰公开包/主仓构建图。通过后再由root认领JsCallLoop的私有DTO消费接线，让旧raw解释owner精确退出；不能长期保留两个对同一回调各自解释的owner或泛化fallback。

## 9. 现在不修P1的生产影响

若直接把B接入this.call，旧版正常的class command将从可运行变成CALL_RESULT_INVALID，命名后台无法启动；含合法own spawn/send/signal/waitFor的对象可能被toJSON改写，导致拒绝、执行另一个命令形态或增加原来没有的模型副作用。watch对象可能在选择abort前被序列化/拒绝，onExit原本忽略的结果可能变成失败；这些不是单纯输出样式问题，影响工程循环可用性。

因此当前可发布的结论只是“隔离RPC机制有限通过”。完成特定命令/裁决/证明投影与真实Host接线前，不能说MF01或生产Callable迁移完成。固定60000ms、borrow/reset、生产ledger/ReadOutput/physical effect与最终主仓验收仍独立保留，不由本设计或前述绿数关闭。
