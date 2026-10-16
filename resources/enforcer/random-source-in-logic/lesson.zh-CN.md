# random-source-in-logic — Lesson

## 评价

### 定义

Random source 藏在 logic 里的问题，不是“系统不该随机”，而是 entropy 成了**没有出现在 decision signature 里的输入**。

同样的 visible state + command，domain core 内部偷偷 `random()` / `uuid4()` / entropy draw，于是两个 replay 可以产生不同 event，却没有任何 recorded fact 能解释差异来自哪里。系统最终只能说：“当时随机到了这个。”——但 seed、sample、source 都没留下。

Randomness 本来就是 input，只是 ambient API 把它伪装成魔法。

### 支配原则

Randomness 仍然是 input。内部采样它的函数只是藏起了输入通道：同样的 visible state 可以导致不同 event，却没有可复现的解释。对 simulation、allocation、game、sampling、tie-breaking，replayability 要求保留生成的 choice，或保留能确定性推出该 choice 的 seed/source。

### 何时触发

以下情形触发：

- domain policy 直接调用 RNG 决定 winner、allocation、sampling、tie-break、shard、variant；
- event replay 会重新抽一次随机数；
- UUID/nonce 被拿来决定业务 identity/order，却只在 core 内现生成；
- test 必须 monkey-patch global RNG 才能稳定；
- incident 无法回答“为什么这次选 A 不选 B”，因为 random choice 没 provenance；
- retry/recovery 重新运行 logic 时可能抽到不同选择，改变已经发生过的业务路径。

### 不应触发

不要误杀所有 entropy。Cryptographic key/nonce generation 如果属于 security adapter，domain replay 根本不要求重现那次随机位串，就可以留在边界。UI-only jitter、backoff jitter 若不改变 business fact，也不必塞进 domain event。

真正区分标准是：**这个随机结果是否参与了需要 replay、解释、持久化的业务决定？** 如果参与，就必须让它成为可见输入或 durable fact。

### 与相邻规则区分

与 `time-source-in-logic` 同族：一个藏 clock，一个藏 entropy。`impure-core` 更广，可能同时混进 network/filesystem/time/random；当最锋利的隐藏输入就是随机性，用本规则。

### 判定程序

问：重放同一个 command/state 是否应该复现同一个 domain decision？如果是，注入 deterministic random source/seed，或把 sampled value 作为 event/input 的一部分记录。

两种健康 replay model 都可以：

1. Shell 先 sample，把具体 chosen value 传给 pure policy，并把 chosen value 记录进 event；
2. 显式传 deterministic RNG/seed，且 replay 能恢复同一 seed/source 与消费顺序。

通常第一种更容易审计，因为 event 直接说“当时选择了什么”，而不是要求未来 runtime 精确复现某个 RNG implementation 的 draw sequence。

### 例子

- positive：billing policy 在 fold 里调用 `uuid4()` 从并列 invoices 中选 winner，同一 command 无法 replay。
- near-miss：TLS adapter 为 handshake 生成 nonce，从不把 nonce 喂进 domain events。
- counterexample：shell 掷一次骰子，把整数传给 pure `apply(command, roll)`，并把 `roll` 记录到 event。

## 建议

### 现在该做什么

把 entropy 从 ambient magic 变成显式 provenance。

先判断 domain 是否需要重放这次随机决定。如果需要，优先在 shell/adapter 抽样，把 chosen value 作为普通 input 传给 pure policy，并在 durable event/fact 中记录。这样 replay 不需要重新抽，也不依赖未来 RNG implementation 仍与今天完全相同。

例如：

```text
roll = rng.next()
decision = decide(command, state, roll)
append DecisionMade(..., roll)
```

或者在确实需要一连串 deterministic stochastic process 时，传入明确 seed/RNG port，并把 seed/version/consumption model 做到足以 replay。

### 为什么重要

隐藏的 randomness 打破“同样 inputs”与“同样 decision”的等价。这会破坏 deterministic tests、incident replay、event rebuilding，以及“为什么选了这条 branch 而不是那条”的解释。

### 修复策略

不要把所有随机都推入 event。Crypto nonce、TLS key、纯 UI jitter、不会改变 business fact 的 backoff jitter 可以留在各自 adapter；domain 不拥有它们的 replay obligation。

有目的地选择 replay model：要么把 sampled value 作为 fact 持久化，要么持久化/控制 seed 并使用 deterministic generator。Crypto entropy 在 domain replay 不拥有它时留在 security adapter。

### 决策分支

- 如果 domain 必须 replay 这个 decision，把 seed 或 sampled value 传进 core，并把该值随 event 持久化。
- 如果 entropy 是 cryptographic、不是业务选择，把它留在 security adapter，不进 domain policy。
- 如果测试现在 monkey-patch global RNG，换成 production 会用的同一个显式 port。

### 常见假修复

常见假修复：

- test 全局 monkey-patch `Math.random`，production core 仍直接读 ambient RNG；
- process startup 固定 seed，就声称 randomness “已经可重现”，但 seed 没成为 operation/session identity 的一部分；
- core 里生成 UUID，之后只 log 一下，recovery 仍会生成新 UUID；
- 把 RNG object 放进 global singleton，依赖 draw order 永远不变；
- event 只记录 “RandomChoiceMade”，却不记录实际 choice/seed；
- replay 时重新 sample，祈祷相同 input 得到相同结果。

### 验证

验证要从 replay 出发。给相同 declared input（包含 choice/seed）执行两次，domain output 必须一致；改变 entropy input 时，只允许那些本来就被 randomness 授权影响的 decision 变化。

再做 restart/replay：已发生的 stochastic decision 应恢复原 choice，而不是重新参加一次 lottery。若 event/history 使用 seed model，还要证明 generator version 与 consumption order 不会因 unrelated refactor 改变过去。

对于 security entropy，反而应验证它**没有**被不必要地拖入 domain replay 或持久化敏感 material。显式边界不是“所有 randomness 都记录”，而是“每种 entropy 的 owner 与 replay义务清楚”。

### 完成条件

完成时，任何改变 business history 的 stochastic choice 都能回答：这次选择的 entropy 从哪来、选择结果是什么、restart 后为什么仍是同一个结果。

> Randomness 可以决定哪条合法路径发生；系统仍然必须记得当时是哪条路径，以及为什么 replay 不该再抽一次。

### Nudge

> Entropy 可以让结果不确定，但不能让原因不可追溯。随机是输入，不是神谕。
