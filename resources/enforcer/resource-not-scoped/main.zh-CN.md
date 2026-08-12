# resource-not-scoped — Main

把 lifetime 变成结构。

让 acquire 与 release 落在同一个 owning construct 里：`using`、`defer`、bracket、`try/finally`、scoped lease、disposable、task group、context manager，或者任何能保证**所有 exit**都释放的机制，而不是只覆盖作者当时想到的几条路径。

核心不变量：

> **每次 acquisition 只创建一份 ownership obligation；结构要么把它完整 discharge，要么显式 transfer。**

Resource 尽量留在真正 owner 的最小 scope 内。一个从不 escape 的 handle，比一个穿过五层、靠口头约定“谁最后负责 close”的 handle 好推理得多。

Ownership 必须移动时，让 transfer 可见。常见健康形状：

- move/linear：sender transfer 后不能再 use/release；
- lease：receiver 暂时拥有使用权，pool/parent 仍拥有 underlying resource；
- ref-count/shared lifetime：明确最后一个 owner 何时释放；
- durable workflow ownership：session/job/resource 归一个命名、更长寿的 principal。

不要用“return handle，然后文档写 caller 记得 close”模拟 transfer。如果这真是 contract，就把它写进 type/API 并测试。

常见假修复：

- 给目前已知每个 `return` branch 补 `close()`；
- 安装 global shutdown sweep 给 local leak 擦屁股；
- 对 socket/process/file lock/worktree/subscription/scarce handle 主要依赖 GC/finalizer；
- swallow dispose error，让测试看上去 clean；
- cleanup 放在一个 cancellation 后可能根本不执行的 callback；
- 因为 local scope 没 ownership，就维护一个 global live-resource registry；
- 所有东西塞给 singleton，最后看不出哪个 request/workflow 让 resource 存在。

Nested lifetime 也要认真处理。A create B，B create C 时，teardown 通常应按反 ownership 顺序 C→B→A。Structured scope 会自然暴露这个顺序；散落 cleanup 很容易在 shutdown 时反过来制造 race。

验证要强制覆盖所有 exit class：

- normal success；
- early return；
- exception；
- cancellation；
- timeout；
- partial initialization（第 N 个 acquire 成功，第 N+1 个失败）；
- ownership transfer；
- repeated cleanup / idempotent disposal（若 API 允许）。

能测真实 consequence 时，不要只断言“Dispose 被调用”。看 file descriptor、process、subscription、worktree、temp dir、session、permit 是否真的消失；async teardown 尤其如此。

完成时 reviewer 应能从 local structure 与 explicit transfer 直接推断完整 lifetime，不需要对所有 branch 做控制流证明，也不需要 repository-wide 搜索配对 `close()`。

> Acquire 与 release 不是两个无关调用，而是一条 ownership fact 的 opening edge 与 closing edge。