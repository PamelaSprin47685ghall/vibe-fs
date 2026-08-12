# fragment-event-as-data — Enforcer

Transport fragment 被误当 domain data，是因为 client 把**delivery choreography**当成 authoritative business fact。

Notification、patch、partial stream chunk、callback、delta、progress event、websocket frame、provider-specific update 往往只回答一个窄问题：

> 某些东西变了，或者一部分 observation 到了。

它们并不自动回答：

> 现在完整 authoritative state 到底是什么？

这个区别很重要，因为很多 transport contract 本来就允许 coalesce、duplicate、reorder、omit intermediate update、reconnect 后从更晚位置继续、改变 patch granularity，而仍然完全符合协议。如果 client 把这些 fragment 当 durable ordered fact 去 fold domain state，就在偷偷把 transport guarantee 强化成 provider 从未承诺的东西。

以下情形触发：

- websocket/SSE/provider delta 被直接 fold 成 canonical domain state，没有 authoritative refresh path；
- reconnect 从 “now” 继续，client 就当 missing intermediate fragment 从未重要；
- duplicate/reordered notification 能造出 source 从未存在过的 state；
- progress/update stream 被 persist 成 business history，但 provider 明确把它称为 ephemeral；
- callback arrival order 被当 transition order，却没有 sequence/commit identity；
- patch 说 “field X changed”，client 却把它应用到旧 base version 并当 current truth；
- test 假设每次 source mutation 必有一条 notification，但 contract 允许 coalescing。

不要误杀真正 event source。如果 stream 本身就是 durable ordered domain facts，拥有 stable identity、replay、retention、authoritative semantics，那么 event sourcing 完全合法——这些 event 本来就是 data，不是“fragment 冒充 data”。

如果 fragment 只是 wake-up signal 也没问题：notification 到 → 读取 authoritative snapshot/version → domain behavior 基于完整 state。

邻近规则：

- `snapshot-as-truth`：derived projection 高于 source；
- `log-as-recovery-protocol`：diagnostic output 被当 durable truth；
- `race-first-wins-semantics`：arrival order 决定 business outcome。

最决定性的分类问题：

> 这条 channel 是 **fact log**，还是 **notification channel**？

如果 provider 可以 drop/coalesce/reorder fragment 而仍然算正确，这条 channel 就不能安全定义 domain history。它能告诉你**什么时候该看**，不能告诉你**该相信什么**。

健康 client 常把 fragment 当 invalidation hint：

```text
notification arrives
        ↓
identify affected object/version
        ↓
read authoritative state
        ↓
replace/derive local view
```

如果 full refresh 太贵而必须 incremental，也应强化 protocol，而不是给 client 堆 heuristic：base version、event identity、必要 order、gap detection、replay/resume、duplicate semantics、snapshot/resync escape hatch，都应由 provider contract 正式提供。

> 不要把 transport 的形状抬成 truth 的形状。Notification 可以告诉你去哪里看，只有 authoritative contract 才能告诉你发生了什么。