# structured-workflow — HOW

## 架构与实现机制

`structured-workflow` 将业务控制流严格约束在语言原生结构之内，系统实现分为四种纯粹的代码性质，并由多组静态门禁进行全量拦截：

### 1. 四种实现分类（宿主于 Owner 内部）

- **Business CE（业务故事层）**：owner workflow 入口与有界递归，由宿主语言原生语法（`task { }`）直接表达业务时序。
- **Semantic Vocabulary（语义词汇层）**：为已被证明的复杂时序赋予清晰的领域业务承诺名称（如 `reviewUntilPerfect`、`recoverDurably`），词汇内部仍为直接执行的 CE。
- **Port Decorator（端口装饰器）**：在 capability 端口上叠加非侵入的观测、指标或已命名的语义策略，严禁全局匿名管道。
- **Physical Adapter（物理适配器）**：负责与真实底层环境（Node 子进程、Git、文件系统、计时器）对接，将物理事件收敛为强类型事实。

### 2. 结构所有权与反状态机门禁（`dsl-ownership`）

`scripts/checks/dsl-ownership.mjs`（由 `tests/dsl-ownership.test.mjs` 提供完备验证）全量扫描源码并实施零容忍拦截：
- `second-runtime-protocol` & `business-interpreter`：拦截 Command/Reply 总线、AST 解释器与协议重放逻辑。
- `program-counter` & `behaviour-bool`：拦截作为控制流标记的枚举、阶段后缀与跨调用状态字段。
- `state-product`：解析记录类型的状态轴乘积，强制多轴状态必须提供显式的结构化合理性证明。
- `mutable-record-field`：拦截业务类型中的可变字段，确保 `let mutable` 仅限于底层物理资源声明。
- `dup-cases`：阻断跨文件出现完全同构的重复 DU 定义。

### 3. 控制金字塔消除门禁（`fsharp-control-pyramid`）

`scripts/checks/fsharp-control-pyramid.mjs` 与 `tests/fsharp-control-pyramid.test.mjs`、`tests/error-handling-vocabulary.test.mjs` 共同治理控制流嵌套深度：
- 识别并阻断 `match` / `if` / `try` 内部产生的第二层及更深控制分支。
- 引入标准的异步与同步 Result 组合子（`TaskResultCE`、`TaskValue`、`traverse`），将嵌套分支扁平化为线性管线。

### 3.3 语义词汇与证明义务注册

| 词汇 | 所属模块 |
|---|---|
| `ManagerBackground.ensureSettled` | Mission/Manager/Background.fs |
| `ManagerIdle.encourageLabor` | Mission/Manager/Idle.fs |
| `ReviewerContinuation.ensurePerfectConfirmed` | Mission/Review/Judgement/Continuation.fs |
| `ReviewBarrierWorkflow.reverify` | Mission/Review/Barrier/Reverify.fs |
| `FallbackLedger.recordConfirmedFailure` | Participant/Provider/Attempt/Fallback/Ledger.fs |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | Participant/Provider/Attempt/Fallback/Workflow.fs |
| `FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed` | Mission/Finality/Cohort.fs |
| `SessionRecoveryWorkflow.recoverFamilyDirect` | Execution/Session/Recovery/Workflow.fs |
| `Orchestrator.publishEventually` | Change/Program.fs |

### 3.3.1 词汇约束

### 4. 跨回调状态机阻断门禁（`cross-callback-pc`）

`scripts/checks/cross-callback-pc.mjs` 守卫异步交互边界，拦截在回调 A 中写入、在回调 B 中读取并用于驱动业务分支的伪 PC 模式（如 `TryTake*`、`IsArmed` 探测），确保回调间传递的是不透明物理能力或不可伪造的许可（permit）。

### 5. Owner contract 与 dependency 门禁（`owner-dependencies`）

`scripts/checks/owner-symbol-uses.fsx` 从仓库固定的 Fable tool manifest 载入同版本 FCS，按 `Wanxiangshu.fsproj` 做一次真实 project check。它只输出带 production declaration location 的具体 symbol use，覆盖 alias、fully-qualified reference、经 `open` 解析的真实符号、pattern 与 type-only use；raw `open`、namespace/module container、definition、same-file 与 framework symbol 不形成依赖。project symbol 缺声明位置、同一 symbol 落到多个 production 文件、FCS production set 与 `.fsproj` 不一致均 fail closed。门禁不读取生成 JS/TS，也没有词法 fallback。

`scripts/checks/owner-dependencies.mjs` 将 FCS file edge 投影到 `semantic-owners.json`。DONE migration node 的 provider edge 立即进入 strict graph；未完成 provider edge 保留为 `pendingEdges`，不能提前登记为已发布 contract。`scripts/checks/published-contracts.json` 只允许 exact path + exact `symbols` / `symbol_roots`：provider contract 明确 consumer owner，physical adapter 与 composition root 明确 consumer file、target file 与 target symbol。每个声明绑定真实 DONE node、该 node 的 published vocabulary、executable proof 与架构理由；stale symbol、stale consumer、stale target、stale SCC justification 均失败。不存在 wildcard、目录 public、文件名 public、baseline 或 allowlist 通道。

cycle 只从已授权 semantic-contract edge 构建；physical adapter 与 composition-root wiring 不伪装成领域依赖。门禁同时拒绝 foreign execution-position vocabulary 与 composition-root 对未发布 foreign DU case 的 pattern match。source graph 与 requirement graph 分别输出、分别验证，只共享 owner identity，不要求边集合相等。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| STRUCTURED-WORKFLOW-001 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs` |
| STRUCTURED-WORKFLOW-002 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs` |
| STRUCTURED-WORKFLOW-003 | `requirements/structured-workflow/tests/workflow-surface.test.mjs` |
| STRUCTURED-WORKFLOW-004 | `requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs` |
| STRUCTURED-WORKFLOW-005 | `requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-006 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs` |
| STRUCTURED-WORKFLOW-007 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-008 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-009 | `requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-010 | `requirements/structured-workflow/tests/parallel.test.mjs` |
| STRUCTURED-WORKFLOW-011 | `requirements/structured-workflow/tests/owner-dependencies.test.mjs`; `requirements/structured-workflow/tests/integration/owner-dependencies-fcs.test.mjs` |
