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
   - 独立 Git Hook 进程在执行同步时，获取远端快照与本地所有 `.git/wanxiang/events/*.ndjson` 文件。
   - 执行 k-way merge 产生统一事实后，全量替换本地同步快照，并将每个本地写者文件完整编码为一个 Git blob，经由标准 CAS 推送至远端。
   - 热路径利用文件元数据指纹跳过无变更文件的重复读取与编解码。

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
