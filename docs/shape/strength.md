# Strength — 所有权与边界

本页只规定 Strength-specific owner 与依赖方向；共有结构引用既有 Clause。

## STRENGTH-013：边界分层

```text
Domain
  StrengthPolicy      Evidence → Decision / control / value
  StrengthFrame       semantic batches / digest / deterministic wire identity
  StrengthEvents      durable event vocabulary（只含 opaque PayloadRef）
  StrengthProjection  events → indexed durable view
  ProjectionAlgebra   UseStrengthMirror / InsertStrengthFrames cases

Application
  StrengthWorkflow    eligible evidence → replica port → Prepared → candidate intent
  StrengthPromotion   ReconciledTurn → promotion decision/append resolution
  StrengthReplay      promoted view → replay/traced intent

Session
  StrengthRuntime     decision-local InternalLeaf physical resource + request budget

Persist/Journal adapter
  Strength event codec + fold/index + EventStore payload material mapping

Infrastructure/OpenCode
  Host session/tool/provider adapter + transform hooks + canary facts
```

Domain 不读 Host、不访问 EventStore/Git、不读时钟/RNG；Application 只依赖 typed ports；Session 不拥有 durable truth；Persist 不决定 eligibility/promotion；Host adapter 不复制 Strength policy。

## STRENGTH-014：Universal ownership

长期分类由 HOST-008 唯一拥有：`AttachmentKind.StrengthReplica` 对应 `SessionExecutionClass.InternalLeaf × SessionOwnership.Attached(ownerWorkSessionId, StrengthReplica)`。Strength 不扩 `SatelliteKind`。owner×attachment 的 durable association/index 是唯一 Session 归属事实；`StrengthRuntime` 只保存当前 decision 的 process-local single-flight/batch collector，不成为第二份 association registry。

每个 owner Work Session 至多一个 active StrengthReplica attachment。owner cancellation/delete 由通用 Attached/leaf 路径级联；Replica retire 后不得跨 decision 复用 transcript。

## STRENGTH-015：Prompt authority

PROMPT-008 的 `AttemptExecutionProfile` 是 Replica provider request 的唯一 profile 构造入口；`ProviderRequestKind.StrengthReplica` 由 PromptAuthority 拥有 request-specific capability narrowing。Strength workflow 只提供不能推导的 request kind / projection choice / provider-run facts，不传入自造 role、system prompt 或 tool set。

same-role fast peer 由 AGENT-001/003 的 canonical pair 解析；没有 `Role.Replica`、`fast-replica`、`deep-replica` 或 StrengthPromptAuthority。

## STRENGTH-016：Projection owner

PROJ-005/006 唯一拥有：

```fsharp
UseStrengthMirror of StrengthMirrorIntent
InsertStrengthFrames of StrengthFramesIntent
```

`UseStrengthMirror` 是 base selection，只对 `InternalLeaf × Attached(StrengthReplica)` + `ProviderRequestKind.StrengthReplica` 合法，并与 `KeepPhysicalPrefix` / `ActivatePrefixEpoch` 互斥。`InsertStrengthFrames` 显式携带 visibility/anchor，不让 renderer 从来源猜 Candidate/Promoted/Replica-local。Strength 业务模块只声明 intent，不直接改 `Message list`。

PairProgrammingThought 继续由 HOST-013 raw anchored writer 拥有；Strength 只保证 frames 在 pair placement 之前进入最终 view。ReviewSeal writer 不变。

## STRENGTH-017：Durable facts owner

统一 EventStore 是 Strength durable authority。核心 event family：

```text
StrengthDecisionObserved        // 可选 shadow/control audit
StrengthCandidatePrepared
StrengthCandidatePromoted
StrengthFramesTraced
StrengthCandidateAbandoned      // 仅明确未消费终止时可选
```

`StrengthProjection` 纯 fold 至少建立：OwnerSessionId→open candidate、TargetProviderRun→DecisionId、DecisionId→Prepared metadata+PayloadRefs、DecisionId→Promotion、DecisionId→XTrace range。热路径只读该 projection/index，不扫描全 EventStore 或 legacy NDJSON。

大 material 通过 EventEnvelope.PayloadRefs 引用 PERSIST-007 raw payload；Domain 不出现 Git OID/RuntimePath/feature-owned blob ref。Prepared/Promoted 可引用同一 payload；禁止复制第二份 material。

## STRENGTH-018：XTrace/Companion owner

XTraceCursor 仍由 HOST-005 拥有，Strength 不创建 semantic cursor。`StrengthFramesTraced` 只记录“已 Promoted frame 实际进入哪段现有 XTrace”的关联事实。Companion 的 RecordCoverage/PrefixCoverage 仍由 COMPANION owner 维护；Strength 只读取语义 coverage 决定 raw replay 是否仍必要，不写伪 cutoff/coverage。

## STRENGTH-019：Fallback/Review 隔离

FALLBACK owner 不接收 StrengthReplica attempt outcome；Review/Finality writer、challenge、seal、witness 与 cohort 不接受 Strength control state。Strength event 或 tool result 文本永不具有 Review authority。Reviewer/Finality request 不进入 Strength eligibility。
