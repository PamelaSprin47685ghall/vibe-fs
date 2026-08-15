# HOW — review-assurance

> 本文非 normative。它解释 assurance 语义在当前实现里落地的模型与位置，并收纳「历史与弃权」。
> Normative 合同只有 `WHAT.md`。

## 实现模型

### 1. Witness / challenge 代数（纯 Domain）

- `src/Wanxiangshu/Domain/ReviewWitness.fs`：
  - `VerdictWitness { ProviderRun; ToolCallId; GitTreeHash; ReviewerSessionId }`——刻意**无** AuthorityRoot 字段（REVIEW-003/006）。
  - `ReviewWitness = NoReview | RevisionWitness | PerfectPending | Confirmed`——confirmed 是 union case 携带证据，不是布尔（REVIEW-004/005）。
  - `confirm(barrier, challengeDigest, secondInputDigest, first, second)`：digest 是参数不是布尔——witness 自带证据；`isDistinctAttempt` 是必要非充分条件（条件 6 在 seal 侧）。
  - `isValidForTree` 是派生谓词（REVIEW-008）；`attemptIdentity` 组装五元组（REVIEW-004）。
- `src/Wanxiangshu/Domain/ReviewChallenge.fs`：
  - `Path = "review/challenge"`、`TextVersion = 1`、`promptOf`（ARCH-010 `# …\n` 指令形式）、`contentDigest` 委托 `ProviderProjection.toolResultDigest`——challenge 是工具结果，digest 必须与 seal 同源（REVIEW-003/010）。

### 2. 投影与 fold（Journal）

- `src/Wanxiangshu/Mission/Review/Barrier/Projection.fs`：
  - `PerfectChallenge`（第一次 PERFECT 的 durable 证据）、`ProviderInputSeal`（`IncludedToolResultDigests` = 因果证据集）、`ReviewGuardProjection`（barrier/tree/witness/TerminalFrontier/PendingChallenge/Seals/`ObservedAttempts: ReviewAttemptIdentity list`/`ClosedAttempts: ClosedAttempt list`）。attempt 窗口是唯一积分器维护的 bounded typed evidence；业务层禁止从 Journal/XTrace 重建 verdict identity。
  - `startBarrier`：新 barrier 清 pending challenge 与 attempt/closure 窗口，保留 confirmed witness 可审计（REVIEW-008）；同 barrier 重入幂等。
  - `applyChallengeIssued`：pending witness 由 challenge 构建，参数只有一个——防两者不一致（REVIEW-005）。
  - `applyVerdict`：REVISE 清 pending（REVIEW-002 条件 7 的机制）；**不**判因果、不构建 Confirmed——writer 证明、fold 应用（seal 窗口有界，重放不重证）。
  - `applyAttemptClosed`/`closedAttemptOf`（REVIEW-013/017）：按 attempt identity 幂等记录 closure；消费方用 `closedAttemptOf` 取冻结 frontier，禁止读 session 当前 head。
  - 窗口：`AttemptWindow = 8`、`SealWindow = 8`（PERSIST-008 有界）。
- `src/Wanxiangshu/Mission/Review/Barrier/Workflow.fs`（`openBarrier`）、`ReviewFactFold.fs`（fold 分派，含 `ReviewAttemptClosed`）、`FinalityReviewCohort.fs`（roster/graduate 代数——finality 消费侧）。

### 3. 确认写入与 continuation（Application/Review）

- `src/Wanxiangshu/Application/Review/VerdictWorkflow.fs`：
  - `VerdictSubmission` 携带一次判断的全部身份；`submit` 是 `PerfectChallengeIssued` 与 `ConfirmedReviewWitness` 的唯一 writer。
  - `provenSeal`：`Map.tryFind providerRun guard.Seals` 且 `IncludedToolResultDigests ∋ ChallengeContentDigest` → 因果证明；否则 `ChallengeUnproven`。
  - 分支：`AlreadyCounted`（REVIEW-004 去重）→ `ProcessTerminal`（REVIEW-013 过程一次判断）→ `Revised` / `ChallengeIssued` / `Confirmed` / `ChallengeUnproven`。
- `src/Wanxiangshu/Application/Review/ReviewBarrierWorkflow.fs`（`reverify`：openBarrier → awaitWitness 事件驱动 → readStatus 读 durable 证据，`ConfirmationUnproven`/`ReviewerProducedNoVerdict` 续等）、`ReviewerContinuation.fs`（ensureVerdictSubmitted / ensurePerfectConfirmed）、`ReviewerWorkflow.fs`（observe 唯一业务 writer）。
- `HostReviewGuard`（`Mission/Review/OpenCode/HostGuard.fs`）：missing-verdict / confirm-perfect 物理发送。预留键在 `SharedState.ReviewGuardNudges`（HOST-012）。missing-verdict 的 occasion 是 durable `ReviewBarrierId`，**不含 RuntimeId，也不含触发它的 ProviderRunIdentity**——root/worktree 可能在不同 provider run 上观察到同一个缺失 judge，但它们仍只是在处理同一个 review requirement；confirm-perfect 才以第一次 PERFECT 的 provider run 标识 challenge occasion。本地 journal 的 `PendingClaims` 仍作同实例二次闸；无 open barrier 的 missing-verdict repair fail closed。
- `src/Wanxiangshu/Application/Review/ReviewerEvidence.fs`：`continuationOpen`（sibling REVISE 后撤销 capability）、`classifyNeed`（process → `CompleteRevision` 无 confirmation nudge）、`confirmed` 从 witness 派生。

### 4. Seal 绑定与 attempt scope 冻结（Mission/Review/Assurance/Seal.fs）

- `bindableRun`（HOST-010）：候选 = assistant ∧ ¬Completed ∧ ¬compaction ∧ ParentId = physical user；恰好一个且为最新 id → Ok，否则 `NoBindableRun | AmbiguousRun | NotLatestRun`——四条件合取，缺一 admit 错误答案。
- `sealTransform`：只在 Reviewer session 的 `messages.transform` 时刻 park seal 候选（`IncludedToolResultDigests` 含 challenge digest）；challenge request 一律 deferred binding。候选同时冻结 `ManagerSessionId + BarrierId + GitTreeHash`（REVIEW-013 attempt scope）——该 request 启动时的身份，之后任何新 barrier 都不能给这个 attempt 改身份证。
- `bindToRun`：`JudgeTool` 以工具持有的 ProviderRunId 查询并 append `ProviderInputSealed`，返回带冻结 scope 的候选——唯一绑定点；`NoPendingSeal`/`AppendFailed` fail closed。历史上曾在 onTurn 二次绑定（Host 1.18.10 下 run id 不一致，实测每轮 dual-PERFECT 失败）——已删第二 writer。
- `JudgeTool.execute`（OpenCode/JudgeTool.fs）：先 bindToRun，再用冻结 scope 构造 `VerdictSubmission` 一次性 `VerdictWorkflow.submit`；不再读 current barrier/current tree。

### 5. record-ready 与消费（Application/Review/TodoProcessReviewProgram.fs）

- `tryConclude`：**同一 snapshot** 读 checkpoint + reviewer guard；VerdictKnown 从 `ObservedAttempts` 最新 typed identity 读取，且必须存在**匹配的 `ReviewAttemptClosed`**（reviewer 的 reconciled turn 已完成、XTrace 已收敛）；record frontier 取 closure 冻结值，禁止取 session 当前 head——已结束 turn 的迟到 XTrace 尾巴不能穿进下一 checkpoint 的 request range。用同一 snapshot 的冻结 range `[ReviewWorkStartCursor, closure frontier)` 物化 canonical ProcessReviewLWR；非空 report → writeBlob → 以该 typed `ProviderRun/ToolCallId` append `TodoReviewConcluded`。任何不足 → `Pending`（等待信号，不是 provider 红字）。
- `appendAttemptClosed`（ReviewerWorkflow）：turn 完成（含 terminal capture 收敛）后 append `ReviewAttemptClosed`；无 observed attempt 的 turn 无可关闭对象；append 失败保持未闭合，idle revisit 重跑、fold 按 attempt identity 幂等。
- `producerPresence`：无 durable verdict 时，只有 reviewer work-unit 仍可继续（Active/CompletedAwaitingJoin）才允许等待；durable process verdict 已存在后，handle `CompletedAwaitingJoin`/`Retired` 不再代表 producer loss，剩余 producer 是 closure/record-ready 收敛。无 verdict 且 Abandoned/Retired 才 `Absent`。
- `awaitConsumableReview`：Direct CE 有界递归——先采样 `revision` → `tryConclude` → Pending → producer 存在 → `awaitChangeFrom sampledRevision` → 重判；因此判定与订阅之间发生的 append 会令 await 立即完成，不丢 wakeup。producer 缺失 → Error fail closed（REVIEW-017/018）。无 stage/phase/program counter，无 total-review deadline。
- assignment reentry（DedicatedTodoRuntime.ensureReview）：`TodoProcessReviewAssigned` 在物理发送**前** durable append，冻结 pre-dispatch reviewer frontier；resend 准入读 `PromptAuthorityLedger.dispatchStatusFor`（Accepted/Pending/Dispatchable）——XTrace head 不参与 dispatch 成功判断（REVIEW-018）。

### 6. 数据流

```text
第一次 PERFECT → PerfectChallengeIssued（pending + challenge tool result）
  → ReviewConfirmation 启动下一 provider request（不是确认事实）
  → transform 时刻 park seal（冻结 attempt scope + IncludedToolResultDigests）
  → 第二次 PERFECT → JudgeTool bindToRun → provenSeal 查 digest
  → ConfirmedReviewWitness（自包含）→ Finality cohort 消费（finality）
TodoProcessReview：一次 judge（冻结 scope 一次 submit）→ ProcessTerminal → VerdictKnown
  → reviewer turn 完成 → ReviewAttemptClosed（冻结 frontier）
  → tryConclude 同 snapshot record-ready → TodoReviewConcluded（≡ ConsumableReview）
  → T(k+1)/suicide 消费
```

## 依赖（DEPENDS ON）

| 依赖 | 理由（一句话） |
|---|---|
| `review-judgement` | assurance 消费的是 judgement；judgement 语义由 review-judgement 定义（本包不复制）。 |
| `semantic-trace` | verdict/witness 事实落在 Journal/XTrace 语义历史；record-ready 的 frontier 是 XTrace 边界。 |
| `durable-events` | challenge/seal/witness/Concluded 都是 durable facts，经 append-only journal 与 fold 重放（REVIEW-010 绑定、REVIEW-014 两段式）。 |
| `causal-wait` | record-ready 等待只经 `AgentJournal.awaitChangeFrom` 事件唤醒，禁 timer/polling；因果可观测性由 causal-wait 提供。 |

## 历史与弃权

### 被拒方案（保留考古，不进入 WHAT）

来自历史 why/review「备选与被拒」与历史 shape/review 条款：

- **单 PERFECT 即确认**：拒（可被随口同意）→ 双 PERFECT + seal（REVIEW-003）。
- **外围 Map 补 witness 身份**：拒（恢复/并发读到别人或空的确认）→ 自包含 witness（REVIEW-006）。
- **tree 变化后旧确认坚持**：拒（审的是代码状态不是 Session 情绪）→ 派生失效（REVIEW-008）。
- **same-root / physical-message 猜测绑定**：拒（Host 重排消息时假绿）→ 唯一绑定 + fail closed（REVIEW-010/HOST-010）。
- **「只有 verdict 即放行」**：拒 → VerdictKnown 与 ConsumableReview 两段式（REVIEW-014）。
- **sleep/timer 等待 record-ready**：拒（把因果等待退化成运气；waiter 崩溃无法重建）→ awaitChangeFrom + 同 snapshot（REVIEW-017/GLORY-073）。
- **XTrace head watermark 判 assignment 送达**：拒（XTrace append 顺序 ≠ request 因果顺序；已送达的 assignment 可以两秒不涨 head）→ PromptAuthority durable dispatch evidence（Accepted/Pending/Dispatchable），2s deadline 与 AwaitHead 整体删除（REVIEW-018）。
- **verdict 即 Concluded**：拒（judge 执行 ≠ reviewer turn 完成；迟到的 XTrace 尾巴会穿进下一 checkpoint 的 range）→ `ReviewAttemptClosed` 冻结 frontier，`TodoReviewConcluded` 只消费 closure 值（REVIEW-013/017）。
- **judge 读 current barrier/current tree 猜身份**：拒（上一轮未真正结束就开下一 barrier 时，旧 attempt 会被未来 barrier 重标记）→ transform 冻结 scope，绑定后一次性 submit（REVIEW-013）。
- **infra 失败伪 REVISE**：拒 → typed infra failure + obligation outstanding（REVIEW-018）。
- **过程 PERFECT 计入 terminal 2N**：拒 → 代数分离（REVIEW-020/GLORY-058）。
- **onTurn 二次 seal 绑定**：拒（Host 1.18.10 下 reconcile run 与工具 run id 不一致，实测全流失败）→ 唯一绑定点 bindToRun（ReviewSeal.fs 注释记录）。

### 弃权记录（GARBAGE / HOW 裁决）

| 内容 | 判定 | 理由 | 记录位置 |
|---|---|---|---|
| 历史 change（fix-revise） | GARBAGE（review transcript） | REVISE follow-up 登记；其 Gap A 的 record-ready fail-closed 回归与 waiter 恢复已由 GLORY-072/073 命题 + 本包 `consumable-review.test.mjs` 与 `tests/unit/execution|temporal` 回归承接；transcript 本身不是规范 | WHAT 反向覆盖；本 HOW §5；WHY 考古 |
| 历史 change（ce-revise-review） | GARBAGE（review transcript） | CE 复审记录；Student–Teacher 争议归 session-ontology/delegation（`universal.md`/`ce-student-teacher-collapse.md`），与本包无 normative 关系 | 见 `review-judgement` HOW；CHANGES-AUDIT |
| `ChallengeTextVersion=1`、英文 canonical 字节不变版本保持 | HOW | 文案世代机制（COVERAGE review.md GARBAGE 行）；版本是解码语义不是产品合同 | 本 HOW §1；WHAT 不冻结版本号 |
| `fast-reviewer` / `deep-reviewer` 机器名 | GARBAGE | HANDOFF §12：machine names 不进入永久 WHAT | 不提及 |
| REVIEW-007 的 Manager 面无 Review Guard | 边界 → `finality` | ManagerWorkflow 分支判据是 finality 的（GLORY-070）；本包移动的 witness 文件含 barrier-mirror 传输断言（`REVIEW_007_*`），cutover 时按断言拆给 finality | PROOF.md SPLIT@cutover 行 |
| REVIEW-002 的 cohort 关闭 | 边界 → `finality` | 关 cohort/continuation 是 finality 请求生命周期；本包只使用「REVISE 清 pending challenge」作为条件 7 机制 | WHAT REVIEW-ASSURANCE-001 边界 |
| `tests/unit/review/` 两文件的旧路径 | 迁移记录 | 已物理移入本包 tests/（MOVE）；旧路径删除 | PROOF.md |
