# structured-workflow — HOW

`WHAT.md` 是唯一 normative 合同。本文仅说明架构治理、分片机制、数据流转与端口实现的当前实现决策。

## 1. 架构模型与治理入口

- `scripts/checks/subsystems.json`：定义 legacy-owner → subsystem 映射。它帮助尚未显式声明的 compile shard 解析 subsystem，不定义 consumer ACL、exposure 或业务 law。
- `scripts/lib/compile-shards.mjs`：纯构建层。读取 fsproj、source、sibling `.fsi`、ProjectReference 与 aggregate union；不知道业务 owner/slice/exposure。
- `scripts/checks/subsystems.mjs`：唯一 subsystem 结构 release gate。验证 source 唯一归属、compile-shard DAG、aggregate 等价与显式 runtime-platform shard 的依赖倒置，并报告 subsystem SCC。
- `scripts/check.mjs`：运行 `subsystems.mjs` 作为 source/subsystem/shard 的架构权威。

系统以 subsystem 为唯一架构治理粒度，compile shard 显式声明 `WanxiangshuSubsystem` 与 `WanxiangshuCompileShard`。

## 2. 拆 compile shard 的固定方法

按知识内聚而不是文件大小分 cohort：

1. 列出当前 shard 的公开类型、纯 decision、effect/factory、runtime registry、codec 与测试 surface。
2. 按真实 consumer 需要分组；两组若 reason-to-change 不同或 audience 长期不同，拆 shard。
3. 最底层通用 primitive 必须无领域依赖；若需要领域 identity，说明它不是 platform primitive。
4. consumer 直接引用最窄 shard。禁止保留 umbrella reference 作为“保险”；编译失败用于发现遗漏依赖。
5. `.fs` 与 sibling `.fsi` 同迁，aggregate 顺序不变；full build 输入并集不变。
6. 用定向真实 Fable compile 验证 ProjectReference closure，而不是 aggregate 成功或源码 grep 冒充编译证明。

典型分片结构：

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

`runtime-platform/*` 明确不依赖 domain subsystem。Fission facts 归属于 `delegation`。

## 3. 子系统分层架构与两层装配模型

系统整体遵循严格单向依赖分层：

```text
runtime-platform / participant
  ↓
resources / requirements / persistence / session-lifecycle
  ↓
provider / relay / process / repository-programming / sphinx
  ↓
authority → context → enforcer / work → chat-execution → delegation / knowledge / strength
  ↓
change → interaction / output → dispatch
  ↓
host / durable-composition
  ↓
application-composition
  ↓
verification
```

1. **`durable-composition` 承载持久化装配**：
   - 集中拥有所有跨域持久化事实组合、投影桥接（如 `DomainFamilyBridge`、`DelegationProjectionBridge`）与端口适配（`AgentJournalPortAdapter` 等）。
   - 持久化内核（`durable-events`）仅保留规范编解码、进程日志、Store 与合并归并等纯机制，不包含业务领域事实的外层包装。
2. **`application-composition` 承载生命周期与工具装配**：
   - 集中管理真实宿主环境与插件生命周期装配（`PluginHost`、`PluginHooks`、`PluginRuntimeScope`、`PluginTransforms`、`ToolRegistry` 等）。
   - 业务领域通过注入的窄能力端口（如 `TerminalPolicyPort`、`TurnObservationJournalPort` 等）操作，不直接依赖装配层或全局宿主容器。
3. **领域层零反向依赖**：
   - 业务领域分片不反向引用 `durable-composition` 或 `application-composition`，子系统依赖图为单向严格无环 DAG（`cyclicComponents = []`）。

## 4. 事实折叠与聚合解耦

1. **单字段事实族 fold 移交聚合投影**：
   - 单字段事实族（`change`、`fission`、`concern`、`attention`、`institutional-learning`）的领域 fold 仅负责本切片状态投影，返回切片变更或拒绝。
   - 写回聚合及渲染 fail-closed 报告由 `durable-composition`（如 `ProjectionUpdate` 与 `Fold.foldAgentFact`）独占承担，满足 `delegation-029` 与 `durable-events-023` 的单一装配点约束。
2. **多切片事实族 fold 移交聚合写入**：
   - 多切片事实族（`interaction-authority`、`provider-attempt-recovery`、`context-compression`）的领域 fold 仅接收窄查询函数与本切片状态，返回封闭变更列表与闭合拒绝。
   - `DomainFamilyBridge` 是唯一把变更写回聚合、把领域拒绝渲染为 spine `FoldRejection` 的桥接模块。
3. **Blogger 事实折叠与前缀吸收策略**：
   - `ContextFactFold` 接收窄查询并返回六种 `ContextProjectionChange` 与闭合 `ContextFoldRejection`。
   - 前缀观测的吸收策略统一由 `PrefixEpochProjection.describe` 提供，与 context fold 共用同一判定。

## 5. 核心组件窄端口契约实现

1. **`Wire.fs` 端口化**：
   - 抽象 `WireJournalPort` 封装切片快照读取与事实追加，业务层不再直接感知 `AgentJournal` 或全量 `ProjectionSet`。
   - 快照读取通过 `ReadView` 保证同一 revision 的单次快照一致性。
2. **`Orchestrator` 端口化**：
   - 通过 `OrchestratorSweepPort` 与 `OrchestratorRelayPort` 解耦任务超时扫描与 Relay 结果交互，由 `OrchestratorJournalAdapter` 在 composition 层提供适配。
3. **`OrdinaryTurnWorkflow` 端口化**：
   - 观察、终结策略、Join 守卫与追踪记录分别解耦为 `TurnObservationJournalPort`、`TerminalPolicyPort`、`HostJoinGuardJournalPort`、`TerminalTracePort`，每个调用点获取独立 live 快照读取。
4. **交互工具事实端口化**：
   - `Attention`、`Concern`、`InstitutionalLearning` 等工具分别通过 `AttentionJournalPort`、`ConcernJournalPort`、`InstitutionalLearningJournalPort` 接入持久化，工具实现与全量 journal 解耦。
   - `Delegation` 运行时改用自有契约分片 `delegation-journal-port`（声明 `AgentJournalPort`）与 `delegation-pty-port`（声明 `DelegationPtyCapability`），PTY 适配器不再直接穿透 `HostForkRuntime`，满足 `delegation-028` 与 `delegation-029`。
5. **Sphinx 服务入口解耦**：
   - 服务进程入口（`ServeEntry`）与 durable store 组装交由 composition 分片承担，规则分片专注于领域推理，满足 `epistemic-reasoning-019` 与 `epistemic-reasoning-030`。
6. **平台原语与领域知识分离**：
   - 字符串摘要下沉为独立的 `runtime-platform/digest`，不包含 OpenCode 或领域类型，满足 `host-boundary-019`。
   - glob 匹配原语下沉为独立的 `runtime-platform/glob`，不依赖业务领域分片。
   - 领域事件名分别收归各领域的 `EventVocabulary` 契约分片（如 `CasebookEventTypes`、`JsTransactionEventTypes`），满足 `durable-events-022`。

## 6. 语义词汇与证明义务注册

### 3.3 语义词汇与证明义务注册

此表保留既有业务词汇 proof edge。第二列中的旧模块身份只用于定位已有源码，不恢复 owner 作为治理粒度。

| 词汇 | subsystem / legacy module / path | WHAT law | 允许的 trace relation | executable proof |
|---|---|---|---|---|
| `ManagerWorkflow.observe` | relay / Mission.Manager / Mission/Manager/Workflow.fs | `structured-workflow-007` | one admission → one settled/no-effect outcome | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ManagerWorkflow.observeIdle` | relay / Mission.Manager / Mission/Manager/Workflow.fs | `structured-workflow-007` | one idle observation → at most one encouragement | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `FallbackLedger.recordAuthorizedFailure` | provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Ledger.fs | `structured-workflow-007` | one policy licence + duplicate observation → one durable cursor advance | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Workflow.fs | `structured-workflow-008` | `R_fallback`: confirmed failure → bounded ordinary CE re-entry | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary` |
| `OrchestratorProgram.run` | change / Change / Change/Program.fs | `structured-workflow-008` | `R_publish`: finite retry → one accepted or typed failed result | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary` |

### 3.3.1 词汇约束

这些 proof 约束业务 trace；subsystem 迁移不得通过改变测试标题、降低 multiplicity 或放宽 failure/cancel 语义取得绿色。
