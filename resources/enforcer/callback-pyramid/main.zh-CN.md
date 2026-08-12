# callback-pyramid — Main

把 continuation tree 拉平成一个 structured async lifetime。

Foreign callback API 先在 adapter edge 转成 Promise/Task/Async；之后让一个 top-level operation 负责 sequence、resource scope、cancellation、error propagation。独立分支用 named join/combinator 汇合，不要继续往更深 closure 里嵌。

目标形状是能从上到下读：

```text
acquire
await step A
await step B
await parallel join
commit
finally release
```

而不是在每个 callback 里再注册下一层 callback，并把 cleanup/error scattered 到叶子。

常见假修复：

- 只是把 inner callback 抽成 named function，隐藏 nesting 却保留相同 lifetime；
- `.then(...).then(...)` 看起来横向了，但 error/cancel/resource ownership 仍不清；
- flatten 后忘记 thread cancellation，结果 syntax 线性、physical child 仍 detached；
- 所有 error 都 catch 成 `null`，让主流程“更直”；
- 为避免 callback nesting 改成 event bus，sequence 反而进入 `implicit-control-flow`；
- 把 shared mutable flags 当 callback 之间的 continuation state。

资源要跟 top-level scope 走。Acquire 后无论哪一步 throw/cancel，release 都由同一结构保证。若某 child 真需要 outlive parent，就做显式 ownership transfer，不要因为 callback 已注册就默认 detach。

验证 success/failure/cancel 三条路径：从一个顶层 operation 能追到每个 child 的结束，并且 cleanup deterministic。故意让中间 step fail、最后 step cancel、parallel branch 一边 fail，一边 slow，确认 owner 的 policy 可读且可执行。

如果 foreign API 会同步调用 callback（re-entrancy）或多次 callback，adapter 还要把这些特殊 semantics 收敛成内部明确 contract，防止 structured async 表面线性、实际仍被外部 callback model 偷袭。

完成时，operation causal order、failure、cancellation、resource lifetime 都能在一个 lexical scope 里理解；callback 只留在真正不可避免的外部 edge。

> Flattening 的意义不是少几个缩进，而是让一段工作的整个生命重新有一个主人。