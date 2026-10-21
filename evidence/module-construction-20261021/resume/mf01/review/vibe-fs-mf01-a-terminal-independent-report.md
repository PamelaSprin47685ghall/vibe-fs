# MF01 A 终局修复：独立复验

本轮可接收为 **isolated A 的有限终局修复**：上一轮的 Closing 后发布真实 Result 成功反例，在 Node22 与 Bun 均转绿；原12项及独立8项分别全绿。不是完整 MF01、生产 Host/RPC/ledger 或 Callable 接线验收。

## 冻结输入

目录 `/private/tmp/vibe-fs-mf01-vertical-a-20261021`。

- owner SHA256 `66ce95326e007a0d147f81c058892f0a11b669a34e7bacbbe3bed918f88b2c30`。
- candidate.test SHA256 `04ae0da0f3740568d4c56cc49e5a595e32d23691e75da492bbdde28bec3c03f8`。
- 其余5文件未变，完整清单 `/private/tmp/vibe-fs-mf01-a-terminal-inputs.sha256`。
- 开始前 `/private/tmp/vibe-fs-mf01-a-terminal-independent-before.log`，结束后 `/private/tmp/vibe-fs-mf01-a-terminal-independent-after.log`，均由独立 `shasum -a 256 -c` 命令 exit0 得到，七项全部 OK。

本审查未修改这七份文件、主仓源码、index 或编译产物。原红日志及其审查测试保留，不覆盖成绿。

## 实际读取到的修正

`settleBusiness` 只在 Active/Abandoning 接纳第一次业务终局，立即进入 ProgramSettled。Closing/ClosingUnknown/Closed 不再接纳迟到 result/failure；ProgramSettled 也不再接受新 request/activated。此前已经接纳的 Host 工作仍走原 accepted drain。

一旦 abandonRequested 成立，后来的业务结果统一为 `{kind:'Abandoned'}`，包括 guest 业务代码 catch 住原 ABANDONED 后正常返回。独立测试只为此明确合同改了旧 marker 预期；未把旧测试宽容为任意终局。

## 分开执行的实际结果

执行顺序：Node base12 → Bun base12 → Node extra8 → Bun extra8。每条测试命令独立取得退出值，未与后面的 hash 命令合并。

| 集合 | 结果 | 退出 | 日志 |
| --- | --- | --- | --- |
| Node22 原12项 | 12 pass / 0 fail，cancelled/skip/TODO 0 | 0 | `/private/tmp/vibe-fs-mf01-a-terminal-independent-base-node.log` |
| Bun 原12项 | 12 pass / 0 fail | 0 | `/private/tmp/vibe-fs-mf01-a-terminal-independent-base-bun.log` |
| Node22 独立8项 | 8 pass / 0 fail，cancelled/skip/TODO 0 | 0 | `/private/tmp/vibe-fs-mf01-a-terminal-independent-extra-node-final.log` |
| Bun 独立8项 | 8 pass / 0 fail | 0 | `/private/tmp/vibe-fs-mf01-a-terminal-independent-extra-bun-final.log` |

Node 22.13.0，Bun 1.3.14；macOS 26.6.2 / Darwin25.6.0 arm64。没有并发物理实验，没有外部 Host 或不属于本 harness 的目标信号。

### 原 Closing 红例

仍用原真实 ProcessSession 的输出代理：拿到真实 guest result chunk，转交 owner.drain 前调用该 owner.kill()。没有伪造 result/frame/runId，也没有直接调用 fake dispatch。

Node PID48886：取消前 native Running、PID存在、owner Active；取消后 Closing；对外发布 Closed，实际 native SIGKILL/Closed。

Bun PID48962：相同顺序，对外也发布 Closed；实际 native exit0 自然退出赢 KILL。即使原生退出恰好成功，先发生的 Host Closing 仍不再被迟到 Result 改写。

其余独立7项维持旧 scope：clone/跨owner exact park site；sync、after-await getter/toJSON/thenable 四类循环的 Host 响应及真实外部 SIGKILL；abandon 原 finally 的实际同步 write 与拒绝再次 park；旧 accepted async reply 在旧 child 关闭后结算而不影响 fresh owner。

原12项中 normal result 后的重复 result/failure/activated 属作者的直接 dispatch 状态正控，不能冒充真实 guest 未 await continuation 已执行。caught abandon 则经真实 guest、真实 finally write、真实显式 Abandoned 终局完成。

## 可选第9项：前提未建立，未计为通过

另试了一个真实 continuation fixture：复制原 guest，仅在发正常 result 后保留 stdin/FIFO，模型先合法 write、再未 await 一个 async cat，主程序返回；期待原 cat 的 continuation 随后尝试 sync write。复制 guest 放在另 /tmp，没有修改七份冻结输入；没有 fake dispatch。

Node 实际未发该新 RPC，1000ms 因果界得到 `CONTINUATION_DID_NOT_ATTEMPT_RPC`。现 Sandbox 主 program 结算后停止其驱动循环，保留传输不等于还能驱动原 vm continuation。因此此用例没有建立“确实尝试新 write”的前提，不能证明拒绝失效，也不能计作拒绝正控。未继续为凑数改 guest/vm 或发伪帧。

原失败源 `/private/tmp/vibe-fs-mf01-a-terminal-continuation-premise.test.mjs`；复制 guest `/private/tmp/vibe-fs-mf01-a-terminal-keep-open-guest.mjs`；原日志 `/private/tmp/vibe-fs-mf01-a-terminal-independent-extra-node.log`。该失败批次前8项均绿，所有11个实际 child 都已 Closed/ESRCH、FD/FIFO 回收。审查测试自身的 Promise.race 外层到界没有取消内部轮询，导致 harness PID48467 留有 timer；随后只对已核验 PID/PPID/PGID/出生时间/完整命令一致的该自建 harness 发 TERM。清理日志 `/private/tmp/vibe-fs-mf01-a-terminal-independent-audit-poll-cleanup.log`，执行 session 最终 exit1；后续单独 kill(pid,0) 确认 ESRCH。没有信号其他进程。

上述失败源作为原始证据保留，不应原样无人监督复跑：它含已确认的审查轮询取消缺口。最终独立8项源不包含该可选用例，运行已正常自行退出。未来若要证明此可选路径，应先明确结果后还会驱动 guest 代码的合法入口，再写可取消的等待，而非把传输保留当作执行前提。

## 收尾

最终 Node extra8 的10个 child PID：48836、48839、48851、48854、48865、48871、48874、48880、48883、48886。

最终 Bun extra8 的10个 child PID：48908、48912、48924、48935、48939、48943、48947、48954、48958、48962。

每个 cleanup 均先等 native Closed、failure null、PID ESRCH、acceptedRemaining0、FIFO 不存在及旧 FD fstat/EBADF，再删除自己的 temp 目录。原12项也有每个 owner 的真实 Closed/ESRCH 收尾。无仍运行测试、owned guest、retained Unknown 或待删除活动目录；敏感运行预约已交还 root。

## 复跑与剩余边界

最终测试源 `/private/tmp/vibe-fs-mf01-a-terminal-independent.test.mjs`，SHA256 `b2dee149e49927013aa2009a9554b0373926980fc1fdc1bcfa015714deff419a`。源及两个额外历史 fixture 的完整指纹 `/private/tmp/vibe-fs-mf01-a-terminal-independent-harnesses.sha256`。最终8项只导入冻结候选的 owner/wire/ProcessSession，无新 npm 依赖。

```sh
cd /private/tmp/vibe-fs-mf01-vertical-a-20261021
shasum -a 256 -c /private/tmp/vibe-fs-mf01-a-terminal-inputs.sha256
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test candidate.test.mjs
/opt/homebrew/bin/bun test candidate.test.mjs
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test /private/tmp/vibe-fs-mf01-a-terminal-independent.test.mjs
/opt/homebrew/bin/bun test /private/tmp/vibe-fs-mf01-a-terminal-independent.test.mjs
shasum -a 256 -c /private/tmp/vibe-fs-mf01-a-terminal-inputs.sha256
```

结果后真实 continuation 尝试新 RPC 尚缺正控；Callable 的函数/undefined/引用身份参数、同 proof 实例本地 Reflect.apply、nested borrow/reset、超过固定 guest 60000ms 的借用期限、生产 Host authority/ReadOutput/Prepared/Committed ledger、实际内存硬限额与极端 OS/IO 收尾均不由本轮关闭。B 在另目录静态实现，本报告没有运行或接受 B。
