# provider-attempt-recovery — 唯一 normative 合同

条款 ID 前缀：`PAR-`。本文每个命题都是**当前世界必须同时成立的事实**；测试落点见 PROOF.md。
术语：Logical Run = 一次 Authority Root 决定的对话生命周期；ProviderRunIdentity = 一次物理
provider attempt 的稳定身份；cursor = FallbackCursor（Offset + ConsecutiveFailureCount）。

## PAR-001：Fallback 属于 Logical Run

Fallback 不是 Session 永久状态，也不是「模型槽位」。SelectedAgent 与 PeerAgent 构成 A/B 两侧；
新的 Authority Root 开启新的 Fallback 生命周期（Offset 归零，A 侧 = SelectedAgent）。跨 Run
**不得**继承上一次的连续失败计数或侧边状态。

含义：cursor 的出生是 `AuthorityRootAccepted` 事实（`Domain/AgentPairCursor.fs` 的
`forNewAuthorityRoot`），不是某个进程启动时顺手初始化的内存变量。

边界：Cursor 归零只由新 Authority Root 或用户显式恢复动作触发；本包不定义「用户显式恢复动作」
的 UI 语义。

## PAR-002：Cursor 是 modulo-4 封闭 DU，损坏字节 fail-closed

Offset 只有 0|1|2|3 四个合法值（`FallbackOffset = Fork0|Fork1|Fork2|Fork3`），byte 只存在于
codec 边界（`FallbackOffsetCodec.ofByte/toByte`）。反序列化遇到非法字节必须返回
`Result.Error(FallbackOffsetDecodeError.InvalidFallbackOffset)`，Journal load/fold 把该 envelope
当作损坏拒绝；**严禁** `invalidOp` 抛异常，也**严禁**在该路径构造 `CommitUnknown`（那是 Append
提交结局，不是 decode 结局）。

含义：非法状态在类型层生不出来；数据损坏是可预见失败，走 typed error 而不是程序事故。

## PAR-003：唯一写入口与同一失败只推进一次

唯一允许提交 `FallbackCursorAdvanced` / `FallbackExhausted` 的写入口是
`Application/Recovery/FallbackLedger.fs`（FALLBACK-003）。同一已确认失败（按
`FallbackAttemptIdentity = { SessionId; LogicalRunId; AuthorityRootUserMessageId; ProviderRun }`
去重）**最多推进一次**；第二次 observe 得到 `AlreadyRecorded`，不写事实、不推进、不通知 terminal。

含义：`ProviderRecoveryWorkflow.continueAfterConfirmedFailure` 中 `AlreadyRecorded` /
`NoActiveRun` 分支不产生第二个 continuation、不重复 `NotifyTerminal Failed`——第一个 observe
保持 owner。

边界：去重窗口有界（不随历史增长）；第二写入口（continuation 直写 cursor、raw retry 事件直写）
被 shape/fallback.md 明令禁止，静态 gate 见 `scripts/checks/` 相关检查。

## PAR-004：推进不变量

任意一次已确认失败的 provider attempt 恰好产生下列效果之一组：

| 结局 | Offset | ConsecutiveFailureCount | Authority 身份 | EffectiveAgent |
|---|---|---|---|---|
| 失败 | 前进一格（mod 4） | +1 | 不变 | 可因 Offset 侧变化而变 |
| 成功 | 不变 | 归零 | 不变 | 不变 |

成功**不写** cursor 事实——归零由 Host snapshot 的 Completed 派生（无第二写入口）。Offset 在
成功时不复位：一次失败后成功的 Run 会停在奇数 Offset，这是合法状态（见 PAR-011 的 parked-cursor
陷阱）。

## PAR-005：有限自动恢复预算

必须区分两件独立的事：A/A/B/B 侧循环**无界**（失败永远可以换侧，循环本身不判死）；自动恢复
预算**有界**（连续失败达到预算后停止自动物理请求）。默认 `AutoRecoveryBudget = 12`
（`Domain/AgentPairCursor.fs` `DefaultAutoRecoveryBudget`），可配置为其它有限正整数；无限不是
合法设置。达到预算写入 `FallbackExhausted`，不再自动发新物理请求；恢复路径只有新 Authority
Root 或用户显式恢复动作，两者都必须创建新 cursor。预算只数连续失败，不数时间（无 wall-clock
deadline）。

Host-facing 裁决：`FallbackLedger.admitConfirmedFailure` 把 `RecoveryExhausted` 映射为
`RecoveryAdmission.RecoveryExhausted`（停止自动请求），其余映射为 `ContinueRecovery`。

## PAR-006：侧序列与预算的维度分离

Offset 每失败前进一格，映射到 A/A′/B/B′（`sideSequence` 从 0 开始计数，无界）；第 B 次连续失败
落在 Offset=3（SideB）→ 前进到 0 → 立即 final，**没有自动的第 B+1 次**。

## PAR-007：Fold 拒绝条件

`FallbackProjection.applyAdvance` 拒绝（fail closed，不是吸收）：

```text
PreviousOffset 无法解码 → InvalidFallbackOffset
NextOffset ≠ (PreviousOffset + 1) mod 4 → InvalidTransition
ConsecutiveFailureCount ≠ 前值+1 且 ≠ 1（成功重置后重新起步） → InvalidTransition
ConsecutiveFailureCount > AutoRecoveryBudget → InvalidTransition
FallbackExhausted 之后同一 (LogicalRunId, AuthorityRoot) 再收 Advanced → AlreadyExhausted
```

含义：Journal 重放遇到损坏 transition 会停住而不是跳过；历史一致性优先于「尽量多恢复」。

## PAR-008：空 / XML-only terminal 不计入推进

空 terminal 或 XML-only terminal 不是已确认 provider failure：可以进入一次有界 Interaction Repair，但**不得**因此推进 provider fallback cursor 或消耗 provider failure budget。ordinary repair 的 authority budget 由 interaction-authority 定义为同一 LogicalRun + repair family 一次；repair 后仍无可用 terminal 时以 `INTERACTION_REPAIR_EXHAUSTED` 收束该业务 run，而不是继续生成 repair。Blogger exact-one 协议保留自己的 terminal-scoped 特例：第一次 invalid terminal 只获得一次 nudge；nudge 后仍 invalid 才开始记 confirmed failure 并进入 request-scoped AABB。首发 AABB 是 Blogger protocol 已赢得的 repair occasion，即使该次记账恰好把 generic cursor 打到 exhausted 也仍发送；此后每个新的 invalid AABB terminal 再推进一次同一 fallback projection，只有 projection 真正 exhausted 才停止继续 AABB 并收束业务 run。terminal validity 已 resolve 进 `AttemptOutcome`：`CompletedInvalid` 与 `Failed` 分开（`Participant/Provider/Attempt/RecoverySlot.fs`），因为前者是「回应完整但不可用」，后者才是「provider request 已确认失败」。

## PAR-009：Host Attempt 不是领域计数

`HostSignal.ProviderRetry.Attempt` 是 Host 自己的重试序号（OpenCode 语义，可重置、可重复）。
`ConsecutiveFailureCount` 是万象术领域计数，只在确认失败的 `ProviderRunIdentity` 上由唯一写入口
推进。禁止：把 Host Attempt 写入 count、用 Attempt 判断预算、用 Attempt 推导 Offset、用 Attempt
决定是否发 continuation。`Attempt` 仅可用于诊断日志与唤醒；「Host 是否仍会自动继续」只能由
reconcile 后的完整 snapshot 判断。

## PAR-010：槽内维护子请求

一次自动恢复槽最多两个物理 provider request（按序）：Step 1 维护子请求（BloggerSquash），
Step 2 业务主请求（WorkMain / BloggerMain）。

| 路径 | 结果 |
|---|---|
| 维护失败 | 槽失败，不发主请求，记录唯一 FallbackCursorAdvanced（指向该失败的 ProviderRun） |
| 维护成功 | 不清零 ConsecutiveFailureCount，继续主请求 |
| 主失败 | 槽失败，记录唯一 FallbackCursorAdvanced |
| 主成功 | 清零 ConsecutiveFailureCount |

每个失败槽在终态时**恰好产生一次** `FallbackCursorAdvanced`。维护成功单独不算 Logical Run 业务
完成。决策逻辑在 `Domain/RecoverySlot.fs`（`onSquashOutcome` / `onMainOutcome` /
`advancesCursor`）。

## PAR-011：armed 合取与 parked-cursor 陷阱

恢复槽允许 X prefix probe 或 Y squash 当且仅当：`armedByFailure ∧ primed ∧ hasMaterial`
（FALLBACK-012 + CTX-006）。`armedByFailure` 是**进程内局部执行标志**：仅当本槽由本次自动恢复
中紧邻的真实失败推进而来才为 true；新 Logical Run 第一槽永不 armed；崩溃/重启后自动丢失并恢复
为 false（安全侧 Fail-Closed）。`primed` = Offset 为奇数（A′/B′）。**禁止**仅凭持久化奇数 Offset
判定 armed——成功可以停在奇数 Offset，此时 `armedByFailure=false`。不变量：任意两次 squash 之间
至少隔一次真实物理失败。

parked-cursor 陷阱：`RecoverySlot` 类型故意不提供「这个 Offset 是否 armed」的函数——答案不存在，
arming 是本次序列的控制流事实，不是位置的属性。

## PAR-012：Host abort / cleanup 残留不计入推进

Host 因 abort 清理把在途工具调用标记为失败（`status=error` 且 `metadata.interrupted=true`）
**不是**已确认的 provider attempt 失败：不得推进任何 cursor，也不得消耗自动恢复预算。判据只看
Host 标记（`Session/EnforcerRepair.fs` 的 interrupted 判定），不看错误散文。`status=error` 且
无 interrupted（工具本身失败）才推进。原因：一次 owner attempt 失败会被两个观察者看到（own
provider failure 路径 + abort 打断的 Companion cycle），两边的 `ProviderRunIdentity` 来自不同
Session，FALLBACK-003 去重无法折叠，会把同一次失败记两次。

## PAR-013：换 Peer = 换执行者，不换身份

Fallback 推进只改写下一次 `AttemptExecutionProfile.EffectiveAgent`；对应物理 ModelTarget 由 `execution-model-routing` 在下一条 physical user execution 的 `(SessionId, PhysicalUserMessageId)` admission 中按该 EffectiveAgent 解析。A/B execution 可以落到同一物理 model，不影响 peer/fallback 本体；不得为同一可复用 session 常驻保留 A/B 两个 capacity lease。同一 session /
Life 内下列字节与身份**不得**因 Offset / SideA·B / Peer 切换而改变：

```text
SessionPersona
SessionProviderLanguage
system prompt（office + Role Law + Common Law composition）
CanonicalRole / SelectedAgent / PeerAgent / Authority 身份
```

cursor / SideA·B / Offset / ConsecutiveFailureCount 是墙内机器代数，**禁止**投影进 provider
horizon。T1 / review / reanchor / Strength 同守此边界（ARCH-016 Gate D）。

## PAR-014：continuation 只在失败记账后、预算允许时

Host 仍在自动重试时插件不得额外发 continuation；仅当 Host 已停止自动重试，才允许发送同一 Logical
Run 的 continuation（FALLBACK-004/009）。continuation 本身**不得**触发第二次 cursor 推进，也不得
新建 completion、不得重置 cursor、不得伪称「无限 AABB 已由 Host 完成」。continuation 的 wire
语义（`ProviderRetryAttempt` 等）属 `dispatch-protocol`，本包只保证时序与次数。

## PAR-015：StrengthReplica 不进 owner 的 FallbackController

StrengthReplica attempt 的成功或失败不进入 owner Logical Run 的 FallbackController：不推进
FallbackCursor，也不清零 ConsecutiveFailureCount（STRENGTH-004/019，交叉引用
`speculative-investigation`）。
