# MF01 isolated A 独立有限验收

结论：冻结 A 的双通道、物理暂停及外部终止原语已有有限正控；**不能接收为具有正确终局撤销的 owner**。独立用例在 Node22 与 Bun 都确认：取消已将 owner 变为 Closing，随后 drain 仍发布真实 guest Result。未修改候选、主仓、index、构建图或公开包；未启动外部 Host。

## 输入与运行环境

候选目录 `/private/tmp/vibe-fs-mf01-vertical-a-20261021`。原冻结清单 `/private/tmp/vibe-fs-mf01-a-gates-boundary-inputs.sha256` 列出七份实际源码/依赖 bundle。独立运行后 `shasum -a 256 -c` 单独命令 exit 0，七项全部 OK：`/private/tmp/vibe-fs-mf01-a-independent-inputs-after.log`。未将校验命令的 exit 当作前一个测试命令的 exit。

关键哈希：owner `85c26dff60cb466a308cede9b7789964448ad9595f9b0571a7293559dac49c6a`；guest `28c220730240351b9936c63ecf66b6a4fd707073738920065e0faa6b765ccda7`；ProcessSession `57ecdb37c1c7c00cca0b735853fb563c85b42efd9ca3290261a332cd0f199054`；Sandbox `9157f06c7b4ca850c1e5f0ba709db4dcd0ad1e0c0ab9e92bdf34c192e0952eec`。其余三份见原清单。

Node 22.13.0：`/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node`。Bun 1.3.14：`/opt/homebrew/bin/bun`。macOS 26.6.2 / 25G83，Darwin 25.6.0 arm64。先 Node，后 Bun，未并行运行物理用例。

## 独立命令与判决

每条测试命令单独执行，退出值由执行工具获得；日志中的 counts 与真实退出值分别记录。

| 集合 | Node22 | Bun | 日志 |
| --- | --- | --- | --- |
| 作者原 10 项，不改测试 | 10 pass / 0 fail，exit 0 | 10 pass / 0 fail，exit 0 | `/private/tmp/vibe-fs-mf01-a-independent-base-node.log`、`/private/tmp/vibe-fs-mf01-a-independent-base-bun.log` |
| 最终独立 8 项 | 7 pass / 1 fail，exit 1；cancelled/skip/TODO 均 0 | 7 pass / 1 fail，exit 1 | `/private/tmp/vibe-fs-mf01-a-independent-extra-node-accepted-scope.log`、`/private/tmp/vibe-fs-mf01-a-independent-extra-bun.log` |

完整复跑命令（先检查候选仍是该冻结点）：

```sh
cd /private/tmp/vibe-fs-mf01-vertical-a-20261021
shasum -a 256 -c /private/tmp/vibe-fs-mf01-a-gates-boundary-inputs.sha256
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test candidate.test.mjs
/opt/homebrew/bin/bun test candidate.test.mjs
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test /private/tmp/vibe-fs-mf01-a-independent.test.mjs
/opt/homebrew/bin/bun test /private/tmp/vibe-fs-mf01-a-independent.test.mjs
```

独立测试源 `/private/tmp/vibe-fs-mf01-a-independent.test.mjs` 只导入上述冻结目录的 owner、wire、ProcessSession。没有新 npm 依赖、工作区 link、编译或生产协议变更。不得用后续修订的候选覆盖本轮输入再声称此红已被原版本通过。

## P1：Closing 后仍发布 Result

位置：`owner.mjs:142–147`。runId 检查之后，result/failure 立即 resolve 对外结果，先于任何 phase 限制；`kill()` 已先在 `owner.mjs:274–277` 撤销为 Closing，但不阻止这一分支。

独立用例最后一项以真实 ProcessSession 启动真实 guest，模型做一次实际 scratch write 后返回 `complete`。ProcessSession 代理读取真实 stdout chunk，旁路 FrameReader 只识别其中确实已有的 result 帧；转交原 chunk 给 owner.drain 之前调用该 owner.kill()。未伪造 guest 值、runId、PID 或 stdout 帧，没有向其他进程发信号。

两种 runtime 均观察到：nativeBefore `Running`、absentBefore `false`、phaseBefore `Active`，取消后 phase `Closing`；原 chunk 随后被 drain 接收，结果仍是 `Result`。Node 最终 native Closed/SIGKILL；Bun 本次 native 自然 exit 0 先于 KILL 生效，但同样发生 Closing 之后发布成功。因此 native 最后恰好自然退出，不能替代 Host 已发生的撤销线性化点。

最窄修正建议：由 owner 的同步终局/权限状态判定是否能接收 result/failure。Closing、ClosingUnknown、Closed 不可被迟到成功改写；正常接收终局也要同时撤销后续新 RPC，保留此前已接受 Host 工作的 drain/Unknown 归属。真实 native close 与业务结果仍分开保留。对正常 result 后到 nativeClosed 之间还能否接新 RPC，本轮只有静态路径观察，未另造伪帧冒充真实模型反例。

## 最终通过的 7 项实际证明

1. 两个实际 parked owner：复制 site 与跨 owner site 都拒绝，未消费各自正确 site；两个原闭包各自从 41 到 42 并得到正确值。
2. 顶层 sync 循环：Host 定时任务仍推进，250ms 外部 watchdog 实际触发，实际 child SIGKILL/Closed。
3. after-await 返回对象 getter 循环：同上。
4. after-await 返回对象 toJSON 循环：同上。
5. after-await thenable getter 循环：同上。
6. abandon 原 await：同一 closure 的 finally 执行两次真实同步 Host write，42 落盘，再次 park 返回 `CANNOT_PARK_WHILE_ABANDONING`，仅一份实际 park 帧。保持原 Sandbox 返回 `{__jsAbandoned:true}` marker 的现有行为。
7. 原 owner 已接受的延迟 async cat 在旧 child 关闭后才结算，不复活原 continuation；同时新 owner 的同号首次请求正常写盘/返回，旧 late 文件不存在。

循环用例明确断言物理 Closed、failure null、SIGKILL、watchdogFired 及 Host timer 至少推进三次；结果诊断允许 `TimedOut` 或确切 `GuestFailure/ERR_SCRIPT_EXECUTION_TIMEOUT`。Node 原生 vm 可能先产生 timeout 错误，不能把“某次最终错误槽不是 TimedOut”另算一条循环不可终止缺陷。Bun 本轮四个循环均由外部 owner 返回 TimedOut。

## 保留的测试自身红证据

首版源 `/private/tmp/vibe-fs-mf01-a-independent-first.test.mjs`、日志 `/private/tmp/vibe-fs-mf01-a-independent-extra-node.log` 为 6/2 exit 1。除了真实 Closing 反例，另一个红是审查测试错把 abandon 预期为 GuestFailure。既有 Sandbox 明确将 `__jsAbandoned` 转为 JSON marker，故只修审查测试，不改作者实现，也不把此红算缺陷。

第二版源 `/private/tmp/vibe-fs-mf01-a-independent-second.test.mjs`、日志 `/private/tmp/vibe-fs-mf01-a-independent-extra-node-final.log` 为 5/3 exit 1。abandon 已绿；getter/thenable 两项物理 SIGKILL/Closed 正确，但审查测试过严地要求所有 runtime 结果诊断必须 TimedOut。最终版缩到上述实际目标，保留且单独严格检验 Closing 成功反例，没有将真实失败兼容为通过。

## 收尾与仍未证明的边界

最终 Node 独立集合有 10 个 actual child：46341、46344、46355、46358、46361、46364、46367、46373、46376、46390。最终 Bun：46427、46431、46443、46447、46451、46455、46466、46473、46477、46481。每个 cleanup 都等待 native Closed，断言 failure null、ESRCH、acceptedRemaining 0、FIFO 不存在、旧 FD fstat 为 EBADF，然后才删除其私有 temp 目录。原始所有修订批次同样有 all-owned-absent 断言；无自建常驻进程、定时实验或 retained Unknown owner。

这些仅支持 isolated A 的有限能力。生产 Host authority/Prepared/Committed ledger、ReadOutput、可调用对象与 proof 实例、同步 Callable 参数的 function/undefined/引用身份、nested borrow/reset、超过固定 guest 60000ms 的借用期限、真实内存硬限额、极端 IO/OS cleanup 失败仍未接线或未证明。A 的 marker 正控不是后续 B 的强制 Abandoning 业务终局证明。尚无生产主仓接线、本轮不称 MF01 完成。
