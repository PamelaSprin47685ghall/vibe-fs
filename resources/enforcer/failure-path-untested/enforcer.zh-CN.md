# failure-path-untested — Enforcer

Failure path untested，不是说“error branch coverage 不够”，而是代码里已经写了 failure / cancellation / rollback / malformed input / conflict / timeout / retry / recovery 的 policy，却从来没有 test 真正制造过那个让 policy 有意义的条件。

危险之处在于 failure code 往往**看起来**特别合理：

```text
catch
  release permit
  rollback reservation
  return error
```

但真正复杂的 ownership、partial effect、cleanup order、idempotency、stale state、secondary failure 都集中在这里。这些 branch 平时最少执行，事故时却最依赖它们。

以下情形触发：

- 新 rollback branch 从未在 test 里真正走过；
- cancellation cleanup 只靠 code review 相信；
- retry 只是直接 call retry helper，没有通过真实 owning operation 的 failure 触发；
- malformed provider/wire branch 存在，却从未喂过 malformed fixture；
- partial initialization 后 cleanup 从未被故障注入验证；
- CAS/conflict path 存在，但 tests 把 writer 全串行了；
- recovery 通过直接 construct post-failure internal state “测试”，而不是制造实际 failure；
- error mapping 被单测，但 production inner/external failure 从未经过那条映射。

如果已有 test 通过同一个 owning boundary 制造 exact failure，并观察同一份 externally relevant semantics，就不要重复。Dead unreachable branch 也不要为了 coverage 造 test，直接删。Property/exhaustive test 如果真的能生成 failure condition 并断言 cleanup/state，也可以算足够 evidence。

与 `missing-regression-test` 区分：后者起点是一个**已经观察到的 concrete defect**，问 repository 有没有保留 executable memory；本规则即使事故尚未发生也能触发，只要重要 failure policy 从未被实际执行验证。

与 `coverage-theater` 也不同。某行 catch 可能 coverage=100%，但 test 根本没断言真正 guarantee。关键问题不是“branch 跑了吗”，而是：

> Test 是否主动制造这个 failure，并证明 result、cleanup、state preservation、forbidden side effects？

一条靠谱 failure test 至少写清四栏：

```text
induce: 到底什么失败？
observe: 哪个 result/state 必须成立？
cleanup: 哪些 owned resource/effect 必须 discharge？
forbid: 尽管失败，哪些事情绝不能发生？
```

真正最值钱的往往是 `forbid`：no duplicate charge、no stale publish、no leaked permit、no state advance、no second retry、no swallowed error。

> Failure handling 是 executable policy。代码从未被逼到失败，不等于 failure 已经被证明处理正确。