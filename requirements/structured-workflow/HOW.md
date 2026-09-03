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
- M6 终态由 locality dependency analyzer、slice manifest validator 与 owner-project graph 共同治理跨 locality 引用：analyzer 从 fresh compiler-resolved declaration use 推导 actual source edge；slice validator 核对 exact direct grant、bounded effective audience 与 exposure matrix；owner-project graph 校验 DAG、closure、locality kind 与 flattened emit source 并集。M6.4 前旧 `owner-contracts`/`owner-projects` 仍是唯一 release gate，新 analyzer 只报告 cutover blocker；M6.4 在一个 commit 中替换旧授权 schema与 owner-wide expansion，禁止双重权威。semantic-evidence 继续由共享 validator 对齐 requirement-trace、Surface owner/law 与 exact callback 可达 Surface use。

### 5. Locality slice authorization cutover

57.15 cutover 后，旧白盒 FCS snapshot/delta/cache 管线已退休。M6 只恢复一个 fresh、轻量的 compiler-resolved locality dependency analyzer：symbol identity 仅用于定位 declaration owner，映射到 provider locality 后立即丢弃；输出不持久化，不恢复 per-symbol ACL。`owner-projects` 继续提供 source→locality 唯一映射、ProjectReference DAG 与 closure。M6.4 后 slice validator 以 locality ID 为授权主键，owner identity 不参与准入；semantic-evidence 的 `{path,title,what_id,surface_module}` 仍由共享 validator同时对齐 requirement-trace proof graph、Surface owner/law 与 exact callback 静态可达 Surface use。

Fable-specific proof 分三层。第一层是结构 gate：ProjectReference graph 与 locality manifest 一致，并检查 foreign-facing contract/adapter locality 的**整个 transitive ProjectReference closure**；任一 contract → runtime/private 反向依赖即 RED。第二层是 compiler surface：graduated owner 的每个 production `.fs` 都有 sibling `.fsi`；owner project 与 flattened emit 都按 `.fsi → .fs` 编译，真实 build 同时证明 implementation 符合签名。`.fsi` 未签名 symbol 在 Fable source-merge 后不可见；signature-only project 本身不会产生可消费模块，因此不能用 header-only 假实现替代真实 contract implementation。第三层是永久工具链 canary：direct/transitive ProjectReference 下 `internal`、top-level private module 与 `DisableTransitiveProjectReferences` 都不是 firewall；module-local `let private` 与 `.fsi` 才是已证明的源码内隐藏原语。ProjectReference graph 始终是输入与归属边界的权威；cutover 编译是其可达 DAG 的精确扁平 closure projection，消除 Fable 递归 MSBuild 图展开成本，而原生递归 fixture 保持作为工具链行为的永久 oracle。flattened emit 只证明可发布 JS + signature compatibility，不替代 project DAG gate。

`scripts/checks/published-contracts.json` 在 M6.4 原子迁移为 slice manifest：普通 contract 记录 provider locality、exposure、allowed direct/effective locality、laws 与 evidence，不再重复 `.fsi` export inventory。physical port、adapter 与 composition wiring 保留 exact capability relation，但两端身份迁移为 consumer locality 与 provider slice/module。实际 direct/effective audience 与 source edge只在运行时从 ProjectReference DAG 与 compiler evidence 推导。

source graph 与 requirement graph 分别输出、分别验证，只共享 owner identity，不要求边集合相等。semantic-evidence 是唯一额外连接：它不关闭 WHAT，只消费 requirement graph 已建立的唯一 active、无 rejection exact edge来授权架构例外。compiler-resolved evidence 只证明 actual source edge 位于声明 closure；`.fsi` 证明 slice 完整公开面；行为 oracle 证明对应 WHAT，三者不得互相冒充。

M6 前完成的 locality 拆分是可复用的结构准备，不构成旧 ACL 的延续。`GitGateway` 已从混合 `git-integrationgate` 剥离到单文件 `git-gateway` locality，删除未使用的 `SyncActiveEnv`，由 `.fsi` 仅公开 `GitGatewayRunner`、`converge`、`createDefaultRunner`；其 direct consumer 集将在 M6 slice manifest 中按 locality 表达。

物理 Host API 必须同时固定 provider port slice 与 consumer adapter locality。`NodeFs` 是唯一 Node `fs` import owner：独立 adapter locality 的 `.fsi` 只公开当前 live port，`FileMutationTools` 不再复制第二套 import。M6 capability relation 必须精确记录 consumer locality、provider slice、consumer module 与 provider surface module；不得退回 owner pair 或裸 ProjectReference。

`ProviderRequestKind` 与 `FallbackFactCases` 的 direct consumer locality 不同，已分居 `participant-provider-attempt-requestkind` 与 `participant-provider-attempt-fallback-facts` 两个 source-pure contract locality。该拆分缩小物理 closure，但 M6.4 前仍由旧 owner-based manifest授权；只有迁入 slice grant 并由 analyzer 验证 actual/effective audience 后，才算新模型闭合。

Review opening prompt 已从历史 `mission-review-fact` 混合 composition 抽离为无依赖、单 source 的 `mission-review-prompt` contract locality。`change-integration` 与 `finality` 显式引用该 edge；前者不再因一个 opening path 获得 Fact/Witness/Challenge/RequestIdentity。该批完成时，后者因真实 Witness 消费暂留旧 mixed edge；后续 Witness 批已删除该 edge。永久 counterexample 固定 Prompt 的唯一 symbol、source-pure compile set，以及全仓 direct referencer 恰为 Change 与 Finality；任一额外 locality 获得 Prompt 均 RED。

Review judgement request identity 与 challenge renderer 已继续从 `mission-review-fact` 抽离：`mission-review-judgement-requestidentity` 只依赖 foundation identity，并只向 host-boundary/managed-session-lifecycle 发布 canonical key 与 ownership predicate；`mission-review-judgement-challenge` 只依赖 provider-projection 的 `LlmFacing` contract，并只向 review-assurance 发布 path/renderer。Host、managed runtime 与 JudgeTool 删除旧 mixed direct edge；review-assurance 因当时仍真实消费 Fact/Witness 暂留旧 edge并新增 Challenge edge。后续 Witness/Fact 两批已完成剩余切分。旧 locality 同时删除只由 Challenge 引入的 provider-projection reference。Focused compile 暴露 Host `PluginSessionScope`/`PluginRecoveryScope` 与 behavior-diagnosis Enforcer Cycle 四对 `.fsi/.fs` 的 WorkRecord namespace 均无任何 symbol 使用；十条 dead open 全部删除，不新增 ProjectReference、不扩 contract consumer伪造依赖。固定 proof 明确拒绝 Host WorkRecord edge，并枚举 RequestIdentity 与 Challenge 的全仓 exact direct referencer 集合；任一额外 locality 获得 capability 均 RED，不声称消除经其他已授权 contract 进入的传递 closure。

Review Witness 已收敛到 `mission-review-judgement-witness`：唯一依赖是 foundation identity，durable-events、finality 与 review-assurance 分别显式取得同一 witnessed confirmation contract；owner 内 ReviewFactFold 也显式声明真实依赖。Finality 与 Barrier Projection 不再引用旧 Fact locality，durable runtime、Barrier Workflow 与 ReviewFactFold 因真实构造 durable Review fact而保留 Fact edge。Witness `.fsi` 同时删除 manifest 未授权的 `isQualifiedConfirmationFor` 与 `witnesses`：前者只留实现文件内参与 `create`，后者无调用并删除实现。永久 proof 枚举全仓五个 exact direct referencer；任一额外 locality 获得 Witness 均 RED。七个 provider/consumer focused closure 全绿，未暴露新的 incidental dependency；后续 Fact 批已完成该组最终裁决。

最终 `mission-review-fact` 保持 composition：它只编译 Fact sibling pair，并以 identity、ReviewFactCases、Composition AgentFact 三条显式边完成内层 Review fact 到 durable outer union 的路由；改成 contract 会让 contract closure 反向包含 composition。全仓 direct referencer 恰为 durable runtime、Barrier Workflow 与 owner 内 ReviewFactFold。旧 manifest 曾按 symbol 拆分两组 owner 授权；M6.4 必须删除该不可执行精度，改由完整 `.fsi` surface、locality grant 与 composition-wiring relation 如实表达可见性。

### 6. Owner/impact flat compile

`scripts/lib/owner-compile.mjs` 同时生成 owner closure 与 changed-source impact plan。实现 `.fs` 使用 owning locality 的 forward closure；公开 `.fsi` 使用 owning locality 的 transitive reverse consumers，再求所有 root 的 forward union。工程、aggregate、lockfile 与 Fable tool manifest 变化直接选择 full；选中 production `.fs` 超过 aggregate 60% 也选择 full。所有模式共用 aggregate-order、zero-ProjectReference materializer 与单一 Fable launcher。

`scripts/compile-owner.mjs` 与 `scripts/compile-impact.mjs` 只消费上述 plan，禁止把原生 owner `ProjectReference` 图交给 Fable。`scripts/build.mjs` 采用全自动增量编译管道，根据代码与产物新旧状态执行 focused impact 编译或完整编译，并在无变更时直接复用产物缓存。`requirements/structured-workflow/tests/owner-impact-compile.test.mjs` 固定 implementation/signature impact、incremental compile focused flat execution & cache recording、multi-change union、project/toolchain conservative full fallback、production `.fs`/`.fsi`/project/toolchain impact-set 阶梯、CLI `--plan-only` smoke 与 obsolete recursive-graph probe 删除；`requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs` 用真实 `compile-impact.mjs` 编译 focused production `.fs` 并验证增量编译产物缓存。release 交付另记录原始 aggregate 与最终 full 路径的等价 clean timing。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| STRUCTURED-WORKFLOW-001 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-001] FLOW_001_direct_task_workflow_is_allowed` |
| STRUCTURED-WORKFLOW-002 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-002] FLOW_006_second_runtime_patterns_are_rejected` |
| STRUCTURED-WORKFLOW-003 | `requirements/structured-workflow/tests/workflow-surface.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names`；`requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] stored_and_cross_module_execution_positions_are_rejected`；`requirements/structured-workflow/tests/dsl-ownership.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] DSL_OWNERSHIP_negative_program-counter_goes_red` |
| STRUCTURED-WORKFLOW-004 | `requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs::WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision`；`requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-004] PluginTransforms_order_requires_executable_calls` |
| STRUCTURED-WORKFLOW-005 | `requirements/structured-workflow/tests/dsl-ownership.test.mjs::WHAT[STRUCTURED-WORKFLOW-005] DSL_OWNERSHIP_negative_mutable_goes_red` |
| STRUCTURED-WORKFLOW-006 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-006] FLOW_017_composition_keeps_domain_results_and_rejects_child_program_counters` |
| STRUCTURED-WORKFLOW-007 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| STRUCTURED-WORKFLOW-008 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary`；`requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| STRUCTURED-WORKFLOW-009 | `requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-009] operator abort is a control-plane wake, never a business outcome` |
| STRUCTURED-WORKFLOW-010 | `requirements/structured-workflow/tests/parallel.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order` |
| STRUCTURED-WORKFLOW-011 | `requirements/structured-workflow/tests/owner-dependencies.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] private cross-owner symbols are rejected`；`requirements/structured-workflow/tests/owner-dependencies.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] external symbols and raw open tokens are ignored`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flattened Fable emitter mirrors owner-locality source coverage`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] owner-locality project graph is complete, authorized, and acyclic`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection planner produces exact closure and canonical aggregate order`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materializes zero ProjectReference and isolated scratch props`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat projection rejects missing or stale ProjectReference before compiler invocation`；`requirements/structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] independent Fable checks enforce compile-input locality boundaries`；`requirements/structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat closure compilation compiles transitive closure green and keeps unreferenced sources red`。 |
| STRUCTURED-WORKFLOW-012 | `requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] implementation changes exclude reverse consumers`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] signature changes include every reverse consumer and exact forward union`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] toolchain changes and oversized impact select one full flat build`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] materialized impact project has exact canonical inputs and zero ProjectReference`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] incremental compile executes focused flat compile and records cache`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] multi-change union compiles each closure once`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] project file changes select one full flat build`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] production impact-set ladder classifies fs fsi project and toolchain`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI plan-only smoke matches the planner`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] obsolete recursive-graph compile probes stay deleted`；`requirements/structured-workflow/tests/owner-impact-compile.property.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] generated impact DAGs preserve change union signature monotonicity and canonical flat inputs`；`requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI compiles a focused production implementation change`；`requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI incremental compile detects and caches fresh output` |

STRUCTURED-WORKFLOW-011 的首个 exact consumer counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] GitGateway exact contract has an isolated compiler boundary`。

STRUCTURED-WORKFLOW-011 的首个 physical capability counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] NodeFs physical port and tool contracts have isolated compiler boundaries`。

STRUCTURED-WORKFLOW-011 的纯 vocabulary cohort counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] request kind and fallback facts have disjoint compiler boundaries`。

STRUCTURED-WORKFLOW-011 的 Review Prompt cohort counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] review opening prompt has a source-pure compiler boundary`。

STRUCTURED-WORKFLOW-011 的 Review request/challenge cohort counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] review request identity and challenge have source-pure compiler boundaries`。

STRUCTURED-WORKFLOW-011 的 Review Witness cohort counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] review witness has a source-pure compiler boundary`。

STRUCTURED-WORKFLOW-011 的 Review Fact exact authorization counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] review fact routing has exact per-consumer authorization`。
