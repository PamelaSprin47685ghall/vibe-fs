# finality — HOW

行为合同见 `WHAT.md`（FINALITY-001..028）。本文件只描述实现模型与约束，非 normative。

## 1. 模块地图

| 模块 | 职责 | 命题 |
|---|---|---|
| `src/Wanxiangshu/Mission/Manager/Life/Facts.fs` | `ManagerLifecycleFact` 类型（`LifeOpened` / `FinalityRequested` / `FinalityReviewerEnlisted` / `FinalityRejected` / `FinalitySiblingSteered` / `FinalityBlessed` / `FinalityUndecided` / `LifeCompleted`；`WorkActivated` 仅 legacy decode，writer 已删除 2026-08-17 (LEGACY-010 closed)，e2e long-stroke 已解耦） | 008/021/025 |
| `src/Wanxiangshu/Mission/Manager/Life/Projection.fs` | `LifeProjection` / `FinalityRequestProjection` fold：ActiveFinality、EnlistedReviewers、LastBlessing、Completed、Resolution（Open/Rejected/Blessed/Undecided） | 008-017/021/022 |
| `src/Wanxiangshu/Composition/Bridges/FinalityReview/FinalityReviewCohort.fs` | `rosterOf` / `graduatedReviewer`（纯函数） | 009/010 |
| `src/Wanxiangshu/Mission/Manager/FinalitySurface.fs` | JS-native owner boundary: lifecycle/history fold, Life/cohort views, ending/labor decisions, background obligation and ManagerJob projection | 001-010/016-028 |
| `src/Wanxiangshu/Mission/Finality/PromptSurface.fs` | JS-native owner boundary: Manager narrative projections, lifecycle/finality prompt documents, and role prompt resource text | 004/012/013/019/020/022/024 |
| `src/Wanxiangshu/Mission/Finality/Workflow.fs` | 终结 CE：start / resume / undecided 收束 | 008/012/026 |
| `src/Wanxiangshu/Mission/Finality/Cohort.fs` | cohort 并发驱动（`concurrentAllOrShortCircuit`） | 008/011 |
| `src/Wanxiangshu/Mission/Finality/Blessing.fs` | `blessIfTreeUnchanged`（tree 重读 + stable-ordinal bundle + `FinalityBlessed`） | 016 |
| `src/Wanxiangshu/Mission/Finality/Revision.fs` | REVISE 关闭 + record-ready 等待 + 双轨交付 | 011/012 |
| `src/Wanxiangshu/Mission/Finality/Record.fs` | rejection record 物化 | 011/012 |
| `src/Wanxiangshu/Mission/WorkRecord/Materialize.fs` | canonical LWR 物化（process/Finality 共用；`includeOpening=false`；→ work-record） | 016/017 |
| `src/Wanxiangshu/Mission/Manager/Finality.fs` | `classifyEnding` / `admitLabor`（纯 disposition 代数） | 004-007/014/016-018 |
| `src/Wanxiangshu/Mission/Manager/Workflow.fs` | Manager terminal sequencing：只判 join / finality / planning / handedOff | 019 |
| `src/Wanxiangshu/Mission/Manager/Idle.fs` | ordinary idle encouragement：从 MagicTodo plan commitment 派生 before/after commitment kind；process key + durable continuation claim 都绑定 exact terminal ProviderRun，Life/condition 只决定文案，不限制 fresh terminal 次数 | 019 |
| `src/Wanxiangshu/Mission/Manager/Life/Workflow.fs` | `ensureOpening` / `completeBlessedLife`（rest 路径） | 017/022 |
| `src/Wanxiangshu/Mission/Finality/Prompt.fs` | rejection / blessed / steer / undecided 文案（SyntheticToml 唯一渲染） | 012/013/026 |
| `src/Wanxiangshu/Mission/Obligation/Todo/FinalityCohort.fs` | Dedicated 首次 enlist 的 roster 输入 | 009 |
| `src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs` | `suicide` 唯一入口：前置条件 + drain + FinalityOutcome 映射 | 001/003-008 |

## 2. JS semantic boundary

`FinalitySurface` is the single production owner for the Finality semantic test
zone. Plain lifecycle, review, handle, and ManagerJob history enters through
`project` / `jobProjection`; typed facts, durable projections, and cohort
algebra remain private to production. Life and cohort views, ending/labor
classification, background obligations, and ManagerJob recovery are emitted as
JSON-shaped objects/arrays. `World` is an opaque capability: tests create it,
pass it back, and never inspect it. Prompt-language assertions enter the
separate production-owned `Mission/Finality/PromptSurface` boundary, whose
narrative parts are arrays and whose prompt/resource APIs return strings. No
package-local support module re-exports compiled Fable internals.


## 3. 关键算法（非 normative 摘要）

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

### handleEnding（FINALITY-003，SW-017① 对齐）

`ManagerFinality.handleEnding(disposition, exec)` 在 Finality 域内 dispatch ending action。
Tool adapter 构造 `FinalityEndingExecution` record（封装执行能力）并调用 `handleEnding`，
后者内部 match disposition 并返回 `FinalityEndingOutcome`（`Refused path | Result toolResult`）。
Tool adapter 只渲染边界结果，不 match `EndingDisposition` case。`EndingDisposition` 仍是纯领域分类，
但不再是 child action opcode → caller effect 的 CE seam。

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

## 4. 历史与弃权

### GARBAGE —— 不进入 WHAT 的历史沉积

| 内容 | 裁决理由 |
|---|---|
| 生产 Activation 资格门、`WorkActivated` 资格门、`PlanningTail`、Birth/Labor floor、Activation-only suicide gate | planning→Activation 两阶段删除；`acceptActivation` / `applyAcceptedActivation` / wire Activation 检测已删除（无 creditor）；`WorkActivated` 仅 inert legacy decode，writer 已删除 2026-08-17，ratchet 改为断言 writer 不存在（LEGACY-010 closed，e2e long-stroke 已解耦），不得决定工作/压缩/Finality（GLORY-014/016..021）。不在本包 WHAT 中写成命题 |
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

## DEPENDS ON

- `obligation-ledger`：终结资格依赖「当前仍欠什么」的唯一真相源与 1:1 过程评审节拍；drain 消费的是其 ConsumableReview 义务。
- `review-assurance`：cohort 收束必须消费「对当前 request/barrier/tree 有资格」的 dual-PERFECT witness；rejection 记录必须 record-ready。
- `participant-horizon`：隐藏 Reviewer / barrier / witness / cohort 不进 Manager 面是信息准入约束（与 delegation 同型）——本包只拥有「隐藏机制不变成 Manager checklist、只暴露 consequence」的 finality 侧。

## 验证与测试落点

行为合同：`WHAT.md`（FINALITY-001..028）。实现模型：`HOW.md`。

### 测试资产

#### 本包 tests/（`requirements/finality/tests/`）

所有 Finality semantic tests 只导入注册的
`dist/Mission/Manager/FinalitySurface.js`。输入是 plain lifecycle/job/handle
history，输出是 plain objects/arrays；Life、cohort、ManagerJob、handle
projection 的 F# facts 与 unions 留在 `FinalitySurface.fs` 内。旧的
`tests/support/finality-surface.mjs` re-export 已删除，测试区不再拥有
Finality/domain/Fable authority。

| 文件 | 来源 | 类型 |
|---|---|---|
| `manager-finality-disposition.test.mjs` | NEW | `classifyEnding` / `admitLabor` / capability / ReviewerOutcome JS contract |
| `manager-job-no-resurrection.test.mjs` | NEW | FINALITY-028 ManagerJob history projection |
| `finality-background-obligation.test.mjs` | NEW | FINALITY-027 parent-visible handle projection |
| `lifecycle.test.mjs` | REUSE | lifecycle fact projection, completion/archive and provider-language laws |
| `finality-cohort-law.test.mjs` | REUSE | roster, graduation, idempotence and history replay |
| `work-activated-writer-ratchet.test.mjs` | NEW | source ratchet: canonical paths never write WorkActivated; writer deleted 2026-08-17, ratchet asserts absence (LEGACY-010 closed) |

Focused commands remain one-file `node --test` invocations; the repository
owner runs them after the semantic migration converges.

#### JS semantic boundary

`FinalitySurface` is the single production owner for this vertical slice. Its
opaque `World` is the only representation handle. `project` / `applyEvents`
fold lifecycle and review history; `lifeView`, `archivedLivesView`,
`cohortRoster`, `cohortRosterFromSnapshot`, `graduatedReviewer`,
`classifyEnding`, `admitLabor`, `endingAdmission`, `backgroundOutstanding`, and
`jobProjection` translate the result to JSON-shaped data. Prompt-language
assertions use the separately registered `Mission/Finality/PromptSurface.fs`
owner, which projects narrative parts as arrays and exposes only text/resource
strings. No test constructs an F# DU, unwraps a Fable list/map, or imports a
package-internal dist module.

#### REUSE（留在原处；glory 族按 PROOF-MAP KEEP，多 owner 交叉 SPLIT@cutover）

| 文件 | 锚点 | 本包拥有的断言 | SPLIT@cutover |
|---|---|---|---|
| `requirements/finality/tests/lifecycle.test.mjs` | `WHAT[FINALITY-021] LifeOpened opens the first life`、`WHAT[FINALITY-022] a second life cannot open while one is active`、`WHAT[FINALITY-008] FinalityRequested is rejected while a request is open`、`WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`、`WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`、`WHAT[FINALITY-017] the second suicide is the rest: LifeCompleted archives the Life`、`WHAT[FINALITY-017] isLifeArchived true only after life completed`、`WHAT[FINALITY-026] FinalityUndecided closes the request without a wound record`、`WHAT[FINALITY-011] a revise closes finality without confirming the life`、`WHAT[FINALITY-021] lifecycle facts round trip through ndjson`、`WHAT[FINALITY-019] idle encouragement golden bytes`、`WHAT[FINALITY-026] host undecidable golden bytes`、`WHAT[FINALITY-012] finality rejection renders work record as guidance comments`、`WHAT[FINALITY-020] rejection rendering exposes no mechanism vocabulary`、`WHAT[FINALITY-013] finality three experiences`、`WHAT[FINALITY-022] reawakening golden bytes`、`WHAT[FINALITY-004] first birth golden bytes`、`WHAT[FINALITY-024] activation golden bytes` | lifecycle 事实代数、rejection 关闭、blessing 不结束、rest 归档、undecided 收束、三经验文案、idle、Reawakening | `GLORY_075`→`WHAT[PREFIX-STABILITY-007]`（prefix-stability）、`SURFACE_002`→`WHAT[PROVIDER-LANGUAGE-005]`（provider-language）、`SURFACE_005`（participant-horizon）、`SURFACE_006`（verification-system）、`GLORY_074`（obligation-ledger）、`GLORY_014/019/021`（GARBAGE legacy，迁移窗口后随 absence 政策退役） |
| `requirements/finality/tests/finality-cohort-law.test.mjs` | `rosterOf` / `graduatedReviewer` / enlistment 幂等 / opaque history replay 的 theorem 集 | roster 代数、graduate 推导、durable resolution replay | witness/ConfirmedReviewWitness 的代数断言 → `review-assurance` |
| `requirements/finality/tests/rewrite-consistency.test.mjs` | `WHAT[FINALITY-023] opening rewrite is byte identical across requests`、`WHAT[FINALITY-022] host title request never opens a life`、`WHAT[FINALITY-023] opening rewrite survives a persisted rewritten message`、`WHAT[FINALITY-024] work-time messages are never rewritten` | Opening 改写幂等；host title 请求不开 Life；工作期输入不改写 | ARCH-004 seal 断言 → `prefix-stability` |
| ~~`tests/unit/glory/manager-lifecycle-gate.test.mjs`~~ | `GLORY_018_in_progress_manager_turn_never_activates` | 生产 Activation 缺席（GARBAGE 侧回归） | 已 DELETE（Wave 2a）：仅证明迁移完成（PROOF-MAP 强制删除清单第 6 项） |
| `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` | `TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission`、`TODO-006 T2 prepare succeeds once T1 process review is Concluded` | drain 输入的 ConsumableReview gate | 其余 → obligation-ledger / effect-accounting / host-boundary |

#### e2e（cutover 范围外，记录指针）

- ~~`tests/e2e/cases/manager-unhappy-path.test.mjs`~~：完整自杀/拒绝/继续/祝福/rest 剧本（glory.md proof 第 3 层；stroke 13 last_words 逐字 terminal；cases/ 已随 G4R cutover 删除，剧本并入 Long Stroke）。
- ~~`tests/e2e/cases/finality-cohort-law.test.mjs`~~：GLORY_074/075 record-ready 崩溃 canary（→ review-assurance 交叉；cases/ 已删除，canary 语义由本包 finality-cohort-law 测试承接）。
- `requirements/verification-system/tests/e2e/support/magic-todo-host-canary-plugin.mjs`：canary A/E/G/H 真实 Host 侧（→ host-boundary）。

### 命题 → 落点

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| F-1 001 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-001] only the Manager holds ToolPermission.Finality` | NEW | `node --test requirements/finality/tests/manager-finality-disposition.test.mjs` |
| F-2 002 | F-3..F-7 组合（前置 + drain + 门禁）；总纲由各落点共同承担；`tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-002] finality eligibility is the combination of commitment, request, and experience typing`；REUSE lifecycle `GLORY_010/045/060` | NEW + REUSE | 见 F-1 |
| F-3 003 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-003] an open request resumes the same ToolCallId replay`、`WHAT[FINALITY-003] an open request with no enlisted members is recoverable`、`WHAT[FINALITY-003] a request already in motion waits for the current cohort`（受理失败路径零创建）；REUSE lifecycle `WHAT[FINALITY-008] FinalityRequested is rejected while a request is open` | NEW + REUSE | 见 F-1 |
| F-4 004 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-004] no accepted planComplete=true commitment stays at Planning Table`（无 durable plan commitment 时 `ContinuePlanning`，即使已有 accepted `planComplete=false` planning checkpoint 也不得进入 Finality）；REUSE lifecycle `WHAT[FINALITY-004] first birth golden bytes`；账本侧见 obligation-ledger commitment projection proof | REWRITE + REUSE | 见 F-1 |
| F-5 005 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-005] the rest-path suicide is a drain, not a new cohort`（二次 suicide 仍走 rest 而非新 cohort）；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded`（drain 的 ConsumableReview gate） | NEW + REUSE | 见 F-1 |
| F-6 006 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-006] drain outcomes are two-typed: Revision (REVISE) vs Confirmed (PERFECT)`（REVISE 后不 BeginFinality 之前置）；REUSE lifecycle `WHAT[FINALITY-011] a revise closes finality without confirming the life` | NEW + REUSE | 见 F-1 |
| F-7 007 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-007] no mechanical terminal-todo completeness gate`（有 obligations 的 Life 无机械 completeness 判定）；HOW 记录机械 gate 缺失 | NEW | 见 F-1 |
| F-8 008 | REUSE lifecycle `WHAT[FINALITY-008] FinalityRequested is rejected while a request is open`、`WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`（request 生命周期 durable）；REUSE `tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-008] history replay preserves durable finality facts` | REUSE | lifecycle SPLIT@cutover |
| F-9 009 | REUSE `requirements/finality/tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-009] roster is ungraduated history plus exactly one new`、`WHAT[FINALITY-009] crash reentry reuses already created new slot exactly once`、`WHAT[FINALITY-009] historical enlist order confluent for roster`、`WHAT[FINALITY-009] replay preserves an open finality roster source` | REUSE | finality-cohort-law SPLIT@cutover |
| F-10 010 | REUSE `requirements/finality/tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-010] graduated reviewer excluded from roster`（graduate 只由 enlistment + witness 推导） | REUSE | 同上 |
| F-11 011 | REUSE `requirements/finality/tests/lifecycle.test.mjs` `WHAT[FINALITY-011] a revise closes finality without confirming the life`（REVISE 关 cohort 不落 FinalityRejected）；`WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`（rejected 后新 suicide 开新 request） | REUSE | lifecycle SPLIT@cutover |
| F-12 012 | REUSE `requirements/finality/tests/lifecycle.test.mjs` `WHAT[FINALITY-012] finality rejection renders work record as guidance comments`（rejection evidence 渲染）；steer 双轨交付 e2e 见指针（cutover 范围） | REUSE | lifecycle SPLIT@cutover |
| F-13 013 | REUSE `requirements/finality/tests/lifecycle.test.mjs` `WHAT[FINALITY-013] finality three experiences`、`WHAT[FINALITY-012] finality rejection renders work record as guidance comments`、`WHAT[FINALITY-026] host undecidable golden bytes` | REUSE | lifecycle SPLIT@cutover |
| F-14 014 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-014] rejection keeps the same Life and a new suicide begins fresh Finality`（同 Life 继续；BeginFinality）；REUSE lifecycle `WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`（Rejected 永不 blessing） | NEW + REUSE | 见 F-1 |
| F-15 015 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-015] a blessing keeps the enlisted process-review standing: no dispose`（Blessing 不 Dispose process duty）；REUSE lifecycle `WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`（Blessing 不结束 Life）；过程 duty 保留 → obligation-ledger O-20 | NEW + REUSE | 见 F-1 |
| F-16 016 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-016] a blessing leaves the Life open until the second suicide`；REUSE lifecycle `WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`、`tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-016] blessed exactly once: second completion rejected` | NEW + REUSE | 见 F-1 |
| F-17 017 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-017] the second suicide after a blessing is the rest path`（CompleteBlessedLife 分派）；REUSE lifecycle `WHAT[FINALITY-017] isLifeArchived true only after life completed`（归档 + CompletedTerminal）、`WHAT[FINALITY-017] the second suicide is the rest: LifeCompleted archives the Life` | NEW + REUSE | 见 F-1 |
| F-18 018 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-018] an open request owns the Life: Manager labor is deferred`（open request 停放劳动） | NEW | 见 F-1 |
| F-19 019 | REUSE lifecycle `WHAT[FINALITY-019] idle encouragement golden bytes`（鼓励正文）；cross-owner idempotence evidence: `requirements/interaction-authority/tests/idle-continuation-authority.test.mjs`（同 terminal 重放幂等；新的 Manager terminal 即使 Life/plan-commitment condition 不变也再次发送）；open/completed 不发送由 Manager lifecycle 组合断言 | REUSE | `node --test requirements/finality/tests/lifecycle.test.mjs requirements/interaction-authority/tests/idle-continuation-authority.test.mjs` |
| F-20 020 | REUSE `requirements/finality/tests/lifecycle.test.mjs` `WHAT[FINALITY-020] manager surface has no forbidden words`（无隐藏机制词——admission 归 participant-horizon，本包引用其 proof）、`WHAT[FINALITY-020] manager role law does not name foreign tools`、`WHAT[FINALITY-020] rejection rendering exposes no mechanism vocabulary`（rejection 渲染无机制解释） | REUSE | `node --test requirements/finality/tests/lifecycle.test.mjs` |
| F-21 021 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-021] disposition never derives from narrative text`；`tests/work-activated-writer-ratchet.test.mjs` source ratchet: `acceptActivation` / `applyAcceptedActivation` absent, `ensureMigrated` writes only LifeOpened, writer deleted 2026-08-17, ratchet asserts absence (LEGACY-010 closed)；REUSE lifecycle `WHAT[FINALITY-021] lifecycle facts round trip through ndjson`、`WHAT[FINALITY-021] LifeOpened opens the first life` | NEW + REUSE | `node --test requirements/finality/tests/manager-finality-disposition.test.mjs requirements/finality/tests/work-activated-writer-ratchet.test.mjs` |
| F-22 022 | `requirements/finality/tests/life-admission.test.mjs` `WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history` + `WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id`；`requirements/finality/tests/rewrite-consistency.test.mjs` `WHAT[FINALITY-022] active HumanRoot profile does not make another user message a root`、`WHAT[FINALITY-022] host title request never opens a life`；`tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-022] a new Life inherits no blessing/roster/request and starts fresh Finality`；REUSE lifecycle `WHAT[FINALITY-022] a second life cannot open while one is active`、`WHAT[FINALITY-022] reawakening golden bytes` | NEW + REUSE | `node --test requirements/finality/tests/life-admission.test.mjs requirements/finality/tests/rewrite-consistency.test.mjs` |
| F-23 023 | REUSE `requirements/finality/tests/rewrite-consistency.test.mjs` `WHAT[FINALITY-023] opening rewrite is byte identical across requests`、`WHAT[FINALITY-023] opening rewrite survives a persisted rewritten message` | REUSE | rewrite-consistency SPLIT@cutover |
| F-24 024 | `requirements/finality/tests/rewrite-consistency.test.mjs` `WHAT[FINALITY-024] work-time messages are never rewritten`；REUSE lifecycle `WHAT[FINALITY-024] activation golden bytes`（工作期输入不改写 → obligation-ledger O-17/O-25 交叉：Opening 不因工作期输入移动） | NEW + REUSE | 见 F-22 |
| F-25 025 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-025] a completed Life replays as AlreadyCompleted, never restarts`（completed 保持）；REUSE lifecycle `WHAT[FINALITY-021] lifecycle facts round trip through ndjson`（inert decode 回归） | NEW + REUSE | 见 F-1 |
| F-26 026 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-026] a rejected request does not block labor: labor may continue`、`WHAT[FINALITY-026] resolved historical requests do not block labor`（undecided/resolved 不阻塞劳动——LaborMayContinue）；REUSE lifecycle `WHAT[FINALITY-026] FinalityUndecided closes the request without a wound record`、`WHAT[FINALITY-026] host undecidable golden bytes` | NEW + REUSE | 见 F-1 |
| F-27 027 | `tests/finality-background-obligation.test.mjs` `WHAT[FINALITY-027] Manager without journal or handles is never outstanding`、`WHAT[FINALITY-027] Manager with a listable child handle has a join obligation`、hidden-handle and completed-awaiting-join assertions through `FinalitySurface.backgroundOutstanding` | NEW + REUSE | `node --test requirements/finality/tests/finality-background-obligation.test.mjs` |
| F-28 028 | `tests/manager-job-no-resurrection.test.mjs` `WHAT[FINALITY-028] a terminal ManagerJob is not active and does not resume` / `WHAT[FINALITY-028] later progress cannot reopen a terminal ManagerJob` / `WHAT[FINALITY-028] replaying ManagerJobCreated cannot re-enlist a terminal job` / `WHAT[FINALITY-028] an active owned job continues on the same session and worktree`, all through `FinalitySurface.jobProjection` | NEW | `node --test requirements/finality/tests/manager-job-no-resurrection.test.mjs` |

### 覆盖统计

- 命题 28 / 落点 28（FinalitySurface owner；NEW 3 文件；REUSE 3 文件族；GAP 0）。
- 语义入口：`FinalitySurface.fs` 是唯一 Finality JS boundary；无 package-local
  re-export、domain helper 或 Fable representation adapter。
- `lifecycle.test.mjs` 与 `finality-cohort-law.test.mjs` 的 durable assertions
  通过 opaque history replay，不再借测试 journal/fold facade 构造内部 facts。

### semantic anchor id（semantic-anchors.mjs，MECHANISM 逐 ID 归包）

本包声明拥有 `scripts/checks/semantic-anchors.mjs` 中 manager 角色的下列 anchor id
（`ROLE_SEMANTIC_ANCHORS.manager`；机制文件在 cutover 时按此声明标注 owner）：

- `returned-record` —— 返回的记录只通过它所建立的事实改变 mission（FINALITY-012/016：
  rejection/blessing 的 LWR 是 evidence 不是新指令）。
