# durable-events — HOW

## 架构机制与持久化执行链

`durable-events` 提供全局唯一的事件存储与积分管线：

1. **单写者日志与本地提交**：
   - 进程启动时获取唯一的 `WriterId`，独占写入 `.git/wanxiang/events/<WriterId>.ndjson`。
   - `EventStore.append` 获取跨进程门禁锁后，将规范化编码的 `EventEnvelope` 以单行 `JSON + LF` 形式追加到末尾。
   - 大对象内容优先落盘至 `.git/wanxiang/payloads/<PayloadRef>`，确保在事件追加前满足 Payload 完整闭包约束。

2. **Canonical Integrator 与状态折叠**：
   - 系统仅由唯一的 `CanonicalIntegrator` 消费事件历史。
   - 启动或恢复时：先按统一 24h writer-retention predicate 仅枚举活跃本地写者文件，再通过 `EventKWayMerge` 按确定性顺序输入 Integrator 计算当前 `Current` 投影；过期 writer 不读取、不解码。
   - 运行时追加时：新事件经结构校验后直接输入 Integrator 推进 `Current` 状态。若业务规则判定语义不合法，则紧随写入 `ProjectionCutTail` 并在返回前触发进程安全退出。

3. **独立 Git Hook 同步**：
   - 运行时完全不进行 Git 树或对象的读写。
   - 当用户触发 Git 远程操作时，安装的 `reference-transaction` 或 `pre-push` Hook 进程拉起同步脚本。
   - Hook 进程读取本地与远端写者流，在同一截止时刻先整体淘汰过期 writer，再完成 retained k-way merge；每个保留的本地完整写者文件封装为单个 Git blob，并发布带 writer activity manifest 的远端快照。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DURABLE-EVENTS-001 | `requirements/durable-events/tests/append-only-laws.test.mjs` |
| DURABLE-EVENTS-002 | `requirements/durable-events/tests/envelope.test.mjs` |
| DURABLE-EVENTS-003 | `requirements/durable-events/tests/event-store-identity-collision.test.mjs` |
| DURABLE-EVENTS-004 | `requirements/durable-events/tests/event-store-append.test.mjs` |
| DURABLE-EVENTS-005 | `requirements/durable-events/tests/local-process-event-log.test.mjs` |
| DURABLE-EVENTS-006 | `requirements/durable-events/tests/event-store-journal-writer.test.mjs` |
| DURABLE-EVENTS-007 | `requirements/durable-events/tests/event-store-append.test.mjs` |
| DURABLE-EVENTS-008 | `requirements/durable-events/tests/event-store-fold.test.mjs` |
| DURABLE-EVENTS-009 | `requirements/durable-events/tests/integration/persist/leave-unread.test.mjs` |
| DURABLE-EVENTS-010 | `requirements/durable-events/tests/workspace-event-store-host.test.mjs` |
| DURABLE-EVENTS-011 | `requirements/durable-events/tests/local-process-event-log.test.mjs` |
| DURABLE-EVENTS-012 | `requirements/durable-events/tests/journal-payload-closure.test.mjs` |
| DURABLE-EVENTS-013 | `requirements/durable-events/tests/canonical-integrator.test.mjs` |
| DURABLE-EVENTS-014 | `requirements/durable-events/tests/event-store-merge.test.mjs` |
| DURABLE-EVENTS-015 | `requirements/durable-events/tests/fold-context-recovery.test.mjs` |
| DURABLE-EVENTS-016 | `requirements/durable-events/tests/unified-store-gate.test.mjs` |
| DURABLE-EVENTS-017 | `requirements/durable-events/tests/local-process-event-log.test.mjs` |
| DURABLE-EVENTS-018 | `requirements/durable-events/tests/hook-dispatcher.test.mjs` |
| DURABLE-EVENTS-019 | `requirements/durable-events/tests/canonical-integrator.test.mjs` |
| DURABLE-EVENTS-020 | `requirements/durable-events/tests/event-store-journal-boot.test.mjs` |
| DURABLE-EVENTS-021 | `requirements/durable-events/tests/event-store-append.test.mjs` |
