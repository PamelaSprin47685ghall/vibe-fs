# managed-chat-execution — HOW

## 架构机制

### 1. Durable projection

`ManagedChatExecutionProjection` 以 `(SessionId, PhysicalUserMessageId)` 分区，纯折叠 versioned facts：

```text
None
  └─ Accepted
       ├─ ProviderStarted ─ Terminal(disposition)
       └─ Terminal(disposition)        # pre-provider settlement
```

重复等值 fact 不改变投影；越级、冲突 identity、terminal 后启动及不同 terminal 竞争返回 typed conflict，绝不修补历史。事件 codec 与 upgrader 只处理 schema 演化，不读取时钟或环境。

### 2. Admission transaction

Host `chat.message` 先从 `interaction-authority` 取得 exact `AttemptExecutionProfile`。该 profile 必须携带上游以一次原子持久化产生的 durable root witness：root 的完整版本化 `ParticipantIdentityEvidence` 与对应 `AuthorityRootAccepted` 不可拆分、不可孤立提交。Host 校验该 witness 的 LogicalRunId 属于当前 durable run，再以 exact physical identity 与完整原子 profile 追加 `Accepted`；确认 durable 后才向 `execution-model-routing` 获取 exact capacity，建立 message-keyed binding，将选定 target 与 evidence 的逐字段只读投影送入 Host mutable message。任何组件不得缓存或从 agent/model/session 重新推导 Role、initial Tier、Persona、Peer。`messages.transform` 只冻结 user-bounded pending plan；公开 exact assistant `message.updated` 首次暴露 `(sessionID,parentID,id,role=assistant,time.created)` 后才绑定 run 并追加 `ProviderStarted`。terminal 同事件必须等待 start 持久确认。每个 effect 的 capability 只携 exact key，不能降格为 `SessionId`。

### 3. Settlement 与 failure policy

Host success evidence、logical cancel 与 session delete 提供确定外部 evidence；其他失败先交由 `execution-failure-policy` 产生 closed typed disposition command。`managed-chat-execution` 校验并单赋值追加 terminal fact。若 provider 尚未启动，terminal durable 后直接精确归还已绑定容量；若已经启动，则按 Host terminal evidence 与 typed policy 收敛，free-form diagnostic 不参与分支。

### 4. Activation 与 recovery

plugin construction 只组装 ports。durable store 激活后，Task29 runtime owner 把 canonical `ChatExecutionState` 与公开 Host physical observation 转成 `ChatExecutionRecoveryEvidence`，调用唯一纯 `ChatExecutionRecovery.decide`，再解释其 exact request。Surface 仅转换测试 representation，不镜像 decision table。每次事件重入同一 admission/settlement interpreter；等待仅存在于进程内，停止时丢弃，重启时从 durable facts 新建。cancel/delete 使用 projection 列出的 exact keys 驱动 settlement barrier，不设 timer 或 polling loop。

### 5. Incident evidence

`tests/support/incident-evidence.mjs` 是 read-only evidence adapter。v1 envelope 只收 owner surfaces 的 canonical fact/projection/status、capacity snapshot/reconciliation、causal diagnostic projection、Host canary contract 与 typed recovery observation/runtime decision；SHA-256 覆盖确定 canonical JSON。capture 与 replay 均拒绝未知或缺失字段。Replay 重跑 `Surface.fold`、`StatusSurface.queryFacts`、`ModelRoutingSurface.reconcileCapacityEvidence`、`ReliabilityDiagnosticsSurface.projectRecord` 与 `RecoveryRuntimeSurface.recoverScenarios`，只输出 `EffectRequestOnly` owner action 和空 mutation list。

Schema：`tests/fixtures/incident-evidence-v1.schema.json`。操作流程：`OPERATOR-RUNBOOK.md`。Host canary 当前只证明 duplicate delivery 被 Host deduplicate，未证明 exact accepted-message replay API；缺失该能力时必须升级，不能重发。

## 未闭合验证边界

`admissionCrashPointScenarios` 的 recovery decision 与 port invocation 走 production runtime；A–I 的 durable prefix 另由 production transaction/lifecycle Surface 验证。原先按 cut label 构造的 `beforeRestart` / `afterRestart` snapshot 与对应假测试已删除，因为它们未执行真实 runtime dispose、同一 durable backing reopen 与 public snapshot。现有 CHATEXEC-009 proof 只闭合 canonical durable fact 不包含 process-local artifact。完整闭合需要最窄 Host/runtime canary：注入 cut → 关闭 physical scope → 复用 durable journal 新建 scope → 读取正式 snapshot；不得把 label table 改写成另一套模拟器。

## DEPENDS ON

- `durable-events`
- `interaction-authority`
- `participant-identity`
- `execution-model-routing`
- `execution-failure-policy`
- `host-boundary`

## 计划验证与测试落点

| 命题 | 计划 executable proof |
|---|---|
| CHATEXEC-001 | `requirements/managed-chat-execution/tests/chat-execution-facts.test.mjs::WHAT[CHATEXEC-001] exact key indexes two physical messages within one session` |
| CHATEXEC-002 | `requirements/managed-chat-execution/tests/facts-roundtrip.test.mjs::WHAT[CHATEXEC-002] schema v1 Accepted ProviderStarted and Terminal round-trip canonically`；`requirements/managed-chat-execution/tests/facts-roundtrip.test.mjs::WHAT[CHATEXEC-002] unknown schema version fails closed during production fold` |
| CHATEXEC-003 | `requirements/managed-chat-execution/tests/admission-transaction.test.mjs::WHAT[CHATEXEC-003] managed admission has one fixed success order`；`requirements/managed-chat-execution/tests/admission-transaction.test.mjs::WHAT[CHATEXEC-003] append failure performs zero downstream effects`；`requirements/managed-chat-execution/tests/bootstrap-single-owner.test.mjs::WHAT[CHATEXEC-003] managed path calls one admission transaction`；`requirements/managed-chat-execution/tests/bootstrap-single-owner.test.mjs::WHAT[CHATEXEC-003] acceptance uncertainty, acquire, bind, and Host projection failures stop before provider` |
| CHATEXEC-004 | `requirements/managed-chat-execution/tests/acceptance-idempotence.test.mjs::WHAT[CHATEXEC-004] exact duplicate reconstructs an equivalent witness without another append`；`requirements/managed-chat-execution/tests/acceptance-idempotence.test.mjs::WHAT[CHATEXEC-004] established evidence conflict is typed and appends nothing`；`requirements/managed-chat-execution/tests/chat-execution-facts.test.mjs::WHAT[CHATEXEC-004] identical Accepted replay is idempotent and conflicting evidence fails closed`；`requirements/managed-chat-execution/tests/admission-decision-table.test.mjs::WHAT[CHATEXEC-004] pure admission decision rejects conflicting exact evidence` |
| CHATEXEC-005 | `requirements/managed-chat-execution/tests/provider-start-terminal-facts.test.mjs::WHAT[CHATEXEC-005] ProviderStarted before Accepted rejects`；`requirements/managed-chat-execution/tests/provider-start-terminal-facts.test.mjs::WHAT[CHATEXEC-005] equal start and terminal duplicates are semantic no-ops`；`requirements/managed-chat-execution/tests/provider-terminal-accounting.test.mjs::WHAT[CHATEXEC-005] exact public assistant observation alone establishes provider start`；`requirements/managed-chat-execution/tests/chat-execution-facts.test.mjs::WHAT[CHATEXEC-005] ProviderStarted enforces acceptance provider run and terminal fences`；`requirements/host-boundary/tests/ordered-transform.test.mjs::WHAT[CHATEXEC-005] user-only transform cannot manufacture ProviderStarted from public Host evidence` |
| CHATEXEC-006 | `requirements/managed-chat-execution/tests/provider-start-terminal-facts.test.mjs::WHAT[CHATEXEC-006] provider terminal before ProviderStarted rejects`；`requirements/managed-chat-execution/tests/provider-start-terminal-facts.test.mjs::WHAT[CHATEXEC-006] conflicting terminal rejects without a second write`；`requirements/managed-chat-execution/tests/provider-terminal-accounting.test.mjs::WHAT[CHATEXEC-006] production Host terminal owner persists before exact capacity settlement`；`requirements/managed-chat-execution/tests/provider-terminal-accounting.test.mjs::WHAT[CHATEXEC-006] exact provider failure remains typed but awaits retry-owner disposition`；`requirements/managed-chat-execution/tests/chat-execution-facts.test.mjs::WHAT[CHATEXEC-006] Terminal directly after Accepted is rejected` |
| CHATEXEC-007 | `requirements/managed-chat-execution/tests/pre-provider-settlement.test.mjs::WHAT[CHATEXEC-007] detects missing exact pre-provider release`；`requirements/managed-chat-execution/tests/pre-provider-settlement.test.mjs::WHAT[CHATEXEC-007] rejects raw AGENT-028 before it can enter a legal managed flow`；`requirements/managed-chat-execution/tests/admission-transaction.test.mjs::WHAT[CHATEXEC-007] every acquired pre-commit failure releases exactly once`；`requirements/managed-chat-execution/tests/chat-execution-facts.test.mjs::WHAT[CHATEXEC-007] pre-provider failure cancellation and rejection settle without a provider run` |
| CHATEXEC-008 | `requirements/managed-chat-execution/tests/lifecycle-recovery.test.mjs::WHAT[CHATEXEC-008] recovery begins from durable activation and re-enters only on causal events` |
| CHATEXEC-009 | `requirements/managed-chat-execution/tests/facts-roundtrip.test.mjs::WHAT[CHATEXEC-009] durable execution fact round-trip excludes process-local artifacts`（只闭合 durable codec 不持久化 artifact；真实 dispose/reopen 仍属下述验证缺口） |
| CHATEXEC-010 | `requirements/managed-chat-execution/tests/chat-execution-facts.test.mjs::WHAT[CHATEXEC-010] cancel and delete settle every exact projected execution before capacity is drained`；`requirements/managed-chat-execution/tests/provider-terminal-accounting.test.mjs::WHAT[CHATEXEC-010] recovery drain completion uses the Fable-compatible completion owner` |
| CHATEXEC-011 | `requirements/managed-chat-execution/tests/acceptance-idempotence.test.mjs::WHAT[CHATEXEC-011] external and plugin roots share AcceptManagedChatIntent`；`requirements/managed-chat-execution/tests/provider-start-terminal-facts.test.mjs::WHAT[CHATEXEC-011] exact physical provider run and evidence are frozen`；`requirements/managed-chat-execution/tests/facts-roundtrip.test.mjs::WHAT[CHATEXEC-011] malformed exact identity seed is rejected by the production codec` |
| CHATEXEC-012 | `requirements/managed-chat-execution/tests/recovery-decision.test.mjs::WHAT[CHATEXEC-012] durable facts plus explicit physical evidence exhaustively determine recovery`；`requirements/managed-chat-execution/tests/recovery-decision.test.mjs::WHAT[CHATEXEC-012] duplicate evaluation is deterministic and effect-free`；`requirements/managed-chat-execution/tests/lifecycle-recovery.test.mjs::WHAT[CHATEXEC-012] lifecycle recovery interprets every typed decision through its owner port`；`requirements/managed-chat-execution/tests/lifecycle-recovery.property.test.mjs::WHAT[CHATEXEC-012] every duplicate recovery decision invokes its owner port`；`requirements/managed-chat-execution/tests/lifecycle-recovery.property.test.mjs::WHAT[CHATEXEC-012] stale physical identity and stale policy evidence cannot affect a newer execution`；`requirements/managed-chat-execution/tests/lifecycle-recovery.property.test.mjs::WHAT[CHATEXEC-012] restart creates a fresh recovery port observer`；`requirements/managed-chat-execution/tests/admission-crash-points.test.mjs::WHAT[CHATEXEC-012] A–I production transaction and lifecycle prefixes drive recovery decisions`；`requirements/managed-chat-execution/tests/admission-crash-property.test.mjs::WHAT[CHATEXEC-012] duplicate crash-cut requests and input permutations preserve canonical production recovery` |
| CHATEXEC-013 | `requirements/managed-chat-execution/tests/diagnostics.test.mjs::WHAT[CHATEXEC-013] diagnostic query derives nonterminal and physical-attempt counts from canonical projection` |
| CHATEXEC-014 | `requirements/managed-chat-execution/tests/evidence-capture.test.mjs::WHAT[CHATEXEC-014] capture canonicalizes facts and preserves only immutable owner evidence`；`requirements/managed-chat-execution/tests/evidence-capture.test.mjs::WHAT[CHATEXEC-014] capture redacts known failures and rejects payload or stack fields`；`requirements/managed-chat-execution/tests/incident-replay.test.mjs::WHAT[CHATEXEC-014] replay reconstructs the canonical projection and emits only owner effect requests`；`requirements/managed-chat-execution/tests/incident-replay.test.mjs::WHAT[CHATEXEC-014] replay fails closed on tamper, version, unknown, or missing evidence` |
