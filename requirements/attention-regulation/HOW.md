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

## 验证与测试落点

| 命题 | 最低充分 proof |
|---|---|
| ATTENTION-REGULATION-001 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-001] enough is a pure cognitive stop with no durable authority state` |
| ATTENTION-REGULATION-002 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-002] abandon releases only cognitive attention and never mutates obligations or authority` |
| ATTENTION-REGULATION-003 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-003] defer creates pending work without creating execution or obligation state` |
| ATTENTION-REGULATION-004 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-004] deferred work is occurrence-idempotent and participant-life isolated` |
| ATTENTION-REGULATION-005 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-005] resurfacing consumes deferred visibility once without activating work` |
| ATTENTION-REGULATION-006 | `requirements/attention-regulation/tests/attention-regulation.test.mjs::WHAT[ATTENTION-REGULATION-006] attention state stays a minimal deferred-work projection, not a workflow engine` |
