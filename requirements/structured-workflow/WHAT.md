# structured-workflow — WHAT

本文件是 `structured-workflow` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## STRUCTURED-WORKFLOW-001: 业务流程由宿主语言结构直接表达

业务控制流必须直接由宿主语言的原生结构（当前为 F# 的 `task { }`、`let!`、`do!`、`use!`、`match` / `match!`、`return!`、具名纯函数与有界递归）表达。宿主语言的调用栈即为业务流程栈，严禁将「程序下一步走向」编码为可长期存储的字段或枚举。

## STRUCTURED-WORKFLOW-002: 禁止第二业务运行时

领域 DSL 必须是直接执行的 CE 与领域具名操作。严禁在业务层构造内部 AST 后再进行二次解释，严禁引入 Command/Reply 消息总线、Step continuation 状态树或调用序列回放器。外部协议的解码器与物理进程命令的强类型化不受此限。

## STRUCTURED-WORKFLOW-003: 存储描述现实而非执行位置

DU 与数据字段仅允许表示封闭的领域词汇（如角色、终态类别）、已发生事实的持久证据（如 AgentFact 子族）或单次函数返回结果。严禁把 `NextAction`、`NextStep`、`ResumeAt*`、`StepIndex`、`ContinueToken` 等执行位置存入 record/DU/mutable cell，或通过 exported/cross-module surface 暴露给调用者驱动下一步；改名、跨文件搬运与恢复专用命名均不改变其 PC 性质。外部协议、物理句柄等同名碰撞必须在声明处以 `DSL-class: ExternalSignal` / `PhysicalHandle` 正向分类，路径或类型名本身不形成豁免。合法分类集合还明确区分 `Witness`、`Capability` 与 `Receipt`，这些权威/证据值仍不得承载执行位置。严禁表示流程当前执行到第几步（如 `CurrentStage`、`InFlight`、`Parked`）或多个状态的正交乘积。持久化事实与投影只描述发生过的事实与已证明的证据，严禁记录持久化程序计数器（Durable PC）。`Stage`、`Phase`、`Lease`、`Owner`、`Generation` 等词汇严禁作为流程控制的程序计数器或伪领域状态；除真实物理归属与底层物理资源世代标识外，上述词汇不得出现在业务状态机命名中。同一领域事实严禁在多处定义同构的 DU 类型；跨文件出现相同 case 集合的重复定义必须合并或明确划分有界上下文（bounded context）并提供单向转换，消除双写与状态漂移。系统崩溃后的恢复必须通过 Journal fold 产生领域事实，随后直接调用普通业务 workflow 入口重新进入流程；严禁恢复底层的 continuation 指针、程序计数器或临时中间状态；Reconcile 仅作为观测稳定边界，不承担业务调度操作系统的职责。

SW-003 vs SW-009 消歧：若恢复时将 projection fold 成唯一「最新 case」，等价于恢复一个隐藏的 durable resume-address，此举为 SW-009 禁止；合法的模式必须是 semantic entry 从一组 durable facts 与当前物理现实重新证明 outstanding obligation，随后直接调用普通业务流程。

## STRUCTURED-WORKFLOW-004: 纯决策与物理效果显式分缝

代码目录必须按照拥有者（owner）成树组织，严禁设立全局分层的顶层根目录（如 `Domain/`、`Application/`、`Infrastructure/`）。纯决策计算、具名语义词汇、端口装饰器与物理适配器属于 owner 内部的实现种类。composition root 必须宽而浅，只能承担 construction、typed topology/mode selection、fixed order、routing、lifetime、drain 与 disposal；`PluginBoot`、`HostSignalBootstrap`、`PluginTransforms`、`ToolRegistry` 等 root 严禁实现 owner-specific decision/recovery/classification、存储 PC 或动态 pipeline。此约束不得退化为 LOC/import-count 规则。领域操作必须通过具名 capability 调用副作用，严禁使用泛化的执行总线抹平强类型边界。控制分支（如 `match`、`if`、`try`）内部严禁嵌套产生第二层及更深的控制决策树（lexical pyramid）。嵌套错误处理与短路逻辑必须通过标准的 `Result` / `Option` 组合子（如 `result { }`、`taskResult { }`、`traverse`）进行扁平化表达，复杂的领域决策必须提取为独立的具名决策责任。

## STRUCTURED-WORKFLOW-005: 可变存储仅承载物理资源、投影缓存或算法草稿

可变存储（`let mutable` 与 `ref`）仅允许用于纯算法局部暂存（algorithm scratch）、并发同步原语、投影缓存（projection cache）或底层物理句柄（如 Task、Dictionary、CancellationTokenRegistration、锁对象等物理资源）。严禁使用可变存储记录业务阶段、执行槽位或行为布尔值。物理可变存储必须显式进行声明式标注。

## STRUCTURED-WORKFLOW-006: 业务 workflow 组合具有结构闭包与合法状态证明

当数据结构同时包含两类以上的状态控制轴（如 `State`、`Pending/Offer`、`Recovery/Repair`、`Drain`）时，必须能够证明所有可达组合均具备确切的业务含义并完成结构化分类。无法证明合理性的多轴组合必须拆分为独立的业务流程或显式权限许可（permit）。当一个业务 workflow 组合另一个 workflow 时，组合结果必须继续由原生语言调用、CE bind/return 及具名词汇直接表达。父 workflow 只能观察子 workflow 的类型化输入、领域结果与能力证明，严禁接收、存储或探测子流程的执行位置（包括 `Stage`、`NextAction`、`NextStep`、`ResumeAt*`、`StepIndex`、`ContinueToken`），也不得通过 `Advance/Tick/Step` 等轮询接口驱动子流程。模块接缝与 cross-callback registry 必须携带正向 physical/capability proof，否则严禁退化为状态机驱动总线；不存在 debt baseline。

Protocol-boundary exemption（外部协议边界豁免条件）：若存在外部交互协议必须通过 step/nextTool 与外部 caller 交互，必须满足：(1) kernel 唯一拥有 continuation/closure/停止权；(2) external caller 只提供 observation；(3) 豁免必须以书面 protocol-boundary exemption 形式记录于规范中。

## STRUCTURED-WORKFLOW-007: 语义压缩需 owner law 与行为证明

业务 CE 中调用的复杂时序操作必须封装为具有明确领域承诺的 Semantic Vocabulary（语义词汇）。词汇名称必须准确表达业务承诺（如 `reviewUntilPerfect`、`recoverDurably`），严禁使用无明确语义的伪操作码（如 `process`、`handle`、`doRetry`）。已被独立测试完整覆盖的机械时序允许通过 Semantic Vocabulary 进行压缩。被压缩的词汇必须拥有自身专属的时序或行为证明（temporal/behavioral proof），隐藏内部机械步骤不得改变宿主 CE 直接调用的本质。业务流程的正确性必须由可观察效果（产生的领域事实、调用轨迹、端口交互与最终状态）进行端到端证明，严禁通过断言解释器内部运行到的 AST 节点来判定正确性。

## STRUCTURED-WORKFLOW-008: 改 trace 的高阶组合必须命名与拥有

端口装饰器与高阶组合分为两类：passed operation 恰好 once-through，且保持 business outcome、multiplicity 与 authority 的透明资源/诊断 scope 合法；重复调用或在 recovery/fallback/catch path 再调用会改变业务 trace，必须在声明处绑定 owner、WHAT law、允许的 trace relation、executable proof、有限 bound，以及 failure/cancel/deadline policy，并在调用点具有明确名称。缺少任一项即为匿名策略。严禁 generic middleware/decorator interface、动态注册和匿名全局框架（如 MiddlewarePipeline、DecoratorBase、IWorkflowDecorator、ITransformMiddleware、WorkflowBuilder）；不得以 central runtime 取代普通 CE re-entry。

## STRUCTURED-WORKFLOW-009: 取消是控制面，不是业务数据

取消与中断属于控制面事件，用于决定程序是否继续执行，严禁伪装为业务终态结果数据。取消事件不得直接当作业务结果写入数据流，防止恢复与降级逻辑误判业务状态。

## STRUCTURED-WORKFLOW-010: 有界循环与有界扇出

所有业务循环与并发扇出必须有界。业务并发扇出必须通过 `Parallel.mapBounded` 进行，明确指定正有限的并发上限、保持输入下标顺序、支持取消传递并在异常时立即拒绝与归还许可。严禁在业务层使用无界并发或无界重试作为默认机制。

## STRUCTURED-WORKFLOW-011: 跨 locality 依赖必须由 contract slice 授权

每个 production `.fs` 必须拥有恰一个 primary semantic owner，并且被恰一个 owner-locality fsproj 编译；每个 locality 只承载一个 primary semantic owner、恰对应一个 fsproj，并拥有稳定且全局唯一的 locality ID。owner 只拥有 vocabulary、invariant、failure algebra 与业务决策；locality/project 拥有编译身份；contract slice 拥有能力授权。任意 `consumer locality != provider locality` 的 dependency 都必须经过 provider slice grant，same-owner 不豁免。owner rename 或 merge 不得改变任何 locality edge、grant 或 authorization verdict。

ProjectReference 图必须是 DAG，并且始终是源码输入与归属边界的权威。compiler-resolved declaration use 必须映射为规范化的 cross-locality source edge；每条 actual source edge 的 provider locality 必须位于 consumer locality 的 ProjectReference transitive closure。release lane 必须 fresh 分析完整 production compile set；禁止以 aggregate 编译成功、人工 baseline、旧 snapshot、delta、mtime 或跨 run cache替代该证明。`Wanxiangshu.fsproj` 只是无语义的 flattened emitter：其 compile set 必须与 locality source 并集精确相等、不得引用 locality project、不得成为授权 provider。requirement dependency graph 与 source graph 只共享 owner identity，不要求边集合相等。

每个可被其他 locality 使用的 provider locality 必须形成一个 contract slice，并由 sibling `.fsi` 作为唯一 export inventory；manifest 不得重复保存或声称实施 per-symbol/per-owner ACL。同一 slice 的全部 `.fsi` exports 对其 effective audience 可见；若该能力集合不可共同授权，必须拆 slice。未来若重新要求 exact symbol isolation，必须使用保留 symbol identity 的 compiler-resolved analyzer，或改用提供真实 assembly isolation 的构建机制。

slice exposure 只有 `shared`、`bounded`、`effect`；locality kind 只有 `contract`、`runtime`、`adapter`、`composition`。`private` locality 不形成 slice，禁止其他 locality 引用。`shared` 只允许 immutable data、opaque identity、纯函数与无 authority 的 capability type；`bounded` 还必须满足 `actual effective consumers ⊆ allowed effective consumers`；两者只能由 `.fsi` 完整、直接与传递 closure 均为 Contract 的 contract locality 承载，且 closure 不超过 100 个 production `.fs`。Host import、IO、写入、网络、进程、provider、Git mutation、mutable registry、capability value 与 factory 属于 `effect`，只能位于 runtime/adapter；其全部实际反向可达 consumer 必须是 composition。composition 只承担 terminal wiring；若被引用，只允许 composition consumer 通过 exact composition-wiring relation 到达，禁止充当普通 provider。

manifest 只保存规范允许集合：每个 slice 的 provider locality、owner、exposure、`allowed_direct_consumers`、bounded slice 的 `allowed_effective_consumers`、`laws[]`、justification 与 semantic evidence。实际 direct consumers、effective consumers 与 source edges 必须由当前 ProjectReference DAG 和 compiler-resolved analyzer 同次推导，不得写入 manifest 形成第二事实源。每条 cross-locality direct ProjectReference 必须恰有一个 slice grant；actual direct consumers 必须与 allowed direct consumers 精确相等。physical port、adapter 与 composition wiring 必须保留 exact `consumer locality → provider slice` capability relation及两端 module，不得降级为 owner pair、裸路径或仅证明 ProjectReference 存在。

`semantic-evidence` 必须继续引用由 `requirement-trace` 建立的唯一 active、无 rejection 的 exact `{path,title,what_id,surface_module}` proof edge：WHAT package 等于 contract owner；`surface_module` 是 owner 注册、包含该 law 的 production Surface；exact test callback 的静态可达调用闭包消费该 Surface。裸路径、文件存在、源码字符串、comment-only、skip/todo、错误 title/WHAT、路径穿越、替代 proof、仅 import 未使用或仅由同文件其他 callback 使用均不授权。该闭包只证明 callback 触达 production Surface；行为 oracle 仍由 owner proof 负责。

门禁必须拒绝：unowned/duplicate owner 或 locality、重复编译归属、locality kind/exposure 非法、ProjectReference SCC、missing/stale/duplicate reference 或 grant、same-owner 未授权 edge、actual source edge 逃出声明 closure、private 被引用、contract closure 含非 Contract 或超过预算、shared 携带 effect authority、bounded effective audience 越界、effect 被非 composition 到达、composition 被普通 consumer 引用、emit/locality compile-set drift、`.fsi` 缺失或与 implementation 不兼容、伪造/悬空 semantic evidence，以及 physical port/adapter/composition relation 降级。真实 compiler canary 必须同时固定：aggregate 可编译但 missing closure edge 为 RED；合法 direct/transitive edge 为 GREEN；alias/open/generic/type/pattern declaration use 均可归属；external/package symbol 不产生 production locality edge；module-local private binding 与 `.fsi` 未导出 symbol 保持不可见。

## STRUCTURED-WORKFLOW-012: owner/impact compile 必须是精确输入并集的一次 flat Fable

owner compile 的输入是目标 owner locality 加其 transitive ProjectReference closure；impact compile 的输入按 changed contract 决定。修改带 sibling `.fsi` 的实现 `.fs` 且 `.fsi` 未变时，只选择该实现 locality 与其 forward contract closure，普通 reverse consumer 禁止进入 impact set。修改 `.fsi`、新增/删除公开 source，或同批同时修改 sibling `.fsi` 时，必须选择 owning locality 的全部 transitive reverse consumers，再对所有选中 root 求 forward closure。多个改动合并为集合并集，不得逐工程重复编译。fsproj、`Directory.Build.*`、package lock、Fable/tool manifest 或 aggregate emitter 变化保守选择 full。

所有选中 source 必须按 `Wanxiangshu.fsproj` 的 canonical order 生成一个零 ProjectReference flat fsproj，并由一次真实 Fable invocation 编译；input union 少一个、多一个、重复或乱序都失败。选中 production `.fs` 超过 aggregate 的 60% 时直接退化为 full flat build，不伪装 focused。全自动增量编译根据代码与产物的新旧状态精确计算 impact 闭包并复用产物缓存，无文件变更时秒级返回，禁止依赖 watch 守护进程。

full release build 始终以 aggregate emitter 的完整 source/config union 启动一次 Fable；多工程 topology 只负责 ownership 与 impact 计算，不得逐 owner 编译后再拼接。等价 clean 环境下，最终 full multi-project 路径的输入集合、Fable 配置、invocation 数与原始单工程路径相同；交付必须记录两者的实际 clean timing，禁止仅凭结构推断性能等价。

## STRUCTURED-WORKFLOW-013: 授权裁决只消费唯一 canonical world

locality-slice 授权的唯一输入必须由 production pure `buildCanonicalWorldV1` 产生。world 顶层只允许 `schema_version=1`、`fact_schema_version=1`、`observed`、`normative`；`observed` 只允许 `localities/project_references/actual_source_edges/generated_artifacts/javascript_traversals/capability_extraction/capability_facts`，`normative` 只允许 `authorization_schema_version=2`、`slices/capability_relations/generated_module_relations`。nested row 同样是 closed schema，未知、缺失或 variant 外字段全部 RED。locality owner/kind 只存在于 observed locality row；generated linkage与output/input digest只存在于 observed artifact row；grant、law与evidence只存在于所属 normative claim。禁止另建可竞争的 owner、fact、linkage、actual audience 或 source-edge snapshot。

唯一字节入口 `encodeCanonicalJsonV1` 只接受 `null/bool/string/non-negative safe integer/dense array/plain object`；拒绝 `undefined/NaN/Infinity/-0/fraction/bigint/function/symbol/sparse array/non-plain object` 与 unpaired surrogate。object key按 Unicode code point 序列升序，array 不重排，string 不做 normalization；输出是无 BOM、whitespace、尾随 LF 的 UTF-8 JSON。repository path 必须是无绝对前缀、反斜杠、`.`、`..`、空 segment 的 POSIX relative path。所有 unordered collection 使用同一 comparator按其规范 identity 排序并保持 unique；完全相同的 compiler source edge可投影一次，同 identity 不同 payload、locality/source重复归属、重复 grant/law/evidence/relation一律 RED，禁止以 dedupe 修复非法输入。

`canonical_world_digest = "sha256:" + SHA256(UTF8("canonical-world/v1\u0000") ++ serializeCanonicalWorldV1(world))`；query digest使用同一encoder及`canonical-query/v1\u0000<query-id>\u0000` domain。ProjectReference forward closure是 reflexive；actual effective consumers是reverse reflexive closure删掉provider自身。`classifyTerminalV1`是唯一 terminal classifier：无 slice=`private`；合法唯一 slice只能得到`contract-shared/contract-bounded/runtime-effect/adapter-effect/composition-terminal`，不得猜默认。`deriveAdjudicationCandidates`必须为每个 live locality精确产生一个key，始终含`TerminalClassificationRequired`，其他reason只能增加信息，不能过滤 locality。surface/audience/capability query、record、report、property与release gate必须调用同一production classifier/query，禁止复制 switch 或 closure 公式。

## STRUCTURED-WORKFLOW-014: capability partition 与 JavaScript traversal 是两个完整闭集

production extractor必须先由`enumerateCapabilityObservationsV1`枚举完整`C(W)`，再由总函数`classifyCapabilityObservationV1`给每条 observation恰一个`Irrelevant | Classified | Unknown` disposition；`extractObservedCapabilityFactsV1`必须证明 observation keys 与 disposition keys 全等且三类两两不交。`C(W)`包含全部production `.fs`可执行F# AST node、compiler-resolved external symbol occurrence、Fable Import/Emit/emitJsExpr、全部sibling `.fsi` public export，以及JS visitor实际产出的capability observations。`Classified`必须保留runtime、authority、mutable resource与semantic class的完整多标签集合；metadata、kind、exposure、grant、generated proof与allowlist均不得增加、删除或降格 observation。

observation、disposition、fact与diagnostic collection必须是array；null、scalar或object输入必须稳定返回`capability-extraction-incomplete`，不得抛异常、迭代object或把非法collection视为空集。

每个canonical fact的disposition还必须与`classifyCapabilityObservationV1(fact.observation)`逐字节相等；caller提供的伪造classification即使拥有自洽`fact_id`也必须RED。canonical world必须闭合fact→artifact、Emit/generated source→traversal及artifact→traversal引用；不存在的引用不得被query静默过滤。
`PublicSignatureExport.export_kind`只允许`pure-type | pure-value | pure-function | capability-type`；前三类映射PureRepresentation，后一类映射CapabilityTypeOnly。未知export kind必须分类为Unknown，禁止由“不是capability-type”默认PureRepresentation。

`J(W)`独立包含每个Fable Emit/emitJsExpr parse unit与每个production generated artifact的全部JavaScript AST node。generic structural enumerator为每个child-index path产生stable node ID；独立closed semantic visitor必须对每个node精确返回`NoCapabilityObservation | EmittedCapabilityObservations(nonempty) | UnknownNodeType`。traversal validator只接受raw AST、scope resolver、visit partition与canonical facts，并从raw AST内部重建完整node universe；caller不得提交node rows，更不得同步删除node、visit与fact伪造自洽子集。每个traversal row必须满足`ast>0`、`visited=ast=no-capability+capability-emitting+unknown`、node keys与visit keys全等且unique、emitted observation ID union与同source进入`C(W)`的JS capability observation集合全等。普通declaration/literal/operator只进入coverage，不伪造capability fact；未知node type必须fail-closed。canonical world同样拒绝`ast_node_count=0`，不得把无证明的空coverage持久化。

visitor必须产出完整closed `JsCapabilityObservationV1`而非只有ID，其中binding provenance只能是`local | imported | free | unresolved`并由scope resolver按node identity提供，禁止按root字符串猜free。traversal validator以`source_kind/source_id/site`包装成canonical `JavaScriptCapability` raw observation，且`generated_artifact_id`当且仅当source kind为generated artifact时存在并等于source ID。`J(W)`的emitted union只能与canonical `C(W)` facts独立投影出的同source observation比较，禁止把visitor自己的IDs回填为expected。未知result case、空emitted集合、malformed payload、空/非法AST、静态computed member、CommonJS import、parameterless `new Date()`及无法证明为local pure call的动态target必须成为精确Unknown或extraction violation，不能退成`NoCapabilityObservation`。

Node/Bun/Browser/ExternalPackage只是runtime标签，不是authority。`RuntimeV1.Node + PureRepresentation`必须GREEN；只有非空authority、非空mutable resource、`CapabilityValue/CapabilityFactory/EffectConstructor`或`Unknown`决定拒绝。`node:path/posix`可由closed rule判纯；ambient `node:path`另判Environment；`node:fs`累积FileSystem，`node:child_process`累积ProcessControl。分类优先级是`Unknown > authority/mutable/value/factory/effect > pure/type-only`，但不得丢弃低优先级标签。

永久反例必须逐项固定 exact code与最窄坐标：`capability-extraction-incomplete`、`capability-observation-missing`、`capability-observation-duplicate`、`capability-fact-id-collision`、`unknown-capability-classification`、`javascript-traversal-missing`、`javascript-traversal-stale`、`javascript-traversal-duplicate`、`javascript-traversal-source-mismatch`、`javascript-ast-node-unvisited`、`javascript-ast-node-duplicate-visit`、`javascript-ast-node-unknown`。不得以“violations非空”、异常文案或一种泛化RED替代。

## STRUCTURED-WORKFLOW-015: generated module 必须绑定唯一 artifact、输入与lineage

repository-generated JavaScript例外只能通过exact `compile-contract-support` relation取得。normative relation必须唯一绑定`consumer_locality/import_specifier/generated_owner/package_import_target/generator/build_invocation/input_selector/runtime_surface_module/laws/determinism_proof`；`laws`必须是singleton且等于`[determinism_proof.what_id]`。observed `GeneratedArtifactRowV1`必须唯一承载`id/artifact_path/artifact_digest/selected_inputs_digest/linkage/javascript_traversal_id`；artifact ID只哈希canonical `{artifact_path,linkage}`，内容变化不换identity，两个digest显式变化。Fable import与`JavaScriptCapability[source_kind=generated-artifact]` fact只引用artifact ID，不复制linkage或digest。

input selector的closed contract只返回filesystem path array；selector boundary必须拒绝root外路径，并把root内路径规范为canonical repository-relative identity。generator、build invocation、selector与selector输出的每个path必须经同一tracking reader读取；reader以raw bytes产生`{path,blob_digest}`，canonical排序、unique后计算`selected_inputs_digest`。generator必须消费这些tracked bytes，禁止selector内部或下游用直接`readFileSync`绕过tracking。artifact output bytes产生`artifact_digest`并接受完整`J(W)` traversal；deterministic只证明bytes由声明输入决定，不能把Date/time/random/network/filesystem/process、mutable global或Unknown降格为pure。

relation、artifact、traversal、actual import、lineage、runtime Surface、proof observation与ephemeral traversal-observation set都必须是exact closed row；未知key、缺key、非法digest/path/count/identity或空AST统一得到`generated-module-observed-evidence-invalid`。`TraversalObservationSetV1`只允许`traversal_id/emitted_observation_ids`，由同一次production traversal validation返回的`emitted_observation_ids`形成；它不进入canonical world、不拥有授权力，M6.3b extractor不得另造scanner或从fact反推该集合。每个artifact traversal必须恰有一行，集合精确等于该artifact的canonical JavaScript capability fact observation ID；删除全部JS facts、同步伪造空集合或`unknown_node_count>0`都必须RED。

relation-specific validator必须同时证明package import target、actual imported member、generator/build/selector entry、artifact linkage、output/input digest、traversal、determinism proof owner/law及runtime Surface callback。永久反例逐项固定：`missing-generated-module-relation`、`stale-generated-module-relation`、`duplicate-generated-module-relation`、`duplicate-generated-module-semantic-key`、`generated-module-specifier-mismatch`、`generated-module-target-mismatch`、`generated-module-member-mismatch`、`generated-module-lineage-missing`、`generated-module-lineage-duplicate`、`generated-module-lineage-mismatch`、`generated-module-lineage-stale`、`generated-module-nondeterministic`、`generated-module-proof-duplicate`、`generated-module-proof-stale`、`generated-module-determinism-proof-owner-mismatch`、`generated-module-determinism-proof-law-mismatch`、`generated-module-determinism-proof-mismatch`、`generated-module-runtime-surface-missing`、`generated-module-runtime-surface-duplicate`、`generated-module-runtime-surface-stale`、`generated-module-runtime-surface-callback-mismatch`、`generated-module-physical-authority`、`generated-module-observed-evidence-invalid`、`generated-module-observed-evidence-duplicate`、`generated-artifact-missing`、`generated-artifact-stale`、`generated-artifact-duplicate`、`generated-artifact-reference-missing`、`generated-artifact-reference-stale`、`generated-artifact-linkage-mismatch`、`generated-artifact-digest-mismatch`、`generated-artifact-inputs-digest-mismatch`、`javascript-traversal-observation-set-missing`、`javascript-traversal-observation-set-duplicate`、`javascript-traversal-observation-set-stale`、`javascript-traversal-source-mismatch`、`javascript-ast-node-unknown`。单独增加`RuntimeV1.Node`标签必须保持GREEN。

上述证据必须是closed observed rows并与canonical fact、test registry及runtime Surface交叉验证；execution lineage以directed entry edges证明build→generator→selector可达，proof callback同时触达generator entry与runtime Surface，不要求回调执行build。任意字符串ID集合、并列entry列表或`artifact_id + disposition`镜像不得取得证明力。同consumer的额外relation、不同ID的相同semantic key、member/callback/lineage/proof owner漂移必须分别RED。

## STRUCTURED-WORKFLOW-016: M6.4 cutover 只验证同一 staged input

`resolveCutoverInputClosureV1`只从真实入口、repository-local transitive imports、input selector实际输出与tracking reader实际读取构造closed input set，不按扩展名猜测。owner/aggregate project及compile/signature/reference/props、semantic-owner、v2 manifest、WHAT/HOW/Surface/proof、analyzer/build/generator/selector、package/tool manifest与lock均必须由真实依赖进入closure。任何实际读取不在closure、dynamic local import、root外/duplicate/无法解析selector path必须得到`cutover-input-closure-incomplete`。

cutover closure中的每个repository input必须精确命中`git ls-files --stage -z`的一个stage-0 tracked entry；拒绝untracked、ignored、unmerged、仅存在working tree、unstaged tracked change、gitlink与非法mode。generator/build/selector及每个selected input只允许regular blob；唯一build-output exception必须由artifact row承接且不得成为selected input。object format必须来自`git rev-parse --show-object-format`；`CanonicalInputIndexRowV1={path,mode,blob_oid}`排除worksheet与formal snapshot后canonical排序，digest使用`cutover-input-index/v1\u0000` domain。scan前后都必须逐项证明working-tree bytes等于index blob并重建相同closure；正式snapshot写入stage、worksheet staged删除后，排除二者的index digest必须不变。

semantic import closure的每个待遍历path必须有exact scan-result row；缺行不是“无依赖”。selector结果必须是closed path array，null、scalar、duplicate或非法path一律RED。selected-input row、index row、exclusion与build-output exception均使用closed schema且只能从同一tracked input projection派生，caller不得提供第二份自洽但虚假的closure。

`validateCutoverInputStateV1`只接受exact raw `closure_input/index_entries/object_format/index_blob_bytes_by_path/working_tree_bytes_by_path`，并在函数内部调用`resolveCutoverInputClosureV1`；不得接受caller构造的`closure`、第二份`build_output_paths`或任意`excluded_paths`。唯一排除项固定为formal snapshot与migration worksheet两个协议路径；任何caller试图排除`src/**`或其他tracked path都因state schema非法而RED。所有path/index collection必须是指定Array或Map，index row exact为`{path,mode,stage,blob_oid,object_type}`；全index任一`stage != 0`均得到`unmerged-index-entry`，不能因不在closure而忽略。canonical index只接受`100644/100755`regular blob，`120000` symlink与unsupported object format在OID判断前fail-closed。selector selected input与内部推导出的build output交集必须得到`selected-input-build-output-overlap`。

M6.3b worksheet只有closed migration-only schema，不含digest、manifest claim或授权力，并在M6.4删除。M6.3c formal snapshot只冻结一次cutover审计：record locality集合精确等于live candidates，world/query/index digest、terminal classifier、manifest claim IDs、WHAT与proof全部绑定同一world。record validator必须使用closed code：`adjudication-record-missing/unexpected/duplicate/locality-mismatch`、`adjudication-fact-schema-mismatch`、`adjudication-world-digest-mismatch`、`adjudication-index-digest-mismatch`、`adjudication-query-digest-mismatch`、`adjudication-target-mismatch`、`adjudication-manifest-claim-missing/stale`、`adjudication-proof-missing/orphan/invalid`。M6.4提交后snapshot永久冻结且release gate不得读取或与live world比较；live authority只能来自当前manifest与fresh analyzer。M6.4前report-only parser/analyzer可以提交，但不得阻断release或与旧gate形成双重权威。
