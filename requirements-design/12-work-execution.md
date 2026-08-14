# Work / execution

## `delegation`

WHY: 把语义工作交给另一 participant 时，必须明确是谁拥有这条工作、允许产生什么后果、返回什么可消费结果；否则 runtime topology 会冒充业务委托。

OWNS:
- delegation = semantic charge + entitled office + logical owner + bounded returned consequence。
- delegation by consequence，而不是按 persona 名或“看起来能做”。
- independent road 与 same-road continuation 的区别。
- sync delegate 与 asynchronous delegation 都不能泄漏 SessionId/AgentId/worktree 等机器身份作为业务 contract。
- returned WorkRecord/结果只改变 caller 的认识，不自动转移 authority。

DOES NOT OWN:
- office capability 本身。
- session ontology/lifecycle。
- provider rendering。
- 当前 `fork`/`commission`/`inspect` 工具名。
- 当前 dedicated session reuse HOW。

DEPENDS ON: `office-capability`, `session-ontology`, `managed-session-lifecycle`, `participant-horizon`。

PROVIDES: 可组合的跨 participant 工作委托语义。

FAILURE MEANING: RED = caller 无法从业务语义区分“创建独立工作”“续做同一路”“同步取证”，或 delegation 暗中改变 authority/personhood。

INDEPENDENT CHANGE: 把 sync delegation 从 dedicated reusable session 改成 one-shot invocation，而 returned consequence contract 不变。

CURRENT EVIDENCE: ARCH-017；AGENT-009/024；type `Kernel/SyncDelegate.fs`、`Domain/{SyncDelegatePrompt,ForkChildPayload}.fs`；wiring `Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore,ForkRuntime}.fs`；resource `resources/provider/tool/{fork,commission,inspect,sync-delegate}/`、`resources/provider/delegation/**`；sync-delegate、fork tests。

---

## `process-execution`

WHY: participant 控制真实 process/PTY 时，command、signal、output、exit 与 cancellation 必须对应物理世界，不能靠 stdout 文本或 Host DTO 猜完成。

OWNS:
- process/terminal 的 create/run/send/read/signal/exit semantics。
- command/signal 是 real-world acts；stdout/stderr 是 observation，不是 completion。
- physical completion 由 backend exit/terminal fact 建立。
- bounded execution、timeout/cancel 后资源收束。
- continuing process 与 one-shot execution 的区别。
- execution evidence 与 source mutation/report 的区别。

DOES NOT OWN:
- Coder/DevOps office authority。
- output 如何被蒸馏。
- generic time capability。
- current PTY/backend implementation。
- provider-visible DTO layout。

DEPENDS ON: `time-capability`, `host-boundary`, `participant-horizon`。

PROVIDES: 可信的物理执行与 completion evidence。

FAILURE MEANING: RED = stdout/transport 状态可冒充 process completion，或 participant 无法可靠区分 act、observation、exit。

INDEPENDENT CHANGE: 更换 terminal backend，而 command/exit/cancel semantics 不变。

CURRENT EVIDENCE: `docs/why/execution.md`；EXEC-015/017/020；type `Process/{Pty,PtySession,PtyTypes,PtyBackend,PtySupervisor,ProcessRunner,NodeProcessHost,NodeProcessWait}.fs`；failure onExit-only completion、`Process/Deadline.fs`；tests `tests/unit/process/**`。

---

## `output-distillation`

WHY: 真实执行输出可能大到不能原样进入 participant horizon；压缩必须保留会改变后续判断的事实，同时承认 fragment 的视野边界。

OWNS:
- large execution output → bounded observation 的语义压缩。
- preserve distinguishing facts、conflicts、uncertainty、locatability。
- fragment silence ≠ whole-run success。
- 合并多个 fragments 时不发明因果或成功率。
- distilled output 必须对未见原始输出的 reader 仍可用。

DOES NOT OWN:
- process control/completion。
- Reviewer judgement。
- generic context compression/history memory。
- Distiller 当前 Persona 名或 hidden-session mechanics。

DEPENDS ON: `process-execution`, `participant-horizon`。

PROVIDES: bounded but honest execution observation。

FAILURE MEANING: RED = 截断/压缩会把局部片段伪装成整体事实，或丢失足以改变下一动作的关键信息。

INDEPENDENT CHANGE: 把当前 Distiller agent 替换为 deterministic+LLM hybrid summarizer，而 process execution contract 不动。

CURRENT EVIDENCE: `docs/why/agent.md` Distiller；resource `resources/provider/role/distiller/`（fragment humility）；wiring `Agent/AgentProgram.fs`、distill tool；failure `Process/LargeGate.fs`、`Domain/ToolResultBound.fs`；Distiller surface tests。

---

## `change-integration`

WHY: 独立 Git 工作道路进入共享 ref 时必须保持短原子发布；长时间 review/repair 若持全局锁会把并行工作错误串行化。

OWNS:
- independent worktree/job 到 shared target 的 publish lifecycle。
- candidate/rebase/publish claim/CAS 的原子边界。
- shared ref mutation 的唯一短 critical section。
- conflict 后在门外 repair/review，再重新 claim。
- restart 后 publish outcome reconciliation。
- same-road continuation 与独立 road 的 integration identity。

DOES NOT OWN:
- Git 命令具体序列。
- general durable event store。
- review judgement 本身。
- Orchestrator Persona/guidance。
- generic effect accounting law。

DEPENDS ON: `effect-accounting`, `durable-events`, `crash-reconciliation`。

PROVIDES: 并行道路安全进入共享 repository state 的 publication guarantee。

FAILURE MEANING: RED = 并发 publish 可互相覆盖，或系统为了安全把长 review/repair 全部塞进全局锁。

INDEPENDENT CHANGE: 从 worktree+rebase 改为另一 candidate integration strategy，而 publish/CAS semantics 不变。

CURRENT EVIDENCE: `docs/{what,why}/orchestrator.md`；type/wiring `Infrastructure/Git/{IntegrationGate,GitGateway,WorktreeResource,HookDispatcher,GitOperations,GitSubject}.fs`；fact `Journal/{OrchestratorProjection,OrchestratorFactFold}.fs`；failure PublishClaimed 三分支 CAS、`Application/Reconciliation` restart reconcile；tests `tests/unit/orchestrator/**`。
