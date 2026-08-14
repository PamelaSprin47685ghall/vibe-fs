# speculative-investigation — HOW

> 非 normative。描述当前实现模型与约束，以及「历史与弃权」裁决。
> 当前实现名（Strength、same-role-fast、K1/K2 数值、predictor 特征）全部是 HOW，不是 WHAT。
> 若未来换实现，WHAT.md 不变。源：`docs/how/strength.md`、`docs/shape/strength.md`、
> `changes/completed/strength.md`、`src/Wanxiangshu/**`。

## 1. 模块地图（当前实现）

```text
src/Wanxiangshu/Domain/
  StrengthBudget.fs        StrengthBudget ∈ {K0,K1,K2}；requestLimit；K1/K2 margin 门
  StrengthPolicy.fs        StrengthOpportunity / StrengthDecision；eligibility / controlBucket /
                           isControlHoldout / decideFromFacts / budgetOf / isSpeculate
  StrengthCostModel.fs     StrengthValueInputs / StrengthValueEstimate{V0,V1,V2}；estimateFrom
  StrengthPredictor.fs     StrengthPrimarySymbol / StrengthFeatureKey / StrengthPredictorBucket；
                           observeFirst / observeSecond / predict（纯、确定性状态更新）
  StrengthRollout.fs       StrengthRolloutMode（Shadow/DryRun/...）/ StrengthExplicitCostTemplate；
                           estimate / isShadow
  StrengthFrame.fs         StrengthToolExchange / StrengthRequestBatch / StrengthFrameBundle；
                           isAllowedTool（read/glob/grep）；tryBuild（完整配对校验）；utf8ByteCount；
                           canonicalText（去 wire-id 的 canonical）；tryLocalizeMirror
  StrengthBatchCollector.fs  collectCompleteBatches：按 provider request 边界收完整 call/result 配对
  StrengthEvents.fs        StrengthCandidatePrepared / Promoted / FramesTraced / CandidateAbandoned；
                           StrengthEventTypes.all；事件只含 opaque PayloadRef
  StrengthProjection.fs    StrengthProjection；tryCandidate / hasPrepared / isPromoted /
                           tryDecisionForTarget / tryTraceRange；apply（纯 fold，不扫全 EventStore）
  StrengthCommit.fs        StrengthAppendOutcome / StrengthDurableEvidence / StrengthCommitDecision；
                           resolvePrepared / resolvePromotion（CommitUnknown 三态裁决）
  StrengthPromotion.fs     StrengthProviderOutputEvidence / StrengthPromotionDecision；decide
                           （wrong run / NoOutput / TransportOnly → 不 Promote）

src/Wanxiangshu/Application/Strength/
  StrengthDurabilityPort.fs    Application 只依赖的 typed port（EventStore/GitRawStore 只在 Persist）
  StrengthLifecycle.fs         reconcileEvent（ReconciledTurn → Promotion/Abandoned）；replayPlans；
                               needsRawReplay（以 Companion coverage 判退休）；replayIntents
  StrengthReplicaRuntime.fs    decision-local InternalLeaf 物理资源 + request budget + fuse gate
  StrengthReplicaTransform.fs  StrengthSpeculate 的 Replica 侧：frozen mirror → batch 收割 → insert
  StrengthTraceRecovery.fs     recoverRange：XTrace 已写、Traced 未写时按 stable identity / 唯一
                               canonical contiguous range 补 fact
  StrengthTurnEvidence.fs      classifyParts（NoOutput/TransportOnly/RealOutput）；primarySymbol；
                               promotionDecision

src/Wanxiangshu/Session/StrengthRuntime.fs          decision 生命周期（single-flight、retire）
src/Wanxiangshu/Infrastructure/Persist/
  StrengthDurability.fs / StrengthStore.fs          Prepared/Promoted payload 与 EventStore 收口
src/Wanxiangshu/Infrastructure/OpenCode/Host/
  StrengthSettings.fs / PluginStrengthScope.fs      env 设置、Host canary fingerprint、process fuse
```

主 transform 顺序固定（docs/how/strength.md）：

```text
StrengthReplay → XTraceCapture → Companion → XWire → EnforcerHost → StrengthSpeculate
→ PairProgrammingThoughtTransform → HostMessageProjection.sanitizeMessages → ReviewSeal
```

- `StrengthReplay` 只读 durable Promoted view，把 frame 插在 TargetProviderRun 对应 assistant
  output 之前；Candidate 永不早期 replay。
- `StrengthSpeculate` 在 post-Enforcer view 上先冻结 `ProviderSemanticProjection`，再决定/运行
  Replica，最后只为当前唯一 TargetProviderRun 声明 Candidate insertion。

## 2. Opportunity → Decision 管线

Coordinator 构造不可变 `StrengthOpportunity`（owner/session ownership、AuthorityRoot、
TargetProviderRun、CanonicalRole、Selected/EffectiveAgent、tier/model binding/cost metadata、
request kind、fallback/recovery/finality facts、frozen semantic history/bytes、
EventStore/canary health）。`StrengthPolicy.decideFromFacts` 顺序：

```text
eligibility gate → deterministic control bucket → shadow/treatment mode
→ predictor P1/P2 + evidence floor → value(V0,V1,V2) → margin gate
→ Skip | ControlHoldout | Speculate K1/K2
```

任何缺失/不可信证据直接 `Skip`。control hash 使用 canonical frozen key + PolicyVersion，
不调用 RNG/clock（SPEC-INV-010）。

## 3. Replica request loop（决策内）

1. 解析 same-role fast peer；创建/复用仅当前 decision 的 `InternalLeaf × Attached(StrengthReplica)`。
2. **继承** owner `SessionPersona` / `SessionProviderLanguage`；只换 ExecutionBinding
   （`fast-<owner-role>`）。禁止新建 Persona、重写 system 身份字节、换世界语。
3. `ProviderRequestKind.StrengthReplica` profile；tool schema 从 `ToolCapabilitySet` 生成，
   execution gate 读同一 set（恰好 `read/glob/grep`）。
4. `UseStrengthMirror`：frozen owner semantic history 作 base；Host 边界临时读 owner wire 保留
   call/result 配对，随后按 `DecisionId + semantic digest + encounter ordinal` 把 owner
   ToolCallId 全部重定位为 decision-local deterministic id；`ProviderSemanticProjection` 前后
   相等。media / 无法唯一配对的历史不可逆 → K0。
5. 每次 provider request 完成后只收集真实 readonly call/result。并发调用按 Host/provider 稳定
   顺序收割；任一未配对、未知 tool、超 byte limit → 本 decision unusable。
6. 请求计数达 K 后，在下一 transform/reconcile 边界停止并 retire，禁止 K+1 外发。text-only
   completion 丢弃正文并停止；之前完整 batch 可保留。
7. owner cancellation 立即 abort/retire leaf。Replica provider/tool 普通失败不进入 owner fallback。

## 4. Frame canonicalization 与 durable 事实

- 每个 exchange 规范化为 `ToolName + CanonicalArguments + CanonicalResult`；batch 保留
  `RequestOrdinal`，exchange 保留 stable ordinal。semantic digest 对去 wire-id 的 canonical
  bundle 计算。owner synthetic ToolCallId 由 `ownerSessionId + decisionId + requestOrdinal +
  exchangeOrdinal + semanticDigest` 经稳定 hash 派生；同 DecisionId 不同 digest 是冲突。
- Prepared append：`write raw frame payload → append StrengthCandidatePrepared(envelope.payload_refs)`
  → resolve append outcome → 成功后才声明 InsertStrengthFrames(Candidate, targetRun)。
  append 明确失败 → K0；CommitUnknown → 查 projection（同 digest+refs → 继续；明确不存在 →
  K0；无法证明 → 阻止 target request）。
- Promotion reconcile：`无 Prepared → no-op；Prepared + same run InProgress → no-op；
  Prepared + same run NeedsContinuation/Completed 且有 usable output → append Promoted；
  Failed/Aborted 或 Completed 无 usable output → append Abandoned`。Promoted 校验
  Prepared/run/digest/payload refs 完全一致；重复同事实幂等。
- Replay → Traced：多 Promoted decision 的 `BeforeMessageIndex` 是原始 base 的绝对索引；
  planner canonical sort、renderer 按 index 倒序插入。XTraceCapture 写入正常 XTrace parts 后
  append `StrengthFramesTraced`。raw replay 退休只由语义 coverage 证明（当前读 Companion
  `IngestedThroughSequence` 覆盖到 traced range 最后一项）；物理 message cutoff 不参与。

## 5. Predictor 与 value 方程（当前数值）

- Primary request 符号化为 readonly/read-search/mutate/execute/text/other；Shadow/control 观察
  下一次 primary request `R1`（非空纯 readonly batch 再观察 `R2`）；Replica request 永不进入
  该序列。第一版 predictor 按 CanonicalRole + 最近 1..3 primary symbols + tool-result
  structural features + visible bytes 分桶；纯、确定性状态更新。
- value：`V0=0`；`V1=P1*SavedDeep1−Fast1−Byte1−Delay1−Risk1`；
  `V2=P1*SavedDeep1+P1*P2*SavedDeep2−Fast1−P1*Fast2−Byte2−Delay2−Risk2`。选最大值后应用
  `K1Margin` / 更高 `K2Margin` 与 K2 evidence floor。没有可靠 fast/deep cost metadata 时
  treatment 强制 K0，shadow 仍可记录 prediction。默认 Host settings = Shadow（SPEC-INV-010）。

## 6. 崩溃矩阵（当前行为）

| 崩溃点 | 行为 |
|---|---|
| Replica 尚未 Prepared | 重启丢弃，只读副作用为零 |
| Prepared durable、target 未消费 | 同 run + 同 AnchorDigest 重放同 Candidate；run 明确终止且未消费 → Abandoned |
| provider 已产出可用 output、Promotion 前 crash | reconcile 以 ProviderRunIdentity 补 Promotion；Failed/Aborted 不补 |
| Promoted、XTrace 未捕获 | StrengthReplay 重建 |
| XTrace 已捕获、Traced 缺失 | stable identity 或唯一 canonical contiguous range 补 fact |
| durable outcome 无法证明 | fail closed；普通 pre-commit Replica failure：fail open K0 |
| process-local fuse | 记住首个 durable/projection/schema/frame 不一致，只禁止**新** speculation；Prepared recovery、Promoted replay/promotion/tracing 永不被 fuse 关闭 |
| Host canary | `WANXIANGSHU_STRENGTH_HOST_CANARY` 必须逐字等于当前 `opencode-ai` + `@opencode-ai/plugin` 版本指纹；依赖版本变化自动回到 K0 |

## 7. 依赖（DEPENDS ON，逐条理由）

来自 `requirements-design/INDEX.md` 依赖骨架（不增删 edge）：

- `repository-investigation`：投机的是「接下来需要哪些只读调查」；被消费后的 frame 是
  repository fact acquisition 的合法输入。
- `participant-identity`：Replica 继承 owner 的 persona/language、只换 execution binding——
  「换执行者不等于换人」由该包保证。
- `participant-horizon`：Replica 可见面是 owner horizon 的投影；跨 Session 只比语义投影。
- `provider-projection`：UseStrengthMirror / InsertStrengthFrames 的代数与确定性由该包保证。
- `semantic-trace`：Promoted 最终进入 XTrace；unpromoted ∉ history 的另一半在该包。

## 8. 历史与弃权（考古记录，非 normative）

- **算法/常量降为 HOW**：`same-role-fast` 模型选择、K1/K2 数值、margin/evidence floor、
  predictor 特征分桶、canary 指纹格式——全部是当前实现，不进 WHAT（边界卡片
  DOES NOT OWN 与 HANDOFF §6.7 同类裁决）。
- **STRENGTH-013..019（docs/shape/strength.md）**：这些是「所有权分配」条款，不是本包新增
  行为——Session 归属 → `session-ontology`、profile 构造 → `participant-identity`、
  projection intent → `provider-projection`、durable substrate → `durable-events`、
  XTrace/Companion coverage → `semantic-trace`/`context-compression`、fallback/review 隔离 →
  `provider-attempt-recovery`/`review-*`。信息已分别落入本 WHAT 各命题的「边界」节。
- **被拒方向**：见 WHY.md §3（changes/completed/strength.md §三十逐条）。
- **Semble 弃权**：Strength 不消费 Semble（AGENT-027）；历史伪造 read 的失败模式见
  WHY.md §1.9。
- **Student/Teacher**：已删除领域；`Student & Teacher.md` 为 GARBAGE（CHANGES-AUDIT）；
  absence ratchet 归 `session-ontology` 的 `student-teacher-absence` gate，本包不背墓碑。
- **`docs/why/loop.md` / `docs/{what,how,proof}/loop.md`**：loop 主题是退化循环检测
  （`degeneration-guard`），全篇 grep `speculat/投机/strength` 零命中——无本包可吸收的
  speculation 内容，弃权。
- **dry-run / e2e**：`tests/e2e/entry.test.mjs` long-stroke `strength-canary-*` 是 Host
  request-budget 的物理证明（K2 恰好两轮、第 3 轮不外发、`StrengthCandidatePrepared=0`），
  归 `verification-system` MECHANISM，本包 PROOF 交叉引用。
- **GARBAGE 结论**：旧稿 `FrameBundleRef` / `PredictorSnapshotRef` / Journal NDJSON /
  RuntimePath blob 类型名已被存储收口删除（changes/completed/strength.md §二十二）——只留
  EventStore `payload_refs`；不进入 WHAT。
