# memory-before-disk — Lesson

## 评价

### 定义

Memory-before-disk 是 durability ordering 缺陷：authoritative runtime state 已经前进，但本应证明这次前进合法的 durable fact 还没 commit。

真正危险的不只是“crash 后丢数据”，而是**同一个 process lifetime 内出现 epistemic split-brain**。

Memory 一旦先走，后续工作就可能观察并依赖一个 recovery 无法重建的状态。系统短暂创造了一个“consequence 已经真实发生，但 evidence 还不存在”的世界。

### 支配原则

核心法则：

> **Durable commit 先建立事实；authoritative memory 只能在之后投影这个事实，不能跑到它前面。**

### 何时触发

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

### 不应触发

不要把这条理解成“每个 byte 都必须先落到旋转磁盘”。真正 durability boundary 是 recovery protocol 认可的 commit：transactional DB commit、fsynced WAL、replicated quorum、durable journal append ack 等。WAL 本身可以是 authority，final materialization 晚一点完全没问题。

也不要误杀 private speculative state。Commit 前先在 memory 里算 candidate aggregate、validate/hash 都可以，只要它**不能 escape**：没人能观察、没有 effect 依赖、commit fail 时直接丢弃。Speculation 一旦产生 authoritative consequence，才触发本规则。

### 与相邻规则区分

邻近规则：

- `blob-after-event`：event 已 durable，却引用一个尚未 durable 的 blob；
- `snapshot-as-truth`：derived snapshot 被当 canonical history；
- `overwrite-history`：已经 committed 的 past fact 被改写；
- `partial-write-assumption`：interrupted persistence 被擅自当 all-or-nothing；
- `unverified-completion-claim`：prose 超过 evidence；这里是 runtime state 自己超过 durable evidence。

### 判定程序

决定性验证是 crash injection：在 “memory changed” 与 “durable commit” 之间的每个 boundary 停进程，列出此时已经可能产生的 externally visible behavior，再从 durable state restart。只要 recovery 无法重建那些 effect 所依赖的 state，memory 就拥有了尚未赚到的 authority。

健康顺序通常是：

1. 在不 mutate shared authority 的情况下推导 intended transition；
2. atomic commit durable fact；
3. 从 committed fact fold/apply 到 authoritative memory；
4. 最后才 expose success 或启动依赖新 state 的 consequence。

Step 3 后 crash 没关系，recovery 能 replay；Step 2 fail 则 command 根本没有发生。这种不对称正是正确性来源。

### 例子


## 建议

### 现在该做什么

把 authority 移到 durability boundary 之后。

Candidate transition 可以先在 memory 里算，但在证明它的 durable commit 成功之前，不要 publish、不要 return success、不要启动依赖 effect、不要把它 swap 进 shared authoritative state。

### 为什么重要

这个顺序提供了正确的 recovery asymmetry：

- persistence fail → command 没发生，authoritative memory 仍是旧状态；
- persistence 成功，但 memory apply 前 crash → restart 能 replay committed fact，恢复新状态。

反过来的顺序没有这种安全解释。Memory 先前进并影响了其他事情后，crash 可以把 evidence 抹掉，却留下由“被抹掉状态”造成的真实 consequence。

### 修复策略

目标顺序：

```text
old authoritative state
        ↓ pure/isolated decision
candidate transition / fact
        ↓ durable commit
committed fact
        ↓ fold/apply
new authoritative memory
        ↓ consequence / success
```

Private speculative object 要保持真的 private。Commit 前先计算 `nextState`、hash、validate、prepare derived artifact 都没问题。不能跨的是 **authoritative escape**：在 commit success 前，任何 command、callback、provider response、child effect、publication、shared reader 都不能把 candidate 当真。

### 决策分支

Rollback 尤其容易骗人。Advanced memory 一旦被 observer 看见，rollback 无法撤销那些 observer 已经做出的决定，只能再制造一个 transition，并祈祷所有 escaped consequence 都可逆。

### 常见假修复

常见假修复：

- memory 先 mutate，persistence fail 后再 rollback；
- 先 return success，再 async persist “优化 latency”；
- 只写进 process-local buffer 就叫 durable，但 crash 后 recovery 根本看不到；
- cache/projection 先更新，因为 “DB commit 大概率成功”；
- 先从 candidate state 发 external effect，最后才 persist state；
- persistence fail 后因“process 还活着”就保留 advanced memory；
- durability contract 明明包含 hard crash/power loss，却只靠 graceful shutdown flush pending facts。

### 验证

验证必须围绕 ordering boundary 注入 failure：

1. durable commit 前失败：authoritative memory 不变，也不能有 dependent effect escape；
2. commit 中失败：严格服从 storage protocol 的 committed / not-committed / unknown semantics；
3. commit 成功、memory apply 前 crash：restart 必须重建新 state；
4. commit + apply：observer 看到的状态必须与 replay 完全一致。

如果 durability 是 async/replicated，必须精确定义“commit success”在 recovery 里的含义。不能一条 path 把 local write 当 committed，restart 却要求 quorum/fsync。

还要测 concurrent reader：不能因为 candidate 已经 assign 给 mutable field，就让 reader 在 durable success 前看到它。

### 完成条件

完成时，每个 authoritative state transition 都有一个先于 visibility 的 durable witness；restart 后也能从同一 durability boundary 重建所有曾经暴露过的状态。

> Durable history 先赚到 authority；Memory 是它的快速投影，不是 impatient predecessor。

### Nudge

> Memory 可以快，但不能替一个 durable history 尚未承认的未来作证。
