# WHAT —— 唯一 normative 合同（interaction-authority）

> 当前世界必须同时成立的事实。每条命题的测试落点见 [`PROOF.md`](PROOF.md)（锚点 `R1`..`R17`）。
> 术语在本文件首次出现处定义；「延续」「Logical Run」等概念引用自 `session-ontology` 与
> `participant-identity`，本包只拥有 authority 判定。

```text
术语：
  LogicalRun       = 一个 logical interaction（一次 Root 建立，多个 Continuation 延长）。
  Root             = PromptOrigin.AuthorityRoot，唯一能「创建」LogicalRun 的 provenance。
  Continuation     = PromptOrigin.Continuation，只能「延长」已存在 LogicalRun 的 provenance。
  Authority 证据   = 唯一能证明一条消息/一个动作属于 Root 或 Continuation 的 typed 来源机制。
```

## INTERACTION-AUTHORITY-001 — PhysicalUserMessage ≠ AuthorityTurn

物理 `role=user` 消息与 authority turn 是两种不同概念。`role=user` 只是 Host 上的运输格式；
`PhysicalUserMessageId` 只能经**唯一**的显式提升函数
`PhysicalUserMessageId.promoteToAuthorityRoot` 变成 `AuthorityRootUserMessageId`（PROMPT-001）。

- 含义：类型系统承载这条不变量——「从物理消息到 authority root」只有一个 crossing，且该 crossing
  只在 `PhysicalAccepted` 已建立后发生（`PromptAuthorityRun.createAuthorityRoot`）。
- 边界：没有从 `TransportReceipt` 到 `AuthorityRootUserMessageId` 的函数（那是 dispatch 的事）。
- 证据：→ PROOF.md R1。

## INTERACTION-AUTHORITY-002 — 形态不是 authority 证据

零宽字符、空白、固定模板、时间戳、文本长度、Synthetic TOML 的 comment/field 形态都**不是**
Authority 身份证据；身份只能由 typed 来源机制证明（PROMPT-001）。

- 含义：正文形态分析永远不能替代 provenance 判定。
- 边界：本包不裁决 TOML 布局/转义（`provider-projection`）。
- 证据：→ PROOF.md R1。

## INTERACTION-AUTHORITY-003 — Root 独占权

`PromptOrigin.AuthorityRoot` 独有权限（PROMPT-002）：

1. 创建新的 Logical Run；
2. 选择或改变 SelectedAgent（并据此确定 PeerAgent / CanonicalRole / SelectedTier / ExecutionBinding）；
3. 成为新的 Fallback root；
4. 重置 Interaction Repair 预算；
5. 成为后续缺省 SelectedAgent 的延续来源。

新 Root 生效时**重置全部 run-scoped 状态**：PendingClaims、AcceptedContinuationIds、
ClaimSequences 清空（`PromptAuthorityRun.registerAuthority`），并（经调用方）重置 Fallback cursor。

- 含义：这些事实只能在 Root 到来时建立或重置；Continuation 永远不得触碰它们。
- 边界：「Root 不得改变 Companion 关联 / SessionPersona」分别归 `session-ontology` /
  `participant-identity`；「Root 不得选择/覆盖 model ID（发送恒 `Model=None`）」归
  `dispatch-protocol`（`AuthorityExecutionProfile` 没有 model 字段，不可表达）。
- 证据：→ PROOF.md R2、R4。

## INTERACTION-AUTHORITY-004 — Continuation 禁区

每个 `ContinuationKind` 都是 Continuation：只延长已有 Logical Run，**不得**执行
INTERACTION-AUTHORITY-003 所列操作（PROMPT-003）。具体地：不新建 RunId、不改
SelectedAgent/PeerAgent/CanonicalRole/SelectedTier、不更新 LastAuthorityProfile、
不重置 Fallback/repair。

- 含义：continuation 继承运行与 root（claim 记录 `LogicalRunId = Some`、
  `AuthorityRootUserMessageId = Some`），只携带当前 Fallback cursor 选的 `EffectiveAgent`。
- 边界：物理请求使用哪个 `EffectiveAgent` 由 cursor 决定（`provider-attempt-recovery` 消费）；
  本包只保证 continuation 不得改写 authority。
- 证据：→ PROOF.md R3。

## INTERACTION-AUTHORITY-005 — 四类 provenance 与两种 Root

```fsharp
type PromptOrigin =
    | AuthorityRoot of RootAuthorityKind   // RootAuthorityKind = HumanRoot | AgentOwnerRoot
    | Continuation of ContinuationKind
    | HostInternal
    | UnknownOrigin
```

（PROMPT-004。完整 `ContinuationKind` 枚举见 `Domain/PromptAuthority.fs`。）每个 continuation 名称
可解析、可标注，且**没有**一个是 Root；`AuthorityRoot` 名称（`HumanRoot`）不可解析为 continuation
（`tryParseContinuationKind` 返回 None）。

- 含义：枚举是闭世界——缺一个 kind，对应 prompt 会落 UnknownOrigin 并在 dispatch 处 fail-closed。
- 边界：枚举成员本身是 HOW（可增删），「任何成员都是 continuation 而非 root」是 WHAT。
- 证据：→ PROOF.md R5。

## INTERACTION-AUTHORITY-006 — HumanRoot 必须显式命名 managed agent

`HumanRoot` 要求显式 `fast-*` / `deep-*` managed agent；省略、legacy 裸名、未知名、malformed
→ fail-closed，不默认、不推断（AGENT-005 / PROMPT-004）。

- 含义：推断 agent 就是让 human prompt 静默获得一个没人选的 agent。拒绝是 typed 的
  （`AgentNameRejection = LegacyAgentName | UnknownManagedAgent | Malformed`），调用方可分支。
- 边界：精确的 legacy 名单与错误文案 = 迁移 ratchet（HOW/弃权，见 HOW.md）；「必须显式 agent 且
  失败关闭」是 WHAT。
- 证据：→ PROOF.md R6。

## INTERACTION-AUTHORITY-007 — UnknownOrigin fail-closed

`UnknownOrigin` 不得更新 profile、不得启用 Fallback、不得发 continuation（PROMPT-004）。

- 含义：无法证明身份的东西被拒绝，而不是被猜测。
- 证据：→ PROOF.md R7。

## INTERACTION-AUTHORITY-008 — 来源解析优先级

消息来源按序判定（PROMPT-009），先匹配者生效：

```text
accepted HostMessageId → claimed PromptKey → Host compaction/synthetic → registered AgentOwnerRoot
→ proven external prompt acceptance (HumanRoot) → UnknownOrigin
```

- 含义：顺序本身是语义：插件自己发出并见到 accepted 的消息必须是 continuation，即使同一 turn Host
  也报告 compaction（compaction 先读会把真实工作标成 HostInternal 而丢出 Logical Run）。
- 边界：`accepted HostMessageId` 与 `claimed PromptKey` 的匹配**机制**（key 组成、claim 表）归
  `dispatch-protocol`；「匹配到什么 provenance 类别」归本包。
- 证据：→ PROOF.md R7。

## INTERACTION-AUTHORITY-009 — 纯函数永不推断 HumanRoot

`resolveKnownOrigin` 是纯函数，无法观测「外部接受且携显式 agent」这一事实，因此**永不**返回
HumanRoot；未证明的消息落 UnknownOrigin 并 fail-closed。已激活的 HumanRoot 不会让后来的未知消息
看起来像 root（PROMPT-004/009）。

- 含义：HumanRoot 只能由 `PromptIngress` 在 ActiveProfile 缺席 + 显式有效 agent 时授予；
  mid-run 的 UnknownOrigin + 有效 agent **不得**抬成 HumanRoot。
- 证据：→ PROOF.md R7、R8。

## INTERACTION-AUTHORITY-010 — 禁自激励；自动 continuation 必须有稳定且有界的 authority budget

合成/repair/review/synthetic/重试 不得抬权（PROMPT-010）：

```text
零宽/repair continuation → HumanRoot
repair response 的新 terminal → 又获得同类 generic repair
Manager idle continuation 的新 terminal → 又获得同 phase encouragement
Review confirmation → 改 Reviewer SelectedAgent
synthetic → 重置 Fallback Offset
B 侧重试 → 下一真人 root 默认 Agent
```

普通 `missing-final-report` / `incomplete-interaction` repair 的 durable budget = **(SessionId, LogicalRunId, repair family)** 一次；第一次 repair 后若同一 LogicalRun 再次需要同 family，得到 `BudgetExhausted`，不得发送第二个 prompt，并以 bounded recovery exhaustion 收束该业务 run。该 claim 由 `ClaimSequences` 派生，跨 restart 存活；abandon 也不重置预算。

Blogger exact-one chronicle 协议是显式例外：它的 durable repair occasion = **(SessionId, LogicalRunId, BloggerRequestId, terminal ProviderRunIdentity, repair kind)**。`BloggerRequestId` 隔离同一长寿命 Blogger run 上的连续 request；terminal id 区分“同 terminal 重入”与“nudge 后新的 invalid terminal”。它自己的状态机严格限制为 `nudge once → AABB once → fatal/exhaust`，不能成为 generic repair 的无界后门。ClaimSequence 只证明该 occasion 曾 claim；feature 恢复“已执行 AABB”还必须结合当前 dispatch lifecycle，Abandoned claim 不得冒充 issued。

Manager idle automatic encouragement 的 durable budget = **Manager Life + business condition（plan commitment 前 / 后）**，不是 trigger ProviderRun；同一 business condition 下后续 terminal 不再赚新 encouragement。JoinGuard/ReviewGuard 也必须由稳定 session/barrier occasion key 去重。

- 边界：repair/fallback 的业务结局 → `provider-attempt-recovery` / 各 feature；本包拥有“自动 prompt 不得自我扩张 authority budget”的 claim identity。
- 证据：→ PROOF.md R9、R12。

## INTERACTION-AUTHORITY-011 — authority 是原子 profile 内的稳定子记录

一次 provider request 的执行身份来自**同一个不可变** `AttemptExecutionProfile`；其中嵌套的
`AuthorityExecutionProfile`（SessionId、LogicalRunId、AuthorityRootUserMessageId、AuthorityKind、
SelectedAgent、PeerAgent、CanonicalRole、SelectedTier）在 Logical Run 内稳定（PROMPT-008 的本包半边）。

- 含义：root/continuation authority 是 atomically 携带的，禁止从 session cache / 最后一条 user
  message / Role map / fallback projection 临时拼装。
- 边界：`ToolCapabilitySet` 同源 → `capability-enforcement`；`ProjectionChoice` → `prefix-stability`；
  ProviderRunIdentity bind-once → `host-boundary`；record 字段集 → HOW。
- 证据：→ PROOF.md R2、R4。

## INTERACTION-AUTHORITY-012 — assistance 是 continuation，不是 fallback 失败

`NeedHelpEscalation` 与 `NeedHelpAdvice` 是 typed `ContinuationKind`（PROMPT-018 / AGENT-031 /
HOST-027 本包半边）。二者延长当前 LogicalRun，复用现有 AuthorityRoot、CanonicalRole、Persona、
system prompt 与 ToolCapabilitySet；不得建立 HumanRoot、不得 reset FallbackCursor、
不得记作 `ProviderRetryAttempt`。

- 含义：`[NEEDHELP]` abort 的 owner 是 assistance：不得进入 ProviderFailure/LoopKill、不得推进
  FallbackCursor、不得增加 consecutive failure 或 retry budget（`increase-strength.md` §6/§14）。
  AbortWake 只 claim 当前 ProviderRun，**不得**在 abort stack 内发送 Fast→Deep continuation 或创建
  consultation child；Fast escalation 与 Deep consultation 共用同一物理 admission：必须等该 session
  的 fresh `SessionIdle` revisit 后才消费 NEEDHELP arm 并执行下一动作。这样 continuation 不会在
  OpenCode 的 abort descendant sweep 尚未结束时“消息已落地但 provider 未启动”，再被 fallback 误判失败。
- 边界：sentinel 检测/arm/abort 机制 → `host-boundary`；deep 命中后的 consultation child →
  `delegation`；Pair Hint wire → `provider-projection` + `prefix-stability`；craft 正文 →
  `cognitive-environment`。
- 证据：→ PROOF.md R10、R11。

## INTERACTION-AUTHORITY-013 — fast→deep escalation 是 authority continuity

同一 Session / LogicalRun / AuthorityRoot / Persona 上的 fast→deep escalation continuation：
只有 `EffectiveAgent` 改变，其余全部不变（AGENT-031 本包半边）。

- 含义：escalation 不改 authority、不改 profile、不改 cursor 位置。
- 证据：→ PROOF.md R11。

## INTERACTION-AUTHORITY-014 — Nudge / JoinGuard 是 Continuation

Nudge 与 JoinGuard 都是 Continuation，不创建新 Authority（EXEC-007 / EXEC-016 本包半边）。有 join 义务且仍有 outstanding 后台时，本 turn 只发 `JoinGuard` Continuation；finality 处理停放。Manager idle encouragement 也是 continuation，但按 INTERACTION-AUTHORITY-010 每 Life + plan-commitment condition 至多自动发送一次。

- 含义：idle/join 场景的续推永远走 continuation 通道，不许静默开新 root，也不许由 continuation 自己的新 terminal 生成无限续推预算。
- 边界：outstanding-background 的判定（listable handles / active jobs / live PTY）归
  `delegation` / `managed-session-lifecycle`；「只发 JoinGuard continuation」归本包。
- 证据：→ PROOF.md R12。

## INTERACTION-AUTHORITY-015 — external-user ingress 不授予 authority

External-user ingress 只打断**当前** join wait：不取消 mailbox/runtime/session/child，
也不本身授予 Prompt authority（EXEC-017 本包半边 / `corrective.md` §3）。

- 含义：`UserMessageArrived` 是低权限 wake；mid-run 用户消息不得 AcceptHumanRoot、不 reset
  LogicalRun、不新建 Manager Life。无 active attempt 的消息作为 join wake 丢弃，绝不 latched 给
  future join。
- 边界：join 的等待/中断机制（JoinInterruptReason、registry fan-out）归 `delegation`；
  「ingress 不给 authority」归本包。
- 证据：→ PROOF.md R7、R8、R13。

## INTERACTION-AUTHORITY-016 — Root claim 不进入 continuation 映射

AgentOwnerRoot claim 的接受（`acceptClaim`）**不**把消息记入 `AcceptedContinuationIds`
（PROMPT-009 边界：root 不是 continuation；REVIEW-003 禁止从共享 root 猜 review 确认）。

- 含义：接受后的 root 物理消息在 `resolveKnownOrigin` 中仍是 UnknownOrigin——
  「这条消息曾经是 root」不是「这条消息是 continuation」的证据。
- 证据：→ PROOF.md R4。

## INTERACTION-AUTHORITY-017 — continuation 只能接续 active run

Continuation 的归属只看 `ActiveLogicalRun`，绝不回退到 `LastAuthorityProfile`——已结束的 run
不得被 continuation 续上（PROMPT-004/003）。

- 含义：stale profile 正是「必须不能冒充 active run」的东西。
- 证据：→ PROOF.md R3、R7。

## INTERACTION-AUTHORITY-018 — HumanRoot Manager 的 LifeCompleted 原子释放 active run

HumanRoot Manager 的 `LifeCompleted` 是该 HumanRoot Logical Run 的 durable terminal evidence；
其 canonical fold **同时**把匹配的 `ActiveLogicalRun` 置空，并清空 run-scoped
`PendingClaims / AcceptedContinuationIds / ClaimSequences`，但保留 `LastAuthorityProfile` 作为历史。
禁止再追加一条平行的 `LogicalRunClosed` durable fact——同一终止事实不得有两个 writer / 两次 append。

AgentOwnerRoot 不受此派生关闭影响：它在 Manager Life 完成后仍可能承担 owner-directed 的
publish conflict resumption 等工作，其 authority lifetime 由 owner/session lifecycle 管理。
因此 FINALITY-022 的 Reawakening 只发生在 HumanRoot 边界：旧 HumanRoot Life 完成后，下一条
真实 external + explicit-agent message 才可建立新的 HumanRoot；continuation / review / synthetic
message 因无 active run 只能 fail closed，绝不能复活旧 run。

- 含义：`LifeCompleted` 与 HumanRoot authority closure 是一个 durable truth 的两个 projection 视图，
  不是两个 durable 事实；避免 LifeCompleted 已落盘但 close append 失败的两阶段裂缝。
- 边界：`LifeCompleted` 的业务资格与 AgentOwner migration Life 归 `finality`；普通 session / child 的
  物理 retirement 归 `managed-session-lifecycle`。
- 证据：→ PROOF.md R18。

## 反向覆盖核对（COVERAGE.md 归属）

本包 WHAT 覆盖 COVERAGE.md 中单 owner 行：PROMPT-001/002（本包部分）/003/004/009/010/018、
PROMPT-008（authority 子记录分片）、AGENT-005、AGENT-031（authority continuity 分片）、
HOST-027（assistance-abort 分片）、EXEC-007/016/017（continuation/authority 分片）。
`Model=None`、PromptKey 匹配机制、claim 生命周期 → `dispatch-protocol`（不在此复制）。
