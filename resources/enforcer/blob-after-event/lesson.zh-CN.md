# blob-after-event — Lesson

## 评价

### 定义

`blob-after-event` 是 temporal referential-integrity failure：durable history 已经说“内容 H 属于这段历史”，但 H 自己还没跨过真正 durability boundary。

Reference 不是一个无害 ID。一旦 journal/event/manifest commit `blob = H`，它就在向未来每一次 replay 承诺：

> H 对应的内容真实存在，并且按当前 storage contract 可以恢复。

如果 blob upload/write 仍 pending、只在 memory buffer、只在 temp path、或者 event durable 后 blob 仍可能独立失败，history 就先提交了一句自己还证明不了的话。

这不是 cache miss，而是 dangling history。

### 支配原则

这种不对称是故意的。Unreferenced durable blob 通常只是可清垃圾；referenced missing blob 则让 history 自相矛盾。所以 prefer referent-first。

### 何时触发

以下情形触发：

- event/journal row 带 blob/content reference 先 commit，blob store 后确认 durability；
- manifest/index 先 publish，“large payload upload”再 async 跟上；
- 从 in-memory bytes 算 hash，先 durable 引用，再晚些写 bytes；
- blob write 只完成 local buffer/temp location，但 recovery 依赖更强 remote/fsync/quorum durability；
- durable reference 已存在后 cleanup 还能删掉 temp blob；
- replay 把“event 在、blob 不在”当正常 branch，虽然 domain 从未允许 dangling reference。

### 不应触发

如果 blob 与 reference 在一个真实 transaction 中 atomic commit，而且 recovery guarantee 覆盖二者，不触发。Reference 指向已经 durable 的 content-addressed object，也没问题。Content inline 时也没有独立 referent ordering。

### 与相邻规则区分

它与 `memory-before-disk` 的区别是：这里两边都可能是 durable artifact，问题是**durable referent 与 durable reference 的顺序**，不是 volatile memory 跑太快。

`partial-write-assumption` 管“recovery 自己幻想 storage state”；本规则则是 application ordering 真的能够制造一个合法 committed event + missing target 的坏世界。

### 判定程序

修复必须依赖 blob store **真正的 durability success condition**。“upload request 返回了”不一定等于 replay-grade durable；如果 store 只有 finalize/quorum/commit 后才保证可读，就必须等到那里。

Content-addressed store 还要验证 hash identity 来自实际 persisted bytes，而不是只信 caller upload 前 buffer。Durable reference 必须准确命名 recovery 最终读到的东西。

### 例子

最清楚的 crash table：

```text
blob durable 前               → 绝不能有 event reference
blob durable 后、event 前     → orphan blob 可接受/可 GC
event append 后               → blob 必须按 retention contract 可读
```

## 建议

### 现在该做什么

把 publication order 反过来，或者让它真的 atomic。

如果 blob 与 reference 是两个独立 commit，就先 persist blob，等到 blob store 真正达到 **recovery-grade durability**，按需要验证 identity，然后才 append event/manifest/index，让 reference 正式进入 history。

### 为什么重要

目标不变量：

> **每一个 committed reference，在同一 recovery contract 下都必须解析到 durably readable referent。**

### 修复策略

安全两步 protocol：

```text
prepare content
persist blob H
verify durable/committed H
append history reference H
publish consequence
```

如果 underlying store 真支持 blob + reference 的 atomic transaction，可以直接使用，但要验证真实 guarantee，不能把两个异步 write 靠得很近就自称 atomic。

Content-addressed storage 还要做到：

- identity 从 store 实际接受的 exact bytes 计算/验证；
- corruption/substitution 重要时，replay 检查 digest；
- retry 复用同一 content identity，不要旧 reference 未解决就再发新 identity；
- GC 永远不能删仍被 retained history reach 到的 blob。

### 决策分支

如果 process 在 blob commit 后、event append 前 crash，最多留下 orphan blob。通常没问题：GC 可按 retention policy 清掉 unreachable content。

如果 process 在 event append 后 crash，replay 必须能读 H。不应存在“history 说内容存在，但也许 upload 还没结束”的正常 semantic branch。正确 ordering 应该让这个世界根本不存在。

### 常见假修复

常见假修复：

- event 先 append，blob “马上”排队上传；
- replay blob missing 就无限 retry，把 corruption 变普通 control flow；
- temp file 先被 reference，rename/finalize 后做，但没有 recovery 信任的 atomic rename contract；
- SDK callback 只代表 local buffer complete，却被当 remote durable；
- pre-upload memory 算 hash，persisted bytes 从不 verify；
- event 已 commit 后 blob upload fail，再补一条 “blob missing” event；早先 history 仍然矛盾；
- event 本身做成 mutable，稍后再补 blob reference。

### 验证

验证要在每个 boundary crash：

1. blob durable 前 → 无 committed reference；
2. blob durable 后、event 前 → 最多 orphan content；
3. event 后 → reference 必须 resolve；
4. replay 时 → digest/identity 按 contract 校验；
5. GC 时 → reachable content 必须保留。

如果 blob-store acknowledgement 也会丢，还要测 unknown outcome：先按 content identity/status resolve，再决定 reference 是否能 publish。

### 完成条件

完成时 replay engine 可以把所有 committed reference 当事实，而不是每次都去参与一场 storage archaeology。

> Orphan bytes 比 orphaned truth 便宜。宁可 durable content 暂时没 reference，也不要 durable history 等一个不存在的 content。

### Nudge

> 先让内容成为事实，再让它的名字成为历史。
