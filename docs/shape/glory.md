# Glory：所有权与边界（shape 层）

本文件定义 GLORY 机制的模块所有权、数据流边界与禁止的泄漏路径。条款正文见 `docs/what/glory.md`（GLORY-001..073、SURFACE-001..006）。Magic Todo 所有权见 `docs/shape/todo.md` 与 `TODO-*`。

## 模块所有权

| 关注点 | 拥有者 | 边界 |
|------|------|------|
| Life 事实与投影 | `Domain/ManagerLifecycle.fs` + `Journal/ManagerLifecycleProjection.fs` | 纯逻辑；事实经 `Fact.ManagerLifecycle` case 进入 journal（GLORY-010）；`WorkRecordStart` 由 Life/XTrace Opening 纯推导（TODO-001） |
| Opening / Reawakening 改写 | `Infrastructure/OpenCode/Host/ManagerNarrativeTransform.fs` | 只在 provider-facing transcript 生效；durable X 保持原始（GLORY-013/064）；生产路径无 planning-only Activation 改写义务（TODO-001） |
| Magic Todo checkpoint | todo 域模块（见 shape/todo） | `todowrite` membrane、canonical projection、process-review obligation（TODO-001..014）；GLORY 不拥有其代数 |
| 终结工具 | `Infrastructure/OpenCode/Tools/FinalityTool.fs` | `suicide` 唯一写入口：受理顺序见 GLORY-040；尾抽干/零 checkpoint 门禁 TODO-010 |
| Manager terminal sequencing | `Application/Manager/ManagerWorkflow.fs` | 唯一判定 JoinGuard、Finality defer、Orchestrator handoff 与 idle encouragement；**不**再判定生产 Activation（GLORY-018/070）；`TurnCompletionProgram` 只做普通 terminal plumbing（GLORY-029） |
| 自动评审 | `Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs` | 从 `OrchestratorHostReview` 提炼的通用 reverify（GLORY-042）；process-review 与 Finality 共用 LWR 物化，不另造 renderer（TODO-008） |
| 拒绝记录就绪 | `Infrastructure/OpenCode/Tools/FinalityController.fs` | 关闭 Reviewer cohort 后只等待 durable journal evidence；不写 `BlogEntryCommitted`，只在 GLORY-072 成立时写 `FinalityRejected` |
| 反馈渲染 | `Domain/FinalityPrompt.fs` | `SyntheticToml` 唯一渲染路径（GLORY-052，SURFACE-004） |
| 冻结文本 | 各 Domain owner + `resources/prompts/*.md` | 每个固定文本恰好一个 owner（SURFACE-004）；MagicTodoManagerGuideline 见 TODO-013 |

## 数据流（成功路径）

```text
HumanRoot [X] → XTrace durable capture → LifeOpened
→ Manager 立即持续工作（TODO-001；工具面含 todowrite）
→ TodoWriteAccepted checkpoints（lag-1 ConsumableReview，TODO-006）
→ WorkRecordStart 保护 Opening；PrefixEpoch(TodoCheckpoint) lag-1 rebase（TODO-001/009）
→ suicide(last_words)
→ TODO-010：零 checkpoint fail-closed；抽干最新 ConsumableReview
→ （过程 PERFECT）FinalityRequested → HostReviewProgram（隐藏 cohort + barrier）
   roster 含未 graduate ordinary + 恰好一个新 ordinary
   + 首次 terminal 时 Dedicated ordinary enlist（TODO-010）
→ 全员双 PERFECT → 重读 tree → FinalityBlessed → minor-work continuation
→ （可选）blessing 后继续 todowrite / 工作
→ 第二次 suicide：再抽干过程评审（TODO-010）→ rest in peace → LifeCompleted
→ last_words 成为 terminal → 完成 handle
→ 新 HumanRoot → 下一 Life（Reawakening）
```

## 数据流（失败路径）

```text
suicide → （TODO-010 抽干）过程 REVISE
→ 回灌 ProcessReviewLWR（TODO-006/013）→ 不创建 FinalityRequest → 同一 Life 继续

或

suicide → FinalityRequested → HostReviewProgram → REVISE
→ 立即关闭 Reviewer continuation/cohort
→ `BlogEntryCommitted` 覆盖 reviewer terminal frontier → 同一 snapshot 的 Reviewer LWR（includeOpening=false）
→ FinalityRejected → FinalityPrompt.rejected 拒绝 prompt（作为 suicide 工具返回值直接返回）
→ Manager 同一 Life 继续工作（checkpoint 协议仍运转）→ 再次 suicide → 新 request/Reviewer/barrier
```

## 硬性边界

1. Manager 面（system prompt、continuation、工具 schema、固定 tool result）不得出现 SURFACE-005 禁止的隐藏机制词；checkpoint 过程 outcome/report 是 GLORY-030/TODO-013 唯一窄例外；动态 LWR 不得清洗（GLORY-048）。
2. 自动 Reviewer session（含 Dedicated process reviewer）产生 `HostOwnedHidden` 句柄，对 Manager 的 `list`/`join` 不可见（GLORY-002；TODO-008）。
3. `ManagerOpensReviewBarrier` 从 Manager 普通 fork surface 删除（GLORY-033）；barrier 只由 Finality workflow 与 Orchestrator post-rebase review 拥有。
4. Reviewer / process-review 工作记录只由既有 canonical LWR 物化产生，request-range bounded，`includeOpening=false`（GLORY-004/049；TODO-008）。
5. 成功输出逐字等于 `last_words`，Host 零附加文本（GLORY-062）。
6. 状态身份只来自 typed facts + projection，禁止故事文本反向解析（GLORY-008/009；TODO-012）。
7. `FinalityController` 可等待 `AgentJournal` change，却不得伪造 Blogger frame、推进 RecordCoverage 或把 coverage snapshot 与 LWR materialization 分两次读取；process-local waiter 不构成 durable request abandonment（GLORY-072/073）。
8. 生产路径不得再以 `ManagerWorkActivation` / `WorkActivated` 决定工作资格、压缩 floor 或 Finality；`WorkActivated` 仅 legacy decode（GLORY-018/021）。Opening floor = `WorkRecordStart`（TODO-001）。

## 与既有系统的关系

- **XTrace**：保持 append-only；ManagerLifecycle 按 cursor range 物化 Life（GLORY-066/067）；通用单 Opening/Terminal 字段保留为兼容层。
- **PromptAuthority**：生产 Manager continuation 为 `FinalityRejected` / `FinalitySteer` / `ManagerIdleEncouragement` 等；`ManagerWorkActivation` 仅 legacy（GLORY-018/020/029/053）。Magic Todo Manager-only guidance 见 TODO-013。
- **Blogger**：`effectiveStart = max(RecordCoverage, Life.WorkRecordStart)`（GLORY-023/024；TODO-001）。Prefix rebase 只消费 PrefixCoverage 可证明的 Y（TODO-008/009）。
- **Orchestrator**：ManagerJob 已发布/释放不复活；active owned Job 可由 Orchestrator append requirement（GLORY-068）；HostReviewProgram 由 Orchestrator 与 Manager Finality 共用（GLORY-042/044）。
- **ReviewGuard**：`HostReviewGuard` 仅保留 Reviewer 面（openBarrier/read/verdict）；Manager 面（missingTree/nudgeManager/ManagerGuard）已删除（GLORY-070）。
- **Magic Todo**：checkpoint、ConsumableReview、Dedicated process duty、Host sink reconciliation 的边界以 TODO-* 为准；GLORY 只定义终局 cohort、Blessing、rest 与 Life 合同。
