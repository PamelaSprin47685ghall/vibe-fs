# U0-G0：GateAcquire 的锁争用与原始 I/O 错误

## G1 payload 独立证明（有限完成）

2026-10-07 从 `766044391` 认领 G1，仅新增 durable-events[012] 四叶与独立 native helper，补原 JournalSurface 的012归属；生产算法不改。G0共享锁分类已有保护，新增正式四叶直接通过，没有新业务红。

实际链为 `JournalSurface_writePayload → EventStoreBlobWriter.Write → Store.WritePayload → withStoreLock`。WHAT[012]规定内容寻址落盘与引用闭包；原 `IBlobWriter.Write` 仍为 `Result<BlobWriteReceipt,string>`，拒绝保原字符串错误，不谎称 AppendError/Prepared 或 Error 引用合同。先以原 Journal 写非空旧正文和合法 TerminalOutputCaptured，建立真实 RuntimeStarted/业务事实、Current/XTrace、read/head/heads及 canonical bytes，再只测新 payload。首次EACCES、首次EIO、真实ELOCKED后成功、真实ELOCKED后EACCES四叶保持原等待预算；busy使用实际锁与受控barrier，不sleep或概率重跑。每叶另开OS进程cold，只读原Current、正文、事实/head及字节；被测新payload不得追加事件或强制lazy初始化。

gen297 Fable174 Surface/834模块及完整check通过；正式完整durable-events/EFP＋JS002/requirement017为41/41文件、287pass/0fail/14skip/15TODO，73.34s wall/335.00s累计test，仅pending退出1；outer27527 accepted=true88.401ms。所有14skip均integration未启用，15TODO保持原完整清单。独立native四叶4pass/0fail/0skip/0TODO、exit0、9.068155s，measure/cold PID依次28780/28790、28803/28806、28826/28831、28841/28847。

两直接错误仅mkdir1次；busy两叶先实际ELOCKED，释放barrier后原mkdir总2次。成功只有新payload的write/fsync/close/Release各1，无事件append；三失败均无新增payload/append、legalAfterFailure=0。两条真实旧事实及read/head/heads、旧正文、XTrace.latestTerminalPresent、Current和canonical bytes均严格保留，cold没有新writer/init/锁。

gen297 compiler=`5a110810e2a6e42005106082d96aeab4efba261d6b40395a0d9dea4b4f7b8c14`，generated=`dcdd5dcebd5d8a44c28bb61059644dad3506831866410d661660e44c85eb6438`，artifact=`624e33e4e020e8c42e90f53151e94081b6e9751a81c745978199771889d7bba2`。最终文档冻结与原件/SHA另见[本包收据](archive/2026-10-07/payload-lock-g1/receipt.txt)。

本项不关闭 owner.json、持续争用取消/等待上限、Git hook、payload fsync/Release故障分类、Journal mandatory fatal owner、完整WHAT[012]或CI。下一D0-P原装配前提；[J3整包卡](U0-A1-Journal-J3施工卡-2026-10-07.md)已补串行拒绝/同步Release的实际风险，须另完整窗口迁移，不以纯类型候选冒领。

## 认领与根因

从 `149aa5d31` 接续U0-A1卡的GateAcquire项。依据 durable-events[004,006,013,017]：进入物理追加前失败应保原请求、阶段与原异常，不能悄悄重试成一次成功写入。本包只修原EventStore物理锁等待；独立Git IntegrationGate不改。WritePayload与Git hook也调用这个共享助手，因而一并停止重试非争用错误，但不借本批Append证明验收其各自错误合同。

ProcessEventLog.acquireStoreLock 向 proper-lockfile4.1.2 传 retries.forever=true。该依赖在acquireLock返回任意错误后无条件operation.retry(err)；retry0.12不区分code。因此mkdir的EACCES/EIO也进入无限等待。Store.acquireAppendGate已有原AppendNotAttempted/GateAcquire结果，无需新错误类型或第二gate。

先补正式006有限反例：自有native child只对原lockPath的首次mkdirSync注入明确controlled EACCES/EIO，第二次若发生则转原合法mkdir。旧实现会有限地误成功，不用sleep、监督超时或概率重跑证明无穷等待。此为端口错误分类证明，不称真实OS权限故障。另以原proper-lockfile真实持同一锁，观察实际ELOCKED后通过microtask barrier释放；原重试方法只读观察、原样执行。主体验收仍是公开typed结果、原cause、零或非零实际append以及独立cold。

## 拟实施与验收

- 原proper-lockfile每次retries=0；仅明确ELOCKED由原ProcessEventLog owner按原延时序列再尝试。原retry默认10项，实际循环为30/33/36/40/44/48/53/58/64/71ms；虽maxTimeout配置150ms，这个序列未到该上限。保原factor1.1与10项循环，不改为单调长至150ms。其他原错误立即raise，由既有Store分类为AppendNotAttempted GateAcquire。
- 不新增锁、registry、durable状态、超时或自动权限修复；不包装原Error，不重试业务fact，不改变原release/CommitUnknown合同。
- 负例核原cause同引用、Requested完整、Prepared=None、cleanup=[]、无新writer/锁残留、旧canonical事实与Current/head均不动。Busy阳性须真实ELOCKED、锁释放后非零原append、完整canonical bytes和独立cold一致。
- 构建只用node scripts/build.mjs；冻结后跑durable相关套件、并发/成本及JS/需求门禁，保准确pass/fail/skip/TODO与raw失败。

## 正式红绿与有限验收

gen271只补测试、未改生产时，完整durable006正式 **1/1文件、24 pass、3 fail、0 skip、0 TODO**，7.42s wall/7.37s test。首次EACCES、首次EIO、真实持锁后EACCES均被旧实现误重试成成功；真实争用后成功正控通过。负例在公开结果断言前已执行独立cold，因此不是测试超时、未导入或缺少清理造成的红。

修复只在原owner分流：proper-lockfile每次retries=0；单次尝试仅捕获ELOCKED并真实等待，其余Error原样离开。外层while保原十项循环，没有递归Promise链；ensureDirectory仍在循环外。成功仍返回原release能力，既有Store将原异常分类为AppendNotAttempted/GateAcquire；无新领域状态或异常包装。

第一版修复用了Fable未提供的Task.Delay，模块链接失败；check另拒绝while→try嵌套。这两项是实现/门禁失败，不计业务红。现用Node自带timers/promises等待，把单次尝试及争用等待归一小函数；未改门禁、预算或测试断言。只读子agent复核未见阻断项。

gen272全Fable、173 Surface/833模块链接、完整check和局部Fantomas通过。冻结输入正式006为 **27 pass、0 fail、0 skip、0 TODO，退出0**，7.55s wall/7.50s test；outer38160 accepted=true/15.432ms。随后完整durable-events、durable-convergence、sphinx-v2、knowledge-reuse、delegation、interaction-authority、dispatch-protocol、managed-chat-execution、managed-session-lifecycle、capability-enforcement、host-boundary及JS002/requirement017，**243/243文件、1691 pass、0 fail、35 skip、106 TODO**，29.92s wall/213.77s test，仅pending退出1；outer38417 accepted=true/16.403ms。

四叶均核原cause引用、原Requested/Prepared/cleanup、真实append/fsync/close/release计数、原canonical bytes、live Current/head及独立OS cold。EACCES/EIO是明确的单跳端口注入，真实争用由原proper-lockfile产生；不宣称OS权限矩阵或持续争用取消已验收。

gen272 compiler `77769ffe049d5c895d59404f747aa0a08fe572c1a7b2d0ff5dd370e7448f0546`，generated `64b82865c69ae38818b03dfbbf926525f8d4eb0df3ae3229d4ee50c8ce45df6c`，artifact `624e33e4e020e8c42e90f53151e94081b6e9751a81c745978199771889d7bba2`。原红、原源码快照、失败修复构建、最终正式日志及fresh证据保存在[原始证据目录](archive/2026-10-07/gate-acquire-g0/)，SHA256绑定；最终文档单独刷新generated输入，不借其覆盖原测试输入。

最新收到的[K1官方CI收据](archive/2026-10-07/baselines/4c251-ci/vibe-fs-ci4c251-receipt.txt)属于4c251原merge，不属于本修复：format/check/build通过，原300秒预算823/824排空、1 active/0 queued，没有全局summary。外层成本821 complete exit0、2 invalid、1 missing不是测试判定总数；不得据此宣布全局0 fail。原group2830 accepted=true/7.690ms只覆盖该组。

## 剩余边界与下一项

本项有限完成。持续ELOCKED的取消/等待上限、真实OS权限矩阵、owner.json写入失败、库内创建失败后清理、其他proper-lockfile调用方的独立验收及完整U0/Host/CI保持开放，不把分类修复写成所有锁生命周期已完成。下一独立证明补Casebook两个waiter共享同一cut拒绝与原owner；路径别名、Capture/Evict和实际physical fatal分开认领。
