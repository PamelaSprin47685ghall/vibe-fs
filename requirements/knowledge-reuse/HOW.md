# knowledge-reuse — HOW

## 架构机制与核心模型

### 1. 观察捕获与重放管线

1. **类型化捕获（Capture）**：
   - 监听 Inspector 工具调用，对 `read` 生成 `FileRead(path, contentHash)`、对 `glob` 生成 `GlobResult(pattern, paths)`、对 `grep` 生成 `GrepResult(pattern, matches)`；
   - 提取的观察经 `Observations.normalize` 执行按路径与内容的稳定去重和排序，折叠为规范的观察集合。

2. **只读重放（Replay）**：
   - `fetch` 调用首先通过 `CasebookReplay.replayAll` 对当前工作区重放已记录的各条 observation；
   - 比对重放结果：若与原集合完全一致，判定为 `Fresh` 并直接返回原规范答案；若存在差异，判定为 `Stale` 并转入刷新流程。

### 2. Bookkeeper 维护与事务机制

1. **事务 Staging 与 SDK**：
   - `BookkeeperStaging` 提供 `beginTransaction`、`snapshot`、`apply` 与 `take` 操作；
   - `js-bookkeeper(program)` 执行传入的 JS 代码，在沙箱中提供 `setQuestion` 与 `setAnswer` 接口，支持单事务内的原子修改与异常自动回滚。

2. **生命周期与 Finalize**：
   - `Lifecycle` 模块管理草稿收集；在 ReuseScope 关闭时触发恰好一次 `tryFinalizeInspector`，生成归档请求并持久化。

### 3. 持久化与索引投影

1. **统一事件流（Store）**：
   - 归属统一 EventStore 的 `casebook` 流，支持 `InspectorCaseCaptured`、`InspectorCaseRefreshed`、`InspectorCaseAccessed` 与 `InspectorCaseEvicted` 事件；
   - 大文本通过 `PayloadRef` 存储在 blob 存储中，事件体仅保留引用与元数据。

2. **低信任索引（Index）**：
   - `CasebookIndex` 管理 `{ shelfmark, canonicalQuestion }` 快照，按 epoch 缓存冻结；
   - 当检测到可见集合变化或显式失效时推进 epoch，保证同一 epoch 内提示词字节完全稳定。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| KNOWLEDGE-REUSE-001 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs` |
| KNOWLEDGE-REUSE-002 | `requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs` |
| KNOWLEDGE-REUSE-003 | `requirements/knowledge-reuse/tests/casebook-capture.test.mjs` |
| KNOWLEDGE-REUSE-004 | `requirements/knowledge-reuse/tests/fetch-tool.test.mjs` |
| KNOWLEDGE-REUSE-005 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs` |
| KNOWLEDGE-REUSE-006 | `requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs` |
| KNOWLEDGE-REUSE-007 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs` |
| KNOWLEDGE-REUSE-008 | `requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| KNOWLEDGE-REUSE-009 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs` |
| KNOWLEDGE-REUSE-010 | `requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs` |
| KNOWLEDGE-REUSE-011 | `requirements/knowledge-reuse/tests/fetch-tool.test.mjs` |
| KNOWLEDGE-REUSE-012 | `requirements/knowledge-reuse/tests/casebook-index.test.mjs` |
