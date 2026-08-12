# memory-before-disk — Main

把 authority 移到 durability boundary 之后。

Candidate transition 可以先在 memory 里算，但在证明它的 durable commit 成功之前，不要 publish、不要 return success、不要启动依赖 effect、不要把它 swap 进 shared authoritative state。

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

这个顺序提供了正确的 recovery asymmetry：

- persistence fail → command 没发生，authoritative memory 仍是旧状态；
- persistence 成功，但 memory apply 前 crash → restart 能 replay committed fact，恢复新状态。

反过来的顺序没有这种安全解释。Memory 先前进并影响了其他事情后，crash 可以把 evidence 抹掉，却留下由“被抹掉状态”造成的真实 consequence。

Private speculative object 要保持真的 private。Commit 前先计算 `nextState`、hash、validate、prepare derived artifact 都没问题。不能跨的是 **authoritative escape**：在 commit success 前，任何 command、callback、provider response、child effect、publication、shared reader 都不能把 candidate 当真。

常见假修复：

- memory 先 mutate，persistence fail 后再 rollback；
- 先 return success，再 async persist “优化 latency”；
- 只写进 process-local buffer 就叫 durable，但 crash 后 recovery 根本看不到；
- cache/projection 先更新，因为 “DB commit 大概率成功”；
- 先从 candidate state 发 external effect，最后才 persist state；
- persistence fail 后因“process 还活着”就保留 advanced memory；
- durability contract 明明包含 hard crash/power loss，却只靠 graceful shutdown flush pending facts。

Rollback 尤其容易骗人。Advanced memory 一旦被 observer 看见，rollback 无法撤销那些 observer 已经做出的决定，只能再制造一个 transition，并祈祷所有 escaped consequence 都可逆。

验证必须围绕 ordering boundary 注入 failure：

1. durable commit 前失败：authoritative memory 不变，也不能有 dependent effect escape；
2. commit 中失败：严格服从 storage protocol 的 committed / not-committed / unknown semantics；
3. commit 成功、memory apply 前 crash：restart 必须重建新 state；
4. commit + apply：observer 看到的状态必须与 replay 完全一致。

如果 durability 是 async/replicated，必须精确定义“commit success”在 recovery 里的含义。不能一条 path 把 local write 当 committed，restart 却要求 quorum/fsync。

还要测 concurrent reader：不能因为 candidate 已经 assign 给 mutable field，就让 reader 在 durable success 前看到它。

完成时，每个 authoritative state transition 都有一个先于 visibility 的 durable witness；restart 后也能从同一 durability boundary 重建所有曾经暴露过的状态。

> Durable history 先赚到 authority；Memory 是它的快速投影，不是 impatient predecessor。