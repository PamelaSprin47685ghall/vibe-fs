# obligation-ledger — HOW

## 1. 架构与状态投影

`obligation-ledger` 依托 `durable-events` 提供的事件溯源与不可变事实，通过增量投影实现状态管理：

- **增量 Projection Facts**：每个 Life 维护 O(1) 增量积分，包括 `CurrentObligationsRef`、`FirstPlanCommitment`、`LatestCommittedCheckpoint` 以及 `PreviousCommittedCheckpoint`。普通查询直接读取定位器，避免热路径扫描全量历史。
- **Direct Workflow 控制流**：业务流程采用宿主语言原生控制流，读取当前投影事实后，经由校验与执行能力直接写入持久事实，崩溃恢复通过 Boot Fold 重建投影后重入普通入口。
- **Supersession 语义**：`TodoWritePrepared` 冻结 Base 与 Submitted 账目；`TodoWriteAccepted` 持久化后，Submitted 账目立即无条件覆盖 `CurrentObligations`。

## 2. 工具拦截与生命周期机制

`todowrite` 的执行生命周期严格划分为阶段钩子：

1. **Before 阶段与延迟准备**：同步执行参数解码与内存兼容投影；后台启动延迟准备，在 Snapshot 中同步当前 ProviderRun 前的语义前缀，固化 `ReviewFrontier` 并持久化 `TodoWritePrepared`。
2. **物理执行与 After 阶段**：物理返回成功后，幂等收敛 `TodoWriteAccepted`，推导 committed lag-1 cutoff，并在首次 T1 确认时在返回结果中富化交托确认。
3. **无阻塞因果节拍**：移除过程性评审，各 checkpoint 之间无因果等待，checkpoint 提交后 Manager 可直接继续开展后续工作。

## 3. 依赖声明

```text
DEPENDS ON: durable-events, effect-accounting, semantic-trace
```

## 4. 边界（DOES NOT OWN）

- Reviewer 判定哲学与具体语义 → `review-judgement`
- 评审证据可消费性、witness 与因果确认 → `review-assurance`
- 终结资格、cohort 编排与 rest 经验 → `finality`
- 原始语义追踪与 XTrace cursor → `semantic-trace`
- 物理执行结果分类与记账 → `effect-accounting`
- 提示词文本本地化 → `provider-language`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| OBLIGATION-LEDGER-001 | `requirements/obligation-ledger/tests/magic-todo.test.mjs::WHAT[OBLIGATION-LEDGER-001] canonical obligation wire carries no provider-visible cold state` |
| OBLIGATION-LEDGER-002 | `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs::WHAT[OBLIGATION-LEDGER-002] decodes required planComplete, workingOn, and obligations`；`requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs::WHAT[OBLIGATION-LEDGER-002] malformed provider wire is a typed provider rejection` |
| OBLIGATION-LEDGER-003 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-003] clean break removes the legacy todo ontology from the production graph` |
| OBLIGATION-LEDGER-004 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-004] Manager Role Law distinguishes planning relation from entrusted mission without owning tool timing`；`requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-004] committed mode rejects planning-only debt by consequence, not keywords` |
| OBLIGATION-LEDGER-005 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-005] empty placeholders remain invalid while concrete planning work is legal before commitment` |
| OBLIGATION-LEDGER-006 | `requirements/obligation-ledger/tests/magic-todo.test.mjs::WHAT[OBLIGATION-LEDGER-006] rejects blank and duplicate obligation names as call syntax` |
| OBLIGATION-LEDGER-007 | `requirements/obligation-ledger/tests/magic-todo.test.mjs::WHAT[OBLIGATION-LEDGER-007] rejects different todowrite calls in one assistant message as syntax/protocol error` |
| OBLIGATION-LEDGER-008 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs::WHAT[OBLIGATION-LEDGER-008] rejects Accepted when it names another Prepared envelope`；`requirements/obligation-ledger/tests/magic-todo-projection.test.mjs::WHAT[OBLIGATION-LEDGER-008] rejects a replay whose frozen prepared identity differs` |
| OBLIGATION-LEDGER-009 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-009] failure triage keeps red for syntax and kills OpenCode on infrastructure faults` |
| OBLIGATION-LEDGER-010 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs::WHAT[OBLIGATION-LEDGER-010] Accepted supersedes Current immediately` |
| OBLIGATION-LEDGER-011 | `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-011] next checkpoint updates Current without rollback`；`requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-011] production checkpoint path has no reviewer settlement owner` |
| OBLIGATION-LEDGER-012 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-012] TodoWriteAccepted is the sole SSOT for checkpoints`；`requirements/obligation-ledger/tests/magic-todo.test.mjs::WHAT[OBLIGATION-LEDGER-012] replays an identical obligation checkpoint even while its review is outstanding (no new review from replay)` |
| OBLIGATION-LEDGER-013 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs::WHAT[OBLIGATION-LEDGER-013] successive checkpoints can be prepared and accepted without review blockage` |
| OBLIGATION-LEDGER-014 | `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-014] successive checkpoints can be prepared and accepted seamlessly` |
| OBLIGATION-LEDGER-015 | `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs::WHAT[OBLIGATION-LEDGER-015] workingOn projects to in_progress and every other obligation to pending`；`requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs::WHAT[OBLIGATION-LEDGER-015] projects obligations into a non-enumerable V1 compatibility view` |
| OBLIGATION-LEDGER-016 | `requirements/obligation-ledger/tests/opening-floor.test.mjs::WHAT[OBLIGATION-LEDGER-016] T1 constitutive boundary is independent from the compression floor`；`requirements/obligation-ledger/tests/opening-floor.test.mjs::WHAT[OBLIGATION-LEDGER-016] T1 constitutive body renders in Opening, not Recent`；`requirements/obligation-ledger/tests/opening-floor.test.mjs::WHAT[OBLIGATION-LEDGER-016] XTrace.forOpening keeps T1 tools; forWorkRecord drops them` |
| OBLIGATION-LEDGER-017 | `requirements/obligation-ledger/tests/opening-floor.test.mjs::WHAT[OBLIGATION-LEDGER-017] Pre-T1 BlindPlan does not enlarge the structural Opening floor`；`requirements/obligation-ledger/tests/opening-floor.test.mjs::WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor`；`requirements/obligation-ledger/tests/opening-floor.test.mjs::WHAT[OBLIGATION-LEDGER-017] static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs` |
| OBLIGATION-LEDGER-018 | `requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs::WHAT[OBLIGATION-LEDGER-018] business sequencing is a direct F# CE, not a second runtime`；`requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs::WHAT[OBLIGATION-LEDGER-018] hot-path queries use incremental projection facts, never AcceptedOrder replay`；`requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs::WHAT[OBLIGATION-LEDGER-018] recovery contract is fact reentry, not a resumable workflow position`；`requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs::WHAT[OBLIGATION-LEDGER-018] Manager authority root on blessed life completion is derived from durable LifeOpening facts, not transient PromptAuthority profiles`；`requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs::WHAT[OBLIGATION-LEDGER-018] ObligationLedgerWorkflow is isolated from foreign domain dependencies` |
| OBLIGATION-LEDGER-019 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs::WHAT[OBLIGATION-LEDGER-019] rejects a legacy seed after the first Magic provider request` |
| OBLIGATION-LEDGER-020 | `requirements/obligation-ledger/tests/magic-todo-after.test.mjs::WHAT[OBLIGATION-LEDGER-020] quality assurance is consolidated to Finality Review without dedicated process reviewers` |
| OBLIGATION-LEDGER-021 | `requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs::WHAT[OBLIGATION-LEDGER-021] committed cutoff is supplied by one previous locator, never by scanning Accepted history`；`requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs::WHAT[OBLIGATION-LEDGER-021] TodoCheckpoint evidence binds trigger plus O(1) previous committed locator` |
| OBLIGATION-LEDGER-022 | `requirements/obligation-ledger/tests/magic-todo.test.mjs::WHAT[OBLIGATION-LEDGER-022] blocks Finality until plan commitment, not merely until any checkpoint` |
| OBLIGATION-LEDGER-023 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::WHAT[OBLIGATION-LEDGER-023] manager guideline freezes ledger discipline as Manager-only content` |
| OBLIGATION-LEDGER-024 | `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs::WHAT[OBLIGATION-LEDGER-024] advertises planComplete in description, parameters, and jsonSchema` |
| OBLIGATION-LEDGER-025 | `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-025] accept rejects unknown physical success evidence`；`requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-025] openLife and compatibility injection do not wait for snapshot IO`；`requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-025] prepare rejects a pending ToolPart whose provider input is still empty`；`requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-025] before materializes the exact provider input including planComplete and workingOn`；`requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-025] materialization fails closed when the provider input differs`；`requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::WHAT[OBLIGATION-LEDGER-025] materialized snapshot input must still match tool.execute.before args` |
| OBLIGATION-LEDGER-026 | `requirements/obligation-ledger/tests/magic-todo-after.test.mjs::WHAT[OBLIGATION-LEDGER-026] after hook accepts checkpoint durably and enriches T1 revelation` |
| OBLIGATION-LEDGER-027 | `requirements/obligation-ledger/tests/magic-todo.test.mjs::WHAT[OBLIGATION-LEDGER-027] horizon is planning resolution, not provider-visible lifecycle state` |
