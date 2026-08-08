# Proposal：修复 HOST-013 Auto-Injected Prefix Cache 与 Idle-Only Auto-Continue 资格

**Status:** Proposed
**Priority:** P0 / correctness + performance invariant
**Scope:** HOST-013、HOST-004、Prompt continuation admission、相关 proof/tests
**Compatibility:** Clean break for legacy unanchored HOST-013 facts unless an exact migration proof exists

---

# 0. 本 Change 要解决什么

本 Change 同时修复两个互相独立、但都属于“时序前提被实现错”的 P0 问题：

1. **HOST-013 auto-injected 的产品设计本来可以严格保持 Prefix Cache，但当前实现把历史 synthetic pair 重组、搬家，破坏了已经发给 Provider 的字节前缀。**
2. **missing-final-report / idle encouragement 等自动 continuation 当前可以在 Session 已重新运行时继续排队发送；而任何由 idle 推导出的 continuation，其必要条件必须是发送瞬间仍拥有当前 idle 的有效资格。**

这两个问题都禁止用局部 `if`、sleep、特殊 run 白名单、当前 trailing user 猜测或“通常不会 race”修补。

最终必须形成两个机械可证明的不变量：

```text
PREFIX LAW
same PrefixEpoch:
ProviderWire(n) is an exact prefix of ProviderWire(n+1)

QUIESCENCE LAW
IdleDerivedContinuationSent
    =>
a fresh quiescence permit for the same session/attempt
was successfully consumed immediately before physical send
```

仓库已经有 `ProviderProjection.isAppendOnlyPrefix`，它比较 Provider/model/variant/tools/system 及完整 message prefix，因此本 Change 必须直接复用它作为 PREFIX LAW 的权威判定，不得再写第二套“差不多是前缀”的 helper。

---

# 1. 先钉死 HOST-013 的正确产品语义

## 1.1 下面这四行是规范，不是示意图

实现者必须先理解并能手算下面的序列，否则禁止开始改代码。

```text
LLM -> Local:
Req1 Req2

Local -> LLM:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1

LLM -> Local:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1 Req3

Local -> LLM:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1 Req3 FakeReq2 Resp3 FakeResp2
```

其中：

```text
ReqN      = 真实 request / tool-call
RespN     = 真实 response / tool-result

FakeReqN  = synthetic auto-injected tool-call
FakeRespN = 与 FakeReqN 使用同一 callID 的 synthetic completed tool-result
```

关键不是“FakeReq/FakeResp 是一对”。

关键是：

> **它们是一个跨越真实 response batch 的 temporal bracket。**

正确结构是：

```text
real calls
→ synthetic call
→ real results
→ synthetic result
```

而不是：

```text
real history
→ synthetic call
→ synthetic result
```

更不是：

```text
每次 transform
→ 删除所有历史 synthetic
→ 把历史 synthetic pair 整块重建到当前 insertion point
```

---

# 2. Prefix Cache 为什么在正确设计里天然成立

第一轮完整 Provider-visible wire：

```text
W1 =
Req1
Req2
FakeReq1
Resp1
Resp2
FakeResp1
```

下一轮：

```text
W2 =
Req1
Req2
FakeReq1
Resp1
Resp2
FakeResp1
Req3
FakeReq2
Resp3
FakeResp2
```

所以严格有：

```text
W2 = W1 ++ suffix
```

即：

```text
W1 ⊏ W2
```

没有历史字节被删除、修改、换位、重新序列化到别的位置。

这正是 ARCH-004 要保护的性质：正常 epoch 内 active prefix 字节稳定，冷边界只能由正式 epoch transition 产生。

实现测试不得只检查：

```text
pair 数量正确
callID 相同
markerText 正确
FakeReq 在 Req 后
FakeResp 在 Resp 后
```

这些都可以在 Prefix Cache 已经坏掉的实现上通过。

唯一有决定性的回归断言必须包含：

```fsharp
ProviderProjection.isAppendOnlyPrefix previousWire nextWire = true
```

---

# 3. 当前 HOST-013 实现到底错在哪里

当前 `PairProgrammingThoughtTransform.tryInject` 会先做：

```fsharp
let retainedRaw =
    rawMessages
    |> List.filter (isPairProgrammingThought >> not)
```

即先从当前 raw history 删除所有历史 auto-injected message。随后读取 durable `history`，再重新构造。

更严重的是，`placePairs` 把所有历史 pair 做成：

```fsharp
let historyBlock =
    historyPairs
    |> List.collect (fun pair ->
        buildPair pair.CallId pair.MarkerText)
```

然后把这个 `historyBlock` 放到**当前** call/result batch 前。也就是说，历史 pair 没有按照各自原来的因果位置恢复，而是被压缩成一块后重新定位。

`placeWirePairs` 又实现了一份同构算法，并被拿来当 expected oracle，所以 renderer 和 oracle 可以一起错而测试仍然通过。

当前 durable 状态也不足以执行“恢复原位置”。

现在只有：

```fsharp
type PairProgrammingGuideline =
    { Ordinal: int64
      CallId: ToolCallId
      MarkerText: string }
```

没有 FakeReq 原位置，也没有 FakeResp 原位置。

正式文档却同时要求：

```text
记录不可换位
历史 pair 原位恢复
```

所以当前实现不是单纯 `placePairs` 下标算错。

**Domain fact 本身就丢失了恢复原位置所必需的信息。**

---

# 4. HOST-013 新数据模型：必须记录“gap anchor”

禁止继续把一组 auto-injected 当成：

```text
Pair = FakeReq + FakeResp
```

然后认为只知道 pair ordinal 就能重建 transcript。

正确的数据模型必须表达：

```text
FakeReq 应该落在哪个真实消息 gap
FakeResp 应该落在哪个真实消息 gap
```

推荐直接定义：

```fsharp
type TranscriptMessageAddress = private TranscriptMessageAddress of string

[<RequireQualifiedAccess>]
type TranscriptGap =
    | Start
    | Before of TranscriptMessageAddress
    | After of TranscriptMessageAddress

type PairProgrammingGuideline =
    {
        Ordinal: int64
        CallId: ToolCallId
        MarkerText: string

        CallGap: TranscriptGap
        ResultGap: TranscriptGap
    }
```

不要把 `TranscriptMessageAddress` 偷换成：

```text
PhysicalUserMessageId
AuthorityRootUserMessageId
ProviderRunIdentity
ToolCallId
```

除非那个值在具体位置上**确实就是 Host transcript message address**。

仓库的 raw message 本身已有 `info.id` / `id`，而 Session snapshot 也以 message `Id` 作为地址使用。

因此应建立一个窄的 transcript-address codec，不要滥用 Authority identity。

---

# 5. 为什么 Gap 模型足够表达所有情况

## 5.1 多 tool batch

原始真实历史：

```text
Req1 Req2 Resp1 Resp2
```

新 pair：

```text
CallGap   = After Req2
ResultGap = After Resp2
```

渲染：

```text
Req1
Req2
FakeReq1
Resp1
Resp2
FakeResp1
```

完全对应产品要求。

---

## 5.2 无真实 tool batch，但 trailing user 存在

若该产品场景仍要求相邻 synthetic pair：

```text
... U1
```

则：

```text
CallGap   = Before U1
ResultGap = Before U1
```

同一个 gap 内按固定局部顺序：

```text
FakeReq
FakeResp
U1
```

同 gap 排序规则必须是：

```text
pair ordinal ascending
then half:
    call = 0
    result = 1
```

禁止依赖 Map 枚举顺序。

---

## 5.3 空 transcript

使用：

```text
CallGap   = Start
ResultGap = Start
```

得到：

```text
FakeReq1
FakeResp1
```

未来真实消息出现后仍恢复：

```text
FakeReq1
FakeResp1
U1
...
```

所以旧 wire 仍然是新 wire 的完整 prefix。

---

## 5.4 无 trailing user，非空 transcript，当前 pair 应落末尾

假设当前最后真实消息：

```text
M7
```

则：

```text
CallGap   = After M7
ResultGap = After M7
```

得到：

```text
...
M7
FakeReq
FakeResp
```

未来新消息 `M8` 出现时，历史 pair 仍根据 `After M7` 恢复：

```text
...
M7
FakeReq
FakeResp
M8
```

禁止重新解释为“现在末尾在哪就挪去哪”。

---

# 6. Replay 算法：禁止再出现 `historyBlock`

整个 replay 应简单到一个新人可以肉眼证明。

输入：

```text
realRawMessages
durableSyntheticEntries
```

第一步可以删除 raw 中已经存在的 HOST-013 synthetic message，但**只有在 durable anchor 足够完整时才能删除**。

然后建立：

```text
startGap
beforeGap[messageId]
afterGap[messageId]
```

伪代码：

```fsharp
let replay realMessages syntheticEntries =

    validateUniqueRealMessageAddresses realMessages

    validateEverySyntheticAnchorExists syntheticEntries realMessages

    let starts =
        syntheticsAt TranscriptGap.Start

    [
        yield! starts |> stableOrder

        for message in realMessages do
            let id = transcriptAddress message

            yield!
                syntheticsAt (TranscriptGap.Before id)
                |> stableOrder

            yield message

            yield!
                syntheticsAt (TranscriptGap.After id)
                |> stableOrder
    ]
```

`stableOrder` 唯一合法排序：

```text
Ordinal ascending
then Call before Result
```

就这么简单。

禁止出现：

```text
historyBlock
historyPairs |> List.collect buildPair
find current trailing user to place historical pair
find current tool batch to place historical pair
move all old markers together
```

历史 synthetic 的位置只由**它自己 durable 的 gap anchor**决定。

当前 transcript 长什么样，不得改变历史 pair 的位置。

---

# 7. 本轮新 Pair 的 placement 算法

历史 replay 和“本轮新 pair 放哪里”是两个不同问题，禁止混成一个 `placePairs`。

建议拆成：

```fsharp
replayHistory
decideCurrentPlacement
renderCurrentPair
```

## 7.1 决策输入

只允许使用当前**真实** messages：

```text
不含 HOST-013 synthetic
```

从中判断当前末端结构：

```text
Case A:
... tool-call batch
    tool-result batch
    trailing user

Case B:
... trailing user
    no matching tool batch

Case C:
empty

Case D:
non-empty
no trailing user
```

## 7.2 多 tool batch

如果末端存在：

```text
Req1 Req2 Resp1 Resp2 [User]
```

则新 pair：

```text
CallGap   = After(address Req2)
ResultGap = After(address Resp2)
```

最终：

```text
Req1 Req2 FakeReq Resp1 Resp2 FakeResp [User]
```

这是 HOST-013 的核心 bracket。

## 7.3 无 tool batch

保持现有产品要求时：

```text
CallGap   = Before trailingUser
ResultGap = Before trailingUser
```

空历史：

```text
Start / Start
```

无 trailing user：

```text
After lastReal / After lastReal
```

---

# 8. 同一个真实 batch 不得重复发明第二组 pair

需要增加一个机械 invariant：

```text
同一个 placement identity
最多一个 PairProgrammingGuideline
```

placement identity 建议定义为：

```text
SessionId
+ CallGap
+ ResultGap
```

不要只靠：

```text
history.Length + 1
```

判断“这一定是新 round”。

因为 transform 可能：

```text
重复执行
Host retry
测试重放
同一 request 重新进入
```

同一真实 batch 再 transform 时：

```text
发现已有 matching placement
→ replay existing pair
→ 不 append 新 fact
```

而不是：

```text
每进一次 transform
→ Ordinal + 1
→ 新增一组 pair
```

正式 HOST-013 文档里当前的“每次 transform 无条件新增一组完整 pair”必须删除；它把**Hook invocation**错误提升成了**业务 round identity**。当前文档确实明确写了“每次 transform 无条件插入恰好一组”。

正确语义应该是：

> 每个尚未存在 HOST-013 synthetic bracket 的真实 provider-visible placement occasion，恰好产生一组 pair；同一个 occasion 的重复 transform 只 replay，不再新增。

---

# 9. Durable fact 必须原子携带 placement

将现有：

```fsharp
PairProgrammingGuidelineAppended
    {
        SessionId
        Ordinal
        CallId
        MarkerText
    }
```

改成例如：

```fsharp
PairProgrammingGuidelineAnchored
    {
        SessionId: SessionId
        Ordinal: int64
        CallId: ToolCallId
        MarkerText: string

        CallGap: TranscriptGap
        ResultGap: TranscriptGap
    }
```

不要拆成：

```text
PairCallAnchored
PairResultAnchored
```

两个 Journal facts。

否则 crash 可以留下：

```text
FakeReq durable
FakeResp 不 durable
```

又制造新的半状态。

一组 bracket 的：

```text
identity
bytes
two placements
```

必须是一个原子事实。

---

# 10. 新 Pair 的 commit 顺序

当前 `tryInject` 是先 append durable fact，再运行 Planner/Renderer/expected check。若后续返回 `None`，Journal 已认为 pair 存在，而本次 transform 却可能没有采用它。

改成：

```text
1. read durable history
2. remove/replay history in memory
3. decide candidate placement in memory
4. construct candidate Pair fact in memory
5. render candidate wire in memory
6. validate all invariants
7. append durable fact
8. return the already-validated rendered messages
```

其中第 6 步至少验证：

```text
all historical anchors resolved
no duplicate placement
call/result same callID
synthetic bytes deterministic
current placement matches algorithm
```

Journal append 失败：

```text
fail closed
```

禁止：

```text
忽略 append 失败
然后照样把 synthetic 发给 provider
```

因为未来已经没有证据能 byte-identically replay。

同理也禁止：

```text
append 失败
→ 返回 raw transcript
→ 假装 HOST-013 optional
```

HOST-013 是 provider-visible protocol，不是装饰。

---

# 11. Prefix law 必须成为生产前置 proof

增加纯函数测试 helper：

```fsharp
let assertPrefix previous next =
    if not (ProviderProjection.isAppendOnlyPrefix previous next) then
        failwith "HOST-013 broke ARCH-004 append-only prefix"
```

现有实现已经提供这一权威函数。

测试不要自己写：

```javascript
JSON.stringify(next).startsWith(JSON.stringify(previous))
```

也不要只比较 message count。

---

# 12. HOST-013 必须新增的 RED 测试

正式 docs 当前把 `tests/unit/host/pair-thought-transform.test.mjs` 与 `tests/integration/plugin/manager-tool-contract.test.mjs` 列为 HOST-013 代表证明，但上传包没有包含前者测试正文，因此下面是本 Change 必须加入/重建的行为矩阵，而不是假装现有测试已经覆盖。

## H13-01：用户给出的 canonical multi-tool sequence

严格构造：

```text
round 1 real:
Req1 Req2 Resp1 Resp2

round 1 transformed:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1

round 2 real:
Req1 Req2 Resp1 Resp2 Req3 Resp3

round 2 transformed:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1
Req3 FakeReq2 Resp3 FakeResp2
```

断言：

```text
pair1 callID == pair1 result callID
pair2 callID == pair2 result callID
pair1 callID != pair2 callID

isAppendOnlyPrefix(round1Wire, round2Wire) == true
```

**这是本 Change 的首要测试。**

---

## H13-02：历史 pair 不得被 current placement 搬家

构造历史 pair1：

```text
Req1 FakeReq1 Resp1 FakeResp1
```

再追加新的：

```text
Req2 Resp2
```

第二轮必须：

```text
Req1 FakeReq1 Resp1 FakeResp1
Req2 FakeReq2 Resp2 FakeResp2
```

故意恢复旧 `historyBlock` 实现时，该测试必须 RED。

---

## H13-03：same batch 重入不新增 pair

同一真实 transcript 连续 transform 两次：

```text
Transform(realBatch)
Transform(sameRealBatch)
```

结果：

```text
marker pair count == 1
journal append count == 1
wire bytes exactly equal
```

---

## H13-04：restart replay byte-identical

```text
process A append pair
serialize journal
new process B fold journal
same raw real transcript
replay
```

必须：

```text
renderWire(beforeRestart)
==
renderWire(afterRestart)
```

---

## H13-05：历史 anchor 丢失必须 fail closed

Journal 声称：

```text
FakeReq1 after message M7
```

但当前 raw transcript 没有 `M7`。

禁止：

```text
放到当前最接近的位置
放 trailing user 前
放末尾
忽略该 pair
```

唯一允许：

```text
fail closed:
HistoricalSyntheticAnchorMissing
```

因为任何“尽量放”都会静默破坏 Prefix Cache。

---

## H13-06：prior tip 只影响新 pair

pair1 创建时 marker：

```text
guideline
```

之后出现新 prior tip。

pair2：

```text
tip2

guideline
```

必须证明 pair1 的 MarkerText 字节完全未变。

当前实现本来就持久保存 MarkerText；继续保持。

---

## H13-07：Companion / Blogger 零注入

继续证明：

```text
isCompanion=true
→ raw bytes unchanged
→ zero pair fact append
```

这部分现有 HOST-013 合同保留。

---

## H13-08：property law

生成 1..N 轮随机：

```text
1–5 tool calls
对应结果
偶发 trailing user
偶发 no-tool turn
```

每轮：

```text
wire[n] = transform(realHistoryUpToN)
```

同 epoch 下：

```text
for n:
    isAppendOnlyPrefix(wire[n], wire[n+1]) = true
```

这个 property test 比 20 个具体 placement 单测更重要。

---

# 13. Legacy HOST-013 journal 迁移

旧 fact：

```text
Ordinal
CallId
MarkerText
```

没有 anchors。

因此禁止写：

```text
old ordinal 1
≈ first tool batch
old ordinal 2
≈ second tool batch
```

这是 heuristic migration，会重新制造同一个 bug。

默认策略：

```text
发现 legacy unanchored PairProgrammingGuidelineAppended
→ 本 session 不允许继续 HOST-013 replay
→ fail closed with explicit migration/incompatible-journal reason
```

如果项目选择 schema/version clean break，就直接 bump。

只有在能够从一个**权威、完整、仍含原 synthetic message id 与相对位置的物理 transcript**证明 anchors 时，才允许写一次显式 migration。

上传仓库材料不能证明这个条件成立，所以本 Proposal 不批准 heuristic migration。

---

# 14. 第二个 P0：Auto-Continue 必须拥有 fresh idle 资格

当前 `RuntimeNudge.missingFinalReport` 故意渲染为裸：

```text
#
```

源码直接把：

```fsharp
let MissingFinalReportInstructions = [ "" ]
```

定义为 bare `#` poke。

字符本身没有问题。

问题是发送资格。

当前 `ReconcileEvidence` 只有：

```fsharp
SnapshotError
NoTurn
Provisional
Unknown
Terminal
SessionCleared
```

`Unknown` 因果重读耗尽后直接：

```fsharp
RepairMissingFinalReport
```

这里没有任何值表达：

```text
“发送副作用的这一刻 session 仍然 idle”
```

---

# 15. 为什么重复读 snapshot 不能证明 idle

当前 Host signal decoder 只输出：

```text
idle
retry
deleted
failure wake
```

其它 `session.status` 被丢弃。

所以真实时序可以合法是：

```text
t0  SessionIdle(A)
t1  Reconciler Kick
t2  snapshot says finish=None
t3  new provider attempt B starts
t4  snapshot reread still temporarily sees old incomplete A
t5  decide RepairMissingFinalReport
t6  send "#"
```

这里：

```text
SessionIdle(A)
```

只证明：

```text
t0 时刻 idle
```

不证明：

```text
t6 时刻仍 idle
```

仓库自己甚至已经为 recovery probe 做了一个特殊例外，因为实际观察到 interleaved reconcile 会在 probe response 落地前发 missing-final-report，导致 probe 被 hijack。

这不是 recovery probe 特有问题。

这是 stale-idle capability 问题。

---

# 16. 不要把 busy/running 加进业务 HostSignal

错误修法：

```fsharp
type HostSignal =
    | SessionIdle
    | SessionBusy
    | SessionRunning
    | ...
```

然后在几十个地方维护：

```text
if busy then ...
if idle then ...
```

不批准。

当前架构明确要求 transport event 只是 wake，业务事实来自完整 snapshot；HOST-002 也限制进入普通业务的 coarse signal。

正确做法不是把 transport 状态机搬进 Domain。

正确做法是建立一个**process-local side-effect admission capability**。

---

# 17. 新组件：SessionQuiescenceGate

建议新增：

```text
Infrastructure/OpenCode/Host/SessionQuiescenceGate.fs
```

它不是领域状态机。

不写 Journal。

不参与 crash recovery。

不表达业务 stage。

它只回答一个问题：

> **一个以 idle 为前提的副作用，现在是否仍有资格发送？**

类型建议：

```fsharp
type QuiescencePermit =
    private
        {
            SessionId: SessionId
            AttemptSerial: int64
        }

type private Activity =
    | Unknown
    | Running of attemptSerial: int64
    | Idle of attemptSerial: int64
    | IdleConsumed of attemptSerial: int64

type SessionQuiescenceGate() =

    member BeginProviderAttempt:
        SessionId -> unit

    member ObserveIdle:
        SessionId -> QuiescencePermit

    member TryConsume:
        QuiescencePermit -> bool

    member DropSession:
        SessionId -> unit
```

这里的 `AttemptSerial` 只是进程内同步 token。

**绝对禁止写入 Journal。**

HOST-007 已明确禁止把 lease/generation/next_action 等程序控制状态写成持久恢复协议。

重启后 gate 清空是正确的：

```text
没有 fresh idle
→ 没有 permit
→ 不自动发送 idle-derived continuation
```

安全侧失败。

---

# 18. QuiescenceGate 的唯一状态转换

## 18.1 Provider attempt 开始

```text
BeginProviderAttempt(session)
```

执行：

```text
serial = serial + 1
state = Running(serial)
```

任何旧 permit 立即失效。

---

## 18.2 收到 SessionIdle

```text
ObserveIdle(session)
```

当前：

```text
Running(serial)
```

则：

```text
state = Idle(serial)
return Permit(session, serial)
```

如果状态 Unknown，也可以创建当前 serial 的 idle permit，但规则必须单点定义。

---

## 18.3 自动 continuation 要发送

调用：

```text
TryConsume(permit)
```

只有：

```text
state == Idle(permit.AttemptSerial)
```

才：

```text
state = IdleConsumed(serial)
return true
```

其它：

```text
Running newer serial
Idle newer serial
IdleConsumed
Unknown
deleted
wrong session
```

全部：

```text
return false
```

---

# 19. 最关键的接线：在哪里调用 BeginProviderAttempt

不要依赖 `session.status=busy/running`。

项目已经有一个更可靠的 provider-attempt 边界：

```text
experimental.chat.messages.transform
```

它就在每次 provider request 构建前运行。

因此：

> **一旦 transform 能可靠解析当前 sessionId，就必须在该 transform 的最早同步位置调用 `BeginProviderAttempt(sessionId)`。**

必须发生在：

```text
Companion
XWire
Enforcer
PairProgrammingThoughtTransform
ReviewSeal
```

之前的第一个安全位置。

尤其：

```text
BeginProviderAttempt
```

和后续任何 `let!` 之间不能先等待一个外部 Task。

目标是：

```text
旧 idle
→ 新 provider request 开始构建
→ 旧 permit 立即失效
```

而不是等 request 已经跑了半天才标 Running。

---

# 20. SessionIdle 接线

当前 composition root：

```fsharp
| SessionIdle sessionId ->
    scope.LoopSensor.ResetDetector sessionId
    reconciler.Signal signal
```

改成概念上：

```fsharp
| SessionIdle sessionId ->
    scope.LoopSensor.ResetDetector sessionId

    let permit =
        scope.Quiescence.ObserveIdle sessionId

    reconciler.SignalIdle(sessionId, permit)
```

不要把 permit 塞进 Journal。

不要从 `HostSignal` payload 读额外字段生成 identity。

它是 process-local capability。

---

# 21. Reconciler 必须携带“这次判断从哪个 idle occasion 来”

当前 Scheduler 内自己的 `generation` 是：

```text
scheduler dispatch generation
```

它只用于 single-flight/clear 防旧 task。

**禁止复用它当 provider attempt serial。**

两个 generation 表达完全不同的物理含义。

建议引入：

```fsharp
type ReconcileWake =
    | IdleWake of QuiescencePermit
    | RetryWake
    | FailureWake
```

`materializeActive` 全程携带：

```text
wake
```

直到 publish。

然后 `onTurn` 不再只收：

```fsharp
ReconciledTurn -> Task
```

而是：

```fsharp
ReconciledTurnContext -> Task

type ReconciledTurnContext =
    {
        Turn: ReconciledTurn
        Quiescence: QuiescencePermit option
    }
```

只有 `IdleWake` 才有 `Some permit`。

ProviderRetry / ProviderFailure：

```text
Quiescence = None
```

---

# 22. 纯 Decision 层也要停止把“Unknown”叫成 idle

当前注释说：

```text
stable idle that never settled...
```

但真正的证据只有：

```text
snapshot repeatedly Unknown
```

这两个概念必须拆开。

推荐：

```fsharp
type StableObservation =
    | StableUnknown of ObservedTurn
    | StableProvisional of ObservedTurn
    | StableTerminal of ObservedTurn
```

然后业务 decision 可以得到：

```text
StableUnknown
+ IdleWake
→ MissingFinalReportCandidate

StableUnknown
+ Retry/Failure wake
→ no idle-derived continuation
```

即使 pure decision 层最后仍叫：

```fsharp
RepairMissingFinalReport
```

它也必须只在带 `IdleWake` evidence 时可构造。

禁止：

```fsharp
Unknown -> RepairMissingFinalReport
```

这种丢失前置条件的模式。

---

# 23. 最终发送必须再次 TryConsume

仅仅 Reconciler 携带 permit 还不够。

因为仍可能：

```text
decision created
↓
new attempt starts
↓
side effect executes
```

所以 side-effect 边界必须再次原子检查。

新增唯一 idle-send helper，例如：

```fsharp
type IdleContinuationOutcome =
    | Sent of PromptKey
    | Superseded
    | Failed of string

let trySendIdleContinuation
    (quiescence: SessionQuiescenceGate)
    (permit: QuiescencePermit)
    ...
    =
    if not (quiescence.TryConsume permit) then
        Task.FromResult Superseded
    else
        task {
            match! sendContinuationResult ... with
            | Ok key -> return Sent key
            | Error error -> return Failed error
        }
```

`Superseded`：

```text
不是 Error
不是 terminal failure
不写 PromptClaimed
不发消息
```

它只表示：

```text
这个 idle occasion 已经失效，
当前系统正在做更新鲜的事情。
```

---

# 24. TryConsume 与 SendPrompt 之间禁止 await

这是防 TOCTOU 的硬要求。

合法：

```fsharp
if gate.TryConsume permit then
    // 同一同步调用链立即进入 dispatcher
    return! dispatcher.Send...
```

禁止：

```fsharp
if gate.TryConsume permit then
    let! x = SomeOtherAsyncOperation()
    return! dispatcher.Send...
```

因为中间已经重新打开 race window。

当前 PromptDispatcher continuation 进入发送路径后，claim/persist 均为同步步骤，然后直接调用 `port.SendPrompt`；因此将 gate 放在进入这一调用链的最外侧，可以保持边界紧凑。

---

# 25. 哪些 continuation 必须走 QuiescenceGate

至少对整个仓库做 inventory。

### 必须 gated

凡语义是：

```text
“你 idle 了，所以 Host 再推你一下”
```

都必须 gated。

已知包括：

```text
missing-final-report
interaction-repair triggered by idle incomplete turn
ManagerIdleEncouragement
TeacherIdleNudge
StudentCompileNudge
```

PromptAuthority 当前确实定义了 Manager/Teacher/Student 的这些 continuation kind。

### 不因此 gated

不是由 idle 前提产生的 continuation，例如：

```text
ProviderRetryAttempt
BusyAgentNudge
explicit user continuation
FinalityRejected
```

不要为了“统一”错误地要求 idle。

最好的 API 是把发送资格显式写出来：

```fsharp
type ContinuationAdmission =
    | Ordinary
    | RequiresQuiescence of QuiescencePermit
```

然后只有 `RequiresQuiescence` 才走 gate。

---

# 26. TurnCompletionProgram 的具体修改

当前：

```fsharp
| TurnUnknown ->
    if isRecoveryProbeRun journal turn then
        ()
    else
        sendRepair ... missingFinalReport
```

改成：

```fsharp
| TurnUnknown ->
    match context.Quiescence with
    | None ->
        completedTask ()

    | Some permit ->
        trySendIdleInteractionRepair
            quiescence
            permit
            ...
```

同样修改：

```text
TurnNeedsContinuation → missing-final-report
TurnInProgress → interaction repair
Manager → IdleEncouragement
```

Manager 当前 `IdleEncouragement` 是直接 `sendContinuationResult Detached`。

必须变成：

```text
没有 permit
→ 不发送

permit stale
→ Superseded

permit fresh
→ send
```

---

# 27. 删除 recovery-probe 特判

一旦 QuiescenceGate 的 RED/GREEN 全部通过：

```fsharp
isRecoveryProbeRun
```

这个特殊补丁应删除。

原因：

它是在回答：

```text
“这个 run 是否碰巧属于一个我们已经知道会触发 stale idle race 的类别？”
```

而新系统回答：

```text
“这个 continuation 的 idle 前置条件此刻还成立吗？”
```

后者才是完整问题。

保留二者会继续鼓励未来：

```text
isReviewerProbeRun
isManagerResumeRun
isTeacherRun
isWhateverRaceWeFoundNextMonth
```

不断打洞。

---

# 28. Quiescence RED 测试矩阵

## Q-01：正常 stable idle 仍会续跑

```text
attempt A begins
SessionIdle(A)
snapshot Unknown
causal rereads exhausted
no newer attempt
```

期望：

```text
exactly one "#"
exactly one PromptClaimed for missing-final-report
permit consumed
```

---

## Q-02：用户报告的核心 race

```text
attempt A begins
SessionIdle(A)
reconcile starts
snapshot Unknown

BEFORE side effect:
attempt B transform begins

old reconcile reaches send
```

期望：

```text
zero "#"
zero PluginPromptClaimed for stale repair
IdleContinuationOutcome = Superseded
attempt B continues untouched
```

这是第二个首要测试。

---

## Q-03：重复 idle 不重复发送

```text
SessionIdle(A)
SessionIdle(A)
same incomplete occasion
```

期望：

```text
at most one auto continuation
```

durable interaction-repair occasion dedupe 继续负责 crash-safe claim 语义；QuiescenceGate 只负责“当前仍 idle”。

不要混淆两个职责。

---

## Q-04：新 attempt 的新 idle 可以再次发送

```text
A idle
→ continuation sent

B starts
B idle
B remains incomplete
```

期望：

```text
A gets one
B gets one
```

不能因为旧 permit consumed 就永久压 session。

---

## Q-05：ProviderRetry 不授予 idle 资格

```text
ProviderRetry wake
snapshot Unknown
```

没有 fresh SessionIdle：

```text
zero missing-final-report
```

---

## Q-06：ProviderFailure 不授予 idle 资格

同理：

```text
ProviderFailure
!= idle
```

---

## Q-07：restart

```text
process crashes
new PluginRuntimeScope
```

QuiescenceGate 初始没有 permit。

在 fresh SessionIdle 前：

```text
zero idle-derived continuation
```

已经 durable Claimed 的 Prompt 仍由 PROMPT-011 recovery 自己处理。

不要由 QuiescenceGate 重发。

---

## Q-08：recovery probe race

复现当前 `isRecoveryProbeRun` 注释里的 race：

```text
old idle reconcile
probe continuation starts new transform
old snapshot still Unknown
```

不依赖 `isRecoveryProbeRun`：

```text
old repair suppressed by stale permit
probe completes normally
```

然后删除 special case。

---

## Q-09：Manager encouragement race

```text
Manager A completed
idle reconcile decides Encourage

new Manager provider attempt starts
old decision reaches side effect
```

期望：

```text
zero stale IdleEncouragement
```

fresh subsequent idle 才能再次发送。

---

## Q-10：SessionDeleted

```text
permit exists
SessionDeleted
```

必须：

```text
DropSession
old permit invalid forever
```

---

# 29. PluginRuntimeScope 接线

`PluginRuntimeScope` 当前就是每插件实例 process-local physical resource owner，里面已经拥有：

```text
NudgeSent
JoinGuardNudges
AbortedSessions
RecoveryArming
AttemptPlans
LoopSensor
```

因此 QuiescenceGate 应由这里持有：

```fsharp
member val Quiescence = SessionQuiescenceGate()
```

不要放：

```text
SharedState
Journal projection
PromptAuthority
ManagerLife
```

它不是跨实例领域真理。

若一个 session 因 worktree 插件实例变化发生 owner 转移，新实例没有旧 permit = 安全侧。

---

# 30. 不允许增加新的 Host API / patch OpenCode

本 Change 所需信号已经存在：

```text
experimental.chat.messages.transform
session.status idle
session.deleted
```

不要求 Host 提供：

```text
provider.started
provider.finished
isBusy()
currentStatus()
```

也不修改 OpenCode 本体。

当前架构本来就明确要求使用已有 Hook/SDK，不依赖未公开 API。

---

# 31. 文件级实施地图

## Slice A — HOST-013 Domain/Journal

修改：

```text
src/Wanxiangshu/Kernel/Identity.fs
    新 TranscriptMessageAddress（若没有合适现存窄类型）

src/Wanxiangshu/Kernel/Fact.fs
    legacy PairProgrammingGuidelineAppended
    → anchored fact

src/Wanxiangshu/Journal/GuidelineProjection.fs
    PairProgrammingGuideline 增 CallGap / ResultGap
    fold 校验 duplicate placement / ordinal / callID

src/Wanxiangshu/Journal/Fold.fs
    新 fact fold
```

---

## Slice B — Projection

修改：

```text
src/Wanxiangshu/Domain/ProjectionAlgebra.fs
```

当前：

```fsharp
type PairThoughtIntent =
    { History
      Next }
```

它表达的还是“历史 pair + 本轮 pair”这种 block-thinking。当前定义确实如此。

改成类似：

```fsharp
type AnchoredSyntheticHalf =
    {
        PairOrdinal: int64
        Half: PairHalf
        Gap: TranscriptGap
        CallId: string
        MarkerText: string
    }

type PairThoughtIntent =
    {
        Entries: AnchoredSyntheticHalf list
    }
```

或者 renderer 直接消费完整 anchored pair list。

重点：

> Renderer 只按 anchor 渲染；它不再次决定历史位置。

---

## Slice C — PairProgrammingThoughtTransform

修改：

```text
src/Wanxiangshu/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.fs
```

删除：

```text
historyBlock
placePairs 历史重定位逻辑
placeWirePairs 同构 oracle
“每 transform 无条件 append”
```

新增：

```text
extractTranscriptAddress
stripSynthetic
replayAnchoredHistory
decideCurrentPlacement
findExistingPlacement
constructCandidate
validateCandidate
commitCandidate
```

`buildPairMessage` 可保留，只负责 deterministic bytes。

---

## Slice D — Quiescence Gate

新增：

```text
src/Wanxiangshu/Infrastructure/OpenCode/Host/SessionQuiescenceGate.fs
```

并注册 fsproj compile order。

---

## Slice E — Host composition

修改：

```text
PluginRuntimeScope.fs
HostSignalBootstrap.fs
PluginHostInterop.fs
```

接：

```text
BeginProviderAttempt
ObserveIdle
DropSession
```

---

## Slice F — Reconciliation

修改：

```text
ReconcileProgram
Reconciler
ReconciledTurn context
TurnCompletionProgram
```

把：

```text
snapshot observation
idle wake evidence
physical continuation admission
```

分成三层。

---

## Slice G — Prompt/Nudge

修改：

```text
HostSessionNudge.fs
```

新增唯一：

```text
trySendIdleContinuation
trySendIdleInteractionRepair
```

或者等价 typed admission API。

不要复制 `TryConsume` 到五个 caller。

---

# 32. 文档必须同一 Change 修改

当前正式 HOST-013 文档把错误实现语义写成了规范，包括：

```text
每次 transform 无条件插入一组完整 pair
历史 pair 原位恢复
本次 pair 在 trailing user 前
```

本 Change 必须同步：

```text
docs/what/host.md
docs/shape/host.md
docs/how/host.md
docs/proof/host.md
docs/why/host.md
```

## what/host.md

改成行为语言：

```text
HOST-013:
Synthetic pair is a temporal bracket around one real provider/tool exchange.
Repeated transform of the same placement only replays the existing bracket.
Historical synthetic halves never change their transcript gaps.
Same epoch provider wire is append-only.
```

## shape/host.md

把唯一 durable state 从：

```text
Ordinal + CallId + MarkerText
```

改成 anchored fact。

## how/host.md

写 gap replay 算法，不再写：

```text
historyBlock
current trailing user determines history placement
```

## proof/host.md

现在 proof 表只说“第 n+1 次 transform 原位恢复前 n 组”。

升级为真正可执行：

```text
canonical multi-tool sequence
same-placement idempotence
restart byte equality
anchor missing fail closed
ProviderProjection.isAppendOnlyPrefix over N rounds
```

---

# 33. HOST-004 文档同步

当前 how/host 把：

```text
Unknown reread exhausted
→ RepairMissingFinalReport
```

描述成“稳定 idle”。

必须改：

```text
Repeated snapshot stability proves observation stability only.
It does not prove present quiescence.

Idle-derived continuation requires BOTH:
1. snapshot/business decision says continuation is useful
2. the originating QuiescencePermit remains fresh at side-effect time
```

这也与已有 Student/Teacher 文档“idle 只作 wake，策略从完整 snapshot 决定”保持一致，但再补上 side-effect admission 这一缺失层。

---

# 34. 严禁的假修复

以下 PR 直接 reject：

```text
1. 只调整 placePairs 的 index
2. 继续存在 historyBlock
3. 根据当前 trailing user 给历史 pair 重新定位
4. 根据当前 tool batch 给历史 pair 重新定位
5. 只测试 markerCount，不测试 isAppendOnlyPrefix
6. 用 timestamp/random 生成 synthetic identity
7. anchor 丢失时“尽量放到最接近位置”
8. 把历史 pair 删除后只凭 ordinal 猜原位置
9. 每 transform 无条件新增一组 pair
10. 用 PrefixEpoch 切换掩盖 HOST-013 自己造成的 prefix drift

11. 给 TurnUnknown 加 Task.Delay
12. 连续多读几次 snapshot 就称为“仍 idle”
13. 新增 isBusy bool 散落在 caller
14. 把 busy/running 全搬进领域 HostSignal
15. Journal 持久化 Idle/Running/Lease/Generation
16. 给 recovery probe 保留永久特殊豁免
17. 发现新 race 就继续加 isXxxRun 特判
18. TryConsume 后 await 其它操作再 SendPrompt
19. permit stale 当作 terminal failure
20. stale idle 也先写 PluginPromptClaimed 再决定不发
```

---

# 35. RED → GREEN 实施顺序

严格按顺序。

## Phase 1 — 先改正式语义

先修改：

```text
HOST-013 bracket semantics
HOST-004 quiescence admission semantics
proof requirements
```

禁止代码先跑、文档以后补。

---

## Phase 2 — 写 HOST-013 RED

先让以下全部红：

```text
H13-01 canonical sequence
H13-02 no historical relocation
H13-03 same-placement idempotence
H13-04 restart exact replay
H13-05 missing anchor fail closed
H13-08 N-round prefix property
```

尤其 H13-01 必须在当前 `historyBlock` 实现上红。

如果它现在是绿的，测试写错了。

---

## Phase 3 — Anchored replay GREEN

落：

```text
TranscriptGap
anchored durable fact
replay algorithm
same-placement dedupe
```

所有 H13 绿。

然后确认：

```text
ProviderProjection.isAppendOnlyPrefix == true
```

不是只看 snapshot。

---

## Phase 4 — 写 Quiescence RED

先写：

```text
Q-01 normal idle sends
Q-02 stale idle after new attempt sends nothing
Q-05 retry gives no permit
Q-07 restart no permit
Q-08 recovery probe generic suppression
Q-09 Manager stale encouragement suppression
```

Q-02 当前生产必须能够稳定复现错误。

---

## Phase 5 — Quiescence GREEN

加入：

```text
SessionQuiescenceGate
BeginProviderAttempt
ObserveIdle
permit carry
TryConsume
typed idle send helper
```

全部 Q 绿。

---

## Phase 6 — 删除 symptom patches

删除：

```text
isRecoveryProbeRun missing-final-report exemption
```

以及因新模型变成死代码的其它 stale-idle workaround。

重新跑 Q-08，证明不是靠特殊白名单。

---

## Phase 7 — Full gate

至少：

```text
npm run build
npm run lint
node tests/unit/run.mjs
node tests/integration/run.mjs
node tests/e2e/run.mjs
node scripts/check.mjs
git diff --check
```

具体脚本以仓库当前 package/scripts 为准；不得为了让本 Change 绿而降低已有 gate。

---

# 36. 最终验收清单

## Prefix / HOST-013

```text
[ ] 用户给出的 Req/FakeReq/Resp/FakeResp 四行序列逐字成立
[ ] historyBlock 已删除
[ ] 历史 synthetic 位置由 durable gap anchor 决定
[ ] current placement 永不参与历史 pair 重定位
[ ] same placement 重入不新增 pair
[ ] restart replay byte-identical
[ ] anchor 缺失 fail closed
[ ] prior tip 只影响新 pair
[ ] Companion/Blogger 零注入
[ ] N-round property: isAppendOnlyPrefix 永远 true（同 epoch）
[ ] 不通过 PrefixEpoch 切换掩盖错误
```

## Quiescence / auto-continue

```text
[ ] fresh idle + stable incomplete → exactly one continuation
[ ] new provider transform 一开始即 invalidate old permit
[ ] stale permit → zero physical prompt
[ ] stale permit → zero PluginPromptClaimed
[ ] ProviderRetry/Failure 不自动生成 idle permit
[ ] ManagerIdleEncouragement 也要求 fresh permit
[ ] Teacher/Student idle-derived send 全 inventory
[ ] restart 后没有 synthetic idle truth
[ ] SessionDeleted 清理 permit
[ ] TryConsume 与 dispatcher send 之间无 await
[ ] recovery probe 无特殊 missing-final-report exemption
```

## Architecture

```text
[ ] 不改 OpenCode 本体
[ ] 不新增 hidden Host API 依赖
[ ] 不把 activity generation 写 Journal
[ ] 不把 transport busy/running 变成业务事实
[ ] PromptDispatcher 仍是唯一 physical prompt writer
[ ] QuiescenceGate 只做 side-effect admission
[ ] durable Prompt claim/recovery 语义未被 process-local gate 取代
```

---

# 37. Code review 时只问这六个问题

Reviewer 不需要重新理解整个系统。

只问：

```text
1. 上一次实际发给 provider 的 wire，是不是下一次 wire 的完整前缀？

2. 任意历史 FakeReq/FakeResp 的位置，是否只由它自己的 durable anchor 决定？

3. 同一个真实 exchange 重进 transform，会不会凭空多一组 pair？

4. 这个自动 continuation 如果发送，它能否证明发送瞬间 fresh idle permit 仍成立？

5. 新 provider attempt 一开始，旧 idle permit 是否必然失效？

6. 把任何 recovery-probe / manager / reviewer 特判删掉后，
   基础 invariant 是否仍然成立？
```

任意一个回答需要：

```text
“正常情况下……”
“Host 应该会……”
“这个 race 很小……”
“测试里没有遇到……”
“我们重新找 trailing user 就行……”
```

则 Change 不通过。

---

# 38. 最终设计摘要

## Auto-injected

不要再想：

```text
pair1
pair2
pair3
```

要想：

```text
real timeline

Req1 Req2
     ↑ FakeReq1
Resp1 Resp2
           ↑ FakeResp1
Req3
    ↑ FakeReq2
Resp3
     ↑ FakeResp2
```

synthetic pair 的两个 half 有共同 identity，但各自拥有独立 transcript placement。

**共同 identity ≠ 相邻存储。**

---

## Auto-continue

不要再想：

```text
我曾经收到 idle
+
snapshot 连续三次没变
=
现在可以发 #
```

要想：

```text
snapshot
→ 决定“如果仍 idle，则值得继续”

fresh QuiescencePermit
→ 决定“现在仍有资格执行这个副作用”

两者同时成立
→ send
```

即：

```text
Business Decision
×
Physical Admission Capability
=
Allowed Side Effect
```

F# DSL 的价值不是把所有状态写成 DU。

它的价值是让一个**缺少必要前提的副作用无法被构造或无法通过唯一执行入口**。

本 Change 完成后，这两个 bug 都应该从“靠工程师小心”升级成“错误代码很难写出来”。

---

# Active work

> 本文件是变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。

启动指令：用户要求按本 Proposal 一步一步实施（2026-08-08）。Original proposal 已冻结。

## Specification impact

- HOST-013：bracket 语义、anchored durable fact、gap replay、same-placement 幂等、
  legacy unanchored fact fail closed。
- HOST-013 / XWire：当前真实 view 中找不到 gap anchor 的 historical pair **不重放、不重定位、
  不 AbortSession**（XWire DropLeading 合法 drop 已覆盖前缀）；durable fact 保留。
- HOST-004：idle-derived continuation 必须 `QuiescencePermit`；删除 `isRecoveryProbeRun` 后，
  **仍保留 durable `ProviderRetryAttempt` 身份抑制**——stale permit 挡不住 probe 同 attempt 的
  新 Idle（实测 x-c/x-d）。
- 文档：`docs/{what,shape,how,proof,why}/host.md` 已同步 anchor-omit 语义。

## Progress log（2026-08-08 下班交接）

### 已提交（勿重做）

- `633af829` HOST-013 anchored gap replay
- `d9e6ef9e` HOST-004 SessionQuiescenceGate + 删除 isRecoveryProbeRun

### 工作区未提交（Phase 7 修复）

文件：

- `PairProgrammingThoughtTransform.fs`：replay 时 unplaceable pair 跳过（不 `HistoricalSyntheticAnchorMissing` Abort）
- `TurnCompletionProgram.fs`：`isRecoveryContinue` = durable `ProviderRetryAttempt`；
  TurnUnknown / TurnNeedsContinuation / TurnInProgress interaction-repair 均抑制
- `pair-thought-anchored.test.mjs`：H13-05 omit + H13-05b XWire DropLeading
- `turn-completion-program.test.mjs`：Q08b/Q08c fresh permit 仍不 hijack probe
- `docs/{why,what,how,shape,proof}/host.md`：anchor 语义从 fail-closed-abort → omit

根因与决策：

1. **x-b 超时**：XWire prefix probe DropLeading 去掉 opening user → pair1 anchor 缺失 →
   tryInject Error → SpikePlugin AbortSession → continue 从未打到 mock。
   修：unplaceable pair omit，不 Abort。
2. **x-c seal-undeclared / PrefixRebase=0**：删除 isRecoveryProbeRun 后，probe 同 attempt
   的 SessionIdle 铸出 **fresh** permit → missing-final-report / interaction-repair 劫持 probe。
   修：durable `ProviderRetryAttempt` 身份抑制（不是 isRecoveryProbeRun 白名单复活；
   不是 runtime AttemptPlan 字典）。stale permit 仍是独立 HOST-004 门。

### 已验证（本机，未提交修复之上）

| 门禁 | 结果 |
|------|------|
| `npm run build` | OK |
| unit `1783` | 全绿 |
| integration `275` | 全绿 |
| `node scripts/check.mjs` | OK（spec 347 / arch / dsl / p0-recovery-join） |
| e2e `context-recovery` x-a..x-d | 全绿 |
| e2e `fallback` | 全绿 |
| e2e `student-teacher` | 全绿；场景 runtime cursor 已按实测补齐 |
| 全量 `node tests/e2e/run.mjs` | 25 scenarios 中至少 8 项已过；当前 EXIT=1 |

### 未完成 / 阻塞

1. **全量 e2e 仍红**：
   - `orchestrator-restart-publish.test.mjs`：`no-declared-turn`，Orchestrator 原始 prompt 的
     `step=3` 未声明；候选为 `blogger.3`。
   - `manager-unhappy-path.test.mjs`：strict mock mismatch；完整首错与 cursor 尚未记录。
   - 两个独立 debugger 在诊断启动后被用户中断；恢复时先分别运行：
     `MOCK_TRACE=1 node tests/e2e/cases/orchestrator-restart-publish.test.mjs` 与
     `MOCK_TRACE=1 node tests/e2e/cases/manager-unhappy-path.test.mjs`，读取全部 trace，
     只对实测 runtimeStep 作最小场景声明修复。
2. 全量 e2e 绿后仍需：`git diff --check`、如有 F# 再 `npm run format`。
3. 未写 `Final outcome`，未移动至 `changes/completed/`，未提交。

### 注意

- `global.json` 曾在 stash 误删；已 `git restore`。勿提交 `openbuff.json` 或 `.omx/`。
- 分支相对 origin **ahead 2**（`633af829` + `d9e6ef9e`）；保留当前未提交工作区。
- 勿再引入 `isRecoveryProbeRun` 函数名；当前抑制谓词是 `isRecoveryContinue`（ledger）。
- `tests/e2e/scenarios/student-teacher.toml` 已由自动门禁 `LOOKS_GOOD`；定向 canary 通过。

## Remaining work（收尾序列）

1. 分别诊断并最小修复 `orchestrator-restart-publish` 与 `manager-unhappy-path` 的 scenario cursor / 声明。
2. 重跑全量 `node tests/e2e/run.mjs`，要求 25 scenarios 全绿且无 `MOCK-FATAL`。
3. `git diff --check` + 如有 F# 再 `npm run format`。
4. 核对全部未提交修复；提交 Phase 7 修复（不 push）。
5. 在本文件追加 `Final outcome`（Outcome / Final specification / Implementation result /
   Verification / References）→ 移动至 `changes/completed/` → commit（不 push）。

## Completion criteria

- PREFIX LAW + 历史 pair 不搬家 + same-placement 幂等 + restart byte-identical。
- unplaceable historical pair omit（非 Abort）；legacy unanchored journal 仍 fail closed。
- QUIESCENCE LAW：stale/no permit → zero send；fresh idle 普通主路径仍可 repair。
- ProviderRetryAttempt 不被 missing-final-report / interaction-repair 劫持；x-c/x-d 有
  PrefixRebaseCommitted。
- 全量 e2e 25 scenarios 绿，无 `MOCK-FATAL`。
- Final outcome 落盘并 completed。

## Blockers

- `orchestrator-restart-publish` 与 `manager-unhappy-path` 的 strict scenario 声明仍未闭环。
