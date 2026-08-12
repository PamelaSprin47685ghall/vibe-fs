# overwrite-history — Main

不要再通过“替换过去”来修正过去。

保留原 committed fact，再 append 一个新的 correction / compensation / revocation / supersession / reclassification / reversal / redaction marker，或者领域真正需要的变更事实。

目标是同时回答两个问题：

- **时间 T 当时记录/相信/做了什么？**
- **现在的 current interpretation 是什么？**

如果回答第二个问题必须先摧毁第一个问题，history model 就不够强。

Event/journal 系统里，current state 应由 original facts + correcting facts fold 得出。Correction event 应明确指出自己修正什么，并保留足够 reason/provenance，让人能理解“为什么 interpretation 改了”，而不是悄悄把 earlier event 的 bytes 改掉。

Ledger/accounting 优先 compensating entry，而不是重写已经 posted 的 balance。Audit record 应 append 谁/什么 authority/为什么改变 interpretation。Migration 也要分清：修复 malformed storage representation、但 semantic history 不变，可以是机械 migration；如果 migration 改了系统对“过去发生过什么”的主张，那就是 semantic migration，必须有正式 policy。

常见假修复：

- 原 event row 原地 update，event ID 还保持不变；
- 删除原 event，再插一个 replacement，让 replay 只能看到 corrected story；
- migration 直接 normalize 历史值，却不记录哪些 semantic fact 被改了；
- 把 current truth 反写到所有旧 record/snapshot，只为“报表一致”；
- 因 current-state query 难写，就去改历史；应该修 projection/query；
- 另写 audit log 记录“我改过”，但 authoritative event 本身仍被重写；
- historical event 加 `deleted=true` 就算完成，却没有定义 replay 怎么理解 deletion。

验证要证明 temporal fidelity。Correction 前的 replay/query 仍应看到当时原 record；correction 后的 replay 应得到新的 current interpretation。两个视图都必须真实存在，不能靠一方擦掉另一方。

还要检查 downstream causality。Earlier effect 若由旧 fact 触发，它们仍需可解释。Correction 可以要求 compensation，但不能让历史 effect 看起来突然“没有原因”。

Redaction 另测：敏感 content 按 policy 消失，同时法律允许的 metadata 仍能证明 redaction 发生过，并且 replay 对 redacted fact 有 deterministic semantics。

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