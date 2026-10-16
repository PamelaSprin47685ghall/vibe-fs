# lost-update — Lesson

## 评价

### 定义

Lost update 不是“两个 write 离得太近”这么浅。它是对历史的静默篡改：两个 writer 都从 version N 做出决定，其中一个结果已经被接受，另一个 writer 却仍拿 N 时代的前提去覆盖当前状态，于是一个已经成立的事实没有经过任何显式 supersede，就消失了。

最常见的外形非常普通：

```text
read current
compute next from current
write next
```

真正危险的是这三步之间藏着一句没写出来的话：**“我读到的状态，在我提交时仍然是当前状态。”** 并发一旦存在，这句话默认就是假的，除非有 protocol 证明它仍然成立。

### 支配原则

不要把问题简化成 “last writer wins”。如果业务明确定义“后来的 authority 覆盖之前的 authority”，last-writer-wins 可以是合法规则。Lost update 的病在于：**scheduler timing 让一个旧前提拥有了抹掉新事实的权力**，而 domain 从未作出这个决定。

### 何时触发

以下情形应触发：

- 两个 writer 可以读同一 revision，最后各自 replace 同一份 state；
- update 从 snapshot 计算整个 replacement object，然后无条件写回；
- storage write 返回 success，但另一个已经 accepted 的变化从结果里消失；
- conflict 后只是把同一份 stale derived payload 再写一次；
- storage 明明支持 etag/version/CAS，application 却没有把 read version 带到 commit；
- 所谓 “merge” 只是挑一份完整 object，另一方改过的 field 被顺手丢掉。

### 不应触发

不要因为“有并发”就触发。Atomic commutative update、append-only fact、真正 single-writer owner、或有数学语义的 merge law，都可能合法并发而不丢 update。

### 与相邻规则区分

邻近规则要分清：`shared-mutable-concurrency` 管多个执行体共享 write authority 的架构；`race-first-wins-semantics` 管 arrival order 决定业务真相；`optimistic-retry-assumption` 管上一次 effect 结果未知却乐观 retry。本规则只在这一刀最准确时使用：**一个已接受的 update，会因为另一个 stale writer 成功提交而消失。**

### 判定程序

最有力的判定实验很简单：让 A、B 同时读 version N；先让 A commit；再让 B 提交它基于旧 N 算出的结果。此时只能有三种合法结局：

1. B 被判 stale，必须 re-read；
2. single writer 让 B 根本没有真正从 stale state 提交；
3. 明确 merge law 同时保住两边 intent。

如果 B 能成功返回，而 A 的已接受信息静默消失，系统就在不承认的情况下重写历史。

修复也不是“加个锁”。只锁最后的 `write()` 没用；stale premise 早在 read 时就形成了。Protocol 必须把 **read identity 与 commit identity** 绑定在同一个 logical update 上。

### 例子


## 建议

### 现在该做什么

真正要修的是 state owner 的 update protocol，不是给几个出问题的 call site 打零散补丁。

### 为什么重要

不变量比“write 不报错”强得多：

> **任何已经 accepted 的 intent，除非有明确 domain rule supersede 或 merge，否则都不能静默消失。**

### 修复策略

先选符合 domain 的 ownership model。

如果天然只有一个 authority，就用 single writer。所有 command 进入这个 owner，由它串行 state transition。很多时候这比让每个 caller 都学一套 distributed conflict protocol 更简单，也更诚实。

如果多个 writer 本来就合法，就把 read 时看到的 version/etag/revision 一直带到 commit，用 atomic compare-and-swap。Stale writer 必须得到显式 conflict，重新读 current state，再重新计算 intent。不要“原样 retry”旧 payload；那份 computation 是在旧世界里成立的。

如果并发 intent 本来就能组合，就定义真正针对 **intent / fact** 的 merge law，而不是拿两个 replacement object 做 heuristic field merge。需要 associative / commutative / idempotent 时把这些性质说清楚，并在 arrival permutation 下验证。

### 决策分支


### 常见假修复

常见假修复：

- 只锁最后的 `write()`，read 仍然并发；
- unconditional replacement 失败后无限 retry；
- 加 timestamp，把 last-write-wins 叫 conflict resolution，但业务从没说 wall-clock 更晚者拥有覆盖权；
- field-by-field merge 没有 ownership rule，结果仍能丢掉另一 writer 合法改过的 field；
- storage 接受 stale write 后照样返回 OK，再指望 audit log 解释“为什么数据没了”；
- 只用本进程 mutex，但其他进程/host 仍在写同一 durable record。

### 验证

验证时必须**强制制造真实 conflict**。让两个 writer 明确读到同一 revision，暂停它们；先 commit A，再释放 B。断言 B 不可能静默抹掉 A。真实 topology 若跨 process/storage，也要在那一层做同样实验，而不是只测两个 Promise“碰巧并行”。

还要测 recovery/retry：B 被 reject 后，必须基于新 state 重新计算，不能 replay stale derived bytes；如果走 merge，就 permutation arrival order，证明 merge invariant。

### 完成条件

完成时，每个 write 都应能清楚回答至少一个问题：

- **Which version justified me?**
- **Who serialized me?**
- **What merge law makes my stale premise safe?**

如果答案只是“数据库收下了”，缺陷仍然存在。Storage acceptance 只能证明字节写进去了，不能证明这次覆盖在因果上有资格发生。

### Nudge

> 从 version N 推导出的 write，除非有明确 merge law，否则没有资格直接提交到 N+1。
