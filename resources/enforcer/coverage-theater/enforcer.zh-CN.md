# coverage-theater — Enforcer

## 定义
Coverage theater 的本质是：**把“代码被执行过”包装成“行为被证明过”。**

Line 被走到、branch counter 增长、文件出现在报告里，都只能说明 execution 到过那里。它们不能证明结果正确。

Coverage 本身不是罪。它是一张很有用的侦察地图。病灶出现在 reachability metric 开始冒充 behavioral evidence 的那一刻。

## 支配原则
一个 test 的价值，在于它让某些“看起来也能跑”的错误实现变得不可接受。

如果把返回 ID 对调、吞掉 error、反转 ordering、绕过 authorization、写错 state transition，test 仍然 green，那么相关 lines 即使 100% covered，也没有证明这些 property。

现代 dashboard 让这种自欺尤其诱人，因为它提供非常精确的数字。`94.7%` 看起来像科学。但对错误 quantity 的高精度，仍然只是无知。Line coverage 能告诉你 execution 去过哪里，不能告诉你这趟旅程有没有意义。

## 何时触发
当 coverage / traversal 被当成 correctness 的主要证据，而 assertions 无法区分现实中可能发生的 defect 时触发。常见形式：

- 每个 method 都调用了，但只 assert “not null”“defined”“did not throw”；
- snapshot 大到没人能说清哪些字段改变应该让 test fail；
- mock 验证“调用发生了”，却从未检查 caller-visible outcome；
- 两个 branch 都走到了，却没断言 branch-specific invariant；
- coverage threshold 驱动大量低信息 test，唯一目的就是把 line 染绿；
- 把一个重要结果改坏，suite 仍然 green，而 coverage 几乎不变。

## 不应触发
- Coverage 只用于发现 unvisited risk，而真正 behavior 已由可证伪 assertion 独立保护。
- Smoke test 明确只证明“process 能启动”或“endpoint 有响应”，且没有人把它夸大成更深语义。
- Property test 经过很多 branches，但最终断言的是一个真实 invariant。
- 一个窄 test 只覆盖少量 lines，却恰好保护当前 change 真正威胁的 public behavior。

## 与相邻规则区分
`false-gate` 是 green 与 advertised property 在结构上脱节。`coverage-theater` 里的 test 与 CI 可能都工作正常，问题是它们问的问题太贫乏。

`test-implementation-coupled` 往往 assertion 很多，但保护的是 private choreography，而不是有价值的 behavior。`weakened-test-to-pass` 则是原本有意义的问题，在 implementation 失败后被人为削弱。

## 判定程序
对每一个被拿来当 evidence 的 test，问：

> 请说出一个现实可发生、且与当前行为相关的 defect，它会让这个 test fail。

如果答案只是“这行不会执行”“mock 不会被叫”“coverage 会下降”，继续追问：到底哪个 caller-visible result 或 invariant 被拒绝了？

然后做 mental mutation：对调 ID、丢 error、反转顺序、跳 authorization、返回 stale state。Test 仍绿，就说明 execution count 没有证明那个 property。

## 例子
- positive：parser 所有 branches 都执行，但唯一 assertion 是 `result !== undefined`；malformed input 被静默接受，coverage 仍 100%。
- positive：service test snapshot 500 行 object；reviewer 每次直接更新整份 snapshot，却说不出哪些字段是 contract。
- positive：test 只验证 `repository.save` 被调用一次，从不检查保存的是不是正确 durable state。
- near-miss：coverage 报告暴露 cancellation branch 从未访问，于是新增 test 明确断言 cancellation 阻止 publication。
- counterexample：一个很小的 contract test 覆盖 implementation 很少，却能在 authorization、identity、result semantics 出错时稳定打红。

## Nudge
Coverage 告诉你手电筒照过哪里。

Verification 问的是：小偷经过时，你到底看不看得出来。
