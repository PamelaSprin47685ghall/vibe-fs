# blocking-event-loop — Main

把“等待”与“占着 loop”拆开。

I/O 型慢工作用真正 non-blocking API，让 pending 期间 event-loop thread 能继续调度其他 task。CPU-heavy 或不可避免的 blocking work 放进**有明确容量上限的 worker boundary**，loop 只提交工作并接收 completion。

不要只是给 blocking function 套一层 `async`。`async` keyword 不会自动搬走执行线程；只要 sync read/process wait/CPU loop 仍在同一个 executor 上，它就照样阻塞。

修复时保留原语义：

- cancellation 必须能穿过 worker boundary；
- error 要映射回原 owning operation；
- context/identity 不得丢；
- worker queue 必须有 capacity，不能为了救 event loop 又制造 `unbounded-fanout`；
- ordering 若有真实 protocol 语义，不能因 offload 被破坏；
- shutdown 必须能等待/终止 worker-owned work。

常见假修复：

- timeout 调大，让 blocked loop 更久才被承认有问题；
- 在 loop 里间歇 `sleep(0)` / tiny delay，假装“yield”解决了长 CPU job；
- `Promise.resolve().then(() => blockingCall())`，仍在同一 loop；
- 每次 blocking call 都 spawn 一个无界 thread/process；
- 把 CPU work 放 worker，却在 loop 上同步等待 worker 结果；
- offload 后忘记 cancel，caller 已结束，worker 继续吃资源；
- 只测 slow request 自己最终成功，没有观察 unrelated work 是否还能及时 progress。

验证必须同时制造一个慢路径和一个无关快路径。慢路径 pending 时，快路径的 latency/heartbeat/cancellation 应保持在 service budget 内；否则 loop 仍被某处独占。

CPU-heavy work 要用 worst-case 或代表性大输入测，不要拿一个 tiny fixture 证明 “通常很快”。若某段纯计算确实 bounded 且明显低于 loop budget，就让它留在 loop；不要为了形式主义增加 worker hop。

还要确认 worker capacity 与 backpressure。Event loop 不阻塞，但如果它能无限向 worker 队列塞任务，系统只是把崩溃点从 scheduler 搬到了 memory/queue。

完成时，慢 operation 可以自己慢，却不能让**与它没有因果依赖的工作也跟着停止进展**。

> Liveness 的边界很简单：谁在等待世界，谁就应该把共享 progress engine 还给世界。