# semantic-trace — WHAT（唯一 normative 合同）

条款前缀：`SEMANTIC-TRACE-`。每条命题都是**当前世界必须同时成立**的事实。
证据指针 → `PROOF.md` 对应行号。

---

## SEMANTIC-TRACE-001：XTrace 是 X 的唯一 append-only 原始语义历史

**规范**：X（Work Session）的 lifecycle 语义轨迹是 append-only 的 `XTrace`
（`Domain/XTrace.fs`），它是 X 的**唯一**原始语义历史。一个 X 生命期内：
`Opening` 捕获一次且永不覆盖；`Parts` 按 cursor 严格增长；`Terminal` 捕获一次
（同 ref+digest 的重放是幂等 no-op）。

**含义/动机**：没有第二份「原始历史」。Host transcript 数组下标可被 compaction 重编号；
任何基于 transcript 下标的历史都不是 canonical。

**边界**：XTrace 的**存储机制**（如何 append、如何 fold 拒绝）归 `durable-events`（PERSIST-010）；
本命题拥有「唯一 + append-only + 三事实捕获」的语义。XTrace 是 `work-record` 物化的 source
material，不是第二事实源。

**证据**：历史 HOST-005；历史 COMPANION-003；
`src/Wanxiangshu/Domain/XTrace.fs`；`src/Wanxiangshu/Context/Trace/Projection.fs`。

---

## SEMANTIC-TRACE-002：typed capture 边界

**规范**：XTrace 包含：Host 可见 prompt、assistant 正文、host-visible reasoning、tool call/result、
omission marker。**不包含**：UI delta、usage、cost、timestamp、directory、finish reason、runtime ID。
`MessagePart → SemanticPart` 映射只有一条（`XTraceCapture.semanticPart`）；`Activity` part 是
transport bookkeeping，被丢弃不映射。

**含义/动机**：capture 边界是 typed 的，不是「挑着记」。工具调用号、Host 消息 id 属于
provenance（证明/恢复用），renderer 永不输出它们（SEMANTIC-TRACE-005）。

**边界**：渲染形状（wire）归 `provider-projection`；「哪些 part 语义上算数」是本命题。
媒体以 omission marker 记录存在性，不记内容——媒体字节本身不是语义文本。

**证据**：HOST-005；`src/Wanxiangshu/Context/Trace/Capture.fs`
（`semanticPart`、`partShape`）。

---

## SEMANTIC-TRACE-003：cursor 严格单调、独立于 Host 坐标

**规范**：`XTraceCursor` 在 lifecycle 内严格单调（`XTrace.nextCursor` / `XTrace.isAfter`），
独立于 Host transcript 数组下标与 semantic turn 编号。同 cursor 重复 append 是拒绝条件
（PERSIST-010 `CursorNotAfterHead`）；cursor 回退同样拒绝。Host compaction 后 turn 编号
从 0 重来（`ContextReanchored` 的 `Snapshot=None`），但 XTrace cursor 与 RecordCoverage
是 durable lifecycle facts，**不得**随 compaction 清零或重读。

**含义/动机**：frontier 定位必须不随 Host 视图变化。RecordCoverage 可落在 turn 中间，
只有 cursor 能表达这种「半 turn」位置。

**边界**：「compaction 不得清零 XTrace」还要求 reanchor 只动 PrefixCoverage —— 见
SEMANTIC-TRACE-009；epoch 语义本身归 `prefix-stability`。

**证据**：HOST-005/006；`Domain/XTrace.fs`（`originCursor`/`nextCursor`/`isAfter`）；
`Context/Trace/Projection.fs`（`headSequence`/`applyPart`/`CursorNotAfterHead`）。

---

## SEMANTIC-TRACE-004：provenance 按 provider run 分段

**规范**：XTrace 的 provenance 按 provider run 分段（因 Fallback / Agent 切换），
不用单值 `Model`。每个 `XTracePartRef.ProviderRun` 记录该 part 归属的 provider run；
reanchor 后 provenance 携带新的 generation（`g:N/...`），使重编号的 Host turn 不会与
旧编号碰撞。

**含义/动机**：Peer Fallback 换模型是同一人换执行绑定，历史语义连续；provenance 必须能
区分「同一 turn 编号在不同 run 里」而不是用一个模型名假装唯一身份。

**边界**：ExecutionBinding 变化语义归 `participant-identity` / `provider-attempt-recovery`；
本命题只要求 trace 的定位身份按 run 分段。

**证据**：HOST-005；`Context/Trace/Projection.fs`（`provenanceGeneration`、
`currentGenerationParts`）；`Context/Trace/Capture.fs`
（`captureGeneration`、`captureSourcesStable` 的 `g:N/msg:...` provenance）。

---

## SEMANTIC-TRACE-005：semantic parts 与 transport/wire identity 分离

**规范**：XTrace item 携带 `Provenance`（session/run/message/part 归属），但
`XTrace.render` 的输出**永不**包含 provenance；tool call id / Host tool part id 只作
定位与证明，不进入渲染文本。`XTrace.render` 是确定性的：同输入同输出（COMPANION-012）。

**含义/动机**：Y delta、LWR gap、terminal capture 都从同一渲染消费；若渲染混入 call id，
同一对话重放（新 call id）会被误判为新内容。

**边界**：确定性的「比较」函数（`ProviderProjection.semanticallyEqual`）跨包共享，但
「XTrace 自身渲染稳定」是本命题。

**证据**：`Domain/XTrace.fs`（`renderItem`/`render`：assistant 正文不带 role 前缀、
tool 渲染为 `[tool call] name args`）。

---

## SEMANTIC-TRACE-006：稳定 frontier / range / cutoff

**规范**：XTrace 提供半开区间定位：`sliceBetween [start, endExclusive)`、
`sliceFrom [start, head)`、`head` = 最后一条之后（空轨迹为 origin）。`RecordCoverage =
{ IngestedThrough: XTraceCursor }` 是 Y 已消化位置的 durable 游标（COMPANION-003），
决定 LWR gap 起点，可落在 turn 中间。

**含义/动机**：任何消费者（LWR 物化、Blogger chunker、review frontier）都必须能对
同一段历史给出**可证明相同**的 range。半开区间让「covered through N」与「next starts at N+1」
无歧义。

**边界**：`PrefixCoverage`（完整 turn 边界、digest 证明）是另一种量纲，属于
`context-compression` / `prefix-stability` 的 coverage 分型，本命题只拥有 RecordCoverage
作为 XTrace 游标的事实。

**证据**：`Domain/XTrace.fs`（`sliceBetween`/`sliceFrom`/`head`）；
`Context/Trace/Projection.fs`（`head`/`parts`/`semanticCursorFor`）。

---

## SEMANTIC-TRACE-007：XTrace 是 Y delta / LWR gap / terminal 的单一 source

**规范**：送 Y 的 delta 与 LWR gap **同源** XTrace、**不同投影**：delta 可含 tool 作压缩输入；
LWR gap 剔除 raw tool（`XTrace.isWorkRecordPart` / `forWorkRecord`）。canonical digest 用
Semantic projection，禁止反向解析 TOML（COMPANION-007）。同一 segment 的语义解析不得分叉
（`XTrace.flatten` 是唯一平铺）。

**含义/动机**：两套解析器早晚给出两套「同一段话」；digest 失配 fail closed 的前提是
「只有一种 canonical 表示」。

**边界**：delta 的压缩用途归 `context-compression`；LWR 的三段形状归 `work-record`；
本命题只拥有「同源 + 投影分叉」这个事实本身。

**证据**：COMPANION-007；`Domain/XTrace.fs`（`flatten`/`isWorkRecordPart`/`forWorkRecord`）；
`Context/Trace/Projection.fs`。

---

## SEMANTIC-TRACE-008：未发生材料永不写成历史

**规范**：capture 不得把未发生的 speculative / provider-local state 提前写成历史：
- Strength Candidate（未 Promote）永不进入 XTrace / LWR / PrefixSnapshot
  （STRENGTH-006/008；`StrengthReplay` 只重放 durable Promoted frames）；
- X 的失败 prefix probe 不写任何事实（CTX-010：无 `PrefixProbeRolledBack` 类事实）；
- 可 append 的 XTrace 事实集只有 `OpeningPromptCaptured` / `XTracePartAppended` /
  `TerminalOutputCaptured`——没有「candidate frame」事实族。

**含义/动机**：提前写入 = 用未发生的干预污染未来请求；事后回滚 = 删除真实因果。两者都破坏
「历史 = 已发生」。（与历史 why/strength 的 Candidate ≠ 历史同构。）

**边界**：promotion 的因果链（何时 Candidate 算被消费）归 `speculative-investigation`；
本命题拥有 capture 侧负律。

**证据**：历史 why/context（probe 失败不写事实）；历史 why/strength；
历史 change（strength）；`src/Wanxiangshu/Context/Trace/Projection.fs`（无 candidate 事实）。

---

## SEMANTIC-TRACE-009：Host compaction 不得删除 XTrace

**规范**：Host compaction **不得删除** XTrace：否则 Y 落后补缺口与 LWR 自包含同时失效
（HOST-005）。compaction 唯一合法收容语义是 `ContextReanchored`（`EpochId+1`、
`Snapshot=None`、PrefixCoverage 归零），它不选择压缩内容、不替代失败驱动恢复；XTrace
parts / Opening / RecordCoverage / Frames 全部存活（COMPANION-008）。

**含义/动机**：compaction 作废的是 Host 索引映射，不是「工作真实发生过」这个事实。
删除 XTrace = 把「证明发生过」的载体销毁。

**边界**：`ContextReanchored` 的 epoch 语义归 `prefix-stability`；「什么时候允许重锚」归
`context-compression`（HOST-006 containment）；本命题只拥有「XTrace 不被删除」。

**证据**：HOST-005/006；COMPANION-008；`Context/Companion/Blogger/ContextFactFold.fs`
（`ContextReanchored` 分支只更新 PrefixEpoch + Blog + TipDelivery，不动 XTrace）。

---

## SEMANTIC-TRACE-010：Opening 在 trace 内 preserved

**规范**：`OpeningMaterial` = exact XTrace 区间 `[work start, OpeningBoundary)`
（COMPANION-014）。`XTrace.forOpening` 保留 constitutive commitment 材料（BlindPlan 下
T1 `todowrite` call + canonical accepted result）；不得当 incidental tool 滤掉。Opening
捕获幂等：同文本重放 no-op，**不同**文本拒绝（PERSIST-010 `OpeningAlreadyCaptured`）。

**含义/动机**：Opening 是「章程」，不是可重拼 blob。T1 交托 call/result 属 Opening 的
constitutive 材料，一旦滤入 Recent work，Opening 就缺了「交托关闭」这一段。

**边界**：Opening 何时关闭（OpeningPolicy：Immediate / BlindPlan T1）归 `work-record`
（WORK-RECORD-008）与 `obligation-ledger`（GLORY-074）；「Opening 不送 Y 压缩」归
`context-compression`。本命题只拥有「Opening 区间在 trace 内是 preserved 的原始材料」。

**证据**：COMPANION-014；`Domain/XTrace.fs`（`forOpening` = identity）；
`Context/Trace/Projection.fs`（`applyOpening`：同文本幂等、异文本拒绝）；
`Application/Finality/LifecycleWorkRecordProjection.fs`（`withConstitutive`）。
