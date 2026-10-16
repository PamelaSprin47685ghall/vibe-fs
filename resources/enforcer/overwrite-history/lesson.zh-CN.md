# overwrite-history — Lesson

## 评价

### 定义

Overwrite history 的病，不是“durable row 被 update”这么机械，而是一个本来回答“**当时我们知道、决定、做了什么**”的 committed record，被 present 改写成仿佛 corrected version 从一开始就是真相。

Correction 与 history 是两件事。

### 支配原则

这种丢失会直接破坏很多重要问题：

- earlier decision 当时掌握了什么信息；
- later correction 为什么发生；
- replay 到时间 T 是否能复现当时行为；
- 哪些 downstream effect 是由旧 belief 触发；
- bug/fraud/migration/operator 是否改写过历史；
- audit evidence 是否完整。

### 何时触发

当 committed event、journal entry、ledger fact、audit record、decision history 或其他“what happened then” 记录被普通 update/delete 用来做 correction 时，触发本规则。

### 不应触发

不要误杀普通 mutable present state。Projection、cache、search index、current balance table、rebuildable read model，如果背后另有 authoritative history，完全可以被重写。本规则保护 historical testimony，不保护所有 durable row。

Legal/privacy erasure 也必须区分。GDPR deletion、secret redaction、cryptographic erasure、court-ordered removal 可能要求某些 content 不再可见，但这不等于允许随手 silent mutation。正式 policy 应在法律允许范围内保留“发生过 redaction/removal、由什么 authority 发起、replay 怎么解释”这类非敏感证据。

### 与相邻规则区分

邻近规则：

- `snapshot-as-truth`：derived projection 被抬到 source 之上；
- `in-place-mutation`：current shared state 被 mutate，不一定碰 history；
- `stale-documentation`：prose 与现在 behavior 不一致；
- `unrecorded-decision`：关键选择从未 durable capture。

### 判定程序

只有当最准确的伤口是这句时才用本规则：**present 被允许伪造 durable past 看起来曾经是什么。**

判定问题很简单：

> 这条 record 回答的是“现在什么是真的”，还是“当时记录了什么”？

前者可以 mutable；后者通常应 append correction/compensation/revocation/supersession，而不是把原事实擦成一个更漂亮的过去。

### 例子

例如一笔 charge 当时记录 100，后来发现错了，应改成 80。此时有两个都值得保留的事实：

1. 系统曾经记录/依赖过 100；
2. 后来的 evidence 让这个 belief 被改成 80。

直接 `UPDATE amount = 80` 会把两次事件压成一个 timeless value。Current answer 看起来更干净，但真正发生过的 causal transition 被抹掉了。

## 建议

### 现在该做什么

不要再通过“替换过去”来修正过去。

保留原 committed fact，再 append 一个新的 correction / compensation / revocation / supersession / reclassification / reversal / redaction marker，或者领域真正需要的变更事实。

### 为什么重要

目标是同时回答两个问题：

- **时间 T 当时记录/相信/做了什么？**
- **现在的 current interpretation 是什么？**

如果回答第二个问题必须先摧毁第一个问题，history model 就不够强。

### 修复策略

Event/journal 系统里，current state 应由 original facts + correcting facts fold 得出。Correction event 应明确指出自己修正什么，并保留足够 reason/provenance，让人能理解“为什么 interpretation 改了”，而不是悄悄把 earlier event 的 bytes 改掉。

### 决策分支

Ledger/accounting 优先 compensating entry，而不是重写已经 posted 的 balance。Audit record 应 append 谁/什么 authority/为什么改变 interpretation。Migration 也要分清：修复 malformed storage representation、但 semantic history 不变，可以是机械 migration；如果 migration 改了系统对“过去发生过什么”的主张，那就是 semantic migration，必须有正式 policy。

### 常见假修复

常见假修复：

- 原 event row 原地 update，event ID 还保持不变；
- 删除原 event，再插一个 replacement，让 replay 只能看到 corrected story；
- migration 直接 normalize 历史值，却不记录哪些 semantic fact 被改了；
- 把 current truth 反写到所有旧 record/snapshot，只为“报表一致”；
- 因 current-state query 难写，就去改历史；应该修 projection/query；
- 另写 audit log 记录“我改过”，但 authoritative event 本身仍被重写；
- historical event 加 `deleted=true` 就算完成，却没有定义 replay 怎么理解 deletion。

### 验证

验证要证明 temporal fidelity。Correction 前的 replay/query 仍应看到当时原 record；correction 后的 replay 应得到新的 current interpretation。两个视图都必须真实存在，不能靠一方擦掉另一方。

还要检查 downstream causality。Earlier effect 若由旧 fact 触发，它们仍需可解释。Correction 可以要求 compensation，但不能让历史 effect 看起来突然“没有原因”。

Redaction 另测：敏感 content 按 policy 消失，同时法律允许的 metadata 仍能证明 redaction 发生过，并且 replay 对 redacted fact 有 deterministic semantics。

### 完成条件

完成时，系统应该能讲出一条诚实变化史：

```text
当时记录/相信 X
后来 authority/evidence Y 到达
因此 X 被 Z 修正
现在 interpretation 是 W
```

而不是在第二句本来是假的情况下声称：

```text
我们一直都相信 W
```

> Auditability 不是“旧 row 永远不删”，而是保留“当时是什么”与“现在相信什么”之间真实发生过的因果差异。

### Nudge

> 一个 fact 可以后来 obsolete，但不能因此变成“从未发生”。Correction 本身就是 history。
