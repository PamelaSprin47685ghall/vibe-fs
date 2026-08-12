# log-as-recovery-protocol — Enforcer

Log-as-recovery-protocol 的缺陷，是 restart logic 把**写给人看的 diagnostic output**当成“什么事实真正 commit 过”的 authoritative record。

“log”这个词本身很混乱，所以必须按 semantics 判断，不能按文件名判断。

一个真正 event journal 也可能是 append-only text/JSON，甚至文件就叫 `.log`。只要它有 schema、durability guarantee、ordering model、stable identity、atomic commit、replay contract，而且系统就是有意把 business fact 写在那里，那它是 durable fact store，不是本规则的坏味道。

本规则打的是：**diagnostic commentary 被抬成 machine truth。**

Diagnostic channel 天然可能更弱：

- effect commit 前就先 emit message；
- process crash 时 buffer 还没 flush；
- line 会 sampled、duplicated、reordered、rotated、truncated、redacted、dropped；
- wording 会因 clarity/localization 改；
- structured logging pipeline 会 transform field；
- 一个 business fact 可能打多条 log，也可能一条没有；
- retry 可能重复打印同一句，但真正 effect 只 commit 一次。

这些对 observability 不一定是 bug；但 recovery 一旦依赖 presence/order/wording，就会致命。

以下情形触发：

- restart grep `INFO order committed` 决定哪些 order 存在；
- daemon 从上次 stdout/stderr 找“最后成功 step”；
- 因为 JSON log “已经 structured”，就直接拿来当 lifecycle record；
- 没有某条 log 被当成“effect 没发生”的证据；
- async worker 的 log arrival order 被当 causal order；
- 为了 recovery parser，operator message 不敢改 wording；
- metric/tracing event 被当 business completion 的 durable source。

不要误杀纯解释性 log。也不要误杀真正 durable event journal；如果 observability event 本身就拥有完整 commit contract，那就把它正式称为 journal，别把 durability 当 logging 的偶然属性。

邻近规则：

- `stringly-typed-error`：normal control flow 依赖 human error prose；
- `recovery-by-filesystem-state`：restart 从 artifact residue 猜 progress；
- `memory-before-disk`：runtime state 先于 durable fact；
- `unrecorded-decision`：关键 decision 根本没有 durable record。

决定性实验：完全 suppress diagnostic output，但 business effect 与 durable store 保持。Recovery 如果因此改变，observability 就被授予了不该有的 authority。

反方向也要测：只 emit 同一句 diagnostic line，但不 commit underlying effect。如果 recovery 因此相信 effect 发生，log 已经成了 counterfeit testimony。

> Diagnostics 可以描述 history；它不应该成为 history 被相信的唯一理由。