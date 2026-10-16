# random-source-in-logic — Enforcer

## 定义

Random source 藏在 logic 里的问题，不是“系统不该随机”，而是 entropy 成了**没有出现在 decision signature 里的输入**。

同样的 visible state + command，domain core 内部偷偷 `random()` / `uuid4()` / entropy draw，于是两个 replay 可以产生不同 event，却没有任何 recorded fact 能解释差异来自哪里。系统最终只能说：“当时随机到了这个。”——但 seed、sample、source 都没留下。

Randomness 本来就是 input，只是 ambient API 把它伪装成魔法。

## 支配原则

Randomness 仍然是 input。内部采样它的函数只是藏起了输入通道：同样的 visible state 可以导致不同 event，却没有可复现的解释。对 simulation、allocation、game、sampling、tie-breaking，replayability 要求保留生成的 choice，或保留能确定性推出该 choice 的 seed/source。

## 何时触发

以下情形触发：

- domain policy 直接调用 RNG 决定 winner、allocation、sampling、tie-break、shard、variant；
- event replay 会重新抽一次随机数；
- UUID/nonce 被拿来决定业务 identity/order，却只在 core 内现生成；
- test 必须 monkey-patch global RNG 才能稳定；
- incident 无法回答“为什么这次选 A 不选 B”，因为 random choice 没 provenance；
- retry/recovery 重新运行 logic 时可能抽到不同选择，改变已经发生过的业务路径。

## 不应触发

不要误杀所有 entropy。Cryptographic key/nonce generation 如果属于 security adapter，domain replay 根本不要求重现那次随机位串，就可以留在边界。UI-only jitter、backoff jitter 若不改变 business fact，也不必塞进 domain event。

真正区分标准是：**这个随机结果是否参与了需要 replay、解释、持久化的业务决定？** 如果参与，就必须让它成为可见输入或 durable fact。

## 与相邻规则区分

与 `time-source-in-logic` 同族：一个藏 clock，一个藏 entropy。`impure-core` 更广，可能同时混进 network/filesystem/time/random；当最锋利的隐藏输入就是随机性，用本规则。

## 判定程序

问：重放同一个 command/state 是否应该复现同一个 domain decision？如果是，注入 deterministic random source/seed，或把 sampled value 作为 event/input 的一部分记录。

两种健康 replay model 都可以：

1. Shell 先 sample，把具体 chosen value 传给 pure policy，并把 chosen value 记录进 event；
2. 显式传 deterministic RNG/seed，且 replay 能恢复同一 seed/source 与消费顺序。

通常第一种更容易审计，因为 event 直接说“当时选择了什么”，而不是要求未来 runtime 精确复现某个 RNG implementation 的 draw sequence。

## 例子

- positive：billing policy 在 fold 里调用 `uuid4()` 从并列 invoices 中选 winner，同一 command 无法 replay。
- near-miss：TLS adapter 为 handshake 生成 nonce，从不把 nonce 喂进 domain events。
- counterexample：shell 掷一次骰子，把整数传给 pure `apply(command, roll)`，并把 `roll` 记录到 event。

## Nudge

> Entropy 可以让结果不确定，但不能让原因不可追溯。随机是输入，不是神谕。