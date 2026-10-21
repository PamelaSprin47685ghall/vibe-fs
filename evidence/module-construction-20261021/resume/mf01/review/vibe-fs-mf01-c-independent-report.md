# MF01 C 独立验收收尾

结论：最终冻结C的核心deadline流程通过本轮独立验证，没有新发现阻碍该隔离流程运行的问题。遵循用户“忽略小问题、先记录、保证流程能跑通”的收窄，已启动的C16正常结束；额外只跑真实缓冲offer/reply过期准入两项，不继续lease/T组合或无效trusted输入扩测。C/D仍是隔离候选，不作为当前可用主仓正式验收/PR的阻断。

未修改被验源、主仓、metadata/index，未构建或实现D。所有测试串行，无仍运行实验，敏感运行预约已交还root。

## 最终源与完整性

目录 `/private/tmp/vibe-fs-mf01-vertical-c-deadline-20261021`，实际15输入清单 `/private/tmp/vibe-fs-mf01-c-round03-inputs.sha256`。

- owner `ebedbc5470368ffedfd646b776e53f4e23869d4deaff5761bf7bf607b4829f9e`
- guest `736544ede5056d07cf21b28d3cd8b9a5083554fb32e258417023b39c38bf3f29`
- host-deadline、最终测试、F#、实际bundle及其余来源见原15项manifest。

开始前 `/private/tmp/vibe-fs-mf01-c-independent-before.log`、结束后 `/private/tmp/vibe-fs-mf01-c-independent-after.log`，单独sha检查各exit0、15项全部OK。含baseline-only B依赖的24项最终manifest也单独exit0全OK：`/private/tmp/vibe-fs-mf01-c-independent-all24-after.log`，对照 `/private/tmp/vibe-fs-mf01-c-final-inputs.sha256`，可复跑归档 `/private/tmp/vibe-fs-mf01-c-final-source.tar.gz`。本轮没有更改开发依赖link或运行bundle。

Node22.13.0 `/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node`；Bun1.3.14 `/opt/homebrew/bin/bun`；macOS26.6.2 / Darwin25.6.0 arm64。

## 分别取得的实际退出

| 顺序/集合 | 结果 | 独立退出 | 日志 |
| --- | --- | --- | --- |
| Node最终C16 | 16 pass / 0 fail，cancelled/skip/TODO 0，68.43s | 0 | `/private/tmp/vibe-fs-mf01-c-independent-base-node.log` |
| Bun最终C16 | 16 pass / 0 fail，68.27s | 0 | `/private/tmp/vibe-fs-mf01-c-independent-base-bun.log` |
| Node核心独立2项 | 2 pass / 0 fail，cancelled/skip/TODO 0 | 0 | `/private/tmp/vibe-fs-mf01-c-independent-core-node.log` |
| Bun核心独立2项 | 2 pass / 0 fail | 0 | `/private/tmp/vibe-fs-mf01-c-independent-core-bun.log` |

没有用后面的sha命令退出替代测试退出。未重跑无关A10/B16或旧B baseline。作者先前Node42 tied旧fixture的事实仍按作者receipt保留；本轮独立只证明**最终源C16**，不把它扩大成最终源42项全跑。

### 长异步借用的实际事实

Node长例PID59003，callback单调耗时61059.95ms；超过原epoch后完成真实sync write，释放后恢复1496ms原base预算并正常exit0。Bun长例PID59719，61061.23ms，恢复1493ms并正常exit0。

独立C16的另一耗尽用例：Node保存347ms、释放后约349.49ms真实关闭；Bun保存345ms、约354.14ms关闭。它们区分了恢复原剩余budget与重获完整lease时长。

这是61秒async cat/light interaction及后续sync write，不是61秒单次同步CPU运行证明。移除原60000ms调用cap只有F#及实际bundle的静态/连续性证据；外部loop切断验证有限时段，不冒充已跑61秒同步模型。可信installer仍有独立5000ms启动界，尚不代表完整生产program最大时长合同。

### 额外核心过期准入

独立测试从真实ProcessSession读取真实stdout frame，仅在交给owner.drain之前把同owner deadline reset为past；没有fake dispatch、伪造结果、PID或runId，也没有向无关进程发信号。

两种runtime每次都观察到native Running/PID存在、owner Active、watchdog尚未触发，然后：

- callable-offer：过期offer未签发，等待方得到OWNER_REVOKED，未调用scratch API。
- callable-reply：原cat与write已经实际接纳，write-1真实存在且精确一次；迟到callback值没有成为成功，等待方得到OWNER_REVOKED，业务未settled，不回滚或重发此前write。

两项都由当前epoch准入检查触发实际SIGKILL/Closed及TimedOut，不靠timer先执行这一巧合。新ref/业务成功不可用缓冲结果越过已过期权限。

## 全部owned资源收尾

C16每项都有early owned-opened记录，helper等待native Closed、failure null、PID ESRCH、accepted排空、replyFd=null、FIFO不存在后删除私有目录。核心两项还对原FD实际fstat断言EBADF，callablePending=0。

核心Node PID60379、60382；Bun60425、60430，全部实际SIGKILL/Closed、failure null、ESRCH。

四份本轮日志共36个owned-opened。收尾后独立读取这些精确PID，逐个signal0确认ESRCH，并核私有目录已不存在：`/private/tmp/vibe-fs-mf01-c-independent-cleanup-audit.json`，检查命令exit0，count36/allAbsent=true/allDirectoriesGone=true。该审计只检查存在性，不发送终止信号。没有未确认owner、活动FIFO、私有目录残留或继续等待的harness。

作者早前Bun默认5000ms framework abort的旧日志/残留归档保持原样；本轮不补造其当时未记录的PID或ESRCH，也不将旧framework失败计作候选deadline失败。

## 保存的独立源与复跑

实际运行源 `/private/tmp/vibe-fs-mf01-c-independent.test.mjs`，SHA256 `ef48b68ffd5a6b14be891e1ef9c5c1d96d159c23b787d14e32f710a7096e22a8`，只含两项core。

收窄前准备但未运行的五项源保存为 `/private/tmp/vibe-fs-mf01-c-independent-prepared-unrun.test.mjs`，SHA256 `f664251ec7b18c10f452da3390fcc0da0f5dd742c49cde3d5e305dabc4845b07`。其中额外lease/T三项没有结果、没有计入通过数；按用户要求不继续运行。两个源指纹清单 `/private/tmp/vibe-fs-mf01-c-independent-harnesses.sha256`。

```sh
cd /private/tmp/vibe-fs-mf01-vertical-c-deadline-20261021
shasum -a 256 -c /private/tmp/vibe-fs-mf01-c-round03-inputs.sha256
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test --test-concurrency=1 deadline.test.mjs
/opt/homebrew/bin/bun test deadline.test.mjs
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test --test-concurrency=1 /private/tmp/vibe-fs-mf01-c-independent.test.mjs
/opt/homebrew/bin/bun test /private/tmp/vibe-fs-mf01-c-independent.test.mjs
shasum -a 256 -c /private/tmp/vibe-fs-mf01-c-round03-inputs.sha256
```

## 记录而不扩修

- **接生产前P1，当前主仓不受影响：** B/C继承generic passive的command兼容差异，已由此前旧compiled正控和Node/Bun反例确认。候选未接入主仓，不作为当前主仓流程/PR阻断；将来接生产前再处理按用途投影，不能因C deadline绿而说兼容P1关闭。
- **P2候选私有API边缘，仅静态：** 无效NaN/Infinity resume budget在finite检查前消耗site并转Resuming；显式dispose仍能持有/清理owner。未测、未扩大正常有效流程影响，按用户要求记录不修。
- **P2已知候选接口边缘，仅静态：** nextOffer先shift队列再检查终局，可能交出已退休ref；后续callable权限门仍拒绝。没有因此声称可执行新操作，不为边缘扩测。

cleanup的enterCleanup是明确批准的新隔离政策：退休旧execution leases、固定同一个guest清理epoch；不是现生产reset仍取live lease最大值的旧语义。已接纳Host工作仍独立drain，所以guest在cleanup期限内关闭不代表整套Host工作也已完成。生产借用计算/admin hard limit、shell物理owner、ledger/ReadOutput、内存限额与完整主仓接线仍未由隔离C关闭。
