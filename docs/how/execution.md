# 执行 — 目标实现

## Implements

行为合同见 `what/execution.md`；本文件只描述 handle、PTY、commission/fork/join/horizon 与有界并发算法。

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

Esc callback 先 resolve 当前 lease，使 join 可渲染 operator-abort 自然语言后果；父 provider 随后进入 `TurnAborted`，通用 cleanup 依次中止父 PTY、调用 `AbortChildren`、发布 Aborted terminal。external-user ingress 只 signal lease，不进入这条 cleanup，因此 sub-session 继续运行并可晚到完成。

## EXEC-018：Join 批次

- MaxJoinBatch = 32
- 中断前再 drain
- 稳定排序
- 逐项 CAS

竞争优先级固定，禁止「谁先完成谁先入 wire」的非确定序。

---

## EXEC-019：Orchestrator commission 批量 join

FIFO 排空，上限 32，与 EXEC-018 同界。

---

## EXEC-025：DevOps Join 超时

- `DevOpsJoinTimeoutMs = 10_000`
- DevOps 角色的 `join` 工具在 10s 内无任何完成项时触发 timerTask 超时，结束本次等待并以自然语言告知等待结束（Host 事实 `DeadlineExpired`）；**不**向 provider 暴露 `TIMED_OUT` / `status="failed"` / `code=...`。
- Orchestrator 与 Manager 角色的 `join` 不使用 10s timerTask，维持无限期等待。
- Join race 唤醒源含 completion 可用、本地 operator abort、external-user ingress 与适用的 DevOps deadline（EXEC-017）；任意 race 后先 drain 已可用 completion，再发 interrupt 结果。

---

## completion blob 机制（行为见 what/execution.md EXEC-021/022）

行为定义（finality 仅 completed|failed、LegacyFalseAbort 永不 RunCompletion、假 completion 确定性补偿）见 `what/execution.md` EXEC-021/022。
本处只留机制：`fromDecoded` 是 completion blob 的唯一构造入口；provider wire 见 Join / WorkRecord 一节。

---

## Join / WorkRecord wire（与 ARCH-010）

LLM-visible join：**禁止** `status` / `count` / `ordinal` / `kind` / `agent` / `code` / `message` 等通用 DTO。后果用自然语言 + entry-local WorkRecord / LWR（`includeOpening=false`）；禁止字段式 `work_record` DTO。agent 完成项前 entry-local LWR 注释（四标题见 GLORY-025）。详见 synthetic-toml 与 EXEC-004。

中断 wire（EXEC-017）：本地 operator abort / user message / DevOps deadline 均以自然语言后果表达；`interrupted` 不是 `ForkError` / `failed` / `aborted`。DevOps 超时走 `JoinInterruptReason.DeadlineExpired`（自然语言），不走 `ForkError.TimedOut` DTO。

---

## 工具动词算法面

| 动词 | 算法要点 |
|------|----------|
| `commission` | Orchestrator：`calling?` + `name` + `charge`；新路或按 Byname 续做；成功仅 `# <Byname> has taken your charge.`；禁止 `job_id` / worktree / `reused`（EXEC-029） |
| `fork` | Manager：使命内 witness；Byname 承接；禁止 agent_id / role / tier / worktree（EXEC-002） |
| `horizon` | 在场名册（Byname / TerminalName）；自然语言；无 id / status（EXEC-005） |
| `inspect` | SyncDelegate → Inspector；普通 completion → bounded WorkRecord（EXEC-031） |
| `establish-behavior` / `repair-behavior` | SyncDelegate → Coder；同 WorkRecord 路径；无 `tdd` / `return` |
| `judge` | Reviewer typed verdict enum；结果不 echo verdict |
| `chronicle` | Blogger 记账；替代已删 `blog` |
| `run` | DevOps 有界执行；≠ Distiller office |
| `open/send/read/signal-terminal` | 四动词四分合同；不返回 `pty_id` / `closed` / `status`（EXEC-003） |

已删除算法面（不得再写）：`fork-manager`、`list`、`verdict`、`blog`、`executor`(工具)、`fork-pty`、`return`、Meditator/Executor 角色路径。

---

## 同步返回路径（EXEC-028）

### A. Residual OneShot（dispose-after）

仅 residual 非 dedicated callers。算法：create child → subscribe-before-send → await 一次 terminal →
finally abort/dispose child。

成功 wire：COMPANION-003 物化 child LWR（`includeOpening=false`）→ `SyntheticToml.comment` 作
entry-local 注释，后接末条 TurnFormalText；禁止 `work_record` 字段。与 Join 共用物化器；wire 面独立于
批次。Opening 在 send 前从原始 assignment 捕获（对齐 fork）；返回的 LWR 仍为
`includeOpening=false`。fail-closed：`Completed` 若无法物化出非空 LWR，返回工具级失败，绝不静默
退回仅 formal report 的成功。encode 的 soft-omit 仅覆盖非 Completed 的 soft Ok（如 send-failed）；
Completed 缺 LWR 在 run 内 fail-closed，不改 encode 行为。

### B. Reusable SyncDelegate（ordinary completion → bounded WorkRecord）

Dedicated `inspect` / `establish-behavior` / `repair-behavior`（EXEC-026/031）算法：

```text
use! scopeLease = syncDelegateGate.Acquire(immediateCallerReuseScope)
let! delegate = attachedSessions.GetOrCreate(ownerReuseScopeId, role)  // tier from owner
do! promptDispatcher.Send(delegate, message)
let! completion = await ordinary Assistant Completion
let workRecord = materializeBoundedWorkRecord(
      InvocationStartCursor .. InvocationEndCursor,
      includeOpening = false)
return project workRecord to caller
```

**删除**独立 `return(message)` 工具、`Returned` await、`Returned → Completion` 双 await、
`completion_text` / `SyncDelegateReturnCompletion` magic literal、TextComplete 改写路径。

顺序钉死：

```text
ordinary Assistant completion
→ Host 物化 bounded WorkRecord（当前 invocation range）
→ caller 收到投影（答案在 Closing report，无额外 answer 字段）
```

成功路径不 `AbortSession` / 不 dispose dedicated Session；abort 只保留给失败、取消与 ReuseScope
teardown。同 immediate caller ReuseScope 同时最多一个 active sync call；下一同步调用必须等上一
Completion，防止与 terminal 尾部重叠。

### Serialization / tier 机制

- Gate key = immediate caller ReuseScope（**禁止** family-root gate）。嵌套
  `DevOps waits Coder` / `Coder waits Inspector` 各持本层 scope lease，可并行完成而无 deadlock。
- `GetOrCreate` 绑定 owner effective tier → 固定 delegate Agent（fast/deep）；调用参数不得覆盖 tier。
- 不暴露 `inspector_id` / `coder_id` / `agent` / `tdd`。

---

## Student / Teacher / Meditator / Executor — 已删除（absent）

`Role.Student` / `Role.Teacher`、Learn/Compile、`StudentQaStore`、`teacher` 工具、
`TeacherIdleNudge` / `StudentCompile` / `StudentCompileNudge` 与 `StudentTeacherRuntime`
**不存在于生产**（EXEC-027 / AGENT-020…022 / PROMPT-012 空缺）。

`Role.Meditator` / `Role.Executor`、`fork-manager` / `list` / `verdict` / `blog` / `return`
**不存在于生产**（GrandRewrite clean-break；AGENT-002/006/024）。不得写成 pending / 过渡 / 双写。

后继：SyncDelegate ordinary completion → bounded WorkRecord（EXEC-028/031）；idle 为
`SyncDelegateIdleNudge`（PROMPT-003）。**无**独立 `return` 工具。
