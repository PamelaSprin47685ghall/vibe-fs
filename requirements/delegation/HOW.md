# delegation — HOW

## 架构机制

### 委托接口分流与权能门禁

DELEG-020 约束：委托语义不依赖当前工具名字面值（`fork`、`commission`、`inspect`、`establish-behavior`、`repair-behavior`），改名不动 WHAT 语义定义。

系统定义三类委托途径，由角色权能门禁严格限制：

1. **异步见证委托（`fork`）**：由 Manager 在使命内部调用，创建具有独立 Byname 的子执行者，支持附加历史背景（attachment）与建议性工具调用估算。
2. **独立道路委托（`commission`）**：由 Orchestrator 调用，负责开启或续做独立集成道路，支持多道路并行推进。
3. **同步委托（`inspect` / `establish-behavior` / `repair-behavior`）**：由业务角色在单轮内发起阻塞式子任务，经由 `SyncDelegate` 管道调度，完成取证或局部修复。

### 载荷渲染与方向不对称

- **父 → 子（初始提示词注入）**：父会话向子会话传递上下文时，`ForkChildPayload` 将任务正文渲染为 `instructions`，将 `commissioner_record` 与 `attached_work_record` 作为 TOML 数据字段嵌入 body，杜绝将背景解析为指令或混入注释。
- **子 → 父（完成项回传）**：子会话结束并回传结果时，`JoinResultRenderer` 仅将物化的 WorkRecord 以 entry-local 注释形式注入 wire，严禁包裹为字段式 DTO。

### 同步委托批次与串行化

- **批次聚拢**：宿主在处理同单次运行中指向同一角色的多个同步委托时，按工具调用顺序合并为一个语义 batch，拼接 charge 后单次调度。
- **单栈执行与结果分发**：串行化键为直接调用方的 `ReuseScope`。仅第一位 canonical 调用方获得完整 WorkRecord，其余 sibling 调用方获得引用句柄。
- **普通完成收口**：被委托方普通 Assistant 结束即触发返回，无独立 return 协议通道。

### Join 消费与中断

Join 机制从所有者的完成信箱中按稳定排序逐项 CAS 消费可用结果，单次消费上限受 `MaxJoinBatch` 约束。外部打断信号与超时仅产生 `Interrupted` 结果，确保子会话的执行与既有权能不受破坏。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DELEG-001 | `requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-002 | `requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-003 | `requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-004 | `requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-005 | `requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-006 | `requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-007 | `requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-008 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-009 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-010 | `requirements/delegation/tests/sync-delegate.test.mjs` |
| DELEG-011 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-012 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-013 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-014 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-015 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-016 | `requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-017 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-018 | `requirements/delegation/tests/assistance-host.test.mjs` |
| DELEG-019 | `requirements/delegation/tests/fork-child-payload.test.mjs` |
| DELEG-020 | `requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-021 | `requirements/delegation/tests/fork-attachment.test.mjs` |
| DELEG-022 | `requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs` |
| DELEG-023 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
