# WHAT —— effect-accounting（唯一 normative 合同）

条款前缀 `EFFECT-ACCOUNTING-`。每条的落点测试见 `PROOF.md`。
来源：历史五层 persist 条款（PERSIST-009）、
历史 why/what execution（EXEC-020/021/022）、
历史 COVERAGE persist 小节、历史 PROOF-MAP
（p0-recovery-join SPLIT）。

## EFFECT-ACCOUNTING-001 —— Requested/Claimed 与 Accepted/Created/Published 分型

**规范陈述**：每个外部效果有两类 typed durable fact：Request/Claim（意图，durable
intent）与 Accepted/Created/Published（已确认物理发生）。它们是不同的 union case，
不是同一个字段的两个取值；禁止用单个 bool/status 表达 effect 结局。通用
`DurableEffectRequested/Accepted` union（0.5.1）已被 typed facts 取代，decode 必须拒绝。

**含义/动机**：「请求过」「可能已发生」「已确认发生」是三个事实；bool 会在中断窗口
造成重复 effect 或虚假成功。
**边界**：typed 事实的存储/append 机制 → `durable-events`；Prompt 特有 policy →
`dispatch-protocol`。
**证据**：→ PROOF.md 001。

## EFFECT-ACCOUNTING-002 —— Requested-only = outcome unknown

**规范陈述**：只有 Request/Claim、没有 Accepted → **结局未知**：不等于效果不存在（不得当未发生而盲重试），也不等于成功（不得跳过 reconcile 宣称完成）。Requested 状态必须被投影如实表达，不得被静默抹除或提升。

同理，agent 的 `MISSING_FINAL_REPORT` / empty completed terminal 只是“本次 terminal 不足以证明完成/失败”，在有 repair 资格时保持 pending；但一旦 interaction-authority 已证明同 LogicalRun 的 ordinary repair budget **耗尽**，`INTERACTION_REPAIR_EXHAUSTED` 是新的、明确的 terminal failure，必须 settle pending child run。不能继续把它当 `MISSING_FINAL_REPORT` observation-only，否则“有界 repair”会退化成永远 Active 的 hang。

**含义/动机**：未知是第三种状态，不是「未发生」或「成功」的别名。
**边界**：未知后的重试政策见 005；「未知 ≠ failure」的另一实例（agent completion）见 007。
**证据**：→ PROOF.md 002。

## EFFECT-ACCOUNTING-003 —— durable intent 先于权威内存状态更新

**规范陈述**：外部效果的 durable intent/accounting（Request/Claim fact）必须**先于**
权威内存状态更新，也先于物理 effect 的执行/确认：
`WorktreeCreateRequested` 先于 `git worktree add`、`TodoWritePrepared` 先于 provider
调用、`PublishClaimed` 写在 CAS 窗口内。禁止「先改内存再补盘」、禁止「先执行后记账」。

**含义/动机**：崩溃后重放只能看到「已经请求过」，才能按效果身份 reconcile；先执行后
记账会让 effect 发生而系统不知道。
**边界**：session.create 例外（不引入 `SessionCreateRequested`，accepted 证据 = 链接事实
`HandleLinked`）见 HOW。
**证据**：→ PROOF.md 003。

## EFFECT-ACCOUNTING-004 —— Accepted 不折回 Requested；重复 acceptance 幂等

**规范陈述**：Accepted/Created/Published 一旦存在，不得折回 Requested/Claimed
（重放 retry 重写 Requested 必须被 fold 拒绝/忽略）。重复的 acceptance（重放、幂等
retry）必须幂等：不改变已确认状态、不产生副作用。

**含义/动机**：CommitUnknown retry 可能重放 Requested——已确认的 effect 绝不能被撤销；
重复确认无害。
**边界**：错误事实用新事实纠正（append-only 推论），不是 rewrite。
**证据**：→ PROOF.md 004。

## EFFECT-ACCOUNTING-005 —— reconciliation 先查物理 effect identity；禁盲重试

**规范陈述**：崩溃/中断后处理 Requested-only 效果：**先核对物理 effect identity**
（worktree 存在？ref/head 前进？provider receipt？），只有证明效果**不存在**且该效果
的领域合同**允许幂等重试**时才重试；否则保持 pending/挂起。禁止未核对物理证据的盲重试
与自动重发。

**含义/动机**：未知 ≠ 未发生；未证实的重试是重复 effect 的配方。
**边界**：各效果的 reconcile 具体算法（git worktree list / ref 核对 / PromptRecovery /
OrchestratorSweep）归各自的 domain/change-integration；本命题钉「先证后重试」律。
**证据**：→ PROOF.md 005。

## EFFECT-ACCOUNTING-006 —— outcome-unknown 显式分型，不假装 committed

**规范陈述**：append/effect 结局未知时（写失败、CAS 未见证、Requested 无 Accepted），
必须以显式分型表达（`CommitUnknown`、`WriteUnknown`、Pending、Requested-only），
不得假装成功、不得假装未发生、不得把「函数没返回」当成「没发生」。

**含义/动机**：结局未知是可 reconcile 的第三态；判定手段（canonical root witness）由
`durable-events` 提供，本命题钉「怎么表达与消费它」。
**边界**：writer poisoned 后的恢复路径 → `crash-reconciliation`。
**证据**：→ PROOF.md 006。

## EFFECT-ACCOUNTING-007 —— aborted ≠ terminal（false finality）

**规范陈述**：取消（aborted）是**控制面**，不是业务结果：ABORTED 不是 agent 终态。
agent 终态只有 `Completed | Failed | Abandoned`。`LegacyFalseAbort` 永不成为
`RunCompletion`；completion blob finality 仅 `completed|failed`；假 completion 经
`HandleFalseCompletionRejected` 确定性补偿，禁止把历史假 abort 洗成成功。

**含义/动机**：把 abort 洗成终态会让恢复/fallback 走错分支（P0-RECOVERY-JOIN-001
false finality）。
**边界**：崩溃后从 snapshot 恢复的流程 → `crash-reconciliation`（p0-recovery-join
gate 的 recovery 侧规则归它）；本命题拥有 aborted≠terminal 半边。
**证据**：→ PROOF.md 007。

## EFFECT-ACCOUNTING-008 —— typed 效果家族实例（Worktree/Publish/Blogger）

**规范陈述**：至少以下效果以 typed Request/Accepted 事实成对出现：Worktree
（`WorktreeCreateRequested`/`WorktreeCreated`）、Publish（`PublishClaimed`/`Published`）、
Blogger（`BloggerRequestMaterialized` → Entry/SquashCommitted）、Todo（`TodoWritePrepared`
→ `TodoWriteAccepted`）。每个效果有自己的 reconcile 手段（见 README 表）。

**含义/动机**：分型不是理论：每个实例都必须是 typed 对，且各自可核对。
**边界**：各实例的业务编排归各 domain owner（`change-integration`/`obligation-ledger`/
blogger 域）；本命题钉「typed 对」这一共性。
**证据**：→ PROOF.md 008。

## EFFECT-ACCOUNTING-009 —— PublishClaimed 三分支 fixed order

**规范陈述**：Publish 效果 reconcile 按固定顺序检查物理 identity（ORCH-007）：
(1) currentHead = claim.RebasedCommit → 发布已经发生、只缺事实 → BackfillPublished；
(2) currentHead = claim.ExpectedHead → 目标未变 → AttemptPublish；
(3) 其它 → 目标已动，post-rebase witness 作废 → RebaseAndReviewAgain。
无法观察物理 head → FailClosed，绝不猜测。顺序不可调换（先查「已发布」再查「目标未变」，
否则会重试一个已成功的 ff）。

**含义/动机**：「先查物理 effect identity」的精确实例：ref 是否已前移到 rebased commit
是发布效果的物理身份。
**边界**：rebase/ff 的编排算法 → `change-integration`。
**证据**：→ PROOF.md 009。

## EFFECT-ACCOUNTING-010 —— 0.5.1 通用 DurableEffect union 拒绝

**规范陈述**：历史格式的通用 `DurableEffectRequested` / `DurableEffectAccepted` marker
必须被拒绝（decode error + 迁移信息），不得静默双读、不得作为 ongoing vocabulary。

**含义/动机**：typed facts 取代通用 union；历史 marker 是 pre-0.5.1 沉积，进入
one-shot 迁移信息路径。
**边界**：canonical/identity 机制 → `durable-events`。
**证据**：→ PROOF.md 010。

## EFFECT-ACCOUNTING-011 —— TodoWriteAccepted 必须精确指名 Prepared

**规范陈述**：`TodoWriteAccepted` 必须携带精确的 `PreparedFactRef`（指名它接受的
`TodoWritePrepared` envelope）+ digest 核对：Prepared 缺失、PreparedFactRef/ToolCallId/
InputDigest/SemanticVersion 失配 → IdentityCorruption，拒绝。Accepted 后 Current
立即切换；REVISE 结论不得回滚已 Accepted 的 checkpoint。

**含义/动机**：Accepted 必须能追溯到它确认的那一次 Request——效果身份是精确的，
不是「最近一个」。
**边界**：Todo checkpoint 的义务/评审语义 → `obligation-ledger`。
**证据**：→ PROOF.md 011。

## EFFECT-ACCOUNTING-012 —— 先证后重试的实例律

**规范陈述**：每个效果的 retry 门都必须先核对证据：Magic Todo 的 lag-1 语义（T1
Accepted 前 T2 prepare 是等待不是重试；review Concluded 后 T2 才 prepare）；Prompt
recovery 先 snapshot 核对再决定（at-most-one）；publish 无 RebasedCandidateReady 时
claim 被 fold 拒绝（claim 必须基于已 committed 的 witness）。

**含义/动机**：把 005 的律落到具体效果：等待/重试由证据门决定，不由计时器或猜测决定。
**边界**：Prompt 的 PromptKey/no-resend 细节 → `dispatch-protocol`；Todo 评审 →
`obligation-ledger`。
**证据**：→ PROOF.md 012。
