# Message Transform 钩子重构：TransformState 与引用稳定

## 一、Flow 管线位置

本文件描述 transform 钩子的内部状态管理重构。在 Flow-first 架构中，transform 钩子位于 `scan` 输出（Step.State）与实际 provider 调用之间——它是 message pipeline 的**同步变换层**，不属于 `flatMapMerge` effect 区。

```text
committed kernel.step result
  → Step.State (含 projection 快照；这里只展示已提交 step 到 transform 的输入，不是提交算法)
  → transform hook (本文件)
      ├── CAPS 段（TransformState.Caps）
      ├── Backlog 段（TransformState.Backlog）
      └── Top slot 段（TransformState.Top）
  → replaceArrayInPlace → provider 调用
```

TransformState 不是 correctness cache——它就是 synthetic 注入段的进程状态；其中按 revision key 复用 CAPS/segment 只是性能优化。其内容 SSOT 关系遵循 PRD-09 投影纪律：NDJSON 是 durable SSOT，TransformState 从中派生。

## 二、当前 bug 面：整体重算而非增量维护

当前三类问题共享同一根因——transform 钩子每轮做"整体重算"而非"增量维护"：

### 问题 A：错误的身份/指纹式缓存判定

`MessageTransformHostEntry.fs` 里的 `SessionTransformCache` 不能以对象身份或一组不相干的元数据作为正确性判定。正确性必须比较最终 outbound message 的规范化内容及其稳定 prefix 的字节序列；`ReferenceEquals` 只能作为性能优化（命中时可复用对象），绝不能证明内容相同。缓存条目由 `scopeId × capsRevision × policyVersion` 等明确 revision 组成，不使用 hash。

### 问题 B：stripSyntheticBySource 无条件调用

`Messaging.stripSyntheticBySource` 在每次 transform 开头无条件扫描并剔除所有 `Synthetic` 消息（caps、backlog projection、context-budget-nudge、parallel-tool-synth），然后管线重新计算、重新插入。哪怕只有栈顶的 nudge 需要换一条，也要把栈底稳定了几十轮的 caps 前缀一起摘掉再贴回去。

### 问题 C：transform 钩子里正则推断 review 状态

`messagesTransform` 钩子里通过 `extractTextsFromEncodedMessages` + `inferReviewTaskFromTexts` 从消息文本里正则解析 `task:` frontmatter 来推断 review 是否激活。这不是 transform 钩子该做的事：消息切片在 compaction 后可能看不到激活锚点，真相源应只有一个 `.wanxiangshu.ndjson`（PRD-08 ReviewProjection）。

## 三、关键事实修正：transform 钩子修改不持久

### 原假设（已推翻）

"opencode 主机传给 transform 钩子的 `messages` 数组，其前缀本身就是 host 侧维护好的、跨轮次稳定的引用"，据此设计了"真栈"方案——记住上一轮推了几个 synthetic 元素，本轮弹出那么多个，再 push 新的。

### 实际行为（源码验证）

OpenCode `prompt.ts` 的 `runLoop` 每次循环迭代顶部：

```typescript
// prompt.ts:1092
let msgs = yield* MessageV2.filterCompactedEffect(sessionID)
```

`filterCompactedEffect` → `stream(sessionID)` → `page()` 从**数据库**分页读取消息，每次产生全新 JS 对象。然后才调用 transform 钩子：

```typescript
// prompt.ts:1254
yield* plugin.trigger("experimental.chat.messages.transform", {}, { messages: msgs })
```

插件的 `messagesTransform` 用 `replaceArrayInPlace` 原地修改 `msgs`。修改后的 `msgs` 直接传给 provider。**但修改从未写回数据库。** 下一轮迭代 `msgs` 重新从 DB 读取，上一轮注入的 synthetic 消息全部消失。

`ContextBudget.fs:91` 注释已承认此事实：`"Host strips synthetic nudge each transform round; reinject whenever pressure still holds."`

**结论**：transform 钩子的修改不持久。"真栈"的 pop 操作是空操作——上一轮推入的 synthetic 元素在本轮数组里根本不存在，无从弹出。

### 正确认识

* **无钩子时**，OpenCode 前缀天然稳定：同一 human turn 内 auto-continue 步骤间 DB 行不变（只有新 tool result 追加到末尾），前缀内容字节一致 → provider prompt-cache 命中。
* **有钩子时**，钩子每轮收到 fresh host 数组（DB 直读，无上一轮 synthetic），必须重新注入全部 synthetic 消息。
* **问题**：如果每轮重新创建 synthetic JS 对象（即使内容相同），对象引用不同 → 下游 `replaceArrayInPlace` 产出全新数组 → 无法保证 JSON 序列化字节一致。
* **解法**：在 RuntimeScope 中维护 synthetic 注入段的状态。状态不变时可复用同一份 JS 对象引用以减少分配；状态变化时重建。host 前缀天然稳定，发送前再以 canonical outbound bytes/prefix equality 判断是否可以命中 prompt-cache。

## 四、状态层次与 SSOT

| 层级 | 持久性 | 角色 |
| :--- | :--- | :--- |
| NDJSON 事件日志 | 跨重启 | durable SSOT：backlog 内容、review 状态、续命租约、nudge 去重（PRD-09） |
| `FallbackRuntimeState` / `ContextBudgetStore` / `TransformState` | 进程生命周期 | 进程状态：从 NDJSON fold 重建（重启时），运行时更新 |
| 每轮 host 消息数组 | 瞬时（不持久） | 非状态：每轮从 DB 重读，上一轮修改消失 |

| 段 | 内容 SSOT | TransformState 字段 | 何时更新 |
| :--- | :--- | :--- | :--- |
| CAPS | `CapsFormat` / `CapsPrelude` | `Caps: { ScopeId; CapsRevision; PolicyVersion; Segment } option` | scope、caps 内容变更计数或策略版本变化 → 重建对应段 |
| Backlog | NDJSON `work_backlog_committed`（PRD-07） | `Backlog: { BacklogRevision: int; Segment: obj array }` | `BacklogRevision` 变化 → 重建投影段 |
| Top slot | `PromptFragments`（docs/10） | `Top: { BudgetRevision; Key: TopSlotKey; Item: obj option }` | revision 或 identity-bearing key 变化 → 重建或清除 |

## 五、设计原则

1. **不弹不改 host 前缀**：每轮收到 fresh host 数组，原样保留。
2. **revision 先决定候选复用**：用 scope/revision/policy 计数和 identity-bearing ADT 判断哪些段可能复用；不使用内容 hash。
3. **规范化内容是正确性证明**：发送前比较 outbound message 的 canonical bytes（含稳定 prefix）；只有 canonical bytes 相等才可声称 prompt-cache 可复用。`ReferenceEquals` 仅用于减少分配，不能推出字节相等。
4. **字符串状态全部升级为 ADT**：F# union case 的相等比较编译为 tag 整数比较，零字符串比较。
5. **保留文档记载的行为**：管线行为（7 阶段、同 source 替换不重复追加等）正确；本计划只改实现方法，不改行为契约。

## 六、与文档管线（docs/10）的对应

| 阶段 | 文档描述 | 本计划调整 |
| :--- | :--- | :--- |
| 1. 剥离 synthetic | `stripSyntheticBySource` 无条件调用 | **删除**：修改不持久，host 数组中不存在上一轮 synthetic，strip 是空操作 |
| 2. Caps / 清理 | caps 前缀注入 + 消息净化 | `TransformState.Caps` 按 `scopeId × capsRevision × policyVersion` 复用 |
| 3. Backlog 投影 | 从事件 fold 投影，非历史 tool SSOT | `TransformState.Backlog`（`BacklogRevision` 驱动） |
| 4. Review replay | 从消息文本推断 review 状态 | **删除死代码**：`_replayTexts` 参数从未被使用，review 状态走 NDJSON 事件 fold（PRD-08） |
| 5. applyContextBudget | `classifyPressure` → 紧急 nudge 注入 | `TransformState.Top`，`BudgetRevision` + `TopSlotKey` 驱动。扩展现有 `ContextBudgetStore.StableSyntheticNudgeID` + `BudgetNudgeTrack`（ADT）到完整 JS 对象 |
| 6. tryInjectParallelToolPrompt | 单工具伪装并行提示 | `TransformState.Top`，与 budget nudge 互斥 |
| 7. Semble | investigator 断点注入 | investigator 始终收到 CAPS；Semble 操作 last assistant 的 parts，与三段状态的数组级操作正交 |

## 七、TransformState 类型

```fsharp
type TransformState = {
    Caps: { ScopeId: string; CapsRevision: int; PolicyVersion: int; Segment: obj array } option
    Backlog: { BacklogRevision: int; Segment: obj array }
    Top: { BudgetRevision: int; Key: TopSlotKey; Item: obj option }
}

// CapsRevision/BacklogRevision 等均为对应内容的单调 revision/counter，
// 不是全局事件数，也不是内容 hash。
type TopSlotKey =
    | NoTop
    | BudgetNudgeTop of episodeId: string * syntheticId: string * contentVersion: int
    | ParallelHintTop of callId: string * assistantMessageId: string * contentVersion: int
```

存在 `RuntimeScope` 里，key 用 `sessionID`，跟进程生命周期，不落盘。重启后自然为空 → 从 NDJSON 重建。

### 专用 revision 计数器

系统必须维护彼此独立的单调计数器：`BacklogRevision`、`CapsRevision`、`ReviewRevision`、`BudgetRevision`。每个计数器只在自己负责的规范化内容发生变化并完成相应持久化提交后递增：

* `BacklogRevision` 只表示 backlog projection 内容变化；
* `CapsRevision` 只表示 CAPS 内容变化；
* `ReviewRevision` 只表示 review projection 内容变化；
* `BudgetRevision` 只表示 budget pressure/nudge 决策内容变化。

禁止恢复或新增全局事件总数，也禁止用 event-log 总数、对象 identity 或 hash 充当这些 revision。compaction、host 数组重读、token 估算抖动在没有对应内容变化时不得递增任何 revision。`ReviewRevision` 虽不驱动 synthetic review 注入，却必须由 review projection 的消费者使用，以避免 transform 从消息文本推断 review。

### CAPS 状态

* CAPS cache 必须以 `scopeId × capsRevision × policyVersion` 为 key；三者任一变化，都必须按新的规范化 CAPS 内容构建对应段。cache key 不得包含 hash，也不得以对象 identity 代替任一字段。
* `scopeId` 隔离 RuntimeScope；`capsRevision` 仅在 CAPS 内容发生变化时递增，`policyVersion` 仅在 CAPS 策略发生变化时递增。内容未变时可复用已有段引用，但必须能以 canonical outbound bytes/prefix equality 证明正确性。
* 进程重启后 RuntimeScope 可为空，首次命中该 key 时重建；CAPS 段不得被视为永久构造物。
* compaction 不改变 CAPS revision；investigator 会话和 investigator 断点消息仍必须收到 CAPS。

### Backlog 状态

* 从对应 projection 读取 `BacklogRevision`；它只在 backlog 内容发生变化并提交 `work_backlog_committed` 时递增。不得用全局事件总数或任意 event-log 总数代替它。
* `BacklogRevision = state.Backlog.BacklogRevision` 时可以复用 `Segment` 引用；revision 变化时必须以新的 folded backlog 重建投影段。
* compaction 不改变 backlog 内容，也不递增 `BacklogRevision`，因此可复用段；若 compaction 明确改变 backlog 内容，则必须先产生对应 revision，再重建。
* auto-continue 中的 `todowrite` 只有在对应 backlog commit 成功后才递增 `BacklogRevision`，并只重建 backlog 段。

### Top slot 状态

关键区分（PRD-04 + PRD-06）：
* **context-budget-nudge**：transform 管线内的同步注入，由 `classifyPressure` 返回 `RequireTodoWriteEmergency` 驱动。消息内容固定（`PromptFragments` SSOT）。`ContextBudgetStore` 已有 `BudgetNudgeTrack` ADT（`Idle` / `EmergencySignaled`）。
* **async nudge**：`SessionIdle` 后的异步 `session.prompt`，由 `NudgeRuntime` 编排。不经 transform 钩子，不在 Top slot 范围内。
* **parallel-tool-synth**：`tryInjectParallelToolPrompt` 条件注入，消息内容固定。

`TopSlotKey` 必须携带 identity：budget nudge 绑定 episode/synthetic identity 和 content version；parallel hint 绑定 tool call、assistant message identity 和 content version。不得以无 payload 的 ADT tag 复用不同 top item。

`computeTopSlotKey` 从 `ContextBudgetStore` + 当前消息列表读取决策，返回完整 `TopSlotKey`。比较 key 时必须比较其 identity payload；key 相等时可复用 item 引用，key 不等时必须重建或清除。canonical 内容仍是正确性标准。

`BudgetRevision` 只在 budget pressure 或 nudge 内容发生变化后递增。conversation start 不得凭空产生 emergency todo prompt；settled 后不得通过 fallback nudge 补发。parallel hint 仍只在最后一条 assistant 恰有一个 tool call 时注入（single-tool hint），并以 call/message identity 区分实例。

Budget nudge 和 parallel-hint 互斥（pipeline 顺序已决定优先级，阶段 5 在阶段 6 之前）。

与现有代码的关系：`ContextBudgetStore.StableSyntheticNudgeID` 已存 nudge ID 用于 `isSameEpisode` 判断（`MessageTransformCore.fs:198-210`）。本计划将状态从 ID 扩展到完整 JS 对象引用，避免每轮 `buildContextBudgetNudgeMessage` 重构。

## 八、transform 函数

```text
transform(hostArray, state) → (newArray, newState)

1. CAPS（阶段 2）：
   let key = (scopeId, capsRevision, policyVersion)
   match state.Caps with
   | Some cached when cached key = key → reuse cached Segment as an optimization
   | _ → build canonical CAPS Segment for key, state.Caps ← Some {
             ScopeId = scopeId; CapsRevision = capsRevision
             PolicyVersion = policyVersion; Segment = newSegment }
   unshift the segment only after canonical outbound prefix equality is established

2. Backlog（阶段 3）：
   let revision = ProjectionStore.GetBacklogRevision(sid)
   if revision = state.Backlog.BacklogRevision → append state.Backlog.Segment
   else → rebuild, state.Backlog ← { revision, newSegment }, append

3. Top slot（阶段 5+6）：
   let budgetRevision = BudgetStore.GetBudgetRevision(sid)
   let key = computeTopSlotKey(contextBudgetStore, messages)
   if budgetRevision = state.Top.BudgetRevision && key = state.Top.Key →
       match state.Top.Item with
       | Some item → append item          // reference reuse is optimization only
       | None → ()                         // 不追加
   else →
       rebuild or clear, state.Top ← { budgetRevision, key, newItem }, append

4. replaceArrayInPlace(hostArray, finalArray)
5. assert canonical outbound content and stable prefix equality; return (finalArray, state)
```

核心不变式：正确性由每轮 outbound message 的 canonical content 和 prefix bytes equality 决定。revision/key 未变时可以通过 `ReferenceEquals` 复用 synthetic 段以减少分配；该引用相等只是性能优化，不能作为内容相等或 prompt-cache 命中的证明。

行为保持：docs/10 记载的"同 source 已存在则替换而非无限追加"行为不变——由于 host 数组不含上一轮 synthetic，"替换"自然退化为"追加"，效果等价。

## 九、字符串状态升级为 ADT

现有代码中大量使用字符串做同步判断（`sessionOwner = "Fallback"`、`lease.Status = "requested"` 等）。全部替换为 ADT，消除字符串比较。

### 新增 ADT 类型

```fsharp
// 替代 sessionOwner: string（"None"/"Human"/"Fallback"/"Nudge"/"Compaction"）
type SessionOwner =
    | NoOwner
    | Human
    | Fallback
    | Nudge
    | Compaction

// 替代 PendingLease.Status: string 和 NudgeLease.Status: string
type LeaseStatus =
    | Requested
    | DispatchStarted
    | Dispatched
    | Running
    | Cancelled

// 替代 finishContinuation 的 outcome: string（"failed"/"cancelled"/"settled"）
type ContinuationOutcome =
    | Failed
    | Cancelled
    | Settled
```

F# 对 union case 的相等比较必须包含 payload；不得依赖字符串化状态或 hash。内容正确性仍由 canonical outbound bytes 断言。

### ADT 相比 enum 的优势

| 方面 | enum | ADT |
| :--- | :--- | :--- |
| 比较成本 | 整数 | tag 整数（无数据 case）或 tag+payload |
| 携带数据 | 否 | 是（`BudgetNudgeTop` 未来可携带 ordinal） |
| 穷尽匹配 | 需手动 | 编译器强制 |
| 新增 case | 全部 `if/match` 静默漏过 | 编译器报错所有未处理位置 |

### 受影响位置

| 现有（string） | 替换为（ADT） | 影响文件 |
| :--- | :--- | :--- |
| `sessionOwner: string` | `SessionOwner` | `FallbackRuntimeState.fs` 全部 owner 读写；`FallbackEventBridge.fs` 全部 owner 比较 |
| `PendingLease.Status: string` | `LeaseStatus` | `verifyLeaseWithStatus`、`TryTransitionPendingLease`、`executeContinuationIntent` |
| `NudgeLease.Status: string` | `LeaseStatus`（复用） | `TryTransitionPendingNudgeLease` |
| `finishContinuation` 的 `outcome: string` | `ContinuationOutcome` | `finishContinuation`、所有调用点 |
| `PendingLease.Owner: string` | `SessionOwner`（复用） | lease 构造与验证 |
| `NudgeLease.Owner: string` | `SessionOwner`（复用） | nudge lease 构造与验证 |

## 十、Auto-continue 与复杂时序

### Auto-continue（OpenCode tool-call 循环）

OpenCode `runLoop` 每步：`status.busy` → `filterCompactedEffect`（DB 重读）→ transform 钩子 → provider → tool result 入 DB → 循环。只有循环退出才 `session.idle`（PRD-02）。

transform 钩子被调用多次（每步一次），每次收到 fresh host 数组。

| 计数器/状态 | auto-continue 步骤间 | TransformState 行为 |
| :--- | :--- | :--- |
| `sessionGeneration` | 不变（同一 human turn） | — |
| `BacklogRevision` | 可能变（tool 里有 todowrite 并完成 backlog commit） | 变 → backlog 重建；不变 → 可复用引用 |
| `BudgetNudgeTrack` | 可能变（token 增长触发 `EmergencySignaled`） | 变 → top slot 重建；不变 → 同一份引用 |
| `Caps` | key 通常不变 | key 未变时可复用引用；发送前验证 canonical bytes |

结果：auto-continue 步骤间若无 backlog content change 且未触发 budget nudge，所有 synthetic 段可用 `ReferenceEquals` 减少分配；发送前仍须验证 canonical JSON bytes/prefix equality，满足时 prompt-cache 才可跨步骤命中。有 todowrite 并完成 backlog commit → 只重建 backlog 段。触发 budget nudge → 只重建 top slot 段。CAPS 按 key 判断是否重建。

### Compaction

compaction 发生时 `compactionOrdinal` 递增，`SessionOwner` 短暂为 `Compaction`，完成后回到 `NoOwner`。仅 `contextGeneration` 递增；`sessionGeneration` 保持不变，因为 compaction 仍属于同一会话。host 消息被摘要替换。

compaction 对 host/数据库摘要的持久化不等于 projection event commit：它本身不得递增 `BacklogRevision`、`CapsRevision`、`ReviewRevision` 或 `BudgetRevision`。只有对应内容改变并完成 NDJSON/journal append 后，所属 revision 才递增。TransformState 仍是进程内派生状态；重启或重新加载时从 durable NDJSON 重建，而不是从 compaction 后的 host 数组猜测。

| 段 | compaction 后 | 原因 |
| :--- | :--- | :--- |
| CAPS | revision/key 通常不变 | CAPS 内容由 `CapsFormat` 规范化生成，与对话 compaction 无关；investigator 仍接收该段 |
| Backlog | `BacklogRevision` 不变（若 backlog 内容未变） | backlog 投影从 NDJSON 事件 fold 计算（PRD-07），不从 host 消息计算；compaction 不写 backlog event |
| Top slot | 可能重建 | `ContextBudgetStore` phase reset（PRD-04），`BudgetNudgeTrack` 回到 `Idle` → `TopSlotKey` 从 `BudgetNudgeTop(...)` 变为 `NoTop` → 重建 |

### Async nudge 派发（PRD-06）

async nudge 是 `SessionIdle` 后的异步 `session.prompt`，不经 transform 钩子。nudge 派发时 `nudgeOrdinal` 递增，`SessionOwner` 变为 `Nudge`。

* 对 TransformState 无直接影响：async nudge 创建的是真实 user 消息（DB 持久化），不是 synthetic 消息
* 下一轮 transform 收到的 host 数组包含这条 nudge 消息（作为 Native 消息），不影响 synthetic 段状态
* CAPS、Backlog、Top slot 均不受影响

### Fallback continuation 派发（PRD-02）

continuation 派发时 `continuationOrdinal` 递增，`SessionOwner` 变为 `Fallback`。ZWS prompt 作为真实 user 消息入 DB。

* 对 TransformState 无直接影响：与 async nudge 类似，continuation 创建真实 user 消息
* CAPS、Backlog 不受影响
* Top slot：`BudgetNudgeTrack` 可能因 token 增长而变化 → 独立判断

### 新 human turn

仅当前真人轮次的 `turnGeneration`/`cancelGeneration` 递增，`sessionGeneration` 保持不变；`SessionOwner` 变为 `Human`，pending lease/nudge lease 被取消。

* `ContextBudgetStore` phase reset：`BudgetNudgeTrack` 回到 `Idle` → `TopSlotKey` 变为 `NoTop` → 重建（清除）
* CAPS：不变
* Backlog：`BacklogRevision` 不变 → 可复用引用；有新 todowrite 且 commit 成功 → 重建

### 状态变化总览

```
                        auto-continue    compaction     async nudge   cont.派发     新human turn
──────────────────────────────────────────────────────────────────────────────────────────────────
sessionGeneration       不变             不变           不变          不变          递增
BacklogRevision         可能递增         不变           不变          不变          可能递增
CapsRevision            不变             不变           不变          不变          不变
ReviewRevision          可能变           不变           不变          不变          可能变
BudgetRevision          可能变           phase reset    不变          不变          phase reset
BudgetNudgeTrack        可能变           Idle(复位)     不变          不变          Idle(复位)
SessionOwner(ADT)       不变             Compaction    NoOwner→Nudge NoOwner→Fallback NoOwner→Human
                                        →NoOwner(完成后)

Caps                    不变             不变           不变          不变          不变
Backlog                 revision不变→复用 不变           不变          不变          revision变化→重建
Top                     key不变→复用      可能重建       不受影响      不受影响      key变→重建
```

关键区别：async nudge 和 fallback continuation 创建的是真实 user 消息（DB 持久化），不是 transform 钩子的 synthetic 注入。它们不影响 TransformState，只影响下一轮 host 数组的内容。

## 十一、新增/删除的模块

### 删除

* `Shell/MessageTransformHostEntry.fs` 里的 `TransformFingerprint`、`FingerprintMetadata`、`SessionTransformCache`、`computeFingerprint`、`fingerprintEqual`、`computeMetadata`、`getSessionCache`——整套深比对机制全部删除。
* 三个 Host 的 `MessageTransform.fs` 里对 `stripSyntheticBySource` 的无条件调用——transform 钩子修改不持久，strip 是空操作。
* `runHostMessagesTransform` 的 `_reviewReplayMode` 和 `_replayTexts` 两个死参数。
* `Kernel/ReviewReplayPolicy.fs` 的 `reviewTaskFromTexts`、`Shell/ReviewReplaySync.fs` 的 `syncReviewFromTexts`——不再在 messagesTransform 路径里调用。保留 `Kernel/LoopMessages.fs::inferReviewTaskFromTexts` 本体和独立单测。

### 新增

* `Shell/MessageTransformStack.fs`：TransformState 类型 + `TopSlotKey` ADT + `computeTopSlotKey` 函数 + RuntimeScope 存取函数。
* ADT 类型定义（放在 `Kernel/FallbackKernel/Types.fs`）：`SessionOwner`、`LeaseStatus`、`ContinuationOutcome`。

### 修改

* `Shell/FallbackRuntimeState.fs`：`sessionOwners: Map<string, string>` → `Map<string, SessionOwner>`；`PendingLease.Status`/`NudgeLease.Status` → `LeaseStatus`；`PendingLease.Owner`/`NudgeLease.Owner` → `SessionOwner`。
* `Shell/FallbackEventBridge.fs`：`verifyLeaseWithStatus` 参数 `string` → `LeaseStatus`；`finishContinuation` 参数 `string` → `ContinuationOutcome`；所有字符串字面量 → ADT case。
* `Shell/MessageTransformPipeline.fs`：按三段独立判断重构。
* `Shell/MessageTransformCore.fs::checkAndInjectNudge`：改造为读写带 identity payload 的 `TransformState.Top`。
* `Shell/MessageTransformHostEntry.fs`：删除整套 Fingerprint 机制。
* 三个 Host `MessageTransform.fs`：删除 `stripSyntheticBySource` 无条件调用；删除 `replayTexts` 相关死代码。

## 十二、Review 状态：messagesTransform 钩子里做什么

结论不变：messagesTransform 钩子对 review 不需要做任何事——不渲染、不推断、不同步。删除 `replayTexts`/`_replayTexts` 死参数就是全部动作。真正的 review 状态同步路径（`syncReviewFromEventLogDedicated`，在 `/loop` 命令执行、reviewer 会话结束时调用）保持不变，它已经是从 `.wanxiangshu.ndjson` 单向恢复到 `ReviewStore` 内存投影，并在 review 内容提交变化时递增 `ReviewRevision`，符合“真相源唯一”的原则。investigator 消息变换仍必须注入 CAPS，不得因 review replay 被删除而省略 CAPS。

## 十三、分步执行顺序

1. **Kernel 层**：新增 `SessionOwner`、`LeaseStatus`、`ContinuationOutcome` ADT 类型定义。
2. **改 `Shell/FallbackRuntimeState.fs`**：全部 `string` → ADT。
3. **改 `Shell/FallbackEventBridge.fs`**：所有字符串字面量替换为 ADT case。
4. **新增 `Shell/MessageTransformStack.fs`**：TransformState + TopSlotKey + computeTopSlotKey + RuntimeScope 存取。
5. **改 `Shell/MessageTransformHostEntry.fs`**：删除 Fingerprint 机制，签名去掉死参数。
6. **改 `Shell/MessageTransformPipeline.fs`**：按三段独立判断重构。
7. **改 `Shell/MessageTransformCore.fs::checkAndInjectNudge`**：改造为读写 TransformState.Top。
8. **改三个 Host `MessageTransform.fs`**：删除 `stripSyntheticBySource`；删除 `replayTexts` 死代码。
9. **删除死代码**：`reviewTaskFromTexts`、`syncReviewFromTexts`。
10. **调整测试**：去掉死参数；字符串断言改为 ADT 断言；新增 `MessageTransformStackTests.fs`。
11. **验证**：重点检查 canonical outbound bytes/prefix equality、四个专用 revision 的递增边界、CAPS cache key 隔离和 investigator CAPS 注入；不得以 `ReferenceEquals` 作为正确性断言。

## 十四、不变式（验收标准）

1. 同一 session 连续两轮 transform 的 correctness 由 outbound canonical content 与稳定 prefix bytes equality 证明。revision/key 未变时 synthetic 段可逐一 `ReferenceEquals` 复用，但该引用相等仅是性能优化。
2. CAPS 段按 `scopeId × CapsRevision × policyVersion` 缓存；任一 key 字段变化必须重建对应段，key 不变时才可复用。CAPS 不得被当作永久不变的单次构造；compaction 不因自身动作改变 CAPS revision。investigator 始终收到 CAPS。
3. Backlog 折叠区间只有在对应 `BacklogRevision` 递增时才会整体替换；不递增则可复用。compaction 不改变 backlog 内容时不产生 backlog commit，因此 `BacklogRevision` 不变。
4. Top slot 由带 identity payload 的 `TopSlotKey` 驱动：`NoTop`、`BudgetNudgeTop(episodeId, syntheticId, contentVersion)`、`ParallelHintTop(callId, assistantMessageId, contentVersion)`。key 不变可复用 item；key 变必须重建或清除。
5. Auto-continue 步骤间：若 backlog/caps/review/budget 内容均未变化，且 top key 未变化，发送前 canonical bytes/prefix equality 成立时 prompt-cache 才可跨步骤命中。
6. messagesTransform 钩子对 review 激活/关闭状态零读取、零写入。
7. `FallbackRuntimeState` 和 `FallbackEventBridge` 中不存在任何字符串字面量做状态比较。全部使用 ADT case。
8. docs/10 记载的管线行为保持不变：caps 注入、backlog 投影、context budget nudge、parallel hint 的触发条件和注入内容不变，只改状态管理机制。
9. 架构测试不回归：`UsesProjectionPolicy`、`noQuadraticListAppend`、`parallelToolPromptSSOTGuard` 等。
10. async nudge（PRD-06）和 fallback continuation（PRD-02）不受本计划影响：它们创建真实 user 消息（DB 持久化），不经 transform 钩子 synthetic 注入。
11. `TransformState` 是 synthetic 注入段的唯一进程状态——不存在第二份"缓存"或"投影"与之竞争。NDJSON 是 durable SSOT，`TransformState` 从中派生。
