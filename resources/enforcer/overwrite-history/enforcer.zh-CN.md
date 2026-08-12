# overwrite-history — Enforcer

Overwrite history 的病，不是“durable row 被 update”这么机械，而是一个本来回答“**当时我们知道、决定、做了什么**”的 committed record，被 present 改写成仿佛 corrected version 从一开始就是真相。

Correction 与 history 是两件事。

例如一笔 charge 当时记录 100，后来发现错了，应改成 80。此时有两个都值得保留的事实：

1. 系统曾经记录/依赖过 100；
2. 后来的 evidence 让这个 belief 被改成 80。

直接 `UPDATE amount = 80` 会把两次事件压成一个 timeless value。Current answer 看起来更干净，但真正发生过的 causal transition 被抹掉了。

这种丢失会直接破坏很多重要问题：

- earlier decision 当时掌握了什么信息；
- later correction 为什么发生；
- replay 到时间 T 是否能复现当时行为；
- 哪些 downstream effect 是由旧 belief 触发；
- bug/fraud/migration/operator 是否改写过历史；
- audit evidence 是否完整。

当 committed event、journal entry、ledger fact、audit record、decision history 或其他“what happened then” 记录被普通 update/delete 用来做 correction 时，触发本规则。

不要误杀普通 mutable present state。Projection、cache、search index、current balance table、rebuildable read model，如果背后另有 authoritative history，完全可以被重写。本规则保护 historical testimony，不保护所有 durable row。

Legal/privacy erasure 也必须区分。GDPR deletion、secret redaction、cryptographic erasure、court-ordered removal 可能要求某些 content 不再可见，但这不等于允许随手 silent mutation。正式 policy 应在法律允许范围内保留“发生过 redaction/removal、由什么 authority 发起、replay 怎么解释”这类非敏感证据。

邻近规则：

- `snapshot-as-truth`：derived projection 被抬到 source 之上；
- `in-place-mutation`：current shared state 被 mutate，不一定碰 history；
- `stale-documentation`：prose 与现在 behavior 不一致；
- `unrecorded-decision`：关键选择从未 durable capture。

只有当最准确的伤口是这句时才用本规则：**present 被允许伪造 durable past 看起来曾经是什么。**

判定问题很简单：

> 这条 record 回答的是“现在什么是真的”，还是“当时记录了什么”？

前者可以 mutable；后者通常应 append correction/compensation/revocation/supersession，而不是把原事实擦成一个更漂亮的过去。

> 一个 fact 可以后来 obsolete，但不能因此变成“从未发生”。Correction 本身就是 history。