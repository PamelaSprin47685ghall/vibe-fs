# Strength — 执行程序

实现 STRENGTH-001..019；共有 Session/Prompt/Projection/Persist/XTrace/Review 程序仍以各主题 how 为准。

## Main transform 两段程序

顺序固定：

```text
StrengthReplay
→ XTraceCapture
→ Companion
→ XWire
→ EnforcerHost
→ StrengthSpeculate
→ PairProgrammingThoughtTransform
→ HostMessageProjection.sanitizeMessages
→ ReviewSeal
```

`StrengthReplay` 只读取 durable Promoted view，把 frame 插在其 `TargetProviderRun` 对应 assistant output 之前；Candidate 永不早期 replay。

`StrengthSpeculate` 在 post-Enforcer view 上先冻结 `ProviderSemanticProjection`，再决定/运行 Replica，最后只为当前唯一 `TargetProviderRun` 声明 Candidate insertion。冻结发生在 candidate 与 pair marker 之前；Replica 和 main 最终 view 都继续经过 Pair writer。

## Opportunity → Decision

Coordinator 构造不可变 `StrengthOpportunity`：owner/session ownership、AuthorityRoot、TargetProviderRun、CanonicalRole、Selected/EffectiveAgent、tier/model binding/cost metadata、request kind、fallback/recovery/finality facts、frozen semantic history/bytes、EventStore/canary health。

`StrengthPolicy.decide` 顺序：

```text
eligibility gate
→ deterministic control bucket
→ shadow/treatment mode
→ predictor P1/P2 + evidence floor
→ value(V0,V1,V2)
→ margin gate
→ Skip | ControlHoldout | Speculate K1/K2
```

任何缺失/不可信证据直接 `Skip`。control hash 使用 canonical frozen key + PolicyVersion；不得调用 RNG/clock。

## Replica request loop

1. 解析 same-role fast peer；创建/复用仅当前 decision 的 `InternalLeaf × Attached(StrengthReplica)` leaf。
2. 构造 `ProviderRequestKind.StrengthReplica` profile；tool schema 从 `ToolCapabilitySet` 生成，执行 gate 读取同一 set。
3. `UseStrengthMirror` 以 frozen owner semantic history 作为 base。Host 边界只临时读取 owner wire 以保留 tool call/result 配对，随后按 `DecisionId + semantic digest + encounter ordinal` 把 owner ToolCallId 全部重定位为 decision-local deterministic id；`ProviderSemanticProjection` 必须前后相等，owner id 不得跨 Session。media 或无法唯一配对的历史不可逆，直接 K0；不建立通用 Semantic→Wire 逆变换。每完成一个 batch，把本 decision 的 prior batches 通过 `InsertStrengthFrames` 加入下一次 Replica view。
4. 每次 provider request 完成后只收集真实 readonly call/result。并发调用按 Host/provider 稳定顺序收割；任一未配对、未知 tool、超 byte limit → 本 decision unusable。
5. 请求计数达到 K 后，在下一 transform/reconcile 边界停止并 retire，禁止 K+1 外发。text-only completion 丢弃正文并停止；之前完整 batch 可保留。
6. owner cancellation 立即 abort/retire leaf。Replica provider/tool 普通失败不进入 owner fallback。

## Frame canonicalization

每个 exchange 规范化为 `ToolName + CanonicalArguments + CanonicalResult`；batch 保留 `RequestOrdinal`，exchange 保留 stable ordinal。semantic digest 对去 wire-id 的 canonical bundle 计算。owner synthetic ToolCallId 由：

```text
ownerSessionId
+ decisionId
+ requestOrdinal
+ exchangeOrdinal
+ semanticDigest
```

经稳定 hash 派生。相同 decision replay 必须产生同一 ids/bytes；同 DecisionId 不同 digest 是冲突。

## Prepared append

Replica 产出非空合法 bundle后：

```text
write raw frame payload
→ append StrengthCandidatePrepared(envelope.payload_refs=[framePayload,...])
→ resolve append outcome
→ 成功后才声明 InsertStrengthFrames(Candidate,targetRun)
```

raw payload 与 event 的 publish 原子性服从 PERSIST-002/007。Prepared metadata inline 只放小字段，payload 正文只经 opaque `PayloadRef`。Application/Host 只依赖 `StrengthDurabilityPort`；`IGitRawStore` / `IEventStore` 只出现在 Persist adapter，禁止与 `AgentJournal` 形成 dual-write bridge。

append `Rejected/StorageInvalid` 等明确失败 → K0，不插入。`CommitUnknown` → 按 DecisionId/TargetProviderRun 查 StrengthProjection；同 digest+refs 已存在则继续，明确不存在则 K0，无法证明则阻止 target request。若重启时已有未消费 Prepared，则**先于 rollout/fuse 开关**按同 TargetProviderRun + AnchorDigest 重建同一 Candidate；anchor 或 payload 不再等价 → fail closed。

## Promotion reconcile

Reconciler 对每个 `ReconciledTurn` 查询 `TargetProviderRun` 索引：

```text
无 Prepared → no-op
Prepared + same run InProgress → no-op
Prepared + same run NeedsContinuation/Completed 且有 usable provider output → append Promoted
Prepared + Failed/Aborted，或 Completed 但无 usable provider output → append Abandoned
```

Promoted 校验 Prepared、run、digest、payload refs 完全一致；重复同事实幂等。Failed/Aborted 即使带部分 provider output 也不得 Promotion。Abandoned 释放 TargetProviderRun 索引且永不转 Promoted。append CommitUnknown 同样重读 resolve。Promotion 未证明前禁止该 run 的下一 continuation。

## Replay → Traced

下一主 transform 查询 Promoted 且仍需 raw replay 的 decisions，按 target assistant anchor 确定性插入。多个 Promoted decision 的 `BeforeMessageIndex` 是**原始 base 的绝对索引**：planner 先 canonical sort，renderer 按 index 倒序插入，注册顺序不得改变 bytes。XTraceCapture 识别 Strength synthetic stable identity 后写入正常 XTrace parts；得到首次/末次 cursor 后 append `StrengthFramesTraced`。若 crash 在 XTrace 已写、Traced 未写之间，下一次 capture 优先按 stable Host message identity 找 range，旧 positional trace 只允许 canonical body 唯一且 cursor 连续匹配，否则 fail closed。

raw replay retirement 条件只能由现有 semantic coverage 证明：当前实现读取 Companion `IngestedThroughSequence`，覆盖到 traced range 最后一项后才停止 raw replay。物理 message cutoff 不参与这个判断；未来若改用其它 representation，只能读取该 owner 已有的 `CoveredXTraceThrough` 等价事实。

## Predictor labels

Primary request symbol 化为 readonly/read-search/mutate/execute/text/other。Shadow/control opportunity 观察下一次 primary request `R1`，若 R1 是非空纯 readonly batch再观察 R2；Replica request 永不进入该序列。第一版 predictor 可按 CanonicalRole + 最近 1..3 primary symbols + tool-result structural features + visible bytes 分桶，必须是纯、确定性状态更新。

value：

```text
V0 = 0
V1 = P1*SavedDeep1 - Fast1 - Byte1 - Delay1 - Risk1
V2 = P1*SavedDeep1 + P1*P2*SavedDeep2
     - Fast1 - P1*Fast2 - Byte2 - Delay2 - Risk2
```

选最大值后再应用 `K1Margin` / 更高 `K2Margin` 与 K2 evidence floor。没有可靠 fast/deep cost metadata 时 treatment 强制 K0，shadow 仍可记录 prediction。默认 Host settings = Shadow；仓库不因本程序落地而宣称 K1 正收益 cohort。

## Crash / fuse

- Replica 尚未 Prepared：重启丢弃，只读副作用为零。
- Prepared durable、target 未消费：同 run + 同 AnchorDigest 重放同 Candidate；run 明确终止且未消费则 Abandoned。
- provider 已产生可用 output、Promotion 前 crash：reconcile 以 ProviderRunIdentity 补 Promotion；Failed/Aborted 不补 Promotion。
- Promoted、XTrace 未捕获：StrengthReplay 重建。
- XTrace 已捕获、Traced 缺失：stable identity 或唯一 canonical contiguous range 补 fact。
- durable outcome 无法证明：fail closed；普通 pre-commit Replica failure：fail open K0。
- process-local fuse 记住首个 durable/projection/schema/frame 不一致，只禁止**新** speculation；Prepared recovery、Promoted replay/promotion/tracing 永不被 feature switch/fuse 关闭。
- treatment 的 Host canary 不是布尔开关：`WANXIANGSHU_STRENGTH_HOST_CANARY` 必须逐字等于当前 `opencode-ai` + `@opencode-ai/plugin` 版本指纹；依赖版本变化自动回到 K0，直到新 canary 重新证明。
