# obligation-ledger — HOW

行为合同见 `WHAT.md`（OBLIGATION-LEDGER-001..026）。本文件只描述实现模型与约束，非 normative。

## 1. 目标模块地图（本次大修）

| 层 | 模块 | 职责 | 命题 |
|---|---|---|---|
| Domain | `src/Wanxiangshu/Domain/MagicTodo.fs` | Obligation / provider input 值对象、纯 validation、identity、纯 decision；不得读 Journal/Host | 001-009/016 |
| Domain | `src/Wanxiangshu/Domain/MagicTodoFacts.fs` | 只定义已发生事实：Prepared/Accepted/reviewer/legacy seed；不定义 Stage/NextAction | 008/010/012/018/019 |
| Domain | `src/Wanxiangshu/Domain/MagicTodoObligationCodec.fs` | provider/blob wire codec；外部协议解码，不解释业务流程 | 002/024 |
| Journal | `src/Wanxiangshu/Mission/Obligation/Todo/Projection.fs` | **O(1) 增量积分**：Current、pending review locator、first plan commitment、latest/previous committed checkpoint、dedicated reviewer locator；append 后单步 fold | 010-021 |
| Journal | `src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoFactCodec.fs` | typed fact codec；boot 时可从 event history 重建 projection，但普通业务查询不得 replay | 008/018 |
| Application | `src/Wanxiangshu/Application/Manager/ObligationLedgerWorkflow.fs` | **Direct F# `task {}` CE**：读取当前 projection facts → `let!/match` → 调用具名 capabilities → append facts；恢复调用同一入口 | 007-014/018/022/025/026 |
| Application | `src/Wanxiangshu/Application/Review/TodoProcessReviewProgram.fs` | record-ready / ConsumableReview CE；只读当前投影与 reviewer evidence | 012-014 |
| Application | `src/Wanxiangshu/Application/Review/DedicatedTodoReviewerRuntime.fs` | dedicated reviewer physical session 的复用/恢复；不拥有 ledger stage | 020 |
| Infrastructure | `src/Wanxiangshu/Infrastructure/OpenCode/**` | Host hook / schema / JS compatibility effect shell：decode、materialize、调用 Application workflow、回写 result；**不拥有 business sequencing** | 024-026 |

现有 `Mission/Obligation/Todo/MagicTodoMembrane.fs` 同时混合 Host adapter、Journal I/O、admission 与业务顺序；本次重构目标是把业务 CE 提升到 `Application/Manager/ObligationLedgerWorkflow.fs`，让 Infrastructure hook 只做 effect-shell 适配。若文件名保留，也必须缩退到薄 adapter，不得继续成为第二个 workflow owner。

## 2. Direct CE 与增量 projection 形状（非 normative 摘要）

### Direct CE（STRUCTURED-WORKFLOW-001/009 交叉）

目标调用形状直接使用宿主语言控制流，不构造 Command/Reply AST，也不保存执行位置：

```fsharp
let submitCheckpoint ports input = task {
    let! observed = ports.ReadCurrentFacts input.ManagerSessionId
    match decideAdmission observed input with
    | Rejected reason -> return Rejected reason
    | Replay receipt -> return Replay receipt
    | Fresh plan ->
        let! prepared = ports.PrepareDurably plan
        let! physical = ports.ExecuteHostSink prepared
        let! accepted = ports.AcceptDurably prepared physical
        do! ports.EnsureReview accepted
        return Accepted accepted
}
```

崩溃恢复不恢复到 `Prepare/Execute/Accept` 的某个 stage；而是 Boot Fold 得到当前事实后再次调用普通入口。Prepared/Accepted identity 令入口自然走 Replay/repair 分支。

### O(1) commitment / lag-1 projection（DURABLE-EVENTS-013 交叉）

每个 Life 的 projection 至少维护以下**已发生事实的 locator**，每次 fold 只基于旧 projection + 当前 event O(1) 更新：

```text
CurrentObligationsRef
FirstAcceptedCheckpoint?         // once-set: first Accepted; reviewer first-delivery identity
LatestAcceptedCheckpoint?        // current review / previous-review locator
PendingReviewCheckpoint?
LatestConcludedManagerReviewFrontier? // dedicated reviewer 已消费的 Manager LWR exclusive end
FirstPlanCommitment?             // once-set: first Accepted whose Prepared declared true
LatestCommittedCheckpoint?      // after FirstPlanCommitment, every later Accepted is effective committed
PreviousCommittedCheckpoint?    // lag-1 cutoff without scanning history
DedicatedReviewer?
ReviewerLifeBySession            // reviewer-session -> ManagerLifeId reverse locator
```

`isPlanCommitted`、T1 Opening anchor、Finality admission、review delivery identity、latest ConsumableReview、desired committed cutoff 均只读这些 locator；process reviewer 反向找所属 Life 直接读 `ReviewerLifeBySession`，不得 `Map.tryPick` 全部 `ByLife`。`AcceptedOrder` / `AcceptedIds` 不再需要承担 production query；本次大修优先删除它们。若未来仅为审计重新引入历史序列，也不得成为热路径业务事实源，更不得每次 `find/filter/rev` 重建 commitment 子链。


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
deferred prepare 读取 full Host snapshot，先用 `SessionSnapshotPort.locateToolCall` 找到当前 provider run，
再用 `XTraceCapture.captureSessionMessages` 只同步该 run **之前**的完整 transcript prefix，最后在 fresh
projection 上 localize 当前 call；这样补齐漏 capture 的历史，又不会把当前 `pending + {}` tool stub 写进
durable XTrace。当前 `pending + {}` 只允许由本次 before live args materialize。
同 message 其它 `pending + {}`/null ToolPart 只是 Host construction stub，不计作第二个 semantic todowrite；
另一个已有真实 input/terminal state 的 sibling 仍由 O-7 全拒。随后校验 materialized canonical == 捕获值并
durable `TodoWritePrepared`，其中原样冻结 submitted `planComplete`。ephemeral JS bridge
（process-local Map + hidden Symbol）只搬运本次 effect shell 的 ephemeral 数据，**不是** durable 状态；
crash recovery 只重放 Prepared/Accepted，不读取 bridge。

### after / recovery → Accepted（OBLIGATION-LEDGER-026）

physical success 双路径（live after / recovery ToolPart completed）收敛同一
`TodoWriteId + input digest + output digest`；ensure Accepted（幂等，`PreparedFactRef` 必须指向
真实 append 返回的 EventId）→ ensure Dedicated → ensureReview。禁止先 reviewer 后 Accepted。

Dedicated reviewer 的 manager-side LWR 起点也是 O(1) projection：首次 assignment 使用 structural
`WorkRecordStart`；`TodoReviewConcluded` fold 把当前 checkpoint `ReviewFrontier` 写入
`LatestConcludedManagerReviewFrontier`，下一 assignment 直接物化
`[LatestConcludedManagerReviewFrontier, current ReviewFrontier)`。不得扫描历史 checkpoint 找“上一份”。

### 失败分型（OBLIGATION-LEDGER-009）

`MagicTodoMembrane.fs` 中 `Diagnostic.fatal "magic-todo-infrastructure-failed"` 覆盖
snapshot/locality/materialization/ConsumableReview 等待失败；schema decode / deferred syntax
Error 允许 `invalidOp`（provider 红字）。REVISE 是正常业务结果，走富化 tool result。

### commitment latch / desired cutoff（OBLIGATION-LEDGER-016/021）

`TodoWritePrepared.PlanCompleteDeclared` 保存 provider 原始 bool；旧 payload 缺字段按 `true` migration decode（旧协议本身即 complete-plan contract）。`foldAccepted` 在 `FirstPlanCommitment=None && declared=true` 时 once-set T1 locator。之后每次 Accepted O(1) 推进 `PreviousCommittedCheckpoint/LatestCommittedCheckpoint`，所以 provider 后续声明 false 也无需扫描历史即可得到 effective true。Pre-T1 false checkpoints 不进入 committed lag-1 cutoff；T1 自身无 prior；之后 cutoff 直接读 `PreviousCommittedCheckpoint`。

## 3. 历史与弃权

### GARBAGE —— 不进入 WHAT 的历史沉积

| 内容 | 裁决理由 |
|---|---|
| `settled` / `proposed` / `semanticMerge` 三态 + status min-merge | GrandRewrite clean break 删除；reviewer 不拥有账本写权（TODO-005）。源码 production path 不得出现（静态 proof 断言，PROOF O-11） |
| provider `kind` / `id` / `status` / `priority` 冷状态 | 删除；wire 只有 top-level `planComplete` + `workingOn` + `{name,work}` obligations。`planComplete` 是单调业务承诺；`workingOn` 是单一当前焦点指针，不是 item status state |
| `TodoPlanningStage` / `ReviewStage` / `AwaitingReview` bool / `TodoStage` PC | 程序计数器；恢复只从 durable facts（TODO-012） |
| 生产 Activation 资格门 / `WorkActivated` 资格门 / `PlanningTail` / Birth/Labor floor | planning→Activation 两阶段删除；`acceptActivation` / `applyAcceptedActivation` / wire Activation 检测已删除（无 creditor）；`WorkActivated` 仅 inert legacy decode + `appendLegacyMigrationWorkActivatedCompat` bounded-compat writer（LEGACY-010，e2e long-stroke creditor）（TODO-001/GLORY-018..021）。不在本包 WHAT 中写成命题 |
| 第二套 PrefixEpoch / 平行 LWR renderer | 单一 SSOT（TODO-009/012） |
| Host 按 `plan` / `survey` / `placeholder` 等关键词分类 planning work | 语义改由显式 `planComplete` 表达；Host 只校验 bool/call-shape，不猜自然语言 |

### HOW —— 目标实现形状，非永久需求

| 内容 | 说明 |
|---|---|
| Host TodoTable compatibility sink（`content=name: work` / `status=(name=workingOn ? in_progress : pending)` / `priority=medium`） | **compatibility 不写成永久需求**。它是当前 Host V1 的兼容 UI 投影；未来 sink 可整体替换，canonical obligation 语义不变。sink 永不反推 canonical（OBLIGATION-LEDGER-015 是永久命题；sink 字段形态是 HOW） |
| `todowrite` schema / `planComplete` / `name`/`work` 字段名 / T1 文案具体 wording | 当前 authoring surface；`provider-language` 拥有本地化字节，本包拥有 commitment 语义 |
| `ReviewFrontier` / `ReviewWorkStartCursor` 的具体 cursor 算法 | 与 `semantic-trace`（cursor 表示）、`work-record`（LWR 有界）、`review-assurance`（assignment 范围）交界；本包只引用 |
| bridge / `TodoWritePrepared` 的具体字段 | 当前事实形态；bridge 只可搬运一次 Host effect-shell 的 ephemeral 数据，不得保存业务 stage；语义合同以 WHAT 为准 |
| `MagicTodoManagerGuideline` / Planning Table / T1 revelation 的逐字文案 | `provider-language` / SURFACE-004 拥有冻结字节；本包只拥有账本语义（OBLIGATION-LEDGER-023/016） |

### 与邻域包的交界（引用不复制）

- `finality`：drain 执行（零 checkpoint fail closed、REVISE 回灌、PERFECT 进 Finality 前置）与
  blessed/rest 经验 → 见 `requirements/finality/WHAT.md`。
- `review-assurance`：ConsumableReview 的 record-ready / 同 snapshot 物化 → 引用其命题。
- `work-record`：ProcessReviewLWR 物化、三段标题、coverage 分型 → 引用其命题。
- `prefix-stability`：PrefixRebaseCommitted / ActivePrefixEpoch → 引用其命题。
- `effect-accounting`：physical success 的 Requested/Accepted 分型 → 引用其命题。
- `structured-workflow`：Direct CE、无第二 runtime、无 durable program counter、Boot Fold 后重入普通 workflow → 本包必须消费这些法则，不复制新的控制抽象。
- `durable-events`：普通查询 O(1) projection、append/publish 成功后才 fold → commitment/current/pending-review locator 必须增量维护。
