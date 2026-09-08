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

以下事实核对于第 3.1 节基线；关闭依据是后续工程工作的证明要求，本次文档记录没有修复这些实现或测试。

1. **知识依赖尚未收敛。** subsystem 聚合图仍有 SCC，公共重力井仍待拆解。按真实 consumer 收窄 contract／port，记录被切断的具体知识依赖与前后闭包；对受影响 shard 和 consumer 提供真实 focused Fable 编译及相关行为证明。SCC 数字下降不能独自关闭整体缺口。
2. **结构测试锁住迁移快照。** `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] subsystem is the only semantic governance identity` 固定 `sourceCount = 702`、`subsystemCount = 26`，并要求最大 SCC 长度大于 1。正常源码增长或最终消环会使快照断言失败。以 source 唯一归属、编译输入完整性和 SCC 报告的性质替换：合法增长及无环图应通过，缺失／重复归属应拒绝，有环图应准确报告。保留有效 WHAT 标签与第 5 节的精确证明边，不仅删除断言。
3. **平台检查覆盖不完整。** `scripts/checks/subsystems.mjs` 的平台域外依赖拒绝只覆盖 `explicitSubsystem === 'runtime-platform'` 且存在 `explicitCompileShard` 的项目；同套测试中的平台用例使用相同过滤。由 legacy 映射解析的平台 shard 尚未被这条检查完整覆盖。先区分真正平台原语与含领域知识的 shard，迁回错误归属，再覆盖所有解析为平台的 shard；显式与 legacy 映射路径都要有合法正例及域外依赖反例，不以补标签或整类豁免代替边界迁移。
4. **旧身份仍被 authority gate 消费。** `scripts/checks/authority-boundary.mjs` 的 `authorityRegistry()` 仍优先取 `legacyOwner`，`owners` 集合也只加入旧身份。这里只确认读取逻辑，尚未完成整道 authority gate 的语义审计。先读 manifest schema 与全部消费者，区分源码 subsystem 归属和 requirement package 的命题／证明归属，再迁移对应解析与消费者；无 legacy 字段的合法项目应通过，错误归属仍应拒绝。保留有效能力检查，不批量把所有 owner 改名为 subsystem 或关闭 gate。

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
