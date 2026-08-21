# durable-convergence — HOW

## 架构机制与收敛流水线

`durable-convergence` 实现去中心化的多副本事实合并与远程同步：

1. **K-way 规范归并（K-Way Merge）**：
   - 算法为每个写者流维护一个游标，按 `(canonical sort key, EventId)` 确定性前进。
   - 相同 `EventId` 且相同规范字节自动幂等去重，相同 `EventId` 产生不同字节则阻断报错。
   - 本地启动与远程同步均复用同一归并原语。

2. **结构化冲突跟踪（Structural Frontier）**：
   - `StructuralProjection` 跟踪每个流当前的未裁决头部集合 `Heads: stream_id → Set<EventId>`。
   - 出现并发追加时，头部集合包含多个 `EventId`，系统进入 `DomainConflict` 状态。
   - 领域裁决事件（Resolution Event）必须显式在 `parents` 中包含当前全部竞争头部，折叠后将头部集合重新收敛为单个。

3. **双向无损同步（Bidirectional Convergence）**：
   - 独立 Git Hook 进程在执行同步时，以同一 `now`/24h TTL 对本地与远端 writer 做整流 retention；writer 内部仍保持完整 append-only 流。
   - `lastActivity` 首选从 durable writer tail 推导：`JournalEnvelope.payload.ObservedAt` 为 producer 精确观测时间；连续尾随的 `ProjectionCutTail` 不推进生命周期，反向读取最近 Journal 时间。非 Journal tail 才回退 producer-side file activity。
   - snapshot 根包含 `writer-manifest v2`，逐 writer 原子记录完整 blob OID 与上述 `lastActivity`。远端导入复用该 activity，不允许 fetch 动作刷新 writer 生命周期；manifest/materialization cache 只是派生索引，不取代 writer bytes 中的 Journal 时间真值。
   - 完全没有 `writer-manifest` 或仍为 mtime 语义 `v1` 的旧 snapshot 采用 clean break：不导入其 writer tree。新协议 v2 snapshot 要求 manifest 与 `writers/` 一一覆盖并绑定精确 blob OID；结构不完整直接 `StorageInvalid`。没有 v2 activity 证据时禁止根据 fetch 时间、对象到达时间或本地新 mtime 猜测活跃性。
   - retention 后执行 k-way merge；过期 writer 文件从本地删除，新的远端 `writers/` tree 也不再包含它，因此旧 snapshot 再参与同步时仍会被相同 retention predicate 过滤而不能稳定复活。
   - 热路径利用文件元数据指纹与 manifest 跳过无变更文件的重复读取与编解码；materialization cache 同时记录“下一次 expiry”，跨过该时刻即使文件未变化也必须重新 materialize。
   - NDJSON 尾记录读取使用从 EOF 反向按块 `pread` 查找裸 LF 的确定性算法；不猜测最后一行长度，也不解码整条 writer。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DURABLE-CONVERGENCE-001 | `requirements/durable-convergence/tests/event-store-merge.test.mjs` |
| DURABLE-CONVERGENCE-002 | `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` |
| DURABLE-CONVERGENCE-003 | `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` |
| DURABLE-CONVERGENCE-004 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` |
| DURABLE-CONVERGENCE-005 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` |
| DURABLE-CONVERGENCE-006 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` |
| DURABLE-CONVERGENCE-007 | `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` |
| DURABLE-CONVERGENCE-008 | `requirements/durable-convergence/tests/event-store-converge.test.mjs` |
| DURABLE-CONVERGENCE-009 | `requirements/durable-convergence/tests/dumb-remote-no-domain.test.mjs` |
| DURABLE-CONVERGENCE-010 | `requirements/durable-convergence/tests/hook-performance-fast-path.test.mjs` |
| DURABLE-CONVERGENCE-011 | `requirements/durable-convergence/tests/writer-retention.test.mjs` |
