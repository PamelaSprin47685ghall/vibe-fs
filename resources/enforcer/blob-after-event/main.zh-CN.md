# blob-after-event — Main

把 publication order 反过来，或者让它真的 atomic。

如果 blob 与 reference 是两个独立 commit，就先 persist blob，等到 blob store 真正达到 **recovery-grade durability**，按需要验证 identity，然后才 append event/manifest/index，让 reference 正式进入 history。

目标不变量：

> **每一个 committed reference，在同一 recovery contract 下都必须解析到 durably readable referent。**

安全两步 protocol：

```text
prepare content
persist blob H
verify durable/committed H
append history reference H
publish consequence
```

如果 process 在 blob commit 后、event append 前 crash，最多留下 orphan blob。通常没问题：GC 可按 retention policy 清掉 unreachable content。

如果 process 在 event append 后 crash，replay 必须能读 H。不应存在“history 说内容存在，但也许 upload 还没结束”的正常 semantic branch。正确 ordering 应该让这个世界根本不存在。

如果 underlying store 真支持 blob + reference 的 atomic transaction，可以直接使用，但要验证真实 guarantee，不能把两个异步 write 靠得很近就自称 atomic。

Content-addressed storage 还要做到：

- identity 从 store 实际接受的 exact bytes 计算/验证；
- corruption/substitution 重要时，replay 检查 digest；
- retry 复用同一 content identity，不要旧 reference 未解决就再发新 identity；
- GC 永远不能删仍被 retained history reach 到的 blob。

常见假修复：

- event 先 append，blob “马上”排队上传；
- replay blob missing 就无限 retry，把 corruption 变普通 control flow；
- temp file 先被 reference，rename/finalize 后做，但没有 recovery 信任的 atomic rename contract；
- SDK callback 只代表 local buffer complete，却被当 remote durable；
- pre-upload memory 算 hash，persisted bytes 从不 verify；
- event 已 commit 后 blob upload fail，再补一条 “blob missing” event；早先 history 仍然矛盾；
- event 本身做成 mutable，稍后再补 blob reference。

验证要在每个 boundary crash：

1. blob durable 前 → 无 committed reference；
2. blob durable 后、event 前 → 最多 orphan content；
3. event 后 → reference 必须 resolve；
4. replay 时 → digest/identity 按 contract 校验；
5. GC 时 → reachable content 必须保留。

如果 blob-store acknowledgement 也会丢，还要测 unknown outcome：先按 content identity/status resolve，再决定 reference 是否能 publish。

完成时 replay engine 可以把所有 committed reference 当事实，而不是每次都去参与一场 storage archaeology。

> Orphan bytes 比 orphaned truth 便宜。宁可 durable content 暂时没 reference，也不要 durable history 等一个不存在的 content。