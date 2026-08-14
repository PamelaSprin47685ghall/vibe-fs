# effect-accounting

> 向外部世界请求一个 effect、该 effect 可能已经发生、以及系统已经确认其发生，是三个
> 不同事实；把它们压成一个 bool 会在中断窗口造成**重复 effect** 或**虚假成功**。

## 这是什么包

`effect-accounting` 拥有「外部副作用的 durable 记账」语义。核心三句：

```text
Requested / Claimed  ≠  Accepted / Created / Published   —— 分型，不是 bool
Requested-only        = outcome unknown                  —— 不等于未发生，也不等于成功
Accepted              不折回 Requested                   —— 重复 acceptance 幂等
reconciliation        先查物理 effect identity           —— 证明不存在且合同允许才重试
```

效果与事实的典型对应（PERSIST-009）：

| 效果 | Request | Accepted | 崩溃后核对（reconcile） |
|---|---|---|---|
| Worktree | `WorktreeCreateRequested` | `WorktreeCreated` | `git worktree list` / Sweep |
| Publish | `PublishClaimed` | `Published` | ref/head（ORCH-007 三分支） |
| Blogger | `BloggerRequestMaterialized` | Entry/SquashCommitted | ProviderRun receipt |
| Prompt | （PROMPT-011） | PhysicalAccepted | PROMPT-011 at-most-one（policy 归 `dispatch-protocol`） |
| Todo（Magic） | `TodoWritePrepared` | `TodoWriteAccepted` | 物理成功证据 + digest 核对 |

```text
README.md   ← 你在这里
WHY.md      为什么必须分型（中断窗口的重复/虚假成功）
WHAT.md     唯一 normative 合同：12 条命题（EFFECT-ACCOUNTING-001..012）
HOW.md      实现模型：typed facts、CommitUnknown、PublishClaimed 三分支；历史与弃权
PROOF.md    每条命题的测试落点
tests/      本包拥有的可执行 proof（1 个 NEW 文件，4 断言）
```

## WHAT 概览（按命题组）

- **分型**（001–002）：Requested/Accepted/Created/Published 是不同 typed 事实；
  Requested-only = outcome unknown。
- **顺序与幂等**（003–004）：durable intent 先于权威内存状态；Accepted 不折回、
  重复 acceptance 幂等。
- **reconcile**（005–006）：先查物理 effect identity 再决定重试；outcome-unknown 显式
  分型（CommitUnknown/Pending），不假装 committed。
- **false finality**（007）：aborted ≠ terminal；LegacyFalseAbort 永不成为 RunCompletion。
- **实例**（008–012）：typed 效果家族、PublishClaimed 三分支、0.5.1 通用 union 拒绝、
  TodoWriteAccepted 精确指名 Prepared、各效果的 reconciliation 律。

## HOW 概览

```text
Kernel/Fact.fs           OrchestratorFactCases：WorktreeCreateRequested / WorktreeCreated /
                         PublishClaimed / Published（typed 事实）
Journal/OrchestratorFactFold.fs   fold 拒绝「Accepted → Requested」回归
Journal/OrchestratorProjection.fs recoveryAction：PublishClaimed 三分支（固定顺序）
Journal/EventStoreJournalWriter.fs 写失败 → CommitUnknown（结局未知，poison）
Journal/AgentJournal.fs    JournalAppendFailure.WriteUnknown | FactRejected
Application/Reconciliation/   PromptRecovery（先 snapshot 核对再决定）、MagicTodoMembrane
                              （Prepared 先于 provider 调用，Accepted 需物理成功证据）
```

核心文件（精确到符号）：

| 概念 | 文件 |
|---|---|
| typed effect 事实 | `src/Wanxiangshu/Kernel/Fact.fs`（`OrchestratorFactCases.WorktreeCreateRequested/WorktreeCreated/PublishClaimed/Published`）、`Domain/MagicTodoFacts.fs`（`TodoWritePrepared/TodoWriteAccepted`） |
| effect 状态投影 | `Journal/OrchestratorProjection.fs`（`WorktreeEffectStatus = Requested\|Created`、`JobProgress.PublishClaimed`、`recoveryAction`） |
| 拒绝回归 | `Journal/OrchestratorFactFold.fs`（PublishClaimed 需 RebasedCandidateReady；`acceptWorktree` 后 request 不回归） |
| outcome-unknown 机械面 | `Journal/EventStoreJournalWriter.fs`（`CommitUnknown`）、`Journal/AgentJournal.fs`（`JournalAppendFailure`） |
| 先证后重试 | `Application/Reconciliation/PromptRecovery.fs`（`reconcileClaim`：snapshot 核对先于 budget）、`Application/Reconciliation/MagicTodoMembrane.fs`（`prepare` 先 append 再 provider 调用；`accept` 需物理成功 + digest） |

## proof 概览

```bash
node --test requirements/effect-accounting/tests/effect-facts.test.mjs
# 主要 REUSE 落点：
node --test requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs
node --test requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs
node --test tests/unit/temporal/orchestrator-conflict-confluence.test.mjs
node --test requirements/change-integration/tests/job.test.mjs
node --test requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs
```

## DEPENDS ON `durable-events`

效果事实的 append/CAS/commit witness 由 `durable-events` 提供；本包定义
「effect 的 Requested/Accepted 语义」这一层。

## 边界（DOES NOT OWN）

- EventStore 编码/提交机制 → `durable-events`。
- Prompt 特有 PromptKey / no-resend policy → `dispatch-protocol`。
- Git publish/worktree、repository transaction 的具体 reconcile 算法 → `change-integration`
  （本包拥有其中的 PublishClaimed 三分支与 Worktree Requested/Created 律）。
- effect 的业务授权（谁有权发起）→ `office-capability` / `interaction-authority`。
- 进程中断后重入普通程序 → `crash-reconciliation`。
