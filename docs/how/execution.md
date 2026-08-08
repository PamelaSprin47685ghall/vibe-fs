# 执行 — 目标实现

## Implements

行为合同见 `what/execution.md`；本文件只描述 handle、PTY、fork/join 和有界并发算法。

## Ownership

进程、会话和完成事实边界见 `shape/execution.md`。

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

## EXEC-025：DevOps Join 超时

- `DevOpsJoinTimeoutMs = 10_000`
- DevOps 角色的 `join` 工具在 10s 内无任何完成项时触发 timerTask 超时，返回 `ForkError.TimedOut`（wire 渲染为 `status="failed"`, `code="TIMED_OUT"`）。
- Orchestrator 与 Manager 角色的 `join` 不使用 10s timerTask，维持无限期等待。
- Join race 唤醒源含 completion 可用、本地 operator abort、external-user ingress 与适用的 DevOps deadline（EXEC-017）；任意 race 后先 drain 已可用 completion，再发 interrupt 结果。

---

## completion blob 机制（行为见 what/execution.md EXEC-021/022）

行为定义（finality 仅 completed|failed、LegacyFalseAbort 永不 RunCompletion、假 completion 确定性补偿）见 `what/execution.md` EXEC-021/022。  
本处只留机制：`fromDecoded` 是 completion blob 的唯一构造入口，wire 形状见 Join wire 一节。

---

## Join wire（与 ARCH-010）

LLM-visible join：顶层 status+count，再 `[[result]]` 表数组；agent 项前 entry-local LWR 注释。详见 synthetic-toml §9.6 与 EXEC-004。  
中断 wire（EXEC-017）：本地 operator abort → `status="interrupted", reason="operator_abort"`；user message → `status="interrupted", reason="user_message"`；`interrupted` 不是 `ForkError` / `failed` / `aborted`。DevOps 超时仍走 `ForkError.TimedOut`（`status="failed", code="TIMED_OUT"`）。

---

## Student / Teacher（EXEC-027/026）

`teacher` 工具程序：

```text
claim StudentRun single-flight
→ QA.atomicAppend(question)
→ SatelliteRuntime.ensureTeacher(owner,tier)
→ install return waiter before send
→ first: SendAgentOwnerRoot / later: SendContinuation(TeacherQuestion)
→ await return；idle 由 reconcile 发送 TeacherIdleNudge
→ Teacher return 执行 QA.atomicAppend(answer)，武装 pending return
→ tool result 要求同一 Host loop 输出固定 completion text
→ experimental.text.complete 绑定并校正该 completion provider run
→ terminal reconcile 核对 run、正文和 TurnCompleted
→ release flight；answer 作为父 teacher tool result
```

这与 Blogger 的 deferred-completion 原则相同：工具先持久化语义结果，terminal reconcile 再提交外层完成。
Teacher 成功路径不调用 `AbortSession`；abort 只保留给失败、取消和 teardown。

Student learning idle 构造完整 `StudentCompile` profile 与 tools map后才发送正式编译 Prompt。Compile idle
重复发送固定 continuation，但 claim sequence 保证每次是独立 PromptKey；同一时刻只能有一个未决发送。

最终 `return`：`deleteQa` 成功或 absent → 保存 pending final message → 返回要求同一 Assistant completion
逐字输出该 message 的 tool result。`experimental.text.complete` 只校正该 pending Student session 的最终
text part；普通 session、Teacher 和非 pending part 原样通过。terminal reconcile 核对最终正文后清理
pending/run；失败或 abort 保留明确未完成结局，不伪造成功。
