# missing-regression-test — Lesson

## 评价

### 定义

Missing regression test 的意思，不是“bug fix 后少了一个 test 文件”，而是团队已经付出成本发现了一个真实 defect，修了代码，却没有把这份**关于 reachable bad behavior 的新知识**留进 repository。

Bug report 不是一张改代码工单，而是一个 counterexample：系统 state space 里存在一个过去没人预料、没人编码的坏世界。Implementation fix 只消掉今天的 symptom；regression test 才把这次发现变成项目记忆。

### 支配原则

与 `failure-path-untested` 最大区别是 provenance：本规则起点是**已经知道的 concrete defect**。`failure-path-untested` 可以在事故前触发，因为 failure policy 从未被执行。Known bug 即使以前“有 coverage”，仍值得 regression，因为事实已经证明原 coverage 没能区分这个缺陷。

### 何时触发

以下情形触发：

- concrete production/test/user-reported defect 修了，却没有 executable scenario 重现它；
- 新 test 虽然跑到 repaired code，但 old buggy behavior 下也会 green；
- test 断言 fix 新增的 internal detail，而不是用户真正看到的 failure；
- bug 依赖 timezone、stale version、cancellation race、malformed input、duplicate delivery、migration state 等 boundary condition，但 suite 仍没有该条件；
- incident postmortem 写得很完整，却没有 test/property/canary 阻止同一 mechanism 回来；
- manual repro 用过一次验证 fix，之后就丢了。

### 不应触发

如果已有 test 本来就抓到了 defect 并继续留在 suite，不触发；它已经是 regression memory。Documentation-only error 或纯 operational misconfiguration 若不属于 product behavior，也不要机械造 regression，除非产品真的新增了防复发行为。

### 与相邻规则区分


### 判定程序

一条强 regression test 至少有三点：

1. 通过真正 owning behavioral boundary 重现 original failure；
2. old mechanism 下因同一个 material reason 失败；
3. internal refactor 后仍有意义，因为它保护的是 promise，不是 patch shape。

最好的 regression 往往比事故现场小。可以删除无关 environment noise，但不能把真正 causal ingredient 一起简化掉。

Concurrency/nondeterministic incident 不要写“跑 1000 次看看 race 会不会来”的 flaky stress test，应通过 barrier/fake clock/controlled ordering 固定 causal schedule。

如果是 property bug，shrunk counterexample 可以既保留为 concrete regression，也用来强化 general property/generator。

决定性检查：临时恢复或模拟 old defect。新 test 如果仍 green，它就不是 regression memory，只是事后 ceremony。

### 例子


## 建议

### 现在该做什么

把 defect 变成 executable memory。

重现仍包含 original failure causal ingredient 的最小 scenario，通过真正 owning behavioral boundary 进入，并断言 caller-visible wrong/right outcome。

### 为什么重要

不要从 patch 开始，要从 incident 开始：

```text
当时有哪些条件？
本来应该发生什么？
实际上发生了什么？
哪个 observable 能区分二者？
```

保留 cause，不保留 noise。真实 incident 可能有巨大 log、多 service、timing、无关 retry、历史 residue。只有识别出哪些 fact load-bearing 后才能删 noise。一个很小但已经不再重现 cause 的 test，比稍大但 faithful 的 test 更差。

### 修复策略

然后证明 test 真能抓 old behavior。Production 已修好时，可以临时 revert/mutate 对应 mechanism，或在 controlled branch 构造旧 outcome。直到 old defect 能把 test 打红，regression 才算成立。

Boundary defect 就把 regression 放在失败的 boundary；serializer/protocol bug 把 incompatible bytes/identity 作为 fixture；recovery bug 在真实 transition fault/crash；concurrency bug 用 barrier 固定 causal order；timezone bug freeze exact instant/zone；duplicate-delivery bug 用同 identity replay。

如果 defect 暴露更广的 law，concrete regression 与 strengthened property/contract 都要留。Concrete counterexample 记录人类真正付过成本学到的事实，property 保护周围 input space。

### 决策分支


### 常见假修复

常见假修复：

- 新 test 只 call fix 新增的 helper，old code 根本没法跑这条 test；
- 断言新 field/type 存在，而不是用户丢失的 behavior；
- snapshot repaired internal structure，不看 bug 的 public consequence；
- input 看起来类似，却漏掉 stale/cancelled/duplicate/versioned 等真正 trigger；
- 用 comment / issue link 代替 executable memory；
- ticket 里留 manual repro command，suite 不接管；
- 写 stress loop 偶尔碰 old race，而不是 deterministic control schedule。

### 验证

验证很直接：old defect red，repair green；再做 semantics-preserving refactor，确保 test 不依赖 patch decomposition。

### 完成条件

完成时，同一个 material failure 只要被重新引入，就必定在 delivery 前打红 test。

> Fix 修今天的代码；regression test 修项目的记忆。

### Nudge

> 一个只改变了代码、却没改变 repository executable knowledge 的 bug，只是在等第二次变贵。
