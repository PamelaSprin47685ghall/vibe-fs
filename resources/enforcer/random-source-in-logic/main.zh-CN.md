# random-source-in-logic — Main

## 现在该做什么

把 entropy 从 ambient magic 变成显式 provenance。

先判断 domain 是否需要重放这次随机决定。如果需要，优先在 shell/adapter 抽样，把 chosen value 作为普通 input 传给 pure policy，并在 durable event/fact 中记录。这样 replay 不需要重新抽，也不依赖未来 RNG implementation 仍与今天完全相同。

例如：

```text
roll = rng.next()
decision = decide(command, state, roll)
append DecisionMade(..., roll)
```

或者在确实需要一连串 deterministic stochastic process 时，传入明确 seed/RNG port，并把 seed/version/consumption model 做到足以 replay。

## 为什么重要

隐藏的 randomness 打破“同样 inputs”与“同样 decision”的等价。这会破坏 deterministic tests、incident replay、event rebuilding，以及“为什么选了这条 branch 而不是那条”的解释。

## 修复策略

不要把所有随机都推入 event。Crypto nonce、TLS key、纯 UI jitter、不会改变 business fact 的 backoff jitter 可以留在各自 adapter；domain 不拥有它们的 replay obligation。

有目的地选择 replay model：要么把 sampled value 作为 fact 持久化，要么持久化/控制 seed 并使用 deterministic generator。Crypto entropy 在 domain replay 不拥有它时留在 security adapter。

## 决策分支

- 如果 domain 必须 replay 这个 decision，把 seed 或 sampled value 传进 core，并把该值随 event 持久化。
- 如果 entropy 是 cryptographic、不是业务选择，把它留在 security adapter，不进 domain policy。
- 如果测试现在 monkey-patch global RNG，换成 production 会用的同一个显式 port。

## 常见假修复

常见假修复：

- test 全局 monkey-patch `Math.random`，production core 仍直接读 ambient RNG；
- process startup 固定 seed，就声称 randomness “已经可重现”，但 seed 没成为 operation/session identity 的一部分；
- core 里生成 UUID，之后只 log 一下，recovery 仍会生成新 UUID；
- 把 RNG object 放进 global singleton，依赖 draw order 永远不变；
- event 只记录 “RandomChoiceMade”，却不记录实际 choice/seed；
- replay 时重新 sample，祈祷相同 input 得到相同结果。

## 验证

验证要从 replay 出发。给相同 declared input（包含 choice/seed）执行两次，domain output 必须一致；改变 entropy input 时，只允许那些本来就被 randomness 授权影响的 decision 变化。

再做 restart/replay：已发生的 stochastic decision 应恢复原 choice，而不是重新参加一次 lottery。若 event/history 使用 seed model，还要证明 generator version 与 consumption order 不会因 unrelated refactor 改变过去。

对于 security entropy，反而应验证它**没有**被不必要地拖入 domain replay 或持久化敏感 material。显式边界不是“所有 randomness 都记录”，而是“每种 entropy 的 owner 与 replay义务清楚”。

## 完成条件

完成时，任何改变 business history 的 stochastic choice 都能回答：这次选择的 entropy 从哪来、选择结果是什么、restart 后为什么仍是同一个结果。

> Randomness 可以决定哪条合法路径发生；系统仍然必须记得当时是哪条路径，以及为什么 replay 不该再抽一次。