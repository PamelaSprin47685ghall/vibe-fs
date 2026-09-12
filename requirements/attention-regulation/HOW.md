# attention-regulation — HOW

## 架构与机制

本包遵循轻量级工具契约设计，避免重型状态机与全局框架：

1. **Tool 契约与资源**：
   - `EnoughTool`、`AbandonTool`：纯粹的边界强化动作，接收单字符串参数，返回明确的认知反馈，不引入持久领域存储。
   - `DeferTool`：基于统一 EventStore 追加最小 DeferredWork 事实，不维护独立特征数据库或定时器。

2. **生命周期与重浮现机制**：
   - `DeferredProjection(participant)`：提取当前未消费的延后工作条目。
   - `prepareResurface(participant, celebrationOccurrence)`：在 `celebrate` 阶段提供待露出条目批次，与学习凭据在同一次事务中原子提交，防止断电导致的状态不一致。

3. **交互规范约束**：
   - 各工具入参均限定为单一自然语言字符串，禁止引入置信度、优先级、截止时间等结构化表单字段。

### 工具与持久化的边界

`Interaction/Attention/JournalPort.fs` 定义本域的 `AttentionJournalPort`：一次读取 Attention 投影，以及携带 session／provider-run 身份追加 `AttentionFactCases`。`AttentionTools` 只消费它，不再接收 `AgentJournal`、读取 `ProjectionSet` 或构造 `AgentFact`。已有 occurrence 的拒绝重复判断仍由工具调用唯一 `AttentionProjection.tryFind` 完成；每次执行只读取一次快照。

`Composition/Durable/AgentJournalPortAdapter.forAttention` 从同一个 journal 的单次 snapshot 取 Attention 切片，以原 stream／provider-run 追加 `AgentFact.Attention`。`ToolRegistry` 只选择并接入该适配器，不拥有 outer union 包装。预期追加失败仍渲染原 durable-unavailable 结果；物理异常继续传播，没有 catch-all、默认成功或第二套存储。其余领域消费者尚未迁移，不因这一工具解耦宣称全局 SCC 已消除。

`OpenCode/Tools/AttentionToolSurface` 执行真实 `AttentionTools.specs` 的工具，以 JS 原生回调适配本域 port。001／002 的证明不再匹配源码函数名，而是实际执行 enough／abandon，确认接受／拒绝及零持久化调用。003／004 执行 defer，证明空输入、缺身份、缺 journal、预期失败、异常传播、身份传递、重复 occurrence 及已 resurface 条目不复活；投影使用现有 production Surface，不复制算法。这里的记录型 port 不证明物理 journal 重启或跨进程原子性，原有持久化与重放证明仍须保留。

## 验证与测试落点

| 命题 | 最低充分 proof |
|---|---|
| ATTENTION-REGULATION-001 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-001] enough is a pure cognitive stop with no durable authority state` |
| ATTENTION-REGULATION-002 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-002] abandon releases only cognitive attention and never mutates obligations or authority` |
| ATTENTION-REGULATION-003 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-003] defer creates pending work without creating execution or obligation state` |
| ATTENTION-REGULATION-004 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-004] deferred work is occurrence-idempotent and participant-life isolated` |
| ATTENTION-REGULATION-005 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-005] resurfacing consumes deferred visibility once without activating work` |
| ATTENTION-REGULATION-006 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-006] attention state stays a minimal deferred-work projection, not a workflow engine` |
