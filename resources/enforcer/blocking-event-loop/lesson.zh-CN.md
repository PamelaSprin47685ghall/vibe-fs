# blocking-event-loop — Lesson

## 评价

### 定义

Blocking event loop 的问题，不是“某个 operation 很慢”，而是一个 task 在等待自己并不需要独占的事情时，仍然握着**所有其他 task 共同依赖的 progress engine**。

### 支配原则

Event loop 的并发模型有一条隐含契约：callback 可以短暂借用 thread，但只要进入慢 I/O、process wait、sleep、lock wait、长 CPU loop，就应该把 thread 还给 scheduler。违反这条契约，一次局部等待会被放大成全局 head-of-line blocking。

五秒同步等待不是“这个 request 慢五秒”，而是五秒内所有排在同一 loop 后面的无关工作都失去进展机会。

### 何时触发

以下情形触发：

- shared event-loop/UI/reactor/hook thread 上调用 sync filesystem/network/process API；
- 在 loop 上 `sleep` 或 busy wait；
- CPU-heavy parsing/compression/hash/large transform 长时间占着 turn；
- 等一个 lock/condition，但等待期间 thread 不能调度那个最终会解除条件的 work；
- 函数表面 `async`，内部仍直接调用 blocking API；
- slow child process 用同步 wait，导致 cancel/heartbeat/other requests 都无法处理；
- 一个 plugin hook 的长工作让 Host 其他 session 一起停顿。

### 不应触发

不要因为 callback 做了计算就报警。几微秒、worst-case 有明确 service budget 的 bounded computation 完全正常。Native non-blocking I/O 也正常：pending 时 thread 已归还。CPU/blocking work 若已放到 bounded worker，loop 只负责 dispatch/completion，也不是本规则。

### 与相邻规则区分

与 `serial-when-parallel` 区分：后者是独立 work 被无谓串行，浪费 latency；本规则更严重——一个 operation 正在**霸占共享 scheduler**，让本来无关的 work 都无法前进。`sleep-based-synchronization` 管 sleep 被当 causal proof；如果 sleep 同时发生在 event loop，两条都可能成立。

### 判定程序

判定时先问：

1. 这段代码运行在哪个 executor/thread？
2. 还有哪些 unrelated operation 依赖它及时归还？
3. 这段工作 worst-case 多久？
4. pending 时 thread 是否真正释放？

如果一个不受严格上限的 wait/CPU work 仍占着共享 loop，liveness 就交给了单个 callback。

### 例子

- positive：request handler 在共享 event-loop thread 上同步调用 `readFileSync` 或 `sleep`。
- near-miss：loop 上几微秒的纯 JSON parse，在实测 service budget 之内。
- counterexample：用 async I/O 等待；把 CPU-heavy 工作移到不拥有 loop 的 bounded worker。

## 建议

### 现在该做什么

把“等待”与“占着 loop”拆开。

I/O 型慢工作用真正 non-blocking API，让 pending 期间 event-loop thread 能继续调度其他 task。CPU-heavy 或不可避免的 blocking work 放进**有明确容量上限的 worker boundary**，loop 只提交工作并接收 completion。

不要只是给 blocking function 套一层 `async`。`async` keyword 不会自动搬走执行线程；只要 sync read/process wait/CPU loop 仍在同一个 executor 上，它就照样阻塞。

### 为什么重要

loop 是共享的 liveness 资源。在那里引入的 latency 会被排在它后面的每个无关 operation 放大。五秒同步等待因此不是一次五秒延迟，而是系统在这五秒内放弃了独立进展的能力。

### 修复策略

修复时保留原语义：

- cancellation 必须能穿过 worker boundary；
- error 要映射回原 owning operation；
- context/identity 不得丢；
- worker queue 必须有 capacity，不能为了救 event loop 又制造 `unbounded-fanout`；
- ordering 若有真实 protocol 语义，不能因 offload 被破坏；
- shutdown 必须能等待/终止 worker-owned work。

### 决策分支

- 如果工作是在等 I/O，用 native async API，让 loop thread 在 pending 期间被释放。
- 如果工作是 CPU-heavy 或不可避免 blocking，移到 bounded worker，loop 只当 completion dispatcher。
- 如果 `async` wrapper 仍在同一 executor 上调用 blocking API，就仍算 blocking。

### 常见假修复

常见假修复：

- timeout 调大，让 blocked loop 更久才被承认有问题；
- 在 loop 里间歇 `sleep(0)` / tiny delay，假装“yield”解决了长 CPU job；
- `Promise.resolve().then(() => blockingCall())`，仍在同一 loop；
- 每次 blocking call 都 spawn 一个无界 thread/process；
- 把 CPU work 放 worker，却在 loop 上同步等待 worker 结果；
- offload 后忘记 cancel，caller 已结束，worker 继续吃资源；
- 只测 slow request 自己最终成功，没有观察 unrelated work 是否还能及时 progress。

### 验证

验证必须同时制造一个慢路径和一个无关快路径。慢路径 pending 时，快路径的 latency/heartbeat/cancellation 应保持在 service budget 内；否则 loop 仍被某处独占。

CPU-heavy work 要用 worst-case 或代表性大输入测，不要拿一个 tiny fixture 证明 “通常很快”。若某段纯计算确实 bounded 且明显低于 loop budget，就让它留在 loop；不要为了形式主义增加 worker hop。

还要确认 worker capacity 与 backpressure。Event loop 不阻塞，但如果它能无限向 worker 队列塞任务，系统只是把崩溃点从 scheduler 搬到了 memory/queue。

### 完成条件

完成时，慢 operation 可以自己慢，却不能让**与它没有因果依赖的工作也跟着停止进展**。

> Liveness 的边界很简单：谁在等待世界，谁就应该把共享 progress engine 还给世界。

### Nudge

> Event loop 是交通枢纽，不是工地。借它完成调度，然后尽快把路还给其他人。
