# snapshot-as-truth — Main

恢复单向 provenance。

如果 source facts 才是 authority，snapshot 就必须满足三件事：能从 source rebuild；携带足够 identity 证明自己代表哪一段 source；一旦与 source 冲突，永远输给 source。

一个非常有用的不变量：

> **删掉所有 snapshot 最多损失时间，绝不能损失 truth。**

把这句话做成真实机制。

Snapshot 旁要记录 provenance：source offset/count、version、tree/hash digest、schema version、generation，或者 source boundary 真正能证明的 identity。不要拿 file mtime、process start time、“看起来更新”的 timestamp、文件名约定替代 source identity。

Load/recovery 时：

1. 校验 snapshot format/schema；
2. 校验它与 authoritative source 的 provenance；
3. 任一 mismatch 就 reject；
4. 从 trusted point replay/rebuild；
5. 新 state 成立后再选择写一份新 snapshot。

不要因为 replay 贵，就从 derived snapshot 反向重建 source history。那是在颠倒 evidence direction。Replay 成本高可以提高 snapshot frequency、index、compaction，或者明确重设计谁才是 authority；不能偷偷让 optimization 上位。

如果所谓 snapshot 实际就是真 system of record，那就承认并简化。不要维护一份 recovery 根本不信的 ceremonial event log，同时 checkpoint 暗中拥有 truth。两个 source precedence 模糊，比一个诚实 source 更糟。

常见假修复：

- 选 timestamp 最新的文件；
- mismatch 也保留 snapshot，因为“replay 太慢”；
- 再加第二个 snapshot，两个 projection 自己投票；
- 直接修 materialized view，再从它 backfill event log；
- 只比较 length/count，却没有能检测内容替换的 digest/version；
- deserialize 成功就当“这肯定属于当前 history”；
- 老/新 snapshot format 同时保留，各有不同 precedence rule，又没有 one-way migration contract。

验证要故意攻击 provenance：

- stale snapshot + newer source；
- 来自另一个 session/account/tree、但 shape 合法的 snapshot；
- corrupt bytes；
- bytes 合法但 source digest 错；
- snapshot 缺失；
- snapshot 位于 prefix N，source 已到 M；
- schema migration。

所有情况都必须收敛到“只从 authoritative source replay”得到的同一 state。

还要证明 rebuild snapshot 不会改写 earlier source fact。Snapshot creation 只是 optimization side effect，除非 domain 明确规定，否则不是新的 semantic event。

完成时 source-of-truth direction 只能画成一条箭头：

```text
authoritative facts → current state → snapshot
```

绝不能是：

```text
snapshot ↔ facts   // 谁看起来更新谁赢
```

> Cache 可以 disposable，authority 不可以。