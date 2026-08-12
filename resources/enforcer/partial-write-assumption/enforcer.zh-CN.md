# partial-write-assumption — Enforcer

Partial-write assumption 是一种“过度防御式想象”：recovery 自己发明了 storage/effect boundary 从未承诺会出现的中间状态，然后开始根据这个幻想做破坏性修复。

它很容易伪装成谨慎，因为“disk 可能写到一半”听起来很现实。但 application recovery 没有资格根据硬件 folklore 或最坏想象建模，它只能根据**自己真正使用的 boundary contract**推理。

如果 database transaction 对 caller 只暴露：

```text
Committed
NotCommitted
Unknown
```

那么 application 再发明一个 `HalfCommitted`，并不会更 robust，只会把 state machine 扩大到一个它无法观察、无法证明的世界。更糟的是，这个虚构状态往往会授权 destructive action：truncate、rewrite、compensate、delete、replay，最后亲手破坏本来有效的数据。

以下情形触发：

- timeout 被解释成“probably half-written”，但 boundary 明明承诺 atomic commit；
- recovery 只因 file length “看起来可疑”就 truncate 最后一条，却没有 format-level checksum/commit marker 证明 torn write；
- application 增加 `MaybePartial`、`PartiallyPersisted`、`HalfApplied` 等状态，来源只是对底层实现的想象；
- external API 只说 outcome unknown，caller 却自己猜它内部执行到哪一步；
- storage abstraction 保证 atomic append，caller 仍越过 abstraction 看 filesystem residue，试图判断“是不是只写了一半”；
- test mock 出 production contract 永远不可能返回的状态，反过来逼 production 支持一个 fantasy failure model。

不要误杀真正可 partial 的 boundary。有些 log format 明确允许 torn tail，并提供 length prefix、checksum、sequence、commit marker；某些 distributed operation 真的有多阶段 outcome。只要 boundary 暴露这些 fact，recovery 能观察并证明，就应该认真建模。

区分标准不是乐观还是悲观，而是 evidence：

> **Application 能不能从 boundary 的 observable data 证明这个 intermediate state？**

能，就建模；不能，就别制造。

邻近规则：

- `optimistic-retry-assumption`：unknown external effect 被擅自当失败并重试；
- `truncation-skips-damaged`：durable history 真的损坏，却被错误 skip/truncate；
- `memory-before-disk`：volatile authority 先于 durable commit；
- `blob-after-event`：reference publication 先于 referent durability。

尤其危险的是“defensive truncation”：process crash 后，startup 代码默认最后一条可能 torn，于是先删掉。如果 append 本来 atomic，这会在最需要历史可靠的事故恢复时，亲手删除**最后一条合法 committed fact**。

正确纪律是让 boundary owner 定义 recovery state space。读 storage/database/provider contract，只编码 API 真正能区分的状态。Knowledge 缺失时保留 `Unknown`，不要把 physical imagination 伪装成 evidence。

> Robust recovery 不是处理你能想象出的所有失败，而是处理现实能产生、并且你真正能够区分的失败。