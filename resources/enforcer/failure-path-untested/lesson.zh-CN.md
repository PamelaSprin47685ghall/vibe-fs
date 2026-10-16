# failure-path-untested — Lesson

## 评价

### 定义

Failure path untested，不是说“error branch coverage 不够”，而是代码里已经写了 failure / cancellation / rollback / malformed input / conflict / timeout / retry / recovery 的 policy，却从来没有 test 真正制造过那个让 policy 有意义的条件。

### 支配原则

危险之处在于 failure code 往往**看起来**特别合理：

```text
catch
  release permit
  rollback reservation
  return error
```

但真正复杂的 ownership、partial effect、cleanup order、idempotency、stale state、secondary failure 都集中在这里。这些 branch 平时最少执行，事故时却最依赖它们。

### 何时触发

以下情形触发：

- 新 rollback branch 从未在 test 里真正走过；
- cancellation cleanup 只靠 code review 相信；
- retry 只是直接 call retry helper，没有通过真实 owning operation 的 failure 触发；
- malformed provider/wire branch 存在，却从未喂过 malformed fixture；
- partial initialization 后 cleanup 从未被故障注入验证；
- CAS/conflict path 存在，但 tests 把 writer 全串行了；
- recovery 通过直接 construct post-failure internal state “测试”，而不是制造实际 failure；
- error mapping 被单测，但 production inner/external failure 从未经过那条映射。

### 不应触发

如果已有 test 通过同一个 owning boundary 制造 exact failure，并观察同一份 externally relevant semantics，就不要重复。Dead unreachable branch 也不要为了 coverage 造 test，直接删。Property/exhaustive test 如果真的能生成 failure condition 并断言 cleanup/state，也可以算足够 evidence。

### 与相邻规则区分

与 `missing-regression-test` 区分：后者起点是一个**已经观察到的 concrete defect**，问 repository 有没有保留 executable memory；本规则即使事故尚未发生也能触发，只要重要 failure policy 从未被实际执行验证。

与 `coverage-theater` 也不同。某行 catch 可能 coverage=100%，但 test 根本没断言真正 guarantee。关键问题不是“branch 跑了吗”，而是：

> Test 是否主动制造这个 failure，并证明 result、cleanup、state preservation、forbidden side effects？

### 判定程序

一条靠谱 failure test 至少写清四栏：

```text
induce: 到底什么失败？
observe: 哪个 result/state 必须成立？
cleanup: 哪些 owned resource/effect 必须 discharge？
forbid: 尽管失败，哪些事情绝不能发生？
```

真正最值钱的往往是 `forbid`：no duplicate charge、no stale publish、no leaked permit、no state advance、no second retry、no swallowed error。

### 例子


## 建议

### 现在该做什么

在真正 owning boundary 上制造 failure。

除非 catch/recovery helper 本身就是 supported boundary，否则不要只把 handler 单独 call 一遍。安排最小、deterministic 的条件，让 production 自己选择 failure path，然后断言 externally meaningful contract。

### 为什么重要

每个重要 failure 都按四个问题写 test：

```text
什么失败？
之后哪个 result/state 必须成立？
哪些 cleanup/compensation 必须发生？
哪些 side effect 绝不能发生？
```

### 修复策略

能用 deterministic fault injection 就不要靠 timing 运气：让 fake/store port 精确在第 N 个 operation fail；可控 cancellation point；provider double 返回特定 malformed payload；barrier 强制两个 writer 形成 conflict。

### 决策分支

如果系统真的定义了 secondary failure policy，也要测真正 material 的组合：cleanup 自己 fail、compensation unavailable、cancel 与 completion race。不要做 combinatorial apocalypse theater，但存在正式语义的地方就不能只靠想象。

如果某 failure branch construction 上根本不可能，删除或把 impossibility 编得更强，不要维护一个永远无法诚实验证的 defensive myth。

### 常见假修复

常见假修复：

- 直接 call internal recovery helper，不证明 production 的真实 failure 会路由到这里；
- 只断言“返回了 error”，cleanup/state invariant 完全没看；
- mock failing dependency 过重，把 ownership/transaction boundary 一起绕掉；
- 断言 private `rollbackCalled`，而不是 rollback 真正保护的 public/durable consequence；
- 用 coverage percentage 证明 failure semantics；
- 只测任何 partial work 发生前的最早 failure，真正危险的中途 failure 不测；
- 一口气让所有 dependency 都失败，造一个不现实且无法定位的灾难场景。

### 验证

例如：

- storage commit fail → state 保持旧值、无 publication、resource release；
- provider malformed response → typed rejection、不能以错误 identity retry；
- cancellation win → child 停止、permit 归还、后来不再 mutate；
- CAS conflict → stale writer reject、accepted update 保留；
- 第二个 acquisition fail → 第一个 resource 仍然 release；
- retry exhaust → final error 暴露、没有隐藏额外 attempt。

Mutation verification 很有价值。暂时重放 plausible bug：skip cleanup、commit fail 后仍 advance state、swallow error、多 retry 一次、rollback 前先 publish。Test 应因真正 guarantee 被破坏而红。

### 完成条件

完成时，重要 failure semantics 不只是“代码看起来没问题”，而是 suite 真正见过系统失败，并证明它接下来会做什么、拒绝做什么。

> Happy path 证明系统会工作；failure test 证明系统在不再幸运时仍知道该怎么办。

### Nudge

> Failure handling 是 executable policy。代码从未被逼到失败，不等于 failure 已经被证明处理正确。
