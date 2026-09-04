# crash-reconciliation — WHAT

## CRASH-001: process-local 状态不是恢复权威

进程重启后，所有 process-local 状态（包括 `armedByFailure`、degeneration-guard armed anomaly、`QuiescencePermit` 与 detector 状态）全部清空，绝不得作为恢复权威。没有 fresh evidence 严禁自动产生任何副作用。

## CRASH-002: 重启从 durable facts + 可信物理观察重建世界

系统恢复仅允许两类输入：EventStore 中已提交的不可变事件及其 fold 投影，以及 Host SDK 快照、Git ref 等可信物理观察。严禁使用缓存、墙钟时间或日志散文推断状态。

## CRASH-003: 未决外部 effect 先 reconcile 再决定是否可重试

结局未知的外部 effect 严禁直接视为未发生而盲目重放。Reconcile 观察中 `finish=None` 的快照属于私有观测 `TurnUnknown`，必须等待静止证据后由业务层决定处理策略。

## CRASH-004: 恢复复用普通 workflow 入口，不发明程序计数器

恢复过程遵循 `Journal facts → Fold → 纯恢复决策 → 普通 workflow 合法入口`。严禁恢复 Program 节点、continuation 或执行步数，严禁引入 `RecoveryStage` 等第二状态机。

## CRASH-005: ambiguous / multiple / missing 证据 fail closed

恢复证据不足、冲突或缺失时，系统必须显式停留在 `Waiting`、`Blocked` 或 `RecoveryIncomplete` 分支，严禁猜测继续。

## CRASH-006: 没有 fresh evidence 就没有自动 effect

恢复闭合后，所有副作用操作必须持有有效证明：持有 `FamilyRecoveryPermit` 才能执行 join；持有保持 fresh 的 `QuiescencePermit` 才能发送 idle-derived continuation。quiescence 是物理条件的合取：当前 provider attempt 已被 Host 观测为 idle，且该 SessionId 没有仍在执行的 tool body；Host 的 `SessionIdle` 若先于 tool completion 到达，只能建立待静止证据，permit 在最后一个 active tool 结束前不可消费。新的物理用户输入到达时立即幂等撤销旧的静止许可。permit 在物理发送边界被消费；若 Host 明确证明 acceptance 前拒绝、且同一 attempt serial 仍未被更新材料取代，则允许把该 exact permit 从 `IdleConsumed` 原子归还为 `Idle`，使仍未满足的 gate 可重试。任何更新的 provider attempt、物理用户材料或 acceptance-unknown 都使归还失败。

## CRASH-007: TurnUnknown 是 reconciliation 私有观测

`TurnUnknown` 仅为 reconciliation 内部观测，严禁作为正式的 `TurnOutcome` 对外发布。

## CRASH-008: abort 是 typed 控制面，不是 ProviderFailure

Host 的 abort 信号解码为类型化的 `AttemptAborted` 控制面事件，撤销当前 attempt 的所有 continuation 能力，并唤醒 Reconciler；严禁改写为 `ProviderFailure`。

## CRASH-009: child recovery 没有 Aborted 终态

Child 终态仅包含 `Succeeded | Failed | Abandoned`，不存在 Aborted 终态。单纯的 abort 观察绝不构成 terminal 证据，JoinableCompletion 必须具有真实解码正文。

## CRASH-010: 恢复结果分支穷尽，Waiting ≠ Blocked

恢复结果分支必须语义互斥且穷尽：`RecoveredActive`（活跃运行中）≠ `RecoveryIncomplete`（缺少终态证据需等待）；`Waiting`（瞬态等待）≠ `Blocked`（硬性失败阻断）。

## CRASH-011: 线性序 permit → join，每 join 重新验证

每次执行 join 之前必须重新验证 `FamilyRecoveryPermit`。Permit 携带恢复闭包的成员集合；若已恢复成员丢失则拒绝执行，恢复后新增成员允许单调准入。

## CRASH-012: completion 单一 owner

HandleController 的 `recordCompletion` 是提交完成态的唯一入口，采用 blob 先于事实的原则，拒绝重复 claim，并通过 retire 墓碑保证重启后完成态不重复投递。

## CRASH-013: combine 优先级 Blocked > Waiting > Recovered，按层序无关

多个恢复结果合并时，优先级严格满足 Blocked 优于 Waiting 优于 Recovered；同层级内的合并与输入顺序无关。

## CRASH-014: closure 校验与 permit 单调准入

闭包中若出现重复 session 则判定为 `RecoveryCycle` 并 fail-closed 阻断。Permit 校验要求闭包成员单调不丢失。

## CRASH-015: Attached restore 复用/替换/fail-closed

重启后附加子会话恢复时：匹配唯一关联 ID、agent 与 title 时复用；关联不存在时新建；发生冲突或多重匹配时 fail-closed 阻断。Replacement 必须先证明旧物理会话消失，显式执行 Close 后再 Link 新会话。

## CRASH-016: Blogger 崩溃窗口按 durable + snapshot 分类

对未完成的 Blogger 请求窗口，严格基于 durable 事件与 Host 快照（最新 assistant 的唯一 completed chronicle）分类为 unsent、in-flight 或 tool-present，快照不可读时阻断。

## CRASH-017: 工具中断不恢复；未来 session 续传必须显式

工具执行本身不设隐式的崩溃恢复 owner。进程死亡时正在运行的工具调用均按中断处理，严禁在新进程启动时自动重放、补写完成态或隐式修复。

## CRASH-018: `/continue` 是唯一显式 session resume；重启断点必须暴露给 LLM

用户显式执行 `/continue` 是唯一的会话续传入口。系统仅重新登记物理可访问的子会话，并将进程重启、中断工具状态与可复用会话清单作为公开 briefing 放入 provider-visible 消息中，由 LLM 根据公开历史决定后续工具调用。续传材料保持 disclosure-only，不触发自动业务转换。

## CRASH-019: 外部 effect 必须逐项闭合 crash reconciliation 合同

每个高价值外部 effect 必须在唯一 owner 下登记类型化 `intent → process-local admission → physical receipt → durable outcome`；不适用阶段必须给出明确理由。登记项必须锚定物理 effect identity、有限且穷尽的歧义状态、查询或补偿入口、安全重试律（仅 `proven-not-applied` 或 `never`）、以及复用普通 CE 的重入入口。Host、provider、Git 与 process 边界必须同时具有确定性歧义证明和 Adapter 或 Long-Stroke 证据；证据层级由 verification owner 的独立 registry 按精确 `(path, title, WHAT)` 唯一分类，effect 行自报、改标或未登记分类均不得计入证明。Prompt dispatch 必须锚定物理发送前的 process-local `physicalAdmission`；Blogger 的外部 receipt 是 `TransportReceipt`/`PluginPromptSubmitted` 以及随后接受的 `PhysicalUserMessageId`，不是预先可派生的 `PromptKey`。恢复不得持久化 capability、continuation、`ResumeAt`、`RecoveryStage`、`RecoveryStep` 或 `NextAction` 程序计数器；未知、冲突、缺失证据一律 fail closed。登记的 owner、WHAT、source symbol 与 executable proof title 均为精确锚点，重复 WHAT ID、过期锚点或未闭合 effect 必须使 gate 失败。
