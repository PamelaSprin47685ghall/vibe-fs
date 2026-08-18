# obligation-ledger — HOW

行为合同见 `WHAT.md`（OBLIGATION-LEDGER-001..027）。本文件只描述实现模型与约束，非 normative。

## 1. 目标模块地图（本次大修）

| 层 | 模块 | 职责 | 命题 |
|---|---|---|---|
| Domain | `src/Wanxiangshu/Mission/Obligation/Todo/Model.fs` | Obligation / horizon / provider input 值对象、纯 validation、identity、纯 decision；不得读 Journal/Host | 001-009/016/027 |
| Domain | `src/Wanxiangshu/Mission/Obligation/Todo/Facts.fs` | 只定义已发生事实：Prepared/Accepted/reviewer/legacy seed；不定义 Stage/NextAction | 008/010/012/018/019 |
| Domain | `src/Wanxiangshu/Mission/Obligation/Todo/ObligationCodec.fs` | provider/blob wire codec；新 provider wire 严格要求 horizon；历史 durable blob/input migration 只在 codec ingress 收敛成 typed horizon，不解释业务流程 | 002/024/027 |
| Journal | `src/Wanxiangshu/Mission/Obligation/Todo/Projection.fs` | **O(1) 增量积分**：Current、pending review locator、first plan commitment、latest/previous committed checkpoint、dedicated reviewer locator；append 后单步 fold | 010-021 |
| Journal | `src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoFactCodec.fs` | typed fact codec；boot 时可从 event history 重建 projection，但普通业务查询不得 replay | 008/018 |
| Application | `src/Wanxiangshu/Mission/Obligation/LedgerWorkflow.fs` | **Direct F# `task {}` CE**：读取当前 projection facts → `let!/match` → 调用具名 capabilities → append facts；恢复调用同一入口 | 007-014/018/022/025/026 |
| Application | `src/Wanxiangshu/Mission/Obligation/Todo/ProcessReview.fs` | record-ready / ConsumableReview CE；只读当前投影与 reviewer evidence | 012-014 |
| Application | `src/Wanxiangshu/Mission/Review/DedicatedTodoRuntime.fs` | dedicated reviewer physical session 的复用/恢复；不拥有 ledger stage | 020 |
| Infrastructure | `src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/**` | Host hook / schema / JS compatibility effect shell：decode、materialize、调用 Application workflow、回写 result；**不拥有 business sequencing** | 024-026 |

现有 `Mission/Obligation/Todo/MagicTodoMembrane.fs` 同时混合 Host adapter、Journal I/O、admission 与业务顺序；本次重构目标是把业务 CE 提升到 `Mission/Obligation/LedgerWorkflow.fs`，让 OpenCode effect shell 只做适配。若文件名保留，也必须缩退到薄 adapter，不得继续成为第二个 workflow owner。

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

### horizon progressive elaboration（OBLIGATION-LEDGER-027）

`ObligationHorizon = Near | Mid | Far` 是封闭 ADT，canonical wire 固定编码为 `near|mid|far`。它是 owed-work
事实的一部分，因此进入 blob/digest/review wire；但 Projection 不为 horizon 建 phase/status machine，也不据此
分支 workflow。Host provider decode 不把 horizon 当 admission gate：exact `workingOn` 可命中任意 horizon；
拼写未命中时在全部 obligations 中做 Levenshtein 归一。非空 account 没有 Near 也可 accepted；规划分辨率是否
合理交给 process review / provider guidance 判断。空 account 仍归一为 `""`。

新 tool definition 严格 require `{name,horizon,work}`。为了重放升级前已 durable 的 v4 bytes，durable blob 与
snapshot input codec 允许缺 horizon 的历史 obligation 在 ingress 全部映射为 `Near`：旧协议没有记录距离，
保留其原先“平面、同分辨率”事实比猜测 Mid/Far 更诚实。下一份新 account 再由 Manager 按当前前沿重新投影。
这只是旧 bytes → 新 typed world 的单向 migration；新 Host provider decode 不接受缺失 horizon，也不会重新写出
旧形状。新写入使用 `magic-todo.v5`。

provider prose 冻结一条 authoring law：complete coverage ≠ uniform decomposition。Near 是 execution-sized，
Mid 是 next-outcome-sized，Far 是 coverage-sized；靠近前沿时下一份完整 account 以更细 obligations 替换粗 parent，
不保留 parent+children 双重计债。

## 3. 历史与弃权

### GARBAGE —— 不进入 WHAT 的历史沉积

| 内容 | 裁决理由 |
|---|---|
| `settled` / `proposed` / `semanticMerge` 三态 + status min-merge | GrandRewrite clean break 删除；reviewer 不拥有账本写权（TODO-005）。源码 production path 不得出现（静态 proof 断言，PROOF O-11） |
| provider `kind` / `id` / `status` / `priority` 冷状态 | 删除；wire 只有 top-level `planComplete` + `workingOn` + `{name,horizon,work}` obligations。`planComplete` 是单调业务承诺；`workingOn` 是单一当前焦点指针；`horizon` 是相对规划分辨率，二者都不是 item status state |
| `TodoPlanningStage` / `ReviewStage` / `AwaitingReview` bool / `TodoStage` PC | 程序计数器；恢复只从 durable facts（TODO-012） |
| 生产 Activation 资格门 / `WorkActivated` 资格门 / `PlanningTail` / Birth/Labor floor | planning→Activation 两阶段删除；`acceptActivation` / `applyAcceptedActivation` / wire Activation 检测已删除（无 creditor）；`WorkActivated` 仅 inert legacy decode，writer 已删除 2026-08-17（LEGACY-010 closed），e2e long-stroke 已解耦（TODO-001/GLORY-018..021）。不在本包 WHAT 中写成命题 |
| 第二套 PrefixEpoch / 平行 LWR renderer | 单一 SSOT（TODO-009/012） |
| Host 按 `plan` / `survey` / `placeholder` 等关键词分类 planning work | 语义改由显式 `planComplete` 表达；Host 只校验 bool/call-shape，不猜自然语言 |

### HOW —— 目标实现形状，非永久需求

| 内容 | 说明 |
|---|---|
| provider `workingOn` decode-time canonicalization | exact obligation name 优先；未命中时按 Levenshtein 编辑距离选最近 name，并列按 provider obligations 原顺序取第一个；空 account 归一为 `""`。这是 authoring 边界容错，进入 durable account 后仍保持 exact focus invariant |
| provider `horizon` / progressive elaboration | current surface 为 `near|mid|far` enum；只度量相对 `workingOn` 前沿的展开分辨率。新 provider input 严格 require；旧 durable v4 bytes 只在 codec ingress migration。不得进入 Projection workflow control |
| Host TodoTable compatibility sink（`content=name: work` / `status=(name=workingOn ? in_progress : pending)` / `priority=medium`） | **compatibility 不写成永久需求**。它是当前 Host V1 的兼容 UI 投影；未来 sink 可整体替换，canonical obligation 语义不变。sink 永不反推 canonical（OBLIGATION-LEDGER-015 是永久命题；sink 字段形态是 HOW） |
| `todowrite` schema / `planComplete` / `name`/`horizon`/`work` 字段名 / T1 文案具体 wording | 当前 authoring surface；`provider-language` 拥有本地化字节，本包拥有 commitment + perspective 语义 |
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

## DEPENDS ON

- `durable-events`：`TodoWritePrepared/Accepted` 事实的不可变、原子 append、先 commit 后 fold、O(1) projection 查询，是 canonical account + commitment 恢复的 substrate。
- `effect-accounting`：physical success 的 Requested/Accepted 双路径分型决定 `TodoWriteAccepted` 何时可落盘（live/recovery 收敛）。
- `semantic-trace`：ReviewFrontier / Opening 区间由 XTrace cursor 界定；过程 review 需要原始语义历史可定位。

## 边界（DOES NOT OWN）

- Reviewer judgement meaning（PERFECT/REVISE 的语义）→ `review-judgement`
- review evidence / witness / seal 的可消费性 → `review-assurance`
- Finality 接受资格与 cohort / blessed / rest → `finality`
- Host TodoTable / UI sink 的具体实现 → HOW（compatibility 不是永久需求）
- 当前 `todowrite` schema、`planComplete`/`name`/`horizon`/`work` 字段名、T1 文案具体 wording → HOW / `provider-projection` / `provider-language`
- Direct CE / 禁止第二 runtime / 恢复重入普通 workflow 的一般法则 → `structured-workflow`
- Manager Persona / Role Law → `participant-identity` / `office-capability`
- desired cutoff 的 PrefixEpoch seal 机制 → `prefix-stability`
- LWR 物化与三段标题 → `work-record`
- 隐藏 reviewer 的可见性 admission → `participant-horizon`
- infra fatal 的进程级处理 → `crash-reconciliation`

## 验证与测试落点

行为合同：`WHAT.md`（OBLIGATION-LEDGER-001..027）。实现模型：`HOW.md`。

### 测试资产

#### 本包 tests/（`requirements/obligation-ledger/tests/`）

| 文件 | 来源 | 类型 | 断言数 |
|---|---|---|---|
| `magic-todo.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo.test.mjs` | domain 纯函数 | 8 |
| `magic-todo-after.test.mjs` | MOVE + REWRITE | domain 纯函数 + dedicated work-unit static contract | 6 |
| `magic-todo-projection.test.mjs` | MOVE + REWRITE | O(1) fold 代数 + commitment/reviewer reverse locator | 16 |
| `magic-todo-event-store.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs` | EventStore 恢复 | 1 |
| `magic-todo-provider-boundary.test.mjs` | MOVE + REWRITE | static（provider surface / planComplete relation） | 10 |
| `magic-todo-host-codec.test.mjs` | MOVE + REWRITE | codec / definition | 3 |
| `opening-floor.test.mjs` | MOVE + REWRITE | T1 / Opening floor | 6 |
| `prefix-epoch-cutoff.test.mjs` | NEW + REWRITE | committed lag-1 locator | 2 |
| `obligation-ledger-workflow-contract.test.mjs` | NEW | Direct CE / O(1) projection / 无第二 runtime 静态合同 | 3 |

本目录顶层实际为 12 个 test 文件，当前 runner **92/92 GREEN**；其中还包括 `lifecycle-opening.test.mjs`、`magic-todo-host-canaries.test.mjs` 与 `magic-todo-membrane.test.mjs`。

#### REUSE（留在原处；跨包 SPLIT@cutover）

| 文件 | 锚点 | 本包拥有的断言 | SPLIT@cutover |
|---|---|---|---|
| `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` | `WHAT[OBLIGATION-LEDGER-0xx]*` 18 个 test（002/009/010/011/012/013/014/016/025/026） | admission、materialization、lag-1 等待、REVISE 不回滚、structured rejection 分型、富化 result | physical success 分型 → `effect-accounting`；snapshot 定位 → `host-boundary` |
| `tests/unit/plugin/magic-todo-host-canaries.test.mjs` | `MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged`、`MAGIC_TODO_CANARY_B_definition_jsonSchema_ternary_keeps_schema_when_both_replaced`、`MAGIC_TODO_CANARY_C_obligations_project_to_original_v1_decoder_shape`、`MAGIC_TODO_CANARY_C_projection_helper_mutates_original_args_in_place`、`MAGIC_TODO_CANARY_F_after_does_not_run_when_executor_throws`、`MAGIC_TODO_CANARY_F_after_runs_when_executor_succeeds` | definition 三处同步；compatibility 投影；physical-success 才 Accepted | canary A′/H 定位与 carrier → `host-boundary` |
| `requirements/finality/tests/lifecycle.test.mjs` | `GLORY_074_t1_revelation_hook`、`GLORY_010_LifeOpened_opens_the_first_life`、`GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once` | T1 revelation 属 Opening；WorkActivated inert decode | 其余 finality / participant-horizon / provider-language 断言归各自包；本包已拆分 `lifecycle-opening.test.mjs`（`WHAT[OBLIGATION-LEDGER-016/017]`） |

### 命题 → 落点

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| O-1 001 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-001] canonical obligation wire carries no provider-visible cold state`；`tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-003] clean break removes the legacy todo ontology...` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| O-2 002 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-002] canonical obligation wire is exactly name/horizon/work with stable digest input`；`tests/magic-todo-host-codec.test.mjs` `WHAT[OBLIGATION-LEDGER-002] decodes required planComplete, workingOn, and obligations` | MOVE + REWRITE | `node --test requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` |
| O-3 003 | `tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-003] clean break...`；`tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-001]` wire doesNotMatch | MOVE | 见 O-1 |
| O-4 004 | `tests/magic-todo-provider-boundary.test.mjs`：Pre-T1 `planComplete=false` 明确允许 concrete planning work；effective true 后才启用 completion-counterfactual mission-debt 纪律；Host 无 planning 关键词分类 | REWRITE | `node --test requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| O-5 005 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-005] empty placeholders remain invalid while concrete planning tasks are valid`：placeholder/TBD 等无 concrete owed work 的空槽位在 false/true 两侧都非法；具体 planning task 在 false 侧合法 | REWRITE | 见 O-4 |
| O-6 006 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-006] rejects blank and duplicate obligation names as call syntax` | MOVE | 见 O-1 |
| O-7 007 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-007] rejects different todowrite calls in one assistant message as syntax/protocol error`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs` `TODO-004 pending empty todowrite stubs are not semantic sibling calls` + `TODO-004 a populated sibling todowrite remains a real protocol sibling`（Host construction stub 不升级为第二份账，真实 sibling 仍保留 multi-call 证据） | MOVE + REUSE | 见 O-1；locality 交叉见 semantic-trace |
| O-8 008 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-008] pure replay identity checker detects corruption...`；`tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-008] rejects Accepted when it names another Prepared envelope`、`WHAT[OBLIGATION-LEDGER-008] rejects a replay whose frozen prepared identity differs` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| O-9 009 | `tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-009] failure triage keeps red for syntax and kills OpenCode on infrastructure faults`；REUSE `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` `WHAT[OBLIGATION-LEDGER-009] duplicate obligation name is the provider-red class`、`WHAT[OBLIGATION-LEDGER-009] prepare succeeds without review runtime; accept flags review as required downstream`；REUSE canaries `WHAT[OBLIGATION-LEDGER-026] after does not run when executor throws` | MOVE + REUSE | 见 O-4；membrane 见 SPLIT@cutover |
| O-10 010 | `tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-010] Accepted supersedes Current immediately`；REUSE membrane `WHAT[OBLIGATION-LEDGER-010] T1 accept makes the proposed account Current immediately, before any review`、`WHAT[OBLIGATION-LEDGER-010] T2 accepted account supersedes CurrentObligations`、`WHAT[OBLIGATION-LEDGER-010] provider wording says Accepted becomes Current without reviewer settlement`；`tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-010] fresh admission freezes Base and Submitted without a merge preview`（Prepared 不改 Current；无 RevisePreview） | MOVE + REUSE | 见 O-8 |
| O-11 011 | `tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-011] REVISE conclusion cannot roll back CurrentObligations`；REUSE membrane `WHAT[OBLIGATION-LEDGER-011] REVISE is feedback only: next checkpoint sees the report and Current never rolls back`；`tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-011] production checkpoint path has no reviewer settlement owner` | MOVE + REUSE | 见 O-8 / O-4 |
| O-12 012 | `tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-012] rejects a conclusion with no matching assignment`；`tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-012] replays an identical obligation checkpoint even while its review is outstanding`（replay 不新增 review）；REUSE membrane `WHAT[OBLIGATION-LEDGER-012] T1 accept derives the process-review duties (SSOT = TodoWriteAccepted)`；`tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-012] process reviewer is told the effective planning-vs-mission relation` | MOVE + REUSE | 见 O-8 |
| O-13 013 | `tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-013] rejects a new prepare until the preceding review concludes`、`WHAT[OBLIGATION-LEDGER-013] treats an exact durable conclusion replay as idempotent`；REUSE membrane `WHAT[OBLIGATION-LEDGER-013] T2 prepare while R1 is outstanding is a legal lag-1 wait, not a fail-closed Admission` | MOVE + REUSE | 见 O-8 |
| O-14 014 | `tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-014] legacy conclusion locator remains replayable but is not a Current writer`（VerdictKnown→Concluded 两段式；不写 Current）；REUSE membrane `WHAT[OBLIGATION-LEDGER-014] T2 prepare is gated on a consumable TodoReviewConcluded, not on a mere verdict`（ConsumableReview gate） | MOVE + REUSE | 见 O-8 |
| O-15 015 | `tests/magic-todo-host-codec.test.mjs` `WHAT[OBLIGATION-LEDGER-015] workingOn projects to in_progress and every other obligation to pending`、`WHAT[OBLIGATION-LEDGER-015] projects obligations into a non-enumerable V1 compatibility view`；REUSE canaries `WHAT[OBLIGATION-LEDGER-015] obligations project to the original V1 decoder shape`、`WHAT[OBLIGATION-LEDGER-015] projection helper mutates original args in place` | MOVE + REUSE | 见 O-2 |
| O-16 016 | `tests/opening-floor.test.mjs`：accepted false checkpoints 仍保持 dynamic Opening；第一次 accepted true 的 call/result 才是 constitutive T1；`tests/magic-todo-projection.test.mjs`：FirstPlanCommitment once-set、true 后 raw false effective 仍 true；REUSE membrane `WHAT[OBLIGATION-LEDGER-016] first accepted planComplete=false stays at the Planning Table without commitment`；REUSE lifecycle `WHAT[OBLIGATION-LEDGER-016] T1 revelation hook wraps the accepted result with entrustment`；provider-boundary：false/true/不可回退文案 | REWRITE + REUSE | `node --test requirements/obligation-ledger/tests/opening-floor.test.mjs requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| O-17 017 | `tests/opening-floor.test.mjs` `WHAT[OBLIGATION-LEDGER-017] Pre-T1: effectiveOpeningFloor tracks XTrace head...`、`WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor`、`WHAT[OBLIGATION-LEDGER-017] static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs`；`tests/lifecycle-opening.test.mjs` `WHAT[OBLIGATION-LEDGER-017] WorkActivated is an inert legacy fact...`；`tests/magic-todo-membrane.test.mjs` `WHAT[OBLIGATION-LEDGER-017] zero-work planComplete=true with empty obligations is a valid T1 commitment` | MOVE + REUSE + ADD | 见 O-16 |
| O-18 018 | `WHAT[OBLIGATION-LEDGER-018]` `tests/obligation-ledger-workflow-contract.test.mjs`：Application workflow 是直接 CE（`taskResult {}`）/ `let!` / `match`，无 Command/Reply/Interpreter/Stage；生产热路径不得通过 Accepted history 或 `ByLife |> Map.tryPick` 全表扫描推导 commitment/finality/opening/cutoff/reviewer authority；`tests/magic-todo-projection.test.mjs`：checkpoint surface 为 `Prepared/Accepted/Assigned/Concluded` tagged lifecycle，非法跳跃 fail-closed、exact replay 幂等；每次 fold O(1) 更新 FirstPlanCommitment/LatestCommitted/PreviousCommitted，并在 dedicated enlist/replacement 增量维护 ReviewerLifeBySession；reviewer reverse locator 直接读 keyed index；`tests/magic-todo-event-store.test.mjs`：Boot Fold 后得到同一 projection | NEW + REWRITE | `node --test requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs requirements/obligation-ledger/tests/magic-todo-projection.test.mjs requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs` |
| O-19 019 | `tests/magic-todo-projection.test.mjs` `WHAT[OBLIGATION-LEDGER-019] rejects a legacy seed after the first Magic provider request` | MOVE | 见 O-8 |
| O-20 OBLIGATION-LEDGER-020 | `tests/magic-todo-after.test.mjs`：dedicated 首个 assignment 使用 OwnerRoot、后续 checkpoint Continuation；同 checkpoint 重入的 resend 准入来自 PromptAuthority durable dispatch evidence（Accepted/Pending/Dispatchable），禁止 XTrace head watermark（AwaitHead 已删）；`first T1 review start is frozen before its own commitment can move the global opening floor` 锁定首份 ManagerCheckpointLWR 从 `next(Life.OpeningCursor)` 开始而不消费当前 post-T1 global floor；`TodoProcessReviewAssigned` 在物理发送前 durable append 并冻结 exact Manager frontier；`persistent process reviewer receives only manager work after its last concluded frontier` 同时锁定后续 ManagerCheckpointLWR 不从头重放、已有 concluded review 后不再读取/发送 OpeningRaw；`requirements/review-judgement/tests/process-review-judgement.test.mjs` `REVIEW_013_continuation_process_assignment_does_not_replay_opening_authority` 锁定 continuation wire 只携带新增 bounded LWR；static/behavior proof：新 assignment 复用 logical reviewer 时允许为已 Retired 的旧 work-unit 重新 link Active，但 checkpoint 已 Assigned 后不得复活 handle；`tests/magic-todo-projection.test.mjs` `concluded manager coverage advances to the exact assigned frontier rather than the provisional prepared frontier` + `concluded manager review frontier advances only when the dedicated reviewer concludes` 验证 O(1) coverage 从实际 assigned frontier 推进，并含 `rejects process assignment before dedicated enlistment`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs` 交叉锁定 current-message captured prefix 不重复计数 | REWRITE | `node --test requirements/obligation-ledger/tests/magic-todo-after.test.mjs requirements/review-judgement/tests/process-review-judgement.test.mjs requirements/obligation-ledger/tests/magic-todo-projection.test.mjs requirements/semantic-trace/tests/x-trace-locality.test.mjs` |
| O-21 021 | `WHAT[OBLIGATION-LEDGER-021]` `tests/prefix-epoch-cutoff.test.mjs`：false planning checkpoints 不产生 committed predecessor；T1 无 prior；T1 后每次 Accepted（即使 raw false）使用 O(1) PreviousCommitted locator；EvidenceKind 仍为 TodoCheckpoint | REWRITE | `node --test requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs` |
| O-22 022 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-022] blocks Finality until plan commitment, not merely until any checkpoint`（false planning checkpoint 不授予 Finality 资格；drain 执行见 `requirements/finality/HOW.md`） | REWRITE | 见 O-1 |
| O-23 023 | `tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-023] manager guideline freezes ledger discipline as Manager-only content`（含 `manager-guideline/en.md`、`zh-CN.md` 断言） | MOVE | 见 O-4 |
| O-24 024 | `tests/magic-todo-host-codec.test.mjs` `WHAT[OBLIGATION-LEDGER-024] advertises planComplete in description, parameters, and jsonSchema`（同时锁 `horizon` enum/description）；REUSE canaries `WHAT[OBLIGATION-LEDGER-024] definition replaces description, parameters, and jsonSchema...`、`WHAT[OBLIGATION-LEDGER-024] jsonSchema ternary: both parameters and jsonSchema are replaced together` | MOVE + REWRITE + REUSE | 见 O-2 |
| O-25 025 | REUSE membrane `WHAT[OBLIGATION-LEDGER-025] openLife and compatibility injection do not wait for snapshot IO`、`WHAT[OBLIGATION-LEDGER-025] prepare rejects a pending ToolPart whose provider input is still empty`、`WHAT[OBLIGATION-LEDGER-025] before materializes the exact provider input including planComplete`、`WHAT[OBLIGATION-LEDGER-025] materialization fails closed when the provider input differs`、`WHAT[OBLIGATION-LEDGER-025] materialized snapshot input must still match tool.execute.before args`；`tests/magic-todo-after.test.mjs` `deferred prepare synchronizes the Host snapshot before freezing ReviewFrontier`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs` 交叉证明 pending/captured 两条 locality 都忽略空 stub 且保留真实 sibling | REUSE + ADD | membrane SPLIT@cutover；locality 交叉见 semantic-trace |
| O-26 026 | REUSE `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` `WHAT[OBLIGATION-LEDGER-026] first accepted checkpoint reviewer assignment is AgentOwnerRoot, independent of plan commitment`、`WHAT[OBLIGATION-LEDGER-026] reentry decides resend from durable dispatch evidence, never an XTrace head watermark`、`WHAT[OBLIGATION-LEDGER-026] the assignment is durable before the physical send freezes the reviewer frontier`、`WHAT[OBLIGATION-LEDGER-011] REVISE is feedback only: next checkpoint sees the report and Current never rolls back`、`WHAT[OBLIGATION-LEDGER-026] prepare without open life is a structured rejection, never a provider red path`、`WHAT[OBLIGATION-LEDGER-026] accepted planComplete=false carries no T1 entrustment revelation`、`WHAT[OBLIGATION-LEDGER-026] first accepted planComplete=true reveals entrustment in the enriched result`、`WHAT[OBLIGATION-LEDGER-026] enriched result after a concluded PERFECT review is silent about the previous review`；REUSE canaries `WHAT[OBLIGATION-LEDGER-026] after runs when executor succeeds`、`WHAT[OBLIGATION-LEDGER-026] after does not run when executor throws` | REUSE | membrane SPLIT@cutover |
| O-27 027 | `tests/magic-todo.test.mjs` `WHAT[OBLIGATION-LEDGER-027] horizon is planning resolution, not provider-visible lifecycle state`；`tests/magic-todo-host-codec.test.mjs` horizon required/enum + non-empty Near + workingOn→Near；`tests/magic-todo-provider-boundary.test.mjs` `WHAT[OBLIGATION-LEDGER-027] provider prose freezes progressive elaboration around workingOn` | NEW | `node --test requirements/obligation-ledger/tests/magic-todo.test.mjs requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |

### 覆盖统计

- 命题 27 / 落点 27；O-27 新增 planning-resolution 命题，不新增 phase/status 命题。
- GAP：0。O-27 已由 typed `ObligationHorizon`、strict Host decode、v4→typed ingress migration、provider prose 与 Host canary 闭合；horizon 不进入 workflow control。
- 本包顶层 12 个 test 文件，当前 **92/92 GREEN**；另有 Host boundary Magic Todo canaries **27/27 GREEN**。
- REUSE 文件的 cutover 拆分：membrane（effect-accounting / host-boundary）、host-canaries（host-boundary）、sink（HOW）、lifecycle（finality / participant-horizon / provider-language）——见上表 SPLIT@cutover。

### semantic anchor id（semantic-anchors.mjs，MECHANISM 逐 ID 归包）

本包声明拥有 `scripts/checks/semantic-anchors.mjs` 中 manager 角色的下列 anchor id
（`ROLE_SEMANTIC_ANCHORS.manager`；机制文件在 cutover 时按此声明标注 owner）：

- `obligations` —— Manager 义务账词汇（OBLIGATION-LEDGER-001/002）
- `planning-table-or-entrusted` —— BlindPlan Pre-T1 Planning Table / T1 entrustment（OBLIGATION-LEDGER-016/017）
