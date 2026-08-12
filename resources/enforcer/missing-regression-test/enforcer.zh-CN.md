# missing-regression-test — Enforcer

Missing regression test 的意思，不是“bug fix 后少了一个 test 文件”，而是团队已经付出成本发现了一个真实 defect，修了代码，却没有把这份**关于 reachable bad behavior 的新知识**留进 repository。

Bug report 不是一张改代码工单，而是一个 counterexample：系统 state space 里存在一个过去没人预料、没人编码的坏世界。Implementation fix 只消掉今天的 symptom；regression test 才把这次发现变成项目记忆。

以下情形触发：

- concrete production/test/user-reported defect 修了，却没有 executable scenario 重现它；
- 新 test 虽然跑到 repaired code，但 old buggy behavior 下也会 green；
- test 断言 fix 新增的 internal detail，而不是用户真正看到的 failure；
- bug 依赖 timezone、stale version、cancellation race、malformed input、duplicate delivery、migration state 等 boundary condition，但 suite 仍没有该条件；
- incident postmortem 写得很完整，却没有 test/property/canary 阻止同一 mechanism 回来；
- manual repro 用过一次验证 fix，之后就丢了。

如果已有 test 本来就抓到了 defect 并继续留在 suite，不触发；它已经是 regression memory。Documentation-only error 或纯 operational misconfiguration 若不属于 product behavior，也不要机械造 regression，除非产品真的新增了防复发行为。

与 `failure-path-untested` 最大区别是 provenance：本规则起点是**已经知道的 concrete defect**。`failure-path-untested` 可以在事故前触发，因为 failure policy 从未被执行。Known bug 即使以前“有 coverage”，仍值得 regression，因为事实已经证明原 coverage 没能区分这个缺陷。

一条强 regression test 至少有三点：

1. 通过真正 owning behavioral boundary 重现 original failure；
2. old mechanism 下因同一个 material reason 失败；
3. internal refactor 后仍有意义，因为它保护的是 promise，不是 patch shape。

最好的 regression 往往比事故现场小。可以删除无关 environment noise，但不能把真正 causal ingredient 一起简化掉。

Concurrency/nondeterministic incident 不要写“跑 1000 次看看 race 会不会来”的 flaky stress test，应通过 barrier/fake clock/controlled ordering 固定 causal schedule。

如果是 property bug，shrunk counterexample 可以既保留为 concrete regression，也用来强化 general property/generator。

决定性检查：临时恢复或模拟 old defect。新 test 如果仍 green，它就不是 regression memory，只是事后 ceremony。

> 一个只改变了代码、却没改变 repository executable knowledge 的 bug，只是在等第二次变贵。