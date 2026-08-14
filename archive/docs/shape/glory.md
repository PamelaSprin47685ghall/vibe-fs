# Glory：所有权与边界（shape 层）

本文件定义 GLORY 机制的模块所有权、数据流边界与禁止的泄漏路径。条款正文见 `docs/what/glory.md`（GLORY-001..076、SURFACE-001..006）。Magic Todo 所有权见 `docs/shape/todo.md` 与 `TODO-*`。OpeningMaterial / WorkRecord 三段标题的表示合同见 COMPANION-003/014/015；本文件只定 Manager Life 侧所有权与 floor。

## 模块所有权

| 关注点 | 拥有者 | 边界 |
|------|------|------|
| Life 事实与投影 | `Domain/ManagerLifecycle.fs` + `Journal/ManagerLifecycleProjection.fs` | 纯逻辑；事实经 `Fact.ManagerLifecycle` case 进入 journal（GLORY-010）；`WorkRecordStart` 由 Life/XTrace Opening 纯推导（TODO-001；BlindPlan 含 T1） |
| Durable Life open / migrate / activate | `Application/Manager/ManagerLifeWorkflow.fs` | `ensureOpening` / `ensureMigrated` / `acceptActivation`（及既有 `ensureMigrationLife` / `completeBlessedLife`）；不读 Host wire |
| Opening / Reawakening 改写 | `Infrastructure/OpenCode/Host/ManagerNarrativeTransform.fs` | 只在 provider-facing transcript 生效；wire 门控 + rewrite；durable 写委托 `ManagerLifeWorkflow`；durable X 保持原始（GLORY-013/064）；生产路径无 planning-only Activation 改写义务（TODO-001；GLORY-074） |
| OpeningMaterial | XTrace 区间 `[work start, OpeningBoundary)`（COMPANION-014） | preserved，禁止 `OpeningPromptRaw` 拼接重建；BlindPlan 下 T1 call/result 属 constitutive Opening（GLORY-006/022/074） |
| OpeningPolicy / BlindPlan | Manager Life + TODO-015 冻结文案 | `BlindPlan` = first accepted `todowrite`（T1）；交托在 conversation tool result，不在 system prompt 切换（GLORY-074/075） |
| LWR journal 物化 | `Application/Finality/LifecycleWorkRecordProjection.fs` | XTrace/`chronicle` → opaque work record；三标题 `Opening / Chronicle / Recent work`（GLORY-025；COMPANION-003）；Terminal 非 LWR 段；与 `XTraceCapture` 分居（TODO-008） |
| Magic Todo checkpoint | todo 域模块（见 shape/todo） | `todowrite` membrane、canonical projection、process-review obligation（TODO-001..015）；GLORY 不拥有其代数 |
| 终结工具 | `Infrastructure/OpenCode/Tools/FinalityTool.fs` | `suicide` 唯一写入口：受理顺序见 GLORY-040；尾抽干/零 checkpoint 门禁 TODO-010 |
| Manager terminal sequencing | `Application/Manager/ManagerWorkflow.fs` | 唯一判定 JoinGuard、Finality defer、Orchestrator handoff 与 idle encouragement；**不**再判定生产 Activation（GLORY-018/070）；`TurnCompletionProgram` 只做普通 terminal plumbing（GLORY-029） |
| 自动评审 | `Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs` | 从 `OrchestratorHostReview` 提炼的通用 reverify（GLORY-042）；process-review 与 Finality 共用 LWR 物化，不另造 renderer（TODO-008） |
| 拒绝记录就绪 | `Infrastructure/OpenCode/Tools/FinalityController.fs` | 关闭 Reviewer cohort 后只等待 durable journal evidence；不写 `BlogEntryCommitted`，只在 GLORY-072 成立时写 `FinalityRejected` |
| 反馈渲染 | `Domain/FinalityPrompt.fs` | `SyntheticToml` 唯一渲染路径（GLORY-052，SURFACE-004）；wire 标题同 LWR 三段 |
| 冻结文本 | 各 Domain owner + `resources/provider/**` | 每个固定文本恰好一个 owner（SURFACE-004）；MagicTodoManagerGuideline / BlindPlan T1 见 TODO-013/015 |

## 数据流（成功路径）

```text
HumanRoot [X] → XTrace durable capture → LifeOpened
→ BlindPlan Opening（Planning Table；TODO-001 / GLORY-074）
→ Manager 立即持续工具面（含 todowrite）；Pre-T1 不扛路
→ T1 accepted → Opening 关闭；WorkRecordStart = OpeningBoundary
→ TodoWriteAccepted checkpoints（lag-1 ConsumableReview，TODO-006）
→ WorkRecordStart 保护 OpeningMaterial；PrefixEpoch(TodoCheckpoint) lag-1 rebase（TODO-001/009）
→ suicide(last_words)
→ TODO-010：零 checkpoint fail-closed；抽干最新 ConsumableReview
→ （过程 PERFECT）FinalityRequested → HostReviewProgram（隐藏 cohort + barrier）
   roster 含未 graduate ordinary + 恰好一个新 ordinary
   + 首次 terminal 时 Dedicated ordinary enlist（TODO-010）
→ 全员双 PERFECT → 重读 tree → FinalityBlessed → minor-work continuation
→ （可选）blessing 后继续 todowrite / 工作
→ 第二次 suicide：再抽干过程评审（TODO-010）→ rest in peace → LifeCompleted
→ last_words 进入 Recent work（普通助手文本）→ 完成 handle
→ 新 HumanRoot → 下一 Life（Reawakening；再入 BlindPlan）
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
2. 自动 Reviewer session（含 Dedicated process reviewer）产生 `HostOwnedHidden` 句柄，对 Manager 的 `horizon`/`join` 不可见（GLORY-002；TODO-008）。
3. `ManagerOpensReviewBarrier` 从 Manager 普通 fork surface 删除（GLORY-033）；barrier 只由 Finality workflow 与 Orchestrator post-rebase review 拥有。
4. Reviewer / process-review 工作记录只由既有 canonical LWR 物化产生，request-range bounded，`includeOpening=false`；三标题为 `Opening / Chronicle / Recent work`（GLORY-004/025/049；TODO-008；COMPANION-003）。旧标题 `Opening task / Work log / Uncompressed tail / Final output` 与 `Closing report` 已删除。
5. 成功输出逐字等于 `last_words`，Host 零附加文本（GLORY-062）。
6. 状态身份只来自 typed facts + projection，禁止故事文本反向解析（GLORY-008/009；TODO-012）。
7. `FinalityController` 可等待 `AgentJournal` change，却不得伪造 Blogger frame、推进 RecordCoverage 或把 coverage snapshot 与 LWR materialization 分两次读取；process-local waiter 不构成 durable request abandonment（GLORY-072/073）。
8. 生产路径不得再以 `ManagerWorkActivation` / `WorkActivated` 决定工作资格、压缩 floor 或 Finality；`WorkActivated` 仅 legacy decode（GLORY-018/021）。Opening floor = `WorkRecordStart` / OpeningMaterial 边界（TODO-001；GLORY-074）。
9. OpeningMaterial 唯一真相 = preserved XTrace 区间；禁止第二事实源重建（COMPANION-014；GLORY-022）。

## 与既有系统的关系

- **XTrace**：保持 append-only；ManagerLifecycle 按 cursor range 物化 Life（GLORY-066/067）；OpeningMaterial 为 range 上 preserved 区间，非通用单字段重建。
- **PromptAuthority**：生产 Manager continuation 为 `FinalityRejected` / `FinalitySteer` / `ManagerIdleEncouragement` 等；`ManagerWorkActivation` 仅 legacy（GLORY-018/020/029/053）。BlindPlan / Magic Todo Manager-only guidance 见 TODO-013/015；同一 Life 内 system prompt 字节不因 T1 改变（GLORY-075）。
- **Blogger / chronicle**：`effectiveStart = max(RecordCoverage, Life.WorkRecordStart)`（GLORY-023/024；TODO-001）。Prefix rebase 只消费 PrefixCoverage 可证明的 Y（TODO-008/009）。provider 记账动词 = `chronicle`（旧名 `blog` 非法）。
- **Orchestrator**：ManagerJob 已发布/释放不复活；active owned Job 可由 Orchestrator append requirement（GLORY-068）；HostReviewProgram 由 Orchestrator 与 Manager Finality 共用（GLORY-042/044）。
- **ReviewGuard**：`HostReviewGuard` 仅保留 Reviewer 面（openBarrier/read/`judge`）；Manager 面（missingTree/nudgeManager/ManagerGuard）已删除（GLORY-070）。
- **Magic Todo / BlindPlan**：checkpoint、ConsumableReview、Dedicated process duty、T1 commitment 的边界以 TODO-* 为准；GLORY 只定义终局 cohort、Blessing、rest、Life 与 OpeningPolicy 合同（GLORY-074）。