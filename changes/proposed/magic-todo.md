# Magic Todo Checkpoint Protocol — Formal Review Candidate

## 以 `todowrite` 为 Manager 节拍器的持续规划、迟滞过程评审、主动 Y 重基与终末 2N 整合

**Status:** Formal Review Candidate
**Priority:** P0 — lifecycle / review / context protocol
**Compatibility strategy:** OpenCode V1 hook overlay；不修改 OpenCode 本体
**Protocol SSOT（六源）:**
1. `LifeOpened` + `WorkRecordStart` → Manager Life / Opening 边界；
2. `TodoWriteAccepted` → checkpoint 与 process-review obligation；
3. `MagicTodoProjection` → canonical todo list；
4. frontier/request-range bounded canonical LWR → 工作与评审证据；
5. `PrefixCoverage` → lag-1 X→Y replacement 证明；
6. 既有 Finality witness/cohort → 终末性质量证明。
**Evidence SSOT:** Process review input / reviewer report / Finality work record 一律复用既有 canonical `LifecycleWorkRecord`（LWR）；禁止为 Magic Todo 另造平行工作记录投影。
**Coverage split:** Process review 使用 `RecordCoverage` / LWR（允许 canonical RawGap）；Manager lag-1 prefix rebase 使用 `PrefixCoverage` / proven Y only（禁止 RawGap）。二者不得互转。
**原则:** 君子不立危墙。非法状态应尽量不可表达；恢复依赖 durable facts，而不是内存 Stage、布尔组合、时间猜测或“下一次应该还会发生某事件”。

---

# 0. Executive Decision

本 Change 将 Manager 从现有：

```text
HumanRoot
→ LifeOpened
→ Planning-only Birth
→ ManagerWorkActivation
→ WorkActivated
→ Labor
→ Finality
```

改为：

```text
HumanRoot
→ LifeOpened
→ 立即持续工作
→ todowrite checkpoint
→ 工作
→ todowrite checkpoint
→ 工作
→ ...
→ suicide / Finality
```

当前正式 Manager 生命周期确实把 planning terminal、Activation continuation、`WorkActivated` 和 protected compression floor 作为独立边界。
本 Change **删除这条两阶段业务协议**；不将其改名为新的 `TodoPlanningStage`、`ReviewStage` 或其它程序计数器。

新的唯一持续节拍是：

> **每一次成功受理的 `todowrite` 都是一个 Todo Checkpoint。**

每个 checkpoint 原子地承担五件事：

```text
1. 消费上一 checkpoint 的过程性 review 结论；
2. 得到本轮 canonical old todo list；
3. 校验并受理 Manager 想提交的新 todo list；
4. 启动本 checkpoint 对应的新过程性 review；
5. 建立下一 provider turn 应采用的 lag-1 Y prefix rebase 边界。
```

过程 review 与 `todowrite` 严格 **1:1**：

```text
TodoWrite k
    consumes Review(k-1)
    creates  Review(k)
```

Review(k) 不阻塞 TodoWrite(k) 等待自己的结论；Manager 可以立即执行后续独立工作。

但当 TodoWrite(k+1) 到来时：

```text
Review(k) 尚未形成 ConsumableReview
→ TodoWrite(k+1) 必须阻塞
→ 直到 durable PERFECT / REVISE 已产生
   AND 该 verdict frontier 的 canonical Reviewer LWR 已 record-ready
```

`VerdictKnown` 立即决定业务 outcome；`ConsumableReview` 才允许下一 TodoWrite 消费上一报告。

这形成严格的 **lag = 1**。

额外硬边界：

```text
同一 Manager Life
同时最多一个新的 Todo Checkpoint admission

同一 provider turn 多个不同 todowrite
→ 按 canonical ToolPart ordinal 只 admit 第一个
→ 其余 fail closed

相同 ToolCallId 的 Host replay
→ 同一 TodoWriteId / 同一 obligation
→ 不新增 checkpoint / review
```

Host executor 与 Journal 非原子双写通过：

```text
TodoWritePrepared  →  before 已通过 Magic 校验
TodoWriteAccepted  →  physical tool completed 后 durable checkpoint
```

恢复，而不是靠“下次覆盖”。

`Accepted` 一旦存在且尚无 `TodoReviewConcluded`，Rk 就是必然的 durable obligation；after 不必“成功启动 reviewer 才算成功”。

---

# 1. 本 Change 为什么必须是协议级修改

## 1.1 当前 Manager 的两阶段模型与新需求直接冲突

当前正式语义中：

* Opening 到 `WorkActivated` 永久 raw X；
* planning terminal 才能触发 Activation；
* `WorkActivated` 建立 protected prefix end；
* Blogger compression 不能越过 Birth/Labor floor。

新需求要求：

```text
规划与执行不再是两阶段
Manager 从一开始就做真实工作
todo 规划随工作持续更新
每次 todowrite 本身成为 review/context 同步点
```

因此禁止保留：

```text
PlanningTail
ManagerWorkActivation
WorkActivated 作为业务资格
Birth/Labor compression floor
Activation-only suicide gate
```

否则系统只是：

```text
旧阶段机
+
新 Todo 阶段机
```

复杂度会进一步叠加。

---

## 1.2 OpenCode Host 的三钩子足够承载 V1 compatibility membrane

宿主调研已经证明 V1 主路径存在：

```text
tool.definition
tool.execute.before
tool.execute.after
```

且 `before` 可以异步等待并原地修改 args，`after` 可以改写模型可见的 result。

因此：

> 不替换 Host `todowrite` executor；把它降级成 compatibility sink。

真正的 canonical truth 由 Wanxiangshu 自己拥有。

---

## 1.3 Raw OpenCode 缺的能力由 Wanxiangshu 上层补，不要求修改 Host core

宿主原生 todo：

```ts
{
  content: string,
  status: string,
  priority: string
}
```

没有 stable item id；update 是整表 DELETE + INSERT；没有 reviewing transition law、lag-1 review 或 semantic merge。

Raw OpenCode 也没有 TodoCheckpoint rebase、Finality 2N 或 dedicated reviewer。

但这些都属于 Wanxiangshu 已经拥有的上层领域：

```text
AgentJournal
X / Y / Blogger
hidden Reviewer
Review witness
Finality cohort
messages.transform
Prompt projection
```

所以本 Change 的裁决是：

```text
OpenCode
= transport / executor compatibility layer

Wanxiangshu
= Magic Todo protocol owner
```

唯一不能静默绕过的是 V2 runner：当前 V2 local settle 尚未接同等 tool hooks。

因此在 V2 获得等价 hook contract 以前：

> **Magic-Todo Manager Attempt 不得使用 V2 todowrite execution path。**

不是“V1 有协议，V2 暂时裸奔”。

---

# 2. Goals

本 Change 必须一次性完成：

1. 删除 Manager planning-only → activation 两阶段，但保留 Opening 的结构性 Blogger floor（`WorkRecordStart`）；
2. 在 **Manager-only** pair-programming fragment 中要求持续使用 `todowrite`；
3. 新增结构上不可混淆的 tagged todo identity（`kind:"existing"|"new"`）；
4. 新增 `reviewing` 状态；
5. `completed` 前强制必须经过 `reviewing`；
6. 每次 `TodoWriteAccepted` 恰好派生一次 process-review obligation；
7. 本次调用消费上次 ConsumableReview；
8. 上次 review 尚未形成 ConsumableReview 时本次调用阻塞；
9. 同一 Manager Life 同时最多一个新 checkpoint admission；同 turn 多 `todowrite` 按 ToolPart ordinal 只 admit 第一个；
10. 相同 `ToolCallId` replay 幂等为同一 checkpoint；不同 `ToolCallId` 即使 list 相同也是新 checkpoint；
11. REVISE 时 canonical list 使用 union + progress-min + 明确字段裁决的 semantic merge；
12. PERFECT 时 proposed list 完全替换 old list；
13. tool result 返回上次 review 的 canonical ProcessReviewLWR；
14. tool result 返回当前 review 若 REVISE 时的 merge preview；
15. 明确提示 PERFECT 时 preview 不生效，以 submitted list 为准；
16. dedicated process reviewer 跨整个 Manager Life 持续复用，并至少保留到 `LifeCompleted`；
17. reviewer 每次看到 OpeningRaw + 当前 Manager Life 截止冻结 `ReviewFrontier` 的 canonical LWR（`includeOpening=false`；以有效 Y 为主体并保留未覆盖 RawGap）+ old todo + proposed todo；
18. 前一 checkpoint 被 review 时 Manager 可并行执行后续独立工作；
19. 每次 accepted todowrite 使下一 provider turn 的 desired lag-1 prefix cutoff 可推导；真实采用后才写 PrefixRebaseCommitted；rebase 只消费 PrefixCoverage 可证明的 Y prefix；
20. Opening 永不被 Y 替换，且不经 LWR 再复制一次 Opening；
21. `suicide` 抽干最后一个尚未被下一次 todowrite 消费的 process review；
22. dedicated reviewer 在首次进入 terminal Finality 时作为 cohort member enlist，之后遵循 ordinary graduate 规则；
23. process PERFECT 不计入 terminal dual-PERFECT；
24. 全部恢复逻辑只从事实重建（Prepared/Accepted/physical ToolPart）；
25. Manager 可看到过程 review outcome/report，但不可知道隐藏 reviewer/session/barrier/witness/2N 编排；
26. process review report / Finality dedicated reviewer record 都是 request-range bounded canonical LWR，不得取 session head；
27. 下一 TodoWrite 消费上一 review 需要 `ConsumableReview`：verdict 已知且同 snapshot 下 canonical Reviewer LWR record-ready；
28. 正常新 Life 的 MagicTodo canonical 初始为空；仅升级瞬间的 legacy open Life 允许一次 `LegacyTodoSeedAdopted`。

---

# 3. Non-goals

本 Change 明确不做：

```text
- 不修改 OpenCode 源码；
- 不把 plugin tool 同名覆盖 builtin todowrite；
- 不创建 TodoStage / ReviewStage / AwaitingReview bool；
- 不让 Manager 自己 fork dedicated reviewer；
- 不把 process PERFECT 当 terminal PERFECT；
- 不使用 wall-clock polling；
- 不靠 content 文本猜 todo identity；
- 不靠 Host TodoTable 恢复 canonical todo truth；
- 不把 session.compacted 冒充 TodoCheckpoint；
- 不长期维护 V1/V2 两套不同 Magic Todo 语义；
- 不新增 TodoProcessReviewEvidenceProjection / Y-complete reviewer projection 等第二份工作记录模型；
- 不用 RecordCoverage 推导 prefix 可替换性，也不用 PrefixCoverage 计算 LWR gap；
- 不拿 session 当前 head LWR 冒充某次 checkpoint / review / Finality 的 frontier-bounded LWR；
- 不用 `id?: string` 靠缺字段猜新旧；
- 不发明 Dedicated reviewer 永不 graduate / 每轮 Finality 强制回流特例；
- 不发明 Finality mechanical terminal-todo completeness gate；
- 不把 desired rebase 写成 Requested Stage / 未实际采用就 committed；
- 不把 Host TodoTable 在同 session 后续新 Life 中再次反推为 canonical seed。
```

---

# 4. Vocabulary 与形式化模型

定义：

```text
Tk = 第 k 个 TodoWriteAccepted checkpoint
     （先有 TodoWritePrepared，再有 Accepted）

Ck = Tk 开始时已经结算完成的 canonical old todo list

Pk = Manager 在 Tk 提交的 normalized proposed todo list

Rk = 由 Accepted(Tk) 派生的第 k 次 process-review obligation

Mk = semanticMerge(Ck, Pk)

WorkRecordStart = Opening exclusive end；Blogger/Y floor
```

结算只有两条规则：

```text
settle(Ck, Pk, PERFECT) = Pk

settle(Ck, Pk, REVISE)  = semanticMerge(Ck, Pk)
```

所以：

```text
T1:
    C1 = initial canonical list
    submit P1
    start R1

T2:
    await R1
    C2 = settle(C1,P1,R1)
    submit P2
    start R2

T3:
    await R2
    C3 = settle(C2,P2,R2)
    submit P3
    start R3
```

永远没有：

```text
Tk
→ 等待 Rk
→ 才返回
```

---

# 5. Manager 生命周期：彻底删除 Activation

## 5.1 新生命周期

```text
HumanRoot
→ append LifeOpened
→ Manager 立即获得正常工作工具
→ 普通 provider work
→ Todo checkpoints / fork / join / commands
→ suicide
```

`LifeOpened` 仍表示一个真实 Manager Life 的开始。

删除控制语义：

```text
PlanningTail
ManagerWorkActivation continuation
WorkActivated eligibility
ProtectedPrefixEnd as Birth/Labor / planning-labor stage floor
planning terminal → activation
```

历史 Journal 中已有 `WorkActivated` 可继续 decode，但升级后成为 inert legacy fact：

> 新生产决策不得读取它决定 Manager 是否可以工作、压缩或 Finality。

**删除的是 planning/labor stage floor，不是 Opening protection。**

---

## 5.2 Opening 保留

Opening 仍是：

```text
原始 HumanRoot
```

必须满足：

```text
Opening 永久 raw；
Opening 不交给 Y 改写；
Opening 不随 TodoCheckpoint rebase 消失；
Opening 是 reviewer authority context 的固定根；
process-review LWR 使用 includeOpening=false，不得再复制 Opening。
```

当前仓库本来就把原始 HumanRoot 先落 XTrace/LifeOpened，再做 provider-facing narrative。

新协议只删除 planning narrative/activation，不删除 Opening durable identity。

---

## 5.3 WorkRecordStart：Opening 的结构性 Blogger floor

现行 `WorkActivated.ProtectedPrefixEnd` 曾同时承担“不让 Opening/Birth 进入 Y”的 floor。

删除 Activation 后，正式稿必须新增一个**不是 Stage 的结构性 cursor**：

```text
ManagerLife.WorkRecordStart
=
Opening HumanRoot semantic range 的 exclusive end
```

它从 `LifeOpened` / XTrace Opening cursor 纯推导，不需要额外 Stage fact。

然后：

```text
Manager Blogger effectiveStart
=
max(RecordCoverage, Life.WorkRecordStart)
```

因此：

```text
删除
= planning/labor stage floor

保留
= Opening protection via WorkRecordStart
```

禁止实现成：

```text
删掉 WorkActivated
→ Blogger 从 0 / session head 开始
→ Opening 也被送进 Y
```

---

# 6. Pair-programming guidance：持续规划而不是 planning stage

现有 HOST-013 pair-programming marker 是对非 Companion/Blogger work session 的通用机制，**不天然只属于 Manager**。

因此禁止把 Magic Todo 文案直接并入全局：

```text
ProjectionConstants.PairProgrammingGuidelineText
```

正式投影必须是：

```text
general pair-programming guideline
+
if canonical role = Manager
   AND todowrite is provider-visible
then MagicTodoManagerGuideline
```

`MagicTodoManagerGuideline`（Manager-only fragment）冻结语义：

```text
Keep the todo list continuously accurate with todowrite.

Planning and execution are one continuous activity.
Do not stop for a separate planning-only phase.

Update todowrite whenever the truthful decomposition, discovered work,
or progress has materially changed.

For every previously returned todo, submit kind:"existing" with that exact id.
For a genuinely new todo, submit kind:"new" and omit id.

A todo must pass through reviewing before it can become completed.

While preceding work is being reviewed, continue useful independent
next-stage work. Do not idle merely waiting for that review.

Each accepted todowrite synchronizes the preceding checkpoint review
and starts the next checkpoint review.
Do not emit multiple concurrent todowrite calls in the same turn.
```

这里只暴露：

```text
checkpoint review
PERFECT / REVISE outcome
```

绝不暴露：

```text
reviewer identity
reviewer agent name
session id
barrier
witness
2N
finality cohort
confirmation mechanics
```

现有 GLORY-030 对 Manager 固定 surface 是全面禁止 review/PERFECT/REVISE 暴露。

本 Change 必须将其改成更精确的边界：

> Manager 可以观察 **Todo Checkpoint process-review protocol** 的 outcome 和 concrete report；仍不得观察执行该评审的隐藏角色及 Finality 内部机制。

---

# 7. Magic Todo V2 Schema

Provider-visible `todowrite` 必须用**结构上不可混淆的 tagged union**，禁止 `id?: string` 靠缺字段猜新旧：

```ts
type TodoStatus =
  | "pending"
  | "in_progress"
  | "reviewing"
  | "completed"
  | "cancelled"

type ExistingTodo = {
  kind: "existing"
  id: string
  content: string
  status: TodoStatus
  priority: string
}

type NewTodo = {
  kind: "new"
  content: string
  status: TodoStatus
  priority: string
}

type MagicTodoItem = ExistingTodo | NewTodo

type MagicTodoWriteInput = {
  todos: MagicTodoItem[]
}
```

## 7.1 为什么新旧必须 tagged

若使用 optional `id`：

```text
模型忘记给旧 item 带 id
=
Host 无法区分
“旧 item 丢了 id”
还是
“真正的新 item”
```

而 Host TodoTable 没有 stable id，只有 position，不能帮忙判定。

因此：

```text
kind:"existing" + id
→ 必须引用 canonical old list 中已有 identity

kind:"new"
→ 禁止携带 id
→ 仅 Host/MagicTodo 分配稳定 id
```

非法：

```text
kind:"existing" 缺少 id
kind:"existing" 的 id 不在 Ck
kind:"new" 携带 id
缺少 kind
→ reject；不产生 Prepared/Accepted；不产生 review
```

模型不应拥有“创造已有 identity”的 authority。

---

## 7.2 ID 生成

仅 `kind:"new"` 分配：

```text
TodoItemId =
    digest(
        ManagerLifeId
        + TodoWriteToolCallId
        + newItemOrdinal
    )
```

不使用：

```text
timestamp
random UUID
content text
position across checkpoints
```

同一 `ToolCallId` replay 必须生成相同 new ids。

---

## 7.3 Identity invariant

同一个 proposed list 内：

```text
duplicate id（existing 之间）
→ fail closed
```

已有 item 改 content / priority：

```text
kind:"existing" + 同 id
→ 合法
```

删除 item：

```text
PERFECT settlement
→ 真正删除

REVISE settlement
→ union 语义可能把旧 item 保留下来
```

tool result 返回给 Manager 的 settled/submitted 视图一律带稳定 `id`；下一轮 Manager 必须用 `kind:"existing"` 回传这些 id。

---

# 8. Status algebra

productive progress chain：

```text
pending
   <
in_progress
   <
reviewing
   <
completed
```

`cancelled` 是终止 disposition，不是 progress rank。

---

## 8.1 Completed gate

硬规则：

```text
old != completed
AND proposed == completed
→ old 必须恰好是 reviewing
```

因此：

```text
pending     → completed   ❌
in_progress → completed   ❌
new item    → completed   ❌

reviewing   → completed   ✅
completed   → completed   ✅
```

这是 Host execution gate，不是 prompt 建议。

非法 transition：

```text
before hook 直接失败
→ builtin todowrite executor 不运行
→ 不产生 TodoWritePrepared / TodoWriteAccepted
→ 不产生 process review
```

本文所称“每次 todowrite 触发一次 review”严格解释为：

> **每次 `TodoWriteAccepted` 的 checkpoint。**

Schema/transition/admission-admission 被拒绝的物理调用不是 checkpoint。

相同 `ToolCallId` 的 Host replay 视为同一个物理 attempt，不新增 review。

---

# 9. semanticMerge

用户明确要求：

> 条目取并集，进度取 min。

**本提案额外冻结的协议裁决（不是用户原文定理）：**

```text
identity = id
status   = conservative merge（见下）
content  = proposed.content
priority = proposed.priority
```

理由：

> REVISE 否定的是“整个 proposed list 可以完全取代 old”，不代表 Manager 对同一任务最新文字/优先级描述必须回滚；真正需要迟滞的是进度声明。

若未来要改成同 id 全字段 old-win，必须另开明确 Change；实现者不得自行切换。

正式定义如下。

设：

```text
old      = C
proposed = P
```

首先：

```text
keys = id(C) ∪ id(P)
```

### 仅 old 有

```text
result = old item
```

### 仅 proposed 有

```text
result = proposed item
```

### 双方都有且都在 productive progress chain

```text
content  = proposed.content   // 协议裁决
priority = proposed.priority  // 协议裁决
status   = minProgress(old.status, proposed.status)
```

例如：

```text
in_progress + completed
→ in_progress

reviewing + completed
→ reviewing

completed + completed
→ completed
```

---

## 9.1 cancelled

取消不是“完成进度”。

REVISE merge 采用保守语义：

```text
cancelled + cancelled
→ cancelled

old.status != proposed.status
AND 任一 side = cancelled
→ 保留 old.status
```

因此未经 PERFECT：

```text
old active + proposed cancelled
→ 不自动取消

old cancelled + proposed active
→ 不自动复活
```

即一次未经 PERFECT 接受的 cancellation / resurrection 都不得单边改写旧工作 disposition。

双方都 productive 时仍取 `minProgress`。

若 Reviewer 判定 proposed cancellation（或复活）完全合理：

```text
PERFECT
→ proposed list 完全替换 old
→ proposed disposition 正式生效
```

---

## 9.2 PERFECT 不 merge

非常重要：

```text
PERFECT
≠ merge 然后把 status 提高

PERFECT
= P 完整替换 C
```

旧 item 如果未出现在 P 中：

```text
→ 消失
```

所以 merge preview 永远只能描述：

> **“如果当前 review 最终 REVISE”**

不能被 Manager 误认为未来必定生效的 list。

---

# 10. Tool Definition Hook

`tool.definition` 是 provider-visible V2 schema 的唯一 owner。

必须同时更新：

```text
parameters
jsonSchema
description
```

宿主调研已发现，只更新其中一个引用可能造成 definition 组装不一致。

description 必须解释：

```text
- kind:"existing" must reuse exact id
- kind:"new" omits id; Host assigns id
- reviewing
- completed gate
- keep list current
- process review lag semantics
- do not emit concurrent todowrite in one turn
```

但不能出现：

```text
dedicated reviewer
hidden agent
reviewer session
Finality cohort
barrier
witness
```

---

# 11. Tool Before：admission + Prepared + compatibility projection

Host 主路径的 before 是：

```text
tool.execute.before(
    { tool, sessionID, callID },
    { args }
)
```

而且 executor 只会观察 **原地 mutation**；重新赋 `output.args = ...` 不会改变本地 `args` 引用。

---

## 11.1 并发 / 同 turn 多 todowrite admission

lag-1 公式隐含单链：

```text
T1 → T2 → T3
```

但模型可能在同一 provider turn / tool batch 并行发多个 `todowrite`。若两个 before 同时读 `Ck`：

```text
T5a reads C5
T5b reads C5
```

1:1 review 链会分叉。

正式协议：

> **一个 Manager Life 同时最多只有一个新的 Todo Checkpoint admission。**

规则冻结为：

```text
同一 provider turn 出现多个不同 ToolCallId 的 todowrite
→ 按 canonical ToolPart ordinal 只允许第一个
→ 其余 fail closed

不得按 hook 到达顺序 / wall-clock / Map 抢锁决定胜者

相同 ToolCallId 的 Host replay
→ 幂等视为同一 Prepared/Accepted checkpoint
→ 不新增 review
```

“第一个”必须来自 durable ToolPart ordinal / 已证明的 Host ordering，而不是 process-local race。

---

## 11.2 ToolCallId 与 TodoWriteId

```text
TodoWriteId =
    digest(ManagerLifeId + ToolCallId)
```

因此：

```text
same ToolCallId replay
→ same TodoWriteId
→ same review obligation
→ 不新增 review

different ToolCallId
即使 submitted list byte-identical
→ 新 checkpoint
→ 新 process review
```

禁止“相同 list 去重优化”。

---

## 11.3 before 程序

```fsharp
todoCheckpointBefore {
    let! life = requireOpenManagerLife()
    do! admitSingleCheckpointOrFail life callId // ToolPart ordinal rule

    let! old = settlePreviousCheckpointIfAny life // awaits ConsumableReview(k-1)

    let! original = readProviderTodoInput sessionId callId

    let proposed =
        original
        |> decodeTaggedTodos
        |> allocateNewIds life callId
        |> validateUniqueIds
        |> validateExistingIds old
        |> validateTransitions old

    let preview =
        semanticMerge old proposed

    do! appendTodoWritePrepared
            life
            callId
            old
            proposed
            // ReviewFrontier frozen here as exclusive cursor before this tool-call

    do! installEphemeralBridge
            sessionId
            callId
            old
            proposed
            preview
            previousReview

    do! mutateArgsInPlaceToCompatibilityV1 proposed
}
```

`TodoWritePrepared` 是已经发生的事实：

> 这个 call 已通过 Magic 校验，并冻结了 BaseTodo / Proposed / ReviewFrontier。

它**不是** Stage，也还不构成 Accepted checkpoint。

---

# 12. Before → After 的隐藏 JS bridge

用户要求 before→after 之间通过 JS 对象隐藏属性传递信息。

由于宿主只证明“同一个 trigger 内 plugin 共享 mutable output”，并没有合同证明 before hook 的 output object 与 after hook 的 output object 是同一个对象，所以禁止依赖这种偶然对象身份。宿主报告也明确把这一点列为需 canary 的边界。

正式实现采用：

```js
const MagicTodoBridge = Symbol("wanxiangshu.magic-todo.bridge")
const bridges = new Map()
```

before：

```js
const carrier = {}

Object.defineProperty(carrier, MagicTodoBridge, {
  enumerable: false,
  configurable: false,
  writable: false,
  value: {
    settledOld,
    normalizedProposal,
    previousReview,
    revisePreview,
    compatibilityProjection
  }
})

bridges.set(sessionID + ":" + callID, carrier)
```

after：

```js
const carrier = bridges.get(key)
const bridge = carrier?.[MagicTodoBridge]
```

成功后：

```text
delete Map entry
```

所以：

```text
JS hidden property
= before→after 的 process-local short bridge

AgentJournal
= durable truth
```

绝不反过来。

---

## 12.1 Bridge 不能承担恢复

若 execute 抛错，Host 不保证 after 会运行。

因此 bridge：

```text
不能表示 TodoWritePrepared / Accepted
不能表示 checkpoint 已创建
不能表示 review obligation
不能表示 settlement 已提交
```

Prepared/Accepted 只能进 Journal。

tool/turn failure cleanup 时清除残留 bridge。

崩溃恢复完全忽略 bridge。

---

# 13. P0 Host Canary：V2 historical input alias

这是整个 membrane 上线前最重要的 blocker。

宿主当前顺序是：

```text
provider args
→ ToolPart.input 持久化
→ before
→ executor
```

理论上这是好事：provider history 可保留原始 V2 input。

但必须证明：

```text
before 修改 args.todos
不会同时修改 durable ToolPart.input.todos
```

测试：

```text
provider sends:
[
  { kind:"existing", id:"A", status:"reviewing", ... }
]

before:
    args.todos := V1 compatibility projection

executor:
    MUST see compatibility list

durable ToolPart.input:
    MUST still contain original V2 list
```

如果 alias：

```text
before mutation
→ historical provider call 也被改写
```

则当前 membrane **禁止上线**。

不能通过：

```text
“after 再改回来”
```

补救，因为 durable/prefix identity 已经在不受控对象别名上。

Canary fail 后只能：

```text
等待/增加 Host 能力
或改变 execution seam
```

不能绕。

---

# 14. Compatibility projection 到 Host TodoTable

Host store 只有：

```text
content
status
priority
position
```

没有 stable id。

因此：

```text
Host TodoTable
= compatibility / optimistic working projection

MagicTodo Journal projection
= canonical semantic truth
```

---

## 14.1 reviewing 的 sink 策略

因为 Host status 实际是 string，第一选择是：

```text
reviewing → "reviewing"
```

先做 UI/API canary：

```text
TodoTable reviewing
→ todo.updated
→ API
→ TUI/sidebar
```

若所有消费者都能容忍：

```text
直接保留 reviewing
```

如果现有 UI/消费者会错误处理第五态：

```text
canonical reviewing
→ compatibility in_progress
```

仅 compatibility sink 降级。

不得修改 canonical status。

---

## 14.2 kind / id 必须在 before 剥离

Provider V2：

```text
{kind, id?, content, status, priority}
```

送给 original Host executor：

```text
{content, status, priority}
```

因为 `tool.definition` 修改的是广告 schema，不会自动替换原 executor 的 decode schema。

---

# 15. Tool After：Accepted + ensureReview + enriched result

只有：

```text
TodoWritePrepared exists
AND Host physical ToolPart completed successfully
```

之后，after / recovery 才能 `ensure TodoWriteAccepted`。

顺序严格：

```text
1. recover hidden bridge / or rebuild from Prepared + physical ToolPart
2. ensure TodoWriteAccepted（幂等；含已冻结 ReviewFrontier）
3. ensure DedicatedTodoReviewer
4. ensureReview(Tk)
   = 若尚无 TodoReviewConcluded
     → 必然义务
     → ensure TodoProcessReviewAssigned
     → 提交/续跑 reviewer
   （ManagerCheckpointLWR 可含 RawGap；不要求 Manager Y 追平）
5. desired lag-1 prefix cutoff 由 Accepted checkpoints 纯推导
   （此时还不写 PrefixRebaseCommitted）
6. render enriched tool result
7. cleanup bridge
8. return
```

关键不变量：

```text
TodoWriteAccepted(Tk) exists
AND TodoReviewConcluded(Tk) does not exist
→ Rk 是必然的 durable obligation
```

因此：

```text
不需要 TodoReviewStarted / ReviewInFlight
after 不必“成功启动 reviewer 才算 Accepted 成功”
ensureReview 可在 after / restart / 下一 todowrite / suicide 任意重入
```

禁止：

```text
先启动 reviewer
→ 后 Accepted
```

否则 crash 可以留下没有 checkpoint authority 的幽灵 review。

也禁止把：

```text
Host TodoTable 已变成 Pk
```

误当成 Accepted；没有 `TodoWriteAccepted` 就没有 checkpoint。

---

# 16. Durable Facts

建议新增领域事实。

## 16.1 TodoWritePrepared

before 在 Magic 校验通过后立刻 durable：

```fsharp
TodoWritePrepared of
    { ManagerSessionId
      ManagerLifeId
      TodoWriteId
      ToolCallId
      ToolPartOrdinal

      BaseTodoRef
      BaseTodoDigest

      ProposedTodoRef
      ProposedTodoDigest

      // Exclusive XTrace / SemanticCursor frontier of the current Manager Life.
      // Frozen at prepare time: immediately before this TodoWrite's physical tool-call.
      ReviewFrontier : XTraceCursor
      SemanticVersion }
```

含义：

> 这个 call 已通过 Magic 校验；BaseTodo / Proposed / ReviewFrontier 已冻结。

`Prepared` **还不是** checkpoint，也还不派生 review obligation。

---

## 16.2 TodoWriteAccepted

```fsharp
TodoWriteAccepted of
    { ManagerLifeId
      TodoWriteId
      ToolCallId
      PreparedFactRef
      PhysicalToolCompletedEvidence
      SemanticVersion }
```

只有：

```text
Prepared
+
Host physical todowrite ToolPart completed successfully
```

才能 Accepted。

`TodoWriteAccepted` 是：

```text
checkpoint SSOT
+
process-review obligation SSOT
```

```text
Accepted(Tk) exists
AND TodoReviewConcluded(Tk) missing
→ Rk 必然存在，可被任意 ensureReview 重入
```

`BaseTodo` / `Proposed` / `ReviewFrontier` 以对应 `Prepared` 为准；不能未来用新 merge 算法重新猜。

`ReviewFrontier` 禁止：

```text
模糊字符串
session 当前 head
After(TodoWrite result)
跨 Life 的绝对 session cursor 误用
```

它必须与 `ManagerLifeId` 绑定，并作为该 checkpoint 的不可变证据上界。

Crash 裂缝的正式恢复：

```text
Prepared + physical tool completed
→ ensure Accepted

Prepared + physical tool failed / absent
→ 不 Accepted
→ Host TodoTable 若已乐观写成 Pk
  也不构成 canonical checkpoint
  下次 before 从 Journal canonical 覆盖 sink
```

---

## 16.3 TodoProcessReviewAssigned

每次 process review assignment 冻结 dedicated reviewer 的 request-range 起点：

```fsharp
TodoProcessReviewAssigned of
    { ManagerLifeId
      TodoWriteId
      TodoReviewId
      DedicatedReviewerId
      ReviewerSessionId
      AssignmentStartCursor : XTraceCursor
      ManagerReviewFrontier : XTraceCursor }
```

同一 `TodoReviewId` 的 assignment 必须幂等；crash recovery 只能重建同一个 range，不得另开第二段 assignment。

---

## 16.4 TodoReviewConcluded

```fsharp
TodoReviewConcluded of
    { ManagerLifeId
      TodoWriteId
      TodoReviewId
      DedicatedReviewerId
      ReviewerSessionId

      Verdict : PERFECT | REVISE

      // Canonical ProcessReviewLWR for this request range only.
      WorkRecordRef
      WorkRecordDigest

      ReviewerRecordFrontier : XTraceCursor
      ProviderRunId
      ToolCallId }
```

Process review 只需要一个 verdict。

它不进入 Finality dual-PERFECT witness algebra。

`WorkRecordRef` 的唯一来源是：

```text
canonical Reviewer LWR
range =
    AssignmentStartCursor
    through ReviewerRecordFrontier
includeOpening = false
```

禁止从：

```text
verdict 参数
assistant terminal 文本摘取
Host issue list
第二个 summarizer
tree diff
```

另造 review report。

Manager 在下一次 `todowrite` 中看到的 `Previous checkpoint review report` 即该 `WorkRecordRef`。

---

## 16.5 DedicatedTodoReviewerEnlisted

```fsharp
DedicatedTodoReviewerEnlisted of
    { ManagerLifeId
      DedicatedReviewerId
      ReviewerSessionId }
```

---

## 16.6 DedicatedTodoReviewerReplaced

只有 Host 已证明原 physical session 永久不可恢复时：

```fsharp
DedicatedTodoReviewerReplaced of
    { ManagerLifeId
      DedicatedReviewerId
      OldSessionId
      NewSessionId
      EvidenceRef }
```

逻辑 dedicated identity 不变。

Replacement 新 session 必须重新获得：

```text
OpeningRaw
+
当前 Manager Life 截止最新已消费 checkpoint 的 frontier-bounded Manager LWR
+
全部既往 process-review WorkRecordRef
```

然后才可继续。

---

## 16.7 TodoCheckpointPrefixRebaseCommitted

Accepted checkpoints **已经**使 desired lag-1 cutoff 可纯推导：

```text
desiredCutoff(Tk) = Before(T(k-1))   // T1 无 prior
```

这**不需要** durable Requested / NeedRebase fact。

只有下一次真实 provider request **成功采用**该 projection 时，才写：

```fsharp
TodoCheckpointPrefixRebaseCommitted of
    { ManagerLifeId
      TriggerTodoWriteId
      CoveredBeforeTodoWriteId : TodoWriteId option
      YBundleRef
      YBundleDigest
      ProviderPrefixDigest }
```

它描述的是：

> “某次真实 provider projection 已采用这个 checkpoint rebase。”

不是意图，也不是下一步程序位置。

因此：

```text
Accepted checkpoints
= desired prefix policy 的事实源

PrefixRebaseCommitted
= 某次实际 provider projection 的证明
```

todowrite 后马上 crash：

```text
不得声称 rebase 已 committed
只需在下次 provider transform 按 Accepted 链重算 desired cutoff
```

`YBundleRef` 必须来自 PrefixCoverage 可证明的 complete-turn Y prefix；不得嵌入 LWR RawGap。

---

# 17. 禁止的 durable 状态

不得新增：

```text
TodoStage
ReviewStage
WaitingForReview
ReviewInFlight
HasPendingReview bool
NextTodoWriteNumber
CurrentTodoStatus
NeedRebase bool
AwaitingReviewer
ReviewGeneration
ResumeAtTodo
```

是否仍有未消费 / 未完成 process review，直接由 facts 推导：

```text
TodoWriteAccepted exists
AND matching TodoReviewConcluded does not yet exist
→ review obligation pending

TodoWriteAccepted exists
AND matching ConsumableReview does not yet exist
→ next TodoWrite must await consumability
```

其中 `ConsumableReview` 要求：

```text
matching TodoReviewConcluded exists
AND WorkRecordRef record-ready
AND verdict / frontier / LWR 来自同一 Journal snapshot
```

不得用 `HasPendingReview` bool 或 Stage 代替。

这延续现有 GLORY “事实与 Projection 不保存程序计数器”的原则。

---

# 18. Process Review Evidence：复用 canonical LWR

每个 `TodoWriteAccepted(Tk)` 恰好派生一个 Rk obligation。

Review identity：

```text
TodoReviewId =
digest(ManagerLifeId + TodoWriteId)
```

因为 `TodoWriteId = digest(ManagerLifeId + ToolCallId)`，Host replay 不会另造第二义务。

`ensureReview(Tk)` 必须幂等，并可在 after / restart / 下一 todowrite / suicide 任意重入。

不需要：

```text
TodoReviewStarted
ReviewInFlight
```

Todo Checkpoint Process Review **不得新增独立的工作记录投影体系**。

唯一工作证据表示继续复用既有 canonical `LifecycleWorkRecord`（LWR）。

禁止：

```text
TodoProcessReviewEvidenceProjection
Y-complete reviewer projection
“只发送纯 Y frames” 的第二套 renderer
拿 session 当前 head LWR 冒充 checkpoint LWR
```

---

## 18.1 ReviewFrontier 冻结

对于第 `k` 个 accepted Todo Checkpoint：

```text
ReviewFrontier(k)
=
当前 Manager Life 中
紧邻 TodoWrite(k) physical tool-call 之前的
exclusive semantic / XTrace frontier
```

该 frontier 在 `TodoWritePrepared(k)` 中冻结，并由 `TodoWriteAccepted(k)` 继承；后续 Manager 并发产生的工作不得改变它。

因此要**复用 LWR renderer/algebra，不复用“取 session 当前 head”这个 convenience API**。

---

## 18.2 Reviewer 输入

Rk 的 canonical logical input：

```text
OpeningRaw(current Manager Life)

+

ManagerCheckpointLWR(k):
    canonical LWR
    includeOpening = false
    range =
        current Life opening work cursor
        through ReviewFrontier(k)

+

OLD TODO LIST = Ck

+

PROPOSED TODO LIST = Pk
```

`ManagerCheckpointLWR(k)` 保持 canonical LWR 的既有语义：

```text
CompressedMiddleFromY
+
RawGapFromX
+
TerminalOutputRaw（若该 bounded range 合法存在）
```

其中 Y 是主体；Y 尚未覆盖到 `ReviewFrontier(k)` 时，由 LWR 的 canonical RawGap 补足未覆盖 suffix。

用户原来的“从头开始所有 Y 工作记录”，在正式产品术语中解释为：

> **canonical LWR：以当前 Manager Life 内所有当前有效 Y 为主体，并以 canonical raw gap 补足 Y 尚未覆盖到 `ReviewFrontier(k)` 的工作尾部。**

这不是削弱要求，而是用仓库已经批准的“不会漏证据”的正式表示来满足它。

OpeningRaw 单独作为当前任务 authority 传入；LWR 使用 `includeOpening=false`，不得重复 Opening。

同时禁止进入该 LWR：

```text
raw tool call/result
raw call/result linkage
future work after ReviewFrontier(k)
previous Manager Life material
```

todowrite 自己的 raw tool call/result 不属于被评审工作内容；old/new todo 已结构化单独提供。

---

## 18.3 Manager Y 未覆盖 frontier ≠ review 不可开始

因为 Manager-side process evidence 允许合法 RawGap：

```text
Y 已覆盖部分
→ LWR 用 Y

Y 尚未覆盖 suffix
→ LWR 用 canonical RawGap
```

所以只要能够从**同一个冻结 ReviewFrontier(k)** 物化合法 LWR，Rk 就可以立刻启动。

因此：

```text
Manager Y 尚未覆盖 ReviewFrontier(k)
≠ Process review 不可开始
```

Process review **不得**等待 Manager Blogger 单纯为了把 RawGap 全部转成 Y；合法 canonical LWR 已经是完整 process-review evidence。

这更符合：

```text
每次 accepted todowrite
→ 触发一次 review
```

而不是：

```text
每次 todowrite
→ 先触发“等待 Blogger”
→ 某个未来时刻才真正 review
```

---

## 18.4 RecordCoverage 与 PrefixCoverage 继续严格分型

允许 Process Review 复用 LWR **不改变 prefix replacement 规则**。

```text
Process Review evidence
    → canonical LWR
    → RecordCoverage
    → Y + canonical RawGap
```

而：

```text
TodoCheckpoint lag-1 prefix rebase
    → CoverableRecordPrefix / equivalent proven prefix
    → PrefixCoverage
    → only complete-turn Y prefix
```

LWR RawGap **永远不得**直接进入 X prefix replacement。

因此：

```text
“足够完整，可以拿去 review”
```

与：

```text
“已经证明，可以替换 provider-visible X prefix”
```

是两个不同命题。

前者允许 canonical RawGap；
后者不允许。

禁止用 `RecordCoverage` 推导 prefix 可替换性，也禁止用 `PrefixCoverage` 计算 LWR gap。

---

## 18.5 Dedicated Process Reviewer 的每次报告也复用 LWR

Dedicated reviewer 物理 session 可以跨多个 Todo Checkpoint 复用，但每个 Process Review 必须拥有独立的 request range。

对于 `Rk`：

```text
ReviewerRecordStart(k)
=
本次 process-review assignment 的 durable opening cursor
（来自 TodoProcessReviewAssigned）

ReviewerRecordFrontier(k)
=
本次 PERFECT / REVISE verdict 对应的 durable terminal frontier
```

上一轮、下一轮 process-review material 不得进入本轮 report。

本轮 review report 的唯一来源为：

```text
ProcessReviewLWR(k)
=
canonical LWR
includeOpening = false
range =
    ReviewerRecordStart(k)
    through ReviewerRecordFrontier(k)
```

**不要新造两种 work-record renderer。**

若直接调用：

```text
lifecycleWorkRecord(dedicatedReviewer, includeOpening=false)
```

取当前 head，则 R4 report 会吞入 R1–R3 全部历史。这被明确禁止。

---

## 18.6 Process Review 的可消费结论

Process reviewer 的 PERFECT / REVISE 一旦 durable，即立即决定该 checkpoint 的业务 verdict；不存在第二次 confirmation。

但下一 TodoWrite 要消费上一 Review 时，需要的不是只有 `VerdictKnown`，而是：

```text
ConsumableReview(k)
=
durable verdict exists
AND
the canonical ProcessReviewLWR(k)
is record-ready
AND
verdict / frontier / LWR
come from one causally consistent journal snapshot
```

若 verdict 已知但 canonical LWR 尚未 record-ready：

```text
TodoWrite(k+1)
→ 等待 AgentJournal change
→ 重读同一普通 projection
→ 重新判断 record-ready
```

等待方式直接复用 GLORY-072/073：

```text
同一个 Journal snapshot
→ 判断 record-ready
→ 在同 snapshot materialize canonical LWR
→ 不 ready 就 await Journal change
```

禁止：

```text
timer
sleep
wall-clock polling
用较晚 XTrace head 替换 frozen frontier
用 raw terminal 或 summary 临时顶替 LWR
coverage snapshot 与 LWR materialization 分两次读取
```

Process-local waiter 的消失或进程重启不构成 review abandonment；恢复必须从 durable assignment、verdict 和 frontier 重建同一个等待。

两段式语义必须分开：

```text
Manager → Reviewer:
    LWR = Y + RawGap
    不要求 Manager Y 追到 frontier
    Rk 可及时启动

Reviewer → Manager report:
    为生成 canonical reviewer LWR
    Reviewer 自己的 Y/Blogger 可能还在追 verdict frontier
    所以 T(k+1) consume Rk 前必须 record-ready
```

业务 verdict 立即关闭逻辑判断；work-record materialization 稍后因果同步。二者不得混为一个 Stage。

---

# 19. Dedicated Reviewer

每个 Manager Life：

```text
恰好一个 logical DedicatedTodoReviewer
```

首次 accepted TodoWrite：

```text
若尚不存在
→ Host-owned hidden session 创建
→ durable enlist
```

后续：

```text
same logical reviewer
prefer same physical session
fresh TodoReviewId
fresh process-review assignment
```

Manager 永远不能：

```text
fork
join
list
resume
inspect
```

这个隐藏 session。

Raw OpenCode 的 resumable/background task 能力证明“连续子 session”在宿主层可行，但本协议不得通过 Manager 可见 `task` 创建 dedicated reviewer。

应使用 Wanxiangshu Host-owned reviewer/session runtime。

---

# 20. Process Reviewer Prompt

Process request 与 Finality request 必须是两个 typed RequestKind。

Process reviewer instruction：

```text
You are reviewing the ongoing quality and truthfulness of a work process.

You receive:
- the original task authority (OpeningRaw);
- the frontier-bounded canonical Lifecycle Work Record for the current
  Manager Life through this checkpoint (Y-backed middle plus any canonical
  raw gap not yet covered by Y; Opening is not duplicated inside it);
- the previously settled todo list;
- the newly proposed todo list.

Return exactly one process verdict:

PERFECT
when the work record and proposed todo list truthfully represent the current
state, no necessary unfinished work has been concealed or overclaimed, and no
material correction is required before this process may continue.

REVISE
when any defect, omission, overclaim, missed work, incorrect progress,
unsafe transition, unresolved issue, or required correction remains.

For REVISE, provide a concrete report inside this review turn.
The Host will persist the canonical ProcessReviewLWR for this request range
as the durable review report; do not invent a second report channel.

Do not describe hidden orchestration mechanics, session identity, barriers,
finality cohorts, or who consumes this report.
```

它只有一次 verdict。

绝无：

```text
first PERFECT
→ skeptical challenge
→ second PERFECT
```

那是 Finality 专属语义。

---

# 21. Manager 可以在 process review 期间继续工作

这是协议显式并发语义，不只是 prompt 建议。

```text
Tk returns
↓
Rk runs in hidden reviewer
||
Manager executes next useful work
```

Manager 不应因为：

```text
“上一阶段还在 review”
```

而停止可安全并行的下一阶段工作。

真正的同步点只有：

```text
Tk+1
```

如果 Rk 尚未形成 ConsumableReview（verdict + record-ready Reviewer LWR）：

```text
Tk+1 blocks
```

不是：

```text
Manager work blocks
```

---

# 22. Enriched `todowrite` Tool Result

建议使用 byte-stable renderer。

结果必须包含：

```text
Previous checkpoint review
  = Verdict + canonical ProcessReviewLWR(WorkRecordRef)
Settled current todo list
Submitted todo list
Current REVISE preview
Explicit PERFECT rule
Concurrency instruction
```

示意：

```text
Previous checkpoint review:
Verdict: REVISE

Report:
<canonical ProcessReviewLWR for previous review request range>

Settled current todo list:
<canonical Ck>

Submitted todo list:
<normalized Pk>

If THIS checkpoint later receives REVISE,
the next settled todo list will be:
<semanticMerge(Ck,Pk)>

IMPORTANT:
The list above is only the REVISE preview.
If this checkpoint receives PERFECT,
your submitted todo list will replace the settled list exactly.

This checkpoint is now being reviewed.
Continue useful independent next-stage work.
Your next todowrite call will synchronize with this review if necessary.
```

第一次：

```text
Previous checkpoint review:
None — this is the first checkpoint.
```

---

# 23. “current todo” 的两个视图必须命名清楚

避免概念打架：

```text
Settled Current
= Ck
= 上一 review 已经结算的 canonical truth

Working Proposal
= Pk
= Manager 当前正在执行的最新工作视图
```

Host TodoTable 可以立即显示：

```text
Pk compatibility projection
```

所以 UI 更贴近 Manager 当前工作。

但下一轮 canonical settlement 仍：

```text
Rk PERFECT → Pk

Rk REVISE  → merge(Ck,Pk)
```

Host store 绝不能反向成为 canonical truth。

---

# 24. Lag-1 Y Prefix Rebase

这是 Todo Checkpoint 的第二条核心协议。

当前正式 ARCH-004 原本只允许少数 cold boundary 改 active prefix，并明确普通回合保持 X prefix 字节稳定。

本 Change 增加一种合法 cold boundary：

```text
TodoCheckpointPrefixRebaseCommitted
```

注意：Accepted checkpoints 只使 desired cutoff 可推导；只有实际 provider projection 采用后才产生这条 committed fact。

第 `k` 个 checkpoint 同时拥有两个不同 frontier 用途：

```text
ReviewFrontier(k)
    → 为 Rk 冻结 process-review evidence
    → 允许 LWR RawGap

TodoPrefixCutoff(k) / TodoRebaseCutoff(Tk)
    → 下一轮 Manager context 的 lag-1 rebase
    → 只能使用可证明的 Y prefix
```

两者可以指向同一 semantic boundary，但**证明类型不同**，不得互转。

---

## 24.1 精确 lag 语义

设：

```text
T1, T2, T3...
```

T1 后：

```text
Opening
+ raw X[Opening .. T1]
```

T2 后下一 provider projection：

```text
Opening
+ Y[after Opening .. before T1]
+ raw X[T1 .. T2]
```

T3 后：

```text
Opening
+ Y[after Opening .. before T2]
+ raw X[T2 .. T3]
```

T4 后：

```text
Opening
+ Y[after Opening .. before T3]
+ raw X[T3 .. T4]
```

因此：

> **上一个 checkpoint 到本 checkpoint 之间始终仍是 raw X。**

严格迟滞 1。

例如：

```text
T3 accepted

R3 input:
    Opening
    + canonical Manager LWR through Before(T3)
      （可含 T2→T3 尚未被 Y 覆盖的 RawGap）
    + C3
    + P3

Manager next provider prefix:
    Opening
    + proven Y only through Before(T2)
    + raw X[T2..]
```

这正是预期的 lag-1：

```text
Reviewer 可以审到当前 checkpoint 前的全部 canonical 工作证据；

Manager context 小压缩仍故意迟滞一个 checkpoint，
不把未经 PrefixCoverage 证明的 RawGap 冒充 Y prefix。
```

---

## 24.2 为什么 cutoff 是 `Before(previous TodoWrite)`

不能使用：

```text
After(previous TodoWrite result)
```

否则上一 checkpoint tool result 中刚返回给 Manager 的：

```text
previous review report
settled current
merge preview
```

可能立刻被压缩掉。

定义：

```text
TodoRebaseCutoff(Tk)
= Before(T(k-1) tool-call)
```

这样整个上一 checkpoint call/result 至少保留一个 raw X 节拍。

---

# 25. Rebase 不要求修改 OpenCode history

宿主报告正确指出 OpenCode 没有一等 TodoCheckpoint rebase API，但 `experimental.chat.messages.transform` 能修改当前 provider-bound messages。

Wanxiangshu 可以自己做：

```text
durable TodoWriteAccepted facts
+
durable Y frames
+
TodoCheckpointPrefixRebaseCommitted
+
deterministic messages.transform
```

所以：

```text
Journal fact = durability
messages.transform = renderer
```

不要求 OpenCode transcript 物理重写。

重启后：

```text
Boot fold
→ derive latest required checkpoint cutoff
→ materialize same Y bundle
→ deterministic transform
```

只要相同 facts 能产生 byte-stable provider projection，就完成 crash-safe rebase。

---

# 26. Process Review 与 Prefix Rebase 共享 source，但不共享 coverage

禁止两套“差不多的工作记录 renderer”。

共享的唯一 source：

```text
XTrace
Y frames
frontier identity
既有 LWR planner / algebra
```

但必须分型消费：

```text
                 XTrace
                   │
          ┌────────┴────────┐
          │                 │
          ▼                 ▼
 Record/LWR world      Prefix world
          │                 │
    RecordCoverage      PrefixCoverage
          │                 │
      Y + RawGap          proven Y
          │                 │
          ▼                 ▼
 process review       Manager lag-1 rebase
```

Process Rk：

```text
frontier = ReviewFrontier(k) = Before(Tk)
materialize = canonical LWR(includeOpening=false)
coverage   = RecordCoverage
```

Manager lag-1 rebase at Tk：

```text
frontier = TodoRebaseCutoff(Tk) = Before(T(k-1))
materialize = CoverableRecordPrefix / proven Y prefix only
coverage   = PrefixCoverage
```

因此：

```text
Review 看得比 Manager compressed prefix 最多新一个 checkpoint
```

这是预期行为。

Review 要检查刚完成的阶段，且允许合法 RawGap；

Manager context 则保留最近一个 checkpoint interval 的 raw X，且**禁止**把 LWR RawGap 冒充可替换 Y prefix。

禁止：

```text
TodoProcessReviewEvidenceProjection
TodoCheckpointPrefixProjection 另造第二套 Y owner
用 lifecycleWorkRecord(session head) 代替 bounded range LWR
用 RecordCoverage 证明 prefix replacement
用 PrefixCoverage 填 LWR gap
```

实现时优先复用现有 Manager Life cursor-range LWR materialization 与 Finality 同 snapshot record-ready 模式；扩展既有 LWR API（例如 `range`），**不得复制一个第二 renderer**。

---

# 27. `messages.transform` 的 next-attempt admission

TodoWrite after 不需要强制等待 Y rebase materialization 完成。

它只 durable 地使新的 desired cutoff 可推导。

下一次真实 provider attempt：

```text
messages.transform
→ 计算 latest required Todo cutoff
→ 如果对应 Y 尚未 ready：
       await Journal/Y coverage
→ materialize
→ render
→ append TodoCheckpointPrefixRebaseCommitted
→ provider request
```

todowrite after 本身**不**写 PrefixRebaseCommitted；只让 desired cutoff 可从 Accepted 链推导。

因此绝无：

```text
TodoWrite 已 Accepted
→ 下一 model request 偷跑旧 prefix
或
尚未实际采用 projection
→ 却声称 rebase committed
```

---

# 28. Finality 前必须 drain 最后一个 Process Review

由于：

```text
每个 TodoWriteAccepted 都派生新的 Rk obligation
```

所以永远可能存在：

```text
latest Tk
→ Rk 尚未被 Tk+1 消费
```

绝不能要求：

```text
“再调用一次 todowrite flush”
```

因为那会创造 R(k+1)，无限后移。

唯一 tail drain：

```text
suicide
```

---

## 28.1 suicide 前序

```text
suicide
↓
find latest accepted TodoWrite
↓
if its Rk not yet ConsumableReview:
    ensureAssignment / ensureReview
    await ConsumableReview(Rk)
↓
settle latest checkpoint
```

如果：

```text
Rk = REVISE
```

则：

```text
canonical = semanticMerge(Ck,Pk)
return process review report
do NOT create FinalityRequest
Manager continues working
```

如果：

```text
Rk = PERFECT
```

则：

```text
canonical = Pk
continue Finality preconditions
```

---

# 29. 未完成 todo 交给 process review，不另造机械 Finality gate

用户要求的是过程 review、`reviewing` 门禁与终末 2N。

**本稿收回**上一版自行增加的：

```text
Finality Todo Completeness Gate
= Host 机械拒绝任何仍含 pending/in_progress/reviewing 的 suicide
```

正式语义改为：

```text
suicide
→ drain latest ConsumableReview

REVISE
→ 不进 Finality
→ 返回 ProcessReviewLWR
→ Life 继续

PERFECT
→ 按既有 Finality preconditions 继续
```

Todo 中是否还有“不该有的未完成工作”，由 process reviewer 的 PERFECT/REVISE 判断，而不是额外写一套与用户需求无关的机械真理。

这不削弱 `reviewing → completed` Host gate；那条仍是 Magic Todo 自身状态代数。

---

# 30. Dedicated Reviewer 加入终末 2N

现有终末 2N 定义是：

```text
N = 当前 FinalityRequest enlisted member 数

每个 member
→ fresh barrier
→ first causal PERFECT
→ challenge
→ second causal PERFECT

任何 REVISE
→ request immediately rejected
```

用户原始要求只是：

> dedicated reviewer 最终也会加入终末性 2N 评审团作为一员。

**本稿收回**“Dedicated 永不 graduate / 每个未 Blessed FinalityRequest 都强制回流”的过度推导。

正式保守默认：

```text
Dedicated reviewer
在首次进入 terminal Finality 时
作为普通 cohort member enlist

之后完全遵循既有 ordinary graduate 规则
```

因此当前 Finality roster 是：

```text
若 Dedicated 尚未 graduate：
    DedicatedTodoReviewer
+
所有 ordinary historical ungraduated reviewers
+
恰好一个 fresh ordinary reviewer
=
current Finality cohort
```

集合按 physical/session identity 去重后得到 N。

若 Dedicated 已在更早 FinalityRequest 中完成合法 dual-PERFECT 并 graduate：

```text
后续 FinalityRequest 不再因 Magic Todo 特例强制 enlist 它
```

这与现有 Finality 毕业语义一致；若产品要改成“每轮强制回流”，必须另开明确 Change。

---

## 30.1 Dedicated reviewer 进入 Finality 时仍要 fresh terminal proof

即使它刚刚 process PERFECT：

```text
process PERFECT
≠ terminal first PERFECT
```

当它被 enlist 进某个 FinalityRequest 时：

```text
same physical reviewer session/context 可以复用

BUT

fresh FinalityRequestId
fresh BarrierId
fresh GitTreeHash
fresh Authority Root
fresh dual-PERFECT chain
```

这与现有“旧上下文可以复用，但新 FinalityRequest 的因果证明不能复用”原则一致。

---

## 30.2 Dedicated Finality LWR 必须 request-range bounded

允许长期复用同一个 dedicated physical session 后，禁止把：

```text
R1 process
R2 process
R3 process
...
```

整段 history 塞进终末 Reviewer LWR。

Dedicated reviewer 在 Finality 中必须有：

```text
FinalityAssignmentStart
→ FinalityVerdictFrontier
```

的 bounded canonical LWR（`includeOpening=false`）。

同一 renderer，三个用途：

```text
Manager checkpoint input LWR   → bounded by Tk / ReviewFrontier(k)
Process reviewer report LWR    → bounded by Rk assignment range
Terminal reviewer record LWR   → bounded by current FinalityRequest
```

任何地方都不得偷拿 whole-session LWR。

---

## 30.3 Dedicated process-reviewer 资源生命周期 ≠ Finality graduation

ordinary Finality reviewer：

```text
可按既有规则在 Blessing 后释放
```

Dedicated **process** reviewer：

```text
即使已从 Finality roster graduate
仍必须继续服务 blessing 后的 todowrite process reviews
至少保留到 LifeCompleted
或存在明确 proven-loss replacement
```

资源生命周期拆开：

```text
Finality cohort membership
    → 可 graduate

Dedicated process-review duty / physical session retention
    → 直到 LifeCompleted
```

否则第二次 suicide 前的 process review 无人可做。

---

# 31. Finality REVISE / Blessing 后继续复用 Dedicated Process Reviewer

当任意 terminal reviewer REVISE：

```text
FinalityRejected
→ Manager receives correction evidence
→ Life continues
```

Dedicated process reviewer：

```text
不 Dispose
不丢 process history
恢复 / 继续 process-review duty
```

即使某次 Finality 中 Dedicated 已 dual-PERFECT 并 graduate：

```text
其 process-review physical session 仍不因 Blessing 立即释放
```

ordinary Finality cohort member 仍遵守现有 carryover / graduation / release 规则。

当前 2N 已要求 REVISE 立即关闭 request，不能等其它 reviewer。

这一性质不变。

---

# 32. Blessing 后的 Process Review

现有 Finality 语义是：

```text
all current cohort confirmed
→ FinalityBlessed
→ Manager 收到 work records
→ 继续修 minor issues
→ second suicide 不再跑 2N
```

Magic Todo 加入后：

Manager 在 blessing 后继续修 minor issues 时仍需持续 `todowrite`。

所以：

```text
second suicide
```

虽然：

```text
不创建新的 terminal 2N cohort
```

但仍必须：

```text
drain latest process review
```

如果：

```text
latest process review = REVISE
```

则：

```text
不得 rest in peace
→ 返回 report
→ Life 继续
```

只有：

```text
latest process review settled PERFECT
```

才走既有 blessed fast-path：

```text
rest in peace
Terminate the conversation now.
```

这样“第二次 suicide 不再终末评审”仍成立，但不会绕过最新过程质量门。

---

# 33. Manager Surface Security

更新后的允许 surface：

```text
todowrite description:
    checkpoint review exists

todowrite result:
    PERFECT / REVISE
    concrete report
    merge preview

pair-programming guidance:
    previous work may be reviewed concurrently
```

禁止 surface：

```text
DedicatedTodoReviewer
ReviewerSessionId
fast-reviewer
deep-reviewer
hidden task
barrier
witness
dual PERFECT mechanics
2N
historical reviewer roster
```

fork schema 继续完全没有 Reviewer。

隐藏 target 继续 generic unavailable。

---

# 34. Reviewer RequestKind 隔离

建议至少：

```fsharp
type ReviewerRequestKind =
    | TodoProcessReview of TodoWriteId
    | FinalityReview of FinalityRequestId * ReviewBarrierId
```

Process request：

```text
一次 PERFECT/REVISE 即 terminal
```

Finality request：

```text
REVISE immediate
PERFECT → challenge → second causal PERFECT
```

禁止同一个 review controller 用：

```text
if pendingChallenge then ...
```

猜当前到底是哪种业务。

RequestKind 必须来自 typed authority。

---

# 35. Crash Recovery

## 35.1 Prepared / Accepted / ConsumableReview 裂缝

### Prepared 已有，physical tool 未完成或失败

```text
不 Accepted
不派生 review obligation
下次 before 从 Journal canonical 覆盖 Host TodoTable
```

### Prepared + physical tool completed，Accepted 尚无

```text
ensure TodoWriteAccepted
→ 然后走 Accepted 恢复路径
```

### Accepted 已有，ConsumableReview 尚无

```text
derive pending process-review obligation from Accepted
→ ensure Dedicated reviewer
→ ensure TodoProcessReviewAssigned for same TodoReviewId
→ ensureReview(TodoWriteId)
→ if verdict durable but LWR not record-ready:
      await AgentJournal change
      re-read same snapshot semantics
```

不创建第二个 TodoWrite；不另开第二个 assignment range。

---

## 35.2 reviewer prompt 已发送，plugin crash

恢复：

```text
从 Journal/session physical facts
证明原 reviewer session/attempt

存在
→ resume/observe

永久丢失
→ Dedicated replacement protocol

不确定
→ fail closed
```

---

## 35.3 ReviewConcluded 已落，但下一 todowrite 尚未发生

无需写：

```text
CurrentTodoUpdated
```

下一调用或 suicide：

```text
fold conclusion
→ settle
```

---

## 35.4 Host executor 成功，但 Accepted 未落

Host TodoTable 可能已写 compatibility Pk。

若 Journal 仅有 `Prepared` 或连 `Prepared` 都因 before 未成功而没有：

```text
recovery 必须看 physical ToolPart + Prepared

Prepared + completed ToolPart
→ ensure Accepted

否则
→ canonical protocol 认为该 checkpoint 从未成功受理
```

下一次 MagicTodo before：

```text
从 Journal canonical truth 重建
→ 再次覆盖 compatibility sink
```

Host TodoTable 不拥有恢复权；“以后覆盖”必须建立在 Prepared/Accepted 协议上，而不是口头承诺。

---

## 35.5 TodoCheckpoint rebase 未 commit

下一 provider transform：

```text
derive desired cutoff from accepted checkpoints
→ materialize Y
→ retry ordinary projection
```

无：

```text
RebasePending Stage
```

---

## 35.6 Verdict 已落 / waiter 丢失 / LWR 尚未 record-ready

若 `TodoReviewConcluded` 与 `WorkRecordRef` 均已 durable 且同 snapshot 可证明：

```text
下一 before 直接消费 ConsumableReview
→ 不等待
```

若只有 verdict、canonical ProcessReviewLWR 尚未 record-ready：

```text
从 durable assignment + ReviewerRecordFrontier 重建同一个等待
→ await Journal change
→ 同 snapshot 再判 record-ready
```

禁止用 raw terminal / summary 临时顶替 `WorkRecordRef`。

---

# 36. V2 Runner Gate

当前宿主 V2 local settle 不执行 V1 plugin tool hook membrane。

所以正式生产 invariant：

```text
MagicTodo-enabled Manager Attempt
→ must use execution path with proven
   definition + before + after hooks
```

若 runner=V2 且 Host 未提供等价 contract：

```text
Attempt construction fail closed
```

不得：

```text
静默退化成裸 SessionTodo.update
```

未来 Host 原生提供 V2 hook parity 后，必须先跑同一套 contract canary，再取消限制。

---

# 37. Host Canary Suite — Phase 0 Blocking Gate

在写业务代码前先建立以下真实 OpenCode contract tests。

## Canary A — before mutation alias

证明：

```text
before mutation reaches executor
BUT
does not mutate durable pre-before ToolPart input
```

**FAIL = 停止 membrane 实现。**

---

## Canary B — definition schema

证明：

```text
同时替换 parameters + jsonSchema
→ provider 看到 V2
→ executor 仍运行 original V1 decoder
```

---

## Canary C — unknown `id`

证明：

```text
before 剥除 id 后
→ original decoder succeeds
```

---

## Canary D — reviewing sink

分别验证：

```text
status="reviewing"
→ TodoTable
→ todo.updated
→ API
→ UI/TUI
```

结果决定：

```text
reviewing passthrough
or
reviewing→in_progress compatibility projection
```

---

## Canary E — after output history

证明：

```text
after 改写 output.output
→ 本次 model 看见
→ 下一 provider history 仍看到同字节 result
```

---

## Canary F — after failure path

证明 execute throw 时：

```text
after 是否运行
```

协议不依赖它运行，但测试必须冻结实际行为，防止未来误用。

---

# 38. Domain 实现建议

以下文件名是目标模块建议；本附件没有完整 `src/**` 树，因此实现者应按仓库现有命名放入对应 Domain/Application/Infrastructure 层，**不可因为路径不同改变 ownership**。

建议模块：

```text
Domain/
  TodoCheckpoint.fs
  TodoIdentity.fs
  TodoMerge.fs
  TodoReview.fs
  TodoPrompt.fs

Application/
  TodoCheckpointProgram.fs
  TodoProcessReviewProgram.fs

Infrastructure/OpenCode/
  TodoWriteDefinitionHook.fs
  TodoWriteBeforeHook.fs
  TodoWriteAfterHook.fs
  TodoCheckpointBridge.fs
  TodoCheckpointProjection.fs
  DedicatedTodoReviewerRuntime.fs
```

`TodoCheckpointProjection.fs` 只拥有 todo-list / checkpoint settlement projection，**不是**工作记录 renderer。

Process-review evidence 与 report 必须调用既有 LWR / `lifecycleWorkRecord` range API；禁止在此树新增平行 work-record module。

Journal fact 进入现有 durable fact owner，而不是另造 JSON 状态文件。

---

# 39. F# CE 结构：禁止退化成一阶大状态机

不要写：

```fsharp
match state with
| WaitingReview -> ...
| Settling -> ...
| Submitting -> ...
| StartingReview -> ...
| WaitingRebase -> ...
```

推荐：

```fsharp
let prepareTodoWrite ports input =
    task {
        let! life =
            ports.requireLife input.SessionId

        do! admitSingleCheckpointOrFail
                ports
                life
                input.ToolCallId
                input.ToolPartOrdinal

        let! settled =
            settlePrevious ports life // await ConsumableReview(k-1)

        let! proposed =
            normalizeTaggedAndValidate
                ports
                life
                settled.Current
                input

        let preview =
            TodoMerge.revisePreview
                settled.Current
                proposed

        do! appendPrepared ports life input proposed

        return
            { Settled = settled
              Proposed = proposed
              Preview = preview }
    }
```

Review launch / consume：

```fsharp
let materializeManagerCheckpointLwr ports checkpoint =
    // RecordCoverage LWR; RawGap allowed; includeOpening=false
    // range: current Life opening .. frozen ReviewFrontier(k)
    ports.lifecycleWorkRecordRange
        checkpoint.ManagerSessionId
        checkpoint.LifeOpeningCursor
        checkpoint.ReviewFrontier

let rec awaitConsumableReview ports checkpoint =
    task {
        let! snap =
            ports.readJournalSnapshot checkpoint

        match snap.tryConsumableReview checkpoint.TodoReviewId with
        | Some consumable ->
            // verdict + WorkRecordRef from SAME snapshot
            return consumable

        | None ->
            do! ports.ensureProcessReviewAssigned checkpoint

            match snap.tryVerdict checkpoint.TodoReviewId with
            | None ->
                let! evidence =
                    materializeManagerCheckpointLwr ports checkpoint

                do!
                    ports.ensureReviewerPrompt
                        checkpoint
                        evidence

            | Some _ ->
                // VerdictKnown but ProcessReviewLWR not record-ready yet
                ()

            return!
                ports.awaitJournalChange checkpoint
                |> Task.bind (fun _ ->
                    awaitConsumableReview ports checkpoint)
    }
```

递归表达：

```text
“ConsumableReview 尚未出现 → 等 Journal 变化 → 同 snapshot 重读普通程序”
```

而不是持久化程序位置；也不是 timer/sleep polling。

---

# 40. Manager Finality CE 修改点

`suicide` 前增加：

```fsharp
let! todo =
    TodoCheckpointProgram.drainLatestProcessReview life

match todo with
| NeedsRevision report ->
    return processRevisionResult report

| Settled canonical ->
    return! existingFinalityWorkflow canonical
```

Blessed fast path也先走：

```text
drainLatestProcessReview
```

然后才：

```text
rest in peace
```

---

# 41. One-stroke Manager Unhappy Path

必须增加一个完整 e2e：

```text
magic_todo_manager_unhappy_path_one_stroke
```

不要把所有坎坷拆成几十个互不关联 happy/unit case。

主剧情建议如下。

---

## Stroke 1 — 无 Activation，直接工作

```text
HumanRoot
→ LifeOpened
→ Manager 立即执行真实工作
```

断言：

```text
0 ManagerWorkActivation
0 new WorkActivated
```

---

## Stroke 2 — T1 创建第一个 checkpoint

```text
T1:
new A pending
```

断言：

```text
TodoWritePrepared #1
TodoWriteAccepted #1
Dedicated reviewer exactly once
Review1 obligation exactly once
```

---

## Stroke 3 — R1 仍在跑，Manager 继续下一 lane；同 turn 双 todowrite fail closed

```text
R1 pending

Manager:
fork / read / edit / test...
```

另测同一 provider turn 两个不同 ToolCallId todowrite：

```text
ordinal-first admitted
second fail closed
Accepted count += 1 only
```

断言：

```text
Manager 未因 review pending 被停住
concurrent second todowrite 不产生第二 checkpoint
```

---

## Stroke 4 — T2 时 R1 尚未 ConsumableReview

```text
T2 enters before
→ blocks
```

随后 reviewer：

```text
R1 REVISE
+ ProcessReviewLWR(R1) record-ready
```

T2 继续。

断言：

```text
previous report = ProcessReviewLWR(R1)
C2 = merge(C1,P1)
VerdictKnown alone without record-ready LWR still blocks
```

---

## Stroke 5 — merge 重新拉低 progress

例如：

```text
C1.A = in_progress
P1.A = reviewing

R1 = REVISE

C2.A = in_progress
```

Manager 在 T2 试：

```text
A = completed
```

断言：

```text
before rejects
executor call count unchanged
TodoWriteAccepted count unchanged
Review obligation count unchanged
```

---

## Stroke 6 — 合法进入 reviewing

T2 retry：

```text
A = reviewing
```

accepted → R2。

---

## Stroke 7 — 下一工作与 R2 并行

Manager 同时执行 B。

R2 PERFECT。

---

## Stroke 8 — T3 完成 A

T3：

```text
consume R2 PERFECT
C3.A = reviewing

propose A.completed
```

合法。

启动 R3。

---

## Stroke 9 — lag-1 prefix proof

分别抓 provider wire：

```text
after T1
after T2
after T3
```

证明：

```text
Opening bytes identical

T2 projection:
Y only through before T1
raw tail starts at T1

T3 projection:
Y only through before T2
raw tail starts at T2
```

---

## Stroke 10 — suicide 遇到 pending R3

```text
suicide
→ waits R3
```

R3 REVISE：

```text
→ no FinalityRequest
→ Manager receives report
→ canonical merge applied
```

---

## Stroke 11 — 修复后最终 Todo 完结

经过 reviewing → completed 的合法 checkpoint 链。

最后 process review PERFECT。

---

## Stroke 12 — first Finality cohort

当前存在：

```text
Dedicated D（尚未 graduate）
old ungraduated Rold
new Rnew
```

断言：

```text
N = 3
require 6 causal PERFECT
```

让：

```text
D P/P
Rold P/P
Rnew REVISE
```

断言：

```text
FinalityRejected immediately
不等待其它 terminal
D physical session retained for process review
D 因合法 dual-PERFECT 可 graduate（不再因 Magic Todo 特例强制回流）
```

---

## Stroke 13 — Manager 修改工作

继续：

```text
todowrite
process review
work
todowrite
```

证明 Dedicated **process** role 仍在，即使它已从 Finality roster graduate。

---

## Stroke 14 — second Finality request

ordinary graduated members（含已 graduate 的 D）按旧规则退出。

新 cohort 由剩余 ungraduated + fresh ordinary 组成。

Dedicated process session 仍存活服务 todowrite。

最终 all confirmed：

```text
FinalityBlessed
Life still open
Dedicated process reviewer still retained until LifeCompleted
```

---

## Stroke 15 — minor polish 后 process REVISE

Manager 处理 finality work record minor issue，调用 todowrite。

第二次 suicide：

```text
drain process review
→ REVISE
```

断言：

```text
NOT rest in peace
NOT new 2N
Life continues
```

---

## Stroke 16 — 最终 Rest

修完：

```text
reviewing
→ completed
→ process PERFECT
```

再次 suicide：

```text
drain latest process review PERFECT
no new Finality cohort
rest in peace
Terminate the conversation now.
```

LifeCompleted exactly once；此后才允许释放 Dedicated process reviewer。

---

# 42. Unit / Property Tests

必须至少覆盖：

### Todo identity

```text
kind existing/new is structurally required
new item gets deterministic id
existing id preserved
existing without id rejected
new with id rejected
unknown existing id rejected
duplicate id rejected
reordering does not change identity
content edit does not change identity
```

### Transition

```text
pending→completed rejected
in_progress→completed rejected
new→completed rejected
reviewing→completed accepted
completed→completed accepted
```

### Merge

```text
union
old-only preserved
new-only added
same-id status=min
proposed content wins
proposed priority wins
PERFECT exact replace
cancelled conservative merge keeps old.status on unilateral cancelled conflict
no auto-cancel / no auto-resurrect without PERFECT
```

### Review cadence / admission

```text
Accepted count == process review obligation count
rejected / non-admitted physical tool call creates no review
same ToolCallId replay creates no second review
different ToolCallId same list creates new review
same-turn second todowrite fail closed by ToolPart ordinal
Tk consumes only R(k-1)
Tk never waits Rk
Tk+1 waits until ConsumableReview(k)
VerdictKnown alone is insufficient without record-ready LWR
Prepared + completed ToolPart recovers to Accepted
Prepared + failed ToolPart never Accepts
```

### Dedicated reviewer

```text
one logical reviewer per Life
same session preferred
replacement only on proven permanent loss
process review input = OpeningRaw + bounded Manager LWR + Ck + Pk
process PERFECT not terminal witness
first Finality enlist then ordinary graduate
process session retained until LifeCompleted
Finality LWR bounded by FinalityAssignmentStart..VerdictFrontier
```

### Rebase / coverage split / Opening floor

```text
Opening byte-identical forever
WorkRecordStart excludes Opening from Blogger Y
T1 no prior replacement
desired cutoff derived from Accepted chain without Requested fact
PrefixRebaseCommitted only after real provider adoption
Tk cutoff == before T(k-1)
latest interval remains raw X
restart reproduces same projection
LWR RawGap may enter process-review evidence
LWR RawGap never enters prefix replacement
RecordCoverage must not imply PrefixCoverage
```

### LWR frontiers

```text
ManagerCheckpointLWR(k) never crosses ReviewFrontier(k)
concurrent Manager work after Tk does not leak into Rk evidence
ProcessReviewLWR(k) excludes R(k-1) history
dedicated reviewer head LWR is not used as report
OpeningRaw is separate; LWR includeOpening=false
Rk may start while Manager Y lags frontier (RawGap present)
```

### Finality

```text
suicide drains latest process review
process REVISE prevents FinalityRequest
no mechanical terminal-todo completeness gate
D joins first eligible Finality as ordinary member
D needs fresh dual PERFECT when enlisted
ordinary graduation unchanged for D after first dual-PERFECT
blessed second suicide does not create 2N
blessed second suicide still drains process review
```

---

# 43. Static Governance Gates

新增永久静态门禁。

## 43.1 No Activation owner

生产源码不得再新增/引用：

```text
ManagerWorkActivation
PlanningTail
WorkActivated
ProtectedPrefixEnd
```

作为新 Manager 业务决策。

Legacy decoder/migration 白名单除外。

---

## 43.2 No Todo program counter

禁止：

```text
TodoStage
ReviewStage
AwaitingTodoReview
NeedTodoRebase
NextTodoAction
```

及等价 mutable/persisted PC。

---

## 43.3 One merge owner

`semanticMerge` 唯一 owner。

禁止：

```text
before 自己一版 merge
tool result preview 一版 merge
Finality drain 一版 merge
test helper 又一版 merge
```

---

## 43.4 One schema owner

MagicTodo V2：

```text
tool definition
decoder
examples
result renderer
```

从同一 schema/codec module 派生。

---

## 43.5 Manager hidden-review surface

允许：

```text
checkpoint review
PERFECT
REVISE
report
```

仅限 MagicTodo process protocol。

禁止：

```text
reviewer
reviewer session
barrier
witness
2N
confirmation rounds
```

---

## 43.6 V2 bypass gate

若 Manager Attempt 使用未证明 MagicTodo hook parity 的 runner：

```text
build/check 必须红
```

---

## 43.7 One work-record renderer

禁止新增：

```text
TodoProcessReviewEvidenceProjection
Y-complete reviewer projection
独立 ReportRef summarizer
```

Process review input、process review report、Finality reviewer work record 一律复用既有 canonical LWR machinery；仅 range / includeOpening / coverage 分型不同。

---

## 43.8 Coverage type split

静态门禁应拒绝：

```text
用 RecordCoverage / LWR RawGap 做 prefix replacement
用 PrefixCoverage 计算 LWR gap
用 session head LWR 代替 frozen ReviewFrontier / request-range / Finality-range LWR
```

---

## 43.9 Tagged identity + single admission

静态/合同门禁应拒绝：

```text
id?: string optional-id schema 回流
同 turn 多 todowrite 抢锁 admission
相同 list 去重跳过新 ToolCallId 的 review
```

---

## 43.10 Opening floor without Activation

删除 Activation 后，生产路径必须仍能证明：

```text
WorkRecordStart
→ Blogger / Y 不吞 Opening
```

禁止把 Opening protection 绑回 `WorkActivated`。

---

## 43.11 Desired rebase ≠ committed rebase

禁止：

```text
Accepted 后立刻写 PrefixRebaseCommitted
NeedRebase / RebaseRequested Stage
```

---

# 44. Docs 修改

至少同步：

```text
docs/what/glory.md
docs/shape/glory.md
docs/how/glory.md
docs/proof/glory.md

docs/what/host.md
docs/shape/host.md
docs/how/host.md
docs/proof/host.md

docs/what/context.md
docs/shape/context.md
docs/how/context.md
docs/proof/context.md

docs/what/review.md
docs/shape/review.md
docs/how/review.md
docs/proof/review.md

docs/what/prompt.md
docs/what/projection.md
docs/shape/architecture.md
```

建议新增：

```text
docs/{what,why,shape,how,proof}/todo.md
```

由 `TODO-*` 条款拥有 Magic Todo 自身语义，GLORY / REVIEW / CONTEXT 只交叉引用，避免五处复制。

特别写清：

```text
Process review → RecordCoverage / bounded LWR
Prefix rebase → PrefixCoverage / proven Y only
ConsumableReview → verdict + record-ready Reviewer LWR
TodoWritePrepared / TodoWriteAccepted crash protocol
WorkRecordStart Opening floor
tagged existing/new identity
single-admission ToolPart ordinal rule
Dedicated process retention until LifeCompleted
Dedicated Finality enlist then ordinary graduate
```

---

# 45. Legacy Journal Migration

## Existing completed Life

```text
保持 completed
```

不回放 Magic Todo。

---

## Existing open Life + WorkActivated

升级后：

```text
LifeOpened 仍有效
WorkActivated 只作为 legacy inert fact
不再影响 eligibility
Opening floor 改由 WorkRecordStart 承担
```

历史 planning/activation provider bytes保持原样，不改写历史。

---

## Existing open Life 尚未 WorkActivated

升级后：

```text
直接视为正常 active single-stage Life
```

禁止继续发送 Activation continuation。

下一真实 provider round 开始收到新的 Manager-only Todo pair guidance。

---

## 正常新 Life vs legacy seed

Host todo store 是 session 级，而 Manager 可以同 session 多 Life。

因此必须分开：

```text
正常新 Life
→ MagicTodo canonical 初始为空
→ 绝不从 Host TodoTable 自动 adopt 上一 Life 的旧 todo

只有升级瞬间已经存在的 legacy open Life
→ 允许一次 LegacyTodoSeedAdopted
```

---

## Existing Host TodoTable without stable ids（仅 legacy open Life）

不能把：

```text
position
content
```

猜成 durable identity。

对该 legacy open Life 的第一轮 Magic Todo checkpoint：

```text
Host old TodoTable
→ 作为 legacy seed list
→ Host 分配全新 Magic Todo ids
→ tool result 将 IDs 正式交给 Manager
→ append LegacyTodoSeedAdopted
```

该 adoption checkpoint 后：

```text
只认 Magic Todo ids
```

同 session 后续新 Life：

```text
canonical 从空开始
禁止再次从 Host TodoTable 反推 identity
```

---

# 46. 实施顺序

## Phase 0 — Host contract canaries

先写真实 OpenCode 测试：

```text
alias
definition
decoder
reviewing UI
after history
error path
```

**任何 blocking canary 未证明前，不写 production membrane。**

Host alias canary 仍是 P0 blocker：必须证明 before 原地 mutation 不会污染 durable ToolPart.input。

---

## Phase 1 — Domain algebra

实现并测试：

```text
tagged ExistingTodo / NewTodo codec
TodoItemId
status transition
semanticMerge（含 cancelled 保守语义 + content/priority 裁决）
settlement
single-admission ordinal rule
checkpoint projection
```

全部纯函数先绿。

---

## Phase 2 — Durable facts + projection

加入：

```text
TodoWritePrepared（含冻结 ReviewFrontier）
TodoWriteAccepted
TodoProcessReviewAssigned
TodoReviewConcluded（WorkRecordRef / WorkRecordDigest）
DedicatedTodoReviewerEnlisted
DedicatedTodoReviewerReplaced
TodoCheckpointPrefixRebaseCommitted
LegacyTodoSeedAdopted（仅升级路径）
```

Boot Fold / crash tests先绿。

---

## Phase 3 — Process reviewer via bounded LWR

实现：

```text
ensureDedicatedReviewer
bounded ManagerCheckpointLWR(includeOpening=false)
ensure TodoProcessReviewAssigned
ensureReview
ConsumableReview / same-snapshot record-ready
bounded ProcessReviewLWR as WorkRecordRef
replacement recovery
```

**不得**新增 `TodoProcessReviewEvidenceProjection`。

优先扩展既有 `lifecycleWorkRecord` / LWR planner 的 range 能力，禁止复制第二 renderer。

此时尚不改 Manager prompt/lifecycle。

---

## Phase 4 — V1 todowrite membrane

接：

```text
definition
before
hidden-property carrier
original executor
after
enriched result
```

验证 Host TodoTable 只是 sink。

---

## Phase 5 — Y checkpoint rebase

实现：

```text
desired cutoff derivation
PrefixCoverage-only Y materialization
messages.transform
commit proof
restart replay
```

明确 fail closed：LWR RawGap 不得进入 rebase。

---

## Phase 6 — Finality integration

接：

```text
suicide tail drain
dedicated first-enlist then ordinary graduate
bounded Finality LWR for dedicated session
Dedicated process retention until LifeCompleted
blessed second-suicide process drain
```

---

## Phase 7 — Atomic Manager cutover

**同一个 production cutover 中**完成：

```text
删除 planning-only activation workflow
停止生产 WorkActivated
开启 Magic Todo schema
开启 process review
开启 pair-programming todo guidance
开启 Finality todo drain
```

禁止拆成：

```text
commit A: 先删 Activation
commit B: 以后再加 Magic Todo
```

A 单独上线就是非法中间态。

---

## Phase 8 — Migration / unhappy path / static gates / docs

最后：

```text
legacy journals
one-stroke e2e
all property tests
architecture gates
formal docs
full canary
```

通过才可标 Completed。

V2 runner parity 仍然 fail closed：没有等价 hook contract 前，MagicTodo Manager Attempt 不得使用 V2 todowrite path。

---

# 47. Release Gate

本 Change 只有以下全部成立才允许 Completed：

1. 新 Manager 不再产生 `ManagerWorkActivation` / 新 `WorkActivated`。
2. Manager 从 LifeOpened 后即可正常工作。
3. Opening 由 `WorkRecordStart` 保护，不被 Blogger/Y 吞掉。
4. MagicTodo guidance 是 Manager-only fragment，不污染其它角色。
5. provider-visible todo 使用 `kind:"existing"|"new"` tagged union。
6. 仅 `kind:"new"` 由 Host 分配 id；`kind:"existing"` 必须带已知 id。
7. reviewing 是正式状态；pending/in_progress/new→completed 被 Host 拒绝。
8. 每个 `TodoWriteAccepted` 恰好一个 process-review obligation。
9. rejected / 非 admission 的 todowrite 不创建 review。
10. 同 turn 多 todowrite 按 ToolPart ordinal 只 admit 第一个，其余 fail closed。
11. 相同 ToolCallId replay 幂等；不同 ToolCallId 即使 list 相同也新开 checkpoint。
12. Tk 消费 R(k-1)；Tk 不等待 Rk；Tk+1 等待 ConsumableReview(k)。
13. REVISE = union + progress min + content/priority=proposed 裁决；unilateral cancelled 保留 old.status。
14. PERFECT = proposed exact replace。
15. tool result 返回上一 review 的 ProcessReviewLWR，以及当前 REVISE preview / PERFECT 规则。
16. process review 期间 Manager 可继续独立工作。
17. 每 Life 恰好一个 logical dedicated process reviewer，并保留到 LifeCompleted。
18. reviewer input = OpeningRaw + frontier-bounded Manager LWR(`includeOpening=false`) + old/new todo。
19. process-review LWR 允许 RawGap；不得等 Manager Y 追平才启动。
20. ProcessReviewLWR / Finality dedicated LWR 都不得用 session head。
21. desired lag-1 cutoff 由 Accepted 链推导；PrefixRebaseCommitted 仅在真实 provider adoption 后写入。
22. T2/T3... 只替换 proven Y prefix；LWR RawGap 不得进入 prefix replacement。
23. Opening 永远 raw、byte-stable，且不经 LWR 重复。
24. Prepared + completed ToolPart 可恢复为 Accepted；Prepared + failed 永不 Accepted。
25. suicide drain latest ConsumableReview；process REVISE 不得进入 Finality。
26. 无机械 Finality terminal-todo completeness gate。
27. Dedicated 首次进入 Finality 后遵循 ordinary graduate；不强制每轮回流。
28. process PERFECT 不计 terminal PERFECT；enlisted 时仍需 fresh dual-PERFECT。
29. FinalityBlessed 后仍可 todowrite；Dedicated process session 不因 Blessing 释放。
30. second suicide 不创建新 2N，但仍 drain process review。
31. 正常新 Life canonical todo 为空；仅 legacy open Life 一次 seed adopt。
32. Manager 看不到 dedicated reviewer / barrier / witness / 2N。
33. Host TodoTable 不参与 canonical recovery；bridge 只是 ephemeral。
34. alias canary 与 V2 runner fail-closed 成立。
35. 无独立 process-review work-record projection；Record/Prefix coverage 分型成立。
36. one-stroke unhappy path / docs / static gates / 全量 check 通过。

---

# 48. 审阅时请重点攻击的五个问题

本稿已经给出默认裁决，Reviewer 如果反对应直接针对这些点给出反例：

### A. Dedicated reviewer 是否应每轮 Finality 都 mandatory？

本稿：**否。** 只保证它最终能作为一员进入终末 2N；首次 enlist 后走 ordinary graduate。

但其 **process-review session** 必须保留到 LifeCompleted。

### B. cancelled 在 REVISE merge 中如何处理？

本稿：**任一 side cancelled 且 status 冲突时保留 old.status。**

未经 PERFECT：不自动取消，也不自动复活。

### C. same-id 的 content/priority 谁赢？

本稿：**REVISE 时 proposed 赢；真正迟滞的是 status。** 这是明确协议裁决。

### D. Host TodoTable 应显示 canonical Ck 还是 working Pk？

本稿：**显示 working Pk compatibility projection；canonical 只由 MagicTodo Journal 拥有。**

### E. lag-1 rebase 是否可以只用 plugin Journal + messages.transform？

本稿：**可以，但 desired cutoff ≠ committed proof；且只消费 PrefixCoverage 可证明的 Y prefix。**

### F. V2 runner / before alias 怎么办？

本稿：**没有 hook parity / alias canary 就禁止上线。**

### G. 为什么 process review 复用 LWR 而不是纯 Y？

本稿：**LWR 是不丢证据的正式表示；但必须 frontier/request-range bounded，且不得做 prefix replacement。**

### H. 要不要 Finality mechanical terminal-todo gate？

本稿：**不要。** 未完成工作真实性交给 process reviewer PERFECT/REVISE。

---

# 49. Final Protocol in One Picture

```text
HumanRoot
│
├─ LifeOpened
│    └─ WorkRecordStart = exclusive end of Opening
│
├─ immediate real work
│    └─ Blogger effectiveStart = max(RecordCoverage, WorkRecordStart)
│
├─ T1 todowrite
│    ├─ admit by ToolPart ordinal（同 turn 其它 todowrite fail closed）
│    ├─ decode kind existing/new
│    ├─ transition gate
│    ├─ TodoWritePrepared（freeze ReviewFrontier）
│    ├─ Host executor
│    ├─ TodoWriteAccepted
│    ├─ enlist Dedicated D
│    ├─ ensureReview(R1) obligation
│    └─ desired cutoff becomes derivable（尚未 committed）
│
├──────── Manager work ────────┐
│                              │
│                         D reviews R1
│                         (verdict → later record-ready LWR)
│                              │
├─ T2 todowrite                │
│    ├─ if R1 not ConsumableReview ──┘ wait
│    ├─ settle:
│    │    PERFECT → P1
│    │    REVISE  → merge(C1,P1)
│    ├─ return R1 ProcessReviewLWR
│    ├─ Prepared/Accepted T2
│    ├─ ensureReview(R2)
│    └─ next provider prefix（采用后才 PrefixRebaseCommitted）:
│         Opening
│         + proven Y(before T1)
│         + raw X[T1..T2]
│
├──────── Manager work ───────── D reviews R2
│
├─ T3 ...
│
├─ suicide
│    ├─ drain latest ConsumableReview
│    ├─ REVISE → continue Life
│    └─ PERFECT → existing Finality preconditions
│          └─ Finality cohort:
│               Dedicated D（若尚未 graduate）
│               + ordinary ungraduated
│               + fresh ordinary
│
│              enlisted members fresh P/P
│              any REVISE immediately rejects
│              D Finality LWR is request-range bounded
│
├─ FinalityBlessed
│    └─ ordinary Finality resources may release
│       BUT Dedicated process reviewer retained
│
├─ minor polish + Magic Todo checkpoints / process reviews
│
└─ second suicide
     ├─ drain latest process review
     ├─ REVISE → continue
     └─ PERFECT
          → LifeCompleted
          → rest in peace
          → then Dedicated process reviewer may release
```

---

# 50. 总结

这个 Change 的核心不是“给 todowrite 增加一个 reviewing 枚举”。

真正的新协议是：

> **把 todowrite 提升为 Manager 整个工作生命周期的因果 checkpoint。**

它同时成为：

```text
计划真实性边界
+
状态迁移边界
+
过程 review 节拍器
+
迟滞同步边界
+
主动 Y 小压缩边界
+
Finality 前最后过程质量门
```

六源 SSOT：

```text
LifeOpened + WorkRecordStart
    = Manager Life / Opening 边界

TodoWriteAccepted
    = checkpoint + review obligation

MagicTodoProjection
    = canonical todo

bounded canonical LWR
    = 工作 / process report / Finality record 证据

PrefixCoverage
    = lag-1 X→Y replacement 证明

existing Finality witness/cohort
    = 终末性质量证明
```

而仍然保持：

```text
真实世界 = durable facts（Prepared/Accepted/Concluded/...）
程序流程 = recursive CE
Host TodoTable = compatibility projection
隐藏 bridge = ephemeral transport detail
Manager = 不知道 reviewer 身份
Process review = 一次 verdict
Terminal review = fresh 2N dual-PERFECT（无 dedicated 永不 graduate 特例）
不发明第五种工作记录
不发明机械 terminal-todo Finality gate
```

这使原来的：

```text
Planning stage
Activation stage
Labor stage
Finality stage
```

不再继续横向扩张成：

```text
Planning
Activation
Todo
Review
Compression
Finality
```

而收敛成：

```text
LifeOpened
→ 工作
→ checkpoint
→ 工作
→ checkpoint
→ ...
→ Finality
```

控制点来自**真正发生的工具事实**，不是人为维护的程序阶段。

这应当成为本 Change 最重要的架构验收标准。
