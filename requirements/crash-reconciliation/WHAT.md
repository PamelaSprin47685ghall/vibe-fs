# crash-reconciliation — 唯一 normative 合同

条款 ID 前缀：`CRASH-`。本文每个命题都是**当前世界必须同时成立的事实**；测试落点见 PROOF.md。
术语：durable facts = EventStore 中已提交的不可变事件；可信物理观察 = Host SDK snapshot、
Git ref/head、PTY onExit 等可复核的外部事实；process-local 状态 = 仅存在于当前进程内存的
标志/permit/waiter/sensor。

## CRASH-001：process-local 状态不是恢复权威

进程重启后，以下 state **不得**被当作恢复依据：armed 标志（`armedByFailure`、
`RecoverySlot.afterRestart = NotArmed`）、`LoopKillArmed`、`QuiescencePermit`、
`SessionQuiescenceGate` 内容、detector 状态。它们允许在崩溃后安全消失；没有 fresh evidence 就
没有自动 effect（shape/host.md：gate 重启清空，无 fresh idle → 无 permit → 不自动发送
idle-derived continuation）。

含义：崩溃丢失临时状态是**安全侧**（fail-closed），不是需要修复的 bug；把临时状态写进日志或
Journal 冒充恢复协议才是 bug（HOST-007）。

## CRASH-002：重启从 durable facts + 可信物理观察重建世界

恢复输入只有两类：durable facts/projections（Journal fold 结果）与可信物理观察（Host snapshot、
Git ref 等）。禁止用缓存、墙钟时间、「上次大概做到哪」的日志散文推断状态。

含义：`SessionRecoveryWorkflow.recoverFamilyDirect` 从 `RecoveryClosureProjection.discover`
（durable 关联）构建 family；`ChildRecoveryWorkflow` 读 durable handle projection + Host
snapshot；ORCH-007 从每个活跃 Job 的最后事实决定唯一恢复动作（`change-integration` 域内应用）。

## CRASH-003：未决外部 effect 先 reconcile 再决定是否可重试

outcome unknown 的外部 effect 不得被当作「未发生」而重放。reconcile 的观察分类：
`finish=None` 的稳定 snapshot 是 reconciliation 私有观测 `TurnUnknown`
（`SnapshotObservation`），**不是**可 publish 的 `TurnOutcome`（HOST-004）。Requested/Accepted
分型法律属 `effect-accounting`；本包保证恢复路径在消费该分型之前先完成 reconcile。

## CRASH-004：恢复复用普通 workflow 入口，不发明程序计数器

恢复 = `Journal facts → Fold → 纯恢复决策 → 普通 workflow 合法入口`（FLOW-005/DSL-004）。
禁止恢复 Program 节点、continuation 或「执行到第几步」；禁止 `RecoveryStage` /
`EnsureRecoveryDone: Task<unit>` 之类的第二状态机。`ReconcileDecision` 只有 observation
vocabulary（Reread / Publish / StopPass），不含业务 repair 名字。

## CRASH-005：ambiguous / multiple / missing 证据 fail closed

恢复证据不足时必须显式停在 `Waiting` / `Blocked` / `RecoveryIncomplete`，而不是猜一个继续：

```text
SessionRecovery = NoRecoveryRequired | Recovered | Waiting | Blocked
ChildRecoveryResult = RecoveredActive | RecoveredTerminal | RecoveredAbandoned
                   | RecoveryIncomplete | RecoveryBlocked
```

`SnapshotUnreadable`（真读错误）→ `RecoveryIncomplete`（等待，不发 permit，不是硬 block）；
冲突 / retired / 多个匹配 → `Blocked`。Append CAS retry 耗尽且 EventId 仍不在 store → fail
closed（PERSIST-003）。Attached restore 中 journal 关联 id 匹配但 agent/title 冲突、或多个 id
匹配、或查询失败 → fail closed（HOST-009）。

## CRASH-006：没有 fresh evidence 就没有自动 effect

恢复闭合后，副作用入口必须持有证明：`FamilyRecoveryPermit`（family 恢复闭合的唯一凭据）才可
join；`QuiescencePermit` 在发送边界 fresh 才可发 idle-derived continuation（`TryConsume` 失败
→ Superseded，不写 claim 不发消息）。线性序：permit → join，禁止跳步（EXEC-023）。

## CRASH-007：TurnUnknown 是 reconciliation 私有观测

`TurnUnknown` 不得作为 `TurnOutcome` case 发布；`publishDecision` 在类型上不可接收它。
Unknown 交接用 placeholder Outcome 做 provisional seal / dedupe，业务侧（TurnWorkflow /
InteractionRepair）在持有 quiescence 时才决定是否 missing-final-report。

## CRASH-008：abort 是 typed 控制面，不是 ProviderFailure

Host 的 `MessageAbortedError` / `AbortError` 解码为 typed `AttemptAborted`，撤销当前 attempt
的全部 idle-derived continuation capability，原样 signal Reconciler 的 `AbortWake`；禁止改写为
`ProviderFailure`（不推进 fallback）。`AttemptAborted` 分支先 `RevokeCurrentAttempt`，再进
Reconciler。

## CRASH-009：child recovery 没有 Aborted 终态

`ChildFinality = Succeeded | Failed | Abandoned`，无 Aborted。aborted-only 观察
（`HostObservation.AbortedObserved`）**永不**成为 terminal 证据：durable 侧没有 fake aborted
fact，snapshot 侧只接受 terminal-completed assistant 的正文。`JoinableCompletion` 只由
`fromDecoded`（decoded v2 terminal）或 `tryFromProvenTerminal` 构造，禁止 raw JSON /
kind+body / 任意 body 字符串充当证明（EXEC-021）。

## CRASH-010：恢复结果分支穷尽，Waiting ≠ Blocked

恢复分支必须穷尽且语义互斥：`RecoveredActive`（child 还活着，恢复步骤完成）≠
`RecoveryIncomplete`（缺 terminal 证据，必须等）；`Waiting`（transient/unreadable，不发 permit
但可等）≠ `Blocked`（硬失败）。`HandleFamilyRecovery.HandlesWaiting` → `SessionRecovery.Waiting`
（不是 Blocked）；`JobRecoveryUnknown` → Waiting（不是硬 FamilyBlocked）。

## CRASH-011：线性序 permit → join，每 join 重新验证

`AwaitAgentWithPermit` 每次定向 await 前重新 requirePermit；校验通过才可读目标 agent 的
Journal 权威 completion。TCS/Pulse 只作唤醒，不构成第二份 RunCompletion 真理源。permit 携带
closure members（不止 digest）：join 时检查 recovered 的每个 member 仍在场——丢失成员拒绝
（`FamilyRecoveryPermit.missingFrom`），恢复后新增的成员合法（monotone admission）。

## CRASH-012：completion 单一 owner

`HandleController.recordCompletion` 只接受 `JoinableCompletion`（Succeeded | Failed finality），
blob 先于事实（PERSIST-007）；fold 拒绝第二次 claim。`ChildRecoveryWorkflow` 是生产唯一调用方；
`recordCompletion` 后仅 Pulse agent handle（唤醒），Journal 是事实源。`retire` tombstone 让已
消费 completion 不可重复投递（重启后不会把同一次完成再投一次）。

## CRASH-013：combine 优先级 Blocked > Waiting > Recovered，按层序无关

`SessionRecovery.combine`：Blocked 优先于 Waiting 优先于 Recovered 优先于其它；同层内顺序无关。
`authorizeFamilyResume`：any Blocked → `FamilyBlocked`（硬，无 permit）；else any Waiting →
`FamilyWaiting`（无 permit，消费方等待）；else `FamilyReady`（私有 permit）。

## CRASH-014：closure 校验与 permit 单调准入

`validateClosurePure`：closure 中同一 session 出现两次 → `RecoveryCycle` block（fail closed）。
`RecoveryNode.token` 是稳定成员身份（W:/A:/C:/B:/M:/R: 前缀）；permit 的 closureMembers 必须
仍被当前 family 包含——丢失拒绝、增长合法。

## CRASH-015：Attached restore 复用/替换/fail-closed

重启后 Attached 创建：有 journal 关联（`RestoredSessionId`）且恰好 1 个 id+agent+title 匹配 → 复用；关联 id 不存在 → Replacement（新建）；无关联 → 不复用任何候选直接新建；id 匹配但 agent/title 冲突、多个匹配或查询失败 → fail closed。登记顺序：先写 `SessionAssociation`，再发首个 prompt（HOST-009）。

Replacement 不得把新的 attached child 直接覆盖到仍然存在的 durable association 上：physical old child 被证明永久消失后，必须 `create fresh → Close(old durable association) → Link(new)`。对 Companion 而言，旧 `CompanionBloggerLinked(old)` 尚存在时直接 append `CompanionBloggerLinked(new)` 按 COMPANION-002 必须拒绝；正确 recovery 不能靠 semantic-cut“帮忙重置”。

## CRASH-016：Blogger 崩溃窗口按 durable + snapshot 分类

`BloggerCrashRecovery.reconcile` 对 open request 窗口分类（unsent / tool-present /
in-flight），只从 durable 与 Host snapshot 判据得出 `WindowOutcome`；snapshot 不可读 →
`Unreadable` → `SessionRecovery.Blocked`。`tool-present` 只由 snapshot **最新 assistant** 的具名
`SessionToolPart` 证明：raw `chronicle` 总数必须恰好 1，且该唯一 part 必须 `Completed`；历史旧
chronicle、2+ raw chronicle、pending/failed/statusless 都不得把 open request 误判为已 recommit。
恢复机会经 `HostTurnObserver` 观察，不自行发消息。Blogger protocol 的 AABB 阶段若由 idle 路径实际发出，则以 durable `blogger-aabb` InteractionRepair claim 作为“预算已花掉”的恢复证据；纯 snapshot/transcript 本身不得凭空推导 AABB consumed。

## CRASH-017：工具中断不恢复；未来 session 续传必须显式

plugin load、workspace open、EventStore acquire、Host signal subscription 都不是 recovery trigger。更进一步：tool call 本身没有 crash recovery owner。进程死亡时正在执行的 Fission、NEEDHELP consultation、js-* transaction、fork/join/tool workflow 等均按“该次工具执行已中断/失败”处理；不得在新进程中自动 abort/send/rollback/replay/补 terminal，也不得由下一次普通 tool invocation 偷偷替上一工具善后。

未完成 durable facts 只保留为可审计历史证据。旧 session 若未来支持断点续传，只能由用户显式 `/continue`（或等价显式命令）触发：恢复入口必须先把“进程已重启、上一条工具执行中断、历史可能含未完成 sub session”作为可见上下文交给 LLM，再由 LLM 基于公开历史选择复用哪些 sub session。禁止透明续跑、隐藏断点、伪造上一 tool 成功或把坏 tool 从 transcript 抹掉。

一个 feature 的旧未完成状态不得阻断 OpenCode plugin load；普通新 session 也不得因旧 tool 残留被自动恢复逻辑劫持。

## CRASH-018：`/continue` 是唯一显式 session resume；重启断点必须暴露给 LLM

用户在已有 session 中显式执行 `/continue` 时，Wanxiangshu 才可查询该 session 的 durable child linkage 与 Host physical session snapshot，并把仍可访问的 sub session **仅重新登记到当前进程 runtime** 供后续显式复用。`/continue` 自身不得补写旧 tool terminal、不得把 Active/Completed/Retired handle 偷偷改成成功、不得发送新 charge、不得自动 join/abort/replay；真正的新业务 effect 必须来自 LLM 在看到 resume briefing 后作出的后续 tool call。

`/continue` 交给 LLM 的正文必须明确写出：① OpenCode/Wanxiangshu 进程刚刚重启；② 重启前最后一个 tool call 可能中断，保持 transcript 中的坏/未完成状态，不能假定成功；③ 哪些 sub session 仍物理可访问（至少 byname、session id、role/agent、durable lifecycle）；④ 哪些 durable child 已不可访问；⑤ 若要继续，LLM 可用正常 `fork(name=已有 byname, charge=...)`/等价已有复用面选择性复用。禁止把 restart disclosure 放在隐藏日志、system-only side channel 或仅诊断字段里。

`/continue` 自身产生的 provider turn 是 **disclosure-only**：`command.execute.before` 必须给该次 restart briefing 的 Host user part 加专用 typed metadata marker；普通 main transform 只在**当前 trailing user material** 带此 marker 时禁止把 briefing 当成新的 Companion material，从而创建/替换 Blogger、补旧 context-compression work 或间接恢复上一进程工具。该 suppression 不以 SessionId、idle、abort、delete 或“session 是否结束”为生命周期：同一 marked material 的 provider retry 仍受抑制；一旦下一条普通 user material 成为当前请求，即使此前没有任何 end signal，也必须自然恢复正常 Companion 行为。这样显式 resume 只登记/公开，真正业务 effect 来自后续 LLM tool call，而且不会把可复用 session 错当成一次性 execution。

`/continue` 重复调用必须幂等：重复发现/登记同一 surviving child 不产生 durable fact，不重复发送 prompt，不改变 child transcript。没有 durable journal、没有 snapshot port、某 child snapshot 查询失败都只影响本次 briefing 的对应条目；command 本身仍返回可见说明，不熔断 future `/continue` 或其它功能。

## 反向覆盖说明

`p0-recovery-join` gate（`scripts/checks/p0-recovery-join.mjs`）是共享 checker
（MECHANISM），SPLIT 后本包拥有其 **recovery 部分**规则：`restore-handles-none-no-recovery`、
`recover-job-none-no-recovery`、`spike-restore-handles-none`、`host-fork-runtime-recovery-task`、
`host-fork-runtime-await-recovery-call`、`host-fork-restart-proof-structure`、
`record-completion-single-owner`、`session-ports-restore-handles-mandatory`、
`session-ports-recover-jobs-mandatory`、`child-recovery-result-five-cases`、
`joinable-from-decoded`、`join-with-permit-closure-digest`、`join-tool-family-recovery`、
`join-tool-family-blocked`、`executor-tool-require-permit`、`distillation-join-with-permit`、
`distillation-runtime-join-with-permit`、`mailbox-pulse-agent-handle`、
`false-completion-rejected-fact`、`parent-join-correction-fact`、`fork-recovery-synthetic-restored`、
`fork-recovery-interrupted-finality`、`ensure-recovery-unit`、`missing-ports-family-ready`、
`lifecycle-aborted-record`、`lifecycle-aborted-setresult`、`awaiting-evidence-case`；
**effect-accounting 部分**（aborted≠terminal：`agent-aborted-type` 等）不归本包。
