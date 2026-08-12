# memory-before-disk — Enforcer

Memory-before-disk 是 durability ordering 缺陷：authoritative runtime state 已经前进，但本应证明这次前进合法的 durable fact 还没 commit。

真正危险的不只是“crash 后丢数据”，而是**同一个 process lifetime 内出现 epistemic split-brain**。

Memory 一旦先走，后续工作就可能观察并依赖一个 recovery 无法重建的状态。系统短暂创造了一个“consequence 已经真实发生，但 evidence 还不存在”的世界。

典型坏顺序：

```text
compute transition
mutate in-memory aggregate
publish / answer / launch dependent work
append event or persist record
```

如果 persistence 在这段 gap 失败或进程 crash，会出现非常难解释的历史：

- caller 已收到 success，restart 却忘了这次 command；
- 后续 command 基于一份 durable history 从未拥有的 state 做了决定；
- event 已从 memory publish，journal append 随后失败；
- child effect 因 memory 说 “accepted” 被启动，recovery 却回到 “not accepted”；
- cache/projection 因为先前进，逐渐变成事实上的 authority。

核心法则：

> **Durable commit 先建立事实；authoritative memory 只能在之后投影这个事实，不能跑到它前面。**

不要把这条理解成“每个 byte 都必须先落到旋转磁盘”。真正 durability boundary 是 recovery protocol 认可的 commit：transactional DB commit、fsynced WAL、replicated quorum、durable journal append ack 等。WAL 本身可以是 authority，final materialization 晚一点完全没问题。

也不要误杀 private speculative state。Commit 前先在 memory 里算 candidate aggregate、validate/hash 都可以，只要它**不能 escape**：没人能观察、没有 effect 依赖、commit fail 时直接丢弃。Speculation 一旦产生 authoritative consequence，才触发本规则。

邻近规则：

- `blob-after-event`：event 已 durable，却引用一个尚未 durable 的 blob；
- `snapshot-as-truth`：derived snapshot 被当 canonical history；
- `overwrite-history`：已经 committed 的 past fact 被改写；
- `partial-write-assumption`：interrupted persistence 被擅自当 all-or-nothing；
- `unverified-completion-claim`：prose 超过 evidence；这里是 runtime state 自己超过 durable evidence。

决定性验证是 crash injection：在 “memory changed” 与 “durable commit” 之间的每个 boundary 停进程，列出此时已经可能产生的 externally visible behavior，再从 durable state restart。只要 recovery 无法重建那些 effect 所依赖的 state，memory 就拥有了尚未赚到的 authority。

健康顺序通常是：

1. 在不 mutate shared authority 的情况下推导 intended transition；
2. atomic commit durable fact；
3. 从 committed fact fold/apply 到 authoritative memory；
4. 最后才 expose success 或启动依赖新 state 的 consequence。

Step 3 后 crash 没关系，recovery 能 replay；Step 2 fail 则 command 根本没有发生。这种不对称正是正确性来源。

> Memory 可以快，但不能替一个 durable history 尚未承认的未来作证。