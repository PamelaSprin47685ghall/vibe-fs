# obligation-ledger — HOW

行为合同见 `WHAT.md`（OBLIGATION-LEDGER-001..026）。本文件只描述实现模型与约束，非 normative。

## 1. 模块地图

| 模块 | 职责 | 命题 |
|---|---|---|
| `src/Wanxiangshu/Domain/MagicTodo.fs` | `TodoWriteInput={planComplete,obligations}`、Obligation wire、单调 commitment 推导、validate/admission 纯函数 | 001-009、016/021 |
| `src/Wanxiangshu/Domain/MagicTodoAdmission.fs` | `admitObligations`：FreshPrepare / IdempotentReplay / 拒绝分型 | 007/008/025 |
| `src/Wanxiangshu/Domain/MagicTodoFacts.fs` | `TodoWritePrepared` / `TodoWriteAccepted` / `DedicatedTodoReviewerEnlisted` / `TodoProcessReviewAssigned` / `TodoReviewConcluded` / `LegacyTodoSeedAdopted` 等事实 | 008/010/012/019 |
| `src/Wanxiangshu/Domain/MagicTodoAfter.fs` | `assignmentDelivery`（dedicated reviewer 首个 checkpoint = AgentOwnerRoot；重试等 XTrace head；后续 = Continuation） | 020/026 |
| `src/Wanxiangshu/Domain/MagicTodoObligationCodec.fs` | obligations wire codec（与 tool.definition 同源） | 002/024 |
| `src/Wanxiangshu/Domain/MagicTodoSurface.fs` | Manager-only guideline / compatibility TodoTable sink 投影 | 003/015/023 |
| `src/Wanxiangshu/Domain/MagicTodoProcessReview.fs` | Rk 派生与 verdict/conclusion 关系 | 012-014 |
| `src/Wanxiangshu/Domain/MagicTodoPrefixEpoch.fs` | desired cutoff 推导（effective committed Accepted 子链） | 021 |
| `src/Wanxiangshu/Application/Reconciliation/MagicTodoMembrane.fs` | before/after hook overlay；Diagnostic.fatal 分型 | 007-009/024-026 |
| `src/Wanxiangshu/Application/Reconciliation/MagicTodoLocality.fs` | `LocalizedToolCall` / `materializeInput`（snapshot 定位与 materialization） | 025 |
| `src/Wanxiangshu/Application/Review/TodoProcessReviewProgram.fs` | ensureReview / ConsumableReview 判定 | 012-014 |
| `src/Wanxiangshu/Application/Review/DedicatedTodoReviewerRuntime.fs` | dedicated reviewer session 续跑 / assignment | 020 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Codec/MagicTodoHostCodec.fs` | provider schema / 富化 result 渲染 | 024/026 |
| `src/Wanxiangshu/Journal/MagicTodoProjection.fs` | fold：`CurrentObligationsRef = Some(cp.ProposedTodoRef, cp.ProposedTodoDigest)`（唯一 Current writer）、Checkpoints、conclusion gate | 010-015 |
| `src/Wanxiangshu/Journal/MagicTodoFactCodec.fs` | MagicTodo 事实的 typed NDJSON codec | 008/018 |

## 2. 关键算法（非 normative 摘要）

### Accepted supersession（OBLIGATION-LEDGER-010）

```text
Prepared(Tk)  freezes Base=C(k-1), Submitted=Pk; Current unchanged
Accepted(Tk)  => CurrentObligations := Pk immediately (fold)
Review(k)     => verdict/report only; never rewrites Current
```

`MagicTodoProjection.foldAccepted` 是唯一 Current writer；`TodoReviewConcluded` 只封口 review
义务，不写 Current（本包 WHAT OBLIGATION-LEDGER-010..014）。

### before / deferred prepare（OBLIGATION-LEDGER-025）

before 同步阶段只 decode + 内存 compatibility 投影，启动 deferred prepare 后立即返回；
deferred prepare 等 `pending + {}` materialize，校验 materialized canonical == 捕获值后
durable `TodoWritePrepared`，其中原样冻结 submitted `planComplete`。ephemeral JS bridge
（process-local Map + hidden Symbol）只搬运本次 effect shell 的 ephemeral 数据，**不是** durable 状态；
crash recovery 只重放 Prepared/Accepted，不读取 bridge。

### after / recovery → Accepted（OBLIGATION-LEDGER-026）

physical success 双路径（live after / recovery ToolPart completed）收敛同一
`TodoWriteId + input digest + output digest`；ensure Accepted（幂等，`PreparedFactRef` 必须指向
真实 append 返回的 EventId）→ ensure Dedicated → ensureReview。禁止先 reviewer 后 Accepted。

### 失败分型（OBLIGATION-LEDGER-009）

`MagicTodoMembrane.fs` 中 `Diagnostic.fatal "magic-todo-infrastructure-failed"` 覆盖
snapshot/locality/materialization/ConsumableReview 等待失败；schema decode / deferred syntax
Error 允许 `invalidOp`（provider 红字）。REVISE 是正常业务结果，走富化 tool result。

### commitment latch / desired cutoff（OBLIGATION-LEDGER-016/021）

`TodoWritePrepared.PlanCompleteDeclared` 保存 provider 原始 bool；projection 从 Accepted checkpoints 找到
第一个 `true`，不另写 phase fact。`effectivePlanComplete = historicalTrue || submittedTrue`。
Pre-T1 false checkpoints 不派生 TodoCheckpoint rebase；从 T1 起的 effective-true Accepted 子链按
`desiredCutoff(current) = Before(previous committed checkpoint tool-call)` 推导。下一 attempt seal 前再由
既有 `PrefixRebaseCommitted(EvidenceKind=TodoCheckpoint)` 原子提交。

## 3. 历史与弃权

### GARBAGE —— 不进入 WHAT 的历史沉积

| 内容 | 裁决理由 |
|---|---|
| `settled` / `proposed` / `semanticMerge` 三态 + status min-merge | GrandRewrite clean break 删除；reviewer 不拥有账本写权（TODO-005）。源码 production path 不得出现（静态 proof 断言，PROOF O-11） |
| provider `kind` / `id` / `status` / `priority` / `reviewing` 冷状态 | 删除；wire 只有 top-level `planComplete` + `{name,work}` obligations。`planComplete` 是单调业务承诺，不是 item progress state |
| `TodoPlanningStage` / `ReviewStage` / `AwaitingReview` bool / `TodoStage` PC | 程序计数器；恢复只从 durable facts（TODO-012） |
| 生产 `ManagerWorkActivation` / `WorkActivated` 资格门 / `PlanningTail` / Birth/Labor floor | planning→Activation 两阶段删除；`WorkActivated` 仅 inert legacy decode（TODO-001/GLORY-018..021）。不在本包 WHAT 中写成命题 |
| 第二套 PrefixEpoch / 平行 LWR renderer | 单一 SSOT（TODO-009/012） |
| Host 按 `plan` / `survey` / `placeholder` 等关键词分类 planning work | 语义改由显式 `planComplete` 表达；Host 只校验 bool/call-shape，不猜自然语言 |

### HOW —— 当前实现形状，非永久需求

| 内容 | 说明 |
|---|---|
| Host TodoTable compatibility sink（`content=name: work` / `status=in_progress` / `priority=medium`；reviewing 降级 in_progress） | **compatibility 不写成永久需求**。它是当前 Host V1 的兼容 UI 投影（HOST-023，canary D/I 冻结）；未来 sink 可整体替换，canonical 语义不变。sink 永不反推 canonical（OBLIGATION-LEDGER-015 是永久命题；sink 字段形态是 HOW） |
| `todowrite` schema / `planComplete` / `name`/`work` 字段名 / T1 文案具体 wording | 当前 authoring surface；`provider-language` 拥有本地化字节，本包拥有 commitment 语义 |
| `ReviewFrontier` / `ReviewWorkStartCursor` 的具体 cursor 算法 | 与 `semantic-trace`（cursor 表示）、`work-record`（LWR 有界）、`review-assurance`（assignment 范围）交界；本包只引用 |
| bridge / `TodoWritePrepared` 的具体字段 | 当前事实形态；语义合同以 WHAT 为准 |
| `MagicTodoManagerGuideline` / Planning Table / T1 revelation 的逐字文案 | `provider-language` / SURFACE-004 拥有冻结字节；本包只拥有账本语义（OBLIGATION-LEDGER-023/016） |

### 与邻域包的交界（引用不复制）

- `finality`：drain 执行（零 checkpoint fail closed、REVISE 回灌、PERFECT 进 Finality 前置）与
  blessed/rest 经验 → 见 `requirements/finality/WHAT.md`。
- `review-assurance`：ConsumableReview 的 record-ready / 同 snapshot 物化 → 引用其命题。
- `work-record`：ProcessReviewLWR 物化、三段标题、coverage 分型 → 引用其命题。
- `prefix-stability`：PrefixRebaseCommitted / ActivePrefixEpoch → 引用其命题。
- `effect-accounting`：physical success 的 Requested/Accepted 分型 → 引用其命题。
