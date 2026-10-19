# partial-write-assumption — Lesson

## 评价

### 定义

Partial-write assumption 是一种“过度防御式想象”：recovery 自己发明了 storage/effect boundary 从未承诺会出现的中间状态，然后开始根据这个幻想做破坏性修复。

它很容易伪装成谨慎，因为“disk 可能写到一半”听起来很现实。但 application recovery 没有资格根据硬件 folklore 或最坏想象建模，它只能根据**自己真正使用的 boundary contract**推理。

如果 database transaction 对 caller 只暴露：

```text
Committed
NotCommitted
Unknown
```

### 支配原则

区分标准不是乐观还是悲观，而是 evidence：

> **Application 能不能从 boundary 的 observable data 证明这个 intermediate state？**

能，就建模；不能，就别制造。

尤其危险的是“defensive truncation”：process crash 后，startup 代码默认最后一条可能 torn，于是先删掉。如果 append 本来 atomic，这会在最需要历史可靠的事故恢复时，亲手删除**最后一条合法 committed fact**。

### 何时触发

以下情形触发：

- timeout 被解释成“probably half-written”，但 boundary 明明承诺 atomic commit；
- recovery 只因 file length “看起来可疑”就 truncate 最后一条，却没有 format-level checksum/commit marker 证明 torn write；
- application 增加 `MaybePartial`、`PartiallyPersisted`、`HalfApplied` 等状态，来源只是对底层实现的想象；
- external API 只说 outcome unknown，caller 却自己猜它内部执行到哪一步；
- storage abstraction 保证 atomic append，caller 仍越过 abstraction 看 filesystem residue，试图判断“是不是只写了一半”；
- test mock 出 production contract 永远不可能返回的状态，反过来逼 production 支持一个 fantasy failure model。

### 不应触发

不要误杀真正可 partial 的 boundary。有些 log format 明确允许 torn tail，并提供 length prefix、checksum、sequence、commit marker；某些 distributed operation 真的有多阶段 outcome。只要 boundary 暴露这些 fact，recovery 能观察并证明，就应该认真建模。

### 与相邻规则区分

邻近规则：

- `optimistic-retry-assumption`：unknown external effect 被擅自当失败并重试；
- `truncation-skips-damaged`：durable history 真的损坏，却被错误 skip/truncate；
- `memory-before-disk`：volatile authority 先于 durable commit；
- `blob-after-event`：reference publication 先于 referent durability。

### 判定程序

正确纪律是让 boundary owner 定义 recovery state space。读 storage/database/provider contract，只编码 API 真正能区分的状态。Knowledge 缺失时保留 `Unknown`，不要把 physical imagination 伪装成 evidence。

### 例子

那么 application 再发明一个 `HalfCommitted`，并不会更 robust，只会把 state machine 扩大到一个它无法观察、无法证明的世界。更糟的是，这个虚构状态往往会授权 destructive action：truncate、rewrite、compensate、delete、replay，最后亲手破坏本来有效的数据。

## 建议

### 现在该做什么

删掉你无法观察的 recovery state。

从 storage/effect boundary 的真实 contract 出发，而不是从你想象的底层物理实现出发。把 caller 真正能区分的 outcome 全部列出来，recovery 只围绕这组状态设计。

### 为什么重要

很多 boundary 的诚实 state space 比工程师直觉里小：

```text
known committed
known not committed
unknown
```

`Unknown` 不是建模失败，反而经常是 timeout、process death、transport loss 后最准确的事实。保留它，直到 authoritative lookup、idempotency protocol、transaction query、checksum、commit marker 等 boundary-owned evidence 把它解决。

### 修复策略

如果 torn/partial data 真的可能出现，就建模**证明 partial 的证据**，例如：

- length prefix + checksum；
- explicit commit marker；
- WAL/page sequence semantics；
- store 暴露的 transaction state；
- 能区分 accepted/rejected 的 provider status endpoint；
- durable multipart manifest 与 per-part commit。

如果 abstraction 已承诺 atomicity，就不要根据“发生过 crash”、elapsed time、可疑 file size、或者底层 folklore 自己推断 partial。

### 决策分支

特别警惕 destructive recovery。Truncate/delete/rewrite 必须有 positive corruption evidence 或 owner 明确规则。**“我不确定”不是“这条 history 可以删”的证据。**

### 常见假修复

常见假修复：

- 任意 crash 后“保险起见”truncate 最后一次 append；
- 从未校验 checksum 就 rewrite record；
- 加 `HalfWritten` state，但没有任何 API outcome 能诚实构造它；
- 用 timeout duration 猜 write 进行到哪里；
- 为了 second-guess atomic abstraction，把 filesystem internals 泄漏给 caller；
- test mock 出 impossible outcome，再把支持这些 mock 当 resilience；
- 因为不喜欢 `Unknown`，就把它压成某个 guessed physical state。

### 验证

验证也应从 contract 推导。Fault-inject 每一个**documented outcome**，把 committed / not-committed / unknown / 明确 partial/corrupt 状态都证明一遍。

还要做 abstraction boundary 检查：caller 不应越过 store 去看 contract 故意隐藏的 implementation residue。如果 recovery 真需要那条信息，就让 abstraction 正式暴露 typed fact；否则就是 caller 越权。

### 完成条件

完成时应形成一一对应：

```text
observable boundary outcome ↔ recovery branch
```

现实状态一个不少；幻想状态一个不多；不存在 precondition 只能写成“内部大概发生了这个”的 branch。

> Recovery 更安全，往往不是因为想象更多，而是因为想象更少、证据更强。

### Nudge

> Robust recovery 不是处理你能想象出的所有失败，而是处理现实能产生、并且你真正能够区分的失败。
