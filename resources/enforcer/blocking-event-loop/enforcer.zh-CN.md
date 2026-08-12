# blocking-event-loop — Enforcer

Blocking event loop 的问题，不是“某个 operation 很慢”，而是一个 task 在等待自己并不需要独占的事情时，仍然握着**所有其他 task 共同依赖的 progress engine**。

Event loop 的并发模型有一条隐含契约：callback 可以短暂借用 thread，但只要进入慢 I/O、process wait、sleep、lock wait、长 CPU loop，就应该把 thread 还给 scheduler。违反这条契约，一次局部等待会被放大成全局 head-of-line blocking。

五秒同步等待不是“这个 request 慢五秒”，而是五秒内所有排在同一 loop 后面的无关工作都失去进展机会。

以下情形触发：

- shared event-loop/UI/reactor/hook thread 上调用 sync filesystem/network/process API；
- 在 loop 上 `sleep` 或 busy wait；
- CPU-heavy parsing/compression/hash/large transform 长时间占着 turn；
- 等一个 lock/condition，但等待期间 thread 不能调度那个最终会解除条件的 work；
- 函数表面 `async`，内部仍直接调用 blocking API；
- slow child process 用同步 wait，导致 cancel/heartbeat/other requests 都无法处理；
- 一个 plugin hook 的长工作让 Host 其他 session 一起停顿。

不要因为 callback 做了计算就报警。几微秒、worst-case 有明确 service budget 的 bounded computation 完全正常。Native non-blocking I/O 也正常：pending 时 thread 已归还。CPU/blocking work 若已放到 bounded worker，loop 只负责 dispatch/completion，也不是本规则。

与 `serial-when-parallel` 区分：后者是独立 work 被无谓串行，浪费 latency；本规则更严重——一个 operation 正在**霸占共享 scheduler**，让本来无关的 work 都无法前进。`sleep-based-synchronization` 管 sleep 被当 causal proof；如果 sleep 同时发生在 event loop，两条都可能成立。

判定时先问：

1. 这段代码运行在哪个 executor/thread？
2. 还有哪些 unrelated operation 依赖它及时归还？
3. 这段工作 worst-case 多久？
4. pending 时 thread 是否真正释放？

如果一个不受严格上限的 wait/CPU work 仍占着共享 loop，liveness 就交给了单个 callback。

> Event loop 是交通枢纽，不是工地。借它完成调度，然后尽快把路还给其他人。