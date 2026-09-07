# structured-workflow — HOW

## 架构与实现机制

`structured-workflow` 将业务控制流严格约束在语言原生结构之内，系统实现分为四种纯粹的代码性质，并由多组静态门禁进行全量拦截：

### 1. 四种实现分类（宿主于 Owner 内部）

- **Business CE（业务故事层）**：owner workflow 入口与有界递归，由宿主语言原生语法（`task { }`）直接表达业务时序。
- **Semantic Vocabulary（语义词汇层）**：为已被证明的复杂时序赋予清晰的领域业务承诺名称（如 `reviewUntilPerfect`、`recoverDurably`），词汇内部仍为直接执行的 CE。
- **Port Decorator（端口装饰器）**：在 capability 端口上叠加非侵入的观测、指标或已命名的语义策略，严禁全局匿名管道。
- **Physical Adapter（物理适配器）**：负责与真实底层环境（Node 子进程、Git、文件系统、计时器）对接，将物理事件收敛为强类型事实。

### 2. 结构所有权与反状态机门禁（`dsl-ownership`）

DSL 门禁只允许纯源码文本检查。`scripts/checks/dsl-ownership.mjs` 中下列规则及其反例仍适用；当前 CLI 调用 FCS 的路径违反 VERIFICATION-SYSTEM-001，属于 GAP-031，不能把现有入口宣称为合规门禁：
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
| `ManagerWorkflow.observe` | Mission.Manager / Mission/Manager/Workflow.fs | `STRUCTURED-WORKFLOW-007` | one admission → one settled/no-effect outcome | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ManagerWorkflow.observeIdle` | Mission.Manager / Mission/Manager/Workflow.fs | `STRUCTURED-WORKFLOW-007` | one idle observation → at most one encouragement | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `FallbackLedger.recordAuthorizedFailure` | Participant.Provider / Participant/Provider/Attempt/Fallback/Ledger.fs | `STRUCTURED-WORKFLOW-007` | one policy licence + duplicate observation → one durable cursor advance | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | Participant.Provider / Participant/Provider/Attempt/Fallback/Workflow.fs | `STRUCTURED-WORKFLOW-008` | `R_fallback`: confirmed failure → bounded ordinary CE re-entry | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| `OrchestratorProgram.run` | Change / Change/Program.fs | `STRUCTURED-WORKFLOW-008` | `R_publish`: finite retry → one accepted or typed failed result | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |

### 3.3.1 词汇约束

每行是正向合同而不是名称清单：owner 拥有 WHAT，trace relation 规定允许的 multiplicity/ordering，proof 必须是可执行测试。仅在 production 中存在同名 `let` 不构成时序证明。

### 4. 跨回调状态机阻断门禁（`cross-callback-pc`）

`scripts/checks/cross-callback-pc.mjs` 守卫异步交互边界，拦截在回调 A 中写入、在回调 B 中读取并用于驱动业务分支的伪 PC 模式（如 `TryTake*`、`IsArmed` 探测）。不存在 baseline/ceiling；每个命中必须在声明处携带 `DSL-cross-callback-proof: physical <category>`，否则硬失败。

### 4.1 高阶 trace 与 composition-root 门禁

- `scripts/checks/semantic-decorator-invariant.mjs` 对名称明确表达 retry/fallback/recovery/eventually/dedupe/deadline 的 trace-policy 词汇检查全部函数端口，对其他函数只检查 canonical decorator port（operation/next/wrapped/inner），并识别等价重复调用、loop 与递归再进入；语义装饰器必须声明 owner、WHAT、trace relation、executable proof、有限 bound，以及 failure/cancel/deadline policy。只调用一次且保持业务结果、multiplicity 与 authority 的透明资源/诊断 scope 合法。generic middleware/decorator interface 与动态注册硬失败。
- `scripts/checks/plugin-transforms-invariant.mjs` 以纯 scanner 固定 `PluginTransforms` 的 typed `TransformMode` 与静态变换顺序。
- M6 终态由 slice manifest validator 与 owner-project graph 共同治理跨 locality 引用：slice validator 核对 exact direct grant、bounded effective audience 与 exposure matrix；owner-project graph 校验 declared ProjectReference DAG、exact compile closure、locality kind 与 flattened emit source 并集。仓库自定义 FCS/FSharp.Compiler.Service 扫描在任何位置均被禁止（whole-tree、focused/locality、fixture、report-only、CLI、CI、prebuild、cached/snapshot/reuse/externally supplied evidence 均在内）；不存在从 compiler-resolved declaration use 推导 actual source edge 的义务，canonical world 不含 actual_source_edges。M6.4 前旧 `owner-contracts`/`owner-projects` 仍是唯一 release gate；禁止以 report-only 或其他临时通道运行自定义 FCS analyzer。M6.4 在一个 commit 中替换旧授权 schema与 owner-wide expansion，禁止双重权威。semantic-evidence 继续由共享 validator 对齐 requirement-trace、Surface owner/law 与 exact callback 可达 Surface use。现有 FCS analyzer、evidence 与 tests 为不合规缺口（见 GAP-031），不构成可执行验收。

### 5. Locality slice authorization cutover

57.15 cutover 后，旧白盒 FCS snapshot/delta/cache 管线已退休，且不得以任何形式恢复：禁止新增任何 fresh、轻量的 compiler-resolved locality dependency analyzer（whole-tree、focused/locality、fixture、report-only、CLI、CI、prebuild、cached/snapshot/reuse/externally supplied evidence 均在内）；以 symbol identity 定位 declaration owner、映射后丢弃、输出不持久化、不恢复 per-symbol ACL 等写法也不改变被禁性质。要求（desired rule）与当前违规实现的区别以 GAP-031 为准，不得声称移除已完成。`owner-projects` 继续提供 source→locality 唯一映射、ProjectReference DAG 与 closure。M6.4 后 slice validator 以 locality ID 为授权主键，owner identity 不参与准入；semantic-evidence 的 `{path,title,what_id,surface_module}` 仍由共享 validator同时对齐 requirement-trace proof graph、Surface owner/law 与 exact callback 静态可达 Surface use。

Fable-specific proof 分三层。第一层是结构 gate：ProjectReference graph 与 locality manifest 一致，并检查 foreign-facing contract/adapter locality 的**整个 transitive ProjectReference closure**；任一 contract → runtime/private 反向依赖即 RED。第二层是 compiler surface：graduated owner 的每个 production `.fs` 都有 sibling `.fsi`；owner project 与 flattened emit 都按 `.fsi → .fs` 编译。真实编译 canary 只断言编译成功/失败或公开行为：从 consumer 精确 closure 移除必需 provider 后编译为 RED；合法 direct/transitive 输入使用为 GREEN；module-local private binding 与 `.fsi` 未导出 symbol 的外部访问为 RED；implementation 与签名不兼容为 RED。不读取 compiler AST、symbol table 或扫描报告。`.fsi` 未签名 symbol 在 Fable source-merge 后不可见；signature-only project 本身不会产生可消费模块，因此不能用 header-only 假实现替代真实 contract implementation。第三层是永久工具链 canary：direct/transitive ProjectReference 下 `internal`、top-level private module 与 `DisableTransitiveProjectReferences` 都不是 firewall；module-local `let private` 与 `.fsi` 才是已证明的源码内隐藏原语，其隐藏性必须由真实编译器边界反例证明，不得由扫描器或人工 symbol 清单模拟；不得把 Fable 的 `internal`、top-level private module 或 `DisableTransitiveProjectReferences` 当作未经证明的隔离边界。ProjectReference 声明图始终是源码输入与归属边界的权威，但声明 DAG 本身不是 Fable assembly 隔离：跨 locality 可见性仅由 sibling `.fsi` 签名面、module-local `let private` 与普通 Fable 编译行为共同约束；普通受支持 Fable 编译（内部使用 compiler services）允许，不属于被禁的自定义 scanner。cutover 编译是其可达 DAG 的精确扁平 closure projection，消除 Fable 递归 MSBuild 图展开成本，而原生递归 fixture 保持作为工具链行为的永久 oracle。flattened emit 只证明可发布 JS + signature compatibility，不替代 project DAG gate。

`scripts/checks/published-contracts.json` 在 M6.4 原子迁移为 slice manifest：普通 contract 记录 provider locality、exposure、allowed direct/effective locality、laws 与 evidence，不再重复 `.fsi` export inventory。physical port、adapter 与 composition wiring 保留 exact capability relation，但两端身份迁移为 consumer locality 与 provider slice/module。实际 direct/effective audience 只从当前 ProjectReference DAG 推导，再与 manifest 允许集合比较；grant 不决定实际 audience，不消费 FCS source edge。

### 5.1 Canonical authorization oracle

M6.3a 先建立不读取仓库、不拥有 release authority 的 production pure oracle。`scripts/lib/canonical-json-v1.mjs` 唯一拥有 canonical text comparator、closed JSON byte encoding 与 domain-separated digest；`scripts/lib/locality-slice-world-v1.mjs` 唯一拥有 `CanonicalWorldV1` 的 closed projection、ProjectReference closure、terminal classifier 与完整 locality candidate universe。任何 report、property、worksheet 或后续 release gate只调用这些函数，不复制排序、closure 或 classifier。

`scripts/lib/capability-observations-v1.mjs` 分开验证 capability partition `C(W)` 与 JavaScript traversal `J(W)`。前者核对 observation/disposition/fact identity 全等，后者核对 AST node/visit partition 与 emitted observation union；Node 仅作为 runtime 标签保留，拒绝只由 authority、mutable resource、capability value/factory/effect constructor或 Unknown 决定。M6.3a 的 fixtures 只向该 production oracle提供最小 legal world 与单点 mutation；真实 JS production extractor属于 M6.3b，禁止在测试中重建；自定义 F#/FCS production extractor 被禁止（直接或经 wrapper、反射、`.fsx` 调用 compiler service 提取 F# typed AST、symbol use、application use、推断类型或源码依赖图；whole-tree、focused、fixture、report-only 均无豁免），现有实现为不合规缺口（见 GAP-031）。

fact validator对每行重新调用唯一classifier并比较canonical disposition；所有collection入口先验证array，非法shape只返回`capability-extraction-incomplete`。world reference validator闭合fact/artifact/traversal并拒绝零node traversal。JS visitor返回带closed binding provenance的`JsCapabilityObservationV1`，不得按root字符串猜free；traversal validator以raw AST与scope resolver内部重建node universe，只接受外部visit partition，不接受caller node rows。它以显式source context包装`JavaScriptCapability` raw observation并自行计算 emitted observation union，再与canonical facts按source独立投影比较。测试的visit partition可由production visitor产生，但canonical expected facts必须独立写出，不能把visitor输出回填为expected。scope-aware binding resolution属于M6.3b extractor输入；`unresolved`必为Unknown。
`scripts/lib/locality-slice-policy-v1.mjs`拥有M6 point boundary的pure policy：contract purity、compile closure、physical-port与fatal settlement/injection/incident唯一性。它只消费`CanonicalCapabilityFactV1`与声明的 dependency mode（declared ProjectReference closure，不接受 FCS compiler dependency 观察），不接受测试手写的authority或semantic-class标签；owner fixtures只能构造非 FCS raw observation（JS/generated/explicit interop byte-source）并调用同一classifier。测试不得另写第二套 classification formula 或自定义 FCS scanner；普通源码文本静态检查仍允许。

`scripts/lib/generated-artifact-v1.mjs` 唯一计算 artifact identity、raw-byte digests、tracked-input digest并校验 exact generated relation。selector只返回filesystem paths；generator boundary拒绝root外路径并规范为repository-relative identity，再经注入tracking reader取得bytes。loop-detector generator同步消费该 reader，不能由 `repositoryTextEntries()` 私自读取。`scripts/lib/cutover-inputs-v1.mjs` 只表达 semantic closure、stage-0 index与worksheet/formal snapshot closed lifecycle；M6.4 前保持 report-only（非 FCS lifecycle，不授权任何自定义扫描），旧 gate仍是唯一 release authority。

worksheet 的合法规则仅是：从当前 owner inventory 构造 live ID，只允许覆盖全 `undecided` 集合，拒绝覆盖任何 `decided` record，不携 digest、classification 或授权力。现有 `locality-slice-report.mjs --write-fresh-worksheet` 会进入被禁 FCS 路径，不得执行；其替换属于 GAP-031，不能以 bootstrap 为例外运行扫描。

generated relation validator只消费closed relation/artifact/traversal/actual-import、directed execution-lineage edges、registered-proof/runtime-callback与canonical fact rows；artifact reference从正式observation payload派生，不接受镜像列表。lineage证明build→generator→selector，proof callback证明generator+runtime Surface；两类证据不可互换。`TraversalObservationSetV1`是同次production traversal validation返回的ephemeral closed evidence，不进入canonical world；validator要求每个artifact traversal恰有一行，并把其emitted observation ID union与同artifact canonical JavaScript fact精确比较。`unknown_node_count>0`直接RED，零node或open traversal row在schema边界RED。semantic closure要求每个扫描入口有exact import row，selector必须返回closed array；M6.3b只负责从 JS 真实 AST/Acorn scope与registry产生这些 evidence；禁止从 F# typed AST 或 FCS symbol 产生 evidence，禁止增加 FCS scanner（普通源码文本静态检查除外），不能放宽M6.3a oracle。

cutover state validator不接收已求值closure。调用方只交exact `closure_input`原始扫描集合、全量index rows、object format与两份byte map；validator内部唯一调用`resolveCutoverInputClosureV1`并从其结果取得closure与build-output exception。formal snapshot/worksheet exclusion是模块内固定协议常量，不是调用参数。collection与row先做closed-shape验证，再检查全index只有stage 0、`100644/100755` regular blob、byte map与index key全等、working tree bytes等于index blob；unsupported object format直接停止，不能按sha1继续。selector/build-output交集、symlink、额外byte row、任意caller closure/build-output/exclusion字段均由`cutover-input-closure.test.mjs`固定为RED。

### 5.2 Production report 的非 FCS 边界与现存缺口

现有 `scripts/checks/locality-dependencies.mjs`、`locality-symbol-uses.fsx`、`scanCompilerObservationsV1`、`scanDslCompilerEvidence` 及其 caller/fixture 均属 GAP-031 的违规残余，不得执行。上次按需裁剪分类、批量 project check 与双路径 evidence 对等比较的优化不改变其被禁性质；外部传入 JSON、缓存或 snapshot 也不能恢复扫描证据的权威。

合法 report 只能组合当前声明性 inventory、tracked generator、显式 interop 源码字节、JavaScript AST/Acorn scope 与唯一 canonical world。调用方不得提交 compiler observations、typed-AST 节点、FCS symbol 或 `PublicSignatureExport` 分类。F# 可见性由签名编译证明，业务语义由 owner 行为 proof 证明；缺失证据不得补空数组冒充通过。现有 `production-capability-extractor-v1.mjs` 与 `locality-slice-report.mjs` 尚依赖旧扫描输入，不能宣称已满足此机制。

非 FCS 路径仍要求完整 JS structural enumeration、semantic visit、traversal validation、generated artifact byte/linkage 核对与 canonical fact 校验。report 的 Unknown census 只是验证后事实的确定性投影，不得反向回填 world、worksheet 或授权输入；`--full` 只能扩展合法非 FCS 诊断，不能运行扫描器。当前 report CLI、worksheet writer 与旧 schema/test 的迁移均在 GAP-031，report-only 无执行豁免。

canonical JSON仍只有一套byte protocol；digest改用同一recursive encoder直接增量写入SHA-256，避免先物化超大字符串。文本比较器按Unicode scalar逐项读取，不分配code-point数组。derived disposition与caller-supplied fact最终都经过同一classifier、identity、collision、partition与coverage校验；production summary只把海量Unknown violation坐标压成带exact count的单行finding，canonical facts与`unknown_count`不丢失，显式full模式保留逐坐标诊断。

owner-impact 性能比较只允许正常 Fable 构建与不执行 FCS 的结构测量。`scripts/owner-impact-report.mjs` 和 `owner-impact-corpus.json` 当前仍登记 `fresh-production-scan` 计时命令，属于 GAP-031；禁止执行该命令，不能声称已移除。fixed case/forward closure/reverse impact/input union 的结构基线可保留；待计时命令与 release sink 均清除独立 FCS 调用后，再按 clean checkout、固定 compiler inputs 与有限 recorded timing 比较构建性能。性能finding不取得correctness authority。

`scripts/checks/owner-impact-corpus.json`绑定clean exact `e6268f35a8a3bbff6587960160bd4ceb3b64dbc3`。以下计时中 fresh-production-scan 一组为历史测量（经已禁用的自定义 FCS 扫描取得），仅具历史意义，不得作为必跑命令或验收依据：同环境三次 raw sample 的 fresh production scan 曾为`64,736/67,141/63,156ms`，median=`64,736ms`；完整release sink曾为`141,550/140,316/140,148ms`，median=`140,316ms`。结构measurement保存每个fixed case的完整compile-item identity；后续split只更新对应`successor_path`并生成candidate comparison，不重写baseline。

声明的 project graph 与 requirement graph 分别输出、分别验证，只共享 owner identity，不要求边集合相等。semantic-evidence 是唯一额外连接：它不关闭 WHAT，只消费 requirement graph 已建立的唯一 active、无 rejection exact edge来授权架构例外。不存在 compiler-resolved evidence 与 actual source edge 观察义务：跨 locality 引用只由声明的 ProjectReference DAG 与 exact compile closure 约束；`.fsi` 证明 slice 完整公开面；行为 oracle 证明对应 WHAT，三者不得互相冒充。

M6 前完成的 locality 拆分是可复用的结构准备，不构成旧 ACL 的延续。`GitGateway` 已从混合 `git-integrationgate` 剥离到单文件 `git-gateway` locality，删除未使用的 `SyncActiveEnv`，由 `.fsi` 仅公开 `GitGatewayRunner`、`converge`、`createDefaultRunner`；其 direct consumer 集将在 M6 slice manifest 中按 locality 表达。

物理 Host API 必须同时固定 provider port slice 与 consumer adapter locality。`NodeFs` 是唯一 Node `fs` import owner：独立 adapter locality 的 `.fsi` 只公开当前 live port，`FileMutationTools` 不再复制第二套 import。M6 capability relation 必须精确记录 consumer locality、provider slice、consumer module 与 provider surface module；不得退回 owner pair 或裸 ProjectReference。

`ProviderRequestKind` 与 `FallbackFactCases` 的 direct consumer locality 不同，已分居 `participant-provider-attempt-requestkind` 与 `participant-provider-attempt-fallback-facts` 两个 source-pure contract locality。该拆分缩小物理 closure，但 M6.4 前仍由旧 owner-based manifest授权；只有迁入 slice grant，并把当前 ProjectReference DAG 推导的 direct/effective audience 与允许集合独立比较后，才算新模型闭合。


### 6. Owner/impact flat compile

`scripts/lib/owner-compile.mjs` 同时生成 owner closure 与 changed-source impact plan。实现 `.fs` 使用 owning locality 的 forward closure；公开 `.fsi` 使用 owning locality 的 transitive reverse consumers，再求所有 root 的 forward union。工程、aggregate、lockfile 与 Fable tool manifest 变化直接选择 full；选中 production `.fs` 超过 aggregate 60% 也选择 full。所有模式共用 aggregate-order、zero-ProjectReference materializer 与单一 Fable launcher。

`scripts/compile-owner.mjs` 与 `scripts/compile-impact.mjs` 只消费上述 plan，禁止把原生 owner `ProjectReference` 图交给 Fable。`scripts/build.mjs` 采用全自动增量编译管道，根据代码与产物新旧状态执行 focused impact 编译或完整编译，并在无变更时直接复用产物缓存。`requirements/structured-workflow/tests/owner-impact-compile.test.mjs` 固定 implementation/signature impact、incremental compile focused flat execution & cache recording、multi-change union、project/toolchain conservative full fallback、production `.fs`/`.fsi`/project/toolchain impact-set 阶梯、CLI `--plan-only` smoke 与 obsolete recursive-graph probe 删除；`requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs` 用真实 `compile-impact.mjs` 编译 focused production `.fs` 并验证增量编译产物缓存。release 交付另记录原始 aggregate 与最终 full 路径的等价 clean timing。

---

## 验证与测试落点

STRUCTURED-WORKFLOW-011 不存在 compiler-resolved locality closure proof：`requirements/structured-workflow/tests/locality-dependencies.test.mjs` 的两条 scanner 行为断言与 `requirements/structured-workflow/tests/integration/locality-dependency-analyzer.test.mjs` 的 analyzer fixture（执行 `runLocalityDependencyScan`/`scanDslCompilerEvidence` 并 spawn dotnet FCS checker）均为不合规缺口（见 GAP-031），不构成可执行验收。011 的可执行证明只由下表中的声明式 project graph、exact compile closure、sibling `.fsi`、正常 Fable 编译 canary 与已注册行为证明承载。

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
| STRUCTURED-WORKFLOW-010 | `requirements/structured-workflow/tests/parallel.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order`；`requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] RECONCILE_SCHEDULER_BOUND_001: same-session burst coalesces and stale work is invalidated`；`requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] RECONCILE_SCHEDULER_BOUND_002: stop waits for the live pass and drains to zero observable work`；`requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] RECONCILE_SCHEDULER_BOUND_003: kicks after stop do nothing` |
| STRUCTURED-WORKFLOW-011 | `requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flattened Fable emitter mirrors owner-locality source coverage`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] owner-locality project graph is complete, authorized, and acyclic`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection planner produces exact closure and canonical aggregate order`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materializes zero ProjectReference and isolated scratch props`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat projection rejects missing or stale ProjectReference before compiler invocation`；`requirements/structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] independent Fable checks enforce compile-input locality boundaries`；`requirements/structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] flat closure compilation compiles transitive closure green and keeps unreferenced sources red` |
| STRUCTURED-WORKFLOW-012 | `requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] implementation changes exclude reverse consumers`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] signature changes include every reverse consumer and exact forward union`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] toolchain changes and oversized impact select one full flat build`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] materialized impact project has exact canonical inputs and zero ProjectReference`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] incremental compile executes focused flat compile and records cache`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] multi-change union compiles each closure once`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] project file changes select one full flat build`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] production impact-set ladder classifies fs fsi project and toolchain`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI plan-only smoke matches the planner`；`requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] obsolete recursive-graph compile probes stay deleted`；`requirements/structured-workflow/tests/owner-impact-compile.property.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] generated impact DAGs preserve change union signature monotonicity and canonical flat inputs`；`requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI compiles a focused production implementation change`；`requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI incremental compile detects and caches fresh output` |
| STRUCTURED-WORKFLOW-014 | `requirements/structured-workflow/tests/capability-observation.test.mjs::WHAT[STRUCTURED-WORKFLOW-014] JavaScript visitor closes dynamic computed CommonJS and parameterless Date capabilities`；`requirements/structured-workflow/tests/capability-observation.test.mjs::WHAT[STRUCTURED-WORKFLOW-014] JavaScript visitor uses resolved binding provenance and never guesses from a root name`；`requirements/structured-workflow/tests/capability-observation.test.mjs::WHAT[STRUCTURED-WORKFLOW-014] JavaScript traversal rejects empty AST and every open visit-result shape`；`requirements/structured-workflow/tests/capability-observation.test.mjs::WHAT[STRUCTURED-WORKFLOW-014] traversal derives the complete node universe from AST and every array boundary is total` |
| STRUCTURED-WORKFLOW-015 | `requirements/structured-workflow/tests/generated-module-relation.test.mjs::WHAT[STRUCTURED-WORKFLOW-015] generated artifact binds tracked inputs bytes lineage traversal and import` |
| STRUCTURED-WORKFLOW-016 | `requirements/structured-workflow/tests/cutover-input-closure.test.mjs::WHAT[STRUCTURED-WORKFLOW-016] cutover closure and stage index reject every competing input world`；`requirements/structured-workflow/tests/cutover-input-closure.property.test.mjs::WHAT[STRUCTURED-WORKFLOW-016] one staged input mutation yields the exact closure or index violation` |

STRUCTURED-WORKFLOW-013 尚无符合修订合同的可执行证明：现有 canonical-world 测试仍要求旧 `actual_source_edges`/FCS fact schema，不能登记为当前证明，回归缺口计入 GAP-031。未登记的旧 symbol ACL、capability FCS 分类与 adjudication 测试同样不取得证明权威。上表的非 FCS 落点只证明各自行为，不宣称当前实现已符合整个修订合同。

STRUCTURED-WORKFLOW-011 的首个 exact consumer counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] GitGateway exact contract has an isolated compiler boundary`。

STRUCTURED-WORKFLOW-011 的首个 physical capability counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] NodeFs physical port and tool contracts have isolated compiler boundaries`。

STRUCTURED-WORKFLOW-011 的纯 vocabulary cohort counterexample：`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] request kind and fallback facts have disjoint compiler boundaries`。
