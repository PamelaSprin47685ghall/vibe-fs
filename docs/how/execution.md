# 执行 — 目标实现

## EXEC-010：Process Request

Process Request 类型化。

## EXEC-011：Process Deadline

Deadline 有界。超时走确定失败路径，不用无限 wait。

## EXEC-012：大输出摘要

超限走摘要策略；不静默截断成「成功空结果」。

## EXEC-013：Large Gate

Large Gate 与输出预算合同一致；超限拒绝或摘要路径确定，禁止无界缓冲。

## EXEC-018：Join 批次

- MaxJoinBatch = 32  
- 中断前再 drain  
- 稳定排序  
- 逐项 CAS  

竞争优先级固定，禁止「谁先完成谁先入 wire」的非确定序。

## EXEC-019：Orchestrator verdict 批量 join

FIFO 排空，上限 32，与 EXEC-018 同界。

## EXEC-021：completion blob v2

schemaVersion=2；finality 仅 completed|failed。  
`LegacyFalseAbort` **永不**成为 RunCompletion。  
`fromDecoded` 唯一构造。

## EXEC-022：假 completion 补偿

HandleFalseCompletionRejected → 确定性 replacement → parent correction。  
禁止把历史假 abort 洗成成功。

## Join wire（与 ARCH-010）

LLM-visible join：顶层 status+count，再 `[[result]]` 表数组；agent 项前 entry-local LWR 注释。详见 synthetic-toml §9.6 与 EXEC-004。
