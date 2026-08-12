# lost-update — Main

真正要修的是 state owner 的 update protocol，不是给几个出问题的 call site 打零散补丁。

不变量比“write 不报错”强得多：

> **任何已经 accepted 的 intent，除非有明确 domain rule supersede 或 merge，否则都不能静默消失。**

先选符合 domain 的 ownership model。

如果天然只有一个 authority，就用 single writer。所有 command 进入这个 owner，由它串行 state transition。很多时候这比让每个 caller 都学一套 distributed conflict protocol 更简单，也更诚实。

如果多个 writer 本来就合法，就把 read 时看到的 version/etag/revision 一直带到 commit，用 atomic compare-and-swap。Stale writer 必须得到显式 conflict，重新读 current state，再重新计算 intent。不要“原样 retry”旧 payload；那份 computation 是在旧世界里成立的。

如果并发 intent 本来就能组合，就定义真正针对 **intent / fact** 的 merge law，而不是拿两个 replacement object 做 heuristic field merge。需要 associative / commutative / idempotent 时把这些性质说清楚，并在 arrival permutation 下验证。

常见假修复：

- 只锁最后的 `write()`，read 仍然并发；
- unconditional replacement 失败后无限 retry；
- 加 timestamp，把 last-write-wins 叫 conflict resolution，但业务从没说 wall-clock 更晚者拥有覆盖权；
- field-by-field merge 没有 ownership rule，结果仍能丢掉另一 writer 合法改过的 field；
- storage 接受 stale write 后照样返回 OK，再指望 audit log 解释“为什么数据没了”；
- 只用本进程 mutex，但其他进程/host 仍在写同一 durable record。

验证时必须**强制制造真实 conflict**。让两个 writer 明确读到同一 revision，暂停它们；先 commit A，再释放 B。断言 B 不可能静默抹掉 A。真实 topology 若跨 process/storage，也要在那一层做同样实验，而不是只测两个 Promise“碰巧并行”。

还要测 recovery/retry：B 被 reject 后，必须基于新 state 重新计算，不能 replay stale derived bytes；如果走 merge，就 permutation arrival order，证明 merge invariant。

完成时，每个 write 都应能清楚回答至少一个问题：

- **Which version justified me?**
- **Who serialized me?**
- **What merge law makes my stale premise safe?**

如果答案只是“数据库收下了”，缺陷仍然存在。Storage acceptance 只能证明字节写进去了，不能证明这次覆盖在因果上有资格发生。