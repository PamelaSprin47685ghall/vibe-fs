# finality — HOW

行为合同见 `WHAT.md`（FINALITY-001..028）。本文件只描述实现模型与约束，非 normative。

## 1. 模块地图

| 模块 | 职责 | 命题 |
|---|---|---|
| `src/Wanxiangshu/Domain/ManagerLifecycle.fs` | `ManagerLifecycleFact` 类型（`LifeOpened` / `FinalityRequested` / `FinalityReviewerEnlisted` / `FinalityRejected` / `FinalitySiblingSteered` / `FinalityBlessed` / `FinalityUndecided` / `LifeCompleted`；`WorkActivated` 仅 legacy decode） | 008/021/025 |
| `src/Wanxiangshu/Mission/Manager/Life/Projection.fs` | `LifeProjection` / `FinalityRequestProjection` fold：ActiveFinality、EnlistedReviewers、LastBlessing、Completed、Resolution（Open/Rejected/Blessed/Undecided） | 008-017/021/022 |
| `src/Wanxiangshu/Composition/Bridges/FinalityReview/FinalityReviewCohort.fs` | `rosterOf` / `graduatedReviewer`（纯函数） | 009/010 |
| `src/Wanxiangshu/Application/Finality/FinalityWorkflow.fs` | 终结 CE：start / resume / undecided 收束 | 008/012/026 |
| `src/Wanxiangshu/Application/Finality/CohortWorkflow.fs` | cohort 并发驱动（`concurrentAllOrShortCircuit`） | 008/011 |
| `src/Wanxiangshu/Application/Finality/BlessingWorkflow.fs` | `blessIfTreeUnchanged`（tree 重读 + stable-ordinal bundle + `FinalityBlessed`） | 016 |
| `src/Wanxiangshu/Application/Finality/RevisionWorkflow.fs` | REVISE 关闭 + record-ready 等待 + 双轨交付 | 011/012 |
| `src/Wanxiangshu/Application/Finality/RecordWorkflow.fs` | rejection record 物化 | 011/012 |
| `src/Wanxiangshu/Application/Finality/LifecycleWorkRecordProjection.fs` | canonical LWR 物化（process/Finality 共用；`includeOpening=false`；→ work-record） | 016/017 |
| `src/Wanxiangshu/Application/Manager/ManagerFinality.fs` | `classifyEnding` / `admitLabor`（纯 disposition 代数） | 004-007/014/016-018 |
| `src/Wanxiangshu/Application/Manager/ManagerWorkflow.fs` | Manager terminal sequencing：只判 join / finality / planning / handedOff | 019 |
| `src/Wanxiangshu/Mission/Manager/Idle.fs` | ordinary idle encouragement：从 MagicTodo plan commitment 派生 before/after commitment kind；process key + durable continuation claim 都绑定 exact terminal ProviderRun，Life/condition 只决定文案，不限制 fresh terminal 次数 | 019 |
| `src/Wanxiangshu/Application/Manager/ManagerLifeWorkflow.fs` | `ensureOpening` / `completeBlessedLife`（rest 路径） | 017/022 |
| `src/Wanxiangshu/Domain/FinalityPrompt.fs` | rejection / blessed / steer / undecided 文案（SyntheticToml 唯一渲染） | 012/013/026 |
| `src/Wanxiangshu/Domain/MagicTodoFinalityCohort.fs` | Dedicated 首次 enlist 的 roster 输入 | 009 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FinalityTool.fs` | `suicide` 唯一入口：前置条件 + drain + FinalityOutcome 映射 | 001/003-008 |

## 2. 关键算法（非 normative 摘要）

### classifyEnding（FINALITY-004/007/014/016/017）

`ManagerFinality.classifyEnding(toolCallId, life, hasPlanCommitment)` 纯函数：

```text
not hasPlanCommitment           → ContinuePlanning        // false planning checkpoints 仍是 Pre-T1
life.Completed                  → AlreadyCompleted        // 幂等重放
open request + same ToolCallId  → ResumeRequest
open request + empty Members    → RecoverRequestWithoutReviewers
open request + other call       → WaitForCurrentRequest   // already in motion
LastBlessing 存在               → CompleteBlessedLife     // rest 路径（GLORY-062）
否则                            → BeginFinality           // 新 cohort（GLORY-040）
```

`admitLabor`：open request 拥有 Life（Manager 普通劳动停放，GLORY-041）；
resolved 历史 request（Rejected/Blessed/Undecided）不阻塞劳动。

### FinalityTool.execute（GLORY-034/035/037-041）

前置条件按序检查（先要求 durable plan commitment，再含 TODO-010 drain：`awaitConsumableReview(latest Accepted)`）；
过程 REVISE → 回灌 ProcessReviewLWR、sink reconcile、**不**建 FinalityRequest；
过程 PERFECT → `gitTreePort.GetTreeHash()` → journal `WriteBlob last_words` →
append `FinalityRequested` → 启动 `FinalityController` cohort CE（`rosterOf` 选员）。
tool result 由 `FinalityOutcome` 映射三种经验（Rejected / Blessed / Undecided）。
Blessed Life 再 suicide → 先 drain，再 `completeBlessedLife`（at rest）。

### Manager ordinary idle（FINALITY-019 / GLORY-029）

`ManagerIdle.encourageLabor` 先由 current Life + MagicTodo plan commitment 派生 encouragement kind：未 commit plan = before commitment，已 commit = after commitment。幂等 identity 是 exact terminal `ProviderRunIdentity`（同时携带 Life/condition 以便审计）；同一个 terminal 的重复 idle 只消费一次 permit/claim，不重复发送。**新的 completed Manager terminal 永远是新的 encouragement occasion**，即使仍处相同 pre-T1/post-T1 condition，也必须再次发送；不存在 Life 级或 condition 级次数上限。open finality / completed Life / join outstanding 仍由 ManagerWorkflow 在此之前拦截。

### Cohort 收束（GLORY-044/059/060）

任一 REVISE → 立即关闭 continuation/cohort；随后按双轨收束（record-ready 物化 →
预置 primary blob → 一次性 append `FinalitySiblingSteered` → 密封 `FinalityRejected`）。
全员 dual-PERFECT → 重读 tree → 与 `FinalityRequested.GitTreeHash` 比较；不等 → fail closed；
相等 → stable-ordinal LWR bundle → `FinalityBlessed` → minor-work continuation。
无法证明 → `FinalityUndecided`，不伪造 work record。

### 第二次 suicide / rest（GLORY-062）

`classifyEnding → CompleteBlessedLife`：先 drain（REVISE → 继续 Life）；无阻塞 REVISE 时
不读 tree / 不建 Reviewer / 不检查 witness；写 last_words → `LifeCompleted` → NotifyTerminal →
at-rest 经验。输出逐字等于 last_words。

## 3. 历史与弃权

### GARBAGE —— 不进入 WHAT 的历史沉积

| 内容 | 裁决理由 |
|---|---|
| 生产 `ManagerWorkActivation` / `WorkActivated` 资格门、`PlanningTail`、Birth/Labor floor、Activation-only suicide gate | planning→Activation 两阶段删除；`WorkActivated` 仅 inert legacy decode，不得决定工作/压缩/Finality（GLORY-014/016..021）。不在本包 WHAT 中写成命题 |
| `status="already_completed"` / `"already_received"` 与 `Work log N` ordinal | 三种经验分型删除这些枚举（GLORY-076）；idempotent replay 重放原 result |
| 旧 `HostReviewGuard` 的 Manager 面（missingTree/nudgeManager/ManagerGuard） | 删除；ManagerWorkflow 只判 join/finality/planning/handedOff（GLORY-070/REVIEW-007） |
| 结构化 `FinalityFinding` schema / 固定 report DTO | 第二事实源 + 摘要漂移；反馈 = canonical LWR（GLORY-004/049/050） |
| `verdict`（旧工具名） | `judge` 属 Reviewer、`suicide` 属 Manager，因果身份不同 |

### HOW —— 当前实现形状，非永久需求

| 内容 | 说明 |
|---|---|
| `suicide` 字面工具名与叙事风格（"End your life when your task is complete."） | 实测抑制提前结束；内部模块用 Finality 语义命名。**不是**永久 contract——改名/改 UX 不改变「只有合格证据才允许 life completion」（边界 card DOES NOT OWN） |
| hidden Reviewer 的 HostForkRuntime / `HostOwnedHidden` 句柄 | 当前隐藏机制形状（GLORY-002）；信息准入 law 归 participant-horizon |
| `FinalityRequested` 事实字段（GitTreeHash/LastWordsRef/ProviderRun/ToolCallId…） | 当前事实形态；语义合同以 WHAT 为准 |
| FinalityOutcome 映射的具体 tool result 文案 | 文案字节归 provider-language / SURFACE-004；三经验语义在本包 |
| record-ready 的同 snapshot 物化细节 | review-assurance 机制 |
| LWR bundle 的 stable-ordinal 物化 | work-record 机制 |
| lifecycle completion 后 dedicated reviewer session 退休 | `managed-session-lifecycle` owner-closure 的下游 effect，非 finality 定义前提 |

### 与邻域包的交界（引用不复制）

- `obligation-ledger`：drain 输入（ConsumableReview）、零 checkpoint 门禁的 Accepted 计数 →
  引用 OBLIGATION-LEDGER-004/010/022。
- `review-assurance`：dual-PERFECT witness 因果代数、record-ready、tree 新鲜性、VerdictKnown
  与 ConsumableReview 分型 → 引用其命题（本包只消费）。
- `participant-horizon`：Manager 面可见/禁止词与句的 admission law → 引用其命题。
- `work-record`：canonical LWR 物化、三段标题、`includeOpening=false` → 引用其命题。
- `crash-reconciliation`：undecided 恢复与 infra fatal 的进程级处理 → 引用其命题。
- `change-integration`：ManagerJob 投影与 `ContinueManager` 在 Orchestrator 域；本包拥有
  FINALITY-028（已发布/释放不复活；active 可在同 session/worktree 续做）。
