# 执行模型 — 可观察行为

条款前缀：`EXEC-`。  
Handle / PTY / Mailbox 所有权见 `shape/execution.md`。  
Join 批次、blob、进程预算见 `how/execution.md`。

## EXEC-001：Fork/Join/Horizon

| 角色 | 工具 |
|------|------|
| Manager | `fork` / `join` / `horizon` |
| Orchestrator | `commission` / `join` / `horizon` |
| DevOps | `open-terminal` / `send-terminal` / `read-terminal` / `signal-terminal`，以及 `join` / `horizon` |

## EXEC-002：Fork 语义

- 新建：`calling`（Persona 名）+ `name`（Byname）+ 非空 `charge`。  
- 续做：省略 `calling`，按 `name` + 当前 `charge` 识别既有 person；不暴露 AgentId，不用 `reuse` flag。  
- 续发必须沿用该 person 已绑定的 managed agent 与其 model；不得把 `deep-*` 换成 `fast-*`。  
- Busy existing：不新 RunId、不新 listener、不新 completion；nudge 归属当前 active Run。  
- 成功：仅 Byname 承接 charge 的自然语言后果；不返回 agent_id / role / tier / fallback_peer / worktree。

## EXEC-003：终端动词语义

四个不同动词、四个不同 contract（删除 `fork-pty`）：

- `open-terminal(name, command)`：打开  
- `send-terminal(name, input)`：写入  
- `read-terminal(name)`：读取增量  
- `signal-terminal(name, signal)`：发信号  

不向 provider 返回 `pty_id` / `closed` / `status`。

## EXEC-004：Join 语义

Join 消费当前 owner 可用 completion，有界批次；agent 完成项为 entry-local WorkRecord / LWR（`includeOpening=false`），禁止字段式 `work_record` DTO。  
**禁止**向 provider 投影 `status` / `count` / `ordinal` / `kind` / `agent` / `code` / `message` 等通用 DTO。后果用自然语言 + WorkRecord（或 terminal 的 `exit_code` + 相关输出）。  

DevOps 角色的 `join` 在无完成项时包含 10s 等待预算（`DevOpsJoinTimeoutMs = 10_000`）；若 10s 内无 completion，结束本次等待并以自然语言告知等待结束（Host 事实 `DeadlineExpired`）；**不**暴露 `TIMED_OUT` / `status="failed"` / `code=...`。Orchestrator 与 Manager 的 `join` 无此 10s 预算。  

工具调用中止（operator abort）与外部用户入站均为中断后果（自然语言），非 error（EXEC-017）。

## EXEC-028：同步返回语义（OneShot vs SyncDelegate）

同步 agent 工具有两条互斥生命周期路径。**不得**混用：OneShot 的 dispose-after 不得套在 dedicated
SyncDelegate Session 上；SyncDelegate 不得退化成 OneShot dispose-after。

### A. Residual OneShot（dispose-after）

仍用于**非 dedicated SyncDelegate** 的 residual one-shot callers（若有）：每次调用新建 child Session，
成功完成后 abort/dispose child，不跨调用复用。

成功完成时：entry-local LWR / WorkRecord（`includeOpening=false`）+ 末条 TurnFormalText 报告；禁止字段式
`work_record` DTO。LWR 物化与子→父方向同 COMPANION-003（与 EXEC-004 共用物化器，非 Join 批次 wire）。
Opening 从原始 assignment 捕获（对齐 fork），以便 COMPANION-003 物化可运行；返回的 LWR 仍为
`includeOpening=false`。若 Completed 无法物化出非空 child LWR，则 fail-closed：显式工具失败，
绝不静默退回仅 formal report 的 soft success。

### B. Reusable SyncDelegate（ordinary completion → bounded WorkRecord）

Inquiry / Coder / DevOps 的 dedicated `inspect` / `establish-behavior` / `repair-behavior`
（及同类 SyncDelegate）走本路径。**删除**独立 `return` 通道与 `Returned → Completion` 双 await。

语义由 EXEC-031 规定：callee 普通 Assistant completion → Host 物化该次 invocation 的 bounded WorkRecord
（`includeOpening=false`）→ 投影给 caller。

### Serialization 与 tier（行为面）

- Serialization key = **immediate caller ReuseScope**（非 family root）。嵌套
  `DevOps → Coder → Inspector` 合法；同 caller ReuseScope 禁止并发两个 active sync delegate calls。
- Owner effective tier → deterministic delegate tier（`fast→fast`，`deep→deep`）；不可每轮选 Agent。复用既有 child 时沿用其已绑定 managed agent，不得把 `deep-*` 换成 `fast-*`。
- Session 按 `(OwnerReuseScopeId, role)` 复用（EXEC-026）；不以 OneShot dispose-after 为准。

## EXEC-005：Horizon 语义

`horizon()` 是 pull-only snapshot：调用者需要朝向时主动看一次；不得 timer 轮询、后台订阅、`AwaitChangeFrom` watcher 或自动刷新。每次调用只观察一个当前 journal snapshot。

返回当前在场名册（Byname / TerminalName 等）：谁还在远方、谁已归来、终端是否仍开。对每个 parent-visible subagent，同时显示该 child session 在这个 snapshot 中最新一条 durable 工作记录；内部来源是最新 `BlogFrame`。若尚无 frame，以自然语言说明尚无工作记录。若最新 frame 的 blob 缺失或 digest 无效，不得退回更旧 frame 冒充“最新”，而应以自然语言说明最新工作记录当前不可读。Terminal 没有工作记录。

不是可创建 Agent 菜单。无 `status` / `id` / `kind` / `ordinal` 等状态机词汇。

## EXEC-006：Child Run 生命周期

Child Run 生命周期与父背景记录分离。

## EXEC-007：Nudge

Nudge 是 Continuation（PROMPT-003），不建新 Authority。

## EXEC-008：Parent Background

父背景记录不冒充 child completion。

## EXEC-015：PTY 行为

PTY completion **只**由 backend `onExit` 触发。禁止 stdout 启发式「看起来结束了」。

## EXEC-016：Background Join Guard

有 join 义务且仍有 outstanding 后台时，本 turn 只发 JoinGuard Continuation；finality 处理停放，Manager 不做 idle 鼓励（GLORY-029/070）。

## EXEC-017：Join 中断不是错误

join 等待直至：completion 可用 / 本地 operator abort / external-user ingress 唤醒 / 适用的 DevOps deadline。中断是 `JoinWaitOutcome.Interrupted of JoinInterruptReason`，不是 ForkError。`JoinInterruptReason` = `OperatorAbort` \| `UserMessageArrived` \| `DeadlineExpired`。

External-user ingress 只打断**当前** wait：不 cancel mailbox/runtime/session/child，也不本身授予 Prompt authority。每个 `join` 入口先建立一个 `JoinAttempt`；消息只 fan-out 给该 Session 当时 active 的 attempt。无 active attempt 的消息仍进入正常 Host 队列，但作为 join wake 丢弃，绝不 latched 给 future join。任意 race 唤醒后，已可用的 completion 先 drain，再才发出 interrupt 结果。

operator abort 先打断当前 `JoinAttempt`，使 join 返回 operator-abort 自然语言后果；同一次 Esc 随后终止父 provider attempt，`TurnAborted` cleanup 必须取消该父全部仍在运行的 sub-session。已经完成并进入 `CompletedAwaitingJoin` 的结果仍可消费。与之相对，external-user ingress 不产生 `TurnAborted`，不得取消任何 sub-session。

provider wire：**禁止** `status` / `code` / `message` DTO。operator abort / user message / DevOps deadline 均以自然语言后果表达（EXEC-004）。tool abort ≠ runtime.Cancel。中途用户消息可唤醒 join，不经 AcceptHumanRoot、不重置 LogicalRun、不新建 Manager Life（PROMPT-004 不变，fail-closed）。

## EXEC-020：Agent 终态代数（无 ABORTED）

```text
Completed | Failed | Abandoned
```

**ABORTED 不是 agent 终态。** 取消是控制面。

## EXEC-021：completion blob v2

schemaVersion=2；finality 仅 `completed|failed`。  
`LegacyFalseAbort` **永不**成为 RunCompletion。  
`fromDecoded` 唯一构造。

## EXEC-022：假 completion 补偿

`HandleFalseCompletionRejected` → 确定性 replacement → parent correction。  
禁止把历史假 abort 洗成成功。

## EXEC-027：（空缺）Student 学习与编译程序 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 Student HumanRoot→QA→`StudentLearn`→`teacher`→
`StudentCompile`→SKILL/`return` 程序，以及 Teacher/Compile idle nudge 与 `StudentQaStore`。
无 alias、无 deprecated 执行路径。

后继：SyncDelegate ordinary completion → bounded WorkRecord（EXEC-028/031）；idle 为
`SyncDelegateIdleNudge`（PROMPT-003；`shape/host.md` quiescence gate）。**无**独立 `return` 工具。

## EXEC-029：Commission 语义

Orchestrator 专用：`commission(calling?, name, charge)` 委托独立集成之路给 Manager
（`fast-manager` / `deep-manager` 新路，或按 Byname 续做既有路；同 job / worktree / session 续做属墙内事实，见 AGENT-015、GLORY-068）。

- 与 Manager `fork` **不同 contract**（commission = independent road；fork = witness within mission），故不同名。  
- `calling` 在场 → 新路；缺省 → 续做既有路。  
- 成功：仅 `# <Byname> has taken your charge.`（或等价自然语言）。  
- **禁止**向 provider 返回 `job_id` / worktree / `reused` / agent / role / tier / fallback_peer。

## EXEC-030：Provider leak 禁令

provider 输出与工具后果**不得**包含下列机器拓扑（Host/Journal 墙内可保留精度）：

```text
SessionId / AgentId / ManagerJobId / PtyId / FissionGroupId
lane_index / worktree path / fallback offset / fast-|deep- binding 自称 / spool path
status / code / message 通用 DTO（Join/horizon 等）
```

例外仅限语义驱动的精确观测字段（例如 terminal/`run` 的 `exit_code`、非空 stdout/stderr）。  
机器态可存在；穿过 horizon 的只能是后果与 WorkRecord。

## EXEC-031：SyncDelegate 无 return / bounded WorkRecord

Dedicated SyncDelegate（`inspect` / `establish-behavior` / `repair-behavior`）：

```text
caller 发起同步委派
→ admission / single-flight 成功
→ 构造 typed SyncDelegatePromptRequest { Charge; ProviderPrompt }
→ specialist 按普通 Work Session 工作
→ ordinary Assistant completion 结束本次 invocation
→ Host 物化 bounded WorkRecord（InvocationStartCursor .. InvocationEndCursor，includeOpening=false）
→ caller 收到该 WorkRecord 投影
```

不变量：

- **删除** `return(message)` 工具与 `Returned → Completion` 双 await。  
- **删除** `completion_text` magic literal。  
- Reusable session 记忆可跨调用保留；每次 caller **只**看见当前 invocation range。  
- `Charge` 是 semantic assignment、Opening/Casebook Q；`ProviderPrompt` 是实际发给 provider 的字节。没有 warm-start 时两者字节相同；有 AGENT-032 keywords 时只 enrich `ProviderPrompt`。禁止解析 rendered TOML 反推出 Charge。  
- 不暴露 `inspector_id` / `coder_id` / `agent` / `tdd`。  
- 答案就是 bounded WorkRecord 本身，不是额外 `answer` 字段；最后一条助手文本在 Recent work（无 Closing report 段）。

## EXEC-032：RepositoryWarmStart invocation timing

Warm-start 搜索属于 invocation admission 后、首个 provider send 前的 prompt preparation。SyncDelegate 必须先取得 ReuseScope single-flight，再调用注入的 `PrepareProviderPrompt`；通用 workflow 不依赖 Semble。搜索完成前不得发送 callee 首 prompt；搜索完成后也不得另发第二条“late hints” synthetic user message。

Fork 的新 work unit 同样保留原 `charge` 作为 child Opening，只把 warm-start envelope 作为该 work unit 的 provider prompt。reuse 时只有在确实开启一个新 work unit 时才能 enrich；对 active/busy reuse 不得先支付 Semble 成本后丢弃，也不得用 warm-start 改写既有 Opening。
