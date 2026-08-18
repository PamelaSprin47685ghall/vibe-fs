# speculative-investigation — WHAT

> 本页是**唯一 normative 合同**：当前世界必须同时成立的编号命题。每条命题 = 标题 +
> 规范陈述 + 含义/动机 + 边界 + 证据指针（→ HOW.md）。
> 历史断言、迁移沉积、被拒方案**不是**命题（见 HOW.md「历史与弃权」）。
> 前缀 `SPEC-INV-`。测试落点表见 `HOW.md`。

源条款：历史 what/strength STRENGTH-001..012（本包主导全部 12 条，COVERAGE.md 单-owner
裁决）；历史 why/shape/how/proof strength 条款、历史 change（strength）。

---

## SPEC-INV-001：优化目标与零影响基线

**规范陈述**：Strength 只优化 eligible deep Work provider request 前的机械只读调查。
Strength disabled、熔断、证据不足或策略选择 K0 时，普通 Work Session 的 provider-visible
bytes、工具权限、Fallback、Review/Finality 与控制流必须与没有 Strength 时**相同**。
Strength 从不成为任务正确性的必要条件。

**含义/动机**：投机是优化不是功能。关闭优化不得改变产品行为——否则投机就从「可丢弃」
变成「正确性依赖」，RED 判定直接命中。

**边界**：不定义「普通 Work Session 的正常形态」本身（归各领域 owner）；只保证 Strength
的介入与缺席不可区分。

**证据**：HOW.md SPEC-INV-001 行（host-policy K0/canary、host-canary-k0 REUSE）。

## SPEC-INV-002：Eligible opportunity

**规范陈述**：实际 speculation 仅允许同时满足：Root `SessionExecutionClass.Work`；
`ProviderRequestKind.WorkMain`；CanonicalRole ∈ {Coder, Inspector, DevOps, Inquiry}；
Authority 选择 Deep 且 `EffectiveAgent = SelectedAgent`；不是 fallback B-side、
InteractionRepair、prefix probe、Reviewer/finality、Attached 或 InternalLeaf；owner 未取消；
可唯一绑定即将消费输入的 `TargetProviderRun`；存在 same-role fast peer，且该 replica 的 resolved execution target 经显式成本模型判定仍有正收益；EventStore、Host canary 与成本模型均健康可用。**任一事实未知或不满足 → K0。** 不再以 fast/deep model string 是否不同作为 eligibility 或启动校验。Browser、Manager、Orchestrator、Reviewer 第一版不 eligible。

**含义/动机**：投机只能在「不会改变主路径语义」的窗口内发生。任何不确定都是关闭理由。

**边界**：role/request-kind 的枚举与 profile 语义由 `participant-identity`/`office-capability`
定义；本命题只规定「eligible 集合」这一事实。

**证据**：HOW.md SPEC-INV-002 行（authority-policy、host-canary-k0 REUSE）。

## SPEC-INV-003：预算单位 K

**规范陈述**：`StrengthBudget ∈ {K0,K1,K2}`，K 是 **Replica provider request 数**，不是 tool
call 数。一个 provider request 可并发产生多个允许工具调用，只有全部 call/result 完整配对后
才形成一个 batch。Host 在第 K 个 request 的结果收割后**阻止 K+1 外发**；Replica text-only
completion 终止 speculation，正文永不注入 primary。

**含义/动机**：成本单元是 provider 决策。按 tool call 预算会让并发工具调用逃逸成本控制。

**边界**：K 的数值与枚举是当前实现选择（HOW）；「K 是 provider request 单位」是命题。

**证据**：HOW.md SPEC-INV-003 行（batch-collector、replica-transform REUSE）。

## SPEC-INV-004：Replica authority

**规范陈述**：Replica = `InternalLeaf × Attached(owner, StrengthReplica)`，使用
`fast-<owner-role>`；**不新增** CanonicalRole/Agent/system prompt。Replica **继承** owner 的
`SessionPersona` 与 `SessionProviderLanguage`；只换 ExecutionBinding 到 fast EffectiveAgent，其物理 ModelTarget 由 `execution-model-routing` 的 MJS scheduler/lease 解析，不换人、不换世界语。每个 Strength decision 使用短生命周期 leaf，完成即 retire，不跨 decision 复用
transcript。Replica 无 Companion、SyncDelegate、嵌套 StrengthReplica、fork/horizon/join、
deep fallback 或用户权限交互。provider-visible schema 与 execution gate 必须同源且恰好允许
`read/glob/grep`；任何其它工具 fail closed。Replica 成败不推进 owner FallbackCursor，不清零
owner failure count，不触发 owner InteractionRepair。

**含义/动机**：低成本路径必须具有更低 authority，而不是更弱的文字提醒。约束来自结构化
schema/gate，不来自 prompt 自觉。

**边界**：persona/language 继承的语义定义 → `participant-identity`；read/glob/grep 的
capability 投影 → `capability-enforcement`；fallback cursor 语义 → `provider-attempt-recovery`。

**证据**：HOW.md SPEC-INV-004 行（authority-policy、runtime/host-canary-k0 REUSE）。

## SPEC-INV-005：Candidate frame

**规范陈述**：Strength 只保留真实 Host 工具交换，不复制 Replica prose/reasoning。每个 frame
bundle 保留 provider request batch 边界、稳定 provider 顺序、canonical arguments、真实
canonical result、内容 digest 与 byte length；call/result 必须一一配对且工具只能是
`read/glob/grep`。跨 Session semantic bundle 不携带 Replica tool call id；owner wire id 由
owner session、DecisionId、request/exchange ordinal 与 semantic digest 确定性派生，禁止随机数、
时间戳或 GUID。超过硬字节上限的 speculation 整体丢弃为 K0。

**含义/动机**：frame 是「primary 将会看见的只读事实」的 canonical 表示；必须是确定性的，
否则 replay 无法重建同一字节。

**边界**：digest/byte 上限的数值与 canonical 化具体规则 → HOW；「确定性派生 + 只收真实
交换」是命题。

**证据**：HOW.md SPEC-INV-005 行（frame-projection、projection-adapter REUSE）。

## SPEC-INV-006：Prepared / unpromoted ≠ 历史

**规范陈述**：可用 frame bundle 在 primary 真正看见前必须先 append durable
`StrengthCandidatePrepared`，绑定唯一 `OwnerSessionId + DecisionId + TargetProviderRun +
ReplicaSessionId + Budget + AnchorDigest + FrameDigest + ByteLength`。大 material 只通过该
EventStore envelope 的 `payload_refs: PayloadRef list` 引用；不得存在 Strength-owned NDJSON、
RuntimePath blob、`FrameBundleRef`/`PredictorSnapshotRef` storage 类型。

**Unpromoted Candidate ≠ 历史。** Prepared / 未 Promote 的 Candidate 不得进入 XTrace、
Companion、LWR、PrefixSnapshot 或未来 durable provider history，只能注入它绑定的
TargetProviderRun。source label（strength / replica / prefetch）不得进入 Main reasoning
（SPEC-INV-012）。

Prepared append 明确失败可 fail open 为 K0。提交结局 CommitUnknown 必须重读 Strength
projection：证明同一 Candidate 已提交则以同一 digest/payload refs 注入；证明不存在则 K0；
仍无法证明则**禁止 target request 外发**。

**含义/动机**：Candidate 可以消失，Promoted 不能消失。没有 rollback——Prepared 先于任何
可见性，消费证明先于 Promotion。

**边界**：`unpromoted ≠ history` 是 `speculative-investigation` 与 `semantic-trace` 的
cross-boundary invariant（HANDOFF §18.6）：canonical trace 侧由 semantic-trace 拥有，本包
不复制其命题，只在本命题声明「不得进入历史」这一侧。EventStore envelope/payload_refs
substrate 语义 → `durable-events`。

**证据**：HOW.md SPEC-INV-006 行（commit-promotion、store/durability-port/lifecycle-recovery
REUSE、integration lifecycle REUSE）。

## SPEC-INV-007：Promotion 只由消费证据产生

**规范陈述**：只有 ReconciledTurn 证明 `turn.ProviderRun = Candidate.TargetProviderRun` 且该
run 存在真实 provider output，才能 append `StrengthCandidatePromoted`。请求尚未开始、
transport-only、空失败、Failed 或 Aborted 都不能 Promotion。Promoted 必须引用 Prepared 的
同一 frame digest/payload material；wrong run、无 Prepared、同 Decision 不同 digest 都
fail closed。

Promotion 必须在 target run 的下一次 WorkMain continuation 外发前完成。Promotion
CommitUnknown 必须重读 projection resolve；无法证明时 continuation fail closed。**已经
Promoted 的 frame 是不可删除的语义历史。**

**含义/动机**：只有「primary 真的看见了」才把干预升级为历史；证据必须是该 run 的真实
输出，不是 Host bookkeeping。

**边界**：ReconciledTurn/ProviderRun 的通用语义 → 各领域 owner；「Promotion 的资格条件」是
本命题。

**证据**：HOW.md SPEC-INV-007 行（commit-promotion、turn-evidence、lifecycle-recovery/
store REUSE）。

## SPEC-INV-008：Replay 与 XTrace closure

**规范陈述**：当前 target request 中的 Candidate 在 XTrace capture 之后注入，因此 Candidate
∉ XTrace。Promotion 后的下一次主 transform 必须在 XTrace capture 之前把 Promoted frames
确定性重建到其因果位置——目标 assistant output 之前；随后 XTrace capture 使其进入 durable
semantic timeline，并以 `StrengthFramesTraced` 记录对应 XTrace cursor range。Traced 只能发生
在 Promoted 之后，range 必须单调且可由 deterministic frame identity 幂等恢复。

Promoted frame 在现有 Companion/prefix representation 能以 XTrace coverage 证明覆盖前必须
继续可 raw replay；**物理 cutoff 不能代替语义 coverage**。Companion 只能消化 Promoted
frame，永不消化 Candidate。

**含义/动机**：Promoted 历史不能从语义历史消失；raw replay 只由语义 coverage 退休。

**边界**：XTrace cursor、Companion coverage 的 owner → `semantic-trace`/`context-compression`；
本命题只规定「Promoted → 确定性重建 → Traced 记录」这一侧。

**证据**：HOW.md SPEC-INV-008 行（lifecycle-recovery、frame-projection/projection-algebra
REUSE、integration lifecycle REUSE）。

## SPEC-INV-009：Projection 与 no-reflection

**规范陈述**：Replica provider message base = owner 在 post-Enforcer、pre-Strength-candidate、
pre-Pair-marker 冻结点的 ProviderSemanticProjection + 本 decision 已完成 local batches；
Replica 的 model/system/tool schema 仍来自自身 AttemptExecutionProfile。跨 Session 只允许
语义等价：owner ToolCallId 不得进入 Replica wire，Host 必须先确定性重定位为 decision-local
id 并证明 `ProviderSemanticProjection` 不变；media/孤儿 result 等无法无损重定位的历史 → K0。
当前 Candidate 在 freeze 之后才产生，因此**不能反射回当前 Replica**。fresh decision 不复用
旧 Replica transcript；过去已经 Promoted 的历史作为 owner 正常语义可以出现在未来 decision
mirror。

Strength frame 插入必须早于 PairProgrammingThought marker；Host sanitization 仍在最终 provider
bytes 发送前执行。Candidate wrong-target render、同 anchor 不同 payload、Strength mirror 与普通 Work
base selection 同时出现均为 ProjectionConflict。

**含义/动机**：投影是跨 Session 唯一可比的面；wire id 是本地表示。Replica 不能看见自己
正在被预读这件事。

**边界**：ProjectionAlgebra 通用性质 → `provider-projection`；Pair marker / Host sanitization 的
writer 顺序 → 各自 owner（本命题只规定 Strength frame 的相对位置）。

**证据**：HOW.md SPEC-INV-009 行（projection-algebra、projection-adapter、frame-projection
REUSE）。

## SPEC-INV-010：Predictor 与 control

**规范陈述**：第一版默认 Shadow：eligible opportunity 只预测、始终 K0，并观察后续 primary
request。仓库不因架构实现完成而宣称正收益 cohort。K1 treatment 只在显式成本、exact Host
canary fingerprint、deterministic control 与足够 predictor evidence 同时成立时才可能启用；
任一缺失 → K0。K2 必须独立通过更高 margin、evidence floor 与稳定窗口，**不继承 K1**。

启用 Treatment 后仍保留 restart-stable deterministic control holdout；control assignment 只
由 AuthorityRoot、TargetProviderRun 与 PolicyVersion 等冻结事实决定，不由 predictor score 或
运行时 RNG 决定。训练 label 只来自 shadow/control primary request sequence；**Replica
intervention request 永不成为 counterfactual label。**

K1/K2 由纯 value policy 比较 `V0=0`、`V1`、`V2`；成本必须来自显式 provider usage/price
metadata 或 Host-internal cost class，不能从 Fast/Deep 名字推断。K1 需超过正 margin；K2 需
更高 margin、满足最小 evidence floor，且 K2 margin > K1 margin。成本未知 → K0。

**含义/动机**：干预不能冒充观测。没有干净 label 就没有 treatment；K2 不搭 K1 的便车。

**边界**：predictor 的具体特征/分桶模型 → HOW；「label 只来自 shadow/control、control
restart-stable、K 门禁独立」是命题。

**证据**：HOW.md SPEC-INV-010 行（authority-policy、predictor-rollout REUSE）。

## SPEC-INV-011：失败、取消与熔断

**规范陈述**：Replica 创建/请求/工具普通失败只终止本 decision；已完整验证 batch 可按规则
使用，未完整 batch 丢弃，owner 正常继续。owner cancellation/delete 级联取消并 retire
Replica，未消费 Candidate 不 Promotion。durable Candidate/Promotion 歧义、wrong-target
render、ProjectionConflict、schema/execution-gate mismatch、promotion recovery failure、
关键 Host canary 失败或成本/质量熔断时，新 decision 全部 K0；process-local fuse 一旦因这些
一致性失败触发，在进程余生不得重新开启新 speculation。Host canary 必须绑定当前安装的
OpenCode/plugin 版本指纹，不能以通用 `true/pass` 代替。已有 Promoted history 仍必须
replay/recover，已有 Candidate 只按其已绑定 target run 完成或自然失效。

**含义/动机**：普通 pre-commit 失败可以 fail-open（损失只有成本）；durable/consumed-history
歧义必须 fail-closed（猜错就是污染或丢失历史）。

**边界**：owner cancellation 的通用级联语义 → `managed-session-lifecycle`；canary 指纹的
具体格式 → HOW。

**证据**：HOW.md SPEC-INV-011 行（host-policy、commit-promotion、host-canary-k0 REUSE）。

## SPEC-INV-012：模型不可见、系统可审计

**规范陈述**：primary 与 Replica provider-visible bytes 不得暴露
`strength/replica/prefetch/weak model/confidence/budget/prediction/source=sidecar` 等机制
provenance；Replica 也不接收「替另一个模型预读」的身份提示。Host/EventStore diagnostics
可以记录 DecisionId、ReplicaSessionId、TargetProviderRun、K、digest、predictor
features/score、cost estimate、promotion evidence 与 failure reason。Host-only metadata 若
用于幂等，必须证明不进入 ProviderSemanticProjection。

**含义/动机**：机制不可见 ≠ 系统不可审计。模型字节只承载语义事实，不承载机制。

**边界**：diagnostics/EventStore 的存储语义 → `durable-events`；「哪些字节对模型可见」的
投影规则 → `provider-projection`。

**证据**：HOW.md SPEC-INV-012 行（invisibility REUSE、projection-algebra REUSE）。

## SPEC-INV-013：DryRun = 可见、真实、非阻塞、零 Promotion 的 shadow execution

**规范陈述**：显式 DryRun 模式必须创建并运行真实 `StrengthReplica` physical child；该 child
必须作为 OpenCode 中可观察的 attached/internal execution 出现在用户可见 session/transcript/tool
activity 中。**owner 主路径不得等待 DryRun completion、budget deadline 或 terminal result**：
Replica 成功启动后 owner provider transform 立即继续。DryRun 可真实执行 K1/K2 readonly provider/tool
请求并记录 Host diagnostics；DryRun terminal diagnostics 可携带 `replica_session_id` 作为该可见
physical child 的观察性 identity provenance，但该字段不得成为任何控制输入。其结果不得映射回 owner
provider bytes，不得产生 `StrengthCandidatePrepared` / `StrengthCandidatePromoted` / replay frame，也不得
改变 owner fallback/repair/finality 状态。owner cancel/delete 仍级联取消该 child。

**含义/动机**：Dry 的是 semantic influence，不是 physical execution。用户能直接观察 Strength 是否真的
做了有价值的只读调查，同时一次 canary/实验永远不把 2500ms Replica deadline 加到 owner critical path。

**边界**：Treatment 仍可因需要消费 Candidate 而等待有界结果；Shadow/K0 可完全不创建 Replica。
OpenCode child 的可见性/attached ontology 归 `session-ontology`/Host，本命题只钉 Strength 对该能力的使用。

**证据**：→ HOW.md SPEC-INV-013。
