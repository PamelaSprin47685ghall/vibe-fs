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
| OBLIGATION-LEDGER-001 | `requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| OBLIGATION-LEDGER-002 | `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` |
| OBLIGATION-LEDGER-003 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| OBLIGATION-LEDGER-004 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| OBLIGATION-LEDGER-005 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| OBLIGATION-LEDGER-006 | `requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| OBLIGATION-LEDGER-007 | `requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| OBLIGATION-LEDGER-008 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-009 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| OBLIGATION-LEDGER-010 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-011 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-012 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-013 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-014 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-015 | `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` |
| OBLIGATION-LEDGER-016 | `requirements/obligation-ledger/tests/opening-floor.test.mjs` |
| OBLIGATION-LEDGER-017 | `requirements/obligation-ledger/tests/opening-floor.test.mjs` |
| OBLIGATION-LEDGER-018 | `requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs` |
| OBLIGATION-LEDGER-019 | `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| OBLIGATION-LEDGER-020 | `requirements/obligation-ledger/tests/magic-todo-after.test.mjs` |
| OBLIGATION-LEDGER-021 | `requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs` |
| OBLIGATION-LEDGER-022 | `requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| OBLIGATION-LEDGER-023 | `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| OBLIGATION-LEDGER-024 | `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` |
| OBLIGATION-LEDGER-025 | `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` |
| OBLIGATION-LEDGER-026 | `requirements/obligation-ledger/tests/magic-todo-after.test.mjs` |
| OBLIGATION-LEDGER-027 | `requirements/obligation-ledger/tests/magic-todo.test.mjs` |
