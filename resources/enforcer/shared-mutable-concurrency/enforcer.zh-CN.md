# shared-mutable-concurrency — Enforcer

Shared mutable concurrency 不是“系统里有多个 thread”。它是一种更具体、更危险的架构决定：让多个 execution context 同时拥有**修改同一份 semantic state 的权力**，然后靠 lock、atomic、时序纪律或约定把正确性重新拼回来。

真正的问题是 sovereignty 被共享了。

Mutex 只能证明两段 instruction 没同时跑。它不会告诉你哪些 field 共同组成一个 invariant、哪个 operation 才有资格改变状态、两个 lock 是否安全组合、callback 在 cancellation 之后是否仍有写权、未来新增路径会不会忘记同一套 lock 纪律。

所以 lock-heavy 设计很容易长成口传心授：

```text
先拿 A 再拿 B。
但 callback C 例外。
x 归 A 保护，除非同时碰 y。
read 不加锁，因为“只是读”。
拿着 A 时别调 D，因为 D 可能拿 B。
```

到这一步，并发模型已经不在 domain model 里了，而在团队记忆里。

以下情形触发：

- 多个 handler/worker 可以直接 mutate 同一个 domain object / durable projection；
- 一个业务 invariant 是否成立，取决于所有 caller 是否用同样方式拿锁；
- compound state 被拆成多个 atomic field，代码却因为“每个 field 都 atomic”就假设 snapshot 一致；
- lock ordering 自己长成第二套 architecture；
- callback 持有 mutable reference，在逻辑 owner 已离开后仍可能写；
- 普通 domain transition 要靠 scheduler 运气或超大 critical section 才能测试稳定。

不要因为看到 lock 就触发。窄范围 concurrent queue、atomic counter、immutable snapshot cache、或者围绕一个低层共享资源的明确 mutex，都可能完全正确。真正要问的是：**这个 lock 是否在承担本该属于某个 semantic owner 的 domain authority？**

邻近规则要分清：`lost-update` 是 stale replacement 抹掉 accepted write 的具体损坏；`race-first-wins-semantics` 是 scheduler timing 决定业务真相；`permit-leak` 是 capacity 没归还。本规则更广：多个 actor 同时对同一 mutable domain state 拥有主权。

一个很好的诊断问题：

> 如果删掉所有 lock comment，我还能不能指出一个组件说：“只有它有权改变这份状态”？

如果答案是“不能，正确性来自所有 caller 都记得同一套锁约定”，说明 invariant 被分散给了太多人。

优先修法通常是把 mutation 收回一个 semantic owner：actor、serialized command processor、aggregate、state machine，或者其他能够逐个接收 command 并按 domain law 改状态的边界。并发发生在**owner 之间**，而不是多个 owner 一起伸手改同一个世界。

这不是反锁宗教。OS、library、极小的 performance-critical structure 有时就是需要 lock。规则反对的是把 synchronization 当 ownership 的替代品。

> Lock 可以挡住另一只手，但它永远不能回答：到底谁才有资格伸手改这个东西。
