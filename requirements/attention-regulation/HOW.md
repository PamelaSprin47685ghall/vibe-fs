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
| ATTENTION-REGULATION-001 | tool contract + return semantic test；证明无持久状态与无 authority side effect |
| ATTENTION-REGULATION-002 | tool contract + negative authority/obligation mutation test |
| ATTENTION-REGULATION-003 | pure/event semantic test：证明 defer 后不产生 obligation 或后台任务 |
| ATTENTION-REGULATION-004 | replay/idempotency + participant isolation + owner-life termination retires outstanding defer |
| ATTENTION-REGULATION-005 | temporal：celebrate 先学习后尾部 drain；同 occurrence replay 不重复 drain |
| ATTENTION-REGULATION-006 | architecture negative：无 planner/stage/timer/background executor |
