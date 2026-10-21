# MF01 isolated vertical A：独立反例矩阵（候选冻结前，只读准备）

依据 `/private/tmp/vibe-fs-mf01-resumed-design-20261021.md`、主仓原 `JsSandbox.createDeadlineOwner` 与保存的 owner 合同。此文不是测试结果，也不是候选源码缺陷清单；准备时 `/private/tmp/vibe-fs-mf01-vertical-a-20261021` 尚未出现。未执行测试、构建、native Host 干预或先前被拦实验。

## 接收边界与顺序

作者门1/2运行期间不并行占用物理资源。等冻结五文件 owner/guest/wire/host-apis/candidate.test 后，先静态定位契约，再选少量高价值反例独立执行。记录源码哈希、Node/Bun版本、每个自建 owner 的 started/exit/close/FD/临时路径及 cleanup。失败保留原 evidence，不把 timeout/Requested/输出结束改判为已回收。

门1：真实 scratch Host sync write → async cat，双通道无争读；exact incarnation、外部 watchdog 与关闭。

门2：真实 ps T 后才发布 Parked；同一 continuation 任意有界 JSON 大值 unpark、again-park；先过这两门才继续 Callable/finally。Scratch fs 不能冒充生产 path/read snapshot/ledger/ReadOutput。

## 优先矩阵

| ID / 优先级 | 精确反例与前提 | 必须观察的结果 / 可证伪点 |
| --- | --- | --- |
| T01 / P0 / 门1 | 同一 guest 先发 async cat 不 await，再立刻 sync write，最后 await cat；两个 Host 结果人为相反顺序完成。 | FIFO 只有 sync reply reader，stdin 只有 async reader；ID对应，write后真实文件可读，无Host阻塞/争读/错配。 |
| T02 / P0 / 门1 | 接受一次 append/write 后挡住业务回复，关闭旧 owner，创建新 incarnation，再释放旧回复。 | 原实际 mutation恰一次；不自动重发；旧结果不 resolve 新 run 的同号 request，原工作仍由原Host owner结算。不要用内存count替代scratch文件。 |
| T03 / P0 / 门1 | 错 incarnation、重复 response、未知 ID、旧 run ID、旧 park generation，分别进入各接收方向。 | 不触发Host fs，不解决其他Promise，不分配新许可，不复用旧site。错误只作用于对应owner，不误杀新进程；所有真实PID由Host已创建handle持有。 |
| T04 / P0 / 门1 | guest在sync FIFO read中等待；或在握手/打开FIFO前退出。 | Host主线程未被openSync/writeSync或readSync锁住；外部watchdog/取消仍能清理 exact child；reply写者不能靠guest打开读端才能让Host继续执行。 |
| T05 / P0 / 门1 | top-level sync循环与after-await循环，两种运行时分别在确切已开始后触发。 | 外部watchdog实际触发、只终止该child、ProcessSession Closed包含真实exit与failure；Host可完成另一个真实受控动作。不是vm timeout或固定3s辅助guard替代本deadline。 |
| P01 / P0 / 门2 | guest提出park，同时已排入一个稍后的模型microtask/async callback。 | site发布必须在本目标ps观察T之后；暂停期无guest后续帧/Host副作用。停pump或parked Promise不算物理暂停。外部已接受Host操作仍能结算，不因guest被停丢失。 |
| P02 / P0 / 门2 | unpark携带大于pipe容量的JSON值（如约1MiB Unicode），目标此前已T。 | Host先stage exact generation outbox，不能await停止目标的消费才CONT；恢复后增量传输/解码，值与hash完整、只应用一次，同一closure继续。transport chunk不是产品返回值上限。 |
| P03 / P0 / 门2 | guest收到unpark，发generation ack后立即resolve原await；continuation第一条语句合法sync write。ack/RPC可能落在Host同一次stdout chunk。 | Host必须在处理exact ack的线性化点同步进入Active，然后处理随后的RPC；若等unpark Promise.then才改状态，会误拒合法调用。写入实际落盘，不能只看unpark返回成功。 |
| P04 / P0 / 门2 | CONT后但ack前，有旧microtask发API；错误generation ack先到、正确ack后到；取消与ack同时到。 | ack前不能给新Host副作用权限；正确generation只线性化一次，关闭/abandon不能被晚ack重新Active。若ack与RPC不同通道，不能假定跨stdout/stderr全序。 |
| P05 / P0 / 门2 | park前已接受一个async Host请求，目标T后才完成；其reply与unpark大值同时等待outbox。 | old accepted reply按原ID保留并结算，不作为新的Parked RPC拒掉；不抢stdin读者，不让late reply改变新generation，恢复/关闭都不重放已做过的write。 |
| P06 / P0 / 门2 | unpark同site两次，恢复后立即再次park，再投旧site/旧ack/旧inject。 | 单次消费；新site/代次独立，旧操作不能恢复或取消新site；同一lexical状态41→42→下一阶段，无新进程替身。 |
| D01 / P0 | 原始剩余预算R，借长L1，再借短L2；分别以L1→L2和L2→L1释放；最后再重复一次release。 | 与真实旧owner一致：首借固定R，current=max(now+R,各lease expiry)，最后释放后now+R；重复release无效。实际合法Host等待跨过原deadline仍完成，释放后失控guest被原权威切断。 |
| D02 / P0 | 有active borrow时reset到未来期限，交错两次release；再覆盖reset到过去期限。 | reset更新remaining，不被最后release恢复成reset前R；另一lease仍有效时不能提前杀；全部释放后过去reset立即到界。对live外部timer给实际正负控，不只镜像同一公式。 |
| D03 / P0 | borrow/reset发生在ParkRequested或Resuming，以及取消发生在等待RPC期间。 | 只有一个Host期限权威，旧timer不能杀新epoch，不能让park永久关闭watchdog后恢复无期限；不新增隐藏短timer拒绝合法等待。具体可用状态须先由候选明确。 |
| C01 / P0 / 两门后 | Host通过显式端口构造class、读spec getter、sanity、proof thunk、next/watch/onExit；每一点分别sync与after-await循环。 | 所有模型执行均在guest；Host从不构造原对象、读原getter或调用Proxy。外部exact owner可停；不能用顶层run green覆盖这些调用点。 |
| C02 / P0 / 两门后 | 模型返回getter/toJSON/thenable各自循环或抛错（含异步thenable）。 | 序列化、getter、Promise assimilation仍在guest且受外部期限；Host只接被动data。不能Host JSON.stringify原模型返回值后被其挂住。 |
| C03 / P0 / 两门后 | lexical capture+this mutable state；proof实例修改字段、execution实例应fresh；stale class/instance/thunk ID注入新run。 | receiver/closure保留，证明与执行不同实例；旧ID不能进入新owner。不是函数toString重构或Host本地fallback。 |
| A01 / P0 / 两门后 | abandon parked原await，finally中非await Host write/rm，再试park；业务代码捕获abandon。 | cleanup窗口允许真实Host finally，重复park拒绝；catch不能将abandon变normal成功；ledger未接线则只报告scratch事实。 |
| A02 / P0 / 两门后 | finally sync/after-await死循环，且Host已有一个实际接受但未回执的mutation。 | 外部清理预算到界后实际杀guest/Closed；分别记录finally未完成与Host已接受操作的结算，不因guest死而丢副作用，也不报正常finally完成。 |
| W01 / P1 | 帧头/长度/JSON被任意chunk切分，Unicode分开；stdout与stderr同时有内容；有非协议stdout。 | 增量边界正确，无UTF8替换、无跨通道顺序假设；非协议模型输出不能被当command，错误有界关闭exact owner。 |
| W02 / P1 | undefined、字段缺省、NaN/Infinity、循环对象、带访问器或toJSON对象越边界。 | 按当前JsFailure/无效返回合同区分，不用JSON悄悄把NaN变null或删undefined；错误产生与序列化在正确owner，不把数据失败误记guest成功。 |
| L01 / P0 | 正常返回、程序错误、timeout、protocol失败、read失败、park中abandon，逐项结束。 | 每一自建目标/通道/FD/outbox在对应结局有终局，关闭未知保留owner，不用signalRequested或discardOutput冒充native close；FIFO和scratch目录清理有证据。 |

## 两个实现前须说清的线性化点

**Resuming：** Host先消费旧site并安装新deadline，将该generation的inject排入outbox，然后CONT。guest确认完整注入后发ack再继续原await。ack与随后的RPC若共用有序guest→Host通道，Host在同一ack handler内先变Active、再接受下一帧；不能把权限切换推迟到等待者的Promise continuation。若不同通道，需明确排队/拒绝合同，不可假设跨管道全序。ack只证明该注入已被应用，不证明后续模型成功或整个process关闭。

**关闭与late reply：** Host接收请求的时刻决定工作归属。关闭撤销新请求；已接受Host操作仍由原owner结算，但发送给guest的旧结果不能复活旧site或进入新incarnation。Unknown不允许重新做同一mutation。

## 不由本矩阵关闭的义务

这些都是 isolated A 的建议反例。公开 package/主仓接线、生产Prepared/Committed ledger、真实ReadOutput撤权、session retirement、Host SDK行为、shell/rg owned drain、实际内存硬上限均未证明。Scratch fs、短暂停原语、RSS观测和小批测试数都不能替代这些合同。不得运行先前被moderation拦下的Host干预实验。

## Callable 参数的保持义务

主仓 JsCallLoop 的 sanity thunk 参数只要求 Array，没有要求可 JSON 化。Callable 端口必须在 guest 保留同一 proof 实例、thunk 与参数，并在 guest 本地 Reflect.apply；Host 只收判据和被动诊断。后续用例要包含参数中的 function、undefined、共享对象引用及闭包，核对 this 与引用相等。将整个参数数组发 Host 再回传，或通过函数源码重建，都会损失现有合法能力。当前 A 尚未实现 Callable 端口，本轮不把 tagged wire 的非数据拒绝当作这些参数已经通过。
