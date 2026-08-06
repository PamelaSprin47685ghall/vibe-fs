# 执行 — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在多 Agent 协同（Fork/Join）、PTY 子进程管理与并行 Map-Reduce 执行中，子任务完成信号的乱序投递、假完成（False Abort/Completion）以及无界并发会导致父会话状态机死锁或产生不确定的 Prompt Wire。执行模块旨在提供无状态、强类型的 Handle 四态流转、严格由物理 `onExit` 驱动的 PTY 泵，以及具有确定性稳定排序与有界批次（MaxJoinBatch = 32）的 Join 结果汇总机制。

### 2. 输入输出与规则边界
- **输入**：子任务 Fork 请求、PTY 进程流、物理完成事件 `onExit`、Join 结果队列。
- **输出**：`RunCompletion` 事实 Blob (v2)、LLM-facing `[[result]]` Synthetic TOML Join Payload、Handle 状态迁移。
- **核心边界与不变量**：
  1. Handle 四态与 Tombstone 不可回退：退役 Handle 严禁复活或重新绑定（EXEC-009）。
  2. PTY 完成仅信任 Backend `onExit`（EXEC-015）：严禁根据 stdout 启发式文本猜测完成。
  3. Agent 终态代数（EXEC-020）：Agent 终态仅为 `Completed | Failed | Abandoned`，取消信号严禁直接写为 RunCompletion。
  4. 确定性 Join 批次（EXEC-018）：MaxJoinBatch = 32，采用稳定排序，绝对禁止“谁先完成谁先入 Wire”的非确定性竞争。

---

## EXEC-010：Process Request

Process Request 类型化。

---

## EXEC-011：Process Deadline

Deadline 有界。超时走确定失败路径，不用无限 wait。

---

## EXEC-012：大输出摘要

超限走摘要策略；不静默截断成「成功空结果」。

---

## EXEC-013：Large Gate

Large Gate 与输出预算合同一致；超限拒绝或摘要路径确定，禁止无界缓冲。

---

## EXEC-018：Join 批次

- MaxJoinBatch = 32  
- 中断前再 drain  
- 稳定排序  
- 逐项 CAS  

竞争优先级固定，禁止「谁先完成谁先入 wire」的非确定序。

---

## EXEC-019：Orchestrator verdict 批量 join

FIFO 排空，上限 32，与 EXEC-018 同界。

---

## EXEC-021：completion blob v2

schemaVersion=2；finality 仅 completed|failed。  
`LegacyFalseAbort` **永不**成为 RunCompletion。  
`fromDecoded` 唯一构造。

---

## EXEC-022：假 completion 补偿

HandleFalseCompletionRejected → 确定性 replacement → parent correction。  
禁止把历史假 abort 洗成成功。

---

## Join wire（与 ARCH-010）

LLM-visible join：顶层 status+count，再 `[[result]]` 表数组；agent 项前 entry-local LWR 注释。详见 synthetic-toml §9.6 与 EXEC-004。
