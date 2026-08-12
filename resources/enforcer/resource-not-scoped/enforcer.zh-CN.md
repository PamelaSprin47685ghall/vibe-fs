# resource-not-scoped — Enforcer

Resource 没有 scoped，不是因为“忘了某个 close”这么简单，而是 acquire 产生了一笔 lifetime obligation，系统却把它的结束交给后续 control flow 和记忆。

打开 file、启动 process、借 connection、subscribe stream、创建 temp worktree、分配 session、拿 terminal、持有 lease——这些动作拿到的不只是一个 value，还创建了一个**时间上的责任**：从现在开始谁拥有它？这个 ownership 在什么事件上结束？谁保证只结束一次？

最典型的坏形状：

```text
let r = acquire()
...
if something then return   // r 谁关？
...
release(r)
```

从这一刻起，每个新 branch、exception、cancellation、retry、callback、early return 都必须重新记住同一条 lifetime rule。那不是 ownership，而是 path enumeration。

Scoped lifetime 把责任写进结构：这个 owner 在这里 acquire；离开这个 scope 的任何路径都 release；如果 ownership 要逃逸，就必须显式 transfer。

以下情形触发：

- cleanup 分散在多个 return/exception branch 手工调用；
- handle 可以 escape，但 type/API 看不出谁变成新 releaser；
- process/session/worktree 在一个模块 create，另一个地方“以后会清”；
- exception/cancellation 只靠 best-effort cleanup，而且 cleanup 与 acquisition 没有结构绑定；
- event subscription/listener 没有可见 unsubscribe lifetime；
- temp file/dir 因一条失败路径提前 return 而残留；
- test 依赖 global teardown sweep，因为 local owner 不值得信任。

不要因为 lifetime 很长就触发。Process-wide resource 可以由 process shutdown 正式拥有；pool 可以长期拥有 connection，而 caller 只拥有短 lease；background workflow 可以合法拥有比 initiating request 更长的 session。关键不是“短不短”，而是**ownership duration 是否明确并被结构保证**。

GC/finalizer 可以当 defensive backstop，但对 file lock、process、socket、permit、worktree、subscription 这类 scarce / externally visible resource，通常不能充当 primary semantic owner。“以后 GC 会收”不是 lifecycle contract。

邻近规则：

- `cancellation-not-propagated`：owner cancel 了，child work 还活着；
- `permit-leak`：漏掉的是有限 concurrency capacity；
- `leftover-scaffolding` / `spike-not-cleaned`：临时工程 artifact 残留，不一定是 runtime lifetime 没 scoped。

最好的诊断问题：

> 只看 syntax/API，我能不能直接知道这个 resource 现在归谁，以及什么事件结束这份 ownership？

如果必须脑内追完所有 return、记住 comment、或者相信某个远处 shutdown sweep “大概会找到它”，lifetime 就没有被真正建模。

> Resource correctness 包含**它什么时候停止存在**。如果 release 仍是 caller 的记忆测试，ownership 就写得不够强。