# snapshot-as-truth — Lesson

## 评价

### 定义

Snapshot 真正危险，不是因为它“可能 stale”，而是一个本来**从 history 推导出来**的 representation，后来反过来拥有了否定 source history 的权力。

Snapshot、checkpoint、materialized view、cache、summary、index、projection 之所以有用，就是因为它们主动忘掉一部分 derivation，把长历史压成便宜的 present。只要 source fact 仍然 authoritative，这种 lossiness 完全正常；一旦 compression 被升级成 testimony，问题才开始。

核心问题只有一句：

> Snapshot 与 source 冲突时，谁有资格说对方错？

如果答案是“snapshot，因为它更新/更快/更容易 load”，optimization 已经变成第二个 truth owner。

### 支配原则

最决定性的 test 是删除。把所有 snapshot/checkpoint/cache 删除，仅从 supposedly authoritative source rebuild。若 semantic information 消失，要么 source 从来就不是 authority，要么 snapshot 已偷偷成为第二 owner。两者都需要明确 architecture decision。

### 何时触发

以下情形触发：

- recovery load checkpoint，但无法证明它的 digest/version/source position 对应 underlying fact stream；
- materialized read model 被直接 edit，之后还拿它重建 supposedly authoritative history；
- cache 与 fact log 不一致时，系统选择 cache 当 current truth；
- 用 file mtime / modification timestamp 证明 snapshot freshness，而不是 source identity；
- projection corruption 与合法 state transition 无法区分；
- 删掉 snapshot 会丢掉 supposedly 已经存在于更强 history 里的 semantic information。

### 不应触发

不要因为系统没有 event log 就触发。Database row 完全可以就是 system of record；materialized view 也可以真的 authoritative，只要 contract 就是这样。那就别再假装背后还有一个“更真实”的 hidden history。

也不要误杀 disposable acceleration。Snapshot 如果带足够 provenance、source mismatch 时会 reject、并且能从 source 无损 rebuild，它就是健康 optimization。

### 与相邻规则区分

邻近规则：

- `duplicated-truth`：两个 writable owner 都声称同一事实归自己；
- `recovery-by-filesystem-state`：incidental path residue 被当 lifecycle truth；
- `overwrite-history`：committed historical fact 被改写；
- `memory-before-disk`：volatile state 跑到 durable commitment 前面。

### 判定程序

只有当中心问题是这句时才用本规则：**derived representation 被授予了高于其 source 的 authority。**

修复要让 provenance mechanical：记录 source position、event count、version、digest、schema、generation 等真正能证明“我对应哪段 source history”的 identity。Mismatch 就丢掉/rebuild。除非 projection 明确就是真正 system of record，否则绝不要从 snapshot 反向“修复”source。

### 例子

materialized read model 被直接 edit，之后还拿它重建 supposedly authoritative history。cache 与 fact log 不一致时，系统选择 cache 当 current truth。用 file mtime / modification timestamp 证明 snapshot freshness，而不是 source identity。

## 建议

### 现在该做什么

恢复单向 provenance。

如果 source facts 才是 authority，snapshot 就必须满足三件事：能从 source rebuild；携带足够 identity 证明自己代表哪一段 source；一旦与 source 冲突，永远输给 source。

一个非常有用的不变量：

> **删掉所有 snapshot 最多损失时间，绝不能损失 truth。**

把这句话做成真实机制。

### 为什么重要

Snapshot 旁要记录 provenance：source offset/count、version、tree/hash digest、schema version、generation，或者 source boundary 真正能证明的 identity。不要拿 file mtime、process start time、“看起来更新”的 timestamp、文件名约定替代 source identity。

### 修复策略

Load/recovery 时：

1. 校验 snapshot format/schema；
2. 校验它与 authoritative source 的 provenance；
3. 任一 mismatch 就 reject；
4. 从 trusted point replay/rebuild；
5. 新 state 成立后再选择写一份新 snapshot。

不要因为 replay 贵，就从 derived snapshot 反向重建 source history。那是在颠倒 evidence direction。Replay 成本高可以提高 snapshot frequency、index、compaction，或者明确重设计谁才是 authority；不能偷偷让 optimization 上位。

### 决策分支

如果所谓 snapshot 实际就是真 system of record，那就承认并简化。不要维护一份 recovery 根本不信的 ceremonial event log，同时 checkpoint 暗中拥有 truth。两个 source precedence 模糊，比一个诚实 source 更糟。

### 常见假修复

常见假修复：

- 选 timestamp 最新的文件；
- mismatch 也保留 snapshot，因为“replay 太慢”；
- 再加第二个 snapshot，两个 projection 自己投票；
- 直接修 materialized view，再从它 backfill event log；
- 只比较 length/count，却没有能检测内容替换的 digest/version；
- deserialize 成功就当“这肯定属于当前 history”；
- 老/新 snapshot format 同时保留，各有不同 precedence rule，又没有 one-way migration contract。

### 验证

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

### 完成条件

完成时 source-of-truth direction 只能画成一条箭头：

```text
authoritative facts → current state → snapshot
```

绝不能是：

```text
snapshot ↔ facts   // 谁看起来更新谁赢
```

> Cache 可以 disposable，authority 不可以。

### Nudge

> Snapshot 可以让 history 更便宜地被读取，但不能让 history 反过来向 shortcut 负责。
