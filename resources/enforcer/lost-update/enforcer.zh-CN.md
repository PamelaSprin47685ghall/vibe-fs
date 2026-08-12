# lost-update — Enforcer

Lost update 不是“两个 write 离得太近”这么浅。它是对历史的静默篡改：两个 writer 都从 version N 做出决定，其中一个结果已经被接受，另一个 writer 却仍拿 N 时代的前提去覆盖当前状态，于是一个已经成立的事实没有经过任何显式 supersede，就消失了。

最常见的外形非常普通：

```text
read current
compute next from current
write next
```

真正危险的是这三步之间藏着一句没写出来的话：**“我读到的状态，在我提交时仍然是当前状态。”** 并发一旦存在，这句话默认就是假的，除非有 protocol 证明它仍然成立。

不要把问题简化成 “last writer wins”。如果业务明确定义“后来的 authority 覆盖之前的 authority”，last-writer-wins 可以是合法规则。Lost update 的病在于：**scheduler timing 让一个旧前提拥有了抹掉新事实的权力**，而 domain 从未作出这个决定。

以下情形应触发：

- 两个 writer 可以读同一 revision，最后各自 replace 同一份 state；
- update 从 snapshot 计算整个 replacement object，然后无条件写回；
- storage write 返回 success，但另一个已经 accepted 的变化从结果里消失；
- conflict 后只是把同一份 stale derived payload 再写一次；
- storage 明明支持 etag/version/CAS，application 却没有把 read version 带到 commit；
- 所谓 “merge” 只是挑一份完整 object，另一方改过的 field 被顺手丢掉。

不要因为“有并发”就触发。Atomic commutative update、append-only fact、真正 single-writer owner、或有数学语义的 merge law，都可能合法并发而不丢 update。

邻近规则要分清：`shared-mutable-concurrency` 管多个执行体共享 write authority 的架构；`race-first-wins-semantics` 管 arrival order 决定业务真相；`optimistic-retry-assumption` 管上一次 effect 结果未知却乐观 retry。本规则只在这一刀最准确时使用：**一个已接受的 update，会因为另一个 stale writer 成功提交而消失。**

最有力的判定实验很简单：让 A、B 同时读 version N；先让 A commit；再让 B 提交它基于旧 N 算出的结果。此时只能有三种合法结局：

1. B 被判 stale，必须 re-read；
2. single writer 让 B 根本没有真正从 stale state 提交；
3. 明确 merge law 同时保住两边 intent。

如果 B 能成功返回，而 A 的已接受信息静默消失，系统就在不承认的情况下重写历史。

修复也不是“加个锁”。只锁最后的 `write()` 没用；stale premise 早在 read 时就形成了。Protocol 必须把 **read identity 与 commit identity** 绑定在同一个 logical update 上。

> 从 version N 推导出的 write，除非有明确 merge law，否则没有资格直接提交到 N+1。
