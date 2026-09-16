# knowledge-reuse — HOW

## 架构机制与核心模型

### 1. 观察捕获与重放管线

1. **类型化捕获（Capture）**：
   - 监听 Inspector 工具调用，对 `read` 生成 `FileRead(path, contentHash)`、对 `glob` 生成 `GlobResult(pattern, paths)`、对 `grep` 生成 `GrepResult(pattern, matches)`；
   - 提取的观察经 `Observations.normalize` 执行按路径与内容的稳定去重和排序，折叠为规范的观察集合。
   - `CasebookCapture.contentHash` 保留 null 的空字符串哨兵，其他文本调用 `runtime-platform/digest` 中既有 `HostDigest.sha256Hex`，不再拥有第二份 crypto 适配器；空字符串仍是正常的 SHA-256 输入。`CASE003_read_capture_is_typed_and_hashed` 经真实 Capture Surface 校验含非 ASCII 字符与 CRLF 的独立固定摘要，不再用生产 helper 自证或仅检查输出长度。只读 replay 继续调用同一捕获合同。

2. **只读重放（Replay）**：
   - `FetchTool.Execute` 首先复用 `CasebookFeature.isEnabled(workspaceRoot)` 检查 marker；未启用时不解析 shelfmark、不构建索引、不触碰事件流；
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

`SyncDelegateSurface.executeInspector` 用 typed `ToolHostCodec.factory`、`HostToolArguments` 与 `HostToolContext` 直接执行真实 `InspectorTool.spec`；opaque JS Surface 合同不变，不再动态导入编译产物或依赖生成构造器的布局。`tests/g6-inspector-tool-finalize-fetch.test.mjs` 实际覆盖单一 Inspector child 的多次复用、每次 bounded record 不泄漏之前的回答，以及 Bookkeeper finalize 后的 canonical Casebook 内容。该测试使用无取消来源的 tool context；取消行为另由 SyncDelegate lifecycle 测试覆盖，不将其描述为 Inspector tool abort 接线证明。

`PluginHooks` 静态调用 `CasebookFeature.isEnabled`、`CasebookLifecycle.collector.Collect` 与 `CasebookTools.buildSpecs`。缺失 workspace 仍禁用该功能；观察捕获仍在关键 after hooks 之后经 `HookPolicy.observeOptional` 隔离，工具构造和 store 获取的失败语义仍由原 owner 持有。`casebook-capture.test.mjs`、`lifecycle-wiring.test.mjs` 与 `fetch-tool.test.mjs` 证明捕获、归档和 marker 门禁的生产行为，但不单独证明完整 PluginHooks 创建至 after-hook 的所有接线路径。
