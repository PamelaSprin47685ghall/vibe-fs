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

## JoinAttempt 中断程序

`JoinTool` 在任何 mailbox drain/wait 前先 `Begin(session, toolCall)`，再把 Host abort callback 绑定到该 lease。external-user ingress 只遍历 registry 中当时 active 的 lease；lease dispose 即注销。race 返回后仍先 drain completion。

Esc callback 先 resolve 当前 lease，使 join 可渲染 `operator_abort`；父 provider 随后进入 `TurnAborted`，通用 cleanup 依次中止父 PTY、调用 `AbortChildren`、发布 Aborted terminal。external-user ingress 只 signal lease，不进入这条 cleanup，因此 sub-session 继续运行并可晚到完成。

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

LLM-visible join：顶层 status+count，再 `[[result]]` 表数组；agent 项前 entry-local LWR 注释。详见 synthetic-toml `### Join / fork` 与 EXEC-004。  
中断 wire（EXEC-017）：本地 operator abort → `status="interrupted", reason="operator_abort"`；user message → `status="interrupted", reason="user_message"`；`interrupted` 不是 `ForkError` / `failed` / `aborted`。DevOps 超时仍走 `ForkError.TimedOut`（`status="failed", code="TIMED_OUT"`）。

## 同步返回双路径（EXEC-028）

### A. Residual OneShot（dispose-after）

仅 residual 非 dedicated callers。算法：create child → subscribe-before-send → await 一次 terminal →
finally abort/dispose child。

成功 wire：COMPANION-003 物化 child LWR（`includeOpening=false`）→ `SyntheticToml.comment` 作
entry-local 注释，后接末条 TurnFormalText；禁止 `work_record` 字段。与 Join 共用物化器；wire 面独立于
`[[result]]` 批次。Opening 在 send 前从原始 assignment 捕获（对齐 fork）；返回的 LWR 仍为
`includeOpening=false`。fail-closed：`Completed` 若无法物化出非空 LWR，返回工具级 `error=`，绝不静默
退回仅 formal report 的成功。encode 的 soft-omit 仅覆盖非 Completed 的 soft Ok（如 send-failed）；
Completed 缺 LWR 在 run 内 fail-closed，不改 encode 行为。

### B. Reusable SyncDelegate（Acquire → dual await）

Dedicated `inspector` / `coder`（EXEC-026）算法：

```text
use! scopeLease = syncDelegateGate.Acquire(immediateCallerReuseScope)
let! delegate = attachedSessions.GetOrCreate(ownerReuseScopeId, role)  // tier from owner
use call = delegateCalls.Begin(ownerReuseScopeId, delegate)
do! promptDispatcher.Send(delegate, message)
let! answer = call.Returned.Await(...)      // return(message) resolves Returned only
do! call.Completion.Await(...)              // TurnCompleted resolves Completion
return answer                               // caller blocked until both
```

顺序钉死：

```text
return(A) → Returned resolved → Completion still pending → caller still pending
→ fixed terminal assistant completion
→ reconciler proves TurnCompleted → Completion resolved
→ caller gets A
```

成功路径不 `AbortSession` / 不 dispose dedicated Session；abort 只保留给失败、取消与 ReuseScope
teardown。下一同步调用必须等上一 `Completion`，防止与 terminal 尾部重叠。

### Serialization / tier 机制

- Gate key = immediate caller ReuseScope（**禁止** family-root gate）。嵌套
  `DevOps waits Coder` / `Coder waits Inspector` 各持本层 scope lease，可并行完成而无 deadlock。
- `GetOrCreate` 绑定 owner effective tier → 固定 delegate Agent（fast/deep）；调用参数不得覆盖 tier。

---

## Student / Teacher — G3 已删除（absent）

`Role.Student` / `Role.Teacher`、Learn/Compile、`StudentQaStore`、`teacher` 工具、
`TeacherIdleNudge` / `StudentCompile` / `StudentCompileNudge` 与 `StudentTeacherRuntime`
**不存在于生产**（EXEC-027 / AGENT-020…022 / PROMPT-012 空缺）。不得写成 pending / 过渡 / 双写。

双 await（Returned→Completion）、idle nudge（`SyncDelegateIdleNudge`）与 dedicated reuse 的现行程序
见上文 **Reusable SyncDelegate（EXEC-026/028）**；`return` **仅** SyncDelegate。
