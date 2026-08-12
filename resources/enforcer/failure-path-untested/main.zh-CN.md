# failure-path-untested — Main

在真正 owning boundary 上制造 failure。

除非 catch/recovery helper 本身就是 supported boundary，否则不要只把 handler 单独 call 一遍。安排最小、deterministic 的条件，让 production 自己选择 failure path，然后断言 externally meaningful contract。

每个重要 failure 都按四个问题写 test：

```text
什么失败？
之后哪个 result/state 必须成立？
哪些 cleanup/compensation 必须发生？
哪些 side effect 绝不能发生？
```

例如：

- storage commit fail → state 保持旧值、无 publication、resource release；
- provider malformed response → typed rejection、不能以错误 identity retry；
- cancellation win → child 停止、permit 归还、后来不再 mutate；
- CAS conflict → stale writer reject、accepted update 保留；
- 第二个 acquisition fail → 第一个 resource 仍然 release；
- retry exhaust → final error 暴露、没有隐藏额外 attempt。

能用 deterministic fault injection 就不要靠 timing 运气：让 fake/store port 精确在第 N 个 operation fail；可控 cancellation point；provider double 返回特定 malformed payload；barrier 强制两个 writer 形成 conflict。

常见假修复：

- 直接 call internal recovery helper，不证明 production 的真实 failure 会路由到这里；
- 只断言“返回了 error”，cleanup/state invariant 完全没看；
- mock failing dependency 过重，把 ownership/transaction boundary 一起绕掉；
- 断言 private `rollbackCalled`，而不是 rollback 真正保护的 public/durable consequence；
- 用 coverage percentage 证明 failure semantics；
- 只测任何 partial work 发生前的最早 failure，真正危险的中途 failure 不测；
- 一口气让所有 dependency 都失败，造一个不现实且无法定位的灾难场景。

如果系统真的定义了 secondary failure policy，也要测真正 material 的组合：cleanup 自己 fail、compensation unavailable、cancel 与 completion race。不要做 combinatorial apocalypse theater，但存在正式语义的地方就不能只靠想象。

Mutation verification 很有价值。暂时重放 plausible bug：skip cleanup、commit fail 后仍 advance state、swallow error、多 retry 一次、rollback 前先 publish。Test 应因真正 guarantee 被破坏而红。

如果某 failure branch construction 上根本不可能，删除或把 impossibility 编得更强，不要维护一个永远无法诚实验证的 defensive myth。

完成时，重要 failure semantics 不只是“代码看起来没问题”，而是 suite 真正见过系统失败，并证明它接下来会做什么、拒绝做什么。

> Happy path 证明系统会工作；failure test 证明系统在不再幸运时仍知道该怎么办。