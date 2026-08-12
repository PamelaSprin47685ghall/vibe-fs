# snapshot-as-truth — Enforcer

Snapshot 真正危险，不是因为它“可能 stale”，而是一个本来**从 history 推导出来**的 representation，后来反过来拥有了否定 source history 的权力。

Snapshot、checkpoint、materialized view、cache、summary、index、projection 之所以有用，就是因为它们主动忘掉一部分 derivation，把长历史压成便宜的 present。只要 source fact 仍然 authoritative，这种 lossiness 完全正常；一旦 compression 被升级成 testimony，问题才开始。

核心问题只有一句：

> Snapshot 与 source 冲突时，谁有资格说对方错？

如果答案是“snapshot，因为它更新/更快/更容易 load”，optimization 已经变成第二个 truth owner。

以下情形触发：

- recovery load checkpoint，但无法证明它的 digest/version/source position 对应 underlying fact stream；
- materialized read model 被直接 edit，之后还拿它重建 supposedly authoritative history；
- cache 与 fact log 不一致时，系统选择 cache 当 current truth；
- 用 file mtime / modification timestamp 证明 snapshot freshness，而不是 source identity；
- projection corruption 与合法 state transition 无法区分；
- 删掉 snapshot 会丢掉 supposedly 已经存在于更强 history 里的 semantic information。

不要因为系统没有 event log 就触发。Database row 完全可以就是 system of record；materialized view 也可以真的 authoritative，只要 contract 就是这样。那就别再假装背后还有一个“更真实”的 hidden history。

也不要误杀 disposable acceleration。Snapshot 如果带足够 provenance、source mismatch 时会 reject、并且能从 source 无损 rebuild，它就是健康 optimization。

邻近规则：

- `duplicated-truth`：两个 writable owner 都声称同一事实归自己；
- `recovery-by-filesystem-state`：incidental path residue 被当 lifecycle truth；
- `overwrite-history`：committed historical fact 被改写；
- `memory-before-disk`：volatile state 跑到 durable commitment 前面。

只有当中心问题是这句时才用本规则：**derived representation 被授予了高于其 source 的 authority。**

最决定性的 test 是删除。把所有 snapshot/checkpoint/cache 删除，仅从 supposedly authoritative source rebuild。若 semantic information 消失，要么 source 从来就不是 authority，要么 snapshot 已偷偷成为第二 owner。两者都需要明确 architecture decision。

修复要让 provenance mechanical：记录 source position、event count、version、digest、schema、generation 等真正能证明“我对应哪段 source history”的 identity。Mismatch 就丢掉/rebuild。除非 projection 明确就是真正 system of record，否则绝不要从 snapshot 反向“修复”source。

> Snapshot 可以让 history 更便宜地被读取，但不能让 history 反过来向 shortcut 负责。