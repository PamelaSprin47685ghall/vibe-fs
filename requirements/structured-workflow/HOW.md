# structured-workflow — HOW

`WHAT.md` 是唯一 normative 合同。本文只记录当前实现入口与迁移方法。

## 1. 当前结构入口

- `scripts/checks/subsystems.json`：迁移期唯一 legacy-owner → subsystem 映射。它只帮助尚未显式迁移的 shard 解析 subsystem，不定义 consumer ACL、exposure 或业务 law。
- `scripts/lib/compile-shards.mjs`：纯构建层。读取 fsproj、source、sibling `.fsi`、ProjectReference 与 aggregate union；不知道业务 owner/slice/exposure。
- `scripts/checks/subsystems.mjs`：唯一 subsystem 结构 release gate。验证 source 唯一归属、compile-shard DAG、aggregate 等价与显式 runtime-platform shard 的依赖倒置，并报告 subsystem SCC。
- `scripts/check.mjs`：只运行 `subsystems.mjs` 作为 source/subsystem/shard 的架构权威；旧 semantic-owner/owner-contract/owner-project gate 不再进入 release verdict。

旧 `WanxiangshuSemanticOwner`、`WanxiangshuOwnerLocality` 与 `WanxiangshuOwnerLocalityKind` 暂留在 fsproj，供未迁工具和历史测试读取。新 shard 同时声明 `WanxiangshuSubsystem`、`WanxiangshuCompileShard`；迁移结束后可机械删除旧字段，但删除本身不是架构收益。

## 2. 拆 compile shard 的固定方法

先按知识而不是文件大小分 cohort：

1. 列出当前 shard 的公开类型、纯 decision、effect/factory、runtime registry、codec 与测试 surface。
2. 按真实 consumer 需要分组；两组若 reason-to-change 不同或 audience 长期不同，拆 shard。
3. 最底层通用 primitive 必须无领域依赖；若需要领域 identity，说明它不是 platform primitive。
4. consumer 直接引用最窄 shard。禁止保留 umbrella reference 作为“保险”；编译失败用于发现遗漏依赖。
5. `.fs` 与 sibling `.fsi` 同迁，aggregate 顺序不变；full build 输入并集不变。
6. 用定向真实 Fable compile 验证 ProjectReference closure，而不是 aggregate 成功或源码 grep 冒充编译证明。

第一批样本：

```text
dispatch/identity            <- Foundation/Identity
chat-execution/outcome       <- Foundation/Outcome + OutcomeSurface
dispatch/runtime-nudge       <- Interaction/Dispatch/Nudge
dispatch/prompt-metadata     <- MetadataCodec
runtime-platform/canonical   <- CanonicalJson + surface

runtime-platform/task-result <- TaskResult + FsToolkit compatibility
runtime-platform/parallel    <- bounded Parallel + surface
runtime-platform/async       <- AsyncSupport
delegation/fission-facts     <- Execution/Fission/Facts
```

`runtime-platform/*` 明确不依赖 domain subsystem。Fission facts 因为携带业务 identity，归 Delegation 而不是为了复用留在 TaskResult 大包。

## 3. subsystem 粒度与后续收敛

调整 subsystem 只看 semantic cohesion、历史 co-change、dependency direction、rewrite independence 与 boundary tax。compile shard 数量不参与 subsystem 数量裁决；下面的观测数量不是 quota。

Subsystem SCC 是首要结构债：双向依赖必须通过移动知识所有权、提取窄纯 contract、capability injection 或删除重复 decision 消解。严禁以 facade、service locator、event bus、shared DTO bucket 隐藏环。

后续优先继续处理 reverse closure 最大的公共重力井：`foundation-roles`、剩余 Identity cohorts、provider projection model，以及仍同时承载多种 reason-to-change 的 Host/Delegation/Persistence shard。

### 3.1 观测基线

2026-09-08，在上游提交 `58aa3d934` 运行 `node scripts/checks/subsystems.mjs`：26 subsystem、199 compile shards、702 production sources、1919 ProjectReference，shard DAG，最大 subsystem SCC 为 23 个节点。口径是 fsproj 声明及其聚合图，不是源码实际调用图或编译耗时测量。后续报告附对应代码版本和工作区差异，不把本次快照写成固定测试期望。

### 3.2 GAP-033 当前缺口与关闭证据

本节是 STRUCTURED-WORKFLOW-011 至 016 的实现与证明缺口记录，不新增产品条款，也不构成整表开工授权。仅处理用户任务明确覆盖的部分；相关 Worker 应显式读取本节，不依赖路径触发的自动接地。聚合状态见 `requirements/GAP.md` 的 GAP-033，保持 PARTIAL；单项修复不代表整体已可独立替换。

第 3.1 节保留历史测量口径；以下逐项记录当前实现和仍缺的证据，不用历史数量约束后续合法变化。

1. **知识依赖尚未收敛。** subsystem 聚合图仍有 SCC，公共重力井仍待拆解。按真实 consumer 收窄 contract／port，记录被切断的具体知识依赖与前后闭包；对受影响 shard 和 consumer 提供真实 focused Fable 编译及相关行为证明。SCC 数字下降不能独自关闭整体缺口。
2. **结构快照已替换为性质反例。** `subsystem-boundaries.test.mjs` 与 `owner-project-boundaries.test.mjs` 不再限制 subsystem 数量范围或要求 shard 多于 subsystem。前者通过真实临时 fsproj 验证合法增长、缺失／重复 source 归属、sibling 签名与 aggregate 输入完整性；`A1(alpha) → B1(beta) → A2(alpha)` 是合法 shard DAG，但必须准确报告 subsystem SCC，单向对照则无 SCC。测试标题与第 5 节的精确证明边保持不变。
3. **平台解析路径已有正反例，知识归属审计仍未完成。** `scripts/checks/subsystems.mjs` 已按解析后的 `subsystem` 检查全部平台 shard；`subsystem-boundaries.test.mjs` 对 explicit 与 legacy 映射路径分别证明平台依赖合法、域外依赖拒绝。它不证明所有标成平台的源码都没有领域知识，也不替代真实 focused compile；不得据此宣布整个平台隔离已完成。
4. **authority 源码身份已与 WHAT 包归属分离。** `authority-boundary.mjs` 通过 `buildSubsystemInventory` 取得唯一源码 subsystem；manifest 的 declaration、method、issuer `owner` 全部按实际文件归属迁移，`whatOwners` 继续指向 requirement package。legacy 字段只参与既有映射解析，不再作为第二个可接受身份；`scanRepo` 使用传入 repository root。`requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-014] explicit and legacy-mapped subsystems resolve through the real repository path` 用真实临时工程证明无 legacy 字段与 legacy 映射的合法路径，并拒绝旧 alias、错误源归属及错误 WHAT 包归属。能力发行、一次性消费和持久化检查没有放宽。

2026-09-09，在 merge commit `2c6088fa8` 上叠加本批修改：相关结构、impact 与 authority 测试 40/40 通过；补全实际 consumer 后，subsystem gate 报 26 subsystem、207 shard、702 source、1874 references、最大 SCC 20。相比本批中途的 18，SCC 增长来自显式恢复 Host→Casebook 等已存在的代码依赖，不能把先前漏报当作隔离成果，也不能宣布本 GAP 已关闭。authority cutover 的临时对照使用同一迁移后 manifest，旧 gate 得到 59 个错误，新 gate 零错误；临时模块已删除。

随后恢复 `ToolRuntimeScope → change-integration.git-integrationgate` 已被动态加载隐藏的实际依赖，静态构造 Host 并保留 callback 与 snapshot 类型。scope shard 的 942-source focused Fable compile 通过；subsystem gate 更新为 1875 references，其余上述计数不变，shard 图仍无环。这修复了真实 Long Stroke 首次 publication 的异步调用错误，不代表 subsystem 知识耦合已经消除。

资源校验接缝恢复静态 `EnforcerCatalog.validate` 后，`Wanxiangshu.Owner.cognitive-environment.resources-promptsurface.fsproj` 的真实 focused Fable compile 通过（90 source）；新产物中英文各装载 120 条，移走 scratch 校验器模块时新 Node 进程以 `ERR_MODULE_NOT_FOUND` 失败，随后恢复并清理临时资源链接。随后修复 `enforcer-codec` consumer 的真实闭包：删除无用 namespace 引入，恢复纯 `EnforcementProjection` 静态依赖与 root-workspace contract；将 `SessionNudge`／repair port 从 ingress 大分片移到 `dispatch/session-nudge`，使 Companion 无需反向引用包含自身的 ingress 闭包。原 516-source 编译失败已在 522-source focused Fable compile 中通过；源码总数与 aggregate 顺序不变，没有用动态加载回捞依赖。合入的格式问题由正式 Fantomas 工具修正，整体验收仍使用 `npm run format-build-test`，局部编译不代替该入口。

独立编译原 ingress consumer 又暴露了被 enforcer-codec 根项目遮住的 Companion→repair 依赖。`enforcer/repair` 现单独编译既有 codec、cycle model/decode、repair 和 Blogger probe，Companion 与 enforcer-codec 均静态引用它，不再依赖偶然被聚合根纳入的源码；Host signal adapter 也显式引用实际调用的 `ToolResultBound` 窄合同。`Wanxiangshu.Owner.dispatch-protocol.interaction-dispatch-opencode-ingresscodec.fsproj` 的 564-source focused compile 已通过。这些修复恢复真实声明依赖，references 增长是如实表达知识，不是架构退步的自动判据。

`LanguageSurface` 的 Bookkeeper 资源读取仍需要 `PromptResources`，因此恢复其静态资源引用；真实 Host transform 从 bootstrap 分片移到 `host/provider-system-transform`，由语言验证 Surface 与 bootstrap 共用，公开 API 和源码路径不变。bootstrap 对 `BookkeeperRuntime`／`CasebookLifecycle` 的实际调用分别通过现有 casebook-model／casebook-bookkeeper 分片声明，不再依靠 aggregate 偶然补齐。语言 Surface 与原 bootstrap consumer 各使用自己的 focused compile 入口验证，不能仅用全量构建证明这两条闭包。

旧 M6 计划仅用于追溯退役原因；其 extractor、worksheet、ACL 与 snapshot 清单不形成关闭本节缺口的条件。ENF-015、ENF-016 等独立产品证明缺口仍由所属包 HOW 记录，不因 GAP-031 路线退役或本节记录完成而关闭。

### 3.3 语义词汇与证明义务注册

此表保留既有业务词汇 proof edge。第二列中的旧模块身份只用于定位已有源码，不恢复 owner 作为治理粒度。

| 词汇 | subsystem / legacy module / path | WHAT law | 允许的 trace relation | executable proof |
|---|---|---|---|---|
| `ManagerWorkflow.observe` | relay / Mission.Manager / Mission/Manager/Workflow.fs | `STRUCTURED-WORKFLOW-007` | one admission → one settled/no-effect outcome | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ManagerWorkflow.observeIdle` | relay / Mission.Manager / Mission/Manager/Workflow.fs | `STRUCTURED-WORKFLOW-007` | one idle observation → at most one encouragement | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `FallbackLedger.recordAuthorizedFailure` | provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Ledger.fs | `STRUCTURED-WORKFLOW-007` | one policy licence + duplicate observation → one durable cursor advance | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Workflow.fs | `STRUCTURED-WORKFLOW-008` | `R_fallback`: confirmed failure → bounded ordinary CE re-entry | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| `OrchestratorProgram.run` | change / Change / Change/Program.fs | `STRUCTURED-WORKFLOW-008` | `R_publish`: finite retry → one accepted or typed failed result | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |

### 3.3.1 词汇约束

这些 proof 约束业务 trace；subsystem 迁移不得通过改变测试标题、降低 multiplicity 或放宽 failure/cancel 语义取得绿色。

## 4. 编译与验证

日常结构验证：

```sh
node scripts/checks/subsystems.mjs
node --test requirements/structured-workflow/tests/subsystem-boundaries.test.mjs
```

涉及 shard ProjectReference 或 `.fsi` 时，先跑对应定向 compiler-boundary/impact tests，再运行：

```sh
node scripts/check.mjs
node scripts/build.mjs
```

禁止 `dotnet build`。aggregate full build 只证明最终完整输入可编译；缺失 ProjectReference 必须由定向 shard compile/canary 发现。

## 5. proof map

| WHAT | 当前 proof |
|---|---|
| STRUCTURED-WORKFLOW-001 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-001] FLOW_001_direct_task_workflow_is_allowed` |
| STRUCTURED-WORKFLOW-002 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-002] FLOW_006_second_runtime_patterns_are_rejected` |
| STRUCTURED-WORKFLOW-003 | `requirements/structured-workflow/tests/workflow-surface.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names` |
| STRUCTURED-WORKFLOW-004 | `requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs::WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision` |
| STRUCTURED-WORKFLOW-005 | `requirements/structured-workflow/tests/dsl-ownership.test.mjs::WHAT[STRUCTURED-WORKFLOW-005] DSL_OWNERSHIP_negative_mutable_goes_red` |
| STRUCTURED-WORKFLOW-006 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-006] FLOW_017_composition_keeps_domain_results_and_rejects_child_program_counters` |
| STRUCTURED-WORKFLOW-007 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| STRUCTURED-WORKFLOW-008 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary` |
| STRUCTURED-WORKFLOW-009 | `requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-009] operator abort is a control-plane wake, never a business outcome` |
| STRUCTURED-WORKFLOW-010 | `requirements/structured-workflow/tests/parallel.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order` |
| STRUCTURED-WORKFLOW-011 | `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] subsystem is the only semantic governance identity`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] subsystem ownership and compile-shard graph are complete and acyclic` |
| STRUCTURED-WORKFLOW-012 | `requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] implementation changes exclude reverse consumers`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] flat Fable projection planner produces exact closure and canonical aggregate order` |
| STRUCTURED-WORKFLOW-013 | `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-013] reusable platform shards depend on no domain subsystem`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-013] GitGateway exposes a narrow dependency-inverted compiler boundary` |
| STRUCTURED-WORKFLOW-014 | `requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-014] NodeFs physical port and tool contracts have isolated compiler boundaries` |
| STRUCTURED-WORKFLOW-015 | `requirements/structured-workflow/tests/generated-module-relation.test.mjs::WHAT[STRUCTURED-WORKFLOW-015] generated artifact binds tracked inputs bytes lineage traversal and import` |
| STRUCTURED-WORKFLOW-016 | `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-016] release architecture has one subsystem authority` |
