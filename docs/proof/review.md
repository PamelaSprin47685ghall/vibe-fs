# Review — 证明

行为：`what/review.md`。所有权：`shape/review.md`。程序：`how/review.md`。

## Judge 工具（REVIEW-001）

| 证明 | 期望 | 条款 |
|------|------|------|
| 工具名 `judge`；旧名 `verdict`（工具）非法、无 alias | schema / ToolRegistry / Gate A | REVIEW-001、ARCH-007/016 |
| 参数字段 `verdict` 保留（typed judgment）；无描述字段 / 无固定 formal report schema | 成功回执不 echo verdict | REVIEW-001 |
| Persona Examiner/Auditor + system 字节跨 Fallback/Strength 不变 | Gate D | AGENT-028、FALLBACK-014、ARCH-016 |

## 因果 PERFECT

| 证明 | 期望 | 条款 |
|------|------|------|
| 单次 PERFECT 不足 | 无 Confirmed | REVIEW-003 |
| 第二次含 challenge digest（seal）；`judge` 确实成功执行 | Confirmed | REVIEW-003、REVIEW-010 |
| 同 ProviderRun 额外 PERFECT | 不计数 | REVIEW-004 |
| REVISE | 立即清未完成 PERFECT/关闭 cohort；延迟 `BlogEntryCommitted` 前不得写 `FinalityRejected`；不得在 `judge` 时抢先落盘 | REVIEW-002、GLORY-044/072 |
| tree 变化 | pending 拒绝；confirmed 对 Guard 无效 | REVIEW-008 |
| Examiner's Ledger / PERFECT+minor | 只在值得说处说话；PERFECT 可与 minor 共存；无固定 report DTO | REVIEW-011 |
| Reviewer 提示词权威资源 | `resources/provider/role/reviewer/{en,zh-CN}.md` 承载判断方向；不含双 PERFECT 流程 | REVIEW-012 |


## Seal / Witness

| 证明 | 条款 |
|------|------|
| 绑定 0 或 ≥2 assistant → 不写 seal | REVIEW-010、HOST-010 |
| Witness 自包含，Guard 无外围 Map | REVIEW-006 |
| confirmed 只能派生不能赋值 | REVIEW-006 |

代表：`tests/unit/review/witness.test.mjs`；e2e judge 路径（旧名 `reviewer-verdict.test.mjs` → code phase rename）。

## Guard 顺序

JoinGuard 优先于其它 Manager completion 分支（EXEC-016）；Manager completion 不检查 review
witness，Manager 面无 Review Guard（REVIEW-007、GLORY-070）。`ReviewerWorkflow` 是 ReviewerGuard /
ReviewConfirmation 唯一 writer；durable REVISE 关闭 cohort 后不补发 challenge，record-ready 等待不重开该路径。代表：
`tests/unit/reconciliation/turn-completion-program.test.mjs`、
`tests/e2e/cases/finality-cohort-law.test.mjs`（canary）、e2e judge 路径与
`temporal-ownership-unhappy-path.test.mjs`。
Post-rebase 必须新双 PERFECT（REVIEW-009、ORCH）。

## TodoProcessReview / ConsumableReview

| 证明 | 期望 | 条款 |
|------|------|------|
| RequestKind 分型 | Process 一次 `judge` terminal；Finality 才走 challenge/双 PERFECT；禁止 pendingChallenge 猜测混用 | REVIEW-013 |
| Accepted 与 Rk 1:1 | 每个 `TodoWriteAccepted` 恰好一个 process obligation / TodoReviewId | REVIEW-013、TODO-006 |
| VerdictKnown 不放行下一 Tk | 仅 `judge` 已知时 T(k+1)/suicide 仍阻塞 | REVIEW-014、TODO-006 |
| ConsumableReview ≡ Concluded | record-ready LWR + 同 snapshot `TodoReviewConcluded` 后才可消费 | REVIEW-014/017、TODO-006 |
| 禁止提前 Concluded | verdict 已知但 LWR 未 ready 时无 `TodoReviewConcluded` | REVIEW-014、TODO-012 |
| 无 prose 的过程 PERFECT | fail closed；不形成 ConsumableReview | REVIEW-011/014/016 |
| Process 不写 ConfirmedReviewWitness | 过程 PERFECT 不产生 dual-PERFECT witness | REVIEW-020、GLORY-058 |
| process PERFECT ≠ terminal | Dedicated enlist Finality 后仍 fresh barrier + 双 PERFECT | REVIEW-020、TODO-010 |

## 有界 LWR / safety / coverage

| 证明 | 期望 | 条款 |
|------|------|------|
| 三用途有界 | checkpoint 输入 / process report / finality record 各用冻结 range；禁止 session head | REVIEW-016、TODO-008 |
| 历史 process 不泄漏进终末 LWR | Dedicated 多 Rk 后 Finality LWR 不含 R1..R(k-1) 正文 | REVIEW-016/020、TODO-008 |
| includeOpening=false | Opening 不经 LWR 复制；OpeningMaterial 旁路（旧 Opening task / OpeningRaw 非法） | REVIEW-016、TODO-001、GLORY-004 |
| RecordCoverage 允许 RawGap | Y 未到 frontier 仍可启动 Rk | REVIEW-016、TODO-008 |
| RawGap 不做 prefix | LWR gap 不得证明 X prefix 可替换 | REVIEW-016、TODO-008/009 |
| 无第二 renderer | 不存在 TodoProcessReviewEvidenceProjection / 纯 Y 旁路 | REVIEW-016、TODO-008/012 |
| safety seal | 不清洗 canonical LWR；不能证明 Manager 安全 → fail closed | REVIEW-016、TODO-013、GLORY-048 |
| GLORY-030 窄例外 | 仅 TODO-013 允许的 process PERFECT/REVISE/report 出口 | TODO-013、GLORY-030 |
| 同 snapshot 物化 | coverage 判定与 LWR materialize 同 snap；分两次读 fail | REVIEW-017、GLORY-072/073 |

## 等待 / 失败 / 替换

| 证明 | 期望 | 条款 |
|------|------|------|
| 无 wall-clock polling | record-ready 只由 `awaitChangeFrom`/Journal 事件唤醒；结构+行为拒绝 timer/sleep/re-probe | REVIEW-017、GLORY-073、TODO-012 |
| waiter 崩溃恢复 | 从 durable assignment/VerdictKnown/frontier 续等；不放弃 Rk | REVIEW-017/018、TODO-012 |
| infra ≠ REVISE | create/resume/assignment/LWR 失败不 settle、不 Concluded、不伪 merge | REVIEW-018、TODO-012 |
| infra 阻塞 | outstanding Rk 阻塞下一 TodoWrite 与 Finality 越过 | REVIEW-018、TODO-006/010 |
| 仅 proven loss 替换 | 无永久丢失证据不得 `DedicatedTodoReviewerReplaced`；不确定 fail closed | REVIEW-019、TODO-008 |
| 替换后上下文 | 新 session 含 OpeningMaterial + 有界 Manager LWR + 既往 WorkRecordRef | REVIEW-019、TODO-008 |

## Dedicated / 隐藏面 / Finality drain

| 证明 | 期望 | 条款 |
|------|------|------|
| 每 Life 一个 logical dedicated | 多次 checkpoint 复用同一 DedicatedReviewerId | REVIEW-015、TODO-008 |
| Manager 不可见 | 不能 fork/horizon/join/inspect；surface 无 barrier/witness/2N/roster | REVIEW-015、TODO-013、GLORY-002 |
| graduate ≠ Dispose | Finality graduate 后 process duty 仍在，至少到 LifeCompleted | REVIEW-015/020、TODO-008/010 |
| Blessing 后仍 process | blessing 后 todowrite 仍派生 Rk；二次 suicide 仍 drain | REVIEW-015、TODO-010 |
| suicide drain | latest 非 ConsumableReview 则 await；过程 REVISE 不进 Finality | REVIEW-020、TODO-010 |
| 零 checkpoint first unblessed | fail closed（义务在 TODO-010；审查侧不得放行无 drain 的终末） | TODO-010 |

代表（落地后）：process-review / consumable-review unit 与 Magic Todo canary；Finality 交叉路径复用 `tests/e2e/cases/finality-cohort-law.test.mjs`；禁止轮询与 GLORY_075 同型 waiter 恢复。未落地前本表为合同门禁，不声称已有绿测。治理指针 TODO-014。