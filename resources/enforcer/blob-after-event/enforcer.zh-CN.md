# blob-after-event — Enforcer

`blob-after-event` 是 temporal referential-integrity failure：durable history 已经说“内容 H 属于这段历史”，但 H 自己还没跨过真正 durability boundary。

Reference 不是一个无害 ID。一旦 journal/event/manifest commit `blob = H`，它就在向未来每一次 replay 承诺：

> H 对应的内容真实存在，并且按当前 storage contract 可以恢复。

如果 blob upload/write 仍 pending、只在 memory buffer、只在 temp path、或者 event durable 后 blob 仍可能独立失败，history 就先提交了一句自己还证明不了的话。

这不是 cache miss，而是 dangling history。

以下情形触发：

- event/journal row 带 blob/content reference 先 commit，blob store 后确认 durability；
- manifest/index 先 publish，“large payload upload”再 async 跟上；
- 从 in-memory bytes 算 hash，先 durable 引用，再晚些写 bytes；
- blob write 只完成 local buffer/temp location，但 recovery 依赖更强 remote/fsync/quorum durability；
- durable reference 已存在后 cleanup 还能删掉 temp blob；
- replay 把“event 在、blob 不在”当正常 branch，虽然 domain 从未允许 dangling reference。

如果 blob 与 reference 在一个真实 transaction 中 atomic commit，而且 recovery guarantee 覆盖二者，不触发。Reference 指向已经 durable 的 content-addressed object，也没问题。Content inline 时也没有独立 referent ordering。

它与 `memory-before-disk` 的区别是：这里两边都可能是 durable artifact，问题是**durable referent 与 durable reference 的顺序**，不是 volatile memory 跑太快。

`partial-write-assumption` 管“recovery 自己幻想 storage state”；本规则则是 application ordering 真的能够制造一个合法 committed event + missing target 的坏世界。

最清楚的 crash table：

```text
blob durable 前               → 绝不能有 event reference
blob durable 后、event 前     → orphan blob 可接受/可 GC
event append 后               → blob 必须按 retention contract 可读
```

这种不对称是故意的。Unreferenced durable blob 通常只是可清垃圾；referenced missing blob 则让 history 自相矛盾。所以 prefer referent-first。

修复必须依赖 blob store **真正的 durability success condition**。“upload request 返回了”不一定等于 replay-grade durable；如果 store 只有 finalize/quorum/commit 后才保证可读，就必须等到那里。

Content-addressed store 还要验证 hash identity 来自实际 persisted bytes，而不是只信 caller upload 前 buffer。Durable reference 必须准确命名 recovery 最终读到的东西。

> 先让内容成为事实，再让它的名字成为历史。