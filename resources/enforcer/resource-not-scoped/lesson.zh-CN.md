# resource-not-scoped — Lesson

## 评价

### 定义

Resource 没有 scoped，不是因为“忘了某个 close”这么简单，而是 acquire 产生了一笔 lifetime obligation，系统却把它的结束交给后续 control flow 和记忆。

打开 file、启动 process、借 connection、subscribe stream、创建 temp worktree、分配 session、拿 terminal、持有 lease——这些动作拿到的不只是一个 value，还创建了一个**时间上的责任**：从现在开始谁拥有它？这个 ownership 在什么事件上结束？谁保证只结束一次？

### 支配原则

关键不是“短不短”，而是**ownership duration 是否明确并被结构保证**。

GC/finalizer 可以当 defensive backstop，但对 file lock、process、socket、permit、worktree、subscription 这类 scarce / externally visible resource，通常不能充当 primary semantic owner。“以后 GC 会收”不是 lifecycle contract。

### 何时触发

以下情形触发：

- cleanup 分散在多个 return/exception branch 手工调用；
- handle 可以 escape，但 type/API 看不出谁变成新 releaser；
- process/session/worktree 在一个模块 create，另一个地方“以后会清”；
- exception/cancellation 只靠 best-effort cleanup，而且 cleanup 与 acquisition 没有结构绑定；
- event subscription/listener 没有可见 unsubscribe lifetime；
- temp file/dir 因一条失败路径提前 return 而残留；
- test 依赖 global teardown sweep，因为 local owner 不值得信任。

### 不应触发

不要因为 lifetime 很长就触发。Process-wide resource 可以由 process shutdown 正式拥有；pool 可以长期拥有 connection，而 caller 只拥有短 lease；background workflow 可以合法拥有比 initiating request 更长的 session。

### 与相邻规则区分

邻近规则：

- `cancellation-not-propagated`：owner cancel 了，child work 还活着；
- `permit-leak`：漏掉的是有限 concurrency capacity；
- `leftover-scaffolding` / `spike-not-cleaned`：临时工程 artifact 残留，不一定是 runtime lifetime 没 scoped。

### 判定程序

最好的诊断问题：

> 只看 syntax/API，我能不能直接知道这个 resource 现在归谁，以及什么事件结束这份 ownership？

如果必须脑内追完所有 return、记住 comment、或者相信某个远处 shutdown sweep “大概会找到它”，lifetime 就没有被真正建模。

### 例子

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

## 建议

### 现在该做什么

把 lifetime 变成结构。

让 acquire 与 release 落在同一个 owning construct 里：`using`、`defer`、bracket、`try/finally`、scoped lease、disposable、task group、context manager，或者任何能保证**所有 exit**都释放的机制，而不是只覆盖作者当时想到的几条路径。

核心不变量：

> **每次 acquisition 只创建一份 ownership obligation；结构要么把它完整 discharge，要么显式 transfer。**

Resource 尽量留在真正 owner 的最小 scope 内。一个从不 escape 的 handle，比一个穿过五层、靠口头约定“谁最后负责 close”的 handle 好推理得多。

### 为什么重要

Ownership 必须移动时，让 transfer 可见。常见健康形状：

- move/linear：sender transfer 后不能再 use/release；
- lease：receiver 暂时拥有使用权，pool/parent 仍拥有 underlying resource；
- ref-count/shared lifetime：明确最后一个 owner 何时释放；
- durable workflow ownership：session/job/resource 归一个命名、更长寿的 principal。

不要用“return handle，然后文档写 caller 记得 close”模拟 transfer。如果这真是 contract，就把它写进 type/API 并测试。

### 修复策略

Nested lifetime 也要认真处理。A create B，B create C 时，teardown 通常应按反 ownership 顺序 C→B→A。Structured scope 会自然暴露这个顺序；散落 cleanup 很容易在 shutdown 时反过来制造 race。

### 决策分支

验证要强制覆盖所有 exit class：

- normal success；
- early return；
- exception；
- cancellation；
- timeout；
- partial initialization（第 N 个 acquire 成功，第 N+1 个失败）；
- ownership transfer；
- repeated cleanup / idempotent disposal（若 API 允许）。

### 常见假修复

常见假修复：

- 给目前已知每个 `return` branch 补 `close()`；
- 安装 global shutdown sweep 给 local leak 擦屁股；
- 对 socket/process/file lock/worktree/subscription/scarce handle 主要依赖 GC/finalizer；
- swallow dispose error，让测试看上去 clean；
- cleanup 放在一个 cancellation 后可能根本不执行的 callback；
- 因为 local scope 没 ownership，就维护一个 global live-resource registry；
- 所有东西塞给 singleton，最后看不出哪个 request/workflow 让 resource 存在。

### 验证

能测真实 consequence 时，不要只断言“Dispose 被调用”。看 file descriptor、process、subscription、worktree、temp dir、session、permit 是否真的消失；async teardown 尤其如此。

### 完成条件

完成时 reviewer 应能从 local structure 与 explicit transfer 直接推断完整 lifetime，不需要对所有 branch 做控制流证明，也不需要 repository-wide 搜索配对 `close()`。

> Acquire 与 release 不是两个无关调用，而是一条 ownership fact 的 opening edge 与 closing edge。

### Nudge

> Resource correctness 包含**它什么时候停止存在**。如果 release 仍是 caller 的记忆测试，ownership 就写得不够强。
