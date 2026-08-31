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
- `program-counter` & `behaviour-bool`：拦截作为控制流标记的枚举、阶段后缀、stored/exported `NextAction|NextStep|ResumeAt*|StepIndex|ContinueToken` 与跨调用状态字段；ExternalSignal/PhysicalHandle 同名碰撞必须在声明处正向分类。
- `DSL-class` taxonomy：区分 Vocabulary、DurableFact、Evidence、Decision、ExternalSignal、Witness、Capability、Receipt、PhysicalHandle；分类说明值的语义，不能把 PC 洗成领域状态。
- `state-product`：解析记录类型的状态轴乘积，强制多轴状态必须提供显式的结构化合理性证明。
- `mutable-record-field`：拦截业务类型中的可变字段，确保 `let mutable` 仅限于底层物理资源声明。
- `dup-cases`：阻断跨文件出现完全同构的重复 DU 定义。

### 3. 控制金字塔消除门禁（`fsharp-control-pyramid`）

`scripts/checks/fsharp-control-pyramid.mjs` 与 `tests/fsharp-control-pyramid.test.mjs`、`tests/error-handling-vocabulary.test.mjs` 共同治理控制流嵌套深度：
- 识别并阻断 `match` / `if` / `try` 内部产生的第二层及更深控制分支。
- 引入标准的异步与同步 Result 组合子（`TaskResultCE`、`TaskValue`、`traverse`），将嵌套分支扁平化为线性管线。

### 3.3 语义词汇与证明义务注册

| 词汇 | owner / 模块 | WHAT law | 允许的 trace relation | executable proof |
|---|---|---|---|---|
| `ManagerBackground.ensureSettled` | Mission.Manager / Mission/Manager/Background.fs | `STRUCTURED-WORKFLOW-007` | one admission → one settled/no-effect outcome | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ManagerIdle.encourageLabor` | Mission.Manager / Mission/Manager/Idle.fs | `STRUCTURED-WORKFLOW-007` | one idle observation → at most one encouragement | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ReviewerContinuation.ensurePerfectConfirmed` | Mission.Review / Mission/Review/Judgement/Continuation.fs | `STRUCTURED-WORKFLOW-007` | one judgement trace → one typed confirmation outcome | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ReviewBarrierWorkflow.reverify` | Mission.Review / Mission/Review/Barrier/Reverify.fs | `STRUCTURED-WORKFLOW-007` | fresh reviewer trace → one typed barrier result | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `FallbackLedger.recordAuthorizedFailure` | Participant.Provider / Participant/Provider/Attempt/Fallback/Ledger.fs | `STRUCTURED-WORKFLOW-007` | one policy licence + duplicate observation → one durable cursor advance | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | Participant.Provider / Participant/Provider/Attempt/Fallback/Workflow.fs | `STRUCTURED-WORKFLOW-008` | `R_fallback`: confirmed failure → bounded ordinary CE re-entry | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| `FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed` | Mission.Finality / Mission/Finality/Cohort.fs | `STRUCTURED-WORKFLOW-008` | `R_cohort`: finite roster → first revision or all-confirmed | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| `SessionRecoveryWorkflow.recoverFamilyDirect` | Execution.Session / Execution/Session/Recovery/Workflow.fs | `STRUCTURED-WORKFLOW-008` | `R_recovery`: durable facts + current reality → ordinary entry | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| `Orchestrator.publishEventually` | Change / Change/Program.fs | `STRUCTURED-WORKFLOW-008` | `R_publish`: finite retry → one accepted or typed failed result | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |

### 3.3.1 词汇约束

每行是正向合同而不是名称清单：owner 拥有 WHAT，trace relation 规定允许的 multiplicity/ordering，proof 必须是可执行测试。仅在 production 中存在同名 `let` 不构成时序证明。

### 4. 跨回调状态机阻断门禁（`cross-callback-pc`）

`scripts/checks/cross-callback-pc.mjs` 守卫异步交互边界，拦截在回调 A 中写入、在回调 B 中读取并用于驱动业务分支的伪 PC 模式（如 `TryTake*`、`IsArmed` 探测）。不存在 baseline/ceiling；每个命中必须在声明处携带 `DSL-cross-callback-proof: physical <category>`，否则硬失败。

### 4.1 高阶 trace 与 composition-root 门禁

- `scripts/checks/semantic-decorator-invariant.mjs` 对名称明确表达 retry/fallback/recovery/eventually/dedupe/deadline 的 trace-policy 词汇检查全部函数端口，对其他函数只检查 canonical decorator port（operation/next/wrapped/inner），并识别等价重复调用、loop 与递归再进入；语义装饰器必须声明 owner、WHAT、trace relation、executable proof、有限 bound，以及 failure/cancel/deadline policy。只调用一次且保持业务结果、multiplicity 与 authority 的透明资源/诊断 scope 合法。generic middleware/decorator interface 与动态注册硬失败。
- `scripts/checks/plugin-transforms-invariant.mjs` 以纯 scanner 固定 `PluginTransforms` 的 typed `TransformMode` 与静态变换顺序。
- `scripts/checks/composition-root-invariant.mjs` 以同一正向 root registry 治理 `PluginBoot`、`HostSignalBootstrap`、`PluginTransforms`、`ToolRegistry`：允许 construction、typed topology、fixed order、routing、lifetime、drain/disposal，拒绝 root-local policy、PC 与动态 pipeline。foreign symbol 授权仍唯一由 `owner-dependencies` 判断。

### 5. Owner contract 与 dependency 门禁（`owner-dependencies`）

`scripts/checks/owner-symbol-uses.fsx` 从仓库固定的 Fable tool manifest 载入同版本 FCS，按 `Wanxiangshu.fsproj` 做一次真实 project check。它只输出带 production declaration location 的具体 symbol use，覆盖 alias、fully-qualified reference、经 `open` 解析的真实符号、pattern 与 type-only use；raw `open`、namespace/module container、definition、same-file 与 framework symbol 不形成依赖。project symbol 缺声明位置、同一 symbol 落到多个 production 文件、FCS production set 与 `.fsproj` 不一致均 fail closed。门禁不读取生成 JS/TS，也没有词法 fallback。

`scripts/checks/owner-dependencies.mjs` 将 FCS file edge 投影到 `semantic-owners.json`。DONE migration node 的 provider edge 立即进入 strict graph；未完成 provider edge 保留为 `pendingEdges`，不能提前登记为已发布 contract。`scripts/checks/published-contracts.json` 只允许 exact path + exact `symbols` / `symbol_roots`：provider contract 明确 consumer owner，physical adapter 与 composition root 明确 consumer file、target file 与 target symbol。每个声明绑定真实 DONE node、该 node 的 published vocabulary、executable proof 与架构理由；stale symbol、stale consumer、stale target、stale SCC justification 均失败。不存在 wildcard、目录 public、文件名 public、baseline 或 allowlist 通道。

完整阶梯只执行一次 production FCS scan。其 normalized evidence 原子写入 `schemaVersion + runId + inputFingerprint`；fingerprint 绑定 scanner、project、npm dependency lock 与完整 production compile set 内容。后续 owner gates 与 integration proof 只能携 exact run-id 复用该 artifact；证据缺失、schema/run-id/fingerprint 不符或 production file set 漂移全部 fail closed，禁止以第二次昂贵扫描伪造“独立性”。

cycle 只从已授权 semantic-contract edge 构建；physical adapter 与 composition-root wiring 不伪装成领域依赖。门禁同时拒绝 foreign execution-position vocabulary 与 composition-root 对未发布 foreign DU case 的 pattern match。source graph 与 requirement graph 分别输出、分别验证，只共享 owner identity，不要求边集合相等。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| STRUCTURED-WORKFLOW-001 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-001] FLOW_001_direct_task_workflow_is_allowed` |
| STRUCTURED-WORKFLOW-002 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-002] FLOW_006_second_runtime_patterns_are_rejected` |
| STRUCTURED-WORKFLOW-003 | `requirements/structured-workflow/tests/workflow-surface.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names`；`requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] stored_and_cross_module_execution_positions_are_rejected`；`requirements/structured-workflow/tests/dsl-ownership.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] DSL_OWNERSHIP_negative_program-counter_goes_red` |
| STRUCTURED-WORKFLOW-004 | `requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs::WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision`；`requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-004] composition_root_registry_is_complete_and_new_semantics_are_RED` |
| STRUCTURED-WORKFLOW-005 | `requirements/structured-workflow/tests/dsl-ownership.test.mjs::WHAT[STRUCTURED-WORKFLOW-005] DSL_OWNERSHIP_negative_mutable_goes_red` |
| STRUCTURED-WORKFLOW-006 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-006] FLOW_017_composition_keeps_domain_results_and_rejects_child_program_counters` |
| STRUCTURED-WORKFLOW-007 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| STRUCTURED-WORKFLOW-008 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary`；`requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| STRUCTURED-WORKFLOW-009 | `requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-009] operator abort is a control-plane wake, never a business outcome` |
| STRUCTURED-WORKFLOW-010 | `requirements/structured-workflow/tests/parallel.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order` |
| STRUCTURED-WORKFLOW-011 | `requirements/structured-workflow/tests/owner-dependencies.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] private cross-owner symbols are rejected`；`requirements/structured-workflow/tests/integration/owner-dependencies-fcs.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] FCS resolves open, alias, qualified, and type-only dependencies` |
