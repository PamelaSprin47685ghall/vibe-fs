# HOW — review-assurance

> 本文非 normative。当前合同只在 `WHAT.md`。

## 实现模型

### 1. Completed witness 只保存完成事实

- `src/Wanxiangshu/Mission/Review/Judgement/Witness.fs`
  - `VerdictWitness = { ProviderRun; ToolCallId; GitTreeHash; ReviewerSessionId }`。
  - `ReviewWitness = NoReview | RevisionWitness | Confirmed`。不存在“第一次 PERFECT 已发生、等待第二次”的 durable case。
  - `Confirmed` 自包含两次 judgement identity，以及 `FirstPhysicalUserMessageId` / `SecondPhysicalUserMessageId`。
  - `confirm` 只比较强类型 identity：同 reviewer/tree/barrier，run 与 tool call 均不同，且 first / second judgement 的 physical user message 相同。
  - `isValidForTree` / `confirmedReviewer` 都是完成证据上的派生查询，不存布尔。
- `src/Wanxiangshu/Mission/Review/Judgement/Challenge.fs`
  - 只拥有 provider-visible challenge 的 semantic resource path 与 ARCH-010 prompt assembly。
  - challenge 文本不参与任何业务判断；不 hash、不解析、不扫描。

### 2. 物理因果能力

- `src/Wanxiangshu/OpenCode/Host/ProviderRunBinding.fs`
  - HOST-010 的 assistant-run 绑定是通用 Host identity 能力；它不再属于 Finality witness 协议。
- `ToolRuntimeScope.CurrentPhysicalUserMessage`
  - JudgeTool 读取当前 provider execution 已绑定的 exact physical user message；缺失即 fail closed。
  - Finality 的 first / second judgement 必须共享这个 physical review prompt；challenge 本身不是额外 user message。
- OpenCode tool-call completion
  - JudgeTool 把当前 tool call 的 `Accept()/Challenge()/Reject()` completion capabilities 随 typed judgement 一起交给 CE；不存在业务 `Reply` DU。
  - 第二 judgement waiter 在 CE 调用 first delivery 的 `Challenge()` 前建立，因此 challenge delivery 的因果由调用顺序证明，不需要 PromptKey callback / transcript scan。

### 3. Finality dual-PERFECT 的唯一 temporal owner = F# CE

- `src/Wanxiangshu/Mission/Review/Barrier/Reverify.fs`
  - `ReviewBarrierWorkflow.reverify` 是一次 barrier 的 direct CE。
  - 顺序由源码调用栈表达：注册 final terminal + first judgement wait → `StartReview` → **race(first judgement, final terminal)** → durable record → 若 PERFECT，**先注册 second judgement wait** → `firstDelivery.Challenge()` 完成首个 tool call → **race(second judgement, same final terminal)** → durable record → typed causal validation → completed witness → `secondDelivery.Accept()` → final terminal。任一所需 judgement 之前 terminal/timeout 都直接 fail closed，不能把已完成的 terminal task 丢在旁边继续等 inbox。
  - 唯一业务分叉是 `PERFECT | REVISE`；没有 stage/phase/program counter，也不读 Journal projection 判断“当前做到哪一步”。
- `src/Wanxiangshu/Mission/Review/OpenCode/JudgementInbox.fs`
  - Finality CE 与 JudgeTool 的 one-shot physical rendezvous。
  - `AwaitJudgement()` 的调用次数与顺序由 CE 决定；inbox 本身不知道 first/second/confirmed。
  - JudgeTool 等待 delivery capability 完成当前 tool result；`Challenge()` 只由第一次 PERFECT 分支调用，`Accept()` 用于 REVISE / 第二次判断；durable verdict 先于后续 provider progress。
- `src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs`
  - 只解析 typed `PERFECT | REVISE` 参数与 Host 提供的 Session/ProviderRun/ToolCall/PhysicalUserMessage identity。
  - Finality session 被 active CE ownership 时，将 `ReviewJudgement` 交给 inbox；不读 barrier projection、不扫描 transcript、不推断 challenge delivery。
  - 非 Finality 的 Dedicated Todo process review 走其独立一次-judgement路径，不进入 dual-PERFECT。
- `src/Wanxiangshu/Mission/Finality/Cohort.fs`
  - enlist 只准备 session、写 durable enlist、open barrier；provider 启动发生在 `ReviewBarrierWorkflow` 已经建立 waiter 之后，消除“judge 先发生、CE 后订阅”竞态。
- `src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs` 与 `src/Wanxiangshu/Change/Host/ReviewRunner.fs`
  - Finality 与 Orchestrator 复审都复用同一个 `ReviewBarrierWorkflow`。
  - Host adapter 只负责启动 reviewer 与等待最终 physical terminal；challenge 不再有独立 continuation port。

### 4. Journal 只记录已发生事实，不决定下一步

- `ReviewVerdictRecorded`：一次 typed judge 已被 CE 接受并 durable。
- `ConfirmedReviewWitness`：整个 first → `Challenge()` completion → second 已完成后一次 append。
- `ReviewAttemptClosed`：process review 的 reconciled turn 完成后冻结 XTrace frontier。
- `ReviewBarrierStarted`：barrier identity 已 durable 打开。
- `ReviewProjection` 只回答完成世界事实：current barrier/tree、completed witness、observed attempts、closed attempts、terminal frontier。
- replay/fold 不重演 dual-PERFECT 的中间程序位置；崩溃后从 durable stable facts 重入普通 workflow，不能恢复半截协程。

### 5. Process review 与 Finality review 分离

Dedicated Todo process review 仍是一次 judge 的业务：

- `VerdictKnown(k)` 来自 durable `ReviewVerdictRecorded` 的 typed ProviderRun/ToolCall identity；它不进入 Finality dual-PERFECT。
- reviewer terminal 后 `ReviewAttemptClosed` 冻结 request-range frontier。
- `TodoProcessReviewProgram.tryConclude` 在同一 snapshot 物化 canonical ProcessReviewLWR；record-ready 后 append `TodoReviewConcluded`。
- `awaitConsumableReview` 使用 `AgentJournal.awaitChangeFrom` 因果等待；无 timer/polling/program counter。

### 6. 当前数据流

```text
Finality:
open barrier
  → CE registers final-terminal + first-judgement waits
  → StartReview
  → judge(PERFECT) on PhysicalUserMessageId U / ProviderRun R1
  → CE records first judgement
  → CE registers second-judgement wait
  → CE invokes first delivery `Challenge()`
  → JudgeTool completes the first call with skeptical tool result
  → Host continues provider loop on the same PhysicalUserMessageId U
  → judge(PERFECT) on fresh ProviderRun R2 / fresh ToolCall
  → CE records second judgement
  → validate same U + fresh run/call
  → append ConfirmedReviewWitness
  → CE invokes second delivery `Accept()`
  → final provider terminal

Any REVISE at first or second judgement → `Accept()` current delivery → await final terminal → RevisionRequired.

TodoProcessReview:
one typed judge → ReviewVerdictRecorded
  → reviewer turn closes → ReviewAttemptClosed
  → same-snapshot record-ready → TodoReviewConcluded
  → T(k+1)/suicide may consume
```

## 依赖

| 依赖 | 理由 |
|---|---|
| `review-judgement` | 定义 PERFECT/REVISE 的 judgement 语义。 |
| `dispatch-protocol` | PromptKey claim → PhysicalAccepted 的唯一物理发送证据。 |
| `host-boundary` | ProviderRun 与 exact PhysicalUserMessage 的 Host identity 绑定。 |
| `durable-events` | 完成事实 append-only / replay；不承担程序位置恢复。 |
| `causal-wait` | process record-ready 与 terminal 等待的事件驱动边界。 |

## 被拒方案

- 单 PERFECT 即确认：因果强度不足。
- 解析 challenge 文本、tool-result 文本、provider wire 来判断“是否看过”：文本是展示，不是状态。
- 对 provider input 做 digest，再从历史摘要推导 confirmation：把 transport 表示伪装成业务证据。
- durable “第一次 PERFECT / 等待 challenge / 等待第二次”状态：把 CE 程序计数器写进领域状态。
- terminal observer 读 projection 再判断该 nudge/challenge/complete：形成第二运行时；Finality temporal ownership只在 CE。
- same-root 或 SessionId 猜第二次 judgement 的因果：identity 不足。
- process PERFECT 计入 terminal dual-PERFECT：两个 bounded context 的业务意义不同。
- timer/polling 等 record-ready：因果等待退化成时间运气。

## 边界

- judgement 内容哲学 → `review-judgement`。
- Finality cohort/rejection/blessing/rest → `finality`。
- Host ProviderRun/physical binding 与 tool-call delivery → `host-boundary`。
- LWR 表示 → `work-record`。
- durable substrate → `durable-events`。
- causal waiting → `causal-wait`。

`DEPENDS ON: review-judgement, host-boundary, durable-events, causal-wait`。

## 验证与测试落点

> 运行：`node --test requirements/review-assurance/tests/*.test.mjs`；权威全量：`node requirements/verification-system/tests/run.mjs`。

### 落点表

| 命题 | 可执行 proof |
|---|---|
| REVIEW-ASSURANCE-001 | `tests/witness.test.mjs` → `REVIEW_003_two_attempts_require_distinct_run_and_call`、`REVIEW_003_confirmation_still_requires_distinct_attempts`；`tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_003_reverify_is_the_direct_ce_temporal_owner` |
| REVIEW-ASSURANCE-002 | `tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_001_finality_dual_perfect_has_no_persisted_program_position`；`tests/host-reverify.test.mjs` → `HOST_reverify_terminal_before_first_judgement_fails_closed_without_hanging`、`HOST_reverify_terminal_before_second_judgement_fails_closed_without_hanging`、typed challenge dual-PERFECT Host case；`tests/witness.test.mjs` → `REVIEW_005_single_PERFECT_is_not_a_durable_pending_witness`、`REVIEW_003_confirmation_requires_exact_challenge_physical_identity`、`REVIEW_003_challenge_text_is_presentation_only_and_localized`；`tests/shared-state.test.mjs` → `SHARED_judgement_rendezvous_is_physical_not_a_business_stage` |
| REVIEW-ASSURANCE-003 | `tests/witness.test.mjs` → `REVIEW_004_attempt_identity_names_all_five_components`、`REVIEW_004_duplicate_attempt_is_refused` |
| REVIEW-ASSURANCE-004 | `tests/witness.test.mjs` → `REVIEW_005_confirmedReviewer_is_derived_from_witness` |
| REVIEW-ASSURANCE-005 | `tests/witness.test.mjs` → `REVIEW_006_confirmed_witness_is_self_contained_typed_evidence` |
| REVIEW-ASSURANCE-006 | `tests/witness.test.mjs` → `REVIEW_008_tree_change_invalidates_completed_witness`、`REVIEW_008_new_barrier_requires_a_fresh_completed_CE`、`REVIEW_008_late_old_confirmation_cannot_satisfy_current_barrier`；`tests/review-guard.test.mjs` → `RVGD_openBarrier_is_the_shared_review_barrier_writer`；`tests/host-reverify.test.mjs` → `HOST_reverify_rejects_completed_terminal_with_unknown_role` |
| REVIEW-ASSURANCE-007 | `tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_002_finality_confirmation_never_parses_provider_text_or_seals_it`；`tests/shared-state.test.mjs` → `SHARED_finality_has_no_pending_provider_input_seal_registry`；`tests/seal-bind.test.mjs` → HOST-010 generic ProviderRunBinding fail-closed cases |
| REVIEW-ASSURANCE-008 | `tests/consumable-review.test.mjs` → `REVIEW_014_a_durable_verdict_alone_never_makes_the_review_consumable`、`REVIEW_014_only_todo_review_concluded_marks_the_review_consumable`、`REVIEW_017_process_verdict_identity_comes_from_the_integrated_projection_not_a_judge_tool_call_trace`; `tests/review-guard.test.mjs` → `review_journal_rejects_forged_verdict_role_ownership_and_completion_labels` |
| REVIEW-ASSURANCE-009 | `tests/consumable-review.test.mjs` → `REVIEW_018_producer_presence_is_present_when_reviewer_handle_is_CompletedAwaitingJoin`、`REVIEW_017 durable verdict keeps record-ready producer present after the reviewer work-unit is Retired`；源码结构 = sample revision → tryConclude → presence → awaitChangeFrom sampled revision |
| REVIEW-ASSURANCE-010 | `tests/witness.test.mjs` → `REVIEW_002_REVISE_is_a_completed_revision_fact`；`tests/consumable-review.test.mjs` → concluded/assignment/producer fail-closed cases；`tests/review-guard.test.mjs` → process missing-judge repair fail-closed cases；`tests/host-reverify.test.mjs` → `HOST_reverify_rejects_completed_terminal_with_unknown_role`；`tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_004_transient_reviewer_failures_remain_in_provider_recovery` |
| REVIEW-ASSURANCE-011 | `tests/consumable-review.test.mjs` → `REVIEW_020_a_process_revise_is_a_revision_witness_not_a_finality_rejection`；`tests/witness.test.mjs` → `REVIEW_005_single_PERFECT_is_not_a_durable_pending_witness` |
| REVIEW-ASSURANCE-012 | `tests/consumable-review.test.mjs` → `REVIEW_016_the_concluded_review_evidence_is_bounded_to_the_frozen_request_frontier`；交叉 `requirements/obligation-ledger/tests/magic-todo-after.test.mjs` 首个 T1 start 不受 post-T1 global floor 反向影响，`magic-todo-projection.test.mjs` concluded coverage 采用 assigned exact Manager frontier 而非 Prepared provisional frontier |
| REVIEW-ASSURANCE-013 | `tests/review-requirement.test.mjs` → `requirement identity is the Authority Root and duplicate roots collapse`、`confirmation clears its covered batch but replay cannot clear a later requirement` |

### 行为级 canary

`requirements/verification-system/tests/e2e/scenarios/long-stroke.toml` 的 Finality reviewer 段包含两个独立 provider requests：第一次 `judge(PERFECT)`，随后 skeptical challenge continuation，再次 `judge(PERFECT)`。权威 e2e 必须证明第二次直接形成 completed witness；不存在第三次“再尝试让状态机认账”的路径。

### 可红性

- 把 first PERFECT 恢复成 durable pending state → `REVIEW_CE_001` 红。
- Finality control path重新引用 provider wire/text/digest → `REVIEW_CE_002` 红。
- `Reverify.fs` 重新读 Journal projection 决定下一步 → `REVIEW_CE_003` 红。
- challenge physical id 与 second judgement physical id 不同仍确认 → witness physical-causality case 红。
- 同 run / 同 call 仍确认 → distinct-attempt cases 红。
- 新 barrier/tree 复用旧 witness → REVIEW-008 cases 红。
- process verdict 直接变 ConsumableReview 或伪 Finality confirmation → consumable-review cases 红。
- Finality Reviewer 的非 `TurnCompleted` 再次被 Reviewer CE 直接消费、绕过 ordinary provider recovery → `REVIEW_CE_004` 红。
